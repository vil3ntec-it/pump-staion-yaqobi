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

/// <summary>
/// آزمونِ «همهٔ جدول‌ها» — هر موجودیتِ برنامه یک‌بار ساخته، ذخیره، بسته و
/// دوباره خوانده می‌شود. اگر نگاشتی خراب باشد (کلیدِ خارجی، فیلترِ سراسری،
/// تبدیلِ enum) همین‌جا می‌شکند، نه وسطِ کارِ کاربر.
/// </summary>
public class FullSchemaTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-schema-{Guid.NewGuid():N}.db");

    private PumpDbContext Open()
    {
        var opts = new DbContextOptionsBuilder<PumpDbContext>().UseSqlite($"Data Source={_file}").Options;
        var db = new PumpDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    [Fact]
    public void EveryTable_RoundTrips()
    {
        using (var db = Open())
        {
            db.Reports.Add(new ParchaReport
            {
                ReportNum = 1, DateShamsi = "1405/06/15", DateKey = 14050615, Fuel = FuelType.Diesel,
                DayShift = new ShiftData { Name = "احمد", Start = 100, End = 250, Price = 60, Sale = 150, Money = 9000 },
            });
            db.FuelPurchases.Add(new FuelPurchase { Fuel = FuelType.Petrol, Seller = "شرکت الف", Kg = 30000, Density = 0.75m, PriceTon = 700, UsdRate = 70, DateKey = 14050610 });
            db.TankDips.Add(new TankDip { Fuel = FuelType.Petrol, Measured = 1000, Expected = 1010, DateKey = 14050611 });
            db.TankerUnloads.Add(new TankerUnload { Fuel = FuelType.Diesel, Driver = "کریم", Liters = 20000, DateKey = 14050612 });

            var comp = new TilCompany { Name = "شرکتِ بزرگ" };
            comp.Rows.Add(new CompanyRow { Fuel = FuelType.Petrol, SortIndex = 0, Ton = 30, Usd = 700, Rate = 70, Poul = 1000, DateKey = 14050610 });
            comp.Rows.Add(new CompanyRow { Fuel = FuelType.Diesel, SortIndex = 0, Ton = 10, Usd = 690, Rate = 70, DateKey = 14050611 });
            db.TilCompanies.Add(comp);

            var am = new AmanatAccount { Name = "حاجی", Fuel = FuelType.Diesel, MyPct = 2 };
            am.Rows.Add(new AmanatRow { SortIndex = 0, Liters = 5000, State = AmanatRowState.Open, DateKey = 14050609 });
            db.AmanatAccounts.Add(am);

            var w = new WaraqEntry { DateShamsi = "1405/06/15", DateKey = 14050615, Station = "پمپ یعقوبی" };
            var sh = new WaraqShift { Kind = ShiftKind.Day, WorkerName = "نصیر", PricePerLiter = 60 };
            sh.Pumps.Add(new WaraqPump { SortIndex = 0, Num = 1, Start = 10, End = 110, PricePerLiter = 60 });
            sh.Transactions.Add(new WaraqTransaction { SortIndex = 0, Name = "قرض‌دار الف", Liters = 10, Amount = 600, Type = WaraqTxnType.Debt });
            w.Shifts.Add(sh);
            db.WaraqEntries.Add(w);

            db.Invoices.Add(new Invoice { InvoiceNumber = 1, CustomerName = "مشتری", Liters = 20, PricePerLiter = 60, Amount = 1200, DateKey = 14050615 });

            var staff = new StaffMember { Name = "نصیر", Salary = 12000, PayDay = 1 };
            db.StaffMembers.Add(staff);
            db.SaveChanges();

            db.Attendance.Add(new AttendanceRow { StaffId = staff.Id, DateShamsi = "1405/06/15", DateKey = 14050615, In = "07:00", Out = "19:00" });
            db.SalaryPayments.Add(new SalaryPayment { StaffId = staff.Id, MonthKey = "1405/06", Amount = 12000 });
            db.StaffShortages.Add(new StaffShortage { StaffId = staff.Id, Name = "نصیر", Amount = 500, DateKey = 14050615 });
            db.Cameras.Add(new Camera { Name = "دروازه", Url = "http://x/1" });
            db.ExtraIncomes.Add(new ExtraIncome { Qty = 10, Buy = 50, Market = 60, Amount = 100, DateKey = 14050615 });
            db.RateHistory.Add(new RateHistoryEntry { Fuel = FuelType.Petrol, Rate = 62, DateKey = 14050615 });
            db.Trash.Add(new TrashItem { Kind = "expense", Label = "مصرفِ پاک‌شده", PayloadJson = "{}" });
            db.Settings.Add(new Setting { Key = "stationName", Value = "پمپ یعقوبی" });
            db.Users.Add(new AppUser { UserName = "admin", Role = UserRole.Admin, PasswordHash = "x" });
            db.Audit.Add(new AuditEntry { Actor = "admin", Action = "login" });
            db.SaveChanges();
        }

        using (var db = Open())
        {
            Assert.Equal(FuelType.Diesel, db.Reports.Include(r => r.DayShift).Single().Fuel);
            Assert.Equal("احمد", db.Reports.Include(r => r.DayShift).Single().DayShift!.Name);
            Assert.Equal(2, db.TilCompanies.Include(c => c.Rows).Single().Rows.Count);
            Assert.Single(db.AmanatAccounts.Include(a => a.Rows).Single().Rows);
            var wq = db.WaraqEntries.Include(x => x.Shifts).ThenInclude(s => s.Pumps)
                                    .Include(x => x.Shifts).ThenInclude(s => s.Transactions).Single();
            Assert.Single(wq.Shifts);
            Assert.Single(wq.Shifts[0].Pumps);
            Assert.Single(wq.Shifts[0].Transactions);
            Assert.Single(db.Invoices);
            Assert.Single(db.Attendance);
            Assert.Single(db.SalaryPayments);
            Assert.Single(db.StaffShortages);
            Assert.Single(db.Cameras);
            Assert.Single(db.ExtraIncomes);
            Assert.Single(db.RateHistory);
            Assert.Single(db.Trash);
            Assert.Single(db.Settings);
            Assert.Single(db.Users);
            Assert.Single(db.Audit);
        }
    }

    [Fact]
    public void SoftDelete_HidesRowButKeepsIt()
    {
        using var db = Open();
        var c = new TilCompany { Name = "حذفی" };
        c.Rows.Add(new CompanyRow { Fuel = FuelType.Petrol, Ton = 1 });
        db.TilCompanies.Add(c);
        db.SaveChanges();

        db.TilCompanies.Remove(c);
        db.SaveChanges();

        Assert.Empty(db.TilCompanies);
        Assert.Empty(db.CompanyRows);                       // فیلترِ آبشاری هم کار می‌کند
        Assert.Single(db.TilCompanies.IgnoreQueryFilters()); // ولی رکورد سرِ جایش است
    }
}
