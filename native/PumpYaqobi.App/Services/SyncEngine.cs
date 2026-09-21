using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>چراغِ همگام‌سازی — همان چهار رنگِ بندِ ۱۳ی پرامپت.</summary>
public enum SyncLight
{
    /// <summary>خاکستری — هنوز بند نشده‌ایم یا هنوز چیزی نپرسیده‌ایم.</summary>
    Idle,

    /// <summary>سبز — صف خالی است و آخرین رفت‌وآمد درست بود.</summary>
    Synced,

    /// <summary>زرد — چیزی در صف مانده (یا آفلاینیم و منتظر).</summary>
    Queued,

    /// <summary>سرخ — سرور صریح خطا داد.</summary>
    Failed,
}

/// <summary>
/// ══ موتورِ همگام‌سازی — بندِ ۳ی پرامپتِ ۲۲ ═══════════════════════════════
///
/// یک حلقهٔ پس‌زمینه، و بس:
/// <code>
///   تغییرِ کاربر ──(۵۰۰ms مکث)──▶ push دسته‌ای (۲۰۰تایی)
///   هر ۳۰ ثانیه، یا پیامِ «changed» از WSS ──▶ pull با cursor
///   نشد ⇒ ۲ · ۴ · ۸ · ۱۶ … تا پنج دقیقه، بی‌نهایت تلاش
/// </code>
///
/// ── قاعده‌هایی که نباید بشکنند ─────────────────────────────────────────
///
/// ⛔ <b>هیچ opی گم نمی‌شود.</b> صف در خودِ SQLite است، پس بسته شدنِ
/// برنامه — یا رفتنِ برق — چیزی را نمی‌برد و اجرای بعدی از همان‌جا ادامه
/// می‌دهد. سه روز آفلاین هم همین است.
///
/// ⛔ <b>۴۲۶ یعنی «نگه دار»، نه «دور بریز».</b> نسخهٔ برنامه از سرور جلوتر
/// است و سرور <b>هیچ چیزی</b> ننشانده؛ opها سرِ جا می‌مانند تا سرور
/// به‌روز شود. چراغ زرد می‌ماند و دلیلش در <c>ToolTip</c> است.
///
/// ⛔ <b>بخشی که کاری ندارد هیچ دستوری به دیتابیس نمی‌زند.</b> حلقه با
/// <see cref="PumpDbContext.Version"/> ترمز می‌گیرد: تا داده عوض نشده و
/// صف خالی باشد، حتی یک <c>SELECT</c> هم نمی‌زند — همان قاعدهٔ سنجهٔ
/// <c>idle</c>.
///
/// ⚠️ <b>اشتراک جلوی همگام‌سازی را نمی‌گیرد.</b> دادهٔ کاربر گروگان نیست؛
/// آن‌چه با پایانِ اشتراک بسته می‌شود <b>نوشتنِ تازه</b> است
/// (<see cref="SoftLock"/>)، نه رساندنِ چیزی که از قبل نوشته شده.
/// </summary>
public sealed class SyncEngine : IAsyncDisposable
{
    /// <summary>مکثِ پس از هر تغییر — بندِ ۳: «Debounce ۵۰۰ms».</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    /// <summary>اگر WSS نبود، هر این‌قدر یک بار pull — بندِ ۲۰٫۳.</summary>
    public static readonly TimeSpan PullTick = TimeSpan.FromSeconds(30);

    /// <summary>پیش از اولین کار — تا ورود و صفحهٔ اول بی رقیب بمانند.</summary>
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(15);

    /// <summary>تپشِ اشتراک و اعلان — بندِ ۲۰٫۷: «هر ۱۵ دقیقه، اگر آنلاین».</summary>
    public static readonly TimeSpan HeartbeatTick = TimeSpan.FromMinutes(15);

