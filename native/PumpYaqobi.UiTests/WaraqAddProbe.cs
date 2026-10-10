using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «➕ ردیف» در تراکنش‌های ورق — جدولِ دیگری سفید نشود (۱۴۰۵/۰۷/۱۸) ══════════
/// گزارشِ صاحب ریپو: «هنگامِ اضافه‌کردنِ جدول در بخشِ تراکنش‌ها، جدولِ جدید ایجاد
/// می‌شود اما بخشِ دیگری خراب یا سفید می‌شود؛ با خروج و ورودِ دوباره درست می‌شود.»
/// روی ورقِ خالی ردیف به ردیف اضافه می‌شود و پس از هر بار، هر جدولِ صفحه سنجیده
/// می‌شود: هر ردیفِ داده ساخته و دیدنی است، بلندی و جای هر جدول همانی است که
/// پس از «خروج و ورود» درمی‌آید.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- waraqadd [پوشهٔ عکس]
/// </summary>
internal static class WaraqAddProbe
{
    private static int _bad;
    private static void Check(string what, bool ok, string? d = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (d is null ? "" : " — " + d));
        if (!ok) _bad++;
    }

    public static int Run(string? shots)
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-waraqadd-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1500, Height = 1000 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock); Pump(win);
        if (shots is not null) Directory.CreateDirectory(shots);

        var w = AppHost.Current.WaraqData.OpenOrCreateAsync(PumpYaqobi.Application.Localization.Shamsi.Today(), "پمپ").GetAwaiter().GetResult();
        var sec = vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(sec));
        var open = (CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)sec.GetType().GetProperty("OpenCommand")!.GetValue(sec)!;
        Wait(win, open.ExecuteAsync(w));
        Settle(win);

        foreach (var width in new[] { 1500, 1100 })
        {
            win.Width = width; Settle(win);
            var steps = new (string Name, Func<object, Task> Do)[]
            {
                ("➕ ردیف", pvm => ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)pvm.GetType().GetProperty("AddTxnCommand")!.GetValue(pvm)!).ExecuteAsync(null)),
                ("➕➕ پنج", pvm => ((PumpYaqobi.App.ViewModels.IRowBatchHost)pvm).AddRowsAsync(5)),
                ("پایهٔ تازه", pvm => ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)pvm.GetType().GetProperty("AddPumpCommand")!.GetValue(pvm)!).ExecuteAsync(null)),
                ("➕➕ ده", pvm => ((PumpYaqobi.App.ViewModels.IRowBatchHost)pvm).AddRowsAsync(10)),
                ("پایهٔ تازه ۲", pvm => ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)pvm.GetType().GetProperty("AddPumpCommand")!.GetValue(pvm)!).ExecuteAsync(null)),
            };
            var n = 0;
            foreach (var (name, act) in steps)
            {
                n++;
                var page = win.GetVisualDescendants().OfType<PumpYaqobi.App.Views.Sections.WaraqPageView>().First(p => p.IsEffectivelyVisible);
                Wait(win, act(page.DataContext!));
                Settle(win);
                var now = Snap(win);
                if (shots is not null) Shot(win, shots, $"w{width}-{n}");
                var back = (CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)sec.GetType().GetProperty("BackCommand")!.GetValue(sec)!;
                Wait(win, back.ExecuteAsync(null)); Settle(win);
                Wait(win, open.ExecuteAsync(w)); Settle(win);
                var re = Snap(win);
                if (shots is not null) Shot(win, shots, $"w{width}-{n}-reopen");
                Console.WriteLine($"── {width} {name}: {now} | دوباره: {re}");
                Check($"{width} {name}: هر ردیفِ داده ساخته و دیدنی است", now.AllRealized, now.ToString());
                Check($"{width} {name}: جدول‌ها همانِ «خروج و ورود»", now.Same(re), $"{now} ≠ {re}");
                var headers = win.GetVisualDescendants().OfType<DataGridRow>()
                    .Where(r => r.IsEffectivelyVisible && r.DataContext?.GetType().Name == "WaraqTxnViewModel")
                    .Select(r => r.Header?.ToString() ?? "").ToList();
                var want = headers.Count == 0 ? new List<string>() : Enumerable.Range(1, headers.Count).Select(i => i.ToString()).ToList();
                Check($"{width} {name}: شمارهٔ ردیف‌ها پیوسته ۱ تا {headers.Count}", headers.OrderBy(h => int.TryParse(h, out var v) ? v : -1).SequenceEqual(want), string.Join(",", headers.Take(40)));
            }
        }
        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private sealed record GridState(int Items, int Rows, double Top, double Height, double Width);
    private sealed record State(List<GridState> Grids, double SummaryTop)
    {
        public bool AllRealized => Grids.All(g => g.Rows >= g.Items && (g.Items == 0 || g.Height > 30));
        public bool Same(State o) =>
            Grids.Count == o.Grids.Count
            && Grids.Zip(o.Grids).All(p => p.First.Items == p.Second.Items && p.First.Rows == p.Second.Rows
                                           && Math.Abs(p.First.Top - p.Second.Top) < 2
                                           && Math.Abs(p.First.Height - p.Second.Height) < 2
                                           && Math.Abs(p.First.Width - p.Second.Width) < 2)
            && Math.Abs(SummaryTop - o.SummaryTop) < 2;
        public bool SameShape(State o) =>
            Grids.Count == o.Grids.Count && Grids.Sum(g => g.Items) == o.Grids.Sum(g => g.Items)
            && Grids.Sum(g => g.Rows) == o.Grids.Sum(g => g.Rows)
            && Grids.Select(g => Math.Round(g.Height)).OrderBy(x => x).SequenceEqual(o.Grids.Select(g => Math.Round(g.Height)).OrderBy(x => x))
            && Math.Abs(SummaryTop - o.SummaryTop) < 2;
        public override string ToString() =>
            string.Join(" ", Grids.Select(g => $"[{g.Items}/{g.Rows} y{g.Top:0} h{g.Height:0} w{g.Width:0}]")) + $" Σy{SummaryTop:0}";
    }

    private static State Snap(Window win)
    {
        var page = win.GetVisualDescendants().OfType<PumpYaqobi.App.Views.Sections.WaraqPageView>().First(p => p.IsEffectivelyVisible);
        var grids = page.GetVisualDescendants().OfType<ExcelGrid>().Where(g => g.IsEffectivelyVisible)
            .Select(g =>
            {
                var items = g.ItemsSource is System.Collections.IList l ? l.Count : 0;
                var rows = g.GetVisualDescendants().OfType<DataGridRow>()
                            .Count(r => r.IsEffectivelyVisible && r.Bounds.Height > 0
                                        && r.TranslatePoint(default, g) is { } p && p.Y >= 0 && p.Y < g.Bounds.Height);
                var at = g.TranslatePoint(default, page) ?? default;
                return new GridState(items, rows, at.Y, g.Bounds.Height, g.Bounds.Width);
            }).OrderBy(x => x.Top).ThenBy(x => x.Width).ToList();
        var sum = page.GetVisualDescendants().OfType<AutoFillPanel>().FirstOrDefault()?.TranslatePoint(default, page)?.Y ?? -1;
        return new State(grids, sum);
    }

    private static void Shot(Window win, string dir, string name)
    {
        using var f = win.CaptureRenderedFrame();
        f?.Save(Path.Combine(dir, name + ".png"));
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 2000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Pump(w);
    }
    private static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
    private static void Settle(Window w) { for (var i = 0; i < 30; i++) { Pump(w); Thread.Sleep(5); } }
}
