using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ب۲ — کلیدهای منبع بی شمارهٔ محلی ═════════════════════════════════
///
/// دفترِ واقعیِ SQLite با کلیدهای شکلِ کهنه (‎"id7|day|0"‎ · ‎"wq-sales-7-day"‎ ·
/// ‎"p-3-day"‎ · ‎"parcha|id5"‎ و همان داخلِ آرشیو) — مهاجرت همه را به شناسهٔ
/// سراسری می‌برد، هیچ ردیف و هیچ عددی عوض نمی‌شود، هیچ opی ساخته نمی‌شود،
/// عکسِ ایمنی گرفته می‌شود، و منبعِ نبوده دست نمی‌خورد.
/// ⚠️ این کلاس ‎AppSettings‎ و ‎AppHost‎ را لمس نمی‌کند.
/// </summary>
public class SrcKeyMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-srckey-" + Guid.NewGuid().ToString("N"));
    private string File_ => Path.Combine(_dir, "pump.db");

    public SrcKeyMigrationTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private void Raw(string sql)
    {
        using var c = new SqliteConnection("Data Source=" + File_);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void KelidhayeKohne_BeShenaseyeSarasari_HichRadifiAvazNemishavad()
    {
        var dbf = new PumpDbFactory(File_);
        dbf.EnsureReady();

        long wid, rid, pid;
        string wuid, ruid, puid;
        using (var db = dbf.Create())
        {
            var w = new WaraqEntry { DateShamsi = "1405/07/10" };
            var rep = new ParchaReport { DateShamsi = "1405/07/10", Fuel = FuelType.Petrol };
            var pr = new ParchaReceipt { DateShamsi = "1405/07/10", Name = "رسید" };
            var legacy = new WaraqEntry { DateShamsi = "1405/07/11", LegacyId = "wq1758" };
            db.WaraqEntries.AddRange(w, legacy);
            db.Reports.Add(rep);
            db.ParchaReceipts.Add(pr);
            db.SaveChanges();
            (wid, rid, pid) = (w.Id, rep.Id, pr.Id);
            (wuid, ruid, puid) = (w.SyncUid!, rep.SyncUid!, pr.SyncUid!);

            var shift = new WaraqShift { WaraqId = w.Id, Kind = ShiftKind.Day };
            db.WaraqShifts.Add(shift);
            var d = new Debtor { LegacyId = "k1", Name = "قرض‌دار", MainAccount = new DebtAccount { Name = "قرض‌دار" } };
            d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/10", Liters = 10, SrcKey = "x" });
            d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/10", Liters = 11, SrcKey = "y" });
            d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/10", Liters = 12, SrcKey = "z" });
            d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/10", Liters = 13, SrcKey = "debtQuick|abc" });
            db.Debtors.Add(d);
            db.SafeEntries.Add(new SafeEntry { DateShamsi = "1405/07/10", SrcKey = "s" });
            db.Expenses.Add(new Expense { DateShamsi = "1405/07/10", SrcKey = "e" });
            db.SaveChanges();
            db.WaraqPumps.Add(new WaraqPump { ShiftId = shift.Id, Start = 1, End = 2, SrcKey = "p" });
            db.DebtTableArchives.Add(new DebtTableArchive { AccountId = d.MainAccount.Id,
                RowsJson = "[{\"SrcKey\":\"id" + w.Id + "|day|2\",\"Liters\":5},{\"SrcKey\":\"wq1758|day|0\"},{\"Liters\":1}]" });
            db.SaveChanges();
        }
        //  همان کلیدهایی که نسخهٔ ۳.۱.۲۴۰ می‌نوشت — با SQLِ خام، مثلِ دفترِ مشتریِ امروز
        Raw($"UPDATE DebtRows SET SrcKey='id{wid}|day|0' WHERE SrcKey='x'");
        Raw($"UPDATE DebtRows SET SrcKey='parcha|id{pid}' WHERE SrcKey='y'");
        Raw("UPDATE DebtRows SET SrcKey='id99999|day|0' WHERE SrcKey='z'");      // منبعِ نبوده
        Raw($"UPDATE SafeEntries SET SrcKey='wq-sales-{wid}-day'");
        Raw($"UPDATE Expenses SET SrcKey='id{wid}|night|1'");
        Raw($"UPDATE WaraqPumps SET SrcKey='p-{rid}-day'");
        Raw($"UPDATE DebtRows SET DeletedAt='2026-01-01 00:00:00' WHERE SrcKey='id{wid}|day|0'");   // پاک‌شده هم
        Raw($"DELETE FROM Settings WHERE Key='{PumpDbFactory.SrcKeysFlag}'");

        int ops0, rows0; decimal liters0;
        using (var db = dbf.Create())
        {
            ops0 = db.SyncOps.Count();
            rows0 = db.DebtRows.IgnoreQueryFilters().Count();
            liters0 = db.DebtRows.IgnoreQueryFilters().AsNoTracking().AsEnumerable().Sum(r => r.Liters);
        }

        SqliteConnection.ClearAllPools();
        var dbf2 = new PumpDbFactory(File_);
        dbf2.EnsureReady();
        Assert.True(dbf2.SrcKeysMigrated >= 6, "عوض شد: " + dbf2.SrcKeysMigrated);

        using (var db = dbf2.Create())
        {
            var keys = db.DebtRows.IgnoreQueryFilters().AsNoTracking().Select(r => r.SrcKey).ToList();
            Assert.Contains($"u{wuid}|day|0", keys);
            Assert.Contains($"parcha|u{puid}", keys);
            Assert.Contains("id99999|day|0", keys);           // منبعی نیست ⇒ دست نخورد
            Assert.Contains("debtQuick|abc", keys);
            Assert.Equal($"wq-sales-u{wuid}-day", db.SafeEntries.AsNoTracking().Single().SrcKey);
            Assert.Equal($"u{wuid}|night|1", db.Expenses.AsNoTracking().Single().SrcKey);
            Assert.Equal($"p-u{ruid}-day", db.WaraqPumps.AsNoTracking().Single().SrcKey);
            var json = db.DebtTableArchives.AsNoTracking().Single().RowsJson;
            Assert.Contains($"u{wuid}|day|2", json);
            Assert.Contains("wq1758|day|0", json);

            //  ⛔ هیچ ردیف و هیچ عددی عوض نشد، و هیچ opی به سرور نمی‌رود
            Assert.Equal(rows0, db.DebtRows.IgnoreQueryFilters().Count());
            Assert.Equal(liters0, db.DebtRows.IgnoreQueryFilters().AsNoTracking().AsEnumerable().Sum(r => r.Liters));
            Assert.Equal(ops0, db.SyncOps.Count());
            Assert.True(db.Settings.Any(s => s.Key == PumpDbFactory.SrcKeysFlag));
        }
        Assert.Contains(Directory.EnumerateFiles(Path.Combine(_dir, "backups")), f => Path.GetFileName(f).StartsWith("pre-srckey"));

        //  بارِ دوم هیچ کاری نمی‌کند
        SqliteConnection.ClearAllPools();
        Raw($"DELETE FROM Settings WHERE Key='{PumpDbFactory.SrcKeysFlag}'");
        var dbf3 = new PumpDbFactory(File_);
        dbf3.EnsureReady();
        Assert.Equal(0, dbf3.SrcKeysMigrated);
    }

    /// <summary>هیچ سازندهٔ کلیدی دیگر شمارهٔ محلی نمی‌سازد (آزمونِ سورس).</summary>
    [Fact]
    public void HichSazandeyeKelid_ShomareyeMahali_Nemisazad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var bad = new List<string>();
        foreach (var f in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains("/obj/") || f.Contains("/bin/") || f.Contains("Tests")) continue;
            if (f.EndsWith("SrcKeys.cs")) continue;
            var code = string.Join("\n", File.ReadAllLines(f).Select(l => { var i = l.IndexOf("//"); return i >= 0 ? l[..i] : l; }));
            foreach (var pat in new[] { "\"wq-sales-\" + w.Id", "\"id\" + w.Id", "\"parcha|\" +", "\"-\" + recordId", "\"p-\" : \"d-\"", "\"d-\" : \"p-\"" })
                if (code.Contains(pat)) bad.Add(Path.GetFileName(f) + ": " + pat);
        }
        Assert.True(bad.Count == 0, string.Join(", ", bad));
    }

    [Fact]
    public void Migrate_Khales()
    {
        string? U(string t, long id) => (t, id) switch
        {
            ("WaraqEntries", 7) => "W7", ("Reports", 3) => "R3", ("ParchaReceipts", 5) => "P5", _ => null,
        };
        Assert.Equal("uW7|day|0", SrcKeys.Migrate("id7|day|0", U));
        Assert.Equal("wq-sales-uW7-night", SrcKeys.Migrate("wq-sales-7-night", U));
        Assert.Equal("d-uR3-night", SrcKeys.Migrate("d-3-night", U));
        Assert.Equal("parcha|uP5", SrcKeys.Migrate("parcha|id5", U));
        Assert.Equal("id8|day|0", SrcKeys.Migrate("id8|day|0", U));
        Assert.Equal("p-live-day", SrcKeys.Migrate("p-live-day", U));
        Assert.Equal("wq1758|day|0", SrcKeys.Migrate("wq1758|day|0", U));
        Assert.False(SrcKeys.IsLocal("uW7|day|0"));
        Assert.False(SrcKeys.IsLocal("p-uR3-day"));
        Assert.True(SrcKeys.IsLocal("p-3-day"));
    }
}
