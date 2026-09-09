using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ جدول‌ها بلند می‌شوند، کوچک نمی‌مانند ═════════════════════════════════════
///
/// گزارشِ صاحب ریپو، روشن و بند‌به‌بند: «ساختارِ Excel-like فعلی مورد تأیید
/// است، فقط این مشکل‌ها را ریشه‌ای درست کن — قفل شدنِ ارتفاعِ جدول بعد از چند
/// ردیف، اسکرولِ ناخواسته فقط داخلِ جدول، رشد نکردنِ ارتفاعِ صفحه، شکستنِ خطوط،
/// جابه‌جاییِ عرضِ ستون‌ها، Center نبودن، و Inputِ کوچکِ سفیدِ بزرگ‌شونده.»
///
/// این آزمون متنی است چون چیزی که برگشتنی است همین است: کسی دوباره سقفِ
/// ارتفاع می‌گذارد یا سبکِ کادرِ تایپ را برمی‌دارد. رفتارِ واقعی‌اش را
/// ‎ParchaWaraqAudit‎ روی پنجرهٔ واقعی می‌سنجد (ردیف اضافه می‌کند و بلندی را
/// پیش و پس می‌سنجد).
/// </summary>
public class TableGrowthTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Grid() => Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
    private static string Theme() => Read("PumpYaqobi.App", "Themes", "Controls.axaml");

    /// <summary>همان متن، بی کامنت — برای «این چیز دیگر نباید باشد».</summary>
    private static string Bare(string s) =>
        Regex.Replace(Regex.Replace(s, "<!--.*?-->", "", RegexOptions.Singleline),
                      @"(?m)^\s*(///|//).*$", "");

    // ══ ۱) سقفِ «یک صفحه» رفته است ═══════════════════════════════════════════

    /// <summary>دیگر هیچ‌جا ارتفاعِ جدول به یک صفحه بسته نمی‌شود.</summary>
    [Fact]
    public void TheOneScreenCeilingIsGone()
        => Assert.DoesNotContain("CapToOneScreen", Bare(Grid()));

    /// <summary>
    /// و تا مرزِ رشد هیچ سقفی روی جدول نمی‌نشیند: نه ‎MaxHeight‎ی هست و نه
    /// چیزی که بلندیِ در دسترس را کم کند.
    ///
    /// ⚠️ تنگنا در ‎MeasureOverride‎ اعمال می‌شود، نه پس از چیدمان — وگرنه
    /// همان پاسِ اولِ اندازه‌گیری با بلندیِ بی‌کران انجام شده و جدولِ بزرگ
    /// همان لحظه همهٔ ردیف‌هایش را ساخته است.
    /// </summary>
    [Fact]
    public void BelowTheLimitTheGridHasNoCeilingAtAll()
    {
        var g = Bare(Grid());
        Assert.DoesNotContain("MaxHeight =", g);
        Assert.Contains("MeasureOverride", g);
        Assert.Contains("GrowRowLimit", g);
    }

    /// <summary>
    /// مرزِ رشد باید آن‌قدر بزرگ باشد که هیچ جدولِ واقعیِ برنامه به آن نرسد —
    /// وگرنه دوباره همان کادرِ محدود می‌شود. ۵۰ ردیفِ ورق باید خیلی زیرش باشد.
    /// </summary>
    [Fact]
    public void TheGrowLimitIsFarAboveAnyRealTable()
    {
        var m = Regex.Match(Grid(), @"GrowRowLimit\s*=\s*(\d+)");
        Assert.True(m.Success, "مرزِ رشد پیدا نشد");
        Assert.True(int.Parse(m.Groups[1].Value) >= 500,
                    "مرزِ رشد نباید کمتر از ۵۰۰ ردیف باشد");
    }

    // ══ ۲) پهنای ستون با تایپ تکان نخورد ════════════════════════════════════

    /// <summary>
    /// در جدولِ پهن هم پهنای ستون سفت می‌شود؛ پیش از این آن‌جا ستون ‎Auto‎
    /// می‌ماند و با هر حرفی که تایپ می‌شد پهن‌تر می‌گشت.
    /// </summary>
    [Fact]
    public void ColumnWidthsAreFrozenEvenWhenThereIsNoSpareRoom()
    {
        var g = Bare(Grid());
        Assert.Contains("DataGridLengthUnitType.Pixel", g);
        Assert.Contains("DataGridLengthUnitType.Star", g);
    }

    // ══ ۳) کادرِ تایپ هم‌اندازهٔ خانه ════════════════════════════════════════

    [Theory]
    [InlineData("<Setter Property=\"MinHeight\" Value=\"0\" />")]
    [InlineData("<Setter Property=\"MinWidth\" Value=\"0\" />")]
    [InlineData("<Setter Property=\"Padding\" Value=\"0\" />")]
    [InlineData("<Setter Property=\"FontSize\" Value=\"17\" />")]
    [InlineData("<Setter Property=\"TextWrapping\" Value=\"NoWrap\" />")]
    [InlineData("<Setter Property=\"AcceptsReturn\" Value=\"False\" />")]
    public void TheCellEditorMatchesTheCell(string setter)
    {
        var block = Between(Theme(), "<Style Selector=\"DataGridCell TextBox\">", "</Style>");
        Assert.Contains(setter, block);
    }

    /// <summary>و همچنان بی‌رنگ و بی‌لبه می‌ماند — نه کادرِ سفیدِ تودرتو.</summary>
    [Fact]
    public void TheCellEditorStaysInvisible()
    {
        var block = Between(Theme(), "<Style Selector=\"DataGridCell TextBox\">", "</Style>");
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", block);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", block);
    }

    // ══ ۴) وسط‌چین، افقی و عمودی ════════════════════════════════════════════

    [Fact]
    public void CellsAndHeadersAreCenteredBothWays()
    {
        var cell = Between(Theme(), "<Style Selector=\"DataGridCell\">", "</Style>");
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />", cell);
        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\" />", cell);

        var head = Between(Theme(), "<Style Selector=\"DataGridColumnHeader\">", "</Style>");
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />", head);
        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\" />", head);
    }

    // ══ ۵) خطوطِ اکسلیِ جدول دست‌نخورده ═════════════════════════════════════

    /// <summary>
    /// ⚠️ قاعدهٔ صریحِ صاحب ریپو: «جدول‌ها را از حالت Excel-like خارج نکن.»
    /// خطِ افقی و عمودی و ضخامتشان باید همان بمانند.
    /// </summary>
    [Fact]
    public void TheExcelLookIsUntouched()
    {
        var t = Theme();
        Assert.Contains("<Setter Property=\"GridLinesVisibility\" Value=\"All\" />", t);
        Assert.Contains("PART_BottomGridLine", t);
        Assert.Contains("PART_RightGridLine", t);
        Assert.Contains("<Setter Property=\"RowHeight\" Value=\"44\" />", t);
    }

    private static string Between(string s, string from, string to)
    {
        var i = s.IndexOf(from, StringComparison.Ordinal);
        Assert.True(i >= 0, "پیدا نشد: " + from);
        var j = s.IndexOf(to, i, StringComparison.Ordinal);
        Assert.True(j > i, "پایانِ سبک پیدا نشد");
        return s[i..j];
    }
}
