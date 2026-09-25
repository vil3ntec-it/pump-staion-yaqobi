using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// نقشهٔ مخزنِ زیرِ زمین — همان تصویری که صاحب ریپو داد (۱۴۰۵/۰۷/۱۳):
/// مخزنِ افقی با تیلِ نارنجی، پروب، شناورِ تیل، پمپِ غوطه‌ور، و درصدِ پرشدگی
/// وسطِ مخزن — و آب و شناورِ آب **فقط وقتی دستگاه اندازه‌اش را داده باشد**.
///
/// ⛔ جای نوارِ عمودیِ پیشین («۱۰۰٪» روی یک ستونِ آبی) را گرفته و آن نوار
/// برنمی‌گردد: «اون نشانه‌گرِ مخزن که الان تو برنامه هست رو بردار و این مدلِ
/// جدید رو براش بزار… این همه ریزکارهاش باید باشن.»
///
/// ⚠️ **فقط عددِ واقعی کشیده می‌شود**: سطحِ تیل (‎FillPercent‎ = موجودی ÷
/// ظرفیت) و خطِ آستانهٔ هشدار. پروب، پمپ و میلهٔ شناور شکلِ دستگاه‌اند.
///
/// ⛔ **آب تا دستگاهِ واقعی اندازه نداده دیده نمی‌شود** (خواستهٔ صاحب ریپو،
/// ۱۴۰۵/۰۷/۱۳: «من نمی‌دونم اصلاً توی مخزن آبی هست یا نه»). ‎WaterPercent‎
/// پیش‌فرضش ‎null‎ است یعنی «اندازه‌ای نداریم» — نه «صفر آب»: آن وقت نه نوارِ
/// آبی، نه گویِ شناورِ آب و نه برچسبش کشیده می‌شود و تیل از کفِ مخزن
/// شمرده می‌شود. روزی که دستگاهِ فیزیکی عدد بدهد، همین خاصیت به آن بسته
/// می‌شود و آب خودش پیدا می‌شود. ⛔ عددِ حدسی یا نمایشی به آن ندهید.
///
/// ⚠️ همهٔ اندازه‌ها در یک بومِ طراحیِ ثابت (‎DesignW×DesignH‎) نوشته شده‌اند و
/// کلِ نقشه با یک مقیاس در قابِ واقعی می‌نشیند — پس با هر پهنا و بلندی‌ای
/// همان شکل می‌ماند و هیچ تکه‌ای جای دیگری نمی‌رود.
///
/// ⚠️ ‎FlowDirection‎ همیشه چپ‌به‌راست است (سازنده): کلِ پنجره راست‌به‌چپ است و
/// آوالونیا کشیدنِ خودِ کنترل را آینه می‌کند — پروب می‌رفت سمتِ راست و
/// نوشته‌ها وارونه. همان قاعدهٔ ‎SparkChart‎.
/// </summary>
public class TankGauge : Control
{
    public const double DesignW = 400;
    public const double DesignH = 250;

    public static readonly StyledProperty<double> FillPercentProperty =
        AvaloniaProperty.Register<TankGauge, double>(nameof(FillPercent));

    /// <summary>نوشتهٔ وسطِ مخزن — همان ‎FillText‎ی ویومدل («58.9%»).</summary>
    public static readonly StyledProperty<string?> FillTextProperty =
        AvaloniaProperty.Register<TankGauge, string?>(nameof(FillText));

    /// <summary>آستانهٔ هشدار به درصدِ ظرفیت — خطِ سرخِ نقطه‌چین داخلِ مخزن.</summary>
    public static readonly StyledProperty<double> ThresholdPercentProperty =
        AvaloniaProperty.Register<TankGauge, double>(nameof(ThresholdPercent));

    /// <summary>زیرنویسِ پایینِ مخزن — «حد هشدار: ۱٬۰۰۰ لیتر».</summary>
    public static readonly StyledProperty<string?> CaptionTextProperty =
        AvaloniaProperty.Register<TankGauge, string?>(nameof(CaptionText));

