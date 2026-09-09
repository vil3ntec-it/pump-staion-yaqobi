using PumpYaqobi.Reporting.Pdf;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ صفحهٔ چاپ ══════════════════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو با عکسِ پشتِ‌صحنهٔ چاپِ اکسل: «بخشِ پرینت باید این
/// مدلی باشد؛ در سایت هم همین مدل بود — با تمامِ منطق‌هایی که داشت.»
///
/// این آزمون دو چیز را نگه می‌دارد:
///   ۱. **شکل** — ستونِ تنظیمات با همان ترتیب و همان کادرها.
///   ۲. **منطق** — «کدام ورق‌ها»، «تعدادِ نسخه» و «مرتب/نامرتب» واقعاً همان
///      کاری را بکنند که ‎doPrint()‎ی سایت می‌کند.
/// </summary>
public class PrintPageTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    // ══ منطق ═════════════════════════════════════════════════════════════════

    /// <summary>«چاپِ همهٔ گزارش» یعنی ۱ تا آخر.</summary>
    [Fact]
    public void AllMeansEveryPage()
    {
        var s = PageSetup.Default;
        Assert.Equal(new[] { 1, 2, 3 }, PrintJob.Picked(s, 3, 1));
    }

    /// <summary>«چاپِ همین ورق» یعنی فقط ورقی که باز است.</summary>
    [Fact]
    public void CurrentMeansTheOpenPage()
    {
        var s = PageSetup.Default with { What = PrintWhat.Current };
        Assert.Equal(new[] { 2 }, PrintJob.Picked(s, 5, 2));
    }

    /// <summary>بازه — و اگر وارونه نوشته شده باشد، خودش صافش می‌کند.</summary>
    [Theory]
    [InlineData(2, 4, new[] { 2, 3, 4 })]
    [InlineData(4, 2, new[] { 2, 3, 4 })]     // وارونه
    [InlineData(0, 99, new[] { 1, 2, 3, 4, 5 })] // بیرون از دامنه
    public void RangeIsClampedAndOrdered(int from, int to, int[] want)
    {
        var s = PageSetup.Default with { What = PrintWhat.Range, From = from, To = to };
        Assert.Equal(want, PrintJob.Picked(s, 5, 1));
    }

    /// <summary>«مرتب» یعنی ۱،۲،۳ ۱،۲،۳ — همان خطِ زیرِ کادر.</summary>
    [Fact]
    public void CollatedRepeatsTheWholeSet()
    {
        var s = PageSetup.Default with { Copies = 3, Collate = true };
        Assert.Equal(new[] { 1, 2, 1, 2, 1, 2 }, PrintJob.Order(s, 2, 1));
    }

    /// <summary>«نامرتب» یعنی ۱،۱،۱ ۲،۲،۲.</summary>
    [Fact]
    public void UncollatedRepeatsEachPage()
    {
        var s = PageSetup.Default with { Copies = 3, Collate = false };
        Assert.Equal(new[] { 1, 1, 1, 2, 2, 2 }, PrintJob.Order(s, 2, 1));
    }

    /// <summary>یک نسخه یعنی همان فهرست، بی تکرار.</summary>
    [Fact]
    public void OneCopyChangesNothing()
    {
        var s = PageSetup.Default;
        Assert.Equal(new[] { 1, 2 }, PrintJob.Order(s, 2, 1));
        Assert.True(PrintJob.IsWholeDocument(PrintJob.Order(s, 2, 1), 2));
    }

    /// <summary>
    /// وقتی همهٔ ورق‌ها یک‌بار و به ترتیب خواسته شده‌اند، همان فایلِ اصلی کافی
    /// است — یعنی PDFِ برداری با متنِ قابلِ جست‌وجو، نه ورق‌های تصویری.
    /// </summary>
    [Fact]
    public void ASubsetIsNotTheWholeDocument()
    {
        var range = PageSetup.Default with { What = PrintWhat.Range, From = 2, To = 3 };
        Assert.False(PrintJob.IsWholeDocument(PrintJob.Order(range, 5, 1), 5));

        var twice = PageSetup.Default with { Copies = 2 };
        Assert.False(PrintJob.IsWholeDocument(PrintJob.Order(twice, 2, 1), 2));
    }

    /// <summary>سندِ بی‌ورق هیچ‌چیز نمی‌دهد — نه استثنا.</summary>
    [Fact]
    public void NoPagesMeansNothingToPrint()
        => Assert.Empty(PrintJob.Order(PageSetup.Default, 0, 1));

    /// <summary>
    /// «تعدادِ نسخه» و «کدام ورق‌ها» چیدمانِ ورق را عوض نمی‌کنند، پس نباید
    /// سند را از نو بسازند — وگرنه با هر بالا بردنِ عددِ نسخه، کلِ گزارش
    /// دوباره رسم می‌شد.
    /// </summary>
    [Fact]
    public void JobSettingsDoNotForceARebuild()
    {
        var a = PageSetup.Default;
        var b = a with { Copies = 7, Collate = false, What = PrintWhat.Range, From = 2, To = 3 };
        Assert.Equal(a.LayoutOnly(), b.LayoutOnly());

        // ولی کاغذ و مقیاس چرا
        Assert.NotEqual(a.LayoutOnly(), (a with { Paper = "A5" }).LayoutOnly());
        Assert.NotEqual(a.LayoutOnly(), (a with { Scale = PrintScale.FitPage }).LayoutOnly());
    }

    // ══ شکل ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// ستونِ تنظیمات، با همان ترتیبی که ‎sidebarHtml()‎ی سایت دارد و عکسِ
    /// صاحب ریپو نشان می‌دهد.
    /// </summary>
    [Fact]
    public void TheRailHasEverySectionOfTheExcelBackstage()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");

        foreach (var piece in new[]
        {
            "تعدادِ نسخه",                    // Copies
            "چاپگر", "آماده",                 // Printer ▸ Ready
            "انتخابِ چاپگر و تنظیماتش…",       // Printer Properties
            "{Binding Whats}",                // Print Active Sheets
            "ورق‌ها:",                        // Pages: … to …
            "{Binding Collates}",             // Collated
            "{Binding Orientations}",         // Orientation
            "{Binding Papers}",               // A4
            "{Binding MarginChoices}",        // Margins
            "{Binding Scales}",               // Scaling
            "تنظیمِ ورق…",                    // Page Setup
        })
            Assert.Contains(piece, w);
    }

    /// <summary>پیش‌نمایش و نوارِ پایینش — ‎◀ [۱] از ۲ ▶‎ و دو دکمهٔ گوشه.</summary>
    [Fact]
    public void ThePreviewHasThePageStrip()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");
        Assert.Contains("{Binding PageNumberText}", w);
        Assert.Contains("PrevCommand", w);
        Assert.Contains("NextCommand", w);
        Assert.Contains("ZoomPageCommand", w);
        Assert.Contains("{Binding ShowGuides}", w);
        Assert.Contains("{Binding GuideMargin}", w);
    }

    /// <summary>
    /// دو ستون است، نه یک پنجرهٔ تنظیماتِ جدا: ستونِ تنظیمات و پیش‌نمایش
    /// کنارِ هم دیده می‌شوند — همان چیزی که در عکس است.
    /// </summary>
    [Fact]
    public void ItIsTwoPanesSideBySide()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");
        Assert.Contains("ColumnDefinitions=\"Auto,*\"", w);
        Assert.Contains("WindowState=\"Maximized\"", w);
        // پیش‌نمایش آینه نمی‌شود؛ وگرنه خط‌چینِ حاشیه چپ و راستش برعکس می‌افتاد
        Assert.Contains("FlowDirection=\"LeftToRight\"", w);
    }

    /// <summary>کشوها کارتِ دوخطیِ اکسل‌اند: نشانه، عنوانِ پررنگ و یک خطِ توضیح.</summary>
    [Fact]
    public void TheDropdownsAreExcelStyleCards()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");
        Assert.Contains("c|SettingCard", w);
        Assert.Contains("{Binding Icon}", w);
        Assert.Contains("{Binding Note}", w);

        var vm = Read("PumpYaqobi.App", "Printing", "PrintSetupViewModel.cs");
        Assert.Contains("string Icon = \"\"", vm);
    }

    /// <summary>
    /// ══ چرخِ ماوس مقدارِ کادر را عوض نمی‌کند ═══════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «روی هر کادر که می‌روم با اسکرول تغییر می‌کنند؛ باید
    /// با کلیک بتوانم تغییر بدهم.»
    ///
    /// ریشه‌اش خودِ ‎ComboBox‎ی آوالونیا بود که با چرخ ‎SelectNext/Previous‎
    /// می‌زند. ‎SettingCard‎ آن را برمی‌دارد — و مهم‌تر، رویداد را ‎Handled‎ هم
    /// نمی‌کند تا ستون همچنان بلغزد.
    /// </summary>
    [Fact]
    public void TheWheelScrollsTheRailInsteadOfChangingTheSetting()
    {
        var c = Read("PumpYaqobi.App", "Controls", "SettingCard.cs");
        Assert.Contains("class SettingCard : ComboBox", c);
        Assert.Contains("protected override void OnPointerWheelChanged(PointerWheelEventArgs e)", c);

        // نه ‎base‎ صدا زده می‌شود و نه رویداد مصرف — هر دو لازم است
        var body = c[c.IndexOf("protected override void OnPointerWheelChanged", StringComparison.Ordinal)..];
        Assert.DoesNotContain("base.OnPointerWheelChanged", body);
        Assert.DoesNotContain("e.Handled = true", body);
    }

    /// <summary>
    /// ══ ستونِ تنظیمات سمتِ **چپ** ═══════════════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «آن کادرها باید سمت چپ باشند نه راست.»
    ///
    /// و خودِ سایت هم همین است — خطِ ۲۵۶۴۹ی ‎index.html‎:
    ///     ‎#xlpr{ … direction:ltr … }‎
    ///     ‎#xlpr .xs{flex:0 0 352px; … direction:rtl}‎
    /// یعنی کلِ پنجره چپ‌به‌راست و فقط ستون داخلش راست‌به‌چپ، پس ستون اولین
    /// چیزِ سمتِ چپ می‌شود.
    /// </summary>
    [Fact]
    public void TheRailSitsOnTheLeftLikeTheSite()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");

        // کلِ پنجره چپ‌به‌راست
        Assert.Contains("FlowDirection=\"LeftToRight\" Background=\"#ffffff\"", w);

        // ستون، ستونِ **اول** است و خودش راست‌به‌چپ
        var at = w.IndexOf("Grid.Column=\"0\" Width=\"352\"", StringComparison.Ordinal);
        Assert.True(at > 0, "ستونِ تنظیمات پیدا نشد");
        Assert.Contains("FlowDirection=\"RightToLeft\"", w.Substring(at, 300));

        // و دیگر کلِ پنجره راست‌به‌چپ نیست — همان اشتباهی که ستون را آینه کرد
        Assert.DoesNotContain("FlowDirection=\"RightToLeft\" Background=", w);
    }

    /// <summary>اندازه‌ها و رنگ‌ها از خودِ سایت‌اند، نه حدسی.</summary>
    [Theory]
    [InlineData("#217346")]           // ‎.xt‎ و ‎.xh2‎ و ‎.xpdf‎ — سبزِ اکسل
    [InlineData("Height=\"44\"")]     // ‎.xt{height:44px}‎
    [InlineData("Width=\"352\"")]     // ‎.xs{flex:0 0 352px}‎
    [InlineData("#f3f2f1")]           // ‎.xp{background:#f3f2f1}‎
    [InlineData("#faf9f8")]           // ‎.xb{background:#faf9f8}‎
    [InlineData("#d2d0ce")]           // لبهٔ کارت‌ها
    [InlineData("#0f6cbd")]           // ‎.xlink‎
    [InlineData("22,14,22,30")]       // ‎.xs{padding:14px 22px 30px}‎
    public void TheMeasurementsComeFromTheSite(string piece)
        => Assert.Contains(piece, Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml"));

    /// <summary>
    /// «تنظیمِ ورق» نباید چیزهایی را که نشان نمی‌دهد پاک کند — پیش از این با
    /// هر تایید، تعدادِ نسخه و بازهٔ ورق‌ها به پیش‌فرض برمی‌گشتند.
    /// </summary>
    [Fact]
    public void ThePageSetupDialogKeepsTheJobSettings()
    {
        var vm = Read("PumpYaqobi.App", "Printing", "PrintSetupViewModel.cs");
        Assert.Contains("public PageSetup Build() => _orig with", vm);
        Assert.Contains("private readonly PageSetup _orig;", vm);
    }

    /// <summary>مقیاس یک جا اعمال می‌شود — پس روی همهٔ گزارش‌ها یکسان است.</summary>
    [Fact]
    public void ScalingIsAppliedInOnePlaceForEveryReport()
    {
        var d = Read("PumpYaqobi.Reporting", "Pdf", "DocStyle.cs");
        Assert.Contains("PrintScale.FitPage => c.ScaleToFit()", d);
        Assert.Contains("PrintScale.Custom => c.Scale(", d);
        Assert.Contains("Sized(page.Content())", d);
    }
}
