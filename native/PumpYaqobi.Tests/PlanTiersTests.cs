using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ سه پلن، از نو (۱۴۰۵/۰۷/۲۰) ══════════════════════════════════════════════
///
/// «استاندارد: کیو‌آر، اپِ گوشی، بات قفل؛ مفاد/ضرر تار؛ بکاپ روی سرور نه؛
/// اطلاعاتِ تانک به سرور نیاید و اصلاً به سرور وصل حتی نشه — فقط اشتراک.
/// وی‌آی‌پی همه‌چیز. دائمی همه‌چیز، ولی خدماتِ سرور سالِ اول رایگان و بعد
/// با تمدیدِ مدیر.» — با مجوزِ <b>واقعیِ امضاشده</b>، همان شکلی که سرورِ حساب
/// (‎lib/pump-services.js‎ · ‎042_pump_plan_tiers.sql‎) می‌سازد.
/// </summary>
public class PlanTiersTests : IDisposable
{
    private const string Device = "pc-tier-1";
    private const string Station = "stn_tier_1";
    private readonly long _now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    //  همان فهرست‌هایی که سرورِ حساب در مجوز می‌گذارد (دفترِ همیشه‌رایگان کنارش)
    private static readonly string[] Free = { "settings", "debtors", "safe", "expense", "storage" };
    private static readonly string[] Std = Free.Concat(new[] { "dashboard", "history", "multi_device" }).ToArray();
    private static readonly string[] Vip = Free.Concat(new[]
        { "dashboard", "kar_app", "bot", "messenger", "cloud", "cloudbackup", "profit", "history", "multi_device" }).ToArray();
    private static readonly string[] PermLapsed = Free.Concat(new[] { "dashboard", "profit", "history", "multi_device" }).ToArray();

