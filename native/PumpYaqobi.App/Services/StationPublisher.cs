using System.Security.Cryptography;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>یک پیامی که از گوشیِ کارمند/مشتری بالا آمده.</summary>
/// <param name="Id">شناسهٔ همان پیام روی سرور — برای پاک کردنش.</param>
/// <param name="Text">خودِ متن.</param>
/// <param name="From">نامِ فرستنده، اگر گفته باشد.</param>
/// <param name="Kind">نوعِ پیام — پیش‌فرض ‎note‎.</param>
/// <param name="At">زمانِ سرور (میلی‌ثانیهٔ یونیکس).</param>
public sealed record StationNote(string Id, string Text, string From, string Kind, long At);

/// <summary>
/// ══ انتشارِ زندهٔ برنامه روی سرورِ خانگی ═════════════════════════════════════
///
///     نیتیو ──set live──▶ پوشهٔ همین پمپ ──sub live──▶ اپِ کارمندان (گوشی)
///           ◀─sub inbox──                ◀─post inbox─ اندروید · آیفون
///
/// ══ چرا پوشهٔ اختصاصی و نه یک شاخهٔ مشترک ═══════════════════════════════════
///
/// تا امروز همه‌چیز روی ‎stations/&lt;کد&gt;-live‎ی دفترِ همه‌کارهٔ سرور می‌نشست، با
/// یک رمزِ مشترک. یعنی روزی که پمپِ دوم اضافه می‌شد، همان یک رمز دفترِ پمپِ
/// اول را هم باز می‌کرد. حالا سرور برای هر پمپ پوشه و رمزِ جدا می‌دهد و
/// مسیرها داخلِ همان پوشه‌اند: <see cref="LivePath"/> و <see cref="InboxPath"/>.
///
/// ⚠️ راهِ قدیمی برداشته نشده: سرورِ خانگی‌ای که هنوز به‌روز نشده فقط همان را
/// بلد است. <see cref="HomeSync"/> خودش اول درِ تازه را می‌زند و اگر نبود
/// درِ قدیمی را، و این‌جا فقط مسیر را با همان انتخاب هماهنگ می‌کنیم.
///
/// ══ «هر تغییری که در اپ می‌شود در ربات هم باشد» ═══════════════════════════
///
/// بی این‌که حتی یک خط به مسیرهای ذخیرهٔ برنامه اضافه شود: هر
/// <see cref="Interval"/> یک عکسِ تازه ساخته می‌شود و <b>فقط اگر با عکسِ قبلی
/// فرق داشته باشد</b> فرستاده می‌شود.
///
/// ⚠️ هیچ خطایی بیرون نمی‌دهد. سرورِ خانگی ممکن است خاموش باشد، اینترنت
/// نباشد، یا کاربر هنوز وارد نشده باشد — هیچ‌کدام نباید برنامه را بلرزاند.
/// </summary>
public sealed class StationPublisher : IAsyncDisposable
{
    /// <summary>هر چند وقت یک‌بار دنبالِ تغییر بگردد.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

    /// <summary>
    /// هر چند وقت یک‌بار **خودِ اتصال** سنجیده شود — جدا از انتشار.
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «سرور روشن است اما برنامه می‌گوید
    /// خاموش است.» چراغِ سربرگ از همین اتصال حرف می‌زند، پس اتصال باید
    /// زنده و **زودتر از بیست ثانیه** سنجیده شود.
    /// </summary>
    public static readonly TimeSpan LinkTick = TimeSpan.FromSeconds(5);

    /// <summary>
    /// هر چند وقت یک‌بار خودِ برنامه سراغِ <b>ابر</b> برود — جدا از سرورِ
    /// خانگی و جدا از انتشار.
    ///
    /// <para>
    /// ⛔ <b>باگی که «چرا وصل نمی‌شود» را می‌ساخت</b> (۱۴۰۵/۰۷/۰۱): تا امروز
    /// هیچ‌جای برنامه خودش به ابر وصل نمی‌شد. هر تماسِ ابری پشتِ
    /// <c>CloudDeviceToken</c> بود و آن توکن فقط وقتی می‌آمد که کاربر
    /// <b>صفحهٔ پروفایل را باز کند</b>. یعنی نصبی که حساب داشت ولی پروفایل
    /// را باز نکرده بود، تا ابد بند نمی‌شد: نه اشتراکش می‌آمد، نه مجوزش، و
    /// نه هیچ خبری از ابر می‌گرفت — و برنامه هم هیچ نمی‌گفت.
    /// </para>
    ///
    /// <para>
    /// ⚠️ شصت ثانیه، نه پنج: برخلافِ تیکِ سرورِ خانگی، این یکی واقعاً
    /// اینترنت می‌زند و <c>/api/pump/me</c> را می‌خواند.
    /// </para>
    /// </summary>
    public static readonly TimeSpan CloudTick = TimeSpan.FromSeconds(60);

