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

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ قالبِ زندهٔ عدد و تاریخ — با کلیدِ واقعی، حرف‌به‌حرف (۱۴۰۵/۰۷/۱۹) ════════════
///
///   ۱) خانهٔ عددیِ جدول: «12500» ⇒ همان لحظه «12,500»، مکان‌نما ته، و ردیف ۱۲۵۰۰
///   ۲) خانهٔ تاریخِ جدول: «14050720» ⇒ «1405/07/20»
///   ۳) کادرِ عددیِ فرم: کاما همان لحظه، پاک‌کن، و رقمِ وسطِ عدد سرِ جایش
///   ۴) کادرِ تاریخِ فرم: «1405072» ⇒ «1405/07/2»، و «/»ی پاک‌شده برنمی‌گردد
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- liveformat
/// </summary>
internal static class LiveFormatProbe
{
    private static readonly List<string> Bad = new();

    /// <summary>عکسِ داشبورد (هر دو تبِ روند، هر دو تم) — ‎liveformat shots &lt;پوشه&gt;‎.</summary>
    public static string? ShotDir;

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-lf-" + Guid.NewGuid().ToString("N"), "pump.db");
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

        if (ShotDir is { } sd)
        {
            //  دادهٔ نمونه شرایطِ سنجه‌های جدول را عوض می‌کند — فقط عکس و جهت
            DashShots(win, vm, sd);
            return Bad.Count == 0 ? 0 : 1;
        }

        GridCells(win, vm);
        FormBoxes(win, vm);
        foreach (var id in new[] { "safe", "sarrafi", "rasid", "expenses" }) Toggles(win, vm, id, false);
        //  ستون‌ها جابه‌جا شده‌اند (کشیدنِ سربرگ) — ‎Enter‎/‎Tab‎ باز هم همان کپسول را عوض کنند
        foreach (var id in new[] { "safe", "sarrafi" }) Toggles(win, vm, id, true);

