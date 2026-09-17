using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «با زیاد شدنِ جدول، برنامه کند می‌شود» ═════════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «برنامه با اضافه شدنِ جدول یا زیاد شدنش خیلی
/// کند می‌شود… حساب دیر باز می‌شود و اسکرول نرم نیست، گیر گیر دارد… این را
/// آن‌قدر سریع کن که زیرِ ۱۰۰ یا نهایت ۲۰۰ میلی‌ثانیه باشد… برنامه بی‌نهایت هم
/// باشد نباید افتی داشته باشد، مثلِ برنامه‌های حرفه‌ای.»
///
/// این سنجش همان ادعا را **با عدد** می‌سنجد: یک دفتر، با شش اندازهٔ متفاوت،
/// و برای هر اندازه چهار چیز:
///
///   • باز شدن   — اولین فریمِ قابلِ کار پس از عوض شدنِ ماه
///   • رشد       — بقیهٔ کارِ پس‌زمینه تا جدول آرام بگیرد
///   • ردیفِ زنده — چند ‎DataGridRow‎ واقعاً ساخته شده
///   • گامِ چرخ  — بدترین مکثِ یک گامِ اسکرول روی همان جدول
///
/// ⚠️ **سنجهٔ اصلی «ردیفِ زنده» است، نه میلی‌ثانیه.** وقت روی ماشینِ CI نوسان
/// دارد؛ ولی «چند ردیف ساخته شد» حقیقتِ ساختاری است: تا وقتی شمارِ ردیفِ زنده
/// با شمارِ ردیف‌های داده بالا برود، هزینه هم بالا می‌رود و هیچ ترفندی نجاتش
/// نمی‌دهد. هدف: ردیفِ زنده با هر اندازه‌ای **ثابت** بماند.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- bigtable
/// </summary>
internal static class BigTable
{
    /// <summary>از کوچک تا آن‌قدر بزرگ که هیچ پمپی در ده سال به آن نمی‌رسد.</summary>
    private static readonly int[] Sizes = { 50, 200, 600, 1_000, 5_000, 20_000 };

    /// <summary>هدفِ صاحب ریپو: ۱۰۰، و سقفِ شکست ۲۰۰.</summary>
    private const long OpenGoal = 200;

    /// <summary>گامِ چرخ روی جدولِ ساخته‌شده — بالاتر از این یعنی «گیر گیر».</summary>
    private const long StepGoal = 120;

    /// <summary>
    /// ردیفِ زندهٔ بیشینه. یک صفحهٔ ۹۰۰ پیکسلی حدودِ بیست ردیف است؛ با حاشیهٔ
    /// دو برابری، هر عددی بالای این یعنی جدول واقعاً مجازی‌سازی نمی‌کند.
    /// </summary>
    private const int LiveGoal = 80;

    private const double Step = 58 * 3;
    private const string EmptyMonth = "1300/01";

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-bigtable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");

        var months = MonthsBack(Sizes.Length);
        Seed(file, months);

        AppHost.Start(file);

        //  سنجه با نصبِ **پلن‌دار** می‌دود — وگرنه داشبورد و مفاد/ضرر و
        //  تاریخچه‌ها قفل‌اند و باز نمی‌شوند. شرحش در `FakeLicense`؛ خودِ
        //  قفل در بندِ ۱۷ی `verify` و در `EntitlementsTests` سنجیده می‌شود.
        FakeLicense.Grant();

        var host = AppHost.Current;
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        var vm = (MainViewModel)win.DataContext!;
        win.Show(); Pump(win);
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        LockIn.Wait(vm.Lock);
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        Settle(win);

        var sec = vm.Sections.First(s => s.Id == "safe");
        var rowHost = (IRowBatchHost)sec;
        Wait(win, vm.GoAsync(sec)); Settle(win);

        // ══ حالتِ پروفایل: فقط دفترِ ۶۰۰ ردیفی، چند بار — برای ‎dotnet-trace‎ ══
        if (Trace)
        {
            var big = Array.IndexOf(Sizes, 600);
            for (var i = 0; i < 6; i++)
            {
                SetMonth(sec, EmptyMonth); Settle(win, rowHost, 0); Grow(win);
                SetMonth(sec, months[big]); Settle(win, rowHost, Sizes[big]);
                var ms = Grow(win);
                Console.WriteLine($"دورِ {i + 1}: رشدِ ۶۰۰ ردیف {ms:N0} ms");
            }
            return 0;
        }

