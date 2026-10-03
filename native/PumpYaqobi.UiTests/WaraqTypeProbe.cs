using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «توی ورق نوشتن خیلی کند است… نوشته نمی‌شه، یک دفعه نوشته می‌شه، یا خودبه‌خود
/// پاک می‌شه یا نصفه» (۱۴۰۵/۰۷/۱۹) ══════════════════════════════════════════════
///
/// حرف‌به‌حرف، با فاصلهٔ واقعیِ تایپ (نه یک ‎KeyTextInput‎ِ یک‌جا)، روی دادهٔ
/// پنج‌ساله. بینِ دو حرف همان چیزی می‌دود که در برنامهٔ واقعی می‌دود: مکثِ ذخیره،
/// ثبت به حساب‌ها، تکمله. سه چیز سنجیده می‌شود:
///   ۱) بلندترین مکثِ نخِ رابط بینِ دو حرف (کندی)
///   ۲) پس از هر حرف، کادر دقیقاً همان چیزی را دارد که تا این‌جا زده شده (گم/پاک/نصفه)
///   ۳) پس از Enter، ردیف و دیسک همان نوشته را دارند
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- waraqtype
/// </summary>
internal static class WaraqTypeProbe
{
    private static readonly List<string> Bad = new();
    private const int GapMs = 90;           // تایپِ تند ولی معمولی
    //  پیش از اصلاح ۶۰۰ تا ۹۵۰ (ثبت به حساب‌ها روی نخِ رابط، با هر مکث)؛ پس از آن ~۲۰ تا ۶۰.
    //  ۱۲۰ مرزِ «حس می‌شود» است، با جا برای نوسانِ ماشینِ CI.
    private const int StallGoal = 120;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-wqt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");
        if (Environment.GetEnvironmentVariable("WQT_SMALL") != "1") YearsAudit.Seed(file);
        AppHost.Start(file);
        var h = AppHost.Current;
        if (h.Auth.NeedsFirstRun()) h.Auth.CreateFirstAdmin("1234");
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

