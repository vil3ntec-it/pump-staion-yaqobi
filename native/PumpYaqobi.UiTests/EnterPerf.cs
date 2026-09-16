using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «رفتن داخلِ حساب و بیرون آمدن، هر دو کند است» ═══════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «داخل رفتنِ حسابِ قرض‌داران، شرکت‌ها و ورق‌ها
/// هم کندی دیدم و بازگشت یا بیرون شدن از حساب هم همین‌طور کند بود.»
///
/// این‌جا هر شش کار با دادهٔ پنج‌ساله سنجیده می‌شود — **و برگشت هم مثلِ رفتن
/// شمرده می‌شود**، چون تا امروز هیچ سنجه‌ای «بستن» را نمی‌سنجید:
///
///     قرض‌دار: باز ⇄ بسته · شرکت: باز ⇄ بسته · ورق: باز ⇄ بسته
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- enterperf
/// </summary>
internal static class EnterPerf
{
    /// <summary>هدفِ صاحب ریپو برای هر کارِ کاربر.</summary>
    private const long Goal = 400;

    private static readonly List<(string What, long Ms)> Rows = new();

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-enter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");
        YearsAudit.Seed(file);

        AppHost.Start(file);
        var host = AppHost.Current;
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        var vm = (MainViewModel)win.DataContext!;
        win.Show(); Pump(win);
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        Settle(win);

        Console.WriteLine();
        Console.WriteLine("کار                                    بارِ اول   میانهٔ سه بار   دستورِ دیتابیس");
        Console.WriteLine(new string('-', 80));

        // ══ قرض‌داران ═════════════════════════════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is DebtSectionViewModel debt)
        {
            Wait(win, vm.GoAsync(debt)); Settle(win);
            if (Environment.GetEnvironmentVariable("PUMP_ENTER_DEBUG") == "1" && debt.Cards.Count >= 7)
                Split(win, debt);
            Measure(win, "حسابِ قرض‌دار — باز کردن", () => Wait(win, debt.OpenByNumberAsync(7)),
                    () => { debt.PersonOpen = false; Settle(win); });
            Wait(win, debt.OpenByNumberAsync(7)); Settle(win);
            Measure(win, "حسابِ قرض‌دار — بستن", () => { debt.PersonOpen = false; Settle(win); },
                    () => { Wait(win, debt.OpenByNumberAsync(7)); Settle(win); });
            debt.PersonOpen = false; Settle(win);
        }

        // ══ شرکت‌ها ════════════════════════════════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "noinv") is CompanySectionViewModel co)
        {
            Wait(win, vm.GoAsync(co)); Settle(win);
            var first = co.Cards.FirstOrDefault();
            if (first is not null)
            {
                Measure(win, "حسابِ شرکت — باز کردن", () => { co.OpenCommand.Execute(first); Settle(win); },
                        () => { co.BackCommand.Execute(null); Settle(win); });
                co.OpenCommand.Execute(first); Settle(win);
                Measure(win, "حسابِ شرکت — بستن", () => { co.BackCommand.Execute(null); Settle(win); },
                        () => { co.OpenCommand.Execute(first); Settle(win); });
                co.BackCommand.Execute(null); Settle(win);
            }
        }

        // ══ ورق‌ها ═════════════════════════════════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is WaraqSectionViewModel wq)
        {
            Wait(win, vm.GoAsync(wq)); Wait(win, wq.ReloadAsync()); Settle(win);
            var sheet = wq.Sheets.FirstOrDefault();
            if (sheet is not null)
            {
                Measure(win, "ورقِ روزانه — باز کردن", () => { wq.OpenCommand.Execute(sheet); Settle(win); },
                        () => { wq.BackCommand.Execute(null); Settle(win); });
                wq.OpenCommand.Execute(sheet); Settle(win);
                Measure(win, "ورقِ روزانه — بستن", () => { wq.BackCommand.Execute(null); Settle(win); },
                        () => { wq.OpenCommand.Execute(sheet); Settle(win); });
                wq.BackCommand.Execute(null); Settle(win);
            }
        }

