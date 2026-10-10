using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ لیترِ آزمایشیِ پمپ (۱۴۰۵/۰۷/۲۲) ══
/// خواستهٔ صاحب ریپو: «گرفتنِ تیلِ آزمایشی که نه قرض حساب بشه نه مصرف.» تیلی که در پیمانه
/// ریخته و به مخزن برگشت از فروشِ همان پایه کم می‌شود: پول و فایده‌اش حساب نمی‌شود، از
/// موجودیِ مخزن کم نمی‌شود، و در ورق ستونِ «آزمایشی» دارد. صفر ⇒ همان رفتارِ همیشه.
/// </summary>
public class TestLitersTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-testl-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (ParchaDataService Parcha, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var sync = new ShiftWaraqSyncService(dbf, perm, new WaraqService(), settings);
        return (new ParchaDataService(dbf, perm, trash, new ParchaService(), sync, () => "1405/07/01"), dbf);
    }

    private static Task<ShiftSaveResult> Save(ParchaDataService p, decimal start, decimal end, decimal test) =>
        p.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, "1405/07/01", "احمد", 1,
            start, end, 60m, 0m, 0m, 0m, "", 50m, true, false, test));

    [Fact]
    public void Forosh_KhatmMenhaShoruMenhaAzmayeshi()
    {
        Assert.Equal(80m, ParchaService.SoldLiters(1000m, 1100m, 20m));
        Assert.Equal(100m, ParchaService.SoldLiters(1000m, 1100m));          // بی آزمایشی همان همیشه
        var n = new ParchaService().CalcShift(1000m, 1100m, 60m, 0m, 50m, 0m, 20m);
        Assert.Equal(80m, n.Sale);
        Assert.Equal(4800m, n.Money);        // ۸۰ × ۶۰ — بیستِ آزمایشی پول ندارد
        Assert.Equal(800m, n.Profit);        // ۸۰ × (۶۰ − ۵۰)
    }

    [Fact]
    public async Task Zakhire_RuyePaarcha_VaVaraq_VaMakhzan()
    {
        var (p, dbf) = Host();
        var r = await Save(p, 1000m, 1100m, 20m);
        Assert.True(r.Ok, r.Error);
        Assert.Equal(80m, r.Shift!.Sale);
        Assert.Equal(20m, r.Shift.TestLiters);
        Assert.Equal(4800m, r.Shift.Money);

        await using var db = dbf.Create();
        var pump = await db.WaraqPumps.Include(x => x.Shift).SingleAsync();
        Assert.Equal(20m, pump.TestLiters);                                   // ورق ستونِ خودش را دارد
        var totals = new WaraqService().ShiftTotals(pump.Shift!);
        Assert.Equal(80m, totals.PetrolLiters);                               // نه ۱۰۰
        Assert.Equal(4800m, totals.Sales);

        //  مخزن: فروش ۸۰ است، پس بیستِ برگشته از موجودی کم نمی‌شود
        var reports = await db.Reports.Include(x => x.DayShift).Include(x => x.NightShift).ToListAsync();
        var tank = new StorageService().Tank(Array.Empty<FuelPurchase>(), reports, 0m);
        Assert.Equal(80m, tank.Out);
    }

    [Fact]
    public async Task AzmayeshiBishtarAzLitrPaaye_RadMishavad()
    {
        var (p, _) = Host();
        Assert.False((await Save(p, 1000m, 1010m, 20m)).Ok);
        Assert.False((await Save(p, 1000m, 1100m, -5m)).Ok);
        Assert.True((await Save(p, 1000m, 1100m, 100m)).Ok);                 // همهٔ لیتر آزمایشی ⇒ فروش صفر
    }

    [Fact]
    public async Task VirayeshAzTarikhche_AzmayeshiRaNegahMidarad()
    {
        var (p, dbf) = Host();
        var r = await Save(p, 1000m, 1100m, 20m);
        var e = await p.EditShiftAsync(r.Report!.Id, ShiftKind.Day, "احمد", 1, 1000m, 1150m, 60m, 0m);
        Assert.True(e.Ok, e.Error);
        await using var db = dbf.Create();
        var id = r.Report.Id;
        var s = await db.Reports.Include(x => x.DayShift).Where(x => x.Id == id).Select(x => x.DayShift!).SingleAsync();
        Assert.Equal(20m, s.TestLiters);
        Assert.Equal(130m, s.Sale);                                           // ۱۵۰ − ۲۰
        Assert.False((await p.EditShiftAsync(r.Report.Id, ShiftKind.Day, "احمد", 1, 1000m, 1010m, 60m, 0m)).Ok);
    }
}
