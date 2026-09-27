using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «داخلِ ورق اسکرول می‌کنم پرپر می‌شود و گیر می‌کند، و اگر اسکرول نکنم
///     درجا درست می‌شود» (۱۴۰۵/۰۷/۱۵) ═════════════════════════════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- waraqscroll [پوشهٔ عکس]
///
/// یک ورقِ پُر باز می‌شود و با **چرخِ واقعیِ ماوس** گام‌به‌گام تا ته و برگشت
/// لغزانده می‌شود؛ پس از هر گام **فقط یک فریم** (نه ته‌نشینی) و بعد نگاه:
///   ۱) بلندیِ صفحه وسطِ لغزیدن عوض نشود (رشد/چیدمانِ دوباره = پرش)
///   ۲) جای اسکرول فقط به سمتِ چرخ برود، هیچ‌وقت برنگردد (= «گیر می‌کند»)
///   ۳) هر جدول دقیقاً به اندازهٔ همان گام جابه‌جا شود (= «پرپر»)
///   ۴) پس از ایستادن، هیچ چیزی خودش جابه‌جا نشود (= «درجا درست می‌شود»)
///   ۵) هر گام چند اندازه‌گیریِ ‎ExcelGrid‎ می‌خواهد (باید صفر باشد)
/// </summary>
internal static class WaraqScrollProbe
{
    private static int _bad;
    //  گامِ چرخ؛ صفحه‌لمسیِ ویندوز گام‌های کسری می‌فرستد (‎WQS_STEP=0.37‎) — آفستِ کسری
    private static readonly double Step = double.TryParse(Environment.GetEnvironmentVariable("WQS_STEP"),
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var st) ? st : 1;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-waraqscroll");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-wqs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        if (AppHost.Current.Auth.NeedsFirstRun()) AppHost.Current.Auth.CreateFirstAdmin("1234");
        FakeLicense.Grant();
        AppHost.Current.Session.SignIn(PumpYaqobi.Domain.Enums.UserRole.Admin, "سنجش");
        WaraqPerf.Seed(AppHost.Current);

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1366, Height = 740 };
        //  مقیاسِ نمایشگرِ ویندوز (پیش‌فرض ۱٫۲۵) — گردکردنِ چیدمان همان‌جا فرق دارد
        LayoutCycleProbe.SetScaling(win, double.TryParse(Environment.GetEnvironmentVariable("LC_SCALE"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sc) ? sc : 1.25);
        win.Show();
        Round14Probe.Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        LockIn.Wait(vm.Lock);
        var warmEnd = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < warmEnd)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        Round14Probe.Settle(win);

        var sec = vm.Sections.First(s => s.Id == "waraq");
        Round14Probe.Wait(win, vm.GoAsync(sec));
        var card = (sec.GetType().GetProperty("Cards")?.GetValue(sec) as System.Collections.IEnumerable)?
                   .Cast<object>().FirstOrDefault();
        var open = sec.GetType().GetProperty("OpenCardCommand")?.GetValue(sec) as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;
        if (card is null || open is null) { Console.WriteLine("✖ ورق پیدا نشد"); return 1; }
        Round14Probe.Wait(win, open.ExecuteAsync(card));
        Round14Probe.Settle(win);
        Round14Probe.Shot(win, shots, "waraq-top");

        var page = win.GetVisualDescendants().OfType<ScrollViewer>().First(v => v.Name == "PageScroll");
        //  ماوس هر بار روی یکی از سه جدول — چرخ از ‎ExcelGrid.OnPointerWheelChanged‎ رد می‌شود
        foreach (var (mid, spot) in new[] { (new Point(win.Width * 0.5, win.Height * 0.35), "جدولِ پایه‌ها"),
                                            (new Point(win.Width * 0.25, win.Height * 0.8), "جدولِ چپ"),
                                            (new Point(win.Width * 0.75, win.Height * 0.8), "جدولِ راست") })
        foreach (var (dir2, label0) in new[] { (-1.0, "پایین"), (1.0, "بالا") })
        {
            var label = spot + " · " + label0;
            Console.WriteLine();
            Console.WriteLine($"════ لغزیدن به {label}، یک فریم پس از هر چرخ ════");
            var h0 = page.Extent.Height;
            double hMin = h0, hMax = h0;
            var back = 0; var jitter = 0; var measures = 0; var steps = 0;
            var worstJitter = 0.0;
            var times = new List<double>();
            List<double>? lastWidths = null; var widthFlips = 0; var wrongFrames = 0; var wrongSample = new List<string>();
            for (var i = 0; i < 2000; i++)
            {
                var y0 = page.Offset.Y;
                var pos0 = GridTops(win);
                var m0 = PumpYaqobi.App.Controls.ExcelGrid.DiagMeasure;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                win.MouseWheel(mid, new Vector(0, dir2 * Step));
                Frame(win);
                times.Add(sw.Elapsed.TotalMilliseconds);
                steps++;
                measures += PumpYaqobi.App.Controls.ExcelGrid.DiagMeasure - m0;
                var dy = page.Offset.Y - y0;
                hMin = Math.Min(hMin, page.Extent.Height); hMax = Math.Max(hMax, page.Extent.Height);
                if (dir2 < 0 ? dy < -0.5 : dy > 0.5) back++;
                //  هر جدولی که هم پیش و هم پس از گام دیده می‌شود باید دقیقاً ‎−dy‎ جابه‌جا شود
                var pos1 = GridTops(win);
                foreach (var (g, top0) in pos0)
                    if (pos1.TryGetValue(g, out var top1))
                    {
                        var err = Math.Abs((top1 - top0) + dy);
                        if (err > 1.5) { jitter++; worstJitter = Math.Max(worstJitter, err); }
                    }
                //  خانه‌ها: پهنای هر ستون و ترازِ نوشته‌ها در همین فریم
                var cw = CellWidths(win);
                if (lastWidths is not null && !cw.SequenceEqual(lastWidths)) widthFlips++;
                lastWidths = cw;
                var wrong = LayoutCycleProbe.WrongAligned(win);
                if (wrong.Count > 0) { wrongFrames++; if (wrongSample.Count < 3) wrongSample.AddRange(wrong.Take(2)); }
                var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);
                if (dir2 < 0 ? page.Offset.Y >= max - 0.5 : page.Offset.Y <= 0.5) break;
            }
            Check($"{label}: بلندیِ صفحه وسطِ لغزیدن ثابت ماند", hMax - hMin < 1, $"{hMin:0} تا {hMax:0}");
            Check($"{label}: اسکرول هیچ‌وقت خلافِ چرخ برنگشت", back == 0, back + " بار در " + steps + " گام");
            Check($"{label}: جدول‌ها هم‌پای صفحه جابه‌جا شدند (بی پرپر)", jitter == 0,
                  jitter + " بار، بدترین " + worstJitter.ToString("0") + " پیکسل");
            times.Sort();
            var p95 = times.Count > 0 ? times[(int)(times.Count * 0.95)] : 0;
            Console.WriteLine($"    چیدمان {LayoutMs / Math.Max(1, times.Count):0.0}ms · کشیدن {RenderMs / Math.Max(1, times.Count):0.0}ms در هر فریم");
            LayoutMs = RenderMs = 0;
            Console.WriteLine($"    فریم‌ها: میانه {times[times.Count / 2]:0}ms · p95 {p95:0}ms · بیشینه {times[^1]:0}ms");
            Check($"{label}: پهنای ستون‌ها وسطِ لغزیدن عوض نشد", widthFlips == 0, widthFlips + " فریم");
            Check($"{label}: هیچ نوشته‌ای کج (چپ‌چینِ ناخواسته) کشیده نشد", wrongFrames == 0,
                  wrongFrames + " فریم" + (wrongSample.Count > 0 ? " (" + string.Join("، ", wrongSample) + ")" : ""));
            Check($"{label}: لغزیدن هیچ اندازه‌گیریِ جدولی نخواست", measures == 0, measures + " اندازه‌گیری در " + steps + " گام");

            //  ۴) ایستادیم — آیا چیزی خودش جابه‌جا می‌شود؟
            var still = GridTops(win); var yStill = page.Offset.Y; var hStill = page.Extent.Height;
            Round14Probe.Settle(win);
            var after = GridTops(win);
            var moved = still.Count(kv => after.TryGetValue(kv.Key, out var t) && Math.Abs(t - kv.Value) > 1);
            Check($"{label}: پس از ایستادن هیچ چیزی خودش جابه‌جا نشد",
                  moved == 0 && Math.Abs(page.Offset.Y - yStill) < 1 && Math.Abs(page.Extent.Height - hStill) < 1,
                  $"{moved} جدول · آفست {yStill:0}⇒{page.Offset.Y:0} · بلندی {hStill:0}⇒{page.Extent.Height:0}");
        }
        Round14Probe.Shot(win, shots, "waraq-end");

        Console.WriteLine(_bad == 0 ? "✅ ورق نرم و بی‌پرش می‌لغزد" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>یک فریمِ واقعی: یک دورِ کارهای نخ، یک چیدمان و یک کشیدن — نه ته‌نشینی.</summary>
    internal static double LayoutMs, RenderMs;
    private static void Frame(Window win)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        win.UpdateLayout();
        LayoutMs += sw.Elapsed.TotalMilliseconds; sw.Restart();
        using (win.CaptureRenderedFrame()) { }
        RenderMs += sw.Elapsed.TotalMilliseconds;
    }

    private static List<double> CellWidths(Window win) =>
        win.GetVisualDescendants().OfType<DataGridColumnHeader>()
           .Where(h => h.IsEffectivelyVisible).Select(h => Math.Round(h.Bounds.Width, 1)).ToList();

    private static Dictionary<DataGrid, double> GridTops(Window win)
    {
        var d = new Dictionary<DataGrid, double>();
        foreach (var g in win.GetVisualDescendants().OfType<DataGrid>())
        {
            if (!g.IsEffectivelyVisible || g.Bounds.Height <= 0) continue;
            var p = g.TranslatePoint(new Point(0, 0), win);
            if (p is { } q) d[g] = q.Y;
        }
        return d;
    }
}
