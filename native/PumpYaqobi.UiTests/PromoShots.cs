using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ عکس‌های ویدیوی تبلیغاتی — ۱۶:۹، دو برابر ═══════════════════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- promo [پوشه]
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۹): «ویدیوی تبلیغاتی، همه‌چی از خودِ برنامه».
/// همان دادهٔ باورپذیرِ ‎MarketingShots.Fill‎، ولی پنجرهٔ ۱۶۰۰×۹۰۰ (درست ۱۶:۹)
/// و چند صفحهٔ بیشتر: فهرستِ هشدارها، اپِ گوشی، اشتراک، صفحهٔ ورود. ⛔ فقط در
/// دیتابیسِ موقت؛ هیچ چیزی را نمی‌سنجد.
/// </summary>
internal static class PromoShots
{
    public static int Run(string? outDir)
    {
        outDir ??= Path.Combine(Path.GetTempPath(), "pump-promo");
        Directory.CreateDirectory(outDir);
        var dir = Path.Combine(Path.GetTempPath(), "pump-promo-" + Guid.NewGuid().ToString("N"));
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
        MarketingShots.Fill(AppHost.Current);
        Settle(win);

        SectionViewModel? By(string id) => vm.Sections.FirstOrDefault(x => x.Id == id);

        void Try(string name, Action a)
        {
            try { a(); }
            catch (Exception e) { Console.WriteLine("  ✖ " + name + ": " + e.Message); }
        }

        foreach (var theme in new[] { PumpTheme.Blue, PumpTheme.Gold })
        {
            ThemeManager.Apply(theme);
            Settle(win);
            var t = theme == PumpTheme.Blue ? "light" : "dark";

            Try("debt", () =>
            {
                if (By("debt") is not DebtSectionViewModel debt) return;
                Wait(win, vm.GoAsync(debt));
                Shot(win, Path.Combine(outDir, $"{t}-debtors.png"));
                var first = AppHost.Current.Debtors.ListAsync().GetAwaiter().GetResult().First();
                Wait(win, debt.OpenPersonAsync(first.Id));
                Shot(win, Path.Combine(outDir, $"{t}-account.png"));
                debt.CloseOpenPage();
                Settle(win);
            });

            foreach (var (id, name) in new[]
            {
                ("shifts", "parcha"), ("storage", "storage"), ("profit", "profit"), ("noinv", "companies"),
                ("invoices", "invoices"), ("safe", "safe"), ("attendance", "staff"), ("history", "history"),
                ("expenses", "expenses"), ("sarrafi", "sarrafi"), ("settings", "settings"),
            })
                Try(id, () =>
                {
                    if (By(id) is not { } sec) return;
                    Wait(win, vm.GoAsync(sec));
                    Shot(win, Path.Combine(outDir, $"{t}-{name}.png"));
                });

            Try("waraq", () =>
            {
                if (By("waraq") is not WaraqSectionViewModel waraq) return;
                Wait(win, vm.GoAsync(waraq));
                Shot(win, Path.Combine(outDir, $"{t}-waraq-list.png"));
                var card = waraq.Cards.FirstOrDefault();
                if (card is not null && waraq.GetType().GetMethod("OpenCardAsync",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?.Invoke(waraq, new object?[] { card }) is Task open)
                    Wait(win, open);
                Shot(win, Path.Combine(outDir, $"{t}-waraq.png"));
                waraq.CloseOpenPage();
                Settle(win);
            });

            foreach (var (id, name) in new[] { ("apps", "apps"), ("vip", "vip"), ("backups", "backups") })
                Try(id, () =>
                {
                    var parent = vm.Sections.FirstOrDefault(p => p.SubSections.Any(s => s.Id == id));
                    if (parent is null) return;
                    var sub = parent.SubSections.First(s => s.Id == id);
                    Wait(win, vm.GoAsync(parent));
                    parent.OpenSub = sub;
                    Settle(win);
                    Shot(win, Path.Combine(outDir, $"{t}-{name}.png"));
                    parent.OpenSub = null;
                    Settle(win);
                });

            Try("dashboard", () =>
            {
                if (By("dashboard") is not DashboardSectionViewModel dash) return;
                Wait(win, vm.GoAsync(dash));
                Wait(win, dash.RefreshAsync());
                Shot(win, Path.Combine(outDir, $"{t}-dashboard.png"));
                dash.BellCommand.Execute(null);
                Settle(win);
                Shot(win, Path.Combine(outDir, $"{t}-alerts.png"));
                dash.AlertsOpen = false;
                Settle(win);
            });
        }

        ThemeManager.Apply(PumpTheme.Blue);
        Settle(win);
        Try("login", () =>
        {
            if (By("account") is not AccountSectionViewModel a) return;
            Wait(win, vm.GoAsync(a));
            Shot(win, Path.Combine(outDir, "light-login.png"));
        });

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
