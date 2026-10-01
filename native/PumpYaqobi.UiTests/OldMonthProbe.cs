using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «ماه‌های قبل» و «هیچ نوشته‌ای چپ یا راست نرود» (۱۴۰۵/۰۷/۱۸) ══════════════
///
/// گزارشِ صاحب ریپو: «چرا توی ماه‌های قبل همهٔ بخش‌ها نمی‌شه جدول اضافه کرد و
/// وقتی جدول می‌خوام بسازم می‌ره همون ماهی که فعلاً داخلش بودم… هر ماه حساب‌های
/// جدای خودشو داره… و سربرگ‌ها و نوشته‌ها هرگز نیان به چپ یا راست — همه باید
/// وسط باشن، و به محضِ این‌که این اتفاق افتاد درجا درست بشه.»
///
/// با پنجرهٔ واقعی، در مصارف · گاوصندوق · صرافی · چکنه:
///   ۱) رفتن به ماهِ گذشته ⇒ «➕ ردیف» ⇒ ردیف در <b>همان</b> ماه، ماهِ جلوی چشم
///      عوض نشد، و ماهِ جاری دست نخورد
///   ۲) هر سرستون و هر نوشتهٔ خانه واقعاً وسطِ کادرِ خودش کشیده شده (از
///      ‎TextLayout‎ی خودِ نوشته، نه از قاعدهٔ سبک) — پس از رفت‌وبرگشتِ ماه،
///      رفتن به بخشِ دیگر و برگشت، و تعویضِ تم در بخشِ دیگر
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- oldmonths
/// </summary>
internal static class OldMonthProbe
{
    private static int _bad;
    private static int _shot;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private const string Old = "1404/03";

