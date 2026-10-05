using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>نتیجهٔ یک کارِ ابری — هرگز استثنا بیرون نمی‌دهد.</summary>
/// <param name="Ok">شد یا نشد.</param>
/// <param name="Why">اگر نشد، چرا — به فارسیِ خودِ سرور، برای نشان دادن به کاربر.</param>
/// <param name="Code">کدِ ماشینیِ خطا، اگر سرور داده باشد.</param>
/// <summary>یک پیامِ چتِ پشتیبانی، همان‌طور که ابر می‌دهد.</summary>
public sealed record CloudChatMessage(string Id, long Seq, string Acct, string From, string Name,
                                      string Kind, string Text, string? MediaId, long At, bool Deleted)
{
    public bool FromCustomer => From == "c";

    public static CloudChatMessage? Parse(JsonElement m, string acctFallback = "")
    {
        if (m.ValueKind != JsonValueKind.Object) return null;
        string S(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        long N(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
        var id = S("id");
        if (id.Length == 0) return null;
        var acct = S("acct");
        var media = S("mediaId");
        return new CloudChatMessage(id, N("seq"), acct.Length > 0 ? acct : acctFallback, S("from"), S("name"),
            S("kind").Length > 0 ? S("kind") : "text", S("text"), media.Length > 0 ? media : null, N("at"),
            m.TryGetProperty("deleted", out var d) && d.ValueKind == JsonValueKind.True);
    }

    /// <summary>
    /// پیامِ رشتهٔ «پشتیبانیِ برنامه» (‎/api/pump/device/support/thread‎).
    ///
    /// ⚠️ شکلش با چتِ مشتری فرق دارد و تا ۱۴۰۵/۰۷/۱۳ با همان ‎Parse‎ خوانده
    /// می‌شد، پس هر پیام <b>متنِ خالی</b> می‌داد: سرور ‎body‎ · ‎sender‎ ·
    /// ‎senderName‎ · ‎createdAt‎ می‌فرستد، نه ‎text‎ · ‎from‎ · ‎at‎.
    /// ‎From‎ = ‎o‎ برای «خودمان» (‎user‎) و ‎a‎ برای مدیرِ سامانه؛ ‎Seq‎ همان
    /// ‎createdAt‎ است، چون درِ ‎after‎ی سرور با همین کار می‌کند.
    /// </summary>
    public static CloudChatMessage? ParseSupport(JsonElement m)
    {
        if (m.ValueKind != JsonValueKind.Object) return null;
        string S(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        long N(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
        var id = S("id");
        var body = S("body");
        //  ⛔ ۱۴۰۵/۰۷/۱۶: پیامِ رسانه‌ای (عکس/ویدیو/صدا) متن ندارد — ‎mediaId‎ دارد
        var media = S("mediaId");
        var kind = S("kind") is "image" or "video" or "audio" && media.Length > 0 ? S("kind") : "text";
        if (id.Length == 0 || (body.Length == 0 && kind == "text")) return null;
        var at = N("createdAt");
        return new CloudChatMessage(id, at, "support", S("sender") == "user" ? "o" : "a", S("senderName"),
            kind, body, kind == "text" ? null : media, at, false);
    }

    public static List<CloudChatMessage> ParseList(JsonElement json, string acctFallback = "")
    {
        var list = new List<CloudChatMessage>();
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("messages", out var arr)
            && arr.ValueKind == JsonValueKind.Array)
            foreach (var m in arr.EnumerateArray())
                if (Parse(m, acctFallback) is { } x) list.Add(x);
        return list;
    }
}

/// <summary>یک گفت‌وگوی پشتیبانی از دیدِ صاحبِ پمپ.</summary>
public sealed record CloudChatThread(string Acct, string Name, bool Blocked, int Unread, long UpdatedAt,
                                     CloudChatMessage? Last, long CustSeenSeq = 0);

/// <summary>یک نسخهٔ پشتیبان روی پوشهٔ ابریِ این پمپ.</summary>
/// <remarks>
/// ⚠️ نامِ فایل را <b>سرور</b> می‌سازد، نه برنامه: نامی که از این‌جا
/// برود می‌تواند <c>../</c> داشته باشد و جای دیگری بنشیند.
/// </remarks>
public sealed record CloudBackup(string Id, string Name, long Bytes, string Kind,
                                 string Label, long CreatedAt);

/// <summary>سهمِ این پمپ از پوشهٔ ابری — و چقدرش پر است.</summary>
/// <remarks>
/// ⚠️ سهم دو پله دارد و <b>سرور</b> تصمیمش را می‌گیرد، نه برنامه:
/// پمپِ بی‌اشتراک جای کمتری دارد. همین‌جا نوشته می‌شود تا صاحبِ پمپ
/// غافلگیر نشود.
/// </remarks>
public sealed record CloudBackupStats(int Count, int Keep, long UsedBytes, long QuotaBytes,
                                      long LastAt, bool Paid)
{
    public static readonly CloudBackupStats None = new(0, 0, 0, 0, 0, false);
}

public sealed record CloudResult(bool Ok, string Why = "", string Code = "")
{
    public static readonly CloudResult Done = new(true);
    public static CloudResult No(string why, string code = "") => new(false, why, code);
}

/// <summary>
/// حالِ رسیدن به ابر — و <b>فقط</b> از جوابِ واقعیِ سرور پر می‌شود.
///
/// <para>
/// ⛔ خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۱): «هیچ کلکِ دروغی نباشد که بگوید
/// وصل است.» پس <see cref="Unknown"/> یک حالتِ واقعی است و سبز نیست: یعنی
/// «هنوز نپرسیده‌ایم». تا وقتی یک درخواستِ واقعی نرفته و جوابِ واقعی
/// نیامده، هیچ‌جای برنامه حق ندارد بگوید وصل است.
/// </para>
/// </summary>
public enum CloudReach
{
    /// <summary>هنوز هیچ درخواستی نرفته — نه وصل، نه قطع.</summary>
    Unknown,

    /// <summary><b>سرورِ خودمان</b> جواب داد (حتی اگر جوابش خطا بود).</summary>
    Online,

    /// <summary>نرسیدیم، یا چیزی جواب داد که سرورِ ما نبود.</summary>
    Offline,
}

/// <summary>وضعیتِ اشتراک، آن‌طور که برنامه نشان می‌دهد.</summary>
/// <param name="Active">اشتراک یا دورهٔ آزمایشی باز است.</param>
/// <param name="Source">subscription | trial | free | none</param>
/// <param name="PlanTitle">نامِ پلن.</param>
/// <param name="DaysLeft">روزهای مانده.</param>
/// <param name="EndsAt">پایان (میلی‌ثانیهٔ یونیکس).</param>
/// <param name="Features">بخش‌هایی که باز است.</param>
public sealed record PumpSubscription(
    bool Active, string Source, string PlanTitle, int DaysLeft, long EndsAt,
    IReadOnlyList<string> Features)
{
    public static readonly PumpSubscription None =
        new(false, "none", "", 0, 0, Array.Empty<string>());

    /// <summary>
    /// وقتی سرور «هیچ» می‌گوید، <b>چرا</b> — از ‎entitlement.trial‎ی خودِ
    /// سرور: دوره در پنل خاموش است، یا این پمپ دوره‌اش را مصرف کرده. تا
    /// امروز فقط «اشتراکِ فعالی ندارد» دیده می‌شد و معلوم نبود ایراد از
    /// تنظیمِ پنل است یا از رسیدن. ⚠️ فقط نوشته است؛ هیچ تصمیمی از آن نیست.
    /// </summary>
    public string TrialNote { get; init; } = "";
}

/// <summary>
/// ══ ابر — «اشتراک از سرور می‌آید» ═══════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «اشتراک هم از سرور بره براش.»
///
/// ── راه ────────────────────────────────────────────────────────────────────
/// <code>
///   صاحبِ پمپ شش رقم را در تنظیمات می‌زند
///        │
///        ▼  POST /api/pump/device/activate
///   توکنِ دستگاه + مجوزِ امضاشده + کلیدِ عمومیِ سرور
///        │
///        ▼  هر بار بالا آمدن: /api/pump/device/me و /license
///   اشتراک تازه می‌شود، بی این‌که کسی چیزی بپرسد
/// </code>
///
/// نه حسابی، نه مرورگری، نه نشانی‌ای. نشانی در
/// <see cref="CloudConfig.BaseUrl"/> قفل است و از تنظیمات خوانده نمی‌شود.
///
/// ── سه قاعده ───────────────────────────────────────────────────────────────
///
///  • <b>هیچ استثنایی بیرون نمی‌دهد.</b> نبودِ اینترنت، سرورِ خاموش و فایروال
///    همه یعنی «نشد»، نه «خطا». برنامهٔ پمپ باید آفلاین هم باز شود.
///
///  • <b>کلیدِ عمومی فقط یک بار قفل می‌شود.</b> ⚠️ اگر سرور روزی کلیدِ
///    دیگری بدهد، <b>نمی‌پذیریم</b> — چون همان یعنی یا سرور عوض شده یا
///    کسی وسط نشسته. بی این قید، کلِ قفل با یک سرورِ ساختگی دور می‌خورد.
///
///  • <b>مجوز ذخیره می‌شود.</b> تصمیمِ آفلاین از روی همان گرفته می‌شود، و
///    <see cref="LicenseGuard"/> امضایش را می‌سنجد — نه یک پرچمِ ساده که
///    هر کسی در فایلِ تنظیمات عوضش کند.
/// </summary>
public sealed partial class CloudLink
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>
    /// ⛔ فرستنده و گیرندهٔ <b>فایلِ پشتیبان</b> — مهلتِ بلند (۱۴۰۵/۰۷/۲۰).
    /// پیش از این پشتیبان با همان <see cref="Http"/>ِ بیست‌ثانیه‌ای می‌رفت: دفترِ
    /// چند مگابایتی روی اینترنتِ معمولی هیچ‌وقت در بیست ثانیه نمی‌رسید و هر بار
    /// «سرور دیر جواب داد» می‌شد — دستی و خودکار، هر دو.
    /// </summary>
    public static readonly HttpClient BigHttp = new() { Timeout = TimeSpan.FromMinutes(30) };

    /// <summary>
    /// ⚠️ **تنها راهِ سنجش** — یک شنوندهٔ ساختگی به جای شبکه، تا سنجشِ
    /// `cloudlogin` بتواند «ثبت‌نام، ورود، و زدنِ کدِ شش‌رقمی» را واقعاً تا
    /// تهِ کار ببرد بی این‌که به سرورِ واقعیِ اشتراک دست بزند.
    ///
    /// ⛔ **این قفلِ نشانی را باز نمی‌کند**: نشانی همچنان
    /// <see cref="CloudConfig.BaseUrl"/> است و از تنظیمات یا محیط خوانده
    /// نمی‌شود. این فقط با یک خطِ **کد** مقدار می‌گیرد (و فقط در
    /// `PumpYaqobi.UiTests`)، پس کسی که فایلِ تنظیمات را عوض می‌کند از
    /// این راه هیچ کاری نمی‌تواند بکند. در برنامهٔ واقعی همیشه `null` است.
    /// </summary>
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? TestTransport { get; set; }

    private readonly AppSettings _settings;
    private readonly Func<Task> _save;

    public CloudLink(AppSettings settings, Func<Task> save)
    {
        _settings = settings;
        _save = save;
    }

    /// <summary>آیا این برنامه از قبل فعال شده است.</summary>
    public bool Activated => !string.IsNullOrWhiteSpace(_settings.CloudDeviceToken);

    /// <summary>شناسهٔ این کامپیوتر.</summary>
    public string DeviceUid => CloudConfig.DeviceUid(_settings);

    /// <summary>آخرین وضعیتِ اشتراک که از سرور گرفته شده.</summary>
    public PumpSubscription Subscription
    {
        get => _subscription;
        private set { _subscription = value; _subscriptionKnown = true; }
    }

    private PumpSubscription _subscription = PumpSubscription.None;

    /// <summary>
    /// این نمونه در همین عمرش پاسخِ واقعیِ سرور دربارهٔ اشتراک را دیده؟
    ///
    /// ⚠️ بی این، نمونهٔ تازه‌ای که فقط توکنِ دستگاه دارد (حلقهٔ پس‌زمینه
    /// بی حساب) همیشه «اشتراک: نیست» را کنارِ مجوزِ سالم می‌دید و
    /// <see cref="KeepLicenseFreshAsync"/> آن را «ناجوری» می‌خواند — یعنی هر
    /// شصت ثانیه دو درخواستِ بی‌دلیل.
    /// </summary>
    private bool _subscriptionKnown;

    /// <summary>
    /// حالِ اشتراک برای قفلِ سه چیزِ ابری — همان چیزی که صفحهٔ پروفایل
    /// نشان می‌دهد (<see cref="Entitlements"/>).
    /// </summary>
    public EntitlementState Entitlement => Entitlements.State(_settings, Subscription);

    /// <summary>
    /// سنجشِ مجوزِ ذخیره‌شده — همان چیزی که آفلاین هم کار می‌کند.
    /// ⚠️ از <see cref="LicenseGuard.CheckStored"/> می‌رود تا کفِ ساعت و
    /// اثرِ انگشتِ کامپیوتر هم سنجیده شوند.
    /// </summary>
    public LicenseCheck Verify(long? nowMs = null) => LicenseGuard.CheckStored(_settings, nowMs);

    /// <summary>
    /// «کلیدِ سرور با قفلِ این برنامه نمی‌خورد» — یک جمله برای هر سه راه.
    /// </summary>
    private static CloudResult KeyMismatch() => CloudResult.No(
        "کلیدِ سرور با آن‌چه این برنامه قفل کرده فرق دارد. اگر سرور را واقعاً "
        + "عوض کرده‌اید، با پشتیبانی تماس بگیرید.", "key_mismatch");

    /// <summary>
    /// ⛔ با ریشهٔ اعتمادِ داخلِ برنامه (<see cref="CloudConfig.LicenseKeys"/>)
    /// کلیدِ سرور فقط وقتی پذیرفته می‌شود که داخلِ همان فهرست باشد — و آن
    /// وقت <b>چرخشِ کلید</b> هم پذیرفته است (کلیدِ تازه‌ای که در فهرست هست
    /// جای قفلِ قبلی می‌نشیند). نتیجه: <c>null</c> یعنی «ادامه بده».
    /// فهرستِ خالی ⇒ <c>false</c> برمی‌گردد و راهِ TOFUِ همیشگی می‌رود.
    /// </summary>
    private bool TrustRootDecides(string serverKey, out CloudResult? fail)
    {
        fail = null;
        if (CloudConfig.LicenseKeys.Count == 0) return false;
        if (serverKey.Length == 0) return true;
        if (!CloudConfig.TrustsKey(serverKey)) { fail = KeyMismatch(); return true; }
        _settings.CloudPublicKey = serverKey;
        return true;
    }

    // ── فعال‌سازی ──────────────────────────────────────────────────────

    /// <summary>
    /// کدِ شش‌رقمی ⇒ پمپ، اشتراک، توکنِ دستگاه و مجوز.
    ///
    /// کدِ پمپ و نامش از تنظیماتِ خودِ برنامه می‌روند، پس کاربر جز همان شش
    /// رقم چیزی تایپ نمی‌کند.
    /// </summary>
    /// <param name="stationName">نامِ پمپ — تا ابر همان نامی را بشناسد که کاربر نوشته.</param>
    /// <param name="stationLocation">لوکیشنِ پمپ (نشانی) — همان کادرِ گامِ دومِ ثبت.</param>
    public async Task<CloudResult> ActivateAsync(string code, string stationName = "",
                                                 string stationLocation = "",
                                                 CancellationToken ct = default)
    {
        var clean = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کد باید شش رقم باشد");

        var body = new
        {
            code = clean,
            device = new
            {
                uid = DeviceUid,
                name = Environment.MachineName,
                platform = "windows",
            },
            station = new
            {
                code = _settings.StationCode ?? "",
                name = (stationName ?? "").Trim(),
                location = (stationLocation ?? "").Trim(),
            },
        };

        var (ok, json, why, errCode) = await PostAsync("/api/pump/device/activate", body, null, ct);
        if (!ok) return CloudResult.No(why, errCode);

        //  ⚠️ کلیدِ عمومی فقط همین یک بار قفل می‌شود. اگر از قبل کلیدی
        //  داریم و سرور کلیدِ دیگری داد، نمی‌پذیریم.
        var serverKey = Str(json, "publicKey");
        if (TrustRootDecides(serverKey, out var rootFail))
        {
            if (rootFail is not null) return rootFail;
        }
        else if (string.IsNullOrWhiteSpace(_settings.CloudPublicKey))
        {
            if (string.IsNullOrWhiteSpace(serverKey))
                return CloudResult.No("سرور کلیدِ عمومی نداد؛ فعال‌سازی نیمه‌کاره ماند");
            _settings.CloudPublicKey = serverKey;
        }
        else if (!string.IsNullOrWhiteSpace(serverKey) && serverKey != _settings.CloudPublicKey)
        {
            return KeyMismatch();
        }

        _settings.CloudDeviceToken = Str(json, "deviceToken");
        _settings.CloudStationId = StationId(json);
        _settings.CloudStationCode = StationCodeOf(json);
        _settings.CloudLicense = Str(json, "license");
        _settings.CloudSyncedAt = AppClock.UnixMs;
        Seated();
        ReadSubscription(json);
        //  مُهرِ «دیدیم که باز است» — پایهٔ ارفاق (‎Entitlements.Grace‎). بی این،
        //  یک روزِ بی‌اینترنت می‌توانست کیو‌آر و اپِ کارمندانِ مشتریِ پول‌داده
        //  را خاموش کند.
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();

        return CloudResult.Done;
    }

    /// <summary>
    /// ⛔ <b>پمپِ این حساب — و اگر نداشت، همین‌جا ساخته می‌شود.</b>
    ///
    /// <para>
    /// ⚠️ <b>این همان حلقهٔ گم‌شدهٔ «تا آخرِ مرحله‌ها» بود.</b> ثبت‌نام و ورود
    /// بی‌عیب کار می‌کردند، ولی حسابِ تازه <b>هیچ پمپی</b> ندارد:
    /// <c>/api/pump/me</c> می‌گفت <c>station: null</c>، پس
    /// <see cref="BindAsync"/> ۴۰۴ِ <c>no_station</c> می‌گرفت، توکنِ دستگاه
    /// نمی‌آمد، مجوز صادر نمی‌شد و کدِ اپِ کارمندان هم نه. تنها راهِ ساختنِ
    /// پمپ کدِ شش‌رقمی بود — همان کدی که صاحب ریپو صریح گفت در کار نیست
    /// («اشتراک رو من به حسابِ یارو از سرور می‌دم»). یعنی کاربرِ تازه روی
    /// گامِ دوم گیر می‌کرد و هیچ راهی جلو نداشت.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>خودکار نیست و نباید بشود.</b> فقط از دکمهٔ خودِ کاربر صدا زده
    /// می‌شود، نه از حلقهٔ پس‌زمینه: «هر حساب یک پمپ» قاعدهٔ سرور است و
    /// ساختنِ بی‌خبرِ پمپ یعنی حسابی که دیگر نمی‌تواند به پمپِ واقعی‌اش
    /// بپیوندد.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <c>already_member</c> خطا نیست: یعنی پمپ همین حالا هست (دو کلیک،
    /// یا گوشیِ دیگری که زودتر ساختش). همان را می‌پذیریم.
    /// </para>
    /// </summary>
    public async Task<CloudResult> EnsureStationAsync(string stationName,
                                                      CancellationToken ct = default)
    {
        if (!SignedIn) return CloudResult.No("اول وارد حساب شوید", "no_account");

        //  ۱) پمپی هست؟ — از خودِ حساب پرسیده می‌شود، نه از تنظیماتِ محلی.
        var me = await AccountAsync(HttpMethod.Get, "/api/pump/me", null, ct);
        if (!me.Ok) return CloudResult.No(me.Why, me.Code);
        if (StationId(me.Json).Length == 0)
        {
            //  ۲) ندارد ⇒ بساز. نامِ خالی را خودِ سرور با «پمپ من» پر می‌کند،
            //  ولی نامِ کاربر همیشه بهتر است.
            //
            //  ⛔ **فقط نام می‌رود.** لوکیشن هیچ‌وقت در این بدنه نبود و کادرش
            //  هم در ۱۴۰۵/۰۷/۱۰ از صفحهٔ ورود برداشته شد — «یارو همین که
            //  اسمِ پمپشو بزنه بسه».
            var body = new { name = (stationName ?? "").Trim() };
            var made = await AccountAsync(HttpMethod.Post, "/api/pump", body, ct);
            if (!made.Ok && made.Code != "already_member")
                return CloudResult.No(made.Why, made.Code);
        }

        AccountHasStation = true;
        //  ۳) و حالا بند شدن — همان راهِ همیشگی، با همان قفلِ کلیدِ عمومی.
        var bindRes = await BindAsync(ct);
        LastBindWhy = bindRes.Ok ? "" : (bindRes.Why ?? "");
        return bindRes;
    }

    /// <summary>
    /// ⛔ <b>آیا این حساب پمپ دارد؟</b> — یک پرسشِ ساده از خودِ سرور.
    ///
    /// گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۰): «این بخش مانعِ ساختِ حساب
    /// می‌شود… نمی‌خوام این مشکل پیش بیاد، منو دیوانه نکنی.» و در عکس:
    /// «❌ خطای داخلی سرور» روی همان گامِ پمپ.
    ///
    /// <para>
    /// ⚠️ آن جمله <b>پیامِ خودِ سرور</b> است (۵۰۰)، نه متنی که برنامه
    /// ساخته باشد. یعنی خرابی آن‌طرف بود — ولی <b>گیر کردنِ کاربر</b>
    /// این‌طرف: گامِ پمپ تنها دیوارِ بینِ او و برنامه است و با یک ۵۰۰ی
    /// گذرا تا ابد بسته می‌مانْد.
    /// </para>
    ///
    /// <para>
    /// ⛔ <b>و این «پنهان کردنِ خطا» نیست.</b> کارِ گامِ پمپ یک چیز است:
    /// «این حساب پمپ داشته باشد». اگر <b>دارد</b>، آن کار انجام شده — هر
    /// چه آن درخواستِ شکست‌خورده گفته باشد. بند شدنِ دستگاه هم گم نمی‌شود:
    /// حلقهٔ شصت‌ثانیه‌ایِ پس‌زمینه خودش دوباره می‌زندش
    /// (<c>CloudKeepAsync</c> ⇒ <c>HomeFromAccountAsync</c>).
    /// </para>
    /// </summary>
    public async Task<bool> HasStationAsync(CancellationToken ct = default)
    {
        if (!SignedIn) return false;
        try
        {
            var me = await AccountAsync(HttpMethod.Get, "/api/pump/me", null, ct);
            if (me.Ok) AccountHasStation = StationId(me.Json).Length > 0;
            return me.Ok && StationId(me.Json).Length > 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// ⛔ <b>بند شدنِ این دستگاه به پمپِ همین حسابِ وارد شده — بی هیچ کدی.</b>
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۳۰): «اشتراک رو من به حسابِ یارو از
    /// سرور می‌دم اینترنتی و تو برنامه تو حسابِ همون ثبت می‌شن… من یادم
    /// نمیاد که برای اشتراک کدی گفته باشم.»
    ///
    /// <para>
    /// ⚠️ <b>و چرا این لازم بود، در حالی که <see cref="HomeFromAccountAsync"/>
    /// از قبل اشتراک را از حساب می‌خواند:</b> آن راه فقط «فعال است یا نه» را
    /// می‌آورد، <b>نه فهرستِ قابلیت‌های پلن</b>. فهرست تنها داخلِ مجوزِ
    /// امضاشده (<c>feat</c>) است و مجوز فقط با توکنِ <b>دستگاه</b> صادر
    /// می‌شود. پس برنامه‌ای که فقط از راهِ حساب می‌آمد،
    /// <see cref="LicenseCheck.Listed"/>ش دروغ می‌گفت و
    /// <see cref="Entitlements.Allows"/> همه‌چیز را باز می‌کرد — یعنی
    /// مشتریِ پلنِ <b>استاندارد</b> کیو‌آر و اپِ کارمندان و مفاد/ضرر و
    /// تاریخچه‌ها و داشبورد را هم می‌گرفت. مستقیم روی پول.
    /// </para>
    ///
    /// <para>
    /// ⚠️ این مسیر <b>اشتراک نمی‌سازد</b>: پمپِ بی‌اشتراک هم بند می‌شود و
    /// مجوزی نمی‌گیرد — که درست است. باز شدنِ قفل‌ها کارِ پنلِ سرور است.
    /// </para>
    ///
    /// <para>
    /// ⚠️ و <b>قفلِ کلیدِ عمومی همان‌جاست</b>: نخستین بار ذخیره می‌شود و از
    /// آن به بعد کلیدِ متفاوت رد می‌شود (<c>key_mismatch</c>) — همان قیدی
    /// که سرورِ ساختگی را بی‌اثر می‌کند.
    /// </para>
    /// </summary>
    /// <summary>
    /// ══ مجوزی که مالِ این کامپیوتر نیست ⇒ همین کامپیوتر از نو بند می‌شود ══
    ///
    /// گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۴): پنلِ مدیر «آزمایشی · ۳۰ روز»،
    /// و خودِ برنامه «بدونِ اشتراکِ فعال» و **کلِ برنامه فقط‌خواندنی**.
    /// سنجهٔ `signuptrial` با ‎PUMP_SIGNUP_CORRUPT=1‎ روی سرورِ حسابِ واقعی
    /// چهار حال را یافت که هر کدام **دقیقاً همان صفحه** را می‌سازد و هیچ‌وقت
    /// خودش درست نمی‌شد: شناسهٔ دستگاه (`duid`) ناجور، اثرِ انگشتِ کامپیوتر
    /// ناجور، کلیدِ عمومیِ دیگر روی دیسک، و شناسهٔ پمپِ دیگر. در هر چهار،
    /// `RefreshAsync` هر ده دقیقه همان مجوزِ ناجور را دوباره می‌گرفت (یا
    /// نمی‌پذیرفت)، و چون دستگاه «فعال» بود هیچ‌وقت دوباره بند نمی‌شد.
    ///
    /// ⛔ **فقط وقتی امضا یا هویت شکست خورده** (`!SignatureOk`) — نه برای
    /// مجوزِ منقضی (آن را `RefreshAsync` و `LicenseClock.Anchor` درست
    /// می‌کنند) و نه برای «مجوز نیست» (اشتراک نداشتن یک جوابِ درست است).
    ///
    /// ⛔ **راهش همان `BindAsync`ِ همیشگی است با توکنِ حساب** — نه چیزِ
    /// تازه‌ای. توکنِ حساب با DPAPI رمز شده، پس پوشهٔ کپی‌شده روی کامپیوترِ
    /// دیگر اصلاً «واردشده» نیست و به این‌جا نمی‌رسد؛ یعنی قیدِ «کپیِ پوشه
    /// اشتراک را با خودش نمی‌برد» سرِ جایش است.
    ///
    /// ⚠️ **یک بیت از دفتر لمس نمی‌شود** — فقط توکنِ دستگاه و مجوز و (برای
    /// کامپیوترِ دیگر) شناسه‌های همین کامپیوتر.
    /// </summary>
    private async Task ReseatIfForeignLicenseAsync(string acctStation, string locked, bool bindDue,
                                                   CancellationToken ct)
    {
        if (!Activated || !bindDue || acctStation.Length == 0) return;
        if (!string.Equals(acctStation, locked, StringComparison.Ordinal)) return;
        if (string.IsNullOrWhiteSpace(_settings.CloudLicense)) return;
        var check = Verify();
        if (check.Valid || check.SignatureOk) return;

        var oldUid = _settings.CloudDeviceUid;
        var oldMachine = _settings.CloudDeviceMachine;
        //  «از کامپیوترِ دیگری آمده» ⇒ شناسه‌های **همین** کامپیوتر از نو
        if (CloudConfig.MachineMoved(_settings))
        {
            _settings.CloudDeviceMachine = "";
            _settings.CloudDeviceUid = "";
        }
        var oldToken = _settings.CloudDeviceToken;
        var oldLicense = _settings.CloudLicense;
        _settings.CloudDeviceToken = "";
        _settings.CloudLicense = "";

        CloudResult r;
        try { r = await BindAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { r = CloudResult.No(ex.GetType().Name); }

        if (r.Ok)
        {
            LastBindWhy = "";
            _lastBindFailAt = DateTime.MinValue;
            LicenseChanged?.Invoke();
            return;
        }
        //  ⚠️ نشد ⇒ همان چیزی که بود برمی‌گردد (دستگاه بی‌توکن نمی‌ماند) و
        //  دورِ بعد، ده دقیقه بعد، دوباره.
        _settings.CloudDeviceToken = oldToken;
        _settings.CloudLicense = oldLicense;
        _settings.CloudDeviceUid = oldUid;
        _settings.CloudDeviceMachine = oldMachine;
        LastBindWhy = "مجوزِ این کامپیوتر سنجیده نشد و وصلِ دوباره نشد — " + (r.Why ?? "");
        _lastBindFailAt = AppClock.Mono;
        await SaveQuiet();
    }

    /// <summary>
    /// ══ این کامپیوتر به پمپِ خودِ حساب وصل می‌شود — بی کد، با توکنِ حساب ══
    ///
    /// برای دو حالی که تا ۱۴۰۵/۰۷/۱۴ هیچ‌وقت خودشان درست نمی‌شدند (هر دو با
    /// سرورِ واقعی بازسازی شدند): توکنِ دستگاهِ پمپِ دیگری روی دیسک مانده،
    /// یا سرور جابه‌جاییِ این کامپیوتر از پمپِ دیگری را رد کرده است.
    ///
    /// ⛔ **همان `BindAsync`ِ همیشگی** — نه راهِ تازه. توکن و مجوز کنار
    /// می‌روند؛ اگر <paramref name="leaveOther"/>، بندهای پمپِ دیگر هم (شناسه،
    /// کد، کدِ اپِ کارمندان، سرورِ خانگی) — همان خانه‌هایی که
    /// <see cref="ForgetStationAsync"/> پاک می‌کند، جز کلیدِ عمومی. نشد ⇒ همه
    /// برمی‌گردند و ده دقیقه بعد دوباره.
    ///
    /// ⛔ **یک بیت از دفتر لمس نمی‌شود**، و پمپِ دیگر روی سرور هم دست نمی‌خورد.
    /// </summary>
    private async Task<bool> ReseatToAccountPumpAsync(bool leaveOther, CancellationToken ct)
    {
        var keep = (_settings.CloudDeviceToken, _settings.CloudLicense, _settings.CloudStationId,
                    _settings.CloudStationCode, _settings.CloudAccessCode, _settings.ServerUrl,
                    _settings.ServerLanUrl, _settings.ServerToken, _settings.ServerReadKey, _settings.ServerId);
        _settings.CloudDeviceToken = "";
        _settings.CloudLicense = "";
        if (leaveOther)
        {
            _settings.CloudStationId = "";
            _settings.CloudStationCode = "";
            _settings.CloudAccessCode = "";
            _settings.ServerUrl = "";
            _settings.ServerLanUrl = "";
            _settings.ServerToken = "";
            _settings.ServerReadKey = "";
            _settings.ServerId = "";
        }

        CloudResult r;
        try { r = await BindAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { r = CloudResult.No(ex.GetType().Name); }

        if (r.Ok)
        {
            LastBindWhy = "";
            _lastBindFailAt = DateTime.MinValue;
            try { LicenseChanged?.Invoke(); } catch { /* خبر رفاه است */ }
            return true;
        }
        (_settings.CloudDeviceToken, _settings.CloudLicense, _settings.CloudStationId,
         _settings.CloudStationCode, _settings.CloudAccessCode, _settings.ServerUrl,
         _settings.ServerLanUrl, _settings.ServerToken, _settings.ServerReadKey, _settings.ServerId) = keep;
        LastBindWhy = "این کامپیوتر به پمپِ حسابتان وصل نشد — " + (r.Why ?? "");
        _lastBindFailAt = AppClock.Mono;
        await SaveQuiet();
        return false;
    }

    public async Task<CloudResult> BindAsync(CancellationToken ct = default, bool adopt = false)
    {
        if (!SignedIn) return CloudResult.No("اول وارد حساب شوید", "no_account");

        //  ⛔ `adopt`: این کامپیوتر روی پمپِ دیگری است (روزی با کدِ شش‌رقمی
        //  فعال شده بود) و حالا صاحبش وارد حسابش شده. توکنِ کنونیِ دستگاه
        //  مدرک است و **خودِ سرور** تصمیم می‌گیرد: پمپِ بی‌صاحب ⇒ مالِ حساب
        //  یا دستگاه به پمپِ حساب با روزهای ماندهٔ اشتراکش؛ پمپِ صاحب‌دار ⇒
        //  `station_mismatch`. شرحش بالای `adoptFromDevice`ِ سرورِ حساب.
        var adopting = adopt && Activated;
        var device = new
        {
            uid = DeviceUid,
            name = Environment.MachineName,
            platform = "windows",
        };
        object body = adopting
            ? new { device, adopt = true, deviceToken = _settings.CloudDeviceToken }
            : new { device };

        //  ⚠️ از راهِ `AccountAsync` می‌رود، نه `PostAsync`ِ خام: توکنِ
        //  دسترسی یک ساعت عمر دارد و همین‌جا بی‌صدا می‌مرد.
        var res = await AccountAsync(HttpMethod.Post, "/api/pump/device/bind", body, ct);
        if (!res.Ok) return CloudResult.No(res.Why, res.Code);
        var json = res.Json;

        //  ⛔ سرورِ کهنه `adopt` را نمی‌شناسد و بی‌صدا دستگاه را روی پمپِ حساب
        //  ثبت می‌کند — بی بردنِ روزهای اشتراکِ پمپِ کد. پس بی نشانِ صریحِ
        //  سرور هیچ چیزی این‌جا عوض نمی‌شود — حتی کلیدِ عمومی.
        var adopted = Str(json, "adopt");
        if (adopting && adopted is not ("moved" or "claimed"))
            return CloudResult.No("سرورِ حساب کهنه است و هنوز نمی‌تواند این کامپیوتر را به پمپِ حسابتان "
                + "ببرد — سرورِ حساب را از مرکز فرمان به‌روز کنید.", "adopt_unsupported");

        //  کلیدِ عمومی فقط یک بار قفل می‌شود — همان قاعدهٔ `ActivateAsync`
        var serverKey = Str(json, "publicKey");
        if (TrustRootDecides(serverKey, out var rootFail))
        {
            if (rootFail is not null) return rootFail;
        }
        else if (string.IsNullOrWhiteSpace(_settings.CloudPublicKey))
        {
            if (!string.IsNullOrWhiteSpace(serverKey)) _settings.CloudPublicKey = serverKey;
        }
        else if (!string.IsNullOrWhiteSpace(serverKey) && serverKey != _settings.CloudPublicKey)
        {
            //  ══ کلیدی که چیزی را نگه نمی‌دارد، قفل نیست ═══════════════════
            //
            //  ⛔ **این استثنا قفلِ ضدِ کرک را ضعیف نمی‌کند** — و دلیلش
            //  دقیق است: کارِ آن قفل این است که سرورِ **دیگری** نتواند
            //  برای دستگاهی که **فعال شده** مجوز امضا کند. دستگاهی که نه
            //  توکن دارد و نه مجوز، چیزی برای محافظت ندارد و آن کلید فقط
            //  زباله‌ای از یک تلاشِ ناتمام است.
            //
            //  ⚠️ و بی این، نصبِ نیمه‌کاره **برای همیشه** می‌مرد: هر بند
            //  شدنِ بعدی `key_mismatch` می‌گرفت، دستگاه هیچ‌وقت فعال
            //  نمی‌شد، و دورهٔ آزمایشیِ ۳۰ روزه هم هیچ‌وقت نمی‌رسید —
            //  همان حلقه‌ای که صاحب ریپو در ۱۴۰۵/۰۷/۱۱ با عکس گزارشش کرد.
            //  و چون کاربرِ گیرکرده هیچ کاری نمی‌تواند بکند، خودِ برنامه
            //  باید خودش را آزاد کند.
            //
            //  ⚠️ و TOFU همان لحظه از نو بسته می‌شود: کلیدِ تازه می‌نشیند
            //  و از آن به بعد هر کلیدِ دیگری رد می‌شود.
            //  ⛔ `ActivateAsync` عمداً این استثنا را **ندارد**: آن‌جا
            //  کاربر کدِ شش‌رقمی زده و انتظارِ فعال شدن دارد، پس کلیدِ
            //  ناجور یک هشدارِ واقعی است.
            var neverActivated = string.IsNullOrWhiteSpace(_settings.CloudDeviceToken)
                              && string.IsNullOrWhiteSpace(_settings.CloudLicense);
            if (!neverActivated) return KeyMismatch();
            _settings.CloudPublicKey = serverKey;
        }

        var token = Str(json, "deviceToken");
        if (string.IsNullOrWhiteSpace(token))
            return CloudResult.No("سرور توکنِ دستگاه نداد", "no_device_token");

        if (adopted == "moved") _settings.CloudAccessCode = "";   // کدِ اپِ کارمندانِ پمپِ قبلی

        _settings.CloudDeviceToken = token;
        var station = StationId(json);
        if (station.Length > 0) _settings.CloudStationId = station;
        if (StationCodeOf(json) is { Length: > 0 } bound) _settings.CloudStationCode = bound;
        //  ⚠️ مجوزِ **خالی** هم می‌نشیند: «اشتراک ندارد» یک جوابِ درست
        //  است، و نگه داشتنِ مجوزِ کهنه یعنی قفلی که باز مانده
        _settings.CloudLicense = Str(json, "license");
        _settings.CloudSyncedAt = AppClock.UnixMs;
        Seated();
        ReadSubscription(json);
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();
        return CloudResult.Done;
    }

    /// <summary>
    /// دستگاه تازه بند شد: پیامِ «جدا شده» دیگر درست نیست، و اثرِ انگشتِ همین
    /// کامپیوتر اگر هنوز ثبت نشده، همین حالا ثبت می‌شود (TOFU).
    /// </summary>
    private void Seated()
    {
        DeviceDetachedWhy = "";
        CloudConfig.RecordMachine(_settings);
    }

    /// <summary>تمدید با کدِ تازه — بی فعال‌سازیِ دوباره.</summary>
    public async Task<CloudResult> RedeemAsync(string code, string stationName = "",
                                               string stationLocation = "",
                                               CancellationToken ct = default)
    {
        if (!Activated) return await ActivateAsync(code, stationName, stationLocation, ct);

        var clean = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کد باید شش رقم باشد");

        var (ok, json, why, errCode) =
            await DevPostAsync("/api/pump/device/redeem", new { code = clean }, ct);
        if (!ok) return CloudResult.No(why, errCode);

        _settings.CloudLicense = Str(json, "license");
        _settings.CloudSyncedAt = AppClock.UnixMs;
        ReadSubscription(json);
        //  مُهرِ «دیدیم که باز است» — پایهٔ ارفاق (‎Entitlements.Grace‎). بی این،
        //  یک روزِ بی‌اینترنت می‌توانست کیو‌آر و اپِ کارمندانِ مشتریِ پول‌داده
        //  را خاموش کند.
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();
        return CloudResult.Done;
    }
}
