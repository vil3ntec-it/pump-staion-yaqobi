using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ مفاد/ضرر با کشوی ماه و سال · کادرهای برابر · مخزنِ وسط‌چین (۱۴۰۵/۰۷/۱۳) ══
///
/// سه خواستهٔ صاحب ریپو، با عکس:
///   ۱) «بخشِ مفاد و ضرر کادرِ کشوییِ ماه و سال ندارد… که آدم بفهمد برای ماه یا
///      سال چقدر فایده بوده.» ⇒ ‎ProfitPeriod‎ + چهار خواندنِ دوره‌دار.
///   ۲) «این سه تا خیلی ضایع دیده می‌شوند… هم برابر و تراز نیستند» ⇒ شبکهٔ
///      کشیده با سقف، یک قالبِ ستون برای ورودی و نتیجه، یک سبک برای هر کادر.
///   ۳) مخزن: «نوشته‌اش وسط نیست… جمله فروش و جمله ورود کادر ندارند.»
///
/// ⛔ فرمولِ مفاد/ضرر دست نخورد (‎ProfitLossService.Compute‎)؛ فقط ورودی‌ها به
/// همان دوره کوتاه می‌شوند. سنجهٔ رفتاری با پنجرهٔ واقعی و دادهٔ سه‌ماهه:
/// <c>dotnet run --project PumpYaqobi.UiTests -c Release -- plstore</c>
/// </summary>
public class ProfitPeriodTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-plperiod-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private sealed record Services(StorageDataService Storage, InvoiceService Invoices,
                                   DebtorService Debtors, PumpDbFactory Db);

    private Services Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var companies = new CompanyDataService(dbf, perm, trash);
        var debtors = new DebtorService(dbf, perm, trash);
        return new Services(new StorageDataService(dbf, perm, trash, new StorageService(), settings, companies),
                            new InvoiceService(dbf, perm, trash, debtors), debtors, dbf);
    }

    private static string Root()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    private static string NoComments(string xml) => Regex.Replace(xml, "<!--.*?-->", "", RegexOptions.Singleline);

    // ── ۱) دوره: خالص و بی دیتابیس ─────────────────────────────────────────

    [Theory]
    [InlineData("", "", "")]
    [InlineData(null, "", "")]
    [InlineData("1405/*", "1405", "")]
    [InlineData("1405/07", "1405", "07")]
    [InlineData("۱۴۰۵/۰۷", "1405", "07")]          // رقمِ فارسی هم
    [InlineData("1405", "", "")]                   // کلیدِ خراب ⇒ همه، نه خطا
    [InlineData("1405/7", "", "")]
    [InlineData("abcd/07", "", "")]
    public void Doreh_AzKelideKeshoei(string? key, string year, string month)
    {
        var p = ProfitPeriod.FromKey(key);
        Assert.Equal(year, p.Year);
        Assert.Equal(month, p.Month);
        Assert.Equal(year.Length == 0, p.IsAll);
    }

    [Fact]
    public void Doreh_Baze_Va_Mah()
    {
        var m = ProfitPeriod.FromKey("1405/07");
        Assert.Equal((14050701, 14050799), m.Keys);
        Assert.Equal("1405/07", m.MonthFilter);
        Assert.True(m.Contains(Shamsi.Key("1405/07/31")));
        Assert.False(m.Contains(Shamsi.Key("1405/08/01")));
        Assert.False(m.Contains(0));                              // ردیفِ بی‌تاریخ در یک ماه نیست

        var y = ProfitPeriod.FromKey("1405/*");
        Assert.Equal((14050101, 14051299), y.Keys);
        Assert.Equal("1405/", y.MonthFilter);                     // همان کلیدِ «پیشوندِ سال»ِ LedgerService.SumAsync
        Assert.True(y.Contains(Shamsi.Key("1405/12/29")));
        Assert.False(y.Contains(Shamsi.Key("1404/12/29")));

        var all = ProfitPeriod.All;
        Assert.Null(all.Keys);
        Assert.Null(all.MonthFilter);
        Assert.True(all.Contains(0));                             // «همه» یعنی همه، حتی بی‌تاریخ
    }

    /// <summary>
    /// ⛔ جمعِ «بی‌فاکتور» اگر داده شود، جای فهرست می‌نشیند — همان قاعدهٔ چهار
    /// جمعِ دیگر — و خودِ فرمول دست نمی‌خورد.
    /// </summary>
    [Fact]
    public void JameBiFaktor_JayeFehrest_MiNeshinad()
    {
        var acct = new DebtAccount();
        acct.FuelRows.Add(new DebtRow { Bardagi = 600m });
        acct.FuelRows.Add(new DebtRow { Bardagi = 300m });
        var lists = new ProfitInput { NoInvoiceAccounts = new[] { acct }, ExtraIncomeSum = 5, ExpenseSum = 1 };
        var sums = new ProfitInput { NoInvoiceSum = 900m, ExtraIncomeSum = 5, ExpenseSum = 1 };
        Assert.Equal(ProfitLossService.Breakdown(lists), ProfitLossService.Breakdown(sums));
        Assert.Equal(ProfitLossService.Compute(lists, 2, 3), ProfitLossService.Compute(sums, 2, 3));
        //  و فقط یک دوره: جمعِ کمتر، خالصِ کمتر
        Assert.True(ProfitLossService.Compute(new ProfitInput { NoInvoiceSum = 600m }, 0, 0).Net
                    < ProfitLossService.Compute(sums, 0, 0).Net);
    }

    // ── ۲) خواندن‌های دوره‌دار، روی SQLiteِ واقعی ────────────────────────

    [Fact]
    public async Task BiFaktor_DarDoreh_FaghatHamanMah()
    {
        var h = Host();
        var nf = await h.Debtors.AddDebtorAsync("بی‌فاکتور", null, noInvoice: true);
        var ff = await h.Debtors.AddDebtorAsync("با فاکتور", null, noInvoice: false);
        var nfAcc = (await h.Debtors.LoadFullAsync(nf.Id))!.MainAccount;
        var ffAcc = (await h.Debtors.LoadFullAsync(ff.Id))!.MainAccount;

        await h.Debtors.SaveRowAsync(new DebtRow { FuelAccountId = nfAcc.Id, DateShamsi = "1405/07/02", Liters = 10, PricePerLiter = 60 });
        await h.Debtors.SaveRowAsync(new DebtRow { FuelAccountId = nfAcc.Id, DateShamsi = "1405/06/20", Liters = 5, PricePerLiter = 60 });
        await h.Debtors.SaveRowAsync(new DebtRow { FuelAccountId = nfAcc.Id, DateShamsi = "1404/11/03", Liters = 1, PricePerLiter = 60 });
        //  ردیفِ «به پول»: بردگی همان عددِ خودش است
        await h.Debtors.SaveRowAsync(new DebtRow { FuelAccountId = nfAcc.Id, DateShamsi = "1405/07/09", ByMoney = true, Bardagi = 250 });
        //  حسابِ با فاکتور شمرده نمی‌شود
        await h.Debtors.SaveRowAsync(new DebtRow { FuelAccountId = ffAcc.Id, DateShamsi = "1405/07/02", Liters = 99, PricePerLiter = 60 });
        //  ردیفِ حذف‌شده هم نه
        await using (var db = h.Db.Create())
        {
            db.DebtRows.Add(new DebtRow { FuelAccountId = nfAcc.Id, DateShamsi = "1405/07/03", DateKey = 14050703,
                                          Liters = 1000, PricePerLiter = 60, DeletedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.Equal(600m + 250m, await h.Debtors.NoInvoiceBardagiAsync(ProfitPeriod.FromKey("1405/07").Keys));
        Assert.Equal(300m, await h.Debtors.NoInvoiceBardagiAsync(ProfitPeriod.FromKey("1405/06").Keys));
        Assert.Equal(1150m, await h.Debtors.NoInvoiceBardagiAsync(ProfitPeriod.FromKey("1405/*").Keys));
        Assert.Equal(60m, await h.Debtors.NoInvoiceBardagiAsync(ProfitPeriod.FromKey("1404/*").Keys));

        //  ⛔ «همه» مو‌به‌مو همان عددی است که صفحه تا امروز از کارت‌ها می‌گرفت
        var cards = (await h.Debtors.CardAccountsAsync(noInvoice: true)).Values.SelectMany(x => x)
                        .SelectMany(a => a.FuelRows).Sum(r => r.Bardagi);
        Assert.Equal(cards, await h.Debtors.NoInvoiceBardagiAsync(null));
        Assert.Equal(1210m, cards);

        var months = await h.Debtors.NoInvoiceMonthsAsync();
        Assert.Equal(new[] { "1404/11", "1405/06", "1405/07" }, months.OrderBy(m => m).ToArray());
    }

    [Fact]
    public async Task Parche_DarDoreh_BaTarikheKhodash()
    {
        var h = Host();
        await using (var db = h.Db.Create())
        {
            db.Reports.Add(new ParchaReport { DateShamsi = "1405/07/02", DateKey = 14050702, ReportNum = 1, Fuel = FuelType.Petrol,
                DayShift = new ShiftData { Profit = 100m, Sale = 1m }, NightShift = new ShiftData { Profit = 50m, Sale = 1m } });
            db.Reports.Add(new ParchaReport { DateShamsi = "1405/06/30", DateKey = 14050630, ReportNum = 2, Fuel = FuelType.Petrol,
                DayShift = new ShiftData { Profit = 7m, Sale = 1m } });
            db.Reports.Add(new ParchaReport { DateShamsi = "1404/01/01", DateKey = 14040101, ReportNum = 3, Fuel = FuelType.Petrol,
                DayShift = new ShiftData { Profit = 3m, Sale = 1m } });
            await db.SaveChangesAsync();
        }
        Assert.Equal(150m, (await h.Storage.ShiftSumsAsync(FuelType.Petrol, ProfitPeriod.FromKey("1405/07").Keys)).Profit);
        Assert.Equal(157m, (await h.Storage.ShiftSumsAsync(FuelType.Petrol, ProfitPeriod.FromKey("1405/*").Keys)).Profit);
        //  ⛔ بی دوره همان عددِ بی‌پارامترِ پیشین
        Assert.Equal((await h.Storage.ShiftSumsAsync(FuelType.Petrol)).Profit,
                     (await h.Storage.ShiftSumsAsync(FuelType.Petrol, null)).Profit);
        Assert.Equal(160m, (await h.Storage.ShiftSumsAsync(FuelType.Petrol)).Profit);
        Assert.Equal(new[] { "1404/01", "1405/06", "1405/07" },
                     (await h.Storage.ReportMonthsAsync()).OrderBy(m => m).ToArray());
    }

    /// <summary>
    /// اختلافِ نرخِ فاکتور همان روزِ **تایید** محقق می‌شود (‎_plInvApproveDateSh‎ی
    /// سایت)، نه روزِ ساختنش؛ فاکتوری که روزِ تایید ندارد با تاریخِ خودش.
    /// </summary>
    [Fact]
    public async Task Faktor_BaRoozeTaeid()
    {
        var h = Host();
        var a = await h.Invoices.AddAsync(new Invoice { CustomerName = "الف", Liters = 100, PricePerLiter = 60,
                                                        Fuel = FuelType.Petrol, DateShamsi = "1404/02/15" });
        await h.Invoices.ApproveAsync(a.Id, todayRate: 62m);
        await using (var db = h.Db.Create())
        {
            db.Invoices.Add(new Invoice { InvoiceNumber = 99, Status = InvoiceStatus.Approved, DateShamsi = "1404/03/01",
                                          DateKey = 14040301, Liters = 50, RateOnCreate = 70, RateOnApprove = 65 });
            await db.SaveChangesAsync();
        }
        var dated = await h.Invoices.ApprovedRatesDatedAsync();
        Assert.Equal(2, dated.Count);
        Assert.Contains(dated, d => d.ApproveKey == Shamsi.Key(DateTime.Now));   // تاییدِ امروز ⇒ امروز
        Assert.Contains(dated, d => d.ApproveKey == 14040301);                   // بی روزِ تایید ⇒ تاریخِ خودش
        //  ⛔ همان پنج ستونِ ‎ApprovedRatesAsync‎ — اختلافِ کل یکی است
        Assert.Equal(ProfitLossService.InvoiceRateDiff(await h.Invoices.ApprovedRatesAsync()),
                     ProfitLossService.InvoiceRateDiff(dated.Select(d => d.Rate)));
    }

    // ── ۳) صفحه‌ها: قاعده‌ها روی خودِ سورس ────────────────────────────────

    [Fact]
    public void MofadZarar_KeshoeiMahVaSal_Darad()
    {
        var xaml = NoComments(Read("PumpYaqobi.App", "Views", "Sections", "ProfitSectionView.axaml"));
        Assert.Contains("<c:SectionPage.Filters>", xaml);
        Assert.Contains("ItemsSource=\"{Binding Picker.Years}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Picker.Months}\"", xaml);
        Assert.Contains("{Binding PeriodText}", xaml);

        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "ProfitSectionViewModel.cs");
        Assert.Contains("new YearMonthPicker(", vm);
        Assert.Contains("ProfitPeriod.FromKey(", vm);
        //  ⛔ «همه» همان راهِ پیشین است — مو‌به‌مو
        Assert.Contains("CardAccountsAsync(noInvoice: true)", vm);
        Assert.Contains("NoInvoiceBardagiAsync(keys)", vm);
        //  جوابِ کهنه روی دورهٔ تازه نمی‌نشیند
        Assert.Contains("if (period != _period) return;", vm);
        //  پر کردنِ دوبارهٔ کشویی خودش محاسبه راه نمی‌اندازد
        Assert.Contains("if (_loadingPicker) return;", vm);
    }

    /// <summary>
    /// ⛔ شبکهٔ ستاره‌ایِ وسط‌چین ستون‌هایش را از روی **محتوا** اندازه می‌گیرد —
    /// ریشهٔ «برابر نیستند». هیچ شبکه‌ای در این صفحه دیگر ‎Center‎ نیست.
    /// </summary>
    [Fact]
    public void MofadZarar_KadrhaBarabar_Va_Taraz()
    {
        var xaml = NoComments(Read("PumpYaqobi.App", "Views", "Sections", "ProfitSectionView.axaml"));
        foreach (Match g in Regex.Matches(xaml, "<Grid [^>]*>"))
            Assert.DoesNotContain("HorizontalAlignment=\"Center\"", g.Value);

        //  هر کادرِ تایپ یک سبک (قد و قلم یک جا نوشته شده)
        var boxes = Regex.Matches(xaml, "<TextBox [^>]*>").Select(m => m.Value).ToList();
        Assert.Equal(7, boxes.Count);
        Assert.All(boxes, b => Assert.Contains("Classes=\"plbox\"", b));
        Assert.All(boxes, b => Assert.DoesNotContain("MinHeight=", b));
        Assert.All(boxes, b => Assert.DoesNotContain("FontSize=", b));

        //  ⛔ ورودی و نتیجهٔ خریدِ عمده یک شبکه‌اند، با یک قالبِ ستون
        var bulk = Regex.Match(xaml, "<Grid ColumnDefinitions=\"\\*,16,\\*,16,\\*\" RowDefinitions=\"[^\"]+\"[^>]*>(.*?)</Grid>",
                               RegexOptions.Singleline);
        Assert.True(bulk.Success, "شبکهٔ یگانهٔ خریدِ عمده نیست");
        foreach (var bind in new[] { "BulkQty", "BulkBuy", "BulkMarket", "BulkSellerText", "BulkIncomeText", "AddBulkCommand" })
            Assert.Contains(bind, bulk.Groups[1].Value);
        Assert.Single(Regex.Matches(xaml, "ColumnDefinitions=\"\\*,16,\\*,16,\\*\""));

        //  ⛔ هیچ منطقی عوض نشد: همان اتصال‌ها
        foreach (var bind in new[] { "UnionPetrol", "UnionDiesel", "ManualIncome", "ManualExpense", "NetText", "IncomeText", "ExpenseText" })
            Assert.Contains("{Binding " + bind + "}", xaml);

        var css = NoComments(Read("PumpYaqobi.App", "Views", "Sections", "ProfitSectionView.axaml"));
        var style = Regex.Match(css, "<Style Selector=\"TextBox\\.plbox\">(.*?)</Style>", RegexOptions.Singleline);
        Assert.True(style.Success);
        Assert.Contains("MinHeight", style.Groups[1].Value);
        Assert.Contains("FontSize", style.Groups[1].Value);
    }

    [Fact]
    public void Makhzan_OnvanVasat_Va_JomleHa_KadrDarand()
    {
        var xaml = NoComments(Read("PumpYaqobi.App", "Views", "Sections", "StorageSectionView.axaml"));
        foreach (var bind in new[] { "{Binding TankTitle}", "{Binding Current}" })
        {
            var tag = Regex.Match(xaml, "<TextBlock Text=\"" + Regex.Escape(bind) + "\"[^>]*>").Value;
            Assert.Contains("TextAlignment=\"Center\"", tag);
            Assert.Contains("HorizontalAlignment=\"Stretch\"", tag);
        }
        var chip = Regex.Match(xaml, "<Border [^>]*>\\s*<TextBlock Text=\"\\{Binding StateText\\}\"").Value;
        Assert.Contains("HorizontalAlignment=\"Center\"", chip);

        //  دو جمله داخلِ کادرِ فقط‌خواندنی، هم‌شکلِ دو کادرِ تایپ
        foreach (var bind in new[] { "TotalIn", "TotalOut" })
        {
            var box = Regex.Match(xaml, "<Border Classes=\"calc tankbox\"[^>]*>\\s*<TextBlock Text=\"\\{Binding " + bind);
            Assert.True(box.Success, bind + " کادرِ خواندنی ندارد");
            Assert.DoesNotContain("<TextBox Text=\"{Binding " + bind, xaml);
        }
        Assert.Equal(2, Regex.Matches(xaml, "<TextBox Classes=\"tankbox\"").Count);
        var calc = Regex.Match(xaml, "<Style Selector=\"Border\\.calc\\.tankbox\">(.*?)</Style>", RegexOptions.Singleline).Groups[1].Value;
        var typed = Regex.Match(xaml, "<Style Selector=\"TextBox\\.tankbox\">(.*?)</Style>", RegexOptions.Singleline).Groups[1].Value;
        string H(string s) => Regex.Match(s, "Property=\"Height\" Value=\"(\\d+)\"").Groups[1].Value;
        Assert.NotEqual("", H(calc));
        Assert.Equal(H(calc), H(typed));
    }
}
