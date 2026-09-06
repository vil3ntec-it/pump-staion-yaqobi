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

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<SparkChart, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<SparkChart, IBrush?>(nameof(LabelBrush));

    /// <summary>برچسبِ محورها و نقطه‌ها فقط برای «روند فروش» لازم است.</summary>
    public static readonly StyledProperty<bool> ShowAxisProperty =
        AvaloniaProperty.Register<SparkChart, bool>(nameof(ShowAxis), true);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SparkChart, int>(nameof(SelectedIndex), -1);

    static SparkChart()
    {
        AffectsRender<SparkChart>(ValuesProperty, SecondValuesProperty, LabelsProperty,
            LineBrushProperty, SecondLineBrushProperty, GridBrushProperty, LabelBrushProperty,
            ShowAxisProperty, SelectedIndexProperty);
    }

    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IReadOnlyList<double>? SecondValues { get => GetValue(SecondValuesProperty); set => SetValue(SecondValuesProperty, value); }
    public IReadOnlyList<string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public IBrush? SecondLineBrush { get => GetValue(SecondLineBrushProperty); set => SetValue(SecondLineBrushProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public bool ShowAxis { get => GetValue(ShowAxisProperty); set => SetValue(ShowAxisProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var vals = Values;
        if (vals is null || vals.Count < 2) return;

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 4 || h <= 4) return;

        double left = ShowAxis ? 58 : 2, right = w - 10, top = ShowAxis ? 28 : 8, bottom = h - (ShowAxis ? 30 : 6);
        if (right <= left || bottom <= top) return;

        var second = SecondValues;
        var vmax = Math.Max(1, vals.Max());
        if (second is { Count: > 0 }) vmax = Math.Max(vmax, second.Max());

        double X(int i) => left + i / (double)(vals.Count - 1) * (right - left);
        double Y(double v) => bottom - v / vmax * (bottom - top);

        var grid = GridBrush ?? Brushes.Gray;
        var line = LineBrush ?? Brushes.Teal;
        var labelBrush = LabelBrush ?? Brushes.Gray;
        var gridPen = new Pen(grid, 1);

        // ── خط‌کش‌های افقی و عددهایشان ──
        var unitDiv = vmax >= 1e6 ? 1e6 : vmax >= 1000 ? 1000 : 1;
        var unitTxt = vmax >= 1e6 ? "میلیون" : vmax >= 1000 ? "هزار" : "";
        for (var g = 0; g <= 4; g++)
        {
            var v = vmax * (1 - g / 4.0);
            var y = Y(v);
            ctx.DrawLine(gridPen, new Point(left, y), new Point(right, y));
            if (!ShowAxis) continue;
            var x = v / unitDiv;
            var txt = (x >= 10 || x == 0 ? Math.Round(x) : Math.Round(x * 10) / 10)
                      .ToString(System.Globalization.CultureInfo.InvariantCulture);
            Text(ctx, txt, left - 6, y - 7, labelBrush, 10, TextAlignment.Right, 50);
        }
        if (ShowAxis && unitTxt.Length > 0)
            Text(ctx, unitTxt + " لیتر", left - 6, 2, labelBrush, 10, TextAlignment.Right, 50);

        // ── سطحِ زیرِ خط ──
        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(WithAlpha(line, 0.34), 0),
                new GradientStop(WithAlpha(line, 0.0), 1),
            },
        };
        var area = new StreamGeometry();
        using (var c = area.Open())
        {
            c.BeginFigure(new Point(X(0), bottom), true);
            for (var i = 0; i < vals.Count; i++) c.LineTo(new Point(X(i), Y(vals[i])));
            c.LineTo(new Point(X(vals.Count - 1), bottom));
            c.EndFigure(true);
        }
        ctx.DrawGeometry(fill, null, area);

        DrawPolyline(ctx, vals, X, Y, new Pen(line, 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
        if (second is { Count: > 1 })
            DrawPolyline(ctx, second, X, Y,
                new Pen(SecondLineBrush ?? Brushes.Orange, 2.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));

        if (!ShowAxis) return;

        // ── نقطه‌ها و برچسبِ زیرِ محور ──
        for (var i = 0; i < vals.Count; i++)
        {
            var p = new Point(X(i), Y(vals[i]));
            var r = i == SelectedIndex ? 6.0 : 4.0;
            ctx.DrawEllipse(line, null, p, r, r);
            if (Labels is { } lb && i < lb.Count)
                Text(ctx, lb[i], X(i) - 34, bottom + 6, labelBrush, 10, TextAlignment.Center, 68);
        }
    }

    private static void DrawPolyline(DrawingContext ctx, IReadOnlyList<double> vals,
                                     Func<int, double> X, Func<double, double> Y, Pen pen)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(X(0), Y(vals[0])), false);
            for (var i = 1; i < vals.Count; i++) c.LineTo(new Point(X(i), Y(vals[i])));
            c.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, g);
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
