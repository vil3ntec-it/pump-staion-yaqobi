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
