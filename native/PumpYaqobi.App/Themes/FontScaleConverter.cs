using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// ══ اندازهٔ نوشتهٔ بخش ← دگرگونیِ چیدمان ════════════════════════════════════
///
/// ‎A−‎ / ‎A+‎ِ هر بخش یک عدد است (‎FontScale‎) و بدنهٔ بخش با همان بزرگ و کوچک
/// می‌شود. این مبدل همان عدد را به ‎ScaleTransform‎ می‌رساند.
///
/// ⚠️ چرا ‎LayoutTransform‎ و نه نشاندنِ ‎FontSize‎ روی تک‌تکِ نوشته‌ها (کاری که
/// سایت با اسکنِ DOM می‌کند)؟ چون در برنامهٔ نیتیو اندازهٔ خانه‌های جدول از
/// **قاعدهٔ سبک** می‌آید (‎DataGridCell‎ ⇒ ۱۷) و قاعده همیشه بر ارث می‌چربد؛
/// پس ارث‌بری اصلاً به جدول‌ها نمی‌رسید و باید به هر خانه یک بایندینگ وصل
/// می‌شد — همان چیزی که سنجشِ کاراییِ جدول‌ها را خراب می‌کرد. دگرگونیِ چیدمان
/// یکی است برای کلِ بدنه: نه به منطقِ بخش دست می‌زند، نه به کارایی.
///
/// ⚠️ فقط **بدنه** بزرگ می‌شود، نه سربرگ و نوارِ ابزار — همان قاعدهٔ صریحِ
/// سایت: «‎A−/A+‎ برای نوشته‌های خودِ بخش است، نه برای نوارِ ابزارش.»
/// </summary>
public sealed class FontScaleConverter : IValueConverter
{
    public static readonly FontScaleConverter Instance = new();

    /// <summary>یکِ دست‌نخورده — تا وقتی کاربر چیزی عوض نکرده، هیچ دگرگونی‌ای در کار نیست.</summary>
    private static readonly ScaleTransform One = new(1, 1);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as double? ?? 1;
        if (double.IsNaN(s) || double.IsInfinity(s) || s <= 0) s = 1;
        return Math.Abs(s - 1) < 0.005 ? One : new ScaleTransform(s, s);
    }

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
