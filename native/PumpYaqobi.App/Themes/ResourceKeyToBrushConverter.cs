using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// کلیدِ منبع («Pump.Danger») ← قلمِ همان رنگ در تمِ فعال.
/// این‌طور رنگِ نشانِ کارت از خودِ تم می‌آید و با عوض شدنِ تم درست می‌ماند،
/// بی‌آنکه در XAML سه کادرِ جدا برای سه حال بسازیم.
/// </summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public static readonly ResourceKeyToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Avalonia.Application.Current;
        if (app is null || value is not string key) return null;
        return app.Resources.TryGetResource(key, app.ActualThemeVariant, out var b) ? b : null;
    }

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
