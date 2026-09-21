using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «موقعِ اسکرول کادرها دیر می‌آیند و لگ می‌زند» ═══════════════════════════
///
/// گزارشِ صاحب ریپو: «موقعِ اسکرول کادرها خیلی دیر می‌آیند، لگ می‌زند، جدول‌ها
/// و کادرها رندر می‌شوند… اکسل خیلی راحت بود ولی برنامهٔ من برعکس.»
///
/// این‌جا همان کار سنجیده می‌شود، نه حدس زده: با دادهٔ پنج‌ساله، در هر بخش
/// صفحه **چرخ‌به‌چرخ** پایین می‌رود (هر گام سه دندانهٔ ماوس) و برای هر گام دو
/// عدد ثبت می‌شود:
///
///   • «کار» — چند میلی‌ثانیه نخِ رابط مشغول بود تا دوباره بی‌کار شود
///     (چیدمان + تکه‌های رشدِ جدول + هر کارِ صف‌شده). این همان مکثی است که
///     کاربر زیرِ چرخ حس می‌کند.
///   • «نقاشی» — کشیدنِ یک فریمِ کامل (‎CaptureRenderedFrame‎، اسکیا روی CPU).
///     سایه‌های چهارلایهٔ کارت‌ها این‌جا خودشان را نشان می‌دهند.
///
/// آستانه‌ها: یک فریمِ ۶۰ هرتزی ۱۶ میلی‌ثانیه است؛ گامِ بالای ‎Hitch‎ «لگ» است.
///
/// ══ چه پیدا شد (۱۴۰۵/۰۶/۲۶، با نمونه‌بردارِ ‎dotnet-trace‎) ══════════════════
///   • ۶۰٪ هر گام: **سایهٔ محوِ کادرِ صفحه‌قد** (‎Border.card.section‎) که با هر
///     فریمِ اسکرول از نو کشیده می‌شد — ۹۴ ⇒ ۳۸ میلی‌ثانیه با برداشتنش.
///     ⇒ ‎PumpTheme.SectionShadow‎ (بی محو).
///   • ۱۳٪: ‎TotalsStrip‎ در هر چیدمان کلِ درختِ جدول را برای نوارِ لغزشِ افقی
///     می‌گشت — یازده نوار، حتی در بخش‌های پنهان. ⇒ کَش و ‎IsEffectivelyVisible‎.
///   • بقیه: ساختنِ ردیف‌های تازه (۱۰ تا ۲۵ میلی‌ثانیه برای هر ردیف) — تکهٔ
///     ۱۲۰ ردیفی یک مکثِ ۱٫۳ ثانیه‌ای بود. ⇒ ‎ExcelGrid.GrowMax = 8‎.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- scrollperf
///     … -- scrollperf why     چهار بار همان دفتر، هر بار با یک چیزِ خاموش
///     … -- scrollperf trace   فقط یک دفتر، هشت بار — برای ‎dotnet-trace collect‎
/// </summary>
internal static class ScrollPerf
{
    /// <summary>سه دندانهٔ ماوس، مثلِ خودِ ‎ExcelGrid.WheelStep‎.</summary>
    private const double Step = 58 * 3;

    /// <summary>گامی که از این بلندتر باشد لگِ محسوس است.</summary>
    private const long Hitch = 50;

    /// <summary>سقفِ شکست برای گامِ اسکرولِ جدولِ **ساخته‌شده** (بی ردیفِ تازه).</summary>
    private const long Broken = 120;

    /// <summary>
    /// سقفِ شکست برای یک تکهٔ رشد (‎ExcelGrid.GrowMax‎ ردیفِ تازه در یک پاس).
    /// ساختنِ ردیف در آوالونیا ذاتاً گران است (قالب، سبک‌ها، درختِ ترکیب‌گر) —
    /// روی این ماشینِ بی‌پنجره ۱۰ تا ۲۵ میلی‌ثانیه برای هر ردیف. تکهٔ ۸ ردیفی
    /// همان مکثی است که کاربر هنگامِ رسیدن به ردیف‌های ساخته‌نشده حس می‌کند.
    /// </summary>
    private const long GrowBroken = 600;

