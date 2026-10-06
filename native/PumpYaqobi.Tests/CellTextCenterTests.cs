using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «پس از رفتن به ماهِ پیش، نوشتهٔ بعضی خانه‌ها به چپ رفت» (۱۴۰۵/۰۷/۲۲) ══════════
///
/// عکسِ صاحب ریپو با آخرین نسخه: در «مصارف» تاریخِ ردیف‌های ۴، ۷ و ۱۰ و در «گاوصندوق»
/// «0»ِ ردیف‌های ۲ و ۳ به لبهٔ چپِ همان خانه چسبیده بودند و خطِ خانه سرِ جایش بود —
/// یعنی وسط‌چینیِ <b>داخلِ</b> نوشته (‎TextAlignment‎ روی کادرِ هم‌پهنای خانه، که به
/// پهنای یک بارِ چیدن بند است) از کار افتاده بود، نه خودِ خانه.
///
/// رفتار با پنجرهٔ واقعی سنجیده می‌شود (‎realshift‎ زیرِ X11، و ‎monthshift all blank-*‎
/// روی ویندوز در سه مقیاس). این‌جا خودِ قاعده‌ها قفل‌اند تا برنگردند.
/// </summary>
public class CellTextCenterTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] p) => SrcText.Read(Path.Combine(new[] { Root() }.Concat(p).ToArray()));

    /// <summary>⛔ کادرِ نوشتهٔ خانه هم‌قدِ نوشته است و خانه وسطش می‌گذارد.</summary>
    [Fact]
    public void Neveshte_RaKhane_VasatMigozarad()
    {
        var t = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        var i = t.IndexOf("<Style Selector=\"DataGridCell > TextBlock\">", StringComparison.Ordinal);
        Assert.True(i > 0, "سبکِ «DataGridCell > TextBlock» نیست");
        var block = t[i..t.IndexOf("</Style>", i, StringComparison.Ordinal)];
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Center\" />", block);
        //  وسط‌چینیِ داخلِ نوشته و ‎RtlTrim‎ سرِ جایشان
        var cell = t[t.IndexOf("<Style Selector=\"DataGridCell TextBlock\">", StringComparison.Ordinal)..];
        cell = cell[..cell.IndexOf("</Style>", StringComparison.Ordinal)];
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", cell);
        Assert.Contains("c:RtlTrim.Enabled", cell);
    }

    /// <summary>⛔ جابه‌جاییِ وسط‌چینیِ متنِ پیشین با متنِ تازه نمی‌ماند؛ خانهٔ برگشته دوباره سنجیده می‌شود.</summary>
    [Fact]
    public void JabejayiKohne_BaMatneTaze_Nemimanad()
    {
        var s = Read("PumpYaqobi.App", "Controls", "RtlTrim.cs");
        var h = s.IndexOf("TextBlock.TextProperty.Changed.AddClassHandler", StringComparison.Ordinal);
        var body = s[h..s.IndexOf("});", h, StringComparison.Ordinal)];
        Assert.Contains("t.RenderTransform = null;", body);
        Assert.Contains("Control.LoadedEvent.AddClassHandler<TextBlock>", s);
    }
}
