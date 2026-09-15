using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// رنگِ آیکونِ بخش — از خودِ تم (‎Pump.IconFg‎ / ‎Pump.IconBadge‎)، نه از کلیدِ آیکون.
/// خواستهٔ صاحب ریپو: «رنگ‌های اضافی نباید هویتِ اصلیِ تمِ آبی را تغییر دهند» —
/// لایت: آبیِ اصلی روی زمینهٔ ‎#E0F2FE‎، دارک: زردِ طلایی روی زمینهٔ طلاییِ کم‌آلفا.
/// با پارامترِ <c>badge</c> زمینهٔ گردِ پشتِ آیکون را می‌دهد.
/// </summary>
public sealed class IconColorConverter : IValueConverter
{
    public static readonly IconColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter as string == "badge" ? "Pump.IconBadge" : "Pump.IconFg";
        var app = Avalonia.Application.Current;
        if (app is not null && app.Resources.TryGetResource(key, null, out var b) && b is IBrush brush) return brush;
        return new SolidColorBrush(PumpTheme.C("#3b82f6"));
    }

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
