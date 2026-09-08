using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ قاعده‌های «پرامپت کیفیت بصری» ═══════════════════════════════════════════
///
/// صاحب ریپو یک سندِ جدا داد («پرامپت ارتقای کیفیت بصری») با فهرستی از
/// قاعده‌ها و یک بخشِ «ممنوعیت‌ها». آن‌هایی که **متنی سنجیدنی** هستند این‌جا
/// قفل می‌شوند تا با یک دستکاریِ بعدی برنگردند.
///
/// ⚠️ آن‌چه این‌جا نیست، یعنی هنوز پیاده نشده — نه یعنی لازم نیست.
/// </summary>
public class VisualQualityTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Theme() =>
        File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Themes", "Controls.axaml"));

    /// <summary>
    /// ══ خطِ جدول ═════════════════════════════════════════════════════════
    /// ⚠️ این آزمون **وارونه** شد و عمداً.
    ///
    /// «پرامپت کیفیت بصری» گفته بود «هیچ خط عمودی»، و یک نوبت همان پیاده شد.
    /// نتیجه‌اش گزارشِ صاحب ریپو بود: «خط‌های جدول‌ها نیست و دیده نمی‌شه.»
    ///
    /// و خودِ سایت هم خطِ عمودی دارد — ‎.xls-tbl td‎ هم ‎border-bottom‎ دارد و
    /// هم ‎border-left‎. پس سایت و صاحب ریپو بر آن سند مقدم‌اند.
    ///
    /// رنگ هم مهم است: ‎Pump.GridLine‎ ی ‎Mix(Card, Border, 0.75)‎ عملاً نامرئی
    /// بود؛ سایت خودِ ‎border‎ را می‌گذارد، پس ‎Pump.Border‎.
    /// </summary>
    [Fact]
    public void TableLinesAreVisibleAndGoBothWays()
    {
        var t = Theme();
        Assert.Contains("<Setter Property=\"GridLinesVisibility\" Value=\"All\" />", t);
        // رنگ از تنظیماتِ کاربر می‌آید (‎Pump.Table.Border‎)، نه از داخلِ سبک —
        // خواستهٔ صریحِ صاحب ریپو: «نباید Hard-coded باشد.»
        Assert.Contains("<Setter Property=\"HorizontalGridLinesBrush\" Value=\"{DynamicResource Pump.Table.Border}\" />", t);
        Assert.Contains("<Setter Property=\"VerticalGridLinesBrush\" Value=\"{DynamicResource Pump.Table.Border}\" />", t);

        var bare = NoComments(t);
        Assert.DoesNotContain("GridLinesVisibility\" Value=\"Horizontal", bare);
        Assert.DoesNotContain("GridLinesBrush\" Value=\"{DynamicResource Pump.GridLine}", bare);
    }

    /// <summary>
    /// کادرِ تایپِ داخلِ خانه شفاف است و لبه ندارد — ‎.xls-in‎ی سایت. پیش از
    /// این ‎TextBox‎ی آوالونیا وسطِ جدولِ تیره سفید می‌شد.
    /// </summary>
    [Fact]
    public void TheInCellEditorIsTransparent()
    {
        var cell = Between(Theme(), "<Style Selector=\"DataGridCell TextBox\">", "</Style>");
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", cell);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", cell);
    }

    /// <summary>
    /// «صفر دیده بشود ولی صفری وجود نداشته باشد» — یعنی ‎placeholder‎ی سایت.
    /// خانهٔ عددی (‎CellStyleClasses="num"‎) واترمارکِ «۰» می‌گیرد.
    /// </summary>
    [Fact]
    public void NumericCellsShowAGhostZero()
    {
        var t = Theme();
        Assert.Contains("<Style Selector=\"DataGridCell.num TextBox\">", t);
        Assert.Contains("<Setter Property=\"Watermark\" Value=\"0\" />", t);
    }

    /// <summary>
    /// بندِ ۳: کادر خطِ برجستگیِ ۱px سفیدِ ۶٪ روی لبهٔ بالایی دارد و
    /// **سایه ندارد** («کادر ثابت هرگز سایه ندارد»).
    /// ‎inset‎ همان خط است، نه سایه.
    /// </summary>
    [Fact]
    public void PanelsHaveATopHighlightAndNoDropShadow()
    {
        var t = Theme();
        var panel = Between(t, "<Style Selector=\"Border.panel\">", "</Style>");
        Assert.Contains("BoxShadow", panel);
        Assert.Contains("inset", panel);
        Assert.Contains("<Setter Property=\"CornerRadius\" Value=\"10\" />", panel);
    }

    /// <summary>بندِ ۶: اسکرول‌بارِ سفارشیِ ۱۰px با دستگیرهٔ گرد.</summary>
    [Fact]
    public void TheScrollBarIsCustom()
    {
        var t = Theme();
        Assert.Contains("<Style Selector=\"ScrollBar[Orientation=Vertical]\">", t);
        Assert.Contains("<Setter Property=\"Width\" Value=\"10\" />", t);
        Assert.Contains("<Setter Property=\"CornerRadius\" Value=\"999\" />", t);
    }

    /// <summary>
    /// بندِ ۵٫۵: ردیفِ جمعِ کل خطِ **۲px** بالای خودش دارد تا از ردیف‌های
    /// عادی جدا شود.
    /// </summary>
    [Fact]
    public void TheTotalsRowHasATwoPixelSeparator()
    {
        var t = Theme();
        var bar = Between(t, "<Style Selector=\"c|TotalsBar\">", "</Style>");
        // ضخامتش هم از تنظیمات می‌آید — پیش‌فرضش همان ‎1,2,1,1‎ است
        Assert.Contains("BorderThickness=\"{DynamicResource Pump.Table.SumBorder}\"", bar);
        var style = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Themes", "TableStyle.cs"));
        Assert.Contains("new Thickness(line, sum, line, line)", style);
        Assert.Contains("TableSumLine { get; set; } = 2;",
            File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Services", "AppSettings.cs")));
    }

    /// <summary>
    /// ══ صفحهٔ چاپ ══════════════════════════════════════════════════════════
    /// گزارشِ صاحب ریپو: «آن بخشِ پرینت تمام صفحه نمی‌شود و آن بخش‌های مهم
    /// نمی‌آید.» دو علتش هر دو در همین فایل بود: پنجرهٔ اندازه‌ثابت، و سقفِ
    /// ‎MaxWidth=900‎ روی خودِ ورق.
    /// </summary>
    [Fact]
    public void ThePrintPreviewOpensFullScreenAndCanZoom()
    {
        var w = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Views",
                                              "DocumentPreviewWindow.axaml"));
        Assert.Contains("WindowState=\"Maximized\"", w);
        Assert.Contains("ZoomInCommand", w);
        Assert.Contains("ZoomOutCommand", w);
        Assert.Contains("ZoomFitCommand", w);

        // ⚠️ برای «دیگر نباید باشد» باید کامنت‌ها را برداشت، وگرنه آزمون
        // توضیحِ خودِ ما را می‌خواند و قرمز می‌شود: کامنتِ همان‌جا نوشته چرا
        // ‎MaxWidth="900"‎ برداشته شد، پس خودِ آن رشته در فایل هست.
        Assert.DoesNotContain("MaxWidth=\"900\"", NoComments(w));
    }

    /// <summary>کامنت‌های XAML را برمی‌دارد — برای آزمون‌های «دیگر نباید باشد».</summary>
    private static string NoComments(string xaml) =>
        System.Text.RegularExpressions.Regex.Replace(
            xaml, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);

    private static string Between(string s, string a, string b)
    {
        var i = s.IndexOf(a, StringComparison.Ordinal);
        Assert.True(i >= 0, "پیدا نشد: " + a);
        var j = s.IndexOf(b, i, StringComparison.Ordinal);
        return s[i..(j + b.Length)];
    }
}

