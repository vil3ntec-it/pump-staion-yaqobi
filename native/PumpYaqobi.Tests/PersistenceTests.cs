using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// آزمونِ دیتابیس روی یک SQLiteِ واقعیِ روی دیسک (نه درون‌حافظه‌ای) — چون
/// چیزی که باید ثابت شود همان رفتارِ فایلِ واقعی است: ایندکس‌ها ساخته شوند،
/// حذفِ نرم کار کند و داده پس از بستن و باز کردنِ دوباره سرِ جایش بماند.
/// </summary>
public class PersistenceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-test-{Guid.NewGuid():N}.db");

    private PumpDbContext Open()
    {
        var opts = new DbContextOptionsBuilder<PumpDbContext>()
            .UseSqlite($"Data Source={_file}").Options;
        var db = new PumpDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    [Fact]
    public void Database_IsCreatedWithTheExpectedIndexes()
    {
        using var db = Open();
        var names = new List<string>();
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        db.Database.OpenConnection();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%'";
        using var r = cmd.ExecuteReader();
        while (r.Read()) names.Add(r.GetString(0));
        // ایندکس‌هایی که بندِ ۵ صریحاً خواسته
        Assert.Contains(names, n => n.Contains("DebtRows") && n.Contains("DateKey"));
        Assert.Contains(names, n => n.Contains("DebtRows") && n.Contains("Fuel"));
        Assert.Contains(names, n => n.Contains("Debtors") && n.Contains("Name"));
        Assert.Contains(names, n => n.Contains("SafeEntries") && n.Contains("MonthKey"));
    }

    [Fact]
    public void Data_SurvivesCloseAndReopen()
    {
        long id;
        using (var db = Open())
        {
            var p = new Debtor { LegacyId = "p1", Name = "کریم" };
            p.MainAccount.FuelRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 12.5m, DateKey = 14050614 });
            db.Debtors.Add(p);
            db.SaveChanges();
            id = p.Id;
        }
        using (var db = Open())
        {
            var p = db.Debtors.Include(x => x.MainAccount).ThenInclude(a => a.FuelRows).Single(x => x.Id == id);
            Assert.Equal("کریم", p.Name);                       // فارسی سالم برگشت
            Assert.Equal(FuelType.Diesel, p.MainAccount.FuelRows[0].Fuel);
            Assert.Equal(12.5m, p.MainAccount.FuelRows[0].Liters);   // اعشار دقیق، نه شناور
        }
    }

    [Fact]
    public void Delete_IsSoft_AndHiddenFromQueries()
    {
        using var db = Open();
        var p = new Debtor { LegacyId = "p2", Name = "حذفی" };
        db.Debtors.Add(p); db.SaveChanges();

        db.Debtors.Remove(p); db.SaveChanges();

        Assert.Empty(db.Debtors.ToList());                                   // از دیدِ برنامه رفته
        Assert.Single(db.Debtors.IgnoreQueryFilters().ToList());             // ولی واقعاً پاک نشده
        Assert.NotNull(db.Debtors.IgnoreQueryFilters().Single().DeletedAt);
    }

    [Fact]
    public void Timestamps_AreStampedAutomatically()
    {
        using var db = Open();
        var p = new Debtor { LegacyId = "p3", Name = "زمان" };
        db.Debtors.Add(p); db.SaveChanges();
        var created = p.CreatedAt;
        Assert.NotEqual(default, created);

        Thread.Sleep(5);
        p.Name = "زمانِ تازه"; db.SaveChanges();
        Assert.True(p.UpdatedAt > created);       // ویرایش، UpdatedAt را جلو می‌برد
        Assert.Equal(created, p.CreatedAt);       // و CreatedAt را دست نمی‌زند
    }

    [Fact] // بندِ ۵: عملیاتِ چندمرحله‌ای باید Transaction داشته باشد
    public void Transaction_RollsBackEverythingOnFailure()
    {
        using var db = Open();
        db.Debtors.Add(new Debtor { LegacyId = "keep", Name = "ماندنی" });
        db.SaveChanges();

        using (var tx = db.Database.BeginTransaction())
        {
            db.Debtors.Add(new Debtor { LegacyId = "rollback", Name = "برگشتی" });
            db.SaveChanges();
            tx.Rollback();
        }
        db.ChangeTracker.Clear();
        Assert.Single(db.Debtors.ToList());
        Assert.Equal("ماندنی", db.Debtors.Single().Name);
    }
}
