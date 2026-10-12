using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «تیل امانت» در حالِ ساخت و «دوربین‌ها» پنهان — با پنجرهٔ واقعی ════════
/// دکمهٔ واقعیِ نوار زده می‌شود (نه فرمان)، بعد دیده می‌شود کدام بخش باز است.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- sectiongate
/// </summary>
internal static class SectionGateProbe
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

    public static int Run()
    {
        SectionGate.TestOpenAll = false;   // همین سنجه دروازه را می‌سنجد
        SectionGate.ResetForTests();

        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-gate-" + Guid.NewGuid().ToString("N"), "pump.db");
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

        Console.WriteLine("════ دوربین‌ها ════");
        Check("دوربین‌ها در نوار نیست", vm.NavSections.All(s => s.Id != "cameras"));
        Check("ولی خودِ بخش در برنامه هست", vm.Sections.Any(s => s.Id == "cameras"));
        var navTexts = win.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
        Check("نامِ «دوربین‌ها» در پنجره دیده نمی‌شود", !navTexts.Any(t => t.Contains("دوربین")));
        var cams = vm.Sections.First(s => s.Id == "cameras");
        var before = vm.Current;
        Wait(win, vm.GoAsync(cams));
        Check("رفتن به دوربین‌ها (مثلاً از Alt+عدد) هیچ کاری نمی‌کند", ReferenceEquals(vm.Current, before));

        Console.WriteLine("════ تیل امانت ════");
        Check("تیل امانت در نوار هست", vm.NavSections.Any(s => s.Id == "amanat"));
        var am = vm.Sections.First(s => s.Id == "amanat");
        var btn = win.GetVisualDescendants().OfType<Button>()
                     .FirstOrDefault(b => ReferenceEquals(b.CommandParameter, am));
        Check("دکمهٔ واقعیِ نوار پیدا شد", btn is not null);

        //  ⛔ (۱۴۰۵/۰۷/۲۰) «رمزش را هم بردار» — یک زدن، بی رمز، بی «در حالِ ساخت»
        string? asked = null;
        Dialogs.PromptHook = (t, m) => { asked = t; return ""; };

        void Click() { btn!.Command!.Execute(btn.CommandParameter); Pump(win, 20); }

        Click();
        Check("یک زدن ⇒ تیل امانت باز شد", ReferenceEquals(vm.Current, am), vm.Current?.Id);
        Check("رمزی پرسیده نشد", asked is null, asked);

        SectionGate.ResetForTests();
        Wait(win, vm.GoAsync(vm.Sections.First(s => s.Id == "safe")));
        Click();
        Check("اجرای بعدی (حافظه پاک) ⇒ باز هم بی رمز باز می‌شود", ReferenceEquals(vm.Current, am) && asked is null);

        Dialogs.PromptHook = null;
        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) Pump(w, 2);
        Pump(w);
    }
}
