using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «تنظیمِ ورق» — کارگاهِ چاپ ═══════════════════════════════════════════════
/// همتای ‎PAPERS‎ / ‎MARGINS‎ / ‎hfText‎ی نسخهٔ وب. عددها این‌جا قفل می‌شوند تا
/// ورقِ کاربر با ورقی که سال‌ها چاپ کرده فرق نکند.
/// </summary>
public class PageSetupTests
{
    /// <summary>پیش‌فرض‌ها مو‌به‌مو همان ‎DEF‎ نسخهٔ وب‌اند.</summary>
    [Fact]
    public void TheDefaults_MatchTheWebVersion()
    {
        var s = PageSetup.Default;

        Assert.Equal("A4", s.Paper);
        Assert.Equal("narrow", s.MarginPreset);
        Assert.Equal(PageOrientation.Auto, s.Orientation);
        Assert.Equal(1, s.FirstPage);
        Assert.Equal("ورق &[Page] از &[Pages]", s.FooterCenter);
        Assert.Equal("&[Dates]", s.FooterRight);
        Assert.Equal("", s.HeaderCenter);

        var m = s.Margins();
        Assert.Equal(19.1m, m.Top);
        Assert.Equal(19.1m, m.Bottom);
        Assert.Equal(6.4m, m.Left);
        Assert.Equal(6.4m, m.Right);
    }

    /// <summary>اندازه‌های کاغذ همان جدولِ ‎PAPERS‎اند.</summary>
    [Theory]
    [InlineData("A4", 210, 297)]
    [InlineData("A5", 148, 210)]
    [InlineData("A3", 297, 420)]
    [InlineData("Letter", 215.9, 279.4)]
    [InlineData("Legal", 215.9, 355.6)]
    public void ThePaperTable_MatchesTheWebVersion(string paper, double w, double h)
    {
        var p = PageSetup.Papers[paper];
        Assert.Equal((decimal)w, p.W);
        Assert.Equal((decimal)h, p.H);
    }

    /// <summary>
    /// «ایستاده یا خوابیده» — ‎Auto‎ یعنی هرچه خودِ گزارش می‌خواهد؛ انتخابِ
    /// کاربر بر آن مقدم است.
    /// </summary>
    [Fact]
    public void Orientation_AutoFollowsTheReportButAChoiceWins()
    {
        var s = PageSetup.Default;
        Assert.Equal((210m, 297m), s.SizeMm(naturalLandscape: false));
        Assert.Equal((297m, 210m), s.SizeMm(naturalLandscape: true));

        var portrait = s with { Orientation = PageOrientation.Portrait };
        Assert.Equal((210m, 297m), portrait.SizeMm(naturalLandscape: true));

        var landscape = s with { Orientation = PageOrientation.Landscape };
        Assert.Equal((297m, 210m), landscape.SizeMm(naturalLandscape: false));
    }

    /// <summary>کاغذِ دلخواه عددهای خودش را می‌دهد، نه A4.</summary>
    [Fact]
    public void CustomPaper_UsesItsOwnNumbers()
    {
        var s = PageSetup.Default with { Paper = "Custom", CustomWidth = 120m, CustomHeight = 180m };
        Assert.Equal((120m, 180m), s.SizeMm(false));
        Assert.Equal((180m, 120m), s.SizeMm(true));
    }

    /// <summary>برداشتنِ یک حاشیهٔ آماده، چهار عددش را هم با خود می‌آورد.</summary>
    [Fact]
    public void PickingAMarginPreset_FillsTheFourNumbers()
    {
        var s = PageSetup.Default.WithMarginPreset("wide");
        Assert.Equal(25.4m, s.MarginTop);
        Assert.Equal(25.4m, s.MarginLeft);
        Assert.Equal(25.4m, s.Margins().Right);

        // «دلخواه» عددهای خودِ کاربر را نگه می‌دارد
        var custom = s with { MarginPreset = "custom", MarginLeft = 3m };
        Assert.Equal(3m, custom.Margins().Left);
    }

    // ── کدهای سربرگ/پاورقی ────────────────────────────────────────────────
    private static readonly DateTime When = new(2026, 9, 6, 14, 5, 0);

    [Fact]
    public void HeaderFooterTokens_MatchTheWebVersion()
    {
        var s = PageSetup.Default;
        var got = HeaderFooter.Render("&[ورق] / &[کل] · &[شمسی] · &[میلادی] · &[ساعت] · &[نام]",
                                      2, 7, s, "گاوصندوق", "—", When);

        Assert.Equal("2 / 7 · 1405/06/15 · 2026/09/06 · 14:05 · گاوصندوق", got);
    }

    /// <summary>«شمارهٔ ورقِ اول» هم روی ورقِ جاری اثر دارد هم روی تعدادِ کل.</summary>
    [Fact]
    public void FirstPageNumber_ShiftsBothNumbers()
    {
        var s = PageSetup.Default with { FirstPage = 5 };
        Assert.Equal("ورق 6 از 11", HeaderFooter.Render("ورق &[Page] از &[Pages]", 2, 7, s, "", "", When));
    }