    /// <summary>
    /// بلندیِ آب به درصدِ بلندیِ داخلِ مخزن، **فقط از دستگاهِ اندازه‌گیر**.
    /// ‎null‎ (پیش‌فرض) = اندازه‌ای نیست ⇒ هیچ نشانی از آب کشیده نمی‌شود.
    /// سقفش ۴۰ است تا هیچ‌وقت جای تیل را نگیرد.
    /// </summary>
    public static readonly StyledProperty<double?> WaterPercentProperty =
        AvaloniaProperty.Register<TankGauge, double?>(nameof(WaterPercent));

    public static readonly StyledProperty<IBrush?> BodyBrushProperty =
        AvaloniaProperty.Register<TankGauge, IBrush?>(nameof(BodyBrush));
    public static readonly StyledProperty<IBrush?> BodyDeepBrushProperty =
        AvaloniaProperty.Register<TankGauge, IBrush?>(nameof(BodyDeepBrush));
    public static readonly StyledProperty<IBrush?> RimBrushProperty =
        AvaloniaProperty.Register<TankGauge, IBrush?>(nameof(RimBrush));
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<TankGauge, IBrush?>(nameof(LabelBrush));
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<TankGauge, IBrush?>(nameof(TextBrush));
    public static readonly StyledProperty<IBrush?> ThresholdBrushProperty =
        AvaloniaProperty.Register<TankGauge, IBrush?>(nameof(ThresholdBrush));

    static TankGauge() => AffectsRender<TankGauge>(FillPercentProperty, FillTextProperty,
        ThresholdPercentProperty, CaptionTextProperty, WaterPercentProperty,
        BodyBrushProperty, BodyDeepBrushProperty, RimBrushProperty, LabelBrushProperty,
        TextBrushProperty, ThresholdBrushProperty);

    public TankGauge() => FlowDirection = FlowDirection.LeftToRight;

    public double FillPercent { get => GetValue(FillPercentProperty); set => SetValue(FillPercentProperty, value); }
    public string? FillText { get => GetValue(FillTextProperty); set => SetValue(FillTextProperty, value); }
    public double ThresholdPercent { get => GetValue(ThresholdPercentProperty); set => SetValue(ThresholdPercentProperty, value); }
    public string? CaptionText { get => GetValue(CaptionTextProperty); set => SetValue(CaptionTextProperty, value); }
    public double? WaterPercent { get => GetValue(WaterPercentProperty); set => SetValue(WaterPercentProperty, value); }

    /// <summary>دستگاه اندازهٔ آب را داده است؟ — تنها شرطِ کشیدنِ هر تکهٔ آب.</summary>
    public bool HasWater => HasWaterReading(WaterPercent);

