using PumpYaqobi.Application.Localization;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ کدِ اشتراکِ آفلاین — سه پلن، بسته به یک کامپیوتر ════════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «سه نوع کد برای اشتراک‌ها و یک گیرنده برای
/// برنامه تا کد رو بزنم درجا قفل‌ها باز بشه و بدون نت هم اشتراک داده بشه.»
///
/// ⛔ کدهای این آزمون را **خودِ کدِ جاوااسکریپتِ سرورِ حساب** ساخته
/// (<c>shop/server/src/lib/offline-codes.js</c> با یک جفت‌کلیدِ آزمون) — نه
/// این طرف. پس اگر روزی شکلِ دودویی در یکی عوض شد و در دیگری نه، همین‌جا
/// سرخ می‌شود. کلیدِ خصوصیِ آن جفت هیچ‌جا نیست؛ فقط کلیدِ عمومی.
/// </summary>
[Collection(AppHostCollection.Name)]
public class OfflineKeyTests : IDisposable
{
    //  ── بردارِ آزمون: خروجیِ offline-codes.js ─────────────────────────────
    private const string Spki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEaoYhrwGgI8x+Y2qYsNFpCgaOVBiWByTLhPznMNx8bREhK8rNLHpMkUt/RdAofqIzD1SE6sAKNQHZu1sc7zrHIQ==";
    private const string Kid = "72c7eee9c06d50eb";
    private const string MachineGuid = "TEST-MACHINE-GUID-0001";
    private const string Fingerprint = "m-5b7a1484e2537b07afeb1da85fcc97d6";
    private const string Computer = "C6PJ-6CTR-HPA7-DF35";
    private const string OtherComputer = "77R5-XG20-9SNG-6SWV";
    private const string Vip = "04163-B936D-C8V53-PQHGA-38N3M-JJTCT-NH7E0-7TZ9Y-G1SCF-VQ9R1-PN1TT-F3TYR-X3CZJ-NZC5P-S1CN0-YWJ9V-4A746-FA4PV-BK9V6-MRWNB-AH203-1Y07G-2E0NA-88YS4-GDGXK-QQR5G-JSADF-YE909-T6322-E38G6-8QZHT-QG";
    private const string Std = "040P3-B936D-C8V53-PQHGB-3CNKP-JTVCT-NH7E0-6NP68-G1SCF-VQ9R1-PN1TX-CGQFZ-34D7J-78CBR-XR4BF-PRRWJ-XDQBS-BRQ0Y-K5GGD-JVJWV-ZNMW1-4CNP6-XQ9KJ-3YV8Y-HKSX4-JE4NG-71S99-NPP45-26JWQ-KVC0T-GM616-H6";
    private const string Perm = "041P3-B936D-C8V53-PQHGC-3GP3R-K2WCT-NH7E0-FZZZZ-ZXSCF-VQ9R1-PN1TR-FF0DE-WN3T3-AQA1Q-XWGWF-ACPNR-J98QF-E4GE9-QMWQS-C91MV-89Y1B-RXKXF-Y6BV0-XA21W-GC46R-3KJNC-QP2Q0-JFDB2-SMXY2-GW7JY-7FWKE-R0";
    private const string Expired = "04163-B936D-C8V53-PQHGD-3MPKT-KAXCT-NH7E0-6NCMD-01SCF-VQ9R1-PN1TZ-QET99-8RAK9-6D965-0VH9C-3KZ36-A7AEK-5JQSK-MEQS6-3VFZ0-W6EJ3-0Q8C3-EATDM-7E9SV-Y12KT-TF2WK-A3HF4-TCERR-2H7E5-PZAQ9-7AFTR-VA";
    private const long IssuedMs = 1790000000000;
    private const long Day = 86_400_000;

    private static readonly IReadOnlyDictionary<string, string> Keys =
        new Dictionary<string, string> { [Kid] = Spki };

    private readonly string _dir;
    private readonly string? _was;

    public OfflineKeyTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-offline-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        CloudConfig.MachineIdOverride = () => MachineGuid;
        CloudConfig.TestOfflineKeys = new Dictionary<string, string> { ["#1"] = Spki };
        CloudConfig.TestLicenseKeys = new Dictionary<string, string>();
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _was;
        CloudConfig.MachineIdOverride = null;
        CloudConfig.TestOfflineKeys = null;
        CloudConfig.TestLicenseKeys = null;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void KodeKamputer_HamanKeSarvarMisazad()
    {
        Assert.Equal(Fingerprint, CloudConfig.MachineFingerprint());
        Assert.Equal(Computer, OfflineKey.ComputerCode());
        Assert.Matches("^[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}$", Computer);
        Assert.Equal("", OfflineKey.ComputerCode(""));
        Assert.Equal(Kid, OfflineKey.KidOf(Spki));
    }

