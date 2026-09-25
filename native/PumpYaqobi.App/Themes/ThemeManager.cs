using System.Collections.Generic;
using System.Linq;
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
    public static PumpTheme Current { get; private set; } = PumpTheme.Blue;

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

    private static BoxShadows ParseShadow(string s) =>
        string.IsNullOrWhiteSpace(s) ? new BoxShadows() : BoxShadows.Parse(s);

    private static Color Mix(Color a, Color b, double t) => new(
        (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    public static Color WithAlpha(Color c, double a) => new((byte)(255 * a), c.R, c.G, c.B);

    /// <summary>
    /// ══ چرا یک فرهنگِ جدا، و نه نوشتن روی ‎app.Resources‎ ═══════════════════
    ///
    /// گزارشِ صاحب ریپو: «تعویضِ تمِ دارک و لایت هم همین‌طور کند است.»
    ///
    /// ریشه‌اش این بود: ‎Apply‎ حدودِ شصت کلید را **یکی‌یکی** در
    /// ‎app.Resources‎ می‌نوشت، و هر نوشتن یک خبرِ «منابع عوض شد» می‌داد که
    /// کلِ درختِ بصری را بی‌اعتبار می‌کند. یعنی با یک جدولِ باز روی صفحه،
    /// شصت بار کلِ صفحه از نو سنجیده می‌شد — نه یک بار.
    ///
    /// حالا همهٔ کلیدها اول در یک فرهنگِ **تازه** ساخته می‌شوند و آن فرهنگ
    /// یک‌جا جای قبلی می‌نشیند: یک خبر به‌جای شصت‌تا.
    ///
    /// ⚠️ این فرهنگ باید همیشه همان یک جایگاه در ‎MergedDictionaries‎ را
    /// داشته باشد؛ اگر هر بار یکی اضافه شود، فهرست بی‌انتها بزرگ می‌شود و
    /// جست‌وجوی هر منبع کندتر.
    /// </summary>
    private static int _slot = -1;

    /// <summary>فرهنگِ تمِ فعلی — تا ‎TableStyle‎ خط‌ها را همان‌جا بنویسد، نه روی سرِ آن.</summary>
    public static Avalonia.Controls.ResourceDictionary? Dictionary { get; private set; }

    /// <summary>
    /// ══ هر دو تم از اول داخلِ یک فرهنگ‌اند؛ تعویض فقط «گونه» را عوض می‌کند ═══
    ///
    /// با پنج سال داده و بیست صفحهٔ گرم‌شده در درخت، هر خبرِ «منابع عوض شد»
    /// ۴۰۰ میلی‌ثانیه است. تا دیروز تعویضِ تم دو خبر بود: یکی برای گونهٔ
    /// روشن/تیره و یکی برای نشستنِ فرهنگِ تازه. حالا فرهنگِ آبی و طلایی هر دو
    /// از همان اول ساخته و زیرِ ‎ThemeDictionaries‎ (‎Light‎/‎Dark‎) یک‌بار ادغام
    /// می‌شوند؛ تعویضِ تم تنها ‎RequestedThemeVariant‎ را عوض می‌کند — همان یک
    /// خبری که به‌هرحال لازم بود.
    ///
    /// ⚠️ به همین خاطر «آبی = روشن» و «طلایی = تیره» یک قرارداد است، نه
    /// تصادف: ‎PumpTheme.IsDark‎ می‌گوید هر تم زیرِ کدام گونه می‌نشیند و دو تم
    /// با یک گونه نمی‌توانند هم‌زمان نصب باشند (تمِ سوم اضافه نکنید).
    /// </summary>
    private static readonly Dictionary<ThemeVariant, (PumpTheme Theme, Avalonia.Controls.ResourceDictionary Dict)> _installed = new();

    /// <summary>فرهنگِ هر گونهٔ نصب‌شده با تمش — تا ‎TableStyle.Apply‎ روی هر دو بنویسد.</summary>
    public static IEnumerable<(PumpTheme Theme, Avalonia.Controls.ResourceDictionary Dict)> Installed => _installed.Values;

    private static ThemeVariant VariantOf(PumpTheme t) => t.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

    public static void Apply(PumpTheme t, Avalonia.Application? app = null)
    {
        app ??= Avalonia.Application.Current;
        if (app is null) return;
        Current = t;

        var variant = VariantOf(t);
        var fresh = !_installed.TryGetValue(variant, out var have) || !ReferenceEquals(have.Theme, t);
        if (fresh)
        {
            // تمِ این گونه هنوز ساخته نشده (یا تمِ دیگری با همین گونه خواسته
            // شده — آزمون‌ها): همهٔ گونه‌ها یک‌جا از نو ساخته و یک‌بار ادغام
            // می‌شوند. در کارِ عادی این فقط در راه‌اندازی رخ می‌دهد.
            _installed[variant] = (t, Build(t));
            foreach (var other in PumpTheme.All)
                if (!_installed.ContainsKey(VariantOf(other)))
                    _installed[VariantOf(other)] = (other, Build(other));

            var container = new Avalonia.Controls.ResourceDictionary();
            foreach (var (v, pair) in _installed) container.ThemeDictionaries[v] = pair.Dict;

            // ⚠️ اول گونه، بعد فرهنگ: هر کدام یک خبر است و وقتی گونه پیش از
            // فرهنگ عوض شود، خبرِ دوم روی فرهنگِ درست می‌نشیند و سومی لازم نیست.
            app.RequestedThemeVariant = variant;
            var merged = app.Resources.MergedDictionaries;
            if (_slot >= 0 && _slot < merged.Count) merged[_slot] = container;
            else { merged.Add(container); _slot = merged.Count - 1; }
        }
        else
        {
            // راهِ همیشگی: فقط گونه — یک خبر، نه دو تا
            app.RequestedThemeVariant = variant;
        }

        Dictionary = _installed[variant].Dict;
        Changed?.Invoke(t);
    }

    /// <summary>همهٔ کلیدهای یک تم در یک فرهنگِ تازه — بی هیچ خبری به درخت.</summary>
    private static Avalonia.Controls.ResourceDictionary Build(PumpTheme t)
    {
        var r = new Avalonia.Controls.ResourceDictionary();

        void Set(string key, object v) => r[key] = v;
        void Br(string key, Color c) { r["Pump." + key + "Color"] = c; r["Pump." + key] = new SolidColorBrush(c); }

        Br("Dark", t.Dark);
        Br("Panel", t.Panel);
        Br("Card", t.Card);
        // ══ سه طبقه ═══════════════════════════════════════════════════════
        // بدنهٔ کادرِ بخش بینِ بوم و کارتِ داخلی می‌نشیند، و کادرِ تایپ یک پله
        // از پنل تیره‌تر است — تا «جای خالیِ بغلِ کادر» با خودِ کادر و کادرِ
        // تایپ با کارتش یک رنگ نباشند (شرحش بالای ‎PumpTheme.SectionBg‎).
        Br("Section", t.SectionBg);
        Br("Input", t.Input ?? Mix(t.Panel, t.Dark, t.IsDark ? 0.45 : 0.5));

        // ══ نورِ لبه و سایهٔ کارت (دو تمِ آبی) ═══════════════════════════
        // چهار لایه در یک BoxShadows: هایلایتِ داخلی، خطِ آبی، هالهٔ ۶۰A5FA،
        // سایهٔ بیرونی. تمی که رشته‌اش خالی باشد هیچ سایه‌ای نمی‌گیرد.
        Set("Pump.CardShadow", ParseShadow(t.CardShadow));
        Set("Pump.CardShadowHover", ParseShadow(string.IsNullOrEmpty(t.CardShadowHover) ? t.CardShadow : t.CardShadowHover));
        // کادرهای صفحه‌قد سایهٔ بی‌محو می‌گیرند — چرایی‌اش بالای ‎PumpTheme.SectionShadowOrCard‎.
        Set("Pump.SectionShadow", ParseShadow(t.SectionShadowOrCard));

        // تبِ فعالِ نوار: لایت = زمینهٔ آبیِ بسیار روشن با نوشتهٔ آبیِ تیره،
        // دارک = گرادیانِ تاکید با نوشتهٔ سفید.
        Set("Pump.NavActiveBg", Horizontal(t.NavActiveBg ?? t.AccentGrad));
        Br("NavActiveFg", t.NavActiveFg ?? t.OnAccent);

        // نمودارها و آیکون‌ها: لایت آبی (#3B82F6 / #60A5FA)، دارک طلایی (#FFD700 / #FFF3C4)
        Br("ChartLine", t.IsDark ? PumpTheme.C("#ffd700") : PumpTheme.C("#3b82f6"));
        Br("ChartPoint", t.IsDark ? PumpTheme.C("#fff3c4") : PumpTheme.C("#60a5fa"));
        Br("ChartBarSel", t.IsDark ? PumpTheme.C("#fff3c4") : PumpTheme.C("#1e3a8a"));
        Br("IconFg", t.IsDark ? PumpTheme.C("#ffd700") : PumpTheme.C("#3b82f6"));
        Br("IconBadge", t.IsDark ? Color.FromArgb(0x2e, 0xff, 0xd7, 0x00) : PumpTheme.C("#e0f2fe"));
        Br("Border", t.Border);
        Br("Text", t.Text);
        Br("Muted", t.Muted);
        // ══ تکملهٔ خودکار («Suggest») — عمداً کم‌رنگ‌تر از ‎Muted‎ ══════════════
        // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «پیشنهادی‌ها خیلی تیره‌اند، هم‌رنگِ
        // نوشته‌های من؛ تشخیص داده نمی‌شه تکمیل است یا پیشنهاد.» تکمله نوشتهٔ
        // کاربر نیست — شبیهِ نوشتهٔ راهنمای کادر است، پس نیمه‌راهِ کارت و
        // ‎Muted‎: خوانا، ولی آشکارا «هنوز نوشته نشده».
        Br("Ghost", Mix(t.Card, t.Muted, 0.55));
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
        // پلهٔ چهارمِ نوارِ کارت‌ها (۸۵٪ تا ۹۰٪) — بینِ زرد و سرخ
        var orange = t.IsDark ? PumpTheme.C("#fb923c") : PumpTheme.C("#c2410c");
        Br("Ok", ok);
        Br("Warn", warn);
        Br("Danger", danger);
        Br("Info", info);
        Br("Orange", orange);
        //  نقطهٔ سرخِ «ماهِ تازه» روی کشوی ماه (‎MonthDot‎) — سرخِ روشن‌ترِ یک
        //  نشانِ اعلان، نه سرخِ تیرهٔ «ضرر» که روی کشویی کوچک گم می‌شد.
        Br("Dot", t.IsDark ? PumpTheme.C("#f87171") : PumpTheme.C("#ef4444"));

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
            // ⚠️ نوارِ سرِ جدول دیگر تیره نیست (رنگِ کارت با ۲۰٪ تاکید، مثلِ
            // سایت)، پس رنگِ «حال» لازم نیست روشن شود تا خوانده گردد — همان
            // رنگِ همیشگیِ خودش روی نوار درست دیده می‌شود. روشن کردنش روی
            // نوارِ روشن، برعکس، کم‌رنگ و ناخوانا می‌کرد.
            Br("Band." + key, c);

        Band("Ok", ok);
        Band("Warn", warn);
        Band("Danger", danger);
        Band("Info", info);
        Band("Orange", orange);
        Band("Accent", t.Accent);
        Band("Purple", PumpTheme.C("#805ad5"));
        Band("Diesel", PumpTheme.C("#b7791f"));
        Br("Band.Text", t.Text);
        Br("Band.Label", t.OnHeadBand);
        Br("Band.Muted", t.Muted);

        Set("Pump.HeaderBg", Horizontal(t.HeaderBg));
        Set("Pump.BannerBg", Horizontal(t.BannerBg));
        Set("Pump.NavBg", Horizontal(t.NavBg));
        Set("Pump.AppBg", Vertical(t.AppBg));
        Set("Pump.AccentGrad", Gradient(t.AccentGrad, true));

        // خط‌های جدول هم در همین فرهنگ — نه یک پاسِ جدا برای هر کدام
        TableStyle.Fill(r, t);
        return r;
    }
}