    /// <summary>عددِ آب واقعی است؟ ‎null‎ یا ناجور ⇒ نه.</summary>
    public static bool HasWaterReading(double? water) => water is double w && double.IsFinite(w);
    public IBrush? BodyBrush { get => GetValue(BodyBrushProperty); set => SetValue(BodyBrushProperty, value); }
    public IBrush? BodyDeepBrush { get => GetValue(BodyDeepBrushProperty); set => SetValue(BodyDeepBrushProperty, value); }
    public IBrush? RimBrush { get => GetValue(RimBrushProperty); set => SetValue(RimBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush? ThresholdBrush { get => GetValue(ThresholdBrushProperty); set => SetValue(ThresholdBrushProperty, value); }

    // ── نام‌های چهار تکه — همان چهار برچسبِ تصویرِ مرجع، به فارسی ─────────
    public const string ProbeLabel = "پروب";
    public const string OilFloatLabel = "شناورِ تیل";
    public const string PumpLabel = "پمپِ غوطه‌ور";
    public const string WaterFloatLabel = "شناورِ آب";
    public const string FillCaption = "میزان پرشدگی";

    // ── بومِ طراحی (۴۰۰×۲۵۰) ────────────────────────────────────────────────
    //  بدنهٔ مخزن، و فضای داخلی که تیل و آب در آن می‌نشینند (سه واحد تو رفته).
    private static readonly Rect Body = new(26, 48, 348, 166);
    private const double BodyRadius = 44;
    private static readonly Rect Inner = Body.Deflate(3);

    private const double ProbeX = 60, FloatX = 146, PumpX = 322, HatchX = 104, VentX = 340;

    /// <summary>
    /// سطح‌ها روی بومِ طراحی — **تنها** جای این حساب، و خالص تا آزمون‌پذیر باشد.
    ///
    /// محورِ «ظرفیت» از سطحِ آب تا سقفِ داخلی است: ۱۰۰٪ یعنی تیل تا سقف، ۰٪
    /// یعنی هیچ تیلی روی آب نیست. آستانه هم روی همان محور می‌نشیند، پس خطِ
    /// هشدار هیچ‌وقت داخلِ نوارِ آب نمی‌افتد.
    /// </summary>
    public readonly record struct Levels(double WaterTop, double OilTop, double ThresholdY);

    public static Levels LevelsOf(double innerTop, double innerBottom,
                                  double fillPercent, double? waterPercent, double thresholdPercent)
    {
        var fill = Math.Clamp(double.IsFinite(fillPercent) ? fillPercent : 0, 0, 100) / 100.0;
        var water = Math.Clamp(HasWaterReading(waterPercent) ? waterPercent!.Value : 0, 0, 40) / 100.0;
        var thr = Math.Clamp(double.IsFinite(thresholdPercent) ? thresholdPercent : 0, 0, 100) / 100.0;
        var h = innerBottom - innerTop;
        var waterTop = innerBottom - water * h;
        var span = waterTop - innerTop;
        return new Levels(waterTop, waterTop - fill * span, waterTop - thr * span);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 40 || h < 25) return;

        //  کلِ نقشه با یک مقیاس، وسطِ قاب
        var s = Math.Min(w / DesignW, h / DesignH);
        var ox = (w - DesignW * s) / 2;
        var oy = (h - DesignH * s) / 2;
        using var _ = ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(ox, oy));

        var family = TextElement.GetFontFamily(this);
        var bold = new Typeface(family, FontStyle.Normal, FontWeight.Bold);
        var semi = new Typeface(family, FontStyle.Normal, FontWeight.SemiBold);
        var label = LabelBrush ?? Brushes.DimGray;
        var text = TextBrush ?? Brushes.Black;

        var lv = LevelsOf(Inner.Top, Inner.Bottom, FillPercent, WaterPercent, ThresholdPercent);

        DrawLegs(ctx);
        DrawBody(ctx);
        DrawFluids(ctx, lv);
        DrawFittings(ctx);
        DrawInternals(ctx, lv);
        DrawThreshold(ctx, lv);

        //  ── نوشتهٔ وسط: درصد و «میزان پرشدگی» ──
        //  روی تیل سفید (با سایهٔ نرم)، روی بدنهٔ خالی رنگِ نوشتهٔ تم — وگرنه
        //  با مخزنِ کم، سفید روی سفید می‌شد.
        var cx = Inner.Center.X;
        const double pctTop = 100, capTop = 138;
        var fillText = string.IsNullOrEmpty(FillText) ? Math.Round(Math.Clamp(FillPercent, 0, 100)).ToString(CultureInfo.InvariantCulture) + "%" : FillText!;
        Centered(ctx, fillText, bold, 27, lv.OilTop <= pctTop ? Brushes.White : text, cx, pctTop, 220,
                 shadow: lv.OilTop <= pctTop, FlowDirection.LeftToRight);
        Centered(ctx, FillCaption, semi, 12.5, lv.OilTop <= capTop ? Brushes.White : text, cx, capTop, 220,
                 shadow: lv.OilTop <= capTop);

        //  ── برچسب‌های چهار تکه، و زیرنویسِ آستانه ──
        Centered(ctx, ProbeLabel, semi, 11, label, ProbeX, 7, 90);
        Centered(ctx, OilFloatLabel, semi, 11, label, FloatX, 7, 110);
        Centered(ctx, PumpLabel, semi, 11, label, PumpX, 5, 120);
        if (HasWater)
            Centered(ctx, WaterFloatLabel, semi, 11, label, FloatX, 233, 110);
        if (!string.IsNullOrEmpty(CaptionText))
        {
            var ft = new FormattedText(CaptionText!, CultureInfo.CurrentCulture, FlowDirection.RightToLeft,
                                       semi, 10.5, label) { TextAlignment = TextAlignment.Right, MaxTextWidth = 170 };
            ctx.DrawText(ft, new Point(Body.Right - 170, 233));
        }
    }

