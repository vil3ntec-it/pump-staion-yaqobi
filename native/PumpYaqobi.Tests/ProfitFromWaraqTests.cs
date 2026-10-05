using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ مفاد و ضرر از ورق‌ها، و ضررِ نرخِ فاکتورهای در صف — ۱۴۰۵/۰۷/۲۰ ══════════
/// صاحب ریپو: «نه از پارچه؛ همون قد لیتری که فروخته شده، پولِ همون لیتر بیاد…
/// دیزل و پطرول هر دو جمع بشن… فاکتورهای در صف با نرخِ امروز مقایسه بشن، تاییدشده‌ها
/// نه، و یک کادر که ضررها دریافت شد.» همه روی SQLiteِ واقعی.
/// </summary>
[Collection(OpLogCollection.Name)]
public class ProfitFromWaraqTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-pfw-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private (PumpDbFactory Db, ParchaDataService Parcha, WaraqDataService Waraq, InvoiceService Inv) Make()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var sync = new ShiftWaraqSyncService(dbf, perm, new WaraqService(), new SettingsService(dbf, perm));
        return (dbf, new ParchaDataService(dbf, perm, trash, new ParchaService(), sync),
                new WaraqDataService(dbf, perm, trash),
                new InvoiceService(dbf, perm, trash, new DebtorService(dbf, perm, trash)));
    }

    [Fact]
    public void NarkheFactorDarSaf_BaNarkheEmruz_ZararYaMofad()
    {
        var rows = new[]
        {
            new PendingRate(FuelType.Petrol, 100m, 60m, 0),   // امروز 65 ⇒ ضرر 500
            new PendingRate(FuelType.Diesel, 50m, 90m, 0),    // امروز 80 ⇒ مفاد 500
        };
        Assert.Equal(0m, ProfitLossService.PendingRateDiff(rows, 65m, 80m));
        Assert.Equal(500m, ProfitLossService.PendingRateDiff(rows.Take(1), 65m, 80m));
        Assert.Equal(-500m, ProfitLossService.PendingRateDiff(rows.Skip(1), 65m, 80m));
        Assert.Equal(0m, ProfitLossService.PendingRateDiff(rows, 0m, 0m));   // نرخِ امروز نیست ⇒ نمی‌شمرد
    }

    [Fact]
    public async Task Fayede_PuleLitereForukhteShodeyeVaraq_PetrolVaDiesel()
    {
        var h = Make();
        Assert.True((await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(FuelType.Petrol, ShiftKind.Day,
            "1405/07/10", "کریم", 1, 0m, 100m, 60m, 0m, 0m, 0m, "", 0m, false))).Ok);
        Assert.True((await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(FuelType.Diesel, ShiftKind.Night,
            "1405/07/10", "رحیم", 2, 0m, 50m, 80m, 0m, 0m, 0m, "", 0m, false))).Ok);
        Assert.True((await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(FuelType.Petrol, ShiftKind.Day,
            "1405/08/01", "کریم", 1, 100m, 110m, 60m, 0m, 0m, 0m, "", 0m, false))).Ok);

        var k = Shamsi7();
        var lines = await h.Waraq.SalesLinesAsync(k);
        Assert.Equal(2, lines.Count);                                   // روز و شبِ ۱۴۰۵/۰۷/۱۰
        Assert.Equal(6000m, lines.Sum(l => l.PetrolMoney));
        Assert.Equal(4000m, lines.Sum(l => l.DieselMoney));
        Assert.Equal(100m, lines.Sum(l => l.PetrolLiters));

        //  ⛔ فایدهٔ پارچه صفر است (بی قیمتِ خرید) — ولی فروش در مفاد و ضرر می‌آید
        var r = ProfitLossService.Compute(new ProfitInput
        {
            ShiftProfitPetrol = 0m, ShiftProfitDiesel = 0m,
            WaraqSalesPetrol = lines.Sum(l => l.PetrolMoney), WaraqSalesDiesel = lines.Sum(l => l.DieselMoney),
            NoInvoiceSum = 0m, ExtraIncomeSum = 0m, ExpenseSum = 1000m, InvoiceRateDiffSum = 0m,
        });
        Assert.Equal(10000m, r.Income);
        Assert.Equal(9000m, r.Net);

        Assert.Equal(3, (await h.Waraq.SalesLinesAsync(null)).Count);   // همهٔ زمان‌ها
    }

    private static (int, int) Shamsi7() => (14050701, 14050799);

    [Fact]
    public async Task FaktoreTaiidShode_VaDaryaftShode_DarZararNistand()
    {
        var h = Make();
        await using (var db = h.Db.Create())
        {
            db.Invoices.Add(new Invoice { InvoiceNumber = 1, Status = InvoiceStatus.Pending, Liters = 100m, PricePerLiter = 60m, RateOnCreate = 60m, DateKey = 14050710 });
            db.Invoices.Add(new Invoice { InvoiceNumber = 2, Status = InvoiceStatus.Approved, Liters = 100m, PricePerLiter = 60m, RateOnCreate = 60m, RateOnApprove = 70m, DateKey = 14050710 });
            db.Invoices.Add(new Invoice { InvoiceNumber = 3, Status = InvoiceStatus.Pending, Liters = 10m, PricePerLiter = 60m, ByMoney = true, DateKey = 14050710 });
            db.Invoices.Add(new Invoice { InvoiceNumber = 4, Status = InvoiceStatus.Pending, Liters = 40m, PricePerLiter = 60m, DateKey = 14050710 });
            await db.SaveChangesAsync();
        }
        var rows = await h.Inv.PendingRatesAsync();
        Assert.Equal(2, rows.Count);                                    // ۱ و ۴ — نه تاییدشده، نه پولی
        Assert.Equal(700m, ProfitLossService.PendingRateDiff(rows, 65m, 0m));

        long id4;
        await using (var db = h.Db.Create()) id4 = (await db.Invoices.FirstAsync(v => v.InvoiceNumber == 4)).Id;
        await h.Inv.SetRateDiffReceivedAsync(id4, true);                // «ضرر دریافت شد»
        rows = await h.Inv.PendingRatesAsync();
        Assert.Single(rows);
        Assert.Equal(500m, ProfitLossService.PendingRateDiff(rows, 65m, 0m));
    }
}
