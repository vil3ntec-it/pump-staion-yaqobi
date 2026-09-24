using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «ورق‌ها را پر کردم، برنامه را بستم، دوباره که آمدم هیچ ورقی نبود» ═══════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «ورق‌ها ساخته می‌شد و درست بودن و برنامه
/// رو که می‌بستم ثبت نمی‌شدن… ولی توی پارچه‌ها گزارش‌هاش بودن.»
///
/// ⚠️ این سنجه عمداً در <b>دو فرآیندِ جدا</b> می‌دود، چون ادعا دربارهٔ «بستن و
/// دوباره باز کردن» است — در یک فرآیند، ویومدل‌ها و کَش‌ها هنوز در حافظه‌اند
/// و «هست» را حتی وقتی روی دیسک نیست نشان می‌دهند:
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- persist write &lt;پوشه&gt; [close|crash]
///     dotnet run --project PumpYaqobi.UiTests -c Release -- persist check &lt;پوشه&gt;
///     dotnet run --project PumpYaqobi.UiTests -c Release -- persist          ⇐ هر دو، پشتِ سرِ هم
///
/// <b>close</b> = کاربر ✕ را می‌زند. <b>crash</b> = برق رفت: فرآیند بی هیچ
/// بستنی می‌میرد، درست پس از تایپ.
/// </summary>
internal static class PersistProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    /// <summary>آن‌چه در فرآیندِ اول نوشته می‌شود و فرآیندِ دوم باید ببیند.</summary>
    private const string TxnName = "حسن رضایی";
    private const string TxnLiters = "25";
    private const string MidEditName = "نیمه‌کاره";   // تایپ شد، ‎Enter‎ نخورد
    private const string SecondDate = "1405/06/20";
    private const string SecondName = "کریم جان";

    public static int Run(string[] args)
    {
        if (args.Length >= 2 && args[1] == "probe") return MidEditProbe();
        if (args.Length >= 3 && args[1] == "write") return Write(args[2], args.Length > 3 ? args[3] : "close");
        if (args.Length >= 3 && args[1] == "check") return CheckPhase(args[2], args.Length > 3 ? args[3] : "close");
        return Both(args.Length > 1 ? args[1] : null);
    }

    /// <summary>هر دو حالت، هر کدام در دو فرآیندِ تازه.</summary>
    private static int Both(string? only)
    {
        var exe = Environment.ProcessPath!;
        var dll = typeof(PersistProbe).Assembly.Location;
        var bad = 0;
        foreach (var mode in only is null ? new[] { "close", "crash", "newmonth", "widths" } : new[] { only })
        {
            var dir = Path.Combine(Path.GetTempPath(), "pump-persist-" + mode + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            Console.WriteLine();
            Console.WriteLine($"════ حالتِ «{mode}» ════");
            foreach (var phase in new[] { "write", "check" })
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe)
                { UseShellExecute = false };
                if (Path.GetFileNameWithoutExtension(exe) == "dotnet") psi.ArgumentList.Add(dll);
                foreach (var a in new[] { "persist", phase, dir, mode }) psi.ArgumentList.Add(a);
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.WaitForExit();
                if (phase == "check" && p.ExitCode != 0) bad++;
                if (phase == "write" && p.ExitCode != 0 && mode != "crash") bad++;
            }
        }
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "✅ هر چه نوشته شد، پس از بستن و باز کردن سرِ جایش بود"
                                   : $"❌ {bad} حالت چیزی را گم کرد");
        return bad == 0 ? 0 : 1;
    }

    // ══ فرآیندِ اول: نوشتن ═════════════════════════════════════════════════

    private static int Write(string dir, string mode)
    {
        var (win, vm) = Open(dir);
        if (mode == "newmonth") return WriteLastMonth(dir, win, vm);
        if (mode == "widths") return WriteWidths(dir, win, vm);

        var pa = (ParchaSectionViewModel)vm.Sections.First(s => s.Id == "shifts");
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");

        // ── ۱) پارچهٔ روز ⇒ ورقِ همان روز (همان راهِ کاربر) ──
        Wait(win, vm.GoAsync(pa));
        var date = pa.PaDate;
        pa.Day.Name = "کارمندِ روز"; pa.Day.Start = "100"; pa.Day.End = "300"; pa.Day.Price = "64";
        pa.Day.SaveCommand.Execute(null);
        Settle(win);
        File.WriteAllText(Path.Combine(dir, "date.txt"), date);

        // ── ۲) ورق را باز کن و با صفحه‌کلید در جدول بنویس ──
        Wait(win, vm.GoAsync(wq));
        var sheet = wq.Sheets.FirstOrDefault(w => w.DateShamsi == date);
        Check("ورقِ پارچه ساخته شد", sheet is not null, date);
        if (sheet is null) return 1;
        wq.OpenCommand.Execute(sheet);
        Settle(win);

        var grid = TxnGrid(win);
        if (grid is null) { Check("جدولِ ردیف‌های ورق پیدا شد", false); return 1; }
        TypeCell(win, grid, row: 0, header: "نام", text: TxnName, commit: true);
        TypeCell(win, grid, row: 0, header: "مقدار تیل", text: TxnLiters, commit: true);

        // ── ۳) ورقِ دوم، با تاریخِ دیگر — همان «ورقِ تازه»ی کاربر ──
        var second = AppHost.Current.WaraqData.OpenOrCreateAsync(SecondDate, "").GetAwaiter().GetResult();
        wq.Month = "1405/06"; Settle(win);
        wq.OpenCommand.Execute(wq.Sheets.First(w => w.Id == second.Id));
        Settle(win);
        grid = TxnGrid(win)!;
        TypeCell(win, grid, row: 1, header: "نام", text: SecondName, commit: true);

        // ── ۴) و یکی که تایپ شده ولی Enter نخورده ──
        TypeCell(win, grid, row: 2, header: "نام", text: MidEditName, commit: false);

        if (mode == "crash")
        {
            //  برق رفت — بی بستن، بی هیچ پمپی. هر چه تا این لحظه روی دیسک
            //  نیست، گم شده است.
            Console.WriteLine("  ⚡ فرآیند بی بستن می‌میرد");
            Environment.Exit(0);
        }

        win.Close();
        for (var i = 0; i < 2000 && win.IsVisible; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(2); }
        Check("پنجره بسته شد", !win.IsVisible);
        return _bad == 0 ? 0 : 1;
    }

    // ══ فرآیندِ دوم: همان دفتر، از نو ═════════════════════════════════════

    private static int CheckPhase(string dir, string mode)
    {
        var (win, vm) = Open(dir);
        if (mode == "newmonth") return CheckLastMonth(dir, win, vm);
        if (mode == "widths") return CheckWidths(dir, win, vm);
        var date = File.ReadAllText(Path.Combine(dir, "date.txt"));
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        var pa = (ParchaSectionViewModel)vm.Sections.First(s => s.Id == "shifts");

        Wait(win, vm.GoAsync(pa));
        Check("پارچه سرِ جایش است", pa.Reports.Count > 0, pa.Reports.Count + " پارچه");

        Wait(win, vm.GoAsync(wq));
        var all = AppHost.Current.WaraqData.ListAsync(null).GetAwaiter().GetResult();
        Console.WriteLine("  ورق‌های روی دیسک: " + string.Join("، ", all.Select(w => w.DateShamsi)));
        Check("ورقِ پارچه پس از باز کردنِ دوباره هست", all.Any(w => w.DateShamsi == date), date);
        Check("ورقِ دوم هم هست", all.Any(w => w.DateShamsi == SecondDate), SecondDate);
        Check("فهرستِ صفحهٔ ورق‌ها همان ورقِ امروز را نشان می‌دهد",
              wq.Sheets.Any(w => w.DateShamsi == date),
              "ماهِ کادر: " + wq.Month + " · " + wq.Sheets.Count + " کارت");

        string Names(string d) => string.Join(" | ", all.Where(w => w.DateShamsi == d)
            .SelectMany(w => w.Shifts).SelectMany(s => s.Transactions)
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Select(t => t.Name + (t.Liters != 0 ? " (" + t.Liters + ")" : "")));

        var first = Names(date);
        var second = Names(SecondDate);
        Console.WriteLine("  ردیف‌های ورقِ اول: " + first);
        Console.WriteLine("  ردیف‌های ورقِ دوم: " + second);
        Check("نامِ ردیفِ ورقِ اول ذخیره شد", first.Contains(TxnName));
        Check("مقدارِ تیلِ همان ردیف ذخیره شد", System.Text.RegularExpressions.Regex.IsMatch(first, @"\(" + TxnLiters + @"(\.0+)?\)"));
        Check("نامِ ردیفِ ورقِ دوم ذخیره شد", second.Contains(SecondName));
        Check("متنِ نیمه‌کاره (بی Enter) هم ذخیره شد", second.Contains(MidEditName),
              mode == "crash" ? "برق رفت، پیش از زدنِ Enter" : "✕ زده شد، پیش از زدنِ Enter");

        Console.WriteLine(_bad == 0 ? "  ✅ همه سرِ جایش بود" : $"  ❌ {_bad} چیز گم شد");
        return _bad == 0 ? 0 : 1;
    }

    // ══ «ماه عوض شد» — همان روزی که گزارش آمد ════════════════════════════
    //
    // همه‌چیز در ماهِ **پیش** نوشته می‌شود (مثلِ ورق‌های سنبله که کاربر پر
    // کرده بود) و برنامه در ماهِ **تازه** باز می‌شود. پیش از اصلاح، صفحهٔ
    // ورق‌ها خالی بود و می‌نوشت «هیچ ورقی ثبت نشده».

    private static string LastMonth => Shamsi.MonthOf(DateTime.Now.AddDays(-35));
    private static string LastMonthDate => LastMonth + "/15";
    private const string ExpenseTitle = "برقِ دکان";

    private static int WriteLastMonth(string dir, MainWindow win, MainViewModel vm)
    {
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        var w = AppHost.Current.WaraqData.OpenOrCreateAsync(LastMonthDate, "").GetAwaiter().GetResult();
        Wait(win, vm.GoAsync(wq));
        wq.Month = LastMonth; Settle(win);
        wq.OpenCommand.Execute(wq.Sheets.First(x => x.Id == w.Id));
        Settle(win);
        TypeCell(win, TxnGrid(win)!, row: 0, header: "نام", text: TxnName, commit: true);

        AppHost.Current.ExpenseLedger.AddAsync(new PumpYaqobi.Domain.Entities.Expense
        { DateShamsi = LastMonthDate, Title = ExpenseTitle, Amount = 1500m }).GetAwaiter().GetResult();

        win.Close();
        for (var i = 0; i < 2000 && win.IsVisible; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(2); }
        return 0;
    }

    private static int CheckLastMonth(string dir, MainWindow win, MainViewModel vm)
    {
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(wq));
        Console.WriteLine($"  ماهِ جاری {Shamsi.ThisMonth()} · ماهِ ورق‌ها {LastMonth} · کادر: {wq.Month}");
        Check("ورق‌های ماهِ پیش در فهرست دیده می‌شوند", wq.Cards.Count > 0, wq.Cards.Count + " کارت");
        Check("نوار می‌گوید این ماه هنوز ورقی ندارد", wq.HasMonthHint, wq.MonthHint);
        Check("«هیچ ورقی ثبت نشده» گفته نمی‌شود", !wq.EmptyText.StartsWith("هیچ ورقی ثبت نشده"));
        //  «بفهمونه که ماه عوض شده نه حساب‌ها پاک شدن»
        Check("نوار صریح می‌گوید ماه عوض شده و چیزی پاک نشده",
              wq.MonthHint.Contains("شروع شد") && wq.MonthHint.Contains("پاک نشده"), wq.MonthHint);
        Check("نوار در پوستهٔ پنجره واقعاً دیده می‌شود", HintBarShown(win), "");
        Shot(win, dir, "waraq-newmonth");
        //  «مزاحمت ایجاد نکنه»: یک «×» و همان لحظه می‌رود، و ورق‌ها سرِ جایشان
        wq.DismissMonthHintCommand.Execute(null);
        Settle(win);
        Check("«×» نوار را می‌بندد", !wq.HasMonthHint && !HintBarShown(win), wq.MonthHint);
        Check("بستنِ نوار ورقی را پنهان نمی‌کند", wq.Cards.Count > 0, wq.Cards.Count + " کارت");

        var ex = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(ex));
        Check("مصارفِ ماهِ تازه خالی است و نوار می‌گوید ماهِ پیش سرِ جایش است",
              ex.HasMonthHint, ex.MonthHint);
        Shot(win, dir, "expenses-newmonth-before");
        ex.GoMonthHintCommand.Execute(null);
        Settle(win);
        var rows = ((dynamic)ex).Rows.Count;
        Check("دکمهٔ نوار ردیف‌های ماهِ پیش را نشان می‌دهد", rows > 0, rows + " ردیف");
        Check("نوار پس از رفتن به آن ماه برداشته شد", !ex.HasMonthHint, ex.MonthHint);
        Shot(win, dir, "expenses-newmonth-after");

        Console.WriteLine(_bad == 0 ? "  ✅ همه سرِ جایش بود" : $"  ❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static bool HintBarShown(MainWindow win) =>
        win.GetVisualDescendants().OfType<Button>()
           .Any(b => b.IsEffectivelyVisible && Equals(b.Content, "✕")
                  && Equals(ToolTip.GetTip(b), "بستنِ این نوار"));

    // ══ «اندازهٔ جدول ثبت نمی‌شد — و هر کی برای خودش» ══════════════════════
    //
    // ستونی در حسابِ قرض‌دارِ «الف» با دست پهن می‌شود. سه چیز باید راست
    // باشد: (۱) حسابِ «ب» همان پهنا را **نمی‌گیرد**، (۲) پس از بستن و باز
    // کردنِ برنامه، «الف» همان پهنا را دارد، (۳) و «ب» هنوز دست‌نخورده است.

    private const double Wide = 320;
    private const string WideHeader = "حواله";

    private static PumpYaqobi.App.Controls.ExcelGrid? PersonGrid(Window win) =>
        win.GetVisualDescendants().OfType<PumpYaqobi.App.Controls.ExcelGrid>()
           .FirstOrDefault(g => g.Name == "PersonGrid" && g.IsEffectivelyVisible);

    private static DebtSectionViewModel OpenDebt(MainWindow win, MainViewModel vm, int card)
    {
        var debt = (DebtSectionViewModel)vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(debt));
        Wait(win, debt.RefreshAsync());
        var cards = debt.Cards.OrderBy(c => c.Entity.Id).ToList();
        debt.OpenCommand.Execute(cards[card]);
        Settle(win); Settle(win);
        return debt;
    }

    private static double Width(Window win)
    {
        var g = PersonGrid(win);
        var c = g?.Columns.FirstOrDefault(x => (x.Header as string) == WideHeader && x.IsVisible);
        return c?.ActualWidth ?? -1;
    }

    private static int WriteWidths(string dir, MainWindow win, MainViewModel vm)
    {
        Seed.Fill(AppHost.Current);
        OpenDebt(win, vm, 0);
        var g = PersonGrid(win);
        var col = g?.Columns.FirstOrDefault(x => (x.Header as string) == WideHeader && x.IsVisible);
        Check("جدولِ حساب و ستونِ «" + WideHeader + "» پیدا شد", col is not null,
              string.Join("، ", g?.Columns.Where(x => x.IsVisible).Select(x => x.Header) ?? Array.Empty<object>()));
        if (col is null) return 1;
        var before = col.ActualWidth;
        col.Width = new DataGridLength(Wide, DataGridLengthUnitType.Pixel);   // همان کشیدنِ دستِ کاربر
        Settle(win);
        Check("ستونِ حسابِ «الف» پهن شد", Math.Abs(Width(win) - Wide) < 1, $"{before:0} ⇐ {Width(win):0}");
        Shot(win, dir, "widths-A-before-restart");

        OpenDebt(win, vm, 1);
        var b = Width(win);
        Check("حسابِ «ب» پهنای «الف» را نگرفت", Math.Abs(b - Wide) > 1, $"{b:0}px");
        Shot(win, dir, "widths-B-before-restart");

        win.Close();
        for (var i = 0; i < 2000 && win.IsVisible; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(2); }
        return _bad == 0 ? 0 : 1;
    }

    private static int CheckWidths(string dir, MainWindow win, MainViewModel vm)
    {
        OpenDebt(win, vm, 0);
        var a = Width(win);
        Check("پس از باز کردنِ دوباره، حسابِ «الف» همان پهنا را دارد", Math.Abs(a - Wide) < 1, $"{a:0}px");
        TotalsAligned(win, "حسابِ «الف»");
        Shot(win, dir, "widths-A-after-restart");
        OpenDebt(win, vm, 1);
        var b = Width(win);
        Check("و حسابِ «ب» هنوز پهنای خودش را دارد", Math.Abs(b - Wide) > 1, $"{b:0}px");
        TotalsAligned(win, "حسابِ «ب»");
        Shot(win, dir, "widths-B-after-restart");
        Console.WriteLine(_bad == 0 ? "  ✅ همه سرِ جایش بود" : $"  ❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>
    /// هر خانهٔ «جمله» باید زیرِ سرستونِ هم‌نامش باشد — لبه به لبه.
    /// </summary>
    private static void TotalsAligned(Window win, string what)
    {
        var g = PersonGrid(win);
        var strip = win.GetVisualDescendants().OfType<PumpYaqobi.App.Controls.TotalsStrip>()
                       .FirstOrDefault(t => t.IsEffectivelyVisible);
        if (g is null || strip is null) { Check(what + ": نوارِ جمله پیدا شد", false); return; }
        var heads = g.GetVisualDescendants().OfType<DataGridColumnHeader>()
                     .Where(h => h.Bounds.Width > 0).ToList();
        var bad = new List<string>();
        foreach (var child in strip.Children)
        {
            if (child.DataContext is not PumpYaqobi.App.Controls.TotalCell tc || tc.Column.Length == 0) continue;
            var head = heads.FirstOrDefault(h => (h.Content?.ToString() ?? "").Trim() == tc.Column);
            if (head is null) continue;
            var hb = head.TransformToVisual(win) is { } ht ? new Rect(head.Bounds.Size).TransformToAABB(ht) : default;
            var cb = child.TransformToVisual(win) is { } ct ? new Rect(child.Bounds.Size).TransformToAABB(ct) : default;
            if (Math.Abs(hb.Left - cb.Left) > 2 || Math.Abs(hb.Right - cb.Right) > 2)
                bad.Add($"{tc.Column}: سرستون {hb.Left:0}–{hb.Right:0} · جمله {cb.Left:0}–{cb.Right:0}");
        }
        Check(what + ": هر جمله زیرِ ستونِ خودش است", bad.Count == 0, string.Join(" | ", bad));
    }

    private static void Shot(Window win, string dir, string name)
    {
        Settle((MainWindow)win);
        using var f = win.CaptureRenderedFrame();
        var path = Path.Combine(dir, name + ".png");
        f?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    /// <summary>آزمایشِ یک‌باره: متنِ در حالِ تایپ کِی به ردیف می‌رسد؟</summary>
    private static int MidEditProbe()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-mid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (win, vm) = Open(dir);
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        var w = AppHost.Current.WaraqData.OpenOrCreateAsync(Shamsi.Today(), "").GetAwaiter().GetResult();
        Wait(win, vm.GoAsync(wq));
        wq.OpenCommand.Execute(wq.Sheets.First(x => x.Id == w.Id)); Settle(win);
        var grid = TxnGrid(win)!;
        TypeCell(win, grid, 0, "نام", "علی", commit: false);
        var row = wq.Page!.Txns[0];
        Console.WriteLine($"name-in-vm='{row.Name}' entity='{row.Entity.Name}' dirty={row.IsDirty}");
        TypeCell(win, grid, 1, "مقدار تیل", "1234", commit: false);
        var r1 = wq.Page!.Txns[1];
        Console.WriteLine($"liters-in-vm='{r1.LitersText}' entity={r1.Entity.Liters} dirty={r1.IsDirty}");
        return 0;
    }

    // ══ ابزار ═══════════════════════════════════════════════════════════════

    private static (MainWindow, MainViewModel) Open(string dir)
    {
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
        return (win, vm);
    }

    private static DataGrid? TxnGrid(Window win) =>
        win.GetVisualDescendants().OfType<DataGrid>()
           .Where(g => g.IsEffectivelyVisible)
           .FirstOrDefault(g => g.Columns.Any(c => (c.Header as string) == "مقدار تیل"));

    /// <summary>همان کاری که انگشتِ کاربر می‌کند: انتخاب، تایپ، و (شاید) Enter.</summary>
    private static void TypeCell(Window win, DataGrid grid, int row, string header, string text, bool commit)
    {
        var col = grid.Columns.First(c => (c.Header as string) == header);
        grid.Focus();
        grid.SelectedIndex = row;
        grid.CurrentColumn = col;
        Pump(win);
        win.KeyTextInput(text);
        Pump(win);
        if (commit)
        {
            win.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            win.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        }
        Settle(win);
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
        for (var i = 0; i < 400 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
