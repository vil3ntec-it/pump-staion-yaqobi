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
        var inner = TankGauge.InnerRect;
        foreach (var s in shots)
        {
            var gauge = new TankGauge
            {
                Width = TankGauge.DesignW, Height = TankGauge.DesignH,
                //  ⛔ بی برچسبِ «پروب/شناور/پمپ» (۱۴۰۵/۰۷/۲۲: «لازم نیستند») و بی نوشتهٔ خودِ کنترل
                Compact = true, HideCenterText = true,
                FillPercent = s.FillPercent, FillText = " ",
                ThresholdPercent = s.ThresholdPercent, CaptionText = s.Caption,
                BodyBrush = card, BodyDeepBrush = input, RimBrush = edge,
                LabelBrush = text, TextBrush = text, ThresholdBrush = danger,
            };
            gauge.SetValue(TextElement.FontFamilyProperty, font);

            //  ── درصد و لیترِ مانده «داخلِ مخزن»، مثلِ خودِ برنامه (۱۴۰۵/۰۷/۲۲، با عکس) ──
            //  روی تیل سفید با سایه؛ روی بدنهٔ خالی رنگِ نوشتهٔ تم — همان قاعدهٔ ‎TankGauge‎.
            const double pctTop = 88, litTop = 130;
            var oilTop = TankGauge.LevelsOf(inner.Top, inner.Bottom, s.FillPercent, null, s.ThresholdPercent).OilTop;
            var overlay = new Canvas { Width = TankGauge.DesignW, Height = TankGauge.DesignH, IsHitTestVisible = false };
            void OnTank(string str, double size, double top, FontWeight weight, FlowDirection flow)
            {
                var onOil = oilTop <= top + size * 0.6;
                IBrush fg = onOil ? Brushes.White : (s.Low ? danger : text);
                if (onOil)
                {
                    var sh = T(str, size, new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)), weight);
                    sh.Width = 260;
                    sh.FlowDirection = flow;
                    Canvas.SetLeft(sh, inner.Center.X - 130 + 1.2);
                    Canvas.SetTop(sh, top + 1.6);
                    overlay.Children.Add(sh);
                }
                var tb = T(str, size, fg, weight);
                tb.Width = 260;
                tb.FlowDirection = flow;
                Canvas.SetLeft(tb, inner.Center.X - 130);
                Canvas.SetTop(tb, top);
                overlay.Children.Add(tb);
            }
            OnTank(s.FillText, 34, pctTop, FontWeight.Bold, FlowDirection.LeftToRight);
            OnTank(s.Liters, 17, litTop, FontWeight.SemiBold, FlowDirection.RightToLeft);
            //  ⚠️ کنترلِ مخزن چپ‌به‌راست است (‎TankGauge‎)؛ لایهٔ نوشته هم، تا همان مختصات را بخواند
            var tank = new Grid { Width = TankGauge.DesignW, Height = TankGauge.DesignH, FlowDirection = FlowDirection.LeftToRight };
            tank.Children.Add(gauge);
            tank.Children.Add(overlay);

            var stack = new StackPanel { Spacing = 4 };
            stack.Children.Add(T(s.Title, 22, text, FontWeight.Bold));
            stack.Children.Add(T(s.Sub, 14, muted));
            stack.Children.Add(tank);
            //  ⛔ بی ایموجی در عکس: قلمِ جایگزینِ ایموجی شکلِ حرف‌های فارسی را می‌شکست (سربرگِ ۰۷/۲۲)
            stack.Children.Add(T(s.Note, 16, s.Low ? danger : text, FontWeight.Bold));

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

        //  ⛔ بی سربرگ (۱۴۰۵/۰۷/۲۲): نامِ پمپ و تاریخ زیرِ عکس، در متن است؛ نوشتهٔ سربرگ با ایموجی
        //  در عکس وارونه و جدا‌جدا کشیده می‌شد.
        _ = title;
        var root = new Border
        {
            Background = page,
            Padding = new Thickness(14),
            FlowDirection = FlowDirection.RightToLeft,
            Child = row,
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
