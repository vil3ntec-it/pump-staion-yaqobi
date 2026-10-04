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

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ شورا، ث — تجربهٔ کاربر با پنجره و کلیکِ واقعی ════════════════════════
///     dotnet run --project PumpYaqobi.UiTests -c Release -- ux [پوشهٔ عکس]
///
///  ث۲) نوارِ میانبرهای زیرِ جدول دیده می‌شود و «✕» آن را برای همیشه می‌بندد ·
///      «➕ ردیف»ِ نخستین یک «💡 می‌دانستید؟» می‌دهد، دومی نه · کادرِ «نام»ِ ورق
///      راهنمای کم‌رنگ دارد و نخستین ویرایشش «💡» می‌دهد.
///  ث۴) کلیکِ چراغ ⇒ کادرِ سه‌ردیفی (حساب · خانگی · همگام‌سازی) با یک دکمهٔ کار
///      برای هر کدام، و ⛔ هیچ نشانیِ سروری در آن نیست.
/// </summary>
internal static class UxProbe
{
    private static readonly List<string> Bad = new();
    private static readonly List<string> Toasts = new();

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-ux");
        Directory.CreateDirectory(shots);
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-ux-" + Guid.NewGuid().ToString("N"), "pump.db");
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

        Hints.Quiet = false;
        Hints.Show = Toasts.Add;

        Strips(win, vm);
        WaraqName(win, vm);
        LinkPanel(win, vm, shots);
        Simple(win, vm);

