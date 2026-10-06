using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using PumpYaqobi.Domain;

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

    /// <summary>
    /// ⛔ شورا ت۳ — فقط برای آزمونِ <b>رفتاری</b>ِ همین موتور: یک نمونهٔ
    /// صریح (نه <c>host.Sync</c>) که با «الان همگام کن» یک دور می‌دود، در
    /// حالی که <see cref="Disabled"/> برای بقیهٔ آزمون‌ها روشن می‌ماند.
    /// حلقهٔ پس‌زمینه را راه نمی‌اندازد و در برنامهٔ واقعی هیچ‌جا نوشته نمی‌شود.
    /// </summary>
    public bool RunWhenDisabled { get; init; }

    private readonly SyncStore _store;
    private readonly SemaphoreSlim _wake = new(0, 1);
    /// <summary>«مکثِ نخست را رد کن» — فقط از <see cref="PrimeNow"/>.</summary>
    private readonly SemaphoreSlim _startNow = new(0, 1);
    private CancellationTokenSource? _loop;
    private Task? _live;
    private bool _primeOver;

    private long _lastVersion = -1;
    private int _fails;
    private DateTime _lastPull = DateTime.MinValue;

    /// <summary>
    /// ⛔ یک دور در هر لحظه. دکمهٔ «الان همگام کن» (‎SyncNowAsync‎) تا امروز
    /// ‎StepAsync‎ را هم‌زمان با حلقه می‌دواند: هر دو همان دسته را می‌گرفتند و
    /// می‌فرستادند، هر دو همان opهای رسیده را می‌نشاندند — ردیفِ دوتایی.
    /// </summary>
    private readonly SemaphoreSlim _stepGate = new(1, 1);

    /// <summary>
    /// opهای رسیده‌ای که ننشستند (پدرِ نرسیده، خطای یک ردیف). ⛔ مکان‌نما از
    /// رویشان رد می‌شود، پس تا امروز برای همیشه گم می‌شدند — یعنی کامپیوترِ دوم
    /// آن ردیف را هیچ‌وقت نمی‌دید. با هر گرفتنِ بعدی پیش از opهای تازه دوباره
    /// امتحان می‌شوند (حداکثر ‎DeferMaxTries‎ بار؛ فقط در حافظه).
    /// </summary>
    private readonly List<IncomingOp> _deferred = new();
    private readonly Dictionary<string, int> _deferTries = new(StringComparer.Ordinal);
    public const int DeferMaxTries = 20;
    /// <summary>کنارگذاشته‌ها مالِ کدام دفترند — دفترِ دیگر یعنی از دیسکِ همان دفتر.</summary>
    private string _deferredLedger = "";
    private DateTime _lastBeat = DateTime.MinValue;
    private readonly HashSet<string> _toldNotices = new(StringComparer.Ordinal);

    private readonly AppHost _host;

    public SyncEngine(AppHost host)
    {
        _host = host;
        _store = new SyncStore(host.Db);
    }

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

    // ══ پردهٔ «آوردنِ اطلاعاتِ حساب» ════════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «یارو اینترنت داره و می‌ره تو
    //  حساب است و لودینگ روی صفحه نمیاد تا اطلاعاتی که توی حساب و سرور
    //  است بیاد روی همون حساب.»
    //
    //  ⛔ <b>فقط نخستین گرفتنِ هر حساب.</b> بعدش همان چراغِ کوچکِ نوار کار
    //  را می‌کند؛ پرده‌ای که با هر ‎pull‎ بیاید، یک پرده نیست، یک مزاحم است.
    //  ⛔ <b>و هیچ‌وقت دیوار نیست</b>: «ادامه در پس‌زمینه» همیشه هست، و هر
    //  خطایی خودش پرده را می‌برد. برنامه آفلاین هم باید کار کند.

    /// <summary>همین حالا در حالِ آوردنِ اطلاعاتِ حسابیم؟</summary>
    public bool Priming { get; private set; }

    /// <summary>کاربر همین حالا چه می‌بیند — یک جملهٔ آماده.</summary>
    public string PrimeText { get; private set; } = "";

    /// <summary>چند تغییر تا این لحظه از حساب رسیده.</summary>
    public int PrimeGot { get; private set; }

    /// <summary>
    /// پرده تمام شد. <c>ok</c> یعنی دفترِ حساب واقعاً آمد.
    /// ⚠️ پوسته با همین بخشِ جلوی چشم را از نو می‌خواند — وگرنه کاربر
    /// صفحه‌ای را می‌بیند که پیش از رسیدنِ داده خوانده شده بود.
    /// </summary>
    public event Action<bool, string, int>? PrimeFinished;

    /// <summary>هر بار که یکی از بالایی‌ها عوض شد.</summary>
    public event Action? Changed;

    /// <summary>اعلانِ تازه‌ای رسید (از راهِ تپش یا WSS).</summary>
    public event Action<CloudNotice>? NoticeArrived;

    /// <summary>⛔ شورا ب۱: این دور N تعارض دید — بی‌صدا نماند.</summary>
    public event Action<int>? ConflictsFound;

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
    private bool _held;

    /// <summary>
    /// ══ وسطِ عوض شدنِ دفتر، هیچ کاری ═══════════════════════════════════════
    ///
    /// <see cref="AppHost.UseLedgerOf"/> پیش از جابه‌جایی راست می‌کند و بعدش
    /// دروغ. حلقه روی نخِ دیگری می‌دود، پس بی این نگهبان یک دورِ نیمه‌تمام
    /// می‌توانست opهای دفترِ قبلی را با توکنِ حسابِ تازه بفرستد.
    ///
    /// ⚠️ حلقه را نمی‌کُشد و نخی نمی‌سازد — فقط همان یک دور را رد می‌کند و
    /// دورِ بعد خودش ادامه می‌دهد.
    /// </summary>
    public void Hold(bool on) => Volatile.Write(ref _held, on);

    /// <summary>
    /// ⛔ برای بازگردانیِ بکاپ و آوردنِ «فایلِ کامل» (۱۴۰۵/۰۷/۱۶): نه فقط دورِ تازه نمی‌دود،
    /// دورِ <b>در جریان</b> هم تمام می‌شود تا هیچ اتصالی به فایلِ دفتر باز نماند — وگرنه
    /// ‎-wal‎ِ دفترِ قبلی روی فایلِ تازه بازپخش می‌شد یا مکان‌نمای کهنه در دفترِ تازه می‌نشست.
    /// </summary>
    public async Task<IDisposable> PauseAsync()
    {
        Hold(true);
        await _stepGate.WaitAsync();
        return new Resume(this);
    }

    private sealed class Resume : IDisposable
    {
        private SyncEngine? _e;
        public Resume(SyncEngine e) => _e = e;
        public void Dispose()
        {
            var e = Interlocked.Exchange(ref _e, null);
            if (e is null) return;
            e._stepGate.Release();
            e.Hold(false);
        }
    }

    public void Nudge()
    {
        try { if (_wake.CurrentCount == 0) _wake.Release(); }
        catch (SemaphoreFullException) { /* از پیش بیدار است */ }
    }

    /// <summary>
    /// ══ «همین حالا، نه پانزده ثانیهٔ دیگر» ══════════════════════════════
    ///
    /// از لحظه‌ای صدا زده می‌شود که کاربر واقعاً واردِ حسابش شده
    /// (<c>AccountSectionViewModel</c>). <see cref="FirstDelay"/> برای این
    /// است که صفحهٔ اولِ برنامه بی رقیب بالا بیاید — ولی کسی که همین حالا
    /// وارد شده و منتظرِ دفترِ خودش است، پانزده ثانیه به یک صفحهٔ
    /// <b>خالی</b> نگاه می‌کند و گمان می‌کند اطلاعاتش رفته.
    ///
    /// ⚠️ مکث را فقط همین یک در رد می‌کند، نه هر ذخیرهٔ دیتابیس: وگرنه
    /// بالا آمدنِ برنامه خودش دوباره با همگام‌سازی رقیب می‌شد.
    /// </summary>
    public void PrimeNow()
    {
        try { if (_startNow.CurrentCount == 0) _startNow.Release(); }
        catch (SemaphoreFullException) { /* از پیش گفته شده */ }
        Nudge();
    }

    /// <summary>
    /// «ادامه در پس‌زمینه» — پرده می‌رود و دیگر در این اجرا برنمی‌گردد.
    /// ⚠️ خودِ همگام‌سازی هیچ کاری‌اش نمی‌شود؛ فقط دیگر جلوی صفحه نیست.
    /// </summary>
    public void DismissPrime()
    {
        if (!Priming && _primeOver) return;
        _primeOver = true;
        Priming = false;
        PrimeText = "";
        Changed?.Invoke();
    }

    /// <summary>
    /// پرده حق دارد بیاید؟
    ///
    /// ⛔ <b>هر دو شرط لازم‌اند.</b> <c>PrimedAt == 0</c> تنها مهرِ درست
    /// است (حسابِ خالی با گرفتنِ موفق هم <c>Cursor</c>ش صفر می‌ماند)، و
    /// <c>Cursor == 0</c> نصب‌های امروزی را — که از قبل همگام‌اند و این
    /// ستون را تازه گرفته‌اند — از یک پردهٔ بی‌دلیل نگه می‌دارد.
    /// </summary>
    public static bool PrimeWanted(Domain.Entities.SyncStateRow s) =>
        s.PrimedAt == 0 && s.Cursor == 0;

    /// <summary>گرفتنِ نخستینِ دفتر در همین اجرا شروع شده و هنوز تمام نشده.</summary>
    private bool _primeRun;

    /// <summary>پرده یک بار تمام شد (موفق یا نه) — دیگر در این اجرا برنمی‌گردد.</summary>
    private bool _primeEnded;

    private void SetPrime(bool on, string text)
    {
        if (_primeOver) on = false;
        if (Priming == on && PrimeText == text) return;
        Priming = on;
        PrimeText = on ? text : "";
        Changed?.Invoke();
    }

    /// <summary>
    /// پرده تمام شد — چه دفتر آمده باشد چه نیامده.
    ///
    /// ⛔ <b>یک بار در هر اجرا، و بس.</b> بی این قفل، یک شبکهٔ لرزان هر
    /// چند دقیقه پرده را جلوی چشمِ کاربر روشن و خاموش می‌کرد.
    /// </summary>
    private void EndPrime(bool ok, string why)
    {
        //  ⚠️ «ادامه در پس‌زمینه» فقط پرده را می‌برد، نه خبرِ «رسید» را: بخشِ
        //  جلوی چشم باید پس از رسیدنِ دفتر از نو خوانده شود، دیده یا نادیده.
        if (_primeEnded) return;
        _primeEnded = true;
        _primeRun = false;
        _primeOver = true;
        var got = PrimeGot;
        Priming = false;
        PrimeText = "";
        Changed?.Invoke();
        PrimeFinished?.Invoke(ok, why, got);
    }

    /// <summary>دکمهٔ «الان همگام کن» در تنظیمات.</summary>
    public async Task<bool> SyncNowAsync(CancellationToken ct = default)
    {
        _fails = 0;
        //  ⛔ روی نخِ دیگر، مثلِ خودِ حلقه (۱۴۰۵/۰۷/۱۶) — «بارِ اول» و نشاندنِ یک صفحهٔ
        //  کامل روی نخِ رابط پنجره را می‌خشکاند. شنونده‌ها از قبل برای نخِ حلقه نوشته شده‌اند.
        await Task.Run(() => StepAsync(force: true, ct), ct);
        return Light == SyncLight.Synced;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        //  ⚠️ «تا پانزده ثانیه صبر کن، مگر کسی بگوید همین حالا»
        //  (<see cref="PrimeNow"/>) — نه یک ‎Task.Delay‎ی شکست‌ناپذیر.
        try { await _startNow.WaitAsync(FirstDelay, ct); } catch { return; }

        while (!ct.IsCancellationRequested)
        {
            try { await StepAsync(force: false, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
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
        await _stepGate.WaitAsync(ct);
        try { await StepCoreAsync(force, ct); }
        finally { _stepGate.Release(); }
    }

    private async Task StepCoreAsync(bool force, CancellationToken ct)
    {
        if (Disabled && !RunWhenDisabled) return;

        //  ⛔ دفتر همین حالا در حالِ عوض شدن است — دست نزن.
        //  بی این، opهای دفترِ حسابِ **قبلی** با توکنِ حسابِ **تازه**
        //  می‌رفتند: دادهٔ یک مشتری در دفترِ ابریِ مشتریِ دیگر.
        if (Volatile.Read(ref _held)) return;
        var file = AppSettings.Load();
        var cloud = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });

        if (!cloud.CanSync)
        {
            //  ⚠️ هیچ دستورِ دیتابیسی: نصبی که هنوز بند نشده صفر مصرف دارد
            //  ⛔ و هیچ پرده‌ای: کسی که حساب ندارد منتظرِ هیچ دفتری نیست.
            SetPrime(false, "");
            Set(SyncLight.Idle, "هنوز به سرورِ حساب بند نشده‌ایم — از «پروفایل» وارد شوید", 0);
            return;
        }

        //  ══ ترمزِ Version ═══════════════════════════════════════════════
        //  داده عوض نشده، صف خالی است و وقتِ pull هم نرسیده ⇒ هیچ کاری،
        //  حتی یک SELECT.
        var version = PumpDbContext.Version;
        var pullDue = force || AppClock.Mono - _lastPull >= PullTick;
        if (!force && version == _lastVersion && Queued == 0 && !pullDue) return;

        //  ⚠️ پس از ترمزِ ‎Version‎: سنجیدنِ مجوز هر تیک نه.
        //  ⛔ پلنی که خدماتِ سرور ندارد (استاندارد، دائمیِ بی‌تمدید) دفترش فقط روی
        //  همین کامپیوتر است — ‎Entitlements.Online‎ (۱۴۰۵/۰۷/۲۰). سرور هم
        //  ‎plan_no_services‎ می‌دهد؛ این‌جا فقط تا هیچ درخواستی نرود.
        if (Entitlements.PlanDenies(Entitlements.Online))
        {
            SetPrime(false, "");
            Set(SyncLight.Idle, "همگام‌سازی با سرور در پلنِ شما نیست — دفتر فقط روی همین کامپیوتر است", 0);
            return;
        }
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
        //  ⛔ **پیش از هر خواندنی از دفتر**: دفترِ همین حساب باز است؟
        //
        //  حساب می‌تواند از هر جایی عوض شود (ورود، کدِ ایمیلی، تازه‌سازیِ
        //  نشست) و این حلقه روی نخِ دیگری می‌دود — پس ممکن است پیش از
        //  پوسته بیدار شود. بی این خط، همان یک دور opهای دفترِ حسابِ
        //  **قبلی** را با توکنِ حسابِ **تازه** می‌فرستاد.
        //
        //  ⚠️ تصمیم این‌جا گرفته نمی‌شود — `AppHost.UseLedgerOf` تنها جای
        //  آن است و تا حساب عوض نشده باشد فقط دو رشته را مقایسه می‌کند.
        //  جابه‌جا که شد، همین دور رها می‌شود و دورِ بعد روی دفترِ درست
        //  از نو شروع می‌کند.
        var mine = (file.CloudUserId ?? "").Trim();
        if (_host.UseLedgerOf(mine)) { _lastVersion = -1; _deferred.Clear(); _deferTries.Clear(); _deferredLedger = ""; Nudge(); return; }

        //  ⛔ دفتر ممکن است <b>وسطِ همین دور</b> عوض شود (ورودِ حسابِ دیگر روی
        //  نخِ رابط، در حالی که این‌جا منتظرِ شبکه‌ایم). پس از هر ‎await‎ پیش از
        //  هر نوشتنی می‌سنجیم؛ وگرنه نتیجهٔ opهای حسابِ الف و opهای رسیدهٔ
        //  حسابِ الف در دفترِ حسابِ ب می‌نشستند.
        var ledger = _host.Db.DbPath;
        bool Moved() => Volatile.Read(ref _held)
                        || !string.Equals(_host.Db.DbPath, ledger, StringComparison.Ordinal);

        var state = _store.State();
        //  ⛔ شناسهٔ همگام‌سازیِ همین دفتر — شرحش بالای ‎CloudLink.SyncDeviceFor‎
        cloud.SyncDeviceOverride = CloudLink.SyncDeviceFor(state.DeviceId, state.UidSeed, cloud.DeviceUid);
        if (mine.Length > 0 && !string.Equals((state.AccountId ?? "").Trim(), mine, StringComparison.Ordinal))
        {
            var bind = _store.BindTo(mine);
            state = _store.State();
            if (bind.Rebound)
            {
                _deferred.Clear(); _deferTries.Clear(); _deferredLedger = "";
                //  ⚠️ همین‌جا می‌ایستیم و دورِ بعد را همین حالا صدا می‌زنیم:
                //  وگرنه پیام در همین دور با پیامِ «بارِ اول» عوض می‌شد و
                //  کاربر هیچ‌وقت نمی‌فهمید چرا کلِ دفترش دوباره می‌رود.
                _lastVersion = -1;
                PrimeGot = 0;
                SetPrime(PrimeWanted(state), "حسابِ تازه — دفتر برای حسابِ شما آماده می‌شود…");
                Set(SyncLight.Queued,
                    "حسابِ تازه — کلِ دفترِ این کامپیوتر برای حسابِ شما فرستاده می‌شود", 0);
                Nudge();
                return;
            }
        }

        //  ══ پرده: «آوردنِ اطلاعاتِ حساب» ════════════════════════════════
        //  از همین‌جا تا پایانِ نخستین گرفتنِ کامل. `PrimeWanted` هر دو
        //  شرط را با هم می‌سنجد و `SetPrime` خودش `_primeOver` را رعایت
        //  می‌کند، پس هیچ دو-جای-تصمیمی ساخته نمی‌شود.
        //  ⛔ <b>پرده تا پایانِ گرفتن می‌ماند، نه تا پایانِ صفحهٔ اول.</b>
        //  `PrimeWanted` فقط «شروع کن؟» است و `Cursor == 0` در آن هست — پس
        //  از نخستین صفحه (۵۰۰ تغییر) دیگر راست نبود: پرده وسطِ آوردنِ دفتر
        //  می‌رفت، `PrimedAt` هیچ‌وقت مهر نمی‌خورد و `PrimeFinished` (که بخشِ
        //  جلوی چشم را از نو می‌خواند) هیچ‌وقت شلیک نمی‌شد. سنجهٔ `tensync`
        //  (۱۴۰۵/۰۷/۱۴، دو کامپیوتر روی سرورِ حسابِ واقعی) گرفتش.
        var priming = !_primeEnded && (_primeRun || PrimeWanted(state));
        if (priming) _primeRun = true;

        // ── ۱) بارِ اول: ردیف‌هایی که پیش از این نسخه ساخته شده‌اند ──────
        //  ⚠️ و یک بار «بذرِ ترمیم» برای دفتری که پیش از ۱۴۰۵/۰۷/۲۰ بذر شده بود
        if (state.SeededAt == 0 || state.RepairSeed < SyncStore.RepairVersion)
        {
            var step = _store.SeedStep();
            if (!step.Done)
            {
                SetPrime(priming, "آماده کردنِ دفترِ این کامپیوتر…");
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
            if (priming)
                SetPrime(true, $"فرستادنِ دفترِ این کامپیوتر به حسابِ شما… ({Shamsi.Money(pending)} مانده)");
            var batch = _store.Take();
            var res = await cloud.SyncPushAsync(batch, pending, ct);
            if (Moved()) { _lastVersion = -1; Nudge(); return; }

            if (res.UpgradeRequired)
            {
                //  ⛔ هیچ opی دور ریخته نمی‌شود — فقط می‌ایستیم
                _store.Update(x => { x.Holding = true; x.LastError = res.Why; });
                Set(SyncLight.Queued,
                    "نسخهٔ برنامه از سرورِ حساب جلوتر است — تغییرها نگه داشته شده‌اند تا سرور به‌روز شود",
                    pending);
                LastError = res.Why;
                _fails = 0;
                if (priming) EndPrime(false, "نسخهٔ برنامه از سرورِ حساب جلوتر است");
                return;
            }

            if (!res.Ok && res.TooLarge && batch.Count == 1)
            {
                //  ⛔ یک opِ تنها از سقفِ بدنهٔ سرور بزرگ‌تر است: تلاشِ دوباره هرگز
                //  نمی‌رسد و سرِ صف همهٔ تغییرهای بعدی را برای همیشه نگه می‌داشت.
                //  «رد شد» علامت می‌خورد (همان کارِ ‎rejected‎ی خودِ سرور) و صف
                //  جلو می‌رود؛ خودِ ردیف روی همین کامپیوتر دست نمی‌خورد.
                _store.MarkResults(new Dictionary<string, string> { [batch[0].OpId] = "too_large" });
                LastError = "یک تغییرِ خیلی بزرگ به سرور نرفت";
                Queued = _store.Pending();
                Nudge();
            }
            else if (!res.Ok)
            {
                _fails++;
                _store.CountAttempt(batch.Select(x => x.OpId));
                //  ⛔ ‎plan_no_services‎ با کدِ بی‌اینترنتِ وی‌آی‌پی روی همین کامپیوتر: جملهٔ
                //  عمومیِ پلن دروغ است (سربرگ وی‌آی‌پی می‌گوید) ⇒ دلیلِ واقعی (۱۴۰۵/۰۷/۲۱)
                var why = DeniedWhy(cloud, res.Code, res.Why);
                await HealDeadTokenAsync(cloud, res.Code, ct);
                _store.Update(x => x.LastError = why);
                LastError = why;
                Set(SyncLight.Queued, "در صف — " + why, pending);
                if (priming) EndPrime(false, why);
                return;
            }
            else
            {
                _fails = 0;
                _store.MarkResults(res.Results);
                //  ⛔ شورا ب۱: «همین حالا رفته» — گرفتنِ همین دور مالِ ما را با opِ کهنه‌تر نمی‌پوشاند
                _store.NotePushed(batch, res.Seqs);
                _store.Prune();
                LastOkAt = AppClock.Now;
                _store.Update(x =>
                {
                    x.LastPushAt = AppClock.UnixMs;
                    x.LastOkAt = x.LastPushAt;
                    x.LastError = "";
                    x.ServerSchema = res.ServerSchema;
                    x.Holding = false;
                    x.DeviceId = cloud.SyncDevice;
                });
                LastError = "";
                Queued = _store.Pending();

                //  هنوز چیزی مانده ⇒ همین حالا دورِ بعد. ⚠️ نه «دسته پر بود»: دسته
                //  حالا با بایت هم بسته می‌شود و دستهٔ کوتاه‌تر از دویست هم می‌تواند
                //  پشتش صفِ بلندی داشته باشد.
                if (Queued > 0) Nudge();
            }
        }
        else if (state.Holding && pending > 0)
        {
            //  هر دور یک بار می‌سنجیم که سرور به‌روز شده یا نه — با یک
            //  دستهٔ کوچک، نه کلِ صف.
            var probe = _store.Take(1);
            var res = await cloud.SyncPushAsync(probe, pending, ct);
            if (Moved()) { _lastVersion = -1; Nudge(); return; }
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
            if (priming)
                SetPrime(true, PrimeGot > 0
                    ? $"آوردنِ اطلاعاتِ حساب… ({Shamsi.Money(PrimeGot)} تغییر تا این‌جا)"
                    : "آوردنِ اطلاعاتِ حساب از سرور…");

            _lastPull = AppClock.Mono;
            var pull = await cloud.SyncPullAsync(state.Cursor, ct);
            if (Moved()) { _lastVersion = -1; Nudge(); return; }
            if (!pull.Ok)
            {
                _fails++;
                LastError = DeniedWhy(cloud, pull.Code, pull.Why);
                await HealDeadTokenAsync(cloud, pull.Code, ct);
                //  ⚠️ ‎plan_no_services‎ یعنی رسیدیم و سرور نه گفت — «نمی‌رسیم» نیست
                Set(SyncLight.Queued, pull.Code == "plan_no_services"
                    ? "در صف — " + LastError
                    : "به سرورِ حساب نمی‌رسیم — " + pull.Why, Queued);
                //  ⛔ پرده می‌رود و دیگر برنمی‌گردد. حلقه خودش عقب‌نشینی
                //  می‌کند و باز می‌کوشد؛ ولی کاربر نباید پشتِ یک پردهٔ
                //  بی‌پایان بماند — برنامه آفلاین هم باید کار کند.
                if (priming) EndPrime(false, pull.Why);
                return;
            }

            _fails = 0;
            var applyWhy = "";
            //  ⛔ کنارگذاشته‌ها روی دیسک‌اند (۱۴۰۵/۰۷/۲۰): بستنِ برنامه گمشان نمی‌کند
            if (!string.Equals(_deferredLedger, ledger, StringComparison.Ordinal))
            {
                _deferred.Clear(); _deferTries.Clear(); _deferredLedger = "";
                _deferred.AddRange(_store.LoadDeferred());
                _deferredLedger = ledger;
            }
            if (pull.Ops.Count > 0 || _deferred.Count > 0)
            {
                //  کنارگذاشته‌های دورِ قبل اول (قدیمی‌ترند)، بعد رسیده‌های تازه
                var batch = _deferred.Count == 0 ? pull.Ops : _deferred.Concat(pull.Ops).ToList();
                var applied = _store.ApplyIncoming(batch, state.Cursor);
                if (applied.Conflicts > 0) ConflictsFound?.Invoke(applied.Conflicts);
                _deferred.Clear();
                foreach (var op in applied.FailedOps)
                {
                    var key = op.OpId + "|" + op.RowUid;
                    //  ⛔ تا سرور هنوز صفحهٔ بعد دارد، پدر شاید همان‌جاست — شمرده نمی‌شود.
                    //  پیش از این بیست صفحه (۱۰٬۰۰۰ op) که می‌گذشت دور ریخته می‌شد.
                    var tries = _deferTries.GetValueOrDefault(key) + (pull.HasMore ? 0 : 1);
                    if (tries >= DeferMaxTries) { _deferTries.Remove(key); continue; }
                    _deferTries[key] = tries;
                    _deferred.Add(op);
                }
                //  آن‌هایی که نشستند از شمارش بیرون می‌روند
                if (_deferTries.Count > _deferred.Count)
                {
                    var keep = _deferred.Select(o => o.OpId + "|" + o.RowUid).ToHashSet(StringComparer.Ordinal);
                    foreach (var k in _deferTries.Keys.Where(k => !keep.Contains(k)).ToList()) _deferTries.Remove(k);
                }
                if (applied.Failed > 0) applyWhy = "چند تغییرِ رسیده ننشست: " + applied.LastWhy;
                PrimeGot += pull.Ops.Count;
            }

            //  ⛔ **گرفتنِ موفق، خطای دورِ قبل را پاک می‌کند.**
            //
            //  باگی که این را لازم کرد، و با عکسِ صاحب ریپو دیده شد
            //  (۱۴۰۵/۰۷/۱۱): چراغِ نوارِ پایین **سرخِ جاویدان** می‌شد.
            //  زنجیره‌اش:
            //    ۱) یک نرسیدنِ گذرا (یک لحظه قطعیِ اینترنت) ⇒ `LastError`
            //       پر می‌شود؛
            //    ۲) دورهای بعد صف **خالی** است، پس کلِ بلوکِ فرستادن — و
            //       با آن تنها جای `LastError = ""` — رد می‌شود؛
            //    ۳) گرفتن موفق است ولی چیزی را پاک نمی‌کرد؛
            //    ۴) ته حلقه: صف صفر و خطا پر ⇒ «همگام نشد»ِ سرخ، **برای
            //       همیشه** — در حالی که همگام‌سازی کاملاً سالم بود.
            //
            //  یعنی چراغ دربارهٔ **گذشته** حرف می‌زد، نه دربارهٔ حالا. و
            //  این همان «کلکِ دروغ»ِ قدغن است، فقط وارونه: می‌گفت خراب
            //  است در حالی که نبود. برای کسی که می‌خواهد اشتراک بفروشد،
            //  یک چراغِ سرخِ بی‌دلیل بدترین چیزی است که مشتری می‌بیند.
            //
            //  ⚠️ و خطای **واقعیِ** همین دور (ردیفی که ننشست) پاک نمی‌شود
            //  — فقط خطای دورِ قبل.
            LastError = applyWhy;

            _store.Update(x =>
            {
                x.Cursor = pull.Cursor;
                x.DeferredJson = SyncStore.DeferredText(_deferred);
                x.LastPullAt = AppClock.UnixMs;
                x.LastOkAt = AppClock.UnixMs;
                x.LastError = applyWhy;
            });
            LastOkAt = AppClock.Now;

            //  هنوز مانده ⇒ همین حالا دورِ بعد
            if (pull.HasMore) { _lastPull = DateTime.MinValue; Nudge(); }
            else if (priming)
            {
                //  ⛔ **مهرِ صریح**، نه «مکان‌نما بزرگ‌تر از صفر»: حسابی که
                //  روی سرور هیچ چیزی ندارد هم همین‌جا تمام می‌شود و پرده‌اش
                //  دیگر هر سی ثانیه برنمی‌گردد.
                _store.Update(x => x.PrimedAt = AppClock.UnixMs);
                EndPrime(true, "");
            }
        }

        // ── ۴) چراغ ────────────────────────────────────────────────────
        Queued = _store.Pending();
        if (Queued > 0) Set(SyncLight.Queued, $"{Queued} تغییر در صفِ رفتن", Queued);
        else if (LastError.Length > 0) Set(SyncLight.Failed, LastError, 0);
        else Set(SyncLight.Synced,
            "همگام است" + (LastOkAt is { } at ? $" · آخرین رفت‌وآمد: {at:HH:mm}" : ""), 0);

        // ── ۵) تپش و اعلان ─────────────────────────────────────────────
        if (force || AppClock.Mono - _lastBeat >= HeartbeatTick)
        {
            _lastBeat = AppClock.Mono;
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
            //  ⛔ یک بار، نه با هر باز شدنِ برنامه (۱۴۰۵/۰۷/۱۵ — «هی هر بار میاد
            //  که می‌گه اشتراکِ شما تمدید شد»): هم این‌جا به یاد می‌ماند و هم
            //  روی سرور «خوانده شد» می‌شود. نرسیدنِ دومی اولی را نمی‌شکند.
            if (SeenNotices.Seen(n.Id)) { await MarkReadAsync(cloud, n.Id, ct); continue; }
            SeenNotices.MarkSeen(n.Id);
            //  ⚠️ بنرِ داخلِ برنامه و اعلانِ سیستم، هر دو از همین یک جا.
            //  قاعدهٔ جدا ننویسید، وگرنه روزی یکی می‌آید و آن یکی نه.
            NoticeArrived?.Invoke(n);
            await MarkReadAsync(cloud, n.Id, ct);
        }
    }

    private static async Task MarkReadAsync(CloudLink cloud, string id, CancellationToken ct)
    {
        try { await cloud.NoticeReadAsync(id, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* یادِ محلی کافی است */ }
    }

    /// <summary>
    /// ⛔ توکنِ دستگاهِ مرده (‎invalid_token‎) خودش درست می‌شود — شرحش بالای
    /// ‎CloudLink.RebindDeviceAsync‎. دست‌بالا هر دو دقیقه یک بار، تا سقفِ نرخِ ورود پر نشود.
    /// </summary>
    private async Task HealDeadTokenAsync(CloudLink cloud, string code, CancellationToken ct)
    {
        if (code != "invalid_token" || !cloud.Activated || !cloud.SignedIn) return;
        if (AppClock.Mono - _lastHeal < HealGap) return;
        _lastHeal = AppClock.Mono;
        try { if (await cloud.RebindDeviceAsync(ct)) { _fails = 0; Nudge(); } }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* دورِ بعد دوباره */ }
    }

    public static readonly TimeSpan HealGap = TimeSpan.FromMinutes(2);
    private DateTime _lastHeal = DateTime.MinValue;

    /// <summary>دلیلِ ردِ سرور، با کدِ بی‌اینترنتِ همین کامپیوتر سنجیده (‎CloudLink.ServicesDeniedReason‎).</summary>
    private static string DeniedWhy(CloudLink cloud, string code, string why) =>
        code == "plan_no_services" ? cloud.ServicesDeniedReason(why) : why;

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
        //  ⚠️ سنجه‌ها شبکه ندارند و نباید سوکت باز کنند — مگر سنجهٔ پشتهٔ واقعی (‎TestWsBase‎)
        if (CloudLink.TestTransport is not null && CloudConfig.TestWsBase is null) return;

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