        Console.WriteLine();
        if (Bad.Count == 0) { Console.WriteLine("✅ عدد با کاما و تاریخ با «/» همان لحظهٔ تایپ — جدول و فرم"); return 0; }
        Console.WriteLine($"❌ {Bad.Count} ایراد:");
        foreach (var b in Bad) Console.WriteLine("   • " + b);
        return 1;
    }

    // ── ۱ و ۲) خانه‌های جدول — بخشِ مصارف ─────────────────────────────────────
    private static void GridCells(Window win, MainViewModel vm)
    {
        Console.WriteLine("── جدولِ مصارف ──");
        var ex = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(ex));
        var add = ex.GetType().GetProperty("AddRowCommand")?.GetValue(ex) as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;
        if (add is null) { Check("فرمانِ افزودنِ ردیف پیدا شد", false); return; }
        Wait(win, add.ExecuteAsync(null));
        Settle(win);

        var grid = win.GetVisualDescendants().OfType<ExcelGrid>().FirstOrDefault(g => g.IsEffectivelyVisible
                       && g.Columns.Any(c => (c.Header as string) == "تاریخ"));
        if (grid?.ItemsSource is not System.Collections.IList list || list.Count == 0) { Check("جدولِ مصارف پیدا شد", false); return; }
        var ri = list.Count - 1;
        var row = list[ri]!;
        var cols = grid.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        var amountCol = cols.FindIndex(c => c is DataGridBoundColumn { Binding: Avalonia.Data.Binding { Path: "AmountText" } });
        var dateCol = cols.FindIndex(c => c is DataGridBoundColumn { Binding: Avalonia.Data.Binding { Path: "DateShamsi" } });

        TypeChars(win, () => ClickCell(win, grid, ri, dateCol), "14050720",
                  new[] { "1", "14", "140", "1405", "1405/0", "1405/07", "1405/07/2", "1405/07/20" }, "خانهٔ تاریخ");
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        var date = (string?)row.GetType().GetProperty("DateShamsi")?.GetValue(row);
        Check($"تاریخِ ردیف «{date}»", date == "1405/07/20");
        TypeChars(win, () => ClickCell(win, grid, ri, amountCol), "12500",
                  new[] { "1", "12", "125", "1,250", "12,500" }, "خانهٔ مبلغ");
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        var amt = (string?)row.GetType().GetProperty("AmountText")?.GetValue(row);
        var val = row.GetType().GetProperty("Amount")?.GetValue(row);
        Check($"پس از Enter ردیف «{amt}» ({val})", amt == "12,500" && Equals(val, 12500m));

    }

    // ── ۳ و ۴) کادرهای فرم — پارچه ───────────────────────────────────────────
    private static void FormBoxes(Window win, MainViewModel vm)
    {
        Console.WriteLine("── فرمِ پارچه ──");
        var pa = vm.Sections.First(s => s.Id == "shifts");
        Console.WriteLine("  بخش: " + pa.Id);
        Wait(win, vm.GoAsync(pa));
        Settle(win);
        var boxes = win.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible).ToList();
        var num = boxes.FirstOrDefault(t => LiveFormat.GetKind(t) == "number");
        var date = boxes.FirstOrDefault(t => LiveFormat.GetKind(t) == "date");
        if (num is null || date is null) { Check("کادرِ عدد و تاریخِ فرم پیدا شد", false); return; }

        TypeChars(win, () => { num.Focus(); num.Text = ""; Settle(win); }, "1234567",
                  new[] { "1", "12", "123", "1,234", "12,345", "123,456", "1,234,567" }, "کادرِ شروعِ پایه");
        Tap(win, PhysicalKey.Backspace);
        Settle(win);
        Check($"پاک‌کن: «{num.Text}» مکان‌نما {num.CaretIndex}", num.Text == "123,456" && num.CaretIndex == num.Text.Length);
        //  رقم در وسط: پس از «1»
        num.CaretIndex = 1;
        Settle(win);
        win.KeyTextInput("9");
        Settle(win);
        Check($"رقمِ وسط: «{num.Text}» مکان‌نما {num.CaretIndex} (باید پس از ۹ باشد = ۳)",
              num.Text == "1,923,456" && num.CaretIndex == 3);
        //  پاک‌کن روی کاما ⇒ رقمِ پیشش
        num.CaretIndex = 2;   // «1,|923,456»
        Settle(win);
        Tap(win, PhysicalKey.Backspace);
        Settle(win);
        Check($"پاک‌کن روی کاما: «{num.Text}»", num.Text == "923,456");

        TypeChars(win, () => { date.Focus(); date.SelectAll(); Tap(win, PhysicalKey.Backspace); Settle(win); }, "1405072",
                  new[] { "1", "14", "140", "1405", "1405/0", "1405/07", "1405/07/2" }, "کادرِ تاریخِ پارچه");
        //  «/» را پاک کن — برنگردد
        date.CaretIndex = (date.Text ?? "").Length;
        Tap(win, PhysicalKey.Backspace);   // 2
        Tap(win, PhysicalKey.Backspace);   // /
        Settle(win);
        Check($"«/»ی پاک‌شده برنگشت: «{date.Text}»", date.Text == "1405/07");
    }

    // ── ۵) کپسولِ «نوع» و «واحد» در جدول: Enter و Tab مقدار را عوض می‌کنند ──────
    private static void Toggles(Window win, MainViewModel vm, string id, bool reorder)
    {
        Console.WriteLine($"── کپسول‌های {id}{(reorder ? " (ستون‌های جابه‌جاشده)" : "")} ──");
        var sec = vm.Sections.First(s => s.Id == id);
        Wait(win, vm.GoAsync(sec));
        var add = sec.GetType().GetProperty("AddRowCommand")?.GetValue(sec) as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;
        if (add is not null) Wait(win, add.ExecuteAsync(null));
        Settle(win);
        var grid = win.GetVisualDescendants().OfType<ExcelGrid>()
            .FirstOrDefault(g => g.IsEffectivelyVisible && g.ItemsSource is System.Collections.IList { Count: > 0 });
        if (grid?.ItemsSource is not System.Collections.IList list) { Check($"{id}: جدول پیدا شد", false); return; }
        var ri = list.Count - 1;
        if (reorder)
        {
            //  ستونِ آخر را اول بگذار — همان کاری که کشیدنِ سربرگ می‌کند
            var last = grid.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).Last();
            last.DisplayIndex = 0;
            Settle(win);
        }
        var cols = grid.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        for (var ci = 1; ci < cols.Count; ci++)
        {
            if (cols[ci] is not DataGridTemplateColumn) continue;
            if (cols[ci - 1] is DataGridTemplateColumn) continue;   // پیش از کپسول باید خانهٔ نوشتنی باشد
            var chip = Cell(grid, ri, ci)?.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Classes.Contains("celltoggle"));
            if (chip is null) continue;
            var head = cols[ci].Header as string;
            string Now() => (Cell(grid, ri, ci)?.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Classes.Contains("celltoggle"))?.Content as string) ?? "";

            //  الف) با کلید به این خانه بیا (از خانهٔ نخست، Tab تا این ستون)
            ClickCell(win, grid, ri, ci - 1);
            Tap(win, PhysicalKey.Tab); Settle(win);
            var a = Now();
            Tap(win, PhysicalKey.Enter); Settle(win);
            var b = Now();
            Tap(win, PhysicalKey.Tab); Settle(win);
            var c = Now();
            Check($"{id} «{head}» با کلید: {a} ⇐Enter⇐ {b} ⇐Tab⇐ {c}", a != b && b != c);

            //  الف۲) در خانهٔ پیشین بنویس، Tab ⇐ همین خانه، بعد Enter و Tab (کارِ روزمره)
            ClickCell(win, grid, ri, ci - 1);
            if (cols[ci - 1] is not DataGridTemplateColumn)
            {
                win.KeyTextInput(cols[ci - 1] is DataGridBoundColumn { Binding: Avalonia.Data.Binding { Path: "DateShamsi" } } ? "14050720" : "5");
                Settle(win);
            }
            Tap(win, PhysicalKey.Tab); Settle(win);
            var a2 = Now();
            Tap(win, PhysicalKey.Enter); Settle(win);
            var b2 = Now();
            Tap(win, PhysicalKey.Tab); Settle(win);
            var c2 = Now();
            Check($"{id} «{head}» پس از نوشتن در خانهٔ پیشین و Tab: {a2} ⇐Enter⇐ {b2} ⇐Tab⇐ {c2}", a2 != b2 && b2 != c2);
            c = c2;

            //  ب) با کلیک روی خودِ کپسول، بعد Enter و Tab
            var p = chip.TranslatePoint(new Point(chip.Bounds.Width / 2, chip.Bounds.Height / 2), win);
            if (p is { } pt) { win.MouseDown(pt, MouseButton.Left); win.MouseUp(pt, MouseButton.Left); }
            Settle(win);
            var d = Now();
            Tap(win, PhysicalKey.Enter); Settle(win);
            var e = Now();
            Tap(win, PhysicalKey.Tab); Settle(win);
            var f = Now();
            Check($"{id} «{head}» پس از کلیک: {c} ⇐کلیک⇐ {d} ⇐Enter⇐ {e} ⇐Tab⇐ {f}", d != c && e != d && f != e);
        }
    }

    private static void DashShots(Window win, MainViewModel vm, string dir)
    {
        Directory.CreateDirectory(dir);
        Seed.Fill(AppHost.Current);
        var dash = (DashboardSectionViewModel)vm.Sections.First(s => s.Id == "dashboard");
        Wait(win, vm.GoAsync(dash));
        Wait(win, dash.RefreshAsync());
        Settle(win);
        var scroll = win.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "PageScroll" || s.IsEffectivelyVisible);
        foreach (var theme in PumpYaqobi.App.Themes.PumpTheme.All)
        {
            vm.SelectedTheme = theme;
            Settle(win);
            var chart = win.GetVisualDescendants().OfType<SparkChart>().FirstOrDefault(c => c.IsEffectivelyVisible);
            if (chart is not null) chart.BringIntoView();
            Settle(win);
            foreach (var tab in new[] { "sale", "profit" })
            {
                dash.SetTrendTabCommand.Execute(tab);
                Settle(win);
                var bars = win.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault(i => i.ItemsSource == dash.Bars);
                bars?.BringIntoView();
                Settle(win);
                using var f = win.CaptureRenderedFrame();
                var path = Path.Combine(dir, $"dash-{theme.Id}-{tab}-a.png");
                f?.Save(path);
                var c2 = win.GetVisualDescendants().OfType<SparkChart>().FirstOrDefault(c => c.IsEffectivelyVisible);
                c2?.BringIntoView();
                Settle(win);
                using var f2 = win.CaptureRenderedFrame();
                f2?.Save(Path.Combine(dir, $"dash-{theme.Id}-{tab}-b.png"));
                Console.WriteLine("  📷 " + path);
            }
        }
        //  جهت: ستونِ ۰ (قدیمی‌ترین/اولِ ‎_disp‎) چپ‌تر از آخری
        var ic = win.GetVisualDescendants().OfType<ItemsControl>().First(i => i.ItemsSource == dash.Bars);
        var btns = ic.GetVisualDescendants().OfType<Button>().Where(b => b.DataContext is DashBarViewModel).ToList();
        var first = btns.First(b => ((DashBarViewModel)b.DataContext!).Index == 0);
        var last = btns.First(b => ((DashBarViewModel)b.DataContext!).Index == dash.Bars.Count - 1);
        var x0 = first.TranslatePoint(new Point(0, 0), win)!.Value.X;
        var x1 = last.TranslatePoint(new Point(0, 0), win)!.Value.X;
        Check($"ستون‌های نمودارِ فروش از چپ به راست (ستونِ ۰ در x={x0:0}، آخری در x={x1:0})", x0 < x1);
        var spark = win.GetVisualDescendants().OfType<SparkChart>().First(c => c.IsEffectivelyVisible);
        dash.SetTrendTabCommand.Execute("sale");
        Settle(win);
        var shown = win.GetVisualDescendants().OfType<SparkChart>().Where(c => c.IsEffectivelyVisible).ToList();
        Check($"تبِ فروش: یک نمودار، همان فروش ({shown.Count})", shown.Count == 1 && ReferenceEquals(shown[0].Values, dash.AreaValues));
        dash.SetTrendTabCommand.Execute("profit");
        Settle(win);
        shown = win.GetVisualDescendants().OfType<SparkChart>().Where(c => c.IsEffectivelyVisible).ToList();
        Check($"تبِ مفاد و مصارف: یک نمودار، همان مصارف ({shown.Count})", shown.Count == 1 && ReferenceEquals(shown[0].SecondValues, dash.TrendExpense));
        dash.SetTrendTabCommand.Execute("sale");
        Settle(win);
        _ = spark;
    }

    private static void TypeChars(Window win, Action focus, string text, string[] want, string what)
    {
        focus();
        var lost = new List<string>();
        for (var i = 0; i < text.Length; i++)
        {
            win.KeyTextInput(text[i].ToString());
            for (var k = 0; k < 6; k++) { Pump(win); Thread.Sleep(3); }
            var box = win.FocusManager?.GetFocusedElement() as TextBox;
            var shown = box?.Text ?? "";
            //  تکملهٔ کم‌رنگِ پیشنهادی (تکهٔ انتخاب‌شدهٔ ته) جزوِ نوشته نیست
            var caretAt = box?.CaretIndex ?? 0;
            if (box is not null && box.SelectionStart != box.SelectionEnd
                && Math.Max(box.SelectionStart, box.SelectionEnd) == shown.Length)
            {
                caretAt = Math.Min(box.SelectionStart, box.SelectionEnd);
                shown = shown[..caretAt];
            }
            if (shown != want[i] || caretAt != shown.Length)
                lost.Add($"پس از «{text[..(i + 1)]}» ⇐ «{shown}» مکان‌نما {box?.CaretIndex} [{(box is null ? "-" : LiveFormat.GetKind(box))}|{box?.GetHashCode()}]");
        }
        Check(lost.Count == 0 ? $"{what}: هر حرف همان لحظه قالب خورد («{want[^1]}»)"
                              : $"{what}: " + string.Join(" · ", lost.Take(4)), lost.Count == 0);
    }

    private static DataGridCell? Cell(DataGrid g, int row, int col)
    {
        var cols = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (col < 0 || col >= cols.Count) return null;
        var item = g.ItemsSource?.Cast<object>().ElementAtOrDefault(row);
        return g.GetVisualDescendants().OfType<DataGridRow>()
                .FirstOrDefault(r => ReferenceEquals(r.DataContext, item))?
                .GetVisualDescendants().OfType<DataGridCell>()
                .FirstOrDefault(c => ReferenceEquals(c.GetType().GetProperty("OwningColumn",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public)?.GetValue(c), cols[col]));
    }

    private static void ClickCell(Window win, DataGrid g, int row, int col)
    {
        g.ScrollIntoView(g.ItemsSource?.Cast<object>().ElementAtOrDefault(row), null);
        Settle(win);
        var cell = Cell(g, row, col);
        if (cell is null) { Check($"خانهٔ {row}/{col} پیدا شد", false); return; }
        var p = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), win);
        if (p is null) return;
        win.MouseDown(p.Value, MouseButton.Left);
        win.MouseUp(p.Value, MouseButton.Left);
        Settle(win);
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

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 30; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 2000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
