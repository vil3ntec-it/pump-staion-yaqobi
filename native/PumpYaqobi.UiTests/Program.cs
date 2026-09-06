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
        Pump(win);

        foreach (var theme in PumpTheme.All)
        {
            ThemeManager.Apply(theme);
            Pump(win);
            Shot(win, Path.Combine(outDir, "theme-" + theme.Id + ".png"));
        }

        Console.WriteLine("عکس‌ها در: " + Path.GetFullPath(outDir));
        return 0;
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
