using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ عکس‌گیرِ بی‌نمایشگر ═════════════════════════════════════════════════════
/// همان پنجرهٔ واقعیِ برنامه را با موتورِ رسمِ Skia می‌سازد و PNG می‌گیرد.
/// این‌طور هر بخشی که تحویل می‌دهیم، پیش از تحویل با چشم دیده شده است.
///
///     dotnet run --project PumpYaqobi.UiTests -- shots
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : "shots";

        // ══ حالتِ «سنجشِ اسکرول» ═════════════════════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- scroll
        //
        // گزارشِ صاحب ریپو: «جز جدول‌ها دیگر هیچ چیزی اسکرول نمی‌شود». این
        // حالت به‌جای حدس زدن، در همان پنجرهٔ واقعی و در کوچک‌ترین اندازهٔ
        // مجاز، بخش‌به‌بخش می‌سنجد که محتوا از پنجره بلندتر است یا نه و آیا
        // اصلاً راهی برای رسیدن به بخشِ بیرون‌افتاده هست.
        if (outDir.Equals("scroll", StringComparison.OrdinalIgnoreCase)) return ScrollAudit();

        Directory.CreateDirectory(outDir);

        // دیتابیسِ موقت — عکس‌گیری هرگز به دادهٔ واقعیِ کاربر دست نمی‌زند
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-shots-" + Guid.NewGuid().ToString("N"), "pump.db");
        PumpYaqobi.App.Services.AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);

        // ۱) صفحهٔ قفل — همان چیزی که کاربر اول می‌بیند
        Shot(win, Path.Combine(outDir, "00-lock.png"));

        // ۲) ورود با رمزِ نخستین اجرا، سپس هر تم یک عکس
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Wait(win, Task.CompletedTask);
        Pump(win);

        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        // داشبورد پیش از پر شدنِ دیتابیس ساخته شده بود — یک‌بار از نو بخواند
        if (vm.Sections.FirstOrDefault(s => s.Id == "dashboard")
            is PumpYaqobi.App.ViewModels.Sections.DashboardSectionViewModel dash)
            Wait(win, dash.RefreshAsync());

        // ۳) هر تم یک عکس از داشبورد
        foreach (var theme in PumpTheme.All)
        {
            ThemeManager.Apply(theme);
            Pump(win);
            Shot(win, Path.Combine(outDir, "theme-" + theme.Id + ".png"));
        }
        ThemeManager.Apply(PumpTheme.DarkAmber);

        // ۴) هر بخش یک عکس — چیزی تحویل نمی‌دهیم که ندیده باشیم
        var n = 0;
        foreach (var sec in vm.Sections)
        {
            Wait(win, vm.GoAsync(sec));
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, $"{++n:00}-{sec.Id}.png"));
        }

        // ۵) صفحهٔ حسابِ یک قرض‌دار — مهم‌ترین صفحهٔ برنامه
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is PumpYaqobi.App.ViewModels.Sections.DebtSectionViewModel debt)
        {
            Wait(win, vm.GoAsync(debt));
            Pump(win);
            Wait(win, debt.RefreshAsync());
            debt.OpenCommand.Execute(debt.Cards.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "20-debt-person.png"));
        }

        if (vm.Sections.FirstOrDefault(s => s.Id == "noinv") is PumpYaqobi.App.ViewModels.Sections.CompanySectionViewModel comp)
        {
            Wait(win, vm.GoAsync(comp));
            Pump(win);
            Wait(win, comp.RefreshAsync());
            comp.OpenCommand.Execute(comp.Cards.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "21-company-page.png"));
        }

        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is PumpYaqobi.App.ViewModels.Sections.WaraqSectionViewModel wq)
        {
            Wait(win, vm.GoAsync(wq));
            Pump(win);
            Wait(win, wq.ReloadAsync());
            wq.OpenCommand.Execute(wq.Sheets.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "22-waraq-page.png"));
        }

        if (vm.Sections.FirstOrDefault(s => s.Id == "shifts") is PumpYaqobi.App.ViewModels.Sections.ParchaSectionViewModel pr)
        {
            Wait(win, vm.GoAsync(pr));
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "23-parcha.png"));
        }

        if (vm.Sections.FirstOrDefault(s => s.Id == "amanat")
            is PumpYaqobi.App.ViewModels.Sections.AmanatSectionViewModel am)
        {
            Wait(win, vm.GoAsync(am));
            Pump(win);
            am.OpenCommand.Execute(am.Cards.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "24-amanat-account.png"));
        }

        var failures = CheckShortcuts(win, vm);

        if (Environment.GetEnvironmentVariable("PUMP_PROBE") == "1"
            && vm.Sections.FirstOrDefault(s => s.Id == "chakana") is { } ck)
        { Wait(win, vm.GoAsync(ck)); Pump(win); }

        // ابزارِ عیب‌یابی: پهنای واقعیِ ستون‌های جدولِ چکنه
        if (Environment.GetEnvironmentVariable("PUMP_PROBE") == "1")
        {
            foreach (var g in win.GetVisualDescendants().OfType<Avalonia.Controls.DataGrid>())
            {
                Console.WriteLine("— جدول —");
                foreach (var c in g.Columns)
                    Console.WriteLine($"   [{c.Header}] w={c.ActualWidth:0} type={c.GetType().Name}");
                break;
            }
        }

        Console.WriteLine("عکس‌ها در: " + Path.GetFullPath(outDir));
        return failures;
    }



    /// <summary>
    /// ══ آزمونِ میانبرها روی پنجرهٔ واقعی ═══════════════════════════════════
    /// آزمون‌های ‎PumpYaqobi.Tests‎ قرارداد را می‌سنجند (‎IRowBatchHost‎ و…)، ولی
    /// نه سیم‌کشی‌اش را: این‌که رویدادِ کلید اصلاً به ‎ShortcutService‎ می‌رسد،
    /// که بافرِ چندرقمی درست جمع می‌شود، و که کار دقیقاً هنگامِ <b>رها شدنِ</b>
    /// کلید انجام می‌گیرد. آن‌ها فقط با کلیدِ واقعی روی پنجرهٔ واقعی ثابت
    /// می‌شوند — و همین‌جا می‌شود.
    /// </summary>
    private static int CheckShortcuts(Window win, MainViewModel vm)
    {
        var bad = 0;
        void Check(string what, bool ok)
        {
            Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what);
            if (!ok) bad++;
        }

        Console.WriteLine("── میانبرهای صفحه‌کلید ──");

        // ── Ctrl+Shift+3 → بخشِ سوم (ورق‌های روزانه) ──
        win.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit3, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win);
        Check("Ctrl+Shift+3 → " + vm.Current?.Id, vm.Current?.Id == vm.Sections[2].Id);

        // ── عددِ دو رقمی: Ctrl+Shift+1 سپس 2 → بخشِ دوازدهم ──
        win.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit1, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit2, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win);
        Check("Ctrl+Shift+1,2 → بخشِ ۱۲ (" + vm.Current?.Id + ")", vm.Current?.Id == vm.Sections[11].Id);

        // ── «۰» یعنی دهمین بخش، نه صفرم ──
        win.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit0, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win);
        Check("Ctrl+Shift+0 → بخشِ ۱۰ (" + vm.Current?.Id + ")", vm.Current?.Id == vm.Sections[9].Id);

        // ── Ctrl+عدد روی یک بخشِ دفتری: ردیف افزوده شود ──
        var exp = vm.Sections.First(x => x.Id == "expenses");
        Wait(win, vm.GoAsync(exp));
        var table = vm.RowHost;
        if (table is null) { Check("جدولِ «مصارف» شناخته نشد", false); return bad; }

        var before = table.RowCount;
        win.KeyPressQwerty(PhysicalKey.Digit4, RawInputModifiers.Control);
        win.KeyReleaseQwerty(PhysicalKey.Digit4, RawInputModifiers.Control);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
        Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
        Check($"Ctrl+4 → ۴ ردیف افزوده شد ({before} → {table.RowCount})", table.RowCount == before + 4);

        // ── Shift+عدد: همان‌قدر برداشته شود ──
        var mid = table.RowCount;
        win.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit3, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
        Check($"Shift+3 → ۳ ردیف حذف شد ({mid} → {table.RowCount})", table.RowCount == mid - 3);

        // ── ردیفِ کافی نیست → هیچ کاری نکند ──
        var keep = table.RowCount;
        win.KeyPressQwerty(PhysicalKey.Digit9, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit9, RawInputModifiers.Shift);
        win.KeyPressQwerty(PhysicalKey.Digit9, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit9, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
        Check($"Shift+99 با ردیفِ ناکافی → دست‌نخورده ({keep})", table.RowCount == keep);

        // ── Alt+عدد: حسابِ شمارهٔ ۱ در قرض‌داران باز شود ──
        var debt = vm.Sections.First(x => x.Id == "debt");
        Wait(win, vm.GoAsync(debt));
        win.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.Alt);
        win.KeyReleaseQwerty(PhysicalKey.Digit1, RawInputModifiers.Alt);
        win.KeyReleaseQwerty(PhysicalKey.AltLeft, RawInputModifiers.None);
        Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
        Check("Alt+1 → حسابِ کارتِ ۱ باز شد", debt.ActivePage is not null);

        return bad;
    }

    /// <summary>
    /// انتظارِ «پمپ‌شونده». نخِ رابط کاربری همین نخ است، پس
    /// <c>GetAwaiter().GetResult()</c> رویِ کاری که ادامه‌اش را به همین نخ
    /// برمی‌گرداند قفل می‌کرد و عکس‌گیری وسطِ کار می‌خوابید. این‌جا به‌جای
    /// مسدود کردن، حلقهٔ رویداد چرخانده می‌شود تا کار واقعاً تمام شود.
    /// </summary>
    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Thread.Sleep(5);
        }
        if (!t.IsCompleted) { Console.WriteLine("  ⚠ کار در ۳۰ ثانیه تمام نشد"); return; }
        t.GetAwaiter().GetResult();          // خطا اگر بود، همین‌جا بالا بیاید
        Pump(w);
    }

    /// <summary>چند دورِ چیدمان/رسم تا صفحه واقعاً ساخته شود.</summary>
    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
        }
    }

    private static void Shot(Window w, string path)
    {
        using var frame = w.CaptureRenderedFrame();
        if (frame is null) { Console.WriteLine("  ✖ عکس گرفته نشد: " + path); return; }
        frame.Save(path);
        Console.WriteLine("  ✔ " + Path.GetFileName(path));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  سنجشِ اسکرول
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// برای هر بخش می‌گوید: محتوا چقدر بلند است، پنجره چقدر جا دارد، و اگر
    /// بلندتر است آیا ‎ScrollViewer‎ی هست که واقعاً بتواند به تهش برساند.
    ///
    /// پنجره عمداً در کوچک‌ترین اندازهٔ مجاز (‎MinWidth×MinHeight‎) باز می‌شود:
    /// در ۱۴۴۰×۹۰۰ بیشترِ بخش‌ها اصلاً سرریز نمی‌کنند و سنجش بی‌نتیجه می‌ماند.
    /// </summary>
    private static int ScrollAudit()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-scroll-" + Guid.NewGuid().ToString("N"), "pump.db");
        PumpYaqobi.App.Services.AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1000, Height = 640 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Wait(win, Task.CompletedTask);
        Pump(win);
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        Console.WriteLine();
        Console.WriteLine("بخش                  سرریز؟   بلندیِ محتوا / جا   اسکرول‌ویور  می‌رسد؟");
        Console.WriteLine(new string('-', 74));

        var broken = new List<string>();

        foreach (var sec in vm.Sections)
        {
            Wait(win, vm.GoAsync(sec));
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);

            // ریشهٔ محتوای همین بخش — همان ‎ContentControl‎ی که بخش داخلش است
            var host = win.GetVisualDescendants().OfType<ContentControl>()
                          .FirstOrDefault(c => ReferenceEquals(c.Content, sec));
            if (host is null) { Console.WriteLine($"{sec.Id,-20} — میزبان پیدا نشد"); continue; }

            var avail = host.Bounds.Height;

            // بلندیِ واقعیِ محتوا: بزرگ‌ترین ‎DesiredSize‎ی که زیرِ میزبان هست
            var wanted = host.GetVisualDescendants()
                             .Select(v => v is Layoutable l ? l.DesiredSize.Height : 0)
                             .DefaultIfEmpty(0).Max();

            var overflows = wanted > avail + 1;

            // آیا اسکرول‌ویوری هست که واقعاً چیزی برای لغزاندن دارد؟
            var svs = host.GetVisualDescendants().OfType<ScrollViewer>().ToList();
            var reachable = svs.Any(sv => sv.Extent.Height > sv.Viewport.Height + 1);

            // جدول خودش اسکرول دارد — آن را جدا می‌شماریم
            var hasGrid = host.GetVisualDescendants()
                              .Any(v => v.GetType().Name.Contains("DataGrid", StringComparison.Ordinal));

            var mark = !overflows ? "—" : reachable ? "✔" : "✖";
            Console.WriteLine($"{sec.Id,-20} {(overflows ? "بله" : "نه"),-8} "
                            + $"{wanted,6:0} / {avail,-6:0}      {svs.Count,-2} {(hasGrid ? "+جدول" : "     ")}  {mark}");

            if (overflows && !reachable) broken.Add(sec.Id);
        }

        Console.WriteLine();
        if (broken.Count == 0)
        {
            Console.WriteLine("✅ هر بخشی که سرریز می‌کند، راهی برای رسیدن به تهش دارد");
            return 0;
        }

        Console.WriteLine("❌ این بخش‌ها سرریز می‌کنند ولی اسکرول ندارند — محتوا بریده می‌شود:");
        foreach (var b in broken) Console.WriteLine("   • " + b);
        return 1;
    }
}
