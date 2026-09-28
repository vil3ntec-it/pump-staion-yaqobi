using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ کشوییِ «☰ کارها» — با پنجرهٔ واقعی (۱۴۰۵/۰۷/۱۶) ══════════════════════
/// خواستهٔ صاحب ریپو: تاریخچه، ماهِ جدید، PDF و کارت‌های زیربخشِ هفت بخش در یک
/// کشویی. این سنجه کشویی را واقعاً باز می‌کند، دکمه‌های داخلش را می‌شمارد و
/// «🕘 تاریخچه» و هر زیربخش را واقعاً می‌زند — دکمه‌ای که در کشویی باشد و کار
/// نکند، سرخ است. و «تاریخچهٔ پایه‌ها»ی پارچه دیگر نباید دیده شود.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- moremenu
/// </summary>
internal static class MoreMenuProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private static void Pump(Window w, int n = 10)
    {
        for (var i = 0; i < n; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    /// <summary>همان کارِ کلیکِ واقعی: اول رویدادِ ‎Click‎ (که کشویی را می‌بندد)، بعد فرمان.</summary>
    private static void Press(Button b)
    {
        b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        b.Command?.Execute(b.CommandParameter);
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) Pump(w, 2);
        Pump(w);
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-more-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win, 30);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Pump(win, 30);

        // (بخش، چه دکمه‌هایی در کشویی، آیا زیربخش‌ها هم در کشویی‌اند)
        var cases = new (string id, string[] items, bool subs)[]
        {
            ("debt",      new[] { "تاریخچه" },                        true),
            ("debtrasid", new[] { "تاریخچه", "PDF" },                 true),
            ("sarrafi",   new[] { "ماه جدید", "تاریخچه", "PDF" },     false),
            ("expenses",  new[] { "ماه جدید", "تاریخچه", "PDF" },     false),
            ("safe",      new[] { "ماه جدید", "تاریخچه", "PDF" },     false),
            ("profit",    Array.Empty<string>(),                      true),
        };

        foreach (var (id, items, subs) in cases)
        {
            Console.WriteLine($"════ {id} ════");
            var sec = vm.Sections.First(s => s.Id == id);
            Wait(win, vm.GoAsync(sec));
            Pump(win, 20);

            var page = win.GetVisualDescendants().OfType<SectionPage>()
                          .FirstOrDefault(p => ReferenceEquals(p.DataContext, sec) && p.IsEffectivelyVisible);
            Check("صفحهٔ بخش پیدا شد", page is not null);
            if (page is null) continue;

            var more = page.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "PART_More");
            Check("دکمهٔ «☰ کارها» دیده می‌شود", more is { IsEffectivelyVisible: true });
            if (more is null) continue;

            // کارت‌های زیربخش دیگر کنارِ نوار نیستند
            if (subs)
                Check("کارت‌های زیربخش کنارِ نوار نیستند", !sec.ShowSubLinkCards && sec.ShowSubMenuItems);

            // «🕘 تاریخچه» دیگر بیرونِ کشویی نیست
            var outside = page.GetVisualDescendants().OfType<Button>()
                              .Where(b => b.IsEffectivelyVisible && (b.Content as string ?? "").Contains("تاریخچه")
                                          && sec.SubSections.All(s => !ReferenceEquals(b.CommandParameter, s)))
                              .ToList();
            Check("هیچ دکمهٔ «تاریخچه»ای بیرونِ کشویی نیست", outside.Count == 0, outside.Count.ToString());

            more.Flyout!.ShowAt(more);
            Pump(win, 20);
            var inMenu = page.MoreBody.GetVisualDescendants().OfType<Button>()
                             .Where(b => b.IsEffectivelyVisible).ToList();
            var texts = inMenu.Select(b => b.Content as string ?? "").ToList();
            Check("کشویی باز شد", inMenu.Count > 0, string.Join(" | ", texts));
            foreach (var it in items)
                Check($"«{it}» در کشویی است", texts.Any(t => t.Contains(it)));
            if (subs)
                foreach (var s in sec.SubSections)
                    Check($"زیربخشِ «{s.LinkTitle}» در کشویی است", texts.Contains(s.LinkTitle));
            Check("هر دکمهٔ کشویی فرمان دارد", inMenu.All(b => b.Command is not null),
                  string.Join(",", inMenu.Where(b => b.Command is null).Select(b => b.Content)));

            // زیربخشِ اول را واقعاً بزن
            if (subs && sec.SubSections.Count > 0)
            {
                var first = sec.SubSections[0];
                var b = inMenu.First(x => ReferenceEquals(x.CommandParameter, first));
                Press(b);
                Pump(win, 30);
                Check($"زدنِ «{first.LinkTitle}» همان زیربخش را باز کرد", ReferenceEquals(sec.OpenSub, first));
                Check("کشویی پس از زدن بسته شد", !more.Flyout.IsOpen);
                sec.CloseSub();
                Pump(win, 20);
            }

            // تاریخچه را واقعاً بزن
            if (items.Contains("تاریخچه"))
            {
                more.Flyout.ShowAt(more);
                Pump(win, 20);
                var h = page.MoreBody.GetVisualDescendants().OfType<Button>()
                            .FirstOrDefault(x => x.IsEffectivelyVisible && (x.Content as string ?? "").Contains("تاریخچه")
                                                 && x.CommandParameter is null);
                Check("دکمهٔ تاریخچه در کشویی", h is not null);
                if (h is not null)
                {
                    Press(h);
                    for (var i = 0; i < 100 && vm.Current?.Id != "history"; i++) Pump(win, 3);
                    Check("زدنِ تاریخچه به «تاریخچه‌ها» رفت", vm.Current?.Id == "history", vm.Current?.Id);
                }
            }
            if (more.Flyout.IsOpen) more.Flyout.Hide();
        }

        Console.WriteLine("════ parcha ════");
        var parcha = vm.Sections.First(s => s.Id == "shifts");
        Wait(win, vm.GoAsync(parcha));
        Pump(win, 20);
        var shown = win.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible);
        var words = shown.OfType<TextBlock>().Select(t => t.Text ?? "")
                         .Concat(shown.OfType<Button>().Select(b => b.Content as string ?? ""));
        Check("«تاریخچهٔ پایه‌ها» دیگر در پارچه‌ها نیست", !words.Any(t => t.Contains("تاریخچهٔ پایه‌ها")));

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }
}
