using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «بکاپ‌های پوشه (‎pump-1405-07-19‎، نوعِ "Data") با دوبار-کلیک باز نمی‌شوند» (۳.۱.۲۷۲) ══
/// ‎.db‎ را ویندوز صاحب بود و نصاب عمداً پیش‌فرضش را نمی‌گیرد؛ عکس‌های تازه ‎.pumpbak‎اند،
/// نصاب همین را به برنامه می‌سپارد و برنامه فقط «مشاهده» را باز می‌کند (بازیابی با پرسش).
/// </summary>
public class BackupExtensionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-bext-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private BackupService Host()
    {
        Directory.CreateDirectory(_dir);
        var dbf = new PumpDbFactory(Path.Combine(_dir, "pump.db"));
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        return new BackupService(dbf, new PermissionService(session));
    }

    [Fact]
    public void AksHayeTaze_PasvandeKhodeBarname_VaKohneHa_HanuzKhandeMishavand()
    {
        var backup = Host();
        var snap = backup.SnapshotToday()!;
        Assert.EndsWith(BackupService.SnapshotExt, snap);
        var safety = backup.SafetyCopy()!;
        Assert.EndsWith(BackupService.SnapshotExt, safety);
        Assert.True(BackupService.Inspect(snap) >= 0, "عکسِ .pumpbak همان دفترِ SQLite است");

        //  عکسِ کهنهٔ ‎.db‎ ⇒ به ‎.pumpbak‎ نام عوض می‌کند (هیچ چیزی پاک نمی‌شود)
        var old = Path.Combine(backup.SnapshotDir, "pump-1400-01-01.db");
        File.Copy(snap, old);
        backup.Prune();
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(Path.Combine(backup.SnapshotDir, "pump-1400-01-01" + BackupService.SnapshotExt)));

        //  عکسِ بی‌پسوندِ کهنه هم در فهرست دیده می‌شود
        File.Copy(snap, Path.Combine(backup.SnapshotDir, "pump-1399-12-29"));
        var all = backup.ListAll();
        Assert.Contains(all, b => Path.GetFileName(b.Path) == "pump-1399-12-29");
        Assert.Contains(all, b => Path.GetFileName(b.Path) == "pump-1400-01-01" + BackupService.SnapshotExt);
        Assert.Contains(backup.List(), b => b.Path == snap);
    }

    [Fact]
    public void DobarKelik_RuyePumpbak_VaBiPasvand_FaghatMoshahede()
    {
        Directory.CreateDirectory(_dir);
        var bak = Path.Combine(_dir, "pump-1405-07-19" + BackupService.SnapshotExt); File.WriteAllText(bak, "x");
        var bare = Path.Combine(_dir, "pump-1405-07-19"); File.WriteAllText(bare, "x");
        Assert.Equal(bak, OpenRequest.FromArgs(new[] { "--after-update", bak }));
        Assert.Equal(bare, OpenRequest.FromArgs(new[] { bare }));
        Assert.True(OpenRequest.IsViewOnly(bak) && OpenRequest.IsViewOnly(bare));
        Assert.False(OpenRequest.IsViewOnly("--after-update"));
        Assert.False(OpenRequest.IsViewOnly(Path.Combine(_dir, "notes")));
        Assert.False(BackupService.IsLedgerBackupName("pump-1405.part"));
    }

    [Fact]
    public void Nasab_PumpbakRa_BeBarname_Misepard()
    {
        var iss = SrcText.Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../", "installer/PumpYaqobi.iss")));
        Assert.Contains("Subkey: \"Software\\Classes\\.pumpbak\"; ValueType: string; ValueName: \"\"; ValueData: \"PumpYaqobi.Backup\"; Flags: uninsdeletevalue", iss);
        Assert.Contains("Subkey: \"Software\\Classes\\.pumpbak\\OpenWithProgids\"; ValueType: string; ValueName: \"PumpYaqobi.Backup\"", iss);
        Assert.Contains("FileExts\\.pumpbak\\UserChoice\"; ValueType: none; Flags: deletekey", iss);
        Assert.Contains("ValueName: \".pumpbak\"; ValueData: \"\"", iss);
        Assert.Contains("ChangesAssociations=yes", iss);
        //  همان ‎ProgID‎ که «"%1"» را به برنامه می‌دهد
        Assert.Contains("Subkey: \"Software\\Classes\\PumpYaqobi.Backup\\shell\\open\\command\"; ValueType: string; ValueName: \"\"; ValueData: \"\"\"{app}\\{#AppExe}\"\" \"\"%1\"\"\"", iss);
        Assert.Equal(BackupService.SnapshotExt, OpenRequest.SnapshotExt);
        Assert.Equal(".pumpbak", BackupService.SnapshotExt);
    }
}