    private static readonly List<(string What, long Max, long P95, long Avg, int Hitches, int Steps, long Chunk, bool Build)> Work = new();
    private static readonly List<(string What, long Max, long Avg)> Paint = new();

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-scroll-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");

        var sw = Stopwatch.StartNew();
        YearsAudit.Seed(file);
        Console.WriteLine($"داده ساخته شد در {sw.ElapsedMilliseconds:N0} ms");

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
        LockIn.Wait(vm.Lock);
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        Settle(win);

        Console.WriteLine();
        Console.WriteLine("صفحه                                   گام‌ها   کار: بیشینه  p95   میانگین  لگ‌ها | نقاشی: بیشینه  میانگین | ردیفِ زنده");
        Console.WriteLine(new string('-', 118));

        // ══ حالتِ «چرا»: همان دفتر، چهار بار با یک چیزِ خاموش‌شده ═════════════
        if (Why)
        {
            var sec = vm.Sections.First(s => s.Id == "safe");
            Wait(win, vm.GoAsync(sec)); Settle(win);
            ScrollThrough(win, "گاوصندوق — همه‌چیز روشن (ساختِ ردیف‌ها)");
            ScrollThrough(win, "گاوصندوق — همه‌چیز روشن");
            Why = false;
            ScrollThrough(win, "گاوصندوق — بی سایه", noShadow: true);
            ScrollThrough(win, "گاوصندوق — بی سایه و بی گوشهٔ گرد", noShadow: true, noRound: true);
            ScrollThrough(win, "گاوصندوق — بی سایه، بی گوشه، بی نوارِ جمله", noShadow: true, noRound: true, noTotals: true);
            ScrollThrough(win, "سایه: فقط خطِ ۱ پیکسلی", shadow: "0 0 0 1 #2660A5FA");
            ScrollThrough(win, "سایه: خط + محوِ ۲", shadow: "0 0 0 1 #2660A5FA, 0 1 2 0 #140B1F3A");
            ScrollThrough(win, "سایه: خط + محوِ ۱۲", shadow: "0 0 0 1 #2660A5FA, 0 4 12 0 #121E3A8A");
            ScrollThrough(win, "سایه: خط + جابه‌جاییِ بی‌محو", shadow: "0 0 0 1 #2660A5FA, 0 4 0 0 #121E3A8A");
            ScrollThrough(win, "سایه: خط + محوِ ۶", shadow: "0 0 0 1 #2660A5FA, 0 2 6 0 #121E3A8A");
            Why = true;
            PaintCensus(win);
            return 0;
        }

        // ══ حالتِ پروفایل: فقط گاوصندوق، چند بار — برای ‎dotnet-trace‎ ═══════
        if (Trace)
        {
            var sec = vm.Sections.First(s => s.Id == "safe");
            Wait(win, vm.GoAsync(sec)); Settle(win);
            var mp = sec.GetType().GetProperty("Month")!;
            var month = (string)mp.GetValue(sec)!;
            for (var i = 0; i < 8; i++)
            {
                // هر بار از نو: ماهِ خالی و برگشت، تا ساختِ ردیف‌ها هم در نمونه‌ها باشد
                mp.SetValue(sec, "1300/01"); Settle(win);
                mp.SetValue(sec, month); Settle(win);
                ScrollThrough(win, $"دفترِ گاوصندوق (پروفایل {i + 1})");
            }
            return 0;
        }

