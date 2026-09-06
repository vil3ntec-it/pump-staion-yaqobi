using Avalonia;
using PumpYaqobi.App.Services;
using Dir = PumpYaqobi.App.Services.FieldNavigationService.Dir;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ناوبریِ مکانیِ کادرها ══════════════════════════════════════════════════
/// همان دو باگی که در نسخهٔ وب گزارش و درست شده بود، این‌جا به‌صورتِ آزمون
/// قفل می‌شوند تا در نسخهٔ نیتیو تکرار نشوند:
///
///   ۱) کادرِ تمام‌عرض نباید ردیف‌های میانی را ببلعد.
///   ۲) چپ/راست نباید قاطیِ ردیف‌ها شود.
/// </summary>
public class FieldNavigationTests
{
    private static Point P(double x, double y) => new(x, y);

    [Fact]
    public void Down_goes_to_the_field_directly_below()
    {
        var me = P(100, 100);
        var cands = new[] { P(100, 160), P(400, 160), P(100, 400) };
        Assert.Equal(0, FieldNavigationService.PickIndex(me, 30, Dir.Down, cands));
    }

    [Fact]
    public void Up_goes_to_the_field_directly_above()
    {
        var me = P(100, 300);
        var cands = new[] { P(100, 240), P(100, 100) };
        Assert.Equal(0, FieldNavigationService.PickIndex(me, 30, Dir.Up, cands));
    }

    /// <summary>
    /// باگِ گزارش‌شدهٔ نسخهٔ وب: در فرمِ فاکتور، کادرِ «نامِ مشتری» تمامِ عرض را
    /// می‌گیرد. الگوریتمِ قدیمی که «هم‌راستا با عرضِ کادرِ فعلی» را با هر
    /// فاصلهٔ عمودی‌ای برنده می‌کرد، از کادرِ اول مستقیم به آن می‌پرید و
    /// ردیف‌های میانی را رد می‌کرد.
    /// </summary>
    [Fact]
    public void Down_does_not_skip_rows_to_reach_a_full_width_field()
    {
        var me = P(120, 100);
        var cands = new[]
        {
            P(700, 900),   // کادرِ تمام‌عرضِ خیلی پایین‌تر، هم‌راستا نیست
            P(120, 150),   // ردیفِ بلافاصله بعدی — این باید برنده شود
            P(120, 940),
        };
        Assert.Equal(1, FieldNavigationService.PickIndex(me, 30, Dir.Down, cands));
    }

    /// <summary>
    /// باگِ دومِ گزارش‌شده: «چپ‌رفتن قاطیِ ردیف‌ها می‌شد» — کادری در ردیفِ
    /// دیگر که فاصلهٔ افقی‌اش کمتر بود، به کادرِ واقعاً هم‌ردیف می‌بُرد.
    /// </summary>
    [Fact]
    public void Left_prefers_a_true_row_mate_over_a_closer_field_in_another_row()
    {
        var me = P(600, 200);
        var cands = new[]
        {
            P(560, 400),   // افقی خیلی نزدیک، ولی ۲۰۰ پیکسل پایین‌تر — ردیفِ دیگر
            P(200, 205),   // واقعاً هم‌ردیف (۵ پیکسل اختلاف) — این باید برنده شود
        };
        Assert.Equal(1, FieldNavigationService.PickIndex(me, 30, Dir.Left, cands));
    }

    [Fact]
    public void Right_moves_to_the_nearest_row_mate_on_the_right()
    {
        var me = P(200, 200);
        var cands = new[] { P(900, 200), P(400, 205) };
        Assert.Equal(1, FieldNavigationService.PickIndex(me, 30, Dir.Right, cands));
    }

    [Fact]
    public void Nothing_in_that_direction_means_no_move()
    {
        var me = P(100, 100);
        var cands = new[] { P(100, 40) };          // فقط بالا هست
        Assert.Equal(-1, FieldNavigationService.PickIndex(me, 30, Dir.Down, cands));
        Assert.Equal(-1, FieldNavigationService.PickIndex(me, 30, Dir.Down, Array.Empty<Point>()));
    }

    /// <summary>
    /// وقتی هیچ کادرِ هم‌ردیفی نیست (مثلاً از ردیفِ بالای فرم به داخلِ شبکه)،
    /// نزدیک‌ترینِ کلی با وزنِ بیشتر روی محورِ عمودی گرفته می‌شود — همان
    /// الگوریتمِ قدیمیِ امتحان‌شده، نه «هیچ حرکتی».
    /// </summary>
    [Fact]
    public void Left_falls_back_to_nearest_overall_when_no_row_mate_exists()
    {
        var me = P(600, 100);
        var cands = new[] { P(300, 500), P(500, 300) };
        Assert.Equal(1, FieldNavigationService.PickIndex(me, 30, Dir.Left, cands));
    }

    /// <summary>کادرهایی که تقریباً روی خودِ کادرند (±۳ پیکسل) کاندید نیستند.</summary>
    [Fact]
    public void Fields_within_the_dead_zone_are_not_candidates()
    {
        var me = P(100, 100);
        Assert.Equal(-1, FieldNavigationService.PickIndex(me, 30, Dir.Down, new[] { P(100, 102) }));
        Assert.Equal(-1, FieldNavigationService.PickIndex(me, 30, Dir.Right, new[] { P(102, 100) }));
    }
}
