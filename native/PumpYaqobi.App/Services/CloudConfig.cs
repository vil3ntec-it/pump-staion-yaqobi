using System.Security.Cryptography;
using System.Text;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ نشانیِ ابر — در خودِ برنامه، و دیده نمی‌شود ═════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «اون api.vill3n.top رو توی برنامه ذخیره کن و لازم
/// نباشه تو جای تنظیمات باشه، کاری کن توی خود برنامه باشه و دیده نشه.»
///
/// ── چرا ثابت و نه تنظیمات ─────────────────────────────────────────────────
/// این نشانی جایی است که <b>اشتراک</b> از آن می‌آید. اگر از تنظیمات خوانده
/// می‌شد، هر کسی می‌توانست نشانیِ سرورِ خودش را بنویسد و برنامه‌اش را با
/// مجوزِ ساختگیِ خودش باز کند — یعنی قفل با یک کادرِ متنی دور می‌خورد.
///
/// همان قاعده‌ای که ریپوی <c>shop</c> برای <c>AppConfig.kt</c> و
/// <c>api-config.js</c> گذاشته: نشانی قفل است و هیچ‌چیز روی دستگاه نمی‌تواند
/// برنامه را به سرورِ دیگری ببرد.
///
/// ⚠️ این با <see cref="AppSettings.ServerUrl"/> یکی نیست و نباید قاطی شود:
///   • <b>ابر</b> (این‌جا)      = حساب و اشتراک. یکی، ثابت، برای همهٔ پمپ‌ها.
///   • <b>سرورِ خانگی</b> (آن‌جا) = دفتر و دادهٔ زنده. مالِ خودِ پمپ، و نشانی‌اش
///     با هر بار روشن شدنِ مودم عوض می‌شود.
/// </summary>
public static class CloudConfig
{
    /// <summary>نشانیِ ابر. قفل — نه از تنظیمات خوانده می‌شود نه از محیط.</summary>
    public const string BaseUrl = "https://api.vill3n.top";

    /// <summary>
    /// همان <see cref="BaseUrl"/>، با نامی که پرامپتِ «سرورِ مرکزیِ احراز
    /// هویت» می‌خواهد (<c>AUTH_SERVER_URL</c>).
    ///
    /// ⚠️ سرورِ حساب و سرورِ اشتراک <b>یکی‌اند</b> و باید یکی بمانند: مجوزِ
    /// امضاشده به همان حسابی بسته می‌شود که توکنش از همان‌جا آمده. اگر روزی
    /// دو نشانیِ جدا شوند، `LicenseGuard` چیزی را می‌سنجد که از جای دیگری
    /// آمده — یعنی قفل بی‌اثر است.
    /// </summary>
    public const string AuthServerUrl = BaseUrl;

    /// <summary>شنونده‌ای که مجوزِ این برنامه باید داشته باشد.</summary>
    public const string Audience = "tohid-pump-app";

    /// <summary>
    /// شناسهٔ همین برنامه روی سرورِ مرکزی (<c>APPLICATION_ID</c>) — همان
    /// <see cref="Audience"/>، چون سرور با همین رشته مجوزِ این برنامه را از
    /// مجوزِ بخشِ دکان جدا می‌کند. دو نام برای یک چیز نمی‌سازیم.
    /// </summary>
    public const string ApplicationId = Audience;

    /// <summary>
    /// نسخهٔ همین برنامه (<c>APPLICATION_VERSION</c>) — از شمارهٔ اسمبلی که
    /// <c>build-native.yml</c> می‌گذارد، نه یک رشتهٔ دستی.
    /// </summary>
    public static string ApplicationVersion => PumpYaqobi.App.Update.AppVersion.Current;

    /// <summary>
    /// <b>تنها</b> جای ساختنِ نشانیِ یک مسیرِ ابری.
    ///
    /// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/…): «تمام آدرس‌های Server/API باید از
    /// Configuration دریافت شوند و داخل کد به صورت پراکنده Hard-code نشوند.»
    /// نشانی به خواستهٔ خودش <b>قفل</b> ماند (بالا نوشته شده چرا)، ولی
    /// «پراکنده» بودنش رفت: هیچ فایلی دیگر خودش <c>BaseUrl + "…"</c>
    /// نمی‌چسباند.
    ///
    /// ⚠️ <paramref name="path"/> باید با <c>/</c> شروع شود.
    /// </summary>
    public static string Url(string path) => BaseUrl + path;

