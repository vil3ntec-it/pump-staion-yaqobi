using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class NoRate : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// دفترهای کوچکِ ابزارها روی یک دیتابیسِ واقعی: تخلیهٔ تانکر، تسویهٔ کمبودی،
/// تاریخچهٔ نرخ — و زنجیرهٔ «پرداختِ معاش ← مصرف».
/// </summary>
public class ToolsServiceTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-tools-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (ToolsDataService Tools, AttendanceDataService Att, PumpDbFactory Db) Host(
        UserRole role = UserRole.Admin)
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(role, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var calc = new DebtCalculationService(new NoRate());
        var tools = new ToolsDataService(dbf, perm, trash, new AgingService(calc),
                                         new StaffShortService(new WaraqService()),
                                         new MonthReportService());
        return (tools, new AttendanceDataService(dbf, perm, trash), dbf);
    }

    // ── تخلیهٔ تانکر ───────────────────────────────────────────────────────
    [Fact]
    public async Task Unload_NeedsBothManifestAndActual()
    {
        var (tools, _, _) = Host();
        Assert.Null(await tools.AddUnloadAsync(FuelType.Diesel, 0m, 19000m, null, null));
        Assert.Null(await tools.AddUnloadAsync(FuelType.Diesel, 20000m, 0m, null, null));
        Assert.Empty(await tools.UnloadsAsync());
    }

    [Fact]
    public async Task Unload_KeepsTheShortfall_AndCanBeDeleted()
    {
        var (tools, _, _) = Host();
        var u = await tools.AddUnloadAsync(FuelType.Diesel, 20000m, 19940m, "کریم", "شب");
        Assert.NotNull(u);
        Assert.Equal(-60m, TankDipService.UnloadDifference(u!));

        Assert.Single(await tools.UnloadsAsync());
        await tools.DeleteUnloadAsync(u!.Id);
        Assert.Empty(await tools.UnloadsAsync());
    }

    /// <summary>کارمند نباید بتواند تخلیه ثبت کند — همان ‎_toolsAdminOnly‎.</summary>
    [Fact]
    public async Task Unload_IsManagerOnly()
    {
        var (tools, _, _) = Host(UserRole.Staff);
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => tools.AddUnloadAsync(FuelType.Petrol, 100m, 100m, null, null));
    }

    // ── تاریخچهٔ نرخ ───────────────────────────────────────────────────────
    [Fact]
    public async Task RateHistory_OnlyRecordsRealChanges()
    {
        var (tools, _, _) = Host();
        Assert.True(await tools.RecordRateAsync(FuelType.Petrol, 62m));
        Assert.False(await tools.RecordRateAsync(FuelType.Petrol, 62m));   // همان نرخ
        Assert.False(await tools.RecordRateAsync(FuelType.Petrol, 0m));    // نرخِ صفر
        Assert.True(await tools.RecordRateAsync(FuelType.Diesel, 60m));    // سوختِ دیگر
        Assert.True(await tools.RecordRateAsync(FuelType.Petrol, 64m));

        var hist = await tools.RateHistoryAsync();
        Assert.Equal(3, hist.Count);
        Assert.Equal(64m, hist[0].Rate);                                   // تازه‌ترین اول
        Assert.Equal(FuelType.Petrol, hist[0].Fuel);
    }

    // ── تسویهٔ کمبودی ──────────────────────────────────────────────────────
    private static StaffShortRow RowOf(decimal shortRemain, decimal excessRemain) =>
        new("کریم", "کریم", 2, shortRemain, excessRemain, 0m, 0m, shortRemain, excessRemain);

    [Fact]
    public async Task Settle_RejectsMoreThanTheRemainder()
    {
        var (tools, _, _) = Host();
        Assert.False(await tools.SettleAsync(RowOf(5000m, 0m), StaffSettleKind.Short, 5001m));
        Assert.False(await tools.SettleAsync(RowOf(5000m, 0m), StaffSettleKind.Short, 0m));
        Assert.Empty(await tools.SettlesAsync());

        Assert.True(await tools.SettleAsync(RowOf(5000m, 0m), StaffSettleKind.Short, 5000m));
        var list = await tools.SettlesAsync();
        Assert.Single(list);
        Assert.Equal(StaffSettleKind.Short, list[0].Kind);
        Assert.Equal("کریم", list[0].NameKey);
    }

    [Fact]
    public async Task Settle_ReducesTheRemainder_AndComesBackWhenDeleted()
    {
        var (tools, _, dbf) = Host();

        // یک ورق که در پارچه‌اش ۵٬۰۰۰ قرض نوشته شده ولی در جدولِ ورق هیچ
        // ردیفی پوششش نمی‌دهد — یعنی کمبودیِ ۵٬۰۰۰ برای همان کارمند.
        await using (var db = dbf.Create())
        {
            var w = new WaraqEntry { DateShamsi = "1405/06/10", DateKey = 14050610 };
            var sh = new WaraqShift { Kind = ShiftKind.Day, WorkerName = "کریم" };
            sh.Pumps.Add(new WaraqPump { Num = 1, Fuel = FuelType.Petrol, Debt = 5000m });
            w.Shifts.Add(sh);
            db.WaraqEntries.Add(w);
            await db.SaveChangesAsync();
        }

        var before = await tools.StaffShortAsync();
        Assert.Single(before);
        Assert.Equal(5000m, before[0].RemainShort);

        Assert.True(await tools.SettleAsync(before[0], StaffSettleKind.Short, 2000m));
        var mid = await tools.StaffShortAsync();
        Assert.Equal(3000m, mid[0].RemainShort);

        var settle = (await tools.SettlesAsync())[0];
        await tools.DeleteSettleAsync(settle.Id);
        var after = await tools.StaffShortAsync();
        Assert.Equal(5000m, after[0].RemainShort);
    }

    // ── پرداختِ معاش ← مصرف ────────────────────────────────────────────────
    /// <summary>
    /// زنجیرهٔ ‎attPaySalary‎: پرداختِ معاش باید در «مصارف» هم بنشیند، وگرنه
    /// معاش‌ها در مفاد/ضرر و گزارشِ ماه اصلاً دیده نمی‌شوند.
    /// </summary>
    [Fact]
    public async Task PayingSalary_AlsoWritesExactlyOneExpense()
    {
        var (_, att, dbf) = Host();
        var staff = await att.AddStaffAsync("نصیر", 12000m);

        Assert.True(await att.PaySalaryAsync(staff.Id, "1405/06", 12000m));
        Assert.False(await att.PaySalaryAsync(staff.Id, "1405/06", 12000m));   // دوباره نه

        await using var db = dbf.Create();
        var exp = await db.Expenses.Where(e => e.SalaryStaffId == staff.Id).ToListAsync();
        Assert.Single(exp);
        Assert.Equal(12000m, exp[0].Amount);
        Assert.Equal("معاش نصیر", exp[0].Title);
        Assert.Equal("1405/06", exp[0].SalaryMonth);
        Assert.Equal("پرداخت معاش سنبله 1405", exp[0].Note);
        Assert.Equal(Shamsi.MonthKey(exp[0].DateShamsi), exp[0].MonthKey);

        // ماهِ دیگر مصرفِ خودش را دارد
        Assert.True(await att.PaySalaryAsync(staff.Id, "1405/07", 12000m));
        Assert.Equal(2, await db.Expenses.CountAsync(e => e.SalaryStaffId == staff.Id));
    }
}
