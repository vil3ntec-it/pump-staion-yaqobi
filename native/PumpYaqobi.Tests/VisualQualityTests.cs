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
    /// بندِ ۵٫۲: «جداکنندهٔ افقی ۱px · هیچ خط عمودی».
    /// بندِ ۸ (ممنوعیت‌ها): «خط عمودی جدول».
    /// </summary>
    [Fact]
    public void TablesHaveNoVerticalGridLines()
    {
        var t = Theme();
        Assert.Contains("<Setter Property=\"GridLinesVisibility\" Value=\"Horizontal\" />", t);
        // «دیگر نباید باشد» همیشه روی متنِ بی‌کامنت — وگرنه روزی که کسی در
        // کامنت بنویسد «خطِ عمودی برداشته شد»، آزمون خودِ آن جمله را می‌خواند.
        var bare = NoComments(t);
        Assert.DoesNotContain("<Setter Property=\"GridLinesVisibility\" Value=\"All\" />", bare);
        Assert.DoesNotContain("VerticalGridLinesBrush", bare);
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
        Assert.Contains("BorderThickness=\"1,2,1,1\"", bar);
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
