using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ مجوزِ آفلاین — درزهایی که ۱۴۰۵/۰۷/۱۲ بسته شدند ══════════════════════════
///
/// بازبینیِ امنیتی نشان داد که کلیدِ عمومی، مجوز، مُهرِ ارفاق و شناسهٔ
/// دستگاه همه در <c>settings.json</c>ِ نوشتنی می‌نشستند؛ مجوزِ تازهٔ سرور بی
/// سنجش جای مجوزِ سالم می‌نشست؛ و کپیِ همان فایل روی کامپیوترِ دیگر اشتراک
/// را با خودش می‌برد. هر بندِ این فایل یکی از آن‌ها را با <b>رفتار</b>
/// می‌سنجد — مجوز واقعاً با ES256 امضا می‌شود، سرور ساختگی است.
///
/// ⚠️ کلیدهای سراسریِ <c>Entitlements</c> (<c>Unlocked</c> · <c>Now</c>)
/// این‌جا دست نمی‌خورند — ادعاها روی خودِ <see cref="LicenseCheck"/> و
/// رکوردِ <see cref="EntitlementState"/> است. و ریشهٔ اعتماد و اثرِ انگشت
/// روی <see cref="AsyncLocal{T}"/>اند، پس آزمون‌های موازی نمی‌بینندشان.
/// </summary>
[Collection(AppHostCollection.Name)]
public class LicenseHardeningTests : IDisposable
{
    private const string Station = "stn_hard_1";

    private readonly string _dir;
    private readonly string? _was;
    private readonly List<string> _hits = new();
    private readonly ECDsa _real = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _evil = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public LicenseHardeningTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-hard-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        LicenseClock.ForgetRunning();
        AppSettings.DirOverride = _was;
        _real.Dispose();
        _evil.Dispose();
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    // ── ساختنِ مجوز ────────────────────────────────────────────────────

    private static string Pub(ECDsa k) => Convert.ToBase64String(k.ExportSubjectPublicKeyInfo());

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static string Sign(ECDsa key, string deviceUid, long iat, string[] feat, string? kid = null)
    {
        static string B64(byte[] b) =>
            Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = "tohid-license-server",
            ["aud"] = "tohid-pump-app",
            ["duid"] = deviceUid,
            ["stn"] = Station,
            ["iat"] = iat,
            ["nbf"] = iat - 60_000,
            ["exp"] = iat + 10L * 24 * 3600 * 1000,
            ["sub_ends"] = iat + 30L * 24 * 3600 * 1000,
            ["feat"] = feat,
            ["core"] = new[] { "debtors" },
            ["plan_title"] = "آزمون",
        };
        var head = kid is null
            ? """{"alg":"ES256","typ":"TLIC"}"""
            : "{\"alg\":\"ES256\",\"typ\":\"TLIC\",\"kid\":\"" + kid + "\"}";
        var header = B64(Encoding.UTF8.GetBytes(head));
        var body = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var sig = key.SignData(Encoding.UTF8.GetBytes($"{header}.{body}"),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{body}.{B64(sig)}";
    }

    private static HttpResponseMessage Json(HttpStatusCode code, object body) =>
        new(code)
        {
            Content = new StringContent(body as string ?? JsonSerializer.Serialize(body),
                                        Encoding.UTF8, "application/json"),
        };

