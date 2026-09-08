using PumpYaqobi.Application.Localization;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ صفحه‌کلید و «صفرِ نامرئی» ═══════════════════════════════════════════════
///
/// چهار گزارشِ صاحب ریپو، همه از رفتارِ خودِ سایت گرفته شده‌اند:
///   • «با تب نمی‌شود نوع تیل / نوع / واحد را عوض کرد.»
///   • «کلیدهای چپ و راست برعکس کار می‌کنند.»
///   • «موقعِ تایپ هم نمی‌روند سمتِ دیگر.»
///   • «توی همهٔ جدول‌ها صفر نوشته است؛ صفرِ نامرئی باشد.»
/// </summary>
public class KeyboardAndZeroTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Grid() =>
        File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Controls", "ExcelGrid.cs"));

    /// <summary>
    /// ‎Tab‎ روی خانهٔ کشویی/رادیویی مقدار را عوض می‌کند و فوکوس را نمی‌بَرد —
    /// همان ‎_toggleControl‎ی سایت. ‎Enter‎ هم همان.
    /// </summary>
    [Fact]
    public void TabAndEnterToggleADropdownCell()
    {
        var g = Grid();
        Assert.Contains("Key.Tab", g);
        Assert.Contains("ToggleCell()", g);
        Assert.Contains("IsToggleColumn", g);
        Assert.Contains("case Key.Enter when !IsReadOnly && IsToggleColumn(CurrentColumn) && ToggleCell():", g);
    }

    /// <summary>
    /// چپ/راست باید **دیداری** باشند. در چیدمانِ راست‌به‌چپ، ستونِ بعدی سمتِ
    /// چپ است — پس جهت با ‎FlowDirection‎ حساب می‌شود، نه با ایندکسِ خامِ ستون
    /// (که همان «برعکس»ی بود که گزارش شد).
    /// </summary>
    [Fact]
    public void ArrowKeysFollowTheVisualDirection()
    {
        var g = Grid();
        Assert.Contains("Key.Left or Key.Right", g);
        Assert.Contains("FlowDirection.RightToLeft", g);
        Assert.Contains("(e.Key == Key.Left) == rtl ? +1 : -1", g);
    }

    /// <summary>کُرسر وسطِ متن ⇒ چپ/راست متن را ویرایش می‌کند، نه خانه عوض.</summary>
    [Fact]
    public void ArrowsDoNotStealTheCaretWhileTyping()
    {
        var g = Grid();
        Assert.Contains("CaretInsideText()", g);
        Assert.Contains("tb.SelectionStart == tb.SelectionEnd", g);
    }

    /// <summary>
    /// نوارِ بخش‌ها با کشیدنِ افقیِ تاچ‌پد هم می‌لغزد — پیش از این فقط
    /// ‎Delta.Y‎ خوانده می‌شد و کشیدنِ چپ/راستِ لپ‌تاپ نادیده می‌ماند.
    /// </summary>
    [Fact]
    public void TheNavStripScrollsWithAHorizontalSwipe()
    {
        var n = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Controls", "NavStrip.cs"));
        Assert.Contains("e.Delta.X", n);
    }

    // ══ صفرِ نامرئی ══════════════════════════════════════════════════════════

    /// <summary>صفر ⇒ خالی؛ بقیهٔ عددها دست‌نخورده.</summary>
    [Theory]
    [InlineData(0, "")]
    [InlineData(5, "5")]
    [InlineData(-3, "-3")]
    [InlineData(1500, "1,500")]
    public void ZeroBecomesBlank(int v, string expected)
        => Assert.Equal(expected, Shamsi.MoneyOrBlank(v));

    /// <summary>
    /// هیچ خانهٔ **تایپ‌شدنی**ای نباید صفرِ خام نشان بدهد. الگویش یکی است:
    /// ‎get => Shamsi.Money(x); set => …‎ یعنی خانه‌ای که هم خوانده و هم
    /// نوشته می‌شود — و همان‌هاست که باید ‎MoneyOrBlank‎ باشند.
    ///
    /// ⚠️ عددهای فقط‌خواندنی (جمع‌ها، بردگی، الباقی) عمداً بیرون‌اند: «۰»
    /// آن‌جا خودش یک خبر است، نه یک خانهٔ خالی.
    /// </summary>
    [Fact]
    public void NoEditableCellShowsARawZero()
    {
        var bad = new List<string>();
        var vms = Path.Combine(Root, "PumpYaqobi.App", "ViewModels");
        foreach (var f in Directory.EnumerateFiles(vms, "*.cs", SearchOption.AllDirectories))
            foreach (var line in File.ReadLines(f))
                if (line.Contains("get => Shamsi.Money(") && line.Contains("set =>"))
                    bad.Add(Path.GetFileName(f) + ": " + line.Trim());

        Assert.True(bad.Count == 0,
            "این خانه‌های تایپ‌شدنی هنوز صفرِ خام نشان می‌دهند:\n" + string.Join("\n", bad));
    }
}
