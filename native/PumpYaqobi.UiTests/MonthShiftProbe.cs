using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Media;
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
/// ══ «چپ/راست رفتنِ خط‌ها و نوشته‌ها پس از عوض کردنِ ماه — همهٔ بخش‌ها» (۱۴۰۵/۰۷/۲۱) ══
///
/// همان کارِ کاربر با <b>خودِ کشوی سال و ماه</b> (نه نوشتنِ مستقیمِ ‎Month‎):
/// ماهِ جاری ⇒ ماهِ پرردیف (۱۲۰ ردیف: ستونِ «#» سه‌رقمی و پنجرهٔ چسبان) ⇒ ماهِ
/// کم‌ردیف ⇒ سالِ پیش (نوشته‌های بلند) ⇒ برگشت ⇒ «همهٔ ماه‌ها». پس از هر انتخاب،
/// هم «چند قابِ نخست» و هم «پس از ته‌نشینی» سنجیده می‌شود:
///
///   ۱) لبهٔ چپ و راستِ هر خانهٔ هر ردیفِ دیدنی با سرستونِ همان ستون (≤ ۱px)
///   ۲) خطِ عمودیِ کنارِ هر خانه (‎PART_RightGridLine‎) سرِ لبهٔ ستونش (≤ ۱px)
///   ۳) خانهٔ ستون‌دارِ نوارِ «جمله» با لبه‌های سرستونش (≤ ۱px)
///   ۴) جابه‌جاییِ وسط‌چینیِ ‎RtlTrim‎ همانی است که از چیدمانِ امروز درمی‌آید (≤ ۲px)
///   ۵) جوهرِ هر نوشتهٔ خانه و سرستون با پیکسلِ واقعیِ قاب وسطِ کادرش (≤ ۳px)
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- monthshift
/// </summary>
internal static class MonthShiftProbe
{
    private static int _bad;
    private static int _checks;

    private static void Check(string what, bool ok, string? detail = null)
    {
        _checks++;
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private const string Old = "1404/03";
    private static string _many = "", _few = "";

    public static int Run(string[] args)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pump-mshift-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        //  ⚠️ تنظیماتِ همین اجرا، جدا — وگرنه پهنای به‌یادماندهٔ اجرای پیشین سبزِ دروغ می‌داد
        PumpYaqobi.App.Services.AppSettings.DirOverride = dir;
        AppHost.Start(System.IO.Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        //  ‎MS_FULL=1‎: ماهِ جاری پر است. پیش‌فرض همان حالِ واقعیِ اولِ ماه است:
        //  ماهِ تازه خالی، و برنامه دیروز هم با همین دفترها کار کرده بود.
        var full = Environment.GetEnvironmentVariable("MS_FULL") == "1";
        var w0 = NewWindow();
        var vm0 = (MainViewModel)w0.DataContext!;
        if (full) Seed.Fill(AppHost.Current);
        SeedMonths(AppHost.Current);
        var sections = new[] { "expenses", "safe", "sarrafi", "chakana" };
        var only = args.Skip(1).FirstOrDefault();

        if (!full)
        {
            //  «دیروز»: همان بخش‌ها با ماهی که ردیف دارد دیده شدند — بعد برنامه بسته شد
            Console.WriteLine("════ دیروز: هر بخش یک بار با ماهِ پر، بعد بستنِ برنامه ════");
            foreach (var id in sections)
            {
                if (only is not null && only != id) continue;
                var s0 = Open(w0, vm0, id);
                Pick(w0, s0, _few);
                Settle(w0);
            }
            w0.Close();
            Pump(w0);
            //  پنجرهٔ تازه همه‌چیز را از دیسک بخواند، نه از حافظهٔ همین پروسه
            (typeof(ExcelGrid).GetField("AutoMem", BindingFlags.Static | BindingFlags.NonPublic)?
                .GetValue(null) as System.Collections.IDictionary)?.Clear();
        }

        var win = full ? w0 : NewWindow();
        var vm = (MainViewModel)win.DataContext!;
        foreach (var id in sections)
        {
            if (only is not null && only != id) continue;
            Console.WriteLine();
            Console.WriteLine($"════ {id} ════");
            OneSection(win, vm, id);
        }

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? $"✅ {_checks} سنجه، همه سرِ جایش" : $"❌ {_bad} ایراد از {_checks} سنجه");
        return _bad == 0 ? 0 : 1;
    }

    private static MainWindow NewWindow()
    {
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        LockIn.Wait(((MainViewModel)win.DataContext!).Lock);
        Settle(win);
        return win;
    }