    [Theory]
    [InlineData(Vip, "vip", false, 3650)]
    [InlineData(Std, "std", false, 30)]
    [InlineData(Perm, "perm", true, 0)]
    public void SePlan_BiInternet_Pazirofteh_Mishavand(string code, string plan, bool permanent, int days)
    {
        var now = IssuedMs + Day;
        var c = OfflineKey.Check(code, Fingerprint, now, Keys);
        Assert.True(c.Valid, c.Why);
        Assert.Equal(plan, c.Plan);
        Assert.Equal(permanent, c.Permanent);
        Assert.Equal(IssuedMs, c.IssuedAt);
        if (!permanent) Assert.Equal(IssuedMs + days * Day, c.EndsAt);
        Assert.NotEmpty(c.Features);
        if (plan == "std") Assert.DoesNotContain("kar_app", c.Features);
        else Assert.Contains("kar_app", c.Features);
    }

    [Fact]
    public void KodBaFaseleVaHorufeKuchakVaRaghameFarsi_HamPazirofteh()
    {
        var messy = "  " + Vip.ToLowerInvariant().Replace("-", " \n") + "  ";
        Assert.True(OfflineKey.Check(messy, Fingerprint, IssuedMs + Day, Keys).Valid);
        //  فایلِ ‎.pumpkey‎ — فقط `code` خوانده می‌شود
        var file = "{\"format\":\"pumpyaqobi-offline-key\",\"plan\":\"perm\",\"code\":\"" + Std + "\"}";
        Assert.Equal(Std, OfflineKey.Extract(file));
        Assert.Equal("std", OfflineKey.Check(OfflineKey.Extract(file), Fingerprint, IssuedMs + Day, Keys).Plan);
    }

    [Fact]
    public void KompyutereDigar_KodeDastkari_KelideDigar_HameRad()
    {
        //  همان کد روی کامپیوترِ دیگر
        var other = "m-" + new string('9', 32);
        var c = OfflineKey.Check(Vip, other, IssuedMs + Day, Keys);
        Assert.False(c.Valid);
        Assert.Contains("کامپیوترِ دیگری", c.Why);

        //  یک نویسه عوض ⇒ امضا نمی‌خورد (یا خوانده نمی‌شود)
        var chars = OfflineKey.Clean(Vip).ToCharArray();
        chars[3] = chars[3] == 'A' ? 'B' : 'A';
        Assert.False(OfflineKey.Check(new string(chars), Fingerprint, IssuedMs + Day, Keys).Valid);

        //  کلیدِ دیگری داخلِ برنامه ⇒ این کد پذیرفته نیست
        var foreign = new Dictionary<string, string> { ["0123456789abcdef"] = Spki };
        Assert.False(OfflineKey.Check(Vip, Fingerprint, IssuedMs + Day, foreign).Valid);
        //  بی هیچ کلیدی ⇒ راست می‌گوید چرا
        Assert.Contains("کلیدِ سرورِ حساب", OfflineKey.Check(Vip, Fingerprint, IssuedMs + Day,
            new Dictionary<string, string>()).Why);

        Assert.False(OfflineKey.Check("ABCDE-FGHJK", Fingerprint, IssuedMs, Keys).Valid);
        Assert.False(OfflineKey.Check("", Fingerprint, IssuedMs, Keys).Genuine);
    }

    [Fact]
    public void MohlatTamamShod_BastehMishavad_DaemiHargez()
    {
        var c = OfflineKey.Check(Expired, Fingerprint, IssuedMs + 2 * Day, Keys);
        Assert.False(c.Valid);
        Assert.True(c.Expired);
        Assert.True(c.Genuine);
        Assert.True(OfflineKey.Check(Perm, Fingerprint, IssuedMs + 40L * 365 * Day, Keys).Valid);
        Assert.False(OfflineKey.Check(Std, Fingerprint, IssuedMs + 31 * Day, Keys).Valid);
    }

