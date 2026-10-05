using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «طولِ کادرها توی پرینت نباید بزرگ بشه» (۱۴۰۵/۰۷/۲۰) ════════════════════
///
/// گزارشِ صاحب ریپو: «توی تمام پی‌دی‌اف‌ها همه‌شون طولِ کادرهاشون بزرگ
/// می‌شن… طول نباید بزرگ بشه از کادرهای توی پرینت.»
///
/// ⛔ ملاک **بلندیِ واقعیِ کشیده‌شدهٔ هر خانه** روی ورق است، نه «متن کوتاه
/// است»: ‎DocStyle.CellProbe‎ پشتِ هر خانهٔ جدول (سرستون، خانه، جمله) و هر
/// کادرِ خلاصه یک لایهٔ خالی می‌گذارد که موتورِ QuestPDF با اندازهٔ واقعیِ
/// همان خانه صدایش می‌زند. هر خانه‌ای که از **یک خط** بلندتر شده باشد —
/// یعنی ردیفش بلندتر از کوتاه‌ترین ردیفِ همان نوع در همان سند — سرخ است.
///
/// هر سند با دادهٔ «بد»ِ واقعی ساخته می‌شود: نامِ فارسیِ بلند، یادداشتِ بلند،
/// عددِ بزرگ با واحد («۱٬۰۳۳٬۱۶۲ افغانی») و تاریخ.
///
/// ⚠️ «یک شیفت در ۵ دقیقه» (‎QuickStartReport‎) این‌جا نیست و عمداً: آن برگه
/// جدول و کادرِ عدد ندارد، فقط دستورِ کارِ جمله‌به‌جمله است و بریدنِ جمله‌اش
/// خودِ دستور را می‌برد. ‎QuickStartTests‎ جدا می‌سازدش.
/// </summary>
public class PrintOneLineTests
{
    private const string Dates = "1405/07/20  ·  1448/04/28  ·  2026/10/12";

    /// <summary>
    /// بیشترین فرقِ مجاز با کوتاه‌ترین خانهٔ همان نوع (pt). یک خطِ اضافه
    /// ~۱۷pt است؛ نیم‌پوینت فقط گردکردنِ موتور را می‌پوشاند.
    /// </summary>
    private const float Slack = 1.5f;

    private const string LongName = "حاجی محمد نعیم ولد عبدالرحیم خان صاحبِ شرکتِ ترانسپورتیِ هرات و کابل";
    private const string LongNote = "پرداختِ دو قسطِ باقی‌مانده بابتِ حسابِ شرکتِ نمونه در ماهِ جاری، با رسیدِ کاغذیِ شمارهٔ ۱۲۳۴۵ و امضای مدیر و تاییدِ حسابدار";
    private const decimal Big = 1_033_162m;

    // ── اندازه‌گیری ─────────────────────────────────────────────────────────

    private sealed record Cell(string Kind, string Text, float Height);

    private static List<Cell> Measure(IDocument doc)
    {
        PdfEngine.Initialize();
        var cells = new List<Cell>();
        DocStyle.CellProbe = (k, t, h) => cells.Add(new Cell(k, t, h));
        try { doc.GeneratePdf(); }
        finally { DocStyle.CellProbe = null; }
        return cells;
    }

    private static void AssertOneLine(string name, IDocument doc)
    {
        var cells = Measure(doc);
        Assert.True(cells.Count > 0, $"{name}: هیچ خانه‌ای سنجیده نشد — سنجه کور است");

        var bad = new List<string>();
        foreach (var g in cells.GroupBy(c => c.Kind))
        {
            var one = OneLineFor(g.Key);
            foreach (var c in g.Where(c => c.Height > one + Slack && !RowSpanned(c, one)))
                bad.Add($"  [{c.Kind}] {c.Height:0.0}pt (یک خط = {one:0.0}pt) «{Short(c.Text)}»");
        }

        Assert.True(bad.Count == 0,
            $"{name}: {bad.Count} خانه از یک خط بلندتر شد:\n" + string.Join("\n", bad.Distinct().Take(25)));
    }

