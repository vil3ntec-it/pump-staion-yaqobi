using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ عکسِ ۱۴۰۵/۰۷/۲۲ روی پنجرهٔ واقعی — نه آوالونیای بی‌سر ══════════════════════════
///
/// گزارشِ صاحب ریپو با آخرین نسخه: در «مصارف» و «گاوصندوق» پس از رفتن به ماهِ پیش
/// (ردیف‌های خالی با تاریخ و «0») نوشتهٔ بعضی خانه‌ها به چپ می‌رود. ‎monthshift all‎
/// همین صحنه را در بی‌سر (لینوکس و ویندوز) سبز می‌دید. فرقِ بی‌سر با برنامهٔ واقعی:
/// آن‌جا سنجه خودش چیدمان را پیش از هر قاب ته‌نشین می‌کند؛ این‌جا حلقهٔ واقعیِ پنجره
/// (X11) و زمان‌سنجِ کشیدنِ قاب می‌دود و سنجه فقط مثلِ کاربر صبر می‌کند.
///
/// برای هر خانهٔ دیدنی دو سنجه: ساختاری (‎TextLayout‎ با پهنای همان خانه و خطِ وسط‌چینش
/// وسطِ آن) و <b>پیکسلی</b> — جوهرِ همان خانه از عکسِ واقعیِ صفحه (‎import‎). ⛔ دومی است که
/// باگ را گرفت: همهٔ اندازه‌ها درست بودند و صفحه کج، چون نقاشیِ کهنه دوباره کشیده نشده بود
/// (‎RtlTrim.Recenter‎). صحنهٔ عکس: ‎RS_ROWS=70 RS_POSTED=1‎ (ماهِ پیشِ پُر، هر سومی مثلِ
/// ردیفِ ثبت‌شده از ورق). ‎RS_DUMP=1‎ حالِ هر خانه را چاپ می‌کند، ‎RS_INV=1‎ پس از عکس همه را
/// دوباره می‌کشد و عکسِ دوم می‌گیرد، ‎RS_LOG=1‎ هشدارهای چیدمانِ آوالونیا.
///
///     xvfb-run -a -s "-screen 0 1600x1000x24" dotnet run --project PumpYaqobi.UiTests -c Release -- realshift [expenses|safe]
///     AVALONIA_GLOBAL_SCALE_FACTOR=1.5 … (مقیاسِ ۱۵۰٪)
/// </summary>
internal static class RealShiftProbe
{
    private static int _bad, _checks;
    private static string _shots = "";

