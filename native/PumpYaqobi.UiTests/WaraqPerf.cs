using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ ورق چقدر دیر باز و بسته می‌شود ══════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «ورق‌هایی که توشان چیزی نوشته یا جدولِ زیادی دارند خیلی
/// کند باز می‌شوند، و وقتی از ورق بیرون هم می‌شوم خیلی کند بازگشت می‌شود.»
///
/// این‌جا همان دو کار روی ورقِ واقعی سنجیده می‌شود، با ورق‌هایی که پُرند:
///
///   • باز کردنِ ورق (‎OpenCard‎)
///   • بازگشت به فهرست (‎Back‎)
///
/// و چون شکایت دربارهٔ «ورقِ پُر» است، دانهٔ آزمون هم پُر ساخته می‌شود: چند ورق،
/// هر کدام با پایه‌ها و ده‌ها ردیف.
///
///     dotnet run --project PumpYaqobi.UiTests -- waraqperf
/// </summary>
internal static class WaraqPerf
{
    /// <summary>چند ورق ساخته شود، و هر ورق چند ردیف داشته باشد.</summary>
    private const int Sheets = 24;
    private const int RowsPerSheet = 60;
    private const int PumpsPerSheet = 6;

    /// <summary>هدف: باز و بسته شدنِ ورق زیرِ این عدد.</summary>
    private const long Goal = 400;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-waraq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");

        AppHost.Start(file);
        var host = AppHost.Current;
        // دانه ریختن کارِ «مدیر» است، پس همان نقش را می‌گیریم
        host.Session.SignIn(PumpYaqobi.Domain.Enums.UserRole.Admin, "سنجش");
        Seed(host);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Settle(win, TimeSpan.FromSeconds(3));

        var sec = vm.Sections.First(s => s.Id == "waraq");
        var cold = Time(() => Wait(win, vm.GoAsync(sec)));

