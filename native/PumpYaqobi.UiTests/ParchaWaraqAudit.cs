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
/// ══ سنجشِ «پارچه‌ها» و «ورق‌ها» روی پنجرهٔ واقعی ═══════════════════════════════
///
/// گزارشِ صاحب ریپو، چهار تا:
///   • «توی ورق‌ها دو کادر دارد که شبیه هم است، بابتِ این‌که بیشتر جا بشود.»
///   • «پارچه همهٔ ردیف‌هایش پشتِ هم نیستند — کادرها منظورم است.»
///   • «بخشِ پارچه‌ها هم تمام صفحه نیست.»
///   • «منطقِ پارچه‌ها: در روز چندین پایه است، تکی‌تکی پر می‌کنم و پارچهٔ جدید
///      می‌سازم.»
///
///     dotnet run --project PumpYaqobi.UiTests -- parcha
/// </summary>
internal static class ParchaWaraqAudit
{
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
                                 "pump-pw-" + Guid.NewGuid().ToString("N"), "pump.db");
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

        Console.WriteLine();
        Console.WriteLine("── ۱) هر بخش تمامِ عرضِ پنجره را می‌گیرد؟ ──");
        Widths(win, vm);

        Console.WriteLine();
        Console.WriteLine("── ۲) پارچه‌ها: خانه‌ها پشتِ هم و تمام‌عرض ──");
        ParchaFields(win, vm);

        Console.WriteLine();
        Console.WriteLine("── ۳) پارچه‌ها: «🆕 پارچهٔ جدید» گزارشِ تازه می‌سازد ──");
        ParchaLogic(win, vm);

        Console.WriteLine();
        Console.WriteLine("── ۴) ورق‌ها: دو جدولِ هم‌شکل، کنارِ هم ──");
        WaraqSplit(win, vm);

        Console.WriteLine();
        if (_bad == 0) { Console.WriteLine("✅ پارچه‌ها و ورق‌ها سالم‌اند"); return 0; }
        Console.WriteLine($"❌ {_bad} ایراد");
        return 1;
    }

    // ══ ۱) پهنا ══════════════════════════════════════════════════════════════

    /// <summary>
    /// «تمام صفحه نیست» را می‌شود اندازه گرفت: محتوای بخش باید تقریباً هم‌پهنای
    /// خودِ پنجره باشد. اگر ستونی باریک وسطِ صفحه بنشیند، این‌جا لو می‌رود.
    /// </summary>
    private static void Widths(Window win, MainViewModel vm)
    {
        // پنجرهٔ سنجش ۱۰۰۰ پیکسل است و ستونِ سایت هم ۱۰۰۰ — پس این‌جا هر دو
        // دسته تقریباً هم‌پهنا در می‌آیند و تفاوت دیده نمی‌شود. برای همین
        // پنجره موقتاً پهن می‌شود تا ستونِ ۱۰۰۰ واقعاً «ستون» بماند.
        var was = win.Width;
        win.Width = 1600;
        Pump(win);

        var bad = new List<string>();
        foreach (var sec in vm.Sections)
        {
            Wait(win, vm.GoAsync(sec));
            Pump(win);
            var host = win.GetVisualDescendants().OfType<ContentControl>()
                          .FirstOrDefault(c => ReferenceEquals(c.Content, sec));
            if (host is null) continue;

            var w = host.Bounds.Width;
            var shouldBeWide = double.IsPositiveInfinity(sec.ContentMaxWidth);
            var isWide = w > 1200;
            if (shouldBeWide != isWide)
                bad.Add($"{sec.Id} {w:0}px (باید {(shouldBeWide ? "پهن" : "ستونِ ۱۰۰۰")} می‌بود)");
        }

        win.Width = was;
        Pump(win);

        Check("پهنای هر بخش با فهرستِ ‎_WIDE‎ی سایت می‌خواند", bad.Count == 0);
        foreach (var b in bad) Console.WriteLine("        " + b);
    }

    // ══ ۲) خانه‌های پارچه ════════════════════════════════════════════════════

    /// <summary>
    /// در سایت هر خانهٔ کارتِ شیفت یک ردیفِ جدا و تمام‌عرض است
    /// (‎.field-row{flex-direction:column}‎ + ‎.field-input{width:100%}‎).
    /// پس هیچ دو کادرِ ورودی‌ای نباید هم‌ردیف (هم‌ارتفاع و کنارِ هم) باشند —
    /// جز جفتِ «نام + شمارهٔ پایه» که در خودِ سایت هم یک ردیف است.
    /// </summary>
    private static void ParchaFields(Window win, MainViewModel vm)
    {
        var sec = vm.Sections.FirstOrDefault(s => s.Id == "shifts");
        if (sec is null) { Check("بخشِ پارچه‌ها پیدا شد", false); return; }
        Wait(win, vm.GoAsync(sec));
        Pump(win);

        var card = win.GetVisualDescendants().OfType<Border>()
                      .FirstOrDefault(b => b.Classes.Contains("shift-card"));
        if (card is null) { Check("کارتِ شیفت پیدا شد", false); return; }

        // کادرهای ورودیِ خودِ کارت (نه دکمه‌ها)
        var boxes = card.GetVisualDescendants().OfType<TextBox>()
                        .Where(t => t.IsEffectivelyVisible && t.Bounds.Height > 0)
                        .Select(t => (Box: t, Y: t.TranslatePoint(new Point(0, 0), win)?.Y ?? -1))
                        .Where(x => x.Y >= 0)
                        .ToList();

        // هم‌ردیف = اختلافِ ارتفاعشان کمتر از نصفِ یک ردیف
        var pairs = 0;
        for (var i = 0; i < boxes.Count; i++)
            for (var j = i + 1; j < boxes.Count; j++)
                if (Math.Abs(boxes[i].Y - boxes[j].Y) < 8) pairs++;

        // فقط «نام + شمارهٔ پایه» حق دارد هم‌ردیف باشد
        Check($"{boxes.Count} کادر؛ فقط یک جفت هم‌ردیف (نام + شمارهٔ پایه)", pairs <= 1,
              pairs + " جفت هم‌ردیف");
    }

    // ══ ۳) منطقِ «پارچهٔ جدید» ════════════════════════════════════════════════

    /// <summary>
    /// ‎newParcha('day')‎ی سایت هیچ رکوردی نمی‌سازد — فقط فرم را خالی و پرچم را
    /// بلند می‌کند؛ گزارشِ تازه هنگامِ **ذخیره** ساخته می‌شود. این‌جا همان را
    /// می‌سنجیم: پر کن و ذخیره کن، «پارچهٔ جدید» بزن، دوباره پر و ذخیره کن —
    /// باید دو گزارش شده باشد، نه یکی که روی قبلی نوشته.
    /// </summary>
    private static void ParchaLogic(Window win, MainViewModel vm)
    {
        if (vm.Sections.FirstOrDefault(s => s.Id == "shifts") is not ParchaSectionViewModel pa)
        { Check("ویومدلِ پارچه‌ها پیدا شد", false); return; }

        Wait(win, vm.GoAsync(pa));
        Pump(win);

        var before = pa.Reports.Count;

        Fill(pa.Day, "کارمندِ یک", "100", "300");
        pa.Day.SaveCommand.Execute(null);
        Settle(win);

        pa.NewParchaDayCommand.Execute(null);
        Pump(win);
        Check("فرم خالی شد", pa.Day.Name.Length == 0, "«" + pa.Day.Name + "»");

        Fill(pa.Day, "کارمندِ دو", "300", "560");
        pa.Day.SaveCommand.Execute(null);
        Settle(win);

        Check($"دو پارچهٔ جدا ساخته شد ({before} ← {pa.Reports.Count})",
              pa.Reports.Count >= before + 2);
    }

    private static void Fill(ShiftFormViewModel f, string name, string start, string end)
    {
        f.Name = name; f.Start = start; f.End = end; f.Price = "64";
    }

    // ══ ۴) دو جدولِ ورق ══════════════════════════════════════════════════════

    /// <summary>
    /// سایت: ‎mid = Math.ceil(txns.length / 2)‎ — نیمهٔ اول در جدولِ اول، بقیه
    /// در جدولِ دوم. با ۵ ردیف می‌شود ۳ و ۲.
    /// </summary>
    private static void WaraqSplit(Window win, MainViewModel vm)
    {
        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is not WaraqSectionViewModel wq)
        { Check("بخشِ ورق‌ها پیدا شد", false); return; }

        Wait(win, vm.GoAsync(wq));
        Pump(win);
        Wait(win, wq.ReloadAsync());
        wq.OpenCommand.Execute(wq.Sheets.FirstOrDefault());
        Settle(win);

        var page = wq.Page;
        if (page is null) { Check("ورق باز شد", false); return; }

        while (page.Txns.Count < 5) { page.AddTxnCommand.Execute(null); Settle(win); }

        var mid = (int)Math.Ceiling(page.Txns.Count / 2.0);
        Check($"{page.Txns.Count} ردیف ⇒ {mid} و {page.Txns.Count - mid}",
              page.TxnsFirst.Count == mid && page.TxnsSecond.Count == page.Txns.Count - mid,
              page.TxnsFirst.Count + " و " + page.TxnsSecond.Count);

        Check("شمارهٔ ردیف پیوسته است (جدولِ دوم از همان‌جا ادامه می‌دهد)",
              page.TxnsSecond.Count == 0
              || page.TxnsSecond[0].Index == PumpYaqobi.Application.Localization.Shamsi.Money(mid + 1),
              page.TxnsSecond.FirstOrDefault()?.Index);

        // و روی صفحه هم واقعاً دو جدول کنارِ هم باشند، نه یکی
        var grids = win.GetVisualDescendants().OfType<DataGrid>()
                       .Where(g => ReferenceEquals(g.ItemsSource, page.TxnsFirst)
                                || ReferenceEquals(g.ItemsSource, page.TxnsSecond))
                       .ToList();
        Check("دو جدولِ تراکنش روی صفحه هست", grids.Count == 2, grids.Count + " جدول");

        SummaryBelowTxns(win, page, grids);
    }

    /// <summary>
    /// ══ شش کادرِ «خلاصه شیفت» باید زیرِ جدولِ تراکنش‌ها باشند ═══════════════
    ///
    /// گزارشِ صاحب ریپو: «اون شش تا پایینِ این جدولِ تراکنش‌ها استن.» در سایت
    /// هم همان‌جاست (‎index.html‎ خط ۱۹۹۷۴؛ بعد از دو ‎#wq-trans-tbl-*‎).
    ///
    /// ⚠️ این را روی پنجرهٔ واقعی می‌سنجیم، نه روی متنِ ‎axaml‎: جای واقعیِ یک
    /// کادر را فقط چیدمانِ واقعی می‌گوید.
    /// </summary>
    private static void SummaryBelowTxns(Window win, WaraqPageViewModel page, List<DataGrid> grids)
    {
        string[] labels =
        {
            "⛽ جمله بطرول", "🟤 جمله دیزل", "🟣 جمله مصرف",
            "💳 جمله قرض", "📊 جمله فروش",
        };

        var texts = win.GetVisualDescendants().OfType<TextBlock>().ToList();
        var boxes = labels
            .Select(l => texts.FirstOrDefault(t => (t.Text ?? "").Trim() == l))
            .ToList();

        var missing = labels.Where((_, i) => boxes[i] is null).ToList();
        Check("هر پنج کادرِ نام‌دارِ خلاصه روی صفحه هست", missing.Count == 0,
              missing.Count == 0 ? null : string.Join("، ", missing));

        Check("عنوانِ «خلاصه شیفت» با شیفتِ باز می‌خواند",
              texts.Any(t => (t.Text ?? "").Trim() == page.SummaryTitle),
              page.SummaryTitle);

        if (missing.Count > 0 || grids.Count == 0) return;

        var gridBottom = grids.Max(g => Top(g, win) + g.Bounds.Height);
        var boxTop = boxes.Where(b => b is not null).Min(b => Top(b!, win));

        Check("کادرها پایین‌ترند از جدولِ تراکنش‌ها", boxTop >= gridBottom,
              $"جدول تا {gridBottom:0} · کادرها از {boxTop:0}");
    }

    /// <summary>بلندای یک کنترل نسبت به خودِ پنجره.</summary>
    private static double Top(Visual v, Visual root) =>
        v.TranslatePoint(new Point(0, 0), root)?.Y ?? double.NaN;

    // ══════════════════════════════════════════════════════════════════════

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    /// <summary>چند دورِ بیشتر — برای کارهایی که ذخیرهٔ ناهم‌گام دارند.</summary>
    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) Pump(w);
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) Pump(w);
        Pump(w);
    }
}