    public static int Run(string[] args)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pump-realshift-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppSettings.DirOverride = dir;
        _shots = Environment.GetEnvironmentVariable("RS_SHOTS") ?? System.IO.Path.Combine(dir, "shots");
        Directory.CreateDirectory(_shots);
        var file = System.IO.Path.Combine(dir, "pump.db");
        AppHost.Start(file);
        FakeLicense.Grant();
        var ab = AppBuilder.Configure<PumpYaqobi.App.App>().UsePlatformDetect();
        if (Environment.GetEnvironmentVariable("RS_LOG") == "1") { System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Out)); ab = ab.LogToTrace(Avalonia.Logging.LogEventLevel.Warning, "Layout"); }
        ab.SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900, Position = new PixelPoint(0, 0) };
        win.Show();
        var only = args.Skip(1).FirstOrDefault();
        var cts = new CancellationTokenSource();
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                foreach (var id in new[] { "expenses", "safe" })
                    if (only is null || only == id) await Section(win, id);
            }
            catch (Exception e) { Console.WriteLine("✖ " + e); _bad++; }
            finally { cts.Cancel(); }
        });
        Dispatcher.UIThread.MainLoop(cts.Token);
        Console.WriteLine($"   عکس‌ها: {_shots}");
        if (_checks == 0) { Console.WriteLine("❌ هیچ سنجه‌ای ندوید"); return 1; }
        Console.WriteLine(_bad == 0 ? $"✅ {_checks} سنجه، همه سرِ جایش" : $"❌ {_bad} ایراد از {_checks} سنجه");
        return _bad == 0 ? 0 : 1;
    }

    private static async Task Idle(int ms = 700) => await Task.Delay(ms);

    private static async Task Section(MainWindow win, string id)
    {
        var vm = (MainViewModel)win.DataContext!;
        for (var i = 0; i < 600 && vm.Phase == MainViewModel.AppPhase.Starting; i++) await Task.Delay(50);
        await Idle(1500);
        AppHost.Current.Toasts.Visible = false;
        var s = vm.Sections.First(x => x.Id == id);
        await vm.GoAsync(s);
        await Idle();
        Console.WriteLine($"════ {s.Title} · مقیاس {win.RenderScaling} ════");
        //  ردیف‌های خالی در ماهِ جاری ⇒ تاریخشان آخرین روزِ ماهِ پیش، مثلِ عکس
        var add = s.GetType().GetMethod("AddRowsAsync")!;
        await (Task)add.Invoke(s, new object[] { int.TryParse(Environment.GetEnvironmentVariable("RS_ROWS"), out var nr) ? nr : id == "safe" ? 3 : 11 })!;
        await Idle();
        var prev = Prev();
        var last = $"{prev}/31";
        var j = 0;
        foreach (var r in Rows(s))
        {
            r.GetType().GetProperty("DateShamsi")!.SetValue(r, last);
            //  ‎RS_POSTED=1‎: هر ردیفِ سوم مثلِ ردیفی که از ورق ثبت شده (عنوانِ ایموجی‌دار + مبلغ) — همان ردیف‌های ۴/۷/۱۰ و ۲/۳ِ عکس
            if (Environment.GetEnvironmentVariable("RS_POSTED") == "1" && j++ % 3 == (id == "safe" ? 1 : 0))
            {
                r.GetType().GetProperty("Title")!.SetValue(r, $"📝 فروش ورق {last} — روز");
                r.GetType().GetProperty("Amount")!.SetValue(r, 48250m);
            }
            await r.FlushAsync();
        }
        //  ماهِ جاری با نوشته‌ها و عددهای بلند ⇒ پهنای ستون‌ها با عوض شدنِ ماه عوض می‌شود
        if (Environment.GetEnvironmentVariable("RS_WIDE") != "0")
        {
            await (Task)add.Invoke(s, new object[] { 6 })!;
            await Idle();
            var k = 0;
            foreach (var r in Rows(s).Where(r => (string)r.GetType().GetProperty("DateShamsi")!.GetValue(r)! != last))
            {
                k++;
                r.GetType().GetProperty("Title")!.SetValue(r, "هارون بابت نان و چای کارمندانِ شیفتِ شب و روز " + k);
                r.GetType().GetProperty("Amount")!.SetValue(r, 1234567.89m * k);
                r.GetType().GetProperty("Note")!.SetValue(r, "یادداشتِ بلند برای این ردیف که ستون را پهن می‌کند " + k);
                await r.FlushAsync();
            }
        }
        //  پهنای ستونی که کاربر کشیده (عکس: «نام»ِ گاوصندوق و «تاریخ»ِ مصارف پهن) — همان راهِ کشیدن
        if (Environment.GetEnvironmentVariable("RS_DRAG") == "1")
            foreach (var g in win.GetVisualDescendants().OfType<ExcelGrid>().Where(x => x.IsEffectivelyVisible && x.Bounds.Width > 50))
            {
                var cols = g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
                var room = cols.Sum(c => c.ActualWidth);
                var big = id == "safe" ? Math.Min(2, cols.Count - 1) : 0;
                for (var i = 0; i < cols.Count; i++)
                    cols[i].Width = new DataGridLength(i == big ? room * 0.42 : room * 0.58 / (cols.Count - 1), DataGridLengthUnitType.Pixel);
                await Idle(400);
                typeof(ExcelGrid).GetMethod("RememberWidths", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(g, null);
                await Idle(400);
                Console.WriteLine("   پهنای دستی: " + string.Join(" ", cols.Select(c => c.ActualWidth.ToString("0"))));
            }
        await vm.GoAsync(vm.Sections.First(x => x.Id == "dashboard"));
        await Idle();
        await vm.GoAsync(s);
        await Idle();
        var plan = new[] { prev, Shamsi.ThisMonth(), prev, "Y", prev, Shamsi.ThisMonth(), prev };
        var n = 0;
        foreach (var m in plan)
        {
            n++;
            if (m == "Y") await PickYearAll(win); else await PickMonth(win, m);
            await Idle(900);
            Check(win, $"{s.Title} ⇒ {m} (گامِ {n})", $"{id}-{n}");
        }
    }

    private static List<RowViewModel> Rows(object s) =>
        ((System.Collections.IEnumerable)s.GetType().GetProperty("Rows")!.GetValue(s)!).Cast<RowViewModel>().ToList();

    private static string Prev()
    {
        var y = int.Parse(Shamsi.ThisMonth()[..4]);
        var m = int.Parse(Shamsi.ThisMonth()[5..7]) - 1;
        if (m <= 0) { m += 12; y--; }
        return $"{y}/{m:00}";
    }

    private static (ComboBox? Y, ComboBox? M) Boxes(Window win)
    {
        var combos = win.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.IsEffectivelyVisible && c.Items.OfType<YearMonthItem>().Any()).ToList();
        var y = combos.FirstOrDefault(c => c.Items.OfType<YearMonthItem>().All(i => i.Key.Length is 0 or 4)
                                           && c.Items.OfType<YearMonthItem>().Any(i => i.Key.Length == 4));
        return (y, combos.FirstOrDefault(c => !ReferenceEquals(c, y)));
    }

    /// <summary>مثلِ کاربر: کشو باز ⇒ انتخاب ⇒ بسته.</summary>
    private static async Task PickMonth(Window win, string key)
    {
        var (_, mb) = Boxes(win);
        if (mb is null) { Console.WriteLine("   ◦ کشوی ماه نیست"); return; }
        var it = mb.Items.OfType<YearMonthItem>().FirstOrDefault(i => i.Key == key);
        if (it is null) { Console.WriteLine($"   ◦ ماهِ {key} در کشو نیست"); return; }
        mb.IsDropDownOpen = true;
        await Idle(300);
        mb.SelectedItem = it;
        mb.IsDropDownOpen = false;
    }

    private static async Task PickYearAll(Window win)
    {
        var (_, mb) = Boxes(win);
        var box = mb;
        var it = mb?.Items.OfType<YearMonthItem>().FirstOrDefault(i => i.Key.EndsWith(YearMonthPicker.AllMark));
        if (it is null) { Console.WriteLine("   ◦ «همهٔ ماه‌ها» نیست"); return; }
        box!.IsDropDownOpen = true;
        await Idle(300);
        box.SelectedItem = it;
        box.IsDropDownOpen = false;
    }

    private static void Check(Window win, string what, string tag)
    {
        var bad = new List<string>();
        var cells = 0;
        foreach (var g in win.GetVisualDescendants().OfType<ExcelGrid>().Where(x => x.IsEffectivelyVisible && x.Bounds.Width > 50))
            foreach (var row in g.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsEffectivelyVisible))
                foreach (var cell in row.GetVisualDescendants().OfType<DataGridCell>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 4))
                    foreach (var tb in cell.GetVisualDescendants().OfType<TextBlock>()
                                 .Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text) && t.TextAlignment == TextAlignment.Center))
                    {
                        cells++;
                        //  ⚠️ خانهٔ ستونِ آخر از کادرِ جدول پهن‌تر است و بریده می‌شود — آن‌چه دیده می‌شود سنجیده می‌شود
                        if (cell.TranslatePoint(new Point(0, 0), win) is { } c0 && cell.TranslatePoint(new Point(cell.Bounds.Width, cell.Bounds.Height), win) is { } c1
                            && g.TranslatePoint(new Point(0, 0), win) is { } g0 && g.TranslatePoint(new Point(g.Bounds.Width, g.Bounds.Height), win) is { } g1)
                        {
                            var cr = new Rect(new Point(Math.Min(c0.X, c1.X), Math.Min(c0.Y, c1.Y)), new Point(Math.Max(c0.X, c1.X), Math.Max(c0.Y, c1.Y)));
                            var gr = new Rect(new Point(Math.Min(g0.X, g1.X), Math.Min(g0.Y, g1.Y)), new Point(Math.Max(g0.X, g1.X), Math.Max(g0.Y, g1.Y)));
                            if (cr.Intersect(gr) is { Width: > 0 } vis && vis.Width >= cr.Width - 1)
                                _rects.Add((vis, row.Index + 1, tb.Text!));
                        }
                        var tl = tb.TextLayout;
                        if (tl is null || tl.TextLines.Count == 0) continue;
                        var inner = tb.Bounds.Width - tb.Padding.Left - tb.Padding.Right;
                        //  ⛔ کادرِ نوشته وسطِ خانه‌اش (خانه − ‎Margin‎ِ نوشته)، و نه پهن‌تر از آن
                        var room = cell.Bounds.Width - cell.Padding.Left - cell.Padding.Right - tb.Margin.Left - tb.Margin.Right;
                        //  جای چیدنِ کادر در پدر — بی ‎RenderTransform‎ِ تصحیحِ جوهرِ ‎RtlTrim‎ (آن عمدی است)
                        var par = (Visual)tb.GetVisualParent()!;
                        var o = par.TranslatePoint(new Point(tb.Bounds.X, 0), cell)!.Value.X;
                        var e2 = par.TranslatePoint(new Point(tb.Bounds.Right, 0), cell)!.Value.X;
                        var mid = (Math.Min(o, e2) + Math.Max(o, e2)) / 2;
                        if (tb.Bounds.Width > room + 1.5 || Math.Abs(mid - cell.Bounds.Width / 2) > 1.5)
                            bad.Add($"ردیفِ {row.Index + 1} «{tb.Text}»: کادرِ نوشته {tb.Bounds.Width:0.#} وسطش {mid:0.#} در خانهٔ {cell.Bounds.Width:0.#}");
                        var line = tl.TextLines[0];
                        if (line.Width >= inner - 1) continue;                    // پر از کادر
                        var want = (inner - line.Width) / 2;
                        var dx = (tb.RenderTransform as TranslateTransform)?.X ?? 0;
                        var off = Math.Abs(line.Start - want);
                        //  ⛔ آن‌چه کشیده می‌شود باید با پهنای همین خانه ساخته شده باشد
                        if (double.IsInfinity(tl.MaxWidth) || Math.Abs(tl.MaxWidth - inner) > 1 || off > 1 && !RtlTrim.IsRtl(tb.Text))
                            bad.Add($"ردیفِ {row.Index + 1} «{tb.Text}»: خطِ نوشته از {line.Start:0.#} (باید {want:0.#}) · پهنای چیدن {tl.MaxWidth:0.#} خانه {inner:0.#} · جابه‌جایی {dx:0.#}");
                        else if (Math.Abs(dx) > 3 && !RtlTrim.IsRtl(tb.Text))
                            bad.Add($"ردیفِ {row.Index + 1} «{tb.Text}»: جابه‌جاییِ {dx:0.#} روی نوشتهٔ لاتین");
                    }
        if (Environment.GetEnvironmentVariable("RS_DUMP") == "1")
            foreach (var t in win.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text is { } x && (x.StartsWith("1405/") || x == "48,250")))
            {
                var p0 = t.TranslatePoint(new Point(0, 0), win);
                var tl0 = t.TextLayout;
                var chain = string.Join("<", t.GetVisualAncestors().Take(6).Select(a => a.GetType().Name));
                Console.WriteLine($"      DUMP «{t.Text}» at {p0?.X:0},{p0?.Y:0} w={t.Bounds.Width:0.#} vis={t.IsEffectivelyVisible} mw={tl0?.MaxWidth:0.#} start={(tl0?.TextLines.Count > 0 ? tl0.TextLines[0].Start : -1):0.#} tr={(t.RenderTransform as TranslateTransform)?.X:0.#} ha={t.HorizontalAlignment} ta={t.TextAlignment} fd={t.FlowDirection} mv={t.IsMeasureValid}/{t.IsArrangeValid} {chain}");
            }
        _checks++;
        Shot(win, tag);
        bad.AddRange(PixelCheck(win, tag));
        _rects.Clear();
        if (bad.Count > 0 && !_reported.Contains(tag)) { _reported.Add(tag); _bad++; Console.WriteLine($"  ✖ {what} (پیکسل): {bad.Count}"); foreach (var b in bad.Take(12)) Console.WriteLine("      " + b); }
        if (Environment.GetEnvironmentVariable("RS_INV") == "1")
        {
            foreach (var t in win.GetVisualDescendants().OfType<TextBlock>()) t.InvalidateVisual();
            Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(600);
            Dispatcher.UIThread.RunJobs();
            Shot(win, tag + "-inv");
        }
        if (bad.Count == 0) { Console.WriteLine($"  ✔ {what}: {cells} خانه"); return; }
        _bad++;
        Console.WriteLine($"  ✖ {what}: {bad.Count} از {cells} خانه");
        foreach (var b in bad.Take(12)) Console.WriteLine("      " + b);
    }

    private static readonly List<(Rect r, int row, string text)> _rects = new();
    private static readonly HashSet<string> _reported = new();

    /// <summary>
    /// ⛔ آن‌چه واقعاً روی صفحه کشیده شده: جوهرِ هر خانه از عکسِ واقعیِ پنجره (‎import‎)، نه از
    /// ‎TextLayout‎. سنجهٔ ساختاری سبز بود و صفحه کج — نقاشیِ کهنه‌ای که دوباره کشیده نشده بود.
    /// </summary>
    private static List<string> PixelCheck(Window win, string tag)
    {
        var bad = new List<string>();
        var path = System.IO.Path.Combine(_shots, tag + ".png");
        //  ⛔ بی عکس سبز نیست — سنجه‌ای که نسنجید ایراد است، نه «همه سرِ جایش»
        if (!File.Exists(path)) { bad.Add("عکسِ صفحه گرفته نشد (‎import‎ نیست؟) — پیکسل‌ها سنجیده نشدند"); return bad; }
        using var bmp = new Avalonia.Media.Imaging.Bitmap(path);
        var w = bmp.PixelSize.Width; var h = bmp.PixelSize.Height;
        var buf = new byte[w * h * 4];
        var hnd = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { bmp.CopyPixels(new PixelRect(0, 0, w, h), hnd.AddrOfPinnedObject(), buf.Length, w * 4); }
        finally { hnd.Free(); }
        var sc = win.RenderScaling;
        var origin = win.PointToScreen(new Point(0, 0));
        foreach (var (r, row, text) in _rects)
        {
            //  حاشیه با مقیاس: خطِ دورِ جدول در ۱۵۰٪ از ۴ پیکسل کلفت‌تر است
            var m = (int)Math.Ceiling(6 * sc);
            int x0 = (int)(origin.X + r.X * sc) + m, x1 = (int)(origin.X + r.Right * sc) - m;
            int y0 = (int)(origin.Y + r.Y * sc) + m, y1 = (int)(origin.Y + r.Bottom * sc) - m;
            if (x0 < 0 || y0 < 0 || x1 >= w || y1 >= h || x1 - x0 < 10) continue;
            //  رنگِ زمینه = گوشهٔ خانه
            int bi = (y0 * w + x0) * 4;
            int lo = int.MaxValue, hi = int.MinValue;
            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    int i = (y * w + x) * 4;
                    var d = Math.Abs(buf[i] - buf[bi]) + Math.Abs(buf[i + 1] - buf[bi + 1]) + Math.Abs(buf[i + 2] - buf[bi + 2]);
                    if (d > 120) { if (x < lo) lo = x; if (x > hi) hi = x; }
                }
            if (hi <= lo) continue;
            var off = (lo + hi) / 2.0 - (x0 + x1) / 2.0;
            if (Math.Abs(off) > 3 * sc && hi - lo < (x1 - x0) - 6)
                bad.Add($"ردیفِ {row} «{text}»: جوهرِ کشیده‌شده {off / sc:0.#} از وسط");
        }
        return bad;
    }

    private static void Shot(Window win, string tag)
    {
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("import", $"-window root \"{System.IO.Path.Combine(_shots, tag + ".png")}\"")
                    { UseShellExecute = false });
            p?.WaitForExit(15000);
        }
        catch { }
    }
}
