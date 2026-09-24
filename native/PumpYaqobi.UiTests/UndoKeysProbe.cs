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

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «شیفت با عدد حذف نمی‌کند، کنترول زد و وای کار نمی‌کند» ══════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «شیفت با عدد جدولی رو حذف نمیکنه… اگه
/// جدول یا کادرِ جدول پر بود تایید بخواد، اگه خالی بود حذف کنه بدونِ سوال…
/// کنترول زد اصلن کار نمیکنه — هر چیزی از کادر حذف بشه یا حسابی حذف بشه —
/// و کنترول وای هم جلو نمیره.»
///
/// همه‌چیز با <b>کلیدِ واقعی</b> و <b>کلیکِ واقعی</b> روی جدولِ واقعی زده
/// می‌شود — نه با صدا زدنِ فرمان. شکایت دربارهٔ کلید بود، پس سنجه هم کلید است.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- undokeys
/// </summary>
internal static class UndoKeysProbe
{
    private static readonly List<string> Bad = new();
    private static int _asked;
    private static bool _answer;

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-undo-" + Guid.NewGuid().ToString("N"), "pump.db");
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
        Pump(win);
        Seed.Fill(AppHost.Current);

        Dialogs.ConfirmHook = (_, _) => { _asked++; return _answer; };

        foreach (var id in new[] { "expenses", "safe" })
            Ledger(win, vm, id);
        Debtor(win, vm);
        PersonRows(win, vm);
        DoubleClickEdits(win, vm);
        TypingStillTypes(win, vm);
        GhostIsGrey(win, vm);
        AckInsideTheBox(win, vm);
        PriceLossIsEmpty(win, vm);
        Console.WriteLine();
        Console.WriteLine("عکس‌ها: " + ShotDir);

