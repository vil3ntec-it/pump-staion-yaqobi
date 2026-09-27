using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ «در دارک مود نوشته‌ها می‌روند سمتِ چپ، انگار انگلیسی شده» (۱۴۰۵/۰۷/۱۵) ══
///
/// ریشه در خودِ آوالونیا ۱۱.۲ است — خوانده شد، نه حدس زده شد:
///
///   ۱) ‎TextBlock.CreateTextLayout‎ ⇒ ‎IsMeasureValid ? TextAlignment : TextAlignment.Left‎
///      نوشته‌ای که پیش از اندازه‌گیری کشیده شود، **چپ‌چین** کشیده می‌شود.
///   ۲) ‎CompositingRenderer.UpdateCore‎ هر دیداریِ «کثیف» را می‌کشد، **حتی اگر
///      بخشش پنهان باشد**. و نوشته‌ای که بخشش پنهان است اصلاً اندازه گرفته
///      نمی‌شود (‎LayoutManager.Measure‎ از جدِ نادیدنی برمی‌گردد).
///   ۳) تعویضِ تم رنگِ **همهٔ** نوشته‌ها را عوض می‌کند ⇒ هر کدام هم «کثیف»
///      می‌شود هم «نااندازه». پس در همان فریمِ بعد، هر نوشتهٔ بخش‌های پنهان
///      چپ‌چین کشیده می‌شود (سنجهٔ ‎layoutcycle‎: ۱۶۶ نوشته فقط در «مخزن»).
///   ۴) وقتی بخش دیده شد، نوشته اندازه و چیده می‌شود و چیدمانِ متنِ درست را
///      می‌سازد — ولی ‎ArrangeOverride‎ی ‎TextBlock‎ دوباره نمی‌کشد، و اگر
///      قابش جابه‌جا نشده باشد، کشیدهٔ چپ‌چینِ قبلی سرِ جایش می‌ماند.
///
/// چاره این‌جاست، یک بار برای کلِ برنامه: هر بار که کنترلی دیدنی شد، هر
/// نوشتهٔ زیرش که هنوز «نااندازه» است دوباره کشیده می‌شود — و چون حالا
/// دیدنی است، کشیدنش **پس از** اندازه‌گیریِ همان فریم است.
///
/// ⚠️ ارزان است: فقط شاخه‌های دیدنی گشته می‌شوند، و فقط نوشته‌ای که واقعاً
/// نااندازه است (یعنی همان که ممکن است چپ‌چین کشیده شده باشد) دوباره کشیده
/// می‌شود. نوشتهٔ سالم هیچ کاری نمی‌گیرد.
/// ⛔ هیچ شنوندهٔ ‎LayoutUpdated‎ی ساخته نشد — قاعدهٔ سرعت.
/// </summary>
public static class StaleTextGuard
{
    private static bool _installed;

    /// <summary>چند نوشته تا حالا دوباره کشیده شده — فقط برای سنجه‌ها.</summary>
    public static int Redrawn { get; private set; }

    /// <summary>فقط برای A/B در سنجهٔ ‎layoutcycle‎ (‎LC_NOGUARD=1‎) — برنامه هرگز خاموشش نمی‌کند.</summary>
    public static bool Disabled { get; set; }

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        Visual.IsVisibleProperty.Changed.AddClassHandler<Visual>(OnVisibleChanged);
    }

    private static void OnVisibleChanged(Visual v, AvaloniaPropertyChangedEventArgs e)
    {
        if (Disabled || e.NewValue is not true || v.GetVisualRoot() is null) return;
        Redraw(v);
    }

    /// <summary>
    /// نوشته‌های نااندازهٔ زیرِ <paramref name="root"/> را دوباره کثیف می‌کند؛
    /// شاخهٔ پنهان را نمی‌گردد (وقتی خودش دیدنی شد، نوبتِ خودش را دارد).
    /// </summary>
    public static void Redraw(Visual root)
    {
        if (root is TextBlock tb)
        {
            if (!tb.IsMeasureValid) { tb.InvalidateVisual(); Redrawn++; }
            return;   // نوشته فرزندِ نوشته‌ای ندارد که کثیف شده باشد
        }
        foreach (var c in root.GetVisualChildren())
            if (c.IsVisible) Redraw(c);
    }
}
