using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «داشبورد، مفاد/ضرر و مخزن‌ها سنگین اسکرول می‌شوند» (۱۴۰۵/۰۷/۱۳) ══════════
///
/// سه ریشه پیدا شد و هر سه این‌جا قفل می‌شوند — روی خودِ سورس، چون هر سه با
/// یک ویرایشِ بی‌خبر برمی‌گردند:
///
///   ۱) سایهٔ **محو** روی کارتِ بزرگ: شش کارتِ ۹۰۰×۴۴۰ی داشبورد و شش کارتِ
///      تمام‌عرضِ مفاد/ضرر ‎CardShadow‎ی محو داشتند — همان چیزی که برای کادرِ
///      صفحه‌قد از ۱۴۰۵/۰۶/۲۶ قدغن است. حالا ‎calm‎اند (سایهٔ بی‌محو).
///   ۲) فهرستِ خریدهای مخزن **همهٔ** خریدها را کارت می‌کرد، هر کارت هشت کادرِ
///      ‎.stat‎ با سایهٔ محو — پنج سال یعنی هزار کادرِ زنده. حالا پنجره دارد
///      (‎PurchaseCards‎) و ‎.stat‎ِ داخلِ فهرست بی‌محو است.
///   ۳) مفاد/ضرر و مخزن برای چند جمع **همهٔ** پارچه‌های پنج سال (با هر دو
///      شیفت)، همهٔ مصارف، همهٔ درآمدهای اضافی و همهٔ فاکتورها را به شیءِ
///      کامل می‌خواندند — روی نخِ رابط، چون SQLite «async»ش را همان‌جا
///      می‌دواند. حالا فقط ستونِ خودِ هر عدد.
///
/// سنجهٔ رفتاری‌اش عدد می‌دهد و در CI است:
/// <c>dotnet run --project PumpYaqobi.UiTests -c Release -- scrollperf</c>
/// </summary>
public class ScrollWeightTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-scrollw-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

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

    /// <summary>کامنت‌های XML بیرون می‌روند تا نامِ چیزِ برداشته‌شده در توضیح، سنجه را گول نزند.</summary>
    private static string NoComments(string xml) => Regex.Replace(xml, "<!--.*?-->", "", RegexOptions.Singleline);

    // ── ۱) کارتِ بزرگ سایهٔ محو ندارد ──────────────────────────────────────

    [Fact]
    public void SabkeCalm_Hast_Va_BiMahv_Ast()
    {
        var css = NoComments(Read("PumpYaqobi.App", "Themes", "Controls.axaml"));
        var calm = Regex.Match(css, "<Style Selector=\"Border\\.card\\.calm\">(.*?)</Style>", RegexOptions.Singleline);
        Assert.True(calm.Success, "سبکِ Border.card.calm نیست");
        Assert.Contains("Pump.SectionShadow", calm.Groups[1].Value);
        Assert.DoesNotContain("Pump.CardShadow", calm.Groups[1].Value);
        // زیرِ ماوس هم همان می‌ماند — ماوس همیشه روی صفحه است
        var hover = Regex.Match(css, "<Style Selector=\"Border\\.card\\.calm:pointerover\">(.*?)</Style>", RegexOptions.Singleline);
        Assert.True(hover.Success, "Border.card.calm:pointerover نیست — با ماوسِ روی صفحه، محو برمی‌گردد");
        Assert.Contains("Pump.SectionShadow", hover.Groups[1].Value);
    }

    [Fact]
    public void SheshKarteBozorgeDashboard_Calm_And()
    {
        var xaml = NoComments(Read("PumpYaqobi.App", "Views", "Sections", "DashboardSectionView.axaml"));
        // شش کارتِ دو ردیفِ پایین (وضعیتِ سوخت، روند، مخازن، هشدارها، نمودار، آخرین فروش‌ها)
        var big = Regex.Matches(xaml, "<Border Grid\\.Column=\"[012]\" Classes=\"card calm\"").Count;
        Assert.Equal(6, big);
        // و هیچ کارتِ بزرگی با سایهٔ محو نمانده — فقط پنج کارتِ آماریِ کوچک (داخلِ قالب)
        var plain = Regex.Matches(xaml, "<Border Grid\\.Column=\"[012]\" Classes=\"card\"").Count;
        Assert.Equal(0, plain);
    }

    [Fact]
    public void HameyeKarthayeProfit_Calm_And()
    {
        var xaml = NoComments(Read("PumpYaqobi.App", "Views", "Sections", "ProfitSectionView.axaml"));
        Assert.DoesNotContain("Classes=\"card\"", xaml);
        Assert.True(Regex.Matches(xaml, "Classes=\"card calm\"").Count >= 5, "کارت‌های مفاد/ضرر calm نیستند");
    }

    // ── ۲) فهرستِ خریدها پنجره دارد و کادرهای داخلِ فهرست بی‌محو ────────────

    [Fact]
    public void FehresteKharidha_Panjere_Darad()
    {
        var xaml = NoComments(Read("PumpYaqobi.App", "Views", "Sections", "StorageSectionView.axaml"));
        Assert.Contains("ItemsSource=\"{Binding PurchaseCards}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{Binding Purchases}\"", xaml);
        Assert.Contains("ShowMorePurchasesCommand", xaml);
        Assert.Contains("HasHiddenPurchases", xaml);

        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "StorageSectionViewModel.cs");
        Assert.Contains("PurchaseCards", vm);
        Assert.Contains("public const int PurchasePage", vm);
        // خودِ فهرستِ کامل می‌ماند — موجودی و PDF از آن حساب می‌شوند
        Assert.Contains("Calc.Tank(Purchases.Select(p => p.Entity)", vm);
        // هر جا فهرست عوض می‌شود، پنجره هم‌گام می‌شود (خواندن و حذف)
        Assert.True(Regex.Matches(vm, "SyncPurchaseCards\\(\\);").Count >= 3, "SyncPurchaseCards در هر سه جا صدا زده نمی‌شود");
    }

    [Fact]
    public void StateDakheleFehrest_BiMahv_Ast()
    {
        var css = NoComments(Read("PumpYaqobi.App", "Themes", "Controls.axaml"));
        var m = Regex.Match(css, "<Style Selector=\"ItemsControl Border\\.stat\">(.*?)</Style>", RegexOptions.Singleline);
        Assert.True(m.Success, "سبکِ «ItemsControl Border.stat» نیست — هشت کادرِ هر خرید دوباره سایهٔ محو می‌گیرند");
        Assert.Contains("Pump.SectionShadow", m.Groups[1].Value);
        // و باید **پس از** سبکِ پایهٔ Border.stat بیاید — در آوالونیا سبکِ بعدی برنده است
        Assert.True(css.IndexOf("<Style Selector=\"Border.stat\">", StringComparison.Ordinal)
                    < css.IndexOf("<Style Selector=\"ItemsControl Border.stat\">", StringComparison.Ordinal));
    }

    // ── ۳) برای یک جمع، همهٔ ردیف‌ها خوانده نمی‌شوند ─────────────────────────

    [Fact]
    public void ProfitVaStorage_HameyeRadifha_Ra_Nemikhanand()
    {
        var profit = Read("PumpYaqobi.App", "ViewModels", "Sections", "ProfitSectionViewModel.cs");
        Assert.DoesNotContain("ListAsync(null)", profit);
        Assert.DoesNotContain("ReportsAsync(", profit);
        Assert.DoesNotContain("Invoices.ListAsync(", profit);
        Assert.Contains("ShiftSumsAsync(", profit);
        Assert.Contains("SumAsync(e => e.Amount)", profit);
        Assert.Contains("ApprovedRatesAsync()", profit);

        var storage = Read("PumpYaqobi.App", "ViewModels", "Sections", "StorageSectionViewModel.cs");
        Assert.DoesNotContain("ReportsAsync(", storage);
        Assert.Contains("ShiftSumsAsync(Fuel)", storage);
    }

    [Fact]
    public void ScrollPerf_Profit_Va_Storage_Va_Hame_Ra_Misanjad()
    {
        var probe = Read("PumpYaqobi.UiTests", "ScrollPerf.cs");
        Assert.Contains("\"profit\", \"storage\"", probe);
        Assert.Contains("vm.NavSections.Where(x => !covered.Contains(x.Id))", probe);
        Assert.Contains("StorageSectionViewModel.PurchasePage", probe);
        Assert.Contains("BlurBroken", probe);
    }

    // ── جمع‌های از-پیش-خوانده همان عددها را می‌دهند (خالص) ──────────────────

    private static ProfitInput Lists() => new()
    {
        Reports = new[]
        {
            new ParchaReport { Fuel = FuelType.Petrol, DayShift = new ShiftData { Profit = 100m, Sale = 10m }, NightShift = new ShiftData { Profit = 50m, Sale = 5m } },
            new ParchaReport { Fuel = FuelType.Diesel, DayShift = new ShiftData { Profit = 7m, Sale = 1m } },
            new ParchaReport { Fuel = FuelType.Petrol, NightShift = new ShiftData { Profit = -20m, Sale = 2m } },
        },
        ExtraIncomes = new[] { new ExtraIncome { Amount = 30m }, new ExtraIncome { Amount = 12.5m } },
        Expenses = new[] { new Expense { Amount = 40m }, new Expense { Amount = 0.25m } },
        Invoices = new[]
        {
            new Invoice { Status = InvoiceStatus.Approved, Liters = 100m, PricePerLiter = 60m, RateOnApprove = 62m },
            new Invoice { Status = InvoiceStatus.Approved, Liters = 50m, RateOnCreate = 70m, RateOnApprove = 65m },
            new Invoice { Status = InvoiceStatus.Pending, Liters = 100m, PricePerLiter = 60m, RateOnApprove = 90m },
            new Invoice { Status = InvoiceStatus.Approved, ByMoney = true, Liters = 100m, PricePerLiter = 60m, RateOnApprove = 90m },
        },
    };

    [Fact]
    public void Breakdown_BaJam_Va_BaFehrest_YekiAst()
    {
        var lists = Lists();
        var a = ProfitLossService.Breakdown(lists);
        var sums = new ProfitInput
        {
            ShiftProfitPetrol = 130m, ShiftProfitDiesel = 7m,
            ExtraIncomeSum = 42.5m, ExpenseSum = 40.25m,
            InvoiceRateDiffSum = ProfitLossService.InvoiceRateDiff(lists.Invoices.Select(v =>
                new InvoiceRate(v.ByMoney, v.Status, v.Liters, v.PricePerLiter, v.RateOnCreate, v.RateOnApprove))),
        };
        var b = ProfitLossService.Breakdown(sums);
        Assert.Equal(a, b);
        Assert.Equal(130m, a.Petrol); Assert.Equal(7m, a.Diesel);
        Assert.Equal(42.5m, a.Extra); Assert.Equal(40.25m, a.Expenses);
        // فقط دو فاکتورِ تاییدشدهٔ تیلی: (62−60)×100 + (65−70)×50 = 200 − 250
        Assert.Equal(-50m, a.InvoiceRateDiff);
        Assert.Equal(ProfitLossService.Compute(lists, 3m, 1m), ProfitLossService.Compute(sums, 3m, 1m));
    }

    [Fact]
    public void Tank_BaFuroosheJamShode_HamanAst()
    {
        var svc = new StorageService();
        var buys = new[] { new FuelPurchase { Liters = 1000m, TotalUsd = 10m, TotalAfn = 700m } };
        var reps = Lists().Reports;
        var dips = new[] { new TankDip { BookAdjust = 3m } };
        var a = svc.Tank(buys, reps, 100m, dips);
        var b = svc.Tank(buys, 18m, 100m, dips);   // ۱۰+۵+۱+۲
        Assert.Equal(a, b);
        Assert.Equal(1000m - 18m + 3m, a.Current);
    }

    // ── و روی دیتابیسِ واقعی: ستونِ تنها همان عددِ شیءِ کامل را می‌دهد ───────

    private (StorageDataService Storage, InvoiceService Invoices, PumpDbFactory Db) Host()
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
        return (new StorageDataService(dbf, perm, trash, new StorageService(), settings, companies),
                new InvoiceService(dbf, perm, trash, debtors), dbf);
    }

    [Fact]
    public async Task ShiftSums_HamanJameReportsAsync_Ast()
    {
        var (storage, _, dbf) = Host();
        await using (var db = dbf.Create())
        {
            db.Reports.Add(new ParchaReport
            {
                DateShamsi = "1405/06/12", DateKey = 14050612, ReportNum = 1, Fuel = FuelType.Petrol,
                DayShift = new ShiftData { Name = "احمد", Sale = 300.5m, Profit = 12.25m },
                NightShift = new ShiftData { Name = "محمود", Sale = 200m, Profit = -3m },
            });
            //  پارچهٔ بی شیفتِ شب — نویگیشنِ تهی نباید عدد را خراب کند
            db.Reports.Add(new ParchaReport
            {
                DateShamsi = "1405/06/13", DateKey = 14050613, ReportNum = 2, Fuel = FuelType.Petrol,
                DayShift = new ShiftData { Name = "احمد", Sale = 10m, Profit = 1m },
            });
            //  دیزل جدا شمرده می‌شود
            db.Reports.Add(new ParchaReport
            {
                DateShamsi = "1405/06/13", DateKey = 14050613, ReportNum = 3, Fuel = FuelType.Diesel,
                DayShift = new ShiftData { Name = "کریم", Sale = 999m, Profit = 99m },
            });
            await db.SaveChangesAsync();
        }

        var petrol = await storage.ShiftSumsAsync(FuelType.Petrol);
        var full = await storage.ReportsAsync(FuelType.Petrol);
        Assert.Equal(full.Sum(r => (r.DayShift?.Sale ?? 0m) + (r.NightShift?.Sale ?? 0m)), petrol.Sale);
        Assert.Equal(full.Sum(r => (r.DayShift?.Profit ?? 0m) + (r.NightShift?.Profit ?? 0m)), petrol.Profit);
        Assert.Equal(510.5m, petrol.Sale);
        Assert.Equal(10.25m, petrol.Profit);

        var diesel = await storage.ShiftSumsAsync(FuelType.Diesel);
        Assert.Equal(999m, diesel.Sale);
        Assert.Equal(99m, diesel.Profit);

        //  و بی هیچ پارچه‌ای، صفر — نه خطا
        Assert.Equal(new StorageDataService.ShiftSums(0m, 0m), await storage.ShiftSumsAsync((FuelType)77));
    }

    [Fact]
    public async Task ApprovedRates_HamanEkhtelafeNerkheHameyeFakturha_Ast()
    {
        var (_, inv, _) = Host();
        Invoice Fuel(string name, decimal liters, decimal price) => new()
        {
            CustomerName = name, Liters = liters, PricePerLiter = price,
            Fuel = FuelType.Petrol, DateShamsi = "1405/06/15",
        };
        var a = await inv.AddAsync(Fuel("الف", 100, 60));
        await inv.ApproveAsync(a.Id, todayRate: 62m);
        var b = await inv.AddAsync(Fuel("ب", 50, 70));
        await inv.ApproveAsync(b.Id, todayRate: 65m);
        await inv.AddAsync(Fuel("در صف", 100, 60));                       // تاییدنشده — شمرده نمی‌شود
        var money = await inv.AddAsync(new Invoice { CustomerName = "پولی", Amount = 5000, DateShamsi = "1405/06/15" });
        await inv.ApproveAsync(money.Id, todayRate: 62m);                  // پولی — لیتر ندارد

        var fromAll = ProfitLossService.InvoiceRateDiff(await inv.ListAsync());
        var fromRates = ProfitLossService.InvoiceRateDiff(await inv.ApprovedRatesAsync());
        Assert.Equal(fromAll, fromRates);
        //  فقط دو فاکتورِ تاییدشدهٔ تیلی شمرده شده‌اند
        Assert.Equal(2, (await inv.ApprovedRatesAsync()).Count);
    }
}
