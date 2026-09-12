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
    /// ══ چپ/راست: همان‌جایی که چشم می‌بیند ═══════════════════════════════════
    ///
    /// ⚠️ این آزمون **سه بار** عوض شده و هر سه بار به خاطرِ یک گزارشِ تکراری:
    /// «کلیدِ راست را می‌زنم، چپ می‌رود.» تاریخچه‌اش را بخوان و دوباره عوضش
    /// نکن:
    ///
    ///   ۱) جهت از ‎FlowDirection‎ خوانده می‌شد — به کنترل نمی‌رسید.
    ///   ۲) ایندکسِ خام شد (‎Right → column+1‎) — در جدولِ راست‌به‌چپ ستونِ
    ///      «بعدی» سمتِ چپ است، پس همان شکایت برگشت.
    ///   ۳) از جای سربرگ‌ها اندازه گرفته شد — آوالونیا راست‌به‌چپ را با
    ///      آینه‌کردنِ **رسم** انجام می‌دهد و مختصاتش هنوز چپ‌به‌راست است.
    ///
    /// قاعدهٔ نهایی، بی هیچ تشخیصی: کلِ برنامه راست‌به‌چپ است و ستونِ ۰ سمتِ
    /// راست می‌نشیند، پس ‎→‎ ایندکسِ کمتر و ‎←‎ ایندکسِ بیشتر.
    /// </summary>
    [Fact]
    public void ArrowKeysFollowWhatTheEyeSees()
    {
        var g = Grid();
        Assert.Contains("MoveColumn(e.Key == Key.Right ? -1 : +1, shift)", g);

        // نه ‎FlowDirection‎، نه اندازه‌گیریِ سربرگ — هیچ تشخیصی در کار نیست
        var at = g.IndexOf("case Key.Left:", StringComparison.Ordinal);
        Assert.True(at > 0);
        Assert.DoesNotContain("FlowDirection", g[at..(at + 400)]);
        Assert.DoesNotContain("ColumnsRunRightToLeft", g);
    }

    /// <summary>
    /// در حالتِ ویرایش، فلش‌ها هرگز خانه عوض نمی‌کنند — نه وسطِ متن و نه در
    /// لبهٔ آن. پیش از این از روی جای کُرسر حدس زده می‌شد و همان حدس در لبهٔ
    /// متن می‌شکست («موقعِ تایپ می‌پرد سمتِ دیگر»). حالا حالت را خودِ جدول
    /// می‌گوید.
    /// </summary>
    [Fact]
    public void ArrowsDoNotStealTheCaretWhileTyping()
    {
        var g = Grid();
        Assert.Contains("if (_editing && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)", g);
        // حالت از رویدادهای خودِ جدول خوانده می‌شود، نه از جای کُرسر
        Assert.Contains("PreparingCellForEdit", g);
        Assert.Contains("CellEditEnded", g);
        Assert.DoesNotContain("CaretInsideText", g);
    }

    // ══ کنترلرِ مرکزیِ سه‌حالته ═══════════════════════════════════════════════

    /// <summary>هر سه حالتِ خواسته‌شده واقعاً وجود دارند و ‎Mode‎ آن‌ها را می‌گوید.</summary>
    [Fact]
    public void TheGridHasThreeExplicitStates()
    {
        var g = Grid();
        Assert.Contains("public enum GridMode", g);
        Assert.Contains("Selected,", g);
        Assert.Contains("Editing,", g);
        Assert.Contains("MultiSelect", g);
        Assert.Contains("public GridMode Mode =>", g);
    }

    /// <summary>‎Tab‎ خانهٔ بعدی و ‎Shift+Tab‎ خانهٔ پیشین — و ته ردیف ⇒ ردیفِ بعد.</summary>
    [Fact]
    public void TabWalksCellByCell()
    {
        var g = Grid();
        Assert.Contains("MoveCell(shift ? -1 : +1)", g);
        Assert.Contains("if (next >= cols.Count) { MoveRow(+1); next = 0; }", g);
        Assert.Contains("else if (next < 0) { MoveRow(-1); next = cols.Count - 1; }", g);
    }

    /// <summary>‎Shift+فلش‎ کادرِ چندانتخابی می‌سازد و ‎Delete‎ خالی‌اش می‌کند.</summary>
    [Fact]
    public void ShiftArrowSelectsManyCellsAndDeleteClearsThem()
    {
        var g = Grid();
        Assert.Contains("MoveColumn(e.Key == Key.Right ? -1 : +1, shift)", g);
        Assert.Contains("_colAnchor", g);
        Assert.Contains("rangesel", g);
        Assert.Contains("case Key.Delete when !IsReadOnly && ClearSelectedCells():", g);
    }

    /// <summary>‎Esc‎ ویرایش را لغو می‌کند، کادر را جمع می‌کند و برمی‌گردد به انتخاب.</summary>
    [Fact]
    public void EscapeCancelsAndRestores()
    {
        var g = Grid();
        Assert.Contains("if (e.Key == Key.Escape)", g);
        Assert.Contains("CancelEdit(DataGridEditingUnit.Cell)", g);
        Assert.Contains("_colAnchor = _colHead = -1;", g);
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
