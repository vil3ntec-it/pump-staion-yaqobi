using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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

        Console.WriteLine("عکس‌ها در: " + Path.GetFullPath(outDir));
        return 0;
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
}
