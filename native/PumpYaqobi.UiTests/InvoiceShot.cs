using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ سه صفحهٔ بخشِ فاکتور، بلند و کامل ════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «دیزاینِ فاکتور را عوض کن، یو‌ای یو‌اکس باشد… با دقت و
/// توجهِ زیاد.»
///
/// عکسِ ۱۴۴۰×۹۰۰ی همیشگی برگهٔ فاکتور را نصفه می‌بُرد و نه فهرست را نشان می‌داد
/// نه خودِ فاکتور را. این‌جا پنجره بلند گرفته می‌شود و هر سه صفحه جدا عکس
/// می‌شوند — تا طراحی را بشود واقعاً دید، نه حدس زد.
///
///     dotnet run --project PumpYaqobi.UiTests -- invshot &lt;پوشه&gt;
/// </summary>
internal static class InvoiceShot
{
    /// <summary>
    /// عکسِ سه حالتِ آغاز: لودینگ، قفل، و صفحهٔ اصلی — تا «کاربر چه می‌بیند»
    /// را بشود دید، نه حدس زد.
    /// </summary>
    public static int Startup(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var dir = Path.Combine(Path.GetTempPath(), "pump-startshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        var vm = (MainViewModel)win.DataContext!;
        win.Show();

        // ⚠️ **پیش از** هر ‎RunJobs‎ی عکس گرفته می‌شود: نخستین فراخوانی کلِ
        // گرم کردن را تا ته می‌بَرد و پرده دیگر روی صفحه نیست. چیزی که باید
        // دید همان لحظهٔ اول است.
        win.UpdateLayout();
        Shot(win, Path.Combine(outDir, "start-1-loading.png"), settle: false);

        while (vm.IsStarting)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        Settle(win);
        Shot(win, Path.Combine(outDir, "start-2-lock.png"));

        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        for (var i = 0; i < 60; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        Settle(win);
        Shot(win, Path.Combine(outDir, "start-3-main.png"));

        Console.WriteLine("عکس‌ها در: " + outDir);
        return 0;
    }

    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var dir = Path.Combine(Path.GetTempPath(), "pump-invshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 1500 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Pump(win);
        Seed.Fill(AppHost.Current);

        var sec = vm.Sections.First(s => s.Id == "invoices");
        Wait(win, vm.GoAsync(sec));
        Settle(win);

        // ── ۱) برگه، با یک فاکتورِ نیمه‌پرشده تا کارتِ کناری زنده باشد ──────
        Set(sec, "FCustomer", "عبدالرحمن نوری");
        Set(sec, "FAlias", "شرکت نوری");
        Set(sec, "FPhone", "0799123456");
        Set(sec, "FVehicle", "موتر باربری");
        Set(sec, "FLiters", "۲۰۰");
        Set(sec, "FPrice", "۶۷");
        Set(sec, "FAmount", "۵٬۰۰۰");
        Settle(win);
        Shot(win, Path.Combine(outDir, "inv-1-form.png"));

        // ── ۲) فهرست ──────────────────────────────────────────────────────
        Run(sec, "OpenListCommand", "pending");
        Settle(win);
        Shot(win, Path.Combine(outDir, "inv-2-list.png"));

        // ── ۳) خودِ فاکتور ────────────────────────────────────────────────
        if (sec.GetType().GetProperty("Rows")?.GetValue(sec) is System.Collections.IEnumerable rows
            && rows.Cast<object>().FirstOrDefault() is { } first)
        {
            Run(sec, "OpenDetailCommand", first);
            Settle(win);
            Shot(win, Path.Combine(outDir, "inv-3-detail.png"));
        }

        Console.WriteLine("عکس‌ها در: " + outDir);
        return 0;
    }

    private static void Set(object o, string prop, string value) =>
        o.GetType().GetProperty(prop)!.SetValue(o, value);

    private static void Run(object o, string cmd, object? arg) =>
        (o.GetType().GetProperty(cmd)!.GetValue(o) as System.Windows.Input.ICommand)?.Execute(arg);

    private static void Shot(Window w, string path) => Shot(w, path, settle: true);

    /// <summary>
    /// ⚠️ ‎settle: false‎ برای عکسِ لودینگ لازم است: ‎Settle‎ خودش ‎RunJobs‎
    /// می‌زند و نخستین ‎RunJobs‎ کلِ گرم کردن را تا ته می‌بَرد — یعنی تا
    /// بیت‌مپ گرفته شود، پرده رفته و صفحهٔ قفل آمده. یک بار همین‌طور شد و
    /// عکسِ «لودینگ» صفحهٔ قفل درآمد.
    /// </summary>
    private static void Shot(Window w, string path, bool settle)
    {
        if (settle) Settle(w);
        else w.UpdateLayout();
        var px = new PixelSize((int)w.Width, (int)w.Height);
        var bmp = new RenderTargetBitmap(px, new Vector(96, 96));
        bmp.Render(w);
        using var fs = File.Create(path);
        bmp.Save(fs);
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(1); }
        Pump(w);
    }

    private static void Settle(Window w)
    { for (var i = 0; i < 40; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
