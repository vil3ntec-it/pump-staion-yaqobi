using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PumpYaqobi.App.Update;

/// <summary>نتیجهٔ بررسیِ به‌روزرسانی.</summary>
/// <param name="IsSmallPackage">
/// بستهٔ کوچک است (فقط فایل‌های خودِ برنامه، چند مگابایت) نه بستهٔ کامل.
/// این را کاربر باید ببیند — همان چیزی که «هر بار از سر دانلود نکنم» یعنی.
/// </param>
/// <param name="Problem">
/// چرا بررسی به جایی نرسید. خالی یعنی جواب گرفتیم (چه تازه‌ای بود چه نبود).
/// ⛔ این جدا بودن **لازم** است: پیش از این هر شکستی — قطعیِ اینترنت، بسته
/// بودنِ مسیر، سقفِ نرخِ سرور — همان «برنامه به‌روز است» می‌شد و کاربر
/// ساعت‌ها دنبالِ نسخه‌ای می‌گشت که برنامه ادعا می‌کرد ندارد. همان «کلکِ
/// دروغ»ی که برای چراغِ سرور قدغن شد.
/// </param>
/// <param name="SumsUrl">
/// نشانیِ <c>SHA256SUMS.txt</c>ِ همان انتشار. خالی ⇒ کنارِ خودِ بسته (همان
/// پوشهٔ دانلودِ همان برچسب) — هر دو درِ انتشار آن را دارند.
/// </param>
public sealed record UpdateInfo(
    bool Available, string CurrentVersion, string LatestVersion,
    string? DownloadUrl, long SizeBytes, string? Notes, bool IsSmallPackage = false,
    string Problem = "", string? SumsUrl = null)
{
    /// <summary>بررسی به جایی نرسید — نه «به‌روز است» و نه «تازه‌ای هست».</summary>
    public bool Failed => Problem.Length > 0;

    /// <summary>
    /// همان یک جمله‌ای که کاربر می‌خواند. ⛔ تنها جای ساختنِ این جمله همین‌جاست
    /// تا سه حال هیچ‌وقت با هم قاطی نشوند.
    /// </summary>
    public string StatusText =>
        Failed ? "❌ " + Problem
        : Available ? "نسخهٔ تازه آماده است: " + LatestVersion
        : "برنامه به‌روز است — نسخهٔ " + CurrentVersion;

    /// <summary>رنگِ همان جمله: سرخ فقط وقتی بررسی نشده باشد.</summary>
    public string StatusBrushKey => Failed ? "Pump.Danger" : "Pump.Muted";

    /// <summary>«۲٫۱ مگابایت» — اندازهٔ خواندنی.</summary>
    public string SizeText => SizeBytes <= 0
        ? ""
        : (SizeBytes / 1048576.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
          + " مگابایت";

    /// <summary>«به‌روزرسانیِ کوچک — ۲٫۱ مگابایت» یا «بستهٔ کامل — ۵۷ مگابایت».</summary>
    public string PackageText => !Available
        ? ""
        : (IsSmallPackage ? "به‌روزرسانیِ کوچک" : "بستهٔ کامل")
          + (SizeText.Length > 0 ? " — " + SizeText : "");
}

/// <summary>
/// ══ به‌روزرسانیِ خودکار ══════════════════════════════════════════════════════
/// برنامه خودش نسخهٔ تازه را می‌گیرد و نصب می‌کند.
///
/// ⚠️ خواستهٔ صریحِ صاحب ریپو: «توش نوشته نباشه از مخزن فلان فلان.»
/// پس نشانیِ منبع فقط همین‌جا، به‌صورت ثابتِ داخلی، هست و **هیچ‌جای رابط
/// کاربری** نامِ مخزن، نامِ کاربری یا نشانی دیده نمی‌شود. پیام‌هایی که کاربر
/// می‌بیند فقط این‌هایند: «نسخهٔ تازه هست»، «در حال گرفتن…»، «آماده نصب است».
/// اگر روزی این نشانی عوض شد، فقط همین خط عوض می‌شود.
/// </summary>
public sealed class UpdateService
{
    // نشانیِ داخلی — هرگز به رابط کاربری راه پیدا نمی‌کند
    private const string FeedUrl =
        "https://api.github.com/repos/vil3ntec-it/pump-staion-yaqobi/releases/latest";

    /// <summary>
    /// ══ مخزنِ عمومیِ «فقط خروجی» (از ۱۴۰۵/۰۷/۱۶) ═══════════════════════════
    /// خواستهٔ صاحب ریپو: کدِ برنامه دیده نشود. پس مخزنِ کد روزی **خصوصی**
    /// می‌شود — و آن روز <see cref="FeedUrl"/> برای هر نصبی ۴۰۴ می‌دهد. این
    /// مخزن فقط نصاب و بسته‌ها را دارد و عمومی می‌ماند.
    ///
    /// ⛔ **هر دو خوانده می‌شوند و تازه‌تر برنده است** (<see cref="CheckAsync"/>)،
    /// نه «اولی که جواب داد»: تا مخزنِ خروجی ساخته و پر نشده، ۴۰۴ یا کهنه
    /// است و نباید برنامه را «به‌روز» بخواند. پس ساختنش هیچ نصبی را نمی‌شکند
    /// و خصوصی شدنِ مخزنِ کد هم — فقط اگر این نسخه پیش از آن رسیده باشد.
    /// </summary>
    private const string OutputFeedUrl =
        "https://api.github.com/repos/vil3ntec-it/pump-yaqobi-releases/releases/latest";

    /// <summary>ترتیبِ پرسیدن: خروجی اول (روزِ خصوصی شدن تنها راه است).</summary>
    private static readonly string[] Feeds = { OutputFeedUrl, FeedUrl };

    /// <summary>
    /// ══ درِ اصلی: سرورِ خودِ پمپ (از ۱۴۰۵/۰۷/۱۹) ═══════════════════════════
    /// خواستهٔ صاحب سامانه: «از سرور آپدیت بگیره برنامه، و سرور هم از گیت‌هاب
    /// نسخه‌های جدید رو بگیره.» سرورِ خانگی هر دو دقیقه نسخهٔ تازه را می‌گیرد،
    /// با چک‌سام می‌سنجد و همین‌جا سرو می‌کند (ریپوی server ⇒
    /// ‎src/pumpupdates/mirror.js‎). نشانی از ‎CloudConfig.Url‎ است — همان یک جای
    /// ساختنِ نشانیِ سرور.
    ///
    /// ⛔ سرور جواب داد ⇒ همان، و هیچ درخواستی به جای دیگر نمی‌رود. فقط اگر
    /// سرور نرسید، دو درِ قبلی (پشتیبان) امتحان می‌شوند — تا روزی که سرور خاموش
    /// است هیچ مشتری‌ای از به‌روزرسانی جا نماند.
    /// ⚠️ تزریق‌پذیر فقط برای آزمون؛ <c>null</c> یعنی «سرور نیست».
    /// </summary>
    public static Func<string?> ServerFeed { get; set; } = () => ServerFeedUrl;

    /// <summary>
    /// نشانیِ درِ اصلی — همیشه از ‎CloudConfig.Url‎. کانالِ آزمایشی
    /// <c>?channel=testing</c> می‌گیرد؛ پایدار (پیش‌فرض) هیچ پارامتری ندارد، پس
    /// سرورِ کهنه‌ای که کانال نمی‌شناسد همان پاسخِ همیشگی را می‌دهد.
    /// </summary>
    public static string ServerFeedUrl =>
        Services.CloudConfig.Url("/api/pump-updates/latest") + (TestingChannel ? "?channel=testing" : "");

    private static bool? _testing;

    /// <summary>
    /// 🛤️ شورا، ت۱ — «پایدار» (پیش‌فرض) یا «آزمایشی». تنها جای خواندن و نوشتنِ
    /// <see cref="Services.AppSettings.UpdateChannel"/>. ⛔ کانال فقط می‌گوید <b>کدام</b>
    /// نسخه از سرور پرسیده شود؛ چک‌سام، امضا و «فقط به جلو» همان‌اند.
    /// </summary>
    public static bool TestingChannel
    {
        get
        {
            if (_testing is bool b) return b;
            try { _testing = Services.AppSettings.Load().UpdateChannel == "testing"; }
            catch { _testing = false; }
            return _testing.Value;
        }
        set
        {
            _testing = value;
            try
            {
                var s = Services.AppSettings.Load();
                s.UpdateChannel = value ? "testing" : "stable";
                s.Save();
            }
            catch { /* نشد ⇒ فقط همین اجرا */ }
        }
    }

    /// <summary>فقط آزمون: کانال را بی نوشتن روی دیسک فراموش می‌کند.</summary>
    public static void ForgetChannel() => _testing = null;

    /// <summary>
    /// میزبان‌های گیت‌هاب که سرآیندِ <c>Date</c>شان ساعتِ مطمئن است (‎Services.TimeSync‎)
    /// — از خودِ <see cref="FeedUrl"/>، تا نشانیِ منبع همین یک‌جا بماند.
    /// </summary>
    internal static readonly string[] TimeHosts =
    {
        new Uri(FeedUrl).Host, "github.com",
        "objects.githubusercontent.com", "release-assets.githubusercontent.com",
    };

    /// <summary>پرسشِ سبکِ ساعت وقتی سرورِ حساب جواب نداد (فقط سرآیند).</summary>
    internal const string TimeProbeUrl = "https://github.com/";

    /// <summary>
    /// برچسبِ چرخشیِ درِ دوم — همیشه روی تازه‌ترین ساخت می‌نشیند، پس نشانیِ
    /// فایل‌هایش ثابت است و بی هیچ پرس‌وجویی خوانده می‌شود.
    /// </summary>
    private const string RollingTag = "desktop-latest";

    /// <summary>
    /// ══ درِ دوم ═════════════════════════════════════════════════════════════
    /// نشانیِ یک فایلِ ثابت روی همان انتشارِ چرخشی.
    ///
    /// ⚠️ از خودِ <see cref="FeedUrl"/> ساخته می‌شود، نه از یک رشتهٔ دوم: نامِ
    /// مخزن باید در کلِ برنامه **یک جا** نوشته شود (آزمونِ
    /// ‎TheUpdateServiceItselfKeepsTheAddressPrivate‎ همین را قفل کرده).
    /// </summary>
    private static string FileUrl(string feed, string name)
    {
        var parts = new Uri(feed).AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);   // repos/<owner>/<repo>/releases/latest
        return "https://github.com/" + parts[1] + "/" + parts[2]
             + "/releases/download/" + RollingTag + "/" + name;
    }


    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        // GitHub بدونِ User-Agent پاسخ نمی‌دهد. نامِ برنامه، نه نامِ مخزن.
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PumpYaqobi", AppVersion.Current));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    /// <summary>
    /// ══ درگاهِ سنجش ═════════════════════════════════════════════════════════
    /// همان الگوی <c>CloudLink.TestTransport</c>. ⛔ این‌جا **لازم** بود، نه
    /// تجمل: تا امروز سی‌ویک آزمون دربارهٔ به‌روزرسانی داشتیم و **هیچ‌کدام**
    /// نه <see cref="CheckAsync"/> را می‌دواند و نه
    /// <see cref="DownloadAsync"/> را — همه رشته‌های سورس و خودِ رکورد را
    /// می‌سنجیدند. پس هر خرابیِ **رفتاری** در این مسیر بی‌صدا از CI رد می‌شد،
    /// و همین شد که کاربر روی نسخهٔ کهنه ماند و آزمون‌ها سبز بودند.
    ///
    /// ⚠️ نشانی را باز نمی‌کند: مقدارش فقط از خودِ آزمون می‌آید (نه از
    /// تنظیمات، نه از محیط) و آزمونِ ‎CloudAddressLock‎مانندِ
    /// ‎TheUpdateServiceItselfKeepsTheAddressPrivate‎ سرِ جایش است.
    /// </summary>
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? TestTransport { get; set; }

    /// <summary>تنها جای فرستادنِ درخواست — تا درگاهِ سنجش یک نقطه بماند.</summary>
    private static async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, HttpCompletionOption how, CancellationToken ct)
    {
        var sent = PumpYaqobi.Domain.AppClock.MonoSource();
        var res = TestTransport is null
            ? await Http.SendAsync(req, how, ct)
            : await TestTransport(req, ct);
        //  ⛔ ساعتِ واقعی از پاسخِ گیت‌هاب هم (سرآیندِ Date) — ‎TimeSync‎
        Services.TimeSync.From(req, res, sent);
        return res;
    }

    private static Task<HttpResponseMessage> GetAsync(string url, CancellationToken ct)
        => SendAsync(new HttpRequestMessage(HttpMethod.Get, url),
                     HttpCompletionOption.ResponseContentRead, ct);

    /// <summary>
    /// ══ آیا نسخهٔ تازه‌ای هست؟ ══════════════════════════════════════════════
    /// دو در، به همان ترتیب:
    ///
    ///   ۱) فهرستِ انتشار — یادداشت و اندازهٔ دقیق را هم می‌دهد، ولی برای
    ///      درخواستِ بی‌توکن سقفِ ساعتی دارد و پشتِ یک اینترنتِ مشترک زود
    ///      تمام می‌شود؛ و بعضی شبکه‌ها همان مسیر را اصلاً باز نمی‌کنند.
    ///   ۲) یک فایلِ متنیِ کوچک کنارِ خودِ بسته‌ها — بی سقف، بی احراز هویت.
    ///      یادداشتِ انتشار همراهش نیست، که مهم نیست.
    ///
    /// ⛔ **و اگر هر دو بسته بودند، «برنامه به‌روز است» گفته نمی‌شود.** همین
    /// یک خط بود که کاربر را روی ۳.۱.۱۴۴ نگه داشت در حالی که نسخهٔ تازه
    /// منتشر شده بود: هر شکستی به «به‌روز است» ترجمه می‌شد. ریپوی خواهر همین
    /// درس را از اول داشت (‎Updater.fromFile‎)، این یکی نداشت.
    /// </summary>
    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var current = AppVersion.Current;

        //  ⛔ اول سرورِ خودِ پمپ — جواب داد، همان.
        var server = ServerFeed();
        string serverWhy = "";
        if (!string.IsNullOrEmpty(server))
        {
            _lastStatus = 0;
            var (viaServer, why) = await FromApiAsync(server, current, ct);
            if (viaServer is not null) return viaServer;
            serverWhy = why;

            //  🚦 «آپدیت روی سرور باشد ولی تا خودم نخواهم به هیچ برنامه‌ای نرود»
            //  (۱۴۰۵/۰۷/۱۹). نرسیدن به سرور (قطعی، تایم‌اوت، ۵۰۰) دیگر راهِ
            //  گیت‌هاب را باز نمی‌کند — وگرنه پخشِ خاموشِ مدیر با یک قطعیِ
            //  کوتاه دور می‌خورد. فقط سرورِ کهنه‌ای که این مسیر را ندارد
            //  (۴۰۴) یا هنوز هیچ نسخه‌ای نگرفته (۵۰۳) سراغِ گیت‌هاب می‌فرستد.
            if (!FallbackAllowed(_lastStatus)) return Broken(current, serverWhy);
        }

        UpdateInfo? best = null;
        string bestFeed = FeedUrl, firstWhy = "";
        foreach (var feed in Feeds)
        {
            var (info, why) = await FromFeedAsync(feed, current, ct);
            if (info is null) { if (firstWhy.Length == 0) firstWhy = why; continue; }
            if (best is null || Compare(info.LatestVersion, best.LatestVersion) > 0)
            { best = info; bestFeed = feed; }
        }
        if (best is not null) return best;
        return Broken(current, serverWhy.Length > 0 ? serverWhy : firstWhy);
    }

    private int _lastStatus;

    /// <summary>
    /// جوابِ سرورِ پمپ اجازهٔ رفتن به راهِ دوم را می‌دهد؟ فقط ۴۰۴ (سرورِ کهنه که
    /// این مسیر را ندارد) و ۵۰۳ (هنوز هیچ نسخه‌ای نگرفته). ‎0‎ یعنی «اصلاً
    /// نرسیدیم» — و آن **نه**.
    /// </summary>
    internal static bool FallbackAllowed(int status) => status is 404 or 503;

    /// <summary>هر دو درِ یک مخزن: فهرستِ انتشار، و اگر نشد فایلِ متنی.</summary>
    private async Task<(UpdateInfo? Info, string Why)> FromFeedAsync(
        string feed, string current, CancellationToken ct)
    {
        var (viaApi, apiWhy) = await FromApiAsync(feed, current, ct);
        if (viaApi is not null) return (viaApi, "");
        var (viaFile, fileWhy) = await FromFileAsync(feed, current, ct);
        if (viaFile is not null) return (viaFile, "");
        return (null, apiWhy.Length > 0 ? apiWhy : fileWhy);
    }

    /// <summary>درِ اول. ‎null‎ یعنی «جواب به کار نیامد، درِ بعدی را بزن».</summary>
    private async Task<(UpdateInfo? Info, string Why)> FromApiAsync(
        string feed, string current, CancellationToken ct)
    {
        try
        {
            using var res = await GetAsync(feed, ct);
            _lastStatus = (int)res.StatusCode;
            if (!res.IsSuccessStatusCode)
                return (null, "سرورِ به‌روزرسانی این جواب را داد (کدِ " + (int)res.StatusCode + ")");

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var latest = NormalizeVersion(tag);
            // ⚠️ برچسبِ بی‌شماره (انتشارِ اپِ گوشی، یا هر برچسبِ چرخشی) یعنی
            // «این جواب مالِ برنامهٔ کامپیوتر نیست» — نه «تازه‌ای نیست».
            if (latest.Length == 0) return (null, "");

            string? url = null, fullUrl = null, sums = null;
            long size = 0, fullSize = 0;
            var fullIsSetup = false;
            var localBase = AppBase.LocalId;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (string.Equals(name, SumsName, StringComparison.OrdinalIgnoreCase))
                    {
                        sums = Resolve(feed, a.TryGetProperty("browser_download_url", out var su) ? su.GetString() : null);
                        continue;
                    }
                    if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                        && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                    var u = Resolve(feed, a.TryGetProperty("browser_download_url", out var uu) ? uu.GetString() : null);
                    var s = a.TryGetProperty("size", out var ss) ? ss.GetInt64() : 0;
                    if (u is null) continue;

                    // بستهٔ کوچک: فقط فایل‌های خودِ برنامه. نامش شناسهٔ «پایه»
                    // را با خود دارد؛ فقط وقتی به کار می‌آید که پایهٔ نصب‌شده
                    // دقیقاً همان باشد، وگرنه نیمی از فایل‌ها ناجور می‌شوند.
                    var baseId = AppBase.IdInAssetName(name);
                    if (baseId is not null)
                    {
                        if (localBase.Length > 0 && baseId == localBase) { url = u; size = s; }
                        continue;
                    }

                    // ⛔ بستهٔ کاملِ **معماریِ دیگر** به کارِ این نصب نمی‌آید.
                    //    از ۳.۱.۱۵۸ دو نصاب منتشر می‌شود (۶۴بیتی و ۳۲بیتی) و
                    //    بی این خط، نصبِ ۳۲بیتی می‌توانست نصابِ ۶۴بیتی را
                    //    بگیرد و پس از «به‌روزرسانی» برنامه‌ای داشته باشد که
                    //    ویندوزش اصلاً اجرایش نمی‌کند. بدترین شکلِ خرابی:
                    //    کاربر خودش این را خواسته بود.
                    if (!AppArch.Owns(name)) continue;

                    // بستهٔ کامل. نصاب بر زیپ ترجیح دارد: نصاب میان‌برها و
                    // ثبتِ «برنامه‌ها و قابلیت‌ها» را هم تازه می‌کند، ولی زیپ
                    // فقط فایل‌ها را جابه‌جا می‌کند. پس اگر نصاب در انتشار
                    // باشد، همان برداشته می‌شود — حتی اگر زیپ زودتر آمده باشد.
                    var isSetup = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                    if (fullUrl is null || (isSetup && !fullIsSetup))
                    {
                        fullUrl = u; fullSize = s; fullIsSetup = isSetup;
                    }
                }

            var small = url is not null;      // بستهٔ کوچکِ هم‌پایه پیدا شد
            if (url is null) { url = fullUrl; size = fullSize; }

            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            var newer = Compare(latest, current) > 0;
            if (newer && url is null) return (null, "");   // انتشار فایلی ندارد — درِ دوم
            return (new UpdateInfo(newer, current, latest, url, size, notes, small, "", sums), "");
        }
        catch (Exception e)
        {
            return (null, Why(e));
        }
    }

    /// <summary>
    /// نشانیِ نسبیِ سرورِ پمپ («/api/pump-updates/files/…») روی همان نشانی‌ای
    /// می‌نشیند که فهرست از آن آمد؛ نشانیِ کامل دست نمی‌خورد.
    /// </summary>
    private static string? Resolve(string feed, string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        //  ⚠️ «/…» را اول بسنج: روی لینوکس ‎Uri.TryCreate(Absolute)‎ آن را نشانیِ
        //  فایل (file://) می‌خواند.
        if (url.StartsWith('/') && !url.StartsWith("//"))
            return Uri.TryCreate(new Uri(feed), url, out var r) ? r.ToString() : null;
        return url;
    }

    /// <summary>
    /// درِ دوم: ‎version.txt‎ و ‎base.txt‎ی کنارِ بسته‌ها.
    ///
    /// ⚠️ اندازه را نمی‌داند و لازم هم ندارد: ‎DownloadAsync‎ اندازه را از
    /// خودِ پاسخ برمی‌دارد و فایلِ نیمه‌کاره را همان‌جا رد می‌کند.
    /// </summary>
    private async Task<(UpdateInfo? Info, string Why)> FromFileAsync(
        string feed, string current, CancellationToken ct)
    {
        try
        {
            var latest = NormalizeVersion(await TextAsync(feed, "version.txt", ct));
            if (latest.Length == 0) return (null, "شمارهٔ نسخهٔ تازه خوانده نشد");

            if (Compare(latest, current) <= 0)
                return (new UpdateInfo(false, current, latest, null, 0, null), "");

            // پایهٔ آن‌طرف همان پایهٔ این نصب است؟ آن‌وقت همان چند مگابایت بس است.
            //
            // ⚠️ و پایهٔ **هر معماری** فایلِ خودش را دارد: ۶۴بیتی همان
            //    `base.txt`ِ همیشگی (نصب‌های امروزیِ مشتری کدِ قدیمی دارند و
            //    فقط همین نام را می‌شناسند) و ۳۲بیتی `base-x86.txt`.
            //    فایلِ نبوده ⇒ رشتهٔ خالی ⇒ بستهٔ کامل، که **درست** است:
            //    هیچ‌وقت بستهٔ کوچکِ معماریِ دیگر برداشته نمی‌شود.
            var remoteBase = (await TextAsync(feed, AppArch.BaseFileName, ct)).Trim();
            var localBase = AppBase.LocalId;
            var small = remoteBase.Length > 0 && remoteBase == localBase;

            var url = FileUrl(feed, small ? "PumpYaqobi-app-" + remoteBase + ".zip" : AppArch.SetupName);
            return (new UpdateInfo(true, current, latest, url, 0, null, small, "", FileUrl(feed, SumsName)), "");
        }
        catch (Exception e)
        {
            return (null, Why(e));
        }
    }

    /// <summary>یک فایلِ متنیِ کوچک از انتشارِ چرخشی.</summary>
    private static async Task<string> TextAsync(string feed, string name, CancellationToken ct)
    {
        using var res = await GetAsync(FileUrl(feed, name), ct);
        if (!res.IsSuccessStatusCode) return "";
        var text = await res.Content.ReadAsStringAsync(ct);
        return text.Split('\n')[0].Trim();
    }

    /// <summary>
    /// ⛔ پیامِ خامِ استثنا به کاربر نمی‌رسد — ممکن است نام یا نشانیِ میزبان
    /// داشته باشد (همان قاعدهٔ چراغِ سرور). فقط **جنسِ** خرابی گفته می‌شود.
    ///
    /// ⚠️ و واژهٔ «سرور» از این جمله‌ها برداشته شد و جایش **گیت‌هاب** نشست.
    /// گزارشِ صاحب سامانه با عکس (۱۴۰۵/۰۷/۱۰): «مگه از سرور اپدیت
    /// می‌گرفت؟» و بعد «اپدیت از گیت‌هاب بگیره نه سرور». پیامِ قبلی
    /// «سرورِ به‌روزرسانی جواب نداد» بود و او آن را **سرورِ خانگیِ پمپ**
    /// خواند و رفت دنبالِ درست کردنِ چیزی که اصلاً خراب نبود.
    ///
    /// ⛔ این برنامه سه سرور دارد (خانگی · حساب · به‌روزرسانی) و کاربر قرار
    /// نیست از روی یک واژه حدس بزند کدام‌شان. پس جمله **نامِ جا را می‌گوید**.
    ///
    /// ⚠️ و این قاعدهٔ «نشانی در رابط نوشته نمی‌شود» را نمی‌شکند: آن قاعده
    /// مالِ **نامِ مخزن و نشانی** است (سنجه‌اش «vil3ntec» و «pump-staion» را
    /// در رشته‌های همین فایل قدغن کرده). نامِ خودِ گیت‌هاب نه نشانی است و
    /// نه راهی برای بردنِ برنامه به جای دیگر.
    /// </summary>
    private static string Why(Exception e) => e switch
    {
        TaskCanceledException or TimeoutException => "سرورِ به‌روزرسانی جواب نداد — وقت تمام شد",
        HttpRequestException => "به سرورِ به‌روزرسانی نرسیدیم — اینترنت وصل نیست یا سرور خاموش است",
        _ => "بررسیِ به‌روزرسانی انجام نشد",
    };

    /// <summary>
    /// جملهٔ «نشد» — با نسخهٔ نصب‌شده، و با این‌که **کجا** را باید نگاه کرد.
    ///
    /// ⛔ خطِ دوم لازم است، نه تزئین: تا وقتی نوشته نشود که به‌روزرسانی از
    /// اینترنت می‌آید، کاربر سراغِ سرورِ خانگی و چراغِ سربرگ می‌رود — همان
    /// کاری که یک بار واقعاً شد.
    /// </summary>
    private static UpdateInfo Broken(string current, string why) =>
        new(false, current, current, null, 0, null, false,
            (why.Length > 0 ? why : "بررسیِ به‌روزرسانی انجام نشد")
            + " — نسخهٔ نصب‌شده " + current + " است و معلوم نشد تازه‌تری هست یا نه."
            + "\n⚠️ به‌روزرسانی از سرورِ پمپ می‌آید؛ اگر نرسید، اینترنت و روشن بودنِ سرور را ببینید."
            + " بی اینترنت، «نصبِ نسخهٔ تازه از فایل» راهِ دیگر است.");

    // ══ پوشهٔ نصب ═══════════════════════════════════════════════════════════
    // کاربر خودش انتخاب می‌کند برنامه کجا نصب شود. اگر جایی را انتخاب کند که
    // نوشتن در آن اجازهٔ مدیر می‌خواهد (‎Program Files‎)، به‌روزرسانی باید
    // اجازه بگیرد — نه اینکه بی‌صدا هیچ نکند و کاربر خیال کند به‌روز شده.

    /// <summary>پوشه‌ای که فایل‌های برنامه در آن نشسته‌اند.</summary>
    public static string InstallDir =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>آیا می‌شود بی اجازهٔ مدیر در پوشهٔ نصب نوشت؟</summary>
    public static bool InstallDirWritable => IsWritable(InstallDir);

    /// <summary>
    /// آیا می‌شود بی اجازهٔ مدیر در این پوشه نوشت؟ با نوشتنِ واقعیِ یک فایلِ
    /// کوچک سنجیده می‌شود، نه با نگاه به مسیر — چون اجازه‌ها را می‌شود دستی
    /// عوض کرد و مسیر تنها چیزی را ثابت نمی‌کند.
    /// </summary>
    public static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".pump-write-test-" + Guid.NewGuid().ToString("N")[..8]);
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch { return false; }
    }

    //  ⛔ «باز کردنِ صفحهٔ دانلود» برداشته شد (۱۴۰۵/۰۷/۱۶، خواستهٔ صاحب ریپو):
    //  کاربر را مستقیم به صفحهٔ مخزن در گیت‌هاب می‌برد — «هیچ کس ادرس رو
    //  نبینه». راهِ بیرونِ بی‌اینترنت همان «نصب از فایل» است. برنگردانیدش.

    /// <summary>مسیرِ فایلی که نتیجهٔ آخرین جای‌گزینی در آن نوشته می‌شود.</summary>
    private static string ResultFile =>
        Path.Combine(Services.AppSettings.Dir, "updates", "apply-result.txt");

    /// <summary>
    /// نسخه‌ای که آخرین بار «قرار بود» نصب شود. بعد از باز شدنِ دوبارهٔ
    /// برنامه، همین با نسخهٔ واقعی سنجیده می‌شود.
    /// </summary>
    private static string PendingFile =>
        Path.Combine(Services.AppSettings.Dir, "updates", "pending.txt");

    /// <summary>
    /// ══ «قرار است به این نسخه برویم» ═══════════════════════════════════════
    /// پیش از هر تلاشی نوشته می‌شود. تنها راهِ فهمیدنِ اینکه به‌روزرسانی
    /// **واقعاً** گرفت یا نه، همین است: بعد از باز شدنِ دوباره، نسخهٔ در حالِ
    /// اجرا با این عدد سنجیده می‌شود.
    /// </summary>
    public static void MarkPending(string targetVersion)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PendingFile)!);
            File.WriteAllText(PendingFile, targetVersion);
        }
        catch { /* ننوشتنش نباید جلوی خودِ به‌روزرسانی را بگیرد */ }
    }

    /// <summary>
    /// نتیجهٔ آخرین به‌روزرسانی: گرفت یا نگرفت، و چه بگوییم.
    /// ‎null‎ یعنی اصلاً به‌روزرسانی‌ای در کار نبوده.
    /// </summary>
    public sealed record Outcome(bool Ok, string Message);

    /// <summary>
    /// ══ واقعاً به‌روز شد؟ ══════════════════════════════════════════════════
    /// برنامه هنگامِ باز شدن این را می‌پرسد.
    ///
    /// ⚠️ سنجش با **نسخه** است، نه با کدِ خروجیِ جای‌گزینی. دلیلش یک سوراخِ
    /// واقعی بود: کدِ خروجی را فقط مسیرِ زیپ می‌نوشت. اگر به‌روزرسانی از راهِ
    /// نصاب (‎.exe‎) می‌رفت و شکست می‌خورد — کاربر پنجرهٔ اجازهٔ مدیر را رد
    /// می‌کرد، یا نصابِ بی‌صدا نمی‌توانست در ‎Program Files‎ بنویسد — هیچ
    /// فایلی نوشته نمی‌شد، برنامه با نسخهٔ کهنه باز می‌شد و **هیچ نمی‌گفت**.
    /// یعنی دقیقاً همان چیزی که قرار بود جلویش گرفته شود: کاربر خیال می‌کرد
    /// به‌روز شده است.
    ///
    /// مقایسهٔ نسخه هر دو مسیر را با هم می‌پوشاند و به هیچ جزئیاتِ درونیِ
    /// نصاب یا اسکریپت بند نیست.
    /// </summary>
    public static Outcome? ConsumeLastResult()
    {
        try
        {
            var pendingPath = PendingFile;
            if (!File.Exists(pendingPath)) return null;

            var target = File.ReadAllText(pendingPath).Trim();
            File.Delete(pendingPath);

            // کدِ خروجیِ اسکریپتِ زیپ، اگر بود — فقط برای پیامِ دقیق‌تر
            var code = "";
            try
            {
                if (File.Exists(ResultFile)) { code = File.ReadAllText(ResultFile).Trim(); File.Delete(ResultFile); }
            }
            catch { }

            if (target.Length == 0) return null;

            // گرفت؟ نسخهٔ در حالِ اجرا باید به هدف رسیده باشد (یا از آن جلوتر)
            if (Compare(AppVersion.Current, target) >= 0)
                return new Outcome(true, "✅ برنامه به نسخهٔ " + AppVersion.Current + " به‌روز شد");

            var why = code.Length > 0 && code != "0"
                ? " (فایل‌ها جای‌گزین نشدند)"
                : "";

            return new Outcome(false,
                "به‌روزرسانی کامل نشد" + why + " — برنامه هنوز روی نسخهٔ "
                + AppVersion.Current + " است، نه " + target + ". "
                + "اگر برنامه در پوشه‌ای نصب است که اجازهٔ مدیر می‌خواهد، "
                + "یک‌بار برنامه را «به‌عنوان مدیر» باز کنید و دوباره بزنید.");
        }
        catch { return null; }
    }

    /// <summary>«v2.9.430» یا «2.9.430» → «2.9.430».</summary>
    private static string NormalizeVersion(string tag)
    {
        var s = (tag ?? "").Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
        return Version.TryParse(s, out _) ? s : "";
    }

    /// <summary>مقایسهٔ نسخه — نه رشته‌ای، وگرنه «2.9.9» بزرگ‌تر از «2.9.10» می‌شد.</summary>
    public static int Compare(string a, string b)
    {
        if (!Version.TryParse(a, out var va)) return 0;
        if (!Version.TryParse(b, out var vb)) return 0;
        return va.CompareTo(vb);
    }

    /// <summary>
    /// گرفتنِ بستهٔ نصب. پیشرفت گزارش می‌شود تا کاربر بداند چه خبر است، و
    /// فایل فقط وقتی «آماده» شمرده می‌شود که کاملاً و به همان اندازه رسیده باشد.
    /// </summary>
    public async Task<string?> DownloadAsync(UpdateInfo info, IProgress<double>? progress,
                                             CancellationToken ct = default)
    {
        // ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد. تا امروز می‌داد، و فرمانِ صفحه هم
        // ‎catch‎ نداشت: یک قطعیِ وسطِ دانلود، یک ضدِ ویروسی که فایلِ ‎.part‎ را
        // قفل کند، یا دیسکِ پر ⇒ استثنا از ‎AsyncRelayCommand‎ بیرون می‌زد و
        // کاربر **هیچ پیامی** نمی‌دید — همان «دانلود هم نیست».
        try { return await GetPackageAsync(info, progress, ct); }
        catch { return null; }
    }

    private async Task<string?> GetPackageAsync(UpdateInfo info, IProgress<double>? progress,
                                                CancellationToken ct)
    {
        if (!info.Available || info.DownloadUrl is null) return null;

        //  ══ ۱) نشانی از جای شناخته‌شده است؟ ═════════════════════════════════
        //  ⛔ فایلی که گرفته می‌شود همان لحظه **اجرا** می‌شود. پس فقط https و
        //  فقط میزبان‌های خودِ گیت‌هاب — یک نشانیِ ساختگی در پاسخِ فهرست
        //  (یا یک پروکسیِ دست‌کار) نباید برنامه را به هر جای دیگری ببرد.
        if (!AllowedUrl(info.DownloadUrl))
        {
            LastProblem = "نشانیِ بستهٔ به‌روزرسانی از جای شناخته‌شده‌ای نیست — برای امنیت گرفته نشد.";
            return null;
        }

        //  ══ ۲) چک‌سامِ منتشرشده — **پیش از** دانلودِ بسته ═════════════════
        //  ⛔ تا ۱۴۰۵/۰۷/۱۲ فقط اندازه سنجیده می‌شد، و اندازه چیزی را ثابت
        //  نمی‌کند. CI از قبل `SHA256SUMS.txt` منتشر می‌کرد؛ حالا نبودنش یا
        //  جور نبودنش یعنی **نصب نمی‌شود** — با یک جملهٔ آدمیزاد، بی نامِ
        //  هیچ میزبانی.
        var expectedHash = await ExpectedHashAsync(info, ct);
        if (expectedHash is null) return null;           // `LastProblem` را خودش نوشته

        var dir = Path.Combine(Services.AppSettings.Dir, "updates");
        Directory.CreateDirectory(dir);
        var name = "PumpYaqobi-" + info.LatestVersion + Path.GetExtension(new Uri(info.DownloadUrl).AbsolutePath);
        var path = Path.Combine(dir, name);
        var partial = path + ".part";

        // ⚠️ اندازهٔ مورد انتظار: درِ دوم اندازه را نمی‌داند، پس از خودِ پاسخ
        // برداشته می‌شود — وگرنه نگهبانِ «فایلِ نیمه‌کاره» آن مسیر را بی‌اثر
        // رد می‌کرد و یک دانلودِ بریده روی برنامه می‌نشست.
        long expected = info.SizeBytes;

        using (var res = await SendAsync(new HttpRequestMessage(HttpMethod.Get, info.DownloadUrl),
                                        HttpCompletionOption.ResponseHeadersRead, ct))
        {
            if (!res.IsSuccessStatusCode) return null;
            //  ⚠️ دانلودِ گیت‌هاب به میزبانِ دیگری تغییرِ مسیر می‌دهد؛ جای
            //  **نهایی** هم باید در همان فهرست باشد.
            if (res.RequestMessage?.RequestUri is { } landed && !AllowedUrl(landed.ToString()))
            {
                LastProblem = "بستهٔ به‌روزرسانی از جای ناشناخته‌ای آمد — برای امنیت کنار گذاشته شد.";
                return null;
            }
            var total = res.Content.Headers.ContentLength ?? info.SizeBytes;
            if (expected <= 0) expected = res.Content.Headers.ContentLength ?? 0;
            await using var src = await res.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(partial);

            var buf = new byte[81920];
            long done = 0;
            int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report(done * 100.0 / total);
            }
        }

        // ⚠️ فایلِ نیمه‌کاره هرگز جای فایلِ نهایی را نمی‌گیرد
        if (expected > 0 && new FileInfo(partial).Length != expected)
        {
            try { File.Delete(partial); } catch { }
            return null;
        }

        // ⛔ و فایلِ کامل هم فقط وقتی می‌ماند که چک‌سامش بخورد
        if (!string.Equals(HashOf(partial), expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(partial); } catch { }
            LastProblem = "فایلِ گرفته‌شده با چک‌سامِ منتشرشده جور نیست — دانلود خراب شده یا دست‌کاری شده؛ نصب نمی‌شود.";
            return null;
        }

        if (File.Exists(path)) File.Delete(path);
        File.Move(partial, path);
        //  ⚠️ هشِ مورد انتظار کنارِ بسته می‌ماند تا `Launch` درست پیش از اجرا
        //  **دوباره** بسنجد — بینِ دانلود و نصب ممکن است دقیقه‌ها بگذرد.
        File.WriteAllText(path + HashSuffix, expectedHash);
        return path;
    }

    // ══ چک‌سام و امضا ════════════════════════════════════════════════════════

    private const string SumsName = "SHA256SUMS.txt";
    private const string HashSuffix = ".sha256";

    /// <summary>
    /// چرا آخرین دانلود نشد — یک جملهٔ فارسیِ آمادهٔ نمایش، بی نامِ هیچ
    /// میزبانی. خالی یعنی دلیلِ خاصی ثبت نشد.
    /// </summary>
    public string LastProblem { get; private set; } = "";

    /// <summary>
    /// میزبان‌هایی که بستهٔ به‌روزرسانی از آن‌ها پذیرفته می‌شود: خودِ گیت‌هاب
    /// و دو میزبانی که دانلودِ انتشار به آن‌ها تغییرِ مسیر می‌دهد.
    /// </summary>
    private static readonly string[] AllowedHosts =
    {
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
        "github-releases.githubusercontent.com",
    };

    /// <summary>فقط https، فقط همان میزبان‌ها، فقط درگاهِ پیش‌فرض.</summary>
    public static bool AllowedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == Uri.UriSchemeHttps
        && u.IsDefaultPort
        && string.IsNullOrEmpty(u.UserInfo)
        && (Array.IndexOf(AllowedHosts, u.IdnHost.ToLowerInvariant()) >= 0
            //  ⛔ و سرورِ خودِ پمپ — همان یک نشانیِ قفل‌شدهٔ ‎CloudConfig‎
            || string.Equals(u.IdnHost, ServerHost, StringComparison.OrdinalIgnoreCase));

    private static readonly string ServerHost = new Uri(Services.CloudConfig.Url("/")).IdnHost;

    /// <summary>
    /// کلیدِ عمومیِ امضای <c>SHA256SUMS.txt</c> (SPKIِ base64، P-256) — از
    /// <c>[assembly: AssemblyMetadata("UpdateKey")]</c> که ساختِ CI می‌گذارد.
    /// خالی ⇒ فقط هش (رفتارِ ساختِ بی‌کلید).
    /// </summary>
    public static string UpdateKey => UpdateKeyOverride ?? Services.CloudConfig.Metadata("UpdateKey");

    /// <summary>⚠️ فقط برای آزمون — همان الگوی <see cref="TestTransport"/>.</summary>
    public static string? UpdateKeyOverride { get; set; }

    /// <summary>
    /// هشِ بسته از فهرستِ منتشرشده — و اگر کلیدِ امضا در برنامه هست، امضای
    /// همان فهرست هم. <c>null</c> ⇒ نصب نمی‌شود و <see cref="LastProblem"/>
    /// می‌گوید چرا.
    /// </summary>
    private async Task<string?> ExpectedHashAsync(UpdateInfo info, CancellationToken ct)
    {
        var sumsUrl = string.IsNullOrWhiteSpace(info.SumsUrl) ? Sibling(info.DownloadUrl!, SumsName) : info.SumsUrl!;
        if (!AllowedUrl(sumsUrl))
        {
            LastProblem = "فهرستِ چک‌سامِ این نسخه از جای شناخته‌شده‌ای نیست — نصب نمی‌شود.";
            return null;
        }

        var sums = await BytesAsync(sumsUrl, 256 * 1024, ct);
        if (sums is null || sums.Length == 0)
        {
            LastProblem = "فهرستِ چک‌سامِ این نسخه پیدا نشد — برای امنیت نصب نمی‌شود. "
                        + "کمی بعد دوباره «بررسی» را بزنید.";
            return null;
        }

        var key = UpdateKey;
        if (key.Length > 0)
        {
            var sig = await BytesAsync(sumsUrl + ".sig", 4096, ct);
            if (sig is null || !SignatureOk(sums, sig, key))
            {
                LastProblem = "امضای فهرستِ چک‌سامِ این نسخه درست نیست — برای امنیت نصب نمی‌شود.";
                return null;
            }
        }

        var asset = Uri.UnescapeDataString(new Uri(info.DownloadUrl!).Segments[^1]);
        var hash = HashFromSums(System.Text.Encoding.UTF8.GetString(sums), asset);
        if (hash is null)
        {
            LastProblem = "این بسته در فهرستِ چک‌سامِ همان نسخه نیست — برای امنیت نصب نمی‌شود.";
            return null;
        }
        return hash;
    }

    /// <summary>«…/tag/x.zip» ⇒ «…/tag/<paramref name="name"/>».</summary>
    private static string Sibling(string url, string name)
    {
        var u = new Uri(url);
        return new Uri(u, name).ToString();
    }

    /// <summary>بایت‌های یک فایلِ کوچک — <c>null</c> اگر نبود یا بیش از سقف بود.</summary>
    private static async Task<byte[]?> BytesAsync(string url, int cap, CancellationToken ct)
    {
        using var res = await GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return null;
        if (res.RequestMessage?.RequestUri is { } landed && !AllowedUrl(landed.ToString())) return null;
        var b = await res.Content.ReadAsByteArrayAsync(ct);
        return b.Length > cap ? null : b;
    }

    /// <summary>
    /// خطِ همان فایل در <c>SHA256SUMS.txt</c> — شکلِ <c>sha256sum</c>:
    /// «<c>hash  name</c>» یا «<c>hash *name</c>».
    /// </summary>
    public static string? HashFromSums(string text, string assetName)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r');
            if (line.Length < 66) continue;
            var sp = line.IndexOf(' ');
            if (sp != 64) continue;
            var hash = line[..64];
            var name = line[64..].TrimStart(' ', '*').Trim();
            if (!string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!hash.All(Uri.IsHexDigit)) return null;
            return hash.ToLowerInvariant();
        }
        return null;
    }

    /// <summary>
    /// ES256 روی بایت‌های <b>دقیقِ</b> فهرست. هم امضای خامِ P1363 (۶۴ بایت) و
    /// هم DERِ خروجیِ <c>openssl dgst -sign</c> پذیرفته می‌شود.
    /// </summary>
    public static bool SignatureOk(byte[] data, byte[] sig, string spkiB64)
    {
        try
        {
            using var ec = System.Security.Cryptography.ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(spkiB64), out _);
            var fmt = sig.Length == 64
                ? System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation
                : System.Security.Cryptography.DSASignatureFormat.Rfc3279DerSequence;
            return ec.VerifyData(data, sig, System.Security.Cryptography.HashAlgorithmName.SHA256, fmt);
        }
        catch { return false; }
    }

    private static string HashOf(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fs)).ToLowerInvariant();
    }

    /// <summary>
    /// ⚠️ فقط برای آزمون — به‌جای اجرای واقعیِ نصاب. همان الگوی
    /// <see cref="TestTransport"/>.
    /// </summary>
    public static Func<System.Diagnostics.ProcessStartInfo, bool>? TestStart { get; set; }

    private static void Start(System.Diagnostics.ProcessStartInfo psi)
    {
        if (TestStart is { } hook) { if (!hook(psi)) throw new InvalidOperationException(); return; }
        System.Diagnostics.Process.Start(psi);
    }

    /// <summary>
    /// نصبِ نسخهٔ گرفته‌شده و راه‌اندازیِ دوباره.
    ///
    /// دو حالت دارد:
    ///   • ‎.exe‎ — نصاب است، بی‌صدا اجرا می‌شود (خودش برنامه را می‌بندد،
    ///     جایگزین می‌کند و باز می‌کند).
    ///   • ‎.zip‎ — بستهٔ بی‌نصب. کنارِ برنامه باز می‌شود و یک دستورِ کوچک
    ///     می‌نویسیم که صبر کند تا برنامه بسته شود، فایل‌های تازه را جای
    ///     فایل‌های کهنه بگذارد و دوباره برنامه را باز کند.
    ///
    /// ⚠️ خودِ برنامه نمی‌تواند فایل‌های در حالِ اجرای خودش را جابه‌جا کند —
    /// برای همین کار به آن دستورِ بیرونی سپرده می‌شود و برنامه بلافاصله بسته
    /// می‌شود. اگر بستن را فراموش کنید، جابه‌جایی شکست می‌خورد.
    ///
    /// ⛔ <b>و نصاب همیشه با <c>/DIR</c> صدا زده می‌شود</b> — نبودش یک خرابیِ
    /// واقعی بود: <c>UsePreviousAppDir=yes</c> پوشه را از <b>ثبتِ نصبِ
    /// پیشین</b> برمی‌دارد، و نصبی که کاربر خودش از زیپ باز کرده هیچ ثبتی
    /// ندارد. پس نصابِ بی‌صدا می‌رفت در
    /// <c>%LocalAppData%\Programs\PumpYaqobi</c> می‌نشست — یک پوشهٔ
    /// <b>دیگر</b> — و نسخهٔ در حالِ اجرا (مثلاً <c>D:\…\PumpYaqobi</c>)
    /// دست‌نخورده می‌ماند. کاربر برنامه را باز می‌کرد، همان نسخهٔ کهنه را
    /// می‌دید و به حق می‌گفت «به‌روز نمی‌شود».
    /// </summary>
    public static bool Launch(string packagePath, string? targetVersion = null)
    {
        //  ⛔ **درست پیش از اجرا، دوباره** — و فایل تا لحظهٔ شروعِ نصب باز و
        //  بی‌اجازهٔ نوشتن (`FileShare.Read`) می‌ماند. بینِ دانلود و «نصب»
        //  ممکن است دقیقه‌ها بگذرد و پوشهٔ `updates` مالِ کاربر است؛ هر چیزی
        //  که در آن فاصله جایش نشسته باشد، هیچ‌وقت اجرا نمی‌شود.
        FileStream? held = null;
        try
        {
            var want = File.Exists(packagePath + HashSuffix)
                ? File.ReadAllText(packagePath + HashSuffix).Trim()
                : "";
            if (want.Length != 64) return false;
            held = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var got = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(held)).ToLowerInvariant();
            if (!string.Equals(got, want, StringComparison.OrdinalIgnoreCase)) return false;
            held.Position = 0;

            // ⚠️ پیش از هر کاری: «قرار است به این نسخه برویم». اگر این تلاش
            // بگیرد، دفعهٔ بعد که برنامه باز شود خودش می‌فهمد؛ و اگر نگیرد،
            // به کاربر گفته می‌شود به‌جای آنکه بی‌صدا روی نسخهٔ کهنه بماند.
            if (!string.IsNullOrWhiteSpace(targetVersion)) MarkPending(targetVersion!);

            if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return LaunchZip(packagePath, held);

            // ── نصاب ──
            // ‎/SILENT‎ تا کاربر وسطِ به‌روزرسانی با پنجرهٔ ویزارد روبه‌رو نشود؛
            // نصابِ ما ‎CloseApplications=yes‎ دارد، پس خودش برنامهٔ باز را
            // می‌بندد، فایل‌ها را عوض می‌کند و دوباره بازش می‌کند.
            // (بارِ اول که کاربر خودش ‎setup.exe‎ را می‌زند، بی‌آرگومان اجرا
            //  می‌شود و ویزارد کامل را می‌بیند — این مسیر فقط به‌روزرسانی است.)
            // ⛔ ‎/DIR‎ لازم است — شرحش بالای همین متد.
            var psi = new System.Diagnostics.ProcessStartInfo(packagePath)
            {
                //  ⛔ ‎/RELAUNCH=1‎ — تنها چیزی که نصاب را وامی‌دارد پس از نصبِ بی‌صدا
                //  برنامه را دوباره باز کند (‎WantRelaunch‎ در ‎PumpYaqobi.iss‎).
                Arguments = "/SILENT /NORESTART /RESTARTAPPLICATIONS /RELAUNCH=1"
                          + " " + AppArch.ArchArg
                          + " /DIR=\"" + InstallDir + "\"",
                UseShellExecute = true,
            };

            // ⚠️ اگر پوشهٔ نصب اجازهٔ مدیر بخواهد (‎Program Files‎)، نصابِ
            // بی‌صدا **نمی‌تواند** خودش پنجرهٔ اجازه را بالا بیاورد و کارش
            // نیمه‌کاره می‌ماند. مسیرِ زیپ این را از اول رعایت می‌کرد و این
            // مسیر نه — پس به‌روزرسانیِ نصابی در چنان پوشه‌ای همیشه شکست
            // می‌خورد، بی آنکه کسی بفهمد.
            if (!InstallDirWritable)
            {
                psi.Verb = "runas";
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            }

            Start(psi);
            return true;
        }
        catch
        {
            // کاربر پنجرهٔ اجازهٔ مدیر را رد کرد، یا نصاب اصلاً بالا نیامد
            return false;
        }
        finally { held?.Dispose(); }
    }

    /// <summary>
    /// ══ «به‌روزرسانی از فایل» — فایلِ نصبی که کاربر از فلش برگزیده ══════════
    ///
    /// برخلافِ <see cref="Launch"/> <b>بی‌صدا نیست</b>: کاربر خودش فایل را
    /// آورده، پس ویزاردِ نصاب را می‌بیند (خوش‌آمدش می‌گوید «نسخهٔ X نصب است و
    /// به Y به‌روز می‌شود»). ⛔ و فقط پس از <see cref="OfflineInstaller.Inspect"/>
    /// صدا زده می‌شود — نصاب خودش هم کهنه‌تر را رد می‌کند.
    ///
    /// ⚠️ <c>/DIR</c> همان پوشهٔ همین برنامه است (همان دلیلِ <see cref="Launch"/>:
    /// نصبی که از زیپ باز شده هیچ ثبتی ندارد و نصاب جای دیگری می‌نشست)، و
    /// <c>/ARCH</c> همان معماریِ همین برنامه — صفحهٔ انتخاب با همان پیش‌فرض باز
    /// می‌شود.
    /// </summary>
    public static bool LaunchOffline(string setupPath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(setupPath)
            {
                Arguments = AppArch.ArchArg + " /DIR=\"" + InstallDir + "\"",
                UseShellExecute = true,
            };
            if (!InstallDirWritable) psi.Verb = "runas";
            Start(psi);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// جای‌گزینیِ فایل‌ها با بستهٔ زیپ.
    ///
    /// اگر پوشهٔ نصب اجازهٔ نوشتن ندهد (کاربر برنامه را در ‎Program Files‎
    /// گذاشته)، همان دستور با اجازهٔ مدیر اجرا می‌شود. و نتیجه‌اش — چه موفق
    /// چه نه — در فایلی نوشته می‌شود که برنامه هنگامِ باز شدنِ بعدی می‌خواند.
    /// </summary>
    /// <summary>
    /// ══ «پس از به‌روزرسانی دوباره باز نمی‌شود» — ۱۴۰۵/۰۷/۱۸ ══════════════════
    ///
    /// ⛔ <c>--after-update</c>: برنامهٔ تازه تا ۶۰ ثانیه منتظرِ بسته شدنِ کاملِ
    /// نمونهٔ کهنه می‌ماند (<see cref="SingleInstance.Acquire(TimeSpan)"/>)، نه این‌که
    /// قفل را گرفته ببیند و همان لحظه بیرون برود — همان چیزی که روی کامپیوترِ کند
    /// (یا جایی که ‎tasklist‎ کار نمی‌کند) برنامه را بسته می‌گذاشت.
    /// ⛔ اسکریپتِ بالابرده (پوشهٔ ‎Program Files‎) برنامه را با ‎explorer.exe‎ باز
    /// می‌کند تا با <b>کاربرِ خودش</b> بالا بیاید، نه با مدیر؛ و ‎/D‎ پوشهٔ کار را
    /// درست می‌گذارد (با ‎runas‎ پوشهٔ کار ‎System32‎ است).
    /// </summary>
    internal static string RelaunchLine(string exe, string appDir, bool elevated) =>
        elevated
            ? $"explorer.exe \"{exe}\""
            : $"start \"\" /D \"{appDir}\" \"{exe}\" --after-update";

    private static bool LaunchZip(string zipPath, Stream verified)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return false;
        var appDir = Path.GetDirectoryName(exe)!;

        // بسته در یک پوشهٔ کنارِ فایلِ زیپ باز می‌شود، نه روی خودِ برنامه
        var staging = Path.Combine(Path.GetDirectoryName(zipPath)!, "staging");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        //  ⚠️ از **همان** جریانی باز می‌شود که همین حالا سنجیده شد، نه از
        //  نامِ فایل — تا بایت‌های سنجیده همان بایت‌های بازشده باشند.
        using (var zip = new System.IO.Compression.ZipArchive(verified, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
            System.IO.Compression.ZipFileExtensions.ExtractToDirectory(zip, staging);

        // بعضی بسته‌ها یک پوشهٔ تکیِ بیرونی دارند — همان پوشه منبع است
        var top = Directory.GetDirectories(staging);
        var src = top.Length == 1 && Directory.GetFiles(staging).Length == 0 ? top[0] : staging;

        var pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetDirectoryName(zipPath)!, "apply-update.cmd");
        var writable = IsWritable(appDir);
        var relaunch = RelaunchLine(exe, appDir, elevated: !writable);
        var result = ResultFile;
        Directory.CreateDirectory(Path.GetDirectoryName(result)!);
        // نشانهٔ کهنه پاک شود تا نتیجهٔ همین بار خوانده شود، نه نتیجهٔ دفعهٔ پیش
        try { if (File.Exists(result)) File.Delete(result); } catch { }

        // ‎robocopy‎ کدِ خروجیِ ۰ تا ۷ را «موفق» می‌شمارد و ۸ به بالا یعنی
        // شکست؛ پس همان عدد نوشته می‌شود و اسکریپت با ‎exit /b 0‎ بسته می‌شود.
        File.WriteAllText(script, $"""
            @echo off
            chcp 65001 >nul
            set N=0
            :wait
            tasklist /FI "PID eq {pid}" 2>nul | find "{pid}" >nul
            if not errorlevel 1 (
              set /a N+=1
              if %N% GEQ 120 goto go
              timeout /t 1 /nobreak >nul
              goto wait
            )
            :go
            timeout /t 1 /nobreak >nul
            robocopy "{src}" "{appDir}" /E /IS /IT /R:10 /W:2 >nul
            set RC=%ERRORLEVEL%
            if %RC% GEQ 8 (>"{result}" echo %RC%) else (del "{result}" >nul 2>&1)
            {relaunch}
            rmdir /s /q "{staging}" >nul 2>&1
            exit /b 0
            """, new System.Text.UTF8Encoding(false));      // ⛔ بی BOM — وگرنه خطِ اول «@echo off» خطا می‌داد

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            //  ⛔ ‎/s‎ و گیومهٔ دوتایی (۱۴۰۵/۰۷/۱۶): بی آن، پوشه‌ای با «( ) & @ ^» (مثلاً
            //  نامِ کاربرِ «Ali (Work)» در مسیرِ نصب) گیومه را می‌شکست و به‌روزرسانی
            //  برنامه را می‌بست و هیچ فایلی جا به جا نمی‌کرد.
            Arguments = "/s /c \"\"" + script + "\"\"",
            WorkingDirectory = appDir,
        };

        if (writable)
        {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
        }
        else
        {
            // پوشهٔ نصب اجازهٔ مدیر می‌خواهد — بی این، ‎robocopy‎ بی‌صدا
            // شکست می‌خورد و برنامه با نسخهٔ کهنه باز می‌شود.
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
        }

        try { Start(psi); }
        catch
        {
            // کاربر پنجرهٔ اجازهٔ مدیر را رد کرد
            try { File.WriteAllText(result, "8"); } catch { }
            return false;
        }
        return true;
    }
}

