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
        Console.WriteLine("── ۳) سربرگ ⇄ جدول ⇄ جمله، از یک منبع ──");
        HeaderTableSync(win, person);

        Console.WriteLine();
        Console.WriteLine("── ۴) «📋 جدول جدید» ──");
        NewTable(win, person);

        Console.WriteLine();
        Console.WriteLine("── ۵) پس از بستن و باز کردنِ دوباره ──");
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
    //  ۳) سربرگ ⇄ جدول ⇄ جمله — یک منبعِ داده
    // ══════════════════════════════════════════════════════════════════════
    //
    //  دقیقاً همان سناریوهایی که صاحب ریپو نوشت (TEST 1..8 و 24..26):
    //  رسید از سربرگ، رسیدِ دوم، حذف از جدول، افزودن از جدول، ویرایش در
    //  جدول، ردیفِ خالی. بعد از هر گام، هر سه عدد — سربرگ، جدول، جمله —
    //  باید یکی باشند، بی هیچ بازخوانیِ صفحه.

    private static void HeaderTableSync(Window win, PersonViewModel person)
    {
        person.Current = person.Accounts[0];
        Pump(win);
        var a = person.Current!;

        // از حسابِ خالی شروع می‌کنیم تا عددها بی‌ابهام باشند (TEST 1)
        foreach (var r in a.Rows.ToList())
        {
            a.DeleteRowCommand.Execute(r);
            Pump(win);
        }
        for (var i = 0; i < 40 && a.Rows.Count > 0; i++) Pump(win);
        Check($"۱) حسابِ خالی ({a.Rows.Count} ردیف · سربرگ {Head(a)})",
              a.Rows.Count == 0 && Head(a) == 0m);

        // TEST 2 — رسیدِ اول از سربرگ
        a.HeadPetrolRasidEdit = "10000";
        Settle(win, a, 1);
        Show("۲) رسیدِ ۱۰٬۰۰۰ از سربرگ", a, 10000m, 1);

        // TEST 3 — رسیدِ دوم از سربرگ؛ اولی نباید جایش را بدهد
        a.HeadPetrolRasidEdit = "30000";
        Settle(win, a, 2);
        Show("۳) رسیدِ ۳۰٬۰۰۰ از سربرگ", a, 40000m, 2);

        // TEST 4 — حذفِ ۱۰٬۰۰۰ از خودِ جدول
        var ten = a.Rows.FirstOrDefault(r => r.RasidFuel == 10000m);
        if (ten is null) { Check("۴) ردیفِ ۱۰٬۰۰۰ در جدول پیدا شد", false); return; }
        a.DeleteRowCommand.Execute(ten);
        Settle(win, a, 1);
        Show("۴) حذفِ ۱۰٬۰۰۰ از جدول", a, 30000m, 1);

        // TEST 5 — رسیدِ تازه، این‌بار مستقیم از خودِ جدول
        a.AddRowCommand.Execute(null);
        Settle(win, a, 2);
        var fresh = a.Rows.Last();
        fresh.RasidFuelText = "20000";
        fresh.FlushAsync().GetAwaiter();
        for (var i = 0; i < 60 && Head(a) != 50000m; i++) Pump(win);
        Show("۵) رسیدِ ۲۰٬۰۰۰ مستقیم از جدول", a, 50000m, 2);

        // TEST 6 — ویرایشِ ۳۰٬۰۰۰ به ۵۰٬۰۰۰ در خودِ جدول
        var thirty = a.Rows.FirstOrDefault(r => r.RasidFuel == 30000m);
        if (thirty is null) { Check("۶) ردیفِ ۳۰٬۰۰۰ پیدا شد", false); return; }
        thirty.RasidFuelText = "50000";
        for (var i = 0; i < 60 && Head(a) != 70000m; i++) Pump(win);
        Show("۶) ویرایشِ ۳۰٬۰۰۰ ← ۵۰٬۰۰۰", a, 70000m, 2);

        // TEST 7 — ردیفِ خالی نباید جمع را خراب کند
        a.AddRowCommand.Execute(null);
        Settle(win, a, 3);
        Show("۷) ردیفِ خالی، جمع دست‌نخورده", a, 70000m, 3);

        // TEST 9/10/11 — دیزل جدا از پطرول
        var pBefore = Head(a);
        a.HeadDieselRasidEdit = "5000";
        for (var i = 0; i < 60 && HeadD(a) != 5000m; i++) Pump(win);
        Check($"۹) رسیدِ دیزل فقط دیزل را عوض کرد (پطرول {Head(a)} · دیزل {HeadD(a)})",
              Head(a) == pBefore && HeadD(a) == 5000m);

        var d = a.Rows.FirstOrDefault(r => r.RasidFuel == 5000m);
        if (d is not null)
        {
            a.DeleteRowCommand.Execute(d);
            for (var i = 0; i < 60 && HeadD(a) != 0m; i++) Pump(win);
            Check($"۱۱) حذفِ رسیدِ دیزل، پطرول را دست نزد (پطرول {Head(a)} · دیزل {HeadD(a)})",
                  Head(a) == pBefore && HeadD(a) == 0m);
        }
    }

    /// <summary>«مقدار رسید»ِ سربرگِ پطرول، همان‌طور که کاربر می‌بیند.</summary>
    private static decimal Head(AccountViewModel a) =>
        PumpYaqobi.Application.Localization.Shamsi.Num(a.HeadPetrolRasidText);

    private static decimal HeadD(AccountViewModel a) =>
        PumpYaqobi.Application.Localization.Shamsi.Num(a.HeadDieselRasidText);

    /// <summary>جمعِ ستونِ «رسید تیل» در ردیفِ «جمله»ی ته جدول.</summary>
    private static decimal Foot(AccountViewModel a) =>
        PumpYaqobi.Application.Localization.Shamsi.Num(a.SumRasidFuelText);

    /// <summary>جمعِ همان ستون، از خودِ ردیف‌های جدول.</summary>
    private static decimal Table(AccountViewModel a) =>
        a.Rows.Where(r => r.Fuel == PumpYaqobi.Domain.Enums.FuelType.Petrol).Sum(r => r.RasidFuel);

    private static void Settle(Window win, AccountViewModel a, int wantRows)
    {
        for (var i = 0; i < 80 && a.Rows.Count != wantRows; i++) Pump(win);
        Pump(win);
    }

    /// <summary>هر سه نما باید یک عدد بدهند — وگرنه همان‌جا گزارش می‌شود.</summary>
    private static void Show(string what, AccountViewModel a, decimal want, int rows)
    {
        var h = Head(a); var t = Table(a); var f = Foot(a);
        Check($"{what} — سربرگ {h} · جدول {t} · جمله {f} · {a.Rows.Count} ردیف",
              h == want && t == want && f == want && a.Rows.Count == rows);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ۴) جدولِ جدید
    // ══════════════════════════════════════════════════════════════════════

    private static void NewTable(Window win, PersonViewModel person)
    {
        // روی حسابِ اصلی، که ردیف دارد
        person.Current = person.Accounts[0];
        Pump(win);

        var acct = person.Current!;
        if (acct.RowCount == 0)
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
    //  ۵) ماندگاری
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