    private void Serve(Func<string, HttpRequestMessage, HttpResponseMessage> handler) =>
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            lock (_hits) _hits.Add(path);
            return Task.FromResult(handler(path, req));
        };

    /// <summary>دستگاهی که از قبل بند است و مجوزِ سالمِ دیروز را دارد.</summary>
    private (CloudLink Link, AppSettings File, string Good) Bound()
    {
        var f = AppSettings.Load();
        f.CloudDeviceToken = "dev-1";
        f.CloudStationId = Station;
        f.CloudPublicKey = Pub(_real);
        var good = Sign(_real, CloudConfig.DeviceUid(f), Now, new[] { "cloudbackup" });
        f.CloudLicense = good;
        f.Save();
        return (new CloudLink(f, () => { f.Save(); return Task.CompletedTask; }), f, good);
    }

    private static object DeviceMe() => new
    {
        station = new { id = Station },
        entitlement = new { source = "subscription" },
    };

    // ══ ۱) مجوزِ تازهٔ سرور پیش از نشستن سنجیده می‌شود ══════════════════

    /// <summary>
    /// ⛔ پاسخی با مجوزی که با کلیدِ <b>دیگری</b> امضا شده (سرورِ جعلی، یا
    /// باگِ سرور) مجوزِ سالمِ دیروز را پاک نمی‌کند — و کلیدِ قفل‌شده هم
    /// عوض نمی‌شود.
    /// </summary>
    [Fact]
    public async Task Refresh_MojavezeKelideDigar_JayeMojavezeSalem_NemiNeshinad()
    {
        var (link, f, good) = Bound();
        var uid = CloudConfig.DeviceUid(f);
        var forged = Sign(_evil, uid, Now, Entitlements.Paid);

        //  الف) سرور کلیدِ دیگری هم همراهش می‌دهد ⇒ key_mismatch
        Serve((path, _) => path switch
        {
            "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe()),
            "/api/pump/device/license" => Json(HttpStatusCode.OK, new { license = forged, publicKey = Pub(_evil) }),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });
        var r1 = await link.RefreshAsync();
        Assert.False(r1.Ok);
        Assert.Equal("key_mismatch", r1.Code);
        var disk = AppSettings.Load();
        Assert.Equal(good, disk.CloudLicense);
        Assert.Equal(Pub(_real), disk.CloudPublicKey);

        //  ب) بی کلید — فقط امضایش نمی‌خورد ⇒ bad_license
        Serve((path, _) => path switch
        {
            "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe()),
            "/api/pump/device/license" => Json(HttpStatusCode.OK, new { license = forged }),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });
        var r2 = await link.RefreshAsync();
        Assert.False(r2.Ok);
        Assert.Equal("bad_license", r2.Code);
        Assert.Equal(good, AppSettings.Load().CloudLicense);
        Assert.True(link.Verify().Valid);
    }

    /// <summary>
    /// ⚠️ و جوابِ <b>قطعیِ</b> «مجوزی نیست» هنوز پاکش می‌کند — و ارفاق هم با
    /// آن تمام می‌شود، چون ارفاق از خودِ مجوز می‌آید.
    /// </summary>
    [Fact]
    public async Task Refresh_JavabeGhatieBiMojavez_HamPakMikonad_HamArfaghRa()
    {
        var (link, _, _) = Bound();
        Serve((path, _) => path switch
        {
            "/api/pump/device/me" => Json(HttpStatusCode.OK, new { station = new { id = Station }, entitlement = new { source = "free" } }),
            "/api/pump/device/license" => Json(HttpStatusCode.OK, """{"license":null}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        var r = await link.RefreshAsync();
        Assert.True(r.Ok, r.Why);
        var disk = AppSettings.Load();
        Assert.Equal("", disk.CloudLicense);
        var st = Entitlements.State(disk);
        Assert.False(st.InGrace);
        Assert.Equal(0, st.GraceUntil);
    }

    // ══ ۲) ریشهٔ اعتمادِ داخلِ برنامه ════════════════════════════════════

    /// <summary>
    /// ⭐ با فهرستِ کلیدهای داخلِ برنامه، کلیدِ روی دیسک <b>هیچ اثری ندارد</b>:
    /// کسی که کلیدِ خودش را در <c>settings.json</c> بنویسد و مجوزِ خودش را
    /// امضا کند، رد می‌شود؛ و مجوزِ سرورِ واقعی — با <c>kid</c> — پذیرفته
    /// می‌شود حتی اگر کلیدِ روی دیسک چیزِ دیگری باشد.
    /// </summary>
    [Fact]
    public void KelidhayeDakheleBarname_RisheyeEtemadand()
    {
        CloudConfig.TestLicenseKeys = CloudConfig.ParseKeys("k1=" + Pub(_real));
        try
        {
            var f = AppSettings.Load();
            f.CloudDeviceToken = "dev-1";
            f.CloudStationId = Station;
            var uid = CloudConfig.DeviceUid(f);

            //  الف) کلید و مجوزِ دست‌سازِ روی دیسک ⇒ رد
            f.CloudPublicKey = Pub(_evil);
            f.CloudLicense = Sign(_evil, uid, Now, Entitlements.Paid);
            var forged = LicenseGuard.CheckStored(f);
            Assert.False(forged.Valid);
            Assert.False(forged.SignatureOk);

            //  ب) مجوزِ سرورِ واقعی با kid ⇒ پذیرفته، با همان کلیدِ دست‌سازِ روی دیسک
            f.CloudLicense = Sign(_real, uid, Now, new[] { "cloudbackup" }, kid: "k1");
            Assert.True(LicenseGuard.CheckStored(f).Valid);

            //  ج) بی kid ⇒ هر کلیدِ فهرست امتحان می‌شود
            f.CloudLicense = Sign(_real, uid, Now, new[] { "cloudbackup" });
            Assert.True(LicenseGuard.CheckStored(f).Valid);

            //  د) kidِ ناشناس ⇒ رد، حتی اگر امضا با کلیدِ درست باشد
            f.CloudLicense = Sign(_real, uid, Now, new[] { "cloudbackup" }, kid: "k9");
            Assert.False(LicenseGuard.CheckStored(f).Valid);
        }
        finally { CloudConfig.TestLicenseKeys = null; }
    }

    /// <summary>
    /// ⛔ و با همان فهرست، سرورِ جعلی نمی‌تواند کلیدِ «قفل‌شده»ای را که
    /// داخلِ فهرست نیست به تنظیمات بنشاند.
    /// </summary>
    [Fact]
    public async Task KelidhayeDakheleBarname_KelideBiroonAzFehrest_RaRadMikonad()
    {
        CloudConfig.TestLicenseKeys = CloudConfig.ParseKeys("k1=" + Pub(_real));
        try
        {
            var (link, f, good) = Bound();
            var uid = CloudConfig.DeviceUid(f);
            Serve((path, _) => path switch
            {
                "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe()),
                "/api/pump/device/license" => Json(HttpStatusCode.OK,
                    new { license = Sign(_evil, uid, Now, Entitlements.Paid), publicKey = Pub(_evil) }),
                _ => Json(HttpStatusCode.NotFound, "{}"),
            });

            var r = await link.RefreshAsync();
            Assert.Equal("key_mismatch", r.Code);
            Assert.Equal(good, AppSettings.Load().CloudLicense);
            Assert.Equal(Pub(_real), AppSettings.Load().CloudPublicKey);

            //  و چرخشِ **درست** — کلیدی که داخلِ فهرست است — پذیرفته می‌شود
            using var next = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CloudConfig.TestLicenseKeys = CloudConfig.ParseKeys(
                "k1=" + Pub(_real) + ",k2=" + Pub(next));
            var rotated = Sign(next, uid, Now, new[] { "cloudbackup" }, kid: "k2");
            Serve((path, _) => path switch
            {
                "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe()),
                "/api/pump/device/license" => Json(HttpStatusCode.OK, new { license = rotated, publicKey = Pub(next) }),
                _ => Json(HttpStatusCode.NotFound, "{}"),
            });
            var r2 = await link.RefreshAsync();
            Assert.True(r2.Ok, r2.Why);
            Assert.Equal(rotated, AppSettings.Load().CloudLicense);
            Assert.True(link.Verify().Valid);
        }
        finally { CloudConfig.TestLicenseKeys = null; }
    }

    [Fact]
    public void ParseKeys_KelideKharab_RaDoorMirizad_VaBiKid_RaMipazirad()
    {
        var map = CloudConfig.ParseKeys("a=" + Pub(_real) + "; b=not-a-key ," + Pub(_evil));
        Assert.Equal(2, map.Count);
        Assert.Equal(Pub(_real), map["a"]);
        Assert.Contains(Pub(_evil), map.Values);
        Assert.Empty(CloudConfig.ParseKeys(""));
    }

    // ══ ۳) تنظیماتِ کپی‌شده روی کامپیوترِ دیگر ═════════════════════════════

    /// <summary>
    /// ⛔ همان <c>settings.json</c> روی کامپیوترِ دیگر مجوزش را با خودش
    /// نمی‌برد — و ⚠️ هیچ چیزی هم پاک نمی‌شود.
    /// </summary>
    [Fact]
    public void AsareAngoshteKampyuter_JurNabashad_MojavezPazirofteNemishavad()
    {
        CloudConfig.MachineIdOverride = () => "machine-A";
        try
        {
            var (_, f, good) = Bound();
            Assert.True(CloudConfig.RecordMachine(f));
            Assert.False(CloudConfig.RecordMachine(f));          // دوباره بازنویسی نمی‌شود
            Assert.True(LicenseGuard.CheckStored(f).Valid);

            CloudConfig.MachineIdOverride = () => "machine-B";   // همان فایل، کامپیوترِ دیگر
            var moved = LicenseGuard.CheckStored(f);
            Assert.False(moved.Valid);
            Assert.False(moved.SignatureOk);                      // ⇒ ارفاق هم نه
            Assert.Equal(CloudConfig.MachineMovedWhy, moved.Reason);
            Assert.False(CloudConfig.RecordMachine(f));          // خودش را «درست» نمی‌کند
            Assert.Equal(good, f.CloudLicense);                   // ⛔ چیزی پاک نشد

            //  سیستمی که شناسه نمی‌دهد اصلاً سنجیده نمی‌شود
            CloudConfig.MachineIdOverride = () => "";
            Assert.True(LicenseGuard.CheckStored(f).Valid);
        }
        finally { CloudConfig.MachineIdOverride = null; }
    }

    // ══ ۴) کفِ ساعت ═══════════════════════════════════════════════════════

    /// <summary>
    /// ساعتِ کامپیوتر یک بار اشتباهاً یک ماه جلو رفته بود و کف دنبالش رفت؛
    /// کاربر ساعت را درست کرد. مجوزِ تازهٔ سرور (با <c>iat</c>ِ امضاشده) کف را
    /// پایین می‌آورد — وگرنه مجوزِ مشتریِ پول‌داده زودتر بسته می‌شد.
    /// </summary>
    [Fact]
    public async Task KafeSaat_BaMojavezeTazeyeSarvar_DorostMishavad()
    {
        var (link, f, _) = Bound();
        f.ClockFloorMs = Now + 30L * 24 * 3600 * 1000;
        f.Save();
        Assert.False(link.Verify().Valid);                        // با کفِ اشتباه، منقضی

        var iat = Now;
        var fresh = Sign(_real, CloudConfig.DeviceUid(f), iat, new[] { "cloudbackup" });
        Serve((path, _) => path switch
        {
            "/api/pump/device/me" => Json(HttpStatusCode.OK, DeviceMe()),
            "/api/pump/device/license" => Json(HttpStatusCode.OK, new { license = fresh }),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        var r = await link.RefreshAsync();
        Assert.True(r.Ok, r.Why);
        Assert.Equal(iat, AppSettings.Load().ClockFloorMs);
        Assert.True(link.Verify().Valid);
    }

    /// <summary>
    /// ⚠️ ولی مجوزِ <b>ذخیره‌شده</b> (نه تازه) کف را فقط بالا می‌برد — وگرنه
    /// یک مجوزِ کهنه راهِ عقب بردنِ کف بود.
    /// </summary>
    [Fact]
    public void KafeSaat_BaMojavezeKohne_PayinNemiayad()
    {
        var f = new AppSettings { ClockFloorMs = Now + 30L * 24 * 3600 * 1000 };
        LicenseClock.Anchor(f, Now, fresh: false);
        Assert.True(f.ClockFloorMs > Now + 29L * 24 * 3600 * 1000);

        var g = new AppSettings { ClockFloorMs = 0 };
        var iat = Now;
        LicenseClock.Anchor(g, iat, fresh: false);
        Assert.Equal(iat, g.ClockFloorMs);
    }

    // ══ ۵) دستگاهی که از پمپ جدا شده ═══════════════════════════════════════

    /// <summary>
    /// سرور صریح گفت این دستگاه ثبت نیست ⇒ فقط توکنِ دستگاه و مجوز پاک
    /// می‌شوند و کاربر دلیلش را می‌بیند. ⛔ شناسهٔ پمپ و کلیدِ قفل‌شده
    /// دست نمی‌خورند.
    /// </summary>
    [Fact]
    public async Task DastgaheJodaShode_FaghatToken_VaMojavez_PakMishavand()
    {
        var (link, _, _) = Bound();
        Serve((_, _) => Json(HttpStatusCode.Unauthorized,
            """{"error":{"code":"device_not_registered","message":"این دستگاه ثبت نشده است"}}"""));

        await link.RefreshAsync();

        var disk = AppSettings.Load();
        Assert.Equal("", disk.CloudDeviceToken);
        Assert.Equal("", disk.CloudLicense);
        Assert.Equal(Station, disk.CloudStationId);
        Assert.Equal(Pub(_real), disk.CloudPublicKey);
        Assert.Contains("جدا شده", CloudLink.DeviceDetachedWhy);
    }

    /// <summary>⛔ و خطای شبکه یا ۵۰۰ یا ۴۰۱ِ بی‌کد هیچ چیزی را پاک نمی‌کند.</summary>
    [Fact]
    public async Task DastgaheJodaShode_BaKhataieShabake_HichChiziPakNemishavad()
    {
        var (link, _, good) = Bound();

        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("no network");
        await link.RefreshAsync();
        Serve((_, _) => Json(HttpStatusCode.InternalServerError, """{"error":{"code":"internal","message":"x"}}"""));
        await link.RefreshAsync();
        Serve((_, _) => Json(HttpStatusCode.Unauthorized, """{"error":{"code":"invalid_token","message":"x"}}"""));
        await link.RefreshAsync();

        var disk = AppSettings.Load();
        Assert.Equal("dev-1", disk.CloudDeviceToken);
        Assert.Equal(good, disk.CloudLicense);
    }

    // ══ ۶) حلقهٔ پس‌زمینه دستگاهِ بی‌حساب را هم تازه می‌کند ════════════════

    [Fact]
    public void Halghe_DastgaheBiHesab_RaHam_TazeMikonad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var src = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Services", "StationPublisher.cs"));
        var i = src.IndexOf("private static async Task CloudKeepAsync", StringComparison.Ordinal);
        var end = src.IndexOf("public static string HashOf", i, StringComparison.Ordinal);
        var body = src[i..end];
        var afterAccount = body[body.IndexOf("return;", StringComparison.Ordinal)..];
        Assert.Contains("CloudDeviceToken", afterAccount);
        Assert.Contains("KeepLicenseFreshAsync", afterAccount);
        Assert.Contains("LicenseClock.Tick", body);
    }
}
