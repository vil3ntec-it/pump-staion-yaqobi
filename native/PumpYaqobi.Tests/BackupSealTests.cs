using System.Security.Cryptography;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بکاپِ بسته به پمپِ صاحبش (۱۴۰۵/۰۷/۲۰) ═══════════════════════════════════
/// «بکاپ روی کامپیوترِ جدید باز شود، حتی بی اشتراک؛ ولی حسابِ دوم و آزمایشیِ
/// دوباره نتواند — حتی آفلاین.»
/// </summary>
[Collection(AppHostCollection.Name)]
public class BackupSealTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-seal-" + Guid.NewGuid().ToString("N"));
    private readonly string? _was = AppSettings.DirOverride;
    private static readonly byte[] KeyA = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] KeyB = RandomNumberGenerator.GetBytes(32);

    public BackupSealTests()
    {
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = Path.Combine(_dir, "settings");
        Directory.CreateDirectory(AppSettings.DirOverride);
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string Plain(int bytes)
    {
        var p = Path.Combine(_dir, "plain-" + Guid.NewGuid().ToString("N")[..6] + ".db");
        File.WriteAllBytes(p, RandomNumberGenerator.GetBytes(bytes));
        return p;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(1024 * 1024)]
    [InlineData(2_600_000)]
    public void MohrVaBazKardan_HamanBaytha(int size)
    {
        var src = Plain(size);
        var sealedF = Path.Combine(_dir, "s.bin");
        BackupSeal.Seal(src, sealedF, "stn_A", KeyA);
        Assert.True(BackupSeal.IsSealed(sealedF));
        Assert.Equal("stn_A", BackupSeal.StationOf(sealedF));
        Assert.False(BackupSeal.IsSealed(src));
        var back = Path.Combine(_dir, "back.db");
        Assert.True(BackupSeal.Open(sealedF, back, KeyA));
        Assert.Equal(File.ReadAllBytes(src), File.ReadAllBytes(back));
    }

    [Fact]
    public void KelideDigar_DastKhorde_Boride_HamRad_VaHichFayliNemimanad()
    {
        var src = Plain(2_600_000);
        var sealedF = Path.Combine(_dir, "s.bin");
        BackupSeal.Seal(src, sealedF, "stn_A", KeyA);
        var dst = Path.Combine(_dir, "out.db");

        Assert.False(BackupSeal.Open(sealedF, dst, KeyB));                  // ⛔ کلیدِ پمپِ دیگر
        Assert.False(File.Exists(dst));

        var bytes = File.ReadAllBytes(sealedF);
        var tampered = Path.Combine(_dir, "t.bin");
        var t = (byte[])bytes.Clone(); t[t.Length / 2] ^= 1;
        File.WriteAllBytes(tampered, t);
        Assert.False(BackupSeal.Open(tampered, dst, KeyA));                 // ⛔ یک بیتِ دست‌خورده

        File.WriteAllBytes(tampered, bytes[..(bytes.Length - 40)]);
        Assert.False(BackupSeal.Open(tampered, dst, KeyA));                 // ⛔ تهِ بریده

        var renamed = (byte[])bytes.Clone();
        renamed[11] ^= 1;                                                   // ⛔ نامِ پمپ در سربرگ عوض شد
        File.WriteAllBytes(tampered, renamed);
        Assert.False(BackupSeal.Open(tampered, dst, KeyA));

        File.WriteAllBytes(tampered, bytes.Concat(new byte[] { 1, 2, 3 }).ToArray());
        Assert.False(BackupSeal.Open(tampered, dst, KeyA));                 // ⛔ دنباله پس از تکهٔ آخر
        Assert.False(File.Exists(dst));
    }

    private static void Settings(string station, params (string Station, byte[] Key)[] keys)
    {
        var s = AppSettings.Load();
        s.CloudStationId = station;
        s.CloudDeviceToken = "";       // بی اینترنت: هیچ کلیدی از سرور نمی‌آید
        s.BackupKeys = string.Join(';', keys.Select(k => k.Station + "=" + Convert.ToBase64String(k.Key)));
        s.Save();
    }

    private string SealedFor(string station, byte[] key)
    {
        var f = Path.Combine(_dir, "b-" + Guid.NewGuid().ToString("N")[..6] + ".pumpyaqobi");
        BackupSeal.Seal(Plain(5000), f, station, key);
        return f;
    }

    [Fact]
    public async Task HamanPomp_Baz_HattaAfline()
    {
        Settings("stn_A", ("stn_A", KeyA));
        var r = await BackupKeys.OpenForRestoreAsync(SealedFor("stn_A", KeyA), _dir);
        Assert.True(r.Ok, r.Why);
        Assert.True(r.OwnPump);
        Assert.True(r.Temp);
        Assert.False(BackupSeal.IsSealed(r.Path));
    }

    [Fact]
    public async Task HesabeDovvom_AzmayesheDobare_HarGez()
    {
        //  ⛔ همان کامپیوتر، حسابِ تازه (پمپِ تازه) — کلیدِ پمپِ قبلی هنوز روی دیسک است
        Settings("stn_NEW", ("stn_A", KeyA), ("stn_NEW", KeyB));
        var r = await BackupKeys.OpenForRestoreAsync(SealedFor("stn_A", KeyA), _dir);
        Assert.False(r.Ok);
        Assert.Contains("حسابِ دیگری", r.Why);
        //  و کلیدِ پمپِ قبلی از دیسکِ همین کامپیوتر هم رفت
        Assert.Null(BackupKeys.Cached(AppSettings.Load(), "stn_A"));
        Assert.NotNull(BackupKeys.Cached(AppSettings.Load(), "stn_NEW"));
    }

    [Fact]
    public async Task BiHesab_YaBiKelid_BazNemishavad_VaMigooyadChera()
    {
        Settings("");
        var f = SealedFor("stn_A", KeyA);
        var none = await BackupKeys.OpenForRestoreAsync(f, _dir);
        Assert.False(none.Ok);
        Assert.Contains("وارد شوید", none.Why);

        Settings("stn_A");                    // همان پمپ، ولی کلید هنوز نیامده و اینترنت نیست
        var noKey = await BackupKeys.OpenForRestoreAsync(f, _dir);
        Assert.False(noKey.Ok);
        Assert.Contains("اینترنت", noKey.Why);
    }

    [Fact]
    public async Task BiMohr_HamanDareHamishegi()
    {
        Settings("stn_A", ("stn_A", KeyA));
        var plain = Plain(100);
        var r = await BackupKeys.OpenForRestoreAsync(plain, _dir);
        Assert.True(r.Ok);
        Assert.False(r.OwnPump);   // ⇒ درِ اشتراکِ پولی تصمیم می‌گیرد (‎RestoreGateTests‎)
        Assert.Equal(plain, r.Path);
    }

    [Fact]
    public async Task MohreDarja_BaKelideHamanPomp()
    {
        Settings("stn_A", ("stn_A", KeyA));
        var f = Plain(3000);
        var orig = File.ReadAllBytes(f);
        Assert.True(await BackupKeys.SealIfPossibleAsync(f, network: false));
        Assert.Equal("stn_A", BackupSeal.StationOf(f));
        var back = Path.Combine(_dir, "o.db");
        Assert.True(BackupSeal.Open(f, back, KeyA));
        Assert.Equal(orig, File.ReadAllBytes(back));

        Settings("");                          // بی‌حساب ⇒ بی مُهر می‌ماند و گفته می‌شود
        var g = Plain(10);
        Assert.False(await BackupKeys.SealIfPossibleAsync(g, network: false));
        Assert.False(BackupSeal.IsSealed(g));
    }
}
