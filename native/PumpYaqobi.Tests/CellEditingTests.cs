using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ویرایشِ خانه‌های همهٔ جدول‌ها (۱۴۰۵/۰۷/۲۰) ══════════════════════════════
/// صاحب ریپو: «پیست کردم و بعد نوشتم یا فاصله زدم، متنِ قبلی پاک شد؛ با دوبار-کلیک
/// مکان‌نما ظاهر نمی‌شود؛ انتخابِ متن دیده نمی‌شود؛ عددِ تایپ‌شده بزرگ‌تر از بقیه است.»
/// رفتارِ واقعی را کاوشگرِ ‎undokeys‎ در پنجرهٔ واقعی می‌سنجد؛ این‌جا قفلِ سورس است
/// تا ریشه‌ها برنگردند. همه در ‎ExcelGrid‎ و سبکِ مشترک‌اند، پس همهٔ جدول‌ها با هم.
/// </summary>
public class CellEditingTests
{
    private static string Root()
    {
        var d = AppContext.BaseDirectory;
        while (d is not null && !File.Exists(Path.Combine(d, "PumpYaqobi.sln"))) d = Path.GetDirectoryName(d);
        return d!;
    }

    private static string Src(string rel) => SrcText.Read(Path.Combine(Root(), rel));

    [Fact]
    public void DoubarClick_MakanNamaZireKlik_NaEntekhabeHame()
    {
        var k = Src("PumpYaqobi.App/Controls/ExcelGrid.Keys.cs");
        Assert.Contains("if (e.EditingEventArgs is PointerPressedEventArgs press) CaretAtPointer(eb2, press);", k);
        Assert.Contains("tp.MoveCaretToPoint(press.GetPosition(tp));", k);
        Assert.Contains("box.SelectionStart = box.SelectionEnd = i;", k);
    }

    [Fact]
    public void Chasbandan_BadAzAn_TaypPosht_E_Matn()
    {
        var c = Src("PumpYaqobi.App/Controls/ExcelGrid.Clipboard.cs");
        Assert.Contains("if (log.Count == 1 && cells.Count == 1 && cells[0].Length == 1 && !_editing)", c);
        Assert.Contains("_caretToEnd = true;", c);
        Assert.Contains("else if (_caretToEnd) CaretAtEnd(eb2);", Src("PumpYaqobi.App/Controls/ExcelGrid.Keys.cs"));
    }

    [Fact]
    public void EntekhabeMatn_Narenji_VaFaghatBarayeMatn()
    {
        var x = Src("PumpYaqobi.App/Themes/Controls.axaml");
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(x,
            @"<Setter Property=""SelectionBrush"" Value=""\{DynamicResource Pump\.TextSelection\}"" />").Count);
        Assert.DoesNotContain(@"<Setter Property=""SelectionBrush"" Value=""{DynamicResource Pump.Selected}"" />", x);
        var tm = Src("PumpYaqobi.App/Themes/ThemeManager.cs");
        Assert.Contains("Br(\"TextSelection\", PumpTheme.C(\"#ffa94d\"));", tm);
        Assert.Contains("Br(\"OnTextSelection\", PumpTheme.C(\"#1b1200\"));", tm);
        //  آبیِ ردیفِ انتخاب‌شده دست نخورد
        Assert.Contains("Br(\"Selected\", Mix(t.Card, t.Accent", tm);
    }

    [Fact]
    public void KadreTayp_HamVazn_VaHamAndazeyeKhane()
    {
        var x = Src("PumpYaqobi.App/Themes/Controls.axaml");
        var cell = System.Text.RegularExpressions.Regex.Match(x,
            @"<Style Selector=""DataGridCell"">[\s\S]*?FontSize"" Value=""(\d+)""").Groups[1].Value;
        var box = System.Text.RegularExpressions.Regex.Match(x,
            @"<Style Selector=""DataGridCell TextBox"">([\s\S]*?)</Style>").Groups[1].Value;
        Assert.Contains($"<Setter Property=\"FontSize\" Value=\"{cell}\" />", box);
        Assert.Contains("<Setter Property=\"FontWeight\" Value=\"Normal\" />", box);
        Assert.DoesNotContain("<Setter Property=\"FontWeight\" Value=\"Bold\" />", box);
    }
}
