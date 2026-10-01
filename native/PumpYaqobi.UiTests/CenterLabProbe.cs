using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ آزمایشگاهِ وسط‌چینی — هر نوعِ نوشته، با پیکسلِ واقعیِ قاب ══════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۹): «نوشته‌ها و سربرگ‌ها و ماه‌های قبل همه چپ یا
/// راست می‌رود و وسط نیستند.» سنجه‌های پیشین فقط دادهٔ خودِ سنجه را داشتند؛ این
/// یکی هر شکلِ نوشته‌ای را که دستِ کاربر می‌رسد (فاصلهٔ پایانی و آغازی، نویسهٔ
/// نامرئی، رقمِ فارسی و لاتین، تاریخ، ایموجی، پرانتز، دادهٔ واردشده از نسخهٔ وب)
/// در سه چیدمان می‌گذارد و وسطِ جوهرش را با وسطِ کادر می‌سنجد.
///
///   dotnet run --project PumpYaqobi.UiTests -c Release -- centerlab
/// </summary>
internal static class CenterLabProbe
{
    internal static readonly string[] Texts =
    {
        "کریم", "کریم ", " کریم", "کریم  ", "‏کریم", "کریم‏", "کریم‌", "‎کریم",
        "حوالهٔ 1", "حوالهٔ 12 ", "چکنهٔ ۱۲", "12,000", "۱۲٬۰۰۰", "12000 ", "1405/06/10",
        "۱۴۰۵/۰۶/۱۰", "1405/6/10 ", "⛽ جمله پطرول", "🏛️ جمله ماندگی", "جمله (روز)",
        "پطرول.", "مصرف: 500", "ABC", "abc ", "AFN 500", "500 افغانی", "-1,250",
        "کریم دیزل 40 لیتر", "\t کریم \t", "کریم ", "بابت نان (حواله 742)",
    };

    private static int _bad;

    public static int Run(string[] args)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-clab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        PumpYaqobi.App.Services.AppHost.Start(Path.Combine(dir, "pump.db"));
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        foreach (var scale in new[] { 1.0, 1.25, 1.5 })
        foreach (var mode in new[] { "stretch", "hcenter", "wrap", "grid" })
            One(scale, mode, args.Length > 1 ? args[1] : null);

        Console.WriteLine(_bad == 0 ? "✅ همه وسط" : $"❌ {_bad} نوشتهٔ کج");
        return _bad == 0 ? 0 : 1;
    }

    private static void One(double scale, string mode, string? shots)
    {
        var win = new Window { Width = 900, Height = 1500 };
        LayoutCycleProbe.SetScaling(win, scale);
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(10) };
        DataGrid? grid = null;
        if (mode == "grid")
        {
            grid = new ExcelGrid { Height = 1400, AutoGenerateColumns = false };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "نام", Binding = new Avalonia.Data.Binding("."), Width = new DataGridLength(220)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "مقدار 1", Binding = new Avalonia.Data.Binding("Length"), Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            grid.ItemsSource = Texts;
            win.Content = grid;
        }
        else
        {
            foreach (var t in Texts)
            {
                var tb = new TextBlock { Text = t, FontSize = 15 };
                if (mode == "hcenter") tb.HorizontalAlignment = HorizontalAlignment.Center;
                if (mode == "wrap") RtlTrim.SetEnabled(tb, true);
                panel.Children.Add(new Border
                {
                    Width = 260, Height = 34, Child = tb, BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }
            win.Content = panel;
        }
        win.Show();
        for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        win.CaptureRenderedFrame()?.Dispose();
        for (var i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        using var shot = win.CaptureRenderedFrame()!;
        using var ms = new MemoryStream();
        shot.Save(ms);
        ms.Position = 0;
        using var frame = SkiaSharp.SKBitmap.Decode(ms);
        var bad = new List<string>();
        var n = 0;
        IEnumerable<(TextBlock tb, Visual box)> items = mode == "grid"
            ? grid!.GetVisualDescendants().OfType<DataGridCell>().Where(c => c.IsEffectivelyVisible)
                   .SelectMany(c => c.GetVisualDescendants().OfType<TextBlock>().Select(t => (t, (Visual)c)))
                   .Concat(grid.GetVisualDescendants().OfType<DataGridColumnHeader>()
                       .SelectMany(h => h.GetVisualDescendants().OfType<TextBlock>().Select(t => (t, (Visual)h))))
            : panel.Children.OfType<Border>().Select(b => ((TextBlock)b.Child!, (Visual)b));
        foreach (var (tb, box) in items)
        {
            if (string.IsNullOrEmpty(tb.Text) || !tb.IsEffectivelyVisible) continue;
            n++;
            if (OldMonthProbe.Off(tb, box, frame) is { } o && o > 2)
            {
                bad.Add($"«{Show(tb.Text)}» {o:0.#}px");
                if (Environment.GetEnvironmentVariable("CL_DEBUG") == "1")
                {
                    var tl = tb.TextLayout;
                    var rs = string.Join(" ", tl.HitTestTextRange(0, tb.Text!.Length).Select(r => $"[{r.Left:0.#},{r.Right:0.#}]"));
                    Console.WriteLine($"   dbg «{Show(tb.Text!)}» lines={tl.TextLines.Count} len={tl.TextLines[0].Length}/{tb.Text!.Length} first={tl.TextLines[0].FirstTextSourceIndex} maxW={tl.MaxWidth:0.#} tbW={tb.Bounds.Width:0.#} pad={tb.Padding} rects={rs} fix={RtlTrim.CenterFix(tb):0.#} align={tb.TextAlignment} rt={tb.RenderTransform}");
                }
            }
        }
        if (bad.Count > 0 && shots is { Length: > 0 })
        {
            Directory.CreateDirectory(shots);
            File.WriteAllBytes(Path.Combine(shots, $"clab-{mode}-{scale}.png"), ms.ToArray());
        }
        _bad += bad.Count;
        Console.WriteLine($"{(bad.Count == 0 ? "✅" : "❌")} {mode} ×{scale}: {n} نوشته"
                          + (bad.Count == 0 ? "" : " — " + string.Join("، ", bad)));
        win.Close();
    }

    private static string Show(string s) =>
        s.Replace("‏", "<RLM>").Replace("‎", "<LRM>").Replace("‌", "<ZWNJ>")
         .Replace(" ", "<NBSP>").Replace("\t", "<TAB>");
}
