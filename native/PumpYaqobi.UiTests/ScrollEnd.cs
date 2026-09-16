using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «وقتی اسکرول به آخر می‌رسد متوقف نمی‌شود» ═══════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «یک باگ هم بینِ اسکرول کردن هست — هم راست و
/// چپ و هم پایین و بالا: وقتی به آخر می‌رسند متوقف نمی‌شود، یک لگِ کوچک
/// می‌زند و پرپر می‌شود، بعد دوباره درست می‌شود.»
///
/// «پرپر شدن» را می‌شود شمرد: صفحه که به ته رسید، **هیچ‌چیز نباید تکان
/// بخورد**. پس ته صفحه می‌ایستیم و چند فریم فقط نگاه می‌کنیم:
///
///   • بلندیِ کلِ صفحه (‎Extent.Height‎) عوض می‌شود؟  ⇒ جدول در حالِ رشد است
///     و جای اسکرول زیرِ پای کاربر جابه‌جا می‌شود — همان پرشِ کوچک.
///   • جای خودِ اسکرول (‎Offset.Y‎) خودش می‌جنبد؟
///   • نوارِ لغزشِ افقیِ جدول ظاهر و ناپدید می‌شود؟ ⇒ همان پرپرِ راست و چپ.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- scrollend
/// </summary>
internal static class ScrollEnd
{
    /// <summary>چند فریم پس از رسیدن به ته، همه‌چیز باید ثابت بماند.</summary>
    private const int Watch = 24;

    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-scrollend-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");
        YearsAudit.Seed(file);

