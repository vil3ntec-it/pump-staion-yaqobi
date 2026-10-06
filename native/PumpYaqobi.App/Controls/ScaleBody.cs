using Avalonia;
using Avalonia.Controls;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ قابِ بزرگ‌شوندهٔ بدنهٔ هر بخش (‎A−/A+‎) — که هرگز پهن‌تر از جایش چیده نشود ══
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۲۱): «یکی‌یکی جدول‌ها را پاک کردم؛ کادرِ یک جدول را
/// که پاک کردم همهٔ سربرگ‌ها برگشتند وسط» — و در گاوصندوق: «ردیف‌های فروشِ ورق را
/// که پاک می‌کنم، همه برمی‌گردند وسط»؛ در مفاد و ضرر: «با عوض کردنِ ماه یا سال».
///
/// ⛔ ریشه در ‎LayoutTransformControl‎ِ خودِ آوالونیا بود: در ‎ArrangeOverride‎ اگر
/// ‎DesiredSize‎ِ فرزند از جا بزرگ‌تر باشد، فرزند را به <b>همان اندازهٔ بزرگ</b>
/// می‌چیند و <b>وسطِ</b> جا می‌گذارد — یعنی کلِ بدنه (کارت‌ها، جدول، سرستون‌ها، نوارِ
/// «جمله»، عنوان‌های وسط‌چین) به اندازهٔ نصفِ سرریز به یک سو می‌رود. و فرزند (شبکهٔ
/// بدنه با ‎Margin="14"‎) دقیقاً وقتی از جا بزرگ‌تر می‌شود که محتوایش پهن‌تر از قاب
/// باشد: آوالونیا ‎DesiredSize‎ را به جا می‌بُرد <b>به‌علاوهٔ حاشیهٔ خودِ کنترل</b>. پس
/// یک ردیفِ بلند (عنوانِ «📝 فروش ورق … (پمپ …)»)، عددی درازتر در ماهی دیگر، یا
/// پنجرهٔ تنگ‌تر (۱۳۶۶ با ۱۲۵٪) کلِ بخش را ۱۴ پیکسل جابه‌جا می‌کرد، و برداشتنِ
/// همان محتوا همه را برمی‌گرداند.
///
/// این قاب همان کارِ ‎LayoutTransformControl‎ را می‌کند؛ فقط پس از چیدن، اگر فرزند
/// پهن‌تر از «جا ÷ بزرگنمایی» چیده شده بود، همان را در پهنای جا و از لبهٔ آغاز
/// دوباره می‌چیند. بلندی دست نمی‌خورد (بدنه به بلندیِ محتوایش است). محتوای پهن
/// از این پس در جای خودش «…» یا شکسته یا بریده می‌شود، نه این‌که بخش را بلغزاند.
/// </summary>
public class ScaleBody : LayoutTransformControl
{
    /// <summary>
    /// چند بار بدنه‌ای که <b>بی این قاب</b> جابه‌جا می‌شد سرِ جایش برگردانده شد، و بیشترین
    /// اندازهٔ آن جابه‌جایی (پیکسل) — فقط برای سنجهٔ ‎monthshift all‎؛ هزینه‌اش یک جمع است.
    /// </summary>
    public static int Corrections { get; private set; }
    public static double LargestCorrection { get; private set; }
    public static void ResetCorrections() { Corrections = 0; LargestCorrection = 0; }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (TransformRoot is not { } root || LayoutTransform?.Value is not { } m) return size;
        //  فقط بزرگنماییِ ساده (همان ‎FontScaleConverter‎)؛ چرخش و کجی دست نمی‌خورند
        if (m.M12 != 0 || m.M21 != 0 || m.M11 <= 0 || m.M22 <= 0
            || double.IsInfinity(finalSize.Width) || double.IsNaN(finalSize.Width)) return size;

        var maxW = finalSize.Width / m.M11;
        var slotW = root.Bounds.Width + root.Margin.Left + root.Margin.Right;
        if (slotW <= maxW + 0.01) return size;

        Corrections++;
        LargestCorrection = Math.Max(LargestCorrection, (slotW - maxW) * m.M11 / 2);
        var slotH = root.Bounds.Height + root.Margin.Top + root.Margin.Bottom;
        root.Arrange(new Rect(0, (finalSize.Height - slotH * m.M22) / 2, maxW, slotH));
        return size;
    }
}
