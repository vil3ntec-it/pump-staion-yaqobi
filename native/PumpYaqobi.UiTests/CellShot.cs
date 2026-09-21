using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ عکسِ خانه‌ای که در حالِ تایپ است ═════════════════════════════════════════
///
/// وقتی بحث سرِ «این کادر چه شکلی باشد» است، حرف فایده ندارد — باید دید. این
/// حالت یک بخش را باز می‌کند، یک خانه را به حالتِ تایپ می‌برد، متن می‌گذارد و
/// از همان ناحیه عکس می‌گیرد.
///
/// نامِ عکس را با ‎PUMP_CELLSHOT‎ می‌شود عوض کرد، تا چند حالت کنارِ هم بماند.
///
///     dotnet run --project PumpYaqobi.UiTests -- cellshot /tmp/x
/// </summary>
internal static class CellShot
{
    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-cellshot-" + Guid.NewGuid().ToString("N"), "pump.db");
        PumpYaqobi.App.Services.AppHost.Start(tmpDb);
        //  ⛔ نصبِ تازه از ۱۴۰۵/۰۷/۰۷ **بی‌رمز** باز می‌شود و صفحهٔ قفل ندارد.
        //  این سنجه همان مسیرِ رمزدار را می‌سنجد، پس رمز را خودش می‌گذارد.
        if (PumpYaqobi.App.Services.AppHost.Current.Auth.NeedsFirstRun()) PumpYaqobi.App.Services.AppHost.Current.Auth.CreateFirstAdmin("1234");

        //  سنجه با نصبِ **پلن‌دار** می‌دود — وگرنه داشبورد و مفاد/ضرر و
        //  تاریخچه‌ها قفل‌اند و باز نمی‌شوند. شرحش در `FakeLicense`؛ خودِ
        //  قفل در بندِ ۱۷ی `verify` و در `EntitlementsTests` سنجیده می‌شود.
        FakeLicense.Grant();


        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1100, Height = 560 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        LockIn.Wait(vm.Lock);
        Pump(win);
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        var sec = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(sec));
        for (var i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); Pump(win); }

        var grid = win.GetVisualDescendants().OfType<DataGrid>()
                      .FirstOrDefault(g => g.IsEffectivelyVisible);
        if (grid is null) { Console.WriteLine("جدولی نبود"); return 1; }

        grid.Focus();
        grid.SelectedIndex = 1;
        grid.CurrentColumn = grid.Columns.FirstOrDefault(c => !c.IsReadOnly && c.IsVisible);
        Pump(win);
        grid.BeginEdit();
        Pump(win);

        var box = grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsVisible);
        if (box is not null) { box.Text = "برق دکان"; box.CaretIndex = 4; }
        for (var i = 0; i < 4; i++) Pump(win);

        var name = Environment.GetEnvironmentVariable("PUMP_CELLSHOT") ?? "edit";
        var path = Path.Combine(outDir, $"cell-{name}.png");
        using (var f = win.CaptureRenderedFrame())
        {
            if (f is null) { Console.WriteLine("عکس گرفته نشد"); return 1; }
            f.Save(path);
        }
        Console.WriteLine("✔ " + path);
        return 0;
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        Pump(w);
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