    /// <summary>کدِ ناشناخته همان‌طور که هست می‌ماند — کاربر باید ببیند چه نوشته.</summary>
    [Fact]
    public void AnUnknownToken_IsLeftAlone() =>
        Assert.Equal("x &[چیزی] y",
                     HeaderFooter.Render("x &[چیزی] y", 1, 1, PageSetup.Default, "", "", When));

    /// <summary>‎&amp;[Dates]‎ همان سه‌تاریخِ پانویسِ سند است.</summary>
    [Fact]
    public void TheDatesToken_IsTheDocumentsOwnDateLine() =>
        Assert.Equal("1405/06/15  ·  1448/03/24  ·  2026/09/06",
                     HeaderFooter.Render("&[Dates]", 1, 1, PageSetup.Default,
                                         "", "1405/06/15  ·  1448/03/24  ·  2026/09/06", When));

    // ── ورقِ واقعی با تنظیمِ غیرِ پیش‌فرض ──────────────────────────────────
    /// <summary>
    /// تنظیمِ ورق واقعاً روی سند اثر می‌گذارد: همان گزارش روی A5ِ خوابیده
    /// ورق‌های بیشتری می‌گیرد تا روی A4ِ ایستاده.
    /// </summary>
    [Fact]
    public void TheSetup_ReallyChangesThePaper()
    {
        PdfEngine.Initialize();

        var input = new ExpenseReportInput("سنبله 1405", ManyExpenses(), "1405/06/09", "—");

        var a4 = new ExpenseReport(input);
        var a5 = new ExpenseReport(input)
        {
            Setup = PageSetup.Default with { Paper = "A5", Orientation = PageOrientation.Portrait },
        };

        var a4Pages = a4.GenerateImages(Small()).Count();
        var a5Pages = a5.GenerateImages(Small()).Count();

        Assert.True(a4Pages >= 1);
        Assert.True(a5Pages > a4Pages,
                    $"روی A5 باید ورقِ بیشتری بخورد (A4={a4Pages}, A5={a5Pages})");
    }

