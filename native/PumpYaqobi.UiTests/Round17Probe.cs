using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ فهرستِ چهارتاییِ ۱۴۰۵/۰۷/۱۷ — با کلید و کلیکِ واقعی ════════════════════
///
///   ۱) ویرایشِ پایهٔ ورق ⇒ پارچه و تاریخچه · ویرایش از خودِ تاریخچه ⇒ ورق
///   ۲) «ابراهیم /ها» ⇒ تکمله «/هارون» ⇒ ردیف در حسابِ هارون با نامِ «ابراهیم»
///   ۳) پارچه: Enter ذخیره · Tab تیل · Ctrl+1/Ctrl+2 پارچهٔ جدیدِ روز/شب
///   ۴) ورق: Ctrl+1/Ctrl+2 روز/شب
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- keys17
/// </summary>
internal static class Round17Probe
{
    private static readonly List<string> Bad = new();

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-k17-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        if (AppHost.Current.Auth.NeedsFirstRun()) AppHost.Current.Auth.CreateFirstAdmin("1234");
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        LockIn.Wait(vm.Lock);
        Settle(win);

        var h = AppHost.Current;
        SeedPeople(h.Db);
        //  کَشِ نام‌ها پیش از کاشتنِ قرض‌داران گرم شده بود (خالی) — از نو، و منتظرِ رسیدن
        Suggest.Forget("debtor-acct");
        Suggest.Warm("debtor-acct");
        for (var i = 0; i < 200 && !Suggest.Of("debtor-acct").Contains("هارون"); i++) { Pump(win); Thread.Sleep(20); }

        ParchaKeys(win, vm, h);
        WaraqAndHistory(win, vm, h);

