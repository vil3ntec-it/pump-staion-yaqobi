using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «پس از رفتن به ماهِ پیش، نوشتهٔ بعضی خانه‌ها به چپ رفت» (۱۴۰۵/۰۷/۲۲) ══════════
///
/// عکسِ صاحب ریپو با آخرین نسخه: در «مصارف» تاریخِ ردیف‌های ۴، ۷ و ۱۰ و در «گاوصندوق»
/// «0»ِ ردیف‌های ۲ و ۳ به لبهٔ چپِ همان خانه چسبیده بودند و خطِ خانه سرِ جایش بود —
/// یعنی خانه درست چیده شده بود و فقط وسط‌چینیِ <b>داخلِ</b> نوشته (‎TextAlignment‎، که
/// به ‎TextLayout‎ِ ساخته‌شده با پهنای یک بارِ چیدن بند است) از کار افتاده بود.
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

    /// <summary>
    /// ⛔ نوشتهٔ خانه هم‌پهنای خانه می‌ماند (‎Stretch‎) و ‎TextLayout‎ِ پهنای کهنه یا بی‌نهایت از نو
    /// چیده می‌شود. ⚠️ «کادرِ هم‌قدِ نوشته» (‎HorizontalAlignment=Center‎) آزموده و پس گرفته شد:
    /// روی ویندوز «حوالهٔ 1» و «مصرفِ شمارهٔ 1» را ۵ پیکسل کج کرد (‎align-windows‎ ⇒ ‎oldmonths‎).
    /// </summary>
    [Fact]
    public void LayoutKohne_AzNoChideMishavad_VaKadrHamPahnayeKhane()
    {
        var t = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        Assert.DoesNotContain("<Style Selector=\"DataGridCell > TextBlock\">", t);
        var cell = t[t.IndexOf("<Style Selector=\"DataGridCell TextBlock\">", StringComparison.Ordinal)..];
        cell = cell[..cell.IndexOf("</Style>", StringComparison.Ordinal)];
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\" />", cell);
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", cell);
        Assert.Contains("c:RtlTrim.Enabled", cell);

        var s = Read("PumpYaqobi.App", "Controls", "RtlTrim.cs");
        var r = s.IndexOf("private static void Recenter", StringComparison.Ordinal);
        var body = s[r..s.IndexOf("var dx = CenterFix(t);", r, StringComparison.Ordinal)];
        Assert.Contains("double.IsInfinity(lay.MaxWidth)", body);
        Assert.Contains("t.InvalidateMeasure();", body);
        Assert.Contains("RelaidProperty", body);      //  یک بار برای هر (متن، پهنا) — بی حلقهٔ چیدمان
    }

    /// <summary>
    /// ⛔ نقاشیِ کهنه نمی‌ماند: پس از چیدمانِ تمام، ‎Recenter‎ همان نوشته را یک بار دوباره می‌کشد.
    /// با پنجرهٔ واقعی سنجیده شد (‎realshift‎، پیکسل): بی این خط در هر سه مقیاس ۲۸ ایراد، با آن صفر.
    /// </summary>
    [Fact]
    public void NaqqashiyeKohne_PasAzChidman_DobareKeshideMishavad()
    {
        var s = Read("PumpYaqobi.App", "Controls", "RtlTrim.cs");
        var r = s.IndexOf("private static void Recenter", StringComparison.Ordinal);
        var guard = s.IndexOf("if (!t.IsMeasureValid || !t.IsArrangeValid)", r, StringComparison.Ordinal);
        var paint = s.IndexOf("t.InvalidateVisual();", r, StringComparison.Ordinal);
        var fix = s.IndexOf("var dx = CenterFix(t);", r, StringComparison.Ordinal);
        Assert.True(guard > r && paint > guard && paint < fix,
            "‎InvalidateVisual‎ باید پس از سنجشِ چیدمانِ تمام و پیش از ‎CenterFix‎ در ‎Recenter‎ باشد");
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