    /// <summary>سربرگِ دلخواه واقعاً روی ورق می‌نشیند و سند را نمی‌شکند.</summary>
    [Fact]
    public void ACustomHeader_StillProducesARealPdf()
    {
        PdfEngine.Initialize();

        var doc = new ExpenseReport(
            new ExpenseReportInput("سنبله 1405", ManyExpenses(), "1405/06/09", "—"))
        {
            Setup = PageSetup.Default with
            {
                HeaderRight = "پمپ یعقوبی",
                HeaderCenter = "&[شمسی]",
                HeaderLeft = "ورق &[Page]",
                MarginPreset = "wide",
            },
        };

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        var bytes = ms.ToArray();

        Assert.True(bytes.Length > 3000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    private static QuestPDF.Infrastructure.ImageGenerationSettings Small() => new()
    {
        ImageFormat = QuestPDF.Infrastructure.ImageFormat.Png,
        RasterDpi = 72,
    };

    private static List<PumpYaqobi.Domain.Entities.Expense> ManyExpenses()
    {
        var rows = new List<PumpYaqobi.Domain.Entities.Expense>();
        for (var i = 1; i <= 80; i++)
            rows.Add(new PumpYaqobi.Domain.Entities.Expense
            {
                DateShamsi = "1405/06/" + ((i % 30) + 1).ToString("00"),
                Title = "مصرف شمارهٔ " + i,
                Amount = 1_000m * i,
            });
        return rows;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  از تنظیم تا خودِ فایل — «هرچه می‌بینی همان چاپ می‌شود»
    // ══════════════════════════════════════════════════════════════════════════
    //
    // این‌ها همان سناریوهایی هستند که صاحب ریپو شمرد: گزارشِ سه‌ورقی با بازهٔ
    // ۱-۳، ۲-۳، ۲-۲ و … . سنجش روی خودِ خروجیِ ساخته‌شده است، نه روی ادعای کد:
    // ورق‌ها چیده می‌شوند و شمرده می‌شوند.

    /// <summary>
    /// گزارشی که مطمئناً چندورقی است، به‌علاوهٔ تصویرِ ورق‌هایش.
    ///
    /// ⚠️ دویست ردیف، نه هشتاد: آزمون‌های زیر به «دستِ‌کم سه ورق» بند هستند و
    /// نباید روزی که یک ردیف کوتاه‌تر شد، بی‌صدا بی‌معنی شوند.
    /// </summary>
    private static List<byte[]> RenderPages(PageSetup setup)
    {
        PdfEngine.Initialize();
        var rows = new List<PumpYaqobi.Domain.Entities.Expense>();
        for (var i = 1; i <= 200; i++)
            rows.Add(new PumpYaqobi.Domain.Entities.Expense
            {
                DateShamsi = "1405/06/" + ((i % 30) + 1).ToString("00"),
                Title = "مصرف شمارهٔ " + i,
                Amount = 1_000m * i,
            });

        var doc = new ExpenseReport(
            new ExpenseReportInput("سنبله 1405", rows, "1405/06/09", "—")) { Setup = setup };
        return doc.GenerateImages(Small()).ToList();
    }

    /// <summary>ورق‌های برگزیده را واقعاً می‌سازد و شمارِ ورقِ فایل را برمی‌گرداند.</summary>
    private static int PagesInOutput(PageSetup setup, int currentPage = 1)
    {
        var all = RenderPages(setup);
        var order = PrintJob.Order(setup, all.Count, currentPage);
        var picked = new PagesDocument(order.Select(i => all[i - 1]).ToList(), setup.Dpi);
        return picked.GenerateImages(Small()).Count();
    }

    /// <summary>گزارشِ آزمون واقعاً چندورقی است — وگرنه بقیهٔ آزمون‌ها بی‌معنی‌اند.</summary>
    [Fact]
    public void TheTestReport_IsReallyMultiPage()
        => Assert.True(RenderPages(PageSetup.Default).Count >= 3);

    /// <summary>«همهٔ گزارش» یعنی همان فایلِ اصلی، دست‌نخورده.</summary>
    [Fact]
    public void PrintingEverything_NeedsNoRepacking()
    {
        var all = RenderPages(PageSetup.Default);
        var order = PrintJob.Order(PageSetup.Default, all.Count, 1);
        Assert.True(PrintJob.IsWholeDocument(order, all.Count));
    }

    /// <summary>«از ۲ تا ۳» یعنی فایلِ خروجی دو ورق دارد، نه سه.</summary>
    [Fact]
    public void ARange_ProducesExactlyThoseSheets()
    {
        var s = PageSetup.Default with { What = PrintWhat.Range, From = 2, To = 3 };
        Assert.Equal(2, PagesInOutput(s));
    }

    /// <summary>«از ۲ تا ۲» یعنی یک ورق.</summary>
    [Fact]
    public void ASingleSheetRange_ProducesOneSheet()
    {
        var s = PageSetup.Default with { What = PrintWhat.Range, From = 2, To = 2 };
        Assert.Equal(1, PagesInOutput(s));
    }

    /// <summary>«چاپِ همین ورق» یعنی همان یکی که باز است.</summary>
    [Fact]
    public void TheCurrentSheet_ProducesOneSheet()
        => Assert.Equal(1, PagesInOutput(PageSetup.Default with { What = PrintWhat.Current }, 2));

    /// <summary>«صفحاتِ انتخابی ۱،۳» یعنی دو ورق.</summary>
    [Fact]
    public void CustomPages_ProduceExactlyThoseSheets()
    {
        var s = PageSetup.Default with { What = PrintWhat.Custom, CustomPages = "1,3" };
        Assert.Equal(2, PagesInOutput(s));
    }

    /// <summary>دو نسخه یعنی دو برابرِ ورق در همان فایل.</summary>
    [Fact]
    public void TwoCopies_DoubleTheSheets()
    {
        var one = PageSetup.Default with { What = PrintWhat.Range, From = 1, To = 2 };
        var two = one with { Copies = 2 };
        Assert.Equal(2, PagesInOutput(one));
        Assert.Equal(4, PagesInOutput(two));
    }

    /// <summary>
    /// ورقِ خروجی هم‌اندازهٔ ورقِ اصلی می‌ماند — A5 در خروجیِ بازه هم A5 است.
    ///
    /// (اندازه به «پوینت»، از روی پیکسلِ تصویر و ‎dpi‎ — همان کاری که
    /// <see cref="PagesDocument"/> می‌کند.)
    /// </summary>
    [Fact]
    public void ARangeKeepsThePaperSize()
    {
        var a5 = PageSetup.Default with
        {
            Paper = "A5", Orientation = PageOrientation.Portrait,
            What = PrintWhat.Range, From = 1, To = 1,
        };

        var page = RenderPages(a5)[0];
        var (w, h) = PagesDocument.PngSize(page);

        Assert.True(w > 0 && h > 0, "اندازهٔ PNG خوانده نشد");
        // A5ِ ایستاده: بلندتر از پهنا، و نسبتش ۲۱۰⁄۱۴۸
        Assert.True(h > w);
        Assert.InRange(h / (double)w, 210.0 / 148.0 - 0.05, 210.0 / 148.0 + 0.05);
    }

    /// <summary>«ترتیبِ وارونه» واقعاً روی خودِ فایل اثر می‌گذارد، نه فقط روی برچسب.</summary>
    [Fact]
    public void ReverseOrder_ReallyReordersTheFile()
    {
        var all = RenderPages(PageSetup.Default);
        var s = PageSetup.Default with { Order = PrintOrder.Reverse };
        var order = PrintJob.Order(s, all.Count, 1);

        Assert.Equal(all.Count, order.Count);
        Assert.Equal(all.Count, order[0]);          // اولین ورقِ خروجی، آخرین ورقِ گزارش است
        Assert.Equal(1, order[^1]);
        Assert.False(PrintJob.IsWholeDocument(order, all.Count));
    }
}
