using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ کشیدنِ خطِ ستون با ماوسِ واقعی — نه جای خالی، نه ستونِ بیرون‌زده (۱۴۰۵/۰۷/۱۷) ══
///
/// گزارشِ صاحب ریپو با عکس: «این بزرگ یا کوچک کردن این مدل باشه که نه این جای خالی
/// به وجود بیاد نه اون طرف بره که دیده نشن کادرها… توی همون کادرشون همون اندازه که
/// دیده میشه کوچیک و بزرگ بشن.»
///
/// خطِ یک ستون با ماوس گرفته می‌شود و گام‌به‌گام پهن و بعد باریک می‌شود؛ <b>پس از
/// هر گام، پیش از رها کردن</b>، جمعِ ستون‌ها باید همان قابِ جدول باشد.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- coldrag
/// </summary>
internal static class ColDragProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-coldrag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Seed.Fill(AppHost.Current);
        Settle(win);

        Console.WriteLine("════ مصارف ════");
        var ex = vm.Sections.First(x => x.Id == "expenses");
        Wait(win, vm.GoAsync(ex));
        foreach (var g in Grids(win).Take(1)) DragAll(win, g, "مصارف");

        Console.WriteLine("════ ورقِ امروز · جدولِ ردیف‌ها ════");
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(wq));
        var w = AppHost.Current.WaraqData.OpenOrCreateAsync(Shamsi.Today(), "").GetAwaiter().GetResult();
        Wait(win, wq.ReloadAsync());
        var sheet = wq.Sheets.FirstOrDefault(x => x.Id == w.Id);
        if (sheet is null) { Check("ورقِ امروز در فهرست", false); }
        else
        {
            wq.OpenCommand.Execute(sheet);
            Settle(win);
            var i = 0;
            foreach (var g in Grids(win).ToList()) DragAll(win, g, $"ورق · جدولِ {++i}");
        }

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ کشیدنِ خطِ ستون داخلِ قاب می‌ماند — نه جای خالی، نه بیرون‌زده"
                                    : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void DragAll(Window win, DataGrid g, string what)
    {
        var (u0, r0) = Used(g);
        Console.WriteLine($"    {what}: پیش از کشیدن {u0:0} از {r0:0}px · ستون‌ها "
            + string.Join(",", g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).Select(c => $"{c.Header}:{c.ActualWidth:0}{c.Width.UnitType.ToString()[0]}{(c.IsVisible?"":"H")}"))
            + $" · rowhdr {g.HeadersVisibility} {g.RowHeaderWidth}");
        var cw = typeof(DataGrid).GetProperty("CellsWidth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(g);
        var vbar = g.GetVisualDescendants().OfType<ScrollBar>().Where(b => b.Orientation == Orientation.Vertical).Select(b => $"{b.IsVisible}/{b.Bounds.Width:0}");
        var rp = g.GetVisualDescendants().OfType<DataGridRowsPresenter>().FirstOrDefault()?.Bounds.Width;
        var hidden = string.Join(",", g.Columns.Where(c => !c.IsVisible).Select(c => c.Header?.ToString()));
        Console.WriteLine($"      cellsWidth {cw} · vbar [{string.Join(";", vbar)}] · rowsPresenter {rp:0} · hidden[{hidden}] · classes {string.Join(" ", g.Classes)}");
        var heads = g.GetVisualDescendants().OfType<DataGridColumnHeader>()
                     .Where(h => h.IsEffectivelyVisible && h.Bounds.Width > 20 && h.Content is string { Length: > 0 })
                     .OrderBy(h => h.TranslatePoint(default, g)!.Value.X).ToList();
        //  دو ستون: یکی پهن، یکی باریک — هر دو جهت
        foreach (var h in heads.Take(heads.Count - 1).Where((_, k) => k % 2 == 0).Take(2))
        {
            var name = h.Content as string ?? "?";
            Drag(win, g, h, +160, $"{what} · «{name}» پهن‌تر");
            Drag(win, g, h, -120, $"{what} · «{name}» باریک‌تر");
        }
    }

    private static void Drag(Window win, DataGrid g, DataGridColumnHeader h, double dx, string what)
    {
        var y = h.Bounds.Height / 2;
        var col = h.GetType().GetProperty("OwningColumn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(h) as DataGridColumn;
        var before = col?.ActualWidth ?? double.NaN;
        var (u0, r0) = Used(g);
        var start = h.TranslatePoint(new Point(h.Bounds.Width - 2, y), win)!.Value;
        win.MouseMove(start, RawInputModifiers.None);
        win.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        Pump(win);
        var worstOut = 0.0; var worstGap = 0.0;
        const int steps = 8;
        //  جهتِ «پهن‌تر» در مختصاتِ پنجره — یک بار، پیش از آن‌که سرستون جابه‌جا شود
        var dir = h.TranslatePoint(new Point(h.Bounds.Width - 1, y), win)!.Value.X
                - h.TranslatePoint(new Point(h.Bounds.Width - 2, y), win)!.Value.X;
        for (var k = 1; k <= steps; k++)
        {
            var q = new Point(start.X + dir * dx * k / steps, start.Y);
            win.MouseMove(q, RawInputModifiers.LeftMouseButton);
            Pump(win);
            var (used, room) = Used(g);
            worstOut = Math.Max(worstOut, used - room);
            worstGap = Math.Max(worstGap, room - used);
        }
        var end = new Point(start.X + dir * dx, start.Y);
        win.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
        Settle(win);
        var (u2, r2) = Used(g);
        var bar = g.GetVisualDescendants().OfType<ScrollBar>()
                   .FirstOrDefault(b => b.Orientation == Orientation.Horizontal && b.IsEffectivelyVisible && b.Maximum > 0.5);
        var after = col?.ActualWidth ?? double.NaN;
        Check($"{what}: کشیدن گرفت (ستون {before:0} ⇒ {after:0}px)", Math.Abs(after - before) >= 20 || Math.Abs(u0 - r0) > 6);
        Check($"{what}: وسطِ کشیدن هیچ ستونی بیرون نزد", worstOut <= 3, $"بیشترین بیرون‌زدگی {worstOut:0}px");
        Check($"{what}: وسطِ کشیدن جای خالی نماند", worstGap <= 6, $"بیشترین جای خالی {worstGap:0}px");
        Check($"{what}: پس از رها کردن داخلِ قاب و بی نوارِ افقی",
              bar is null && Math.Abs(u2 - r2) <= 6, $"ستون‌ها {u2:0} از {r2:0}px");
    }

    private static (double Used, double Room) Used(DataGrid g)
    {
        var cols = g.Columns.Where(c => c.IsVisible).ToList();
        //  جدولِ بی‌ردیف سرستونِ ردیف را نمی‌کشد، ولی جایش همان است
        var rh = g.HeadersVisibility.HasFlag(DataGridHeadersVisibility.Row) && !double.IsNaN(g.RowHeaderWidth)
            ? g.RowHeaderWidth
            : g.GetVisualDescendants().OfType<DataGridRowHeader>().FirstOrDefault(h => h.IsEffectivelyVisible)?.Bounds.Width ?? 0;
        return (cols.Sum(c => c.ActualWidth) + rh, g.Bounds.Width - 4);
    }

    private static IEnumerable<DataGrid> Grids(Window win) =>
        win.GetVisualDescendants().OfType<DataGrid>()
           .Where(g => g.IsEffectivelyVisible && g.Bounds.Width > 200 && g.Columns.Count(c => c.IsVisible) >= 3);

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
