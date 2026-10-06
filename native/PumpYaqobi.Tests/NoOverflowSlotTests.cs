using Avalonia;
using Avalonia.Controls;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ هیچ خانه و نواری پهن‌تر از جایش چیده نشود (۱۴۰۵/۰۷/۲۱) ══
///
/// ‎StackPanel‎ِ آوالونیا فرزند را به «بیشترِ جا و پهنای دلخواهِ خودش» می‌چیند. خانهٔ
/// نوارِ «جمله» با پهنای بی‌نهایت اندازه گرفته می‌شود، پس عددِ بلندِ یک ماه
/// («۸۵۲,۳۰۰ افغانی · ۸۵۱,۷۰۰ $») تا ۴۶ پیکسل از خانه بیرون می‌زد و وسط‌چینی‌اش کج می‌شد؛
/// نوارِ میانبرهای زیرِ جدول هم همین‌طور از لبهٔ کادر. ‎DockPanel‎ همان پهنای جا را می‌دهد.
/// رفتارش را ‎monthshift all‎ (‎MS_WIDTH=1093‎) با پنجرهٔ واقعی می‌سنجد.
/// </summary>
public class NoOverflowSlotTests
{
    //  مثلِ ‎TextBlock‎: پهنای ثابت ندارد، فقط محتوایش ۳۰۰ پیکسل می‌خواهد
    private static Control Wide() => new Border { Height = 20, Child = new Border { Width = 300 } };

    private static Rect Place(Panel p)
    {
        var child = Wide();
        p.Children.Add(child);
        var host = new Border { Child = p };
        //  همان کاری که ‎TotalsStrip‎ می‌کند: اندازه با پهنای بی‌نهایت، چیدن در خانهٔ ۱۰۰ پیکسلی
        host.Measure(new Size(double.PositiveInfinity, 100));
        host.Arrange(new Rect(0, 0, 100, 40));
        return child.Bounds;
    }

    /// <summary>سازوکار: ‎StackPanel‎ فرزندِ پهن را بیرون می‌چیند (اگر آوالونیا درستش کند این سرخ می‌شود).</summary>
    [Fact]
    public void StackPanel_FarzandePahnRaBiroonMichinad() =>
        Assert.True(Place(new StackPanel()).Width > 100);

    [Fact]
    public void DockPanel_DarJayeKhodashMichinad() =>
        Assert.True(Place(new DockPanel()).Right <= 100.5);

    private static string Theme()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        return File.ReadAllText(Path.Combine(d!.FullName, "PumpYaqobi.App", "Themes", "Controls.axaml"));
    }

    private static string Between(string t, string a, string b)
    {
        var i = t.IndexOf(a, StringComparison.Ordinal);
        Assert.True(i >= 0, a);
        var j = t.IndexOf(b, i, StringComparison.Ordinal);
        return t[i..j];
    }

    /// <summary>⛔ خانهٔ نوارِ «جمله»: برچسب و عدد در ‎DockPanel‎، نه ‎StackPanel‎.</summary>
    [Fact]
    public void KhaneyeJomle_DockPanelAst()
    {
        var bar = Between(Theme(), "<Style Selector=\"c|TotalsBar\">", "</Style>");
        var cell = Between(bar, "<ItemsControl.ItemTemplate>", "</ItemsControl.ItemTemplate>");
        Assert.Contains("<DockPanel", cell);
        Assert.DoesNotContain("<StackPanel", cell);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", cell);
    }

    /// <summary>⛔ نوارِ «➕ ردیف» و میانبرها: ‎DockPanel‎ — نوارِ میانبر جای مانده را می‌گیرد و می‌شکند.</summary>
    [Fact]
    public void NavareRadif_DockPanelAst()
    {
        var bar = Between(Theme(), "<Style Selector=\"c|RowAddBar\">", "</Style>");
        var top = bar[bar.IndexOf("<ControlTemplate>", StringComparison.Ordinal)..];
        var firstPanel = System.Text.RegularExpressions.Regex.Match(top, @"<(StackPanel|DockPanel)\b[^>]*>").Value;
        Assert.StartsWith("<DockPanel", firstPanel);
        Assert.Contains("c:HintStrip HintKey=\"rows\"", bar);
    }
}