    /// <summary>سقفِ عقب‌نشینی — بندِ ۲۰٫۲: «۲، ۴، ۸، ۱۶… تا پنج دقیقه».</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    /// <summary>
    /// خاموشِ صریح — برای سنجه‌ها و ابزارِ عکس‌گیری.
    ///
    /// ⚠️ سنجهٔ <c>idle</c> می‌پرسد «بخشی که تویش نیستم چه مصرفی دارد»،
    /// نه «کلِ برنامه در سه ثانیه چه می‌کند». حلقهٔ همگام‌سازی کارِ خودِ
    /// برنامه است و هزینه‌اش جای دیگری سنجیده می‌شود — همان دلیلی که
    /// <c>StationPublisher</c> هم آن‌جا خاموش می‌شود.
    /// ⛔ در برنامهٔ واقعی هیچ‌جا نوشته نمی‌شود.
    /// </summary>
    public static bool Disabled { get; set; }

    private readonly SyncStore _store;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private CancellationTokenSource? _loop;
    private Task? _live;

    private long _lastVersion = -1;
    private int _fails;
    private DateTime _lastPull = DateTime.MinValue;
    private DateTime _lastBeat = DateTime.MinValue;
    private readonly HashSet<string> _toldNotices = new(StringComparer.Ordinal);

    public SyncEngine(AppHost host) => _store = new SyncStore(host.Db);

    // ── آن‌چه بیرون می‌بیند ────────────────────────────────────────────

    /// <summary>حالِ چراغ.</summary>
    public SyncLight Light { get; private set; } = SyncLight.Idle;

    /// <summary>چرا — یک جملهٔ آماده. ⛔ هیچ نام و نشانیِ سروری در آن نیست.</summary>
    public string Reason { get; private set; } = "همگام‌سازی هنوز شروع نشده";

    /// <summary>چند تغییر هنوز نرفته.</summary>
    public int Queued { get; private set; }

    /// <summary>آخرین باری که سرور واقعاً چیزی را پذیرفت.</summary>
    public DateTime? LastOkAt { get; private set; }

    /// <summary>آخرین خطا — برای صفحهٔ تنظیمات.</summary>
    public string LastError { get; private set; } = "";

    /// <summary>هر بار که یکی از بالایی‌ها عوض شد.</summary>
    public event Action? Changed;

    /// <summary>اعلانِ تازه‌ای رسید (از راهِ تپش یا WSS).</summary>
    public event Action<CloudNotice>? NoticeArrived;

    // ── راه انداختن ────────────────────────────────────────────────────

    /// <summary>حلقه را روشن می‌کند. دو بار صدا زدنش یکی بیشتر نمی‌سازد.</summary>
    public void Start()
    {
        if (_loop is not null || Disabled) return;
        var cts = new CancellationTokenSource();
        _loop = cts;
        //  هر ذخیرهٔ دیتابیس ⇒ یک بیدارباش. مکثِ نیم‌ثانیه‌ای در خودِ حلقه است.
        PumpDbContext.Saved += Nudge;
        _ = Task.Run(() => LoopAsync(cts.Token), cts.Token);
    }

    /// <summary>
    /// «همین حالا نگاه کن» — از هر ذخیرهٔ دیتابیس صدا می‌خورد.
    ///
    /// ⚠️ خودش هیچ کاری نمی‌کند جز بیدار کردنِ حلقه؛ مکثِ نیم‌ثانیه‌ای
    /// همان‌جا اعمال می‌شود. پس صد ذخیرهٔ پشتِ سرِ هم یک push می‌شود، نه صد تا.
    /// </summary>
    public void Nudge()
    {
        try { if (_wake.CurrentCount == 0) _wake.Release(); }
        catch (SemaphoreFullException) { /* از پیش بیدار است */ }
    }