        Console.WriteLine(Bad.Count == 0 ? "✅ همه سبز" : $"❌ {Bad.Count} ایراد");
        return Bad.Count == 0 ? 0 : 1;
    }

    private static void Strips(Window win, MainViewModel vm)
    {
        Console.WriteLine("── ث۲) نوارِ میانبرها و «می‌دانستید؟» ──");
        Wait(win, vm.GoAsync(vm.Sections.First(s => s.Id == "safe")));
        Settle(win);
        var strip = win.GetVisualDescendants().OfType<HintStrip>()
            .FirstOrDefault(s => s.HintKey == "rows" && s.IsEffectivelyVisible);
        Check("نوارِ میانبرهای جدول زیرِ گاوصندوق دیده می‌شود", strip is not null);
        var txt = strip?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? "";
        Check($"متنش Ctrl+عدد و Shift+عدد را می‌گوید («{txt}»)", txt.Contains("Ctrl+عدد") && txt.Contains("Shift+عدد"));

        var add = win.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == "PART_Add" && b.IsEffectivelyVisible);
        Check("دکمهٔ «➕ ردیف» پیدا شد", add is not null);
        var before = Toasts.Count;
        if (add is not null) { Click(win, add); Click(win, add); }
        Check($"«➕ ردیف»ِ نخستین یک «💡» داد و دومی نه ({Toasts.Count - before})",
              Toasts.Count - before == 1 && Toasts[^1].StartsWith("💡") && Toasts[^1].Contains("Ctrl+عدد"));

        var close = strip?.GetVisualDescendants().OfType<Button>().FirstOrDefault();
        if (close is not null) Click(win, close);
        Check("«✕» نوار را بست", strip is { IsVisible: false });
        Check("و روی همین کامپیوتر به یاد ماند", Hints.IsSeen("strip:rows"));
        Wait(win, vm.GoAsync(vm.Sections.First(s => s.Id == "expenses")));
        Settle(win);
        Check("نوارِ همان کلید در بخشِ دیگر هم دیگر نیست",
              !win.GetVisualDescendants().OfType<HintStrip>().Any(s => s.HintKey == "rows" && s.IsEffectivelyVisible));
    }

    private static void WaraqName(Window win, MainViewModel vm)
    {
        Console.WriteLine("── ث۲) کادرِ «نام»ِ ورق ──");
        var h = AppHost.Current;
        h.WaraqData.OpenOrCreateAsync(PumpYaqobi.Application.Localization.Shamsi.Today(), "").GetAwaiter().GetResult();
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(wq));
        Settle(win);
        Wait(win, wq.ReloadAsync());
        Wait(win, wq.OpenCommand.ExecuteAsync(wq.Sheets.First()));
        Settle(win);
        var page = wq.Page!;
        while (page.Txns.Count < 2) { Wait(win, page.AddTxnCommand.ExecuteAsync(null)); Settle(win); }
        Check("نوارِ قاعده‌های ورق دیده می‌شود",
              win.GetVisualDescendants().OfType<HintStrip>().Any(s => s.HintKey == "waraq" && s.IsEffectivelyVisible));
        var g = win.GetVisualDescendants().OfType<ExcelGrid>()
            .FirstOrDefault(x => x.IsEffectivelyVisible && ReferenceEquals(x.ItemsSource, page.TxnsFirst));
        if (g is null) { Fail("جدولِ تراکنش‌ها پیدا نشد"); return; }
        var before = Toasts.Count;
        g.SelectedIndex = 0;
        g.CurrentColumn = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).First();
        Settle(win);
        g.BeginEdit();
        Settle(win);
        var box = win.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(t => t.IsEffectivelyVisible && t.FindAncestorOfType<DataGridCell>() is not null);
        Check($"راهنمای کم‌رنگ داخلِ کادر («{box?.Watermark}»)", box?.Watermark == Hints.Watermark["waraq-name"]);
        Check("نخستین ویرایشِ «نام» یک «💡» داد", Toasts.Count - before == 1 && Toasts[^1].Contains("/ هارون"));
        g.CancelEdit();
        Settle(win);
        g.BeginEdit(); Settle(win); g.CancelEdit(); Settle(win);
        Check("بارِ دوم «💡» نیامد", Toasts.Count - before == 1);
        //  صفحهٔ ورق پوستهٔ پنجره (سربرگ و نوار) را پنهان می‌کند — بستنش برای بندهای بعد
        wq.CloseOpenPage();
        Settle(win);
    }

    private static void LinkPanel(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine("── ث۴) کادرِ چراغ ──");
        var dot = win.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "LinkDotBtn");
        Check("چراغ پیدا شد", dot is not null);
        if (dot is null) return;
        Click(win, dot);
        Settle(win);
        var panel = win.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(p => p.Name == "LinkPanel")
                    ?? TopLevel.GetTopLevel(win)?.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(p => p.Name == "LinkPanel");
        panel ??= (dot.Flyout as Flyout)?.Content as StackPanel;
        Check("کلیک ⇒ کادرِ وضعِ اتصال باز شد", dot.Flyout is Flyout f && f.IsOpen, (dot.Flyout as Flyout)?.IsOpen.ToString());
        if (panel is null) { Fail("محتوای کادر پیدا نشد"); return; }
        var texts = panel.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")
            .Concat(panel.GetLogicalDescendantsSafe()).ToList();
        var all = string.Join(" | ", texts);
        foreach (var title in new[] { "سرورِ حساب", "سرورِ خانگی", "همگام‌سازی" })
            Check($"ردیفِ «{title}»", all.Contains(title));
        var buttons = panel.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string ?? "").ToList();
        Check($"هر ردیف یک دکمهٔ کار ({string.Join("،", buttons)})",
              buttons.Contains("بپرس") && buttons.Contains("بگرد") && buttons.Contains("همگام کن"));
        Check("⛔ هیچ نشانیِ سروری در کادر نیست", !all.Contains("http") && !all.Contains("vill3n") && !all.Contains("127.0.0.1")
              && !all.Contains(".top"), all.Length > 200 ? all[..200] : all);
        Check("دکمهٔ «بپرس» فرمان دارد", panel.GetVisualDescendants().OfType<Button>().All(b => b.Command is not null));
        try { win.CaptureRenderedFrame()?.Save(Path.Combine(shots, "link-panel.png")); } catch { }
        (dot.Flyout as Flyout)?.Hide();
        Settle(win);
    }

    private static void Simple(Window win, MainViewModel vm)
    {
        Console.WriteLine("── ث۶) حالتِ ساده ──");
        int NavButtons() => win.GetVisualDescendants().OfType<Button>()
            .Count(b => b.Classes.Contains("nav") && b.IsEffectivelyVisible);
        var full = vm.NavSections.Count;
        var sectionsBefore = vm.Sections.Count;
        vm.SimpleMode = true;
        Settle(win);
        var ids = vm.NavSections.Select(s => s.Id).ToList();
        Check($"نوار فقط پنج بخشِ روزانه ({string.Join("،", ids)})",
              ids.Count == 5 && ids.All(i => MainViewModel.SimpleIds.Contains(i)) && NavButtons() == 5, NavButtons().ToString());
        Check("⛔ هیچ بخشی حذف نشد", vm.Sections.Count == sectionsBefore);
        var all = win.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && b.Content is string t && t.StartsWith("☰ همه"));
        Check("دکمهٔ «☰ همه» در سربرگ", all is not null);
        if (all is not null) Click(win, all);
        Check($"«☰ همه» ⇒ همهٔ بخش‌ها ({vm.NavSections.Count})", vm.NavSections.Count == full && NavButtons() == full);
        if (all is not null) Click(win, all);
        Check("دوباره ⇒ فقط روزانه", vm.NavSections.Count == 5);
        //  Alt+۱ همان نخستین بخشِ دیدنی را می‌رود
        win.KeyPressQwerty(PhysicalKey.AltLeft, RawInputModifiers.None);
        win.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.Alt);
        win.KeyReleaseQwerty(PhysicalKey.Digit2, RawInputModifiers.Alt);
        win.KeyReleaseQwerty(PhysicalKey.AltLeft, RawInputModifiers.None);
        Settle(win);
        Check($"Alt+2 ⇒ دومین بخشِ دیدنی ({vm.Current?.Id} / {vm.NavSections[1].Id})", vm.Current?.Id == vm.NavSections[1].Id);
        Check("یادِ همین کامپیوتر", AppSettings.Load().SimpleMode);
        vm.SimpleMode = false;
        Settle(win);
        Check("خاموش ⇒ همهٔ بخش‌ها", vm.NavSections.Count == full && !AppSettings.Load().SimpleMode);
    }

    private static IEnumerable<string> GetLogicalDescendantsSafe(this StackPanel p)
    {
        foreach (var c in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(p))
        {
            if (c is TextBlock t && t.Text is { } s) yield return s;
            if (c is TextBlock t2 && t2.Inlines is { } il) foreach (var r in il.OfType<Avalonia.Controls.Documents.Run>()) yield return r.Text ?? "";
        }
    }

    private static void Click(Window win, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), win);
        if (p is null) { Fail("جای دکمه پیدا نشد"); return; }
        win.MouseDown(p.Value, MouseButton.Left);
        win.MouseUp(p.Value, MouseButton.Left);
        Settle(win);
    }

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}" + (detail is null || ok ? "" : " — " + detail));
        if (!ok) Bad.Add(what);
    }

    private static void Fail(string what) { Console.WriteLine("  ✖ " + what); Bad.Add(what); }

    private static void Settle(Window w)
    {
        var end = DateTime.UtcNow + TimeSpan.FromMilliseconds(600);
        while (DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(10); }
        Pump(w);
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        Pump(w);
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
