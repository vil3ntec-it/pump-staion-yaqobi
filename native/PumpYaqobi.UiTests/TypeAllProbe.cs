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

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «اگر کاربر 60.14 نوشت دقیقاً 60.14 ثبت شود، نه 6514؛ 12960 نه 12906» (۱۴۰۵/۰۷/۱۹) ══
///
/// همهٔ بخش‌ها و صفحه‌های درونی را می‌گردد و در <b>هر</b> خانهٔ نوشتنیِ هر جدول و هر
/// کادرِ تایپِ دیدنی، حرف‌به‌حرف با کلیدِ واقعی می‌نویسد. پس از هر حرف کادر باید
/// دقیقاً همان پیشوندِ نوشته‌شده را داشته باشد، و پس از Enter (یا رفتنِ فوکوس)
/// مقدارِ نشسته همان باشد — عدد همان عدد، نوشته همان نوشته.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- typeall
/// </summary>
internal static class TypeAllProbe
{
    private static readonly List<string> Bad = new();
    private static readonly string[] Inputs = { "60.14", "12960", "حواله 235 هارون بابت تیل قمندانی" };
    private static int _cells, _boxes;

    public static int Run(string[] args)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-typeall-" + Guid.NewGuid().ToString("N"));
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
        var only = args.Skip(1).FirstOrDefault();

        void Visit(string where, Action open)
        {
            if (only is not null && !where.Contains(only)) return;
            Console.WriteLine($"── {where} ──");
            try { open(); } catch (Exception ex) { Console.WriteLine("   (باز نشد: " + ex.Message + ")"); return; }
            Round14Probe.Settle(win);
            _where = where;
            Sweep(win, where);
        }

