using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>══ مخزن‌های شماره‌دار روی دیتابیسِ واقعی — جمع همان موجودیِ کل (۱۴۰۵/۰۷/۲۲) ══</summary>
public class FuelTankDataTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-ftank-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (StorageDataService Storage, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var companies = new CompanyDataService(dbf, perm, trash);
        var settings = new SettingsService(dbf, perm);
        return (new StorageDataService(dbf, perm, trash, new StorageService(), settings, companies), dbf);
    }

    private static async Task<long> Buy(PumpDbFactory dbf, string date, decimal liters)
    {
        await using var db = dbf.Create();
        var p = new FuelPurchase { Fuel = FuelType.Petrol, DateShamsi = date, DateKey = PumpYaqobi.Application.Localization.Shamsi.Key(date), Liters = liters };
        db.FuelPurchases.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static async Task Sell(PumpDbFactory dbf, string date, decimal sale)
    {
        await using var db = dbf.Create();
        db.Reports.Add(new ParchaReport
        {
            Fuel = FuelType.Petrol, DateShamsi = date, DateKey = PumpYaqobi.Application.Localization.Shamsi.Key(date),
            DayShift = new ShiftData { Start = 0, End = sale, Sale = sale },
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TaghsimKharid_KeshidanAzAvvali_JamBaMojudiKol()
    {
        var (s, dbf) = Host();
        Assert.Empty(await s.TankLevelsAsync(FuelType.Petrol));               // بی مخزن ⇒ همان همیشه
        Assert.Null(await s.SaveTankAsync(new FuelTank { Fuel = FuelType.Petrol, Num = 1, Capacity = 10000m }));
        Assert.Null(await s.SaveTankAsync(new FuelTank { Fuel = FuelType.Petrol, Num = 2, Capacity = 8000m }));
        Assert.NotNull(await s.SaveTankAsync(new FuelTank { Fuel = FuelType.Petrol, Num = 2, Capacity = 1m }));   // تکراری
        var tanks = await s.TanksAsync(FuelType.Petrol);

        var p1 = await Buy(dbf, "1405/07/01", 9000m);
        Assert.NotNull(await s.SetFillsAsync(p1, new Dictionary<long, decimal> { [tanks[0].Id] = 6000m, [tanks[1].Id] = 4000m }));   // بیشتر از خرید
        Assert.Null(await s.SetFillsAsync(p1, new Dictionary<long, decimal> { [tanks[0].Id] = 6000m, [tanks[1].Id] = 3000m }));
        var p2 = await Buy(dbf, "1405/07/03", 500m);                            // تقسیم‌نشده ⇒ مخزنِ ۱
        await Sell(dbf, "1405/07/02", 6500m);

        var lv = await s.TankLevelsAsync(FuelType.Petrol);
        Assert.Equal(500m, lv[0].Current);                                      // ۶۰۰۰ − ۶۰۰۰ (تمام شد) + ۵۰۰
        Assert.Equal(2500m, lv[1].Current);                                     // ۳۰۰۰ − ۵۰۰ (بقیهٔ فروش)
        Assert.True(lv[0].Active);

        await using var db = dbf.Create();
        var purchases = await db.FuelPurchases.AsNoTracking().ToListAsync();
        var reports = await db.Reports.Include(r => r.DayShift).Include(r => r.NightShift).AsNoTracking().ToListAsync();
        var total = new StorageService().Tank(purchases, reports, 0m).Current;
        Assert.Equal(total, lv.Sum(x => x.Current));                            // ⛔ جمعِ مخزن‌ها = موجودیِ کل

        await s.DeleteTankAsync(tanks[1].Id);
        lv = await s.TankLevelsAsync(FuelType.Petrol);
        Assert.Single(lv);
        Assert.Equal(total, lv[0].Current);                                     // سهمِ مخزنِ حذف‌شده به اولی برگشت
    }
}