        Console.WriteLine();
        var bad = Rows.Where(r => r.Ms > Goal).ToList();
        if (bad.Count == 0) { Console.WriteLine($"✅ رفتن و برگشتن، هر دو زیرِ {Goal} ms"); return 0; }
        Console.WriteLine($"❌ این کارها از هدفِ {Goal} ms ردند:");
        foreach (var b in bad) Console.WriteLine($"   • {b.What}: {b.Ms:N0} ms");
        return 1;
    }

    /// <summary>
    /// ══ «کجای باز کردنِ حساب وقت می‌برد؟» ═══════════════════════════════════
    /// همان تقسیمی که ریشه را پیدا کرد: خواندنِ داده، ساختنِ ویومدل، و
    /// **چیدمان**. بارِ اول ۱۰ و ۷ و ۶۵۰ میلی‌ثانیه بود — یعنی هزینه نه در
    /// دیتابیس بود و نه در ویومدل، بلکه در ساختنِ دوبارهٔ کلِ صفحه.
    /// </summary>
    private static void Split(Window win, DebtSectionViewModel debt)
    {
        for (var i = 0; i < 3; i++)
        {
            debt.PersonOpen = false; Settle(win);
            var q = PumpYaqobi.Services.Data.DbWatch.Count;
            var work = Stopwatch.StartNew();
            var task = debt.OpenByNumberAsync(7);
            while (!task.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
            work.Stop();
            var lay = Stopwatch.StartNew(); Pump(win); lay.Stop();
            var live = win.GetVisualDescendants().OfType<DataGridRow>().Count();
            Console.WriteLine($"      داده و ویومدل: {work.ElapsedMilliseconds} ms · چیدمان: "
                + $"{lay.ElapsedMilliseconds} ms · {live} ردیفِ زنده · "
                + $"{PumpYaqobi.Services.Data.DbWatch.Count - q} دستور");
        }
        debt.PersonOpen = false; Settle(win);
    }

    /// <summary>یک کار را یک بارِ سرد و سه بارِ گرم می‌سنجد و میانه را می‌گیرد.</summary>
    private static void Measure(Window win, string what, Action act, Action undo)
    {
        var debug = Environment.GetEnvironmentVariable("PUMP_ENTER_DEBUG") == "1";
        if (debug) { PumpYaqobi.Services.Data.DbWatch.Recording = true; while (PumpYaqobi.Services.Data.DbWatch.Log.TryDequeue(out _)) { } }
        var q0 = PumpYaqobi.Services.Data.DbWatch.Count;
        var sw = Stopwatch.StartNew(); act(); sw.Stop();
        var cold = sw.ElapsedMilliseconds;
        var queries = PumpYaqobi.Services.Data.DbWatch.Count - q0;
        if (debug)
        {
            PumpYaqobi.Services.Data.DbWatch.Recording = false;
            // جدولِ اصلیِ هر دستور — تا معلوم شود این پرس‌وجوها مالِ کدام بخش‌اند
            foreach (var g in PumpYaqobi.Services.Data.DbWatch.Log
                                 .Select(x => Table(x)).GroupBy(x => x)
                                 .OrderByDescending(g => g.Count()).Take(8))
                Console.WriteLine($"      {g.Count(),4} × {g.Key}");
        }

        var runs = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            undo();
            var t = Stopwatch.StartNew(); act(); t.Stop();
            runs.Add(t.ElapsedMilliseconds);
        }
        runs.Sort();
        var med = runs[1];
        Console.WriteLine($"{what,-38} {cold,6:N0} ms {med,10:N0} ms {queries,12:N0}");
        Rows.Add((what, med));
    }

    /// <summary>نامِ جدولِ یک دستور — «FROM "X"» یا «UPDATE "X"».</summary>
    private static string Table(string sql)
    {
        var m = System.Text.RegularExpressions.Regex.Match(sql,
            "(?:FROM|UPDATE|INSERT INTO|DELETE FROM)\\s+\"([A-Za-z_]+)\"");
        return m.Success ? m.Groups[1].Value : sql.Split('\n')[0][..Math.Min(40, sql.Split('\n')[0].Length)];
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }

    private static void Settle(Window w)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 200 && sw.ElapsedMilliseconds < 20_000; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            if (w.IsMeasureValid && w.IsArrangeValid
                && !Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.Background)) break;
        }
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(2); }
        Pump(w);
    }
}
