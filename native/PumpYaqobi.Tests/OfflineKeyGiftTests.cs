using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ 🎁 گیفت‌کارت (۱۴۰۵/۰۷/۲۶) ══════════════════════════════════════════════
///
/// «کد اشتراک بدم به طرف بدون این که نت وصل باشه یا کد برنامه شو بده شبیه
/// گیفت کارت باشه و یک بار برای هر برنامه اجرا بشه فقط مگه کد متفاوت زد دوباره.»
///
/// ⛔ کدها را خودِ <c>offline-codes.js</c>ِ سرورِ حساب ساخته (هشت بایتِ کامپیوترِ
/// همه صفر)، با جفت‌کلیدِ یک‌بارمصرفی که کلیدِ خصوصی‌اش دور ریخته شد.
/// </summary>
[Collection(AppHostCollection.Name)]
public class OfflineKeyGiftTests : IDisposable
{
    private const string Spki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEYccfiLcgjMdcZH3W0hE7g+36rea8YSLdaaxN8rkfQzJwMBNUwngnpQwT/H6Woy8YQEIGqff2e4jJaemOYikZmA==";
    private const long IssuedDayMs = 1789948800000;
    private const long Day = 86_400_000;
    //  وی‌آی‌پی ۳۰ روز
    private const string Vip30 = "08100-00000-00000-00000-000H4-8SM80-Z20G0-1B0EN-6D4JV-5MG05-RSVVZ-GJ9D7-TVM3Y-MEEM6-BDNV4-SGX6G-FVE9V-W9D62-694Q7-CVZZY-XP7NX-3XZSK-0BGXF-082VJ-BF4JT-RF9FM-ANSG4-ZCG";
    //  دو گیفتِ دائمیِ جدا (سریالِ دیگر)
    private const string Perm1 = "081G0-00000-00000-00000-0051P-B1X80-Z2ZZZ-Z4KNN-12E1X-9HHQG-RBFHP-F6NP7-ZYJ45-E00YF-8BG91-5E27G-TH8QX-Z220T-KEVYG-NXF84-RDSZC-026XV-3KG1G-8G74V-6MTX8-SKCFZ-A557V-WKG";
    private const string Perm2 = "081G0-00000-00000-00000-0051P-B1XA0-Z2ZZZ-ZF0XY-XCEBE-XVD63-H45DT-K95E5-JC9A0-MHHP6-XWF5A-7KVH4-C6VCW-PHKF8-Z2XQB-WGRR6-PCXM8-B1R8M-EZBSW-G4XDX-4SXW5-6WX8G-MWTQ5-70M";

    private static readonly IReadOnlyDictionary<string, string> Keys =
        new Dictionary<string, string> { ["any"] = Spki };

    private readonly string _dir;
    private readonly string? _was;

    public OfflineKeyGiftTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-gift-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        CloudConfig.MachineIdOverride = () => "TEST-MACHINE-GIFT-0001";
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

    [Theory]
    [InlineData("m-5b7a1484e2537b07afeb1da85fcc97d6")]
    [InlineData("m-99999999999999999999999999999999")]
    public void HarKampyuteri_BiKodeBarnameh(string fingerprint)
    {
        var c = OfflineKey.Check(Vip30, fingerprint, IssuedDayMs + Day, Keys);
        Assert.True(c.Valid, c.Why);
        Assert.True(c.Gift);
        Assert.Equal("vip", c.Plan);
        Assert.Equal(IssuedDayMs + 30 * Day, c.EndsAt);
        Assert.True(OfflineKey.Check(Vip30, fingerprint, IssuedDayMs + 31 * Day, Keys).Expired);
    }

    [Fact]
    public void DastKari_Rad()
    {
        var raw = OfflineKey.Clean(Vip30).ToUpperInvariant().ToCharArray();
        raw[5] = raw[5] == 'Z' ? 'Y' : 'Z';
        Assert.False(OfflineKey.Check(new string(raw), "m-1", IssuedDayMs + Day, Keys).Valid);
    }

    [Fact]
    public void YekBarBarayeHarBarnameh_KodeDigarDobare()
    {
        Assert.True(OfflineKey.Apply(new AppSettings(), Perm1).Valid);
        OfflineKey.Remove(AppSettings.Load());
        var again = OfflineKey.Apply(AppSettings.Load(), Perm1);
        Assert.False(again.Valid);
        Assert.Contains("یک بار", again.Why);
        //  کدِ متفاوت ⇒ دوباره
        var other = OfflineKey.Apply(AppSettings.Load(), Perm2);
        Assert.True(other.Valid, other.Why);
        Assert.True(other.Gift);
    }
}
