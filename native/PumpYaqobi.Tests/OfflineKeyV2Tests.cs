using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ کدِ اشتراکِ بی‌اینترنت، نسخهٔ ۲ (۱۴۰۵/۰۷/۲۰) ══════════════════════════════
///
/// «اونی که الان داره خیلی بزرگ است… ببین نشه دور زد، حتمن از سرور باشه…
/// یک بار استفاده بشه و برای هر کامپیوتر و هر حساب متفاوت باشه.»
///
/// ⛔ کدهای این آزمون را <b>خودِ کدِ جاوااسکریپتِ سرورِ حساب</b> ساخته
/// (<c>offline-codes.js</c> ⇒ <c>body2</c> + <c>encode2</c>، با یک جفت‌کلیدِ
/// یک‌بارمصرف که کلیدِ خصوصی‌اش دور ریخته شد) — نه این طرف.
/// </summary>
[Collection(AppHostCollection.Name)]
public class OfflineKeyV2Tests : IDisposable
{
    private const string Spki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEoGvbjXcCZpEEWcuDCBH9xN5FX/zlspcf+Ns4awh2RZpx3kA6kZBfg+1CbMPaNqyyp1v9/BQnGrZgw5Spp2EZ6g==";
    private const string MachineGuid = "TEST-MACHINE-GUID-0001";
    private const string Fingerprint = "m-5b7a1484e2537b07afeb1da85fcc97d6";
    private const long IssuedDayMs = 1789948800000;
    private const long Day = 86_400_000;
    //  وی‌آی‌پی ۳۶۶ روز، هر حسابی
    private const string Vip = "08163-B936D-C8V53-P0000-000A1-C60T0-Z20N8-B2HQP-ZZ7WZ-TMPP9-1JKES-YYEMP-867SV-NFR1H-YHJ51-QC330-14Q4M-VECKP-Y5AZ6-SYRGP-4V4YN-BRGSW-R30YR-C3EVZ-YC137-ZA5G0-CG7VE-CQM";
    //  استاندارد ۳۱ روز، بسته به حسابِ «usr_owner_1»
    private const string Std = "080P3-B936D-C8V53-PCA7X-BHG10-81G80-Z20G0-Q6R35-8JQRP-R389J-YF1J1-V424E-ZJ0AN-BSCWA-1VQTC-Y1F7T-ZW6T5-HJAC6-XR688-PB282-WF01M-8KSAV-JG41C-HG60D-74STG-ZNFPW-33ATM-NXR";
    private const string Perm = "081P3-B936D-C8V53-P0000-006YN-PZEY0-Z2ZZZ-VPTAB-GQ1E8-CF8BE-AVXQS-F7YFV-T47FX-71RDZ-6809Z-9TXP0-EGQPN-A1JAB-Y2AFN-Y8BDE-3TRWD-F28VY-JCQEM-Q5A1V-2N9YH-4M61E-7Y6SC-0WG";
    //  وی‌آی‌پی، پایان دو روز پس از صدور
    private const string Expired = "08163-B936D-C8V53-P0000-006AZ-VR0T0-Z20FJ-1PQYB-WY485-HYKHH-YT3JM-XV27B-XCCF7-F45RJ-V7TC6-9BSNG-BDDCB-WZ0R4-9V6GF-BQPXA-CXDR3-YG38N-4JJ05-BQ1VF-QZ702-R9VFD-R8QMJ-J00";
    private const string Owner = "usr_owner_1";

    private static readonly IReadOnlyDictionary<string, string> Keys =
        new Dictionary<string, string> { ["any"] = Spki };

    private readonly string _dir;
    private readonly string? _was;

    public OfflineKeyV2Tests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-offline2-" + Guid.NewGuid().ToString("N")[..8]);
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
    public void Kootahtar_AzNoskheyeYek()
    {
        Assert.Equal(138, OfflineKey.Clean(Vip).Length);
        Assert.True(OfflineKey.Clean(Vip).Length < 157);
        Assert.Equal("628fd5c6", Convert.ToHexString(OfflineKey.AccountTag(Owner)).ToLowerInvariant());
    }

    [Theory]
    [InlineData(Vip, "vip", false, 366)]
    [InlineData(Perm, "perm", true, 0)]
    public void BiInternet_Pazirofteh(string code, string plan, bool permanent, int days)
    {
        var c = OfflineKey.Check(code, Fingerprint, IssuedDayMs + Day, Keys);
        Assert.True(c.Valid, c.Why);
        Assert.Equal(plan, c.Plan);
        Assert.Equal(permanent, c.Permanent);
        Assert.Equal(IssuedDayMs, c.IssuedAt);
        if (!permanent) Assert.Equal(IssuedDayMs + days * Day, c.EndsAt);
        Assert.Equal(8, c.Serial.Length);
    }

