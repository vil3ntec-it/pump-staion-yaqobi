using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «بخشِ پرینت هم مشکلاتش را درست کن» — خودِ ورق‌ها، با چشم ════════════════
///
/// همان دکمهٔ PDFِ هر بخش زده می‌شود (نه یک گزارشِ ساختگی)، سند گرفته
/// می‌شود و ورقِ اولش با تنظیمِ پیش‌فرض، و یک بار سیاه‌وسفید، تصویر می‌شود.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- printpages &lt;پوشه&gt;
/// </summary>
internal static class PrintPages
{
    public static int Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-pp-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Pump(win);
        Seed.Fill(AppHost.Current);
        PdfEngine.Initialize();

        var got = new List<(string Title, Func<PageSetup, QuestPDF.Infrastructure.IDocument> Build)>();
        Documents.CaptureHook = (b, t) => got.Add((t, b));

        void Press(string id, Func<SectionViewModel, System.Windows.Input.ICommand?> cmd)
        {
            var sec = vm.Sections.First(s => s.Id == id);
            Wait(win, vm.GoAsync(sec));
            cmd(sec)?.Execute(null);
            Settle(win);
        }

        Press("safe", s => ((dynamic)s).PdfCommand);
        Press("expenses", s => ((dynamic)s).PdfCommand);
        Press("sarrafi", s => ((dynamic)s).PdfCommand);

        var debt = (DebtSectionViewModel)vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(debt));
        Wait(win, debt.RefreshAsync());
        debt.OpenCommand.Execute(debt.Cards.OrderBy(c => c.Entity.Id).First());
        Settle(win);
        debt.Person?.PdfCommand.Execute(null);
        Settle(win);

        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        var w = AppHost.Current.WaraqData.OpenOrCreateAsync(PumpYaqobi.Application.Localization.Shamsi.Today(), "پمپ یعقوبی")
                    .GetAwaiter().GetResult();
        Wait(win, vm.GoAsync(wq));
        wq.OpenCommand.Execute(wq.Sheets.First(x => x.Id == w.Id));
        Settle(win);
        wq.Page?.PdfCommand.Execute(null);
        Settle(win);

        for (var i = 0; i < 200 && got.Count < 5; i++) { Pump(win); Thread.Sleep(10); }
        Console.WriteLine($"  {got.Count} سند گرفته شد: " + string.Join("، ", got.Select(g => g.Title)));

        var bad = 0;
        var n = 0;
        foreach (var (title, build) in got)
        {
            n++;
            foreach (var (tag, setup) in new[] { ("color", PageSetup.Default),
                                                  ("bw", PageSetup.Default with { Color = PrintColor.BlackWhite }) })
            {
                try
                {
                    var solved = ScaleSolver.Solve(setup, build);
                    var s = setup.NeedsScaleSolve ? setup.WithResolvedScale(solved) : setup;
                    var doc = build(s);
                    var pages = doc.GenerateImages(new ImageGenerationSettings
                    { ImageFormat = ImageFormat.Png, RasterDpi = 110 }).ToList();
                    var path = Path.Combine(outDir, $"{n:00}-{tag}.png");
                    File.WriteAllBytes(path, pages[0]);
                    Console.WriteLine($"  ✔ {title} [{tag}] — {pages.Count} ورق، مقیاس {solved}% ⇒ {path}");
                }
                catch (Exception e)
                {
                    bad++;
                    Console.WriteLine($"  ✖ {title} [{tag}] — {e.GetType().Name}: {e.Message}");
                }
            }
        }
        Documents.CaptureHook = null;
        return bad == 0 && got.Count >= 5 ? 0 : 1;
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
