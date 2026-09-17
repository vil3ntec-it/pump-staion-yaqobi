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
/// ══ «تو کادرها این‌ها نوشته نمی‌شوند: + @ # ﷼ ( ) ؟ ؛ : , .» ═══════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «لاگین مشکل دارد، این کادرِ ایمیل ایمیل
/// نیست و هیچی توش نوشته نمی‌شود؛ توی هیچ‌کدامشان این‌ها نوشته نمی‌شوند.»
///
/// این سنجه **با کلیدِ واقعی** تایپ می‌کند و می‌گوید کدام نویسه می‌نشیند و
/// کدام نه — در کادرِ ورود و در جست‌وجوی قرض‌داران.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- inputchars
/// </summary>
internal static class InputCharsProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}{(detail is null ? "" : " — " + detail)}");
        if (!ok) _bad++;
    }

    /// <summary>هر نویسه با کلیدی که واقعاً روی صفحه‌کلید می‌سازدش.</summary>
    private static readonly (string Name, PhysicalKey Key, bool Shift, string Char)[] Keys =
    {
        //  ⚠️ همان‌هایی که صاحب ریپو گفت — روی چیدمانِ انگلیسی و فارسی، همهٔ
        //  این‌ها روی ردیفِ عددها با Shift می‌نشینند (فارسی: ﷼ ٪ × ، ) ( ).
        ("@ (Shift+۲)", PhysicalKey.Digit2, true, "@"),
        ("# (Shift+۳)", PhysicalKey.Digit3, true, "#"),
        ("$ (Shift+۴ — جای ﷼ در چیدمانِ فارسی)", PhysicalKey.Digit4, true, "$"),
        ("% (Shift+۵)", PhysicalKey.Digit5, true, "%"),
        ("( (Shift+۹)", PhysicalKey.Digit9, true, "("),
        (") (Shift+۰)", PhysicalKey.Digit0, true, ")"),
        ("+ (Shift+=)", PhysicalKey.Equal, true, "+"),
        (": (Shift+;)", PhysicalKey.Semicolon, true, ":"),
        ("? (Shift+/ — جای ؟)", PhysicalKey.Slash, true, "?"),
        (". (نقطه)", PhysicalKey.Period, false, "."),
        (", (ویرگول)", PhysicalKey.Comma, false, ","),
        ("۵ (عددِ ساده)", PhysicalKey.Digit5, false, "5"),
        ("a (حرف)", PhysicalKey.A, false, "a"),
    };

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-inputchars-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1366, Height = 768 };
        win.Show(); Pump(win);
        Watch(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234"; vm.Lock.SubmitCommand.Execute(null);
        for (var i = 0; i < 40; i++) Pump(win);

        var account = (AccountSectionViewModel)vm.Sections.First(s => s.Id == "account");
        Wait(win, vm.GoAsync(account));
        for (var i = 0; i < 30; i++) Pump(win);

        Console.WriteLine("── کادرِ ایمیلِ صفحهٔ ورود");
        var email = Boxes(win).FirstOrDefault(b => b.Watermark == "ایمیل");
        if (email is null)
        {
            Console.WriteLine("  ✖ کادرِ ایمیل پیدا نشد");
            return 1;
        }
        Type(win, email);

        Console.WriteLine("── کادرِ نامِ پمپ (گامِ دو)");
        account.SkipAccountCommand.Execute(null);
        for (var i = 0; i < 20; i++) Pump(win);
        var pump = Boxes(win).FirstOrDefault(b => b.Watermark == "نامِ پمپ");
        if (pump is not null) Type(win, pump);
        else Console.WriteLine("  ⚠️ کادرِ نامِ پمپ پیدا نشد");

        Console.WriteLine("── جست‌وجوی قرض‌داران (بیرونِ صفحهٔ ورود)");
        var debt = vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(debt));
        for (var i = 0; i < 30; i++) Pump(win);
        var search = Boxes(win).FirstOrDefault(b => b.IsEffectivelyVisible && b.IsEnabled);
        if (search is not null) Type(win, search);
        else Console.WriteLine("  ⚠️ کادری پیدا نشد");

        //  ⚠️ و آن طرفِ سکه: بیرونِ کادرِ تایپ، میانبرِ «Shift+عدد ⇒ حذفِ
        //  ردیف» باید سرِ جایش باشد — وگرنه این اصلاح یک چیز را درست و یک
        //  چیز را خراب کرده است.
        Console.WriteLine("── بیرونِ کادر، میانبرِ Shift+عدد باید زنده باشد");
        var card = win.GetVisualDescendants().OfType<Button>()
                      .FirstOrDefault(b => b.IsEffectivelyVisible && b.IsEnabled);
        card?.Focus();
        Pump(win);
        _eaten = false;
        win.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Shift);
        Pump(win);
        Check("Shift+۳ بیرونِ کادرِ تایپ همچنان میانبر است", _eaten,
              _eaten ? "" : "میانبر از کار افتاد");

        Console.WriteLine();
        Console.WriteLine(_bad == 0
            ? "✅ هر نویسه‌ای که کاربر می‌زند داخلِ کادر می‌نشیند"
            : $"❌ {_bad} نویسه داخلِ کادر نمی‌نشیند");
        return _bad == 0 ? 0 : 1;
    }

    private static void Type(Window win, TextBox box)
    {
        foreach (var (name, key, shift, ch) in Keys)
        {
            box.Focus();
            box.Text = "";
            box.CaretIndex = 0;
            Pump(win);
            //  ⚠️ **سنجه، «خورده شدنِ کلید» است، نه متنِ داخلِ کادر.**
            //  روی ویندوز، کلیدی که یک شنونده `Handled` کند دیگر `WM_CHAR`
            //  نمی‌سازد، یعنی آن نویسه **اصلاً تایپ نمی‌شود** — همان چیزی که
            //  صاحب ریپو دید. آوالونیای هدلس این را تقلید نمی‌کند (نویسه را
            //  جدا می‌فرستد)، پس اگر فقط به متنِ کادر نگاه کنیم، سنجه سبزِ
            //  دروغ می‌دهد. برای همین خودِ `Handled` را می‌سنجیم.
            _eaten = false;
            win.KeyPressQwerty(key, shift ? RawInputModifiers.Shift : RawInputModifiers.None);
            Pump(win);
            var eaten = _eaten;
            if (!eaten) { win.KeyTextInput(ch); Pump(win); }
            var got = box.Text ?? "";
            Check(name, !eaten && got == ch,
                  eaten ? "کلید خورده شد — روی ویندوز هیچ نویسه‌ای تایپ نمی‌شود"
                        : (got.Length == 0 ? "هیچ چیز نوشته نشد" : "«" + got + "»"));
        }
        box.Text = "";
    }

    private static bool _eaten;

    /// <summary>
    /// شنونده‌ای که **پس از** همهٔ تونل‌ها می‌دود (`handledEventsToo`) و
    /// می‌گوید کلید خورده شد یا نه.
    /// </summary>
    private static void Watch(Window win) =>
        win.AddHandler(InputElement.KeyDownEvent, (_, e) => { if (e.Handled) _eaten = true; },
                       Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);

    private static IEnumerable<TextBox> Boxes(Window win) =>
        win.GetVisualDescendants().OfType<TextBox>().Where(b => b.IsEffectivelyVisible);

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Wait(Window win, Task t)
    {
        for (var i = 0; i < 4000 && !t.IsCompleted; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(2); }
        Dispatcher.UIThread.RunJobs();
        if (t.IsFaulted) throw t.Exception!;
    }
}
