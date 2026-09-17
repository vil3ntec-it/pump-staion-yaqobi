using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «جدول‌ها غیب شدن» — بازسازیِ گزارشِ صاحب ریپو با عکس ═════════════════════
/// صرافی با ۶۱ ردیف در ماهِ جاری، پنجرهٔ لپ‌تاپی. ردیف‌های ساخته‌شده، جای هر
/// ردیف نسبت به جدول، بلندیِ جدول، و آن‌چه پس از لغزاندنِ صفحه به ته می‌ماند.
///     dotnet run --project PumpYaqobi.UiTests -- vanish
/// </summary>
internal static class VanishProbe
{
    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-vanish-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        var host = AppHost.Current;

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1366, Height = 700 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234"; LockIn.Wait(vm.Lock);
        Wait(win, Task.CompletedTask);
        var month = Shamsi.ThisMonth();
        for (var i = 1; i <= 30; i++)
            host.ExchangeLedger.AddAsync(new ExchangeRow
            {
                DateShamsi = $"{month}/17", Description = i == 30 ? "hoioihhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh" : "",
                Amount = 0, Currency = ExchangeCurrency.Toman, Rate = 0, Bardagi = 0,
            }).GetAwaiter().GetResult();
        var sec = vm.Sections.First(s => s.Id == "sarrafi");
        Wait(win, vm.GoAsync(sec));
        for (var k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }

        var page = win.GetVisualDescendants().OfType<ScrollViewer>().First(v => v.Name == "PageScroll");
        var grid = win.GetVisualDescendants().OfType<PumpYaqobi.App.Controls.ExcelGrid>().First(g => g.IsEffectivelyVisible);
        Report("پس از باز شدن (۳۰ ردیف)", page, grid);
        // مثلِ صاحب ریپو: با «چندتایی» ردیف اضافه می‌کنیم تا جدول وسطِ کار بلند شود
        var ledger = (PumpYaqobi.App.ViewModels.IRowBatchHost)sec;
        for (var b = 0; b < 7; b++) { Wait(win, ledger.AddRowsAsync(5)); Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        for (var k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        Report("پس از افزودنِ ۳۵ ردیف", page, grid);

        // ── هزینه کجا می‌رود؟ ۲۰۰ ردیف: ویومدل / اولین چیدمان / رشد ──────────
        for (var i = 0; i < 200; i++)
            host.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = $"{month}/18", Description = "", Amount = 0, Currency = ExchangeCurrency.Toman }).GetAwaiter().GetResult();
        var ledger2 = (PumpYaqobi.App.ViewModels.IRowBatchHost)sec;
        var prop = sec.GetType().GetProperty("Month")!;
        prop.SetValue(sec, "1300/01");
        while (ledger2.RowCount != 0) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        for (var k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        prop.SetValue(sec, month);
        while (ledger2.RowCount != 265) { Dispatcher.UIThread.RunJobs(DispatcherPriority.Normal); Thread.Sleep(1); }
        var tVm = sw.ElapsedMilliseconds; sw.Restart();
        win.UpdateLayout();
        var tLayout = sw.ElapsedMilliseconds; sw.Restart();
        var lastH = -1d; var still = 0;
        var eg = (PumpYaqobi.App.Controls.ExcelGrid)grid;
        while (still < 4) { var s1 = System.Diagnostics.Stopwatch.StartNew(); Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); var h = grid.Bounds.Height; Console.WriteLine($"   پاس: shown={eg.DiagShown} {s1.ElapsedMilliseconds} ms"); if (Math.Abs(h - lastH) < .5) still++; else still = 0; lastH = h; }
        var tGrow = sw.ElapsedMilliseconds;
        Console.WriteLine($"۲۶۵ ردیف: ویومدل {tVm} ms · اولین چیدمان {tLayout} ms · رشدِ کامل {tGrow} ms · بلندی {grid.Bounds.Height:0}");
        Report("۲۶۵ ردیف", page, grid);

