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
using PumpYaqobi.Services.Data;

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

        /*
         *  ⛔ **این نصب باید پلن داشته باشد، وگرنه سنجه دو رفتارِ سالم را
         *  «✖» می‌دید.**
         *
         *  از ۱۴۰۵/۰۶/۳۰ شش دروازهٔ پلن داریم و این سنجه در یک پوشهٔ
         *  موقتِ **خالی** بالا می‌آید — یعنی «بی‌اشتراک». پس تاریخچهٔ
         *  گاوصندوق باز نمی‌شد (بندِ ۵) و پیامِ آمادهٔ واتساپ خالی بود
         *  (بندِ ۱۶)، و هر دو **درست** بودند؛ انتظارِ سنجه کهنه بود.
         *
         *  ⚠️ سقفِ سنجه پایین نیامد و هیچ قفلی ضعیف نشد: مجوز واقعاً با
         *  یک جفت‌کلیدِ ES256ِ همین فرآیند امضا می‌شود و همان کلید در
         *  تنظیمات قفل می‌شود — همان کارِ نخستین فعال‌سازیِ واقعی.
         *  حالتِ **بی‌اشتراک** هم سنجیده می‌شود، در بندِ ۱۷.
         *
         *  ⚠️ و **پیش از** ساختنِ پنجره: `MainViewModel` نسخهٔ خودش از
         *  تنظیمات را نگه می‌دارد و با ذخیرهٔ «آخرین بخش» همان را روی
         *  دیسک می‌نویسد، پس نوشتنِ بعد از آن همان لحظه پاک می‌شود.
         */
        var accessCode = FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1366, Height = 768 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234"; LockIn.Wait(vm.Lock);
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

        // ── ۶) تکمیلِ خودکار **داخلِ خودِ کادر** ────────────────────────────
        //  خواستهٔ صاحب ریپو با عکس (۱۴۰۵/۰۶/۲۷): «پاپ‌آپ نه — با همان «س» که
        //  دادم داخلِ کادر تکملهٔ جمله را نشان بده و با Tab یا Enter تمامش کن.»
        //  و قاعدهٔ حیاتی: تکملهٔ پذیرفته‌نشده نباید در دفتر ذخیره شود.
        Console.WriteLine("── ۶) تکمیلِ خودکار داخلِ خانهٔ «توضیحات»ِ صرافی");
        Wait(win, vm.GoAsync(sarrafi)); for (var i = 0; i < 6; i++) Pump(win);
        grid = win.GetVisualDescendants().OfType<ExcelGrid>().First(g => g.IsEffectivelyVisible);
        var col = grid.Columns.First(c => (c.Header as string) == "توضیحات");
        grid.Focus(); grid.SelectedIndex = 0; grid.CurrentColumn = col; Pump(win);
        grid.BeginEdit(); for (var i = 0; i < 4; i++) Pump(win);
        var editor = grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsFocused);
        if (editor is not null) { editor.Text = ""; editor.CaretIndex = 0; Pump(win); }

        // مثلِ کاربر: یک حرف تایپ می‌شود («حوالهٔ …»ی همین ستون)
        win.KeyTextInput("ح"); for (var i = 0; i < 3; i++) Pump(win);
        var inline = editor?.Text ?? "";
        Check("با یک حرف، ادامهٔ جمله داخلِ خودِ کادر آمد",
              Suggest.Showing == 1 && inline.StartsWith("ح") && inline.Length > 1,
              $"«{inline}» · تکمله={Suggest.Showing}");
        Check("و فقط بخشِ تکمله انتخاب است، نه چیزی که تایپ شده",
              editor is not null && editor.SelectionStart == 1 && editor.SelectionEnd == inline.Length,
              editor is null ? "کادر نیست" : $"{editor.SelectionStart}..{editor.SelectionEnd}");

        // Tab تکمیل می‌کند و از خانه بیرون نمی‌رود
        win.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None); win.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        for (var i = 0; i < 3; i++) Pump(win);
        var picked = editor?.Text ?? "";
        Check("Tab جمله را تکمیل می‌کند و در همان خانه می‌ماند",
              picked == inline && picked.Length > 1 && Suggest.Showing == 0 && grid.CurrentColumn == col,
              $"«{picked}»");
        grid.CancelEdit(); Pump(win);

        // ⚠️ و تکملهٔ **پذیرفته‌نشده** هرگز در داده نمی‌نشیند
        grid.Focus(); grid.SelectedIndex = 1; grid.CurrentColumn = col; Pump(win);
        grid.BeginEdit(); for (var i = 0; i < 4; i++) Pump(win);
        var ed2 = grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsFocused);
        if (ed2 is not null) { ed2.Text = ""; ed2.CaretIndex = 0; Pump(win); }
        win.KeyTextInput("ح"); for (var i = 0; i < 3; i++) Pump(win);
        var ghosted = ed2?.Text ?? "";
        grid.CommitEdit(); for (var i = 0; i < 6; i++) Pump(win);
        var saved2 = (grid.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().ElementAtOrDefault(1);
        var savedText = saved2?.GetType().GetProperty("Description")?.GetValue(saved2) as string ?? "";
        Check("تکملهٔ پذیرفته‌نشده در دفتر ذخیره نمی‌شود",
              savedText == "ح", $"در کادر «{ghosted}» بود · ذخیره‌شده «{savedText}»");

        // ── ۶ب) دوبار-کلیک روی خطِ ستون، مثلِ اکسل ─────────────────────────
        Console.WriteLine("── ۶ب) دوبار-کلیک روی خطِ ستون، ستون را هم‌قدِ محتوا می‌کند");
        grid.Focus(); grid.SelectedIndex = 0; grid.CurrentColumn = col; Pump(win);
        grid.BeginEdit(); for (var i = 0; i < 3; i++) Pump(win);
        var ed3 = grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsFocused);
        if (ed3 is not null) { ed3.Text = new string('م', 40); ed3.CaretIndex = 40; }
        grid.CommitEdit(); for (var i = 0; i < 6; i++) Pump(win);

        var header = grid.GetVisualDescendants().OfType<DataGridColumnHeader>()
                         .FirstOrDefault(h => (h.Content as string) == "توضیحات");
        var wBefore = col.ActualWidth;
        // دوبار-کلیک درست روی خطِ راستِ همان سربرگ — همان‌جا که دستهٔ کشیدن است
        if (header is not null) grid.AutoFitAt(header, header.Bounds.Width - 2);
        for (var i = 0; i < 10; i++) Pump(win);
        var wAfter = col.ActualWidth;
        Check("دوبار-کلیک روی خطِ ستون، پهنا را به محتوا رساند",
              header is not null && wAfter > wBefore + 10
              && col.Width.UnitType == DataGridLengthUnitType.Pixel,
              $"{wBefore:0} ⇒ {wAfter:0} پیکسل");

        // و کلیکِ وسطِ سربرگ (نه روی خط) نباید کاری کند
        var wMid = col.ActualWidth;
        var moved = header is not null && grid.AutoFitAt(header, header.Bounds.Width / 2);
        for (var i = 0; i < 4; i++) Pump(win);
        Check("دوبار-کلیکِ وسطِ سربرگ کاری نمی‌کند", !moved && Math.Abs(col.ActualWidth - wMid) < 1);

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

        // ── ۱۱) مقایسهٔ نرخ فاکتورها: جست‌وجو و انتخابِ یک ردیف ─────────────
        //
        // خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «یک جست‌وجو توی این بخش هم باشه تا
        // شمارهٔ فاکتور را بیرون کند یا با اسم پیدا شود… و روی یکی از کادرها
        // که زدم، سربرگ اطلاعاتِ همان را بالا نشان بدهد؛ جای دیگر که کلیک
        // کردم، از همه را.»
        Console.WriteLine("── ۱۱) مقایسهٔ نرخ فاکتورها: جست‌وجو و انتخابِ یک ردیف");
        if (vm.Sections.SelectMany(x => x.SubSections).FirstOrDefault(x => x.Id == "invrate")
                is InvRateSectionViewModel rate)
        {
            Wait(win, rate.RefreshAsync()); Pump(win);
            var all = rate.Rows.Count;
            Check("فهرستِ فاکتورها آمد", all > 0, all + " ردیف");
            if (all > 0)
            {
                var pick = rate.Rows[0];
                var allScope = rate.ScopeText;

                rate.Search = pick.Number.ToString(); Pump(win);
                Check("جست‌وجو با شمارهٔ فاکتور همان یکی را بیرون کشید",
                      rate.Rows.Count >= 1 && rate.Rows.All(r => r.Number.ToString().Contains(pick.Number.ToString())),
                      rate.Rows.Count + " ردیف");

                rate.Search = ""; Pump(win);
                Check("خالی کردنِ جست‌وجو همه را برمی‌گرداند", rate.Rows.Count == all);

                rate.Selected = pick; Pump(win);
                Check("سربرگ روی همان فاکتور رفت", rate.ScopeText.Contains(pick.NumberText), rate.ScopeText);
                Check("شمارِ کارت‌ها هم همان یکی شد",
                      rate.CountText.StartsWith(Shamsi.Money(pick.Approved && pick.HasDiff ? 1 : 0)),
                      rate.CountText);

                rate.Selected = null; Pump(win);
                Check("با برداشتنِ انتخاب، دوباره همه", rate.ScopeText == allScope, rate.ScopeText);
            }
        }
        else Check("زیربخشِ مقایسهٔ نرخ پیدا شد", false);

        // ── ۱۲) فاکتورِ به‌نامِ یک قرض‌دارِ موجود، به حسابِ خودش برود ─────────
        //
        // گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «فاکتور به اسمِ قرض‌داری که هست
        // نمی‌رود داخلِ حسابِ همان قرض‌دار و تو صفش نیست.»
        Console.WriteLine("── ۱۲) فاکتور به‌نامِ قرض‌دارِ موجود ⇒ حسابِ خودش");
        {
            Wait(win, vm.GoAsync(debt)); Wait(win, debt.RefreshAsync());
            var card = debt.Cards.First();
            var who = card.Name;
            var peopleBefore = debt.Cards.Count;

            var inv = Wait(host.Invoices.AddAsync(new PumpYaqobi.Domain.Entities.Invoice
            {
                CustomerName = who,
                DateShamsi = Shamsi.Today(),
                Fuel = PumpYaqobi.Domain.Enums.FuelType.Petrol,
                Liters = 120m,
                PricePerLiter = 60m,
                Amount = 0m,
                ByMoney = false,
            }));
            Wait(host.Invoices.ApproveAsync(inv.Id, 62m));
            Pump(win);

            Wait(win, debt.RefreshAsync()); Pump(win);
            Check("قرض‌دارِ تکراری ساخته نشد", debt.Cards.Count == peopleBefore,
                  $"{peopleBefore} ← {debt.Cards.Count}");

            var again = debt.Cards.FirstOrDefault(c => c.Name == who);
            if (again is null) Check("همان قرض‌دار پیدا شد", false);
            else
            {
                debt.OpenCommand.Execute(again); Pump(win);
                var rows = debt.Person!.Accounts.SelectMany(a => a.Rows).ToList();
                var hit = rows.FirstOrDefault(r => r.Entity.InvoiceId == inv.Id);
                Check("ردیفِ رسیدِ همان فاکتور در حسابش نشست", hit is not null,
                      hit?.Name ?? $"{rows.Count} ردیف، هیچ‌کدام مالِ این فاکتور");
                Check("و رسیدِ تیلش همان ۱۲۰ لیتر است",
                      hit is not null && hit.Entity.RasidFuel == 120m, hit?.Entity.RasidFuel.ToString());

                // و فاکتورِ **در صف** هم روی همین صفحه دیده شود
                var pending = Wait(host.Invoices.AddAsync(new PumpYaqobi.Domain.Entities.Invoice
                {
                    CustomerName = who,
                    DateShamsi = Shamsi.Today(),
                    Fuel = PumpYaqobi.Domain.Enums.FuelType.Diesel,
                    Liters = 40m,
                    PricePerLiter = 58m,
                }));
                Wait(debt.Person!.Current!.LoadInvoicesAsync()); Pump(win);
                var acctInvs = debt.Person!.Current!.Invoices;
                Check("فاکتورِ در صف هم در صفحهٔ همان حساب دیده می‌شود",
                      acctInvs.Any(x => x.Number == pending.InvoiceNumber && !x.Approved),
                      acctInvs.Count + " فاکتور · " + debt.Person!.Current!.InvoicesText);
                debt.PersonOpen = false; Pump(win);
            }
        }

        // ── ۱۳) فاکتور به نامِ یک حسابِ فرعی ⇒ همان حسابِ فرعی ──────────────
        //
        // خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «اگر بخواهم برای حسابِ فرعی فاکتور
        // بنویسم، این کار هم بشود.» پیش از این نامِ حسابِ فرعی به هیچ حسابی
        // نمی‌خورد و برنامه یک قرض‌دارِ **تازه** با همان نام می‌ساخت.
        Console.WriteLine("── ۱۳) فاکتور به نامِ حسابِ فرعی ⇒ همان حسابِ فرعی");
        {
            var owner = Wait(host.Debtors.AddDebtorAsync("نعیم جان", "0700000022", false));
            var sub = Wait(host.Debtors.AddSubAccountAsync(owner.Id, "تانکر سوم"));
            Wait(win, debt.RefreshAsync());
            var peopleBefore = debt.Cards.Count;

            var inv = Wait(host.Invoices.AddAsync(new PumpYaqobi.Domain.Entities.Invoice
            {
                CustomerName = "تانکر سوم",
                DateShamsi = Shamsi.Today(),
                Fuel = PumpYaqobi.Domain.Enums.FuelType.Petrol,
                Liters = 75m,
                PricePerLiter = 61m,
            }));
            Wait(host.Invoices.ApproveAsync(inv.Id, 63m));

            Wait(win, debt.RefreshAsync());
            Check("قرض‌دارِ تکراری برای نامِ حسابِ فرعی ساخته نشد",
                  debt.Cards.Count == peopleBefore, $"{peopleBefore} ← {debt.Cards.Count}");

            var full = Wait(host.Debtors.LoadFullAsync(owner.Id));
            var rows = full is null ? new List<PumpYaqobi.Domain.Entities.DebtRow>()
                     : full.AllAccounts().SelectMany(a => a.FuelRows.Concat(a.MoneyRows)).ToList();
            var mine = rows.FirstOrDefault(r => r.InvoiceId == inv.Id);
            Check("ردیفِ رسیدِ فاکتور در همان حسابِ فرعی نشست",
                  mine is not null && (mine.FuelAccountId == sub.Id || mine.MoneyAccountId == sub.Id),
                  mine is null ? "ردیفی نیست"
                  : (mine.FuelAccountId == sub.Id || mine.MoneyAccountId == sub.Id
                     ? "فرعی" : "حسابِ اصلی"));
            Check("و رسیدِ تیلش همان ۷۵ لیتر است",
                  mine is not null && mine.RasidFuel == 75m, mine?.RasidFuel.ToString());
        }

        // ── ۱۴) کارتِ ورودِ پروفایل: واقعاً لاگین می‌شود؟ ────────────────────
        //
        // خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «تست بزن ببین کار می‌کنه، لاگین
        // می‌شه یا که نه.» پس این بند خودِ فرم را پر می‌کند و دکمه را می‌زند،
        // و بعد **دیتابیس و تنظیمات** را می‌خواند — نه متغیرهای روی صفحه.
        //
        // ⚠️ فعال‌سازیِ واقعیِ کدِ شش‌رقمی اینترنت و سرورِ ابر می‌خواهد، پس آن
        // یک بند پشتِ ‎PUMP_VERIFY_CLOUD=1‎ است و در CI نمی‌دود: نباید هر اجرا
        // یک کدِ الکی به سرورِ واقعیِ اشتراک بفرستد.
        Console.WriteLine("── ۱۴) ثبت‌نام و ورود — دو گام");
        {
            var account = (AccountSectionViewModel)vm.Sections.First(s => s.Id == "account");
            host.Settings.Set(SettingsService.StationName, "پمپ آزمون");
            Wait(win, vm.GoAsync(account));
            for (var i = 0; i < 60; i++) Pump(win);          // عکس روی نخِ دیگر باز می‌شود

            Check("صفحهٔ پروفایل باز شد", vm.Content == account);
            //  ⚠️ «این آدمک‌ها هم باشند» — و از ۱۴۰۵/۰۶/۲۹ **برداری**‌اند
            //  («باسازی کن… با رنگ و تمِ خودِ برنامه… با کیفیتِ خیلی بالا»).
            //  پس به‌جای اندازهٔ بیت‌مپ، خودِ نقشه را در درخت می‌سنجیم.
            //  سنجهٔ کاملش `loginart` است.
            var artwork = win.GetVisualDescendants()
                             .OfType<PumpYaqobi.App.Controls.LoginArt>()
                             .FirstOrDefault(a => a.IsEffectivelyVisible);
            var vector = artwork?.GetVisualDescendants().OfType<Image>()
                                 .FirstOrDefault()?.Source as Avalonia.Svg.Skia.SvgImage;
            Check("آدمک‌های برداری آمدند و کشیده شدند",
                  artwork is not null && artwork.Bounds.Width > 100 && vector is not null,
                  artwork is null ? "نیامد"
                  : $"{artwork.Bounds.Width:0}×{artwork.Bounds.Height:0} · {(vector is null ? "بی نقشه" : "SVG")}");
            //  ⚠️ «این صفحهٔ لاگین اولویت باشد و تمامِ صفحه همین را نشان بدهد
            //  برای کسانی که حساب ندارند» — پس تا گامِ سه، خودِ پروفایل دیده
            //  نمی‌شود.
            Check("صفحهٔ ورود اولویت دارد و خودِ پروفایل دیده نمی‌شود",
                  account.ShowLoginPage && !account.ShowProfilePage,
                  $"ورود={account.ShowLoginPage} · پروفایل={account.ShowProfilePage}");

            //  الف) گامِ اول باید گامِ «حساب» باشد — چون وارد نشده‌ایم
            Check("گامِ اول، گامِ حساب است", account.LoginStep == 1 && account.StepAccount,
                  "گامِ " + account.LoginStep);
            Check("و «حساب می‌سازم» / «حساب دارم» هر دو در دسترس‌اند",
                  account.SetSignUpCommand.CanExecute("yes") && account.SetSignUpCommand.CanExecute("no"));

            //  ب) هر چهار خطای گامِ اول — و هیچ‌کدام از گام جلو نمی‌رود
            account.SetSignUpCommand.Execute("yes");
            account.LoginName = "هارون یعقوبی";
            account.LoginEmail = "bi-at";
            account.LoginPassword = "123456";
            account.LoginPassword2 = "123456";
            Wait(win, account.AccountStepCommand.ExecuteAsync(null));
            Check("ایمیلِ غلط رد شد و گام جلو نرفت",
                  account.LoginStatus.Contains("ایمیل") && account.LoginStep == 1, account.LoginStatus);

            account.LoginEmail = "test@gmail.com";
            account.LoginPassword = "123";
            account.LoginPassword2 = "123";
            Wait(win, account.AccountStepCommand.ExecuteAsync(null));
            //  ⚠️ هشت نویسه — همان قاعدهٔ خودِ سرور، وگرنه کاربر رمزِ
            //  شش‌نویسه می‌زد و سرور ردش می‌کرد
            Check("رمزِ کوتاه رد شد", account.LoginStatus.Contains("هشت نویسه") && account.LoginStep == 1,
                  account.LoginStatus);

            account.LoginPassword = "Ramz-1234";
            account.LoginPassword2 = "Ramz-4321";
            Wait(win, account.AccountStepCommand.ExecuteAsync(null));
            Check("دو رمزِ ناهمسان رد شد", account.LoginStatus.Contains("یکی نیستند") && account.LoginStep == 1,
                  account.LoginStatus);

            account.SetSignUpCommand.Execute("yes");
            account.LoginName = "ا";
            account.LoginPassword = "Ramz-1234"; account.LoginPassword2 = "Ramz-1234";
            Wait(win, account.AccountStepCommand.ExecuteAsync(null));
            Check("نامِ خالی در «حساب می‌سازم» رد شد",
                  account.LoginStatus.Contains("نامتان") && account.LoginStep == 1, account.LoginStatus);

            //  ⚠️ پذیرشِ شرایط اجباریِ خودِ سرور است — پس بی آن هیچ
            //  درخواستی هم نباید برود
            account.LoginName = "هارون یعقوبی";
            account.AcceptTerms = false;
            Wait(win, account.AccountStepCommand.ExecuteAsync(null));
            Check("بی پذیرشِ شرایط، گام جلو نرفت",
                  account.LoginStatus.Contains("شرایط") && account.LoginStep == 1, account.LoginStatus);

            //  ب‌ب) ⭐ **ریمیکِ ۱۴۰۵/۰۷/۰۵ — چهار بندِ خواسته، با رفتار**
            //
            //  «اون سه مرحله نباید دیده بشن… شرایط و ضوابط باید با زدنش بره
            //  صفحهٔ جداگانه… لاگین رو وسط صفحه‌اش بزار… منطق دست نخوره.»
            //
            //  ⚠️ سه‌تای اول **ظاهر**اند و سنجهٔ سورسشان در `AppLinksTests`
            //  است؛ آن‌چه این‌جا می‌سنجیم چیزی است که فقط با اجرا معلوم
            //  می‌شود: صفحهٔ شرایط واقعاً باز و بسته می‌شود، متنش می‌آید،
            //  و تیکِ پذیرش همان `AcceptTerms`ِ همیشگی است — نه یک حالتِ
            //  تازه که بی‌صدا از فرم جدا افتاده باشد.
            Check("⛔ پیش از زدنِ «شرایط و ضوابط»، صفحه‌اش بسته است",
                  !account.ShowTerms);
            Wait(win, account.LoadTermsCommand.ExecuteAsync(null));
            for (var i = 0; i < 10; i++) Pump(win);
            Check("⭐ زدنِ «شرایط و ضوابط» صفحهٔ جدا را باز کرد",
                  account.ShowTerms, "ShowTerms=" + account.ShowTerms);
            Check("و متنِ شرایط آمد", account.TermsText.Length > 0,
                  account.TermsText.Length + " نویسه");
            //  ⚠️ تیکِ داخلِ همان صفحه **همان** `AcceptTerms` است: کسی که
            //  تا ته متن آمده نباید برای زدنِ تیک برگردد.
            account.AcceptTerms = true;
            Wait(win, account.LoadTermsCommand.ExecuteAsync(null));
            for (var i = 0; i < 10; i++) Pump(win);
            Check("⭐ «بستن و برگشت» همان صفحه را بست",
                  !account.ShowTerms && account.AcceptTerms);
            //  ⛔ و گام‌ها فقط از صفحه رفتند، نه از منطق
            Check("⛔ گام‌ها سرِ جایشان‌اند، فقط شمرده نمی‌شوند",
                  account.StepAccount && !account.StepEmailCode && !account.StepPump);
            account.AcceptTerms = false;

            //  ج) «‹ برگشت به برنامه» — تنها راهِ «بی حساب ادامه بده»، و
            //     واقعاً کارش را می‌کند.
            //
            //  ⛔ دکمهٔ «بعداً — فعلاً بی حساب ادامه می‌دهم» از ۱۴۰۵/۰۶/۳۰
            //  برداشته شد: **دروغ می‌گفت.** `LoginSkipped` نمی‌گذاشت و فقط
            //  به گامِ کدِ پمپ می‌رفت، یعنی کاربر همچنان داخلِ همان دیوارِ
            //  ورود می‌ماند. (گزارشِ صاحب ریپو با عکس: «این نباشه و کار
            //  نمی‌کنه».)
            account.LoginName = "هارون یعقوبی";
            Wait(win, account.CloseLoginCommand.ExecuteAsync(null));
            for (var i = 0; i < 10; i++) Pump(win);
            var f1 = AppSettings.Load();
            Check("⭐ «برگشت به برنامه» واقعاً از صفحهٔ ورود بیرون برد",
                  account.StepDone && !account.ShowLoginPage, "گامِ " + account.LoginStep);
            Check("و نشانِ «بی حساب ادامه بده» روی دیسک نشست", f1.LoginSkipped);
            Check("⭐ و آن‌چه تایپ شده بود گم نشد", f1.CloudEmail == "test@gmail.com"
                  && f1.CloudName == "هارون یعقوبی", f1.CloudEmail + " · " + f1.CloudName);
            Check("⛔ و رمز هیچ‌جا نماند", account.LoginPassword.Length == 0
                  && account.LoginPassword2.Length == 0);

            //  و حالا برگرد به گامِ پمپ تا بندِ بعدی سنجیده شود
            account.BackToPumpCommand.Execute(null);
            for (var i = 0; i < 10; i++) Pump(win);

            //  د) گامِ پمپ: فقط نامِ پمپ سنجیده می‌شود
            //  ⛔ کادرِ دومِ کدِ شش‌رقمی در ۱۴۰۵/۰۷/۰۴ از این گام رفت —
            //  «بعد از کد شش رقمی یک کد شش رقمی دیگه می‌خواد، اون چیه؟
            //  اون رو حذف کن.» پس این‌جا دیگر کدی سنجیده نمی‌شود؛ کد
            //  جای خودش، در کارتِ اشتراکِ پروفایل، سنجیده می‌شود.
            account.LoginPump = "";
            Wait(win, account.FinishPumpCommand.ExecuteAsync(null));
            Check("نامِ پمپِ خالی رد شد", account.LoginStatus.Contains("نامِ پمپ") && account.StepPump,
                  account.LoginStatus);

            account.LoginPump = "پمپِ نو";
            account.LoginLocation = "هرات، جادهٔ کندهار";

            //  ⚠️ «هیچ توکنی نساخت» با «توکن را عوض نکرد» یکی نیست.
            //  این سنجه با یک نصبِ **پلن‌دار** می‌دود (بالای `Run`)، پس
            //  توکنِ دستگاه از قبل هست و سنجشِ «خالی باشد» حرفِ درستی
            //  نمی‌زد: کدِ ناقص نه توکنی می‌سازد و نه توکنِ سالم را
            //  جابه‌جا می‌کند.
            var tokenBefore = AppSettings.Load().CloudDeviceToken;
            account.SubCode = "123";
            Wait(win, account.RedeemSubCommand.ExecuteAsync(null));
            Check("کدِ سه‌رقمیِ کارتِ اشتراک رد شد",
                  account.SubMessage.Contains("شش رقم") && account.StepPump, account.SubMessage);
            Check("و کدِ ناقص توکنِ دستگاه را نه ساخت و نه عوض کرد",
                  AppSettings.Load().CloudDeviceToken == tokenBefore,
                  tokenBefore.Length == 0 ? "بی توکن" : "دست‌نخورده");
            account.SubCode = "";

            //  ه) نامِ پمپ و لوکیشن **پیش از** رفتن به سرور می‌نشینند
            if (Environment.GetEnvironmentVariable("PUMP_VERIFY_CLOUD") == "1")
            {
                Wait(win, account.FinishPumpCommand.ExecuteAsync(null));
                Check("ساختنِ پمپ به سرور رفت و جوابش نشست (بی کرش)",
                      account.LoginStatus.Length > 0 || account.LoginStep == 3, account.LoginStatus);
            }
            else
            {
                Console.WriteLine("  ⚠️ ساختنِ واقعیِ پمپ روی سرور نسنجیده ماند "
                                  + "(PUMP_VERIFY_CLOUD=1 لازم است — اینترنت و سرورِ حساب می‌خواهد)");
                Wait(win, account.FinishPumpCommand.ExecuteAsync(null));   // بی‌اینترنت: باید خطا بدهد، نه کرش
                Check("بی سرور، خطای روشن می‌دهد و در گامِ پمپ می‌ماند",
                      account.StepPump && account.LoginStatus.StartsWith("❌"), account.LoginStatus);
            }
            Check("ولی نامِ پمپ و لوکیشن همان لحظه ذخیره شدند",
                  host.Settings.GetString(SettingsService.StationName) == "پمپِ نو"
                  && host.Settings.GetString(SettingsService.StationAddress) == "هرات، جادهٔ کندهار",
                  host.Settings.GetString(SettingsService.StationName) + " · "
                  + host.Settings.GetString(SettingsService.StationAddress));
            Check("و خودِ پروفایل همان لحظه نامِ تازه را نشان می‌دهد",
                  account.PumpName == "پمپِ نو", account.PumpName);

            //  و) «بعداً»ی گامِ دو — دفترِ کاربر هیچ‌وقت گروگان نیست
            account.SkipPumpCommand.Execute(null);
            for (var i = 0; i < 10; i++) Pump(win);
            Check("«بعداً»ی گامِ پمپ به «تمام» برد", account.StepDone, "گامِ " + account.LoginStep);
            Check("و از آن پس خودِ پروفایل دیده می‌شود، نه صفحهٔ ورود",
                  account.ShowProfilePage && !account.ShowLoginPage,
                  $"ورود={account.ShowLoginPage} · پروفایل={account.ShowProfilePage}");

            //  ز) و راهِ برگشت به صفحهٔ ورود از خودِ پروفایل
            account.OpenAccountPageCommand.Execute(null);
            for (var i = 0; i < 10; i++) Pump(win);
            Check("«حساب و ورود» صفحهٔ ورود را برمی‌گرداند",
                  account.LoginStep == 1 && account.ShowLoginPage);

            //  ح) برگشت‌ها
            account.BackToPumpCommand.Execute(null);
            Check("«تغییرِ پمپ» به گامِ پمپ می‌برد", account.StepPump, "گامِ " + account.LoginStep);
            account.BackToAccountCommand.Execute(null);
            Check("«برگشت به حساب» به گامِ یک می‌برد", account.LoginStep == 1 && account.StepAccount);
        }

        // ── ۱۵) بخشِ «اشتراک و پلن‌ها» و کلیدِ آزمایش ────────────────────────
        //
        // «بخشِ وی‌آی‌پی را هم اعمال کن که من ببینم و تست کنم» — پس این‌جا هم
        // با دست زده می‌شود: کلیدِ آزمایش باید قفل‌ها را ببندد و بعد برگرداند،
        // و پشتیبانی در هر دو حال باز بماند.
        Console.WriteLine("── ۱۵) اشتراک و پلن‌ها + کلیدِ آزمایشِ بی‌اشتراک");
        {
            var account = vm.Sections.First(s => s.Id == "account");
            var vip = (VipSectionViewModel)account.SubSections.First(s => s.Id == "vip");
            account.ShowSubCommand.Execute(vip);
            for (var i = 0; i < 20; i++) Pump(win);
            Check("صفحهٔ اشتراک و پلن‌ها باز شد", vm.Content == vip);
            Check("چهار پلن با قیمتِ خالی", vip.Plans.Count == 4
                  && vip.Plans.Count(p => p.Price == "—") == 3, $"{vip.Plans.Count} پلن");
            Check("پشتیبانی همیشه باز است", vip.SupportText.StartsWith("✅"), vip.SupportText);

            var karBefore = Entitlements.Allows(Entitlements.Kar);
            vip.ToggleTestCommand.Execute(null); Pump(win);
            Check("کلیدِ آزمایش روشن شد", vip.TestDeny);
            Check("با آزمایش، اپِ کارمندان بسته است",
                  !Entitlements.Allows(Entitlements.Kar) && vip.KarText.StartsWith("🔒"), vip.KarText);
            Check("ولی پشتیبانی با آزمایش هم باز است",
                  Entitlements.Allows(Entitlements.Support), vip.SupportText);
            vip.ToggleTestCommand.Execute(null); Pump(win);
            Check("خاموش کردنش همان حالِ واقعی را برمی‌گرداند",
                  !vip.TestDeny && Entitlements.Allows(Entitlements.Kar) == karBefore);
        }

        // ── ۱۶) صفحهٔ «اپِ گوشی»: دو لینک و پیامِ آماده ─────────────────────
        Console.WriteLine("── ۱۶) اپِ گوشی — لینک و کد");
        {
            var settings = vm.Sections.First(s => s.Id == "settings");
            var apps = (AppsSectionViewModel)settings.SubSections.First(s => s.Id == "apps");
            Wait(win, vm.GoAsync(settings));
            settings.ShowSubCommand.Execute(apps);
            for (var i = 0; i < 20; i++) Pump(win);
            Check("صفحهٔ اپِ گوشی باز شد", vm.Content == apps);
            Check("لینکِ اندروید فایلِ نصبِ کارمندان است",
                  apps.AndroidLink.EndsWith("/downloads/PumpYaqobiKar.apk"), apps.AndroidLink);
            Check("لینکِ آیفون همان صفحهٔ اپ است",
                  apps.IphoneLink.EndsWith("/kar/"), apps.IphoneLink);
            Check("پیامِ آماده هر دو لینک را دارد و هیچ رمزی ندارد",
                  apps.ShareText.Contains(apps.AndroidLink) && apps.ShareText.Contains(apps.IphoneLink)
                  && !apps.ShareText.Contains("token"));
        }

        // ── ۱۷) قفلِ پلن: می‌بندد، بی‌صدا نیست، و داده را پاک نمی‌کند ───────
        /*
         *  چهار قاعدهٔ `native/docs/PLANS-fa.md` با رفتارِ واقعی سنجیده
         *  می‌شوند، نه از روی کد:
         *    ۱) داده پاک نمی‌شود، فقط دیده نمی‌شود
         *    ۲) دفترِ خودِ کاربر هیچ‌وقت بسته نمی‌شود
         *    ۳) قفل بی‌صدا نیست — می‌گوید چرا
         *    ۴) با برگشتنِ پلن، همان لحظه باز می‌شود
         *
         *  ⚠️ کلیدِ «آزمایشِ حالتِ بی‌اشتراک» تنها راهِ دیدنِ قفل‌هاست و
         *  **فقط می‌بندد** (`Entitlements.TestDeny`)، پس همین‌جا هم راهِ
         *  دور زدنِ اشتراک نمی‌شود.
         */
        Console.WriteLine("── ۱۷) قفلِ پلن — بستن، دلیل، و باز شدنِ دوباره");
        {
            var settings = vm.Sections.First(s => s.Id == "settings");
            var apps = (AppsSectionViewModel)settings.SubSections.First(s => s.Id == "apps");
            var histVm = vm.Sections.OfType<HistorySectionViewModel>().First();
            var safeVm = (SafeSectionViewModel)vm.Sections.First(s => s.Id == "safe");

            Entitlements.TestDeny = true;
            try
            {
                //  ۳) بی‌صدا نیست
                Check("قفلِ تاریخچه‌ها دلیل دارد", Entitlements.Why(Entitlements.History).Length > 0,
                      Entitlements.Why(Entitlements.History));
                Check("قفلِ اپِ کارمندان دلیل دارد", Entitlements.Why(Entitlements.Kar).Length > 0);

                //  قفل واقعاً می‌بندد — دکمهٔ تاریخچه جایی نمی‌رود
                Wait(win, vm.GoAsync(safeVm)); Pump(win);
                safeVm.OpenHistoryCommand.Execute(null);
                for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
                Check("بی پلن، تاریخچه‌ها باز نمی‌شود", vm.Current?.Id != "history", vm.Current?.Id);

                //  ۱) و داده پاک نشده: خودِ بخش سرِ جایش است
                Check("ولی خودِ بخشِ تاریخچه‌ها پاک نشده", histVm is not null);

                //  ۲) دفترِ خودِ کاربر باز است
                Check("دفترِ خودِ کاربر بسته نمی‌شود",
                      Entitlements.Allows("debtors") && Entitlements.Allows("safe")
                      && Entitlements.Allows(Entitlements.Support));

                //  پیامِ آماده خالی است، ولی کد از دیسک پاک نشده
                Wait(win, vm.GoAsync(settings));
                settings.ShowSubCommand.Execute(apps);
                //  ⚠️ صریح، نه با تکیه بر فرمان: صفحه‌ای که از قبل باز است
                //  دوباره فعال نمی‌شود و `Show()` نمی‌دود، پس سنجه یک قدم
                //  عقب می‌ماند و **سبزِ دروغ** می‌دهد.
                Wait(win, apps.OnActivatedAsync());
                for (var i = 0; i < 20; i++) Pump(win);
                Check("بی پلن، پیامِ آمادهٔ واتساپ داده نمی‌شود", apps.ShareText.Length == 0);
                Check("و دلیلش نوشته شده", apps.CodeHint.Length > 0, apps.CodeHint);
                Check("ولی کدِ پمپ روی دیسک پاک نشده",
                      AppSettings.Load().CloudAccessCode == accessCode, AppSettings.Load().CloudAccessCode);
                Check("و دو لینک همیشه هستند — قفلِ پلن نیستند",
                      apps.AndroidLink.Length > 0 && apps.IphoneLink.Length > 0);
            }
            finally { Entitlements.TestDeny = false; }

            //  ۴) با برگشتنِ پلن، همان لحظه باز می‌شود
            Wait(win, apps.OnActivatedAsync());
            for (var i = 0; i < 20; i++) Pump(win);
            Check("با برگشتنِ پلن، پیامِ آماده همان لحظه برمی‌گردد",
                  apps.ShareText.Contains(apps.AndroidLink) && apps.ShareText.Contains(apps.IphoneLink));
            Wait(win, vm.GoAsync(safeVm)); Pump(win);
            safeVm.OpenHistoryCommand.Execute(null);
            for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }
            Check("و تاریخچه‌ها دوباره باز می‌شود", vm.Current?.Id == "history", vm.Current?.Id);
        }

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ هر هفده رفتار همان‌طور که خواسته شده کار می‌کند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>همان ‎Wait‎، برای کاری که مقدار برمی‌گرداند.</summary>
    private static T Wait<T>(Task<T> t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        return t.GetAwaiter().GetResult();
    }

    private static void Wait(Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        t.GetAwaiter().GetResult();
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        t.GetAwaiter().GetResult(); Pump(w);
    }
    private static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