    public static int Run(string[] args)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-oldm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        static double Env(string k, double d) => double.TryParse(Environment.GetEnvironmentVariable(k),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
        var win = new MainWindow { Width = Env("OM_W", 1440), Height = Env("OM_H", 900) };
        LayoutCycleProbe.SetScaling(win, Env("OM_SCALE", 1));
        win.Show();
        Console.WriteLine($"پنجره {win.Width}×{win.Height} · مقیاس {win.RenderScaling}");
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Seed.Fill(AppHost.Current);
        SeedOld(AppHost.Current);
        Settle(win);

        foreach (var id in new[] { "expenses", "safe", "sarrafi", "chakana" })
        {
            Console.WriteLine();
            Console.WriteLine($"════ {id} ════");
            OneSection(win, vm, id);
        }

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ همه سرِ جایش بود" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void SeedOld(AppHost h)
    {
        var d = Old + "/12";
        const string longText = "هارون بابت نان و غیره — مصرفِ ماهِ پیش با توضیحِ بلند";
        h.ExpenseLedger.AddAsync(new Expense { DateShamsi = d, Title = longText, Amount = 1000m }).GetAwaiter().GetResult();
        h.SafeLedger.AddAsync(new SafeEntry { DateShamsi = d, Title = "فروش ورق — روز (محمد هارون) — " + d, Amount = 88000m }).GetAwaiter().GetResult();
        h.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = d, Description = longText, Amount = 100m, Rate = 1m }).GetAwaiter().GetResult();
        h.RetailLedger.AddAsync(new RetailRow { DateShamsi = d, Name = "علی احمدی", Liters = 10m, PricePerLiter = 60m }).GetAwaiter().GetResult();
        //  ══ دادهٔ قدیمیِ واقعی (۱۴۰۵/۰۷/۱۹): فاصلهٔ پایانی، تب، نویسهٔ جهت‌نما ══
        //  «آن‌هایی که قدیم رسانده بودم باید پاکشان کنم» — همین‌ها ۲٫۵ تا ۱۰ پیکسل کج بودند.
        foreach (var t in new[] { "کریم ", "\t هارون \t", "\u200fمحمد", "نان\u200c", "حوالهٔ 12 ", "500 افغانی " })
        {
            h.ExpenseLedger.AddAsync(new Expense { DateShamsi = d, Title = t, Amount = 500m }).GetAwaiter().GetResult();
            h.SafeLedger.AddAsync(new SafeEntry { DateShamsi = d, Title = t, Amount = 700m }).GetAwaiter().GetResult();
            h.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = d, Description = t, Amount = 5m, Rate = 1m }).GetAwaiter().GetResult();
            h.RetailLedger.AddAsync(new RetailRow { DateShamsi = d, Name = t, Liters = 2m, PricePerLiter = 60m }).GetAwaiter().GetResult();
        }
        //  جدولِ بلند (پنجرهٔ چسبان) — مثلِ ماهِ واقعیِ یک پمپ
        if (Environment.GetEnvironmentVariable("OM_MANY") != "1") return;
        for (var i = 1; i <= 70; i++)
        {
            var di = Old + "/" + (1 + i % 28).ToString("00");
            h.ExpenseLedger.AddAsync(new Expense { DateShamsi = di, Title = "مصرفِ شمارهٔ " + i, Amount = 100m * i }).GetAwaiter().GetResult();
            h.SafeLedger.AddAsync(new SafeEntry { DateShamsi = di, Title = "فروش ورق — روز " + i, Amount = 1000m * i }).GetAwaiter().GetResult();
            h.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = di, Description = "حوالهٔ " + i, Amount = 10m * i, Rate = 1m }).GetAwaiter().GetResult();
            h.RetailLedger.AddAsync(new RetailRow { DateShamsi = di, Name = "مشتری " + i, Liters = i, PricePerLiter = 60m }).GetAwaiter().GetResult();
        }
    }

    private static SectionViewModel Open(MainWindow win, MainViewModel vm, string id)
    {
        if (vm.Sections.FirstOrDefault(x => x.Id == id) is { } top)
        {
            Wait(win, vm.GoAsync(top));
            return top;
        }
        var parent = vm.Sections.First(x => x.SubSections.Any(s => s.Id == id));
        Wait(win, vm.GoAsync(parent));
        var sub = parent.SubSections.First(s => s.Id == id);
        parent.ShowSubCommand.Execute(sub);
        Settle(win);
        return sub;
    }

    private static void OneSection(MainWindow win, MainViewModel vm, string id)
    {
        var s = Open(win, vm, id);
        dynamic d = s;
        var shown = (string)d.Month;
        d.Month = Old;
        Settle(win);
        Check($"{s.Title}: ماهِ {Old} باز شد", (string)d.Month == Old, (string)d.Month);
        Centered(win, $"{s.Title} ({Old})");
        Scrolled(win, s.Title);

        int before = ((System.Collections.ICollection)d.Rows).Count;
        var cmd = (System.Windows.Input.ICommand)d.AddRowCommand;
        cmd.Execute(null);
        Settle(win);
        int after = ((System.Collections.ICollection)d.Rows).Count;
        Check($"{s.Title}: «➕ ردیف» در ماهِ {Old} ردیف ساخت و ماه عوض نشد",
              after == before + 1 && (string)d.Month == Old, $"{before} ⇒ {after} · ماه {d.Month}");

        var newest = LastAdded(id);
        Check($"{s.Title}: تاریخِ ردیفِ تازه در همان ماهِ {Old} است",
              Shamsi.MonthKey(newest) == Old, newest);
        Centered(win, $"{s.Title} ({Old}، پس از ردیفِ تازه)");

        //  ماهِ جاری: ردیفِ تازه همان امروز
        d.Month = Shamsi.ThisMonth();
        Settle(win);
        cmd.Execute(null);
        Settle(win);
        Check($"{s.Title}: در ماهِ جاری «➕ ردیف» همان امروز است",
              LastAdded(id) == Shamsi.Today(), LastAdded(id));

        //  رفت‌وبرگشتِ ماه، بخشِ دیگر و تعویضِ تم در بخشِ دیگر ⇒ باز هم وسط
        d.Month = Old; Settle(win);
        Wait(win, vm.GoAsync(vm.Sections.First(x => x.Id == "dashboard")));
        vm.SelectedTheme = vm.SelectedTheme == PumpYaqobi.App.Themes.PumpTheme.Gold
            ? PumpYaqobi.App.Themes.PumpTheme.Blue : PumpYaqobi.App.Themes.PumpTheme.Gold;
        Settle(win);
        Open(win, vm, id);
        Settle(win);
        Centered(win, $"{s.Title} (پس از بخشِ دیگر و تعویضِ تم)");
        d.Month = shown; Settle(win);
        Centered(win, $"{s.Title} (برگشت به {shown})");

        //  کارهای خودِ کاربر: نوشتن در خانه، حذفِ ردیف، کوچک و بزرگ کردنِ پنجره
        d.Month = Old; Settle(win);
        var rows = (System.Collections.IList)d.Rows;
        if (rows.Count > 0)
        {
            var r0 = rows[0]!;
            foreach (var pn in new[] { "Title", "Name", "Description", "Note" })
                if (r0.GetType().GetProperty(pn) is { CanWrite: true } pi)
                { pi.SetValue(r0, "نوشتهٔ تازهٔ کاربر 12"); break; }
            Settle(win);
            Centered(win, $"{s.Title} (پس از نوشتن در خانه)");
        }
        if (rows.Count > 1 && s is PumpYaqobi.App.ViewModels.IRowBatchHost bh)
        {
            Wait(win, bh.DeleteRowsAsync(1));
            Centered(win, $"{s.Title} (پس از حذفِ ردیف)");
        }
        var (w0, h0) = (win.Width, win.Height);
        win.Width = w0 * 0.75; Settle(win);
        Centered(win, $"{s.Title} (پنجرهٔ کوچک‌تر)");
        win.Width = w0; win.Height = h0; Settle(win);
        Centered(win, $"{s.Title} (پنجره دوباره بزرگ)");
        d.Month = shown; Settle(win);
    }

    /// <summary>با چرخِ صفحه پایین و بالا — در هر جا، هم فریمِ همان لحظه هم پس از ته‌نشینی.</summary>
    private static void Scrolled(Window win, string title)
    {
        var sv = win.GetVisualDescendants().OfType<ScrollViewer>()
                    .Where(v => v.IsEffectivelyVisible && v.Extent.Height > v.Viewport.Height + 50)
                    .OrderByDescending(v => v.Extent.Height).FirstOrDefault();
        if (sv is null) return;
        foreach (var f in new[] { 0.3, 0.6, 1.0, 0.0 })
        {
            sv.Offset = new Vector(sv.Offset.X, (sv.Extent.Height - sv.Viewport.Height) * f);
            Pump(win);
            if (!Measure(win, "", report: false))
            {
                Settle(win);
                Measure(win, $"{title} (اسکرول {f:0.#})", report: true);
            }
        }
    }

    private static string LastAdded(string id)
    {
        var h = AppHost.Current;
        using var db = h.Db.Create();
        return id switch
        {
            "expenses" => db.Expenses.OrderByDescending(x => x.Id).First().DateShamsi ?? "",
            "safe" => db.SafeEntries.OrderByDescending(x => x.Id).First().DateShamsi ?? "",
            "sarrafi" => db.ExchangeRows.OrderByDescending(x => x.Id).First().DateShamsi ?? "",
            _ => db.RetailRows.OrderByDescending(x => x.Id).First().DateShamsi ?? "",
        };
    }

    private static IEnumerable<DataGrid> Grids(Window win) =>
        win.GetVisualDescendants().OfType<DataGrid>()
           .Where(g => g.IsEffectivelyVisible && g.Bounds.Width > 50 && g.Columns.Any(c => c.IsVisible));

    /// <summary>
    /// نوشتهٔ هر سرستون و هر خانه واقعاً وسطِ کادرش کشیده شده — از خودِ
    /// ‎TextLayout‎ (جای خطِ اول)، و سرستون بالای ستونِ خودش.
    /// </summary>
    private static void Centered(Window win, string what)
    {
        //  ⚠️ یک بار دوباره: توست یا بالونِ هشدار گاهی درست روی خانه‌ها می‌نشیند
        //  (یک در شش اجرا دیده شد، با جوهرِ ۱۰۰ پیکسلی روی نوشتهٔ ۵۰ پیکسلی). کجیِ
        //  واقعی در اندازه‌گیریِ دوم هم هست.
        if (Measure(win, what, report: false)) return;
        Settle(win);
        Measure(win, what, report: true);
    }

    private static bool Measure(Window win, string what, bool report)
    {
        var g = Grids(win).FirstOrDefault();
        if (g is null) { if (report) Check(what + ": جدول پیدا شد", false); return false; }
        //  توستِ گذرا روی خانه‌ها می‌افتد و جوهرِ خودش را به نوشته می‌چسباند
        AppHost.Current.Toasts.Visible = false;
        Pump(win);
        //  ⚠️ دو بار: جابه‌جاییِ کشیدن (‎RenderTransform‎) در قابِ بعدی دیده می‌شود
        win.CaptureRenderedFrame()?.Dispose();
        Pump(win);
        using var shot = win.CaptureRenderedFrame()!;
        using var ms = new MemoryStream();
        shot.Save(ms);
        ms.Position = 0;
        using var frame = SkiaSharp.SKBitmap.Decode(ms);
        var bad = new List<string>();
        var n = 0;

        foreach (var h in g.GetVisualDescendants().OfType<DataGridColumnHeader>()
                             .Where(h => h.IsEffectivelyVisible && h.Bounds.Width > 4))
            foreach (var tb in h.GetVisualDescendants().OfType<TextBlock>()
                                .Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
            {
                n++;
                if (Off(tb, h, frame) is { } o && o > 3) bad.Add($"سرستونِ «{tb.Text}» {o:0}px");
            }

        foreach (var cell in g.GetVisualDescendants().OfType<DataGridCell>()
                              .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 4))
            foreach (var tb in cell.GetVisualDescendants().OfType<TextBlock>()
                                   .Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
            {
                n++;
                if (Off(tb, cell, frame) is { } o && o > 3) bad.Add($"«{tb.Text}» {o:0}px");
            }

        //  سرستون بالای ستونِ خودش
        var heads = g.GetVisualDescendants().OfType<DataGridColumnHeader>()
                     .Where(h => h.IsEffectivelyVisible && h.Content is string s && s.Length > 0).ToList();
        var row = g.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.IsEffectivelyVisible);
        if (row is not null)
        {
            var cells = row.GetVisualDescendants().OfType<DataGridCell>().Where(c => c.IsEffectivelyVisible).ToList();
            foreach (var h in heads)
            {
                var hx = h.TranslatePoint(default, g)!.Value.X;
                var cell = cells.MinBy(c => Math.Abs(c.TranslatePoint(default, g)!.Value.X - hx));
                if (cell is null) continue;
                var dx = Math.Abs(cell.TranslatePoint(default, g)!.Value.X - hx);
                var dw = Math.Abs(cell.Bounds.Width - h.Bounds.Width);
                if (dx > 2 || dw > 2) bad.Add($"سرستونِ «{h.Content}» {dx:0}/{dw:0}px از ستونش");
            }
        }

        var stale = LayoutCycleProbe.WrongAligned(g);
        bad.AddRange(stale.Select(x => "کشیده با ترازِ دیگر: " + x));
        if (report && bad.Count > 0 && Environment.GetEnvironmentVariable("OM_SHOTS") is { Length: > 0 } od)
        {
            Directory.CreateDirectory(od);
            File.WriteAllBytes(Path.Combine(od, "om-" + (++_shot) + ".png"), ms.ToArray());
            Console.WriteLine("      📷 om-" + _shot + ".png · " + what);
        }
        if (!report && bad.Count > 0) return false;
        Check($"{what}: {n} نوشته وسطِ کادرِ خودش", bad.Count == 0,
              bad.Count == 0 ? null : string.Join("، ", bad.Take(5)));
        return bad.Count == 0;
    }

    /// <summary>
    /// فاصلهٔ وسطِ جوهرِ نوشته (همان پیکسل‌هایی که کاربر می‌بیند) از وسطِ کادرِ
    /// صاحبش. ‎null‎ یعنی نوشته‌ای که کادرش را پر کرده — آن وسط‌چینی ندارد.
    /// ⚠️ از روی قابِ واقعیِ پنجره، نه ‎TextLine.Start‎: در راست‌به‌چپ با عدد و
    /// فاصلهٔ پایانی گمراه می‌کرد (سنجیده شد).
    /// </summary>
    internal static double? Off(TextBlock tb, Visual box, SkiaSharp.SKBitmap frame)
    {
        var tl = tb.TextLayout;
        if (tl is null || tl.TextLines.Count == 0) return null;
        var inner = tb.Bounds.Width - tb.Padding.Left - tb.Padding.Right;
        if (tl.TextLines[0].Width >= inner - 1) return null;       // پر از کادر
        var root = TopLevel.GetTopLevel(tb)!;
        //  ⚠️ قابِ پنجره به پیکسل است، نه به واحدِ چیدمان: در ۱۲۵٪ هر واحد ۱٫۲۵ پیکسل
        var k = root.RenderScaling;
        (double L, double R, double T, double B) Rect(Visual v)
        {
            var a = v.TranslatePoint(default, root)!.Value * k;
            var b = v.TranslatePoint(new Point(v.Bounds.Width, v.Bounds.Height), root)!.Value * k;
            return (Math.Min(a.X, b.X), Math.Max(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y));
        }
        var bx = Rect(box);
        var tr = Rect(tb);
        //  زیرِ نوارِ شناورِ بخش‌ها یا بیرونِ قاب ⇒ دیده نمی‌شود، سنجیدنی نیست
        var navBottom = root.GetVisualDescendants().OfType<Control>()
            .Where(c => c.Name == "NavItems" && c.IsEffectivelyVisible)
            .Select(c => Rect(c).B).DefaultIfEmpty(0).Max();
        if (tr.T < navBottom + 2 || tr.B > frame.Height - 2) return null;
        int x0 = (int)Math.Ceiling(bx.L) + 3, x1 = (int)Math.Floor(bx.R) - 3;
        int y0 = (int)Math.Ceiling(tr.T) + 1, y1 = (int)Math.Floor(tr.B) - 1;
        if (x1 <= x0 || y1 <= y0 || x1 >= frame.Width || y1 >= frame.Height) return null;
        var bg = frame.GetPixel(x0, y0);
        int lo = -1, hi = -1;
        for (var x = x0; x <= x1; x++)
            for (var y = y0; y <= y1; y++)
            {
                var c = frame.GetPixel(x, y);
                if (Math.Abs(c.Red - bg.Red) + Math.Abs(c.Green - bg.Green) + Math.Abs(c.Blue - bg.Blue) > 150)
                { if (lo < 0) lo = x; hi = x; break; }
            }
        if (lo < 0) return null;
        var off = Math.Abs((lo + hi + 1) / 2.0 - (bx.L + bx.R) / 2) / k;
        if (Environment.GetEnvironmentVariable("OM_DEBUG") == "1" && off > 3)
            Console.WriteLine($"      · «{tb.Text}» ink={lo}..{hi} box={bx.L:0}..{bx.R:0} tb={tr.L:0}..{tr.R:0} w={tl.TextLines[0].Width:0.#} wt={tl.TextLines[0].WidthIncludingTrailingWhitespace:0.#} start={tl.TextLines[0].Start:0.#} wrap={tb.TextWrapping} rt={(tb.RenderTransform as Avalonia.Media.TranslateTransform)?.X} fix={PumpYaqobi.App.Controls.RtlTrim.CenterFix(tb):0.#} mv={tb.IsMeasureValid} av={tb.IsArrangeValid}");
        return off;
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
