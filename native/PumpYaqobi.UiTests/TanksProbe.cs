using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ مخزن‌های شماره‌دار با پنجرهٔ واقعی (۱۴۰۵/۰۷/۲۲) ══
///   • دو مخزن ساخته می‌شود؛ خریدِ اول میانشان تقسیم می‌شود؛ جمعِ مخزن‌ها = موجودیِ کارتِ بالا
///   • یک مخزن «در حالِ کشیدن» است؛ کارت‌ها و کادرِ تقسیم دیده می‌شوند؛ داشبورد همه را نشان می‌دهد
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- tanks [پوشهٔ عکس]
/// </summary>
internal static class TanksProbe
{
    private static int _bad;
    private static void Check(string what, bool ok, string? d = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (d is null ? "" : " — " + d));
        if (!ok) _bad++;
    }

    public static int Run(string? shots)
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-tanks-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1500, Height = 1000 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock); Pump(win);
        Seed.Fill(AppHost.Current);
        if (shots is not null) Directory.CreateDirectory(shots);

        var st = (StorageSectionViewModel)vm.Sections.First(s => s.Id == "storage");
        Wait(win, vm.GoAsync(st)); Settle(win);
        Check("بی مخزن ⇒ هیچ کارتی", st.Tanks.Count == 0 && !st.HasTanks);

        st.TankNewCapacity = "10000"; Wait(win, st.AddTankCommand.ExecuteAsync(null)); Settle(win);
        st.TankNewCapacity = "8000"; Wait(win, st.AddTankCommand.ExecuteAsync(null)); Settle(win);
        Check("دو مخزن ساخته شد (۱ و ۲)", st.Tanks.Count == 2 && st.Tanks[0].Entity.Num == 1 && st.Tanks[1].Entity.Num == 2,
              string.Join(",", st.Tanks.Select(t => t.Entity.Num)));

        var buy = st.PurchaseCards.FirstOrDefault();
        Check("خریدی برای تقسیم هست", buy is not null);
        if (buy is not null)
        {
            Wait(win, st.OpenSplitCommand.ExecuteAsync(buy)); Settle(win);
            Check("کادرِ تقسیم با یک خانه برای هر مخزن", st.SplitFor is not null && st.SplitShares.Count == 2);
            var half = Math.Round(buy.Entity.Liters / 2m, 2);
            st.SplitShares[1].Liters = (buy.Entity.Liters + 100m).ToString(System.Globalization.CultureInfo.InvariantCulture); Settle(win);
            Check("بیشتر از خرید ⇒ سرخ و ثبت بسته", st.SplitOver, st.SplitRemainText);
            st.SplitShares[1].Liters = half.ToString(System.Globalization.CultureInfo.InvariantCulture); Settle(win);
            Check("باقی‌مانده به مخزنِ اول گفته می‌شود", !st.SplitOver && st.SplitRemainText.Contains("مخزنِ اول"), st.SplitRemainText);
            if (shots is not null)
            {
                var box = win.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == st.SplitTitle);
                box?.BringIntoView(); Settle(win);
                Shot(win, shots, "tanks-split");
            }
            Wait(win, st.SaveSplitCommand.ExecuteAsync(null)); Settle(win);
            Check("تقسیم ثبت شد و کادر بسته شد", st.SplitFor is null);
        }

        var sum = st.Tanks.Sum(t => Shamsi.Num(t.CurrentText.Replace(" لیتر", "")));
        var cur = Shamsi.Num(st.Current);
        Check("جمعِ مخزن‌ها = موجودیِ کارتِ بالا (گرد به لیتر)", Math.Abs(sum - cur) <= 2m, $"{sum} / {cur}");
        Check("یک مخزن در حالِ کشیدن", st.Tanks.Count(t => t.Active) == 1);
        if (shots is not null)
        {
            var title = win.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "🛢️ مخزن‌های شماره‌دار");
            title?.BringIntoView(); Settle(win);
            Shot(win, shots, "tanks");
        }

        var dash = (DashboardSectionViewModel)vm.Sections.First(s => s.Id == "dashboard");
        Wait(win, vm.GoAsync(dash)); Settle(win);
        var rows = dash.FuelStatus.Select(r => r.Name).ToList();
        Check("داشبورد: هر مخزنِ شماره‌دار ردیفِ خودش را دارد، کنارِ تیلش",
              rows.Any(r => r.Contains("مخزنِ 1")) && rows.Any(r => r.Contains("مخزنِ 2")), string.Join(" | ", rows));
        if (shots is not null)
        {
            var row = win.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => (t.Text ?? "").Contains("مخزنِ 2"));
            row?.BringIntoView(); Settle(win);
            Shot(win, shots, "tanks-dashboard");
        }

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Shot(Window win, string dir, string name)
    {
        using var f = win.CaptureRenderedFrame();
        f?.Save(Path.Combine(dir, name + ".png"));
    }
    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Pump(w);
    }
    private static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
    private static void Settle(Window w) { for (var i = 0; i < 20; i++) { Pump(w); Thread.Sleep(5); } }
}
