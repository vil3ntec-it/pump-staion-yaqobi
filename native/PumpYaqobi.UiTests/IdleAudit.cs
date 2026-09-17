using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «بخشی که تویش نیستم هیچ مصرفی نداشته باشد — حتی یک درصد» ════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «اگر توی بخش نیستم، آن بخش فعال نباشد و
/// هیچ مصرفی نداشته باشد… من مثلاً توی بخشِ قرض‌دارانم و نمی‌خواهم ده تا یا هجده
/// تا بخشِ دیگر کار کنند یا بخواهند داده‌ای مصرف کنند… توی یک حساب یا حسابِ شرکت
/// یا ورقی هستم، نباید ورق‌های دیگر، حساب‌های دیگر و شرکت‌های دیگر داده‌ای مصرف
/// کنند… آن‌هایی که منطق دارند و از یک بخش به بخشِ دیگر اطلاعات می‌رود، دست نزن.»
///
/// «مصرف» را با نگاه کردن به کد نمی‌شود ثابت کرد، پس این‌جا سه چیزِ واقعی
/// شمرده می‌شود، با دادهٔ پنج‌ساله و در پنجرهٔ واقعی:
///
///   ۱) <b>دستورِ دیتابیس هنگامِ بی‌کاری</b> — سه ثانیه در یک بخش می‌نشینیم و
///      <see cref="DbWatch"/> می‌گوید چند دستور به SQLite رفت. هدف: صفر.
///   ۲) <b>ردیفِ زندهٔ بخش‌های پنهان</b> — چند ‎DataGridRow‎ در درختِ بصری هست که
///      مالِ بخشی است که دیده نمی‌شود. هدف: صفر.
///   ۳) <b>چیدمانِ بخش‌های پنهان</b> — کنترل‌های نامرئی نباید در پاسِ چیدمان
///      شرکت کنند (آوالونیا خودش رد می‌کند؛ این‌جا فقط اثبات می‌شود).
///
/// و یک سنجشِ چهارم برای «داخلِ بخش»: باز کردنِ حسابِ یک قرض‌دار نباید ردیف‌های
/// حساب‌های دیگر را بخواند.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- idle
/// </summary>
internal static class IdleAudit
{
    /// <summary>چند ثانیه در هر بخش بی‌کار می‌نشینیم.</summary>
    private static readonly TimeSpan Sit = TimeSpan.FromSeconds(3);

    /// <summary>
    /// بیشترین دستورِ دیتابیسِ پذیرفتنی در سه ثانیه بی‌کاری.
    /// صفر است و صفر باید بماند: بخشِ باز هم وقتی کاربر دست نمی‌زند کاری ندارد.
    /// </summary>
    private const int IdleQueryBudget = 0;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-idle-" + Guid.NewGuid().ToString("N"));
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
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        LockIn.Wait(vm.Lock);
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        Settle(win);

        Console.WriteLine();
        Console.WriteLine("بخش               باز شدن(دستور)   بی‌کاری(دستور)   ردیفِ زندهٔ بخش‌های دیگر   کنترلِ نامرئیِ چیده‌شده");
        Console.WriteLine(new string('-', 104));

        var bad = new List<string>();

        foreach (var sec in vm.Sections.ToList())
        {
            var before = DbWatch.Count;
            Wait(win, vm.GoAsync(sec));
            Settle(win);
            var open = DbWatch.Count - before;

            DbWatch.Recording = true;
            while (DbWatch.Log.TryDequeue(out _)) { }
            var idleBefore = DbWatch.Count;
            SitIdle(win, Sit);
            // ⚠️ پشتیبانِ خودکار (‎BackupPusher‎، ۴۵ ثانیه پس از ورود) کارِ خودِ
            // برنامه است، نه مصرفِ یک بخش: یک ‎VACUUM INTO‎ در تمامِ عمرِ اجرا.
            // این سنجش «بخشی که تویش نیستم» را می‌سنجد، پس همان یکی جدا شمرده
            // می‌شود و در قضاوت نمی‌آید.
            var backup = DbWatch.Log.Count(x => x.StartsWith("VACUUM INTO", StringComparison.OrdinalIgnoreCase));
            var idle = DbWatch.Count - idleBefore - backup;
            DbWatch.Recording = false;

            var strayRows = StrayRows(win);
            var strayLaid = StrayLaidOut(win);

            Console.WriteLine($"{sec.Id,-16} {open,10:N0}      {idle,10:N0}          {strayRows,10:N0}                {strayLaid,10:N0}"
                            + (backup > 0 ? "   (+" + backup + " پشتیبانِ خودکار)" : ""));

            if (idle > IdleQueryBudget)
            {
                var sample = DbWatch.Log.Take(3).Select(x => x.Replace('\n', ' ')).ToList();
                bad.Add($"«{sec.Id}»: در {Sit.TotalSeconds:N0} ثانیه بی‌کاری {idle} دستورِ دیتابیس — "
                      + (sample.Count > 0 ? "مثلاً: " + string.Join(" | ", sample) : ""));
            }
            if (strayRows > 0)
                bad.Add($"«{sec.Id}»: {strayRows} ردیفِ زنده مالِ بخشی است که دیده نمی‌شود");
            if (strayLaid > 0)
                bad.Add($"«{sec.Id}»: {strayLaid} کنترلِ نامرئی در پاسِ چیدمان شرکت کرده");
        }

