using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// شناسهٔ بخش ← رنگِ آیکونش (‎IconColor.<key>‎ در <c>Icons.axaml</c>) — هر بخش
/// رنگِ خودش. بی‌پارامتر: قلم‌موی خودِ رنگ (خطِ آیکون). با پارامترِ <c>badge</c>:
/// همان رنگ با آلفای کم — زمینهٔ گردِ پشتِ آیکون؛ در تمِ تیره کمی پررنگ‌تر.
/// </summary>
public sealed class IconColorConverter : IValueConverter
{
    public static readonly IconColorConverter Instance = new();

    public static Color ColorOf(string? key)
    {
        var app = Avalonia.Application.Current;
        if (app is not null && app.Resources.TryGetResource("IconColor." + (key ?? ""), null, out var c) && c is Color col)
            return col;
        if (app is not null && app.Resources.TryGetResource("IconColor.fallback", null, out var f) && f is Color fb)
            return fb;
        return Colors.Gray;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var c = ColorOf(value as string);
        if (parameter as string == "badge")
        {
            var dark = ThemeManager.Current?.IsDark ?? false;
            return new SolidColorBrush(new Color((byte)(dark ? 60 : 38), c.R, c.G, c.B));
        }
        return new SolidColorBrush(c);
    }

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