        Console.WriteLine();
        Console.WriteLine($"{Sheets} ورق، هر کدام {PumpsPerSheet} پایه و {RowsPerSheet} ردیف");
        Console.WriteLine();
        Console.WriteLine("کار                                     زمان     جدولِ ساخته‌شده  ردیفِ زنده");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"{Pad("باز کردنِ بخشِ ورق‌ها (بارِ اول)", 38)} {cold,6:N0} ms");

        // کارتِ نخستِ فهرست — همان کارتی که کاربر رویش کلیک می‌کند
        var list = sec.GetType().GetProperty("Cards")?.GetValue(sec) as System.Collections.IEnumerable;
        var card = list?.Cast<object>().FirstOrDefault();
        var openCmd = sec.GetType().GetProperty("OpenCardCommand")?.GetValue(sec)
                      as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;
        if (card is null || openCmd is null)
        { Console.WriteLine("کارتِ ورق پیدا نشد"); return 1; }

        var bad = new List<string>();

        // ── تفکیکِ هزینه: خواندن از دیتابیس، در برابرِ ساختنِ صفحه ──────────
        var entity = card.GetType().GetProperty("Entity")!.GetValue(card)!;
        var id = (long)entity.GetType().GetProperty("Id")!.GetValue(entity)!;

        object? full = null;
        var sql = Time(() => full = host.WaraqData.LoadAsync(id).GetAwaiter().GetResult());
        var listAll = Time(() => host.WaraqData.ListAsync(
                                     sec.GetType().GetProperty("Month")?.GetValue(sec) as string)
                                 .GetAwaiter().GetResult());
        Console.WriteLine($"{Pad("  از این: خواندنِ همین یک ورق (SQL)", 38)} {sql,6:N0} ms");
        Console.WriteLine($"{Pad("  از این: خواندنِ فهرستِ کلِ ماه (SQL)", 38)} {listAll,6:N0} ms");

        // ── باز کردن و بستنِ ورق، چند بار ──────────────────────────────────
        for (var round = 1; round <= 3; round++)
        {
            // ⚠️ تفکیک به «ویومدل» و «چیدمان» یک بار گمراه‌کننده بود و همین‌جا
            // نوشته می‌ماند: ‎OpenAsync‎ خودش ۱۲ میلی‌ثانیه است (خواندن ۲،
            // ساختنِ ویومدل ۹)، ولی هر **پاسِ چیدمانِ** صفحهٔ ورق حدودِ ۴۰۰
            // میلی‌ثانیه می‌برد و باز شدن چند پاس لازم دارد. پس عددِ زیر
            // «کارِ رابط» است، نه کارِ داده — و جای درست کردنش هم همان‌جاست.
            var open = Time(() => { Wait(win, openCmd.ExecuteAsync(card)); Settle(win, 6); });
            var grids = win.GetVisualDescendants().OfType<DataGrid>().Count();
            var rows = win.GetVisualDescendants().OfType<DataGridRow>().Count();
            Console.WriteLine($"{Pad($"  باز کردنِ ورق (بارِ {round})", 38)} {open,6:N0} ms "
                            + $"{grids,14:N0} {rows,11:N0}");
            if (open > Goal) bad.Add($"باز کردنِ ورق {open:N0} ms — بیش از {Goal} ms");

            var back = Time(() => { Wait(win, Back(sec)); Settle(win, 6); });
            Console.WriteLine($"{Pad($"  بازگشت به فهرست (بارِ {round})", 38)} {back,6:N0} ms");
            if (back > Goal) bad.Add($"بازگشت از ورق {back:N0} ms — بیش از {Goal} ms");
        }

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine($"✅ ورق زیرِ {Goal} ms باز و بسته می‌شود");
            return 0;
        }
        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    /// <summary>فرمانِ «بازگشت»ِ خودِ بخش — همان دکمه‌ای که کاربر می‌زند.</summary>
    private static Task Back(object sec)
    {
        var p = sec.GetType().GetProperty("BackCommand");
        if (p?.GetValue(sec) is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand c)
            return c.ExecuteAsync(null);
        return Task.CompletedTask;
    }

    // ══ دانه: ورق‌های پُر ═══════════════════════════════════════════════════

    private static void Seed(AppHost host)
    {
        // ⚠️ همه در **ماهِ جاری**: فهرستِ ورق‌ها ماه‌به‌ماه فیلتر می‌شود و
        // ورقی در ماهِ دیگر اصلاً در کارت‌ها نمی‌آید — یک بار همین باعث شد
        // سنجش بگوید «کارتِ ورق پیدا نشد».
        var today = Shamsi.Today();
        var head = today[..7];

        for (var s = 0; s < Sheets; s++)
        {
            var date = $"{head}/{(s % 28) + 1:00}";

            var w = host.WaraqData.OpenOrCreateAsync(date, "پمپ یعقوبی").GetAwaiter().GetResult();
            var full = host.WaraqData.LoadAsync(w.Id).GetAwaiter().GetResult();
            var sd = full?.Shifts.FirstOrDefault();
            if (sd is null) continue;

            for (var p = 1; p <= PumpsPerSheet; p++)
                host.WaraqData.SavePumpAsync(new WaraqPump
                {
                    ShiftId = sd.Id, SortIndex = p,
                    Num = p, Worker = "کارمند " + p,
                    Fuel = p % 2 == 0 ? FuelType.Diesel : FuelType.Petrol,
                    Start = 10_000m * p, End = 10_000m * p + 500m,
                    PricePerLiter = 67m, Note = "۰۶:۰۰ تا ۱۸:۰۰",
                }).GetAwaiter().GetResult();

            for (var i = 1; i <= RowsPerSheet; i++)
                host.WaraqData.SaveTxnAsync(new WaraqTransaction
                {
                    ShiftId = sd.Id, SortIndex = i,
                    Name = "مشتری شمارهٔ " + i, Liters = 10m * i,
                    Fuel = i % 3 == 0 ? FuelType.Diesel : FuelType.Petrol,
                    Type = i % 4 == 0 ? WaraqTxnType.Expense : WaraqTxnType.Debt,
                    AmountAuto = true,
                }).GetAwaiter().GetResult();
        }
    }

    // ── ابزار ─────────────────────────────────────────────────────────────

    private static long Time(Action a)
    {
        var sw = Stopwatch.StartNew();
        a();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static string Pad(string s, int n) =>
        s.Length >= n ? s[..n] : s + new string(' ', n - s.Length);

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(2); }
        Pump(w);
    }

    private static void Settle(Window w, int passes)
    { for (var i = 0; i < passes; i++) { Dispatcher.UIThread.RunJobs(); Pump(w); } }

    private static void Settle(Window w, TimeSpan budget)
    {
        var end = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