        Console.WriteLine();
        if (Bad.Count == 0) { Console.WriteLine("✅ ورق ⇄ پارچه ⇄ تاریخچه، «/هارون» و کلیدهای پارچه و ورق — همه با کلیدِ واقعی"); return 0; }
        Console.WriteLine($"❌ {Bad.Count} ایراد:");
        foreach (var b in Bad) Console.WriteLine("   • " + b);
        return 1;
    }

    private static void SeedPeople(PumpDbFactory dbf)
    {
        using var db = dbf.Create();
        foreach (var n in new[] { "هارون", "ابراهیم" })
        {
            var p = new Debtor { Name = n, LegacyId = "k17" + n };
            p.MainAccount.Name = n;
            db.Debtors.Add(p);
        }
        db.SaveChanges();
    }

    // ══ ۳) پارچه ═══════════════════════════════════════════════════════════

    private static void ParchaKeys(Window win, MainViewModel vm, AppHost h)
    {
        Console.WriteLine();
        Console.WriteLine("── ۳) پارچه: Enter · Tab · Ctrl+1/2 ──");
        var parcha = (ParchaSectionViewModel)vm.Sections.First(s => s.Id == "shifts");
        Wait(win, vm.GoAsync(parcha));
        Settle(win);
        if (parcha.IsDiesel) { parcha.ToggleFuelCommand.Execute(null); Settle(win); }

        parcha.Day.Name = "کریم";
        parcha.Day.PumpNum = "1";
        parcha.Day.Start = "1000";
        parcha.Day.End = "1100";
        parcha.Day.Price = "60";
        parcha.Day.Debt = "500";
        Settle(win);

        var end = DayBox(win, parcha, nameof(ShiftFormViewModel.End));
        if (end is null) { Fail("کادرِ «ختم پایه»ِ روز پیدا نشد"); return; }
        end.Focus();
        Settle(win);
        var before = CountReports(h.Db);
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        var cur = h.ParchaData.CurrentAsync(FuelType.Petrol).GetAwaiter().GetResult();
        Check($"Enter داخلِ کادر ⇒ شیفتِ روز ذخیره شد (پارچه‌ها {before} ⇒ {CountReports(h.Db)}، قرض {cur?.DayShift?.Debt})",
              cur?.DayShift is { Debt: 500m, End: 1100m });
        Check("و فوکوس همان‌جا ماند — Enter کادرِ بعدی را نگرفت", ReferenceEquals(win.FocusManager?.GetFocusedElement(), end));

        //  Tab ⇒ تیل
        Tap(win, PhysicalKey.Tab);
        Settle(win);
        Check($"Tab ⇒ دیزل ({parcha.FuelLabel})", parcha.IsDiesel);
        var dieselBox = DayBox(win, parcha, nameof(ShiftFormViewModel.Start));
        dieselBox?.Focus();
        Settle(win);
        Tap(win, PhysicalKey.Tab);
        Settle(win);
        Check($"Tab دوباره ⇒ پطرول ({parcha.FuelLabel})", !parcha.IsDiesel);

        //  تکملهٔ باز: Tab همان را می‌پذیرد، تیل عوض نمی‌شود
        var name = DayBox(win, parcha, nameof(ShiftFormViewModel.Name));
        if (name is not null)
        {
            name.Focus();
            name.Text = "";
            Settle(win);
            win.KeyTextInput("ک");
            Settle(win);
            var ghost = Suggest.Showing == 1;
            Tap(win, PhysicalKey.Tab);
            Settle(win);
            Check($"با تکملهٔ باز Tab تکمله را پذیرفت و تیل ماند (تکمله {ghost}، تیل {parcha.FuelLabel})",
                  !ghost || !parcha.IsDiesel);
            name.Text = "کریم";
            Settle(win);
        }

        //  Ctrl+1 / Ctrl+2
        parcha.Night.Name = "محمود";
        Settle(win);
        Ctrl(win, PhysicalKey.Digit1);
        Settle(win);
        Check($"Ctrl+1 ⇒ پارچهٔ جدیدِ روز: کارتِ روز خالی («{parcha.Day.Name}»)، شب دست‌نخورده («{parcha.Night.Name}»)",
              parcha.Day.Name == "" && parcha.Night.Name == "محمود");
        Ctrl(win, PhysicalKey.Digit2);
        Settle(win);
        Check($"Ctrl+2 ⇒ پارچهٔ جدیدِ شب: کارتِ شب خالی («{parcha.Night.Name}»)", parcha.Night.Name == "");
        //  Ctrl تنها ⇒ هیچ
        parcha.Day.Name = "علی";
        win.KeyPressQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
        Settle(win);
        Check("Ctrl بی عدد ⇒ هیچ کاری", parcha.Day.Name == "علی");
    }

    private static int CountReports(PumpDbFactory dbf)
    {
        using var db = dbf.Create();
        return db.Reports.Count();
    }

    private static TextBox? DayBox(Window win, ParchaSectionViewModel parcha, string prop) =>
        win.GetVisualDescendants().OfType<TextBox>()
           .FirstOrDefault(t => t.IsEffectivelyVisible && ReferenceEquals(t.DataContext, parcha.Day)
                             && t.GetBindingObservable(TextBox.TextProperty) is not null
                             && BoundTo(t, prop));

    private static bool BoundTo(TextBox t, string prop)
    {
        var parcha = (ShiftFormViewModel)t.DataContext!;
        var v = typeof(ShiftFormViewModel).GetProperty(prop)!.GetValue(parcha) as string;
        //  برچسبِ یکتا: همان لحظه مقدارِ خاصیت را عوض و برمی‌گردانیم
        var mark = "‹" + prop + "›";
        typeof(ShiftFormViewModel).GetProperty(prop)!.SetValue(parcha, mark);
        Dispatcher.UIThread.RunJobs();
        var hit = t.Text == mark;
        typeof(ShiftFormViewModel).GetProperty(prop)!.SetValue(parcha, v);
        Dispatcher.UIThread.RunJobs();
        return hit;
    }

    // ══ ۱، ۲، ۴) ورق و تاریخچه ═════════════════════════════════════════════

    private static void WaraqAndHistory(Window win, MainViewModel vm, AppHost h)
    {
        Console.WriteLine();
        Console.WriteLine("── ۱، ۲، ۴) ورق ⇄ پارچه ⇄ تاریخچه ──");
        var today = Shamsi.Today();
        var saved = h.ParchaData.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, today, "هارون", 3, 0m, 100m, 60m, 500m, 0m, 0m, "", 0m,
            ForceNew: true)).GetAwaiter().GetResult();
        if (!saved.Ok) { Fail("پارچه ذخیره نشد: " + saved.Error); return; }

        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(wq));
        Settle(win);
        var w = h.WaraqData.OpenOrCreateAsync(today, "").GetAwaiter().GetResult();
        var sheet = wq.Sheets.FirstOrDefault(x => x.Id == w.Id);
        if (sheet is null) { Fail("ورقِ امروز در فهرست نیست"); return; }
        wq.OpenCommand.Execute(sheet);
        Settle(win);
        var page = wq.Page!;

        //  ۴) Ctrl+2 / Ctrl+1
        Ctrl(win, PhysicalKey.Digit2);
        Settle(win);
        Check($"ورق: Ctrl+2 ⇒ شب ({(page.IsNight ? "شب" : "روز")})", page.IsNight);
        Ctrl(win, PhysicalKey.Digit1);
        Settle(win);
        Check($"ورق: Ctrl+1 ⇒ روز ({(page.IsNight ? "شب" : "روز")})", !page.IsNight);

        //  ۱) قرضِ پایهٔ ورق ⇒ پارچه
        var pump = page.Pumps.FirstOrDefault(p => p.Entity.SrcKey == saved.SrcKey);
        if (pump is null) { Fail("پایهٔ پارچه در ورق نیست"); return; }
        pump.DebtText = "800";
        Settle(win);
        Wait(win, SaveGuard.FlushAllAsync());
        Settle(win);
        var s1 = ShiftOf(h.Db, saved.Report!.Id);
        Check($"قرضِ پایهٔ ورق ⇒ پارچه ({s1?.Debt}) و «رسیده» از نو ({s1?.Available})",
              s1 is { Debt: 800m, Available: 5200m });
        var feed = h.History.FeedAsync("shift").GetAwaiter().GetResult();
        Check($"تاریخچهٔ پارچه‌ها همان قرض را دارد ({feed.First(r => r.ShiftRef?.ReportId == saved.Report.Id).ShiftRef!.Debt})",
              feed.First(r => r.ShiftRef?.ReportId == saved.Report.Id).ShiftRef!.Debt == 800m);

        //  برگشت به پارچه‌ها ⇒ کارتِ روز (همان شیفت، دست‌نخورده) عددِ تازه را دارد
        var parcha = (ParchaSectionViewModel)vm.Sections.First(s => s.Id == "shifts");
        Wait(win, vm.GoAsync(parcha));
        Settle(win);
        if (parcha.Day.LoadedId == saved.Shift!.Id)
            Check($"پارچه‌ها پس از برگشت: قرضِ کارتِ روز «{parcha.Day.Debt}»", Shamsi.Num(parcha.Day.Debt) == 800m);
        else Console.WriteLine($"    (کارتِ روز شیفتِ دیگری را نشان می‌دهد — {parcha.Day.LoadedId})");

        //  ۱ب) ویرایش از خودِ تاریخچه ⇒ پارچه و ورق
        var hist = (HistorySectionViewModel)vm.Sections.First(s => s.Id == "history");
        Wait(win, vm.GoAsync(hist));
        Settle(win);
        Wait(win, hist.OpenAsync("shift"));
        Settle(win);
        var grid = win.GetVisualDescendants().OfType<ExcelGrid>()
                      .FirstOrDefault(g => g.IsEffectivelyVisible && !g.IsReadOnly);
        if (grid is null) { Fail("جدولِ ویرایشیِ تاریخچهٔ پارچه‌ها پیدا نشد"); return; }
        var row = hist.Rows.FirstOrDefault(r => r.Entity.ShiftRef?.ReportId == saved.Report.Id);
        if (row is null) { Fail("ردیفِ پارچه در تاریخچه نیست"); return; }
        var cols = grid.Columns.OrderBy(c => c.DisplayIndex).ToList();
        var debtCol = cols.FindIndex(c => (c.Header as string) == "قرض");
        Check("ستونِ «قرض» ویرایشی است", debtCol >= 0 && !cols[debtCol].IsReadOnly);
        Check("ستونِ «تاریخ» خواندنی است", cols[0].IsReadOnly);
        var ri = hist.Rows.IndexOf(row);
        ClickCell(win, grid, ri, debtCol);
        win.KeyTextInput("950");
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        Wait(win, SaveGuard.FlushAllAsync());
        Settle(win);
        var s2 = ShiftOf(h.Db, saved.Report.Id);
        using (var db = h.Db.Create())
        {
            var p2 = db.WaraqPumps.AsNoTracking().Single(p => p.SrcKey == saved.SrcKey);
            Check($"تاریخچه ⇒ پارچه ({s2?.Debt}) و پایهٔ ورق ({p2.Debt})", s2?.Debt == 950m && p2.Debt == 950m);
        }
        //  ختم کمتر از شروع ⇒ برمی‌گردد
        //  شروعِ ۵۰۰ روی ختمِ ۱۰۰ ⇒ رد
        var startCol = cols.FindIndex(c => (c.Header as string) == "شروعِ پایه");
        ClickCell(win, grid, ri, startCol);
        win.KeyTextInput("500");
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        Wait(win, SaveGuard.FlushAllAsync());
        Settle(win);
        var s3 = ShiftOf(h.Db, saved.Report.Id);
        Check($"ختمِ کمتر از شروع رد شد و خانه برگشت (دیسک {s3?.Start}/{s3?.End}، خانه «{row.EditStart}»)",
              s3?.Start == 0m && s3?.End == 100m && Shamsi.Num(row.EditStart) == 0m);

        //  ۲) «ابراهیم /ها» ⇒ تکمله ⇒ حسابِ هارون
        Wait(win, vm.GoAsync(wq));
        Settle(win);
        wq.OpenCommand.Execute(wq.Sheets.First(x => x.Id == w.Id));
        Settle(win);
        page = wq.Page!;
        var txGrid = win.GetVisualDescendants().OfType<ExcelGrid>()
            .FirstOrDefault(g => g.IsEffectivelyVisible && ReferenceEquals(g.ItemsSource, page.TxnsFirst));
        if (txGrid is null) { Fail("جدولِ تراکنش‌های ورق پیدا نشد"); return; }
        ClickCell(win, txGrid, 0, 0);
        win.KeyTextInput("ابراهیم /ها");
        Settle(win);
        var box = win.FocusManager?.GetFocusedElement() as TextBox;
        Check($"پس از «/ها» تکمله آمد («{Suggest.Ghost}»)", Suggest.Ghost == "ابراهیم /هارون");
        Tap(win, PhysicalKey.Tab);
        Settle(win);
        Check($"Tab پذیرفت («{box?.Text}»)", box?.Text == "ابراهیم /هارون");
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        var t0 = page.TxnsFirst.First();
        t0.LitersText = "10";
        Settle(win);
        Wait(win, SaveGuard.FlushAllAsync());
        Settle(win);
        for (var i = 0; i < 50; i++) { Pump(win); Thread.Sleep(20); }
        using (var db = h.Db.Create())
        {
            var haroun = db.Debtors.AsNoTracking().Include(d => d.MainAccount).Single(d => d.Name == "هارون");
            var ebrahim = db.Debtors.AsNoTracking().Include(d => d.MainAccount).Single(d => d.Name == "ابراهیم");
            var rows = db.DebtRows.AsNoTracking().Where(r => r.Src == "waraq").ToList();
            var r = rows.FirstOrDefault();
            Check($"ردیف در حسابِ هارون ({r?.FuelAccountId} == {haroun.MainAccount.Id}) با نامِ «{r?.Name}»",
                  r is not null && r.FuelAccountId == haroun.MainAccount.Id && r.Name == "ابراهیم");
            Check("و هیچ ردیفی در حسابِ ابراهیم نیست", rows.All(x => x.FuelAccountId != ebrahim.MainAccount.Id));
        }
    }

    private static ShiftData? ShiftOf(PumpDbFactory dbf, long reportId)
    {
        using var db = dbf.Create();
        return db.Reports.AsNoTracking().Include(r => r.DayShift).Single(r => r.Id == reportId).DayShift;
    }

    // ── ابزار ─────────────────────────────────────────────────────────────

    private static DataGridCell? Cell(DataGrid g, int row, int col)
    {
        var cols = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (col < 0 || col >= cols.Count) return null;
        var item = g.ItemsSource?.Cast<object>().ElementAtOrDefault(row);
        return g.GetVisualDescendants().OfType<DataGridRow>()
                .FirstOrDefault(r => ReferenceEquals(r.DataContext, item))?
                .GetVisualDescendants().OfType<DataGridCell>()
                .FirstOrDefault(c => ReferenceEquals(ColumnOf(c), cols[col]));
    }

    private static DataGridColumn? ColumnOf(DataGridCell cell) =>
        cell.GetType().GetProperty("OwningColumn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
              | System.Reflection.BindingFlags.Public)?.GetValue(cell) as DataGridColumn;

    private static void ClickCell(Window win, DataGrid g, int row, int col)
    {
        g.ScrollIntoView(g.ItemsSource?.Cast<object>().ElementAtOrDefault(row), null);
        Settle(win);
        var cell = Cell(g, row, col);
        if (cell is null) { Fail($"خانهٔ {row}/{col} پیدا نشد"); return; }
        var p = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), win);
        if (p is null) return;
        win.MouseDown(p.Value, MouseButton.Left);
        win.MouseUp(p.Value, MouseButton.Left);
        Settle(win);
    }

    private static void Ctrl(Window win, PhysicalKey k)
    {
        win.KeyPressQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
        win.KeyPressQwerty(k, RawInputModifiers.Control);
        win.KeyReleaseQwerty(k, RawInputModifiers.Control);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
    }

    private static void Tap(Window win, PhysicalKey key)
    {
        win.KeyPressQwerty(key, RawInputModifiers.None);
        win.KeyReleaseQwerty(key, RawInputModifiers.None);
    }

    private static void Check(string what, bool ok)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}");
        if (!ok) Bad.Add(what);
    }

    private static void Fail(string what) { Console.WriteLine("  ✖ " + what); Bad.Add(what); }

    private static void Settle(Window w)
    {
        var end = DateTime.UtcNow + TimeSpan.FromMilliseconds(700);
        while (DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(10); }
        Pump(w);
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        Pump(w);
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
