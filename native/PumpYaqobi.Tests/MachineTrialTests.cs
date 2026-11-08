using System.Globalization;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ دورهٔ آزمایشیِ یک‌ماهه، یک بار برای هر کامپیوتر (۱۴۰۵/۰۸/۱۷) ═════════════
/// «روی کامپیوترِ جدید بدونِ اینترنت یک ماه آزمایشی… و اگه یک بار گرفته بود بار
/// بعد قبول نشه.» تصمیم در <see cref="MachineTrial"/> و <see cref="AppLock.Decide"/>.
/// </summary>
[Collection(AppHostCollection.Name)]
public class MachineTrialTests : IDisposable
{
    private const long Day = 86_400_000L;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-mt-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _was = AppSettings.DirOverride;

    public MachineTrialTests()
    {
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        MachineTrial.TestVault = new();
    }

    public void Dispose()
    {
        MachineTrial.TestVault = null;
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static long Local(int y, int m, int d, int h, int min)
    {
        var dt = new PersianCalendar().ToDateTime(y, m, d, h, min, 0, 0);
        return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt)).ToUnixTimeMilliseconds();
    }

    [Fact]
    public void Payan_HamanRuzVaSaat_MaheBad()
    {
        Assert.Equal(Local(1405, 9, 17, 10, 30), MachineTrial.EndOf(Local(1405, 8, 17, 10, 30)));
        Assert.Equal(Local(1406, 1, 5, 8, 0), MachineTrial.EndOf(Local(1405, 12, 5, 8, 0)));
        Assert.Equal(0, MachineTrial.EndOf(0));
    }

    [Fact]
    public void BiHesab_AzHaminHala_YekBar()
    {
        var f = AppSettings.Load();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        MachineTrial.Observe(f, hasAccount: false, check: null, now);
        Assert.Equal(now, MachineTrial.Start(f));

        //  بارِ بعد (روزِ بعد) آغاز جلو نمی‌رود
        MachineTrial.Observe(f, hasAccount: false, check: null, now + Day);
        Assert.Equal(now, MachineTrial.Start(f));
        Assert.Equal(now, AppSettings.Load().MachineTrialStartMs);   // روی دیسک هم
    }

    [Fact]
    public void BaHesab_BiAzmayeshi_DoreAghazNemishavad()
    {
        var f = AppSettings.Load();
        MachineTrial.Observe(f, hasAccount: true, check: null, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Assert.Equal(0, MachineTrial.Start(f));
    }

    [Fact]
    public void DastBordanDarTanzimat_DoreRaJeloNamibarad()
    {
        var f = AppSettings.Load();
        var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 40 * Day;
        MachineTrial.Begin(f, start);
        f.MachineTrialStartMs = 0;                       // پاک کردنِ تنظیمات
        Assert.Equal(start, MachineTrial.Start(f));
        f.MachineTrialStartMs = start + 39 * Day;        // نوشتنِ تاریخِ دیرتر
        Assert.Equal(start, MachineTrial.Start(f));
        Assert.False(MachineTrial.Begin(f, start + 39 * Day));
        Assert.Equal(start, MachineTrial.Start(f));
    }

    [Fact]
    public void AzmayesheServer_AghazeDoreRaSabtMikonad()
    {
        var f = AppSettings.Load();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var subEnds = now + 20 * Day;
        var check = new LicenseCheck(true, "", Array.Empty<string>(), Array.Empty<string>(), subEnds, now + 9 * Day, "آزمایشی",
                                     SignatureOk: true, IssuedAt: now, Plan: "trial");
        MachineTrial.Observe(f, hasAccount: true, check, now);
        //  آغاز = یک ماه پیش از پایانِ آزمایشیِ سرور ⇒ پایانِ کامپیوتر = همان پایان
        Assert.Equal(subEnds, MachineTrial.Ends(f));
    }
}
