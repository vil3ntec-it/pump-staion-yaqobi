using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بکاپ‌ها: دیده شدنِ همه، «👁 مشاهده» بی تغییر، «♻ بازیابی» با گفتنِ آن‌چه جایگزین می‌شود (۱۴۰۵/۰۷/۱۸) ══
/// «بکاپ‌هایی که در پوشه هستند آیکن ندارند، باز نمی‌شوند و پیامِ خالی یا پیدا نشد می‌دهند.»
/// </summary>
public class BackupViewTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-bview-" + Guid.NewGuid().ToString("N"));
    private string DbFile => Path.Combine(_dir, "pump.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private (PumpDbFactory Db, BackupService Backup) Host()
    {
        Directory.CreateDirectory(_dir);
        var dbf = new PumpDbFactory(DbFile);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        return (dbf, new BackupService(dbf, new PermissionService(session)));
    }

    private static async Task AddPerson(PumpDbFactory dbf, string name)
    {
        await using var db = dbf.Create();
        var p = new Debtor { Name = name, LegacyId = "p" + Guid.NewGuid().ToString("N")[..8] };
        p.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/06/12", Name = "بردگی", Liters = 40m, PricePerLiter = 80m });
        db.Debtors.Add(p);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task HameyeBackupHa_DideMishavand_VaMovaghatNa()
    {
        var (dbf, backup) = Host();
        await AddPerson(dbf, "کریم");
        Assert.NotNull(backup.SnapshotToday());
        Assert.NotNull(backup.SafetyCopy());
        Assert.NotNull(SyncBackup.Write(dbf, label: "پیش-از-مهاجرت"));
        File.WriteAllText(Path.Combine(backup.SnapshotDir, "tmp-restore-abcd.db"), "x");

        var all = backup.ListAll();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, b => b.Kind.Contains("روزانه"));
        Assert.Contains(all, b => b.Kind.Contains("ایمنی"));
        Assert.Contains(all, b => b.Kind.Contains("رمزشده") || b.Kind.Contains("پشتیبان"));
        Assert.DoesNotContain(all, b => b.Name.StartsWith("tmp-"));
        //  ⛔ ‎List()‎ دست نخورد — ‎Prune‎ عکسِ ایمنی را با سقفِ روزانه پاک نمی‌کند
        Assert.Single(backup.List());
    }

    [Fact]
    public async Task Moshahede_FaghatMikhanad_VaHichChiziAvazNemishavad()
    {
        var (dbf, backup) = Host();
        await AddPerson(dbf, "کریم");
        var snap = backup.SnapshotToday()!;
        var hashBefore = FullBackup.Sha256(snap);
        var stampBefore = File.GetLastWriteTimeUtc(snap);

        var peek = BackupPeeker.Read(snap);
        Assert.True(peek.Ok, peek.Why);
        Assert.True(peek.Integrity);
        Assert.Equal(1, peek.CountOf("Debtors"));
        Assert.Equal(1, peek.CountOf("DebtRows"));
        Assert.Equal("1405/06/12", peek.LatestDebtRow);

        SqliteConnection.ClearAllPools();
        Assert.Equal(hashBefore, FullBackup.Sha256(snap));
        Assert.Equal(stampBefore, File.GetLastWriteTimeUtc(snap));
        Assert.False(File.Exists(snap + "-wal"));
    }

    [Fact]
    public void Moshahede_FaileNaBackup_YaGomshode_PayameRoshan()
    {
        Directory.CreateDirectory(_dir);
        var junk = Path.Combine(_dir, "note.db");
        File.WriteAllText(junk, "not sqlite");
        var a = BackupPeeker.Read(junk);
        Assert.False(a.Ok);
        Assert.Contains("بکاپِ این برنامه نیست", a.Why);
        var b = BackupPeeker.Read(Path.Combine(_dir, "gone.db"));
        Assert.False(b.Ok);
        Assert.Contains("پیدا نشد", b.Why);
    }

    [Fact]
    public async Task Bazyabi_MigooyadCheChiziJaygozinMishavad()
    {
        var (dbf, backup) = Host();
        await AddPerson(dbf, "کریم");
        var snap = backup.SnapshotToday()!;
        await AddPerson(dbf, "رحیم");
        await AddPerson(dbf, "سلیم");
        SqliteConnection.ClearAllPools();

        var diff = BackupPeeker.Diff(BackupPeeker.Read(DbFile), BackupPeeker.Read(snap));
        Assert.Contains(diff, l => l.StartsWith("قرض‌داران: 3 ⇐ 1"));
        Assert.Contains(diff, l => l.StartsWith("ردیف‌های حساب‌ها: 3 ⇐ 1"));
        Assert.DoesNotContain(diff, l => l.StartsWith("ورق‌های روزانه"));   // بی‌تغییر گفته نمی‌شود

        //  و بازیابی پیش از جایگزینی بکاپِ ایمنی می‌گیرد — همان راهِ همیشگی
        var r = backup.Restore(snap);
        Assert.True(r.Ok, r.Message);
        Assert.NotNull(r.SafetyCopy);
        Assert.Equal(3, BackupPeeker.Read(r.SafetyCopy!).CountOf("Debtors"));
        SqliteConnection.ClearAllPools();
        Assert.Equal(1, BackupPeeker.Read(DbFile).CountOf("Debtors"));
    }

    [Fact]
    public void DobarKelik_RuyeBackup_FaghatMoshahede()
    {
        Directory.CreateDirectory(_dir);
        var pyq = Path.Combine(_dir, "backup-1.pyq"); File.WriteAllText(pyq, "x");
        var db = Path.Combine(_dir, "pump-1405-07-18.DB"); File.WriteAllText(db, "x");
        Assert.Equal(pyq, OpenRequest.FromArgs(new[] { pyq }));
        Assert.Equal(db, OpenRequest.FromArgs(new[] { db }));
        Assert.True(OpenRequest.IsViewOnly(pyq) && OpenRequest.IsViewOnly(db));
        Assert.False(OpenRequest.IsViewOnly("x.pumpyaqobi"));

        var main = SrcText.Read(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "PumpYaqobi.App", "ViewModels", "MainViewModel.cs"));
        Assert.Contains("OpenRequest.IsViewOnly(path)) await bv.ViewFileAsync(path)", main);
    }
}