    private static void SeedMonths(AppHost h)
    {
        var y = Shamsi.ThisMonth()[..4];
        var m = int.Parse(Shamsi.ThisMonth()[5..]);
        //  دو ماهِ همین سال، پیش از ماهِ جاری
        _many = $"{y}/{Math.Max(1, m - 3):00}";
        _few = $"{y}/{Math.Max(1, m - 2):00}";
        for (var i = 1; i <= 120; i++)
        {
            var d = _many + "/" + (1 + i % 28).ToString("00");
            h.ExpenseLedger.AddAsync(new Expense { DateShamsi = d, Title = "مصرفِ " + i, Amount = 100m * i }).GetAwaiter().GetResult();
            h.SafeLedger.AddAsync(new SafeEntry { DateShamsi = d, Title = "فروش ورق — روز " + i, Amount = 1000m * i }).GetAwaiter().GetResult();
            h.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = d, Description = "حوالهٔ " + i, Amount = 10m * i, Rate = 1m }).GetAwaiter().GetResult();
            h.RetailLedger.AddAsync(new RetailRow { DateShamsi = d, Name = "مشتری " + i, Liters = i, PricePerLiter = 60m }).GetAwaiter().GetResult();
        }
        for (var i = 1; i <= 3; i++)
        {
            var d = _few + "/0" + i;
            h.ExpenseLedger.AddAsync(new Expense { DateShamsi = d, Title = "نان", Amount = 5m }).GetAwaiter().GetResult();
            h.SafeLedger.AddAsync(new SafeEntry { DateShamsi = d, Title = "بردگی", Amount = 5m }).GetAwaiter().GetResult();
            h.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = d, Description = "حواله", Amount = 5m, Rate = 1m }).GetAwaiter().GetResult();
            h.RetailLedger.AddAsync(new RetailRow { DateShamsi = d, Name = "علی", Liters = 1m, PricePerLiter = 60m }).GetAwaiter().GetResult();
        }
        const string longText = "هارون بابت نان و غیره — مصرفِ ماهِ پیش با توضیحِ بسیار بلندتر از همیشه 12";
        foreach (var t in new[] { longText, "کریم ", "حوالهٔ 12 ", "500 افغانی " })
        {
            var d = Old + "/12";
            h.ExpenseLedger.AddAsync(new Expense { DateShamsi = d, Title = t, Amount = 123456789m }).GetAwaiter().GetResult();
            h.SafeLedger.AddAsync(new SafeEntry { DateShamsi = d, Title = t, Amount = 987654321m }).GetAwaiter().GetResult();
            h.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = d, Description = t, Amount = 5555555m, Rate = 1m }).GetAwaiter().GetResult();
            h.RetailLedger.AddAsync(new RetailRow { DateShamsi = d, Name = t, Liters = 22222m, PricePerLiter = 60m }).GetAwaiter().GetResult();
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
        Settle(win);
        _base = null;
        Measure(win, $"{s.Title} (باز شدن، ماهِ جاری)");

        var thisMonth = Shamsi.ThisMonth();
        foreach (var (key, label) in new[]
                 {
                     (_many, "ماهِ ۱۲۰ ردیفی"), (_few, "ماهِ ۳ ردیفی"), (Old, "سالِ پیش، نوشتهٔ بلند"),
                     (thisMonth, "برگشت به ماهِ جاری"), ("*", "همهٔ ماه‌های امسال"), (thisMonth, "دوباره ماهِ جاری"),
                 })
        {
            Pick(win, s, key);
            //  همان چند قابی که کاربر پس از کلیک می‌بیند — بی ‎Settle‎ی بلند
            for (var f = 0; f < 4; f++) Frame(win);
            Measure(win, $"{s.Title} ⇒ {label} (همان لحظه)", pixels: false);
            Settle(win);
            Measure(win, $"{s.Title} ⇒ {label} (پس از ته‌نشینی)");
        }
    }

    /// <summary>با خودِ کشوهای سال و ماهِ صفحه، مثلِ کلیکِ کاربر.</summary>
    private static void Pick(MainWindow win, SectionViewModel s, string key)
    {
        var combos = win.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.IsEffectivelyVisible && c.Items.OfType<YearMonthItem>().Any()).ToList();
        var yearBox = combos.FirstOrDefault(c => c.Items.OfType<YearMonthItem>().Any(i => i.Key.Length == 4));
        var monthBox = combos.FirstOrDefault(c => !ReferenceEquals(c, yearBox));
        if (monthBox is null) { Check("کشوی ماه پیدا شد", false); return; }
        var y = key == "*" ? Shamsi.ThisMonth()[..4] : key[..4];
        if (yearBox?.SelectedItem is YearMonthItem cy && cy.Key != y
            && yearBox.Items.OfType<YearMonthItem>().FirstOrDefault(i => i.Key == y) is { } yi)
        {
            yearBox.SelectedItem = yi;
            WaitRows(win);
        }
        var want = key == "*" ? y + YearMonthPicker.AllMark : key;
        if (monthBox.Items.OfType<YearMonthItem>().FirstOrDefault(i => i.Key == want) is { } mi)
        {
            if (!Equals(monthBox.SelectedItem, mi)) monthBox.SelectedItem = mi;
        }
        else Check($"ماهِ {want} در کشو هست", false);
        WaitRows(win);
    }

    /// <summary>تا خواندنِ ردیف‌ها (روی نخِ دیگر) تمام شود — قاب‌به‌قاب، بی ‎UpdateLayout‎ی اضافه.</summary>
    private static void WaitRows(MainWindow win)
    {
        for (var i = 0; i < 60; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(3); }
    }

    /// <summary>یک قابِ برنامه: کارهای صف، یک پاسِ چیدمان، کشیدن.</summary>
    private static void Frame(Window w)
    {
        Dispatcher.UIThread.RunJobs();
        w.UpdateLayout();
        w.CaptureRenderedFrame()?.Dispose();
    }

    private static readonly PropertyInfo? CellCol =
        typeof(DataGridCell).GetProperty("OwningColumn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly PropertyInfo? HeadCol =
        typeof(DataGridColumnHeader).GetProperty("OwningColumn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    /// <summary>جای سرستون‌ها هنگامِ باز شدن — خط‌ها با عوض شدنِ ماه نباید بپرند.</summary>
    private static Dictionary<string, (double L, double R)>? _base;

    private static void Measure(Window win, string what, bool pixels = true)
    {
        var g = win.GetVisualDescendants().OfType<ExcelGrid>()
                   .Where(x => x.IsEffectivelyVisible && x.Bounds.Width > 50)
                   .OrderByDescending(x => x.Bounds.Width * Math.Min(x.Bounds.Height, 2000)).FirstOrDefault();
        if (g is null) { Check(what + ": جدول پیدا شد", false); return; }
        AppHost.Current.Toasts.Visible = false;
        var bad = new List<string>();

        //  سرستون‌ها، به ستون
        var heads = new Dictionary<DataGridColumn, (double L, double R)>();
        foreach (var h in g.GetVisualDescendants().OfType<DataGridColumnHeader>().Where(h => h.IsEffectivelyVisible && h.Bounds.Width > 1))
            if (HeadCol?.GetValue(h) is DataGridColumn c) heads[c] = Edges(h, g);

        //  ۰) خطِ ستون‌ها همان جایی است که پیش از عوض شدنِ ماه بود
        var now = heads.ToDictionary(kv => kv.Key.Header?.ToString() ?? "", kv => kv.Value);
        var gx = g.TranslatePoint(default, win)!.Value.X;
        if (_base is null) _base = now;
        else
            foreach (var (k, e) in now)
                if (_base.TryGetValue(k, out var b0) && Math.Max(Math.Abs(b0.L - e.L), Math.Abs(b0.R - e.R)) > 1)
                    bad.Add($"خطِ ستونِ «{k}» جابه‌جا شد: {b0.L:0}..{b0.R:0} ⇒ {e.L:0}..{e.R:0}");

        //  ۱ و ۲) هر خانهٔ هر ردیفِ دیدنی، و خطِ کنارش
        var rows = g.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsEffectivelyVisible && InView(r, win)).ToList();
        int cellN = 0;
        foreach (var r in rows)
            foreach (var cell in r.GetVisualDescendants().OfType<DataGridCell>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 1))
            {
                if (CellCol?.GetValue(cell) is not DataGridColumn col || !heads.TryGetValue(col, out var he)) continue;
                cellN++;
                var ce = Edges(cell, g);
                var d = Math.Max(Math.Abs(ce.L - he.L), Math.Abs(ce.R - he.R));
                if (d > 1) bad.Add($"خانهٔ «{col.Header}» ردیفِ {r.Index + 1}: {d:0.#}px از سرستونش");
                if (cell.GetVisualDescendants().OfType<Rectangle>().FirstOrDefault(x => x.Name == "PART_RightGridLine") is { IsEffectivelyVisible: true } line
                    && line.Bounds.Width > 0)
                {
                    var le = Edges(line, g);
                    //  خط در لبهٔ دورِ خانه است (چپ یا راست، بسته به جهت)
                    var dl = Math.Min(Math.Abs(le.L - he.L), Math.Abs(le.R - he.R));
                    if (dl > 1) bad.Add($"خطِ کنارِ «{col.Header}» ردیفِ {r.Index + 1}: {dl:0.#}px از لبهٔ ستون");
                }
            }

        //  ۳) نوارِ «جمله»
        var strip = win.GetVisualDescendants().OfType<TotalsStrip>().FirstOrDefault(t => t.IsEffectivelyVisible
                        && ReferenceEquals(BarGrid(t.FindAncestorOfType<TotalsBar>()), g));
        int totN = 0;
        if (strip is not null)
            foreach (var ch in strip.Children.OfType<Control>().Where(c => c.IsVisible))
            {
                if (ch.DataContext is not TotalCell tc || string.IsNullOrEmpty(tc.Column)) continue;
                var col = heads.Keys.FirstOrDefault(c => (c.Header?.ToString()?.Trim() ?? "") == tc.Column);
                if (col is null) continue;
                totN++;
                var te = Edges(ch, g);
                var he = heads[col];
                var d = Math.Max(Math.Abs(te.L - he.L), Math.Abs(te.R - he.R));
                if (d > 1) bad.Add($"جملهٔ «{tc.Column}»: {d:0.#}px از سرستونش");
            }

        //  ۴) جابه‌جاییِ وسط‌چینیِ ‎RtlTrim‎ کهنه نیست
        int fixN = 0;
        foreach (var tb in g.GetVisualDescendants().OfType<TextBlock>()
                    .Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text) && t.Bounds.Width > 4)
                    .Where(t => t.TextAlignment == TextAlignment.Center || RtlTrim.GetCenter(t) || RtlTrim.GetEnabled(t)))
        {
            if (tb.FindAncestorOfType<DataGridRow>() is { } rr && !rows.Contains(rr)) continue;
            fixN++;
            var dx = RtlTrim.CenterFix(tb);
            var want = tb.FlowDirection == FlowDirection.RightToLeft ? -dx : dx;
            var have = (tb.RenderTransform as TranslateTransform)?.X ?? 0;
            if (Math.Abs(want - have) > 2) bad.Add($"وسط‌چینیِ کهنهٔ «{tb.Text}»: دارد {have:0.#} باید {want:0.#}");
        }

        //  ۵) جوهرِ واقعی
        int inkN = 0;
        if (pixels)
        {
            win.CaptureRenderedFrame()?.Dispose();
            using var shot = win.CaptureRenderedFrame()!;
            using var ms = new MemoryStream();
            shot.Save(ms);
            ms.Position = 0;
            using var frame = SkiaSharp.SKBitmap.Decode(ms);
            foreach (var h in g.GetVisualDescendants().OfType<DataGridColumnHeader>().Where(h => h.IsEffectivelyVisible && h.Bounds.Width > 4))
                foreach (var tb in h.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
                {
                    inkN++;
                    if (OldMonthProbe.Off(tb, h, frame) is { } o && o > 3) bad.Add($"جوهرِ سرستونِ «{tb.Text}» {o:0}px");
                }
            foreach (var r in rows)
                foreach (var cell in r.GetVisualDescendants().OfType<DataGridCell>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 4))
                    foreach (var tb in cell.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
                    {
                        inkN++;
                        if (OldMonthProbe.Off(tb, cell, frame) is { } o && o > 3) bad.Add($"جوهرِ «{tb.Text}» {o:0}px");
                    }
        }

        Check($"{what}: {rows.Count} ردیف · {cellN} خانه · {totN} جمله · {fixN} وسط‌چین" + (pixels ? $" · {inkN} جوهر" : ""),
              bad.Count == 0, bad.Count == 0 ? null : $"{bad.Count}: " + string.Join("، ", bad.Take(6)));
    }

    private static object? BarGrid(TotalsBar? b) =>
        b is null ? null : typeof(TotalsBar).GetMethod("ResolveGrid", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(b, null);

    private static bool InView(Visual v, Window win)
    {
        var p = v.TranslatePoint(default, win);
        return p is { } q && q.Y > -v.Bounds.Height && q.Y < win.Bounds.Height;
    }

    private static (double L, double R) Edges(Visual v, Visual to)
    {
        var a = v.TranslatePoint(default, to)!.Value.X;
        var b = v.TranslatePoint(new Point(v.Bounds.Width, 0), to)!.Value.X;
        return (Math.Min(a, b), Math.Max(a, b));
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
        w.CaptureRenderedFrame()?.Dispose();
        Pump(w);
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
