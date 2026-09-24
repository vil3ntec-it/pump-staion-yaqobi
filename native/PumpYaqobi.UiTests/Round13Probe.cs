using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ فهرستِ نُه‌تاییِ صاحب ریپو (۱۴۰۵/۰۷/۱۳) — با پنجرهٔ واقعی ═══════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- round13 [پوشهٔ عکس]
/// </summary>
internal static class Round13Probe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-round13");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-round13-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        AppHost.Start(Path.Combine(dir, "pump.db"));
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
        Settle(win);
        var h = AppHost.Current;

        Storage(win, vm, h, shots);
        Companies(win, vm, h, shots);
        Gaps(win, vm, h, shots);
        ParchaHistory(win, vm, shots);

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ همهٔ بندها سبزند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    // ══ ۱) مخزن ═══════════════════════════════════════════════════════════════
    private static void Storage(Window win, MainViewModel vm, AppHost h, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۱) مخزن — وزن به کیلو ════");
        var st = (StorageSectionViewModel)vm.Sections.First(s => s.Id == "storage");
        Wait(win, vm.GoAsync(st));
        st.OpenBuyCommand.Execute(null);
        st.BuySeller = "شرکتِ آزمون";
        st.BuyKg = "1000"; st.BuyDensity = "0.7435"; st.BuyPriceTon = "1200"; st.BuyUsdRate = "66";
        Pump(win);
        Check("۱۰۰۰ کیلو ⇒ «۱ تن»", st.BuyTonText == "1 تن", st.BuyTonText);
        Check("۱۲۰۰ دالر", st.BuyUsdText == "1,200.00 $", st.BuyUsdText);
        Check("۷۹٬۲۰۰ افغانی", st.BuyAfnText == "79,200 افغانی", st.BuyAfnText);
        Check("۱۳۴۵ لیتر", st.BuyLitersText == "1,345 لیتر", st.BuyLitersText);
        Shot(win, shots, "01-storage-buy");
        Wait(win, st.SaveBuyCommand.ExecuteAsync(null));
        Settle(win);
        var p = st.Purchases.FirstOrDefault();
        Check("کارتِ خرید: «۱,۰۰۰ کیلو» و «۱ تن»", p is not null && p.KgShowText == "1,000 کیلو" && p.TonText == "1",
              p is null ? "—" : p.KgShowText + " / " + p.TonText);
        Check("و هشدارِ «هزار برابر» روشن نیست", p is not null && !p.Implausible);
        Shot(win, shots, "02-storage-card");
    }

    // ══ ۵) شرکت‌ها ════════════════════════════════════════════════════════════
    private static void Companies(Window win, MainViewModel vm, AppHost h, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۵) شرکت‌ها — آرشیوِ هر دو تیل ════");
        var comp = h.Companies.AddAsync("شرکتِ آرشیو").GetAwaiter().GetResult();
        var month = Shamsi.ThisMonth();
        for (var i = 1; i <= 6; i++)
            h.Companies.SaveRowAsync(new CompanyRow
            {
                CompanyId = comp.Id, Fuel = i <= 3 ? FuelType.Petrol : FuelType.Diesel, SortIndex = i,
                DateShamsi = $"{month}/{i:00}", Name = "خریدِ " + i, Ton = 20 + i, Usd = 700, Rate = 70,
                Poul = i % 2 == 0 ? 100000 : 0,
            }).GetAwaiter().GetResult();

        var cs = (CompanySectionViewModel)vm.Sections.First(s => s.Id == "noinv");
        Wait(win, vm.GoAsync(cs));
        Wait(win, cs.RefreshAsync());
        var card = cs.Cards.FirstOrDefault(c => c.Name == "شرکتِ آرشیو");
        Check("کارتِ شرکت هست", card is not null);
        if (card is null) return;
        cs.OpenCommand.Execute(card);
        Settle(win);
        var page = cs.Page!;
        Check("جدولِ پطرول ۳ ردیف", page.Rows.Count == 3, page.Rows.Count.ToString());
        Check("⛔ ستونِ «📦» نیست", win.GetVisualDescendants().OfType<DataGridColumnHeader>()
              .All(x => (x.Content as string) != "📦"));

        Dialogs.ConfirmHook = (_, _) => true;
        Wait(win, page.NewTableCommand.ExecuteAsync(null));
        page.IsDiesel = true;
        Settle(win);
        Check("جدولِ دیزل ۳ ردیف", page.Rows.Count == 3, page.Rows.Count.ToString());
        Wait(win, page.NewTableCommand.ExecuteAsync(null));
        Dialogs.ConfirmHook = null;
        Settle(win);

        var arcs = h.Companies.ListArchivesAsync(comp.Id).GetAwaiter().GetResult();
        Check("دو آرشیو روی دیسک: یکی پطرول، یکی دیزل",
              arcs.Count == 2 && arcs.Any(a => a.Fuel == FuelType.Petrol) && arcs.Any(a => a.Fuel == FuelType.Diesel),
              string.Join("، ", arcs.Select(a => a.Fuel + ":" + a.RowCount)));

        Wait(win, cs.OpenArchiveAsync(page.Entity, page.Fuel));
        Settle(win);
        if (cs.Overlay is not CompanyArchivePageViewModel ap) { Check("صفحهٔ آرشیو باز شد", false); return; }
        Check("صفحهٔ آرشیو هر دو تیل را دارد", ap.Archives.Count == 2, ap.Archives.Count.ToString());
        //  همان کاری که کاربر می‌کند: نوارِ بسته را می‌زند — بی هیچ کمکی از سنجه
        foreach (var a in ap.Archives.Where(a => !a.IsOpen)) { a.ToggleCommand.Execute(null); Settle(win); }
        var grids = win.GetVisualDescendants().OfType<ExcelGrid>().Where(g => g.IsEffectivelyVisible).ToList();
        foreach (var a in ap.Archives)
        {
            var g = grids.FirstOrDefault(x => ReferenceEquals(x.ItemsSource, a.Rows));
            var cells = g?.GetVisualDescendants().OfType<TextBlock>()
                          .Where(t => t.IsEffectivelyVisible && t.FindAncestorOfType<DataGridRow>() is not null
                                      && !string.IsNullOrWhiteSpace(t.Text)).Select(t => t.Text!).ToList() ?? new();
            Check($"آرشیوِ {a.FuelWord}: جدولش در صفحه است و نوشته دارد",
                  g is not null && cells.Any(t => t.Contains("خریدِ")), g is null ? "جدول پیدا نشد" : cells.Count + " خانه");
        }
        Shot(win, shots, "05-company-archives");
        Check("هر آرشیو دکمهٔ PDF دارد", ap.Archives.All(a => a.PdfCommand is not null));
        ap.SetFuelFilterCommand.Execute("diesel");
        Pump(win);
        Check("کلیدِ «دیزل» فقط آرشیوِ دیزل را نشان می‌دهد",
              ap.Archives.Count == 1 && ap.Archives[0].Fuel == FuelType.Diesel, ap.DieselFuelText);
        cs.CloseOverlay();
        Settle(win);

        //  ── جستجوی خرید، همان‌جا ──
        Console.WriteLine("════ ۵ب) جستجوی خرید در همان کادر ════");
        Wait(win, cs.BackCommand.ExecuteAsync(null));
        Settle(win);
        cs.FindText = "1000";
        Wait(win, cs.FindCommand.ExecuteAsync(null));
        Settle(win);
        Check("«۱۰۰۰» ⇐ همان لحظه حسابِ «شرکتِ آزمون» باز شد", cs.Page?.Name == "شرکتِ آزمون", cs.Page?.Name ?? "—");
        Check("و صفحهٔ خریدهای همان حساب، با همان خرید", cs.Overlay is CompanyPurchasesPageViewModel,
              cs.Overlay?.GetType().Name ?? "—");
        Shot(win, shots, "05b-company-find");
        cs.CloseOverlay();
        Settle(win);
    }

    // ══ ۷) جدول‌ها: نه جای خالی، نه ستونِ پنهان ═══════════════════════════════
    private static void Gaps(Window win, MainViewModel vm, AppHost h, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۷) جدول‌ها — جای خالیِ ته ردیف ════");
        Seed.Fill(h);
        var hist = (HistorySectionViewModel)vm.Sections.First(s => s.Id == "history");
        foreach (var (w, hh) in new[] { (1920.0, 1040.0), (1366.0, 740.0), (1440.0, 900.0) })
        {
            win.Width = w; win.Height = hh;
            Settle(win);
            foreach (var id in new[] { "safe", "expenses", "sarrafi" })
            {
                var sec = vm.Sections.First(s => s.Id == id);
                Wait(win, vm.GoAsync(sec));
                Settle(win);
                Measure(win, $"{id} @{w:0}", shots);
            }
            Wait(win, vm.GoAsync(hist));
            foreach (var k in new[] { "shift", "waraq", "expense", "company" })
            {
                Wait(win, hist.OpenAsync(k));
                Settle(win);
                Measure(win, $"history/{k} @{w:0}", shots);
            }
        }
        win.Width = 1440; win.Height = 900;
        Settle(win);

        //  همان حالِ عکسِ صاحب ریپو: ستون‌ها پیکسلی و باریک (کشیده یا ذخیره‌شده)
        var safe = vm.Sections.First(s => s.Id == "safe");
        Wait(win, vm.GoAsync(safe));
        Settle(win);
        var g = win.GetVisualDescendants().OfType<ExcelGrid>().First(x => x.IsEffectivelyVisible && x.Bounds.Width > 200);
        foreach (var c in g.Columns.Where(c => c.IsVisible))
            c.Width = new DataGridLength(70, DataGridLengthUnitType.Pixel);
        Settle(win);
        Measure(win, "safe با ستون‌های باریکِ پیکسلی", shots);
        Shot(win, shots, "07-no-gap");
    }

    private static void Measure(Window win, string what, string shots)
    {
        foreach (var g in win.GetVisualDescendants().OfType<ExcelGrid>().Where(g => g.IsEffectivelyVisible && g.Bounds.Width > 200))
        {
            var cols = g.Columns.Where(c => c.IsVisible).ToList();
            if (cols.Count == 0) continue;
            var head = g.HeadersVisibility.HasFlag(DataGridHeadersVisibility.Row) ? g.RowHeaderWidth : 0;
            var used = cols.Sum(c => c.ActualWidth) + (double.IsNaN(head) ? 0 : head);
            var gap = g.Bounds.Width - used;
            var hidden = g.GetVisualDescendants().OfType<DataGridColumnHeader>()
                          .Count(x => x.IsVisible && x.Content is string { Length: > 0 } && x.Bounds.Width < 12);
            var ok = gap <= 12 && gap >= -2 && hidden == 0;
            Check($"{what}: ته ردیف جای خالی ندارد و هیچ ستونی جمع نشده", ok,
                  $"جای خالی {gap:0}px از {g.Bounds.Width:0} · ستونِ جمع‌شده {hidden}");
            if (!ok) Shot(win, shots, "gap-" + what.Replace('/', '-').Replace(' ', '_').Replace("@", ""));
        }
    }

    // ══ ۷ب) تاریخچهٔ پارچه‌ها: تیل و شمارهٔ پایه ════════════════════════════
    private static void ParchaHistory(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۷ب) تاریخچهٔ پارچه‌ها — صافیِ تیل و پایه ════");
        var hist = (HistorySectionViewModel)vm.Sections.First(s => s.Id == "history");
        Wait(win, vm.GoAsync(hist));
        Wait(win, hist.OpenAsync("shift"));
        Settle(win);
        var all = hist.Rows.Count;
        Check("صافی‌ها فقط برای پارچه‌ها دیده می‌شوند", hist.HasShiftFilters);
        Check("دکمه‌های پایه از خودِ ردیف‌ها ساخته شدند (همه + هر پایه)", hist.Pumps.Count >= 3,
              string.Join(" ", hist.Pumps.Select(p => p.Text)));
        hist.PickFuelCommand.Execute("petrol");
        Pump(win);
        Check("«⛽ پطرول» ⇒ فقط پطرول", hist.Rows.Count > 0 && hist.Rows.All(r => r.Entity.Fuel == FuelType.Petrol),
              hist.Rows.Count + " از " + all);
        hist.PickFuelCommand.Execute("diesel");
        Pump(win);
        Check("«🟤 دیزل» ⇒ هیچ ردیفِ پطرولی نیست", hist.Rows.All(r => r.Entity.Fuel == FuelType.Diesel),
              hist.Rows.Count.ToString());
        hist.PickFuelCommand.Execute("all");
        var p2 = hist.Pumps.FirstOrDefault(p => p.Number == 2);
        p2?.PickCommand.Execute(null);
        Pump(win);
        Check("«پایهٔ ۲» ⇒ فقط پایهٔ ۲", p2 is not null && hist.Rows.Count > 0 && hist.Rows.All(r => r.Entity.Pump == 2),
              hist.Rows.Count + " ردیف");
        Check("و دکمه‌اش روشن است", p2?.IsOn == true);
        Shot(win, shots, "07b-parcha-history-pump2");
        hist.Pumps[0].PickCommand.Execute(null);
        Pump(win);
        Check("«همه» ⇒ همهٔ ردیف‌ها برگشتند", hist.Rows.Count == all, hist.Rows.Count + " / " + all);
        Wait(win, hist.OpenAsync("expense"));
        Check("در تاریخچهٔ بخش‌های دیگر صافیِ پارچه نیست", !hist.HasShiftFilters);
    }

    private static void Shot(Window win, string dir, string name)
    {
        Settle(win);
        using var f = win.CaptureRenderedFrame();
        var path = Path.Combine(dir, name + ".png");
        f?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 600 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
