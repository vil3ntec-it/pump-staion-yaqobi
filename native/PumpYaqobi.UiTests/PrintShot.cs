using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ عکسِ صفحهٔ چاپ و پنجرهٔ «تنظیمِ ورق» ══════════════════════════════════
///     dotnet run --project PumpYaqobi.UiTests -- printshot <پوشه>
///
/// گزارشِ صاحب ریپو: «بخشِ پرینت خیلی کم‌بودی دارد و طراحی‌اش خراب است.» این
/// حالت همان دو پنجره را با یک گزارشِ واقعی باز می‌کند و عکس می‌گیرد تا پیش
/// از تحویل با چشم دیده شده باشد — نه حدس زده.
/// </summary>
internal static class PrintShot
{
    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-print-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);

        // یک گزارشِ واقعی — همان مصارف با هشتاد ردیف
        PdfEngine.Initialize();
        var rows = new List<PumpYaqobi.Domain.Entities.Expense>();
        for (var i = 1; i <= 80; i++)
            rows.Add(new PumpYaqobi.Domain.Entities.Expense
            { DateShamsi = "1405/06/" + ((i % 30) + 1).ToString("00"), Title = "مصرف شمارهٔ " + i, Amount = 1_000m * i });
        var input = new ExpenseReportInput("سنبله 1405", rows, "1405/06/09", "1405/06/09  ·  1448/03/27  ·  2026/09/09");

        var vm = new DocumentPreviewViewModel(
            s => new ExpenseReport(input) { Setup = s }, "مصارف سنبله 1405", PageSetup.Default);

        var preview = new DocumentPreviewWindow(vm) { Width = 1440, Height = 900, WindowState = WindowState.Normal };
        preview.Show(win);
        Pump(preview);
        Shot(preview, Path.Combine(outDir, "print-preview.png"));

        // پنجرهٔ تنظیمِ ورق — هر چهار زبانه
        var setup = new PrintSetupWindow(new PrintSetupViewModel(vm.Setup));
        setup.Show(preview);
        Pump(setup);
        var tabs = setup.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        var names = new[] { "page", "margins", "headfoot", "table" };
        for (var i = 0; i < 4; i++)
        {
            if (tabs is not null) tabs.SelectedIndex = i;
            Pump(setup);
            Shot(setup, Path.Combine(outDir, $"print-setup-{i + 1}-{names[i]}.png"));
        }
        setup.Close();
        preview.Close();
        Console.WriteLine("✅ عکس‌های چاپ گرفته شد: " + outDir);
        return 0;
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 10; i++)
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
