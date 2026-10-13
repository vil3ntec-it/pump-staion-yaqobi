using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PumpYaqobi.App.Controls;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ عکسِ مخزن‌ها برای باتِ تلگرام — با خودِ ‎TankGauge‎ (۱۴۰۵/۰۷/۲۱) ══════════
///
/// خواستهٔ صاحب ریپو: «همین عکس باشد — مخزن همان باشد… کیفیت افت نکند.»
/// ⛔ نقاشیِ تازه‌ای نیست: همان کنترلِ ‎TankGauge‎ با همان خاصیت‌ها و همان قلم و
/// رنگ‌های تمِ برنامه (‎Pump.Card‎ · ‎Pump.Input‎ · ‎Pump.ChromeBorder‎ · ‎Pump.Text‎ ·
/// ‎Pump.Danger‎ — عینِ ‎StorageSectionView.axaml‎) در ‎RenderTargetBitmap‎ کشیده
/// می‌شود، با دو برابرِ پیکسل تا روی گوشی تیز بماند.
/// ⚠️ روی نخِ رابط؛ بیرون از درختِ پنجره، پس قلم صریح داده می‌شود.
/// </summary>
public static class TankShotRenderer
{
    /// <summary>دو برابرِ پیکسل — همان «کیفیت افت نکند».</summary>
    public const double Scale = 2.0;

    public static Task<byte[]?> RenderAsync(IReadOnlyList<TankShot> shots, string title) =>
        Dispatcher.UIThread.InvokeAsync<byte[]?>(() =>
        {
            try { return Render(shots, title); }
            catch { return null; }
        }).GetTask();

    private static byte[]? Render(IReadOnlyList<TankShot> shots, string title)
    {
        var app = Avalonia.Application.Current;
        if (app is null || shots.Count == 0) return null;

        object? Res(string key) => app.TryFindResource(key, out var v) ? v : null;
        IBrush Br(string key, string fallback) => Res(key) as IBrush ?? new SolidColorBrush(Color.Parse(fallback));

        var font = Res("Pump.Font") as FontFamily ?? FontFamily.Default;
        var card = Br("Pump.Card", "#ffffff");
        var input = Br("Pump.Input", "#e4ecf8");
        var edge = Br("Pump.ChromeBorder", "#9fb0c3");
        var text = Br("Pump.Text", "#1f2937");
        var muted = Br("Pump.Muted", "#64748b");
        var danger = Br("Pump.Danger", "#dc2626");
        var page = Br("Pump.AppBg", "#f4f7fb");

        TextBlock T(string s, double size, IBrush brush, FontWeight weight = FontWeight.Normal) => new()
        {
            Text = s, FontSize = size, Foreground = brush, FontWeight = weight, FontFamily = font,
            HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, FlowDirection = FlowDirection.RightToLeft };
        foreach (var s in shots)
        {
            var gauge = new TankGauge
            {
                Width = 400, Height = 250,
                FillPercent = s.FillPercent, FillText = s.FillText,
                ThresholdPercent = s.ThresholdPercent, CaptionText = s.Caption,
                BodyBrush = card, BodyDeepBrush = input, RimBrush = edge,
                LabelBrush = text, TextBrush = text, ThresholdBrush = danger,
            };
            gauge.SetValue(TextElement.FontFamilyProperty, font);

            var stack = new StackPanel { Spacing = 4 };
            stack.Children.Add(T(s.Title, 20, text, FontWeight.Bold));
            stack.Children.Add(T(s.Sub, 14, muted));
            if (s.Low) stack.Children.Add(T("🔴 زیرِ حدِ هشدار", 13, danger, FontWeight.Bold));
            stack.Children.Add(gauge);

            row.Children.Add(new Border
            {
                Background = card,
                BorderBrush = s.Low ? danger : edge,
                BorderThickness = new Thickness(s.Low ? 2 : 1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 10),
                Child = stack,
            });
        }

        var head = T(title, 18, text, FontWeight.Bold);
        head.HorizontalAlignment = HorizontalAlignment.Right;
        var root = new Border
        {
            Background = page,
            Padding = new Thickness(18),
            FlowDirection = FlowDirection.RightToLeft,
            Child = new StackPanel { Spacing = 12, Children = { head, row } },
        };
        root.SetValue(TextElement.FontFamilyProperty, font);

        root.Measure(Size.Infinity);
        root.Arrange(new Rect(root.DesiredSize));
        var px = new PixelSize((int)Math.Ceiling(root.DesiredSize.Width * Scale),
                               (int)Math.Ceiling(root.DesiredSize.Height * Scale));
        if (px.Width <= 0 || px.Height <= 0) return null;
        using var bmp = new RenderTargetBitmap(px, new Vector(96 * Scale, 96 * Scale));
        bmp.Render(root);
        using var ms = new MemoryStream();
        bmp.Save(ms);
        return ms.ToArray();
    }
}
