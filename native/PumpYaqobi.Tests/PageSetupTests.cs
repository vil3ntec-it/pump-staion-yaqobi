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
}
