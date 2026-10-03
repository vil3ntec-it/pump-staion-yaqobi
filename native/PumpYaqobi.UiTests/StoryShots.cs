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

        // ۱) پیش از نوشتن
        ShotWaraq("s01-waraq-before");
        ShotAccount("s03-account-before");
        ShotSection("safe", "s05-safe-before");
        ShotDash("s09-dash-before", false);

        // ۲) قرض در ورق نوشته می‌شود — همان کاری که کاربر می‌کند
        Try("write", () =>
        {
            var w = host.WaraqData.LoadAsync(waraq.Id).GetAwaiter().GetResult()!;
            var day = w.Shifts.First(x => x.Kind == ShiftKind.Day);
            var t = day.Transactions.OrderBy(x => x.SortIndex).Skip(5).First();
            t.Name = "سید جلال هاشمی";
            t.Liters = 150;
            t.Amount = 150 * 68;
            t.Type = WaraqTxnType.Debt;
            t.Fuel = FuelType.Petrol;
            t.AmountAuto = true;
            host.WaraqData.SaveTxnAsync(t).GetAwaiter().GetResult();
            var rep = host.WaraqPosting.SyncAsync(waraq.Id).GetAwaiter().GetResult();
            Console.WriteLine("  ثبت به حساب‌ها: " + rep);
        });
        ShotWaraq("s02-waraq-after");
        ShotAccount("s04-account-after");
        ShotSection("safe", "s06-safe-after");

        // ۳) پارچهٔ شب: تیل فروخته می‌شود، مخزن کم می‌شود
        ShotSection("storage", "s08-storage-00");
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
                Shot(win, Path.Combine(outDir, $"s08-storage-{k:00}.png"));
            }
        });
        ShotSection("shifts", "s07-parcha");

        // ۴) هشدار
        ShotDash("s09-dash-after", true);

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