    /// <summary>
    /// همان <see cref="Url"/> برای وب‌سوکت — <c>wss://</c> به‌جای
    /// <c>https://</c>.
    ///
    /// ⚠️ این‌جاست و جای دیگری نیست، چون <c>LicenseGuardTests</c> هر فایلِ
    /// دیگری را که نامِ نشانی را در خطِ <b>کد</b> بیاورد قرمز می‌کند — و
    /// آن سنجه درست است: نشانی یک جا می‌ماند، وگرنه فردا یکی‌شان عوض
    /// می‌شود و دیگری نه.
    /// </summary>
    public static string WsUrl(string path) =>
        (BaseUrl.StartsWith("https://", StringComparison.Ordinal)
            ? "wss://" + BaseUrl["https://".Length..]
            : "ws://" + BaseUrl["http://".Length..]) + path;

    /// <summary>صادرکنندهٔ مجوز.</summary>
    public const string Issuer = "tohid-license-server";

    // ══ ریشهٔ اعتمادِ مجوز — کلیدهای عمومیِ داخلِ خودِ فایلِ برنامه ═══════════
    //
    //  ⛔ تا امروز تنها ریشهٔ اعتماد <c>AppSettings.CloudPublicKey</c> بود —
    //  کلیدی که در نخستین فعال‌سازی قفل می‌شد (TOFU) و **در فایلِ تنظیماتِ
    //  کاربر** می‌نشست. یعنی کسی که آن یک خط را با کلیدِ خودش عوض می‌کرد و
    //  مجوزِ خودش را امضا می‌کرد، همهٔ قیدهای <see cref="LicenseGuard"/> را
    //  با یک ویرایشگرِ متن دور می‌زد.
    //
    //  حالا ساختِ CI کلیدهای عمومیِ سرور را **داخلِ خودِ اسمبلی** می‌گذارد
    //  (<c>-p:LicenseKeys=…</c> ⇒ <c>[assembly: AssemblyMetadata]</c>). اگر
    //  این فهرست پر باشد، **همین ریشه است** و کلیدِ روی دیسک هیچ اثری ندارد؛
    //  و با <c>kid</c>ِ سرآیندِ مجوز، عوض کردنِ کلیدِ سرور (چرخش) هم شدنی
    //  است بی این‌که نصب‌های امروزی بشکنند: کلیدِ تازه کنارِ کهنه در فهرست
    //  می‌نشیند و یک ساخت بعد، کهنه برداشته می‌شود.
    //
    //  ⚠️ **خالی = همان TOFUِ دیروز**: ساختِ محلی و آزمون‌ها بی کلید ساخته
    //  می‌شوند و باید همان‌طور که بودند کار کنند. و ساختِ CIای که متغیرش را
    //  ندارد هم نمی‌شکند.
    //
    //  شکلِ مقدار: <c>kid1=SPKI1;kid2=SPKI2</c>. ⚠️ خطِ فرمانِ MSBuild هم
    //  <c>;</c> و هم <c>,</c> را جداکنندهٔ خاصیت‌ها می‌خواند، پس ساختِ CI
    //  آن‌ها را به <c>|</c> برمی‌گرداند — این کد هر چهارتا (و فاصله) را
    //  می‌پذیرد. کلیدِ بی <c>kid</c> هم پذیرفته است: خودِ SPKIِ base64.

    private static readonly AsyncLocal<IReadOnlyDictionary<string, string>?> TestKeys = new();

    /// <summary>
    /// ⚠️ فقط برای آزمون — روی <see cref="AsyncLocal{T}"/>، پس آزمون‌های
    /// موازیِ کلاس‌های دیگر (که کلیدِ تصادفیِ خودشان را قفل می‌کنند) هیچ‌وقت
    /// آن را نمی‌بینند. در برنامه هیچ‌جا نوشته نمی‌شود و از تنظیمات یا محیط
    /// هم خوانده نمی‌شود — همان قاعدهٔ قفلِ نشانی.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? TestLicenseKeys
    {
        get => TestKeys.Value;
        set => TestKeys.Value = value;
    }

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Embedded =
        new(() => ParseKeys(Metadata("LicenseKeys")));

