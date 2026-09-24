using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ تاریخچه‌ها با ستون‌های خودِ هر بخش (۱۴۰۵/۰۷/۱۲) ═════════════════════════
///
/// پارچه‌ها: کارمند · روز/شب · شمارهٔ پایه · شروع · ختم.
/// ورق‌ها: جملهٔ هر شیفت — نه ردیف‌به‌ردیف.
/// صرافی: تحویل به صرافی یا بردگیِ پمپ · مبلغ · فی · دالر.
/// شرکت‌ها: بردگیِ پمپ از شرکت و رسیدِ پمپ = بردگیِ شرکت، **با ارزش**.
///
/// ⛔ عددها باید همان عددهای سرویسِ خودِ بخش باشند، نه عددِ دوم.
/// </summary>
public class HistoryColumnsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-histcol-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    private (HistoryService H, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var settings = new SettingsService(dbf, perm);
        var trash = new TrashService(dbf, perm, session);
        var amanatData = new AmanatDataService(dbf, perm, trash, settings);
        return (new HistoryService(dbf, perm, new ExchangeService(), new RetailService(),
                                   new CompanyService(), new AmanatService(), amanatData, new WaraqService()), dbf);
    }

    [Fact]
    public void HarBakhsheKhas_SotoonhayeKhodash_RaDarad()
    {
        foreach (var k in new[] { "shift", "waraq", "sarrafi", "company" })
            Assert.NotNull(HistoryService.ColumnsOf(k));
        //  مصارف همان جدولِ کلی است — صاحب ریپو گفت درست است؛ فقط اسکرولش باگ داشت
        Assert.Null(HistoryService.ColumnsOf("expense"));
        var shift = HistoryService.ColumnsOf("shift")!.Select(c => c.Header).ToList();
        foreach (var h in new[] { "کارمند", "شیفت", "شمارهٔ پایه", "شروعِ پایه", "ختمِ پایه" })
            Assert.Contains(h, shift);
    }

    [Fact]
    public async Task Parcha_Karmand_RoozShab_Paye_ShoroVaKhatm()
    {
        var (h, dbf) = Host();
        await using (var db = dbf.Create())
        {
            db.Reports.Add(new ParchaReport
            {
                DateShamsi = "1405/07/01", DateKey = 14050701, ReportNum = 3, Fuel = FuelType.Diesel,
                DayShift = new ShiftData { Name = "احمد", PumpNum = 2, Start = 1000m, End = 1500m, Sale = 500m, Money = 40000m },
                NightShift = new ShiftData { Name = "محمود", PumpNum = 2, Start = 1500m, End = 1700m, Sale = 200m, Money = 16000m },
            });
            await db.SaveChangesAsync();
        }
        var rows = await h.FeedAsync("shift");
        var n = HistoryService.ColumnsOf("shift")!.Count;
        Assert.All(rows, r => Assert.Equal(n, r.Cells!.Count));
        var day = rows.Single(r => r.Cells![3].Contains("روز"));
        Assert.Equal(new[] { "1405/07/01", "3", "🟤 دیزل", "☀️ روز", "احمد", "2", "1,000", "1,500" }, day.Cells!.Take(8));
        var night = rows.Single(r => r.Cells![3].Contains("شب"));
        Assert.Equal("محمود", night.Cells![4]);
        Assert.Equal("1,700", night.Cells![7]);
        //  منطق دست نخورد: همان پول و همان دو ردیف
        Assert.Equal(40000m, day.Amount);
    }

    [Fact]
    public async Task Varaq_JomleyeHarShift_NaRadifBeRadif()
    {
        var (h, dbf) = Host();
        await using (var db = dbf.Create())
        {
            var w = new WaraqEntry { DateShamsi = "1405/07/02", DateKey = 14050702 };
            var day = new WaraqShift { Kind = ShiftKind.Day };
            day.Pumps.Add(new WaraqPump { Num = 1, Fuel = FuelType.Petrol, Start = 0, End = 100, PricePerLiter = 80, Worker = "هارون" });
            day.Transactions.Add(new WaraqTransaction { Name = "کریم", Type = WaraqTxnType.Debt, Amount = 500m, AmountAuto = false });
            day.Transactions.Add(new WaraqTransaction { Name = "نصیر", Type = WaraqTxnType.Debt, Amount = 300m, AmountAuto = false });
            day.Transactions.Add(new WaraqTransaction { Name = "برق", Type = WaraqTxnType.Expense, Amount = 200m, AmountAuto = false });
            day.Transactions.Add(new WaraqTransaction());                           // خالی ⇒ شمرده نمی‌شود
            w.Shifts.Add(day);
            w.Shifts.Add(new WaraqShift { Kind = ShiftKind.Night });              // شیفتِ خالی ⇒ ردیف ندارد
            db.WaraqEntries.Add(w);
            await db.SaveChangesAsync();
        }

        var rows = await h.FeedAsync("waraq");
        var r = Assert.Single(rows);
        Assert.Equal(new[] { "1405/07/02", "☀️ روز", "هارون", "2", "800 افغانی", "1", "200 افغانی", "100 لیتر", "8,000 افغانی" },
                     r.Cells!);
        //  کارت همان شمار را دارد
        var card = (await h.CardsAsync()).Single(c => c.Key == "waraq");
        Assert.Equal(1, card.Count);
    }

    [Fact]
    public async Task Sarrafi_TahvilYaBardagi_MablaghFiDaalar()
    {
        var (h, dbf) = Host();
        var row = new ExchangeRow
        {
            DateShamsi = "1405/07/03", DateKey = 14050703, Description = "حوالهٔ ۷",
            Amount = 7000m, Rate = 70m, Bardagi = 20m, Currency = ExchangeCurrency.Kaldar,
        };
        await using (var db = dbf.Create()) { db.ExchangeRows.Add(row); await db.SaveChangesAsync(); }
        var rows = await h.FeedAsync("sarrafi");
        var tahvil = rows.Single(r => r.Cells![1].Contains("تحویل به صرافی"));
        Assert.Equal(new[] { "1405/07/03", "💵 تحویل به صرافی", "حوالهٔ ۷", "7,000", "کالدار", "70", "100 $" }, tahvil.Cells!);
        var bord = rows.Single(r => r.Cells![1].Contains("بردگیِ پمپ"));
        Assert.Equal("20 $", bord.Cells![6]);
        Assert.Equal("out", bord.Tone);
    }

    [Fact]
    public async Task Sherkat_BardagiyePomp_VaRasideDaalari_BaArzash()
    {
        var (h, dbf) = Host();
        await using (var db = dbf.Create())
        {
            var c = new TilCompany { Name = "شرکتِ نور" };
            db.TilCompanies.Add(c);
            await db.SaveChangesAsync();
            db.CompanyRows.Add(new CompanyRow { CompanyId = c.Id, DateShamsi = "1405/07/04", DateKey = 14050704, Name = "خرید", Ton = 2m, Usd = 700m, Rate = 70m });
            db.CompanyRows.Add(new CompanyRow { CompanyId = c.Id, DateShamsi = "1405/07/05", DateKey = 14050705, Name = "رسید", Poul = 900m, PoulCurrency = Currency.Usd });
            db.CompanyRows.Add(new CompanyRow { CompanyId = c.Id, DateShamsi = "1405/07/06", DateKey = 14050706, Name = "رسید", Poul = 50000m });
            await db.SaveChangesAsync();
        }
        var rows = await h.FeedAsync("company");
        var buy = rows.Single(r => r.DateKey == 14050704);
        Assert.Equal("1,400.0 $", buy.Cells![5]);
        Assert.Equal("98,000", buy.Cells![6]);
        Assert.Equal("—", buy.Cells![7]);
        //  ⚠️ رسیدِ دالری دیگر افغانی خوانده نمی‌شود
        Assert.Equal("900 $", rows.Single(r => r.DateKey == 14050705).Cells![7]);
        Assert.Equal("50,000 افغانی", rows.Single(r => r.DateKey == 14050706).Cells![7]);
    }

    [Fact]
    public void JadvaleTarikhche_SarSotoonHamrahNamiayad_VaNavareSefidNadarad()
    {
        string Read(params string[] p)
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
            return File.ReadAllText(Path.Combine(new[] { d!.FullName }.Concat(p).ToArray()));
        }
        var x = Read("PumpYaqobi.App", "Views", "Sections", "HistorySectionView.axaml");
        Assert.Contains("HeaderFollows=\"False\"", x);
        Assert.Contains("HeaderFollows = false", Read("PumpYaqobi.App", "Views", "Sections", "HistorySectionView.axaml.cs"));
        //  ⛔ نوارِ پنهان جایی را کور نمی‌کند (همان نوارِ سفیدِ بالای سرستون)
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        Assert.Contains("private static double Blind(Control bar) => bar.IsVisible ? bar.Bounds.Height : 0;", g);
    }
}