        Dialogs.ConfirmHook = null;
        Console.WriteLine();
        if (Bad.Count == 0) { Console.WriteLine("✅ حذف با شیفت، برگشت و دوباره — همه با کلیدِ واقعی کار می‌کنند"); return 0; }
        Console.WriteLine($"❌ {Bad.Count} ایراد:");
        foreach (var b in Bad) Console.WriteLine("   • " + b);
        return 1;
    }

    // ══ یک دفترِ ماهانه ═════════════════════════════════════════════════════

    private static void Ledger(Window win, MainViewModel vm, string id)
    {
        Console.WriteLine();
        Console.WriteLine($"── {id} ──");
        var sec = vm.Sections.First(s => s.Id == id);
        Wait(win, vm.GoAsync(sec));
        Settle(win);

        if (vm.RowHost is not { } host) { Fail($"{id}: هیچ جدولی جلوی کاربر نیست"); return; }

        // ── ۱) دو ردیفِ خالی ⇒ Shift+2 بی هیچ پرسشی ──
        Wait(win, host.AddRowsAsync(2));
        Settle(win);
        var grid = Grid(win);
        if (grid is null) { Fail($"{id}: جدول دیده نمی‌شود"); return; }
        ClickCell(win, grid, 0, 1);
        var n0 = host.RowCount;
        _asked = 0; _answer = false;
        ShiftDigit(win, PhysicalKey.Digit2);
        Settle(win);
        Check($"{id}: دو ردیفِ خالی با Shift+2 بی‌پرسش رفتند ({n0} ⇒ {host.RowCount}، پرسش {_asked})",
              host.RowCount == n0 - 2 && _asked == 0);

        // ── ۲) ردیفِ پر ⇒ Shift+1 می‌پرسد؛ «نه» ⇒ دست‌نخورده، «بله» ⇒ رفت ──
        ClickCell(win, grid, 0, 1);
        n0 = host.RowCount;
        _asked = 0; _answer = false;
        ShiftDigit(win, PhysicalKey.Digit1);
        Settle(win);
        Check($"{id}: ردیفِ پر ⇒ پرسید و با «نه» ماند ({n0} ⇒ {host.RowCount}، پرسش {_asked})",
              _asked == 1 && host.RowCount == n0);
        _asked = 0; _answer = true;
        ShiftDigit(win, PhysicalKey.Digit1);
        Settle(win);
        Check($"{id}: با «بله» رفت ({n0} ⇒ {host.RowCount})", _asked == 1 && host.RowCount == n0 - 1);

        // ── ۳) Ctrl+Z ردیفِ حذف‌شده را برمی‌گرداند و Ctrl+Y دوباره می‌برد ──
        Ctrl(win, PhysicalKey.Z);
        Settle(win);
        Check($"{id}: Ctrl+Z ردیفِ حذف‌شده را برگرداند ({host.RowCount})", host.RowCount == n0);
        Ctrl(win, PhysicalKey.Y);
        Settle(win);
        Check($"{id}: Ctrl+Y دوباره برد ({host.RowCount})", host.RowCount == n0 - 1);
        Ctrl(win, PhysicalKey.Z);
        Settle(win);

        // ── ۴) خالی کردنِ خانه با Delete ⇒ Ctrl+Z ⇒ Ctrl+Y ──
        grid = Grid(win)!;
        var col = FirstTextCol(grid);
        if (col < 0) { Fail($"{id}: ستونِ نوشتنی نبود"); return; }
        ClickCell(win, grid, 0, col);
        var before = CellText(grid, 0, col);
        Tap(win, PhysicalKey.Delete);
        Settle(win);
        var cleared = CellText(grid, 0, col);
        Check($"{id}: Delete خانه را خالی کرد («{before}» ⇒ «{cleared}»)", before != "" && cleared == "");
        FocusElsewhere(win);   // کاربر جای دیگری را کلیک کرده
        Ctrl(win, PhysicalKey.Z);
        Settle(win);
        var back = CellText(grid, 0, col);
        Check($"{id}: Ctrl+Z (با فوکوسِ بیرونِ جدول) خانه را برگرداند («{back}»)", back == before);
        Ctrl(win, PhysicalKey.Y);
        Settle(win);
        Check($"{id}: Ctrl+Y دوباره خالی‌اش کرد («{CellText(grid, 0, col)}»)", CellText(grid, 0, col) == "");
        Ctrl(win, PhysicalKey.Z);
        Settle(win);

        // ── ۵) تایپ روی خانه + Enter ⇒ Ctrl+Z ──
        ClickCell(win, grid, 0, col);
        before = CellText(grid, 0, col);
        win.KeyTextInput("۹");
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        var typed = CellText(grid, 0, col);
        Ctrl(win, PhysicalKey.Z);
        Settle(win);
        Check($"{id}: تایپ («{typed}») ⇒ Ctrl+Z ⇒ «{CellText(grid, 0, col)}»",
              typed == "۹" && CellText(grid, 0, col) == before);
    }

    // ══ حذفِ کلِ یک حسابِ قرض‌دار ⇒ Ctrl+Z ═══════════════════════════════════

    private static void Debtor(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── debt (حسابِ کامل) ──");
        var sec = vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(sec));
        Settle(win);

        var debtors = AppHost.Current.Debtors;
        var list = debtors.ListAsync().GetAwaiter().GetResult();
        var card = ((DebtSectionViewModel)sec).Cards.FirstOrDefault();
        if (card is null) { Fail("debt: قرض‌داری نبود"); return; }
        var victim = card.Entity;
        var count = list.Count;

        _answer = true;
        var del = sec.GetType().GetProperty("DeleteDebtorCommand")?.GetValue(sec)
                  as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;
        if (del is null) { Fail("debt: فرمانِ حذفِ حساب پیدا نشد"); return; }
        Wait(win, del.ExecuteAsync(card));
        Settle(win);
        var after = debtors.ListAsync().GetAwaiter().GetResult().Count;
        Check($"debt: حساب حذف شد ({count} ⇒ {after})", after == count - 1);

        FocusElsewhere(win);
        Ctrl(win, PhysicalKey.Z);
        Settle(win);
        var back = debtors.ListAsync().GetAwaiter().GetResult();
        Check($"debt: Ctrl+Z حساب را برگرداند ({back.Count})",
              back.Count == count && back.Any(d => d.Id == victim.Id));
        Ctrl(win, PhysicalKey.Y);
        Settle(win);
        Check($"debt: Ctrl+Y دوباره حذف کرد ({debtors.ListAsync().GetAwaiter().GetResult().Count})",
              debtors.ListAsync().GetAwaiter().GetResult().Count == count - 1);
        Ctrl(win, PhysicalKey.Z);
        Settle(win);
    }

    // ══ ردیفِ داخلِ حسابِ یک قرض‌دار ═══════════════════════════════════════

    private static void PersonRows(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── حسابِ قرض‌دار (ردیف) ──");
        var sec = (DebtSectionViewModel)vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(sec));
        Settle(win);
        var card = sec.Cards.FirstOrDefault();
        if (card is null) { Fail("person: کارتی نبود"); return; }
        Wait(win, sec.OpenCommand.ExecuteAsync(card));
        Settle(win);
        if (vm.RowHost is not { } host) { Fail("person: جدولِ حساب جلوی کاربر نیست"); return; }

        // یک ردیفِ پر در ته جدول، تا حذفش پرسش بخواهد
        Wait(win, host.AddRowsAsync(1));
        Settle(win);
        var grid = Grid(win);
        if (grid is null) { Fail("person: جدول دیده نمی‌شود"); return; }
        var last = host.LastRows(1).FirstOrDefault();
        var ent = last?.GetType().GetProperty("Entity")?.GetValue(last) as PumpYaqobi.Domain.Entities.DebtRow;
        if (ent is null) { Fail("person: ردیفِ تازه نبود"); return; }
        last!.GetType().GetProperty("Name")?.SetValue(last, "ردیفِ آزمایشیِ برگشت");
        Settle(win);
        ClickCell(win, grid, 0, 1);

        var n0 = host.RowCount;
        _asked = 0; _answer = true;
        ShiftDigit(win, PhysicalKey.Digit1);
        Settle(win);
        Check($"person: ردیفِ پر ⇒ پرسید و رفت ({n0} ⇒ {host.RowCount}، پرسش {_asked})",
              _asked == 1 && host.RowCount == n0 - 1);
        FocusElsewhere(win);
        Ctrl(win, PhysicalKey.Z);
        Settle(win);
        var back = vm.RowHost?.LastRows(1).FirstOrDefault();
        var name = back?.GetType().GetProperty("Name")?.GetValue(back) as string;
        Check($"person: Ctrl+Z همان ردیف را با نامش برگرداند ({vm.RowHost?.RowCount}، «{name}»)",
              vm.RowHost?.RowCount == n0 && name == "ردیفِ آزمایشیِ برگشت");
        using var ctx = AppHost.Current.Db.Create();
        var onDisk = ctx.DebtRows.Any(r => r.Id == ent.Id);
        Check("person: و روی دیسک هم برگشته است", onDisk);
    }

    // ══ دوبار-کلیک هنوز ویرایش را باز می‌کند (و کلیکِ تک نه) ═══════════════════

    private static void DoubleClickEdits(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── کلیکِ تک و دوبار-کلیک ──");
        var sec = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(sec));
        Settle(win);
        var grid = Grid(win);
        if (grid is null) { Fail("expenses: جدول دیده نمی‌شود"); return; }
        var col = FirstTextCol(grid);
        ClickCell(win, grid, 1, col);
        ClickCell(win, grid, 1, col);
        var focused = win.FocusManager?.GetFocusedElement();
        Check($"دو کلیکِ جدا روی همان خانه ⇒ ویرایش باز نشد ({focused?.GetType().Name})", focused is not TextBox);

        var cell = Cell(grid, 1, col)!;
        var p = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), win)!.Value;
        win.MouseDown(p, MouseButton.Left); win.MouseUp(p, MouseButton.Left);
        win.MouseDown(p, MouseButton.Left); win.MouseUp(p, MouseButton.Left);
        Settle(win);
        focused = win.FocusManager?.GetFocusedElement();
        Check($"دوبار-کلیک ⇒ ویرایش باز شد ({focused?.GetType().Name})", focused is TextBox);
        grid.CancelEdit();
        Settle(win);
    }

    // ══ و طرفِ دیگر: داخلِ کادرِ تایپ، Shift+عدد نویسه است ═══════════════════

    private static void TypingStillTypes(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── نویسه داخلِ کادرِ تایپ ──");
        var sec = vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(sec));
        Settle(win);
        var box = win.GetVisualDescendants().OfType<TextBox>()
                     .FirstOrDefault(t => t.IsEffectivelyVisible && t.IsEnabled);
        if (box is null) { Fail("کادرِ جست‌وجو پیدا نشد"); return; }
        box.Focus();
        Pump(win);
        var ev = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.D3, KeyModifiers = KeyModifiers.Shift, Source = box };
        box.RaiseEvent(ev);
        Check("Shift+3 داخلِ کادرِ تایپ خورده نمی‌شود", !ev.Handled);
    }

    // ══ عکس‌ها ══════════════════════════════════════════════════════════════

    private static readonly string ShotDir = Path.Combine(Path.GetTempPath(), "pump-undokeys");

    private static void Shot(Window w, string name)
    {
        Directory.CreateDirectory(ShotDir);
        using var frame = w.CaptureRenderedFrame();
        frame?.Save(Path.Combine(ShotDir, name));
    }

    // ══ تکملهٔ خودکار خاکستری است، نه هم‌رنگِ نوشتهٔ کاربر ═══════════════════

    private static void GhostIsGrey(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── تکملهٔ خودکار ──");
        var sec = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(sec));
        Settle(win);
        var grid = Grid(win);
        if (grid is null) { Fail("expenses: جدول دیده نمی‌شود"); return; }
        var col = FirstTextCol(grid);
        var word = CellText(grid, 0, col);
        if (word.Length < 3) { Fail("واژه‌ای برای تکمیل نبود"); return; }

        ClickCell(win, grid, 2, col);
        win.KeyTextInput(word[..1]);
        Settle(win);
        win.KeyTextInput(word[1..2]);
        Settle(win);
        var box = win.FocusManager?.GetFocusedElement() as TextBox;
        if (box is null) { Fail("کادرِ تایپ باز نشد"); return; }
        Check($"تکمله آمد («{box.Text}»)", PumpYaqobi.App.Controls.Suggest.Showing == 1);
        var muted = App("Pump.Ghost");
        var text = App("Pump.Text");
        var sel = (box.SelectionForegroundBrush as Avalonia.Media.ISolidColorBrush)?.Color;
        var selBg = (box.SelectionBrush as Avalonia.Media.ISolidColorBrush)?.Color;
        Check($"تکمله کم‌رنگ است: {sel} (نوشته {text}، کم‌رنگ {muted})",
              box.Classes.Contains("ghost") && sel == muted && sel != text);
        Check($"و زمینهٔ برجسته ندارد ({selBg})", selBg is { A: 0 });
        var tp = box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().FirstOrDefault();
        Console.WriteLine($"    presenter: sel-fg {(tp?.SelectionForegroundBrush as Avalonia.Media.ISolidColorBrush)?.Color} · sel {tp?.SelectionStart}..{tp?.SelectionEnd} · theme {box.Theme?.GetType().Name} · text-fg {(tp?.Foreground as Avalonia.Media.ISolidColorBrush)?.Color}");
        Shot(win, "ghost-completion.png");

        Tap(win, PhysicalKey.Tab);
        Settle(win);
        Check("با Tab پذیرفته شد و کلاسِ کم‌رنگ برداشته شد", !box.Classes.Contains("ghost"));
        grid.CancelEdit();
        Settle(win);
    }

    private static Avalonia.Media.Color? App(string key) =>
        Avalonia.Application.Current!.TryGetResource(key, Avalonia.Application.Current.ActualThemeVariant, out var v)
            ? (v as Avalonia.Media.ISolidColorBrush)?.Color ?? v as Avalonia.Media.Color? : null;

    // ══ «✔ دیدم» داخلِ خودِ کادرِ شروعِ پایه، بی فاصلهٔ اضافه ═════════════════

    private static void AckInsideTheBox(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── «دیدم» در کادرِ شروعِ پایه ──");
        var sec = vm.Sections.First(s => s.Id == "shifts");
        Wait(win, vm.GoAsync(sec));
        Settle(win);
        if (sec is not ParchaSectionViewModel parcha) { Fail("بخشِ پارچه‌ها نبود"); return; }

        TextBox? StartBox() => win.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(t => t.IsEffectivelyVisible && ReferenceEquals(t.DataContext, parcha.Day)
                              && t.InnerRightContent is not null);
        TextBlock? Label(string s) => win.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.IsEffectivelyVisible && t.Text == s && ReferenceEquals(t.DataContext, parcha.Day));

        double Gap()
        {
            var b = StartBox(); var l = Label("ختم پایه (لیتر)");
            if (b is null || l is null) return double.NaN;
            var bb = b.TranslatePoint(new Point(0, b.Bounds.Height), win)!.Value.Y;
            var lt = l.TranslatePoint(new Point(0, 0), win)!.Value.Y;
            return lt - bb;
        }

        var before = Gap();
        parcha.Day.LowBaseText = "⚠️ این شروع پایه از پایهٔ قبلی (250,000) کمتر است";
        parcha.Day.LowBase = true;
        Settle(win);
        var during = Gap();
        var box = StartBox();
        var ack = box?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("lowack"));
        Check($"«✔ دیدم» دیده می‌شود و داخلِ خودِ کادر است",
              ack is { IsEffectivelyVisible: true } && Inside(ack, box!, win));

        // فاصلهٔ کادر تا «ختم پایه» همان فاصلهٔ «نام» تا «شروع» باشد — نه ۲۲ پیکسلِ اضافه
        var name = win.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(t => t.IsEffectivelyVisible && ReferenceEquals(t.DataContext, parcha.Day)
                              && t.Watermark as string == "نام کارمند");
        var startLbl = Label("شروع پایه (لیتر)");
        var normal = name is null || startLbl is null ? double.NaN
            : startLbl.TranslatePoint(new Point(0, 0), win)!.Value.Y - name.TranslatePoint(new Point(0, name.Bounds.Height), win)!.Value.Y;
        Check($"فاصلهٔ زیرِ کادرِ شروع همان فاصلهٔ بقیهٔ کادرهاست ({before:F0} ⇐ با هشدار {during:F0}، معمول {normal:F0})",
              Math.Abs(before - normal) < 2 && Math.Abs(during - before) < 1);
        Shot(win, "parcha-ack-inside.png");

        ack?.Command?.Execute(null);
        Settle(win);
        Check("«دیدم» زده شد ⇒ هشدار رفت و کادر سرخ نیست", !parcha.Day.LowBase && parcha.Day.StartBrushKey == "Pump.Text");
    }

    private static bool Inside(Control c, Control outer, Window w)
    {
        var a = c.TranslatePoint(new Point(0, 0), w); var b = c.TranslatePoint(new Point(c.Bounds.Width, c.Bounds.Height), w);
        var oa = outer.TranslatePoint(new Point(0, 0), w); var ob = outer.TranslatePoint(new Point(outer.Bounds.Width, outer.Bounds.Height), w);
        if (a is null || b is null || oa is null || ob is null) return false;
        static double L(Point p, Point q) => Math.Min(p.X, q.X);
        static double R(Point p, Point q) => Math.Max(p.X, q.X);
        return L(a.Value, b.Value) >= L(oa.Value, ob.Value) - 1 && R(a.Value, b.Value) <= R(oa.Value, ob.Value) + 1
            && Math.Min(a.Value.Y, b.Value.Y) >= Math.Min(oa.Value.Y, ob.Value.Y) - 1
            && Math.Max(a.Value.Y, b.Value.Y) <= Math.Max(oa.Value.Y, ob.Value.Y) + 1;
    }

    // ══ «زیان ناشی از افزایش قیمت» — کادر هست، محتوا نه ══════════════════════

    private static void PriceLossIsEmpty(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── زیان ناشی از افزایش قیمت ──");
        var debt = vm.Sections.First(s => s.Id == "debt");
        var pl = debt.SubSections.OfType<PriceLossSectionViewModel>().FirstOrDefault();
        Check("کارتِ زیربخش هنوز زیرِ «قرض‌داران» هست", pl is not null);
        if (pl is null) return;
        Wait(win, vm.GoAsync(debt));
        Settle(win);
        var dbBefore = PumpYaqobi.Persistence.PumpDbContext.Version;
        debt.OpenSub = pl;
        Settle(win);
        var grids = win.GetVisualDescendants().OfType<DataGrid>().Count(g => g.IsEffectivelyVisible);
        var empty = win.GetVisualDescendants().OfType<TextBlock>()
                       .Any(t => t.IsEffectivelyVisible && t.Text == "این بخش فعلاً خالی است");
        Check($"صفحه باز شد، خالی است و هیچ جدولی ندارد (جدول {grids})", empty && grids == 0);
        Shot(win, "priceloss-empty.png");
        debt.OpenSub = null;
        Settle(win);
    }

    // ── ابزار ─────────────────────────────────────────────────────────────

    private static DataGrid? Grid(Window win) =>
        win.GetVisualDescendants().OfType<DataGrid>()
           .Where(g => g.IsEffectivelyVisible && g.GetVisualDescendants().OfType<DataGridRow>().Any())
           .OrderByDescending(g => g.Bounds.Height).FirstOrDefault();

    private static int FirstTextCol(DataGrid g)
    {
        var cols = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        var row = g.ItemsSource?.Cast<object>().FirstOrDefault();
        for (var i = 0; i < cols.Count; i++)
        {
            if (cols[i].IsReadOnly || cols[i] is not DataGridBoundColumn b) continue;
            if (b.Binding is not Avalonia.Data.Binding bind || row is null) continue;
            var p = row.GetType().GetProperty(bind.Path);
            if (p?.PropertyType == typeof(string) && p.CanWrite
                && !string.IsNullOrEmpty(p.GetValue(row) as string)
                && !bind.Path.Contains("Date")) return i;
        }
        return -1;
    }

    private static DataGridCell? Cell(DataGrid g, int row, int col)
    {
        var cols = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (col >= cols.Count) return null;
        var item = g.ItemsSource?.Cast<object>().ElementAtOrDefault(row);
        return g.GetVisualDescendants().OfType<DataGridRow>()
                .FirstOrDefault(r => ReferenceEquals(r.DataContext, item))?
                .GetVisualDescendants().OfType<DataGridCell>()
                .FirstOrDefault(c => ReferenceEquals(ColumnOf(c), cols[col]));
    }

    private static string CellText(DataGrid g, int row, int col)
    {
        var cell = Cell(g, row, col);
        if (cell is null) return "?";
        var box = cell.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsVisible);
        if (box is not null) return box.Text ?? "";
        return cell.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? "";
    }

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

    private static void FocusElsewhere(Window win)
    {
        win.FocusManager?.ClearFocus();
        Pump(win);
    }

    private static void ShiftDigit(Window win, PhysicalKey k)
    {
        win.KeyPressQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        win.KeyPressQwerty(k, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(k, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
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

    private static DataGridColumn? ColumnOf(DataGridCell cell) =>
        cell.GetType().GetProperty("OwningColumn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
              | System.Reflection.BindingFlags.Public)?.GetValue(cell) as DataGridColumn;

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
