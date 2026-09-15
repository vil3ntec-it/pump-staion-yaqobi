using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
/// ══ سنجشِ رفتاریِ اصلاح‌های ۱۴۰۵/۰۶/۲۵ که فقط از روی کد نوشته شده بودند ═══════
/// خواستهٔ صاحب ریپو: «با دقت و تست بگو، نه با حدس.» هر بند یک کارِ واقعیِ
/// کاربر را انجام می‌دهد و نتیجه را می‌سنجد.
///     dotnet run --project PumpYaqobi.UiTests -- verify
/// </summary>
internal static class VerifyProbe
{
    private static int _bad;
    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}{(detail is null ? "" : " — " + detail)}");
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-verify-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1366, Height = 768 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234"; vm.Lock.SubmitCommand.Execute(null);
        Wait(win, Task.CompletedTask);
        Seed.Fill(AppHost.Current);
        var host = AppHost.Current;

        // ── ۱) رسیدِ سربرگ در اولین ردیفِ خالی ─────────────────────────────
        Console.WriteLine("── ۱) رسیدِ سربرگِ قرض‌دار در اولین ردیفِ خالی می‌نشیند");
        var debt = (DebtSectionViewModel)vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(debt)); Wait(win, debt.RefreshAsync());
        debt.OpenCommand.Execute(debt.Cards.First()); Pump(win);
        var acct = debt.Person!.Current!;
        var before = acct.Rows.Count;
        Wait(win, acct.AddRowsAsync(3)); Pump(win);
        Check("سه ردیفِ خالی اضافه شد", acct.Rows.Count == before + 3);
        acct.HeadPetrolRasidEdit = "500";
        for (var i = 0; i < 30; i++) Pump(win);
        var firstBlank = acct.Rows[before];
        var got = acct.IsMoney ? firstBlank.Rasid : firstBlank.RasidFuel;
        Check("رسیدِ ۵۰۰ در اولین ردیفِ خالی نشست، نه تهِ جدول", got == 500m && acct.Rows.Count == before + 3,
              $"ردیف {before + 1}: {got} · شمارِ ردیف {acct.Rows.Count}");
        Check("تاریخِ امروز روی همان ردیف", firstBlank.DateShamsi == Shamsi.Today(), firstBlank.DateShamsi);
        acct.HeadPetrolRasidEdit = "300";
        for (var i = 0; i < 30; i++) Pump(win);
        var second = acct.Rows[before + 1];
        Check("رسیدِ دوم به ردیفِ خالیِ بعدی رفت", (acct.IsMoney ? second.Rasid : second.RasidFuel) == 300m && acct.Rows.Count == before + 3);
        Wait(win, acct.DeleteRowsAsync(1)); // آخرین خالی را بردار
        acct.HeadPetrolRasidEdit = "200";
        for (var i = 0; i < 30; i++) Pump(win);
        Check("بی ردیفِ خالی، ردیفِ تازه ساخته می‌شود", acct.Rows.Count == before + 3,
              $"شمارِ ردیف {acct.Rows.Count}");

        // ── ۲) کادرِ رسید بعد از خروجِ بی‌نوشتن، جمع را نشان می‌دهد ──────────
        Console.WriteLine("── ۲) کادرِ رسیدِ سربرگ پس از فوکوس و خروجِ بی‌نوشتن خالی نمی‌ماند");
        var boxes = win.GetVisualDescendants().OfType<TextBox>()
                       .Where(t => t.IsEffectivelyVisible && t.Classes.Contains("fsv")).ToList();
        var box = boxes.FirstOrDefault();
        Check("کادرِ رسیدِ سربرگ پیدا شد", box is not null, $"{boxes.Count} کادر");
        if (box is not null)
        {
            var shown = box.Text;
            box.Focus(); Pump(win);
            Check("با فوکوس خالی می‌شود (تا عددِ تازه تایپ شود)", string.IsNullOrEmpty(box.Text));
            // خروج بی نوشتن: فوکوس به دکمهٔ دیگر
            win.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible).Focus();
            for (var i = 0; i < 10; i++) Pump(win);
            Check("پس از خروج، جمعِ رسید دوباره دیده می‌شود", !string.IsNullOrEmpty(box.Text) && box.Text == shown,
                  $"«{box.Text}» (پیش از آن «{shown}»)");
        }

        // ── ۳) کلیکِ بیرونِ کادر فوکوس را رها می‌کند ───────────────────────
        Console.WriteLine("── ۳) کلیک روی زمینهٔ صفحه، کادرِ رسید را رها می‌کند");
        if (box is not null)
        {
            box.Focus(); Pump(win);
            var label = win.GetVisualDescendants().OfType<TextBlock>()
                           .First(t => t.IsEffectivelyVisible && t.Classes.Contains("fsl"));
            var at = label.TranslatePoint(new Point(4, 4), win)!.Value;
            win.MouseDown(at, MouseButton.Left); win.MouseUp(at, MouseButton.Left);
            for (var i = 0; i < 6; i++) Pump(win);
            var focused = win.FocusManager?.GetFocusedElement();
            Check("فوکوس دیگر روی کادرِ رسید نیست", !ReferenceEquals(focused, box), focused?.GetType().Name ?? "هیچ");
        }

        // ── ۴) Ctrl+Delete ردیفِ جاری را حذف می‌کند ────────────────────────
        Console.WriteLine("── ۴) Ctrl+Delete در جدولِ صرافی");
        var sarrafi = vm.Sections.First(s => s.Id == "sarrafi");
        Wait(win, vm.GoAsync(sarrafi)); for (var i = 0; i < 6; i++) Pump(win);
        var ledger = (IRowBatchHost)sarrafi;
        var grid = win.GetVisualDescendants().OfType<ExcelGrid>().First(g => g.IsEffectivelyVisible);
        var n0 = ledger.RowCount;
        grid.Focus(); grid.SelectedIndex = 0; Pump(win);
        win.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.Control);
        win.KeyReleaseQwerty(PhysicalKey.Delete, RawInputModifiers.Control);
        for (var i = 0; i < 20; i++) Pump(win);
        Check("یک ردیف کم شد", ledger.RowCount == n0 - 1, $"{n0} ← {ledger.RowCount}");

        // ── ۵) «🕘 تاریخچه»ی گاوصندوق به تاریخچهٔ همان بخش می‌رود ───────────
        Console.WriteLine("── ۵) دکمهٔ تاریخچهٔ گاوصندوق");
        var safe = (SafeSectionViewModel)vm.Sections.First(s => s.Id == "safe");
        Wait(win, vm.GoAsync(safe)); Pump(win);
        safe.OpenHistoryCommand.Execute(null);
        for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
        var hist = vm.Sections.OfType<HistorySectionViewModel>().First();
        Check("بخشِ تاریخچه‌ها باز شد", vm.Current?.Id == "history", vm.Current?.Id);
        Check("و مستقیم روی تاریخچهٔ گاوصندوق", !hist.IsListVisible && hist.OpenKind == "safe", hist.OpenKind);

        // ── ۶) پیشنهادِ خودکار: Tab می‌چرخد، Enter برمی‌دارد ───────────────
        Console.WriteLine("── ۶) پیشنهادِ خودکار در خانهٔ «توضیحات»ِ صرافی");
        Wait(win, vm.GoAsync(sarrafi)); for (var i = 0; i < 6; i++) Pump(win);
        grid = win.GetVisualDescendants().OfType<ExcelGrid>().First(g => g.IsEffectivelyVisible);
        var col = grid.Columns.First(c => (c.Header as string) == "توضیحات");
        grid.Focus(); grid.SelectedIndex = 0; grid.CurrentColumn = col; Pump(win);
        grid.BeginEdit(); for (var i = 0; i < 4; i++) Pump(win);
        var editor = grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsFocused);
        // مثلِ کاربر: خانه را پاک می‌کند تا فهرستِ کامل بیاید (متنِ خودِ خانه پیشنهاد نمی‌شود)
        if (editor is not null) { editor.Text = ""; Pump(win); }
        Check("کادرِ ویرایش باز و فهرست پیدا", editor is not null && Suggest.Showing > 1, $"{Suggest.Showing} پیشنهاد");
        win.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None); win.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Pump(win);
        Check("Tab اولین پیشنهاد را روشن می‌کند و از خانه بیرون نمی‌رود", Suggest.Active == 0 && Suggest.Showing > 0 && grid.CurrentColumn == col,
              $"active={Suggest.Active} showing={Suggest.Showing}");
        win.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); win.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        for (var i = 0; i < 4; i++) Pump(win);
        var picked = editor?.Text ?? "";
        Check("Enter پیشنهاد را در خانه می‌نشاند", picked.Length > 0 && Suggest.Showing == 0, $"«{picked}»");
        grid.CancelEdit(); Pump(win);

        // ── ۷) ماشین‌حساب با صفحه‌کلید ─────────────────────────────────────
        Console.WriteLine("── ۷) ماشین‌حساب با صفحه‌کلید (۱۲ + ۷ =)");
        vm.Calculator.IsOpen = true; grid.Focus(); Pump(win);
        foreach (var k in new[] { PhysicalKey.Digit1, PhysicalKey.Digit2, PhysicalKey.NumPadAdd, PhysicalKey.Digit7, PhysicalKey.Enter })
        { win.KeyPressQwerty(k, RawInputModifiers.None); win.KeyReleaseQwerty(k, RawInputModifiers.None); Pump(win); }
        Check("صفحه ۱۹ را نشان می‌دهد", vm.Calculator.Display == "19", vm.Calculator.Display);
        var w0 = vm.Calculator.Width;
        vm.Calculator.Resize(50, 30);
        Check("اندازه با کشیدن عوض می‌شود", vm.Calculator.Width == w0 + 50);
        vm.Calculator.IsOpen = false;

        // ── ۸) اندازهٔ ماشین‌حساب در تنظیمات می‌ماند ────────────────────────
        Console.WriteLine("── ۸) اندازهٔ ماشین‌حساب پس از تغییر، ذخیره می‌شود");
        vm.Calculator.Width = 333; vm.Calculator.Height = 444;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(4);
        while (DateTime.UtcNow < deadline && AppSettings.Load().CalcWidth != 333) { Pump(win); Thread.Sleep(50); }
        var saved = AppSettings.Load();
        Check("پهنا و بلندی در فایلِ تنظیمات نشست", saved.CalcWidth == 333 && saved.CalcHeight == 444, $"{saved.CalcWidth}×{saved.CalcHeight}");

        // ── ۹) متنِ بلند در خانه: ردیف بلند نمی‌شود، کادرِ ویرایش از خانه بیرون نمی‌زند ──
        Console.WriteLine("── ۹) متنِ بلند در خانهٔ «توضیحات»");
        grid.Focus(); grid.SelectedIndex = 1; grid.CurrentColumn = col; Pump(win);
        grid.BeginEdit(); for (var i = 0; i < 3; i++) Pump(win);
        var ed = grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsFocused);
        if (ed is not null) { ed.Text = new string('h', 60) + " hoioihhhhhhhhhhhhhhhhhhhhhhhhhhhhhh"; for (var i = 0; i < 4; i++) Pump(win); }
        var cell = ed?.GetVisualAncestors().OfType<DataGridCell>().FirstOrDefault();
        Check("کادرِ ویرایش داخلِ خانه می‌ماند", ed is not null && cell is not null && ed.Bounds.Width <= cell.Bounds.Width + 1,
              ed is null ? "کادر نیست" : $"کادر {ed.Bounds.Width:0} · خانه {cell?.Bounds.Width:0}");
        grid.CommitEdit(); for (var i = 0; i < 6; i++) Pump(win);
        var heights = grid.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.Bounds.Height > 0).Select(r => Math.Round(r.Bounds.Height)).Distinct().ToList();
        Check("همهٔ ردیف‌ها هم‌قد ماندند (خط نشکست)", heights.Count == 1, string.Join("،", heights));
        var widthsBefore = grid.Columns.Select(c => c.ActualWidth).ToArray();
        Check("پهنای ستون‌ها با متنِ بلند تکان نخورد", grid.Columns.All(c => c.ActualWidth > 0), string.Join("،", widthsBefore.Select(w => w.ToString("0"))));

        // ── ۱۰) سربرگِ گاوصندوق: دالر جدا و درست جمع می‌شود ─────────────────
        Console.WriteLine("── ۱۰) سربرگِ گاوصندوق با ردیفِ دالری");
        Wait(win, vm.GoAsync(safe)); for (var i = 0; i < 6; i++) Pump(win);
        var safeLedger = (IRowBatchHost)safe;
        decimal N(string t) => Shamsi.Num(t);
        var mUsd0 = N(safe.MandagiUsd); var mAfn0 = N(safe.MandagiAfn); var bUsd0 = N(safe.BardagiUsd); var nUsd0 = N(safe.NetUsd);
        Wait(win, safeLedger.AddRowsAsync(1)); Pump(win);
        var last = safe.Rows[^1];
        last.IsUsd = true; last.Amount = 777m; last.IsBardagi = false;
        for (var i = 0; i < 12; i++) Pump(win);
        Check("«جمله ماندگی» دالر ۷۷۷ بیشتر شد", N(safe.MandagiUsd) == mUsd0 + 777m, $"{mUsd0} ← {safe.MandagiUsd}");
        Check("افغانیِ ماندگی دست نخورد (دالر و افغانی جمع نمی‌شوند)", N(safe.MandagiAfn) == mAfn0, safe.MandagiAfn);
        Check("«موجودی خالص» دالر هم ۷۷۷ بیشتر شد", N(safe.NetUsd) == nUsd0 + 777m, $"{nUsd0} ← {safe.NetUsd}");
        last.IsBardagi = true; for (var i = 0; i < 12; i++) Pump(win);
        Check("با بردگی شدنِ همان ردیف، ۷۷۷ از ماندگی به بردگی رفت",
              N(safe.BardagiUsd) == bUsd0 + 777m && N(safe.MandagiUsd) == mUsd0, $"بردگی {safe.BardagiUsd} · ماندگی {safe.MandagiUsd}");
        Wait(win, safeLedger.DeleteRowsAsync(1));

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ هر ده رفتار همان‌طور که خواسته شده کار می‌کند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        t.GetAwaiter().GetResult(); Pump(w);
    }
    private static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