    /// <summary>دکمهٔ «الان همگام کن» در تنظیمات.</summary>
    public async Task<bool> SyncNowAsync(CancellationToken ct = default)
    {
        _fails = 0;
        await StepAsync(force: true, ct);
        return Light == SyncLight.Synced;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try { await Task.Delay(FirstDelay, ct); } catch { return; }

        while (!ct.IsCancellationRequested)
        {
            try { await StepAsync(force: false, ct); }
            catch (OperationCanceledException) { return; }
            catch { /* هیچ خطایی حلقه را نمی‌کشد */ }

            var wait = _fails > 0
                ? Backoff(_fails)
                : (Queued > 0 ? Debounce : PullTick);
            try { await _wake.WaitAsync(wait, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>۲ · ۴ · ۸ · ۱۶ … تا پنج دقیقه.</summary>
    public static TimeSpan Backoff(int fails)
    {
        var seconds = Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, Math.Clamp(fails, 1, 9)));
        return TimeSpan.FromSeconds(seconds);
    }

    // ── یک دور ─────────────────────────────────────────────────────────

    private async Task StepAsync(bool force, CancellationToken ct)
    {
        if (Disabled) return;
        var file = AppSettings.Load();
        var cloud = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });

        if (!cloud.CanSync)
        {
            //  ⚠️ هیچ دستورِ دیتابیسی: نصبی که هنوز بند نشده صفر مصرف دارد
            Set(SyncLight.Idle, "هنوز به سرورِ حساب بند نشده‌ایم — از «پروفایل» وارد شوید", 0);
            return;
        }

        //  ══ ترمزِ Version ═══════════════════════════════════════════════
        //  داده عوض نشده، صف خالی است و وقتِ pull هم نرسیده ⇒ هیچ کاری،
        //  حتی یک SELECT.
        var version = PumpDbContext.Version;
        var pullDue = force || DateTime.UtcNow - _lastPull >= PullTick;
        if (!force && version == _lastVersion && Queued == 0 && !pullDue) return;
        _lastVersion = version;

        // ── ۰) این دفتر مالِ کدام حساب است؟ ────────────────────────────
        //
        //  ⛔ ورود با حسابِ **دیگر** روی همین نصب باید دفترِ همگام‌سازی را
        //  از نو ببندد، وگرنه دادهٔ همین کامپیوتر هیچ‌وقت به حسابِ تازه
        //  نمی‌رسد (چون «بارِ اول» یک بار دویده) و opهای نرفتهٔ حسابِ قبلی
        //  در دفترِ او می‌نشینند. شرحِ کامل و سه دلیلش در
        //  `SyncStore.BindTo`.
        //
        //  ⚠️ **یک بیت از دفترِ کاربر لمس نمی‌شود** — فقط `SyncOps` و ردیفِ
        //  حال. و «بارِ اول»ی که همین‌جا از نو روشن می‌شود دقیقاً همان
        //  چیزی است که اطلاعاتِ بی‌حسابِ کاربر را به حسابِ تازه‌اش می‌برد.
        //  ⚠️ حالِ همگام‌سازی همین‌جا **یک بار** خوانده می‌شود و پایین هم
        //  همین به کار می‌رود — وگرنه هر دور یک `SELECT`ِ اضافه می‌شد.
        var state = _store.State();
        var mine = (file.CloudUserId ?? "").Trim();
        if (mine.Length > 0 && !string.Equals((state.AccountId ?? "").Trim(), mine, StringComparison.Ordinal))
        {
            var bind = _store.BindTo(mine);
            state = _store.State();
            if (bind.Rebound)
            {
                //  ⚠️ همین‌جا می‌ایستیم و دورِ بعد را همین حالا صدا می‌زنیم:
                //  وگرنه پیام در همین دور با پیامِ «بارِ اول» عوض می‌شد و
                //  کاربر هیچ‌وقت نمی‌فهمید چرا کلِ دفترش دوباره می‌رود.
                _lastVersion = -1;
                Set(SyncLight.Queued,
                    "حسابِ تازه — کلِ دفترِ این کامپیوتر برای حسابِ شما فرستاده می‌شود", 0);
                Nudge();
                return;
            }
        }

        // ── ۱) بارِ اول: ردیف‌هایی که پیش از این نسخه ساخته شده‌اند ──────
        if (state.SeededAt == 0)
        {
            var step = _store.SeedStep();
            if (!step.Done)
            {
                Set(SyncLight.Queued, "در حالِ آماده کردنِ دفتر برای اولین همگام‌سازی…", _store.Pending());
                Nudge();
                return;
            }
        }

        // ── ۲) فرستادن ─────────────────────────────────────────────────
        var pending = _store.Pending();
        Queued = pending;
        if (pending > 0 && !state.Holding)
        {
            var batch = _store.Take();
            var res = await cloud.SyncPushAsync(batch, pending, ct);

            if (res.UpgradeRequired)
            {
                //  ⛔ هیچ opی دور ریخته نمی‌شود — فقط می‌ایستیم
                _store.Update(x => { x.Holding = true; x.LastError = res.Why; });
                Set(SyncLight.Queued,
                    "نسخهٔ برنامه از سرورِ حساب جلوتر است — تغییرها نگه داشته شده‌اند تا سرور به‌روز شود",
                    pending);
                LastError = res.Why;
                _fails = 0;
                return;
            }

            if (!res.Ok)
            {
                _fails++;
                _store.CountAttempt(batch.Select(x => x.OpId));
                _store.Update(x => x.LastError = res.Why);
                LastError = res.Why;
                Set(SyncLight.Queued, "در صف — " + res.Why, pending);
                return;
            }

            _fails = 0;
            _store.MarkResults(res.Results);
            _store.Prune();
            LastOkAt = DateTime.Now;
            _store.Update(x =>
            {
                x.LastPushAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                x.LastOkAt = x.LastPushAt;
                x.LastError = "";
                x.ServerSchema = res.ServerSchema;
                x.Holding = false;
                x.DeviceId = cloud.DeviceUid;
            });
            LastError = "";
            Queued = _store.Pending();

            //  دسته پر بود ⇒ هنوز چیزی مانده، همین حالا دورِ بعد
            if (batch.Count >= SyncStore.MaxBatch) Nudge();
        }
        else if (state.Holding && pending > 0)
        {
            //  هر دور یک بار می‌سنجیم که سرور به‌روز شده یا نه — با یک
            //  دستهٔ کوچک، نه کلِ صف.
            var probe = _store.Take(1);
            var res = await cloud.SyncPushAsync(probe, pending, ct);
            if (res.Ok)
            {
                _store.MarkResults(res.Results);
                _store.Update(x => { x.Holding = false; x.LastError = ""; });
                Nudge();
            }
        }

        // ── ۳) گرفتن ───────────────────────────────────────────────────
        if (pullDue)
        {
            _lastPull = DateTime.UtcNow;
            var pull = await cloud.SyncPullAsync(state.Cursor, ct);
            if (!pull.Ok)
            {
                _fails++;
                LastError = pull.Why;
                Set(SyncLight.Queued, "به سرورِ حساب نمی‌رسیم — " + pull.Why, Queued);
                return;
            }

            _fails = 0;
            if (pull.Ops.Count > 0)
            {
                var applied = _store.ApplyIncoming(pull.Ops);
                if (applied.Failed > 0) LastError = "چند تغییرِ رسیده ننشست: " + applied.LastWhy;
            }
            _store.Update(x =>
            {
                x.Cursor = pull.Cursor;
                x.LastPullAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            });
            LastOkAt = DateTime.Now;

            //  هنوز مانده ⇒ همین حالا دورِ بعد
            if (pull.HasMore) { _lastPull = DateTime.MinValue; Nudge(); }
        }

        // ── ۴) چراغ ────────────────────────────────────────────────────
        Queued = _store.Pending();
        if (Queued > 0) Set(SyncLight.Queued, $"{Queued} تغییر در صفِ رفتن", Queued);
        else if (LastError.Length > 0) Set(SyncLight.Failed, LastError, 0);
        else Set(SyncLight.Synced,
            "همگام است" + (LastOkAt is { } at ? $" · آخرین رفت‌وآمد: {at:HH:mm}" : ""), 0);

        // ── ۵) تپش و اعلان ─────────────────────────────────────────────
        if (force || DateTime.UtcNow - _lastBeat >= HeartbeatTick)
        {
            _lastBeat = DateTime.UtcNow;
            await BeatAsync(cloud, ct);
        }

        //  ⚠️ **آخرین کار**: شمارهٔ نسخه را پس از نوشتن‌های خودِ همین دور
        //  می‌گیریم. بی این، ذخیرهٔ حالِ همگام‌سازی خودش `Version` را بالا
        //  می‌برد، دورِ بعد «داده عوض شده» می‌دید و حلقه تا ابد می‌چرخید.
        _lastVersion = PumpDbContext.Version;

        StartLive(cloud, ct);
    }

