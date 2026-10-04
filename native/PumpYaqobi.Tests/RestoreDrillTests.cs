using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ب۵ — آزمونِ بازیابیِ ماهانه و «بکاپ فقط روی همین کامپیوتر» ═══════
/// روی دفترِ واقعیِ SQLite: بکاپِ سالم ⇒ سبز؛ بکاپِ خراب یا کم‌ردیف ⇒ سرخ با دلیل؛
/// ماهی یک بار و نه بیشتر؛ و دفتر و عکس دست نمی‌خورند.
/// ⚠️ این کلاس ‎AppSettings‎ و ‎AppHost‎ را لمس نمی‌کند.
/// </summary>
public class RestoreDrillTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-drill-" + Guid.NewGuid().ToString("N"));
    public RestoreDrillTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private (PumpDbFactory Db, BackupService Backup, RestoreDrill Drill) Host()
    {
        var dbf = new PumpDbFactory(Path.Combine(_dir, "pump.db"));
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var b = new BackupService(dbf, new PermissionService(session));
        using (var db = dbf.Create())
        {
            for (var i = 0; i < 20; i++)
            {
                var d = new Debtor { LegacyId = "d" + i, Name = "مشتری " + i, MainAccount = new DebtAccount { Name = "مشتری " + i } };
                d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/01", Liters = 10 + i });
                db.Debtors.Add(d);
            }
            db.SafeEntries.Add(new SafeEntry { Title = "گاوصندوق", Amount = 5, DateKey = 14050701, MonthKey = "1405/07" });
            db.SaveChanges();
        }
        return (dbf, b, new RestoreDrill(dbf, b));
    }

    [Fact]
    public void BackupeSalem_Sabz_VaHarJadvalSanjideMishavad()
    {
        var (db, _, drill) = Host();
        var before = new FileInfo(db.DbPath).Length;   // ⚠️ نه ReadAllBytes: روی ویندوز اتصالِ استخر فایل را باز دارد
        var r = drill.RunNow();
        Assert.True(r.Ok, r.Text);
        Assert.True(r.Tables > 20, "جدول‌ها: " + r.Tables);
        Assert.StartsWith("✅", drill.Last()!.Text);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_dir, "backups"), "tmp-drill-*"));   // رونوشتِ موقت پاک شد
        Assert.True(File.Exists(db.DbPath) && new FileInfo(db.DbPath).Length >= before);
    }

    [Fact]
    public void BackupeKharab_Sorkh()
    {
        var (db, backup, _) = Host();
        var snap = backup.SnapshotToday()!;
        //  وسطِ فایل خراب می‌شود — همان دیسکِ بد یا کپیِ نیمه‌کاره
        var bytes = File.ReadAllBytes(snap);
        for (var i = bytes.Length / 3; i < bytes.Length / 3 + 4096 && i < bytes.Length; i++) bytes[i] = 0x5A;
        File.WriteAllBytes(snap, bytes);
        var why = RestoreDrill.Verify(snap, db.DbPath, out _);
        Assert.NotNull(why);
    }

    [Fact]
    public void BackupeKamRadif_Sorkh_BaNameJadval()
    {
        var (db, backup, _) = Host();
        var snap = backup.SnapshotToday()!;
        using (var c = new SqliteConnection("Data Source=" + snap + ";Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM DebtRows WHERE Id IN (SELECT Id FROM DebtRows LIMIT 3)";
            cmd.ExecuteNonQuery();
        }
        var why = RestoreDrill.Verify(snap, db.DbPath, out _);
        Assert.NotNull(why);
        Assert.Contains("DebtRows", why);
    }

    [Fact]
    public void MahiYekBar()
    {
        var (_, _, drill) = Host();
        Assert.NotNull(drill.MonthlyOnce());
        Assert.Null(drill.MonthlyOnce());   // همان ماه ⇒ هیچ کاری
    }

    [Fact]
    public void BirunAzKampyuter_Khales()
    {
        var now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(BackupOffsite.Ok("", "", now));
        Assert.True(BackupOffsite.Ok(now.AddDays(-3).ToString("O"), "", now));
        Assert.False(BackupOffsite.Ok(now.AddDays(-9).ToString("O"), "", now));
        Assert.True(BackupOffsite.Ok("", now.AddDays(-20).ToString("O"), now));
        Assert.False(BackupOffsite.Ok("", now.AddDays(-40).ToString("O"), now));
        Assert.False(BackupOffsite.Ok(now.AddDays(+30).ToString("O"), "", now));   // مُهرِ آینده چیزی ثابت نمی‌کند
    }
}
