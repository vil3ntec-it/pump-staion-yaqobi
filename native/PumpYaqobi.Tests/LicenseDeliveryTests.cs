using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ اشتراکی که مدیر داد، باید به خودِ برنامه برسد ═══════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۸): «اشتراک که می‌دم توی حساب همون
/// نفر… برنامه اونو دریافت کنه و قفل‌ها باز بشه و درست کار کنه… و بتونم
/// اشتراکشو بردارم یا روش اضافه کنم… و تاریخِ موعود که رسید اشتراک تموم
/// بشه و دسترسی‌ها و غیره.»
///
/// ⛔ <b>و نمی‌رسید.</b> <see cref="CloudLink.RefreshAsync"/> تنها جای گرفتنِ
/// مجوزِ امضاشده است — و تنها صداکننده‌اش صفحهٔ <b>پروفایل</b> بود. حلقهٔ
/// شصت‌ثانیه‌ایِ پس‌زمینه فقط <c>HomeFromAccountAsync</c> را می‌زد، و آن
/// برای دستگاهی که از قبل بند شده هیچ مجوزی نمی‌گیرد.
///
/// نتیجه‌اش دو خرابیِ <b>پولی</b> بود، در دو جهت:
///
/// <list type="number">
///   <item>اشتراکِ تازه ⇒ <c>me</c> می‌گفت «فعال»، ولی فهرستِ قابلیت‌ها
///     نمی‌آمد — و فهرستِ <b>نیامده</b> یعنی «پلنِ کامل». پس مشتریِ
///     <b>استاندارد</b> هر شش قفل را باز می‌دید.</item>
///   <item>اشتراکِ برداشته‌شده ⇒ مجوزِ کهنه روی دیسک می‌ماند و تا انقضای
///     خودش باز بود، و بعدش هم چهارده روز ارفاق.</item>
/// </list>
///
/// ⚠️ سرورِ ساختگی است، ولی مجوزش <b>واقعاً</b> با ES256 امضا می‌شود — پس
/// خودِ <see cref="LicenseGuard"/> هم واقعاً سنجیده می‌شود، نه دور زده.
/// </summary>
//  ⚠️ `AppSettings.DirOverride` استاتیک است و xUnit کلاس‌ها را موازی
//  می‌دواند — قاعدهٔ ۱۴۰۵/۰۶/۳۰.
//
//  ⛔ و این کلاس **هیچ‌کدام از کلیدهای سراسریِ `Entitlements` را دست
//  نمی‌زند** (`Unlocked` · `TestDeny` · `Now`): `AppLinksTests` همان‌ها را
//  عوض می‌کند و `[Collection]` ندارد، پس با این کلاس موازی می‌دود. به‌جایش
//  ادعاها روی خودِ **رکوردِ** `EntitlementState` است — `Open` · `Listed` ·
//  `Features` — که خالص‌اند و به هیچ کلیدی بند نیستند. مرزِ خودِ `Allows`
//  جای دیگری سنجیده می‌شود (`EntitlementsTests`).
[Collection(AppHostCollection.Name)]
public class LicenseDeliveryTests : IDisposable
{
    private const string Station = "stn_delivery_1";

    private readonly string _dir;
    private readonly string? _was;
    private readonly List<string> _hits = new();
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public LicenseDeliveryTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-lic-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        //  ⚠️ `CloudLink.Reach` استاتیک است و هر درخواستِ ساختگی پرش
        //  می‌کند؛ بندِ ۱ی `CloudReachTests` («تا نپرسیده‌ایم، هیچ ادعایی
        //  نیست») روی `Unknown` بودنش حساب می‌کند و هم‌کالکشنِ ماست.
        CloudLink.ResetReach();
        AppSettings.DirOverride = _was;
        _key.Dispose();
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    // ── سرورِ ساختگی ───────────────────────────────────────────────────

