using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// تمِ فعال را داخلِ منابعِ برنامه می‌نشاند. همهٔ XAMLها با
/// <c>{DynamicResource Pump.*}</c> از همین‌جا رنگ می‌گیرند، پس عوض کردنِ تم
/// هیچ صفحه‌ای را از نو نمی‌سازد — فقط رنگ‌ها جابه‌جا می‌شوند.
/// </summary>
public static class ThemeManager
{
    public static PumpTheme Current { get; private set; } = PumpTheme.Marble;

    public static event Action<PumpTheme>? Changed;

    private static LinearGradientBrush Horizontal(params Color[] cs) => Gradient(cs, true);
    private static LinearGradientBrush Vertical(params Color[] cs) => Gradient(cs, false);

    private static LinearGradientBrush Gradient(Color[] cs, bool horizontal)
    {
        var b = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(horizontal ? 1 : 0.5, horizontal ? 0.5 : 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(horizontal ? 0 : 0.5, horizontal ? 0.5 : 1, RelativeUnit.Relative),
        };
        for (var i = 0; i < cs.Length; i++)
            b.GradientStops.Add(new GradientStop(cs[i], cs.Length == 1 ? 0 : i / (double)(cs.Length - 1)));
        return b;
    }

    private static Color Mix(Color a, Color b, double t) => new(
        (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    public static Color WithAlpha(Color c, double a) => new((byte)(255 * a), c.R, c.G, c.B);

    public static void Apply(PumpTheme t, Avalonia.Application? app = null)
    {
        app ??= Avalonia.Application.Current;
        if (app is null) return;
        Current = t;
        var r = app.Resources;

        void Set(string key, object v) => r[key] = v;
        void Br(string key, Color c) { r["Pump." + key + "Color"] = c; r["Pump." + key] = new SolidColorBrush(c); }

        Br("Dark", t.Dark);
        Br("Panel", t.Panel);
        Br("Card", t.Card);
        Br("Border", t.Border);
        Br("Text", t.Text);
        Br("Muted", t.Muted);
        Br("Label", t.Label);
        Br("Accent", t.Accent);
        Br("OnAccent", t.OnAccent);
        Br("HeaderTitle", t.HeaderTitle);
        Br("ChromeBorder", t.ChromeBorder);

        // ══ نوارِ سرِ جدول و ردیفِ «جمله» ══════════════════════════════
        // خواستهٔ صریحِ صاحب ریپو: این دو نوار هم‌رنگِ بدنهٔ جدول نباشند تا
        // جدول «قاب» پیدا کند — ولی «کمی صاف‌تر … خیلی تیره است»، پس رنگش
        // در خودِ تم نشسته و این‌جا فقط ثبت می‌شود، نه محاسبه.
        Br("HeadBand", t.HeadBand);
        Br("OnHeadBand", t.OnHeadBand);
        // نوشتهٔ کم‌رنگِ روی همان نوار (برچسبِ بالای هر عددِ «جمله»)
        Br("OnHeadBandMuted", Mix(t.HeadBand, t.OnHeadBand, 0.62));

        // سطح‌های مشتق‌شده — همان چیزی که در CSS با color-mix ساخته می‌شد
        Br("Hover", Mix(t.Card, t.Accent, t.IsDark ? 0.14 : 0.10));
        Br("Selected", Mix(t.Card, t.Accent, t.IsDark ? 0.26 : 0.18));
        Br("GridLine", Mix(t.Card, t.Border, 0.75));
        Br("RowAlt", Mix(t.Card, t.IsDark ? Colors.White : Colors.Black, 0.035));
        Br("Shadow", WithAlpha(Colors.Black, t.IsDark ? 0.55 : 0.16));
        var ok = t.IsDark ? PumpTheme.C("#4ade80") : PumpTheme.C("#15803d");
        var warn = t.IsDark ? PumpTheme.C("#fbbf24") : PumpTheme.C("#b45309");
        var danger = t.IsDark ? PumpTheme.C("#f87171") : PumpTheme.C("#b91c1c");
        var info = t.IsDark ? PumpTheme.C("#60a5fa") : PumpTheme.C("#1d4ed8");
        Br("Ok", ok);
        Br("Warn", warn);
        Br("Danger", danger);
        Br("Info", info);

        // ⚠️ این دو با تم عوض نمی‌شوند چون در سایت هم نمی‌شوند: ‎--purple‎ در
        // ‎:root‎ی هر دو تم ‎#805ad5‎ است (‎index.html‎ خط ۹۷ و ۱۰۴) و رنگِ
        // «جمله دیزل» همان‌جا مستقیم ‎#b7791f‎ نوشته شده (خط ۱۹۹۷۷).
        Br("Purple", PumpTheme.C("#805ad5"));
        Br("Diesel", PumpTheme.C("#b7791f"));

        // ══ همان رنگ‌ها، ولی خواندنی روی نوارِ سرِ جدول ═════════════════════
        // ردیفِ «جمله» روی نوارِ تیره می‌نشیند و عددهایش کلیدِ رنگ را از خودِ
        // ویومدل می‌گیرند («Pump.Ok»، «Pump.Danger» و …). آن رنگ‌ها برای زمینهٔ
        // روشن ساخته شده‌اند و روی نوار گم می‌شوند.
        //
        // ⚠️ نه کلیدِ ویومدل عوض شد و نه منطقی: همان کلید با
        // ‎ConverterParameter=band‎ به این نسخه‌ها می‌رسد (ResourceKeyToBrushConverter).
        // در تمِ تیره نوار خودش روشن‌تر از جدول است و رنگ‌ها همان می‌مانند.
        void Band(string key, Color c) =>
            Br("Band." + key, t.IsDark ? c : Mix(c, t.OnHeadBand, 0.45));

        Band("Ok", ok);
        Band("Warn", warn);
        Band("Danger", danger);
        Band("Info", info);
        Band("Accent", t.Accent);
        Band("Purple", PumpTheme.C("#805ad5"));
        Band("Diesel", PumpTheme.C("#b7791f"));
        Br("Band.Text", t.OnHeadBand);
        Br("Band.Label", t.OnHeadBand);
        Br("Band.Muted", Mix(t.HeadBand, t.OnHeadBand, 0.62));

        Set("Pump.HeaderBg", Horizontal(t.HeaderBg));
        Set("Pump.BannerBg", Horizontal(t.BannerBg));
        Set("Pump.NavBg", Horizontal(t.NavBg));
        Set("Pump.AppBg", Vertical(t.AppBg));
        Set("Pump.AccentGrad", Gradient(t.AccentGrad, true));

        app.RequestedThemeVariant = t.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        Changed?.Invoke(t);
    }
}
