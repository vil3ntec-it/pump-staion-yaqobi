using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ 🔢 کدِ ۸ رقمیِ روزدار (۱۴۰۵/۰۷/۲۶) ══════════════════════════════════════
///
/// «کدش خیلی بزرگ نباشه در حد شش رقمی یا هشت رقمی… مقدار روز که می‌خوام رو بزارم
/// و بعد با کد بدم به برنامه و دقیق کار کنه.»
///
/// ⛔ بردارها را خودِ <c>shop/server/src/lib/pump-short-codes.js</c> ساخته
/// (<c>make(30,5)</c> ⇒ 04718293 …) — همان‌ها در آزمونِ سرور هم هستند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class ShortCodeTests : IDisposable
{
    private const long Day = 86_400_000;
    private readonly string _dir;
    private readonly string? _was;

    public ShortCodeTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-short-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        CloudConfig.MachineIdOverride = () => "TEST-MACHINE-SHORT-0001";
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _was;
        CloudConfig.MachineIdOverride = null;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("56898027", 1, 0)]
    [InlineData("65615636", 7, 3)]
    [InlineData("04718293", 30, 5)]
    [InlineData("94096184", 30, 6)]
    [InlineData("90490609", 365, 31)]
    [InlineData("95971553", 90, 17)]
    public void HamanBordarhayeSarver(string code, int days, int serial)
    {
        Assert.Equal((days, serial), ShortCode.Open(code));
    }

    [Fact]
    public void RaghamFarsiVaKhatTire()
    {
        Assert.Equal("04718293", ShortCode.Digits("۰۴۷۱-۸۲۹۳"));
        Assert.Equal("04718293", ShortCode.Digits(" 0471 8293 "));
        Assert.Equal("", ShortCode.Digits("0471829"));
        Assert.Equal("", ShortCode.Digits("08163-B936D"));
    }

    [Fact]
    public void TasadofiTaghribanHichvaghtDorostNist()
    {
        var rnd = new Random(7);
        var ok = 0;
        for (var i = 0; i < 100_000; i++)
            if (ShortCode.Open(rnd.Next(100_000_000).ToString("D8")) is not null) ok++;
        Assert.True(ok < 40, ok + " کدِ تصادفیِ درست از صد هزار");
    }

    [Fact]
    public void MohlatAzRuzeZadan_YekBar_KodeDigarDobare()
    {
        var c = OfflineKey.Apply(new AppSettings(), "0471-8293");
        Assert.True(c.Valid, c.Why);
        Assert.True(c.Short);
        Assert.Equal("vip", c.Plan);
        Assert.Equal(30 * Day, c.EndsAt - c.IssuedAt);

        var f = AppSettings.Load();
        var stored = OfflineKey.Stored(f, LicenseClock.Now(f));
        Assert.True(stored.Valid, stored.Why);
        Assert.Equal(c.EndsAt, stored.EndsAt);
        Assert.True(OfflineKey.Stored(f, c.EndsAt + 1).Expired);

        //  همان کد دوباره ⇒ مهلت از نو نمی‌شود
        Assert.Equal(c.EndsAt, OfflineKey.Apply(AppSettings.Load(), "04718293").EndsAt);
        //  کدِ دیگر ⇒ می‌نشیند؛ کدِ قبلی دیگر نه
        Assert.True(OfflineKey.Apply(AppSettings.Load(), "94096184").Valid);
        var again = OfflineKey.Apply(AppSettings.Load(), "04718293");
        Assert.False(again.Valid);
        Assert.Contains("یک بار", again.Why);
    }

    [Fact]
    public void DastKariDarFayl_Rad()
    {
        Assert.True(OfflineKey.Apply(new AppSettings(), "65615636").Valid);
        var f = AppSettings.Load();
        //  عوض کردنِ زمانِ زدن در فایل ⇒ برچسب نمی‌خورد
        var at = f.OfflineCode.IndexOf('@');
        var hash = f.OfflineCode.IndexOf('#');
        var when = long.Parse(f.OfflineCode[(at + 1)..hash]);
        f.OfflineCode = f.OfflineCode[..(at + 1)] + (when + 300 * Day) + f.OfflineCode[hash..];
        Assert.False(OfflineKey.Stored(f, when).Genuine);
    }

    [Fact]
    public void PanjGhalat_YekSaatGhofl()
    {
        var f = new AppSettings();
        for (var i = 0; i < ShortCode.MaxFails; i++)
            Assert.Contains("درست نیست", OfflineKey.Apply(AppSettings.Load(), "12345678").Why);
        var locked = OfflineKey.Apply(AppSettings.Load(), "04718293");
        Assert.False(locked.Valid);
        Assert.Contains("دقیقهٔ دیگر", locked.Why);
        GC.KeepAlive(f);
    }
}
