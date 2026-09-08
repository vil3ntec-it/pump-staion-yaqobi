using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ سنجشِ صفحهٔ حسابِ قرض‌دار، روی پنجرهٔ واقعی ═══════════════════════════════
///
/// گزارشِ صاحب ریپو: «چرا حساب فرعیِ قرض‌داران کار نمی‌کند؟ چرا جدولِ جدیدِ همان
/// حساب درست نمی‌شود؟»
///
/// به‌جای حدس زدن، این حالت همان کاری را می‌کند که کاربر می‌کند: پنجرهٔ واقعی
/// را در <b>کوچک‌ترین اندازهٔ مجاز</b> (۱۰۰۰×۶۴۰، همان ‎MinWidth/MinHeight‎ی
/// ‎MainWindow‎) باز می‌کند، یک قرض‌دار را باز می‌کند و دو چیز را می‌سنجد:
///
///   ۱. <b>می‌شود زدشان؟</b> هر دکمه/کادری که لبه‌اش بیرونِ پنجره بیفتد، با
///      ماوس اصلاً قابلِ زدن نیست. چون کلِ پنجره ‎RightToLeft‎ است، نوارِ
///      یک‌خطیِ حساب از راست چیده می‌شود و دُمش از <b>لبهٔ چپ</b> بیرون
///      می‌زند — دقیقاً همان چیزی که خودِ سایت هم یک‌بار خورده بود و در
///      ‎.pm-acct-bar‎ نوشته: «فیِ خرید و ٪ فیصدی بیرونِ لبهٔ چپ می‌ماندند و
///      اصلاً قابلِ زدن نبودند».
///
///   ۲. <b>کار می‌کنند؟</b> «➕ حساب جدید» و «📋 جدول جدید» هر دو پشتِ یک
///      پنجرهٔ گفت‌وگواند. با قلاب‌های ‎Dialogs.PromptHook/ConfirmHook‎ پاسخِ
///      کاربر از پیش گذاشته می‌شود و کلِ مسیر تا دیتابیس واقعاً اجرا می‌شود.
///
///     dotnet run --project PumpYaqobi.UiTests -- person
/// </summary>
internal static class PersonAudit
{
    /// <summary>پنجره در کوچک‌ترین اندازهٔ مجاز — همان‌جا که سرریز پیدا می‌شود.</summary>
    private const double W = 1000, H = 640;

    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-person-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = W, Height = H };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Pump(win);
        Seed.Fill(AppHost.Current);

        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is not DebtSectionViewModel debt)
        { Console.WriteLine("✖ بخشِ قرض‌داران پیدا نشد"); return 1; }

        Wait(win, vm.GoAsync(debt));
        Pump(win);
        Wait(win, debt.RefreshAsync());
        Pump(win);
        debt.OpenCommand.Execute(debt.Cards.FirstOrDefault());
        Pump(win);

        var person = debt.Person;
        if (person is null) { Console.WriteLine("✖ صفحهٔ شخص باز نشد"); return 1; }

        Console.WriteLine();
        Console.WriteLine("── ۱) هر دکمه و کادرِ صفحهٔ حساب، داخلِ پنجره است؟ ──");
        Reachability(win);

        Console.WriteLine();
        Console.WriteLine("── ۲) «➕ حساب جدید» ──");
        SubAccount(win, person);

        Console.WriteLine();
        Console.WriteLine("── ۳) «📋 جدول جدید» ──");
        NewTable(win, person);

        Console.WriteLine();
        Console.WriteLine("── ۴) پس از بستن و باز کردنِ دوباره ──");
        Persistence(win, debt);

        Console.WriteLine();
        if (_bad == 0) { Console.WriteLine("✅ صفحهٔ حسابِ قرض‌دار سالم است"); return 0; }
        Console.WriteLine($"❌ {_bad} ایراد در صفحهٔ حسابِ قرض‌دار");
        return 1;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ۱) می‌شود زدشان؟
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// هر کنترلِ کلیک‌شدنی که بخشی از آن بیرونِ پنجره بماند، برای کاربر وجود
    /// ندارد. نامش را هم چاپ می‌کنیم تا معلوم باشد کدام از دست رفته.
    /// </summary>
    private static void Reachability(Window win)
    {
        // ⚠️ فقط داخلِ خودِ صفحهٔ شخص. کلِ پنجره را گشتن، صفحهٔ قفلِ پنهان و
        // فهرستِ کارت‌های پشتِ صفحه را هم می‌شمرد و گزارش را بی‌معنا می‌کرد.
        var page = win.GetVisualDescendants()
                      .OfType<PumpYaqobi.App.Views.Sections.PersonView>()
                      .FirstOrDefault();
        if (page is null) { Check("صفحهٔ شخص در درختِ دیداری پیدا شد", false); return; }

        var outside = new List<string>();
        var seen = 0;

        foreach (var c in page.GetVisualDescendants().OfType<Control>())
        {
            if (c is not (Button or TextBox or ComboBox or CheckBox or RadioButton)) continue;
            if (c.Bounds.Width <= 0) continue;
            // پدرِ پنهان یعنی خودش هم دیده نمی‌شود — ‎IsVisible‎ی خودش کافی نیست
            if (!c.IsEffectivelyVisible) continue;

            // ⚠️ داخلِ جدول را نمی‌سنجیم: خودِ جدول اسکرولِ افقی دارد و ستونِ
            // بیرونِ قاب همان‌قدر طبیعی است که در ‎overflow-x:auto‎ی سایت.
            if (c.GetVisualAncestors().OfType<DataGrid>().Any()) continue;

            // ⚠️ کلِ پنجره ‎RightToLeft‎ است و آوالونیا با آینه‌کردن می‌چیندش،
            // پس نقطهٔ ‎(0,0)‎ی محلیِ یک کنترل به لبهٔ **راستش** می‌افتد. اگر
            // فقط همان را بگیریم و پهنا را رویش بیفزاییم، هر کنترلِ سالم هم
            // «بیرونِ پنجره» گزارش می‌شود (همان چیزی که بارِ اول شد). پس هر دو
            // گوشه ترجمه می‌شود و کوچک‌تر/بزرگ‌تر برداشته می‌شود.
            var a = c.TranslatePoint(new Point(0, 0), win);
            var b = c.TranslatePoint(new Point(c.Bounds.Width, 0), win);
            if (a is null || b is null) continue;

            seen++;
            var left = Math.Min(a.Value.X, b.Value.X);
            var right = Math.Max(a.Value.X, b.Value.X);
            if (left < -0.5 || right > W + 0.5)
                outside.Add($"{Label(c)} [x {left:0}…{right:0}]");
        }

        Check($"{seen} کنترلِ صفحهٔ حساب سنجیده شد؛ همه داخلِ پنجره", outside.Count == 0);
        foreach (var o in outside) Console.WriteLine("        بیرونِ پنجره: " + o);
    }

    private static string Label(Control c) => c switch
    {
        Button { Content: string t } => "دکمهٔ «" + t + "»",
        Button b => "دکمهٔ " + (b.Name ?? b.GetType().Name),
        TextBox { Watermark: { Length: > 0 } w } => "کادرِ «" + w + "»",
        ComboBox => "کشویی",
        _ => c.GetType().Name,
    };

    // ══════════════════════════════════════════════════════════════════════
    //  ۲) حسابِ فرعیِ تازه
    // ══════════════════════════════════════════════════════════════════════

    private static void SubAccount(Window win, PersonViewModel person)
    {
        var before = person.Accounts.Count;
        const string name = "موترِ دومِ آزمون";

        Dialogs.PromptHook = (_, _) => name;
        try
        {
            person.AddSubAccountCommand.Execute(null);
            Pump(win);
        }
        finally { Dialogs.PromptHook = null; }

        Check($"حساب ساخته شد ({before} ← {person.Accounts.Count})",
              person.Accounts.Count == before + 1);
        Check("نامِ خودش را گرفت", person.Accounts.Last().Title == name,
              person.Accounts.Last().Title);
        Check("همان لحظه باز شد", person.Current?.Title == name);

        // و در کشویی هم دیده می‌شود — وگرنه ساخته شده ولی رسیدنی نیست
        var combo = win.GetVisualDescendants().OfType<ComboBox>()
                       .FirstOrDefault(b => b.ItemCount == person.Accounts.Count);
        Check("در کشوییِ حساب‌ها آمد", combo is not null);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ۳) جدولِ جدید
    // ══════════════════════════════════════════════════════════════════════

    private static void NewTable(Window win, PersonViewModel person)
    {
        // روی حسابِ اصلی، که ردیف دارد
        person.Current = person.Accounts[0];
        Pump(win);

        var acct = person.Current!;
        if (acct.Rows.Count == 0)
        {
            acct.AddRowCommand.Execute(null);
            Pump(win);
        }

        var rowsBefore = acct.Rows.Count;
        var arcBefore = acct.ArchiveCount;

        Dialogs.ConfirmHook = (_, _) => true;
        try
        {
            acct.NewTableCommand.Execute(null);
            Pump(win);
            // ذخیره و بارگیریِ آرشیو ناهم‌گام است — چند دور بچرخان
            for (var i = 0; i < 40 && acct.ArchiveCount == arcBefore; i++) Pump(win);
        }
        finally { Dialogs.ConfirmHook = null; }

        Check($"جدول خالی شد ({rowsBefore} ← {acct.Rows.Count})", acct.Rows.Count == 0);
        Check($"جدولِ قبلی در آرشیو نشست ({arcBefore} ← {acct.ArchiveCount})",
              acct.ArchiveCount == arcBefore + 1);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ۴) ماندگاری
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ساخته شدن کافی نیست — باید بماند. صفحه بسته و از نو از دیتابیس باز
    /// می‌شود تا معلوم شود حسابِ فرعی و جدولِ آرشیو واقعاً روی دیسک نشسته‌اند،
    /// نه فقط در حافظهٔ همان لحظه.
    /// </summary>
    private static void Persistence(Window win, DebtSectionViewModel debt)
    {
        // باز کردنِ دوباره همیشه ‎LoadFullAsync‎ می‌زند، یعنی از خودِ دیتابیس
        // می‌خواند — پس همین یک کار برای سنجشِ ماندگاری بس است.
        var before = debt.Person;
        debt.OpenCommand.Execute(debt.Cards.FirstOrDefault());
        Pump(win);
        for (var i = 0; i < 60 && ReferenceEquals(debt.Person, before); i++) Pump(win);

        var again = debt.Person;
        if (again is null) { Check("صفحهٔ شخص دوباره باز شد", false); return; }

        Check($"حسابِ فرعی ماند ({again.Accounts.Count} حساب)", again.Accounts.Count == 2,
              string.Join(" · ", again.Accounts.Select(a => a.Title)));

        var main = again.Accounts[0];
        again.Current = main;
        Pump(win);
        for (var i = 0; i < 40 && main.ArchiveCount == 0; i++) Pump(win);

        Check($"جدولِ آرشیو ماند ({main.ArchiveCount} جدول)", main.ArchiveCount == 1);
        Check($"جدولِ زنده خالی ماند ({main.Rows.Count} ردیف)", main.Rows.Count == 0);
    }

    // ══════════════════════════════════════════════════════════════════════

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
        }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) Pump(w);
        Pump(w);
    }
}
