using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ سرِ جدول و «جمله»ی زیرش ════════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «سرِ جدول‌ها هنوز وسط نیستن، جمله‌هاشون؛ و زیرِ جدول‌ها هم
/// پایینِ همان کادرِ موردِ نظرشان نیستن.»
///
/// این دو ادعا با چشم قابلِ بحث‌اند و با عدد نه. پس این‌جا روی پنجرهٔ واقعی
/// اندازه گرفته می‌شود:
///
///   ۱) نوشتهٔ هر سرستون، نسبت به خودِ آن ستون، وسط است؟
///   ۲) هر خانهٔ «جمله» دقیقاً زیرِ ستونِ هم‌نامِ خودش نشسته؟
///
///     dotnet run --project PumpYaqobi.UiTests -- chrome
/// </summary>
internal static class TableChromeAudit
{
    /// <summary>بیشترین انحرافِ پذیرفتنی از مرکز (پیکسل).</summary>
    private const double Slack = 2.0;

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-chrome-" + Guid.NewGuid().ToString("N"), "pump.db");
        PumpYaqobi.App.Services.AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Wait(win, Task.CompletedTask);
        Pump(win);
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        var bad = new List<string>();

        Console.WriteLine();
        Console.WriteLine("بخش            ستون                 مرکزِ ستون   مرکزِ نوشته   مرکزِ جمله   نتیجه");
        Console.WriteLine(new string('-', 88));

        foreach (var sec in vm.Sections.ToList())
        {
            Wait(win, vm.GoAsync(sec));
            // ⚠️ نوارِ «جمله» پهنای ستون‌ها را از خودِ جدول می‌خواند و یک پاسِ
            // چیدمان عقب‌تر است؛ پس تا ته‌نشین شدن پمپ می‌کنیم، وگرنه عددِ
            // نیم‌پختهٔ یک پاسِ میانی خوانده می‌شود.
            for (var k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); Pump(win); }

            var host = win.GetVisualDescendants().OfType<ContentControl>()
                          .FirstOrDefault(c => ReferenceEquals(c.Content, sec));
            if (host is null) continue;

            var grid = host.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
            if (grid is null) continue;

            // مرکزِ هر سرستون و مرکزِ نوشتهٔ داخلش، در مختصاتِ پنجره
            var heads = grid.GetVisualDescendants().OfType<DataGridColumnHeader>()
                            .Where(h => h.Bounds.Width > 0)
                            .ToList();

            // خانه‌های «جمله» — اگر این بخش نوار دارد
            var strip = host.GetVisualDescendants().OfType<TotalsStrip>().FirstOrDefault();
            var cells = strip?.GetVisualChildren().OfType<Control>()
                              .Where(c => c.Bounds.Width > 0).ToList() ?? new List<Control>();

            // ══ هر خانهٔ «جمله» ستونی به نامِ خودش دارد؟ ═════════════════════
            //
            // گزارشِ صاحب ریپو: «بخشِ صرافی الباقی نداره.» عدد ساخته می‌شد ولی
            // جدول ستونی به نامِ «الباقی ($)» نداشت، و نوارِ جمله خانه‌ای را که
            // ستون ندارد به دُمِ خودش می‌فرستد — یعنی از چشم می‌افتد.
            //
            // ⚠️ این از «وسط بودن» جداست: خانهٔ بی‌ستون اصلاً سنجیده نمی‌شد،
            // پس سنجش سبز می‌داد در حالی که عددی گم شده بود.
            var names = heads.Select(h => h.Content?.ToString()?.Trim() ?? "").ToHashSet();
            foreach (var c in cells)
            {
                var want = (c.DataContext as TotalCell)?.Column?.Trim();
                if (string.IsNullOrEmpty(want) || names.Contains(want)) continue;
                Console.WriteLine($"{sec.Id,-14} {Cut(want),-20} {"—",10} {"—",12} "
                                + $"{"بی‌ستون",12}   ✖");
                bad.Add($"{sec.Id} · جملهٔ «{want}» ستونی به این نام ندارد و به دُمِ نوار می‌افتد");
            }