    /// <summary>عکسِ زنده، داخلِ پوشهٔ اختصاصیِ همین پمپ.</summary>
    public const string LivePath = "live";

    /// <summary>راهِ برگشت: چیزی که گوشی‌ها بالا می‌فرستند.</summary>
    public const string InboxPath = "inbox";

    /// <summary>
    /// وقتی سرور پیدا نشد، هر بیست ثانیه دوباره نگردیم — هر تلاش یک پخشِ
    /// UDP و یک درخواستِ HTTP است و روی شبکهٔ خاموش فقط نویز می‌سازد.
    /// </summary>
    private static readonly TimeSpan EnrollRetry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// ⚠️ ولی وقتی **وصل بودیم و قطع شد**، پنج دقیقه صبر کردن غلط است: آی‌پیِ
    /// خانگی با هر بار روشن شدنِ مودم عوض می‌شود و کاربر همان لحظه می‌بیند
    /// که «سرور روشن است و برنامه می‌گوید خاموش». پس در آن حال هر
    /// <see cref="EnrollRetryLost"/> دوباره دنبالِ سرور می‌گردیم.
    /// </summary>
    private static readonly TimeSpan EnrollRetryLost = TimeSpan.FromSeconds(30);

    /// <summary>یک بار وصل شده‌ایم؟ (برای تشخیصِ «قطع شد» از «هیچ‌وقت نبود»)</summary>
    private bool _everLinked;

    /// <summary>آخرین لحظه‌ای که واقعاً وصل بودیم — برای چراغ و راهنمایش.</summary>
    public DateTime? LastLinkedAt { get; private set; }

    private readonly AppHost _host;
    private readonly HomeSync _sync;
    private readonly Func<string> _stationCode;
    private CancellationTokenSource? _loop;
    private string _lastHash = "";
    private long _lastVersion = -1;
    private DateTime _lastEnrollTry = DateTime.MinValue;
    private DateTime _lastRepairTry = DateTime.MinValue;
    private bool _inboxWatched;

    /// <summary>پیام‌هایی که از سرور دیده‌ایم — تا یک پیام دو بار خبر ندهد.</summary>
    private readonly HashSet<string> _seenNotes = new(StringComparer.Ordinal);

    public StationPublisher(AppHost host, HomeSync sync, Func<string> stationCode)
    { _host = host; _sync = sync; _stationCode = stationCode; }

    /// <summary>پیامی از گوشی رسید.</summary>
    public event Action<StationNote>? NoteArrived;

    /// <summary>از کدام در وصل‌ایم — برای صفحهٔ تنظیمات.</summary>
    public HomeSyncMode Mode => _sync.Mode;

    /// <summary>سروری تنظیم شده است؟ — چراغِ سربرگ از این می‌پرسد.</summary>
    public bool Configured => _sync.Configured;

    /// <summary>همین حالا وصل‌ایم؟ — چراغِ سربرگ از این می‌پرسد.</summary>
    public bool Connected => _sync.Connected;

    /// <summary>‎stations/&lt;کد&gt;-live‎ — مسیرِ سرورهای به‌روزنشده.</summary>
    public static string PathOf(string? stationCode)
    {
        //  ⛔ کدِ خالی مسیری نمی‌سازد — نه ‎pump1‎ی مشترکِ دیروز. ‎HomeLink‎
        //  خالی نمی‌دهد، پس این فقط جلوی نوشتن روی پوشهٔ دیگری را می‌گیرد.
        var code = (stationCode ?? "").Trim();
        return code.Length == 0 ? "" : "stations/" + code + "-live";
    }

    /// <summary>عکسِ همین لحظه، بی فرستادن — برای آزمون و برای دکمهٔ دستی.</summary>
    public Task<Dictionary<string, object?>> SnapshotAsync(CancellationToken ct = default)
        => StationSnapshot.BuildAsync(_host, ct);

