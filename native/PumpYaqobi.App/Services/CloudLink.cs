using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

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
        if (id.Length == 0 || body.Length == 0) return null;
        var at = N("createdAt");
        return new CloudChatMessage(id, at, "support", S("sender") == "user" ? "o" : "a", S("senderName"),
            "text", body, null, at, false);
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
                                     CloudChatMessage? Last);

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
        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReadSubscription(json);
        //  مُهرِ «دیدیم که باز است» — پایهٔ ارفاق (‎Entitlements.Grace‎). بی این،
        //  یک روزِ بی‌اینترنت می‌توانست کیو‌آر و اپِ کارمندانِ مشتریِ پول‌داده
        //  را خاموش کند.
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();
        return CloudResult.Done;
    }

    // ── تازه‌سازی ──────────────────────────────────────────────────────

    /// <summary>
    /// وضعیتِ اشتراک و مجوزِ تازه.
    ///
    /// هر بار که برنامه بالا می‌آید صدا زده می‌شود. نشدنش اشکالی ندارد:
    /// مجوزِ ذخیره‌شده تا ده روز آفلاین کار می‌کند.
    /// </summary>
    public async Task<CloudResult> RefreshAsync(CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("این برنامه هنوز فعال نشده است", "not_activated");

        var (ok, me, why, errCode) = await DevGetAsync("/api/pump/device/me", ct);
        if (!ok) return CloudResult.No(why, errCode);

        //  ⚠️ **شناسهٔ پمپ هم مثلِ کلیدِ عمومی قفل است.** پیش از این هر چه
        //  سرور می‌گفت بی سنجش روی تنظیمات می‌نشست؛ یعنی اگر روزی همین
        //  توکن به پمپِ دیگری می‌خورد، برنامه بی‌صدا پمپش را عوض می‌کرد و
        //  از آن به بعد عکسِ حساب‌های این پمپ به پوشهٔ پمپِ دیگری می‌رفت.
        //  `stn`ِ مجوز هم روی همین سنجیده می‌شود (`LicenseGuard`)، پس
        //  عوض شدنش یعنی «یا دستگاه جابه‌جا شده یا کسی وسط نشسته».
        var seen = StationId(me);
        if (seen.Length > 0 && _settings.CloudStationId.Length > 0
            && !string.Equals(seen, _settings.CloudStationId, StringComparison.Ordinal))
            return CloudResult.No(
                "این دستگاه روی پمپِ دیگری ثبت شده است. اگر واقعاً پمپ را عوض کرده‌اید، "
                + "از «پروفایل ← جدا کردنِ این دستگاه» جدا کنید و با حسابِ همان پمپ دوباره وارد شوید.", "station_mismatch");
        if (seen.Length > 0) _settings.CloudStationId = seen;
        if (StationCodeOf(me) is { Length: > 0 } seenCode) _settings.CloudStationCode = seenCode;
        ReadSubscription(me);

        //  مجوزِ تازه — جدا، چون ممکن است اشتراک تمام شده باشد و مجوزی
        //  صادر نشود. آن هم یک جوابِ درست است، نه خطا.
        var (licOk, lic, _, _) =
            await DevPostAsync("/api/pump/device/license", new { }, ct);
        var licBefore = _settings.CloudLicense ?? "";
        CloudResult? rejected = null;
        if (licOk)
        {
            var token = Str(lic, "license");
            if (token.Length == 0)
            {
                //  ⚠️ خالی بودن یعنی «اشتراک ندارد» — جوابِ **قطعیِ** سرور، پس
                //  مجوزِ قبلی را پاک می‌کنیم، وگرنه تا ده روز با مجوزِ کهنه
                //  باز می‌ماند. و چون ارفاق فقط از مجوز می‌آید، ارفاق هم همین
                //  لحظه تمام می‌شود.
                _settings.CloudLicense = "";
            }
            else rejected = AdoptLicense(token, Str(lic, "publicKey"));
        }

        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        //  مُهرِ «دیدیم که باز است» — پایهٔ ارفاق (‎Entitlements.Grace‎). بی این،
        //  یک روزِ بی‌اینترنت می‌توانست کیو‌آر و اپِ کارمندانِ مشتریِ پول‌داده
        //  را خاموش کند.
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();
        if ((_settings.CloudLicense ?? "") != licBefore)
        {
            try { LicenseChanged?.Invoke(); } catch { /* خبر رفاه است، مجوز اصل */ }
        }
        return rejected ?? CloudResult.Done;
    }

    /// <summary>
    /// ══ مجوزِ تازهٔ سرور — <b>پیش از</b> نشستن سنجیده می‌شود ═════════════════
    ///
    /// ⛔ تا ۱۴۰۵/۰۷/۱۲ <see cref="RefreshAsync"/> هر رشته‌ای را که سرور
    /// می‌داد بی هیچ سنجشی روی مجوزِ سالمِ دیروز می‌نشاند — و اگر کلیدی قفل
    /// نبود، کلیدِ همان پاسخ را هم. یعنی پاسخی جعلی (یا یک باگِ سرور) مجوزِ
    /// واقعیِ مشتری را با زباله عوض می‌کرد.
    ///
    /// حالا: کلید همان قاعدهٔ <see cref="ActivateAsync"/> را دارد (قفل‌شده
    /// عوض نمی‌شود؛ با ریشهٔ اعتمادِ داخلِ برنامه فقط کلیدِ داخلِ فهرست)، و
    /// خودِ مجوز با <see cref="LicenseGuard"/> سنجیده می‌شود. امضا یا هویتش
    /// نخورد ⇒ مجوزِ قبلی <b>دست‌نخورده</b> می‌ماند و دلیلش برمی‌گردد
    /// (<c>key_mismatch</c> · <c>bad_license</c>).
    ///
    /// ⚠️ مجوزِ امضاشده‌ای که همین حالا رسیده، <c>iat</c>ش لنگرِ کفِ ساعت
    /// است (<see cref="LicenseClock.Anchor"/>) — تنها جایی که کفی که به خاطرِ
    /// ساعتِ اشتباهاً جلورفته بالا رفته، می‌تواند پایین بیاید.
    /// </summary>
    /// <returns><c>null</c> یعنی نشست؛ وگرنه چرا نه.</returns>
    private CloudResult? AdoptLicense(string token, string serverKey)
    {
        var pinned = (_settings.CloudPublicKey ?? "").Trim();
        var key = pinned;
        if (CloudConfig.LicenseKeys.Count > 0)
        {
            //  کلیدِ روی دیسک این‌جا هیچ اثری ندارد؛ سنجش با خودِ فهرست است
            if (serverKey.Length > 0 && !CloudConfig.TrustsKey(serverKey)) return KeyMismatch();
            if (serverKey.Length > 0) key = serverKey;
        }
        else if (pinned.Length == 0)
        {
            if (serverKey.Length == 0)
                return CloudResult.No("سرور کلیدِ عمومی نداد؛ مجوزِ تازه سنجیده نشد", "bad_license");
            key = serverKey;
        }
        else if (serverKey.Length > 0 && serverKey != pinned)
        {
            return KeyMismatch();
        }

        //  ⚠️ «حالا» عمداً کفِ ساعت است؛ اگر کف اشتباهاً جلو رفته باشد مجوزِ
        //  تازه «منقضی» دیده می‌شود ولی امضایش سالم است — و همان لنگر کف را
        //  درست می‌کند و بعد دوباره سنجیده می‌شود.
        var check = LicenseGuard.Check(token, key, DeviceUid, _settings.CloudStationId,
                                       LicenseClock.Now(_settings));
        if (!check.SignatureOk)
            return CloudResult.No("مجوزی که سرور داد سنجیده نشد: " + check.Reason, "bad_license");

        LicenseClock.Anchor(_settings, check.IssuedAt, fresh: true);
        _settings.CloudLicense = token;
        if (key != pinned) _settings.CloudPublicKey = key;
        CloudConfig.RecordMachine(_settings);
        return null;
    }

    /// <summary>
    /// هر ده دقیقه یک بار، مجوز از نو گرفته می‌شود — حتی اگر هیچ چیزی
    /// عوض نشده باشد. سقفِ «دیر فهمیدن» است، نه راهِ اصلی.
    /// </summary>
    public static readonly TimeSpan LicenseTick = TimeSpan.FromMinutes(10);

    /// <summary>
    /// ══ اشتراکی که مدیر همین حالا داد، باید همین حالا برسد ═══════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۸): «اشتراک که می‌دم … برنامه اونو
    /// دریافت کنه و قفل‌ها باز بشه و درست کار کنه… و بتونم اشتراکشو بردارم
    /// یا روش اضافه کنم.»
    ///
    /// ⛔ <b>و تا امروز نمی‌رسید.</b> حلقهٔ شصت‌ثانیه‌ایِ
    /// <c>StationPublisher.CloudKeepAsync</c> فقط
    /// <see cref="HomeFromAccountAsync"/> را می‌زد، و آن برای دستگاهی که
    /// <b>از قبل بند شده</b> هیچ مجوزی نمی‌گیرد —
    /// <see cref="RefreshAsync"/> تنها جای گرفتنِ مجوز است و **تنها
    /// صداکننده‌اش صفحهٔ پروفایل بود**. یعنی:
    ///
    /// <list type="bullet">
    ///   <item>اشتراک دادم ⇒ <c>/api/pump/me</c> می‌گفت «فعال» و
    ///     <see cref="Entitlements.Remember"/> مُهرِ ارفاق را جلو می‌برد،
    ///     ولی <b>فهرستِ قابلیت‌ها نمی‌آمد</b> — و فهرستِ نیامده یعنی
    ///     «پلنِ کامل» (<see cref="LicenseCheck.HasFeatureList"/>). پس
    ///     مشتریِ <b>استاندارد</b> هر شش قفل را باز می‌دید. مستقیم روی
    ///     پول، و از همان دری که ۱۴۰۵/۰۶/۳۱ یک بار بسته شده بود.</item>
    ///   <item>اشتراک را برداشتم ⇒ مجوزِ کهنه روی دیسک می‌ماند و تا
    ///     انقضای خودش باز بود، و بعدش هم چهارده روز ارفاق.</item>
    /// </list>
    ///
    /// ── تصمیم، و چرا ارزان است ──────────────────────────────────────────
    ///
    /// <c>/api/pump/me</c> در همان دورِ شصت‌ثانیه‌ای می‌گوید اشتراک فعال است
    /// یا نه. اگر آن حرف با <b>مجوزِ روی دیسک</b> جور نبود، همان لحظه مجوز
    /// از نو گرفته می‌شود. پس:
    ///
    /// <code>
    ///   مدیر اشتراک داد   ⇒ me: فعال · مجوز: بسته  ⇒ ناجور ⇒ همین حالا
    ///   مدیر اشتراک گرفت  ⇒ me: خاموش · مجوز: باز  ⇒ ناجور ⇒ همین حالا
    ///   هیچ چیزی عوض نشد  ⇒ جور  ⇒ هیچ درخواستی، تا تیکِ ده‌دقیقه‌ای
    /// </code>
    ///
    /// ⚠️ <b>و ده‌دقیقه‌ای لازم است، نه تجمل</b>: عوض شدنِ <b>پلن</b>
    /// (استاندارد ⇒ وی‌آی‌پی) هر دو طرف را «فعال» نگه می‌دارد، پس از راهِ
    /// ناجوری دیده نمی‌شود. آن سقف تنها چیزی است که فهرستِ تازهٔ
    /// قابلیت‌ها را می‌آورد.
    ///
    /// ⚠️ <b>هیچ‌وقت استثنا بیرون نمی‌دهد و هیچ‌وقت چیزی را نمی‌بندد</b>:
    /// بی‌اینترنت یعنی همان مجوزِ دیروز، و ارفاقِ چهارده‌روزه سرِ جایش.
    /// </summary>
    public async Task KeepLicenseFreshAsync(CancellationToken ct = default)
    {
        if (!Activated) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var since = now - _settings.CloudSyncedAt;
        var due = _settings.CloudSyncedAt <= 0 || since >= (long)LicenseTick.TotalMilliseconds;

        //  ⚠️ «مجوز چه می‌گوید» را از خودِ `LicenseGuard` می‌پرسیم، نه از
        //  مُهرِ ارفاق: ارفاق عمداً دیر می‌بندد و این‌جا باید حقیقتِ همین
        //  لحظه را بدانیم، وگرنه برداشتنِ اشتراک دو هفته دیده نمی‌شد.
        //  ⚠️ فقط اگر این نمونه واقعاً حرفِ سرور را شنیده باشد — وگرنه
        //  «اشتراک: نیست»ِ پیش‌فرض همیشه با مجوزِ سالم ناجور است.
        var mismatch = _subscriptionKnown && Verify().Valid != Subscription.Active;

        //  ⛔ **«سرور چیزِ تازه‌ای گفت» ⇒ یک بار مجوز.** «باز/بسته» تنها
        //  ناجوری نیست: دورهٔ آزمایشی ⇒ وی‌آی‌پی، تمدید، و استاندارد ⇒
        //  وی‌آی‌پی هر دو طرف را «باز» نگه می‌دارند، پس تا ۱۴۰۵/۰۷/۱۳ اشتراکی
        //  که مدیر به حسابِ **آزمایشی** می‌داد تا ده دقیقه به برنامه
        //  نمی‌رسید (سنجهٔ `livestack` با سرورِ واقعی گرفتش: نود ثانیه، هنوز
        //  «دورهٔ آزمایشی · ۲۹ روز»). حالا هر بار که حالِ خوانده‌شده از
        //  `/api/pump/me` با آخرین حالی که مجوزش را گرفته‌ایم فرق کند، همان
        //  دور مجوز می‌آید.
        //  ⚠️ کلید «روزِ مانده» ندارد (هر روز عوض می‌شود) و فقط **یک بار** به
        //  ازای هر تغییر می‌زند، پس ترمزِ «هیچ چیزی عوض نشد ⇒ صفر درخواست»
        //  سرِ جایش است.
        //  ⚠️ «آخرین حال» به **همان مجوزِ روی دیسک** بسته است، نه به حافظهٔ
        //  خام: مجوزی که این نمونه نگرفته (برنامهٔ تازه‌بازشده، پروفایلی که
        //  خودش تازه کرد) نخستین دیدنش «خطِ پایه» است، نه «چیزِ تازه» — وگرنه
        //  هر بالا آمدنِ برنامه یک درخواستِ بی‌دلیل می‌زد.
        var key = SubKey(Subscription);
        var lic = _settings.CloudLicense ?? "";
        var news = false;
        if (_subscriptionKnown)
        {
            if (_lastSub.License != lic) _lastSub = (lic, key);
            else news = _lastSub.Key != key;
        }

        if (!due && !mismatch && !news) return;
        try
        {
            var r = await RefreshAsync(ct);
            //  ⚠️ کلیدِ **پیش از** تازه‌سازی ثبت می‌شود: `RefreshAsync` حال را
            //  از `device/me` می‌خواند که شکلِ دیگری دارد، و ثبتِ آن یعنی
            //  دورِ بعد `/api/pump/me` «تازه» دیده می‌شد — یک درخواست هر دقیقه.
            if (r.Ok) _lastSub = (_settings.CloudLicense ?? "", key);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* بی‌اینترنت خطا نیست — مجوزِ دیروز سرِ جایش است */ }
    }

    /// <summary>آخرین حالِ اشتراک که مجوزش گرفته شد — کلیدِ «چیزِ تازه‌ای شد؟».</summary>
    private static (string License, string Key) _lastSub = ("\u0000", "");

    private static string SubKey(PumpSubscription s) =>
        $"{s.Active}|{s.Source}|{s.PlanTitle}|{s.EndsAt / 3_600_000}|{string.Join(",", s.Features)}";

    /// <summary>
    /// مجوزِ روی دیسک عوض شد — تنها خبرِ آن. پوستهٔ برنامه با همین سربرگ و
    /// پروفایل را از نو می‌خواند (<c>MainViewModel</c>).
    ///
    /// ⛔ بی این، مجوزی که حلقهٔ پس‌زمینه گرفته بود فقط با **باز کردنِ
    /// دوبارهٔ پروفایل** دیده می‌شد: دکمهٔ سربرگ همچنان «VIP · ۲۹ روز» ِ
    /// دیروز را می‌گفت و صاحبِ پمپ گمان می‌کرد اشتراک نرسید.
    /// </summary>
    public static event Action? LicenseChanged;

    /// <summary>
    /// سپردنِ نشانی و رمزِ فقط‌خواندنیِ سرورِ خانگی به ابر.
    ///
    /// همان چیزی که اپِ کارمند را از پرسیدنِ آدرس بی‌نیاز می‌کند: آی‌پیِ
    /// خانگی با هر بار روشن شدنِ مودم عوض می‌شود، پس این باید پرتکرار و
    /// ارزان باشد.
    /// </summary>
    public async Task<CloudResult> PublishHomeAsync(string homeUrl, string readKey,
        CancellationToken ct = default, string station = "")
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(homeUrl)) return CloudResult.No("نشانیِ خانگی خالی است");

        //  ⛔ کدِ پوشهٔ **واقعیِ** همین پمپ روی سرورِ خانگی هم می‌رود (۱۴۰۵/۰۷/۱۳):
        //  اپِ کارمندان با همین پوشه را می‌پرسد. تا امروز سرورِ حساب کورکورانه
        //  کدِ خودش را به گوشی می‌داد، و پوشهٔ برنامه («pump1» یا جایگزینِ
        //  حساب) چیزِ دیگری بود — یعنی درِ شبکهٔ پمپ هیچ‌وقت درست باز نمی‌شد.
        //  سرورِ حسابِ کهنه این فیلد را نادیده می‌گیرد.
        var (ok, _, why, code) = await DevPostAsync("/api/pump/device/home",
            new { homeUrl, readKey = readKey ?? "", station = (station ?? "").Trim() }, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>
    /// نوشتنِ یک فایل در پوشهٔ ابریِ همین پمپ — ‎PUT /api/pump/device/files/&lt;نام&gt;‎.
    /// برای کیو‌آرِ زنده (‎AcctLive‎). سرور خودش اشتراک را می‌سنجد؛ اگر تمام
    /// شده باشد ‎subscription_required‎ برمی‌گردد و این‌جا فقط ‎false‎ می‌شود.
    /// </summary>
    public async Task<CloudResult> PutFileAsync(string name, object data, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(name)) return CloudResult.No("نامِ فایل خالی است");

        var (ok, _, why, code) = await DevPutAsync("/api/pump/device/files/" + Uri.EscapeDataString(name.Trim()),
            new { data }, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    // ══ خبرها ═══════════════════════════════════════════════════════════
    //
    // ⛔ چیزی که تا امروز نبود:
    //
    // خواستهٔ صاحبِ ریپوی shop: «برنامه‌ها جوری باشند که بسته هم باشند،
    // هر اتفاقی که در برنامه بیفتد به سرور برود و سرور وقتی برنامه‌ها
    // بسته هم هستند پیام را برایشان بدهد.»
    //
    // بخشِ دکان این را از روزِ اول داشت (`/api/events`). پمپ نداشت:
    // «اضافه برد» و «کم مانده» فقط روی سرورِ **خانگی** می‌نشستند و
    // گوشیِ کارمند هر پانزده دقیقه از **همان شبکه** می‌پرسید. پس
    // صاحبِ پمپی که بیرون بود — یا مودمش خاموش بود — هیچ‌وقت خبر
    // نمی‌گرفت.
    //
    // ⚠️ فهرست از همان `StationSnapshot.Alerts` می‌آید و جای دیگری
    // ساخته نمی‌شود؛ وگرنه روزی کارتِ قرض‌دار سرخ است و گوشی ساکت.

    /// <summary>
    /// فرستادنِ یک دسته خبر به دفترِ ابریِ همین پمپ.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>کلیدِ هر خبر (<c>clientId</c>) لازم است.</b> سرور با همان
    /// ردیفِ تکراری نمی‌سازد، پس صفی که دو بار برسد دو زنگ نمی‌زند و
    /// خبرِ دیروز فردا دوباره بالا نمی‌آید.
    ///
    /// ⚠️ هیچ‌وقت استثنا بیرون نمی‌دهد — خبر رفاه است، دفتر اصل.
    /// </remarks>
    public async Task<CloudResult> SendEventsAsync(
        IEnumerable<object> events, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var list = events?.ToList() ?? new List<object>();
        if (list.Count == 0) return CloudResult.Done;

        var (ok, _, why, code) = await DevPostAsync("/api/pump/device/events",
            new { events = list }, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>
    /// ══ حالِ زندهٔ پمپ ⇒ سرورِ حساب ⇒ بات ══════════════════════════════════
    ///
    /// <para>
    /// <b>همهٔ</b> هشدارهای بازِ همین حالا (نه فقط تازه‌ها)، موجودیِ دو مخزن،
    /// و — اگر داده شد — خلاصهٔ حالِ قرض‌داران برای جست‌وجوی بات. سرور خودش
    /// می‌سنجد چه باز شد و چه بسته شد (<c>lib/pump-state.js</c> در ریپوی
    /// <c>shop</c>)؛ پس بستن و باز کردنِ برنامه دیگر همان هشدارها را «تازه»
    /// نمی‌کند — همان «حرف‌های تکراری»ِ بات.
    /// </para>
    /// <para>
    /// ⚠️ سرورِ کهنه این مسیر را ندارد و ‎not_found‎ می‌دهد؛ صدا‌زننده همان را
    /// می‌بیند و به <see cref="SendEventsAsync"/> برمی‌گردد.
    /// </para>
    /// </summary>
    /// <param name="owe">بدهیِ پمپ به شرکت‌ها (‎[{n, afn, usd}]‎) — ‎null‎ ⇒ همان قبلی می‌ماند.</param>
    public async Task<CloudResult> SendStateAsync(
        IEnumerable<object> alerts, object tank, IEnumerable<object>? debtors, CancellationToken ct = default,
        IEnumerable<object>? owe = null)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var body = new Dictionary<string, object?> { ["alerts"] = alerts.ToList(), ["tank"] = tank };
        if (debtors is not null) body["debtors"] = debtors.ToList();
        if (owe is not null) body["owe"] = owe.ToList();
        var (ok, _, why, code) = await DevPostAsync("/api/pump/device/state", body, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    // ══ پشتیبانِ ابری ═══════════════════════════════════════════════════
    //
    // ⛔ چیزی که تا امروز نبود:
    //
    // `BackupPusher` هر شش ساعت پشتیبان می‌گرفت و فقط به **سرورِ خانگی**
    // می‌فرستاد. مودم که بسوزد، یا کامپیوترِ سرور که خراب شود، همان‌جا
    // با هم می‌روند — و `data/stations/<کد>/backups/` روی همان یک دیسک
    // است. قابلیتش هم `cloudbackup` نام داشت، که گمراه‌کننده بود: هیچ
    // ابری در کار نبود.
    //
    // حالا همان فایل به ابر هم می‌رود: پوشهٔ همین پمپ، سهمِ همین پمپ.
    //
    // ⚠️ بدنه **خام** است، نه JSON. پشتیبانِ یک پمپِ پنج‌ساله چند صد
    // مگابایت است؛ داخلِ JSON باید base64 می‌شد — یک‌سوم بزرگ‌تر و کلِ
    // فایل دو بار در حافظه.

    /// <summary>یک نسخهٔ پشتیبان روی پوشهٔ ابریِ همین پمپ.</summary>
    /// <param name="file">مسیرِ فایل — همان چیزی که ‎VACUUM INTO‎ ساخته</param>
    /// <remarks>
    /// ⛔ <b>فایل جریانی می‌رود، نه یک‌جا در حافظه.</b> همان قاعده‌ای که
    /// مقصدِ خانگی دارد و <c>InfraTests.Poshtiban_FileRa_YekJa_DarHafeze_Nemikhanad</c>
    /// قفلش کرده: دفترِ چندصد مگابایتیِ یک پمپِ چندساله نباید هر شش ساعت
    /// همان‌قدر رم بخواهد. یک بار همین‌جا با <c>ReadAllBytesAsync</c>
    /// نوشته شد و همان آزمون گرفتش.
    ///
    /// ⚠️ به همین دلیل مسیرِ فایل می‌گیرد، نه <c>byte[]</c>: امضایی که
    /// آرایه بخواهد، خودش دعوت به خواندنِ کلِ فایل در حافظه است.
    /// </remarks>
    public async Task<CloudResult> BackupUploadAsync(
        string file, string label = "", bool manual = false, string ext = "db",
        CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            return CloudResult.No("فایلِ پشتیبان پیدا نشد", "empty_backup");

        var path = "/api/pump/device/backups"
            + "?ext=" + Uri.EscapeDataString(ext)
            + "&kind=" + (manual ? "manual" : "auto")
            + "&label=" + Uri.EscapeDataString(label ?? "");

        await using var stream = new FileStream(
            file, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);
        if (stream.Length == 0) return CloudResult.No("فایلِ پشتیبان خالی است", "empty_backup");

        using var body = new StreamContent(stream, 64 * 1024);
        body.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        body.Headers.ContentLength = stream.Length;

        using var req = new HttpRequestMessage(HttpMethod.Post, CloudConfig.Url(path)) { Content = body };
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, _, why, code) = await Send(req, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>فهرستِ پشتیبان‌های ابریِ همین پمپ، تازه‌ترین اول.</summary>
    public async Task<(bool Ok, List<CloudBackup> Items, CloudBackupStats Stats, string Why)>
        BackupListAsync(CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), CloudBackupStats.None, "فعال نشده");
        var (ok, json, why, _) = await DevGetAsync("/api/pump/device/backups", ct);
        if (!ok) return (false, new(), CloudBackupStats.None, why);

        var list = new List<CloudBackup>();
        if (json.TryGetProperty("backups", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var b in arr.EnumerateArray())
                list.Add(new CloudBackup(Str(b, "id"), Str(b, "name"), Num(b, "bytes"),
                    Str(b, "kind"), Str(b, "label"), Num(b, "createdAt")));

        var stats = CloudBackupStats.None;
        if (json.TryGetProperty("stats", out var st) && st.ValueKind == JsonValueKind.Object)
            stats = new CloudBackupStats((int)Num(st, "count"), (int)Num(st, "keep"),
                Num(st, "usedBytes"), Num(st, "quotaBytes"), Num(st, "lastAt"),
                st.TryGetProperty("paid", out var p) && p.ValueKind == JsonValueKind.True);

        return (true, list, stats, "");
    }

    // ══ پشتیبانیِ صاحبِ پمپ ↔ مدیرِ سامانه ═══════════════════════════════
    //
    // ⚠️ **این با چتِ پایین یکی نیست و نباید قاطی شود.**
    //
    //   چتِ پایین  = مشتریِ کیو‌آر ↔ صاحبِ پمپ   (‎/chat/…‎)
    //   این یکی    = صاحبِ پمپ ↔ کسی که برنامه را ساخته (‎/support/…‎)
    //
    // ⛔ تا امروز پمپ‌داری که گیر می‌کرد **هیچ دری** نداشت: `/api/support`
    // مالِ بخشِ دکان بود و این برنامه حساب ندارد. حالا رشته به خودِ پمپ
    // بسته است (`station_id`)، پس گوشیِ صاحب و این کامپیوتر به **یک**
    // گفت‌وگو می‌رسند.
    //
    // ⛔ و هیچ‌وقت پشتِ اشتراک نمی‌رود: «پشتیبانی یکی از واجبات است.»
    // کسی که اشتراکش تمام شده، بیشتر از همه لازم دارد بپرسد چرا.

    /// <summary>گفت‌وگو با پشتیبانی — پیام‌های بعد از ‎after‎.</summary>
    public async Task<(bool Ok, List<CloudChatMessage> Messages, int Unread, string Why)>
        SupportThreadAsync(long after = 0, CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), 0, "فعال نشده");
        var (ok, json, why, _) = await DevGetAsync(
            "/api/pump/device/support/thread?after=" + after, ct);
        if (!ok) return (false, new(), 0, why);

        var list = new List<CloudChatMessage>();
        if (json.TryGetProperty("messages", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var m in arr.EnumerateArray())
            {
                var parsed = CloudChatMessage.ParseSupport(m);
                if (parsed is not null) list.Add(parsed);
            }

        var unread = 0;
        if (json.TryGetProperty("thread", out var th) && th.ValueKind == JsonValueKind.Object)
            unread = (int)Num(th, "unreadUser");

        return (true, list, unread, "");
    }

    /// <summary>پیام به پشتیبانی.</summary>
    public async Task<CloudResult> SupportSendAsync(string text, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(text)) return CloudResult.No("پیام خالی است", "empty_message");
        var (ok, _, why, code) = await DevPostAsync("/api/pump/device/support/messages",
            new { body = text.Trim() }, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>«خواندم» — نقطهٔ قرمز را پاک می‌کند.</summary>
    public async Task<bool> SupportSeenAsync(CancellationToken ct = default)
    {
        if (!Activated) return false;
        var (ok, _, _, _) = await DevPostAsync("/api/pump/device/support/read",
            new { }, ct);
        return ok;
    }

    // ── چتِ پشتیبانی — مشتریِ کیو‌آر ↔ صاحبِ پمپ ────────────────────────
    //
    // خواستهٔ صاحب ریپو: «داخلِ کیو‌آر یک چتِ پشتیبانی با من داشته باشد… عینِ
    // واتساپ.» مشتری با رمزِ حسابش روی ابر می‌نویسد؛ این‌ها همان درها از
    // سمتِ برنامه‌اند (‎/api/pump/device/chat/…‎ با توکنِ دستگاه).

    /// <summary>گفت‌وگوها با نخوانده‌ها و آخرین پیام.</summary>
    public async Task<(bool Ok, List<CloudChatThread> Threads, string Why)> ChatThreadsAsync(CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), "فعال نشده");
        var (ok, json, why, _) = await DevGetAsync("/api/pump/device/chat/threads", ct);
        if (!ok) return (false, new(), why);
        var list = new List<CloudChatThread>();
        if (json.TryGetProperty("threads", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var t in arr.EnumerateArray())
            {
                CloudChatMessage? last = t.TryGetProperty("last", out var l) && l.ValueKind == JsonValueKind.Object
                    ? CloudChatMessage.Parse(l, Str(t, "acct")) : null;
                list.Add(new CloudChatThread(Str(t, "acct"), Str(t, "name"),
                    t.TryGetProperty("blocked", out var b) && b.ValueKind == JsonValueKind.True,
                    (int)Num(t, "unread"), Num(t, "updatedAt"), last));
            }
        return (true, list, "");
    }

    /// <summary>همهٔ پیام‌های تازهٔ همهٔ گفت‌وگوها بعد از ‎after‎.</summary>
    public async Task<(bool Ok, List<CloudChatMessage> Messages, string Why)> ChatInboxAsync(long after, CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), "فعال نشده");
        var (ok, json, why, _) = await DevGetAsync("/api/pump/device/chat/inbox?after=" + after, ct);
        if (!ok) return (false, new(), why);
        return (true, CloudChatMessage.ParseList(json), "");
    }

    /// <summary>پیامِ متنی یا رسانه‌ای به یک حساب. ‎kind‎ی خالی یعنی متن.</summary>
    public async Task<(bool Ok, CloudChatMessage? Message, string Why)> ChatSendAsync(
        string acct, string name, string text, string kind = "", string? mediaId = null, CancellationToken ct = default)
    {
        if (!Activated) return (false, null, "فعال نشده");
        object body = string.IsNullOrEmpty(kind)
            ? new { name, text }
            : new { name, kind, mediaId };
        var (ok, json, why, _) = await DevPostAsync("/api/pump/device/chat/" + Uri.EscapeDataString(acct), body, ct);
        if (!ok) return (false, null, why);
        return (true, json.TryGetProperty("message", out var m) ? CloudChatMessage.Parse(m, acct) : null, "");
    }

    /// <summary>بالا بردنِ عکس/ویدیو/صدا — خام، با نوعش. خروجی شناسهٔ رسانه.</summary>
    public async Task<(bool Ok, string MediaId, string Why)> ChatUploadAsync(
        string acct, byte[] bytes, string mime, CancellationToken ct = default)
    {
        if (!Activated) return (false, "", "فعال نشده");
        var req = new HttpRequestMessage(HttpMethod.Post,
            CloudConfig.Url("/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/media"))
        {
            Content = new ByteArrayContent(bytes),
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, json, why, _) = await Send(req, ct);
        return ok ? (true, Str(json, "mediaId"), "") : (false, "", why);
    }

    /// <summary>خودِ رسانه — بایت‌ها و نوعش. ‎null‎ یعنی نرسید.</summary>
    public async Task<(byte[] Bytes, string Mime)?> ChatMediaAsync(string mediaId, CancellationToken ct = default)
    {
        if (!Activated || string.IsNullOrWhiteSpace(mediaId)) return null;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                CloudConfig.Url("/api/pump/device/chat/media/" + Uri.EscapeDataString(mediaId)));
            req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
            using var res = TestTransport is null
                ? await Http.SendAsync(req, ct)
                : await TestTransport(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            var bytes = await res.Content.ReadAsByteArrayAsync(ct);
            return (bytes, res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
        }
        catch { return null; }
    }

    /// <summary>پاک کردنِ نرمِ یک پیام (هر پیامی — صاحبِ پمپ است).</summary>
    public async Task<CloudResult> ChatDeleteAsync(string messageId, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var req = new HttpRequestMessage(HttpMethod.Delete,
            CloudConfig.Url("/api/pump/device/chat/message/" + Uri.EscapeDataString(messageId)));
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, _, why, code) = await Send(req, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>بلاک / رفعِ بلاکِ یک مشتری.</summary>
    public async Task<CloudResult> ChatBlockAsync(string acct, bool blocked, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var req = new HttpRequestMessage(blocked ? HttpMethod.Post : HttpMethod.Delete,
            CloudConfig.Url("/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/block"))
        {
            Content = blocked ? JsonContent.Create(new { }) : null,
        };
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, _, why, code) = await Send(req, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>«تا این‌جا خواندم» — نخوانده‌های همان گفت‌وگو صفر می‌شود.</summary>
    public async Task<bool> ChatSeenAsync(string acct, long seq, CancellationToken ct = default)
    {
        if (!Activated) return false;
        var (ok, _, _, _) = await DevPostAsync("/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/seen",
            new { seq }, ct);
        return ok;
    }

    /// <summary>کدِ کوتاهی که کارمند با آن به این پمپ می‌پیوندد.</summary>
    public async Task<(bool Ok, string Code, string Why)> JoinCodeAsync(CancellationToken ct = default)
    {
        if (!Activated) return (false, "", "این برنامه هنوز فعال نشده است");
        var (ok, json, why, _) = await DevPostAsync("/api/pump/device/join-code",
            new { role = "staff", hours = 24, maxUses = 10 }, ct);
        return ok ? (true, Str(json, "code"), "") : (false, "", why);
    }

    /// <summary>
    /// کدِ دسترسیِ پمپ — همان کدی که هر کسی اپِ گوشی را نصب می‌کند باید بزند.
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «برای هر پمپ یک کد باشد… با پمپ‌های دیگر قاطی
    /// نشود.» سرور برای هر پمپ یک کدِ دائمی دارد؛ ‎rotate‎ کدِ تازه می‌سازد و
    /// کدِ قبلی همان لحظه از کار می‌افتد. نسخهٔ آخر در تنظیمات می‌ماند تا
    /// بی‌اینترنت هم دیده شود.
    /// </summary>
    public async Task<(bool Ok, string Code, string Why)> AccessCodeAsync(bool rotate = false,
                                                                          CancellationToken ct = default)
    {
        if (!Activated) return (false, _settings.CloudAccessCode, "این برنامه هنوز فعال نشده است");
        var (ok, json, why, _) = rotate
            ? await DevPostAsync("/api/pump/device/access-code/rotate", new { }, ct)
            : await DevGetAsync("/api/pump/device/access-code", ct);
        if (!ok) return (false, _settings.CloudAccessCode, why);
        var code = Str(json, "code");
        if (code.Length > 0 && code != _settings.CloudAccessCode)
        {
            _settings.CloudAccessCode = code;
            await _save();
        }
        return (true, code, "");
    }

    /*
     *  ══ کدِ هشت‌رقمیِ اپِ گوشی، بی زدنِ هیچ دکمه‌ای (۱۴۰۵/۰۷/۱۴) ═══════════
     *
     *  خواستهٔ صریحِ صاحب سامانه: «برای هر حساب کاربری یک کد هشت‌رقمی درست
     *  بشه.» سرورِ حساب (۲.۱۱.۷) کد را همان لحظهٔ ساختنِ پمپ می‌سازد؛ ولی
     *  برنامه تا امروز فقط با «گرفتنِ کد»ِ پروفایل آن را می‌پرسید، پس پروفایل
     *  «هنوز گرفته نشده» می‌گفت و نصبی که کدِ حرفیِ پیشین را نگه داشته بود
     *  هیچ‌وقت کدِ هشت‌رقمی را نمی‌دید.
     *
     *  ⛔ حلقهٔ پس‌زمینه حالا خودش می‌پرسد — **یک بار در هر اجرا**، و تا
     *  کدِ روی دیسک هشت رقم نیست هر ده دقیقه یک بار (یک GETِ سبک). هیچ کدی
     *  این‌جا ساخته یا عوض نمی‌شود؛ فقط خوانده می‌شود.
     */
    private static DateTime _codeCheckedAt = DateTime.MinValue;
    private static readonly TimeSpan CodeRecheck = TimeSpan.FromMinutes(10);

    /// <summary>کدِ امروزی: هشت رقم.</summary>
    public static bool IsDigitCode(string? code)
    {
        var c = (code ?? "").Replace("-", "").Trim();
        return c.Length == 8 && c.All(char.IsAsciiDigit);
    }

    public async Task KeepAccessCodeAsync(CancellationToken ct = default)
    {
        if (!Activated) return;
        var fresh = _codeCheckedAt != DateTime.MinValue && IsDigitCode(_settings.CloudAccessCode);
        if (fresh || DateTime.UtcNow - _codeCheckedAt < CodeRecheck) return;
        _codeCheckedAt = DateTime.UtcNow;
        try { await AccessCodeAsync(false, ct); }
        catch { /* بی‌اینترنت خطا نیست — کدِ روی دیسک سرِ جایش است */ }
    }

    /// <summary>برای نمایش: ‎4829-1736‎ — همان قاعدهٔ سرور و اپِ گوشی.</summary>
    public static string FormatAccessCode(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        return c.Length == 8 ? c[..4] + "-" + c[4..] : c;
    }

    // ── حساب: ورود با گوگل ─────────────────────────────────────────────

    /// <summary>آیا با حسابِ گوگل وارد شده‌ایم.</summary>
    public bool SignedIn => !string.IsNullOrWhiteSpace(_settings.CloudAccountToken);

    /// <summary>ایمیلِ حساب — برای نشان دادن در صفحهٔ حساب.</summary>
    public string Email => _settings.CloudEmail;

    /// <summary>نامِ حساب — برای نشان دادن در صفحهٔ حساب.</summary>
    public string Name => _settings.CloudName;

    /// <summary>
    /// شناسهٔ کلاینتِ گوگل را از خودِ سرور می‌پرسد.
    ///
    /// ⚠️ از داخلِ کد نمی‌آید تا عوض شدنش نسخهٔ تازهٔ برنامه نخواهد — همان
    /// قاعده‌ای که اپِ کارمندان هم دارد.
    /// </summary>
    public async Task<string> GoogleClientIdAsync(CancellationToken ct = default)
    {
        var (ok, json, _, _) = await GetAsync("/api/config", null, ct);
        if (!ok) return "";
        //  شناسهٔ Desktop مالِ همین برنامه است؛ شناسهٔ Web مالِ سایت و اپِ
        //  کارمندان. اگر سرور هنوز جدا نکرده باشد، به همان یکی برمی‌گردیم.
        var desktop = Str(json, "googleDesktopClientId");
        return desktop.Length > 0 ? desktop : Str(json, "googleClientId");
    }

    /// <summary>
    /// توکنِ هویتِ گوگل را به سرور می‌دهد و نشستِ حساب می‌گیرد.
    /// </summary>
    public async Task<CloudResult> SignInAsync(string idToken, CancellationToken ct = default)
    {
        var (ok, json, why, code) = await PostAsync(
            "/api/auth/google", new { idToken, app = "pump" }, null, ct);
        if (!ok) return CloudResult.No(why, code);

        return await SeatAsync(json);
    }

    /// <summary>
    /// نشستی که سرور داد را می‌نشاند.
    ///
    /// ⚠️ **سرور `accessToken` می‌دهد، نه `token`** — و برنامه تا امروز فقط
    /// دنبالِ `token` بود، پس هر ورودِ **موفقی** را «سرور نشست نداد»
    /// می‌خواند. با آزمونِ واقعیِ سرور دیده شد
    /// (`shop/server/test/pump-account.test.js`). هر دو نام خوانده می‌شوند
    /// تا نسخهٔ قدیمیِ سرور هم کار کند.
    /// </summary>
    /// <summary>
    /// ══ «این کامپیوتر حالا مالِ حسابِ دیگری است» ═════════════════════════
    ///
    /// ⛔ <b>نشتی که این می‌بندد</b> (بندِ ۱۲ی خواستهٔ صاحب ریپو: «اگر یک
    /// حساب از دستگاه دیگری وارد شود… اطلاعاتِ حسابِ قبلی نباید باقی
    /// بماند، با حسابِ جدید مخلوط نشود، و توکنِ حسابِ قبلی اشتباهاً برای
    /// حسابِ جدید استفاده نشود»): تا امروز ورودِ یک حسابِ <b>دیگر</b> روی
    /// همین نصب فقط چهار فیلدِ حساب را عوض می‌کرد و بندهای پمپِ قبلی
    /// دست‌نخورده می‌ماندند — توکنِ <b>دستگاه</b>، شناسهٔ پمپ، مجوزِ
    /// امضاشده، کلیدِ عمومی، کدِ اپِ کارمندان، نشانی و رمزِ سرورِ خانگی و
    /// مُهرِ ارفاق. نتیجه‌اش سه چیز بود:
    ///   • عکسِ حساب‌ها با توکنِ دستگاهِ پمپِ قبلی به پوشهٔ ابریِ <b>او</b>
    ///     می‌رفت،
    ///   • کدِ اپِ کارمندان و اشتراکِ پمپِ قبلی روی پروفایلِ حسابِ تازه
    ///     دیده می‌شد (و ارفاقش برای این یکی خرج می‌شد)،
    ///   • و <see cref="HomeFromAccountAsync"/> با «این حساب مالِ پمپِ
    ///     دیگری است — دستگاه را جدا کنید» جلوی کاربر دیوار می‌کشید،
    ///     در حالی که <b>هیچ راهی برای جدا کردن نبود</b>:
    ///     <see cref="ForgetStationAsync"/> نوشته شده بود و هیچ‌جا صدا
    ///     زده نمی‌شد. یعنی جابه‌جاییِ پمپ عملاً ناممکن بود.
    ///
    /// ⚠️ <b>فقط با شناسهٔ حساب تصمیم می‌گیریم</b>، و فقط وقتی شناسهٔ کهنه
    /// را <b>داریم</b> و با تازه <b>یکی نیست</b>. پس این سه حالت هیچ چیزی
    /// را باز نمی‌کنند: ورودِ دوبارهٔ همان حساب، نخستین ورودِ یک نصبِ تازه،
    /// و نصبی که شناسه‌اش را ندارد (نسخهٔ پیش از امروز) و ایمیلش هم همان
    /// است. «قفلِ ناخواسته بدتر از بازِ ناخواسته است» این‌جا هم برقرار
    /// است: مشتریِ امروزی نباید با یک به‌روزرسانی از پمپش جدا شود.
    ///
    /// ⚠️ و برای نصبِ کهنه‌ای که شناسه ندارد، <b>ایمیل</b> ملاک است —
    /// همان چیزی که داریم. (ایمیلِ عوض‌شدهٔ همان حساب یک بار بندها را
    /// بی‌دلیل باز می‌کند و کاربر دوباره کدِ شش‌رقمی می‌زند؛ بدترین
    /// حالتش همین است، و دفتر دست نمی‌خورد.)
    ///
    /// ⛔ <b>یک بیت از دفتر لمس نمی‌شود</b> — همان قاعدهٔ
    /// <see cref="ForgetStationAsync"/>.
    /// </summary>
    private async Task ReleaseIfOtherAccountAsync(JsonElement json)
    {
        //  ⚠️ هر ورود از صفر: وگرنه هشدارِ یک جابه‌جاییِ قدیمی سرِ ورودهای
        //  بعدیِ همان حساب هم نشان داده می‌شد.
        AccountSwitched = false;

        if (!json.TryGetProperty("user", out var u) || u.ValueKind != JsonValueKind.Object) return;

        var freshId = Ident(u, "id");
        var freshMail = Str(u, "email").Trim();
        var oldId = (_settings.CloudUserId ?? "").Trim();
        var oldMail = (_settings.CloudEmail ?? "").Trim();

        bool other;
        if (oldId.Length > 0 && freshId.Length > 0)
            other = !string.Equals(oldId, freshId, StringComparison.Ordinal);
        else if (oldMail.Length > 0 && freshMail.Length > 0)
            other = !string.Equals(oldMail, freshMail, StringComparison.OrdinalIgnoreCase);
        else
            other = false;

        if (!other) return;

        //  بندی هست که باز شود؟ نصبی که هیچ‌وقت فعال نشده چیزی ندارد.
        //
        //  ⛔ **و کلیدِ عمومی هم یک بند است.** این خط تا ۱۴۰۵/۰۷/۱۱ سه
        //  موردِ اول را داشت و کلید را نه — و همان یک قلم، دیوارِ
        //  نامرئیِ «هر بار می‌روم پروفایل، دوباره اسمِ پمپ را می‌خواهد»
        //  بود:
        //
        //    ۱) نصبی که یک بار با حسابِ الف کلید را قفل کرده ولی هنوز
        //       توکنِ دستگاه نگرفته بود (bind نیمه‌کاره) ⇒ `bound` دروغ
        //       می‌شد؛
        //    ۲) پس ورود با حسابِ ب `ForgetStationAsync` را **صدا نمی‌زد**
        //       و کلیدِ حسابِ الف سرِ جا می‌ماند؛
        //    ۳) از آن به بعد هر `BindAsync` با `key_mismatch` رد می‌شد —
        //       بی هیچ پیامی، چون `HomeFromAccountAsync` نتیجه‌اش را
        //       می‌بلعد؛
        //    ۴) پس توکنِ دستگاه هیچ‌وقت نمی‌آمد، «فعال نشده» می‌ماند،
        //       مجوز و دورهٔ آزمایشیِ ۳۰ روزه هم هیچ‌وقت صادر نمی‌شد.
        //
        //  یعنی یک قفلِ ضدِ کرک، به جانِ خودِ مشتری افتاده بود.
        var bound = (_settings.CloudDeviceToken ?? "").Length > 0
                 || (_settings.CloudStationId ?? "").Length > 0
                 || (_settings.CloudAccessCode ?? "").Length > 0
                 || (_settings.CloudPublicKey ?? "").Length > 0;

        AccountSwitched = bound;
        if (bound) await ForgetStationAsync();
    }

    /// <summary>
    /// آخرین ورود، حسابِ دیگری بود و بندهای پمپِ قبلی باز شدند.
    ///
    /// ⚠️ برای همین است که هست: قفلی که بی‌صدا باشد در چشمِ کاربر باگ
    /// است. صفحهٔ ورود همین را به او می‌گوید تا بداند چرا باید کدِ
    /// شش‌رقمیِ پمپش را دوباره بزند.
    /// </summary>
    public bool AccountSwitched { get; private set; }

    /// <summary>
    /// چرا آخرین بند شدنِ دستگاه نشد — خالی یعنی مشکلی نبود.
    ///
    /// ⛔ <b>تا ۱۴۰۵/۰۷/۱۱ این دلیل هیچ‌جا نمی‌رفت.</b> حلقهٔ
    /// شصت‌ثانیه‌ای هر دور <c>BindAsync</c> را می‌زد و نتیجه‌اش را دور
    /// می‌ریخت، پس یک شکستِ دائمی (مثلِ <c>key_mismatch</c>) برای همیشه
    /// نامرئی بود و کاربر فقط «سرورِ حساب: فعال نشده» را می‌دید.
    ///
    /// ⚠️ ایستا است چون هر درخواست یک <c>CloudLink</c>ِ تازه می‌سازد —
    /// همان الگوی <see cref="Reach"/>.
    /// </summary>
    public static string LastBindWhy { get; private set; } = "";

    /// <summary>
    /// «حسابِ واردشده روی سرور پمپ دارد؟» — از آخرین جوابِ واقعیِ
    /// <c>/api/pump/me</c>؛ <c>null</c> یعنی هنوز نپرسیده‌ایم.
    ///
    /// ⛔ <b>چراغ و پروفایل از همین می‌خوانند، نه از <c>PumpStepDone</c>ِ روی
    /// دیسک</b> (۱۴۰۵/۰۷/۱۳، سنجهٔ <c>linkstates</c>): آن مُهر می‌گوید «روزی
    /// پمپ داشت»، نه «همین حالا دارد» — و پمپی که از پنل حذف شده بود با مُهرِ
    /// کهنه هیچ‌وقت کارتِ «ساختنِ پمپ» را نشان نمی‌داد. ایستا است، همان
    /// الگوی <see cref="LastBindWhy"/>.
    /// </summary>
    public static bool? AccountHasStation { get; set; }

    /// <summary>آخرین ثبتِ ناموفقِ خودکار — ترمزِ حلقه (بالای `bindDue` نوشته چرا).</summary>
    private static DateTime _lastBindFailAt = DateTime.MinValue;

    /// <summary>پس از شکستِ ثبت، حلقهٔ پس‌زمینه تا این مدت دوباره نمی‌زند.</summary>
    public static readonly TimeSpan BindRetryAfterFail = TimeSpan.FromMinutes(10);

    private async Task<CloudResult> SeatAsync(JsonElement json)
    {
        var token = Str(json, "accessToken");
        if (token.Length == 0) token = Str(json, "token");
        if (token.Length == 0) return CloudResult.No("سرور نشست نداد");

        //  ⚠️ **پیش از نشستنِ توکنِ تازه** — وگرنه یک لحظه توکنِ حسابِ تازه
        //  کنارِ توکنِ دستگاهِ حسابِ قبلی می‌نشیند و هر انتشاری که در همان
        //  لحظه بدود به پوشهٔ پمپِ قبلی می‌رود.
        await ReleaseIfOtherAccountAsync(json);

        _settings.CloudAccountToken = token;
        var refresh = Str(json, "refreshToken");
        if (refresh.Length > 0) _settings.CloudRefreshToken = refresh;
        //  ⚠️ عمرِ توکن را از خودِ سرور برمی‌داریم (یک ساعت)، تا کمی پیش از
        //  انقضا خودمان تازه کنیم و کاربر هیچ‌وقت به دیوارِ ۴۰۱ نخورد.
        _settings.CloudAccessExpiresAt = Num(json, "accessExpiresAt");
        if (json.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            var em = Str(u, "email"); if (em.Length > 0) _settings.CloudEmail = em;
            var nm = Str(u, "name");  if (nm.Length > 0) _settings.CloudName = nm;
            //  ⚠️ شناسه **جانشین** می‌شود، نه «اگر بود»: حسابِ تازه باید
            //  شناسهٔ خودش را بنشاند، وگرنه دفعهٔ بعد شناسهٔ حسابِ قبلی
            //  ملاکِ مقایسه می‌ماند.
            var id = Ident(u, "id"); if (id.Length > 0) _settings.CloudUserId = id;
        }
        await SaveQuiet();
        return CloudResult.Done;
    }

    // ── حساب با ایمیل و رمز ─────────────────────────────────────────────
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «نه می‌گوید حساب داری یا نه، نه
    //  ثبت یا ساختنِ حساب دارد و نه رمز دارد و می‌خواهد… اول اسم، ایمیل،
    //  رمز، تکرارِ رمز؛ بعد برود بخشِ بعدی: کدِ شش‌رقمی و تاییدش از سرور و
    //  اسمِ پمپ و لوکیشنِ پمپ. همین.»
    //
    //  ⚠️ **رمز هیچ‌جا روی این کامپیوتر ذخیره نمی‌شود** — نه خام، نه هش.
    //  فقط همان یک بار به ابر می‌رود و آن‌جا با توکنِ نشست جواب می‌آید،
    //  دقیقاً مثلِ راهِ گوگل. چیزی که ذخیره می‌شود همان توکن است.
    //
    //  ⚠️ پاسخِ سرور **همان شکلِ راهِ گوگل** است (`{token, refreshToken,
    //  user{email,name}}`) تا هیچ جای دیگرِ برنامه فرق نفهمد.

    // ── ثبت‌نام سه‌پله‌ای: نام و ایمیل و رمز ⇒ کدِ ایمیل ⇒ حساب ─────────
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «می‌خواهم با ایمیل خودم حسابی بسازم،
    //  نمی‌شود.»
    //
    //  ⛔ **درِ یک‌مرحله‌ایِ `/api/auth/register` عمداً بسته است** و
    //  `verification_required` می‌دهد: حساب بی تأییدِ ایمیل ساخته نمی‌شود.
    //  همین را با آزمونِ واقعیِ سرور دیدیم، نه با حدس
    //  (`shop/server/test/pump-account.test.js`). پس راهِ درست سه‌پله است:
    //
    //      ۱) /register/start     نام · ایمیل · رمز · تکرارِ رمز ⇒ کد به ایمیل
    //      ۲) /register/verify    کدِ شش‌رقمیِ ایمیل ⇒ «بلیتِ ثبت‌نام»
    //      ۳) /register/complete  بلیت + پذیرشِ شرایط ⇒ حساب و نشست
    //
    //  ⚠️ **بلیت و رمز هیچ‌وقت روی دیسک نمی‌نشینند** — بلیت در حافظهٔ همین
    //  شیء است و رمز را ویومدل در همان صفحه نگه می‌دارد و بعد پاکش می‌کند.

    // ── ترمزِ «ارسالِ بی‌نهایت» ──────────────────────────────────────────
    //
    //  بندِ آخرِ خواستهٔ صاحب ریپو دربارهٔ ورود: «درخواست‌های ورود قابلِ
    //  سوءاستفاده و ارسالِ بی‌نهایت نباشند.»
    //
    //  ⚠️ سقفِ **واقعی** روی خودِ سرور است و هست (سنجیده شد، نه حدس:
    //  `shop/server/src/routes/auth.js` — مسیرهای کدِ یک‌بارمصرف
    //  `otpLimit` دارند، پنج تا در هر پانزده دقیقه، و ورود و ثبت‌نام
    //  `authLimit` ده تا، به‌علاوهٔ قفلِ هشت‌تلاشیِ خودِ حساب). این ترمزِ
    //  محلی **جایش را نمی‌گیرد** — کارِ دیگری می‌کند:
    //
    //    ۱) دو مسیرِ این‌جا به هر زدنِ دکمه یک **ایمیل** می‌فرستند؛
    //    ۲) و کاربری که پنج بار «دوباره بفرست» را بزند، سقفِ سرور را خرج
    //       می‌کند و **پانزده دقیقه** از ثبت‌نامِ خودش بیرون می‌افتد.
    //
    //  پس شمردنِ محلی هم به سود کاربر است هم به سودِ سرور.
    //
    //  ⚠️ ترمز روی **همین شیء** است، نه static: سنجه‌ها هر کدام لینکِ خودشان
    //  را می‌سازند و ترمزِ یکی سنجهٔ دیگری را قرمز نمی‌کند. در خودِ برنامه
    //  صفحهٔ ورود یک لینک دارد و همان است که دکمه‌اش زده می‌شود.
    //  ⚠️ و فقط با **موفقیت** مهر می‌خورد: درخواستی که به سرور نرسید (مودم
    //  خاموش) هیچ ایمیلی نفرستاده، پس نباید کاربر را یک دقیقه معطل کند.

    /// <summary>فاصلهٔ لازم میان دو ایمیلِ کد — به ثانیه.</summary>
    public const int ResendWaitSeconds = 60;

    private readonly Dictionary<string, long> _lastMail = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>هنوز زود است؟ پیامِ آدمیزاد برمی‌گردد، وگرنه <c>null</c>.</summary>
    private string? TooSoon(string route, string email)
    {
        var key = route + "|" + (email ?? "").Trim();
        if (!_lastMail.TryGetValue(key, out var at)) return null;
        var left = ResendWaitSeconds - (int)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - at) / 1000);
        return left > 0
            ? $"کد همین حالا فرستاده شد — {left} ثانیه صبر کنید و صندوقِ ایمیلتان را ببینید."
            : null;
    }

    private void MailSent(string route, string email) =>
        _lastMail[route + "|" + (email ?? "").Trim()] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>بلیتِ ثبت‌نام — فقط در حافظه، تا پلهٔ سوم.</summary>
    private string _registerTicket = "";

    /// <summary>نسخهٔ شرایطی که همراهِ بلیت آمد — همان را برمی‌گردانیم.</summary>
    private string _termsVersion = "";

    /// <summary>پلهٔ دوم را رفته‌ایم و بلیت داریم؟</summary>
    public bool HasRegisterTicket => _registerTicket.Length > 0;

    /// <summary>پلهٔ یک — کد به ایمیل فرستاده می‌شود. حسابی ساخته نمی‌شود.</summary>
    public async Task<CloudResult> RegisterStartAsync(string name, string email, string password,
                                                      CancellationToken ct = default)
    {
        if (TooSoon("register/start", email) is { } wait) return CloudResult.No(wait, "too_soon");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/register/start", new
        {
            name = (name ?? "").Trim(),
            email = (email ?? "").Trim(),
            password,
            passwordConfirm = password,
            app = "pump",
        }, null), ct);
        if (res.Ok) { MailSent("register/start", email); return CloudResult.Done; }
        //  ⚠️ ۴۰۴ اینجا «سرور جواب نداد» نیست — پایینِ همین فایل، `NoRouteAsync`.
        if (res.Status == 404) return await NoRouteAsync(ct);
        return CloudResult.No(res.Why, res.Code);
    }

    /// <summary>پلهٔ دو — کدِ ایمیل. جوابش بلیتِ بیست‌دقیقه‌ای است.</summary>
    public async Task<CloudResult> RegisterVerifyAsync(string email, string emailCode,
                                                       CancellationToken ct = default)
    {
        var clean = new string((emailCode ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کدِ ایمیل باید شش رقم باشد");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/register/verify",
            new { email = (email ?? "").Trim(), code = clean, app = "pump" }, null), ct);
        if (!res.Ok)
        {
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }
        var json = res.Json;

        _registerTicket = Str(json, "ticket");
        if (_registerTicket.Length == 0) return CloudResult.No("سرور بلیتِ ثبت‌نام نداد");
        if (json.TryGetProperty("terms", out var t) && t.ValueKind == JsonValueKind.Object)
            _termsVersion = Str(t, "version");
        return CloudResult.Done;
    }

    /// <summary>
    /// پلهٔ سه — حساب ساخته می‌شود و نشست می‌آید.
    ///
    /// ⚠️ پذیرشِ شرایط **اجباریِ خودِ سرور** است (`terms_required`)، پس
    /// برنامه باید واقعاً از کاربر پرسیده باشد.
    /// </summary>
    public async Task<CloudResult> RegisterCompleteAsync(string name, string password,
                                                         bool termsAccepted,
                                                         CancellationToken ct = default)
    {
        if (_registerTicket.Length == 0) return CloudResult.No("اول کدِ ایمیل را بزنید");
        if (!termsAccepted) return CloudResult.No("برای ساختنِ حساب باید شرایط را بپذیرید", "terms_required");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/register/complete", new
        {
            ticket = _registerTicket,
            name = (name ?? "").Trim(),
            password,
            terms = new { accepted = true, version = _termsVersion },
            device = new { uid = DeviceUid, name = Environment.MachineName, platform = "windows" },
            app = "pump",
        }, null), ct);
        if (!res.Ok)
        {
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }

        _registerTicket = "";      // بلیت خرج شد
        return await SeatAsync(res.Json);
    }

    /// <summary>متنِ شرایط و ضوابط — همان چیزی که کاربر می‌پذیرد.</summary>
    public async Task<(bool Ok, string Text, string Why)> TermsAsync(CancellationToken ct = default)
    {
        var (ok, json, why, _) = await GetAsync("/api/auth/terms", null, ct);
        if (!ok) return (false, "", why);

        var sb = new System.Text.StringBuilder();
        var title = Str(json, "title");
        if (title.Length > 0) sb.AppendLine(title).AppendLine();
        if (json.TryGetProperty("sections", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var sec in arr.EnumerateArray())
            {
                var st = Str(sec, "title");
                var sb2 = Str(sec, "body");
                if (st.Length > 0) sb.AppendLine("• " + st);
                if (sb2.Length > 0) sb.AppendLine(sb2).AppendLine();
            }
        var text = sb.ToString().Trim();
        return (text.Length > 0, text, text.Length > 0 ? "" : "متنِ شرایط نیامد");
    }

    /// <summary>ورود به حسابی که از قبل ساخته شده.</summary>
    public Task<CloudResult> SignInWithPasswordAsync(string email, string password,
                                                     CancellationToken ct = default) =>
        AuthAsync("/api/auth/login",
                  new { email = (email ?? "").Trim(), password, app = "pump" },
                  ct);

    private async Task<CloudResult> AuthAsync(string path, object body, CancellationToken ct)
    {
        var res = await SendFull(Build(HttpMethod.Post, path, body, null), ct);
        if (!res.Ok)
        {
            //  ⚠️ صادق باش: اگر سرورِ ابر این راه را نداشت، «رمز غلط» نگو.
            //  ⚠️ از روی **کدِ خودِ HTTP** تصمیم می‌گیریم، نه از روی گشتنِ
            //  رشتهٔ «404» در متنِ فارسیِ خطا: سرورِ به‌روز برای مسیرِ نبوده
            //  پیامِ خودش را می‌دهد («این مسیر وجود ندارد») و آن گشتن دیگر
            //  نمی‌گرفت.
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }

        return await SeatAsync(res.Json);
    }

    // ── ۴۰۴ی که «سرور جواب نداد» نیست ────────────────────────────────────
    //
    //  ⛔ **پیامی که کاربر را گمراه می‌کرد** (با عکسِ صاحب ریپو، ۱۴۰۵/۰۷/۰۱):
    //  زیرِ دکمهٔ «ساختنِ حساب و ادامه» نوشته می‌شد «سرور جواب نداد (404)» —
    //  و کاربر درست می‌گفت که این پیام هیچ کاری دستش نمی‌دهد. دو حالِ
    //  کاملاً جدا زیرِ آن یک جمله قایم شده بود، با دو کارِ جدا:
    //
    //      سرورِ حساب بالا است ولی این مسیر را ندارد ⇒ سرورِ حساب را به‌روز کن
    //      خودِ سرورِ حساب جواب نمی‌دهد               ⇒ سرویس/دامنه/پروکسی
    //
    //  ⚠️ **و این از روی خودِ ۴۰۴ دیدنی نیست** — سنجیده شد، حدس نیست:
    //  سرورِ نودِ ما برای مسیرِ نبوده همیشه **JSONِ فارسی** می‌دهد (زیرِ
    //  `/api/auth` می‌شود ۴۰۱ «احراز هویت لازم است»، جای دیگر ۴۰۴ «این مسیر
    //  وجود ندارد») — هر دو نسخهٔ کهنه (۲.۰.۰) و تازه محلی سنجیده شدند و هر
    //  دو همین را دادند. پس ۴۰۴ِ **بی‌بدنه** یعنی درخواست به خودِ سرور
    //  نرسیده و چیزی جلوی آن نشسته. تنها راهِ فهمیدنش پرسیدن از
    //  `/api/health` است، که باز است و توکن نمی‌خواهد.
    //
    //  ⚠️ «برنامه را به‌روز کنید» پیامِ غلطی بود و برداشته شد: برنامه تازه
    //  است و سرور کهنه، پس کاربر را دنبالِ نخودِ سیاه می‌فرستاد.
    //  ⛔ و **نامِ میزبان در پیام نمی‌آید** — همان قاعدهٔ همیشه؛ فقط نسخه.

    // ══ چراغِ ابر — راست می‌گوید یا هیچ نمی‌گوید ═════════════════════════
    //
    //  ⛔ **هیچ‌کدامِ این‌ها حدس نیستند.** `Reach` تنها از `SendFull` پر
    //  می‌شود، یعنی از خودِ جوابِ سرور؛ و «سرورِ ما جواب داد» یعنی یا ۲xx
    //  بود یا خطایی به شکلِ خودمان (`error.code`). یک ۴۰۴ِ بی‌بدنه از
    //  پروکسی **وصل حساب نمی‌شود** — همان چیزی که ۱۴۰۵/۰۷/۰۱ گرفتیم.
    //
    //  ⚠️ چرا ایستا: `CloudLink` در شش جای برنامه با یک `new` ساخته می‌شود
    //  (پروفایل، چت، انتشار، پشتیبان…). حالِ «به ابر می‌رسیم؟» مالِ خودِ
    //  برنامه است، نه مالِ یک نمونه.

    /// <summary>آخرین باری که واقعاً جواب گرفتیم چه بود.</summary>
    public static CloudReach Reach { get; private set; } = CloudReach.Unknown;

    /// <summary>آخرین باری که سرورِ ما واقعاً جواب داد (به وقتِ محلی).</summary>
    public static DateTime? CloudOkAt { get; private set; }

    /// <summary>اگر نرسیدیم، چرا — بی نام و نشانیِ میزبان.</summary>
    public static string CloudWhy { get; private set; } = "";

    private static void NoteOnline()
    {
        Reach = CloudReach.Online;
        CloudOkAt = DateTime.Now;
        CloudWhy = "";
    }

    private static void NoteOffline(string why)
    {
        Reach = CloudReach.Offline;
        CloudWhy = why;
    }

    /// <summary>
    /// کدهایی که درگاهِ سرورِ خانگی به جای سرورِ حساب می‌دهد: سرورِ حساب
    /// خاموش است یا درگاه به آن نمی‌رسد. جوابِ خودِ درگاه است، نه سرورِ حساب.
    /// </summary>
    public static bool IsDownCode(string code) =>
        code is "account_server_down" or "account_server_unreachable";

    /// <summary>فقط برای سنجه‌ها — برنامه هیچ‌وقت حال را دستی نمی‌سازد.</summary>
    public static void ResetReach()
    {
        Reach = CloudReach.Unknown;
        CloudOkAt = null;
        CloudWhy = "";
        //  ترمزِ «پس از شکستِ ثبت» هم استاتیک است: آزمونی که ثبتِ ناموفق
        //  می‌سازد نباید ده دقیقه ثبتِ آزمونِ بعدی را ببندد.
        _lastBindFailAt = DateTime.MinValue;
        LastBindWhy = "";
        _codeCheckedAt = DateTime.MinValue;
    }

    /// <summary>
    /// حالِ سرورِ حساب: بالا است؟ چه نسخه‌ای؟ (بی توکن، پیش از ورود هم.)
    /// </summary>
    public static async Task<(bool Up, string Version)> CloudHealthAsync(CancellationToken ct = default)
    {
        var r = await SendFull(Build(HttpMethod.Get, "/api/health", null, null), ct);
        return r.Ok ? (true, Str(r.Json, "version")) : (false, "");
    }

    /// <summary>
    /// باتِ تلگرامِ پمپ روی سرورِ حساب: هست؟ نشانی‌اش چیست؟ (بی توکن.)
    /// </summary>
    /// <remarks>
    /// ⛔ فقط نشانیِ عمومیِ بات (<c>https://t.me/…</c>) — رمزِ بات فقط روی
    /// سرور است و هیچ‌وقت به این برنامه نمی‌رسد؛ اگر داخلِ برنامه بود، هر
    /// کسی از فایلِ برنامه بیرونش می‌کشید و از طرفِ پمپ پیام می‌داد.
    /// ⚠️ هیچ‌وقت استثنا بیرون نمی‌دهد و هر چیزِ ناجور «نیست» است.
    /// </remarks>
    public static async Task<string> TelegramBotUrlAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await SendFull(Build(HttpMethod.Get, "/api/pump/public/telegram", null, null), ct);
            if (!r.Ok || r.Json.ValueKind != JsonValueKind.Object) return "";
            if (!r.Json.TryGetProperty("enabled", out var on) || on.ValueKind != JsonValueKind.True) return "";
            var url = Str(r.Json, "url");
            return url.StartsWith("https://t.me/", StringComparison.Ordinal) ? url : "";
        }
        catch (OperationCanceledException) { throw; }
        catch { return ""; }
    }

    /// <summary>
    /// ۴۰۴ی که از یک مسیرِ حساب آمد ⇒ همان جمله‌ای که کاربر با آن می‌داند
    /// چه کار کند.
    /// </summary>
    private static async Task<CloudResult> NoRouteAsync(CancellationToken ct)
    {
        var (up, ver) = await CloudHealthAsync(ct);
        if (!up)
            return CloudResult.No(
                "به سرورِ حساب نرسیدیم — سرور بالا نیست یا درخواست به آن نمی‌رسد.",
                "no_server");

        var v = ver.Length > 0 ? $" (نسخهٔ {ver})" : "";
        return CloudResult.No(
            $"سرورِ حساب{v} این مسیر را ندارد — سرورِ حساب را به‌روز کنید.",
            "no_route");
    }

    // ── رمزِ فراموش‌شده ─────────────────────────────────────────────────
    //
    //  ⚠️ این راه **ساخته نشد، پیدا شد**: سرور از قبل هر دو مسیر را دارد
    //  (`shop/server/src/routes/auth.js` و `test/password-reset.test.js`) و
    //  فقط برنامهٔ نیتیو هیچ‌وقت صدایشان نزده بود. پس کسی که رمزش را گم
    //  می‌کرد هیچ راهی جز ساختنِ حسابِ تازه نداشت.
    //
    //      POST /api/auth/password/forgot  { email }         ⇒ کد به ایمیل
    //      POST /api/auth/password/reset   { email, code, password }
    //                                      ⇒ رمزِ تازه + نشست، یک‌جا
    //
    //  ⚠️ **جوابِ «این ایمیل هست» و «نیست» عمداً یکی است** (خودِ سرور
    //  این‌طور نوشته). پس این‌جا هم نباید چیزی به آن اضافه کرد: پیامی مثلِ
    //  «چنین حسابی نیست» فهرستِ ایمیل‌های مشتری‌ها را لو می‌دهد.

    /// <summary>پلهٔ یک — کدِ یک‌بارمصرف به همان ایمیل می‌رود.</summary>
    public async Task<CloudResult> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var clean = (email ?? "").Trim();
        if (clean.Length == 0) return CloudResult.No("ایمیل را بنویسید");
        if (TooSoon("password/forgot", clean) is { } wait) return CloudResult.No(wait, "too_soon");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/password/forgot",
            new { email = clean, app = "pump" }, null), ct);
        if (res.Ok) { MailSent("password/forgot", clean); return CloudResult.Done; }
        if (res.Status == 404) return await NoRouteAsync(ct);
        return CloudResult.No(res.Why, res.Code);
    }

    /// <summary>
    /// پلهٔ دو — کدِ ایمیل و رمزِ تازه. سرور خودش همان‌جا وارد هم می‌کند، پس
    /// کاربر رمزِ تازه‌اش را دوباره تایپ نمی‌کند.
    ///
    /// ⚠️ سرور پس از عوض شدنِ رمز **همهٔ نشست‌های باز را می‌بندد**
    /// (<c>revokeAllForSubject</c>) — یعنی اگر رمز را گم کرده بودید چون
    /// گوشی‌تان دستِ دیگری افتاده، آن نشست هم همان لحظه می‌میرد.
    /// </summary>
    public async Task<CloudResult> ResetPasswordAsync(string email, string emailCode, string newPassword,
                                                      CancellationToken ct = default)
    {
        var clean = new string((emailCode ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کدِ ایمیل باید شش رقم باشد");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/password/reset", new
        {
            email = (email ?? "").Trim(),
            code = clean,
            password = newPassword,
            device = new { uid = DeviceUid, name = Environment.MachineName, platform = "windows" },
            app = "pump",
        }, null), ct);
        if (!res.Ok)
        {
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }

        return await SeatAsync(res.Json);
    }

    // ── نشستِ تازه، و ۴۰۱ ────────────────────────────────────────────────
    //
    //  ⛔ **باگی که برنامه را یک‌ساعته خاموش می‌کرد.** سرور به توکنِ دسترسی
    //  دقیقاً **یک ساعت** عمر می‌دهد (`ACCESS_TOKEN_TTL_MIN = 60`) و
    //  `refreshToken` را برای نود روز می‌دهد. برنامه `refreshToken` را
    //  ذخیره می‌کرد ولی **هیچ‌جا نمی‌خواندش** و هیچ ۴۰۱ی را هم نمی‌فهمید.
    //  یعنی یک ساعت پس از ورود، `/api/pump/me` و کدِ اپِ کارمندان و هر کارِ
    //  حسابیِ دیگر بی‌صدا شکست می‌خوردند و **دیگر هیچ‌وقت درست نمی‌شدند** —
    //  در حالی که صفحهٔ پروفایل همچنان «وارد شده‌اید» می‌گفت.
    //
    //  همان کاری که `kar/cloud.js` از اول می‌کرد (`authed()`): یک بار تازه
    //  کن، یک بار دوباره بزن. ⚠️ **فقط یک بار** — حلقه زدن جز پنهان کردنِ
    //  مشکل کاری نمی‌کند.

    /// <summary>
    /// چند دقیقه پیش از انقضا خودمان تازه می‌کنیم — یک درخواستِ حتماً-شکست
    /// کمتر، و کاربری که وسطِ کار پیامِ خطا نمی‌بیند.
    /// </summary>
    private const long RefreshSkewMs = 60_000;

    /// <summary>
    /// ══ یک تازه‌سازی در کلِ پروسه، نه در هر نمونه ═══════════════════════════
    ///
    /// ⛔ <b>باگِ «گاهی بی‌دلیل از حساب بیرون می‌افتم».</b> این قفل تا
    /// ۱۴۰۵/۰۷/۱۲ مالِ <b>هر نمونه</b> بود — در حالی که حلقهٔ پس‌زمینه، صفحهٔ
    /// پروفایل، همگام‌سازی و گزارشِ خطا هر کدام <see cref="CloudLink"/>ِ خودشان
    /// را با <b>عکسِ جداگانه‌ای</b> از تنظیمات می‌سازند، و سرور توکنِ تازه‌سازی
    /// را <b>می‌چرخاند</b> (توکنِ کهنه پس از سی ثانیه رد می‌شود). پس:
    ///
    ///   ۱) نمونهٔ الف تازه می‌کرد ⇒ توکنِ نو روی دیسک؛
    ///   ۲) نمونهٔ ب با توکنِ <b>کهنهٔ</b> عکسِ خودش تازه می‌کرد ⇒ ۴۰۱؛
    ///   ۳) و ب <see cref="ClearSessionAsync"/> را می‌زد ⇒ توکن‌های <b>سالمِ</b>
    ///      الف هم از دیسک پاک می‌شدند.
    ///
    /// حالا قفل ایستا است، و داخلِ قفل تنظیماتِ <b>روی دیسک</b> دوباره خوانده
    /// می‌شود: اگر کسِ دیگری در همین فاصله چرخانده، همان را برمی‌داریم و
    /// اصلاً تازه نمی‌کنیم. و نشست فقط وقتی پاک می‌شود که توکنِ ردشده
    /// <b>هنوز همانی باشد که روی دیسک است</b>.
    /// </summary>
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    /// <summary>توکنِ دسترسی نزدیکِ انقضاست؟</summary>
    private bool AccessNearlyExpired =>
        _settings.CloudAccessExpiresAt > 0
        && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + RefreshSkewMs >= _settings.CloudAccessExpiresAt;

    /// <summary>
    /// نشستِ روی دیسک — ⚠️ فقط وقتی چیزی دارد و با عکسِ همین نمونه فرق دارد.
    /// دیسکِ <b>خالی</b> هیچ‌وقت جای عکسِ پرِ این نمونه نمی‌نشیند: نمونه‌ای
    /// که عمداً روی دیسک نمی‌نویسد (سنجه‌ها) نباید با آن خالی شود.
    /// </summary>
    private bool AdoptDiskSession()
    {
        AppSettings disk;
        try { disk = AppSettings.Load(); }
        catch { return false; }

        var r = (disk.CloudRefreshToken ?? "").Trim();
        if (r.Length == 0 || r == (_settings.CloudRefreshToken ?? "").Trim()) return false;

        _settings.CloudRefreshToken = disk.CloudRefreshToken;
        _settings.CloudAccountToken = disk.CloudAccountToken;
        _settings.CloudAccessExpiresAt = disk.CloudAccessExpiresAt;
        return true;
    }

    /// <summary>
    /// نشستِ تازه از روی <c>refreshToken</c>.
    ///
    /// سه پایان دارد و هر سه مهم‌اند:
    ///  • شد ⇒ توکنِ تازه می‌نشیند.
    ///  • سرور گفت این توکن باطل است (۴۰۱/۴۰۳) ⇒ نشست <b>پاک</b> می‌شود، تا
    ///    صفحهٔ پروفایل دیگر دروغ نگوید و کاربر دوباره وارد شود — ⛔ ولی
    ///    فقط اگر همان توکنِ ردشده هنوز روی دیسک است (بالا نوشته چرا).
    ///  • ⚠️ <b>نرسیدیم</b> (بی‌اینترنت، سرورِ خاموش، تایم‌اوت) ⇒ هیچ چیزی
    ///    پاک نمی‌شود. وگرنه یک قطعیِ اینترنت کاربر را از حسابش بیرون
    ///    می‌انداخت — و این برنامه اساساً آفلاین است.
    /// </summary>
    private async Task<bool> RefreshSessionAsync(CancellationToken ct)
    {
        //  توکنی که این نمونه **پیش از** انتظار داشت — اگر تا نوبتش برسد
        //  کسِ دیگری چرخانده باشد، همین فرق نشان می‌دهد.
        var before = (_settings.CloudRefreshToken ?? "").Trim();

        await RefreshGate.WaitAsync(ct);
        try
        {
            //  ⛔ کسِ دیگری در همین فاصله تازه کرد ⇒ همان را برمی‌داریم.
            if (AdoptDiskSession())
            {
                if (!string.IsNullOrWhiteSpace(_settings.CloudAccountToken) && !AccessNearlyExpired
                    && _settings.CloudRefreshToken.Trim() != before)
                    return true;
            }

            var mine = (_settings.CloudRefreshToken ?? "").Trim();
            if (mine.Length == 0)
            {
                await ClearSessionAsync();
                return false;
            }

            var res = await SendFull(Build(HttpMethod.Post, "/api/auth/refresh",
                new { refreshToken = mine, device = new { deviceId = DeviceUid } },
                null), ct);

            if (res.Ok)
            {
                var token = Str(res.Json, "accessToken");
                if (token.Length == 0) return false;
                _settings.CloudAccountToken = token;
                //  ⚠️ سرور توکنِ تازه‌سازی را **می‌چرخاند**؛ اگر داد، همان
                //  می‌نشیند و کهنه دیگر به کار نمی‌آید.
                var again = Str(res.Json, "refreshToken");
                if (again.Length > 0) _settings.CloudRefreshToken = again;
                _settings.CloudAccessExpiresAt = Num(res.Json, "accessExpiresAt");
                await SaveQuiet();
                return true;
            }

            if (res.Status is 401 or 403)
            {
                //  ⛔ شاید توکنِ ما همین حالا به دستِ پروسهٔ دیگری چرخانده
                //  شده: آن‌وقت نشستِ سالمِ روی دیسک را برمی‌داریم، نه این‌که
                //  پاکش کنیم.
                if (AdoptDiskSession()) return !string.IsNullOrWhiteSpace(_settings.CloudAccountToken);
                await ClearSessionAsync();
            }
            return false;
        }
        catch (OperationCanceledException) { return false; }
        finally { RefreshGate.Release(); }
    }

    /// <summary>
    /// نشست را پاک می‌کند — ولی <b>نام و ایمیل می‌مانند</b>، تا صفحهٔ ورود
    /// کادرها را پر کند و کاربر فقط رمز بزند.
    /// ⛔ و دفترِ روی کامپیوتر و اشتراکِ دستگاه دست نمی‌خورند.
    /// </summary>
    private async Task ClearSessionAsync()
    {
        _settings.CloudAccountToken = "";
        _settings.CloudRefreshToken = "";
        _settings.CloudAccessExpiresAt = 0;
        AccountHasStation = null;
        await SaveQuiet();
    }

    /// <summary>
    /// درخواستی با توکنِ حساب — با یک تازه‌سازی و یک تلاشِ دوباره.
    ///
    /// ⚠️ هر تلاش <b>پیامِ تازه‌ای</b> می‌سازد: یک
    /// <see cref="HttpRequestMessage"/> فقط یک بار فرستادنی است.
    /// </summary>
    private async Task<CloudReply> AccountAsync(HttpMethod method, string path, object? body,
                                                CancellationToken ct)
    {
        if (!SignedIn) return CloudReply.Fail("اول وارد حساب شوید", "signed_out");

        //  زودتر از انقضا، بی این‌که منتظرِ یک ۴۰۱ِ حتمی بمانیم
        if (AccessNearlyExpired) await RefreshSessionAsync(ct);
        if (!SignedIn) return CloudReply.Fail("نشست منقضی شده — دوباره وارد شوید", "signed_out");

        var first = await SendFull(Build(method, path, body, _settings.CloudAccountToken), ct);
        if (first.Status != 401) return first;

        if (!await RefreshSessionAsync(ct)) return first;
        return await SendFull(Build(method, path, body, _settings.CloudAccountToken), ct);
    }

    /// <summary>
    /// خروج از حساب — دفترِ روی کامپیوتر دست نمی‌خورد.
    ///
    /// ⛔ **پیش از این فقط محلی بود.** توکنِ دسترسی و توکنِ تازه‌سازی روی
    /// سرور زنده می‌ماندند (تازه‌سازی تا نود روز)، پس هر کسی که یک بار
    /// آن رشته را برداشته بود، «خروج»ِ کاربر جلویش را نمی‌گرفت. حالا اول
    /// از سرور باطل می‌شوند (<c>POST /api/auth/logout</c>).
    ///
    /// ⚠️ ولی نشدنِ آن هیچ‌وقت جلوی خروجِ محلی را نمی‌گیرد: کسی که
    /// اینترنت ندارد هم باید بتواند از حسابش بیرون بیاید.
    /// </summary>
    public async Task SignOutAsync(CancellationToken ct = default)
    {
        //  ⛔ پیش از باطل کردن، نشستِ **روی دیسک** برداشته می‌شود: عکسِ این
        //  نمونه ممکن است توکنی داشته باشد که پروسهٔ دیگری همین حالا چرخانده
        //  — آن‌وقت «خروج» توکنِ مرده را باطل می‌کرد و توکنِ زنده روی سرور
        //  نود روز می‌ماند. زیرِ همان قفلِ تازه‌سازی، تا وسطِ یک چرخش نیفتد.
        await RefreshGate.WaitAsync(ct);
        try
        {
            AdoptDiskSession();
            if (SignedIn)
            {
                try
                {
                    await SendFull(Build(HttpMethod.Post, "/api/auth/logout",
                        new { refreshToken = _settings.CloudRefreshToken }, _settings.CloudAccountToken), ct);
                }
                catch { /* خروجِ محلی گروگانِ سرور نیست */ }
            }
        }
        finally { RefreshGate.Release(); }

        _settings.CloudAccountToken = "";
        _settings.CloudRefreshToken = "";
        _settings.CloudAccessExpiresAt = 0;
        AccountHasStation = null;
        _settings.CloudEmail = "";
        _settings.CloudName = "";
        await SaveQuiet();
    }

    /// <summary>
    /// ══ «این دستگاه دیگر مالِ این پمپ نیست» ═════════════════════════════
    ///
    /// ⛔ **نشتی که این می‌بندد**: خروج از حساب تا امروز فقط چهار فیلدِ
    /// حساب را پاک می‌کرد. توکنِ **دستگاه**، شناسهٔ پمپ، مجوز، کلیدِ عمومی،
    /// کدِ اپِ کارمندان، نشانی و رمزِ سرورِ خانگی و مُهرِ ارفاق همه سرِ جا
    /// می‌ماندند. یعنی اگر همین کامپیوتر به حسابِ پمپِ دیگری می‌رفت:
    ///   • عکسِ حساب‌ها به پوشهٔ ابریِ **پمپِ قبلی** می‌رفت (توکنِ دستگاهِ او)،
    ///   • کدِ اپِ کارمندان و اشتراکِ پمپِ قبلی روی صفحهٔ پروفایل دیده می‌شد،
    ///   • و ارفاقِ اشتراکِ او برای این یکی خرج می‌شد.
    ///
    /// ⚠️ **دفترِ روی کامپیوتر دست نمی‌خورد.** این تابع یک بیت از دیتابیس
    /// را هم لمس نمی‌کند — دفترِ صاحبِ پمپ مالِ خودش است و گروگان گرفتنش
    /// (یا پاک کردنش) هیچ‌وقت کارِ ما نیست. فقط بندهای «این نصب به کدام پمپ
    /// وصل است» باز می‌شوند.
    ///
    /// ⚠️ کلیدِ عمومی هم پاک می‌شود، و این عمدی است: قفلِ TOFU برای «همین
    /// دستگاه، همین سرور» است و با عوض شدنِ پمپ باید از نو قفل شود. نشانیِ
    /// ابر همچنان در کد قفل است، پس این هیچ دری را باز نمی‌کند.
    /// </summary>
    public async Task ForgetStationAsync()
    {
        AccountHasStation = null;
        _settings.CloudDeviceToken = "";
        _settings.CloudStationId = "";
        _settings.CloudStationCode = "";
        _settings.CloudLicense = "";
        _settings.CloudPublicKey = "";
        _settings.CloudAccessCode = "";
        //  ⛔ حسابِ تازه یعنی گامِ پمپ از نو — وگرنه پمپِ حسابِ قبلی
        //  «تمام‌شده» حساب می‌شد و کاربر هیچ‌وقت پمپِ خودش را نمی‌ساخت.
        _settings.PumpStepDone = false;
        _settings.CloudSyncedAt = 0;
        _settings.EntitledUntil = 0;
        _settings.EntitledPlan = "";
        //  نشانی و رمزهای سرورِ خانگی هم مالِ همان پمپ بودند
        _settings.ServerUrl = "";
        _settings.ServerLanUrl = "";
        _settings.ServerToken = "";
        _settings.ServerReadKey = "";
        _settings.ServerId = "";
        Subscription = PumpSubscription.None;
        await SaveQuiet();
    }

    /// <summary>
    /// نشانیِ سرورِ خانگی و رمزِ خواندن را از حساب می‌گیرد.
    ///
    /// ⚠️ همین است که کادرِ «نشانیِ سرور» و «رمزِ سرور» را از تنظیمات
    /// برداشت: کاربر هیچ‌کدام را تایپ نمی‌کند، از حسابش می‌آید.
    /// </summary>
    public async Task<(bool Ok, string Url, string ReadKey, string Station, string Why)>
        HomeFromAccountAsync(CancellationToken ct = default, bool forceBind = false)
    {
        if (!SignedIn) return (false, "", "", "", "اول وارد حساب شوید");

        //  ⚠️ از راهِ `AccountAsync` می‌رود، نه `GetAsync`ِ خام: توکنِ دسترسی
        //  یک ساعت عمر دارد و این همان جایی بود که بی‌صدا می‌مرد.
        var res = await AccountAsync(HttpMethod.Get, "/api/pump/me", null, ct);
        if (!res.Ok) return (false, "", "", "", res.Why);
        var json = res.Json;

        //  ⛔ **اشتراک به حساب بسته است، نه به یک کد.**
        //
        //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۳۰): «من یادم نمیاد که برای
        //  اشتراک کدی گفته باشم — اون رو من به حسابِ یارو از سرور می‌دم
        //  اینترنتی و تو برنامه تو حسابِ همون ثبت می‌شن.»
        //
        //  سرور همین حالا هم `entitlement`ِ همان پمپ را در همین پاسخ
        //  می‌فرستد (`routes/pump.js`) — ولی تا امروز برنامه فقط `home` را
        //  برمی‌داشت و **بقیه‌اش را دور می‌ریخت**. یعنی صاحبِ پمپی که
        //  اشتراکش را روی سرور گرفته بود، در برنامه «بی‌اشتراک» می‌ماند تا
        //  وقتی کدی بزند. حالا همین‌جا نشانده می‌شود.
        //
        //  ⚠️ فقط **پر** می‌کند: پاسخی که اشتراک ندارد چیزی را خاموش
        //  نمی‌کند، چون ارفاق و مجوزِ امضاشده راهِ خودشان را دارند.
        ReadSubscription(json);
        if (Subscription.Active) Entitlements.Remember(_settings, Subscription, Verify());

        //  ⛔ **پمپِ این حساب با پمپی که این دستگاه رویش قفل شده یکی نیست.**
        //  بی این سنجش، ورود با حسابِ پمپِ دیگر نشانی و رمزِ **آن** پمپ را
        //  روی تنظیماتِ این یکی می‌نشاند: از آن لحظه دفترِ این پمپ به سرورِ
        //  خانگیِ پمپِ دیگری می‌رفت. راهِ درستِ جابه‌جایی
        //  `ForgetStationAsync` است.
        //
        //  ⚠️ **سنجش روی شناسهٔ ابری است، نه کدِ پمپ.** یک بار با کدِ پمپ
        //  نوشتمش و سنجهٔ `cloudlogin` گرفتش: کدِ ابر لازم نیست با
        //  `AppSettings.StationCode`ی محلی یکی باشد (ابر می‌تواند
        //  هنجارش کند یا خودش بسازدش)، پس آن مقایسه نصبِ سالم را هم رد
        //  می‌کرد. شناسه همان چیزی است که مجوز (`stn`) هم رویش قفل است.
        var acctStation = StationId(json);
        AccountHasStation = acctStation.Length > 0;
        if (acctStation.Length == 0) LastBindWhy = "";
        var locked = (_settings.CloudStationId ?? "").Trim();
        var bindDue = forceBind || DateTime.UtcNow - _lastBindFailAt >= BindRetryAfterFail;

        /*
         *  ⛔ **این کامپیوتر روی پمپی است که مالِ این حساب نیست** — یا حساب
         *  اصلاً پمپی ندارد. (۱۴۰۵/۰۷/۱۴، بازسازی‌شده با پشتهٔ واقعی و خودِ
         *  برنامه: نصبی که روزی با کدِ شش‌رقمی فعال شده بود روی پمپِ بی‌صاحبِ
         *  آن کد ماند؛ صاحبش وارد حسابش شد و مدیر به پمپِ حساب VIP داد — و
         *  برنامه برای همیشه روی پمپِ کد ماند، بی هیچ پیامی. «نه آزمایشی،
         *  نه اشتراکی که دادم».)
         *
         *  تا دیروز همین‌جا فقط «مالِ پمپِ دیگری است» برمی‌گشت و هیچ‌جا
         *  دیده نمی‌شد. حالا **سرور** تصمیم می‌گیرد (`BindAsync(adopt)`):
         *  پمپِ بی‌صاحب مالِ حساب می‌شود یا دستگاه با روزهای اشتراکش به پمپِ
         *  حساب می‌رود؛ پمپی که صاحبِ دیگری دارد دست نمی‌خورد و دلیلش
         *  همان‌جا (`LastBindWhy`) گفته می‌شود.
         */
        //  ⛔ نصبی که **هنوز فعال نشده** ولی شناسهٔ پمپِ دیگری رویش مانده، مثلِ
        //  همیشه دست نمی‌خورد: توکنی ندارد که مدرکِ آن پمپ باشد، پس سرور
        //  چیزی برای سنجیدن ندارد و بستنش به پمپِ حساب یعنی نشاندنِ نشانی و
        //  رمزِ پمپِ دیگر روی این دفتر.
        if (!Activated && locked.Length > 0 && acctStation.Length > 0
            && !string.Equals(acctStation, locked, StringComparison.Ordinal))
            return (false, "", "", "", "این حساب مالِ پمپِ دیگری است. برای جابه‌جایی، "
                + "این دستگاه را از پمپِ فعلی جدا کنید.");

        var elsewhere = Activated && locked.Length > 0
            && !string.Equals(acctStation, locked, StringComparison.Ordinal);
        if (elsewhere)
        {
            if (!bindDue)
                return (false, "", "", "", LastBindWhy.Length > 0 ? LastBindWhy
                    : "این کامپیوتر روی پمپِ دیگری است");
            CloudResult moved;
            try { moved = await BindAsync(ct, adopt: true); }
            catch (Exception ex) { moved = CloudResult.No(ex.GetType().Name); }
            LastBindWhy = moved.Ok ? ""
                : (moved.Why ?? "").Contains("پمپِ دیگری") ? moved.Why!
                : "این کامپیوتر روی پمپِ دیگری است — " + (moved.Why ?? "");
            _lastBindFailAt = moved.Ok ? DateTime.MinValue : DateTime.UtcNow;
            if (!moved.Ok) return (false, "", "", "", LastBindWhy);
            //  حالِ تازهٔ حساب — پمپ حالا همان پمپِ این کامپیوتر است
            res = await AccountAsync(HttpMethod.Get, "/api/pump/me", null, ct);
            if (!res.Ok) return (false, "", "", "", res.Why);
            json = res.Json;
            ReadSubscription(json);
            acctStation = StationId(json);
            AccountHasStation = acctStation.Length > 0;
        }

        //  ⛔ کدِ پمپِ همین حساب همین‌جا می‌نشیند — نصبی که از قبل بند شده
        //  هیچ‌وقت دوباره ‎bind‎ نمی‌زند، پس بی این خط کدِ حسابش را هیچ‌وقت
        //  نمی‌گرفت و روی ‎pump1‎ی کهنه می‌ماند.
        if (StationCodeOf(json) is { Length: > 0 } acctCode
            && !string.Equals(acctCode, _settings.CloudStationCode, StringComparison.Ordinal))
        {
            _settings.CloudStationCode = acctCode;
            await SaveQuiet();
        }

        /*
         *  ⛔ نصبی که فقط وارد حساب شده، خودش بند می‌شود — بی هیچ کدی.
         *
         *  تا دیروز تنها راهِ گرفتنِ توکنِ دستگاه و مجوز، کدِ شش‌رقمی بود.
         *  یعنی صاحبِ پمپی که اشتراکش را مدیر روی **حسابش** گذاشته بود،
         *  باز هم بی کد نمی‌توانست برنامه را راه بیندازد — و بی مجوز،
         *  فهرستِ قابلیت‌های پلن هم به برنامه نمی‌رسید.
         *
         *  ⚠️ **پس از** سنجشِ «این حساب مالِ پمپِ دیگری است» می‌آید، وگرنه
         *  ورود با حسابِ پمپِ دیگر همین دستگاه را به آن پمپ می‌بست.
         *  ⚠️ و نشدنش این مسیر را نمی‌شکند: نشانیِ خانگی همان است که بود.
         */
        //  ⛔ **پس از یک شکست، حلقه هر دقیقه دوباره نمی‌زند** (۱۴۰۵/۰۷/۱۳،
        //  سنجهٔ `linkstates` روی سرورِ واقعی): `device/bind` سقفِ ده بار در
        //  ربع ساعت برای هر آی‌پی دارد. کامپیوتری که از پمپ جدا شده
        //  (`device_revoked`) هر دقیقه همان ۴۰۳ را می‌گرفت و در ده دقیقه سقف
        //  را پر می‌کرد — از آن به بعد حتی کلیکِ خودِ کاربر پس از برگرداندنش
        //  «تلاشِ زیاد» می‌دید، و کامپیوترِ دیگرِ همان پمپ پشتِ همان اینترنت
        //  هم. پس پس از شکست فقط هر ده دقیقه، و کلیکِ کاربر (`forceBind`)
        //  همیشه همین حالا.
        if (!Activated && acctStation.Length > 0 && bindDue)
        {
            //  ⛔ **نتیجه‌اش دیگر بلعیده نمی‌شود.** تا دیروز این خط هم
            //  استثنا را می‌خورد و هم مقدارِ بازگشتی را دور می‌ریخت، پس
            //  یک `key_mismatch`ِ دائمی هیچ‌جا دیده نمی‌شد و کاربر فقط
            //  «فعال نشده» را می‌دید بی این‌که بداند چرا.
            //  ⚠️ همچنان چیزی را نمی‌شکند: نشانیِ خانگی مهم‌تر است و
            //  مسیر ادامه می‌یابد؛ فقط دلیل ثبت می‌شود.
            try
            {
                var bind = await BindAsync(ct);
                LastBindWhy = bind.Ok ? "" : (bind.Why ?? "");
                _lastBindFailAt = bind.Ok ? DateTime.MinValue : DateTime.UtcNow;
            }
            catch (Exception ex) { LastBindWhy = ex.GetType().Name; _lastBindFailAt = DateTime.UtcNow; }
        }

        /*
         *  ⚠️ سنجشِ «پمپی هست؟» **پس از** بند شدن آمد، نه پیش از آن.
         *
         *  پمپی که هنوز سرورِ خانگی‌اش را معرفی نکرده `home` ندارد، و با
         *  ترتیبِ قبلی همان‌جا برمی‌گشتیم — پس نصبِ تازه هیچ‌وقت بند
         *  نمی‌شد و مجوزش را نمی‌گرفت. نشانیِ خانگی رفاه است، اشتراک اصل.
         */
        if (!json.TryGetProperty("home", out var home) || home.ValueKind != JsonValueKind.Object)
            return (false, "", "", "", "هنوز سرورِ خانگیِ این پمپ معرفی نشده است");

        return (true, Str(home, "url"), Str(home, "readKey"), Str(home, "station"), "");
    }

    // ── گفت‌وگو با ابر ─────────────────────────────────────────────────

    /// <summary>
    /// ══ هویتِ نسخه روی هر درخواست ═══════════════════════════════════════
    ///
    /// تا پیش از این هیچ درخواستی نمی‌گفت از کدام نسخهٔ برنامه آمده — نه
    /// ‎User-Agent‎ی، نه هدرِ نسخه‌ای. نتیجه‌اش دو چیز بود: سرور نمی‌توانست
    /// نسخهٔ قدیم را از تازه جدا کند (پس هر تغییرِ سرور خطرِ شکستنِ نصب‌های
    /// قدیم داشت)، و در لاگِ خطا معلوم نبود کدام نسخه خطا داده.
    ///
    /// ⚠️ **هیچ چیزِ شناسایی‌کنندهٔ کاربر این‌جا نمی‌رود** — نه ایمیل، نه نامِ
    /// ماشین، نه شناسهٔ دستگاه. فقط شمارهٔ نسخه و نامِ برنامه. خودِ توکن
    /// می‌گوید این کدام دستگاه است.
    ///
    /// ⚠️ جایش عمداً داخلِ <see cref="Build"/> است، نه در فرستنده: هر
    /// درخواست — و هر **تلاشِ دوم** پس از تازه‌سازیِ توکن — از همان یک جا
    /// ساخته می‌شود، پس هیچ مسیری بی هدر نمی‌ماند.
    /// </summary>
    public static void Stamp(HttpRequestMessage req)
    {
        try
        {
            var v = CloudConfig.ApplicationVersion;
            req.Headers.TryAddWithoutValidation("User-Agent", "PumpYaqobi/" + v);
            req.Headers.TryAddWithoutValidation("X-App-Version", v);
            req.Headers.TryAddWithoutValidation("X-App-Platform", "windows-native");
            //  ⚠️ سرورِ مرکزی مالِ چند برنامه است؛ بی این، لاگِ خطا نمی‌گوید
            //  کدام برنامه زده. همان `aud`ی است که مجوز هم با آن سنجیده
            //  می‌شود — نه یک نامِ تازه، و نه چیزی که کاربر را بشناساند.
            req.Headers.TryAddWithoutValidation("X-App-Id", CloudConfig.ApplicationId);
        }
        catch { /* هدر نرفتن هیچ‌وقت نباید جلوی درخواست را بگیرد */ }
    }

    /// <summary>
    /// پاسخِ خامِ ابر — مثلِ قبل، به‌علاوهٔ <b>خودِ کدِ HTTP</b>.
    ///
    /// ⚠️ بی این، «۴۰۱» از «۴۰۰» جدا نمی‌شد و تنها راهِ تشخیصش گشتن دنبالِ
    /// رشتهٔ «404» داخلِ متنِ فارسیِ خطا بود — که هم شکننده بود و هم اجازه
    /// نمی‌داد نشستِ منقضی از رمزِ غلط جدا شود.
    /// </summary>
    private readonly record struct CloudReply(bool Ok, JsonElement Json, string Why, string Code, int Status)
    {
        public static CloudReply Fail(string why, string code, int status = 0) =>
            new(false, default, why, code, status);
    }

    /// <summary>
    /// ⚠️ یک <see cref="HttpRequestMessage"/> فقط <b>یک بار</b> فرستادنی
    /// است، پس هر تلاش باید پیامِ خودش را بسازد — همین است که تلاشِ دوم پس
    /// از تازه‌سازیِ توکن را ممکن می‌کند.
    /// </summary>
    private static HttpRequestMessage Build(HttpMethod method, string path, object? body, string? token)
    {
        var req = new HttpRequestMessage(method, CloudConfig.Url(path));
        if (body is not null) req.Content = JsonContent.Create(body);
        if (!string.IsNullOrWhiteSpace(token)) req.Headers.Add("Authorization", $"Bearer {token}");
        Stamp(req);
        return req;
    }

    private static Task<CloudReply> SendFull(HttpRequestMessage req, CancellationToken ct) =>
        SendOn(Http, req, ct);

    /// <summary>
    /// همان فرستنده، روی یک <see cref="HttpClient"/>ِ دلخواه.
    ///
    /// ⚠️ نیمهٔ همگام‌سازی فرستندهٔ خودش را دارد (gzip و مهلتِ بلندتر) ولی
    /// باید <b>دقیقاً همین</b> خواندنِ پاسخ و همین تصمیمِ «وصل‌ایم یا نه» را
    /// داشته باشد — وگرنه چراغِ سرورِ حساب دو حقیقتِ جدا پیدا می‌کرد.
    /// </summary>
    private static async Task<CloudReply> SendOn(HttpClient client, HttpRequestMessage req, CancellationToken ct)
    {
        using var _ = req;
        try
        {
            using var res = TestTransport is null
                ? await client.SendAsync(req, ct)
                : await TestTransport(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            var status = (int)res.StatusCode;
            JsonElement json = default;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                json = doc.RootElement.Clone();
            }
            catch { /* پاسخِ بی‌شکل */ }

            if (res.IsSuccessStatusCode)
            {
                NoteOnline();
                return new CloudReply(true, json, "", "", status);
            }

            var why = "";
            var code = "";
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var e))
            {
                why = Str(e, "message");
                code = Str(e, "code");
                //  ⛔ درگاهِ سرورِ خانگی وقتی خودِ سرورِ حساب روشن نیست، ۵۰۳ی
                //  با شکلِ خودمان می‌دهد (`account_server_down`) — و این یعنی
                //  **نرسیدیم**، نه «وصل‌ایم». با عکس دیده شد (۱۴۰۵/۰۷/۰۲):
                //  چراغِ دوم سبز بود در حالی که هر ورودی همین خطا را می‌گرفت.
                if (IsDownCode(code))
                    NoteOffline(why.Length > 0 ? why : "سرورِ حساب روی سرورِ خانگی روشن نیست");
                else
                    //  ⚠️ خطای **خودِ سرورِ ما** هم یعنی وصل‌ایم: «رمز غلط» یا
                    //  «این مسیر وجود ندارد» را فقط سرورِ ما به این شکل می‌گوید.
                    NoteOnline();
            }
            else
            {
                //  ⛔ پاسخی که شکلِ خطای ما را ندارد، از سرورِ ما نیامده —
                //  پروکسی، تونل، یا دامنه‌ای که به سرورِ حساب نمی‌رسد. چراغ
                //  نباید این را «وصل» بشمارد. (همان ۴۰۴ِ بی‌بدنهٔ ۱۴۰۵/۰۷/۰۱.)
                NoteOffline($"پاسخِ ناشناس از نشانیِ سرورِ حساب ({status})");
            }
            //  ⚠️ **۴۲۹ همیشه پیامِ خودمان را می‌گیرد، حتی اگر سرور متنی
            //  داده باشد.** متنِ سرور («تعداد درخواست بیش از حد مجاز است»)
            //  درست است ولی نمی‌گوید کاربر باید چه کند؛ و بی این، پیام
            //  «سرور جواب نداد (429)» می‌شد و کاربر فکر می‌کرد برنامه خراب
            //  است (با عکس دیده شد، ۱۴۰۵/۰۶/۳۰).
            if (status == 429)
                why = "تلاشِ زیاد — چند دقیقه صبر کنید و دوباره بزنید";

            if (string.IsNullOrWhiteSpace(why))
                why = status switch
                {
                    429 => "تلاشِ زیاد — چند دقیقه صبر کنید و دوباره بزنید",
                    401 => "نشست منقضی شده — دوباره وارد شوید",
                    >= 500 => "سرور همین حالا مشکل دارد — کمی بعد دوباره",
                    _ => $"سرور جواب نداد ({status})",
                };
            //  کدِ ماشینی: اگر سرور نداد، خودِ شمارهٔ HTTP. (`AuthAsync` از
            //  همین برای تشخیصِ «این راه روی سرور نیست» استفاده می‌کند.)
            if (code.Length == 0) code = status.ToString();
            return new CloudReply(false, json, why, code, status);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            //  ⚠️ `HttpClient.Timeout` هم همین را پرت می‌کند. جدا کردنش از
            //  «کاربر خودش لغو کرد» مهم است، وگرنه تایم‌اوت «لغو شد» دیده
            //  می‌شد.
            NoteOffline("سرور دیر جواب داد");
            return CloudReply.Fail("سرور دیر جواب داد — دوباره بزنید", "timeout");
        }
        catch (OperationCanceledException) { return CloudReply.Fail("لغو شد", "cancelled"); }
        catch (HttpRequestException)
        {
            NoteOffline("به سرورِ حساب نرسیدیم — اینترنت یا نشانی");
            return CloudReply.Fail("به سرور نرسیدیم — اینترنت را بررسی کنید", "offline");
        }
        //  ⚠️ پیامِ خامِ استثنا به کاربر نشان داده نمی‌شود: ممکن است نشانی،
        //  نامِ میزبان یا جزئیاتِ TLS داشته باشد.
        catch
        {
            NoteOffline("ارتباط با ابر برقرار نشد");
            return CloudReply.Fail("ارتباط با سرور برقرار نشد", "error");
        }
    }

    private static async Task<(bool, JsonElement, string, string)> Send(
        HttpRequestMessage req, CancellationToken ct)
    {
        var r = await SendFull(req, ct);
        return (r.Ok, r.Json, r.Why, r.Code);
    }

    private static Task<(bool, JsonElement, string, string)> PostAsync(
        string path, object body, string? token, CancellationToken ct) =>
        Send(Build(HttpMethod.Post, path, body, token), ct);

    private static Task<(bool, JsonElement, string, string)> PutAsync(
        string path, object body, string? token, CancellationToken ct) =>
        Send(Build(HttpMethod.Put, path, body, token), ct);

    private static Task<(bool, JsonElement, string, string)> GetAsync(
        string path, string? token, CancellationToken ct) =>
        Send(Build(HttpMethod.Get, path, null, token), ct);

    // ── درخواست با توکنِ دستگاه — و «این دستگاه از پمپ جدا شده» ──────────
    //
    //  ⛔ تا امروز اگر مدیر یا صاحبِ پمپ این کامپیوتر را از «دستگاه‌ها» جدا
    //  می‌کرد، هر درخواستِ دستگاه ۴۰۱ می‌گرفت و برنامه **برای همیشه** همان
    //  توکنِ مرده و مجوزِ کهنه را نگه می‌داشت: پروفایل «فعال» می‌گفت،
    //  ارفاق جلو می‌رفت و هیچ‌کس نمی‌فهمید چرا هیچ چیزی به سرور نمی‌رسد.
    //
    //  ⚠️ **محافظه‌کار است و باید بماند**: فقط با همین دو کدِ صریحِ سرور
    //  (`device_not_registered` · `device_revoked`)، و فقط توکنِ دستگاه و
    //  مجوز پاک می‌شوند. ⛔ خطای شبکه، تایم‌اوت، ۵۰۰ و هر ۴۰۱ِ بی‌کد هیچ
    //  چیزی را پاک نمی‌کنند — بی‌اینترنت نباید کسی را از پمپش جدا کند. و
    //  ⛔ یک بیت از دفتر، شناسهٔ پمپ و کلیدِ قفل‌شده دست نمی‌خورد: ورودِ
    //  دوباره یا کدِ تازه همان‌جا را از نو بند می‌کند.

    private static readonly string[] DetachedCodes = { "device_not_registered", "device_revoked" };

    /// <summary>
    /// چرا این دستگاه دیگر به پمپ بند نیست — خالی یعنی مشکلی نیست. ایستا،
    /// همان الگوی <see cref="LastBindWhy"/>: هر درخواست نمونهٔ تازه می‌سازد.
    /// </summary>
    public static string DeviceDetachedWhy { get; private set; } = "";

    private async Task<(bool, JsonElement, string, string)> DevSendAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var used = _settings.CloudDeviceToken;
        var r = await SendFull(Build(method, path, body, used), ct);
        if (!r.Ok && (r.Status is 401 or 403) && Array.IndexOf(DetachedCodes, r.Code) >= 0 && Activated)
            await DetachDeviceAsync(used);
        return (r.Ok, r.Json, r.Why, r.Code);
    }

    private Task<(bool, JsonElement, string, string)> DevPostAsync(string path, object body, CancellationToken ct) =>
        DevSendAsync(HttpMethod.Post, path, body, ct);

    private Task<(bool, JsonElement, string, string)> DevPutAsync(string path, object body, CancellationToken ct) =>
        DevSendAsync(HttpMethod.Put, path, body, ct);

    private Task<(bool, JsonElement, string, string)> DevGetAsync(string path, CancellationToken ct) =>
        DevSendAsync(HttpMethod.Get, path, null, ct);

    /// <summary>فقط توکنِ دستگاه و مجوز — شرحش بالای <see cref="DetachedCodes"/>.</summary>
    private async Task DetachDeviceAsync(string failed)
    {
        //  ⛔ **توکنِ کهنه، توکنِ تازه را پاک نکند** (۱۴۰۵/۰۷/۱۴، سنجهٔ `oldacct`
        //  روی پشتهٔ واقعی): بند شدنِ دوباره (`BindAsync`) توکنِ تازه می‌گیرد و
        //  سرور همان لحظه توکنِ قبلی را باطل می‌کند. بخشِ دیگری از برنامه که هنوز
        //  توکنِ قبلی را در دست داشت «ثبت نشده» می‌گرفت و **توکنِ تازه را روی
        //  دیسک خالی می‌کرد** — یعنی کامپیوتری که همین حالا وصل شده بود دوباره
        //  «فعال نشده» می‌شد. حالا اگر دیسک توکنِ دیگری دارد، همان برداشته می‌شود.
        try
        {
            var disk = AppSettings.Load();
            if (!string.IsNullOrWhiteSpace(disk.CloudDeviceToken)
                && !string.Equals(disk.CloudDeviceToken, failed, StringComparison.Ordinal))
            {
                _settings.CloudDeviceToken = disk.CloudDeviceToken;
                _settings.CloudLicense = disk.CloudLicense;
                _settings.CloudStationId = disk.CloudStationId;
                _settings.CloudStationCode = disk.CloudStationCode;
                return;
            }
        }
        catch { /* خواندنِ دیسک نشد ⇒ همان رفتارِ همیشگی */ }

        _settings.CloudDeviceToken = "";
        _settings.CloudLicense = "";
        Subscription = PumpSubscription.None;
        DeviceDetachedWhy = "این دستگاه از پمپ جدا شده است — برای وصلِ دوباره وارد حساب شوید "
                          + "یا از صاحبِ پمپ بخواهید دوباره اجازه‌اش را بدهد.";
        await SaveQuiet();
    }

    // ── خواندنِ پاسخ ───────────────────────────────────────────────────

    private void ReadSubscription(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object) return;
        if (!json.TryGetProperty("entitlement", out var ent) || ent.ValueKind != JsonValueKind.Object)
            return;

        var source = Str(ent, "source");
        var feats = new List<string>();
        if (ent.TryGetProperty("features", out var f) && f.ValueKind == JsonValueKind.Array)
            foreach (var x in f.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String) feats.Add(x.GetString() ?? "");

        var days = 0;
        long endsAt = 0;
        var plan = "";
        if (source == "trial" && ent.TryGetProperty("trial", out var tr) && tr.ValueKind == JsonValueKind.Object)
        {
            days = (int)Num(tr, "daysLeft");
            endsAt = Num(tr, "endsAt");
            plan = "دورهٔ آزمایشی";
        }
        else if (ent.TryGetProperty("subscription", out var sub) && sub.ValueKind == JsonValueKind.Object)
        {
            days = (int)Num(sub, "daysLeft");
            endsAt = Num(sub, "endsAt");
            plan = Str(sub, "plan");
        }

        Subscription = new PumpSubscription(
            source is "subscription" or "trial", source, plan, days, endsAt, feats);
    }

    private static string StationId(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("station", out var st)
        && st.ValueKind == JsonValueKind.Object
            ? Str(st, "id") : "";

    /// <summary>
    /// کدِ پمپِ همین حساب — همان ‎station.code‎ی سرورِ حساب. ⛔ از امروز همین
    /// کدِ پوشهٔ سرورِ خانگی هم هست (‎StationLink.CodeFor‎)، پس «هر حساب،
    /// پوشهٔ خودش» روی یک عددِ یکتای سرور بند است، نه روی یک پیش‌فرضِ مشترک.
    /// </summary>
    private static string StationCodeOf(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("station", out var st)
        && st.ValueKind == JsonValueKind.Object
            ? AcctLive.CloudCode(Str(st, "code")) : "";

    private static string Str(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v)
        && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    /// <summary>
    /// شناسه — چه سرور رشته بدهد چه عدد.
    ///
    /// ⚠️ <c>users.id</c>ی ابر امروز <c>text</c> است (سنجیده شد، نه حدس:
    /// <c>shop/server/migrations/001_core.sql</c>)، ولی مقایسهٔ «همان حساب
    /// است؟» چیزی است که خرابیِ بی‌صدا می‌دهد، پس عددی بودنِ روزی‌اش هم
    /// از همین‌جا می‌گذرد.
    /// </summary>
    private static string Ident(JsonElement e, string k)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            _ => "",
        };
    }

    private static long Num(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v)
        && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private async Task SaveQuiet()
    {
        try { await _save(); } catch { /* ذخیره نشدنِ تنظیمات نباید کار را بشکند */ }
    }
}
