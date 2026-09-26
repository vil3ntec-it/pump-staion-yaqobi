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

    // ⚠️ قلمِ «زنده» برای هر (کلید، پارامتر) — ۱۴۰۵/۰۷/۱۴.
    // تبدیل یک بار رنگ می‌گیرد و اتصال با عوض شدنِ تم دوباره نمی‌پرسد؛ پس
    // پیش از این رنگِ تمِ قبلی روی صفحه می‌ماند (نوشتهٔ آبیِ تیره روی بومِ
    // مشکی). حالا همان یک قلم برگردانده می‌شود و ‎Refresh‎ (از ‎ThemeManager.Apply‎)
    // رنگش را از تمِ تازه می‌نشاند — هیچ اتصالی از نو ساخته نمی‌شود.
    private static readonly Dictionary<string, SolidColorBrush> Live = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (Avalonia.Application.Current is null || value is not string key) return null;
        var mode = parameter as string ?? "";
        var id = mode + "|" + key;
        if (Live.TryGetValue(id, out var live)) return live;
        var found = Resolve(key, mode);
        if (found is not ISolidColorBrush solid) return found;
        var b = new SolidColorBrush(solid.Color, solid.Opacity);
        Live[id] = b;
        return b;
    }

    /// <summary>پس از هر عوض شدنِ تم: هر قلمِ زنده رنگِ تمِ تازه را می‌گیرد.</summary>
    public static void Refresh()
    {
        foreach (var (id, b) in Live)
        {
            var bar = id.IndexOf('|');
            if (Resolve(id[(bar + 1)..], id[..bar]) is ISolidColorBrush s)
            { b.Color = s.Color; b.Opacity = s.Opacity; }
        }
    }

    private static object? Resolve(string key, string mode)
    {
        var app = Avalonia.Application.Current;
        if (app is null) return null;
        if (key.StartsWith("Pump.", StringComparison.Ordinal) && mode is "band" or "ink")
        {
            // ‎ink‎: نوشتهٔ رنگی — در تمِ تیره سفید (‎Pump.Ink.*‎)، در روشن همان رنگ.
            //  ⚠️ پیشوند جدا نوشته می‌شود: ‎ThemeKeyTests‎ هر «کلیدِ» تمام‌رشته را می‌گردد
            var alt = "Pump." + (mode == "band" ? "Band." : "Ink.") + key["Pump.".Length..];
            if (app.Resources.TryGetResource(alt, app.ActualThemeVariant, out var ab)) return ab;
        }
        return app.Resources.TryGetResource(key, app.ActualThemeVariant, out var b) ? b : null;
    }

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
