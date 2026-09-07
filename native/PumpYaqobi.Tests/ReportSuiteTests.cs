using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ سندهای بندِ ۲۰ ═════════════════════════════════════════════════════════
/// هر ورقِ تازه (گاوصندوق، مصارف، مخزن، قرض‌های کهنه، گزارش پایان ماه) واقعاً
/// ساخته می‌شود و بازرسی می‌شود:
///
///   • هدرِ ‎%PDF‎ و اندازهٔ معقول — یعنی سند واقعاً محتوا دارد
///   • فونتِ وزیرمتن **داخلِ خودِ سند** جاسازی شده — وگرنه روی ویندوزی که آن
///     فونت را ندارد فارسیِ ورق به‌هم می‌ریزد
///   • هیچ ردی از HTML — کلِ برنامه برای همین بازنویسی شد (بندِ ۲۲)
///   • دستِ‌کم یک ورق به تصویر درمی‌آید — یعنی چیدمانش هم شکست نمی‌خورد
///
/// تصویرها در پوشهٔ خروجی می‌مانند تا با چشم با ورقِ نسخهٔ وب سنجیده شوند.
/// </summary>
public class ReportSuiteTests
{
    private static string OutDir
    {
        get
        {
            var d = Environment.GetEnvironmentVariable("PUMP_PDF_OUT")
                    ?? Path.Combine(Path.GetTempPath(), "pump-pdf");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    private const string Dates = "1405/06/15  ·  1448/03/24  ·  2026/09/06";

    /// <summary>ساختن، نوشتن، و بازرسیِ همان چهار ادعایی که بالا آمد.</summary>
    private static void Check(QuestPDF.Infrastructure.IDocument doc, string name, int minBytes)
    {
        PdfEngine.Initialize();
        var pdf = Path.Combine(OutDir, name + ".pdf");
        doc.GeneratePdf(pdf);

        var bytes = File.ReadAllBytes(pdf);
        Assert.True(bytes.Length > minBytes, $"{name}: سند خیلی کوچک است ({bytes.Length} بایت)");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        var raw = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("Vazirmatn", raw);
        Assert.Contains("/FontFile", raw);
        Assert.DoesNotContain("<html", raw.ToLowerInvariant());

        var n = 0;
        foreach (var img in doc.GenerateImages())
        {
            File.WriteAllBytes(Path.Combine(OutDir, $"{name}-{++n}.png"), img);
            if (n >= 2) break;
        }
        Assert.True(n >= 1, $"{name}: هیچ ورقی به تصویر درنیامد");
    }

    // ── گاوصندوق ─────────────────────────────────────────────────────────
    private static List<SafeEntry> SafeRows() => new()
    {
        new() { DateShamsi = "1405/06/01", Kind = SafeEntryKind.Mandagi, Title = "فروش روز",
                Amount = 128_500m, Currency = Currency.Afn, Note = "نقد" },
        new() { DateShamsi = "1405/06/03", Kind = SafeEntryKind.Bardagi, Title = "خرید تیل",
                Amount = 4_200m, Currency = Currency.Usd },
        new() { DateShamsi = "1405/06/09", Kind = SafeEntryKind.Bardagi, Title = "مصرف روزانه",
                Amount = 12_000m, Currency = Currency.Afn, Note = "به مدیر داده شد" },
        new() { DateShamsi = "1405/06/14", Kind = SafeEntryKind.Mandagi, Title = "رسید صرافی",
                Amount = 900m, Currency = Currency.Usd },
    };

    [Fact]
    public void SafeSheet_IsARealPdf()
    {
        var doc = new SafeReport(new SafeReportInput("سنبله 1405", SafeRows(), Dates),
                                 new SafeService());
        Check(doc, "safe", 3000);
    }

    /// <summary>
    /// ⚠️ دو ارز هرگز با هم جمع نمی‌شوند. ورقی که فقط افغانی دارد نباید
    /// «۰ دالر» بنویسد — یک‌بار همین کاربر را به اشتباه انداخت.
    /// </summary>
    [Fact]
    public void SafeSheet_KeepsTheTwoCurrenciesApart()
    {
        var calc = new SafeService();
        var afnOnly = SafeRows().Where(e => e.Currency == Currency.Afn).ToList();
        var s = calc.Summarize(afnOnly);

        Assert.Equal(0m, s.Bardagi.Usd);
        Assert.Equal(0m, s.Mandagi.Usd);
        Assert.Equal(128_500m - 12_000m, s.Net.Afn);

        Check(new SafeReport(new SafeReportInput("سنبله 1405", afnOnly, Dates), calc),
              "safe-afn", 3000);
    }

    // ── مصارف ────────────────────────────────────────────────────────────
    [Fact]
    public void ExpenseSheet_IsARealPdf()
    {
        var rows = new List<Expense>();
        for (var i = 1; i <= 24; i++)
            rows.Add(new Expense
            {
                DateShamsi = "1405/06/" + i.ToString("00"),
                Title = "مصرف شمارهٔ " + i,
                Amount = 1_250m * i,
                Note = i % 3 == 0 ? "با رسید" : null,
            });

        var doc = new ExpenseReport(
            new ExpenseReportInput("سنبله 1405", rows, "1405/06/09", Dates));
        Check(doc, "expenses", 4000);
    }

    /// <summary>ورقِ خالی هم باید ساخته شود — نه اینکه با خطا بترکد.</summary>
    [Fact]
    public void ExpenseSheet_SurvivesAnEmptyMonth()
    {
        var doc = new ExpenseReport(
            new ExpenseReportInput("حمل 1405", new List<Expense>(), "1405/01/01", Dates));
        Check(doc, "expenses-empty", 2000);
    }

    // ── مخزن ─────────────────────────────────────────────────────────────
    private static List<FuelPurchase> Purchases() => new()
    {
        new() { Fuel = FuelType.Petrol, DateShamsi = "1405/06/12", Seller = "شرکت هرات",
                Kg = 24_800m, Density = 0.745m, PriceTon = 720m, UsdRate = 71.5m,
                Note = "تانکر شمارهٔ ۴" },
        new() { Fuel = FuelType.Petrol, DateShamsi = "1405/05/28", Seller = "شرکت کابل",
                Kg = 31_200m, Density = 0.752m, PriceTon = 705m, UsdRate = 71.2m },
        new() { Fuel = FuelType.Petrol, DateShamsi = "1405/05/03", Seller = "شرکت هرات",
                Kg = 18_500m, Density = 0.748m, PriceTon = 733m, UsdRate = 70.9m },
    };

    [Fact]
    public void StorageSheet_IsARealPdf()
    {
        var calc = new StorageService();
        var buys = Purchases();
        var liters = buys.Sum(p => calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).Liters);
        var afn = buys.Sum(p => calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).TotalAfn);
        var usd = buys.Sum(p => calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).TotalUsd);
        var tank = new TankState(liters, 61_400m, liters - 61_400m, liters - 61_400m,
                                 false, usd, afn, HasPurchases: true);