    /// <summary>
    /// بلندیِ «یک خط»ِ هر نوع خانه — از یک سندِ مرجع با نوشتهٔ یک‌نویسه‌ای،
    /// از همان درهای ‎DocStyle‎. پس عدد از خودِ موتور و قلم می‌آید، نه ثابتی
    /// که با عوض شدنِ قلم یا حاشیه کهنه شود.
    /// </summary>
    private static readonly Lazy<Dictionary<string, float>> OneLineRef = new(() =>
    {
        var doc = Document.Create(d => DocStyle.Compose(d, "مرجع", null, Dates, body => body.Column(col =>
        {
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                DocStyle.Head(t, cell => { DocStyle.ThText(cell(), "۱"); DocStyle.ThText(cell(), "📋"); });
                DocStyle.TdText(t.Cell(), false, "۱");
                DocStyle.TdText(t.Cell(), false, "⛽ ۱");
                DocStyle.Tf(t.Cell(), "۱");
                DocStyle.Tf(t.Cell(), "⛽ ۱");
            });
            col.Item().Width(120).Element(x => DocStyle.SumBox(x, "ا", "۱", DocStyle.Fuel));
        })));
        return Measure(doc).GroupBy(c => c.Kind).ToDictionary(g => g.Key, g => g.Max(c => c.Height));
    });

    private static readonly Dictionary<string, float> LineRef = new();

    /// <summary>
    /// ‎line:&lt;اندازه&gt;‎ — خودِ ‎DocStyle.Line‎ با همان اندازهٔ قلم. مرجعش
    /// ایموجی و رقم دارد، پس بلندترین «یک خطِ» آن اندازه است.
    /// </summary>
    private static float OneLineFor(string kind)
    {
        if (!kind.StartsWith("line:", StringComparison.Ordinal)) return OneLineRef.Value[kind];
        lock (LineRef)
        {
            if (LineRef.TryGetValue(kind, out var h)) return h;
            var size = float.Parse(kind[5..], System.Globalization.CultureInfo.InvariantCulture);
            var doc = Document.Create(d => DocStyle.Compose(d, "مرجع", null, Dates, body => body.Column(col =>
            {
                col.Item().Width(200).Element(x => DocStyle.Line(x, "⛽ ۱", size, "#000000", DocStyle.Weight.Bold));
                col.Item().Width(200).Element(x => DocStyle.Line(x, "۱", size, "#000000"));
            })));
            h = Measure(doc).Where(c => c.Kind == kind).Max(c => c.Height);
            return LineRef[kind] = h;
        }
    }

    /// <summary>
    /// سرستونِ گروهیِ «تیل امانت» (‎RowSpan(2)‎) عمداً دو ردیفِ سرستون را
    /// می‌گیرد — هر کدام یک خط. دو ردیفِ کامل (۲×) با «یک خط + خطِ پیچیده»
    /// (~۱٫۶×) یکی نیست، پس این استثنا نوشته‌ای را که پیچیده باشد پنهان نمی‌کند.
    /// </summary>
    private static bool RowSpanned(Cell c, float one) =>
        c.Kind == "th" && Math.Abs(c.Height - 2 * one) <= Slack;

    private static string Short(string s) => s.Length <= 60 ? s : s[..60] + "…";

    // ── همهٔ سندها ────────────────────────────────────────────────────────

    public static IEnumerable<object[]> Reports() =>
        Docs.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Reports))]
    public void HichKhaneyi_AzYekKhat_BolandtarNemishavad(string name)
    {
        AssertOneLine(name, Docs[name]());

        // تصویرِ ورقِ اول برای دیدن با چشم (‎PUMP_PDF_OUT‎)
        var dir = Environment.GetEnvironmentVariable("PUMP_PDF_OUT");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        var img = Docs[name]().GenerateImages().First();
        File.WriteAllBytes(Path.Combine(dir, "oneline-" + name + ".png"), img);
    }

    /// <summary>
    /// سنجه کور نیست: هر نوع خانه در جایی سنجیده شده است. اگر روزی یک
    /// گزارش دورِ ‎DocStyle‎ بزند، این‌جا شمارش پایین می‌آید.
    /// </summary>
    [Fact]
    public void Sanje_HarNoeKhaneRa_MibinadVaHarGozareshDarAnHast()
    {
        var kinds = new HashSet<string>();
        foreach (var (name, make) in Docs)
        {
            var cells = Measure(make());
            Assert.True(cells.Count > 0, name + ": هیچ خانه‌ای سنجیده نشد");
            foreach (var c in cells) kinds.Add(c.Kind);
        }
        foreach (var k in new[] { "td", "th", "tf", "box" })
            Assert.Contains(k, kinds);
    }

    /// <summary>
    /// ⛔ ساختاری: هیچ گزارشی خانهٔ جدول را خودش نمی‌نویسد — هر ‎Cell()‎ از
    /// یکی از درهای یک‌خطیِ ‎DocStyle‎ می‌گذرد، وگرنه سنجهٔ بالا آن خانه را
    /// نمی‌بیند و قاعده از درِ پشتی برمی‌گردد.
    /// </summary>
    [Fact]
    public void HichGozareshi_KhaneyeJadvalRa_KhodashNeminevisad()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "PumpYaqobi.Reporting")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var pdf = Path.Combine(dir!.FullName, "PumpYaqobi.Reporting", "Pdf");

        var bad = new List<string>();
        foreach (var f in Directory.GetFiles(pdf, "*Report.cs"))
        {
            // هر «دستور» (تا ‎;‎) جدا: زنجیره‌ای که از ‎Cell()‎ شروع شده و به
            // ‎.Text(‎ می‌رسد، بی این‌که از ‎DocStyle.‎ رد شود.
            var src = File.ReadAllText(f);
            foreach (var stmt in src.Split(';'))
                if (stmt.Contains("Cell()") && stmt.Contains(".Text(") && !stmt.Contains("DocStyle."))
                    bad.Add(Path.GetFileName(f) + ": " + stmt.Trim().Replace('\n', ' ')[..Math.Min(120, stmt.Trim().Length)]);
        }
        Assert.True(bad.Count == 0, "خانهٔ دست‌ساز:\n" + string.Join("\n", bad));
    }

    // ── داده‌ها ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, Func<IDocument>> Docs = new()
    {
        ["expense"] = () =>
        {
            var rows = new List<Expense>();
            for (var i = 1; i <= 12; i++)
                rows.Add(new Expense
                {
                    DateShamsi = "1405/07/" + i.ToString("00"),
                    Title = i % 2 == 0 ? LongName : "مصرف " + i,
                    Amount = Big * i,
                    Note = i % 3 == 0 ? LongNote : null,
                });
            return new ExpenseReport(new ExpenseReportInput("میزان 1405", rows, "1405/07/09", Dates));
        },

        ["safe"] = () => new SafeReport(new SafeReportInput("میزان 1405", new List<SafeEntry>
        {
            new() { DateShamsi = "1405/07/01", Kind = SafeEntryKind.Mandagi, Title = LongName,
                    Amount = Big, Currency = Currency.Afn, Note = LongNote },
            new() { DateShamsi = "1405/07/03", Kind = SafeEntryKind.Bardagi, Title = "خرید تیل",
                    Amount = 42_000.55m, Currency = Currency.Usd, Note = LongNote },
            new() { DateShamsi = "1405/07/09", Kind = SafeEntryKind.Bardagi, Title = "مصرف",
                    Amount = 12_000m, Currency = Currency.Afn },
        }, Dates), new SafeService()),

        ["storage"] = () =>
        {
            var calc = new StorageService();
            var buys = new List<FuelPurchase>
            {
                new() { Fuel = FuelType.Petrol, DateShamsi = "1405/07/12", Seller = LongName,
                        Kg = 1_248_800m, Density = 0.745m, PriceTon = 72_000m, UsdRate = 71.5m, Note = LongNote },
                new() { Fuel = FuelType.Petrol, DateShamsi = "1405/06/28", Seller = "شرکت کابل",
                        Kg = 31_200m, Density = 0.752m, PriceTon = 705m, UsdRate = 71.2m },
            };
            var liters = buys.Sum(p => calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).Liters);
            var afn = buys.Sum(p => calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).TotalAfn);
            var usd = buys.Sum(p => calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).TotalUsd);
            var tank = new TankState(liters, 61_400m, liters - 61_400m, liters - 61_400m,
                                     false, usd, afn, HasPurchases: true);
            return new StorageReport(new StorageReportInput(FuelType.Petrol, buys, tank, Dates), calc);
        },

        ["oldloans"] = () => new OldLoansReport(new OldLoansReportInput(AgingFilter.All, new List<AgingRow>
        {
            new(new Debtor { Name = LongName, Phone = "0700 123 456 / 0799 888 777" },
                new DebtSumFigures(true, Big * 10, 20_000m, Big * 10 - 20_000m), 118),
            new(new Debtor { Name = "شرکت پامیر" },
                new DebtSumFigures(false, 90_000m, 45_500m, 44_500m), 46),
        }, Dates)),

        ["retail"] = () => new RetailReport(new RetailReportInput("میزان 1405", new List<RetailRow>
        {
            new() { DateShamsi = "1405/07/02", Name = LongName, Fuel = FuelType.Petrol,
                    Liters = 24_000m, PricePerLiter = 67m, Rasid = Big, Note = LongNote },
            new() { DateShamsi = "1405/07/07", Name = "نقدی", ByMoney = true,
                    Bardagi = 3_500m, Rasid = 3_500m },
        }, Dates), new RetailService()),

        ["debtreceipt"] = () =>
        {
            var rows = new List<DebtQuickReceipt>();
            for (var i = 1; i <= 6; i++)
                rows.Add(new DebtQuickReceipt
                {
                    DateShamsi = "1405/07/" + i.ToString("00"),
                    Account = i % 2 == 0 ? LongName : "قرض‌دار " + i,
                    Amount = Big * i,
                    Note = i % 2 == 1 ? LongNote : null,
                });
            return new DebtReceiptReport(new DebtReceiptReportInput("میزان 1405", rows, Dates));
        },

        ["shifts"] = () => new ShiftsReport(new ShiftsReportInput(new List<ParchaReport>
        {
            new()
            {
                ReportNum = 1, DateShamsi = "1405/07/01", DateKey = 14050701,
                DateMiladi = "2026/09/23", DateQamari = "1448/04/10", Fuel = FuelType.Petrol,
                DayShift = Shift(LongName, 1_120_400m, 1_221_780m, 67m, 4m, debt: Big, note: LongNote),
                NightShift = Shift("وحید", 1_221_780m, 1_322_910m, 67m, 4m, debt: 18_000m),
            },
        }, Dates)),

        ["waraq"] = () =>
        {
            var sd = new WaraqShift
            {
                Kind = ShiftKind.Day, WorkerName = LongName, FabricDebt = Big,
                PricePerLiter = 67m, PricePerLiterDiesel = 61m,
            };
            sd.Pumps.Add(new WaraqPump { Num = 1, Worker = LongName, Fuel = FuelType.Petrol,
                                         Start = 1_120_400m, End = 1_221_240m, PricePerLiter = 67m,
                                         Debt = Big, Note = LongNote });
            sd.Pumps.Add(new WaraqPump { Num = 2, Worker = "شفیع", Fuel = FuelType.Diesel,
                                         Start = 88_100m, End = 88_640m, PricePerLiter = 61m });
            for (var i = 1; i <= 6; i++)
                sd.Transactions.Add(new WaraqTransaction
                {
                    SortIndex = i, Name = i % 2 == 0 ? LongName : "مشتری " + i, Liters = 1_000m * i,
                    Fuel = i % 3 == 0 ? FuelType.Diesel : FuelType.Petrol,
                    Type = i % 4 == 0 ? WaraqTxnType.Expense : WaraqTxnType.Debt, AmountAuto = true,
                });
            return new WaraqReport(new WaraqReportInput("پمپ یعقوبی", "1405/07/09", ShiftKind.Day, sd, Dates),
                                   new WaraqService());
        },

        ["amanat"] = () =>
        {
            var acc = new AmanatAccount { Name = LongName, Fuel = FuelType.Petrol, MyPct = 3m, Rate = 67m };
            acc.Rows.Add(new AmanatRow { DateShamsi = "1405/06/12", Name = LongName, ToAccount = LongName,
                                         Liters = 1_222_000m, Taken = 9_500m, Days = 26m, Temp = 31m });
            acc.Rows.Add(new AmanatRow { DateShamsi = "1405/07/02", Name = "تانکر کابل", ToAccount = "شرکت پامیر",
                                         Liters = 14_500m, Taken = 0m, Days = 9m, Temp = 27m, Actual = 14_380m });
            var svc = new AmanatService();
            var s = AmanatSettings.Default;
            decimal D(AmanatRow r) => r.Days ?? 0m;
            var a = new AmanatReportAccount(LongName, "⛽ پطرول", svc.AccountCalc(acc, s, D),
                acc.Rows.Select(r => new AmanatReportRow(r, svc.RowCalc(r, acc, s, D(r)))).ToList());
            return new AmanatReport(new AmanatReportInput("تیل امانت", s, new[] { a }, Dates));
        },

        ["company"] = () =>
        {
            var c = Company();
            var rows = CompanyService.RowsOf(c, FuelType.Petrol).Concat(CompanyService.RowsOf(c, FuelType.Diesel)).ToList();
            return new CompanyReport(new CompanyReportInput(c, null, rows, Dates), new CompanyService());
        },

        ["companypurchases"] = () =>
        {
            var c = Company();
            var buys = new List<FuelPurchase>
            {
                new() { Fuel = FuelType.Petrol, DateShamsi = "1405/07/12", Seller = LongName,
                        Kg = 1_248_800m, Density = 0.745m, PriceTon = 72_000m, UsdRate = 71.5m, Note = LongNote },
            };
            var dbuys = new List<FuelPurchase>
            {
                new() { Fuel = FuelType.Diesel, DateShamsi = "1405/07/13", Seller = "شرکت کابل",
                        Kg = 31_200m, Density = 0.84m, PriceTon = 705m, UsdRate = 71.2m },
            };
            return new CompanyPurchasesReport(new CompanyPurchasesReportInput("خریدهای " + LongName, null,
                buys, dbuys, CompanyService.RowsOf(c, FuelType.Petrol).ToList(),
                CompanyService.RowsOf(c, FuelType.Diesel).ToList(), Dates), new CompanyService());
        },

        ["staffshort"] = () => new StaffShortReport(new StaffShortReportInput(new List<StaffShortRow>
        {
            new("نصیر", LongName, 18, Big, 0m, 9_000m, 0m, Big - 9_000m, 0m),
            new("شفیع", "شفیع الله", 12, 0m, 7_500m, 0m, 2_500m, 0m, 5_000m),
        }, new List<StaffShortSettle>
        {
            new() { Name = LongName, Kind = StaffSettleKind.Short, Amount = Big, DateShamsi = "1405/07/03" },
        }, Dates)),

        ["monthend"] = () => new MonthEndReport(new MonthEndReportInput("میزان 1405", Month(100m), Month(90m), false, Dates)),

        ["exchange"] = () =>
        {
            var rows = new List<ExchangeRow>();
            for (var k = 1; k <= 8; k++)
                rows.Add(new ExchangeRow
                {
                    DateShamsi = "1405/07/" + k.ToString("00"),
                    Description = k % 2 == 0 ? LongNote : "صرافی " + k,
                    Amount = 12_345_678m * k, Rate = 6_123.5m, Bardagi = 2_016.25m * k,
                    Currency = ExchangeCurrency.Toman,
                });
            return new ExchangeReport(new ExchangeReportInput("میزان 1405", rows, Dates), new ExchangeService());
        },

        ["debtor"] = () =>
        {
            var calc = new DebtCalculationService(new Rates());
            var rows = new List<DebtRow>();
            for (var i = 1; i <= 8; i++)
                rows.Add(new DebtRow
                {
                    DateShamsi = "1405/07/" + i.ToString("00"), Name = i % 2 == 0 ? LongName : "مزدا",
                    Hawala = "902-" + i, Fuel = i % 3 == 0 ? FuelType.Diesel : FuelType.Petrol,
                    Liters = 10_826m * i, PricePerLiter = 67m, Rasid = i % 2 == 0 ? Big : 0m,
                });
            foreach (var r in rows) calc.NormalizeRow(r);
            var input = new DebtorStatementInput(
                PersonName: LongName, AccountTitle: "حساب " + LongName, IsMoneyLedger: true, Filter: null,
                Rows: rows, PercentPetrol: 0m, PercentDiesel: 4m,
                RasidPetrol: Big * 3, RasidDiesel: 0m, BordPetrol: Big, BordDiesel: 23_070m,
                RasidRowsPetrol: 0m, RasidRowsDiesel: 0m, Dates: Dates);
            return new DebtorStatementReport(input, calc);
        },

        ["dossier"] = () =>
        {
            var rows = new List<DebtRow>
            {
                new() { DateShamsi = "1405/05/01", Bardagi = Big * 10 },
                new() { DateShamsi = "1405/05/11", Rasid = Big },
                new() { DateShamsi = "1405/06/01", Rasid = 200m, Bardagi = Big * 5 },
            };
            return new DebtorDossierReport(new DebtorDossierInput("حساب " + LongName, true,
                DebtorDossierService.Build(rows, true), Big * 14, 0m, Dates));
        },

        ["pumpmonthly"] = () =>
        {
            var buys = new List<FuelPurchase>
            {
                new() { DateShamsi = "1405/06/03", Liters = 1_500_000m, Fuel = FuelType.Petrol },
                new() { DateShamsi = "1405/06/04", Liters = 999_999m, Fuel = FuelType.Diesel },
            };
            var reps = new List<ParchaReport>
            {
                new() { DateShamsi = "1405/06/10", Fuel = FuelType.Petrol,
                        DayShift = new ShiftData { Sale = 250_000m }, NightShift = new ShiftData { Sale = 50_000m } },
            };
            var src = new MonthReportSource(reps, new List<Expense>(), new List<ExtraIncome>(), buys,
                new List<DebtQuickReceipt>(), new List<SafeEntry>(), new List<TankerUnload>());
            var month = new MonthReportService().Compute(src, "1405/06");
            var pm = new PumpMonthlyService();
            return new PumpMonthlyReport(new PumpMonthlyInput("سنبله 1405", month,
                pm.Tank(FuelType.Petrol, "1405/06", buys, reps, new List<TankDip>()),
                pm.Tank(FuelType.Diesel, "1405/06", buys, reps, new List<TankDip>()), false, Dates));
        },
    };

    private sealed class Rates : IUnionRateProvider
    {
        public decimal UnionRate(FuelType fuel) => 62m;
    }

    private static ShiftData Shift(string name, decimal start, decimal end, decimal price, decimal profitPer,
                                   decimal debt = 0m, string? note = null)
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

    private static TilCompany Company()
    {
        var c = new TilCompany { Name = LongName, UsdRate = 71.4m };
        c.Rows.Add(new CompanyRow { Fuel = FuelType.Petrol, SortIndex = 0, DateShamsi = "1405/06/03", Name = LongName,
                                    Kg = 1_231_200m, Usd = 70_500m, Rate = 71.2m, Poul = 900_000_000m, Note = LongNote });
        c.Rows.Add(new CompanyRow { Fuel = FuelType.Petrol, SortIndex = 1, DateShamsi = "1405/07/12", Name = "تانکر ۲",
                                    Kg = 24_800m, Usd = 720m, Rate = 71.5m, Poul = 5_000m, PoulCurrency = Currency.Usd });
        c.Rows.Add(new CompanyRow { Fuel = FuelType.Diesel, SortIndex = 0, DateShamsi = "1405/07/05", Name = "تانکر دیزل",
                                    Kg = 18_400m, Usd = 690m, Rate = 71.3m });
        return c;
    }

    private static MonthReport Month(decimal scale) => new(
        Petrol: new MonthFuel(1_840_000m * scale, 26_400m * scale, 148_000m * scale, 61),
        Diesel: new MonthFuel(910_000m * scale, 15_100m * scale, 71_000m * scale, 33),
        Expenses: 132_000m * scale, Extra: 24_000m * scale,
        BuyPetrol: new MonthBuy(33_100m * scale, 2_010_000m * scale),
        BuyDiesel: new MonthBuy(12_400m * scale, 760_000m * scale),
        Rasid: 415_000m * scale, RasidCount: 12,
        SafeBardagi: 380_000m * scale, SafeMandagi: 512_000m * scale,
        TankerCount: 3, TankerShort: 214m * scale,
        Sales: 2_750_000m * scale, Liters: 41_500m * scale,
        Profit: 219_000m * scale, Net: 111_000m * scale);
}