    private string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    /// <summary>مجوزی دقیقاً به شکلی که سرور می‌سازد (‎ieee-p1363‎).</summary>
    private string Sign(string deviceUid, long now, string[] features, long endsAt)
    {
        static string B64(byte[] b) =>
            Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = "tohid-license-server",
            ["aud"] = "tohid-pump-app",
            ["duid"] = deviceUid,
            ["stn"] = Station,
            ["sub"] = Station,
            ["iat"] = now,
            ["nbf"] = now - 60_000,
            ["exp"] = now + 10L * 24 * 3600 * 1000,
            ["sub_ends"] = endsAt,
            ["feat"] = features,
            ["core"] = new[] { "debtors", "waraq" },
            ["plan_title"] = "استاندارد",
        };

        var header = B64(Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"TLIC"}"""));
        var body = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var sig = _key.SignData(Encoding.UTF8.GetBytes($"{header}.{body}"),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{body}.{B64(sig)}";
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private void Serve(Func<string, HttpResponseMessage> handler) =>
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            _hits.Add(path);
            return Task.FromResult(handler(path));
        };

    private (CloudLink Link, AppSettings File) Bound(Action<AppSettings>? seed = null)
    {
        var f = AppSettings.Load();
        f.CloudAccountToken = "acc-1";
        f.CloudAccessExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;
        f.CloudDeviceToken = "dev-1";               // یعنی «از قبل بند شده»
        f.CloudStationId = Station;
        f.CloudPublicKey = PublicKey;               // قفلِ TOFU، همان کلیدِ این سرور
        seed?.Invoke(f);
        f.Save();
        return (new CloudLink(f, () => { f.Save(); return Task.CompletedTask; }), f);
    }

    /// <summary>
    /// پاسخِ <c>/api/pump/me</c> — «اشتراک فعال است یا نه».
    ///
    /// ⚠️ شکلش <b>خوانده شده، نه حدس زده</b>:
    /// <c>shop/server/src/lib/entitlement.js</c>. کلیدِ «فعال بودن»
    /// <c>source</c> است (<c>subscription</c> · <c>trial</c> ·
    /// <c>free</c> · <c>none</c>)، نه یک <c>active</c>ِ بولی — و
    /// <see cref="CloudLink"/> هم روی همان می‌سنجد.
    /// </summary>
    //  ⚠️ با `JsonSerializer` ساخته می‌شود، نه رشتهٔ خام: JSONِ تودرتو
    //  دنباله‌ای از `}}` می‌سازد و در رشتهٔ `$$"""` آن همان نشانهٔ بستنِ
    //  درج است. یک بار همین‌جا نوشته شد و خوانا هم نبود.
    private static string Me(bool active, long endsAt) =>
        JsonSerializer.Serialize(new
        {
            station = new { id = Station },
            home = new { url = "http://192.168.1.9:4701", readKey = "rk", station = "yaqobi" },
            entitlement = new
            {
                source = active ? "subscription" : "free",
                features = Array.Empty<string>(),
                subscription = new { plan = "standard", endsAt, daysLeft = 30 },
            },
        });

    /// <summary>پاسخِ <c>/api/pump/device/me</c> — همان، بی نشانیِ خانگی.</summary>
    private static string DeviceMe(bool active) =>
        JsonSerializer.Serialize(new
        {
            station = new { id = Station },
            entitlement = new { source = active ? "subscription" : "free" },
        });

    private static long Days(int n) =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + n * 24L * 3600 * 1000;

    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// ⭐ <b>همان باگِ اصلی.</b> مدیر روی سرور اشتراک می‌دهد؛ دستگاه از قبل
    /// بند است و کاربر هیچ صفحه‌ای را باز نمی‌کند. حلقهٔ پس‌زمینه باید
    /// همان دور مجوز را بگیرد — با <b>فهرستِ واقعیِ پلن</b>.
    /// </summary>
    [Fact]
    public async Task Eshterake_Taze_Hamon_Dor_Miresad_Ba_Feheresteh_Vagheie()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (link, f) = Bound();
        var uid = CloudConfig.DeviceUid(f);

        Serve(path => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, Me(true, Days(30))),
            "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe(true)),
            "/api/pump/device/license" => Json(HttpStatusCode.OK,
                JsonSerializer.Serialize(new
                {
                    license = Sign(uid, now, new[] { "cloudbackup" }, Days(30)),
                    publicKey = PublicKey,
                })),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        await link.HomeFromAccountAsync();
        Assert.True(link.Subscription.Active);
        //  پیش از این خط، هیچ مجوزی نیامده بود
        Assert.False(link.Verify().Valid);

        await link.KeepLicenseFreshAsync();

        //  ⛔ مجوز واقعاً گرفته شد و واقعاً معتبر است
        var check = link.Verify();
        Assert.True(check.Valid);
        Assert.True(check.HasFeatureList);
        Assert.Contains("/api/pump/device/license", _hits);

        //  ⛔ و فهرست **واقعیِ پلن** روی دیسک نشست، نه «همه».
        //
        //  ⚠️ عمداً **بی** `live` سنجیده می‌شود، چون خودِ برنامه هم همین‌طور
        //  تصمیم می‌گیرد: `Entitlements.Allows` ⇒ `State()` بی پارامتر ⇒
        //  فقط از روی دیسک. حالِ زندهٔ اشتراک در حافظهٔ یک `CloudLink`ِ
        //  یک‌بارمصرفِ حلقهٔ پس‌زمینه است و هیچ‌وقت به آن‌جا نمی‌رسد — و
        //  **همین** ریشهٔ باگ بود: تنها چیزی که روی دیسک می‌نشست مُهرِ
        //  `EntitledUntil` بود، و `InGrace` هر شش قفل را باز می‌کند. یعنی
        //  اشتراکِ استاندارد، پلنِ کامل دیده می‌شد.
        var state = Entitlements.State(AppSettings.Load());
        Assert.True(state.Open);
        //  ⛔ «فهرست آمد» — و همین است که «پلنِ کامل» را از کار می‌اندازد
        Assert.True(state.Listed);
        Assert.Contains(Entitlements.CloudBackup, state.Features);
        Assert.DoesNotContain(Entitlements.QrLive, state.Features);
        Assert.DoesNotContain(Entitlements.Kar, state.Features);
        //  و پشتیبانی هیچ‌وقت قفل نمی‌شود — پیش از هر کلیدِ سراسری
        //  (`Unlocked` · `TestDeny`) جواب می‌دهد، پس این‌جا امن است.
        Assert.True(state.Allows(Entitlements.Support));
    }

    /// <summary>
    /// ⛔ <b>برداشتنِ اشتراک هم همان دور می‌رسد.</b> مجوزِ کهنه روی دیسک
    /// می‌ماند و تا انقضای خودش باز بود — «بتونم اشتراکشو بردارم».
    /// </summary>
    [Fact]
    public async Task Bardashtane_Eshterak_Ham_Haman_Dor_Miresad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (link, f) = Bound();
        var uid = CloudConfig.DeviceUid(f);
        f.CloudLicense = Sign(uid, now, new[] { "cloudbackup" }, Days(30));
        f.Save();
        Assert.True(link.Verify().Valid);           // مجوزِ دیروز، هنوز معتبر

        Serve(path => path switch
        {
            //  سرور حالا می‌گوید اشتراکی نیست
            "/api/pump/me" => Json(HttpStatusCode.OK, Me(false, 0)),
            "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe(false)),
            //  ⚠️ مجوزِ **خالی** — همان چیزی که سرورِ واقعی برای اشتراکِ
            //  تمام‌شده می‌دهد؛ خطا نیست، جوابِ درست است.
            "/api/pump/device/license" => Json(HttpStatusCode.OK, """{"license":""}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        await link.HomeFromAccountAsync();
        await link.KeepLicenseFreshAsync();

        Assert.Equal("", AppSettings.Load().CloudLicense);
        Assert.False(link.Verify().Valid);
    }

    /// <summary>
    /// ⚠️ <b>و تا چیزی عوض نشده، هیچ درخواستی زده نمی‌شود</b> — همان قاعدهٔ
    /// همیشگیِ این ریپو. مجوز و حرفِ سرور جورند و تیکِ ده‌دقیقه‌ای هم
    /// نرسیده، پس این دور رایگان است.
    /// </summary>
    [Fact]
    public async Task Ta_Chizi_Avaz_Nashode_Hich_Darkhasti_Nemizanad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (link, f) = Bound();
        var uid = CloudConfig.DeviceUid(f);
        f.CloudLicense = Sign(uid, now, new[] { "cloudbackup" }, Days(30));
        f.CloudSyncedAt = now;                      // همین حالا تازه شده
        f.Save();

        Serve(path => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, Me(true, Days(30))),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        await link.HomeFromAccountAsync();
        _hits.Clear();
        await link.KeepLicenseFreshAsync();

        Assert.Empty(_hits);
    }

    /// <summary>
    /// ⛔ <b>اشتراکی که مدیر به حسابِ «باز» داد همان دقیقه می‌رسد</b>، نه ده
    /// دقیقه بعد (۱۴۰۵/۰۷/۱۳، سنجهٔ `livestack` با سرورِ واقعی): دورهٔ
    /// آزمایشی ⇒ وی‌آی‌پی هر دو طرف را «باز» نگه می‌دارد، پس «ناجوری» نمی‌بیندش.
    /// و نخستین دیدنِ یک مجوز خطِ پایه است، نه «چیزِ تازه».
    /// </summary>
    [Fact]
    public async Task EshterakeTaze_BiEntezar_MajvozMigirad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (link, f) = Bound();
        var uid = CloudConfig.DeviceUid(f);
        f.CloudLicense = Sign(uid, now, new[] { "cloudbackup" }, Days(30));
        f.CloudSyncedAt = now;
        f.Save();

        var ends = Days(30);
        Serve(path => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, Me(true, ends)),
            "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe(true)),
            "/api/pump/device/license" => Json(HttpStatusCode.OK,
                JsonSerializer.Serialize(new
                {
                    license = Sign(uid, now, new[] { "cloudbackup", "kar" }, Days(365)),
                    publicKey = PublicKey,
                })),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        //  خطِ پایه: هیچ درخواستی
        await link.HomeFromAccountAsync();
        _hits.Clear();
        await link.KeepLicenseFreshAsync();
        Assert.Empty(_hits);

        //  مدیر اشتراک داد: پایان جلو رفت ⇒ همین دور مجوزِ تازه
        ends = Days(365);
        await link.HomeFromAccountAsync();
        _hits.Clear();
        await link.KeepLicenseFreshAsync();
        Assert.Contains("/api/pump/device/license", _hits);
        Assert.Contains("kar", link.Verify().Features);

        //  و بارِ بعد، بی تغییر، دوباره رایگان است
        await link.HomeFromAccountAsync();
        _hits.Clear();
        await link.KeepLicenseFreshAsync();
        Assert.Empty(_hits);
    }

    /// <summary>
    /// ⚠️ ولی <b>تیکِ ده‌دقیقه‌ای</b> بالاخره می‌رسد — و لازم است: عوض شدنِ
    /// <b>پلن</b> (استاندارد ⇒ وی‌آی‌پی) هر دو طرف را «فعال» نگه می‌دارد،
    /// پس از راهِ «ناجوری» هیچ‌وقت دیده نمی‌شود.
    /// </summary>
    [Fact]
    public async Task Tike_DahDaghighei_Feheresteh_Taze_Ra_Miavarad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (link, f) = Bound();
        var uid = CloudConfig.DeviceUid(f);
        f.CloudLicense = Sign(uid, now, new[] { "cloudbackup" }, Days(30));
        f.CloudSyncedAt = now - (long)CloudLink.LicenseTick.TotalMilliseconds - 1000;
        f.Save();

        Serve(path => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, Me(true, Days(365))),
            "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe(true)),
            "/api/pump/device/license" => Json(HttpStatusCode.OK,
                JsonSerializer.Serialize(new
                {
                    //  ارتقا به وی‌آی‌پی — همان اشتراک، فهرستِ بزرگ‌تر
                    license = Sign(uid, now, new[] { "kar", "qrlive", "cloudbackup" }, Days(365)),
                })),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        await link.HomeFromAccountAsync();
        await link.KeepLicenseFreshAsync();

        var state = Entitlements.State(AppSettings.Load());
        Assert.True(state.Open);
        Assert.True(state.Listed);
        Assert.Contains(Entitlements.Kar, state.Features);
        Assert.Contains(Entitlements.QrLive, state.Features);
    }

    /// <summary>
    /// ⛔ <b>نصبی که بند نشده هیچ درخواستی نمی‌زند.</b> مجوز فقط با توکنِ
    /// دستگاه صادر می‌شود، پس زدنش بی آن فقط یک ۴۰۱ِ بی‌فایده است.
    /// </summary>
    [Fact]
    public async Task Nasbe_Bandnashode_Hich_Darkhasti_Nemizanad()
    {
        var f = AppSettings.Load();
        f.CloudAccountToken = "acc-1";
        f.Save();
        var link = new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });

        Serve(_ => Json(HttpStatusCode.OK, "{}"));
        await link.KeepLicenseFreshAsync();

        Assert.Empty(_hits);
    }

    /// <summary>
    /// ⛔ <b>بی‌اینترنت هیچ چیزی را نمی‌بندد و هیچ استثنایی بیرون نمی‌دهد.</b>
    /// مجوزِ دیروز سرِ جایش می‌ماند — «نمی‌خوام کسی که اشتراک خریده با یک
    /// باگ خراب بشه».
    /// </summary>
    [Fact]
    public async Task BiInternet_Majoze_Diruz_Sare_Jayash_Mimanad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (link, f) = Bound();
        var uid = CloudConfig.DeviceUid(f);
        var good = Sign(uid, now, new[] { "cloudbackup" }, Days(30));
        f.CloudLicense = good;
        f.CloudSyncedAt = 0;                        // یعنی «حتماً بپرس»
        f.Save();

        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("شبکه نیست");

        //  ⛔ استثنا بیرون نمی‌زند
        await link.KeepLicenseFreshAsync();

        Assert.Equal(good, AppSettings.Load().CloudLicense);
        Assert.True(link.Verify().Valid);
    }

    /// <summary>
    /// ⛔ حلقهٔ پس‌زمینه واقعاً از این در رد می‌شود — وگرنه همهٔ سنجه‌های
    /// بالا سبز می‌مانند و در برنامه هیچ اتفاقی نمی‌افتد.
    /// </summary>
    [Fact]
    public void Halgheye_PasZamine_Vagheaan_In_Dar_Ra_Mizanad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var src = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Services", "StationPublisher.cs"));
        var i = src.IndexOf("private static async Task CloudKeepAsync", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = src[i..(i + 900)];
        Assert.Contains("HomeFromAccountAsync", body);
        Assert.Contains("KeepLicenseFreshAsync", body);
        //  و پس از آن، نه پیش از آن: بی حالِ تازهٔ اشتراک، «ناجوری» معنا ندارد
        Assert.True(body.IndexOf("HomeFromAccountAsync", StringComparison.Ordinal)
                  < body.IndexOf("KeepLicenseFreshAsync", StringComparison.Ordinal));
    }
}
