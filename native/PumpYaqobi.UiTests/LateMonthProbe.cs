using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «حساب‌های دیر رسیده» · «هیچ جدولی کاربر را به چپ و راست نبرد» (۱۴۰۵/۰۷/۱۶) ══
///
/// گزارشِ صاحب ریپو: «داخلِ پارچه‌ها تاریخ‌های گذشته رو زدم… مصارف، گاوصندوق و
/// بخش‌های دیگه اون ماه رو توی لیستِ ماه‌هاشون نمیاره… و ستون‌ها و سربرگ‌ها چپ
/// یا راست می‌روند… و بعضی کادرها خیلی بزرگ می‌شوند و مجبوری چپ و راست اسکرول کنی.»
///
/// با پنجرهٔ واقعی و همان راهِ کاربر:
///   ۱) بخش باز است ⇒ کاربر می‌رود ⇒ ردیفِ ماهِ گذشته از بیرون (مثلِ پارچه ⇒ ورق)
///      می‌آید ⇒ برمی‌گردد: ماه در کشویی هست و ماهِ جلوی چشم عوض نشده
///   ۲) رفتن به همان ماه، «همهٔ ماه‌ها» و برگشت: سرستون روی ستونِ خودش
///   ۳) هر بخشِ نوار، در دو اندازهٔ پنجره: هیچ جدولی از قابش بیرون نمی‌زند
///   ۴) ستونی که کاربر خیلی پهن کشید: جدول باز هم داخلِ قاب
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- latemonths
/// </summary>
internal static class LateMonthProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private const string Old = "1404/03";

    public static int Run(string[] args)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-late-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Seed.Fill(AppHost.Current);
        Settle(win);

        Console.WriteLine("════ ۱ و ۲) ماهِ دیر رسیده در کشویی ════");
        foreach (var id in new[] { "expenses", "safe", "sarrafi" }) LateMonth(win, vm, id);

        Console.WriteLine();
        Console.WriteLine("════ ۴) ستونی که کاربر خیلی پهن کشید ════");
        WideDrag(win, vm);

        foreach (var (w, h) in new[] { (1440, 900), (1100, 760) })
        {
            win.Width = w; win.Height = h;
            Settle(win);
            Console.WriteLine();
            Console.WriteLine($"════ ۳) هر بخشِ نوار، پنجرهٔ {w}×{h} ════");
            Sweep(win, vm);
        }

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ همه سرِ جایش بود" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static object? Month(SectionViewModel s) => ((dynamic)s).Month;

    private static void LateMonth(MainWindow win, MainViewModel vm, string id)
    {
        var s = vm.Sections.First(x => x.Id == id);
        var other = vm.Sections.First(x => x.Id == "dashboard");
        Wait(win, vm.GoAsync(s));
        var shown = (string)Month(s)!;
        Wait(win, vm.GoAsync(other));

        //  همان کاری که پارچه ⇒ ورق ⇒ این دفتر می‌کند: ردیفی با تاریخِ ماهِ گذشته
        var h = AppHost.Current;
        var d = Old + "/12";
        switch (id)
        {
            case "expenses": h.ExpenseLedger.AddAsync(new Expense { DateShamsi = d, Title = "مصرفِ ورقِ دیر", Amount = 700m }).GetAwaiter().GetResult(); break;
            case "safe": h.SafeLedger.AddAsync(new SafeEntry { DateShamsi = d, Title = "فروشِ ورقِ دیر", Amount = 9000m }).GetAwaiter().GetResult(); break;
            case "sarrafi": h.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = d, Description = "دیر", Amount = 100m }).GetAwaiter().GetResult(); break;
        }
        Wait(win, vm.GoAsync(s));

        var picker = (YearMonthPicker)((dynamic)s).Picker;
        var months = (System.Collections.ObjectModel.ObservableCollection<string>)((dynamic)s).Months;
        Check($"{s.Title}: ماهِ {Old} پس از بازگشت در فهرست است", months.Contains(Old));
        Check($"{s.Title}: ماهِ جلوی چشم عوض نشد", (string)Month(s)! == shown, $"{shown} ⇒ {Month(s)}");
        var y = picker.Years.FirstOrDefault(x => x.Key == "1404");
        Check($"{s.Title}: سالِ ۱۴۰۴ در کشوی سال هست", y is not null);
        if (y is not null)
        {
            picker.Year = y;
            Settle(win);
            var item = picker.Months.FirstOrDefault(m => m.Key == Old);
            Check($"{s.Title}: ماهِ {Old} در کشوی ماهِ همان سال هست", item is not null);
            if (item is not null) { picker.Selected = item; Settle(win); }
            Check($"{s.Title}: با انتخابش همان ماه باز شد", (string)Month(s)! == Old, (string)Month(s)!);
            Aligned(win, $"{s.Title} ({Old})");
        }
        //  «همهٔ ماه‌ها» و برگشت
        ((dynamic)s).Month = "";
        Settle(win);
        Aligned(win, $"{s.Title} (همهٔ ماه‌ها)");
        ((dynamic)s).Month = shown;
        Settle(win);
        Aligned(win, $"{s.Title} (برگشت به {shown})");
        Console.WriteLine();
    }

    private static void WideDrag(MainWindow win, MainViewModel vm)
    {
        var s = vm.Sections.First(x => x.Id == "expenses");
        Wait(win, vm.GoAsync(s));
        var g = Grids(win).FirstOrDefault();
        if (g is null) { Check("جدولِ مصارف پیدا شد", false); return; }
        var col = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).Skip(1).First();
        col.Width = new DataGridLength(g.Bounds.Width * 2, DataGridLengthUnitType.Pixel);
        Settle(win);
        Inside(g, "مصارف پس از کشیدنِ یک ستون تا دو برابرِ جدول");
    }

    private static void Sweep(MainWindow win, MainViewModel vm)
    {
        foreach (var s in vm.NavSections.ToList())
        {
            if (s.Id is "chat" or "account" or "cameras") continue;
            Wait(win, vm.GoAsync(s));
            var grids = Grids(win).ToList();
            if (grids.Count == 0) continue;
            var i = 0;
            foreach (var g in grids) Inside(g, $"{s.Title} · جدولِ {++i}");
        }

        //  پهن‌ترین جدول‌ها داخلِ حساب‌اند
        if (vm.Sections.FirstOrDefault(x => x.Id == "debt") is PumpYaqobi.App.ViewModels.Sections.DebtSectionViewModel debt)
        {
            Wait(win, vm.GoAsync(debt));
            Wait(win, debt.OpenByNumberAsync(1));
            var i = 0;
            foreach (var g in Grids(win).ToList()) Inside(g, $"حسابِ قرض‌دار · جدولِ {++i}");
            if (i == 0) Check("حسابِ قرض‌دار باز شد و جدول داشت", false);
            debt.PersonOpen = false; Settle(win);
        }
        if (vm.Sections.FirstOrDefault(x => x.Id == "noinv") is PumpYaqobi.App.ViewModels.Sections.CompanySectionViewModel co
            && co.Cards.FirstOrDefault() is { } first)
        {
            Wait(win, vm.GoAsync(co));
            co.OpenCommand.Execute(first); Settle(win);
            var i = 0;
            foreach (var g in Grids(win).ToList()) Inside(g, $"حسابِ شرکت · جدولِ {++i}");
            co.BackCommand.Execute(null); Settle(win);
        }
    }

    private static IEnumerable<DataGrid> Grids(Window win) =>
        win.GetVisualDescendants().OfType<DataGrid>()
           .Where(g => g.IsEffectivelyVisible && g.Bounds.Width > 50 && g.Columns.Any(c => c.IsVisible));

    /// <summary>ستون‌ها داخلِ قابِ جدول‌اند و هیچ نوارِ اسکرولِ افقی‌ای کاربر را به چپ و راست نمی‌برد.</summary>
    private static void Inside(DataGrid g, string what)
    {
        var cols = g.Columns.Where(c => c.IsVisible).ToList();
        var sum = cols.Sum(c => c.ActualWidth);
        var rh = g.GetVisualDescendants().OfType<DataGridRowHeader>().FirstOrDefault(h => h.IsEffectivelyVisible)?.Bounds.Width ?? 0;
        var bar = g.GetVisualDescendants().OfType<ScrollBar>()
                   .FirstOrDefault(b => b.Orientation == Orientation.Horizontal && b.IsEffectivelyVisible && b.Maximum > 0.5);
        var room = g.Bounds.Width;
        Check($"{what}: داخلِ قاب", bar is null && sum + rh <= room + 1.5,
              $"ستون‌ها {sum + rh:0}px از {room:0}px" + (bar is null ? "" : $" · اسکرولِ افقی {bar.Maximum:0}px"));
    }

    private static void Aligned(Window win, string what)
    {
        var g = Grids(win).FirstOrDefault();
        if (g is null) { Check(what + ": جدول", false); return; }
        var heads = g.GetVisualDescendants().OfType<DataGridColumnHeader>()
                     .Where(h => h.IsEffectivelyVisible && h.Content is string s && s.Length > 0).ToList();
        var row = g.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.IsEffectivelyVisible);
        if (row is null) { Inside(g, what); return; }   // ماهِ خالی: فقط قاب
        var cells = row.GetVisualDescendants().OfType<DataGridCell>().Where(c => c.IsEffectivelyVisible).ToList();
        var worst = 0.0; var sum = 0.0;
        foreach (var h in heads)
        {
            var hx = h.TranslatePoint(default, g)!.Value.X;
            sum += h.Bounds.Width;
            var cell = cells.MinBy(c => Math.Abs(c.TranslatePoint(default, g)!.Value.X - hx));
            if (cell is null) continue;
            var cx = cell.TranslatePoint(default, g)!.Value.X;
            worst = Math.Max(worst, Math.Max(Math.Abs(cx - hx), Math.Abs(cell.Bounds.Width - h.Bounds.Width)));
        }
        var spread = sum / Math.Max(1, g.Bounds.Width);
        Check($"{what}: سرستون روی ستونِ خودش و پخش در کلِ پهنا", worst <= 2 && spread >= 0.8,
              $"جابه‌جایی {worst:0.#}px · پخش {spread:P0}");
        Inside(g, what);
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
