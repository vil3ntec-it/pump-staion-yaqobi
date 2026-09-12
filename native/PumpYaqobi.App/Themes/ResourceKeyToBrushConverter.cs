using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// کلیدِ منبع («Pump.Danger») ← قلمِ همان رنگ در تمِ فعال.
/// این‌طور رنگِ نشانِ کارت از خودِ تم می‌آید و با عوض شدنِ تم درست می‌ماند،
/// بی‌آنکه در XAML سه کادرِ جدا برای سه حال بسازیم.
///
/// ⚠️ ‎ConverterParameter="band"‎ یعنی «همین رنگ، ولی روی نوارِ تیرهٔ سرِ جدول».
/// ردیفِ «جمله» روی آن نوار می‌نشیند و رنگ‌های زمینه‌روشن (سبزِ سیر، سرخِ سیر)
/// آن‌جا گم می‌شوند. پس همان کلید به ‎Pump.Band.*‎ برمی‌گردد که ThemeManager
/// برایش نسخهٔ خواندنی ساخته است — و اگر نبود، به خودِ کلیدِ اصلی برمی‌گردیم.
///
/// این‌جا هیچ منطقی از برنامه دخالت ندارد: ویومدل‌ها همان کلیدِ همیشگی را
/// می‌دهند و خبر ندارند رویشان چه زمینه‌ای است.
/// </summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public static readonly ResourceKeyToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Avalonia.Application.Current;
        if (app is null || value is not string key) return null;

        if (parameter as string == "band" && key.StartsWith("Pump.", StringComparison.Ordinal))
        {
            var banded = "Pump.Band." + key["Pump.".Length..];
            if (app.Resources.TryGetResource(banded, app.ActualThemeVariant, out var bb))
                return bb;
        }

        return app.Resources.TryGetResource(key, app.ActualThemeVariant, out var b) ? b : null;
    }

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