        // ══ داشبورد و تنظیمات — «اسکرولشان لگ دارد» ════════════════════════
        //
        //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۵): «بخش‌های زیادی اسکرولشان لگ دارد و
        //  راحت نیست… نمونه‌ها داشبورد و تنظیمات و غیره هستند.»
        //
        //  ⚠️ تا امروز این سنجه فقط **جدول‌ها** را می‌گشت (دفتر، کارت‌های
        //  قرض‌دار، ورق، تاریخچه). داشبورد و تنظیمات جدولِ بلند ندارند، پس
        //  اگر آن‌جا لگی هست، مالِ ردیف‌ها نیست و هیچ عددی هم از آن نداشتیم.
        //  حالا داریم.
        foreach (var id in new[] { "dashboard", "settings" })
        {
            if (vm.Sections.FirstOrDefault(x => x.Id == id) is not { } page) continue;
            Wait(win, vm.GoAsync(page)); Settle(win);
            ScrollThrough(win, page.Title, build: true);
            ScrollThrough(win, page.Title + " (بارِ دوم)");
        }

        // ══ دفترِ ماهانه ═══════════════════════════════════════════════════
        //  ⚠️ «رسید قرض‌داران» و «رسید پارچه» هم اضافه شدند: صاحب ریپو هر دو
        //  را نام برد و هیچ‌کدام تا امروز در این سنجه نبودند.
        foreach (var id in new[] { "safe", "expenses", "sarrafi", "debtrasid", "rasid" })
        {
            if (vm.Sections.FirstOrDefault(x => x.Id == id) is not { } sec) continue;
            Wait(win, vm.GoAsync(sec)); Settle(win);
            ScrollThrough(win, "دفترِ " + sec.Title, build: true);
            ScrollThrough(win, "دفترِ " + sec.Title + " (بارِ دوم، ساخته‌شده)");
        }

        // ══ قرض‌داران: کارت‌ها و یک حساب ═══════════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is DebtSectionViewModel debt)
        {
            Wait(win, vm.GoAsync(debt)); Settle(win);
            ScrollThrough(win, "کارت‌های قرض‌داران", build: true);
            ScrollThrough(win, "کارت‌های قرض‌داران (بارِ دوم)");
            ScrollThrough(win, "کارت‌های قرض‌داران (بی سایه)", noShadow: true);
            Wait(win, debt.OpenByNumberAsync(300)); Settle(win);
            ScrollThrough(win, "حسابِ قرض‌دار (شمارهٔ ۳۰۰)", build: true);
            ScrollThrough(win, "حسابِ قرض‌دار (بارِ دوم)");
            debt.PersonOpen = false; Pump(win);
        }

        // ══ ورق ═════════════════════════════════════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is WaraqSectionViewModel wq)
        {
            Wait(win, vm.GoAsync(wq)); Wait(win, wq.ReloadAsync()); Settle(win);
            if (wq.Sheets.Count > 0)
            {
                wq.OpenCommand.Execute(wq.Sheets.First()); Settle(win);
                ScrollThrough(win, "ورقِ امروز", build: true);
                ScrollThrough(win, "ورقِ امروز (بارِ دوم)");
            }
        }

        // ══ تاریخچه ═════════════════════════════════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "history") is HistorySectionViewModel hs)
        {
            Wait(win, vm.GoAsync(hs)); Settle(win);
            var k = PumpYaqobi.Services.Data.HistoryService.Kinds.First().Key;
            Wait(win, hs.OpenAsync(k)); Settle(win);
            ScrollThrough(win, "تاریخچه", build: true);
        }