        //  کارِ یک‌باره‌ی «فروش ورق منهای قرض» (پس از ورود، روی نخِ دیگر) — اول تمام شود
        var sw0 = Stopwatch.StartNew();
        if (h.ShiftWaraqSync.FixOldSalesTask is { } fix) Wait(win, fix);
        Console.WriteLine($"کارِ یک‌باره‌ی فروشِ ورق: {sw0.ElapsedMilliseconds}ms ({h.ShiftWaraqSync.FixOldSalesTask?.Result})");
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(wq));
        Wait(win, wq.ReloadAsync());
        if (wq.Sheets.Count == 0) { Wait(win, wq.NewSheetCommand.ExecuteAsync(null)); Wait(win, wq.ReloadAsync()); }
        Wait(win, wq.OpenCommand.ExecuteAsync(wq.Sheets.First()));
        var page = wq.Page!;
        if (page.IsNight) { page.IsNight = false; Settle(win); }
        Wait(win, page.AddTxnCommand.ExecuteAsync(null));
        Settle(win);

        var grid = win.GetVisualDescendants().OfType<ExcelGrid>()
            .Where(g => g.IsEffectivelyVisible
                        && (ReferenceEquals(g.ItemsSource, page.TxnsFirst) || ReferenceEquals(g.ItemsSource, page.TxnsSecond)))
            .FirstOrDefault(g => ((System.Collections.IList)g.ItemsSource!).Contains(page.Txns[^1]));
        if (grid is null) { Console.WriteLine("❌ جدولِ ردیفِ تازه پیدا نشد"); return 1; }
        var row = page.Txns[^1];
        var ri = ((System.Collections.IList)grid.ItemsSource!).IndexOf(row);
        var cols = grid.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        int Col(string head) => cols.FindIndex(c => (c.Header as string) == head);

        Console.WriteLine($"ورقِ {page.Entity.DateShamsi} — {page.Txns.Count} ردیف");

        TypeInto(win, grid, ri, Col("نام"), "کریم احمدی بابت نان و چای", () => row.Name);
        TypeInto(win, grid, ri, Col("مقدار تیل"), "12500", () => row.LitersText.Replace(",", "").Replace("٬", ""));

        Wait(win, SaveGuard.FlushAllAsync());
        for (var i = 0; i < 40; i++) { Pump(win); Thread.Sleep(20); }
        using (var db = h.Db.Create())
        {
            var t = db.Set<PumpYaqobi.Domain.Entities.WaraqTransaction>().AsNoTracking().Single(x => x.Id == row.Entity.Id);
            Check($"روی دیسک همان نوشته («{t.Name}» · {t.Liters})",
                  t.Name == "کریم احمدی بابت نان و چای" && t.Liters == 12500m);
        }

        Console.WriteLine();
        if (Bad.Count == 0) { Console.WriteLine("✅ نوشتن در ورق روان است و هیچ حرفی گم یا پاک نشد"); return 0; }
        Console.WriteLine($"❌ {Bad.Count} ایراد:");
        foreach (var b in Bad) Console.WriteLine("   • " + b);
        return 1;
    }

    private static void TypeInto(Window win, DataGrid g, int row, int col, string text, Func<string> model)
    {
        Console.WriteLine();
        Console.WriteLine($"── نوشتنِ «{text}» حرف‌به‌حرف (هر {GapMs}ms) ──");
        ClickCell(win, g, row, col);
        var worst = 0L;
        var lost = new List<string>();
        var dbBefore = DbWatch.Count;
        DbWatch.Recording = Environment.GetEnvironmentVariable("WQT_SQL") == "1";
        while (DbWatch.Log.TryDequeue(out _)) { }
        for (var i = 0; i < text.Length; i++)
        {
            win.KeyTextInput(text[i].ToString());
            //  بینِ دو کلید: هر کاری که برنامه در این فاصله می‌کند
            var until = Stopwatch.StartNew();
            //  مکثِ میانِ دو واژه — همان‌جا ذخیره و ثبت به حساب‌ها می‌دود
            var gap = text[i] == ' ' ? 450 : GapMs;
            while (until.ElapsedMilliseconds < gap)
            {
                var sw = Stopwatch.StartNew();
                Dispatcher.UIThread.RunJobs();
                win.UpdateLayout();
                sw.Stop();
                worst = Math.Max(worst, sw.ElapsedMilliseconds);
                Thread.Sleep(3);
            }
            var box = win.FocusManager?.GetFocusedElement() as TextBox;
            var shown = Typed(box);
            var want = text[..(i + 1)];
            if (shown != want) lost.Add($"پس از «{want}» کادر «{shown}»");
        }
        var db = DbWatch.Count - dbBefore;
        if (DbWatch.Recording)
            foreach (var grp in DbWatch.Log.GroupBy(x => x[..Math.Min(90, x.Length)]).OrderByDescending(x => x.Count()).Take(12))
                Console.WriteLine($"      {grp.Count(),5} × {grp.Key.Replace('\n', ' ')}");
        DbWatch.Recording = false;
        Check($"بلندترین مکثِ نخِ رابط بینِ دو حرف {worst}ms (هدف ≤ {StallGoal}) · {db} دستورِ دیتابیس در حینِ نوشتن",
              worst <= StallGoal);
        Check(lost.Count == 0 ? "هیچ حرفی گم، پاک یا نصفه نشد" : $"{lost.Count} بار کادر چیزِ دیگری نشان داد: " + string.Join(" · ", lost.Take(4)),
              lost.Count == 0);
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        Check($"پس از Enter ردیف همان را دارد («{model()}»)", model() == text);
    }

    /// <summary>متنِ کادر بی تکملهٔ پیشنهادی (تکهٔ انتخاب‌شده).</summary>
    private static string Typed(TextBox? box)
    {
        if (box?.Text is not { } t) return "";
        var a = Math.Min(box.SelectionStart, box.SelectionEnd);
        var b = Math.Max(box.SelectionStart, box.SelectionEnd);
        return b > a && b == t.Length ? t[..a] : t;
    }

    private static DataGridCell? Cell(DataGrid g, int row, int col)
    {
        var cols = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (col < 0 || col >= cols.Count) return null;
        var item = g.ItemsSource?.Cast<object>().ElementAtOrDefault(row);
        return g.GetVisualDescendants().OfType<DataGridRow>()
                .FirstOrDefault(r => ReferenceEquals(r.DataContext, item))?
                .GetVisualDescendants().OfType<DataGridCell>()
                .FirstOrDefault(c => ReferenceEquals(c.GetType().GetProperty("OwningColumn",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public)?.GetValue(c), cols[col]));
    }

    private static void ClickCell(Window win, DataGrid g, int row, int col)
    {
        g.ScrollIntoView(g.ItemsSource?.Cast<object>().ElementAtOrDefault(row), null);
        Settle(win);
        var cell = Cell(g, row, col);
        if (cell is null) { Check($"خانهٔ {row}/{col} پیدا شد", false); return; }
        var p = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), win);
        if (p is null) return;
        win.MouseDown(p.Value, MouseButton.Left);
        win.MouseUp(p.Value, MouseButton.Left);
        Settle(win);
    }

    private static void Tap(Window win, PhysicalKey key)
    {
        win.KeyPressQwerty(key, RawInputModifiers.None);
        win.KeyReleaseQwerty(key, RawInputModifiers.None);
    }

    private static void Check(string what, bool ok)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}");
        if (!ok) Bad.Add(what);
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 30; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 2000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
