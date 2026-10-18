using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ فهرستِ ۱۴۰۵/۰۷/۲۵ — کلیدهای خانه با کلیدِ واقعی ══════════════════════════
/// گزارشِ صاحب ریپو:
///   ۱) «حروفِ زیاد می‌نویسم، چپ یا راست را می‌زنم نمی‌رود ستونِ بعدی و وسطِ
///      نوشته‌ها حرکت می‌کند… قفل می‌کند و فقط بالا و پایین می‌رود.»
///   ۲) «روی یک ستون هستم بک‌اسپیس یا دیلیت را می‌زنم پاک نمی‌شود.»
///   ۳) «برای عددها پیشنهادِ تکمیل نیاید — ۵ زدم ۵۰ شد.»
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- cellfix
/// </summary>
internal static class CellFixProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-cellfix-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1500, Height = 950 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Pump(win);
        Seed.Fill(AppHost.Current);

        foreach (var id in new[] { "expenses", "waraq" })
        {
            var sec = vm.Sections.First(s => s.Id == id);
            Wait(win, vm.GoAsync(sec));
            Pump(win, 30);
            if (id == "waraq") OpenWaraq(win, sec);
            Console.WriteLine($"════ {id} ════");
            var grid = win.GetVisualDescendants().OfType<ExcelGrid>()
                          .FirstOrDefault(g => g.IsEffectivelyVisible && !g.IsReadOnly
                                               && g.ItemsSource is System.Collections.IList { Count: > 1 });
            if (grid is null) { Check("جدولِ پر", false); continue; }
            Scenarios(win, grid);
        }

        Console.WriteLine("════ Ctrl+Z پس از دوباره‌خوانیِ جدول ════");
        UndoAfterReload(win, vm);

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Scenarios(Window win, ExcelGrid grid)
    {
        var cols = grid.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        var textCols = cols.Where(c => !c.IsReadOnly && ColPath(c) is { } p && !RowViewModel.IsNumberColumn(p)
                                       && Prop(grid, p)?.PropertyType == typeof(string) && Prop(grid, p)!.CanWrite).ToList();
        var numCols = cols.Where(c => !c.IsReadOnly && RowViewModel.IsNumberColumn(ColPath(c))).ToList();
        Check("ستونِ نوشتنی و عددی پیدا شد", textCols.Count > 0 && numCols.Count > 0, $"{textCols.Count} متنی، {numCols.Count} عددی");
        if (textCols.Count == 0) return;
        var col = textCols.FirstOrDefault(c => ColPath(c) != "DateShamsi") ?? textCols[0];
        var idx = cols.IndexOf(col);
        var key = idx < cols.Count - 1 ? Key.Left : Key.Right;

        // ── ۱الف) ویرایش با کلیک/دوبار-کلیک باز شد، حرفِ زیاد، بعد فلش ⇒ ستونِ بعد ──
        Select(win, grid, 0, col);
        grid.BeginEdit(); Pump(win);
        foreach (var ch in "سلام این یک نوشتهٔ بلند است") { win.KeyTextInput(ch.ToString()); Pump(win, 2); }
        Pump(win);
        Arrow(win, key);
        Check("۱الف) نوشتن پس از باز کردنِ خانه + فلش ⇒ ستونِ بعد",
              grid.CurrentColumn != col && !Editing(grid), $"از {idx} با {key} به {cols.IndexOf(grid.CurrentColumn!)} ویرایش={Editing(grid)}");
        // فلشِ دوم هم کار کند — «قفل نکند»
        var before = grid.CurrentColumn;
        Arrow(win, key == Key.Left ? Key.Right : Key.Left);
        Check("۱ب) فلشِ دوم هم جابه‌جا می‌کند (قفل نیست)", grid.CurrentColumn != before, $"ستون={cols.IndexOf(grid.CurrentColumn!)}");
        Check("۱ب) فوکوس هنوز داخلِ جدول است", Focused(win) is { } f && (ReferenceEquals(f, grid) || f.FindAncestorOfType<ExcelGrid>() == grid),
              Focused(win)?.GetType().Name);
        Escape(win);

        // ── ۱ج) ویرایش بی تایپ، کُرسر وسط: فلش کُرسر را می‌برد؛ به لبه که رسید ستونِ بعد ──
        Select(win, grid, 0, col);
        grid.BeginEdit(); Pump(win);
        if (Editor(grid) is { } box)
        {
            box.Text = "برق دکان"; box.CaretIndex = 4; Pump(win);
            var moved = false;
            for (var i = 0; i < 20 && !moved; i++) { Arrow(win, key); moved = grid.CurrentColumn != col; }
            Check("۱ج) ویرایشِ بی‌تایپ: به لبهٔ متن که رسید، ستونِ بعد (قفل نمی‌کند)", moved && !Editing(grid));
        }
        else Check("۱ج) کادرِ ویرایش باز شد", false);
        Escape(win);

        // ── ۲) بک‌اسپیس و دیلیت روی خانهٔ انتخاب‌شده ⇒ خالی ──
        foreach (var (k, phys) in new[] { (Key.Back, PhysicalKey.Backspace), (Key.Delete, PhysicalKey.Delete) })
        {
            var row = grid.ItemsSource!.Cast<object>().ElementAt(1);
            var p = Prop(grid, ColPath(col)!)!;
            p.SetValue(row, "نوشتهٔ کهنه");
            Select(win, grid, 1, col);
            win.KeyPressQwerty(phys, RawInputModifiers.None);
            win.KeyReleaseQwerty(phys, RawInputModifiers.None);
            Pump(win);
            var now = p.GetValue(row) as string;
            if (Editing(grid)) { Escape(win); now = p.GetValue(row) as string; }
            Check($"۲) {k} خانه را خالی می‌کند", string.IsNullOrEmpty(now), $"«{now}»");
        }

        // ── ۲ب) پس از نوشتن + فلش، بک‌اسپیس روی خانهٔ تازه هم پاک کند ──
        {
            var row = grid.ItemsSource!.Cast<object>().ElementAt(1);
            var p = Prop(grid, ColPath(col)!)!;
            var other = textCols.Count > 1 ? textCols[1] : null;
            Select(win, grid, 1, other ?? col);
            p.SetValue(row, "نوشتهٔ کهنه");
            foreach (var ch in "تست") { win.KeyTextInput(ch.ToString()); Pump(win, 2); }
            Pump(win);
            // برگرد به همان ستونِ «col» با فلش
            var ci = cols.IndexOf(grid.CurrentColumn!);
            var ti = cols.IndexOf(col);
            if (ci != ti) for (var i = 0; i < 12 && grid.CurrentColumn != col; i++) Arrow(win, ti > cols.IndexOf(grid.CurrentColumn!) ? Key.Left : Key.Right);
            else Arrow(win, Key.Down);
            if (ci == ti) { Arrow(win, Key.Up); }
            if (Editing(grid)) Escape(win);
            win.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            win.KeyReleaseQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Pump(win);
            if (Editing(grid)) Escape(win);
            var cur = grid.CurrentColumn == col ? p.GetValue(row) as string : "(ستون عوض شد)";
            Check("۲ب) پس از نوشتن و فلش، بک‌اسپیس هم پاک می‌کند", string.IsNullOrEmpty(cur), $"«{cur}»");
        }

        // ── ۲ج) کلیکِ واقعیِ ماوس روی خانه، بعد بک‌اسپیس/دیلیت ──
        foreach (var (k, phys, clicks) in new[] { ("Back", PhysicalKey.Backspace, 1), ("Delete", PhysicalKey.Delete, 1) })
        {
            var row = grid.ItemsSource!.Cast<object>().ElementAt(1);
            var p = Prop(grid, ColPath(col)!)!;
            p.SetValue(row, "نوشتهٔ کهنه");
            Select(win, grid, 0, col);
            Pump(win);
            var cell = Cell(grid, 1, col);
            if (cell is null) { Check($"۲ج) خانه برای کلیک", false); continue; }
            var pt = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), win)!.Value;
            for (var c = 0; c < clicks; c++)
            {
                win.MouseDown(pt, MouseButton.Left, RawInputModifiers.None);
                win.MouseUp(pt, MouseButton.Left, RawInputModifiers.None);
                Pump(win);
            }
            win.KeyPressQwerty(phys, RawInputModifiers.None);
            win.KeyReleaseQwerty(phys, RawInputModifiers.None);
            Pump(win);
            var editing = Editing(grid);
            var shown = Editor(grid)?.Text;
            if (editing) Escape(win);
            var now = p.GetValue(row) as string;
            Check($"۲ج) کلیک + {k} ⇒ خانه خالی", editing ? string.IsNullOrEmpty(shown) : string.IsNullOrEmpty(now),
                  $"ویرایش={editing} کادر=«{shown}» ردیف=«{now}» فوکوس={Focused(win)?.GetType().Name}");
            Escape(win);
        }

        // ── ۳) ستونِ عددی: «5» تکمیل به «50» نشود ──
        if (numCols.Count > 0)
        {
            var nc = numCols[0];
            var p = Prop(grid, ColPath(nc)!)!;
            var rows = grid.ItemsSource!.Cast<object>().ToList();
            p.SetValue(rows[1], "50");
            Pump(win);
            Select(win, grid, 0, nc);
            win.KeyTextInput("5"); Pump(win, 12);
            var t = Editor(grid)?.Text;
            Check("۳) ستونِ عددی پیشنهاد نمی‌دهد", t == "5", $"«{t}» نمایش={Suggest.Ghost}");
            Escape(win);
        }

        // ── ۳ب) ستونِ متنی هنوز پیشنهاد می‌دهد ──
        {
            var p = Prop(grid, ColPath(col)!)!;
            var rows = grid.ItemsSource!.Cast<object>().ToList();
            p.SetValue(rows[1], "پطرولِ آزمایشی");
            Pump(win);
            Select(win, grid, 0, col);
            win.KeyTextInput("پ"); Pump(win, 12);
            Check("۳ب) ستونِ متنی هنوز پیشنهاد می‌دهد", Suggest.Ghost.Length > 0, $"«{Editor(grid)?.Text}»");
            Escape(win);
        }
    }

    private static ExcelGrid? FirstGrid(Window win) =>
        win.GetVisualDescendants().OfType<ExcelGrid>()
           .FirstOrDefault(g => g.IsEffectivelyVisible && !g.IsReadOnly && g.ItemsSource is System.Collections.IList { Count: > 1 });

    /// <summary>
    /// ۴) خانه خالی شد، جدول از دیسک دوباره خوانده شد (رفتن و برگشتن)، بعد Ctrl+Z ⇒
    /// همان نوشته در همان ردیفِ رکورد برمی‌گردد — نه «قدمِ قبلی» و نه هیچ.
    /// </summary>
    private static void UndoAfterReload(Window win, MainViewModel vm)
    {
        var exp = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(exp)); Pump(win, 30);
        var grid = FirstGrid(win);
        if (grid is null) { Check("جدولِ مصرف", false); return; }
        var cols = grid.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        var col = cols.First(c => !c.IsReadOnly && ColPath(c) is { } p && p != "DateShamsi" && !RowViewModel.IsNumberColumn(p)
                                  && Prop(grid, p)?.PropertyType == typeof(string));
        var path = ColPath(col)!;
        var row = grid.ItemsSource!.Cast<object>().ElementAt(2);
        var key = (row.GetType().GetProperty("Entity")?.GetValue(row) as PumpYaqobi.Domain.Entities.EntityBase)?.Id;
        Prop(grid, path)!.SetValue(row, "برای برگشت");
        Pump(win);
        Wait(win, ((PumpYaqobi.App.ViewModels.RowViewModel)row).FlushAsync());
        Select(win, grid, 2, col);
        win.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        win.KeyReleaseQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Pump(win, 20);
        Wait(win, ((PumpYaqobi.App.ViewModels.RowViewModel)row).FlushAsync());

        var other = vm.Sections.First(s => s.Id == "safe");
        Wait(win, vm.GoAsync(other)); Pump(win, 30);
        Wait(win, vm.GoAsync(exp)); Pump(win, 30);
        grid = FirstGrid(win)!;
        win.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        win.KeyReleaseQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        for (var i = 0; i < 20; i++) Pump(win, 10);
        var fresh = grid.ItemsSource!.Cast<object>().FirstOrDefault(o =>
            (o.GetType().GetProperty("Entity")?.GetValue(o) as PumpYaqobi.Domain.Entities.EntityBase)?.Id == key);
        var now = fresh is null ? null : Prop(grid, path)!.GetValue(fresh) as string;
        Check("۴) پس از دوباره‌خوانی، Ctrl+Z همان خانه را برگرداند", now == "برای برگشت",
              $"«{now}» تازه={(fresh is null ? "نبود" : ReferenceEquals(fresh, row) ? "همان شیء" : "شیءِ تازه")}");
    }

    private static void OpenWaraq(Window win, SectionViewModel sec)
    {
        if (sec.GetType().GetProperty("OpenCardCommand")?.GetValue(sec)
                is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand open
            && sec.GetType().GetProperty("Cards")?.GetValue(sec) is System.Collections.IEnumerable list
            && list.Cast<object>().FirstOrDefault() is { } card)
        { Wait(win, open.ExecuteAsync(card)); Pump(win, 30); }
        var page = win.GetVisualDescendants().OfType<PumpYaqobi.App.Views.Sections.WaraqPageView>().FirstOrDefault(p => p.IsEffectivelyVisible);
        if (page?.DataContext is { } pvm && pvm.GetType().GetProperty("AddTxnCommand")?.GetValue(pvm)
                is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand add)
            for (var i = 0; i < 4; i++) { Wait(win, add.ExecuteAsync(null)); Pump(win, 10); }
    }

    private static string? ColPath(DataGridColumn? c) =>
        (c as DataGridBoundColumn)?.Binding is Avalonia.Data.Binding b ? b.Path : null;

    private static System.Reflection.PropertyInfo? Prop(DataGrid g, string path) =>
        g.ItemsSource?.Cast<object>().FirstOrDefault()?.GetType().GetProperty(path);

    private static bool Editing(ExcelGrid g) =>
        (bool)typeof(ExcelGrid).GetField("_editing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(g)!;

    private static TextBox? Editor(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>().SelectMany(c => c.GetVisualDescendants().OfType<TextBox>())
            .FirstOrDefault(t => t.IsVisible);

    private static void Select(Window win, ExcelGrid grid, int row, DataGridColumn col)
    {
        grid.Focus();
        grid.SelectedIndex = row;
        grid.CurrentColumn = col;
        grid.ScrollIntoView(grid.SelectedItem, col);
        Pump(win);
        grid.Focus();
        Pump(win);
    }

    private static DataGridCell? Cell(DataGrid grid, int row, DataGridColumn col)
    {
        var item = grid.ItemsSource!.Cast<object>().ElementAt(row);
        var r = grid.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(x => ReferenceEquals(x.DataContext, item));
        return r?.GetVisualDescendants().OfType<DataGridCell>().FirstOrDefault(c =>
            ReferenceEquals(c.GetType().GetProperty("OwningColumn", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(c), col));
    }

    private static Control? Focused(Window win) => win.FocusManager?.GetFocusedElement() as Control;

    private static void Arrow(Window win, Key k)
    {
        var p = k switch
        {
            Key.Left => PhysicalKey.ArrowLeft, Key.Right => PhysicalKey.ArrowRight,
            Key.Up => PhysicalKey.ArrowUp, _ => PhysicalKey.ArrowDown,
        };
        win.KeyPressQwerty(p, RawInputModifiers.None);
        win.KeyReleaseQwerty(p, RawInputModifiers.None);
        Pump(win);
    }

    private static void Escape(Window win)
    {
        win.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        win.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump(win);
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