            foreach (var h in heads)
            {
                var name = h.Content?.ToString()?.Trim() ?? "";
                if (name.Length == 0) continue;

                var colMid = MidX(win, h);
                var text = h.GetVisualDescendants().OfType<TextBlock>()
                            .FirstOrDefault(t => (t.Text ?? "").Trim() == name);
                var textMid = text is null ? colMid : MidX(win, text);

                var cell = cells.FirstOrDefault(c =>
                    (c.DataContext as TotalCell)?.Column?.Trim() == name);
                var cellMid = cell is null ? double.NaN : MidX(win, cell);

                var headOff = Math.Abs(textMid - colMid);
                var cellOff = double.IsNaN(cellMid) ? 0 : Math.Abs(cellMid - colMid);
                var ok = headOff <= Slack && cellOff <= Slack;

                if (!ok || headOff > 0.5 || cellOff > 0.5)
                    Console.WriteLine($"{sec.Id,-14} {Cut(name),-20} {colMid,10:0.0} {textMid,12:0.0} "
                                    + $"{(double.IsNaN(cellMid) ? "—" : cellMid.ToString("0.0")),12} "
                                    + $"  {(ok ? "✔" : "✖")}");

                if (Environment.GetEnvironmentVariable("PUMP_HEADPROBE") == "1" && headOff > Slack)
                {
                    Console.WriteLine($"   سرستونِ «{name}» ({h.Bounds.Width:0}px) از چه ساخته شده:");
                    foreach (var d in h.GetVisualDescendants().OfType<Control>())
                        Console.WriteLine($"      {d.GetType().Name,-22} "
                            + $"x={MidX(win, d) - MidX(win, h),7:0.0} w={d.Bounds.Width,6:0.0} "
                            + $"want={d.DesiredSize.Width,6:0.0} vis={d.IsVisible} "
                            + $"col={Avalonia.Controls.Grid.GetColumn(d)} "
                            + $"halign={d.HorizontalAlignment} m={d.Margin} name={d.Name}"
                            + (d is Avalonia.Controls.Grid gg
                                ? "  cols=[" + string.Join(" | ", gg.ColumnDefinitions
                                    .Select(cd => $"{cd.Width}→{cd.ActualWidth:0}")) + "]"
                                : ""));
                    Environment.SetEnvironmentVariable("PUMP_HEADPROBE", "0");
                }

                if (headOff > Slack)
                    bad.Add($"{sec.Id} · سرستونِ «{name}» {headOff:0.0}px از مرکز دور است");
                if (cellOff > Slack)
                    bad.Add($"{sec.Id} · جملهٔ «{name}» {cellOff:0.0}px از ستونش دور است");
            }
        }

        // ── رنگِ نوارِ سر و ته جدول ──────────────────────────────────────────
        Console.WriteLine();
        foreach (var key in new[] { "Pump.HeadBand", "Pump.OnHeadBand", "Pump.Table.Border" })
            if (Avalonia.Application.Current!.TryFindResource(key, out var v) && v is ISolidColorBrush b)
                Console.WriteLine($"{key,-22} {b.Color}   روشنایی {Luma(b.Color):0.00}");

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine("✅ سرستون‌ها وسط‌اند و هر جمله زیرِ ستونِ خودش است");
            return 0;
        }

        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    /// <summary>روشناییِ دیداریِ رنگ (۰ سیاه، ۱ سفید) — برای «خیلی تیره است؟».</summary>
    private static double Luma(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static double MidX(Visual root, Visual v)
    {
        var p = v.TranslatePoint(new Point(v.Bounds.Width / 2, 0), root);
        return p?.X ?? double.NaN;
    }

    private static string Cut(string s) => s.Length <= 18 ? s : s[..18];

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Thread.Sleep(5);
        }
        Pump(w);
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }
}