    [Fact]
    public void BasteBeHesab_HesabeDigar_YaBiHesab_Rad()
    {
        Assert.True(OfflineKey.Check(Std, Fingerprint, IssuedDayMs + Day, Keys, Owner).Valid);
        var other = OfflineKey.Check(Std, Fingerprint, IssuedDayMs + Day, Keys, "usr_someone_else");
        Assert.False(other.Valid);
        Assert.Contains("حسابِ دیگری", other.Why);
        var none = OfflineKey.Check(Std, Fingerprint, IssuedDayMs + Day, Keys, "");
        Assert.False(none.Valid);
        Assert.Contains("با همان حساب وارد شوید", none.Why);
        //  کدِ «هر حسابی» با هر حساب و بی حساب
        Assert.True(OfflineKey.Check(Vip, Fingerprint, IssuedDayMs + Day, Keys, "usr_x").Valid);
    }

    [Fact]
    public void KampyutereDigar_KodeDastkari_KelideDigar_Rad()
    {
        var c = OfflineKey.Check(Vip, "m-" + new string('9', 32), IssuedDayMs + Day, Keys);
        Assert.False(c.Valid);
        Assert.Contains("کامپیوترِ دیگری", c.Why);

        //  هر نویسه‌ای از بدنه عوض ⇒ امضا نمی‌خورد (پلن، روزِ پایان، حساب، کامپیوتر)
        var raw = OfflineKey.Clean(Vip).ToUpperInvariant();
        for (var i = 1; i < 36; i++)
        {
            var ch = raw.ToCharArray();
            ch[i] = ch[i] == 'Z' ? 'Y' : 'Z';
            Assert.False(OfflineKey.Check(new string(ch), Fingerprint, IssuedDayMs + Day, Keys).Valid, "نویسهٔ " + i);
        }
        //  کلیدِ دیگری داخلِ برنامه ⇒ رد؛ بی کلید ⇒ راست می‌گوید چرا
        using var other = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var foreign = new Dictionary<string, string> { ["x"] = Convert.ToBase64String(other.ExportSubjectPublicKeyInfo()) };
        Assert.False(OfflineKey.Check(Vip, Fingerprint, IssuedDayMs + Day, foreign).Valid);
        Assert.Contains("کلیدِ سرورِ حساب", OfflineKey.Check(Vip, Fingerprint, IssuedDayMs + Day,
            new Dictionary<string, string>()).Why);
    }

    [Fact]
    public void MohlatTamam_Daemi_Hargez()
    {
        var c = OfflineKey.Check(Expired, Fingerprint, IssuedDayMs + 3 * Day, Keys);
        Assert.False(c.Valid);
        Assert.True(c.Expired);
        Assert.True(OfflineKey.Check(Perm, Fingerprint, IssuedDayMs + 40L * 365 * Day, Keys).Valid);
    }

    [Fact]
    public void YekBarEstefade_BardashtehYaJaygozin_DobareNemiNeshinad()
    {
        //  ⚠️ «حالا»ی واقعی دیرتر از صدورِ بردار است، پس از کدهای بی‌مهلت/بلند استفاده می‌شود
        var f = new AppSettings();
        Assert.True(OfflineKey.Apply(f, Perm).Valid);
        //  همان کد دوباره ⇒ بی‌خطر
        Assert.True(OfflineKey.Apply(AppSettings.Load(), Perm).Valid);
        //  برداشتن ⇒ دیگر پذیرفته نیست
        OfflineKey.Remove(AppSettings.Load());
        var again = OfflineKey.Apply(AppSettings.Load(), Perm);
        Assert.False(again.Valid);
        Assert.Contains("یک بار", again.Why);
        Assert.Equal("", AppSettings.Load().OfflineCode);
    }

    [Fact]
    public void DarFayleKamel_NemiRavad()
    {
        Assert.DoesNotContain(nameof(AppSettings.OfflineCodesUsed), PortableSettings.Fields);
    }

    [Fact]
    public void NoskheyeYek_HamchenanKhandeMishavad()
    {
        //  کدِ نسخهٔ ۱ با کلیدِ دیگر رد می‌شود ولی «خوانده» می‌شود (پیامِ امضا، نه «خوانده نشد»)
        const string v1 = "04163-B936D-C8V53-PQHGA-38N3M-JJTCT-NH7E0-7TZ9Y-G1SCF-VQ9R1-PN1TT-F3TYR-X3CZJ-NZC5P-S1CN0-YWJ9V-4A746-FA4PV-BK9V6-MRWNB-AH203-1Y07G-2E0NA-88YS4-GDGXK-QQR5G-JSADF-YE909-T6322-E38G6-8QZHT-QG";
        Assert.DoesNotContain("خوانده نشد", OfflineKey.Check(v1, Fingerprint, IssuedDayMs, Keys).Why);
    }
}