    /// <summary>
    /// ══ تپشِ هر پانزده دقیقه — بندِ ۲۰٫۷ ═══════════════════════════════
    ///
    /// یک درخواستِ ارزان: اشتراک، روزهای مانده و شمارِ اعلانِ نخوانده.
    /// نتیجه کش می‌شود و «اشتراکِ من» از همان می‌خواند، پس صفحهٔ اشتراک
    /// آفلاین هم چیزی برای نشان دادن دارد.
    ///
    /// ⚠️ عوض شدنِ پلن در پنل، اگر آنلاین باشیم همین‌جا و همان لحظه
    /// دیده می‌شود؛ وگرنه سرِ اولین اتصال.
    /// ⛔ هیچ عددِ قیمتی از این‌جا نمی‌آید و نباید بیاید.
    /// </summary>
    private async Task BeatAsync(CloudLink cloud, CancellationToken ct)
    {
        if (!cloud.SignedIn) return;
        var beat = await cloud.HeartbeatAsync(ct);
        if (!beat.Ok || beat.Unread <= 0) return;

        var (ok, notices, _, _) = await cloud.NoticesAsync(ct);
        if (!ok) return;
        foreach (var n in notices)
        {
            if (n.Read || !_toldNotices.Add(n.Id)) continue;
            //  ⚠️ بنرِ داخلِ برنامه و اعلانِ سیستم، هر دو از همین یک جا.
            //  قاعدهٔ جدا ننویسید، وگرنه روزی یکی می‌آید و آن یکی نه.
            NoticeArrived?.Invoke(n);
        }
    }

