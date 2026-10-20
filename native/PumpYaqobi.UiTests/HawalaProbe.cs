using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ ⛽ تیل چکنهٔ حواله — با پنجرهٔ واقعی (۱۴۰۵/۰۷/۲۵) ═══════════════════════════
/// حساب باز ⇒ دکمه ⇒ صفحهٔ جدا ⇒ حوالهٔ ۵۰ لیتری ⇒ ردیف‌های همان حساب با همان حواله
/// کم می‌شوند ⇒ از ۷۰٪ نقطهٔ سرخ ⇒ حسابِ دیگر با همان شماره دست نمی‌خورد.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- hawala [پوشهٔ عکس]
/// </summary>
internal static class HawalaProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string? shots)
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-hawala-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Pump(win);
        Seed.Fill(AppHost.Current);

        var debt = vm.Sections.OfType<DebtSectionViewModel>().First();
        Wait(win, vm.GoAsync(debt));
        Wait(win, debt.RefreshAsync());
        var cards = debt.Cards.Take(2).ToList();
        if (cards.Count < 2) { Check("دو قرض‌دار", false); return 1; }

        // ── حسابِ اول: قرض‌دارِ تازه با رسیدِ ۱۰۰ لیتر پطرول (حسابِ «تمام‌نشده») ──
        //  ⚠️ قرض‌دارِ نمونه بیشتر از رسیدش برده و حسابش «تمام شده» است — آن‌جا حواله از
        //  همان اول «سوخت» است (بارِ دومِ ۱۴۰۵/۰۷/۲۵)، پس این‌جا حسابِ پاک لازم است.
        debt.NewName = "ادارهٔ آزمونِ حواله";
        Wait(win, debt.AddDebtorCommand.ExecuteAsync(null));
        var freshId = debt.Cards.First(c => c.Name == "ادارهٔ آزمونِ حواله").Entity.Id;
        Open(win, debt, freshId);
        var acct = debt.Person!.Current!;
        Wait(win, acct.AddRowsAsync(5));
        Pump(win, 20);
        var rows = acct.Rows.OfType<DebtRowViewModel>().Where(r => string.IsNullOrWhiteSpace(r.Name)).Take(5).ToList();
        Check("پنج ردیفِ تازه", rows.Count == 5, rows.Count.ToString());
        rows[4].Name = "رسیدِ ادارهٔ دولتی"; rows[4].RasidFuelText = "100";
        Wait(win, rows[4].FlushAsync());
        Wait(win, acct.LoadArchiveCountAsync());

        Wait(win, acct.OpenHawalasCommand.ExecuteAsync(null));
        Pump(win, 20);
        var page = debt.Overlay as HawalaPageViewModel;
        Check("صفحهٔ تیل چکنهٔ حواله باز شد", page is not null && win.GetVisualDescendants().OfType<PumpYaqobi.App.Views.Sections.HawalaView>().Any(v => v.IsEffectivelyVisible));
        if (page is null) return 1;

        page.NewNumber = "۴۲"; page.NewLiters = "50"; page.NewNote = "ریاستِ معارف";
        Wait(win, page.AddCommand.ExecuteAsync(null));
        Pump(win, 10);
        Check("حواله ثبت شد", page.Items.Count == 1 && page.Items[0].Entity.Number == "42", page.Items.FirstOrDefault()?.Title);
        page.NewNumber = "42"; page.NewLiters = "10";
        Wait(win, page.AddCommand.ExecuteAsync(null));
        Check("حوالهٔ تکراری در همان حساب پذیرفته نشد", page.Items.Count == 1);
        Check("بی برداشت ⇒ بی نقطهٔ سرخ", !acct.HawalaAlert && !page.Items[0].Alert);
        Wait(win, page.CloseCommand.ExecuteAsync(null));
        Pump(win, 10);

        // ── برداشت‌ها: 5 + 15 = 20 (۴۰٪) ⇒ باز؛ + 16 = 36 (۷۲٪) ⇒ سرخ ──
        void Take(DebtRowViewModel r, string name, string liters)
        {
            //  ⛔ با فی، مثلِ دفترِ واقعی (۱۴۰۵/۰۷/۲۸): بردگی هست و رسیدِ پول نیست ⇒ دفترِ پول
            //  «تمام» است — و حواله نباید از آن «قرض‌دار تیل ندارد» بخواند.
            r.Name = name; r.Hawala = "42"; r.LitersText = liters; r.PriceText = "68";
            Wait(win, r.FlushAsync());
        }
        Take(rows[0], "روزِ اول", "5");
        Take(rows[1], "روزِ دوم", "15");
        Wait(win, acct.LoadArchiveCountAsync());
        Check("۲۰ از ۵۰ ⇒ هنوز بی نقطهٔ سرخ", !acct.HawalaAlert);
        Take(rows[2], "روزِ سوم", "16");
        Wait(win, acct.LoadArchiveCountAsync());
        Check("۳۶ از ۵۰ (۷۲٪) ⇒ نقطهٔ سرخ روی دکمه", acct.HawalaAlert);

        Wait(win, acct.OpenHawalasCommand.ExecuteAsync(null));
        Pump(win, 20);
        page = (HawalaPageViewModel)debt.Overlay!;
        var it = page.Items[0];
        Check("برداشت ۳۶ و مانده ۱۴", it.UsedText.StartsWith("36") && it.LeftText.StartsWith("14"), it.UsedText + " / " + it.LeftText);
        Check("سه برداشت با نام و تاریخ", it.Takings.Count == 3, it.TakeCountText);
        Check("حسابِ قرض‌دار هنوز تیل دارد ⇒ حواله نسوخته", !it.IsBurned, it.StateText);
        it.IsOpen = true; Pump(win, 10);
        if (shots is not null) { Directory.CreateDirectory(shots); Shot(win, shots, "hawala-70"); }

        // ── «تیلِ قرض‌دار تمام شد ⇒ حواله هم تمام، مانده سوخت» ──
        Wait(win, page.CloseCommand.ExecuteAsync(null));
        rows[3].Name = "بی حواله"; rows[3].LitersText = "70";      // ۳۶ + ۷۰ = ۱۰۶ از رسیدِ ۱۰۰
        Wait(win, rows[3].FlushAsync());
        Wait(win, acct.OpenHawalasCommand.ExecuteAsync(null));
        Pump(win, 20);
        page = (HawalaPageViewModel)debt.Overlay!;
        it = page.Items[0];
        Check("حسابِ قرض‌دار تمام شد ⇒ حواله «سوخت»", it.IsBurned && it.IsDone && it.Alert && it.LeftText.Contains("سوخت"),
              it.StateText + " · " + it.LeftText);
        if (shots is not null) Shot(win, shots, "hawala-burned");
        Wait(win, page.CloseCommand.ExecuteAsync(null));
        Check("نقطهٔ سرخ روی دکمه", acct.HawalaAlert);
        rows[3].LitersText = ""; rows[3].Name = "";
        Wait(win, rows[3].FlushAsync());
        Wait(win, acct.OpenHawalasCommand.ExecuteAsync(null));
        Pump(win, 20);
        page = (HawalaPageViewModel)debt.Overlay!;
        it = page.Items[0];
        Check("تیل به حساب برگشت ⇒ حواله دوباره باز (۷۲٪)", !it.IsBurned && !it.IsDone, it.StateText);

        // تمام شد
        it.LitersEdit = "36";
        Wait(win, it.SaveLitersCommand.ExecuteAsync(null));
        Pump(win, 10);
        it = page.Items[0];
        Check("مقدار ۳۶ شد ⇒ «تمام شد»", it.IsDone && it.StateText.Contains("تمام"), it.StateText);
        if (shots is not null) Shot(win, shots, "hawala-done");
        Wait(win, page.CloseCommand.ExecuteAsync(null));
        Pump(win, 10);
        Check("دکمه هنوز سرخ (تمام‌شده)", acct.HawalaAlert);
        //  «بغلِ فیصدی، پایین نیاید، همه تراز» — همان خط و همان بالا/پایین
        var btns = win.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToList();
        var pct = btns.FirstOrDefault(b => (b.Content as string)?.Contains("فیصدی") == true);
        var hw = btns.FirstOrDefault(b => b.Command == acct.OpenHawalasCommand);
        var rp = pct?.TranslatePoint(new Point(0, 0), win); var rh = hw?.TranslatePoint(new Point(0, 0), win);
        Check("دکمهٔ حواله هم‌خطِ «٪ فیصدی» است", rp is { } p1 && rh is { } p2 && Math.Abs(p1.Y - p2.Y) < 3
              && Math.Abs(pct!.Bounds.Height - hw!.Bounds.Height) < 3,
              $"فیصدی y={rp?.Y:0} h={pct?.Bounds.Height:0} · حواله y={rh?.Y:0} h={hw?.Bounds.Height:0}");
        if (shots is not null) Shot(win, shots, "hawala-button");

        // ── حسابِ دوم، همان شماره، بی برداشت ⇒ جدا ──
        Wait(win, debt.BackCommand.ExecuteAsync(null));
        Wait(win, debt.RefreshAsync());
        Check("🔴 نقطهٔ سرخِ حواله سرِ کارتِ قرض‌دار", debt.Cards.First(c => c.Entity.Id == freshId).HawalaAlert);
        Pump(win, 10);
        if (shots is not null) Shot(win, shots, "hawala-card");
        Open(win, debt, cards[1].Entity.Id);
        var acct2 = debt.Person!.Current!;
        Wait(win, acct2.OpenHawalasCommand.ExecuteAsync(null));
        Pump(win, 10);
        var page2 = (HawalaPageViewModel)debt.Overlay!;
        page2.NewNumber = "42"; page2.NewLiters = "50";
        Wait(win, page2.AddCommand.ExecuteAsync(null));
        Check("حسابِ دیگر با همان شماره: برداشتِ صفر", page2.Items.Count == 1 && page2.Items[0].UsedText.StartsWith("0"), page2.Items.FirstOrDefault()?.UsedText);
        Wait(win, page2.CloseCommand.ExecuteAsync(null));

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Open(Window win, DebtSectionViewModel debt, long id)
    {
        var card = debt.Cards.First(c => c.Entity.Id == id);
        debt.OpenCommand.Execute(card);
        for (var i = 0; i < 200 && !(debt.PersonOpen && debt.Person?.Accounts.FirstOrDefault()?.Entity.MainOfDebtorId == id); i++)
            Pump(win);
        Pump(win, 20);
    }

    private static void Shot(Window win, string dir, string name)
    {
        Pump(win, 20);
        using var f = win.CaptureRenderedFrame();
        var path = Path.Combine(dir, name + ".png");
        f?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        Pump(w);
    }

    private static void Pump(Window w, int n = 8)
    { for (var i = 0; i < n; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