        // ══ حالتِ عکس: همان دفترِ ۶۰۰ ردیفی، در چند جای اسکرول ══════════════
        if (Shots is not null)
        {
            Directory.CreateDirectory(Shots);
            var big = Array.IndexOf(Sizes, 600);
            SetMonth(sec, months[big]); Settle(win, rowHost, Sizes[big]); Settle(win);
            var page = win.GetVisualDescendants().OfType<ScrollViewer>().First(v => v.Name == "PageScroll");
            foreach (var frac in new[] { 0d, 0.02, 0.25, 0.5, 1d })
            {
                var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);
                page.Offset = new Vector(0, max * frac);
                Settle(win);
                using var f = win.CaptureRenderedFrame();
                var name = Path.Combine(Shots, $"sticky-{frac * 100:000}.png");
                f?.Save(name);
                Console.WriteLine($"{name}  (آفست {page.Offset.Y:N0} از {max:N0})");
            }
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("ردیفِ داده   SQL     باز شدن    رشد      ردیفِ زنده  بلندیِ جدول   گامِ چرخ: میانه  بیشینه   حافظه");
        Console.WriteLine(new string('-', 108));

        var bad = new List<string>();

        for (var k = 0; k < Sizes.Length; k++)
        {
            SetMonth(sec, EmptyMonth); Settle(win, rowHost, 0); Grow(win);

            var sql = Time(() => host.SafeLedger.ListAsync(months[k]).GetAwaiter().GetResult());

            SetMonth(sec, months[k]);
            var open = Time(() => Settle(win, rowHost, Sizes[k]));
            var grow = Grow(win);

            var grid = Grid(win);
            var live = grid?.GetVisualDescendants().OfType<DataGridRow>().Count() ?? 0;
            var (med, max) = WheelSteps(win);
            var mem = GC.GetTotalMemory(false) / (1024 * 1024);

            Console.WriteLine($"{Sizes[k],9:N0} {sql,6:N0} ms {open,8:N0} ms {grow,7:N0} ms {live,10:N0} {grid?.Bounds.Height ?? 0,10:N0}px "
                            + $"{med,14:N0} ms {max,6:N0} ms {mem,6:N0} MB");

            foreach (var why in Correctness(win, Sizes[k])) bad.Add($"{Sizes[k]:N0} ردیف: {why}");

            if (open > OpenGoal) bad.Add($"{Sizes[k]:N0} ردیف: باز شدن {open:N0} ms (سقف {OpenGoal})");
            if (max > StepGoal) bad.Add($"{Sizes[k]:N0} ردیف: بدترین گامِ چرخ {max:N0} ms (سقف {StepGoal})");
            if (live > LiveGoal) bad.Add($"{Sizes[k]:N0} ردیف: {live:N0} ردیفِ زنده — مجازی‌سازی نمی‌کند (سقف {LiveGoal})");
        }

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine($"✅ شمارِ ردیف‌ها روی باز شدن، اسکرول و حافظه اثری ندارد");
            return 0;
        }
        Console.WriteLine("❌ هنوز با بزرگ شدنِ جدول کند می‌شود:");
        foreach (var b in bad) Console.WriteLine("   • " + b);
        return 1;
    }

    /// <summary>‎bigtable trace‎: فقط رشدِ یک دفترِ ۶۰۰ ردیفی، چند بار.</summary>
    internal static bool Trace;

    /// <summary>‎bigtable shot &lt;پوشه&gt;‎: عکسِ همان جدول در چند جای اسکرول.</summary>
    internal static string? Shots;

    /// <summary>
    /// ══ درستیِ پنجرهٔ چسبان — نه فقط سرعتش ═════════════════════════════════
    ///
    /// جدولِ سریعی که ردیفِ اشتباه نشان بدهد از جدولِ کند بدتر است. اولین
    /// پیاده‌سازیِ چسبان با عکس همین را لو داد: با پرشِ بزرگ ردیف‌ها **وارونه**
    /// چیده می‌شدند (۳۰۷، ۳۰۶، ۳۰۵…) و یک ردیفِ کهنه ته جدول می‌ماند. پس
    /// این‌جا در چند جای اسکرول — هم با پرش، هم گام‌به‌گام — سنجیده می‌شود که:
    ///
    ///   ۱) شماره‌های ردیف‌های دیده‌شده از بالا به پایین **یکی‌یکی بالا** بروند،
    ///   ۲) و با پایین رفتنِ صفحه، ردیفِ بالای جدول هم جلو برود.
    /// </summary>
    private static List<string> Correctness(Window win, int rows)
    {
        var bad = new List<string>();
        var page = win.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(v => v.Name == "PageScroll");
        var grid = Grid(win) as ExcelGrid;
        if (page is null || grid is null || !grid.DiagSticky) return bad;

        var seen = new List<int>();
        foreach (var frac in new[] { 0d, 0.1, 0.4, 0.75, 1d })
        {
            var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);
            page.Offset = new Vector(0, max * frac);
            Settle(win);
            var order = Shown(grid);
            if (order.Count == 0) { bad.Add($"در {frac * 100:N0}٪ هیچ ردیفی دیده نمی‌شود"); continue; }
            for (var i = 1; i < order.Count; i++)
                if (order[i] != order[i - 1] + 1)
                { bad.Add($"در {frac * 100:N0}٪ ترتیبِ ردیف‌ها به هم خورده: … {order[i - 1]} بعد {order[i]}"); break; }
            seen.Add(order[0]);
        }
        for (var i = 1; i < seen.Count; i++)
            if (seen[i] < seen[i - 1])
            { bad.Add($"با پایین رفتنِ صفحه ردیفِ بالای جدول عقب رفت ({seen[i - 1]} ⇒ {seen[i]})"); break; }
        if (seen.Count > 1 && seen[^1] <= seen[0])
            bad.Add("صفحه تا ته رفت ولی جدول نلغزید");

        page.Offset = new Vector(0, 0);
        Settle(win);
        return bad;
    }

    /// <summary>شمارهٔ ردیف‌های دیده‌شده، از بالا به پایین.</summary>
    private static List<int> Shown(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridRow>()
            .Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0 && r.DataContext is not null)
            .OrderBy(r => r.TranslatePoint(new Point(0, 0), grid)?.Y ?? 0)
            .Select(r => r.Index)
            .ToList();

    /// <summary>ده گامِ چرخ روی صفحهٔ همین جدول — میانه و بیشینه.</summary>
    private static (long Med, long Max) WheelSteps(Window win)
    {
        var page = win.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(v => v.Name == "PageScroll");
        if (page is null) return (0, 0);
        page.Offset = new Vector(0, 0);
        Settle(win);

        var work = new List<long>();
        for (var i = 0; i < 12; i++)
        {
            var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);
            if (page.Offset.Y >= max - 0.5) break;
            var sw = Stopwatch.StartNew();
            page.Offset = new Vector(0, Math.Min(max, page.Offset.Y + Step));
            win.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            win.UpdateLayout();
            sw.Stop();
            work.Add(sw.ElapsedMilliseconds);
        }
        if (work.Count == 0) return (0, 0);
        var sorted = work.OrderBy(x => x).ToList();
        return (sorted[sorted.Count / 2], sorted[^1]);
    }

    // ══════════════════════════════════════════════════════════════════════

    private static void SetMonth(object section, string month) =>
        section.GetType().GetProperty("Month", BindingFlags.Public | BindingFlags.Instance)
               ?.SetValue(section, month);

    private static DataGrid? Grid(Window w) =>
        w.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault(g => g.IsEffectivelyVisible);

    private static void Settle(Window w, IRowBatchHost host, int want)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
            w.UpdateLayout();
            if (host.RowCount == want) break;
            Thread.Sleep(2);
        }
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        w.UpdateLayout();
    }

    private static long Grow(Window w)
    {
        var sw = Stopwatch.StartNew();
        var last = -1d;
        var still = 0;
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        var page = w.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(v => v.Name == "PageScroll");
        while (DateTime.UtcNow < end)
        {
            if (page is not null) page.Offset = new Vector(0, Math.Max(0, page.Extent.Height - page.Viewport.Height));
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            var h = Grid(w)?.Bounds.Height ?? 0;
            if (Math.Abs(h - last) < 0.5) { if (++still >= 4) break; }
            else still = 0;
            last = h;
        }
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static long Time(Action a)
    {
        var sw = Stopwatch.StartNew();
        a();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static long Time(Func<object> a) => Time(() => { _ = a(); });

    private static string[] MonthsBack(int n)
    {
        var now = Shamsi.ToEnDigits(Shamsi.ThisMonth()).Split('/');
        var y = int.Parse(now[0]);
        var m = int.Parse(now[1]);
        var outp = new string[n];
        for (var i = 0; i < n; i++)
        {
            outp[i] = $"{y:0000}/{m:00}";
            if (--m < 1) { m = 12; y--; }
        }
        return outp;
    }

    private static void Seed(string file, string[] months)
    {
        var dbf = new PumpYaqobi.Services.Data.PumpDbFactory(file);
        dbf.EnsureReady();
        using var db = dbf.Create();
        var conn = db.Database.GetDbConnection();
        conn.Open();
        LedgerPerf.Exec(conn, "PRAGMA synchronous=OFF;");
        LedgerPerf.Exec(conn, "PRAGMA foreign_keys=OFF;");
        using var tx = conn.BeginTransaction();
        for (var k = 0; k < months.Length; k++)
            LedgerPerf.Fill(conn, "SafeEntries", months[k], Sizes[k]);
        tx.Commit();
        conn.Close();
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 200 && sw.ElapsedMilliseconds < 20_000; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            if (w.IsMeasureValid && w.IsArrangeValid && !Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.Background)) break;
        }
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(2); }
        Pump(w);
    }
}