    /// <summary>
    /// یک‌بار منتشر کن. ‎force‎ی خالی یعنی «فقط اگر چیزی عوض شده».
    /// خروجی: آیا واقعاً چیزی رفت.
    /// </summary>
    public async Task<bool> PublishOnceAsync(bool force = false, CancellationToken ct = default)
    {
        try
        {
            // ══ قفلِ اشتراک ═══════════════════════════════════════════════
            //  عکسِ زندهٔ پمپ فقط یک مشتری دارد: اپِ کارمندان و ربات. پس بی
            //  اشتراک ساخته و فرستاده نمی‌شود — و کیو‌آرِ حساب‌ها هم قفلِ
            //  خودش را دارد. بی این دو، هیچ‌کدام واقعاً قفل نبودند.
            //
            //  ⚠️ «باز» بودن با ارفاق سنجیده می‌شود (‎Entitlements‎)، پس یک
            //  روزِ بی‌اینترنت گوشیِ کارمندِ مشتریِ پول‌داده را خاموش نمی‌کند.
            var karOk = Entitlements.Allows(Entitlements.Kar);
            var qrOk = Entitlements.Allows(Entitlements.QrLive);

            //  ⚠️ خبرها پشتِ اشتراک نیستند — همان قاعده‌ای که سرور دارد
            //  (`routes/pump-events.js`): خبر پیام است، نه دادهٔ فروشی، و
            //  بستنش فقط صاحبِ پمپ را کور می‌کند. کسی که اشتراکش تمام شده
            //  بیشتر از همه لازم دارد بداند تیلش تمام شده.
            var newsOk = CloudActivated;
            if (!karOk && !qrOk && !newsOk) return false;

            var ready = karOk && await ReadyAsync(force, ct);

            // ⚠️ بی سرورِ خانگی و بی ابر، عکس گرفتن فقط CPU می‌سوزاند.
            if (!ready && !((qrOk || newsOk) && CloudActivated)) return false;

            // ⚠️ و بی تغییر هم: با پنج سال داده، ساختنِ عکس یک ثانیه است و هر
            // بیست ثانیه یک‌بار یعنی پنج درصدِ CPU برای همیشه («کامپیوتر داغ»).
            // شمارهٔ نسخهٔ داده می‌گوید از دورِ قبل چیزی ذخیره شده یا نه.
            var version = PumpYaqobi.Persistence.PumpDbContext.Version;
            if (!force && version == _lastVersion && _accts.Pending == 0) return false;

            var snap = await StationSnapshot.BuildAsync(_host, ct);
            _lastVersion = version;

            // ⚠️ ‎seq‎ هر بار عوض می‌شود، پس در محکِ «چیزی عوض شده؟» نمی‌آید —
            // وگرنه هر بیست ثانیه یک‌بار کلِ داده بیخود فرستاده می‌شد.
            var hash = HashOf(snap);
            var went = false;

            if (ready && (force || hash != _lastHash))
            {
                var path = _sync.Mode == HomeSyncMode.Station ? LivePath : PathOf(_stationCode());
                if (path.Length > 0 && await _sync.SetAsync(path, snap, ct))
                {
                    _lastHash = hash;
                    went = true;

                    //  ⚠️ نشانیِ سرورِ خانگی را هم به ابر بسپار — همان چیزی که
                    //  اپِ کارمند را از پرسیدنِ آدرس بی‌نیاز می‌کند. آی‌پیِ خانگی
                    //  با هر بار روشن شدنِ مودم عوض می‌شود، پس باید تکرار شود؛
                    //  ولی نه هر بیست ثانیه، که بی‌جهت به سرور فشار بیاورد.
                    _ = PublishHomeToCloudAsync(ct);
                }
            }

            // کیو‌آرِ زنده: حساب‌های کیو‌آردار، فقط وقتی چیزی عوض شده — یا
            // دورِ پیش یکی‌شان نرفته و هنوز طلبکار است.
            if (qrOk && (force || hash != _lastAcctHash || _accts.Pending > 0))
            {
                _lastAcctHash = hash;
                await PublishAccountsAsync(ready, ct);
            }

            //  ⚠️ خبرها **آخر** می‌روند و نتیجه‌شان `went` را عوض نمی‌کند:
            //  `went` یعنی «عکس روی سرورِ خانگی نشست»، و چراغ و سنجه‌ها
            //  همان را می‌خوانند.
            if (newsOk) await PublishAlertsAsync(snap, ct);

            return went;
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
    }

    /// <summary>انتشارِ حساب‌های کیو‌آردار — به هر دو مقصد (<see cref="AcctLive"/>).</summary>
    private readonly AcctLivePublisher _accts = new();
    private string _lastAcctHash = "";

    /// <summary>آیا برنامه با کدِ شش‌رقمی به ابر وصل شده — بی این، فایلی به ابر نمی‌رود.</summary>
    private static bool CloudActivated => !string.IsNullOrWhiteSpace(AppSettings.Load().CloudDeviceToken);

    /// <summary>حساب‌های کیو‌آردار در این دور — برای آزمون و گزارشِ صفحهٔ تنظیمات.</summary>
    public int LastAccountsSent => _accts.LastSent;

    /// <summary>خبرهای این پمپ روی ابر — «برنامه بسته هم باشد، خبر برسد».</summary>
    private readonly CloudEvents _events = new();

    /// <summary>خبرهایی که آخرین بار به ابر رفت — برای سنجه‌ها.</summary>
    public int LastEventsSent => _events.LastSent;

    /// <summary>
    /// هشدارهای تازهٔ همین عکس ⇒ دفترِ خبرِ ابریِ همین پمپ.
    ///
    /// <para>
    /// ⛔ فهرست از <see cref="StationSnapshot.Alerts"/> می‌آید و جای دیگری
    /// ساخته نمی‌شود — همان جایی که رنگِ کارتِ قرض‌دار هم از آن می‌آید.
    /// </para>
    /// <para>
    /// ⚠️ و هیچ‌وقت خطا بیرون نمی‌دهد: خبر نرفتن نباید انتشارِ عکس را
    /// بشکند.
    /// </para>
    /// </summary>
    private async Task PublishAlertsAsync(Dictionary<string, object?> snap, CancellationToken ct)
    {
        try
        {
            var alerts = snap.TryGetValue("alerts", out var a) ? a as List<object?> : null;
            if (alerts is null || alerts.Count == 0) return;

            var file = AppSettings.Load();
            if (string.IsNullOrWhiteSpace(file.CloudDeviceToken)) return;   // هنوز فعال نشده
            var cloud = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
            await _events.PublishAsync(cloud, alerts, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* خبر رفاه است، دفتر اصل */ }
    }

    /// <summary>
    /// ── چرا این از انتشارِ اصلی جداست ──────────────────────────────────
    /// مشتری روی اینترنت است، نه در شبکهٔ پمپ؛ پس مقصدِ اصلی‌اش **ابر** است
    /// و باید حتی وقتی سرورِ خانگی خاموش است برود. مقصدِ خانگی فقط وقتی هست
    /// که درِ تازه (پوشهٔ همین پمپ) باز باشد: درِ قدیمی شاخهٔ ‎acct‎ ندارد.
    /// </summary>
    private async Task PublishAccountsAsync(bool homeReady, CancellationToken ct)
    {
        try
        {
            var items = await AcctLive.CollectAsync(_host, ct);
            if (items.Count == 0 && _accts.Pending == 0) return;

            Func<string, object, CancellationToken, Task<bool>>? home =
                homeReady && _sync.Mode == HomeSyncMode.Station
                    ? (p, v, c) => _sync.SetAsync(p, v, c)
                    : null;

            Func<string, object, CancellationToken, Task<bool>>? cloud = null;
            var file = AppSettings.Load();
            if (!string.IsNullOrWhiteSpace(file.CloudDeviceToken))
            {
                var link = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
                cloud = async (p, v, c) => (await link.PutFileAsync(p, v, c)).Ok;
            }

            await _accts.PublishAsync(items, home, cloud, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* پیش از ورود، یا سرورِ خاموش — دورِ بعد */ }
    }

    /// <summary>آخرین باری که نشانی به ابر رفت — تا هر بیست ثانیه نرود.</summary>
    private DateTime _lastHomePush = DateTime.MinValue;

    /// <summary>
    /// سپردنِ نشانی و رمزِ فقط‌خواندنیِ سرورِ خانگی به ابر.
    ///
    /// ── چرا ────────────────────────────────────────────────────────────
    /// اپِ کارمند دیگر آدرس نمی‌پرسد: با گوگل وارد می‌شود و نشانی را از ابر
    /// می‌گیرد. ولی ابر فقط وقتی می‌داند که همین‌جا گفته باشیم.
    ///
    /// ⚠️ هیچ‌وقت جلوی انتشارِ اصلی را نمی‌گیرد: اگر اینترنت نباشد یا
    /// برنامه هنوز فعال نشده باشد، بی‌صدا رد می‌شود. دفترِ پمپ روی سرورِ
    /// خانگی کارِ خودش را می‌کند.
    /// </summary>
    private async Task PublishHomeToCloudAsync(CancellationToken ct)
    {
        try
        {
            if ((DateTime.UtcNow - _lastHomePush) < TimeSpan.FromMinutes(10)) return;

            var file = AppSettings.Load();
            if (string.IsNullOrWhiteSpace(file.CloudDeviceToken)) return;   // هنوز فعال نشده

            //  ⛔ نشانیِ گوشی‌ها، نه ‎127.0.0.1‎ی خودِ این کامپیوتر
            var url = HomeLink.ShareUrl(_host);
            if (string.IsNullOrWhiteSpace(url)) return;

            _lastHomePush = DateTime.UtcNow;
            var cloud = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
            await cloud.PublishHomeAsync(url, HomeLink.ReadKey(_host), ct, HomeLink.StationCode(_host));
        }
        catch (OperationCanceledException) { throw; }
        catch { /* ابر نرسید — کارِ پمپ نباید بایستد */ }
    }

    /// <summary>
    /// «نشانی و رمز داریم و وصل‌ایم؟» — و اگر نه، خودش درستش می‌کند.
    ///
    /// این همان «اگر آدرس نداشت، برایش بساز» است: تا سرور پیدا نشود چیزی
    /// منتشر نمی‌شود، و کاربر هم هیچ‌وقت آدرسی تایپ نمی‌کند.
    /// </summary>
    private async Task<bool> ReadyAsync(bool force, CancellationToken ct)
    {
        //  ⛔ پوشهٔ فعلی مالِ این حساب نیست (‎pump1‎ی کهنه، یا کدِ حسابِ دیگر)
        //  ⇒ پوشهٔ خودِ همین حساب را بگیر (۱۴۰۵/۰۷/۱۳). تا ثبتِ تازه ننشسته،
        //  اتصالِ فعلی دست نمی‌خورد؛ و با همان ترمزِ ثبتِ همیشگی، نه هر ۵ ثانیه.
        if (StationLink.NeedsMove(AppSettings.Load())
            && (force || DateTime.UtcNow - _lastEnrollTry >= EnrollRetryLost))
        {
            _lastEnrollTry = DateTime.UtcNow;
            var moved = await StationLink.EnsureAsync(_host, ct: ct);
            if (moved.Ok && !StationLink.NeedsMove(AppSettings.Load()))
            {
                //  پوشهٔ تازه خالی است ⇒ همه‌چیز از نو برود
                await _sync.DropAsync();
                _inboxWatched = false;
                _lastHash = "";
                _lastVersion = -1;
                _lastAcctHash = "";
                _accts.ForgetHome();
                //  و سرورِ حساب همان لحظه پوشهٔ تازه را بداند — نه ده دقیقهٔ بعد
                _lastHomePush = DateTime.MinValue;
            }
        }

        if (!_sync.Configured || _sync.Mode == HomeSyncMode.None)
        {
            // روی سرورِ خاموش، هر بیست ثانیه نگردیم — ولی اگر یک بار وصل
            // بوده‌ایم و قطع شده، زود دوباره بگرد (آی‌پی عوض شده باشد).
            var wait = _everLinked ? EnrollRetryLost : EnrollRetry;
            var due = DateTime.UtcNow - _lastEnrollTry >= wait;
            if (force || due)
            {
                _lastEnrollTry = DateTime.UtcNow;
                await StationLink.EnsureAsync(_host, ct: ct);
            }
            else if (!_sync.Configured)
            {
                return false;
            }
        }

        if (!await _sync.ConnectAsync(ct))
        {
            //  ⛔ **نشانیِ ذخیره‌شده دیگر جواب نمی‌دهد ⇒ دوباره بگرد** (۱۴۰۵/۰۷/۱۳).
            //  تا امروز `EnsureAsync` با «نشانی و رمز داریم» همان‌جا برمی‌گشت، پس
            //  نشانی‌ای که یک بار مُرد (آی‌پیِ تازهٔ مودم، یا نشانیِ کارتِ شبکهٔ
            //  سروری که فقط روی ‎127.0.0.1‎ گوش می‌داد) برای همیشه می‌ماند و چراغ
            //  هیچ‌وقت سبز نمی‌شد. حالا با `force` دوباره ثبت می‌شود، و اگر آن
            //  نشانی نرسید، کشفِ خودکار سرورِ رسیدنی را پیدا می‌کند — با همان رمز.
            if (!_sync.Configured || !(force || DateTime.UtcNow - _lastRepairTry >= EnrollRetryLost))
                return false;
            _lastRepairTry = DateTime.UtcNow;
            var before = HomeLink.Url(_host);
            var repaired = await StationLink.EnsureAsync(_host, force: true, ct: ct);
            if (!repaired.Ok) return false;
            await _sync.DropAsync();
            _inboxWatched = false;
            if (!string.Equals(before, HomeLink.Url(_host), StringComparison.OrdinalIgnoreCase)) _lastHomePush = DateTime.MinValue;
            if (!await _sync.ConnectAsync(ct)) return false;
        }
        _everLinked = true;
        LastLinkedAt = DateTime.Now;
        await WatchInboxAsync(ct);
        return true;
    }

    /// <summary>
    /// «فقط وصل بمان» — بی ساختنِ عکس و بی هیچ دستورِ دیتابیس.
    ///
    /// ⚠️ **قفلِ اشتراک روی عکسِ زنده است، نه روی خودِ اتصال.** پیش از این،
    /// برنامهٔ بی‌اشتراک در نخستین خطِ <see cref="PublishOnceAsync"/> برمی‌گشت
    /// و هیچ‌وقت به سرورِ خانگی وصل نمی‌شد — پس چراغِ سربرگ «سرور جواب
    /// نمی‌دهد» می‌گفت در حالی که سرور روشن بود. حالا اتصال همیشه برقرار
    /// می‌ماند (که رایگان است) و آن‌چه قفل می‌شود همان انتشارِ عکس است.
    /// </summary>
    public async Task<bool> KeepLinkAsync(bool force = false, CancellationToken ct = default)
    {
        try { return await ReadyAsync(force, ct); }
        catch { return false; }
    }

    /// <summary>
    /// گوش دادن به صندوقِ ورودی — یک‌بار، و <see cref="HomeSync"/> خودش با هر
    /// قطعیِ شبکه از نو می‌گیردش.
    /// </summary>
    private async Task WatchInboxAsync(CancellationToken ct)
    {
        if (_inboxWatched || _sync.Mode != HomeSyncMode.Station) return;
        _inboxWatched = await _sync.SubscribeAsync("inbox", InboxPath, OnInbox, ct);
    }

    /// <summary>
    /// عکسِ تازهٔ صندوق رسید. سرور کلِ شاخه را می‌فرستد (نه فقط تفاوت را)،
    /// پس خودمان می‌فهمیم کدام‌ها تازه‌اند.
    /// </summary>
    private void OnInbox(JsonElement box)
    {
        if (box.ValueKind != JsonValueKind.Object) return;
        foreach (var note in Notes(box))
        {
            if (!_seenNotes.Add(note.Id)) continue;
            NoteArrived?.Invoke(note);
            var who = note.From.Length > 0 ? note.From + ": " : "";
            _host.Toast(who + note.Text, ToastKind.Info);
        }
    }

    /// <summary>خواندنِ شاخهٔ صندوق. هر ردیفِ ناشناس بی‌صدا رد می‌شود.</summary>
    public static IReadOnlyList<StationNote> Notes(JsonElement box)
    {
        var list = new List<StationNote>();
        if (box.ValueKind != JsonValueKind.Object) return list;

        foreach (var row in box.EnumerateObject())
        {
            if (row.Value.ValueKind != JsonValueKind.Object) continue;
            string S(string k) =>
                row.Value.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var text = S("text");
            if (text.Length == 0) continue;
            var at = row.Value.TryGetProperty("at", out var a) && a.TryGetInt64(out var n) ? n : 0;
            var kind = S("kind");
            list.Add(new StationNote(row.Name, text, S("from"), kind.Length > 0 ? kind : "note", at));
        }
        list.Sort((x, y) => x.At.CompareTo(y.At));
        return list;
    }

    /// <summary>پیامِ خوانده‌شده را از سرور بردار.</summary>
    public async Task<bool> ClearNoteAsync(string id, CancellationToken ct = default)
    {
        if (_sync.Mode != HomeSyncMode.Station || string.IsNullOrWhiteSpace(id)) return false;
        return await _sync.RemoveAsync(InboxPath + "/" + id, ct);
    }

    /// <summary>
    /// خاموشِ صریح — فقط برای آزمون‌های واحد (<c>PumpYaqobi.Tests</c>).
    ///
    /// <para>
    /// ⛔ <b>چرا لازم شد:</b> هر آزمونی که <c>MainViewModel</c> می‌سازد و وارد
    /// می‌شود، این حلقه را راه می‌انداخت و هیچ‌کس نمی‌بستش. حلقه هر دقیقه
    /// <c>LicenseClock.Tick</c> ⇒ <c>SaveSoon()</c> می‌زند و <c>SaveSoon</c>
    /// پوشهٔ <b>همان لحظه</b> را برمی‌دارد؛ پس اگر آزمونِ بعدی تازه
    /// <c>AppSettings.DirOverride</c> را عوض کرده بود، تنظیمات در پوشهٔ
    /// <b>او</b> می‌نشست. <c>NevashtaneDarSaf_DarPushehyeDigari_Nemineshinad</c>
    /// همین را در یکی از دو اجرای CI دید و در دیگری نه — شکلِ مسابقه.
    /// </para>
    /// <para>⚠️ سنجه‌های رابط (<c>serverdot</c> و …) خودِ حلقه را می‌خواهند و این را نمی‌زنند.</para>
    /// <para>⛔ در برنامهٔ واقعی هیچ‌جا نوشته نمی‌شود — همان قاعدهٔ <c>SyncEngine.Disabled</c>.</para>
    /// </summary>
    public static bool Disabled { get; set; }

    /// <summary>حلقهٔ پس‌زمینه. صدا زدنش دو بار، یکی بیشتر نمی‌سازد.</summary>
    public void Start()
    {
        if (Disabled) return;
        if (_loop is not null) return;
        var cts = new CancellationTokenSource();
        _loop = cts;
        _ = Task.Run(() => LoopAsync(cts.Token), cts.Token);
    }

    /// <summary>پیش از اولین انتشار — تا ورود و اولین صفحه بی رقیب بمانند.</summary>
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(12);

    private async Task LoopAsync(CancellationToken ct)
    {
        // ⚠️ نه همان لحظهٔ ورود: عکسِ ایستگاه با داده‌ی زیاد یک ثانیه CPU است و
        // درست وقتی می‌رفت که کاربر تازه رمز زده و منتظرِ صفحهٔ اول بود.
        try { await Task.Delay(FirstDelay, ct); } catch { return; }
        var lastPublish = DateTime.MinValue;
        //  ⚠️ `MinValue` یعنی همان دورِ اول می‌رود — «در جا وصل شود».
        var lastCloud = DateTime.MinValue;
        while (!ct.IsCancellationRequested)
        {
            //  ۱) اتصال — هر پنج ثانیه، بی هیچ هزینه‌ای (وصل باشیم، همین
            //     بی‌درنگ برمی‌گردد). چراغِ سربرگ از همین حرف می‌زند.
            try { await KeepLinkAsync(false, ct); }
            catch (OperationCanceledException) { return; }
            catch { /* سرورِ خاموش خطا نیست */ }

            //  ۲) انتشار — همان بیست ثانیهٔ همیشگی، نه زودتر: ساختنِ عکس با
            //     پنج سال داده یک ثانیه CPU است.
            if (DateTime.UtcNow - lastPublish >= Interval)
            {
                lastPublish = DateTime.UtcNow;
                try { await PublishOnceAsync(false, ct); }
                catch (OperationCanceledException) { return; }
                catch { /* سرورِ خاموش خطا نیست */ }
            }

            //  ۳) ابر — خودش، بی این‌که کاربر صفحه‌ای را باز کند.
            if (DateTime.UtcNow - lastCloud >= CloudTick)
            {
                lastCloud = DateTime.UtcNow;
                try { await CloudKeepAsync(ct); }
                catch (OperationCanceledException) { return; }
                catch { /* بی‌اینترنت خطا نیست */ }
            }

            try { await Task.Delay(LinkTick, ct); }
            catch { return; }
        }
    }

    /// <summary>
    /// وصل شدن به ابر — خودکار، و بی هیچ ادعای دروغ.
    ///
    /// <para>
    /// دو حالت، و هر دو یک درخواستِ <b>واقعی</b> می‌زنند:
    /// </para>
    /// <list type="bullet">
    ///   <item>توکنِ حساب داریم ⇒ <see cref="CloudLink.HomeFromAccountAsync"/>:
    ///     نشست را تازه می‌کند، دستگاه را (اگر نبند بود) خودش بند می‌کند،
    ///     حالِ اشتراک را می‌خواند و نشانیِ سرورِ خانگی را می‌گیرد؛ و بعد
    ///     <see cref="CloudLink.KeepLicenseFreshAsync"/> مجوزِ امضاشده را
    ///     تازه می‌کند.
    ///     ⚠️ آن دومی <b>لازم است</b>: خودِ <c>HomeFromAccountAsync</c>
    ///     برای دستگاهی که از قبل بند شده هیچ مجوزی نمی‌گیرد، و مجوز
    ///     تنها جایی است که <b>فهرستِ قابلیت‌های پلن</b> در آن است.</item>
    ///   <item>حساب نداریم ⇒ فقط <see cref="CloudLink.CloudHealthAsync"/>، تا
    ///     دستِ‌کم بدانیم ابر بالا است یا نه. این بی توکن است و هیچ دری را
    ///     باز نمی‌کند.</item>
    /// </list>
    ///
    /// <para>
    /// ⚠️ خروجی‌اش دور ریخته می‌شود و هیچ استثنایی بیرون نمی‌دهد: چراغِ ابر
    /// از <see cref="CloudLink.Reach"/> حرف می‌زند که خودِ
    /// <c>SendFull</c> پرش می‌کند — یعنی از جوابِ واقعیِ سرور، نه از این‌که
    /// این تابع صدا زده شده باشد.
    /// </para>
    /// </summary>
    /// <summary>
    /// همان دورِ شصت‌ثانیه‌ای — <b>همین حالا</b>. کلیکِ چراغِ سربرگ و دکمهٔ
    /// «ثبتِ همین کامپیوتر»ِ پروفایل از همین می‌روند، نه از راهِ دومی
    /// (۱۴۰۵/۰۷/۱۳: کلیکِ چراغ فقط «سرور بالاست؟» را می‌پرسید و هیچ‌وقت این
    /// کامپیوتر را ثبت نمی‌کرد). هیچ استثنایی بیرون نمی‌دهد.
    /// ⛔ و مثلِ خودِ حلقه **هیچ پمپی نمی‌سازد** — «هر حساب یک پمپ».
    /// </summary>
    public static async Task CloudKeepNowAsync(CancellationToken ct = default)
    {
        try { await CloudKeepAsync(ct); }
        catch { /* بی‌اینترنت خطا نیست — چراغ از `Reach` راست می‌گوید */ }
    }

    private static async Task CloudKeepAsync(CancellationToken ct)
    {
        var file = AppSettings.Load();
        //  کفِ ساعتِ مجوز — هر دقیقه، هم‌پای زمانی که برنامه باز است
        //  (`LicenseClock`). یک خواندنِ فایل که همین‌جا بود، نه دستورِ دیتابیس.
        LicenseClock.Tick(file);
        if (!string.IsNullOrWhiteSpace(file.CloudAccountToken))
        {
            var cloud = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
            await cloud.HomeFromAccountAsync(ct);
            //  ⛔ **نشست همین حالا مُرد** (۴۰۱ و تازه‌سازیِ ناموفق) — مثلاً حساب
            //  از ریشه در پنل حذف شد. سنجهٔ `linkstates` روی پشتهٔ واقعی دید
            //  که توکنِ دستگاهِ پمپِ حذف‌شده ده دقیقه زنده می‌ماند و چراغ سبز
            //  «به سرورِ حساب وصل است» و پروفایل «فعال — وصل به پمپِ شما»
            //  می‌گفتند. پس همین حالا خودِ دستگاه پرسیده می‌شود: سرور اگر
            //  «device_not_registered» گفت، `DevSendAsync` توکن را برمی‌دارد.
            //  ⚠️ نشستِ مرده به‌تنهایی دستگاه را پاک **نمی‌کند** — نشستِ
            //  نودروزه هم طبیعی می‌میرد و دستگاه سالم می‌ماند؛ فقط جوابِ صریحِ
            //  سرور دربارهٔ خودِ دستگاه.
            if (!cloud.SignedIn && cloud.Activated)
            {
                await cloud.RefreshAsync(ct);
                return;
            }
            //  ⛔ و مجوز — وگرنه اشتراکی که مدیر همین حالا روی سرور داد
            //  هیچ‌وقت به این دستگاه نمی‌رسید مگر کاربر صفحهٔ پروفایل را
            //  باز کند. شرحِ کامل بالای `CloudLink.KeepLicenseFreshAsync`.
            await cloud.KeepLicenseFreshAsync(ct);
            return;
        }

        //  ⛔ دستگاهی که **بی حساب** بند است (با کدِ شش‌رقمی فعال شده، یا
        //  کاربر از حسابش بیرون آمده) هم مجوزش باید تازه شود — تا
        //  ۱۴۰۵/۰۷/۱۲ فقط حالتِ بالا تازه می‌شد، پس اشتراکی که مدیر برداشته
        //  بود روی چنین نصبی تا انقضای خودِ مجوز باز می‌ماند و تمدیدش هم
        //  هیچ‌وقت نمی‌رسید. (ترمزِ ده‌دقیقه‌ایِ خودِ همان تابع سرِ جایش است.)
        if (!string.IsNullOrWhiteSpace(file.CloudDeviceToken))
        {
            var device = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
            await device.KeepLicenseFreshAsync(ct);
        }

        await CloudLink.CloudHealthAsync(ct);
    }

    /// <summary>
    /// اثرِ انگشتِ عکس، بی ‎seq‎ و بی زمان — تا «عوض شد؟» معنی داشته باشد.
    /// </summary>
    public static string HashOf(Dictionary<string, object?> snap)
    {
        var copy = new Dictionary<string, object?>(snap);
        copy.Remove("seq");
        copy.Remove("at");
        copy.Remove("atUtc");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(copy);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public async ValueTask DisposeAsync()
    {
        var cts = _loop;
        _loop = null;
        if (cts is not null)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        await _sync.DisposeAsync();
    }
}