    /// <summary>
    /// کلیدهای عمومیِ مجوز، به ازای <c>kid</c>. کلیدِ بی‌نام زیرِ رشتهٔ خالی
    /// (یا <c>#1</c>، <c>#2</c>…) می‌نشیند. خالی یعنی «TOFU».
    /// </summary>
    public static IReadOnlyDictionary<string, string> LicenseKeys => TestLicenseKeys ?? Embedded.Value;

    /// <summary>
    /// این کلیدِ عمومی را می‌شود پذیرفت؟ با فهرستِ خالی همیشه «بله» (TOFU
    /// تصمیمِ بعدی را خودش می‌گیرد)؛ با فهرستِ پر فقط اگر داخلِ همان باشد.
    /// </summary>
    public static bool TrustsKey(string? spki)
    {
        var set = LicenseKeys;
        if (set.Count == 0) return true;
        return !string.IsNullOrWhiteSpace(spki) && set.Values.Contains(spki.Trim());
    }

    /// <summary>خواندنِ یک <c>AssemblyMetadata</c>ِ همین برنامه — خالی اگر نبود.</summary>
    internal static string Metadata(string key)
    {
        try
        {
            foreach (var a in typeof(CloudConfig).Assembly
                         .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false))
                if (a is System.Reflection.AssemblyMetadataAttribute m && m.Key == key)
                    return (m.Value ?? "").Trim();
        }
        catch { /* نبودنش یعنی «ساختِ بی کلید» */ }
        return "";
    }

    /// <summary>«kid1=SPKI1;kid2=SPKI2» ⇒ نقشه. هر تکهٔ بی‌معنا دور ریخته می‌شود.</summary>
    public static IReadOnlyDictionary<string, string> ParseKeys(string raw)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var part in (raw ?? "").Split(new[] { ';', ',', '|', '\n', '\r', ' ' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            //  ⚠️ base64 خودش با «=» تمام می‌شود، پس «=»ی که فقط در دو نویسهٔ
            //  آخر است جداکنندهٔ kid نیست.
            var i = part.IndexOf('=');
            string kid, key;
            if (i > 0 && i < part.Length - 2) { kid = part[..i]; key = part[(i + 1)..]; }
            else { kid = "#" + (++n); key = part; }
            try
            {
                using var ec = ECDsa.Create();
                ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
                map[kid] = key;
            }
            catch { /* کلیدِ خراب هیچ‌وقت ریشهٔ اعتماد نمی‌شود */ }
        }
        return map;
    }

    // ══ اثرِ انگشتِ کامپیوتر — «این نصب از کامپیوترِ دیگری آمده» ═════════════
    //
    //  ⚠️ <see cref="DeviceUid"/> عمداً **دست نخورد**: از نامِ ماشین و کاربر و
    //  مسیرِ نصب ساخته می‌شود و یک بار که ساخته شد در تنظیمات می‌ماند — پس
    //  کپیِ <c>settings.json</c> روی کامپیوترِ دیگر همان <c>duid</c> را با
    //  خودش می‌برد و مجوز «سالم» دیده می‌شد. عوض کردنِ طرزِ ساختنش هم یعنی
    //  باطل شدنِ مجوزِ **همهٔ** نصب‌های امروزی.
    //
    //  پس کنارش یک اثرِ انگشتِ دوم می‌نشیند که از خودِ سیستم‌عامل خوانده
    //  می‌شود (ویندوز: <c>MachineGuid</c>ِ رجیستری · لینوکس:
    //  <c>/etc/machine-id</c>) و در فایل **فقط هشِ نمک‌دارش** است. نصبِ
    //  امروزی که این را ندارد، بارِ اول ثبتش می‌کند (TOFU) و مجوزش سالم
    //  می‌ماند. اگر روزی جور نبود، مجوزِ روی دیسک نامعتبر شمرده می‌شود —
    //  ⛔ بی این‌که یک بیت پاک شود؛ ورودِ دوباره خودش درستش می‌کند.
    //
    //  ⚠️ سیستمی که هیچ شناسه‌ای نمی‌دهد (رشتهٔ خالی) اصلاً سنجیده نمی‌شود:
    //  قفلِ ناخواسته بدتر از بازِ ناخواسته است.

    private static readonly AsyncLocal<Func<string>?> MachineHook = new();

    /// <summary>⚠️ فقط برای آزمون — روی <see cref="AsyncLocal{T}"/>.</summary>
    public static Func<string>? MachineIdOverride
    {
        get => MachineHook.Value;
        set => MachineHook.Value = value;
    }

    /// <summary>شناسهٔ خامِ سیستم‌عامل — هیچ‌وقت جایی نمی‌رود، فقط هشش.</summary>
    private static string MachineId()
    {
        if (MachineIdOverride is { } hook) return hook() ?? "";
        try
        {
            if (OperatingSystem.IsWindows())
            {
                //  ⚠️ نمای ۶۴بیتی: نصبِ ۳۲بیتی روی ویندوزِ ۶۴بیتی بی این، شاخهٔ
                //  WOW64 را می‌خواند.
                using var hk = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
                using var k = hk.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return (k?.GetValue("MachineGuid") as string ?? "").Trim();
            }
            if (File.Exists("/etc/machine-id")) return File.ReadAllText("/etc/machine-id").Trim();
        }
        catch { }
        return "";
    }

    /// <summary>هشِ نمک‌دارِ شناسهٔ این کامپیوتر — خالی یعنی «سیستم چیزی نداد».</summary>
    public static string MachineFingerprint()
    {
        var id = MachineId();
        if (id.Length == 0) return "";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("pump-yaqobi|machine|v1|" + id));
        return "m-" + Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    /// <summary>این تنظیمات از کامپیوترِ دیگری آمده‌اند؟ (ثبت‌نشده ⇒ نه.)</summary>
    public static bool MachineMoved(AppSettings settings)
    {
        var stored = (settings.CloudDeviceMachine ?? "").Trim();
        if (stored.Length == 0) return false;
        var now = MachineFingerprint();
        return now.Length > 0 && !string.Equals(stored, now, StringComparison.Ordinal);
    }

    /// <summary>
    /// اگر هنوز ثبت نشده، همین حالا ثبت کن (TOFU). راست ⇒ چیزی عوض شد و
    /// باید ذخیره شود. ⛔ اثرِ انگشتِ ثبت‌شده هیچ‌وقت بازنویسی نمی‌شود —
    /// وگرنه کپیِ تنظیمات روی کامپیوترِ دیگر خودش را «درست» می‌کرد.
    /// </summary>
    public static bool RecordMachine(AppSettings settings)
    {
        if ((settings.CloudDeviceMachine ?? "").Trim().Length > 0) return false;
        var now = MachineFingerprint();
        if (now.Length == 0) return false;
        settings.CloudDeviceMachine = now;
        return true;
    }

    /// <summary>پیامِ کاربر وقتی اثرِ انگشت جور نیست.</summary>
    public const string MachineMovedWhy = "این نصب از کامپیوترِ دیگری آمده — دوباره وارد شوید";

    /// <summary>
    /// شناسهٔ این کامپیوتر — ثابت می‌ماند و از دستگاهِ دیگری درنمی‌آید.
    ///
    /// مجوز به همین بسته می‌شود، پس کپی کردنِ پوشهٔ برنامه روی کامپیوترِ
    /// دیگر، اشتراک را با خودش نمی‌برد.
    ///
    /// ⚠️ از نامِ ماشین و شناسهٔ کاربر و مسیرِ نصب ساخته می‌شود و بعد هش
    /// می‌شود — پس خودِ این مقادیر هیچ‌جا نمی‌روند، فقط اثرشان.
    /// </summary>
    public static string DeviceUid(AppSettings settings)
    {
        //  اگر یک‌بار ساخته شده، همان می‌ماند. عوض شدنش یعنی مجوزِ قبلی
        //  باطل می‌شود و کاربر بی‌دلیل از کار می‌افتد.
        if (!string.IsNullOrWhiteSpace(settings.CloudDeviceUid)) return settings.CloudDeviceUid;

        var seed = string.Join('|',
            Environment.MachineName,
            Environment.UserName,
            Environment.OSVersion.Platform.ToString(),
            AppContext.BaseDirectory);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var uid = "pc-" + Convert.ToHexString(hash).ToLowerInvariant()[..24];
        settings.CloudDeviceUid = uid;
        return uid;
    }
}
