using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PumpYaqobi.App.Controls;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «کادرِ یک جدول را پاک کردم، همهٔ سربرگ‌ها برگشتند وسط» (۱۴۰۵/۰۷/۲۱) ══
///
/// بدنهٔ هر بخش داخلِ قابِ بزرگ‌شوندهٔ ‎A−/A+‎ است. ‎LayoutTransformControl‎ِ خامِ
/// آوالونیا فرزندی را که پهن‌تر از «جا ÷ بزرگنمایی» بخواهد به همان پهنای بزرگ
/// می‌چیند و وسط می‌گذارد ⇒ کلِ بدنه (جدول، سرستون‌ها، کارت‌ها) به یک سو می‌رود؛ و
/// فرزند دقیقاً وقتی چنین می‌خواهد که محتوایش تمامِ پهنا را بخواهد (ردیفِ بلندِ
/// «📝 فروش ورق»، عددِ درازترِ یک ماه). ‎ScaleBody‎ همان را در جای خودش نگه می‌دارد.
/// </summary>
public class ScaleBodyTests
{
    /// <summary>همان شکلِ قالبِ ‎SectionPage‎: شبکهٔ بدنه با حاشیهٔ ۱۴ و محتوایی که تمامِ پهنا را می‌خواهد.</summary>
    private static (Decorator Host, Grid Root, Border Content) Lay(Decorator host, double scale, double wide, double width = 1000, double? measured = null)
    {
        var content = new Border { Width = wide, Height = 300 };
        var root = new Grid { Margin = new Thickness(14) };
        root.Children.Add(content);
        host.Child = root;
        if (host is LayoutTransformControl ltc) ltc.LayoutTransform = new ScaleTransform(scale, scale);
        host.HorizontalAlignment = HorizontalAlignment.Stretch;
        host.Measure(new Size(measured ?? width, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
        return (host, root, content);
    }

    /// <summary>
    /// سازوکارِ ریشه، روی خودِ آوالونیا: با «‎A+‎» (۱.۰۸) و محتوای تمام‌پهنا، شبکهٔ بدنه
    /// پهن‌تر از جا چیده و به چپ رانده می‌شود. اگر روزی آوالونیا این را درست کند، این
    /// سنجه سرخ می‌شود و ‎ScaleBody‎ دیگر لازم نیست.
    /// </summary>
    [Theory]
    [InlineData(1.0, 1100)]
    [InlineData(1.08, 1100)]
    [InlineData(1.08, double.PositiveInfinity)]
    public void LayoutTransformControlKham_BadaneRaMiLaghzanad(double s, double measured)
    {
        var (_, root, _) = Lay(new LayoutTransformControl(), s, 5000, measured: measured);
        Assert.True(root.Bounds.Width + 28 > 1000 / s + 1,
            $"پهنای چیده‌شده {root.Bounds.Width + 28:0.#} از جای {1000 / s:0.#} بیشتر نشد");
        Assert.True(root.Bounds.X < 13, $"X={root.Bounds.X:0.#}");
    }

    /// <summary>⛔ با هر بزرگنمایی و هر پهنای محتوا، شبکهٔ بدنه همان‌جاست: از ۱۴ تا «جا ÷ بزرگنمایی − ۱۴».</summary>
    [Theory]
    [InlineData(1.0, 5000)]
    [InlineData(1.08, 5000)]
    [InlineData(1.16, 5000)]
    [InlineData(1.4, 5000)]
    [InlineData(0.92, 5000)]
    [InlineData(1.08, 990)]
    [InlineData(1.08, 300)]
    public void ScaleBody_HargezPahnTarAzJayashNemichinad(double s, double wide)
    {
        var (_, root, _) = Lay(new ScaleBody(), s, wide);
        Assert.InRange(root.Bounds.X, 13.5, 14.5);
        Assert.InRange(root.Bounds.Width, 1000 / s - 28 - 1, 1000 / s - 28 + 1);
    }

    /// <summary>
    /// ⛔ اندازه‌گیریِ پهن‌تر از چیدن (نوارِ لغزشی که پس از اندازه‌گیری آمد، یا پدری که با
    /// پهنای بی‌نهایت می‌پرسد) هم بدنه را بیرون نمی‌برد.
    /// </summary>
    [Theory]
    [InlineData(1.0, 1100)]
    [InlineData(1.08, 1100)]
    [InlineData(1.08, double.PositiveInfinity)]
    [InlineData(1.0, double.PositiveInfinity)]
    public void ScaleBody_AndazeyePahnTar_BadaneRaNemiLaghzanad(double s, double measured)
    {
        var (_, root, _) = Lay(new ScaleBody(), s, 5000, measured: measured);
        Assert.InRange(root.Bounds.X, 13.5, 14.5);
        Assert.InRange(root.Bounds.Width, 1000 / s - 28 - 1, 1000 / s - 28 + 1);
    }

    /// <summary>محتوای پهن و باریک — همان «با ردیف‌های فروشِ ورق» و «پس از پاک کردنشان» — یک جا.</summary>
    [Fact]
    public void ScaleBody_BaPahnaVaBarikiMohtava_JaNemiparad()
    {
        var a = Lay(new ScaleBody(), 1.08, 5000).Root.Bounds;
        var b = Lay(new ScaleBody(), 1.08, 400).Root.Bounds;
        Assert.InRange(Math.Abs(a.X - b.X), 0, 0.5);
        Assert.InRange(Math.Abs(a.Width - b.Width), 0, 0.5);
    }

    /// <summary>⛔ قالبِ بخش‌ها از ‎ScaleBody‎ استفاده می‌کند، نه از ‎LayoutTransformControl‎ِ خام.</summary>
    [Fact]
    public void GhalebeBakhsh_ScaleBodyDarad()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        var t = File.ReadAllText(Path.Combine(d!.FullName, "PumpYaqobi.App", "Themes", "Controls.axaml"));
        Assert.Contains("<c:ScaleBody Grid.Row=\"1\"", t);
        Assert.DoesNotContain("<LayoutTransformControl", t);
    }
}