    [Fact]
    public void BiTokenVaBiInternet_GhoflhaBazMishavand()
    {
        //  کامپیوترِ هرگز‌آنلاین‌نشده: نه توکنِ دستگاه، نه مجوز
        var f = new AppSettings();
        var before = Entitlements.State(f);
        Assert.True(before.NotActivated);
        Assert.False(before.Open);

        var r = OfflineKey.Apply(f, Vip);
        Assert.True(r.Valid, r.Why);
        var after = Entitlements.State(AppSettings.Load());
        Assert.False(after.NotActivated);
        Assert.True(after.Open);
        Assert.True(after.Listed);
        Assert.Contains("kar_app", after.Features);
        Assert.Contains("dashboard", after.Features);
        Assert.Contains(CodeNames.OfflineKey, after.PlanTitle);
        Assert.True(after.EntitledUntil > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        //  برداشتن ⇒ همان حالِ پیشین
        OfflineKey.Remove(AppSettings.Load());
        Assert.True(Entitlements.State(AppSettings.Load()).NotActivated);
    }

    [Fact]
    public void KodeGhalat_HichChiziRaAvazNemikonad()
    {
        var f = new AppSettings();
        Assert.True(OfflineKey.Apply(f, Perm).Valid);
        //  کدِ خراب یا کدِ منقضی کدِ درستِ قبلی را پاک نمی‌کند
        Assert.False(OfflineKey.Apply(AppSettings.Load(), "ABCDE").Valid);
        Assert.False(OfflineKey.Apply(AppSettings.Load(), Expired).Valid);
        Assert.Equal(OfflineKey.Pretty(Perm), AppSettings.Load().OfflineCode);
        Assert.Equal("perm", OfflineKey.Stored(AppSettings.Load(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Plan);
    }

    [Fact]
    public void KopiyeTanzimat_BeKampyutereDigar_HichNemibarad()
    {
        var f = new AppSettings();
        Assert.True(OfflineKey.Apply(f, Vip).Valid);
        //  همان settings.json روی کامپیوترِ دیگر
        CloudConfig.MachineIdOverride = () => "SOME-OTHER-PC";
        var st = Entitlements.State(AppSettings.Load());
        Assert.False(st.Open);
        Assert.True(st.NotActivated);
    }

    [Fact]
    public void KelideRooyeDisk_JayeKelideBarnamehRaNemigirad()
    {
        //  کسی کلیدِ خودش را در settings.json نوشته: با کلیدِ داخلِ برنامه اثری ندارد
        var f = new AppSettings { CloudPublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAAAA" };
        var keys = OfflineKey.TrustedKeys(f);
        Assert.Single(keys);
        Assert.Equal(Spki, keys[Kid]);
        //  و بی کلیدِ داخلِ برنامه، همان TOFU
        CloudConfig.TestOfflineKeys = new Dictionary<string, string>();
        var tofu = OfflineKey.TrustedKeys(new AppSettings { CloudPublicKey = Spki });
        Assert.Equal(Spki, tofu[Kid]);
    }


    private static readonly string Native =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    /// <summary>
    /// ⛔ کامپیوترِ هرگز‌آنلاین‌نشده هیچ کلیدِ TOFUی ندارد؛ پس ساختِ CI باید
    /// کلیدِ سرورِ حساب را داخلِ خودِ برنامه بگذارد — وگرنه همهٔ کدهای
    /// آفلاین روی همان کامپیوترهایی که برایشان ساخته شده‌اند رد می‌شدند.
    /// </summary>
    [Fact]
    public void SakhteCI_KelideKodeAfline_Ra_DakheleBarname_Migozarad()
    {
        var wf = File.ReadAllText(Path.Combine(Native, "..", ".github", "workflows", "build-native.yml"));
        Assert.Contains("-p:OfflineKeys=", wf);
        Assert.Contains("/api/license/public-key", wf);
        Assert.Contains("vars.OFFLINE_PUBLIC_KEYS", wf);
        var csproj = File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", "PumpYaqobi.App.csproj"));
        Assert.Contains("<AssemblyMetadata Include=\"OfflineKeys\" Value=\"$(OfflineKeys)\" />", csproj);
        var cfg = File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", "Services", "CloudConfig.cs"));
        Assert.Contains("Metadata(\"OfflineKeys\")", cfg);
        //  ⛔ نشانیِ دریافتِ کلید همان نشانیِ قفل‌شدهٔ برنامه است، نه نشانیِ دیگری
        Assert.Contains("https://api.vill3n.top/api/license/public-key", wf);
    }

    /// <summary>برنامه آنلاین شد ⇒ کد به سرورِ حساب می‌رود — از هر دو راهِ حلقه.</summary>
    [Fact]
    public void HalgheyePasZamine_KodRa_BeSarvar_Mibarad()
    {
        var pub = File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", "Services", "StationPublisher.cs"));
        Assert.Contains("await cloud.RedeemOfflineAsync(ct);", pub);
        Assert.Contains("await device.RedeemOfflineAsync(ct);", pub);
        var link = File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", "Services", "CloudLink.cs"));
        Assert.Contains("\"/api/pump/device/offline-code\"", link);
        Assert.Contains("code == \"code_revoked\"", link);
    }

    [Fact]
    public void DarFayleKamel_NemiRavad()
    {
        Assert.DoesNotContain(nameof(AppSettings.OfflineCode), PortableSettings.Fields);
        Assert.DoesNotContain(nameof(AppSettings.OfflineCodeRedeemed), PortableSettings.Fields);
    }
}