    // ── بدنه ─────────────────────────────────────────────────────────────────

    private void DrawBody(DrawingContext ctx)
    {
        var rr = new RoundedRect(Body, BodyRadius);
        var top = ColorOf(BodyBrush, Colors.White);
        var deep = ColorOf(BodyDeepBrush, Color.Parse("#e4ecf8"));
        ctx.DrawRectangle(Vertical((top, 0), (deep, 1)), null, rr);
        //  جلوهٔ فولادِ استوانه: روشن در بالا، کمی تیره در پایین
        ctx.DrawRectangle(Vertical((Color.FromArgb(0x8c, 255, 255, 255), 0),
                                   (Color.FromArgb(0x00, 255, 255, 255), 0.42),
                                   (Color.FromArgb(0x22, 0, 0, 0), 1)), null, rr);
    }

    private void DrawRim(DrawingContext ctx)
    {
        var rim = RimBrush ?? new SolidColorBrush(Color.Parse("#9fb0c3"));
        ctx.DrawRectangle(null, new Pen(rim, 2.5), new RoundedRect(Body, BodyRadius));
    }

    private static void DrawLegs(DrawingContext ctx)
    {
        var fill = new SolidColorBrush(Color.Parse("#8896a6"));
        var pen = new Pen(new SolidColorBrush(Color.Parse("#5f6b7a")), 1);
        foreach (var x in new[] { 68.0, 296.0 })
            ctx.DrawGeometry(fill, pen, Polygon(new Point(x, Body.Bottom - 2), new Point(x + 36, Body.Bottom - 2),
                                                new Point(x + 42, 232), new Point(x - 6, 232)));
    }

    /// <summary>دریچهٔ بازدید (چپ)، لولهٔ هوا (راست)، و سرِ بیرونیِ پروب و پمپ و میلهٔ شناور.</summary>
    private static void DrawFittings(DrawingContext ctx)
    {
        var steel = new SolidColorBrush(Color.Parse("#a7b3c1"));
        var lid = new SolidColorBrush(Color.Parse("#cfd7e0"));
        var dark = new SolidColorBrush(Color.Parse("#6b7785"));
        var edge = new Pen(new SolidColorBrush(Color.Parse("#5f6b7a")), 1);

        //  دریچه
        ctx.DrawRectangle(steel, edge, new Rect(HatchX - 14, 36, 28, 14));
        ctx.DrawRectangle(lid, edge, new RoundedRect(new Rect(HatchX - 20, 30, 40, 10), 3));
        //  لولهٔ هوا
        ctx.DrawRectangle(steel, edge, new Rect(VentX - 5, 36, 10, 14));
        ctx.DrawRectangle(lid, edge, new RoundedRect(new Rect(VentX - 9, 32, 18, 6), 2));
        //  سرِ پروب
        ctx.DrawRectangle(dark, new Pen(new SolidColorBrush(Color.Parse("#4b5563")), 1),
                          new RoundedRect(new Rect(ProbeX - 9, 28, 18, 16), 4));
        //  سرِ میلهٔ شناور
        ctx.DrawRectangle(dark, null, new RoundedRect(new Rect(FloatX - 6, 38, 12, 12), 2));
        //  سرِ پمپِ غوطه‌ور (سرخ) و طوقهٔ خاکستری‌اش
        var red = new SolidColorBrush(Color.Parse("#e11d48"));
        var redEdge = new Pen(new SolidColorBrush(Color.Parse("#9f1239")), 1);
        ctx.DrawRectangle(red, redEdge, new RoundedRect(new Rect(PumpX - 11, 26, 22, 22), 5));
        ctx.DrawRectangle(dark, null, new Rect(PumpX - 7, 48, 14, 6));
    }

