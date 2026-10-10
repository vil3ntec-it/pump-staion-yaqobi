using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ نشانِ «کارِ پس‌زمینه» در سربرگ (۱۴۰۵/۰۷/۲۲، بندِ ۴ِ کندی‌ها) ══
///   • بی کار ⇒ نشان پنهان · کارِ درصددار ⇒ «⏳ … ۴۰٪» · دو کار ⇒ «و ۱ کارِ دیگر» · تمام ⇒ پنهان
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- bgwork [پوشهٔ عکس]
/// </summary>
internal static class BgWorkProbe
{
    private static int _bad;
    private static void Check(string what, bool ok, string? d = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (d is null ? "" : " — " + d));
        if (!ok) _bad++;
    }

    public static int Run(string? shots)
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-bgwork-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1500, Height = 900 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock); Pump(win);
        if (AppHost.Current.ShiftWaraqSync.FixOldSalesTask is { } fix) { for (var i = 0; i < 3000 && !fix.IsCompleted; i++) { Pump(win); Thread.Sleep(2); } }
        if (AppHost.Current.Parity.DailyTask is { } par) { for (var i = 0; i < 3000 && !par.IsCompleted; i++) { Pump(win); Thread.Sleep(2); } }
        Settle(win);
        if (shots is not null) Directory.CreateDirectory(shots);

        var chip = win.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "BgWorkChip");
        Check("نشان در سربرگ هست", chip is not null);
        Check("بی کار ⇒ پنهان", chip is { IsEffectivelyVisible: false } && !vm.HasBackgroundWork, vm.BackgroundWorkText);

        BackgroundWork.Set("probe-a", "مرتب کردنِ فروشِ ورق‌های قدیم", 40, 100);
        Settle(win);
        Check("کارِ درصددار ⇒ دیده می‌شود با درصد", chip is { IsEffectivelyVisible: true } && vm.BackgroundWorkText.Contains("40٪"),
              vm.BackgroundWorkText);
        BackgroundWork.Set("probe-b", "سنجشِ برابریِ حساب‌ها");
        Settle(win);
        Check("دو کار ⇒ «و ۱ کارِ دیگر»", vm.BackgroundWorkText.Contains("کارِ دیگر"), vm.BackgroundWorkText);
        if (shots is not null) Shot(win, shots, "bgwork");
        BackgroundWork.Clear("probe-a"); BackgroundWork.Clear("probe-b");
        Settle(win);
        Check("تمام ⇒ دوباره پنهان", chip is { IsEffectivelyVisible: false } && !vm.HasBackgroundWork, vm.BackgroundWorkText);

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Shot(Window win, string dir, string name)
    {
        using var f = win.CaptureRenderedFrame();
        f?.Save(Path.Combine(dir, name + ".png"));
    }
    private static void Pump(Window w) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    private static void Settle(Window w) { for (var i = 0; i < 30; i++) { Pump(w); Thread.Sleep(5); } }
}
