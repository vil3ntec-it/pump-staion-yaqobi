using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// نمودارِ سطحیِ «روند فروش» و «روند مفاد و مصارف» — همان چیزی که در نسخهٔ وب
/// با SVG کشیده می‌شد، این‌بار با خودِ موتورِ رسمِ برنامه.
///
/// نقطهٔ ۰ چپ‌ترین نقطه است (خواستهٔ صریحِ صاحب ریپو: «امروز باید اول باشد»).
/// </summary>
public class SparkChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<SparkChart, IReadOnlyList<double>?>(nameof(Values));

    /// <summary>خطِ دومِ اختیاری — «مصارف» کنارِ «مفاد».</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> SecondValuesProperty =
        AvaloniaProperty.Register<SparkChart, IReadOnlyList<double>?>(nameof(SecondValues));

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<SparkChart, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<SparkChart, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> SecondLineBrushProperty =
        AvaloniaProperty.Register<SparkChart, IBrush?>(nameof(SecondLineBrush));

    /// <summary>رنگِ نقطه‌های روی خط — ‎#60A5FA‎ی مرجع؛ خالی = همان رنگِ خط.</summary>
    public static readonly StyledProperty<IBrush?> PointBrushProperty =
        AvaloniaProperty.Register<SparkChart, IBrush?>(nameof(PointBrush));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<SparkChart, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<SparkChart, IBrush?>(nameof(LabelBrush));

    /// <summary>برچسبِ محورها و نقطه‌ها فقط برای «روند فروش» لازم است.</summary>
    public static readonly StyledProperty<bool> ShowAxisProperty =
        AvaloniaProperty.Register<SparkChart, bool>(nameof(ShowAxis), true);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SparkChart, int>(nameof(SelectedIndex), -1);

    /// <summary>واحدِ محور («لیتر» برای فروش، «افغانی» برای مفاد و مصارف).</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<SparkChart, string>(nameof(Unit), "لیتر");

    /// <summary>نامِ دو خط در حبابِ زیرِ ماوس — پیش‌فرض همان «مصارف»ِ کنارِ مفاد.</summary>
    public static readonly StyledProperty<string> FirstNameProperty =
        AvaloniaProperty.Register<SparkChart, string>(nameof(FirstName), "");
    public static readonly StyledProperty<string> SecondNameProperty =
        AvaloniaProperty.Register<SparkChart, string>(nameof(SecondName), "مصارف");

    public string FirstName { get => GetValue(FirstNameProperty); set => SetValue(FirstNameProperty, value); }
    public string SecondName { get => GetValue(SecondNameProperty); set => SetValue(SecondNameProperty, value); }

    static SparkChart()
    {
        AffectsRender<SparkChart>(ValuesProperty, SecondValuesProperty, LabelsProperty,
            LineBrushProperty, SecondLineBrushProperty, PointBrushProperty, GridBrushProperty, LabelBrushProperty,
            ShowAxisProperty, SelectedIndexProperty, UnitProperty, FirstNameProperty, SecondNameProperty);
    }

    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IReadOnlyList<double>? SecondValues { get => GetValue(SecondValuesProperty); set => SetValue(SecondValuesProperty, value); }
    public IReadOnlyList<string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public IBrush? SecondLineBrush { get => GetValue(SecondLineBrushProperty); set => SetValue(SecondLineBrushProperty, value); }
    public IBrush? PointBrush { get => GetValue(PointBrushProperty); set => SetValue(PointBrushProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public bool ShowAxis { get => GetValue(ShowAxisProperty); set => SetValue(ShowAxisProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    // ══ ظاهرِ تازه (۱۴۰۵/۰۷/۱۹) ═══════════════════════════════════════════════
    //  خواستهٔ صاحب ریپو: «چارت‌ها مربع/مستطیلی، گوشه‌دار و قدیمی‌اند — مدرن، نرم و
    //  باکیفیت.» پس: خطِ نرم (منحنیِ یکنوای ‎Hermite‎ — هرگز زیرِ صفر یا بالای بیشینه
    //  نمی‌رود و عدد را دروغ نمی‌کند)، سطحِ گرادیانیِ هر دو خط، خط‌کشِ نقطه‌چینِ
    //  کم‌رنگ، نقطه‌های حلقه‌ای، و با بردنِ ماوس روی نمودار یک خطِ راهنما و حبابِ
    //  عدد. ⛔ هیچ عددی این‌جا ساخته نمی‌شود — فقط همان ‎Values‎ کشیده می‌شود.
    //  ⚡ کشیدن فقط با تغییرِ داده یا جای ماوس؛ هیچ زمان‌سنج یا انیمیشنی نیست.
    private int _hover = -1;

    protected override void OnPointerMoved(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var n = Math.Max(Values?.Count ?? 0, SecondValues?.Count ?? 0);
        if (n < 2) return;
        var (left, right, _, _) = Frame(Bounds.Width, Bounds.Height);
        var x = e.GetPosition(this).X;
        var i = (int)Math.Round((x - left) / Math.Max(1, right - left) * (n - 1));
        i = Math.Clamp(i, 0, n - 1);
        if (i == _hover) return;
        _hover = i;
        InvalidateVisual();
    }

    protected override void OnPointerExited(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover < 0) return;
        _hover = -1;
        InvalidateVisual();
    }

    private (double Left, double Right, double Top, double Bottom) Frame(double w, double h) =>
        (ShowAxis ? 58 : 4, w - 12, ShowAxis ? 30 : 10, h - (ShowAxis ? 30 : 8));

    public override void Render(DrawingContext ctx)
    {
        //  ⛔ خطِ اول خالی (مفادِ پشتِ رمز) ⇒ خطِ دوم (مصارف) تنها کشیده می‌شود
        var vals = Values;
        var second = SecondValues;
        if (vals is null || vals.Count < 2)
        {
            if (second is not { Count: >= 2 }) return;
            vals = null;
        }
        var n = vals?.Count ?? second!.Count;

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 4 || h <= 4) return;

        var (left, right, top, bottom) = Frame(w, h);
        if (right <= left || bottom <= top) return;

        var vmax = 1.0;
        if (vals is not null) vmax = Math.Max(vmax, vals.Max());
        if (second is { Count: > 0 }) vmax = Math.Max(vmax, second.Max());
        vmax = Nice(vmax);

        double X(int i) => left + i / (double)(n - 1) * (right - left);
        double Y(double v) => bottom - Math.Max(0, v) / vmax * (bottom - top);

        var grid = GridBrush ?? Brushes.Gray;
        var line = LineBrush ?? Brushes.Teal;
        var line2 = SecondLineBrush ?? Brushes.Orange;
        var labelBrush = LabelBrush ?? Brushes.Gray;
        var gridPen = new Pen(grid, 1, new DashStyle(new double[] { 3, 4 }, 0));
        var basePen = new Pen(grid, 1.2);

        // ── خط‌کش‌های افقیِ نقطه‌چین و عددهایشان ──
        var unitDiv = vmax >= 1e6 ? 1e6 : vmax >= 1000 ? 1000 : 1;
        var unitTxt = vmax >= 1e6 ? "میلیون" : vmax >= 1000 ? "هزار" : "";
        for (var g = 0; g <= 4; g++)
        {
            var v = vmax * (1 - g / 4.0);
            var y = Y(v);
            ctx.DrawLine(g == 4 ? basePen : gridPen, new Point(left, y), new Point(right, y));
            if (!ShowAxis) continue;
            var x = v / unitDiv;
            var txt = (x >= 10 || x == 0 ? Math.Round(x) : Math.Round(x * 10) / 10)
                      .ToString(System.Globalization.CultureInfo.InvariantCulture);
            //  ⛔ جعبهٔ ۵۰ پیکسلی **پیش از** ‎left‎ می‌نشیند (در حاشیهٔ چپ)؛ تا
            //  امروز از ‎left−6‎ شروع می‌شد و عدد ۴۴ پیکسل داخلِ نمودار روی خط‌ها
            //  و نقطهٔ اول می‌افتاد.
            Text(ctx, txt, left - 6 - 50, y - 7, labelBrush, 10, TextAlignment.Right, 50);
        }
        if (ShowAxis)
            Text(ctx, (unitTxt.Length > 0 ? unitTxt + " " : "") + Unit, left - 6 - 50, 2, labelBrush, 10, TextAlignment.Right, 50);

        // ── خطِ دوم (پشت) و خطِ اول (رو)، هر کدام با سطحِ گرادیانیِ خودش ──
        if (second is { Count: > 1 })
        {
            var p2 = Points(second, X, Y);
            Area(ctx, p2, bottom, line2, vals is null ? 0.30 : 0.16);
            ctx.DrawGeometry(null, new Pen(line2, 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Smooth(p2));
        }
        if (vals is not null)
        {
            var p1 = Points(vals, X, Y);
            Area(ctx, p1, bottom, line, 0.32);
            ctx.DrawGeometry(null, new Pen(line, 2.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Smooth(p1));
        }

        // ── برچسبِ زیرِ محور، کم‌پشت وقتی جا تنگ است ──
        if (ShowAxis && Labels is { } lb)
        {
            var step = (right - left) / Math.Max(1, n - 1);
            var every = Math.Max(1, (int)Math.Ceiling(56 / Math.Max(1, step)));
            for (var i = 0; i < n && i < lb.Count; i++)
                if (i % every == 0 || i == n - 1 && (n - 1) % every >= every / 2.0)
                    Text(ctx, lb[i], X(i) - 34, bottom + 7, labelBrush, 10, TextAlignment.Center, 68);
        }

        // ── نقطه‌ها: حلقه، و هالهٔ نرم دورِ نقطهٔ انتخاب‌شده ──
        var dot = PointBrush ?? line;
        if (ShowAxis && vals is not null && n <= 40)
            for (var i = 0; i < n; i++)
            {
                var p = new Point(X(i), Y(vals[i]));
                if (i == SelectedIndex || i == _hover)
                    ctx.DrawEllipse(new SolidColorBrush(WithAlpha(line, 0.22)), null, p, 10, 10);
                ctx.DrawEllipse(dot, new Pen(line, 2), p, i == SelectedIndex ? 5.5 : 3.6, i == SelectedIndex ? 5.5 : 3.6);
            }

        // ── راهنمای زیرِ ماوس: خطِ عمودی و حبابِ عدد ──
        if (_hover >= 0 && _hover < n)
        {
            var hx = X(_hover);
            ctx.DrawLine(new Pen(new SolidColorBrush(WithAlpha(line, 0.55)), 1.2, new DashStyle(new double[] { 2, 3 }, 0)),
                         new Point(hx, top), new Point(hx, bottom));
            var parts = new List<string>();
            if (Labels is { } l2 && _hover < l2.Count) parts.Add(l2[_hover]);
            if (vals is not null) parts.Add((FirstName.Length > 0 ? FirstName + " " : "") + Fmt(vals[_hover]) + " " + Unit);
            if (second is { } s2 && _hover < s2.Count && vals is not null) parts.Add(SecondName + " " + Fmt(s2[_hover]));
            else if (second is { } s3 && _hover < s3.Count) parts.Add(Fmt(s3[_hover]) + " " + Unit);
            var tip = string.Join(" · ", parts);
            var ft = new FormattedText(tip, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.RightToLeft, Typeface.Default, 11.5, Brushes.White);
            var bw = ft.Width + 18;
            var bh = ft.Height + 10;
            var bx = Math.Clamp(hx - bw / 2, left, Math.Max(left, right - bw));
            var by = Math.Max(0, top - bh - 2);
            if (vals is not null) by = Math.Max(0, Math.Min(by, Y(vals[_hover]) - bh - 12));
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xe6, 0x1f, 0x29, 0x37)), null,
                new RoundedRect(new Rect(bx, by, bw, bh), 8));
            ctx.DrawText(ft, new Point(bx + 9, by + 5));
        }
    }

    /// <summary>بیشینهٔ «گرد» برای محور — ۷۳۴ ⇐ ۸۰۰، تا خط‌کش‌ها عددهای خوانا بگیرند.</summary>
    private static double Nice(double v)
    {
        if (v <= 0) return 1;
        var p = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 })
            if (m * p >= v) return m * p;
        return v;
    }

    //  عددِ کوچک (فایدهٔ فی لیتر: ۳٫۵) یک رقمِ اعشار نگه می‌دارد؛ بزرگ‌ها همان گرد.
    private static string Fmt(double v) => Math.Abs(v) < 100
        ? Math.Round(v, 1).ToString("#,##0.#", System.Globalization.CultureInfo.InvariantCulture)
        : Math.Round(v).ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture);

    private static Point[] Points(IReadOnlyList<double> v, Func<int, double> X, Func<double, double> Y)
    {
        var p = new Point[v.Count];
        for (var i = 0; i < v.Count; i++) p[i] = new Point(X(i), Y(v[i]));
        return p;
    }

    private static void Area(DrawingContext ctx, Point[] p, double bottom, IBrush line, double alpha)
    {
        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(WithAlpha(line, alpha), 0),
                new GradientStop(WithAlpha(line, alpha * 0.35), 0.6),
                new GradientStop(WithAlpha(line, 0.0), 1),
            },
        };
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(p[0].X, bottom), true);
            c.LineTo(p[0]);
            Curve(c, p);
            c.LineTo(new Point(p[^1].X, bottom));
            c.EndFigure(true);
        }
        ctx.DrawGeometry(fill, null, g);
    }

    private static StreamGeometry Smooth(Point[] p)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(p[0], false);
            Curve(c, p);
            c.EndFigure(false);
        }
        return g;
    }

    /// <summary>
    /// منحنیِ یکنوای ‎Hermite‎ (‎Fritsch–Carlson‎) — از هیچ نقطه‌ای بالاتر یا پایین‌تر
    /// از همسایه‌هایش نمی‌رود، پس نمودار هرگز «زیرِ صفر» یا قله‌ای ساختگی نشان نمی‌دهد.
    /// </summary>
    private static void Curve(StreamGeometryContext c, Point[] p)
    {
        var n = p.Length;
        if (n < 2) return;
        if (n == 2) { c.LineTo(p[1]); return; }
        var d = new double[n - 1];
        for (var i = 0; i < n - 1; i++)
            d[i] = (p[i + 1].Y - p[i].Y) / Math.Max(1e-9, p[i + 1].X - p[i].X);
        var m = new double[n];
        m[0] = d[0];
        m[n - 1] = d[n - 2];
        for (var i = 1; i < n - 1; i++)
            m[i] = d[i - 1] * d[i] <= 0 ? 0 : (d[i - 1] + d[i]) / 2;
        for (var i = 0; i < n - 1; i++)
        {
            if (d[i] == 0) { m[i] = 0; m[i + 1] = 0; continue; }
            var a = m[i] / d[i];
            var b = m[i + 1] / d[i];
            var s = a * a + b * b;
            if (s > 9)
            {
                var t = 3 / Math.Sqrt(s);
                m[i] = t * a * d[i];
                m[i + 1] = t * b * d[i];
            }
        }
        for (var i = 0; i < n - 1; i++)
        {
            var dx = (p[i + 1].X - p[i].X) / 3;
            c.CubicBezierTo(new Point(p[i].X + dx, p[i].Y + m[i] * dx),
                            new Point(p[i + 1].X - dx, p[i + 1].Y - m[i + 1] * dx),
                            p[i + 1]);
        }
    }

    private static Color WithAlpha(IBrush b, double a) =>
        b is ISolidColorBrush s
            ? Color.FromArgb((byte)(a * 255), s.Color.R, s.Color.G, s.Color.B)
            : Color.FromArgb((byte)(a * 255), 0x38, 0xb2, 0xac);

    private void Text(DrawingContext ctx, string s, double x, double y, IBrush brush,
                      double size, TextAlignment align, double width)
    {
        var ft = new FormattedText(s, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.RightToLeft, Typeface.Default, size, brush)
        { TextAlignment = align, MaxTextWidth = width };
        ctx.DrawText(ft, new Point(x, y));
    }
}
