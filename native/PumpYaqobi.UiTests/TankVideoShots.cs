using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ قاب‌های ویدیوی تبلیغِ مخزن ═════════════════════════════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- tankvideo [پوشه]
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۸): ویدیوی تبلیغاتیِ بخشِ مخزن. همان دادهٔ
/// نمونهٔ <see cref="MarketingShots"/>، در دیتابیسِ موقت. ⛔ هیچ چیزی را
/// نمی‌سنجد و هرگز داخلِ خودِ برنامه نمی‌رود.
/// خروجی: ‎storage.png‎، ‎fill-NNN.png‎ (پر شدنِ نقشه)، ‎buy-NN.png‎ (فرمِ خرید)،
/// ‎dip.png‎، ‎low-NNN.png‎ (پایین رفتن تا زیرِ آستانه)، و ‎gauge.txt‎ (جای نقشه، پیکسل).
/// </summary>
internal static class TankVideoShots
{
    public static int Run(string? outDir)
    {
        outDir ??= Path.Combine(Path.GetTempPath(), "pump-tankvideo");
        Directory.CreateDirectory(outDir);
        var dir = Path.Combine(Path.GetTempPath(), "pump-tv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        LayoutCycleProbe.SetScaling(win, 2.0);
        MarketingShots.Settle(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        MarketingShots.Settle(win);
        MarketingShots.Fill(AppHost.Current);
        MarketingShots.Settle(win);
        ThemeManager.Apply(PumpTheme.Gold);
        MarketingShots.Settle(win);

        if (vm.Sections.FirstOrDefault(x => x.Id == "storage") is not StorageSectionViewModel st)
        { Console.Error.WriteLine("بخشِ مخزن پیدا نشد"); return 1; }
        MarketingShots.Wait(win, vm.GoAsync(st));
        MarketingShots.Shot(win, Path.Combine(outDir, "storage.png"));

        //  همان نقشه‌ای که روی صفحه دیده می‌شود (نقشهٔ دیزل و داشبورد پنهان‌اند)
        var gauge = win.GetVisualDescendants().OfType<TankGauge>()
            .Where(g => g.IsEffectivelyVisible && g.Bounds.Width > 300)
            .OrderByDescending(g => g.Bounds.Width).FirstOrDefault();
        if (gauge is not null)
        {
            var p = gauge.TranslatePoint(new Point(0, 0), win) ?? new Point();
            File.WriteAllText(Path.Combine(outDir, "gauge.txt"),
                $"{(int)(p.X * 2)} {(int)(p.Y * 2)} {(int)(gauge.Bounds.Width * 2)} {(int)(gauge.Bounds.Height * 2)}");
            var target = gauge.FillPercent;
            var targetText = gauge.FillText;
            var threshold = gauge.ThresholdPercent;
            const int n = 48;
            for (var i = 0; i <= n; i++)
            {
                var t = (double)i / n;
                var v = target * (1 - Math.Pow(1 - t, 3));          // آرام می‌نشیند
                gauge.FillPercent = v;
                gauge.FillText = v.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                MarketingShots.Shot(win, Path.Combine(outDir, $"fill-{i:000}.png"));
            }
            var low = Math.Max(2, threshold - 6);
            for (var i = 0; i <= n; i++)
            {
                var v = target + (low - target) * ((double)i / n);
                gauge.FillPercent = v;
                gauge.FillText = v.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                MarketingShots.Shot(win, Path.Combine(outDir, $"low-{i:000}.png"));
            }
            //  ⚠️ ClearValue بایند را هم می‌بُرد و نقشه ۰٪ می‌ماند — همان عددِ واقعی برمی‌گردد
            gauge.FillPercent = target;
            gauge.FillText = targetText;
            MarketingShots.Settle(win);
        }

        // فرمِ «ثبت خرید» — تایپِ گام‌به‌گام، حساب‌ها خودکار
        st.OpenBuyCommand.Execute(null);
        MarketingShots.Settle(win);
        var k = 0;
        MarketingShots.Shot(win, Path.Combine(outDir, $"buy-{k++:00}.png"));
        void Type(Action<string> set, string full)
        {
            for (var c = 1; c <= full.Length; c++)
            {
                set(full[..c]);
                MarketingShots.Shot(win, Path.Combine(outDir, $"buy-{k++:00}.png"));
            }
        }
        Type(s => st.BuySeller = s, "شرکت آریانا");
        Type(s => st.BuyKg = s, "26800");
        Type(s => st.BuyDensity = s, "0.745");
        Type(s => st.BuyPriceTon = s, "708");
        Type(s => st.BuyUsdRate = s, "71");
        st.BuyOpen = false;
        MarketingShots.Settle(win);

        st.OpenDipCommand.Execute(null);
        MarketingShots.Settle(win);
        MarketingShots.Shot(win, Path.Combine(outDir, "dip.png"));

        Console.WriteLine("قاب‌ها در " + outDir);
        return 0;
    }
}