        Console.WriteLine();
        // صفحهٔ ساخته‌شده با گامِ اسکرول سنجیده می‌شود؛ بارِ اول (ساختنِ ردیف‌ها و
        // کارت‌ها) فقط با گران‌ترین تکهٔ رشدِ جدول — آن‌جا کلِ زنجیرهٔ رشد در یک
        // «گام»ِ این سنجش می‌دود و عددِ گام مالِ یک فریم نیست.
        var bad = Work.Where(w => w.Build ? w.Chunk > GrowBroken : w.Max > Broken).ToList();
        var hitchy = Work.Where(w => w.Hitches > 0).ToList();
        foreach (var w in hitchy) Console.WriteLine($"⚠️ {w.What}: {w.Hitches} گام از {w.Steps} بالای {Hitch} ms (بیشینه {w.Max:N0})");
        if (bad.Count == 0) { Console.WriteLine($"✅ اسکرولِ جدولِ ساخته‌شده زیرِ {Broken} ms و هر تکهٔ رشد زیرِ {GrowBroken} ms"); return 0; }
        foreach (var w in bad) Console.WriteLine($"❌ {w.What}: {(w.Build ? $"تکهٔ رشد {w.Chunk:N0} ms" : $"بیشینهٔ گام {w.Max:N0} ms")}");
        return 1;
    }

    /// <summary>صفحه را از سر تا ته چرخ‌به‌چرخ می‌برد و هر گام را می‌سنجد.</summary>
    /// <param name="build">بارِ اول روی این صفحه — ردیف‌ها و کارت‌ها همین‌جا ساخته می‌شوند؛ فقط تکهٔ رشد سنجیده می‌شود.</param>
    private static void ScrollThrough(Window win, string what, bool noShadow = false, bool noRound = false, bool noTotals = false, string? shadow = null, bool build = false)
    {
        var page = win.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(v => v.Name == "PageScroll");
        if (page is null) { Console.WriteLine($"{what,-40} بی PageScroll"); return; }

        page.Offset = new Vector(0, 0);
        Settle(win);

        List<(Border B, BoxShadows S)>? shadows = null;
        if (noShadow || shadow is not null)
        {
            shadows = win.GetVisualDescendants().OfType<Border>()
                         .Where(b => b.BoxShadow.Count > 0)
                         .Select(b => (b, b.BoxShadow)).ToList();
            var v = shadow is null ? default : BoxShadows.Parse(shadow);
            foreach (var (b, _) in shadows) b.BoxShadow = v;
            Settle(win);
        }
        List<(Border B, CornerRadius R)>? rounds = null;
        if (noRound)
        {
            rounds = win.GetVisualDescendants().OfType<Border>()
                        .Where(b => b.CornerRadius != default)
                        .Select(b => (b, b.CornerRadius)).ToList();
            foreach (var (b, _) in rounds) b.CornerRadius = default;
            Settle(win);
        }
        List<TotalsBar>? totals = null;
        if (noTotals)
        {
            totals = win.GetVisualDescendants().OfType<TotalsBar>().Where(t => t.IsVisible).ToList();
            foreach (var t in totals) t.IsVisible = false;
            Settle(win);
        }

        var work = new List<long>();
        var paint = new List<long>();
        var guard = 0;
        while (guard++ < 400)
        {
            var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);
            if (page.Offset.Y >= max - 0.5) break;

            var sw = Stopwatch.StartNew();
            page.Offset = new Vector(0, Math.Min(max, page.Offset.Y + Step));
            if (Why && guard <= 4) Census(win, "پس از عوض شدنِ آفست");
            // همان کاری که نخِ رابط تا فریمِ بعد می‌کند: چیدمان، بعد هر کارِ
            // صف‌شده (تکه‌های رشدِ جدول با اولویتِ پس‌زمینه هم همین نخ را
            // می‌گیرند و فریمِ بعد را عقب می‌اندازند).
            var t0 = sw.ElapsedMilliseconds;
            win.UpdateLayout();
            var t1 = sw.ElapsedMilliseconds;
            if (Why && guard <= 4) Census(win, $"پس از چیدمانِ اول ({t1 - t0} ms)");
            Dispatcher.UIThread.RunJobs();
            var t2 = sw.ElapsedMilliseconds;
            if (Why && guard <= 4) Census(win, $"پس از کارهای صف ({t2 - t1} ms)");
            win.UpdateLayout();
            var t3 = sw.ElapsedMilliseconds;
            if (Why && guard <= 4) Console.WriteLine($"      چیدمانِ دوم {t3 - t2} ms");
            sw.Stop();
            work.Add(sw.ElapsedMilliseconds);

            sw.Restart();
            using (var f = win.CaptureRenderedFrame()) { }
            sw.Stop();
            paint.Add(sw.ElapsedMilliseconds);
        }

        if (shadows is not null) foreach (var (b, s) in shadows) b.BoxShadow = s;
        if (rounds is not null) foreach (var (b, r) in rounds) b.CornerRadius = r;
        if (totals is not null) foreach (var t in totals) t.IsVisible = true;

        if (work.Count == 0) { Console.WriteLine($"{what,-40} صفحه کوتاه است، اسکرولی نیست"); return; }

        var live = win.GetVisualDescendants().OfType<DataGridRow>().Count(r => r.IsEffectivelyVisible);
        var sorted = work.OrderBy(x => x).ToList();
        var p95 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))];
        var hitches = work.Count(x => x > Hitch);
        Work.Add((what, work.Max(), p95, (long)work.Average(), hitches, work.Count, ExcelGrid.DiagChunkMaxMs, build));
        Paint.Add((what, paint.Max(), (long)paint.Average()));
        Console.WriteLine($"{what,-40} {work.Count,4}    {work.Max(),8:N0} {p95,5:N0} {work.Average(),8:N0}  {hitches,4}  | {paint.Max(),8:N0} {paint.Average(),8:N0} | {live,6}  تکهٔ گران: {ExcelGrid.DiagChunkMaxRows} ردیف {ExcelGrid.DiagChunkMaxMs} ms");
        ExcelGrid.DiagChunkMaxMs = 0; ExcelGrid.DiagChunkMaxRows = 0;
    }

    /// <summary>‎scrollperf why‎: کدام کنترل‌ها با هر گام دوباره اندازه می‌گیرند.</summary>
    internal static bool Why;

    /// <summary>‎scrollperf trace‎: فقط یک صفحه، چند بار — تا نمونه‌بردار چیزی برای گرفتن داشته باشد.</summary>
    internal static bool Trace;

    private static void Census(Window w, string when)
    {
        var all = w.GetVisualDescendants().OfType<Layoutable>().Where(x => x.IsEffectivelyVisible).ToList();
        var m = all.Where(x => !x.IsMeasureValid).GroupBy(x => x.GetType().Name)
                   .OrderByDescending(g => g.Count()).Select(g => $"{g.Key}×{g.Count()}").Take(8);
        var a = all.Where(x => x.IsMeasureValid && !x.IsArrangeValid).GroupBy(x => x.GetType().Name)
                   .OrderByDescending(g => g.Count()).Select(g => $"{g.Key}×{g.Count()}").Take(8);
        Console.WriteLine($"      {when}: اندازه‌نامعتبر [{string.Join(", ", m)}] · چیدمان‌نامعتبر [{string.Join(", ", a)}] · کل {all.Count}");
    }

    /// <summary>چه چیزهایی روی صفحهٔ دیده‌شونده گوشهٔ گرد یا سایه دارند.</summary>
    private static void PaintCensus(Window w)
    {
        var page = w.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(v => v.Name == "PageScroll");
        bool InView(Visual v)
        {
            if (page is null) return true;
            if (v.TranslatePoint(new Point(0, 0), page) is not { } at) return false;
            return at.Y + v.Bounds.Height >= 0 && at.Y <= page.Viewport.Height;
        }
        var borders = w.GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible && InView(b)).ToList();
        string Key(Border b) => (b.Name ?? "") + "." + string.Join('.', b.Classes) + " در " + (b.GetVisualParent()?.GetType().Name ?? "?");
        Console.WriteLine("── گوشهٔ گرد در قاب:");
        foreach (var g in borders.Where(b => b.CornerRadius != default).GroupBy(Key).OrderByDescending(g => g.Count()).Take(12))
            Console.WriteLine($"     {g.Count(),5} × {g.Key}");
        Console.WriteLine("── سایه در قاب:");
        foreach (var g in borders.Where(b => b.BoxShadow.Count > 0).GroupBy(Key).OrderByDescending(g => g.Count()).Take(12))
            Console.WriteLine($"     {g.Count(),5} × {g.Key}  ({g.First().BoxShadow.Count} لایه)");
        var rows = w.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsEffectivelyVisible).ToList();
        Console.WriteLine($"── ردیف‌های زنده {rows.Count}، در قاب {rows.Count(InView)}");
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