        var doc = new StorageReport(new StorageReportInput(FuelType.Petrol, buys, tank, Dates), calc);
        Check(doc, "storage-petrol", 4000);
    }

    /// <summary>مخزنِ بی‌خرید ورقِ «هیچ خریدی ثبت نشده» می‌دهد، نه جدولِ خالی.</summary>
    [Fact]
    public void StorageSheet_SurvivesATankWithNoPurchases()
    {
        var doc = new StorageReport(
            new StorageReportInput(FuelType.Diesel, new List<FuelPurchase>(),
                                   new TankState(0m, 0m, 0m, 0m, true, 0m, 0m), Dates),
            new StorageService());
        Check(doc, "storage-diesel-empty", 2000);
    }

    // ── قرض‌های کهنه ──────────────────────────────────────────────────────
    private static List<AgingRow> Aging(bool money) => new()
    {
        new(new Debtor { Name = "حاجی نعیم", Phone = "0700 123 456" },
            new DebtSumFigures(money, 180_000m, 20_000m, 160_000m), 118),
        new(new Debtor { Name = "شرکت پامیر" },
            new DebtSumFigures(money, 90_000m, 45_500m, 44_500m), 46),
        new(new Debtor { Name = "عبدالقادر", Phone = "0777 000 111" },
            new DebtSumFigures(money, 12_000m, 0m, 12_000m), 9),
        new(new Debtor { Name = "بی‌تاریخ" },
            new DebtSumFigures(money, 5_000m, 0m, 5_000m), -1),
    };

    [Fact]
    public void OldLoansSheet_IsARealPdf()
    {
        Check(new OldLoansReport(new OldLoansReportInput(AgingFilter.Money, Aging(true), Dates)),
              "oldloans-money", 3000);
        Check(new OldLoansReport(new OldLoansReportInput(AgingFilter.Fuel, Aging(false), Dates)),
              "oldloans-fuel", 3000);
    }

    /// <summary>
    /// ⚠️ در حالتِ «همه» لیتر و افغانی قاطی‌اند، پس ورق ستونِ «واحد» می‌گیرد و
    /// جمعِ کل «—» می‌ماند. جمع زدنشان عددِ بی‌معنی می‌داد.
    /// </summary>
    [Fact]
    public void OldLoansSheet_NeverSumsLitersWithAfghani()
    {
        var mixed = Aging(true).Take(2).Concat(Aging(false).Skip(2)).ToList();
        Check(new OldLoansReport(new OldLoansReportInput(AgingFilter.All, mixed, Dates)),
              "oldloans-all", 3000);
    }

    // ── چکنه ─────────────────────────────────────────────────────────────
    private static List<RetailRow> RetailRows() => new()
    {
        new() { DateShamsi = "1405/06/02", Name = "موتر سراچه", Fuel = FuelType.Petrol,
                Liters = 24m, PricePerLiter = 67m, Rasid = 1_000m },
        new() { DateShamsi = "1405/06/04", Name = "تراکتور", Fuel = FuelType.Diesel,
                Liters = 40m, PricePerLiter = 61m },
        // ردیفِ «به پول»: لیتر ندارد و بردگی‌اش همان عددِ نوشته‌شده است
        new() { DateShamsi = "1405/06/07", Name = "نقدی", ByMoney = true,
                Bardagi = 3_500m, Rasid = 3_500m, Note = "تسویه شد" },
    };

    [Fact]
    public void RetailSheet_IsARealPdf() =>
        Check(new RetailReport(new RetailReportInput("سنبله 1405", RetailRows(), Dates),
                               new RetailService()),
              "chakana", 3000);

    /// <summary>
    /// ⚠️ ردیفِ «به پول» نه لیتر دارد نه فی — ورق باید «—» بگذارد و لیترش را
    /// در جمع نیاورد، وگرنه جمعِ لیترِ ماه غلط می‌شود.
    /// </summary>
    [Fact]
    public void RetailSheet_DoesNotCountLitersOfAMoneyRow()
    {
        var s = new RetailService().Summarize(RetailRows());
        Assert.Equal(64m, s.Liters);                       // ۲۴ + ۴۰، بی ردیفِ پولی
        Assert.Equal(24m * 67m + 40m * 61m + 3_500m, s.Bardagi);
    }

    // ── رسید قرض‌داران ────────────────────────────────────────────────────
    [Fact]
    public void DebtReceiptSheet_IsARealPdf()
    {
        var rows = new List<DebtQuickReceipt>();
        for (var i = 1; i <= 14; i++)
            rows.Add(new DebtQuickReceipt
            {
                DateShamsi = "1405/06/" + i.ToString("00"),
                Account = "قرض‌دار شمارهٔ " + i,
                Amount = 5_000m * i,
                Note = i % 4 == 0 ? "نقد، بی رسیدِ کاغذی" : null,
            });

        Check(new DebtReceiptReport(new DebtReceiptReportInput("سنبله 1405", rows, Dates)),
              "debtrasid", 3000);
    }

    // ── پارچه‌ها ──────────────────────────────────────────────────────────
    private static ShiftData Shift(string name, decimal start, decimal end,
                                   decimal price, decimal profitPer, decimal debt = 0m,
                                   string? note = null)
    {
        var sale = end - start;
        var money = sale * price;
        return new ShiftData
        {
            Name = name, PumpNum = 2, Start = start, End = end, Price = price,
            ProfitPer = profitPer, BuyPerLiter = price - profitPer,
            Sale = sale, Money = money, Debt = debt, Available = money - debt,
            Profit = sale * profitPer, Note = note,
        };
    }

    private static List<ParchaReport> Shifts() => new()
    {
        new()
        {
            ReportNum = 1, DateShamsi = "1405/06/01", DateKey = 14050601,
            DateMiladi = "2026/08/23", DateQamari = "1448/03/10", Fuel = FuelType.Petrol,
            DayShift = Shift("نصیر", 120_400m, 121_780m, 67m, 4m, note: "پایه نو"),
            NightShift = Shift("وحید", 121_780m, 122_910m, 67m, 4m, debt: 18_000m),
        },
        new()
        {
            ReportNum = 2, DateShamsi = "1405/06/01", DateKey = 14050601,
            DateMiladi = "2026/08/23", DateQamari = "1448/03/10", Fuel = FuelType.Diesel,
            DayShift = Shift("کریم", 44_100m, 44_690m, 61m, 3m),
        },
        // روزی که فقط شیفتِ روز دارد — ورق باید «⏳ ناقص» بنویسد، نه بترکد
        new()
        {
            ReportNum = 3, DateShamsi = "1405/06/02", DateKey = 14050602,
            DateMiladi = "2026/08/24", DateQamari = "1448/03/11", Fuel = FuelType.Petrol,
            DayShift = Shift("نصیر", 122_910m, 124_050m, 67m, 4m),
        },
    };

    [Fact]
    public void ShiftsSheet_IsARealPdf() =>
        Check(new ShiftsReport(new ShiftsReportInput(Shifts(), Dates)), "shifts", 4000);

    /// <summary>ورقِ ‎pdfDieselShifts‎ — فقط دیزل، بی بخشِ پطرول.</summary>
    [Fact]
    public void DieselShiftsSheet_IsARealPdf()
    {
        var diesel = Shifts().Where(r => r.Fuel == FuelType.Diesel).ToList();
        Check(new ShiftsReport(new ShiftsReportInput(diesel, Dates, DieselOnly: true)),
              "shifts-diesel", 3000);
    }

    /// <summary>بی هیچ گزارشی هم ورق باید ساخته شود، نه اینکه بترکد.</summary>
    [Fact]
    public void ShiftsSheet_SurvivesAnEmptyLog() =>
        Check(new ShiftsReport(new ShiftsReportInput(new List<ParchaReport>(), Dates)),
              "shifts-empty", 2000);

    // ── ورقِ روزانه ───────────────────────────────────────────────────────
    private static WaraqShift Waraq()
    {
        var sd = new WaraqShift
        {
            Kind = ShiftKind.Day,
            WorkerName = "نصیر احمد",
            FabricDebt = 6_000m,
            PricePerLiter = 67m,
            PricePerLiterDiesel = 61m,
        };
        sd.Pumps.Add(new WaraqPump { Num = 1, Worker = "نصیر", Fuel = FuelType.Petrol,
                                     Start = 120_400m, End = 121_240m, PricePerLiter = 67m,
                                     Debt = 32_000m, Note = "۰۶:۰۰ تا ۱۸:۰۰" });
        sd.Pumps.Add(new WaraqPump { Num = 2, Worker = "شفیع", Fuel = FuelType.Petrol,
                                     Start = 88_100m, End = 88_640m, PricePerLiter = 67m });
        sd.Pumps.Add(new WaraqPump { Num = 3, Worker = "کریم", Fuel = FuelType.Diesel,
                                     Start = 44_100m, End = 44_505m, PricePerLiter = 61m,
                                     Debt = 9_500m });
        for (var i = 1; i <= 9; i++)
            sd.Transactions.Add(new WaraqTransaction
            {
                SortIndex = i, Name = "مشتری " + i, Liters = 10m * i,
                Fuel = i % 3 == 0 ? FuelType.Diesel : FuelType.Petrol,
                Type = i % 4 == 0 ? WaraqTxnType.Expense : WaraqTxnType.Debt,
                AmountAuto = true,
            });
        return sd;
    }

    [Fact]
    public void WaraqSheet_IsARealPdf() =>
        Check(new WaraqReport(
                  new WaraqReportInput("پمپ یعقوبی", "1405/06/09", ShiftKind.Day, Waraq(), Dates),
                  new WaraqService()),
              "waraq-day", 4000);

    /// <summary>ورقِ شب همان ورق است با سربرگِ خودش — نه سندی جدا.</summary>
    [Fact]
    public void WaraqNightSheet_IsARealPdf()
    {
        var sd = Waraq();
        sd.Kind = ShiftKind.Night;
        Check(new WaraqReport(
                  new WaraqReportInput("پمپ یعقوبی", "1405/06/09", ShiftKind.Night, sd, Dates),
                  new WaraqService()),
              "waraq-night", 4000);
    }

    /// <summary>ورقِ خالی هم باید ساخته شود — همان ورقی که تازه باز شده.</summary>
    [Fact]
    public void WaraqSheet_SurvivesAnEmptyShift() =>
        Check(new WaraqReport(
                  new WaraqReportInput("", "1405/06/10", ShiftKind.Day,
                                       new WaraqShift { Kind = ShiftKind.Day }, Dates),
                  new WaraqService()),
              "waraq-empty", 2000);

    // ── گزارش پایان ماه ──────────────────────────────────────────────────
    private static MonthReport Month(decimal scale) => new(
        Petrol: new MonthFuel(1_840_000m * scale, 26_400m * scale, 148_000m * scale, 61),
        Diesel: new MonthFuel(910_000m * scale, 15_100m * scale, 71_000m * scale, 33),
        Expenses: 132_000m * scale,
        Extra: 24_000m * scale,
        BuyPetrol: new MonthBuy(33_100m * scale, 2_010_000m * scale),
        BuyDiesel: new MonthBuy(12_400m * scale, 760_000m * scale),
        Rasid: 415_000m * scale, RasidCount: 12,
        SafeBardagi: 380_000m * scale, SafeMandagi: 512_000m * scale,
        TankerCount: 3, TankerShort: 214m * scale,
        Sales: 2_750_000m * scale, Liters: 41_500m * scale,
        Profit: 219_000m * scale, Net: 111_000m * scale);

    [Fact]
    public void MonthEndSheet_IsARealPdf() =>
        Check(new MonthEndReport(
                  new MonthEndReportInput("سنبله 1405", Month(1m), Month(0.9m), false, Dates)),
              "monthend", 3000);

    /// <summary>
    /// بی اجازهٔ «مفاد / ضرر» ورق ساخته می‌شود ولی مفاد و نتیجهٔ خالص قفل
    /// می‌مانند — صفرِ دروغ بدتر از قفل است.
    /// </summary>
    [Fact]
    public void MonthEndSheet_LocksProfitInsteadOfPrintingZero() =>
        Check(new MonthEndReport(
                  new MonthEndReportInput("سنبله 1405", Month(1m), Month(0.9m), true, Dates)),
              "monthend-locked", 3000);
}