/// <summary>
/// ══ جدولِ حسابِ قرض‌دار، ستون‌به‌ستون مثلِ سایت ══════════════════════════════
/// سایت (‎#pm-tbl‎): ‎# │ تاریخ │ نام │ حواله │ نوع تیل │ مقدار تیل │ فی لیتر │
/// مقدار بردگی │ رسید │ رسید تیل │ الباقی │ حذف‎.
/// </summary>
public class PersonColumnsTests
{
    private static string View() => File.ReadAllText(Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..",
        "PumpYaqobi.App", "Views", "Sections", "PersonView.axaml")));

    private static string Bare() => System.Text.RegularExpressions.Regex.Replace(
        View(), "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);

    /// <summary>«بردگیِ دستی» در سایت نیست — اختراعِ نیتیو بود و برداشته شد.</summary>
    [Fact]
    public void ThereIsNoManualBardagiColumn()
        => Assert.DoesNotContain("بردگیِ دستی", Bare());

    /// <summary>ستونِ «الباقی» که اصلاً نبود، حالا هست.</summary>
    [Fact]
    public void TheAlbaqiColumnExists()
        => Assert.Contains("Header=\"الباقی\"", Bare());

    /// <summary>نامِ ستون‌ها همان‌های سایت‌اند.</summary>
    [Theory]
    [InlineData("نوع تیل")]
    [InlineData("مقدار تیل")]
    [InlineData("فی لیتر")]
    [InlineData("مقدار بردگی")]
    [InlineData("رسید تیل")]
    public void ColumnHeadersMatchTheSite(string header)
        => Assert.Contains("Header=\"" + header + "\"", Bare());

    /// <summary>
    /// دو دکمهٔ خطرناک رفتند بالا، ولی با خطِ جداکننده از «📄 PDF» — خواستهٔ
    /// صریحِ صاحب ریپو: «کنجِ سمتِ راست، که دستم اشتباهی جای پی‌دی‌اف نزنم».
    /// </summary>
    [Fact]
    public void DeleteAndRenameSitInTheToolbarAwayFromPdf()
    {
        var v = Bare();
        var toolbar = v[v.IndexOf("<c:SectionPage.Toolbar>", StringComparison.Ordinal)
                        ..v.IndexOf("</c:SectionPage.Toolbar>", StringComparison.Ordinal)];
        Assert.Contains("RenamePersonCommand", toolbar);
        Assert.Contains("DeleteSubAccountCommand", toolbar);
        Assert.Contains("Pump.GridLine", toolbar);        // خطِ جداکننده
    }

    /// <summary>«حساب جداگانه…» شد «حساب پطرول» و «حساب دیزل».</summary>
    [Fact]
    public void TheFuelFiltersAreNamedPlainly()
    {
        var v = Bare();
        Assert.DoesNotContain("حساب جداگانه", v);
        Assert.Contains("⛽ حساب پطرول", v);
        Assert.Contains("🟤 حساب دیزل", v);
    }
}
