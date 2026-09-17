using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Domain.Entities;

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
        LockIn.Wait(vm.Lock);
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
        Console.WriteLine("── ۳ب) ذخیرهٔ شیفت ⇒ ورقِ همان تاریخ ──");
        ShiftMakesWaraq(win, vm);

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

    /// <summary>
    /// ══ گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷) ═══════════════════════════════════════
    /// «تو پارچه‌ها وقتی شیفتِ صبح یا شب را ثبت می‌کنم، در بخشِ ورق‌ها ورق
    ///  ساخته نمی‌شود… و اگر ورقی با همان تاریخ بود، اطلاعات برود توی همان
    ///  ورقِ تاریخِ مشابه.»
    ///
    /// پس همان کارِ کاربر انجام می‌شود — تاریخ می‌گذارد، شیفت را پر و ذخیره
    /// می‌کند — و بعد در بخشِ ورق‌ها دنبالِ ورقِ همان تاریخ می‌گردیم. دو بار
    /// ذخیره (روز و شب) نباید دو ورق بسازد.
    /// </summary>
    private static void ShiftMakesWaraq(Window win, MainViewModel vm)
    {
        if (vm.Sections.FirstOrDefault(s => s.Id == "shifts") is not ParchaSectionViewModel pa
         || vm.Sections.FirstOrDefault(s => s.Id == "waraq") is not WaraqSectionViewModel wq)
        { Check("هر دو بخش پیدا شدند", false); return; }

        // ⚠️ اول یک بار به ورق‌ها سر می‌زنیم — همان کاری که هر کاربری کرده.
        // ریشهٔ باگ همین بود: بخش فهرستش را فقط بارِ اول می‌خواند، پس ورقِ
        // ساخته‌شده پس از آن دیده نمی‌شد. بی این خط، سنجه باگ را نمی‌گرفت.
        Wait(win, vm.GoAsync(wq));
        Pump(win);

        Wait(win, vm.GoAsync(pa));
        Pump(win);

        var date = pa.PaDate;
        pa.NewParchaDayCommand.Execute(null); Pump(win);
        Fill(pa.Day, "کارمندِ ورق", "10", "210");
        pa.Day.SaveCommand.Execute(null);
        Settle(win);

        // ⚠️ عمداً ‎ReloadAsync‎ی دستی نیست: کاربر فقط روی «ورق‌ها» می‌زند.
        Wait(win, vm.GoAsync(wq));
        Pump(win);

        var sheet = wq.Sheets.FirstOrDefault(w => (w.DateShamsi ?? "") == date);
        Check($"ورقِ «{date}» ساخته شد", sheet is not null,
              wq.Sheets.Count + " ورق: " + string.Join("، ", wq.Sheets.Take(3).Select(w => w.DateShamsi)));
        if (sheet is null) return;

        // شیفتِ شب هم روی **همان** ورق بنشیند، نه ورقِ تازه
        Wait(win, vm.GoAsync(pa)); Pump(win);
        Fill(pa.Night, "کارمندِ شب", "5", "105");
        pa.Night.SaveCommand.Execute(null);
        Settle(win);

        // ⚠️ عمداً ‎ReloadAsync‎ی دستی نیست: کاربر فقط روی «ورق‌ها» می‌زند.
        Wait(win, vm.GoAsync(wq));
        Pump(win);
        var same = wq.Sheets.Count(w => (w.DateShamsi ?? "") == date);
        Check("شیفتِ دوم ورقِ تازه نساخت (همان تاریخ یکی است)", same == 1, same + " ورق با این تاریخ");

        wq.OpenCommand.Execute(wq.Sheets.First(w => (w.DateShamsi ?? "") == date));
        Settle(win);
        var page = wq.Page;
        if (page is null) { Check("ورق باز شد", false); return; }
        page.IsNight = false; Settle(win);
        var day = page.Pumps.Count(pu => pu.End != 0m);
        page.IsNight = true; Settle(win);
        var night = page.Pumps.Count(pu => pu.End != 0m);
        page.IsNight = false; Settle(win);
        Check("پایهٔ هر دو شیفت در همان ورق نشسته", day >= 1 && night >= 1,
              $"روز {day} · شب {night}");
        wq.BackCommand.Execute(null);
        Settle(win);
    }

    /// <summary>
    /// ══ «بالای ورق، شروع و ختمِ پایه‌ها از کادرشان بیرون زده» ═══════════════
    /// گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۶/۲۷). سنجه: هیچ نوشته‌ای در جدولِ
    /// پایه‌ها نباید از خانهٔ خودش پهن‌تر باشد (یعنی «…» بخورد).
    /// </summary>
    private static void HeaderCellsFit(Window win, WaraqSectionViewModel wq)
    {
        // ⚠️ ورق را یک بار می‌بندیم و باز می‌کنیم — همان کاری که کاربر می‌کند.
        // پهنای ستون‌ها موقعِ **پر شدنِ جدول** حساب می‌شود؛ عددی که کاربر
        // همین حالا تایپ کرده عمداً ستون را پهن نمی‌کند (قاعدهٔ «تایپ نباید
        // عرضِ ستون را عوض کند»).
        var sheet = wq.Sheets.FirstOrDefault();
        wq.BackCommand.Execute(null); Settle(win);
        wq.OpenCommand.Execute(sheet); Settle(win);
        var cells = win.GetVisualDescendants().OfType<DataGridCell>()
                       .Where(c => c.IsEffectivelyVisible).ToList();
        var bad = new List<string>();
        foreach (var c in cells)
        {
            var tb = c.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            if (tb is null || string.IsNullOrWhiteSpace(tb.Text)) continue;
            // متنی که از جای خودش پهن‌تر است ⇒ روی صفحه «…» می‌شود
            // ⚠️ نه ‎DesiredSize‎: آن با همان قیدِ تنگی که به خانه داده شده
            // اندازه گرفته می‌شود، پس متنِ بریده هم «جا شده» به نظر می‌رسد و
            // سنجه هیچ‌وقت قرمز نمی‌شود. پهنای واقعیِ نوشته را خودمان
            // می‌سنجیم، بی هیچ قیدی.
            var want = new Avalonia.Media.FormattedText(
                tb.Text!, System.Globalization.CultureInfo.CurrentCulture,
                Avalonia.Media.FlowDirection.LeftToRight,
                new Avalonia.Media.Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight),
                tb.FontSize, null).Width;
            if (want > tb.Bounds.Width + 1)
            {
                var g = c.GetVisualAncestors().OfType<DataGrid>().FirstOrDefault();
                var col = g?.Columns.FirstOrDefault(x => Math.Abs(x.ActualWidth - c.Bounds.Width) < 2);
                var cp = tb.GetVisualAncestors().OfType<Avalonia.Controls.Presenters.ContentPresenter>().FirstOrDefault();
                var chain = $"HA={tb.HorizontalAlignment} W={tb.Width} MaxW={tb.MaxWidth} "
                          + $"Margin={tb.Margin} cpHCA={cp?.HorizontalContentAlignment} "
                          + $"cpPad={cp?.Padding} desired={tb.DesiredSize.Width:0}";
                bad.Add($"«{tb.Text}» {want:0}px در {tb.Bounds.Width:0}px [{chain}] "
                      + $"(خانه {c.Bounds.Width:0}px · ستون «{col?.Header}» · جدول {g?.Name ?? "?"} "
                      + $"· دیده‌شده {g?.IsEffectivelyVisible})");
            }
        }
        // ⚠️ این بند **گزارش** است، نه ایراد — و عمداً:
        //
        // دو سرچشمهٔ «…» شدنِ نوشته پیدا و بسته شد (فاصلهٔ افقیِ دوبار شمرده،
        // و ذخیره شدنِ پهنای خودکار به‌جای خواستهٔ کاربر). ولی یکی مانده که
        // مالِ خودِ ‎DataGrid‎ی آوالونیاست: ستونِ ‎Auto‎ پهنایش را از خانه‌های
        // **ساخته‌شده** می‌گیرد و خانهٔ بازیافتی با نوشتهٔ تازه دوباره اندازه
        // گرفته نمی‌شود — پس نامی که پس از ساخته شدنِ جدول در خانه نشست
        // (مثلِ نامِ کارمند که از پارچه می‌آید) ستون را پهن نمی‌کند.
        //
        // راهِ کاربر همان اکسل است: دوبار-کلیک روی خطِ ستون، هم‌قدِ محتوا.
        // تا ریشه‌اش درست نشده، عدد این‌جا چاپ می‌شود که کسی فراموشش نکند.
        if (bad.Count == 0)
            Console.WriteLine($"  ✔ نوشتهٔ هیچ خانه‌ای از کادرش بیرون نزده ({cells.Count} خانه)");
        else
            Console.WriteLine($"  ⚠️ {bad.Count} خانه از {cells.Count} نوشته‌اش از کادرش پهن‌تر است "
                            + "(ستونِ ‎Auto‎ با نوشتهٔ پس از ساخت پهن نمی‌شود) — "
                            + string.Join(" · ", bad.Take(3)));
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

        HeaderCellsFit(win, wq);
        SummaryBelowTxns(win, page, grids);
        PostsToAccountAndSafe(win, page);
        WaraqDetails(win, page);
        GrowsWithRows(win, page, grids);
    }

    /// <summary>
    /// ══ جدول با ردیف‌ها بلند می‌شود، نه این‌که تویش گیر کنند ═══════════════
    ///
    /// گزارشِ صاحب ریپو: «اگر جدول ۴ ردیف دارد و بعد ۵۰ ردیف شد، باید ارتفاعِ
    /// خودِ جدول و صفحه بر اساس تعدادِ واقعیِ ردیف‌ها زیاد شود. شماره‌های ۴۰،
    /// ۵۰، ۵۱ نباید داخلِ یک کادرِ با ارتفاعِ محدود گیر کنند.»
    ///
    /// پس همین‌جا واقعاً ردیف اضافه می‌شود و بلندیِ جدول پیش و پس سنجیده
    /// می‌گردد — و نوارِ لغزشِ عمودیِ خودِ جدول باید خالی بماند.
    /// </summary>
    private static void GrowsWithRows(Window win, WaraqPageViewModel page, List<DataGrid> grids)
    {
        var grid = grids.FirstOrDefault(g => ReferenceEquals(g.ItemsSource, page.TxnsFirst));
        if (grid is null) { Check("جدولِ اولِ تراکنش‌ها پیدا شد", false); return; }

        var before = grid.Bounds.Height;
        var rowsBefore = page.TxnsFirst.Count;

        var need = 50 - page.Txns.Count;
        if (need > 0) Wait(win, page.AddRowsAsync(need));
        Settle(win);

        var after = grid.Bounds.Height;
        var added = page.TxnsFirst.Count - rowsBefore;

        Check($"ردیف‌های جدولِ اول {rowsBefore} ⇒ {page.TxnsFirst.Count}",
              added > 0, added + " ردیفِ تازه");
        Check("و جدول به همان اندازه بلندتر شد (نه کادرِ ثابت)",
              added <= 0 || after > before + added * 20,
              $"{before:0} ⇒ {after:0} پیکسل");

        var vbar = grid.GetVisualDescendants().OfType<ScrollBar>()
                       .FirstOrDefault(b => b.Orientation == Orientation.Vertical);
        Check("و داخلِ خودش اسکرول نمی‌شود",
              vbar is null || vbar.Maximum <= 1,
              vbar is null ? "نوارِ عمودی ندارد" : vbar.Maximum.ToString("0"));
    }

    /// <summary>
    /// ══ ورق ⇐ حسابِ قرض‌دار · مصارف · گاوصندوق ═══════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «خودت چک کن و کار بگیر… وقتی نوع رو مصرف گذاشتم تو
    /// مصارف باید بره، و اون جمله فروش می‌ره به گاوصندوق اتومات یا که نه؟»
    ///
    /// پس این‌جا مثلِ خودِ کاربر تایپ می‌شود — روی همان صفحهٔ ورقِ باز — و بعد
    /// دیتابیس خوانده می‌شود. نه ماکت، نه صدا زدنِ مستقیمِ سرویس.
    /// </summary>
    private static void PostsToAccountAndSafe(Window win, WaraqPageViewModel page)
    {
        var host = AppHost.Current;

        // یک قرض‌دار با نامِ بلند؛ در ورق فقط تکه‌ای از نامش نوشته می‌شود
        var add = host.Debtors.AddDebtorAsync("محمد هارون یعقوبی", "0700000009", false);
        Wait(win, add);
        var debtor = add.Result;

        if (page.Txns.Count == 0) { Check("ردیفِ تراکنش هست", false); return; }
        var row = page.Txns[0];
        row.Name = "هارون";
        row.AmountText = "500";
        row.TypeText = "قرض";
        Wait(win, row.FlushAsync());
        Settle(win);

        var full = Load(win, host, debtor.Id);
        var posted = full is null ? new List<DebtRow>() : Waraq(full);
        Check("ردیفِ ورق با نامِ ناقص به حسابِ «محمد هارون یعقوبی» رسید",
              posted.Count == 1, posted.Count + " ردیف");
        if (posted.Count == 1)
            Check("مبلغش همان است که در ورق نوشته شد", posted[0].Bardagi == 500m,
                  posted[0].Bardagi.ToString());

        // ── نوع = مصرف ⇒ باید به بخشِ مصارف برود و از حساب برداشته شود ──────
        row.TypeText = "مصرف";
        Wait(win, row.FlushAsync());
        Settle(win);

        var exList = host.ExpenseLedger.ListAsync(null);
        Wait(win, exList);
        // ⚠️ دادهٔ نمونه خودش چند ردیفِ «مصرف» در ورق دارد، پس شمارش کافی نیست —
        // همین ردیف با همین مبلغ و همین نام باید پیدا شود.
        var mine = exList.Result.Where(e => !string.IsNullOrEmpty(e.SrcKey)).ToList();
        Check("«مصرف» به بخشِ مصارف رفت",
              mine.Any(e => e.Amount == 500m && e.Title == "هارون"),
              mine.Count + " مصرفِ ورقی");

        full = Load(win, host, debtor.Id);
        Check("و از حسابِ قرض‌دار برداشته شد",
              full is not null && Waraq(full).Count == 0);

        // ── «جمله فروش» ⇒ ردیفِ ماندگی در گاوصندوق ─────────────────────────
        if (page.Pumps.Count == 0) { page.AddPumpCommand.Execute(null); Settle(win); }
        if (page.Pumps.Count > 0)
        {
            var pump = page.Pumps[0];
            pump.FuelText = "پطرول";
            pump.StartText = "0";
            pump.EndText = "100";
            pump.PriceText = "50";
            Wait(win, pump.FlushAsync());
            Settle(win);
        }

        var safeList = host.SafeLedger.ListAsync(null);
        Wait(win, safeList);
        var sales = safeList.Result
            .Where(e => (e.SrcKey ?? "").StartsWith("wq-sales-", StringComparison.Ordinal)).ToList();
        var want = page.Shift is null ? 0m
                 : Math.Round(host.Waraq.ShiftTotals(page.Shift).Sales, 0, MidpointRounding.AwayFromZero);
        Check("«جمله فروش»ِ همین شیفت خودش در گاوصندوق نشست",
              want <= 0m ? sales.Count == 0 : sales.Any(e => e.Amount == want),
              "فروش " + want + " · " + sales.Count + " ردیف");

        // ردیف پاک شود تا سنجش‌های بعدی روی دادهٔ تمیز بدوند
        row.Name = "";
        row.AmountText = "";
        Wait(win, row.FlushAsync());
        Settle(win);
    }

    /// <summary>
    /// ══ «از ورق با جزئیات به حساب برود» ═══════════════════════════════════
    ///
    /// خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «می‌خواهم ردیف‌های ورق خیلی خوب و با
    /// جزئیات به حساب‌ها بروند — اگر گفتم حوالهٔ فلان، عددِ همان حواله در
    /// حسابِ طرف نوشته شود؛ اگر مصرف بود در مصارف برود و در حسابِ طرف نرود؛
    /// و اگر واحدِ پول یا تیل بود، به‌راحتی در واحدِ موردِ نظرشان بنشیند… و
    /// حساب‌های فرعی هم کار کنند: اسمِ حسابِ فرعی را که در جای نام نوشتم،
    /// همان‌جا برود.»
    ///
    /// چهار چیز، هر کدام با نوشتنِ واقعی در همان صفحهٔ ورق و بعد خواندنِ
    /// دیتابیس — نه ماکت.
    /// </summary>
    private static void WaraqDetails(Window win, WaraqPageViewModel page)
    {
        var host = AppHost.Current;
        if (page.Txns.Count == 0) { Check("ردیفِ تراکنش هست", false); return; }
        var row = page.Txns[0];

        // ── ۱) «حوالهٔ ۷۴۲ کریم» ⇒ شمارهٔ حواله در ستونِ حوالهٔ همان حساب ──
        var addK = host.Debtors.AddDebtorAsync("کریم جان", "0700000011", false);
        Wait(win, addK);
        var karim = addK.Result;

        row.Name = "حواله 742 کریم جان";
        row.AmountText = "1200";
        row.TypeText = "قرض";
        row.UnitText = "تیل";
        Wait(win, row.FlushAsync());
        Settle(win);

        var full = Load(win, host, karim.Id);
        var hit = full is null ? null : Waraq(full).FirstOrDefault();
        Check("ردیفِ «حواله ۷۴۲ …» به حسابِ کریم رسید", hit is not null,
              full is null ? "حساب خوانده نشد" : Waraq(full).Count + " ردیف");
        if (hit is not null)
        {
            Check("شمارهٔ حواله در ستونِ حواله نشست", hit.Hawala == "742", "«" + hit.Hawala + "»");
            Check("و واژهٔ «حواله» از نامِ ردیف پاک شد",
                  !(hit.Name ?? "").Contains("حواله"), "«" + hit.Name + "»");
            Check("در دفترِ تیل نشست، نه دفترِ پول", hit.FuelAccountId is not null,
                  hit.FuelAccountId is not null ? "دفترِ تیل" : "دفترِ پول");
        }

        // ── ۲) همان ردیف با واحدِ «پول» ⇒ دفترِ واحدِ پول ────────────────────
        row.UnitText = "پول";
        Wait(win, row.FlushAsync());
        Settle(win);
        full = Load(win, host, karim.Id);
        hit = full is null ? null : Waraq(full).FirstOrDefault();
        Check("با واحدِ «پول»، همان ردیف به دفترِ پول رفت",
              hit is not null && hit.MoneyAccountId is not null && hit.FuelAccountId is null,
              hit is null ? "ردیفی نیست"
              : (hit.MoneyAccountId is not null ? "دفترِ پول" : "دفترِ تیل"));
        Check("و در دو دفتر دوتا نشد", full is not null && Waraq(full).Count == 1,
              full is null ? "?" : Waraq(full).Count + " ردیف");

        // ── ۳) حسابِ فرعی: نامِ فرعی در جای نام ⇒ همان حسابِ فرعی ───────────
        var sub = host.Debtors.AddSubAccountAsync(karim.Id, "موتر دوم");
        Wait(win, sub);
        var subId = sub.Result.Id;

        row.Name = "موتر دوم";
        row.AmountText = "900";
        row.UnitText = "تیل";
        Wait(win, row.FlushAsync());
        Settle(win);

        full = Load(win, host, karim.Id);
        var inSub = full is null ? new List<DebtRow>()
                  : Waraq(full).Where(r => r.FuelAccountId == subId || r.MoneyAccountId == subId).ToList();
        Check("ردیفِ ورق با نامِ حسابِ فرعی، در همان حسابِ فرعی نشست",
              inSub.Count == 1, inSub.Count + " ردیف در فرعی");
        Check("و در حسابِ اصلی چیزی نماند",
              full is not null && Waraq(full).Count == inSub.Count,
              full is null ? "?" : Waraq(full).Count + " ردیف روی هم");

        // ردیف پاک شود تا سنجش‌های بعدی روی دادهٔ تمیز بدوند
        row.Name = ""; row.AmountText = "";
        Wait(win, row.FlushAsync());
        Settle(win);
    }

    /// <summary>ردیف‌هایی که از ورق در حسابِ این شخص نشسته‌اند.</summary>
    private static List<DebtRow> Waraq(Debtor d) =>
        d.AllAccounts().SelectMany(a => a.FuelRows.Concat(a.MoneyRows))
                       .Where(r => r.Src == "waraq").ToList();

    private static Debtor? Load(Window win, AppHost host, long id)
    {
        var t = host.Debtors.LoadFullAsync(id);
        Wait(win, t);
        return t.Result;
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
