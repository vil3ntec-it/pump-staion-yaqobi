using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Infrastructure.Migration;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class ImportRates : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// ══ بندِ ۳۱: مهاجرتِ کاملِ دادهٔ نسخهٔ وب ═════════════════════════════════════
/// ‎legacy-full-backup.json‎ یک بکاپِ کوچک ولی **کامل** است — از هر جدولی چند
/// رکورد — و ‎legacy-full-expected.json‎ عددهایی که **خودِ برنامهٔ وب** از
/// همان داده درآورده.
///
/// این آزمون بکاپ را وارد SQLite می‌کند و بعد همان عددها را از روی دیتابیس
/// دوباره حساب می‌کند. اگر ذره‌ای در راه گم شود، یا نامِ فیلدی اشتباه خوانده
/// شود، عددها نمی‌خوانند و آزمون قرمز می‌شود.
/// </summary>
public class LegacyFullImportTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-full-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var f in Directory.GetFiles(Path.GetTempPath(),
                     Path.GetFileNameWithoutExtension(_file) + "*"))
            try { File.Delete(f); } catch { }
    }

    private static string Fixture(string n)
    {
        var p = Path.Combine(AppContext.BaseDirectory, n);
        return File.Exists(p) ? p : n;
    }

    private static string Json() => File.ReadAllText(Fixture("legacy-full-backup.json"));

    private sealed record MFuel(double a, double l, double p, int c);
    private sealed record MBuyJ(double l, double a);
    private sealed record MonthJ(MFuel petrol, MFuel diesel, double exp, double extra,
                                 MBuyJ buyP, MBuyJ buyD, double rasid, int rasidC,
                                 double sin, double sout, int tkCount, double tkShort,
                                 double sales, double liters, double profit, double net);
    private sealed record WaraqTot(double petrolL, double dieselL, double sales,
                                   double debt, double expenses, double declaredDebt);
    private sealed record ShortJ(double shortage, double excess, double declared, double covered);
    private sealed record FiguresJ(bool money, double bardagi, double rasid, double albaqi);
    private sealed record PersonFig(string id, FiguresJ f);
    private sealed record AgingJ(string id, double albaqi, int days);
    private sealed record StaffJ(string name, int shifts, double remainShort, double remainExcess);
    private sealed record Expected(int recordCount, double stockPetrol, double stockDiesel,
                                   List<string> monthKeys, MonthJ month, WaraqTot waraqDay,
                                   ShortJ waraqShort, List<PersonFig> figures, List<AgingJ> aging,
                                   List<StaffJ> staffShort, string today);

    private static Expected Want() =>
        JsonSerializer.Deserialize<Expected>(File.ReadAllText(Fixture("legacy-full-expected.json")))!;

    private static void Close(double expected, decimal actual, double tol = 1e-6)
    {
        var a = (double)actual;
        Assert.True(Math.Abs(expected - a) <= Math.Max(tol, Math.Abs(expected) * 1e-9),
                    $"انتظار {expected} بود، {a} آمد");
    }

    private LegacyImportService Service(UserRole role = UserRole.Admin)
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(role, "آزمون");
        var perm = new PermissionService(session);
        return new LegacyImportService(dbf, perm, new SettingsService(dbf, perm));
    }

    private PumpDbFactory Factory()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        return dbf;
    }

    // ── ۱) شمارش ────────────────────────────────────────────────────────────
    [Fact]
    public void TheRecordCount_MatchesTheWebAppsOwnCount()
    {
        Assert.Equal(Want().recordCount, LegacyOperationsImporter.RecordCount(Json()));
    }

    [Fact]
    public async Task Importing_BringsEveryRecordAcross()
    {
        var svc = Service();
        var res = await svc.ImportAsync(Json());

        Assert.True(res.Ok, res.Message);
        Assert.Empty(res.Warnings);
        Assert.Equal(Want().recordCount, res.SourceRecords);
        Assert.Equal(res.SourceRecords, res.ImportedRecords);

        await using var db = Factory().Create();
        Assert.Equal(3, await db.Reports.CountAsync());          // ۲ پطرول + ۱ دیزل
        Assert.Equal(3, await db.Debtors.CountAsync());          // ۲ قرض‌دار + ۱ بی‌فاکتور
        Assert.Equal(2, await db.FuelPurchases.CountAsync());
        Assert.Equal(2, await db.TilCompanies.CountAsync());
        Assert.Equal(2, await db.CompanyRows.CountAsync());
        Assert.Equal(1, await db.WaraqEntries.CountAsync());
        Assert.Equal(2, await db.WaraqShifts.CountAsync());
        Assert.Equal(2, await db.Invoices.CountAsync());
        Assert.Equal(1, await db.AmanatAccounts.CountAsync());
        Assert.Equal(1, await db.AmanatRows.CountAsync());
        Assert.Equal(2, await db.StaffMembers.CountAsync());
        Assert.Equal(2, await db.Attendance.CountAsync());
        Assert.Equal(1, await db.SalaryPayments.CountAsync());
        Assert.Equal(1, await db.StaffShortSettles.CountAsync());
        Assert.Equal(1, await db.Cameras.CountAsync());
        Assert.Equal(1, await db.TankDips.CountAsync());
        Assert.Equal(1, await db.TankerUnloads.CountAsync());
        Assert.Equal(2, await db.RateHistory.CountAsync());
        Assert.Equal(1, await db.ExtraIncomes.CountAsync());
        Assert.Equal(1, await db.ParchaReceipts.CountAsync());
        Assert.Equal(1, await db.DebtQuickReceipts.CountAsync());
    }

    // ── ۲) عددها ────────────────────────────────────────────────────────────
    /// <summary>موجودیِ مخزن — همان عددی که ‎_fuelStock‎ در مرورگر داد.</summary>
    [Fact]
    public async Task TheTankStock_MatchesTheWebApp()
    {
        await Service().ImportAsync(Json());
        await using var db = Factory().Create();

        var storage = new StorageService();
        foreach (var (fuel, want) in new[]
                 { (FuelType.Petrol, Want().stockPetrol), (FuelType.Diesel, Want().stockDiesel) })
        {
            var buys = await db.FuelPurchases.Where(p => p.Fuel == fuel).ToListAsync();
            var reports = await db.Reports.Include(r => r.DayShift).Include(r => r.NightShift)
                                  .Where(r => r.Fuel == fuel).ToListAsync();
            var dips = await db.TankDips.Where(d => d.Fuel == fuel).ToListAsync();
            Close(want, storage.Tank(buys, reports, 1000m, dips).Current);
        }
    }

    /// <summary>گزارشِ ماه — پانزده عدد که از نُه جدولِ جدا جمع می‌شوند.</summary>
    [Fact]
    public async Task TheMonthReport_MatchesTheWebApp()
    {
        await Service().ImportAsync(Json());
        await using var db = Factory().Create();

        var src = new MonthReportSource(
            await db.Reports.Include(r => r.DayShift).Include(r => r.NightShift).ToListAsync(),
            await db.Expenses.ToListAsync(),
            await db.ExtraIncomes.ToListAsync(),
            await db.FuelPurchases.ToListAsync(),
            await db.DebtQuickReceipts.ToListAsync(),
            await db.SafeEntries.ToListAsync(),
            await db.TankerUnloads.ToListAsync());

        var svc = new MonthReportService();
        Assert.Equal(Want().monthKeys, svc.AllKeys(src, Want().today));

        var got = svc.Compute(src, Want().monthKeys[0]);
        var w = Want().month;
        Close(w.petrol.a, got.Petrol.Amount); Close(w.petrol.l, got.Petrol.Liters);
        Close(w.petrol.p, got.Petrol.Profit); Assert.Equal(w.petrol.c, got.Petrol.Parcha);
        Close(w.diesel.a, got.Diesel.Amount); Close(w.diesel.l, got.Diesel.Liters);
        Close(w.diesel.p, got.Diesel.Profit); Assert.Equal(w.diesel.c, got.Diesel.Parcha);
        Close(w.exp, got.Expenses); Close(w.extra, got.Extra);
        Close(w.buyP.l, got.BuyPetrol.Liters); Close(w.buyP.a, got.BuyPetrol.Amount);
        Close(w.buyD.l, got.BuyDiesel.Liters); Close(w.buyD.a, got.BuyDiesel.Amount);
        Close(w.rasid, got.Rasid); Assert.Equal(w.rasidC, got.RasidCount);
        Close(w.sin, got.SafeBardagi); Close(w.sout, got.SafeMandagi);
        Assert.Equal(w.tkCount, got.TankerCount); Close(w.tkShort, got.TankerShort);
        Close(w.sales, got.Sales); Close(w.liters, got.Liters);
        Close(w.profit, got.Profit); Close(w.net, got.Net);
    }

    /// <summary>ورقِ روزانه — پایه‌ها و ردیف‌های قرض/مصرف سالم آمده باشند.</summary>
    [Fact]
    public async Task TheDailySheet_MatchesTheWebApp()
    {
        await Service().ImportAsync(Json());
        await using var db = Factory().Create();

        var w = await db.WaraqEntries
            .Include(x => x.Shifts).ThenInclude(s => s.Pumps)
            .Include(x => x.Shifts).ThenInclude(s => s.Transactions)
            .FirstAsync();
        var day = w.Shifts.First(s => s.Kind == ShiftKind.Day);

        var svc = new WaraqService();
        var t = svc.ShiftTotals(day);
        var want = Want().waraqDay;
        Close(want.petrolL, t.PetrolLiters);
        Close(want.dieselL, t.DieselLiters);
        Close(want.sales, t.Sales);
        Close(want.debt, t.Debt);
        Close(want.expenses, t.Expenses);
        Close(want.declaredDebt, t.DeclaredDebt);

        var sh = svc.Shortage(t);
        Close(Want().waraqShort.shortage, sh.Shortage);
        Close(Want().waraqShort.excess, sh.Excess);
    }

    /// <summary>الباقیِ هر قرض‌دار و سنِ قرضش.</summary>
    [Fact]
    public async Task TheDebtorNumbers_MatchTheWebApp()
    {
        await Service().ImportAsync(Json());
        await using var db = Factory().Create();

        var people = await db.Debtors.Where(d => !d.IsNoInvoice)
            .Include(d => d.MainAccount).ThenInclude(a => a!.FuelRows)
            .Include(d => d.MainAccount).ThenInclude(a => a!.MoneyRows)
            .Include(d => d.SubAccounts).ThenInclude(a => a.FuelRows)
            .Include(d => d.SubAccounts).ThenInclude(a => a.MoneyRows)
            .ToListAsync();

        var aging = new AgingService(new DebtCalculationService(new ImportRates()));

        foreach (var want in Want().figures)
        {
            var p = people.First(x => x.LegacyId == want.id);
            var f = aging.Figures(p);
            Assert.Equal(want.f.money, f.IsMoney);
            Close(want.f.bardagi, f.Bardagi);
            Close(want.f.rasid, f.Rasid);
            Close(want.f.albaqi, f.Albaqi);
        }

        var rows = aging.Rows(people, AgingFilter.All, Want().today);
        Assert.Equal(Want().aging.Count, rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            Assert.Equal(Want().aging[i].id, rows[i].Person.LegacyId);
            Assert.Equal(Want().aging[i].days, rows[i].DaysIdle);
        }
    }

    /// <summary>کمبودی/اضافیِ کارمندان — از ورقِ واردشده.</summary>
    [Fact]
    public async Task TheStaffShortage_MatchesTheWebApp()
    {
        await Service().ImportAsync(Json());
        await using var db = Factory().Create();

        var entries = await db.WaraqEntries
            .Include(x => x.Shifts).ThenInclude(s => s.Pumps)
            .Include(x => x.Shifts).ThenInclude(s => s.Transactions).ToListAsync();
        var settles = await db.StaffShortSettles.ToListAsync();

        var got = new StaffShortService(new WaraqService()).Rows(entries, settles);
        var want = Want().staffShort;
        Assert.Equal(want.Count, got.Count);
        for (var i = 0; i < want.Count; i++)
        {
            Assert.Equal(want[i].name, got[i].Name);
            Assert.Equal(want[i].shifts, got[i].Shifts);
            Close(want[i].remainShort, got[i].RemainShort);
            Close(want[i].remainExcess, got[i].RemainExcess);
        }
    }

    /// <summary>تنظیم‌ها هم می‌آیند — بی نرخِ اتحادیه، مفاد غلط می‌شود.</summary>
    [Fact]
    public async Task TheSettings_ComeAcrossToo()
    {
        var svc = Service();
        await svc.ImportAsync(Json());

        var dbf = Factory();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var settings = new SettingsService(dbf, new PermissionService(session));

        Assert.Equal(63m, settings.UnionRate(FuelType.Petrol));
        Assert.Equal(59m, settings.UnionRate(FuelType.Diesel));
        Assert.Equal("پمپ یعقوبی", settings.GetString(SettingsService.StationName));
        Assert.Equal(36.75m, settings.GetDecimal(SettingsService.BuyPerLiterPetrol));
    }

    // ── ۳) بی‌خطری ─────────────────────────────────────────────────────────
    /// <summary>دادهٔ موجود بی تاییدِ صریح بازنویسی نمی‌شود.</summary>
    [Fact]
    public async Task ExistingData_IsNotOverwrittenSilently()
    {
        var svc = Service();
        Assert.True((await svc.ImportAsync(Json())).Ok);

        var again = await svc.ImportAsync(Json());
        Assert.False(again.Ok);
        Assert.Contains("خالی نیست", again.Message);

        // با تاییدِ صریح، جای قبلی را می‌گیرد — نه اینکه دو برابر شود
        var replaced = await svc.ImportAsync(Json(), replaceExisting: true);
        Assert.True(replaced.Ok, replaced.Message);
        await using var db = Factory().Create();
        Assert.Equal(3, await db.Debtors.CountAsync());
    }

    [Fact]
    public async Task AFileThatIsNotABackup_IsRefused()
    {
        var svc = Service();
        var res = await svc.ImportAsync("{\"چیزی\":1}");
        Assert.False(res.Ok);
        Assert.Contains("بکاپِ این برنامه نیست", res.Message);
    }

    [Fact]
    public async Task BrokenJson_IsRefusedWithoutThrowing()
    {
        var res = await Service().ImportAsync("{ نه یک جیسون }");
        Assert.False(res.Ok);
        Assert.Contains("خوانده نشد", res.Message);
    }

    /// <summary>پیش از مهاجرت، بکاپِ خودکار گرفته می‌شود.</summary>
    [Fact]
    public async Task ABackupIsTaken_BeforeAnythingIsWritten()
    {
        var svc = Service();
        var res = await svc.ImportAsync(Json());
        Assert.True(res.Ok, res.Message);
        Assert.NotNull(res.BackupPath);
        Assert.True(File.Exists(res.BackupPath!), "فایلِ بکاپ ساخته نشد");
    }

    [Fact]
    public async Task OnlySomeoneWithImportRights_CanMigrate()
    {
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => Service(UserRole.Staff).ImportAsync(Json()));
    }
}
