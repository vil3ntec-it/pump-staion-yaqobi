using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using PumpYaqobi.App;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Domain;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ فهرستِ هفت‌تاییِ ۱۴۰۵/۰۷/۱۶ — با پنجرهٔ واقعی و عکس ═════════════════════
///
/// <code>dotnet run --project PumpYaqobi.UiTests -c Release -- round16 [پوشهٔ عکس]</code>
///
///   ۱) تاریخِ سربرگ نمایشی است: «ثبت» فقط سربرگ را عوض می‌کند، هیچ هشداری نیست
///   ۳) کارت‌های فاکتور (در صف · تایید شده) جمع‌وجور
///   ۶) توضیحِ بخش بغلِ کادرها
///   ۷) نوارِ چهار عددِ بالا با کلیدِ داشبورد روشن/خاموش — عددها دست نمی‌خورند
/// </summary>
public static class Round16Probe
{
    private static int _bad;

    private static void Check(string what, bool ok, string detail = "")
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}{(detail.Length > 0 ? " — " + detail : "")}");
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-round16");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-r16-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Round14Probe.Settle(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Round14Probe.Settle(win);
        MarketingShots.Fill(AppHost.Current);
        Round14Probe.Settle(win);

        Banner(win, vm, shots);
        Invoices(win, vm, shots);
        Subtitles(win, vm, shots);
        Clock(win, vm, shots);
        UpdateOffer(shots);
        Dark(win, vm, shots);

        Console.WriteLine(_bad == 0 ? "✅ همهٔ بندها سبزند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    // ══ ۷) نوارِ چهار عدد: روشن/خاموش از داشبورد ═══════════════════════════
    private static void Banner(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۷) نوارِ عددهای بالا ════");
        var dash = (DashboardSectionViewModel)vm.Sections.First(s => s.Id == "dashboard");
        Round14Probe.Wait(win, vm.GoAsync(dash));
        Round14Probe.Wait(win, vm.RefreshBannerAsync());
        Round14Probe.Settle(win);
        var block = win.FindControl<Border>("BannerBlock")!;
        var nav = win.FindControl<Border>("NavBar")!;
        var values = vm.Banner.Select(b => b.Value).ToList();
        Check("روشن: نوار دیده می‌شود", block.IsEffectivelyVisible && vm.IsBannerVisible);
        var box = win.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(c => (c.Content as string) == "نوارِ عددهای بالا");
        Check("کلیدِ کوچک در داشبورد هست", box is not null && box.IsEffectivelyVisible);
        var navYOn = nav.TranslatePoint(new Point(0, 0), win)?.Y ?? -1;
        Round14Probe.Shot(win, shots, "r16-banner-on");

        box!.IsChecked = false;
        Round14Probe.Settle(win);
        var navYOff = nav.TranslatePoint(new Point(0, 0), win)?.Y ?? -1;
        Check("خاموش: نوار پنهان شد", !block.IsEffectivelyVisible && !vm.IsBannerVisible);
        Check("و نوارِ بخش‌ها بالا آمد (جای خالی نماند)", navYOff < navYOn - 30, $"{navYOn:0} ⇒ {navYOff:0}");
        Check("⛔ عددها همان‌اند — فقط دیدن عوض شد", vm.Banner.Select(b => b.Value).SequenceEqual(values));
        Round14Probe.Shot(win, shots, "r16-banner-off");
        for (var i = 0; i < 150 && AppSettings.Load().ShowBanner; i++) { Round14Probe.Pump(win); Thread.Sleep(10); }
        Check("انتخاب ذخیره شد", !AppSettings.Load().ShowBanner);

        box.IsChecked = true;
        Round14Probe.Settle(win);
        Check("روشنِ دوباره: نوار برگشت", block.IsEffectivelyVisible && vm.IsBannerVisible);
        Check("و نوارِ بخش‌ها سرِ جای قبلی", Math.Abs((nav.TranslatePoint(new Point(0, 0), win)?.Y ?? -1) - navYOn) < 1);
    }

    // ══ ۳) فاکتورها ═══════════════════════════════════════════════════════
    private static void Invoices(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۳) فاکتورها ════");
        var inv = (InvoiceSectionViewModel)vm.Sections.First(s => s.Id == "invoices");
        Round14Probe.Wait(win, vm.GoAsync(inv));
        Round14Probe.Settle(win);
        Round14Probe.Shot(win, shots, "r16-invoices-form");
        foreach (var which in new[] { "pending", "approved" })
        {
            inv.OpenListCommand.Execute(which);
            Round14Probe.Settle(win);
            var cards = win.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Classes.Contains("inv-row") && b.IsEffectivelyVisible && b.Bounds.Height > 0).ToList();
            Check($"{which}: ردیف‌ها دیده می‌شوند", cards.Count > 0, cards.Count + " ردیف");
            var tallest = cards.Count == 0 ? 0 : cards.Max(c => c.Bounds.Height);
            Check($"{which}: هر فاکتور جمع‌وجور (زیرِ ۶۴ پیکسل)", tallest is > 0 and < 64, $"{tallest:0} پیکسل");
            Round14Probe.Shot(win, shots, "r16-invoices-" + which);

            //  ⛔ خودِ برگهٔ فاکتور — «این بخشش را گفتم» (عکسِ صاحب ریپو)
            var row = inv.Rows.FirstOrDefault();
            if (row is null) continue;
            inv.OpenDetailCommand.Execute(row);
            Round14Probe.Settle(win);
            var sheet = win.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(b => b.IsEffectivelyVisible && b.MaxWidth == 640);
            var title = win.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(t => t.IsEffectivelyVisible && (t.Text ?? "").StartsWith("فاکتور ") && t.FontSize == 16);
            Check($"{which}: برگهٔ فاکتور عنوان دارد (پیش از این خطِ خالی بود)", title is not null && title.Text!.Length > 7, title?.Text ?? "هیچ");
            Check($"{which}: برگهٔ فاکتور جمع‌وجور (زیرِ ۴۲۰ پیکسل، پهنا ≤ ۶۴۰)",
                  sheet is not null && sheet.Bounds.Height < 420 && sheet.Bounds.Width <= 641,
                  $"{sheet?.Bounds.Width:0}×{sheet?.Bounds.Height:0}");
            Round14Probe.Shot(win, shots, "r16-invoice-detail-" + which);
            inv.BackCommand.Execute(null);
            Round14Probe.Settle(win);
        }
        inv.CloseListCommand.Execute(null);
        Round14Probe.Settle(win);
    }

    // ══ ۶) توضیحِ بخش بغلِ کادرها — در چند بخش ═══════════════════════════
    private static void Subtitles(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۶) توضیحِ بخش‌ها ════");
        foreach (var id in new[] { "waraq", "expenses", "sarrafi", "safe", "debtrasid", "rasid" })
        {
            var sec = vm.Sections.First(s => s.Id == id);
            Round14Probe.Wait(win, vm.GoAsync(sec));
            Round14Probe.Settle(win);
            var sub = win.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(t => t.Classes.Contains("sec-sub") && t.IsEffectivelyVisible);
            if (sub is null) { Console.WriteLine($"  · {id}: توضیحی ندارد"); continue; }
            var head = sub.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("card-head"));
            Check($"{id}: سربرگ جمع‌وجور (زیرِ ۱۱۰ پیکسل)", head.Bounds.Height < 110, $"{head.Bounds.Height:0} پیکسل");
            Round14Probe.Shot(win, shots, "r16-sub-" + id);
        }
    }

    // ══ ۱) تاریخ و ساعتِ نمایشی — بی هشدار ════════════════════════════════
    private static void Clock(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۱) تاریخ و ساعت ════");
        Check("سربرگ هیچ هشدارِ ⚠️ دربارهٔ ساعت ندارد",
              !win.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == "⚠️"));
        var real = AppClock.Now;
        var cw = new ClockWindow { Width = 560, Height = 480 };
        cw.Show();
        Round14Probe.Settle(cw);
        var cvm = (ClockViewModel)cw.DataContext!;
        cvm.Year += 2; cvm.MonthIndex = 0; cvm.Day = 1; cvm.HourIndex = 8; cvm.MinuteIndex = 0;
        cvm.ApplyCommand.Execute(null);
        Round14Probe.Settle(cw);
        Round14Probe.Settle(win);
        using (var f = cw.CaptureRenderedFrame()) f?.Save(Path.Combine(shots, "r16-clock-window.png"));
        Check("«ثبت» بی اجازهٔ ویندوز: تاریخِ سربرگ همان شد", vm.TodayText == MainViewModel.HeaderDate(cvm.Picked), vm.TodayText.Trim('⁧', '⁩'));
        Check("⛔ ساعتِ برنامه (ماه‌ها، ردیف‌ها، اشتراک) دست نخورد", Math.Abs((AppClock.Now - real).TotalMinutes) < 2);
        Check("جمله می‌گوید فقط نمایش است", cvm.Status.Contains("نمایشی"), cvm.Status);
        Round14Probe.Shot(win, shots, "r16-header-shown-date");
        cvm.RealCommand.Execute(null);
        Round14Probe.Settle(win);
        Check("«ساعتِ واقعی» برگرداند", !DisplayClock.Shifted && vm.TodayText == MainViewModel.HeaderDate(AppClock.Now));
        cw.Close();
    }

    // ══ ۴) پیشنهادِ به‌روزرسانی — عکسِ همان پرسش ══════════════════════════
    private static void UpdateOffer(string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۴) پیشنهادِ به‌روزرسانی ════");
        var info = new PumpYaqobi.App.Update.UpdateInfo(true, "3.1.211", "3.1.212", "https://x", 6_800_000, null, true);
        var dw = DialogWindow.ForConfirm(PumpYaqobi.App.Update.AutoUpdate.OfferTitle,
            PumpYaqobi.App.Update.AutoUpdate.OfferText(info), "⬆️ نصب کن", "بعداً");
        dw.Width = 620; dw.Height = 320;
        dw.Show();
        Round14Probe.Settle(dw);
        using (var f = dw.CaptureRenderedFrame()) f?.Save(Path.Combine(shots, "r16-update-offer.png"));
        Check("پرسش نامِ نسخه و «یک بار» را می‌گوید", PumpYaqobi.App.Update.AutoUpdate.OfferText(info).Contains("3.1.212")
              && PumpYaqobi.App.Update.AutoUpdate.OfferText(info).Contains("دیگر برای همین نسخه پرسیده نمی‌شود"));
        dw.Close();
    }

    // ══ تمِ تیره: همان صفحه‌های تازه ══════════════════════════════════════
    private static void Dark(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ تمِ تیره ════");
        PumpYaqobi.App.Themes.ThemeManager.Apply(PumpYaqobi.App.Themes.PumpTheme.Gold);
        Round14Probe.Settle(win);
        var inv = (InvoiceSectionViewModel)vm.Sections.First(s => s.Id == "invoices");
        Round14Probe.Wait(win, vm.GoAsync(inv));
        Round14Probe.Settle(win);
        Round14Probe.Shot(win, shots, "r16-dark-invoices-form");
        inv.OpenListCommand.Execute("pending");
        Round14Probe.Settle(win);
        if (inv.Rows.FirstOrDefault() is { } row)
        {
            inv.OpenDetailCommand.Execute(row);
            Round14Probe.Settle(win);
            Round14Probe.Shot(win, shots, "r16-dark-invoice-detail");
            inv.BackCommand.Execute(null);
        }
        inv.CloseListCommand.Execute(null);
        Round14Probe.Wait(win, vm.GoAsync(vm.Sections.First(s => s.Id == "dashboard")));
        Round14Probe.Settle(win);
        Round14Probe.Shot(win, shots, "r16-dark-dashboard");
        PumpYaqobi.App.Themes.ThemeManager.Apply(PumpYaqobi.App.Themes.PumpTheme.Blue);
        Round14Probe.Settle(win);
        Check("تمِ تیره: صفحه‌ها بی خطا ساخته شدند", true);
    }

}
