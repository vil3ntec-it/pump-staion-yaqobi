using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ مجوزِ امضاشدهٔ ساختگی، برای سنجه‌هایی که «پلن‌دار» بودن لازم دارند ═══
///
/// ⛔ <b>چرا این لازم شد، و چرا این شکل:</b> از ۱۴۰۵/۰۶/۳۰ شش دروازهٔ
/// پلن داریم (‎Entitlements.Paid‎) و نصبِ تازه‌ای که هیچ اشتراکی ندارد
/// هیچ‌کدامشان را باز نمی‌بیند. سنجهٔ <c>verify</c> در یک پوشهٔ موقتِ خالی
/// بالا می‌آید، پس یک‌شبه دو رفتارِ سالم را «✖» دید: تاریخچهٔ گاوصندوق باز
/// نمی‌شد و پیامِ آمادهٔ واتساپ خالی بود — هر دو <b>درست</b>، ولی سنجه
/// انتظارِ کهنه داشت.
///
/// راهِ درست «ضعیف کردنِ سنجه» نبود؛ این بود که سنجه واقعاً یک پلن داشته
/// باشد. پس این‌جا یک جفت‌کلیدِ ES256 ساخته می‌شود، مجوز <b>واقعاً</b> با
/// آن امضا می‌شود، و کلیدِ عمومیِ همان جفت در تنظیمات قفل می‌شود — دقیقاً
/// همان کاری که نخستین فعال‌سازیِ واقعی می‌کند (TOFU).
///
/// ⚠️ <b>این هیچ قفلی را ضعیف نمی‌کند.</b> کلیدِ خصوصی همین‌جا در حافظهٔ
/// خودِ سنجه ساخته می‌شود و هیچ‌جا نمی‌رود؛ برنامهٔ واقعی روی کامپیوترِ
/// کاربر کلیدِ سرور را قفل کرده و مجوزِ این‌جا روی آن نمی‌نشیند
/// (<c>key_mismatch</c>).
///
/// ⚠️ و <b>باید پیش از ساختنِ ‎MainViewModel‎ صدا زده شود</b>: آن ویومدل
/// نسخهٔ خودش از <see cref="AppSettings"/> را نگه می‌دارد و با ذخیرهٔ
/// «آخرین بخش» همان را روی دیسک می‌نویسد — پس هر نوشتنی بعد از ساختنش
/// همان لحظه پاک می‌شود. (همان تله‌ای که ۱۴۰۵/۰۶/۳۰ در `CLAUDE.md` ثبت شد.)
/// </summary>
internal static class FakeLicense
{
    private static readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>هر شش دروازه — همان چیزی که پلنِ وی‌آی‌پی و دائمی می‌دهند.</summary>
    public static readonly string[] VipFeatures =
    {
        Entitlements.Kar, Entitlements.QrLive, Entitlements.CloudBackup,
        Entitlements.Profit, Entitlements.History, Entitlements.Dashboard,
    };

    /// <summary>فقط پشتیبانِ ابری — همان مرزِ پلنِ «استاندارد».</summary>
    public static readonly string[] StandardFeatures = { Entitlements.CloudBackup };

    private static string B64(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>یک مجوزِ واقعاً امضاشده برای همین دستگاه و همین پمپ.</summary>
    public static string Token(string deviceUid, string stationId,
                              IReadOnlyList<string> features, string planTitle = "VIP")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var head = B64(Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"JWT"}"""));
        var body = B64(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = CloudConfig.Issuer,
            aud = CloudConfig.Audience,
            duid = deviceUid,
            stn = stationId,
            nbf = now - 60_000,
            exp = now + 7L * 24 * 3600 * 1000,
            sub_ends = now + 42L * 24 * 3600 * 1000,
            plan_title = planTitle,
            feat = features,
            core = Array.Empty<string>(),
        }));
        var sig = Key.SignData(Encoding.UTF8.GetBytes($"{head}.{body}"),
                               HashAlgorithmName.SHA256,
                               DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{head}.{body}.{B64(sig)}";
    }

    /// <summary>
    /// این نصب را «پلن‌دار» می‌کند — توکنِ دستگاه، کلیدِ عمومیِ قفل‌شده،
    /// شناسهٔ پمپ و مجوزِ امضاشده، همه روی دیسک.
    /// </summary>
    /// <returns>کدِ اپِ کارمندانی که روی تنظیمات نشست.</returns>
    public static string Grant(IReadOnlyList<string>? features = null,
                               string planTitle = "VIP",
                               string stationId = "stn-verify")
    {
        var f = AppSettings.Load();
        var uid = CloudConfig.DeviceUid(f);          // اگر نبود، همان‌جا ساخته می‌شود
        f.CloudDeviceToken = "pd_verify_probe";
        f.CloudPublicKey = Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo());
        f.CloudStationId = stationId;
        f.CloudLicense = Token(uid, stationId, features ?? VipFeatures, planTitle);
        if (string.IsNullOrWhiteSpace(f.CloudAccessCode)) f.CloudAccessCode = "K7PM3XQ2";
        f.Save();
        return f.CloudAccessCode;
    }
}
