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

    /// <summary>شنونده‌ای که مجوزِ این برنامه باید داشته باشد.</summary>
    public const string Audience = "tohid-pump-app";

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
