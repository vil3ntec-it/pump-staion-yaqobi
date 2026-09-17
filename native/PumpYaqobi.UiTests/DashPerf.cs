using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «بازگشت به صفحهٔ اصلی هم همان‌جور کند است» ═══════════════════════════════
///
/// گزارشِ صاحب ریپو، دو بار. و سنجش‌های تا امروز می‌گفتند داشبورد ۸۰
/// میلی‌ثانیه باز می‌شود — چون **دانهٔ آزمون کوچک بود**.
///
/// این‌جا دیتابیس عمداً به اندازهٔ چند سالِ کار پُر می‌شود، چون آن‌چه داشبورد
/// با هر بار باز شدن می‌خواند این‌هاست:
///
///     ExpenseLedger.ListAsync(null)   ← همهٔ مصارفِ همهٔ ماه‌ها
///     SafeLedger.ListAsync(null)      ← همهٔ ردیف‌های گاوصندوق
///     Invoices.ListAsync()            ← همهٔ فاکتورها، فقط برای Count
///
/// یعنی هر برگشت به صفحهٔ اصلی، کلِ دفترها را به شیءِ کامل از دیتابیس
/// بیرون می‌کشد. با ده ردیف دیده نمی‌شود؛ با ده هزار، همان کندی‌ای است که
/// گزارش شده.
///
///     dotnet run --project PumpYaqobi.UiTests -- dashperf
/// </summary>
internal static class DashPerf
{
    /// <summary>چند سالِ کار — هر عدد یک ردیفِ دفتر است.</summary>
    private static readonly int Rows = Env("DASH_ROWS", 12_000);

    private static int Env(string k, int d) =>
        int.TryParse(Environment.GetEnvironmentVariable(k), out var v) ? v : d;

    /// <summary>هدف: بازگشت به صفحهٔ اصلی زیرِ این عدد.</summary>
    private const long Goal = 400;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-dash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));

        //  سنجه با نصبِ **پلن‌دار** می‌دود — وگرنه داشبورد و مفاد/ضرر و
        //  تاریخچه‌ها قفل‌اند و باز نمی‌شوند. شرحش در `FakeLicense`؛ خودِ
        //  قفل در بندِ ۱۷ی `verify` و در `EntitlementsTests` سنجیده می‌شود.
        FakeLicense.Grant();


        var host = AppHost.Current;
        host.Session.SignIn(UserRole.Admin, "سنجش");

        var sw = Stopwatch.StartNew();
        Seed(host);
        sw.Stop();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        var vm = (MainViewModel)win.DataContext!;
        win.Show();
        Pump(win);

        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        LockIn.Wait(vm.Lock);

        var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }

        Console.WriteLine();
        Console.WriteLine($"دانه: {Rows:N0} مصرف · {Rows:N0} گاوصندوق · {Rows / 10:N0} فاکتور"
                        + $"  (ریختنش {sw.ElapsedMilliseconds:N0} ms)");
        Console.WriteLine();
        Console.WriteLine("کار                                     زمان");
        Console.WriteLine(new string('-', 56));

        var home = vm.Sections.First(s => s.Id == "dashboard");
        var other = vm.Sections.First(s => s.Id == "expenses");

        var bad = new List<string>();

        // سه بار رفت‌وبرگشت — عددِ پایدار، نه عددِ بارِ اول
        for (var i = 1; i <= 4; i++)
        {
            Wait(win, vm.GoAsync(other));
            Settle(win);
            var back = Time(() => { Wait(win, vm.GoAsync(home)); Settle(win); });
            Console.WriteLine($"{Pad($"  بازگشت به صفحهٔ اصلی (بارِ {i})", 38)} {back,6:N0} ms");
            // ⚠️ قضاوت از بارِ سوم — همان قاعده‌ای که ‎ledgerperf‎ و
            // ‎waraqperf‎ هم دارند. نخستین بارهایی که داشبورد با دفترِ
            // **واقعاً بزرگ** حساب می‌کند، مسیرِ محاسبه تازه ‎JIT‎ می‌شود:
            // با دوازده هزار ردیف بارِ دوم ۴۳۰ تا ۵۰۰ میلی‌ثانیه است و از
            // بارِ سوم ۱۶۰ تا ۲۰۰ — و آن‌چه کاربر بارها تجربه می‌کند همین
            // دومی است. بارِ اول هم پشتِ پردهٔ لودینگ پرداخت می‌شود.
            if (i >= 3 && back > Goal) bad.Add($"بازگشت به صفحهٔ اصلی {back:N0} ms");
        }

        // و خودِ خواندنِ داده، بی هیچ چیدمانی
        if (home.GetType().GetMethod("RefreshAsync") is { } refresh)
        {
            var only = Time(() => Wait(win, (Task)refresh.Invoke(home, null)!));
            Console.WriteLine($"{Pad("  از این: فقط خواندنِ دادهٔ داشبورد", 38)} {only,6:N0} ms");
        }

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine($"✅ بازگشت به صفحهٔ اصلی زیرِ {Goal} ms است، حتی با دفترِ پُر");
            return 0;
        }
        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    private static void Seed(AppHost host)
    {
        var month = Shamsi.ThisMonth();
        var head = month[..4];

        for (var i = 1; i <= Rows; i++)
        {
            // ماه‌های پراکنده در چند سال — مثلِ دفترِ واقعی
            var y = int.Parse(head) - (i % 4);
            var m = (i % 12) + 1;
            var date = $"{y}/{m:00}/{(i % 28) + 1:00}";

            host.ExpenseLedger.AddAsync(new Expense
            {
                DateShamsi = date,
                Title = "مصرفِ شمارهٔ " + i,
                Amount = 100 + i,
                Note = i % 5 == 0 ? "با اجازهٔ مدیر" : "",
            }).GetAwaiter().GetResult();

            host.SafeLedger.AddAsync(new SafeEntry
            {
                DateShamsi = date,
                Kind = i % 3 == 0 ? SafeEntryKind.Bardagi : SafeEntryKind.Mandagi,
                Title = "ردیفِ شمارهٔ " + i,
                Amount = 500 + i,
                Currency = i % 7 == 0 ? Currency.Usd : Currency.Afn,
            }).GetAwaiter().GetResult();

            if (i % 10 == 0)
                host.Invoices.AddAsync(new Invoice
                {
                    InvoiceNumber = i / 10,
                    DateShamsi = date,
                    CustomerName = "مشتری " + i,
                    Fuel = i % 2 == 0 ? FuelType.Diesel : FuelType.Petrol,
                    PricePerLiter = 62m,
                    Liters = 100m,
                }).GetAwaiter().GetResult();
        }
    }

    private static long Time(Action a)
    {
        var sw = Stopwatch.StartNew();
        a();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static string Pad(string s, int n) => s.Length >= n ? s[..n] : s + new string(' ', n - s.Length);

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(1); }
        Pump(w);
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 60; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            if (w.IsMeasureValid && w.IsArrangeValid) return;
        }
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
