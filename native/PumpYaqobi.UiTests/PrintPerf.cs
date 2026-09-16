using System.Diagnostics;
using Avalonia;
using Avalonia.Headless;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «بخشِ پرینت هنوز کند است» ═══════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «برنامه خیلی سریع شده… ولی بخشِ پی‌دی‌اف
/// هنوز کند است، خیلی دیر باز می‌شود.»
///
/// قاعدهٔ «⚡ قانونِ همیشگیِ سرعت» می‌گوید اول بساز که ببینی. این‌جا همان مسیرِ
/// واقعیِ باز شدنِ پیش‌نمایش تکه‌تکه سنجیده می‌شود، با سه اندازهٔ گزارش:
///
///     ثبتِ فونت‌ها (PdfEngine) · ساختِ سند (ScaleSolver) · تصویر کردنِ ورق‌ها
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- printperf
/// </summary>
internal static class PrintPerf
{
    /// <summary>پنجرهٔ چاپ باید **همان لحظه** باز شود، هر اندازه که گزارش باشد.</summary>
    private const long OpenGoal = 200;

    /// <summary>و ورقِ اول پشتِ آن، تا این سقف.</summary>
    private const long FirstPageGoal = 2_500;

    private static readonly int[] Sizes = { 40, 200, 600 };

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-printperf-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var warm = Stopwatch.StartNew();
        PdfEngine.Initialize();
        warm.Stop();
        Console.WriteLine($"\nثبتِ فونت‌ها (بارِ اول): {warm.ElapsedMilliseconds:N0} ms");
        var warm2 = Stopwatch.StartNew(); PdfEngine.Initialize(); warm2.Stop();
        Console.WriteLine($"ثبتِ فونت‌ها (بارِ دوم): {warm2.ElapsedMilliseconds:N0} ms");

        Console.WriteLine();
        Console.WriteLine("ردیف   ورق   باز شدنِ پنجره   ورقِ اول   همهٔ ورق‌ها   تصویرِ همه با dpiِ چاپ");
        Console.WriteLine(new string('-', 88));

        var bad = new List<string>();

        foreach (var n in Sizes)
        {
            var input = Input(n);

            // همان کاری که ‎Documents.ShowAsync‎ می‌کند: ساختنِ ویومدل، که خودش
            // سند را می‌سازد و **همهٔ** ورق‌ها را تصویر می‌کند.
            var all = Stopwatch.StartNew();
            var vm = new DocumentPreviewViewModel(s => new ExpenseReport(input) { Setup = s },
                                                  "مصارف", PageSetup.Default);
            all.Stop();

            // ورقِ اول — کاربر پنجره را باز می‌بیند و این پشتِ آن می‌آید
            var firstSw = Stopwatch.StartNew();
            var end = DateTime.UtcNow + TimeSpan.FromMinutes(3);
            while (vm.PageCount == 0 && DateTime.UtcNow < end)
            { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
            firstSw.Stop();

            var rest = Stopwatch.StartNew();
            while (!vm.AllRendered && DateTime.UtcNow < end)
            { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
            rest.Stop();

            // ── تکه‌تکه: چیدمانِ سند در برابرِ تصویر کردنِ ورق ──────────────
            var lay = Stopwatch.StartNew();
            var count36 = ((IDocument)new ExpenseReport(input) { Setup = PageSetup.Default })
                .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 36 })
                .Count();
            lay.Stop();
            Console.WriteLine($"      چیدمان+تصویرِ سبک (۳۶dpi، {count36} ورق): {lay.ElapsedMilliseconds:N0} ms"
                            + $"   ·   کَشِ QuestPDF: {QuestPDF.Settings.EnableCaching}   ·   دیباگ: {QuestPDF.Settings.EnableDebugging}");

            // و همان دو تکه، جدا
            var build = Stopwatch.StartNew();
            var doc = new ExpenseReport(input) { Setup = PageSetup.Default };
            var imgs = ((IDocument)doc).GenerateImages(new ImageGenerationSettings
            {
                ImageFormat = ImageFormat.Png,
                RasterDpi = 144,
            }).ToList();
            build.Stop();

            var pages = vm.PageCount;
            Console.WriteLine($"{n,5} {pages,5} {all.ElapsedMilliseconds,11:N0} ms {firstSw.ElapsedMilliseconds,9:N0} ms "
                            + $"{firstSw.ElapsedMilliseconds + rest.ElapsedMilliseconds,10:N0} ms {build.ElapsedMilliseconds,18:N0} ms");

            if (all.ElapsedMilliseconds > OpenGoal)
                bad.Add($"گزارشِ {n} ردیفی ({pages} ورق): باز شدنِ پنجره {all.ElapsedMilliseconds:N0} ms");
            if (firstSw.ElapsedMilliseconds > FirstPageGoal)
                bad.Add($"گزارشِ {n} ردیفی: ورقِ اول {firstSw.ElapsedMilliseconds:N0} ms");
            // ⚠️ درستی هم سنجیده می‌شود، نه فقط سرعت: همهٔ ورق‌ها باید سرِ آخر
            // بیایند و شمارشان همان شمارِ ورق‌های سندِ کامل باشد.
            if (!vm.AllRendered || pages != imgs.Count)
                bad.Add($"گزارشِ {n} ردیفی: {pages} ورق آمد ولی سند {imgs.Count} ورق دارد");
        }

        Console.WriteLine();
        if (bad.Count == 0) { Console.WriteLine($"✅ پنجرهٔ چاپ زیرِ {OpenGoal} ms باز می‌شود و ورق‌ها پشتِ آن می‌آیند"); return 0; }
        Console.WriteLine("❌ پیش‌نمایشِ چاپ هنوز کند است:");
        foreach (var b in bad) Console.WriteLine("   • " + b);
        return 1;
    }

    private static ExpenseReportInput Input(int n)
    {
        var rows = new List<PumpYaqobi.Domain.Entities.Expense>();
        for (var i = 1; i <= n; i++)
            rows.Add(new PumpYaqobi.Domain.Entities.Expense
            {
                DateShamsi = "1405/06/" + ((i % 30) + 1).ToString("00"),
                Title = "مصرف شمارهٔ " + i,
                Amount = 1_000m * i,
            });
        return new ExpenseReportInput("سنبله 1405", rows, "1405/06/09", "1405/06/09");
    }
}