        AppHost.Start(file);
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1280, Height = 800 };
        var vm = (MainViewModel)win.DataContext!;
        win.Show(); Pump(win);
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        Settle(win);

        Console.WriteLine();
        foreach (var id in new[] { "safe", "sarrafi", "expenses" })
            if (vm.Sections.FirstOrDefault(s => s.Id == id) is { } sec)
            {
                Wait(win, vm.GoAsync(sec)); Settle(win);
                Console.WriteLine($"── بخشِ «{sec.Title}» ──");
                AtTheEnd(win, sec.Title);
            }

        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is DebtSectionViewModel debt)
        {
            Wait(win, vm.GoAsync(debt)); Settle(win);
            Wait(win, debt.OpenByNumberAsync(7)); Settle(win);
            Console.WriteLine("── حسابِ یک قرض‌دار ──");
            AtTheEnd(win, "حسابِ قرض‌دار");
            debt.PersonOpen = false; Settle(win);
        }

        Console.WriteLine();
        if (_bad == 0) { Console.WriteLine("✅ ته صفحه و ته جدول، همه‌چیز می‌ایستد"); return 0; }
        Console.WriteLine($"❌ {_bad} ایراد");
        return 1;
    }

    /// <summary>صفحه را تا ته می‌برد و بعد فقط نگاه می‌کند.</summary>
    private static void AtTheEnd(Window win, string what)
    {
        var page = win.GetVisualDescendants().OfType<ScrollViewer>()
                      .FirstOrDefault(v => v.Name == "PageScroll");
        if (page is null) { Check("PageScroll پیدا شد", false); return; }

        // ── تا ته، چرخ‌به‌چرخ (رشدِ تدریجی هم همین‌طور دنبال می‌شود) ──
        var guard = 0;
        while (guard++ < 400)
        {
            var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);
            if (page.Offset.Y >= max - 0.5) break;
            page.Offset = new Vector(page.Offset.X, Math.Min(max, page.Offset.Y + 160));
            win.UpdateLayout(); Dispatcher.UIThread.RunJobs(); win.UpdateLayout();
        }
        Settle(win);

        // ── حالا چند فریم فقط نگاه می‌کنیم؛ چرخ را هم به ته می‌زنیم ──
        var h0 = Math.Round(page.Extent.Height);
        var y0 = Math.Round(page.Offset.Y);
        double hMin = h0, hMax = h0, yMin = y0, yMax = y0;
        var barFlips = 0;
        var barWas = Bars(win);

        for (var i = 0; i < Watch; i++)
        {
            // کاربر که به ته رسیده باز هم چرخ می‌زند — این نباید کاری بکند
            var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);
            page.Offset = new Vector(page.Offset.X, Math.Min(max, page.Offset.Y + 160));
            win.UpdateLayout(); Dispatcher.UIThread.RunJobs(); win.UpdateLayout();

            hMin = Math.Min(hMin, page.Extent.Height); hMax = Math.Max(hMax, page.Extent.Height);
            yMin = Math.Min(yMin, page.Offset.Y); yMax = Math.Max(yMax, page.Offset.Y);
            var now = Bars(win);
            if (now != barWas) { barFlips++; barWas = now; }
        }

        Check($"{what}: بلندیِ صفحه ته اسکرول ثابت ماند",
              hMax - hMin < 1, $"{hMin:0} تا {hMax:0} پیکسل");
        Check($"{what}: جای اسکرول خودش نجنبید",
              yMax - yMin < 1, $"{yMin:0} تا {yMax:0} پیکسل");
        Check($"{what}: نوارِ لغزشِ افقیِ جدول ظاهر و ناپدید نشد",
              barFlips == 0, barFlips + " بار");

        Sideways(win, what);
        RealWheel(win, what, page);
    }

    /// <summary>
    /// ══ همان کار، ولی با **چرخِ واقعیِ ماوس** ═══════════════════════════════
    ///
    /// و همین بود که باگ را گرفت: با ‎Offset‎ی دستی همه‌چیز آرام بود، ولی
    /// چرخِ واقعی از ‎ExcelGrid.OnPointerWheelChanged‎ رد می‌شود — و آن‌جا،
    /// وقتی صفحه دیگر جا نداشت، چرخ به ‎base‎ می‌رسید و **خودِ ‎DataGrid‎**
    /// ردیف‌هایش را می‌لغزاند. در حالتِ چسبان جای ردیف‌ها را فقط ‎SyncSticky‎
    /// می‌نویسد، پس یک فریم ردیف‌ها می‌پریدند و فریمِ بعد برمی‌گشتند سرِ
    /// جایشان — همان «پرپر می‌شود و بعد درست می‌شود»ی گزارشِ صاحب ریپو.
    ///
    /// سنجهٔ ساختاری: ته صفحه، شمارهٔ **نخستین ردیفِ دیده‌شده** نباید عوض شود.
    /// </summary>
    private static void RealWheel(Window win, string what, ScrollViewer page)
    {
        var mid = new Point(win.Width / 2, win.Height / 2);
        var first = FirstRow(win);
        var y0 = page.Offset.Y;

        for (var i = 0; i < 6; i++)
        {
            win.MouseWheel(mid, new Vector(0, -3));   // باز هم پایین، با آن‌که ته است
            win.UpdateLayout(); Dispatcher.UIThread.RunJobs(); win.UpdateLayout();
        }

        Check($"{what}: چرخِ ماوس ته صفحه ردیف‌ها را جابه‌جا نکرد",
              FirstRow(win) == first, $"ردیفِ اول {first} ⇒ {FirstRow(win)}");
        Check($"{what}: و خودِ صفحه هم ته ماند",
              Math.Abs(page.Offset.Y - y0) < 1, $"{y0:0} ⇒ {page.Offset.Y:0}");

        // ── و همان داستان در سرِ صفحه ──
        page.Offset = new Vector(page.Offset.X, 0);
        win.UpdateLayout(); Dispatcher.UIThread.RunJobs(); win.UpdateLayout();
        Settle(win);
        var top = FirstRow(win);
        for (var i = 0; i < 6; i++)
        {
            win.MouseWheel(mid, new Vector(0, 3));    // باز هم بالا، با آن‌که سرِ صفحه است
            win.UpdateLayout(); Dispatcher.UIThread.RunJobs(); win.UpdateLayout();
        }
        Check($"{what}: چرخِ ماوس سرِ صفحه هم چیزی را جابه‌جا نکرد",
              FirstRow(win) == top && page.Offset.Y < 1,
              $"ردیفِ اول {top} ⇒ {FirstRow(win)} · آفست {page.Offset.Y:0}");
    }

    /// <summary>شمارهٔ نخستین ردیفِ ساخته‌شدهٔ بلندترین جدولِ صفحه.</summary>
    private static int FirstRow(Window win)
    {
        var rows = win.GetVisualDescendants().OfType<DataGridRow>()
                      .Where(r => r.IsEffectivelyVisible).ToList();
        return rows.Count == 0 ? -1 : rows.Min(r => r.Index);
    }

    /// <summary>
    /// همان کار، ولی **راست و چپ**: جدولی که پهن‌تر از قاب است تا ته لغزانده
    /// می‌شود و بعد چند فریم نگاه می‌کنیم. «به آخر که رسید نباید بجنبد.»
    /// </summary>
    private static void Sideways(Window win, string what)
    {
        var wide = win.GetVisualDescendants().OfType<ScrollViewer>()
                      .Where(v => v.Name != "PageScroll" && v.IsEffectivelyVisible
                               && v.Extent.Width - v.Viewport.Width > 4)
                      .OrderByDescending(v => v.Extent.Width - v.Viewport.Width)
                      .FirstOrDefault();
        if (wide is null) { Console.WriteLine($"  ⋯ {what}: جدولی پهن‌تر از قاب نیست (لغزشِ افقی ندارد)"); return; }

        var guard = 0;
        while (guard++ < 400)
        {
            var max = Math.Max(0, wide.Extent.Width - wide.Viewport.Width);
            if (wide.Offset.X >= max - 0.5) break;
            wide.Offset = new Vector(Math.Min(max, wide.Offset.X + 120), wide.Offset.Y);
            win.UpdateLayout(); Dispatcher.UIThread.RunJobs(); win.UpdateLayout();
        }
        Settle(win);

        double wMin = wide.Extent.Width, wMax = wMin, xMin = wide.Offset.X, xMax = xMin;
        for (var i = 0; i < Watch; i++)
        {
            var max = Math.Max(0, wide.Extent.Width - wide.Viewport.Width);
            wide.Offset = new Vector(Math.Min(max, wide.Offset.X + 120), wide.Offset.Y);
            win.UpdateLayout(); Dispatcher.UIThread.RunJobs(); win.UpdateLayout();
            wMin = Math.Min(wMin, wide.Extent.Width); wMax = Math.Max(wMax, wide.Extent.Width);
            xMin = Math.Min(xMin, wide.Offset.X); xMax = Math.Max(xMax, wide.Offset.X);
        }

        Check($"{what}: پهنای جدول ته لغزشِ افقی ثابت ماند", wMax - wMin < 1, $"{wMin:0} تا {wMax:0}");
        Check($"{what}: لغزشِ افقی خودش نجنبید", xMax - xMin < 1, $"{xMin:0} تا {xMax:0}");
    }

    /// <summary>شمارِ نوارهای لغزشِ افقیِ دیده‌شده — «پرپرِ راست و چپ».</summary>
    private static int Bars(Window win) =>
        win.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>()
           .Count(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal && b.IsVisible);

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }

    private static void Settle(Window w)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 200 && sw.ElapsedMilliseconds < 20_000; i++)
        {
            Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Dispatcher.UIThread.RunJobs();
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
