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

        //  سنجه با نصبِ **پلن‌دار** می‌دود — وگرنه داشبورد و مفاد/ضرر و
        //  تاریخچه‌ها قفل‌اند و باز نمی‌شوند. شرحش در `FakeLicense`؛ خودِ
        //  قفل در بندِ ۱۷ی `verify` و در `EntitlementsTests` سنجیده می‌شود.
        FakeLicense.Grant();


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

        //  چاپگرهای ساختگی — ماشینِ سنجه چاپگر ندارد؛ همان سه حالتِ واقعی:
        //  پیش‌فرضِ آماده (عکسِ صاحب ریپو: EPSON L382)، چاپگرِ PDF، و آفلاین.
        PumpYaqobi.App.Printing.Printers.ListOverride = () => PumpYaqobi.App.Printing.Printers.Order(new[]
        {
            new PumpYaqobi.App.Printing.PrinterItem("Microsoft Print to PDF", false, "آماده", true),
            new PumpYaqobi.App.Printing.PrinterItem("HP LaserJet (دفتر)", false, "آفلاین — روشن و وصل است؟", false),
            new PumpYaqobi.App.Printing.PrinterItem("EPSON L382 Series", true, "آماده", true),
        });

        var vm = new DocumentPreviewViewModel(
            s => new ExpenseReport(input) { Setup = s }, "مصارف سنبله 1405", PageSetup.Default);

        var preview = new DocumentPreviewWindow(vm) { Width = 1440, Height = 900, WindowState = WindowState.Normal };
        preview.Show(win);
        Pump(preview);

        // ⚠️ پنجره دیگر منتظرِ ورق‌ها نمی‌ماند (‎printperf‎): ورق‌ها در پس‌زمینه
        // می‌آیند، پس سنجش هم مثلِ کاربر صبر می‌کند تا همه بنشینند.
        var ready = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (!vm.AllRendered && DateTime.UtcNow < ready) { Pump(preview); Thread.Sleep(5); }
        Pump(preview);
        Shot(preview, Path.Combine(outDir, "print-preview.png"));

        //  ⛔ چاپگرها مثلِ اکسل: فهرستِ واقعی، پیش‌فرضِ ویندوز برگزیده
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (vm.PrintersLoading && DateTime.UtcNow < until) { Pump(preview); Thread.Sleep(5); }
        if (!vm.HasPrinters || vm.Printers.Count != 3)
        { Console.WriteLine("  ✘ فهرستِ چاپگرها نیامد: " + vm.Printers.Count); return 1; }
        if (vm.Printer?.Name != "EPSON L382 Series")
        { Console.WriteLine("  ✘ پیش‌فرضِ ویندوز برگزیده نشد: " + vm.Printer?.Name); return 1; }
        Console.WriteLine("  ✔ چاپگرها آمدند و پیش‌فرضِ ویندوز برگزیده است — " + vm.Printer.Name + " · " + vm.Printer.Line);
        var combo = preview.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.Classes.Contains("prn"));
        if (combo is null || !combo.IsEffectivelyVisible)
        { Console.WriteLine("  ✘ کادرِ چاپگرها دیده نمی‌شود"); return 1; }
        combo.IsDropDownOpen = true;
        Pump(preview);
        Shot(preview, Path.Combine(outDir, "print-printers.png"));
        combo.IsDropDownOpen = false;
        Pump(preview);

        // «ورق‌ها: ۱ تا ۳» همیشه دیده می‌شود و تایپ در آن خودش حالت را بازه می‌کند (مثلِ سایت)
        if (vm.FromText != "1" || vm.ToText != vm.PageCount.ToString())
        { Console.WriteLine("  ✘ کادرِ از/تا کلِ گزارش را نشان نمی‌دهد: " + vm.FromText + " تا " + vm.ToText); return 1; }
        vm.ToText = "2";
        Pump(preview);
        if (vm.What?.Value != "range" || string.Join(",", vm.PickedPages()) != "1,2")
        { Console.WriteLine("  ✘ تایپ در «تا» حالت را بازه نکرد: " + vm.What?.Value); return 1; }
        Shot(preview, Path.Combine(outDir, "print-preview-range.png"));
        Console.WriteLine("  ✔ «ورق‌ها: ۱ تا ۲» خودش بازه شد و همان دو ورق را می‌دهد");

        // «چاپِ ورق‌های دلخواه» — کادرِ ۱،۳ و تیکِ هر ورق
        vm.What = vm.Whats.First(w => w.Value == "pages");
        vm.PagesText = "1,3";
        Pump(preview);
        Shot(preview, Path.Combine(outDir, "print-preview-pages.png"));
        // تیک‌ها همان متن‌اند — و زدنِ تیک متن را می‌نویسد
        var ticked = string.Join(",", vm.PageChecks.Where(c => c.IsOn).Select(c => c.Number));
        if (ticked != "1,3") { Console.WriteLine("  ✘ تیک‌های ورق با کادر یکی نیست: " + ticked); return 1; }
        vm.PageChecks[1].IsOn = true;
        if (vm.PagesText != "1-3") { Console.WriteLine("  ✘ زدنِ تیک متن را ننوشت: " + vm.PagesText); return 1; }
        if (string.Join(",", vm.PickedPages()) != "1,2,3") { Console.WriteLine("  ✘ ورق‌های چاپ با تیک‌ها یکی نیست"); return 1; }
        Console.WriteLine("  ✔ تیک‌های ورق و کادرِ «ورق‌ها» یک چیزند");
        vm.What = vm.Whats.First(w => w.Value == "all");
        Pump(preview);

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