    private void Set(SyncLight light, string reason, int queued)
    {
        if (Light == light && Reason == reason && Queued == queued) return;
        Light = light;
        Reason = reason;
        Queued = queued;
        Changed?.Invoke();
    }

    // ── WSS زنده ───────────────────────────────────────────────────────

    /// <summary>
    /// ══ «چیزی عوض شد» از سرور ═══════════════════════════════════════════
    ///
    /// روی خط فقط یک پیامِ کوچک می‌آید (<c>{"event":"changed","cursor":N}</c>)
    /// و برنامه بعدش pull می‌کند. خودِ داده هرگز از این در نمی‌رود.
    ///
    /// ⚠️ نبودنش چیزی را نمی‌شکند: بی سوکت، همان pullِ هر سی ثانیه کار را
    /// می‌کند. برای همین هیچ خطایی از این‌جا بیرون نمی‌رود و نشدنش فقط
    /// یعنی «دورِ بعد دوباره».
    /// </summary>
    private void StartLive(CloudLink cloud, CancellationToken ct)
    {
        if (_live is { IsCompleted: false }) return;
        var token = cloud.SyncToken;
        if (token.Length == 0) return;
        _live = Task.Run(() => LiveAsync(token, cloud.DeviceUid, ct), ct);
    }

    private async Task LiveAsync(string token, string deviceId, CancellationToken ct)
    {
        //  ⚠️ سنجه‌ها شبکه ندارند و نباید سوکت باز کنند
        if (CloudLink.TestTransport is not null) return;

        try
        {
            //  ⛔ نشانی این‌جا چسبانده نمی‌شود — `CloudConfig` تنها جای
            //  دانستنش است (سنجهٔ `HichFayle_Digari_NeshaniRa_Namichasbanad`).
            var url = CloudConfig.WsUrl("/api/sync/v1/live?device_id="
                    + Uri.EscapeDataString(deviceId) + "&app=pump");
            using var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", "Bearer " + token);
            ws.Options.SetRequestHeader("X-App-Id", CloudConfig.ApplicationId);
            await ws.ConnectAsync(new Uri(url), ct);

            var buffer = new byte[4096];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var got = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (got.MessageType == WebSocketMessageType.Close) break;
                var text = Encoding.UTF8.GetString(buffer, 0, got.Count);
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("event", out var e)
                        && e.ValueKind == JsonValueKind.String && e.GetString() == "changed")
                    {
                        //  «pull کن» — خودِ داده از این در نمی‌آید
                        _lastPull = DateTime.MinValue;
                        Nudge();
                    }
                }
                catch { /* پیامِ ناشناس نادیده */ }
            }
        }
        catch { /* سوکت رفاه است، نه اصل */ }
    }

    public async ValueTask DisposeAsync()
    {
        PumpDbContext.Saved -= Nudge;
        var cts = _loop;
        _loop = null;
        if (cts is not null)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        _wake.Dispose();
    }
}
