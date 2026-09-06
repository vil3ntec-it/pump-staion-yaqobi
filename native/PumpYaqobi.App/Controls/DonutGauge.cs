using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// دایرهٔ «موجودی مخازن» — دو کمان (پطرول و دیزل) روی یک شیارِ خاکستری،
/// هرکدام به اندازهٔ سهمِ خودش از کلِ ظرفیت. همان دایرهٔ ‎#dash-donut‎.
/// </summary>
public class DonutGauge : Control
{
    public static readonly StyledProperty<double> FirstShareProperty =
        AvaloniaProperty.Register<DonutGauge, double>(nameof(FirstShare));

    public static readonly StyledProperty<double> SecondShareProperty =
        AvaloniaProperty.Register<DonutGauge, double>(nameof(SecondShare));

    public static readonly StyledProperty<IBrush?> FirstBrushProperty =
        AvaloniaProperty.Register<DonutGauge, IBrush?>(nameof(FirstBrush));

    public static readonly StyledProperty<IBrush?> SecondBrushProperty =
        AvaloniaProperty.Register<DonutGauge, IBrush?>(nameof(SecondBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<DonutGauge, IBrush?>(nameof(TrackBrush));

    static DonutGauge() => AffectsRender<DonutGauge>(FirstShareProperty, SecondShareProperty,
        FirstBrushProperty, SecondBrushProperty, TrackBrushProperty);

    /// <summary>سهمِ کمانِ اول از کلِ دایره، بر حسبِ درصد.</summary>
    public double FirstShare { get => GetValue(FirstShareProperty); set => SetValue(FirstShareProperty, value); }
    public double SecondShare { get => GetValue(SecondShareProperty); set => SetValue(SecondShareProperty, value); }
    public IBrush? FirstBrush { get => GetValue(FirstBrushProperty); set => SetValue(FirstBrushProperty, value); }
    public IBrush? SecondBrush { get => GetValue(SecondBrushProperty); set => SetValue(SecondBrushProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public override void Render(DrawingContext ctx)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 8) return;
        var thickness = Math.Max(6, size * 0.125);
        var r = (size - thickness) / 2;
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);

        ctx.DrawEllipse(null, new Pen(TrackBrush ?? Brushes.DimGray, thickness), c, r, r);

        var start = -90.0;   // از بالای دایره، ساعت‌گرد
        start = Arc(ctx, c, r, thickness, start, FirstShare, FirstBrush ?? Brushes.Green);
        Arc(ctx, c, r, thickness, start, SecondShare, SecondBrush ?? Brushes.Orange);
    }

    private static double Arc(DrawingContext ctx, Point c, double r, double thickness,
                              double startDeg, double sharePct, IBrush brush)
    {
        var sweep = Math.Max(0, Math.Min(100, sharePct)) / 100.0 * 360.0;
        if (sweep <= 0.01) return startDeg;
        var g = new StreamGeometry();
        using (var ctxG = g.Open())
        {
            var a0 = startDeg * Math.PI / 180;
            var a1 = (startDeg + sweep) * Math.PI / 180;
            ctxG.BeginFigure(new Point(c.X + r * Math.Cos(a0), c.Y + r * Math.Sin(a0)), false);
            ctxG.ArcTo(new Point(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1)),
                       new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise);
            ctxG.EndFigure(false);
        }
        ctx.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), g);
        return startDeg + sweep;
    }
}