    public PlanTiersTests()
    {
        Entitlements.Unlocked = false;
        Entitlements.Now = () => _now;
    }
    public void Dispose() => Entitlements.Now = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private AppSettings Licensed(string[]? feat, long svcEnds = 0, string plan = "std")
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        static string B64(byte[] b) => Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = "tohid-license-server", ["aud"] = "tohid-pump-app", ["duid"] = Device, ["stn"] = Station,
            ["iat"] = _now, ["nbf"] = _now - 60_000, ["exp"] = _now + 10L * 86_400_000,
            ["sub_ends"] = _now + 300L * 86_400_000, ["core"] = new[] { "settings", "debtors" },
            ["plan"] = plan, ["plan_title"] = plan, ["svc_ends"] = svcEnds,
        };
        if (feat is not null) payload["feat"] = feat;
        var head = B64(Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"TLIC"}"""));
        var body = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var sig = key.SignData(Encoding.UTF8.GetBytes($"{head}.{body}"), HashAlgorithmName.SHA256,
                               DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new AppSettings
        {
            CloudDeviceUid = Device, CloudDeviceToken = "dev-token", CloudStationId = Station,
            CloudPublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            CloudLicense = $"{head}.{body}.{B64(sig)}",
        };
    }

    [Fact]
    public void Standard_HichKhadamateServer_MofadTar_DashboardVaTarikhcheBaz()
    {
        var st = Entitlements.State(Licensed(Std));
        Assert.True(st.Open);
        foreach (var f in new[] { Entitlements.Online, Entitlements.Kar, Entitlements.QrLive, Entitlements.CloudBackup })
        {
            Assert.False(st.Allows(f), f);
            Assert.True(st.Denies(f), f + " — پلن صریحاً ندارد ⇒ هیچ درخواستی نرود");
        }
        Assert.False(st.Allows(Entitlements.Profit));           // ⇒ مفاد تار (‎ProfitSectionViewModel.PlanVeiled‎)
        Assert.True(st.Denies(Entitlements.Profit));            // ⇒ همه‌جا تار (‎ProfitVeil‎)
        Assert.True(st.Allows(Entitlements.Dashboard));
        Assert.True(st.Allows(Entitlements.History));
        Assert.True(st.Allows(Entitlements.Support));           // پشتیبانی هرگز
    }

    [Fact]
    public void Vip_HameChiz()
    {
        var st = Entitlements.State(Licensed(Vip, plan: "vip"));
        foreach (var f in Entitlements.Paid) Assert.True(st.Allows(f), f);
        Assert.False(st.Denies(Entitlements.Online));
    }

    [Fact]
    public void Daemi_PasAzPayaneKhadamat_FaghatServerMiravad()
    {
        //  سرورِ حساب پس از پایانِ خدمات کلیدهای خدماتِ سرور را از فهرست برمی‌دارد
        var st = Entitlements.State(Licensed(PermLapsed, svcEnds: _now - 86_400_000, plan: "perm"));
        Assert.True(st.Denies(Entitlements.Online));
        Assert.False(st.Allows(Entitlements.Kar));
        Assert.False(st.Allows(Entitlements.QrLive));
        Assert.False(st.Allows(Entitlements.CloudBackup));
        foreach (var f in new[] { Entitlements.Profit, Entitlements.Dashboard, Entitlements.History })
            Assert.True(st.Allows(f), f + " — دائمی بی محدودیت است");
    }

    [Theory]
    [InlineData("kar_app")] [InlineData("bot")] [InlineData("messenger")] [InlineData("cloud")] [InlineData("cloudbackup")]
    public void HarKelideKhadamat_EtesalRaBazMikonad(string key)
    {
        var st = Entitlements.State(Licensed(Free.Append(key).ToArray()));
        Assert.True(st.Allows(Entitlements.Online));
        Assert.False(st.Denies(Entitlements.Online));
    }

    [Fact]
    public void BiMojavvez_YaNasleAval_EtesalRaNemibandad_SarvarTasmimMigirad()
    {
        //  ⛔ PlanDenies فقط وقتی «نه» می‌گوید که فهرست آمده و ندارد
        Assert.False(Entitlements.State(new AppSettings()).Denies(Entitlements.Online));
        Assert.False(Entitlements.State(Licensed(null)).Denies(Entitlements.Online));
    }

    [Fact]
    public void KoodeBiInterneteStandard_HamanMarz()
    {
        var f = OfflineKey.FeaturesOf("std");
        Assert.DoesNotContain(f, k => EntitlementState.OnlineKeys.Contains(k));
        Assert.Contains("dashboard", f);
        Assert.Contains("history", f);
        Assert.DoesNotContain("profit", f);
    }

    [Fact]
    public void KelidhayeKhadamat_HamanServerAst()
    {
        //  مو‌به‌مو ‎ONLINE_KEYS‎ِ ‎lib/pump-services.js‎ در ریپوی ‎shop‎
        Assert.Equal(new[] { "kar_app", "bot", "messenger", "cloud", "cloudbackup" }, EntitlementState.OnlineKeys);
    }

    [Fact]
    public void ProfileKhadamat_DaemiTaKey_StandardNist_VipHichi()
    {
        var day = 86_400_000L;
        LicenseCheck C(string[] feat, long svc) =>
            new(true, "", feat, Array.Empty<string>(), _now + 300 * day, _now + 10 * day, "x",
                HasFeatureList: true, SignatureOk: true, ServicesEndsAt: svc);
        Assert.StartsWith("تا ", AccountSectionViewModel.ServicesLine(C(Vip, _now + 30 * day), _now));
        Assert.Contains("تمدید", AccountSectionViewModel.ServicesLine(C(PermLapsed, _now - day), _now));
        Assert.Contains("در پلنِ شما نیست", AccountSectionViewModel.ServicesLine(C(Std, 0), _now));
        Assert.Equal("", AccountSectionViewModel.ServicesLine(C(Vip, 0), _now));
        Assert.Equal("", AccountSectionViewModel.ServicesLine(LicenseCheck.Fail("x"), _now));
    }
}

/// <summary>
/// ⛔ رفتاری: پلنِ بی خدماتِ سرور هیچ درخواستِ همگام‌سازی نمی‌زند — نه «سرور
/// ردش کرد»، بلکه «اصلاً نرفت». با وی‌آی‌پی همان موتور واقعاً می‌فرستد (دندان).
/// </summary>
[Collection(AppHostCollection.Name)]
public class PlanTiersSyncTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pump-tier-{Guid.NewGuid():N}");
    private readonly string? _was = AppSettings.DirOverride;
    private int _requests;

    public PlanTiersSyncTests()
    {
        Directory.CreateDirectory(_dir);
        Entitlements.Unlocked = false;
    }
    public void Dispose()
    {
        CloudLink.TestTransport = null;
        AppSettings.DirOverride = _was;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private Task<HttpResponseMessage> Server(HttpRequestMessage req, CancellationToken ct)
    {
        if (req.RequestUri!.AbsolutePath.StartsWith("/api/sync/v1/")) Interlocked.Increment(ref _requests);
        var body = req.RequestUri.AbsolutePath.EndsWith("/push")
            ? """{"ok":true,"results":[],"applied":0,"cursor":0,"schema_version":1}"""
            : """{"ok":true,"ops":[],"cursor":0,"has_more":false}""";
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private (AppHost host, SyncEngine engine) Pc(string[] feat, string plan)
    {
        var d = Path.Combine(_dir, plan);
        Directory.CreateDirectory(d);
        AppSettings.DirOverride = d;
        var host = new AppHost(Path.Combine(d, "pump.db"));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        static string B64(byte[] b) => Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = "tohid-license-server", ["aud"] = "tohid-pump-app", ["duid"] = "pc-sync-tier", ["stn"] = "stn_sync_tier",
            ["iat"] = now, ["nbf"] = now - 60_000, ["exp"] = now + 10L * 86_400_000, ["sub_ends"] = now + 300L * 86_400_000,
            ["core"] = new[] { "settings", "debtors" }, ["plan"] = plan, ["plan_title"] = plan, ["feat"] = feat,
        };
        var head = B64(Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"TLIC"}"""));
        var bodyB = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var sig = key.SignData(Encoding.UTF8.GetBytes($"{head}.{bodyB}"), HashAlgorithmName.SHA256,
                               DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var f = AppSettings.Load();
        f.CloudDeviceUid = "pc-sync-tier"; f.CloudDeviceToken = "pd_tiertiertiertiertier"; f.CloudUserId = "u-tier";
        f.CloudStationId = "stn_sync_tier"; f.CloudPublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        f.CloudLicense = $"{head}.{bodyB}.{B64(sig)}";
        f.Save();
        using (var db = host.Db.Create()) { db.Debtors.Add(new PumpYaqobi.Domain.Entities.Debtor { Name = "کریم" }); db.SaveChanges(); }
        return (host, new SyncEngine(host) { RunWhenDisabled = true });
    }

    [Fact]
    public async Task Standard_HichDarkhastiNemiravad_VipMiravad()
    {
        CloudLink.TestTransport = Server;
        var (_, std) = Pc(new[] { "settings", "debtors", "dashboard", "history" }, "std");
        for (var i = 0; i < 60; i++) await std.SyncNowAsync();
        Assert.Equal(0, _requests);
        Assert.Contains("در پلنِ شما نیست", std.Reason);

        //  دندان: همان موتور با وی‌آی‌پی واقعاً به سرور می‌رود
        var (_, vip) = Pc(new[] { "settings", "debtors", "cloud", "kar_app" }, "vip");
        for (var i = 0; i < 200 && _requests == 0; i++) await vip.SyncNowAsync();
        Assert.True(_requests > 0, "وی‌آی‌پی باید همگام شود — " + vip.Reason + " · " + vip.Light);
    }
}

/// <summary>⛔ پلنِ استاندارد مفاد را همه‌جا تار می‌کند — و هیچ رمزی بازش نمی‌کند.</summary>
public class PlanTiersVeilTests
{
    [Fact]
    public void ProfitVeil_AzPlanDenies_Mikhanad()
    {
        var src = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "PumpYaqobi.Shell", "Services", "ProfitVeil.cs"));
        Assert.Contains("Entitlements.PlanDenies(Entitlements.Profit)", src);
        var vm = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "PumpYaqobi.App", "ViewModels", "Sections", "ProfitSectionViewModel.cs"));
        Assert.Contains("if (PlanVeiled) { Entitlements.Gate(_host, Entitlements.Profit); return; }", vm);
    }
}