        foreach (var sec in vm.NavSections.ToList())
        {
            if (sec.Id is "chat" or "account" or "cameras" or "settings") continue;
            Visit(sec.Title, () => Round14Probe.Wait(win, vm.GoAsync(sec)));
        }
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is DebtSectionViewModel debt)
        {
            var first = AppHost.Current.Debtors.ListAsync().GetAwaiter().GetResult().FirstOrDefault();
            if (first is not null)
            {
                Visit("حسابِ قرض‌دار", () => { Round14Probe.Wait(win, vm.GoAsync(debt)); Round14Probe.Wait(win, debt.OpenPersonAsync(first.Id)); });
                debt.CloseOpenPage();
            }
        }
        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is WaraqSectionViewModel waraq)
        {
            Round14Probe.Wait(win, vm.GoAsync(waraq));
            if (waraq.Sheets.FirstOrDefault() is { } sheet)
            {
                Visit("ورق", () => waraq.OpenCommand.Execute(sheet));
                waraq.CloseOpenPage();
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{_cells} خانهٔ جدول و {_boxes} کادرِ تایپ سنجیده شد");
        ReportSpeed();
        if (Bad.Count == 0) { Console.WriteLine("✅ هیچ عدد یا نوشته‌ای هنگامِ تایپ عوض، کم یا جابه‌جا نشد"); return 0; }
        Console.WriteLine($"❌ {Bad.Count} ایراد:");
        foreach (var b in Bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    private static void Sweep(Window win, string where)
    {
        //  ── جدول‌ها ──
        var grids = win.GetVisualDescendants().OfType<ExcelGrid>()
            .Where(g => g.IsEffectivelyVisible && g.ItemsSource is System.Collections.IList { Count: > 0 } && !g.IsReadOnly)
            .ToList();
        foreach (var g in grids)
        {
            var cols = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
            for (var ci = 0; ci < cols.Count; ci++)
            {
                var col = cols[ci];
                if (col.IsReadOnly || col is not DataGridTextColumn) continue;
                var head = col.Header as string ?? "";
                if (head.Contains("تاریخ")) continue;
                foreach (var text in Inputs)
                {
                    var item = (g.ItemsSource as System.Collections.IList)?.Cast<object>().FirstOrDefault();
                    var box = OpenCell(win, g, 0, col);
                    if (box is null) break;
                    _cells++;
                    var lost = TypeChars(win, text);
                    Tap(win, PhysicalKey.Enter);
                    Round14Probe.Settle(win);
                    //  (۱۴۰۵/۰۷/۱۸) ورق پس از Enter دو جدولش را دوباره تقسیم می‌کند
                    //  (‎WaraqPageViewModel.Rebalance‎) و ردیفِ مرز به جدولِ دیگر می‌رود؛ پس همان
                    //  ردیف سنجیده می‌شود، هر جا که حالا نشسته — نه «ردیفِ اولِ همین جدول».
                    var shown = ShownOf(win, g, item, col);
                    var why = Compare(text, shown);
                    var at = $"{where} · جدول «{head}»";
                    if (lost is not null) Bad.Add($"{at}: هنگامِ نوشتنِ «{text}» {lost}");
                    if (why is not null) Bad.Add($"{at}: «{text}» ⇒ «{shown}» ({why})");
                    Tap(win, PhysicalKey.Escape);
                    Round14Probe.Settle(win);
                }
            }
        }

        //  ── کادرهای تایپِ بیرونِ جدول ──
        var boxes = win.GetVisualDescendants().OfType<TextBox>()
            .Where(b => b.IsEffectivelyVisible && b.IsEnabled && !b.IsReadOnly && b.PasswordChar == default
                        && b.Bounds.Width > 20 && b.FindAncestorOfType<DataGrid>() is null
                        //  کادرِ رسیدِ سربرگِ حساب عمداً پس از نشستن جمعِ دفتر را نشان می‌دهد
                        && b.Tag is not ("petrol" or "diesel"))
            .ToList();
        foreach (var b in boxes)
        {
            var name = b.Watermark?.ToString() ?? b.Name ?? b.GetType().Name;
            foreach (var text in Inputs)
            {
                if (!b.IsEffectivelyVisible) break;
                b.Focus();
                Round14Probe.Settle(win);
                if (!b.IsFocused) break;
                b.SelectAll();
                _boxes++;
                var lost = TypeChars(win, text);
                if (lost is not null) Bad.Add($"{where} · کادرِ «{name}»: هنگامِ نوشتنِ «{text}» {lost}");
                //  رفتنِ فوکوس — اتصالِ ‎LostFocus‎ همین‌جا می‌نشیند
                win.FocusManager?.ClearFocus();
                Round14Probe.Settle(win);
                var why = Compare(text, b.Text ?? "");
                if (why is not null && (b.Text ?? "") != "" ) Bad.Add($"{where} · کادرِ «{name}»: «{text}» ⇒ «{b.Text}» ({why})");
            }
        }
    }

    /// <summary>‎null‎ یعنی درست است.</summary>
    private static string? Compare(string typed, string shown)
    {
        var s = Clean(shown);
        if (s == typed) return null;
        //  قالبِ زندهٔ ۱۴۰۵/۰۷/۱۹: کامای عدد و «/»ی تاریخ جزوِ نوشتهٔ کاربر نیستند
        if (Bare(s, typed) == typed) return null;
        var digitsOnly = typed.All(c => char.IsDigit(c) || c == '.');
        if (digitsOnly)
        {
            //  عدد: قالب‌خوردنش («12,960») درست است؛ عددِ دیگر شدنش نه
            if (s.Any(char.IsLetter)) return null;           // ستونِ متنیِ دیگر (برچسب) — نه این ورودی
            var want = decimal.Parse(typed, System.Globalization.CultureInfo.InvariantCulture);
            var got = Shamsi.Num(s);
            if (got == want) return null;
            if (s.Length == 0) return null;                    // ستونی که عدد نمی‌پذیرد
            return $"عدد {got} شد";
        }
        //  نوشته: ستونی که فقط عدد می‌پذیرد خالی/صفر می‌شود — آن ایراد نیست
        if (!s.Any(char.IsLetter)) return null;
        return "نوشته عوض شد";
    }

    /// <summary>
    /// نوشته بی جداکننده‌ها و «/»هایی که قالبِ زنده گذاشته (فقط وقتی خودِ کاربر چنین
    /// نویسه‌ای نزده). ⛔ رقمِ گم‌شده یا جابه‌جا همچنان سرخ است.
    /// </summary>
    private static string Bare(string s, string typed) =>
        typed.IndexOfAny(new[] { ',', '/' }) >= 0 ? s : s.Replace(",", "").Replace("/", "");

    private static string Clean(string s) =>
        new(s.Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format).ToArray());

    /// <summary>حرف‌به‌حرف؛ ‎null‎ یعنی کادر هر بار دقیقاً همان پیشوند را داشت.</summary>
    private static string? TypeChars(Window win, string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            //  کلیدِ واقعی: ‎KeyDown‎ ⇒ ‎TextInput‎ ⇒ ‎KeyUp‎ — همان راهی که ویندوز می‌رود
            var phys = PhysOf(text[i]);
            //  ⛔ (۱۴۰۵/۰۷/۲۰) زمانِ هر کلید — از فشردن تا نشستنِ حرف و چیدمانِ بعدش، بی خوابِ آزمون
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (phys != PhysicalKey.None) win.KeyPressQwerty(phys, RawInputModifiers.None);
            win.KeyTextInput(text[i].ToString());
            if (phys != PhysicalKey.None) win.KeyReleaseQwerty(phys, RawInputModifiers.None);
            //  ⚠️ در تایپِ تند (‎TA_FAST‎) عمداً هیچ کاری میانِ دو حرف نمی‌دود — آن‌جا سنجیده نمی‌شود
            if (!Fast)
            {
                Dispatcher.UIThread.RunJobs();
                win.UpdateLayout();
                sw.Stop();
                if (!KeyMs.TryGetValue(_where, out var list)) KeyMs[_where] = list = new List<double>();
                list.Add(sw.Elapsed.TotalMilliseconds);
            }
            if (Environment.GetEnvironmentVariable("TA_DEBUG") == "1")
            {
                var f0 = win.FocusManager?.GetFocusedElement();
                Console.WriteLine($"   ·  «{text[i]}» ⇒ فوکوس {f0?.GetType().Name} «{(f0 as TextBox)?.Text}» caret {(f0 as TextBox)?.CaretIndex}");
            }
            //  تایپِ تند: میانِ دو حرف هیچ کاری نمی‌دود، جز گاهی
            if (Fast && i % 3 != 2) continue;
            for (var k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(4); }
            if (Fast) continue;
            var fe = win.FocusManager?.GetFocusedElement();
            var box = fe as TextBox;
            if (Environment.GetEnvironmentVariable("TA_DEBUG") == "1")
                Console.WriteLine($"      «{text[i]}» ⇒ فوکوس {fe?.GetType().Name} «{box?.Text}» caret {box?.CaretIndex}");
            var t = Typed(box);
            var want = text[..(i + 1)];
            if (Bare(Clean(t), want) != want) return $"پس از «{want}» کادر «{t}» نشان داد";
        }
        return null;
    }

    private static bool Fast => Environment.GetEnvironmentVariable("TA_FAST") == "1";

    // ══ سرعتِ تایپ در هر بخش (۱۴۰۵/۰۷/۲۰) ═══════════════════════════════════
    //  صاحب ریپو: «نوشتن توی ستون‌های جدول کند نیست؟ یک بخش برایم کند بود، یادم نیست کدام.»
    //  هر کلید از فشردن تا نشستنِ حرف و چیدمانِ بعدش اندازه گرفته می‌شود؛ هر بخش یک ردیف:
    //  میانه، صدکِ ۹۵ و بیشترین. ⛔ صدکِ ۹۵ بالای ‎SlowKeyMs‎ یعنی تایپِ کند — سرخ.
    private const double SlowKeyMs = 200;
    private static string _where = "";
    private static readonly Dictionary<string, List<double>> KeyMs = new();

    private static void ReportSpeed()
    {
        Console.WriteLine();
        Console.WriteLine("سرعتِ تایپ (میلی‌ثانیه برای هر کلید)      کلید   میانه   صدکِ۹۵   بیشترین");
        foreach (var (where, ms) in KeyMs.OrderByDescending(kv => P(kv.Value, 0.95)))
        {
            var p95 = P(ms, 0.95);
            var ok = p95 <= SlowKeyMs;
            Console.WriteLine($"  {(ok ? "✔" : "✖")} {where,-36} {ms.Count,5} {P(ms, 0.5),7:0.0} {p95,8:0.0} {ms.Max(),9:0.0}");
            if (!ok) Bad.Add($"«{where}»: تایپ کند است — صدکِ ۹۵ِ هر کلید {p95:0} میلی‌ثانیه (سقف {SlowKeyMs:0})");
        }
    }

    private static double P(List<double> v, double q)
    {
        if (v.Count == 0) return 0;
        var s = v.OrderBy(x => x).ToList();
        return s[Math.Min(s.Count - 1, (int)Math.Floor(q * (s.Count - 1)))];
    }

    private static PhysicalKey PhysOf(char c) => c switch
    {
        >= '0' and <= '9' => PhysicalKey.Digit0 + (c - '0'),
        '.' => PhysicalKey.Period,
        ' ' => PhysicalKey.Space,
        _ => PhysicalKey.None,
    };

    private static string Typed(TextBox? box)
    {
        if (box?.Text is not { } t) return "";
        var a = Math.Min(box.SelectionStart, box.SelectionEnd);
        var b = Math.Max(box.SelectionStart, box.SelectionEnd);
        return b > a && b == t.Length ? t[..a] : t;
    }

    private static DataGridCell? Cell(DataGrid g, int row, DataGridColumn col)
    {
        var item = g.ItemsSource?.Cast<object>().ElementAtOrDefault(row);
        return g.GetVisualDescendants().OfType<DataGridRow>()
                .FirstOrDefault(r => ReferenceEquals(r.DataContext, item))?
                .GetVisualDescendants().OfType<DataGridCell>()
                .FirstOrDefault(c => ReferenceEquals(c.GetType().GetProperty("OwningColumn",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public)?.GetValue(c), col));
    }

    private static string ShownOf(Window win, DataGrid g, object? item, DataGridColumn col)
    {
        if (item is null || (g.ItemsSource as System.Collections.IList)?.IndexOf(item) is 0)
            return CellText(g, 0, col);
        var head = col.Header as string;
        foreach (var other in win.GetVisualDescendants().OfType<ExcelGrid>())
        {
            if (other.ItemsSource is not System.Collections.IList l || l.IndexOf(item) is var i && i < 0) continue;
            var oc = other.Columns.FirstOrDefault(c => c.Header as string == head);
            if (oc is null) continue;
            other.ScrollIntoView(item, oc);
            Round14Probe.Settle(win);
            return CellText(other, l.IndexOf(item), oc);
        }
        return CellText(g, 0, col);
    }

    private static string CellText(DataGrid g, int row, DataGridColumn col) =>
        Cell(g, row, col)?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? "";

    private static TextBox? OpenCell(Window win, DataGrid g, int row, DataGridColumn col)
    {
        g.ScrollIntoView(g.ItemsSource?.Cast<object>().ElementAtOrDefault(row), col);
        Round14Probe.Settle(win);
        var cell = Cell(g, row, col);
        if (cell is null || !cell.IsEffectivelyVisible) return null;
        var p = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), win);
        if (p is null) return null;
        //  دوبار-کلیک = بازکردنِ خانه (همان اکسل)
        win.MouseDown(p.Value, MouseButton.Left);
        win.MouseUp(p.Value, MouseButton.Left);
        Round14Probe.Settle(win);
        if (Environment.GetEnvironmentVariable("TA_DIRECT") == "1") return new TextBox();   // اکسلی: خانهٔ انتخاب‌شده، مستقیم تایپ
        Tap(win, PhysicalKey.F2);
        Round14Probe.Settle(win);
        if (win.FocusManager?.GetFocusedElement() is not TextBox box) return null;
        box.SelectAll();
        return box;
    }

    private static void Tap(Window win, PhysicalKey key)
    {
        win.KeyPressQwerty(key, RawInputModifiers.None);
        win.KeyReleaseQwerty(key, RawInputModifiers.None);
    }
}