        // ── پیشنهادِ خودکار: خانهٔ «توضیحات» را ویرایش کن و ببین فهرست می‌آید ──
        var descCol = grid.Columns.First(c => (c.Header as string) == "توضیحات");
        grid.SelectedItem = grid.ItemsSource!.Cast<object>().Skip(3).First();
        grid.CurrentColumn = descCol;
        grid.BeginEdit();
        for (var k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        var editor = grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsFocused);
        Console.WriteLine($"پیشنهادِ خودکار: کادرِ ویرایش {(editor is null ? "نیست" : "هست")} · پیشنهادها {PumpYaqobi.App.Controls.Suggest.Showing}");
        if (editor is not null) { editor.Text = "hoi"; for (var k = 0; k < 3; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); } Console.WriteLine($"   بعد از تایپِ «hoi»: {PumpYaqobi.App.Controls.Suggest.Showing} پیشنهاد"); }
        grid.CancelEdit();

        // ── ماشین‌حساب: باز، بزرگ، عکس ──
        vm.Calculator.IsOpen = true;
        vm.Calculator.Key("1"); vm.Calculator.Key("2"); vm.Calculator.Key("+"); vm.Calculator.Key("7");
        for (var k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        using (var f1 = win.CaptureRenderedFrame()) f1?.Save(Path.Combine(Path.GetTempPath(), "calc-small.png"));
        vm.Calculator.Resize(160, 220);
        for (var k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        using (var f2 = win.CaptureRenderedFrame()) f2?.Save(Path.Combine(Path.GetTempPath(), "calc-big.png"));
        Console.WriteLine($"ماشین‌حساب: {vm.Calculator.Width:0}×{vm.Calculator.Height:0} · قلمِ کلید {vm.Calculator.KeyFont:0.#} · عکس‌ها در {Path.GetTempPath()}calc-*.png");
        vm.Calculator.IsOpen = false;

        var max = page.Extent.Height - page.Viewport.Height;
        for (var step = 0; step <= 4; step++)
        {
            page.Offset = new Vector(0, max * step / 4.0);
            for (var k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
            Report($"اسکرول {step * 25}%", page, grid);
        }
        return 0;
    }

    private static void Report(string title, ScrollViewer page, DataGrid grid)
    {
        var rows = grid.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsVisible && r.Bounds.Height > 0)
                       .Select(r => (idx: r.Index, y: r.TranslatePoint(new Point(0, 0), grid)?.Y ?? double.NaN, h: r.Bounds.Height))
                       .OrderBy(r => r.idx).ToList();
        var totals = grid.GetVisualAncestors().OfType<Panel>().First().Bounds.Height;
        Console.WriteLine($"── {title}: صفحه {page.Offset.Y:0}/{page.Extent.Height:0} (قاب {page.Viewport.Height:0}) · جدول بلندی {grid.Bounds.Height:0}، خواسته {grid.DesiredSize.Height:0} · ردیفِ زنده {rows.Count}");
        if (rows.Count > 0)
            Console.WriteLine($"   ردیف‌ها {rows.First().idx}…{rows.Last().idx} · y از {rows.Min(r => r.y):0} تا {rows.Max(r => r.y + r.h):0} · بلندیِ ردیف {rows.Min(r => r.h):0}…{rows.Max(r => r.h):0}");
        var bar = grid.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().FirstOrDefault(b => b.Orientation == Avalonia.Layout.Orientation.Vertical);
        if (bar is not null) Console.WriteLine($"   نوارِ عمودیِ جدول: پیدا={bar.IsVisible} Value={bar.Value:0} Max={bar.Maximum:0} · pad={((PumpYaqobi.App.Controls.ExcelGrid)grid).DiagPad:0} shown={((PumpYaqobi.App.Controls.ExcelGrid)grid).DiagShown} queued={((PumpYaqobi.App.Controls.ExcelGrid)grid).DiagQueued} measures={PumpYaqobi.App.Controls.ExcelGrid.DiagMeasure}");
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        t.GetAwaiter().GetResult(); Pump(w);
    }
    private static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