/// <summary>نسخهٔ همین ساخت — از خودِ اسمبلی خوانده می‌شود، نه دستی.</summary>
public static class AppVersion
{
    public static string Current
    {
        get
        {
            var v = typeof(AppVersion).Assembly.GetName().Version;
            return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}

/// <summary>
/// ══ معماریِ همین نصب ═══════════════════════════════════════════════════════
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «۳۲ بیت، ۶۴ بیت و ۸۶ بیت، چون کامپیوتر
/// خیلی نسخه قدیمی است.»
///
/// ⚠️ «۸۶ بیت» وجود ندارد — <c>x86</c> **همان ۳۲بیتی** است. پس دو مدل است،
/// نه سه، و این کلاس فقط می‌گوید همین پروسه کدام‌شان است.
///
/// ⛔ <see cref="RuntimeInformation.ProcessArchitecture"/>، نه
/// <c>OSArchitecture</c>: برنامهٔ ۳۲بیتی روی ویندوزِ ۶۴بیتی هم اجرا می‌شود و
/// آن‌جا باید بستهٔ **۳۲بیتی** را بگیرد، نه آن‌چه سیستم‌عامل است.
/// </summary>
public static class AppArch
{
    /// <summary>جای معماری برای آزمون‌ها — همان الگوی <c>AppBase.LocalIdOverride</c>.</summary>
    public static string? Override { get; set; }

    private static readonly string[] Known = { "x64", "x86", "arm64" };

    /// <summary>«x64» · «x86» · «arm64».</summary>
    public static string Id => Override ?? (RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X86 => "x86",
        Architecture.Arm64 => "arm64",
        _ => "x64",
    });

    /// <summary>
    /// ۶۴بیتی، یعنی همان معماری‌ای که نامِ فایل‌هایش **پسوند ندارد**.
    /// ⛔ نامِ فایل‌های ۶۴بیتی هیچ‌وقت عوض نمی‌شود: هر نصبی که همین حالا دستِ
    /// مشتری است کدِ قدیمی دارد و فقط <c>PumpYaqobi-Setup.exe</c> و
    /// <c>base.txt</c> را می‌شناسد.
    /// </summary>
    public static bool IsDefault => Id == "x64";

    /// <summary>
    /// نامِ نصاب در انتشار — <b>یکی برای هر دو معماری</b> (۱۴۰۵/۰۷/۱۴): بارِ
    /// ۳۲ و ۶۴بیتی هر دو داخلِ همین فایل‌اند و <see cref="ArchArg"/> می‌گوید
    /// کدام نصب شود. ⛔ این نام هیچ‌وقت عوض نمی‌شود — نصب‌های امروزی همین را
    /// می‌شناسند.
    /// </summary>
    public const string UnifiedSetup = "PumpYaqobi-Setup.exe";

    /// <summary>همان <see cref="UnifiedSetup"/> — برای هر معماری.</summary>
    public static string SetupName => UnifiedSetup;

    /// <summary>
    /// آرگومانِ نصابِ بی‌صدا: کدام بار بنشیند. بی این، نصاب از رجیستریِ نصبِ
    /// پیشین می‌خواند — ولی گفتنِ صریح یعنی به‌روزرسانی هیچ‌وقت معماریِ برنامهٔ
    /// در حالِ اجرا را عوض نمی‌کند، حتی اگر رجیستری دست خورده باشد.
    /// </summary>
    public static string ArchArg => "/ARCH=" + Id;

    /// <summary>نامِ فایلِ شناسهٔ پایهٔ همین معماری روی برچسبِ چرخشی.</summary>
    public static string BaseFileName => IsDefault ? "base.txt" : "base-" + Id + ".txt";

    /// <summary>
    /// این فایلِ انتشار مالِ همین معماری است؟ پسوندِ معماری در نام تصمیم
    /// می‌گیرد؛ نامِ بی‌پسوند همان ۶۴بیتیِ همیشگی است.
    /// </summary>
    public static bool Owns(string assetName)
    {
        //  یک نصاب، هر دو بار — مالِ همه است
        if (string.Equals(assetName, UnifiedSetup, StringComparison.OrdinalIgnoreCase)) return true;
        var stem = Path.GetFileNameWithoutExtension(assetName);
        foreach (var a in Known)
            if (stem.EndsWith("-" + a, StringComparison.OrdinalIgnoreCase))
                return string.Equals(a, Id, StringComparison.OrdinalIgnoreCase);
        return IsDefault;
    }
}

/// <summary>
/// ══ «پایه»ی نصب ═════════════════════════════════════════════════════════════
/// بستهٔ کامل ۵۷ مگابایت است، ولی از هر ساخت به ساختِ بعدی معمولاً فقط دو
/// مگابایتش عوض می‌شود: خودِ فایل‌های برنامه. بقیه — خودِ دات‌نت، اِوالونیا،
/// اسکیا و بقیهٔ کتابخانه‌ها — همان‌اند و تا وقتی نسخهٔ بسته‌ها عوض نشود
/// دست‌نخورده می‌مانند.
///
/// پس هر ساخت یک «شناسهٔ پایه» دارد که از همان فایل‌های ثابت ساخته می‌شود و
/// در فایلِ ‎base.id‎ کنارِ برنامه می‌نشیند. بستهٔ کوچک هم همان شناسه را در
/// نامش دارد. برنامه فقط وقتی بستهٔ کوچک را می‌گیرد که دو شناسه یکی باشند؛
/// اگر پایه عوض شده باشد، خودبه‌خود می‌رود سراغِ بستهٔ کامل.
///
/// ⚠️ بستهٔ کوچک عمداً ‎base.id‎ ندارد — یعنی شناسهٔ روی دیسک همان می‌ماند و
/// درست هم همین است: پایه که عوض نشده.
/// </summary>
public static class AppBase
{
    /// <summary>شناسهٔ پایهٔ همین نصب. خالی یعنی «نمی‌دانم» → بستهٔ کامل.</summary>
    /// <summary>
    /// جای شناسهٔ پایه برای آزمون‌ها — همان الگوی
    /// <c>AppSettings.DirOverride</c>. ⚠️ فقط از خودِ آزمون مقدار می‌گیرد؛
    /// نه از تنظیمات و نه از محیط، پس راهی به دستِ کاربر ندارد.
    /// </summary>
    public static string? LocalIdOverride { get; set; }

    public static string LocalId
    {
        get
        {
            if (LocalIdOverride is not null) return LocalIdOverride;
            try
            {
                var dir = Path.GetDirectoryName(Environment.ProcessPath);
                if (dir is null) return "";
                var f = Path.Combine(dir, "base.id");
                return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
            }
            catch { return ""; }
        }
    }

    /// <summary>«PumpYaqobi-app-1a2b3c4d.zip» → «1a2b3c4d»؛ وگرنه ‎null‎.</summary>
    public static string? IdInAssetName(string name)
    {
        const string prefix = "PumpYaqobi-app-";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var rest = Path.GetFileNameWithoutExtension(name)[prefix.Length..];
        return rest.Length > 0 ? rest : null;
    }
}
