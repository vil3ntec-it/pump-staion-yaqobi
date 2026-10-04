using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using QuestPDF.Fluent;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ قفلِ «فقط‌خواندنی» روی پنجرهٔ واقعی (۱۴۰۵/۰۷/۲۰) ════════════════════════
///
/// «کسی که هیچ اشتراکی نگرفته همه چی براش خوندنی باشه… داخلش بشه رفت و
/// پی‌دی‌اف گرفت… فقط دیدنی باشه و هیچ کاری نتونه بکنه.»
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- readonly
///
/// چهار حال: بی‌حساب · آزمایشیِ تمام‌شده · پولیِ تمام‌شده در هفتهٔ هشدار ·
/// پولیِ تمام‌شده پس از هفته — و برگشت به «باز» با مجوزِ زنده.
/// </summary>
internal static class ReadOnlyProbe
{
    private static int _bad;
    private const long Day = 86_400_000L;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-readonly-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);

        //  دادهٔ نمونه پیش از قفل — کاربرِ واقعی هم دفترش را از پیش دارد
        SoftLock.Disabled = true;
        Seed.Fill(AppHost.Current);
        SoftLock.Disabled = false;
        SoftLock.Invalidate();
        Settle(win);

        Console.WriteLine("── ۱) نصبِ بی‌حساب ⇒ فقط‌خواندنی ──");
        var st = SoftLock.Status();
        Check("حال: بی‌حساب و فقط‌خواندنی", st.ReadOnly && st.Kind == LockKind.NoAccount, st.Kind.ToString());
        Check("نوارِ بالا می‌گوید چرا و چه باز است", SoftLock.Banner().Contains("حساب بسازید") && SoftLock.Banner().Contains("PDF"),
              SoftLock.Banner());

        var exp = vm.Sections.OfType<ExpenseSectionViewModel>().First();
        Wait(win, vm.GoAsync(exp));
        Settle(win);
        var rows = exp.Rows.Count;
        Check($"ردیف‌ها دیده می‌شوند ({rows})", rows > 0);
        Check("بخش باز شد و قفلِ بخش نیست", vm.Current == exp);

        var grid = win.GetVisualDescendants().OfType<ExcelGrid>().FirstOrDefault(g => g.IsEffectivelyVisible);
        Check("جدولِ بخش پیدا شد", grid is not null);
        if (grid is not null && rows > 0)
        {
            grid.SelectedIndex = 0;
            grid.CurrentColumn = grid.Columns.First(c => !c.IsReadOnly);
            Pump(win);
            var began = grid.BeginEdit();
            Pump(win);
            Check("ویرایشگرِ خانه باز نمی‌شود", !began);
            Check("و توست می‌گوید چرا", AppHost.Current.Toasts.Text == AppLock.Denied, AppHost.Current.Toasts.Text);
        }

        AppHost.Current.Toasts.Text = "";
        exp.AddRowCommand.Execute(null);
        Settle(win);
        Check($"«➕ ردیف» چیزی نمی‌سازد ({rows}⇐{exp.Rows.Count})", exp.Rows.Count == rows);
        Check("و پیامِ فارسیِ قفل نشان داده شد — نه «EditData»", AppHost.Current.Toasts.Text == AppLock.Denied,
              AppHost.Current.Toasts.Text);

        //  ⛔ دیدن، PDF و بکاپ باز
        var pdf = Path.Combine(Path.GetDirectoryName(tmpDb)!, "ro.pdf");
        try
        {
            PumpYaqobi.Reporting.Pdf.PdfEngine.Initialize();
            new PumpYaqobi.Reporting.Pdf.ExpenseReport(new PumpYaqobi.Reporting.Pdf.ExpenseReportInput(
                "آزمون", exp.Rows.Select(r => r.Entity).ToList(),
                PumpYaqobi.Application.Localization.Shamsi.Today(), "")).GeneratePdf(pdf);
        }
        catch (Exception e) { Console.WriteLine("     " + e.Message); }
        Check("PDF ساخته می‌شود", File.Exists(pdf) && new FileInfo(pdf).Length > 1000);
        var perm = AppHost.Current.Permissions;
        Check("بکاپ و دیدنِ مفاد باز است",
              perm.Can(PumpYaqobi.Application.Security.Permission.Backup)
              && perm.Can(PumpYaqobi.Application.Security.Permission.ViewProfit));

        //  هیچ بخشی نمی‌افتد
        foreach (var sec in vm.NavSections.ToList())
        {
            try { Wait(win, vm.GoAsync(sec)); } catch (Exception e) { Check("رفتن به " + sec.Title, false, e.Message); }
        }
        var log = Path.Combine(AppSettings.Dir, "crash.log");
        var crash = File.Exists(log) ? File.ReadAllText(log) : "";
        Check("هیچ کرشی در crash.log نیست (UI/AppDomain/Task)",
              !crash.Contains("· UI ") && !crash.Contains("· AppDomain") && !crash.Contains("· Task "),
              crash.Length > 0 ? crash[..Math.Min(400, crash.Length)] : null);

        Console.WriteLine("── ۲) آزمایشیِ تمام‌شده ⇒ همان لحظه فقط‌خواندنی، بی هفتهٔ هشدار ──");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        FakeLicense.Grant(planTitle: "آزمایشی", plan: "trial", subEnds: now - 60_000, exp: now - 60_000);
        SoftLock.Invalidate();
        st = SoftLock.Status();
        Check("آزمایشیِ تمام‌شده قفل است", st.ReadOnly && st.Kind == LockKind.TrialEnded, st.Kind.ToString());

        Console.WriteLine("── ۳) پولیِ تمام‌شده ⇒ یک هفته هشدار و هنوز می‌نویسد ──");
        FakeLicense.Grant(subEnds: now - 2 * Day, exp: now - 2 * Day);
        SoftLock.Invalidate();
        st = SoftLock.Status();
        Check("در هفتهٔ هشدار می‌نویسد", !st.ReadOnly && st.Kind == LockKind.PaidWarning, $"{st.Kind} · {st.DaysLeft} روز");
        Check("و نوار روزهای مانده را می‌گوید", SoftLock.Banner().Contains("5 روز دیگر"), SoftLock.Banner());
        Wait(win, vm.GoAsync(exp));
        var before = exp.Rows.Count;
        exp.AddRowCommand.Execute(null);
        Settle(win);
        Check($"ردیف ساخته شد ({before}⇐{exp.Rows.Count})", exp.Rows.Count == before + 1);

        Console.WriteLine("── ۴) پولی، هفته هم گذشت ⇒ فقط‌خواندنی ──");
        FakeLicense.Grant(subEnds: now - 8 * Day, exp: now - 8 * Day);
        SoftLock.Invalidate();
        st = SoftLock.Status();
        Check("پس از هفته قفل است", st.ReadOnly && st.Kind == LockKind.PaidEnded, st.Kind.ToString());

        Console.WriteLine("── ۵) تمدید ⇒ همان لحظه باز ──");
        FakeLicense.Grant();
        SoftLock.Invalidate();
        st = SoftLock.Status();
        Check("مجوزِ زنده ⇒ باز", !st.ReadOnly && st.Kind == LockKind.None, st.Kind.ToString());
        before = exp.Rows.Count;
        exp.AddRowCommand.Execute(null);
        Settle(win);
        Check($"و دوباره می‌نویسد ({before}⇐{exp.Rows.Count})", exp.Rows.Count == before + 1);

        Console.WriteLine(_bad == 0 ? "\n✅ قفلِ فقط‌خواندنی همان‌طور که خواسته شده کار می‌کند" : $"\n✖ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
