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
internal static class Story3Shots
{
    public static int Run(string? outDir)
    {
        outDir ??= Path.Combine(Path.GetTempPath(), "pump-story3");
        Directory.CreateDirectory(outDir);
        var dir = Path.Combine(Path.GetTempPath(), "pump-story3-" + Guid.NewGuid().ToString("N"));
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

        //  ── هر عکس دو بار: روشن و تیره (برای جابه‌جاییِ دارک‌مود وسطِ تایپ)
        void Both(string name, Action<string> shoot)
        {
            ThemeManager.Apply(PumpTheme.Blue); Settle(win); shoot(name + "-light");
            ThemeManager.Apply(PumpTheme.Gold); Settle(win); shoot(name + "-dark");
            ThemeManager.Apply(PumpTheme.Blue); Settle(win);
        }

        // ۱) پارچه: هر دو شیفت پر، روزِ تازه با شروع و ختم
        Try("parcha", () =>
        {
            if (By("shifts") is not ParchaSectionViewModel ps) return;
            Away();
            Wait(win, vm.GoAsync(ps));
            ps.IsDiesel = false; Settle(win);
            ps.NewParchaDayCommand.Execute(null); Settle(win);
            var start = host.ParchaData.LastBaseAsync(FuelType.Petrol, 1).GetAwaiter().GetResult();
            ps.Day.Name = "احمد رحیمی"; ps.Day.PumpNum = "1"; ps.Day.Price = "68";
            ps.Day.Start = ((long)start).ToString(); ps.Day.End = ((long)start + 4200).ToString();
            ps.Day.Debt = "10200";
            Settle(win);
            Both("p1-parcha", n => Shot(win, Path.Combine(outDir, n + ".png")));
            Wait(win, ps.Day.SaveCommand.ExecuteAsync(null));
        });
        Try("post1", () => host.WaraqPosting.SyncAsync(waraq.Id).GetAwaiter().GetResult());

        // ۲) ورق: همهٔ ردیف‌های قرض و مصرف پر می‌شوند
        Try("fill", () =>
        {
            var names = host.Debtors.ListAsync().GetAwaiter().GetResult().Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(12).ToList();
            var exp = new[] { "روغنِ جنراتور", "نانِ کارمندان", "برق", "ترمیمِ پایه", "کرایهٔ موتر", "چای و آب" };
            var rnd = new Random(7);
            var w = host.WaraqData.LoadAsync(waraq.Id).GetAwaiter().GetResult()!;
            foreach (var sh in w.Shifts)
            {
                var k = 0;
                foreach (var t in sh.Transactions.OrderBy(x => x.SortIndex))
                {
                    if (!string.IsNullOrWhiteSpace(t.Name) || t.Amount != 0 || t.Liters != 0) { k++; continue; }
                    if (k % 4 == 3)
                    {
                        t.Name = exp[k % exp.Length]; t.Liters = 0; t.Amount = 500 + rnd.Next(1, 40) * 100;
                        t.Type = WaraqTxnType.Expense; t.AmountAuto = false;
                    }
                    else
                    {
                        t.Name = k == 0 && sh.Kind == ShiftKind.Day ? "سید جلال هاشمی" : names[k % names.Count];
                        t.Liters = k == 0 && sh.Kind == ShiftKind.Day ? 150 : 20 + rnd.Next(0, 18) * 10;
                        t.Amount = t.Liters * 68; t.Type = WaraqTxnType.Debt; t.AmountAuto = true;
                        t.Fuel = rnd.Next(0, 4) == 0 ? FuelType.Diesel : FuelType.Petrol;
                    }
                    host.WaraqData.SaveTxnAsync(t).GetAwaiter().GetResult();
                    k++;
                }
            }
            Console.WriteLine("  ثبت: " + host.WaraqPosting.SyncAsync(waraq.Id).GetAwaiter().GetResult());
        });
        Both("p2-waraq", n => ShotWaraq(n));
        Both("p3-expenses", n => ShotSection("expenses", n));
        Both("p4-safe", n => ShotSection("safe", n));
        Both("p5-account", n => ShotAccount(n));
        Both("p6-storage", n => ShotSection("storage", n));
        Both("p8-dash", n => ShotDash(n, true));

        // ۳) مخزن خالی می‌شود
        Try("drain", () =>
        {
            var start = host.ParchaData.LastBaseAsync(FuelType.Petrol, 1).GetAwaiter().GetResult();
            var rep = host.ParchaData.AddAsync(FuelType.Petrol, today).GetAwaiter().GetResult();
            var shift = new ShiftData { Name = "احمد", PumpNum = 1, Start = (long)start, End = (long)start, Price = 68, ProfitPer = 2.5m };
            for (var k = 1; k <= 9; k++)
            {
                shift.End = (long)start + k * 3000;
                host.ParchaData.SaveShiftAsync(rep, ShiftKind.Night, shift).GetAwaiter().GetResult();
                Away(); Go("storage");
                Shot(win, Path.Combine(outDir, $"p7-drain-{k:00}.png"));
            }
        });
        Both("p9-alerts", n => ShotDash(n, true));

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
