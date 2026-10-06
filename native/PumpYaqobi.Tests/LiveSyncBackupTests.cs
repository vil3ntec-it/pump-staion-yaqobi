using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «هر تغییر درجا روی سرور» و «بکاپِ دستی یک ساعت لودینگ» (۱۴۰۵/۰۷/۲۲) ══════
///
/// رفتارِ اصلی روی پشتهٔ واقعی سنجیده می‌شود (سنجهٔ ‎livesync‎، دو کامپیوتر،
/// سرورِ حسابِ واقعی). این‌جا هر قاعده جدا قفل است تا برنگردد:
///   • بکاپِ سرور فشرده می‌رود و برگرداندن خودش بازش می‌کند؛
///   • سرورِ خانگیِ خاموش دیگر فرستادن را دقیقه‌ها معطل نمی‌کند؛
///   • توکنِ دستگاهِ مرده خودش از نو بند می‌شود؛
///   • عکسِ گوشی و کیو‌آر با هر ذخیره بیدار می‌شود؛
///   • هر سه ساعت و پیش از بستن، بکاپ به سرور.
/// </summary>
public class LiveSyncBackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-lsb-" + Guid.NewGuid().ToString("N"));
    public LiveSyncBackupTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static string Src(params string[] parts) => SrcText.Read(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    // ── فشرده‌سازی ─────────────────────────────────────────────────────

    [Fact]
    public void BackupeFeshorde_Bazmigardad_VaKhamHamanMimanad()
    {
        var db = Path.Combine(_dir, "pump.db");
        new PumpDbFactory(db).EnsureReady();
        SqliteConnection.ClearAllPools();
        var gz = Path.Combine(_dir, "pump.db.gz");
        BackupService.Gzip(db, gz);

        Assert.True(BackupService.IsGzip(gz));
        Assert.False(BackupService.IsGzip(db));
        Assert.True(new FileInfo(gz).Length < new FileInfo(db).Length);

        var back = BackupService.ExpandIfGzip(gz, _dir);
        Assert.NotNull(back);
        Assert.Equal(File.ReadAllBytes(db), File.ReadAllBytes(back!));
        Assert.True(BackupService.Inspect(back!) >= 0);
        SqliteConnection.ClearAllPools();
        foreach (var f in Directory.GetFiles(_dir, Path.GetFileName(back!) + "*")) File.Delete(f);

        //  ⛔ فایلِ خام (بکاپ‌های پیش از این نسخه) همان‌طور برمی‌گردد — باز کردنی نیست
        Assert.Null(BackupService.ExpandIfGzip(db, _dir));

        //  ⛔ فشردهٔ نیمه‌کاره هرگز به بازگردانی نمی‌رسد
        var cut = Path.Combine(_dir, "cut.gz");
        var bytes = File.ReadAllBytes(gz);
        File.WriteAllBytes(cut, bytes[..(bytes.Length / 2)]);
        Assert.Null(BackupService.ExpandIfGzip(cut, _dir));
        Assert.Empty(Directory.GetFiles(_dir, "tmp-gunzip-*").Select(Path.GetFileName));
    }

    [Fact]
    public void BackupeServer_Feshorde_HamZaman_VaBiEntezareKhangi()
    {
        var src = Src("PumpYaqobi.Shell", "Services", "BackupPusher.cs");
        Assert.Contains("ext: \"db.gz\"", src);
        Assert.Contains("BackupService.Gzip(file, packed)", src);
        //  دو مقصد هم‌زمان: خانگی پیش از ابر شروع می‌شود و پس از آن منتظرش می‌مانیم
        var home = src.IndexOf("var homeTask = SendAsync(file, ct);", StringComparison.Ordinal);
        var cloud = src.IndexOf("var toCloud = await SendToCloudAsync(", StringComparison.Ordinal);
        Assert.True(home > 0 && cloud > home);
        Assert.Contains("await homeTask;", src);
        Assert.Contains("ConnectTimeout = TimeSpan.FromSeconds(10)", src);
        //  برگرداندن: فشرده پیش از هر سنجشِ دیگری باز می‌شود
        var vm = Src("PumpYaqobi.App", "ViewModels", "Sections", "BackupSectionViewModel.cs");
        var gz = vm.IndexOf("BackupService.ExpandIfGzip(path", StringComparison.Ordinal);
        var allowed = vm.IndexOf("if (!ownCloud && !RestoreAllowed()) return;", StringComparison.Ordinal);
        Assert.True(gz > 0 && allowed > gz);
    }

    [Fact]
    public void Backup_HarSeSaat_VaPishAzBastan()
    {
        Assert.Equal(TimeSpan.FromHours(3), BackupPusher.Every);
        Assert.True(App.ViewModels.MainViewModel.ExitSendCap <= TimeSpan.FromMinutes(3));
        var win = Src("PumpYaqobi.App", "Views", "MainWindow.axaml.cs");
        var offer = win.IndexOf("await vm.OfferBackupBeforeExitAsync();", StringComparison.Ordinal);
        var send = win.IndexOf("await vm.SendToServerBeforeExitAsync();", StringComparison.Ordinal);
        var close = win.IndexOf("_flushed = true;", StringComparison.Ordinal);
        Assert.True(offer > 0 && send > offer && close > send);
        //  ⛔ «بستن بی‌انتظار» همیشه هست
        Assert.Contains("SkipExitSendCommand", Src("PumpYaqobi.App", "Views", "MainWindow.axaml"));
        //  ⛔ دفترِ عوض‌نشده دوباره نمی‌رود
        Assert.Contains("ChangedSinceSend", Src("PumpYaqobi.App", "ViewModels", "MainViewModel.Lifecycle.cs"));
    }

    // ── همگام‌سازی ─────────────────────────────────────────────────────

    [Fact]
    public void TokeneMorde_KhodashAzNoBandMishavad()
    {
        var eng = Src("PumpYaqobi.Shell", "Services", "SyncEngine.cs");
        Assert.Equal(2, eng.Split("await HealDeadTokenAsync(cloud, ").Length - 1);
        Assert.Contains("code != \"invalid_token\"", eng);
        Assert.True(SyncEngine.HealGap >= TimeSpan.FromMinutes(1));
        var link = Src("PumpYaqobi.Shell", "Services", "CloudLink.cs");
        Assert.Contains("ReseatToAccountPumpAsync(leaveOther: false, ct)", link);
    }

    [Fact]
    public void AksGooshi_BaHarZakhire_Bidar()
    {
        Assert.True(StationPublisher.Interval <= TimeSpan.FromSeconds(3));
        Assert.True(StationPublisher.CloudLiveGap <= TimeSpan.FromSeconds(15));
        var pub = Src("PumpYaqobi.Shell", "Services", "StationPublisher.cs");
        Assert.Contains("PumpDbContext.Saved += OnSaved;", pub);
        Assert.Contains("PumpDbContext.Saved -= OnSaved;", pub);
        //  ⛔ ترمزِ ‎Version‎ و سقفِ CPU سرِ جایشان
        Assert.Contains("version == _lastVersion", pub);
        Assert.Contains("_nextBuildAt = AppClock.Mono + built.Elapsed * 19;", pub);
    }
}
