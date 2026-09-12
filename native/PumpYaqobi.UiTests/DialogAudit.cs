using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Vision;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ سنجشِ پنجره‌های گفت‌وگو، بی هیچ قلابی ═══════════════════════════════════
///
/// گزارشِ صاحب ریپو: «موقعِ افزودن شخص، یا ورق، یا ساختِ حساب فرعی، یا جدول
/// جدید، یا رفتن تو آرشیو — هیچ‌کدام کار نمی‌کنند و یک پیامِ بی‌معنی می‌آید:
/// ‎Object reference not set to an instance of an object‎.»
///
/// ⚠️ چرا هیچ سنجشی این را نگرفته بود: تنها سنجشی که این مسیرها را می‌دوانْد
/// (‎person‎) از ‎Dialogs.PromptHook‎ استفاده می‌کند، یعنی **خودِ پنجره هرگز
/// ساخته نمی‌شد**. هر خطایی داخلِ ساختِ پنجره می‌ماند و فقط روی ویندوزِ کاربر
/// بیرون می‌زد.
///
/// این حالت همان پنجره‌های واقعی را می‌سازد و نشان می‌دهد — بی قلاب — و
/// می‌گوید کدامشان می‌ترکد.
///
///     dotnet run --project PumpYaqobi.UiTests -- dialogs
/// </summary>
internal static class DialogAudit
{
    private static int _bad;

    private static void Check(string what, Action body)
    {
        try
        {
            body();
            Console.WriteLine("  ✔ " + what);
        }
        catch (Exception ex)
        {
            _bad++;
            Console.WriteLine("  ✖ " + what + " — " + ex.GetType().Name + ": " + ex.Message);
            Console.WriteLine("      " + (ex.StackTrace ?? "").Replace("\n", "\n      "));
        }
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-dialogs-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1100, Height = 700 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Pump(win);

        Console.WriteLine("── پنجره‌های گفت‌وگو، همان‌طور که کاربر می‌بیند ──");

        // ۱) «بپرس» — پشتِ افزودنِ شخص، حسابِ فرعی، جدولِ جدید و نامِ شرکت
        Check("کادرِ پرسش ساخته و نشان داده می‌شود", () =>
        {
            var d = DialogWindow.ForPrompt("افزودن قرض‌دار", "نام:", "", "✔ افزودن");
            Show(d, win);
        });

        // ۲) «مطمئنی؟» — پشتِ حذف و آرشیو
        Check("کادرِ تایید ساخته و نشان داده می‌شود", () =>
        {
            var d = DialogWindow.ForConfirm("حذف", "این ردیف حذف شود؟", "بله", "انصراف");
            Show(d, win);
        });

        // ۳) کیو‌آر — با عکس و بی عکس
        Check("پنجرهٔ کیو‌آر با عکس", () =>
        {
            var png = QrWriter.EncodePng("https://example.invalid/#acct-1");
            Show(QrWindow.For("حسابِ آزمون", "https://example.invalid/#acct-1", png, "راهنما"), win);
        });
        Check("پنجرهٔ کیو‌آر بی عکس", () =>
            Show(QrWindow.For("حسابِ آزمون", "بی‌لینک", null, "راهنما"), win));

        // ۴) «ورق با تاریخ»
        Check("کادرِ تاریخِ ورق", () =>
            Show(WaraqDateWindow.For(new[] { 14050621 }), win));

        // ۵) و خودِ ساختِ کیو‌آر — بی پنجره
        Check("ساختِ عکسِ کیو‌آر", () =>
        {
            var png = QrWriter.EncodePng("سلام");
            if (png is not { Length: > 0 }) throw new Exception("عکسِ خالی برگشت");
        });

        Console.WriteLine(_bad == 0
            ? "✅ همهٔ پنجره‌های گفت‌وگو باز شدند."
            : "❌ " + _bad + " پنجره باز نشد.");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>
    /// پنجره را واقعاً نشان بده و یک‌بار بچرخان — همان کاری که ‎ShowDialog‎
    /// می‌کند، منهای انتظار برای بسته شدن (که در سنجش معلق می‌ماند).
    /// </summary>
    private static void Show(Window d, Window owner)
    {
        try
        {
            d.Show(owner);
            Pump(d);
            d.UpdateLayout();
        }
        finally { d.Close(); }
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
        }
    }
}