    // ── تیل و آب ─────────────────────────────────────────────────────────────

    private void DrawFluids(DrawingContext ctx, Levels lv)
    {
        //  ⚠️ برش فقط داخلِ همین بلوک؛ لبهٔ مخزن **بیرونِ** برش کشیده می‌شود
        //  وگرنه نیمِ بیرونیِ خطش می‌رفت.
        using (ctx.PushClip(new RoundedRect(Inner, BodyRadius - 3)))
        {
            var left = Inner.Left - 4;
            var right = Inner.Right + 4;
            var bottom = Inner.Bottom + 4;

            //  تیل — از سطحش تا کفِ مخزن؛ آب رویش کشیده می‌شود
            if (lv.OilTop < lv.WaterTop - 0.5)
            {
                var oil = Vertical((Color.Parse("#ffb648"), 0), (Color.Parse("#f28c1e"), 1));
                ctx.DrawGeometry(oil, null, Wave(left, right, lv.OilTop, bottom, 3.2, 0.6));
                ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0x6e, 255, 255, 255)), 2),
                                 WaveLine(left, right, lv.OilTop, 3.2, 0.6));
            }
            //  آب — فقط با اندازهٔ واقعیِ دستگاه
            if (HasWater && lv.WaterTop < Inner.Bottom - 0.5)
            {
                var water = Vertical((Color.Parse("#4ea3f7"), 0), (Color.Parse("#1f6fd6"), 1));
                ctx.DrawGeometry(water, null, Wave(left, right, lv.WaterTop, bottom, 2.4, 2.1));
                ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0x66, 255, 255, 255)), 1.6),
                                 WaveLine(left, right, lv.WaterTop, 2.4, 2.1));
            }
            //  سایهٔ داخلیِ کف و درخششِ زیرِ سقف — تا استوانه تخت دیده نشود
            ctx.DrawRectangle(Vertical((Color.FromArgb(0x00, 0, 0, 0), 0.72), (Color.FromArgb(0x30, 0, 0, 0), 1)), null, Inner);
            ctx.DrawRectangle(Vertical((Color.FromArgb(0x38, 255, 255, 255), 0), (Color.FromArgb(0x00, 255, 255, 255), 0.22)), null, Inner);
        }
        DrawRim(ctx);
    }

    // ── درونِ مخزن: پروب، میلهٔ شناورها، پمپ ─────────────────────────────────

    private void DrawInternals(DrawingContext ctx, Levels lv)
    {
        //  پروب — میلهٔ روشن از سرش تا نزدیکِ کف
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#e8eef4")),
                          new Pen(new SolidColorBrush(Color.Parse("#94a3b3")), 1),
                          new RoundedRect(new Rect(ProbeX - 4, 44, 8, 158), 4));

        //  میلهٔ شناورها و دو گوی — شناورِ تیل روی سطحِ تیل، شناورِ آب روی سطحِ آب
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#55606e")), 3),
                     new Point(FloatX, 50), new Point(FloatX, Inner.Bottom - 6));
        //  ⚠️ ‎Math.Clamp‎ با کمینهٔ بزرگ‌تر از بیشینه استثنا می‌دهد؛ این دو با
        //  ‎Max/Min‎ نوشته شده‌اند تا با هر عددی — حتی آبِ بالا — امن بمانند.
        var waterBall = Math.Max(Inner.Top + 9, Math.Min(lv.WaterTop, Inner.Bottom - 9));
        var oilBall = Math.Max(Inner.Top + 9, Math.Min(lv.OilTop, waterBall - 15));
        var ball = Brushes.White;
        var ballEdge = new Pen(new SolidColorBrush(Color.Parse("#8fa0b3")), 1.2);
        ctx.DrawEllipse(ball, ballEdge, new Point(FloatX, oilBall), 7.5, 7.5);
        //  شناورِ آب و خطِ راهنمای برچسبش فقط با اندازهٔ واقعیِ آب
        if (HasWater)
        {
            ctx.DrawEllipse(ball, ballEdge, new Point(FloatX, waterBall), 7.5, 7.5);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#94a3b3")), 1, new DashStyle(new[] { 2.0, 2.0 }, 0)),
                         new Point(FloatX, Body.Bottom), new Point(FloatX, 232));
        }

        //  پمپِ غوطه‌ور — میلهٔ سیاه، بدنهٔ سرخ نزدیکِ کف
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#1f2937")), null, new Rect(PumpX - 4, 54, 8, 122));
        var red = new SolidColorBrush(Color.Parse("#e11d48"));
        var redEdge = new Pen(new SolidColorBrush(Color.Parse("#9f1239")), 1);
        ctx.DrawRectangle(red, redEdge, new RoundedRect(new Rect(PumpX - 14, 172, 28, 32), 6));
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#9f1239")), null, new Rect(PumpX - 10, 204, 20, 5));
    }

    private void DrawThreshold(DrawingContext ctx, Levels lv)
    {
        if (ThresholdPercent <= 0 || ThresholdPercent >= 100) return;
        var brush = ThresholdBrush ?? new SolidColorBrush(Color.Parse("#dc2626"));
        var pen = new Pen(brush, 1.5, new DashStyle(new[] { 3.0, 3.0 }, 0));
        using var clip = ctx.PushClip(new RoundedRect(Inner, BodyRadius - 3));
        ctx.DrawLine(pen, new Point(Inner.Left, lv.ThresholdY), new Point(Inner.Right, lv.ThresholdY));
    }

    // ── ابزار ────────────────────────────────────────────────────────────────

    private static void Centered(DrawingContext ctx, string s, Typeface face, double size, IBrush brush,
                                 double cx, double top, double width, bool shadow = false,
                                 FlowDirection flow = FlowDirection.RightToLeft)
    {
        if (shadow)
        {
            var sh = new FormattedText(s, CultureInfo.CurrentCulture, flow, face, size,
                                       new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)))
            { TextAlignment = TextAlignment.Center, MaxTextWidth = width };
            ctx.DrawText(sh, new Point(cx - width / 2 + 1, top + 1.5));
        }
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, flow, face, size, brush)
        { TextAlignment = TextAlignment.Center, MaxTextWidth = width };
        ctx.DrawText(ft, new Point(cx - width / 2, top));
    }

    private static Color ColorOf(IBrush? b, Color fallback) => b is ISolidColorBrush s ? s.Color : fallback;

    private static LinearGradientBrush Vertical(params (Color c, double at)[] stops)
    {
        var g = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        };
        foreach (var (c, at) in stops) g.GradientStops.Add(new GradientStop(c, at));
        return g;
    }

    private static StreamGeometry Polygon(params Point[] pts)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(pts[0], true);
        for (var i = 1; i < pts.Length; i++) c.LineTo(pts[i]);
        c.EndFigure(true);
        return g;
    }

    private static double WaveY(double x, double left, double top, double amp, double phase)
        => top + amp * Math.Sin((x - left) / 46.0 + phase);

    /// <summary>سطحِ موج‌دار از ‎top‎ تا ‎bottom‎ — پر می‌شود.</summary>
    private static StreamGeometry Wave(double left, double right, double top, double bottom, double amp, double phase)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(new Point(left, WaveY(left, left, top, amp, phase)), true);
        for (var x = left + 4; x < right; x += 4) c.LineTo(new Point(x, WaveY(x, left, top, amp, phase)));
        c.LineTo(new Point(right, WaveY(right, left, top, amp, phase)));
        c.LineTo(new Point(right, bottom));
        c.LineTo(new Point(left, bottom));
        c.EndFigure(true);
        return g;
    }

    /// <summary>فقط خطِ سطح — برای درخششِ روی موج.</summary>
    private static StreamGeometry WaveLine(double left, double right, double top, double amp, double phase)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(new Point(left, WaveY(left, left, top, amp, phase)), false);
        for (var x = left + 4; x < right; x += 4) c.LineTo(new Point(x, WaveY(x, left, top, amp, phase)));
        c.LineTo(new Point(right, WaveY(right, left, top, amp, phase)));
        c.EndFigure(false);
        return g;
    }
}
