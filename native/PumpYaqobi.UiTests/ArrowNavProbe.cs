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
/// ══ کلیدهای جهت‌نما بینِ کادرها و بینِ دو جدولِ ورق (۱۴۰۵/۰۷/۱۸) ══════════════
/// گزارشِ صاحب ریپو: «در ورق‌ها حرکت بین تراکنش‌های راست و چپ متوقف می‌شود. در
/// پارچه‌ها رفتن به چپ یا شیفتِ شب انجام نمی‌شود و کلیدها داخلِ نوشته حرکت می‌کنند.»
/// با پنجرهٔ واقعی و کلیدِ واقعی:
///   • کادرِ پر و انتخاب‌شده (همان حالِ پس از رسیدن) ⇒ ← و → به کادرِ هم‌ردیفِ همان سمت
///   • کُرسرِ وسطِ متن ⇒ کادر عوض نمی‌شود و کُرسر جابه‌جا می‌شود (ویرایشِ عادی سالم)
///   • لبهٔ جدولِ راستِ تراکنش‌ها ⇒ ← به جدولِ چپ، و → برمی‌گردد
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- arrows
/// </summary>
internal static class ArrowNavProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-arrows-" + Guid.NewGuid().ToString("N"), "pump.db");
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

        foreach (var id in new[] { "shifts", "profit" })
        {
            var sec = vm.Sections.FirstOrDefault(s => s.Id == id);
            if (sec is null) { Check($"بخشِ {id}", false, "نبود"); continue; }
            Wait(win, vm.GoAsync(sec));
            Pump(win, 30);
            Console.WriteLine($"════ {id} ════");
            Fields(win);
        }

        Console.WriteLine("════ waraq ════");
        Waraq(win, vm);

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static List<TextBox> Boxes(Window win) =>
        win.GetVisualDescendants().OfType<TextBox>()
           .Where(t => t.IsEffectivelyVisible && t.IsEffectivelyEnabled && !t.AcceptsReturn
                       && t.Bounds.Width > 20 && t.FindAncestorOfType<DataGrid>() is null
                       && t.FindAncestorOfType<SectionPage>() is not null)
           .ToList();

    private static void Fields(Window win)
    {
        var boxes = Boxes(win);
        var rects = boxes.Select(b => (b, r: FieldNavigationService.ScreenRect(b))).Where(x => x.r is not null)
                         .Select(x => (x.b, r: x.r!.Value)).ToList();
        // جفت‌های هم‌ردیف: B سمتِ چپِ A
        var pairs = new List<(TextBox a, TextBox b)>();
        foreach (var (a, ra) in rects)
        {
            var left = rects.Where(x => !ReferenceEquals(x.b, a)
                                        && Math.Abs(x.r.Center.Y - ra.Center.Y) < Math.Max(24, ra.Height * 0.75)
                                        && x.r.Center.X < ra.Center.X - 3)
                            .OrderBy(x => ra.Center.X - x.r.Center.X).FirstOrDefault();
            if (left.b is not null) pairs.Add((a, left.b));
        }
        Check("کادرهای هم‌ردیف پیدا شدند", pairs.Count > 0, $"{boxes.Count} کادر، {pairs.Count} جفت");

        foreach (var (a, b) in pairs.Take(6))
        {
            // ── حالِ پس از رسیدن: پر و همه انتخاب ──
            Focus(win, a, "12345", select: true);
            Key(win, Avalonia.Input.Key.Left);
            var to = Focused(win);
            Check($"← از «{Label(a)}» به کادرِ چپ رفت", ReferenceEquals(to, b), Label(to));

            Focus(win, b, "12345", select: true);
            Key(win, Avalonia.Input.Key.Right);
            to = Focused(win);
            var rb = FieldNavigationService.ScreenRect(b)!.Value;
            var rt = to is null ? (Rect?)null : FieldNavigationService.ScreenRect(to);
            Check($"→ از «{Label(b)}» به کادرِ راست رفت", rt is { } r && r.Center.X > rb.Center.X, Label(to));

            // ── کُرسر سرِ متن (بی انتخاب) هم ناوبری است ──
            Focus(win, a, "12345", select: false, caret: 5);
            Key(win, Avalonia.Input.Key.Left);
            Check($"← با کُرسرِ تهِ متن هم رفت", ReferenceEquals(Focused(win), b), Label(Focused(win)));

            // ── کُرسرِ وسط ⇒ ویرایشِ عادی ──
            Focus(win, a, "12345", select: false, caret: 2);
            Key(win, Avalonia.Input.Key.Left);
            Check($"کُرسرِ وسطِ متن: کادر ماند و کُرسر جابه‌جا شد",
                  ReferenceEquals(Focused(win), a) && a.CaretIndex != 2, $"caret={a.CaretIndex}");
            a.Text = ""; b.Text = "";
        }
    }

    private static void Waraq(Window win, MainViewModel vm)
    {
        var sec = vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(sec));
        Pump(win, 20);
        if (sec.GetType().GetProperty("OpenCardCommand")?.GetValue(sec)
                is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand open
            && sec.GetType().GetProperty("Cards")?.GetValue(sec) is System.Collections.IEnumerable list
            && list.Cast<object>().FirstOrDefault() is { } card)
        { Wait(win, open.ExecuteAsync(card)); Pump(win, 30); }

        var page = win.GetVisualDescendants().OfType<PumpYaqobi.App.Views.Sections.WaraqPageView>().FirstOrDefault(p => p.IsEffectivelyVisible);
        if (page?.DataContext is { } pvm && pvm.GetType().GetProperty("AddTxnCommand")?.GetValue(pvm)
                is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand add)
            for (var i = 0; i < 4; i++) { Wait(win, add.ExecuteAsync(null)); Pump(win, 10); }

        var grids = win.GetVisualDescendants().OfType<ExcelGrid>()
                       .Where(g => g.IsEffectivelyVisible && g.ItemsSource is System.Collections.IList { Count: > 0 })
                       .Select(g => (g, r: FieldNavigationService.ScreenRect(g)!.Value)).ToList();
        // دو جدولِ هم‌ردیف
        var pair = grids.SelectMany(x => grids.Where(y => !ReferenceEquals(x.g, y.g)
                                                         && x.r.Center.X > y.r.Center.X
                                                         && !(y.r.Bottom <= x.r.Top || y.r.Top >= x.r.Bottom))
                                               .Select(y => (right: x, left: y))).FirstOrDefault();
        Check("دو جدولِ تراکنشِ هم‌ردیف پیدا شدند", pair.right.g is not null, $"{grids.Count} جدولِ پر");
        if (pair.right.g is null) return;

        var rg = pair.right.g; var lg = pair.left.g;
        rg.Focus(); rg.SelectedIndex = 0;
        var cols = rg.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        rg.CurrentColumn = cols[^1];          // لبهٔ چپِ جدولِ راست
        Pump(win);
        Key(win, Avalonia.Input.Key.Left);
        Check("← از لبهٔ جدولِ راست به جدولِ چپ رفت",
              Focused(win)?.FindAncestorOfType<ExcelGrid>(true) is { } g1 && ReferenceEquals(g1, lg)
              || ReferenceEquals(Focused(win), lg), Focused(win)?.GetType().Name);
        Check("ستونِ لبهٔ راستِ جدولِ چپ", lg.CurrentColumn == lg.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).First());
        Key(win, Avalonia.Input.Key.Right);
        Check("→ برگشت به جدولِ راست",
              Focused(win)?.FindAncestorOfType<ExcelGrid>(true) is { } g2 && ReferenceEquals(g2, rg)
              || ReferenceEquals(Focused(win), rg), Focused(win)?.GetType().Name);
    }

    private static string Label(Control? c) => c is TextBox t ? (t.Watermark?.ToString() ?? t.Name ?? "کادر") : c?.GetType().Name ?? "هیچ";

    private static void Focus(Window win, TextBox t, string text, bool select, int caret = 0)
    {
        t.Text = text;
        t.Focus(NavigationMethod.Tab);
        Pump(win);
        if (select) t.SelectAll();
        else { t.SelectionStart = t.SelectionEnd = caret; t.CaretIndex = caret; }
        Pump(win);
    }

    private static Control? Focused(Window win) => win.FocusManager?.GetFocusedElement() as Control;

    private static void Key(Window win, Key k)
    {
        var p = k == Avalonia.Input.Key.Left ? PhysicalKey.ArrowLeft : PhysicalKey.ArrowRight;
        win.KeyPressQwerty(p, RawInputModifiers.None);
        win.KeyReleaseQwerty(p, RawInputModifiers.None);
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
