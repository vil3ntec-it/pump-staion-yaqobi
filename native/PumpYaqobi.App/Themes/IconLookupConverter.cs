using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PumpYaqobi.App.Themes;

/// <summary>شناسهٔ بخش ← مسیرِ برداریِ آیکونش (از <c>Icons.axaml</c>).</summary>
public sealed class IconLookupConverter : IValueConverter
{
    public static readonly IconLookupConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = "Icon." + (value as string ?? "");
        var app = Avalonia.Application.Current;
        if (app is not null && app.Resources.TryGetResource(key, null, out var g) && g is Geometry geo) return geo;
        if (app is not null && app.Resources.TryGetResource("Icon.fallback", null, out var f)) return f;
        return null;
    }

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