        // ══ داخلِ بخش: حسابِ باز، و بس ═══════════════════════════════════════
        Console.WriteLine();
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is DebtSectionViewModel debt)
        {
            Wait(win, vm.GoAsync(debt)); Settle(win);

            var b1 = DbWatch.Count;
            Wait(win, debt.OpenByNumberAsync(1)); Settle(win);
            var one = DbWatch.Count - b1;

            var b2 = DbWatch.Count;
            SitIdle(win, Sit);
            var sit = DbWatch.Count - b2;

            var rows = debt.Person?.Current?.Rows.Count ?? 0;
            Console.WriteLine($"حسابِ قرض‌دارِ ۱: باز شدن {one} دستور · {rows:N0} ردیف · بی‌کاری {sit} دستور");
            if (sit > IdleQueryBudget)
                bad.Add($"حسابِ باز: در بی‌کاری {sit} دستورِ دیتابیس");

            debt.PersonOpen = false; Pump(win);
        }

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine("✅ بخشِ پنهان هیچ دستوری به دیتابیس نمی‌زند، ردیفِ زنده ندارد و چیده نمی‌شود");
            return 0;
        }
        Console.WriteLine("❌ این‌ها هنوز مصرف دارند:");
        foreach (var b in bad) Console.WriteLine("   • " + b);
        return 1;
    }

    /// <summary>
    /// ردیف‌های زنده‌ای که زیرِ یک کنترلِ نامرئی نشسته‌اند.
    ///
    /// ⚠️ ردیف‌های جدولِ **هم‌قدِ ردیف‌ها** (ورق و پارچه) شمرده نمی‌شوند: آن‌ها
    /// عمداً پارک نمی‌شوند، چون سقفشان ۸۰ ردیف است و بازسازی‌شان باز کردنِ
    /// دوبارهٔ ورق را ۱٫۲ ثانیه می‌کرد (سنجشِ ‎waraqperf‎). شرحش بالای
    /// ‎ExcelGrid.OnShownChanged‎.
    /// </summary>
    private static int StrayRows(Window w) =>
        w.GetVisualDescendants().OfType<DataGridRow>()
         .Count(r => !r.IsEffectivelyVisible && !InTallGrid(r));

    private static bool InTallGrid(Visual row) =>
        row.GetVisualAncestors().OfType<ExcelGrid>().FirstOrDefault()?.GrowsToContent == true;

    /// <summary>کنترلِ نامرئی‌ای که با این همه در پاسِ چیدمان اندازه گرفته شده.</summary>
    private static int StrayLaidOut(Window w)
    {
        var n = 0;
        foreach (var c in w.GetVisualDescendants().OfType<Layoutable>())
        {
            if (c.IsEffectivelyVisible) continue;
            // نامرئی و با این حال اندازهٔ غیرصفر ⇒ کسی مجبورش کرده اندازه بگیرد
            if (c is DataGridRow && c.Bounds.Height > 0 && !InTallGrid(c)) n++;
        }
        return n;
    }

    /// <summary>بی‌کاریِ واقعی: کارهای صف را می‌دواند ولی خودش هیچ کاری نمی‌کند.</summary>
    private static void SitIdle(Window w, TimeSpan how)
    {
        var end = DateTime.UtcNow + how;
        while (DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Thread.Sleep(10);
        }
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

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
