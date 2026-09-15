using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class FixedRates : IUnionRateProvider
{
    public decimal UnionRate(FuelType fuel) => 60m;
}

/// <summary>
/// ══ صفحهٔ جدول‌های آرشیوِ قرض‌دار ═════════════════════════════════════════
/// گزارشِ صاحب ریپو: «بخش قرض‌داران، آرشیو اون هم مشکل داره». آرشیو تا امروز
/// یک آکاردئونِ خواندنی بود؛ در سایت صفحهٔ جداگانه است با سربرگِ حسابِ زنده،
/// فیلترِ تیل و ردیف‌های ویرایش‌شدنی. این‌ها همان قاعده‌ها را قفل می‌کنند.
/// </summary>
public class DebtArchivePageTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-darc-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private static DebtRow Row(FuelType f, decimal liters, decimal fee, decimal rasidFuel = 0m, decimal rasid = 0m) =>
        new() { Fuel = f, Liters = liters, PricePerLiter = fee, Bardagi = liters * fee, RasidFuel = rasidFuel, Rasid = rasid };

    [Fact] // ‎_histFigures‎: رسید = سربرگ + رسیدهای جدول؛ فیصدی روی کلِ رسید؛ الباقی = برد + فیصدی − رسید
    public void ArchiveFigures_FollowTheSitesHeaderRule()
    {
        var calc = new DebtCalculationService(new FixedRates());
        var rows = new[] { Row(FuelType.Petrol, 100m, 60m, rasidFuel: 20m), Row(FuelType.Diesel, 50m, 60m) };
        var f = calc.ArchiveFigures(rows, money: false, pctP: 4m, pctD: 0m, hdrP: 30m, hdrD: 10m);

        Assert.Equal(30m, f.Petrol.HeaderRasid);
        Assert.Equal(20m, f.Petrol.RowRasid);
        Assert.Equal(50m, f.Petrol.Rasid);             // ۳۰ + ۲۰
        Assert.Equal(100m, f.Petrol.Bord);
        Assert.Equal(2m, f.Petrol.Comm);               // ۵۰ × ۴٪
        Assert.Equal(52m, f.Petrol.Rem);               // ۱۰۰ + ۲ − ۵۰
        Assert.Equal(10m, f.Diesel.Rasid);
        Assert.Equal(40m, f.Diesel.Rem);               // ۵۰ + ۰ − ۱۰
    }

    [Fact] // دفترِ پول: برد = بردگی و رسید = رسیدِ پولی
    public void ArchiveFigures_MoneyLedgerUsesBardagiAndMoneyReceipts()
    {
        var calc = new DebtCalculationService(new FixedRates());
        var rows = new[] { Row(FuelType.Petrol, 10m, 60m, rasid: 100m) };
        var f = calc.ArchiveFigures(rows, money: true, pctP: 0m, pctD: 0m, hdrP: 200m, hdrD: 0m);
        Assert.Equal(600m, f.Petrol.Bord);
        Assert.Equal(300m, f.Petrol.Rasid);
        Assert.Equal(300m, f.Petrol.Rem);
    }

    [Fact] // ویرایشِ آرشیو فقط خودِ آرشیو را عوض می‌کند و ماندگار است
    public async Task UpdateArchive_PersistsRowsHeaderAndNote_WithoutTouchingTheLiveTable()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var debtors = new DebtorService(dbf, perm, new TrashService(dbf, perm, session));

        var d = await debtors.AddDebtorAsync("کریم", null, false);
        var full = (await debtors.LoadFullAsync(d.Id))!;
        var acc = full.MainAccount;
        await debtors.SaveRowAsync(new DebtRow { FuelAccountId = acc.Id, DateShamsi = "1405/6/1", Liters = 50m, PricePerLiter = 60m });
        var h = await debtors.ArchiveTableAsync(acc.Id, "1405/6/25");
        await debtors.SaveRowAsync(new DebtRow { FuelAccountId = acc.Id, DateShamsi = "1405/6/26", Liters = 7m, PricePerLiter = 60m });

        var rows = DebtorService.ArchiveRows(h);
        rows[0].Liters = 55m;
        rows.Add(new DebtRow { DateShamsi = "1405/6/2", Liters = 5m, PricePerLiter = 60m, Fuel = FuelType.Diesel });
        h.PercentPetrol = 3m; h.RasidFuelPetrol = 12m; h.Note = "فی 56";
        await debtors.UpdateArchiveAsync(h, rows);

        var back = (await debtors.ListArchivesAsync(acc.Id)).Single();
        var backRows = DebtorService.ArchiveRows(back);
        Assert.Equal(2, back.RowCount);
        Assert.Equal(55m, backRows[0].Liters);
        Assert.Equal(FuelType.Diesel, backRows[1].Fuel);
        Assert.Equal(3m, back.PercentPetrol);
        Assert.Equal(12m, back.RasidFuelPetrol);
        Assert.Equal("فی 56", back.Note);

        // جدولِ زنده دست نخورده
        var live = (await debtors.LoadFullAsync(d.Id))!.MainAccount.FuelRows;
        Assert.Single(live);
        Assert.Equal(7m, live[0].Liters);
    }

    [Fact] // آرشیو صفحهٔ جداگانه است و همان سربرگ و فیلتر و ستون‌های حسابِ زنده را دارد؛ آکاردئونِ قدیمی رفته
    public void TheArchivePage_LooksLikeTheLiveAccount()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var v = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "DebtArchiveView.axaml"));
        foreach (var t in new[] { "⛽ حساب پطرول", "🟤 حساب دیزل", "HeadPetrolRasidEdit", "HeadDieselRasidEdit",
                                  "SetFilterCommand", "حساب جداگانه پطرول", "Header=\"مقدار بردگی\"", "Header=\"رسید تیل\"",
                                  "GrowsOnEnter=\"True\"", "RowAddBar", "DeleteCommand", "PercentPetrolText", "NoteText" })
            Assert.Contains(t, v);
        var person = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "PersonView.axaml"));
        Assert.DoesNotContain("IsArchiveOpen", person);
        Assert.Contains("ToggleArchivesCommand", person);
        var debt = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "DebtSectionView.axaml"));
        Assert.Contains("DebtArchiveView", debt);
    }
}
