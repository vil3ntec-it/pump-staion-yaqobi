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
/// برای هر خانهٔ دیدنی: آن ‎TextLayout‎ی که <b>کشیده می‌شود</b> باید با پهنای همان
/// خانه ساخته شده باشد و خطِ وسط‌چینش وسطِ آن باشد (≤ ۱ واحد). همین دو عدد است که
/// اگر غلط باشد نوشته به لبه می‌چسبد.
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
        AppBuilder.Configure<PumpYaqobi.App.App>().UsePlatformDetect().SetupWithoutStarting();
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
        await (Task)add.Invoke(s, new object[] { id == "safe" ? 3 : 11 })!;
        await Idle();
        var prev = Prev();
        var last = $"{prev}/31";
        foreach (var r in Rows(s))
        {
            r.GetType().GetProperty("DateShamsi")!.SetValue(r, last);
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
                        var tl = tb.TextLayout;
                        if (tl is null || tl.TextLines.Count == 0) continue;
                        var inner = tb.Bounds.Width - tb.Padding.Left - tb.Padding.Right;
                        //  ⛔ کادرِ نوشته وسطِ خانه‌اش (خانه − ‎Margin‎ِ نوشته)، و نه پهن‌تر از آن
                        var room = cell.Bounds.Width - cell.Padding.Left - cell.Padding.Right - tb.Margin.Left - tb.Margin.Right;
                        var o = tb.TranslatePoint(default, cell)!.Value.X;
                        var e2 = tb.TranslatePoint(new Point(tb.Bounds.Width, 0), cell)!.Value.X;
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
        _checks++;
        Shot(win, tag);
        if (bad.Count == 0) { Console.WriteLine($"  ✔ {what}: {cells} خانه"); return; }
        _bad++;
        Console.WriteLine($"  ✖ {what}: {bad.Count} از {cells} خانه");
        foreach (var b in bad.Take(12)) Console.WriteLine("      " + b);
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
