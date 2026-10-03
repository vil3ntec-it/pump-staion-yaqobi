using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Domain;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ داستانِ ویدیوی تبلیغاتی — برنامه واقعاً کار می‌کند ═══════════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- story [پوشه]
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۹): «قرض را در ورق بزن، ببین خودش به حسابِ
/// قرض‌دار و گاوصندوق می‌رود، تیلِ فروخته از مخزن کم می‌شود و هشدارش می‌آید».
/// همان راهِ واقعیِ برنامه (‎WaraqPosting.SyncAsync‎، ‎ParchaData‎) روی دادهٔ
/// ‎MarketingShots.Fill‎؛ از هر مرحله «پیش» و «پس» عکس گرفته می‌شود. ⛔ فقط
/// دیتابیسِ موقت؛ هیچ چیزی را نمی‌سنجد.
/// </summary>
internal static class StoryShots
{
    public static int Run(string? outDir)
    {
        outDir ??= Path.Combine(Path.GetTempPath(), "pump-story");
        Directory.CreateDirectory(outDir);
        var dir = Path.Combine(Path.GetTempPath(), "pump-story-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1600, Height = 900 };
        win.Show();
        LayoutCycleProbe.SetScaling(win, 2.0);
        Settle(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);
        var host = AppHost.Current;
        MarketingShots.Fill(host);
        host.Settings.Set(PumpYaqobi.Services.Data.SettingsService.LowStockThreshold, 12000m);
        Settle(win);
        ThemeManager.Apply(PumpTheme.Blue);
        Settle(win);

        SectionViewModel? By(string id) => vm.Sections.FirstOrDefault(x => x.Id == id);
        void Try(string name, Action a)
        {
            try { a(); }
            catch (Exception e) { Console.WriteLine("  ✖ " + name + ": " + e); }
        }
        void Go(string id) { if (By(id) is { } s) Wait(win, vm.GoAsync(s)); }
        void Away() { Go("expenses"); }

        //  ورقِ امروز و حسابِ «سید جلال هاشمی»
        var today = Shamsi.Of(AppClock.Today);
        var waraq = host.WaraqData.OpenOrCreateAsync(today, "پمپ بنزین").GetAwaiter().GetResult();
        Try("post0", () => host.WaraqPosting.SyncAsync(waraq.Id).GetAwaiter().GetResult());
        var debtor = host.Debtors.ListAsync().GetAwaiter().GetResult().First(d => d.Name.StartsWith("سید جلال"));

        void ShotWaraq(string name) => Try(name, () =>
        {
            if (By("waraq") is not WaraqSectionViewModel ws) return;
            Away();
            Wait(win, vm.GoAsync(ws));
            var card = ws.Cards.FirstOrDefault();
            if (card is not null && ws.GetType().GetMethod("OpenCardAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(ws, new object?[] { card }) is Task open)
                Wait(win, open);
            Shot(win, Path.Combine(outDir, name + ".png"));
            ws.CloseOpenPage();
            Settle(win);
        });
        void ShotAccount(string name) => Try(name, () =>
        {
            if (By("debt") is not DebtSectionViewModel debt) return;
            Away();
            Wait(win, vm.GoAsync(debt));
            Shot(win, Path.Combine(outDir, name + "-cards.png"));
            Wait(win, debt.OpenPersonAsync(debtor.Id));
            Shot(win, Path.Combine(outDir, name + ".png"));
            debt.CloseOpenPage();
            Settle(win);
        });
        void ShotSection(string id, string name) => Try(name, () =>
        {
            Away();
            Go(id);
            Shot(win, Path.Combine(outDir, name + ".png"));
        });
        void ShotDash(string name, bool alerts) => Try(name, () =>
        {
            if (By("dashboard") is not DashboardSectionViewModel dash) return;
            Away();
            Wait(win, vm.GoAsync(dash));
            Wait(win, dash.RefreshAsync());
            Shot(win, Path.Combine(outDir, name + ".png"));
            if (!alerts) return;
            dash.BellCommand.Execute(null);
            Settle(win);
            Shot(win, Path.Combine(outDir, name + "-alerts.png"));
            dash.AlertsOpen = false;
            Settle(win);
        });

        //  ۰) پیش از هر چیز
        ShotWaraq("a00-waraq-before");
        ShotSection("expenses", "a00-expenses-before");
        ShotSection("safe", "a00-safe-before");
        ShotSection("storage", "a00-storage-before");
        ShotDash("a00-dash-before", false);

        // ۱) پارچه: شروع و ختمِ پایه — همان فرمِ برنامه، پارچهٔ جدیدِ روز
        Try("parcha", () =>
        {
            if (By("shifts") is not ParchaSectionViewModel ps) return;
            Away();
            Wait(win, vm.GoAsync(ps));
            ps.IsDiesel = false;
            Settle(win);
            ps.NewParchaDayCommand.Execute(null);
            Settle(win);
            var start = host.ParchaData.LastBaseAsync(FuelType.Petrol, 1).GetAwaiter().GetResult();
            ps.Day.Name = "احمد رحیمی";
            ps.Day.PumpNum = "1";
            ps.Day.Price = "68";
            Settle(win);
            Shot(win, Path.Combine(outDir, "b01-parcha-empty.png"));
            ps.Day.Start = ((long)start).ToString();
            Settle(win);
            Shot(win, Path.Combine(outDir, "b02-parcha-start.png"));
            ps.Day.End = ((long)start + 4200).ToString();
            Settle(win);
            Shot(win, Path.Combine(outDir, "b03-parcha-end.png"));
            Wait(win, ps.Day.SaveCommand.ExecuteAsync(null));
            Shot(win, Path.Combine(outDir, "b04-parcha-saved.png"));
            Console.WriteLine("  پارچه: " + start + " ⇒ " + (start + 4200));
        });

        // ۲) همان پایه در ورق آمد
        Try("post1", () => host.WaraqPosting.SyncAsync(waraq.Id).GetAwaiter().GetResult());
        ShotWaraq("c01-waraq-pump");

        // ۳) قرض و مصرف در ورق نوشته می‌شوند
        Try("write", () =>
        {
            var w = host.WaraqData.LoadAsync(waraq.Id).GetAwaiter().GetResult()!;
            var day = w.Shifts.First(x => x.Kind == ShiftKind.Day);
            var rows = day.Transactions.OrderBy(x => x.SortIndex).ToList();
            var free = rows.Where(x => string.IsNullOrWhiteSpace(x.Name) && x.Amount == 0 && x.Liters == 0).ToList();
            Console.WriteLine("  ردیف‌های خالی: " + string.Join(",", free.Select(x => rows.IndexOf(x))));
            var t = free[0];
            t.Name = "سید جلال هاشمی";
            t.Liters = 150;
            t.Amount = 150 * 68;
            t.Type = WaraqTxnType.Debt;
            t.Fuel = FuelType.Petrol;
            t.AmountAuto = true;
            host.WaraqData.SaveTxnAsync(t).GetAwaiter().GetResult();
            var e = free[1];
            e.Name = "روغنِ جنراتور";
            e.Liters = 0;
            e.Amount = 3500;
            e.Type = WaraqTxnType.Expense;
            e.Fuel = FuelType.Petrol;
            e.AmountAuto = false;
            host.WaraqData.SaveTxnAsync(e).GetAwaiter().GetResult();
            var rep = host.WaraqPosting.SyncAsync(waraq.Id).GetAwaiter().GetResult();
            Console.WriteLine("  ثبت به حساب‌ها: " + rep + " ردیفِ قرض " + rows.IndexOf(t) + " · مصرف " + rows.IndexOf(e));
        });
        ShotWaraq("c02-waraq-txns");

        // ۴) مصارف · ۵) گاوصندوق · حسابِ قرض‌دار
        ShotSection("expenses", "d01-expenses-after");
        ShotSection("safe", "e01-safe-after");
        ShotAccount("f01-account-after");

        // ۶) مخزن: پس از پارچه، و بعد تخلیه تا هشدار
        ShotSection("storage", "g00-storage");
        Try("drain", () =>
        {
            var start = host.ParchaData.LastBaseAsync(FuelType.Petrol, 1).GetAwaiter().GetResult();
            var rep = host.ParchaData.AddAsync(FuelType.Petrol, today).GetAwaiter().GetResult();
            var shift = new ShiftData { Name = "احمد", PumpNum = 1, Start = (long)start, End = (long)start, Price = 68, ProfitPer = 2.5m };
            for (var k = 1; k <= 9; k++)
            {
                shift.End = (long)start + k * 3000;
                host.ParchaData.SaveShiftAsync(rep, ShiftKind.Night, shift).GetAwaiter().GetResult();
                Away();
                Go("storage");
                Shot(win, Path.Combine(outDir, $"g{k:00}-storage.png"));
            }
        });

        // ۷) هشدار
        ShotDash("h01-dash-after", true);

        Console.WriteLine("عکس‌ها در " + outDir);
        return 0;
    }

    private static void Shot(Window w, string path)
    {
        Settle(w);
        AppHost.Current.Toasts.Visible = false;
        Settle(w);
        using var frame = w.CaptureRenderedFrame();
        frame?.Save(path);
        Console.WriteLine("  📷 " + Path.GetFileName(path));
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 60; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 1500 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
