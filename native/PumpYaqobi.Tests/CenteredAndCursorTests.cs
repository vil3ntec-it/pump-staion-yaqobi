using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ وسط‌چینیِ همه‌جا و نشانگرِ «به‌علاوه»ی جدول ══════════════════════════════
///
/// دو گزارشِ صاحب ریپو:
///   «ماوس هم باید شکلِ مثبت می‌داشت، اگر جست‌وجو بکنی تو سایت و پیدا کنی.»
///   «جدول‌ها همه‌شون نوشته‌هاشون وسط نیستن … همه نوشته‌ها، همه همه همه
///    نوشته‌ها باید تو کادرهاشون وسط‌چین باشن، تو همه بخش‌ها.»
///
/// هر دو در سایت هستند و اندازه‌شان معلوم است:
///     .tbl td/th, .xls-tbl td/th, .ro-tbl td/th{ cursor:cell; }
///     همان‌ها و .safe-tbl و .rasid-tbl و .att-tbl{ text-align:center }
///
/// این آزمون‌ها متنی‌اند چون چیزی که برمی‌گردد همین است: کسی یک ‎Setter‎ را
/// برمی‌دارد یا یک ویو ‎HorizontalAlignment="Right"‎ می‌گذارد و بی‌سروصدا
/// نصفِ برنامه دوباره به لبه می‌چسبد.
/// </summary>
public class CenteredAndCursorTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Theme() =>
        File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Themes", "Controls.axaml"));

    /// <summary>پیش‌فرضِ هر نوشته وسط است، نه راست.</summary>
    [Fact]
    public void TextDefaultsToCentred()
    {
        var t = Theme();
        var block = Between(t, "<Style Selector=\"TextBlock\">", "</Style>");
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", block);
        Assert.DoesNotContain("Value=\"Right\"", block);
    }

    /// <summary>کادرهای تایپ هم وسط‌چین‌اند — مثلِ ‎.xls-in‎ی سایت.</summary>
    [Fact]
    public void TextBoxesAreCentred()
    {
        var t = Theme();
        var block = Between(t, "<Style Selector=\"TextBox\">", "</Style>");
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", block);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />", block);
    }

    /// <summary>
    /// خانه و سرستونِ جدول: هم وسط‌چین، هم نشانگرِ «به‌علاوه».
    /// ‎Cross‎ نزدیک‌ترین چیزِ آوالونیا به ‎cursor:cell‎ی سایت است.
    /// </summary>
    [Fact]
    public void GridCellsAreCentredAndShowThePlusCursor()
    {
        var t = Theme();

        var cell = Between(t, "<Style Selector=\"DataGridCell\">", "</Style>");
        Assert.Contains("<Setter Property=\"Cursor\" Value=\"Cross\" />", cell);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />", cell);

        var head = Between(t, "<Style Selector=\"DataGridColumnHeader\">", "</Style>");
        Assert.Contains("<Setter Property=\"Cursor\" Value=\"Cross\" />", head);

        // و نوشتهٔ داخلِ خانه هم واقعاً وسط بنشیند
        Assert.Contains("<Style Selector=\"DataGridCell TextBlock\">", t);
        Assert.Contains("<Style Selector=\"DataGridCell TextBox\">", t);
    }

    /// <summary>
    /// ⚠️ هیچ ویویی نوشته‌ای را به راست نچسباند. ‎HorizontalAlignment="Right"‎
    /// روی ‎Border‎ و ‎Button‎ و ‎Grid‎ (جای‌گیریِ خودِ کادر) مجاز است — روی
    /// ‎TextBlock‎ نه، چون همان است که وسط‌چینی را می‌شکند.
    /// </summary>
    [Fact]
    public void NoViewPinsTextToTheRight()
    {
        var views = Path.Combine(Root(), "PumpYaqobi.App", "Views");
        var offenders = new List<string>();

        foreach (var f in Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(text, @"<TextBlock\b[^>]*>", RegexOptions.Singleline))
                if (m.Value.Contains("HorizontalAlignment=\"Right\"") ||
                    m.Value.Contains("TextAlignment=\"Right\""))
                    offenders.Add(Path.GetFileName(f) + " → " + Squash(m.Value));
        }

        Assert.True(offenders.Count == 0,
            "این نوشته‌ها به راست چسبیده‌اند، در حالی که همه باید وسط باشند:\n"
            + string.Join("\n", offenders));
    }

    private static string Between(string s, string open, string close)
    {
        var a = s.IndexOf(open, StringComparison.Ordinal);
        Assert.True(a >= 0, "این استایل پیدا نشد: " + open);
        var b = s.IndexOf(close, a, StringComparison.Ordinal);
        Assert.True(b > a);
        return s[a..b];
    }

    private static string Squash(string s) =>
        Regex.Replace(s, @"\s+", " ").Trim() is { Length: > 110 } t ? t[..110] + "…" : Regex.Replace(s, @"\s+", " ").Trim();
}
