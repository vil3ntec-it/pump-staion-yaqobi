using Avalonia;
using Avalonia.Controls;
using PumpYaqobi.App.Controls;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شبکهٔ کارت‌ها ══════════════════════════════════════════════════════════
/// چیزی که باید ثابت شود ساده است ولی همان چیزی است که در نسخهٔ پیشین غلط
/// بود: ردیفِ کارت‌ها باید <b>تا لبه</b> پر شود — نه اینکه کنارش نوارِ خالی
/// بماند. پس هم شمارِ ستون و هم پهنای هر ستون سنجیده می‌شود.
/// </summary>
public class AutoFillPanelTests
{
    /// <summary>پنل با ‎n‎ فرزندِ ساده، اندازه‌گیری و چیده‌شده در پهنای داده‌شده.</summary>
    private static AutoFillPanel Lay(int count, double width, double minItem, double gap)
    {
        var p = new AutoFillPanel { MinItemWidth = minItem, Gap = gap };
        for (var i = 0; i < count; i++) p.Children.Add(new Border { Height = 100 });
        p.Measure(new Size(width, double.PositiveInfinity));
        p.Arrange(new Rect(0, 0, width, p.DesiredSize.Height));
        return p;
    }

    private static Rect BoundsOf(Control c) => c.Bounds;

    [Fact]
    public void Row_is_filled_edge_to_edge_with_no_leftover_strip()
    {
        // همان حالتِ واقعی: پنجرهٔ ۱۴۴۰، کارتِ کمینه ۲۵۲، فاصله ۱۲
        var p = Lay(10, 1360, 252, 12);
        var first = BoundsOf((Control)p.Children[0]);
        var cols = p.Children.Take(10).Count(ch => BoundsOf((Control)ch).Y == first.Y);

        var last = BoundsOf((Control)p.Children[cols - 1]);
        // آخرین کارتِ ردیف باید دقیقاً به لبهٔ راستِ پنل برسد
        Assert.Equal(1360, last.Right, 1);
    }

    [Fact]
    public void Columns_follow_the_minimum_item_width()
    {
        // ۱۳۶۰ با کمینهٔ ۲۵۲ و فاصلهٔ ۱۲ → ۵ ستون (۵×۲۵۲+۴×۱۲ = ۱۳۰۸ ≤ ۱۳۶۰)
        var p = Lay(12, 1360, 252, 12);
        var y0 = BoundsOf((Control)p.Children[0]).Y;
        Assert.Equal(5, p.Children.Count(ch => BoundsOf((Control)ch).Y == y0));
    }

    /// <summary>
    /// کارت‌های یک ردیف هم‌اندازه‌اند — با اغماضِ یک پیکسل. آن یک پیکسل عمدی
    /// است: باقی‌ماندهٔ تقسیمِ پهنا بینِ ستون‌ها پخش می‌شود تا ردیف دقیقاً به
    /// لبه برسد. مرورگرها هم با ‎1fr‎ همین کار را می‌کنند.
    /// </summary>
    [Fact]
    public void Cards_in_a_row_are_the_same_width_within_one_pixel()
    {
        var p = Lay(7, 1000, 220, 10);
        var y0 = BoundsOf((Control)p.Children[0]).Y;
        var widths = p.Children.Where(ch => BoundsOf((Control)ch).Y == y0)
                               .Select(ch => BoundsOf((Control)ch).Width)
                               .ToList();
        Assert.True(widths.Max() - widths.Min() <= 1.0,
                    $"اختلافِ پهنا بیش از یک پیکسل: {widths.Min()}..{widths.Max()}");
    }

    /// <summary>ردیف هرگز از لبهٔ پنل بیرون نزند — کارتِ آخر بریده نشود.</summary>
    [Fact]
    public void No_card_overflows_the_panel_width()
    {
        foreach (var w in new double[] { 1360, 1000, 777, 640, 481 })
        {
            var p = Lay(9, w, 252, 12);
            foreach (var ch in p.Children)
                Assert.True(BoundsOf((Control)ch).Right <= w + 0.001,
                            $"در پهنای {w} کارت تا {BoundsOf((Control)ch).Right} رفت");
        }
    }

    [Fact]
    public void Narrow_window_falls_back_to_a_single_column()
    {
        var p = Lay(3, 200, 252, 12);
        Assert.Equal(3, p.Children.Select(ch => BoundsOf((Control)ch).Y).Distinct().Count());
        Assert.Equal(200, BoundsOf((Control)p.Children[0]).Width, 1);
    }

    [Fact]
    public void Cards_wrap_onto_following_rows()
    {
        var p = Lay(12, 1360, 252, 12);   // ۵ در هر ردیف → ۳ ردیف
        Assert.Equal(3, p.Children.Select(ch => BoundsOf((Control)ch).Y).Distinct().Count());
    }

    [Fact]
    public void An_empty_grid_takes_no_space()
    {
        var p = new AutoFillPanel { MinItemWidth = 252, Gap = 12 };
        p.Measure(new Size(1360, double.PositiveInfinity));
        Assert.Equal(0, p.DesiredSize.Height);
    }
}
