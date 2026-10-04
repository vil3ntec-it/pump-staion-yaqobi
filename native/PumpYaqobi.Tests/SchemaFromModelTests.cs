using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ب۳ — ستون‌ها و پیوندهای پدر از خودِ مدل، بی فهرستِ دستی ══════════
///
/// «تمام شد یعنی: افزودنِ یک ستونِ تازه بی دست زدن به هیچ فهرستی کار می‌کند.»
/// پس سنجه دیتابیسی می‌سازد، هر ستونی را که SQLite اجازه می‌دهد برمی‌دارد
/// (یعنی «دیتابیسِ مشتریِ کهنه‌ای که این ستون‌ها را هنوز ندارد») و می‌سنجد که
/// <c>EnsureReady</c> همه را برمی‌گرداند و هر جدول خوانده می‌شود.
/// ⚠️ این کلاس ‎AppSettings‎ و ‎AppHost‎ را لمس نمی‌کند.
/// </summary>
public class SchemaFromModelTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-schema-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private List<string> Columns(SqliteConnection c, string table)
    {
        var list = new List<string>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT name FROM pragma_table_info('{table}')";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    [Fact]
    public void SotunhayeOftade_BiFehresteDasti_BarMigardand_VaHarJadvalKhandeMishavad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        using (var db = dbf.Create())
        {
            var d = new Debtor { LegacyId = "s1", Name = "کهنه", MainAccount = new DebtAccount { Name = "کهنه" } };
            d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/01", Fuel = FuelType.Petrol, Liters = 10 });
            db.Debtors.Add(d);
            db.TilCompanies.Add(new TilCompany { Name = "شرکت" });
            db.SaveChanges();
        }

        IReadOnlyList<(string Table, string Column, string Type)> model;
        using (var db = dbf.Create()) model = PumpDbFactory.ModelColumns(db.Model).ToList();

        var dropped = new List<string>();
        using (var c = new SqliteConnection("Data Source=" + _file))
        {
            c.Open();
            foreach (var (t, col, _) in model)
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = $"ALTER TABLE \"{t}\" DROP COLUMN \"{col}\"";
                try { cmd.ExecuteNonQuery(); dropped.Add(t + "." + col); } catch (SqliteException) { }
            }
            using var del = c.CreateCommand();
            del.CommandText = "DELETE FROM Settings WHERE Key='schema.stamp'";
            del.ExecuteNonQuery();
        }
        //  سنجه دندان دارد: صدها ستون واقعاً رفته‌اند، از جمله ستون‌هایی که در هیچ فهرستی نیستند
        Assert.True(dropped.Count > 100 && dropped.Contains("Users.DisplayName") && dropped.Contains("AmanatRows.Liters") && dropped.Contains("DebtRows.Liters"), "ستون‌های برداشته: " + dropped.Count + " — " + string.Join(", ", dropped.Take(40)));

        SqliteConnection.ClearAllPools();
        dbf.EnsureReady();

        using (var c = new SqliteConnection("Data Source=" + _file))
        {
            c.Open();
            var missing = model.Where(m => !Columns(c, m.Table).Contains(m.Column)).Select(m => m.Table + "." + m.Column).ToList();
            Assert.True(missing.Count == 0, "برنگشت: " + string.Join(", ", missing));
        }

        using (var db = dbf.Create())
        {
            foreach (var e in db.Model.GetEntityTypes())
            {
                var set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(e.ClrType).Invoke(db, null)!;
                var list = ((IQueryable<object>)set).AsNoTracking().ToList();   // هیچ «no such column» یا NULL در نوعِ غیرتهی
                Assert.NotNull(list);
            }
            Assert.Equal(0m, db.DebtRows.AsNoTracking().Single().Liters);
        }
    }

    [Fact]
    public void HarSotuneMadel_PishfarzeAddColumnDarad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        using var db = dbf.Create();
        var bad = new List<string>();
        foreach (var e in db.Model.GetEntityTypes())
            foreach (var p in e.GetProperties())
                if (!p.IsPrimaryKey() && !p.IsNullable && p.GetDefaultValue() is null && PumpDbFactory.ZeroOf(p) is null)
                    bad.Add(e.ClrType.Name + "." + p.Name + " : " + p.ClrType.Name);
        Assert.True(bad.Count == 0, "نوعِ ناشناس برای ADD COLUMN: " + string.Join(", ", bad));
    }

    /// <summary>هر ستونِ عددیِ «…Id» یا کلیدِ خارجی است، یا [SyncParent]، یا [NotAParent].</summary>
    [Fact]
    public void HarSotuneId_YaPedarDarad_YaSarihNadarad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        using var db = dbf.Create();
        var bad = new List<string>();
        foreach (var e in db.Model.GetEntityTypes())
        {
            if (!typeof(EntityBase).IsAssignableFrom(e.ClrType)) continue;
            foreach (var p in e.GetProperties())
            {
                var t = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
                if (p.Name == "Id" || !p.Name.EndsWith("Id", StringComparison.Ordinal)) continue;
                if (t != typeof(long) && t != typeof(int)) continue;
                if (p.IsForeignKey()) continue;
                var pi = p.PropertyInfo;
                if (pi is not null && (Attribute.IsDefined(pi, typeof(SyncParentAttribute), true)
                                       || Attribute.IsDefined(pi, typeof(NotAParentAttribute), true))) continue;
                bad.Add(e.ClrType.Name + "." + p.Name);
            }
        }
        Assert.True(bad.Count == 0, "ستونِ «…Id»ِ بی‌تکلیف: " + string.Join(", ", bad));
    }

    /// <summary>جابه‌جاییِ فهرستِ دستی به ویژگی هیچ پیوندی را جا نینداخت.</summary>
    [Fact]
    public void SoftParents_HamanYazdahPeyvandeGhabli()
    {
        var want = new HashSet<string>
        {
            "Expense.SalaryStaffId>StaffMember", "RasidEntry.InvoiceId>Invoice", "DebtRow.InvoiceId>Invoice",
            "DebtTableArchive.AccountId>DebtAccount", "CompanyTableArchive.CompanyId>TilCompany",
            "Invoice.DebtAccountId>DebtAccount", "StaffShortage.StaffId>StaffMember",
            "TilCompany.PurchaseCheckpointPetrol>FuelPurchase", "TilCompany.PurchaseCheckpointDiesel>FuelPurchase",
            "CompanyTableArchive.PurchasesAfter>FuelPurchase", "CompanyTableArchive.PurchasesBefore>FuelPurchase",
        };
        var got = OpLog.SoftParents.Select(s => $"{s.Entity}.{s.Property}>{s.Parent}").ToHashSet();
        Assert.Subset(got, want);
        //  و هیچ پیوندِ ویژگی‌داری که در مدل کلیدِ خارجیِ واقعی است دوباره شمرده نشده
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        using var db = dbf.Create();
        foreach (var s in OpLog.SoftParents)
        {
            var e = db.Model.GetEntityTypes().First(x => x.ClrType.Name == s.Entity);
            Assert.False(e.FindProperty(s.Property)!.IsForeignKey(), s.Entity + "." + s.Property);
        }
    }
}
