using System.Diagnostics;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «برنامه چرا دیر باز می‌شود؟» — اجرای سرد، مرحله به مرحله ═══════════════════
///     dotnet run --project PumpYaqobi.UiTests -- startup
///
/// گزارشِ صاحب ریپو: «برنامه همین الان هم خیلی کند است… ببین چرا برنامه‌های
/// دیگر خیلی زود باز می‌شوند.» پس نه پنج سال داده، بلکه دیتابیسِ معمولی، و
/// دقیقاً همان مسیری که کاربر می‌رود: میزبان ← پنجره ← پرده (هر صفحه جدا) ←
/// رمز ← بخشِ آغازین ← گذرِ دومِ داده. هر مرحله با عدد.
/// </summary>
internal static class StartupAudit
{
    public static int Run()
    {
        var total = Stopwatch.StartNew();
        var dir = Path.Combine(Path.GetTempPath(), "pump-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");

        Console.WriteLine("مرحله                                            زمان");
        Console.WriteLine(new string('-', 58));

        Mark("میزبان (دیتابیس + سرویس‌ها)", () => AppHost.Start(file));
        var host = AppHost.Current;
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        // ⚠️ دیتابیسِ خالی عمداً: کندیِ باز شدن مالِ خودِ برنامه است، نه داده.
        // با پنج سال داده همین را ‎years‎ می‌سنجد.

        Mark("راه‌اندازیِ آوالونیا", () =>
            AppBuilder.Configure<PumpYaqobi.App.App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting());

        MainWindow win = null!;
        Mark("ساختِ پنجره (بیست ویومدل + XAML)", () => win = new MainWindow { Width = 1440, Height = 900 });
        var vm = (MainViewModel)win.DataContext!;
        Mark("نمایش و اولین چیدمان", () => { win.Show(); Dispatcher.UIThread.RunJobs(DispatcherPriority.Render); win.UpdateLayout(); });

        // ══ پرده تا صفحهٔ رمز ═══════════════════════════════════════════════
        // ⚠️ با رویداد سنجیده می‌شود، نه با حلقه: ‎RunJobs()‎ همهٔ کارهای صف را
        // یک‌جا می‌دواند (از جمله گرم کردنِ پشتِ قفل که با ‎Background‎ صف
        // می‌شود)، پس لحظهٔ «قفل آمد» را فقط خودِ رویداد درست می‌گوید.
        var pages = vm.AllPages;
        var sw = Stopwatch.StartNew();
        long lockAt = -1; var warmedAtLock = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Phase) && vm.Phase == MainViewModel.AppPhase.Locked && lockAt < 0)
            { lockAt = sw.ElapsedMilliseconds; warmedAtLock = vm.Warm.Warmed; }
        };
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(1); }
        Console.WriteLine($"{"پرده تا صفحهٔ رمز",-48}{lockAt,6:N0} ms   ({warmedAtLock} صفحه گرم: بخشِ آغازین)");
        Marks.Add(("پرده تا صفحهٔ رمز", lockAt));

        Mark("بقیهٔ صفحه‌ها پشتِ صفحهٔ قفل (کاربر رمز می‌زند)", () =>
        {
            var t = DateTime.UtcNow + TimeSpan.FromSeconds(120);
            while (DateTime.UtcNow < t && vm.Warm.Warmed < pages.Count)
            { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(1); }
            Console.WriteLine($"   ({vm.Warm.Warmed} صفحه گرم)");
        });

        Mark("رمز ⇒ بخشِ آغازین جلوی چشم", () =>
        {
            vm.Lock.Password = "1234";
            vm.Lock.SubmitCommand.Execute(null);
            var t = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < t)
            {
                Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
                win.UpdateLayout();
                if (vm.Phase == MainViewModel.AppPhase.Ready && vm.Current is { IsLoaded: true }) break;
                Thread.Sleep(1);
            }
        });

        Mark("پس از ورود تا آرام شدنِ صفِ کارها", () =>
        {
            var t = DateTime.UtcNow + TimeSpan.FromSeconds(20);
            while (DateTime.UtcNow < t)
            {
                Dispatcher.UIThread.RunJobs();
                win.UpdateLayout();
                if (!Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.Background)) { Thread.Sleep(50); Dispatcher.UIThread.RunJobs(); break; }
                Thread.Sleep(1);
            }
        });

        // ══ بی‌کاری: برنامه که کاری ندارد، چقدر کار می‌کند؟ ═══════════════════
        // «کامپیوترم می‌خواهد بسوزد»: انیمیشنِ بی‌پایان، حلقهٔ چیدمان یا تایمرِ
        // پرکار همین‌جا خودش را نشان می‌دهد — شمارِ چیدمان‌ها و CPU در سه ثانیه.
        // دو پنجرهٔ سه‌ثانیه‌ای: یکی همان اولِ ورود (JITِ پس‌زمینه هنوز کار
        // می‌کند) و یکی ده ثانیه بعد — عددِ دومی «بی‌کاریِ واقعی» است.
        for (var round = 1; round <= 2; round++)
        {
            if (round == 2)
            {
                var pause = Stopwatch.StartNew();
                while (pause.ElapsedMilliseconds < 10_000) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(16); }
            }
            var layouts = 0;
            EventHandler h = (_, _) => layouts++;
            win.LayoutUpdated += h;
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            proc.Refresh();
            var cpu0 = proc.TotalProcessorTime;
            var t = Stopwatch.StartNew();
            while (t.ElapsedMilliseconds < 3000)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(16);   // مثلِ یک فریمِ واقعی
            }
            proc.Refresh();
            var cpu = (proc.TotalProcessorTime - cpu0).TotalMilliseconds;
            win.LayoutUpdated -= h;
            var tag = round == 1 ? "بلافاصله پس از ورود" : "ده ثانیه بعد";
            Console.WriteLine($"{"بی‌کاری ۳ ثانیه (" + tag + ") — چیدمان",-48}{layouts,6:N0} بار");
            Console.WriteLine($"{"بی‌کاری ۳ ثانیه (" + tag + ") — CPU",-48}{cpu,6:N0} ms   ({cpu / 30:0.0}٪ یک هسته)");
            if (round == 2)
            {
                Marks.Add(("بی‌کاری — CPU (ms در ۳ ثانیه)", (long)cpu));
                if (cpu > 300) Console.WriteLine("   ⚠️ برنامهٔ بی‌کار نباید بیش از ۱۰٪ یک هسته بخورد");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"کل تا آرام شدن: {total.ElapsedMilliseconds:N0} ms");
        var toLock = Marks.Where(m => m.What != "پس از ورود تا آرام شدنِ صفِ کارها" && m.What != "رمز ⇒ بخشِ آغازین جلوی چشم" && m.What != "بقیهٔ صفحه‌ها پشتِ صفحهٔ قفل (کاربر رمز می‌زند)").Sum(m => m.Ms);
        Console.WriteLine($"تا صفحهٔ رمز (آنچه کاربر منتظرش می‌ماند): {toLock:N0} ms");
        return 0;
    }

    private static readonly List<(string What, long Ms)> Marks = new();

    private static void Mark(string what, Action run)
    {
        var sw = Stopwatch.StartNew();
        try { run(); }
        catch (Exception e) { Console.WriteLine($"{what,-48}  ✖ {e.GetType().Name}: {e.Message}"); return; }
        Marks.Add((what, sw.ElapsedMilliseconds));
        Console.WriteLine($"{what,-48}{sw.ElapsedMilliseconds,6:N0} ms");
    }
}
