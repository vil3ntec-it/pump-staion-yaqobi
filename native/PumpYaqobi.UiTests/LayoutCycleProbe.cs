using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «در دارک مود نوشته‌ها می‌روند سمتِ چپ، انگار انگلیسی شده» (۱۴۰۵/۰۷/۱۵) ══
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- layoutcycle [پوشهٔ عکس]
///         LC_SCALE=1.25   مقیاسِ نمایشگر (پیش‌فرض ۱٫۲۵ — ویندوزِ ۱۲۵٪)
///
/// ریشه در خودِ آوالونیا ۱۱.۲ است و خوانده شد، نه حدس زده شد:
///   ‎TextBlock.CreateTextLayout‎ ⇒ ‎IsMeasureValid ? TextAlignment : TextAlignment.Left‎
/// یعنی نوشته‌ای که پیش از اندازه‌گیریِ دوباره‌اش کشیده شود، **چپ‌چین** کشیده
/// می‌شود — و در پنجرهٔ راست‌به‌چپ «چپ» یعنی درست همان چیزی که صاحب ریپو دید.
/// و نوشته فقط وقتی پیش از اندازه‌گیری کشیده می‌شود که ‎LayoutManager‎ دورِ
/// ده‌گانه‌اش را تمام کند و هنوز کنترلی نااندازه مانده باشد: «Layout cycle
/// detected». تعویضِ تم همهٔ نوشته‌ها را با هم بی‌اعتبار می‌کند، پس همان لحظه
/// است که چرخه کلِ صفحه را می‌گیرد.
///
/// پس این سنجه دو چیز می‌شمارد، هر دو باید **صفر** باشند:
///   ۱) هشدارِ «Layout cycle» از خودِ آوالونیا (با نوعِ کنترلی که چرخید)
///   ۲) نوشتهٔ دیدنی‌ای که چیدمانِ متنش با چیزی جز ترازِ خودش ساخته شده
/// در هر بخش، در هر دو تم، با تعویضِ تم در همان بخش و در بخشِ دیگر.
/// </summary>
internal static class LayoutCycleProbe
{
    private static int _bad;
    private static readonly List<string> _cycles = new();

    private sealed class Sink : ILogSink
    {
        public bool IsEnabled(LogEventLevel level, string area) => area == LogArea.Layout && level >= LogEventLevel.Warning;
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Add(messageTemplate, null);
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
            => Add(messageTemplate, propertyValues);
        private static void Add(string t, object?[]? v)
        {
            if (!t.Contains("cycle", StringComparison.OrdinalIgnoreCase)) return;
            var item = v is { Length: > 0 } ? v[0] : null;
            var path = item is Visual vis
                ? string.Join(" ‹ ", vis.GetSelfAndVisualAncestors().Take(6).Select(a => a.GetType().Name + (a is StyledElement se && se.Name is { Length: > 0 } n ? "#" + n : "")))
                : item?.GetType().Name ?? "?";
            lock (_cycles) _cycles.Add(path);
        }
    }

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-layoutcycle");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-lc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        Logger.Sink = new Sink();
        PumpYaqobi.App.Controls.StaleTextGuard.Disabled = Environment.GetEnvironmentVariable("LC_NOGUARD") == "1";

        var scale = double.TryParse(Environment.GetEnvironmentVariable("LC_SCALE"), System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var sc) ? sc : 1.25;
        var win = new MainWindow { Width = 1536, Height = 820 };
        SetScaling(win, scale);
        win.Show();
        Round14Probe.Settle(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Round14Probe.Settle(win);
        Seed.Fill(AppHost.Current);
        Console.WriteLine($"مقیاس: {win.RenderScaling} · پنجره {win.Bounds.Width}×{win.Bounds.Height}");

        var only = Environment.GetEnvironmentVariable("LC_SECTIONS")?.Split(',');
        var ids = vm.NavSections.Select(s => s.Id)
                    .Where(id => id is not ("chat" or "account" or "camera"))
                    .Where(id => only is null || only.Contains(id)).ToList();

        //  ══ الف) همان راهِ صاحب ریپو: تم در یک بخش عوض می‌شود، ویندوز فریم می‌کشد
        //  (بخش‌های پنهان هم)، و بعد به بخشِ دیگر می‌رویم ══
        Console.WriteLine();
        Console.WriteLine("════ الف) تم در داشبورد، بعد رفتن به بخش‌های دیگر ════");
        foreach (var target in new[] { "storage", "safe", "rasid", "expenses" })
        {
            if (only is not null && !only.Contains(target)) continue;
            var t0 = vm.Sections.First(x => x.Id == target);
            Round14Probe.Wait(win, vm.GoAsync(t0));
            Round14Probe.Shot(win, shots, target + "-A-before");
            Round14Probe.Wait(win, vm.GoAsync(vm.Sections.First(x => x.Id == "dashboard")));
            vm.SelectedTheme = vm.SelectedTheme == PumpTheme.Blue ? PumpTheme.Gold : PumpTheme.Blue;
            for (var f = 0; f < 3; f++) { Dispatcher.UIThread.RunJobs(); using (win.CaptureRenderedFrame()) { } }
            //  نوشته‌هایی که همین حالا در بخشِ پنهان چپ‌چین کشیده شدند — با کشیدهٔ همان لحظه‌شان
            var drawnLeft = LeftDrawn(win).Select(tb => (tb, DrawListOf(tb))).ToList();
            Round14Probe.Wait(win, vm.GoAsync(t0));
            for (var f = 0; f < 2; f++) { Dispatcher.UIThread.RunJobs(); using (win.CaptureRenderedFrame()) { } }
            //  حالا دیده می‌شوند: هر کدام باید **دوباره کشیده** شده باشد، وگرنه همان
            //  کشیدهٔ چپ‌چین روی صفحه مانده — دقیقاً عکسِ صاحب ریپو.
            var stale = drawnLeft.Where(x => x.tb.IsEffectivelyVisible && x.tb.Bounds.Width > 0
                                             && ReferenceEquals(x.Item2, DrawListOf(x.tb)))
                                 .Select(x => x.tb).ToList();
            Check($"{target}: هر نوشته‌ای که در بخشِ پنهان چپ‌چین کشیده شد، با دیده شدن دوباره کشیده می‌شود",
                  stale.Count == 0,
                  $"{drawnLeft.Count} چپ‌چین در پنهان · {stale.Count} همان کشیدهٔ کهنه" +
                  (stale.Count > 0 ? " (" + string.Join("، ", stale.Take(4).Select(t => t.Text)) + ")" : ""));
            Round14Probe.Shot(win, shots, target + "-A-after-" + vm.SelectedTheme.Id);
        }

        foreach (var theme in new[] { PumpTheme.Blue, PumpTheme.Gold })
        {
            Console.WriteLine();
            Console.WriteLine($"════ تم: {theme.Id} ════");
            foreach (var id in ids)
            {
                var s = vm.Sections.First(x => x.Id == id);
                Round14Probe.Wait(win, vm.GoAsync(s));
                Step(win, id + " · باز شد");
                //  تعویضِ تم در همین بخش — همان کارِ صاحب ریپو
                //  نوشته‌ای که پیش از تم **از قبل** کج بود، یا متنش همین لحظه عوض شد
                //  (زنگِ هشدارِ داشبورد وقتی فهرست رسید)، گناهِ تم نیست — آن را بندِ «الف»
                //  می‌سنجد: با دیده شدن دوباره کشیده شود. این‌جا فقط کجیِ **تازهٔ** تم.
                var before = Snapshot(win);
                vm.SelectedTheme = theme == PumpTheme.Blue ? PumpTheme.Gold : PumpTheme.Blue;
                Frame(win, id + " · فریمِ پس از تم", before);
                Step(win, id + " · تم عوض شد");
                if (id is "storage" or "rasid" or "dashboard" or "safe")
                    Round14Probe.Shot(win, shots, $"{id}-{vm.SelectedTheme.Id}");
                vm.SelectedTheme = theme;
                Step(win, id + " · تم برگشت");
            }
        }

        Console.WriteLine();
        lock (_cycles)
        {
            foreach (var g in _cycles.GroupBy(x => x).OrderByDescending(g => g.Count()).Take(20))
                Console.WriteLine($"    ↻ {g.Count()}× {g.Key}");
            Check("هیچ «Layout cycle»ی نیست", _cycles.Count == 0, _cycles.Count + " هشدار");
        }
        Console.WriteLine(_bad == 0 ? "✅ همهٔ بندها سبزند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>
    /// یک قدم، مثلِ یک فریمِ واقعی: یک دورِ کارهای نخ و یک دورِ چیدمان — بعد
    /// پیش از هر چیدمانِ دیگری می‌پرسیم کدام نوشتهٔ دیدنی هنوز نااندازه است یا
    /// چیدمانِ متنش با ترازِ دیگری ساخته شده. این همان چیزی است که روی ویندوز
    /// کشیده می‌شود.
    /// </summary>
    private static void Step(Window win, string what)
    {
        var before = _cycles.Count;
        Dispatcher.UIThread.RunJobs();
        win.UpdateLayout();
        var wrong = WrongAligned(win);
        Round14Probe.Settle(win);
        var cyc = _cycles.Count - before;
        if (cyc > 0 || wrong.Count > 0)
            Check(what, false, $"چرخه {cyc} · نوشتهٔ کج {wrong.Count}" +
                                (wrong.Count > 0 ? " (" + string.Join("، ", wrong.Take(4)) + ")" : ""));
    }

    /// <summary>
    /// یک فریمِ واقعی بی هیچ چیدمانِ اضافه: ویندوز شصت بار در ثانیه می‌کشد و هر
    /// نوشتهٔ «کثیف» را — حتی در بخشِ پنهان — همان لحظه می‌کشد.
    /// </summary>
    /// <summary>پیش از تم: متنِ هر نوشته، و این‌که همین حالا با ترازِ خودش چیده شده یا نه.</summary>
    private static Dictionary<TextBlock, (string Text, bool Ok)> Snapshot(Window win)
    {
        var map = new Dictionary<TextBlock, (string, bool)>();
        foreach (var tb in win.GetVisualDescendants().OfType<TextBlock>())
        {
            var ok = LayoutField?.GetValue(tb) is TextLayout tl && ParaField?.GetValue(tl) is TextParagraphProperties pp
                     && pp.TextAlignment == tb.TextAlignment;
            map[tb] = (tb.Text ?? "", ok);
        }
        return map;
    }

    private static void Frame(Window win, string what, Dictionary<TextBlock, (string Text, bool Ok)> before)
    {
        Dispatcher.UIThread.RunJobs();
        using (win.CaptureRenderedFrame()) { }
        var hidden = 0; var left = new List<string>();
        foreach (var tb in win.GetVisualDescendants().OfType<TextBlock>())
        {
            if (LayoutField?.GetValue(tb) is not TextLayout tl || ParaField?.GetValue(tl) is not TextParagraphProperties pp) continue;
            if (pp.TextAlignment == tb.TextAlignment) continue;
            //  نوشته‌ای که هرگز چیده نشده (پنهان از روزِ اول) با نخستین دیده شدن
            //  قاب می‌گیرد و خودش دوباره کشیده می‌شود — آن مالِ این سنجه نیست.
            if (tb.Bounds.Width <= 0) continue;
            if (!before.TryGetValue(tb, out var b) || !b.Ok || b.Text != (tb.Text ?? "")) continue;
            if (!tb.IsEffectivelyVisible) hidden++;
            left.Add(tb.Text ?? "");
            if (Environment.GetEnvironmentVariable("LC_DEBUG") == "1" && left.Count <= 30)
                Console.WriteLine("      · «" + tb.Text + "» fg=" + tb.Foreground?.GetType().Name + " fs=" + tb.FontSize + " ‹ " +
                    string.Join(" ‹ ", tb.GetVisualAncestors().Take(5).Select(a => a.GetType().Name + (a is StyledElement se && se.Classes.Count > 0 ? "." + string.Join(".", se.Classes) : ""))));
        }
        if (left.Count > 0)
            Check(what, false, $"{left.Count} نوشته با ترازِ دیگر کشیده شد ({hidden} در بخشِ پنهان): " + string.Join("، ", left.Take(4)));
    }

    private static List<TextBlock> LeftDrawn(Window win)
    {
        var list = new List<TextBlock>();
        foreach (var tb in win.GetVisualDescendants().OfType<TextBlock>())
            if (LayoutField?.GetValue(tb) is TextLayout tl && ParaField?.GetValue(tl) is TextParagraphProperties pp
                && pp.TextAlignment != tb.TextAlignment && !tb.IsEffectivelyVisible)
                list.Add(tb);
        return list;
    }

    /// <summary>کشیدهٔ فعلیِ یک دیداری (‎CompositionDrawListVisual.DrawList‎) — عوض نشدنش یعنی دوباره کشیده نشده.</summary>
    private static object? DrawListOf(Visual v)
    {
        var comp = typeof(Visual).GetProperty("CompositionVisual", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(v);
        return comp?.GetType().GetProperty("DrawList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(comp);
    }

    /// <summary>صفِ «کثیف»های نقاشِ پنجره (‎CompositingRenderer._dirty‎).</summary>
    private static HashSet<Visual>? DirtySet(Window win)
    {
        var r = typeof(TopLevel).GetProperty("Renderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(win);
        return r?.GetType().GetField("_dirty", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(r) as HashSet<Visual>;
    }

    private static readonly FieldInfo? LayoutField = typeof(TextBlock).GetField("_textLayout", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? ParaField = typeof(TextLayout).GetField("_paragraphProperties", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static List<string> WrongAligned(Visual root)
    {
        var bad = new List<string>();
        foreach (var tb in root.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!tb.IsEffectivelyVisible || tb.Bounds.Width <= 0 || string.IsNullOrEmpty(tb.Text)) continue;
            if (!tb.IsMeasureValid) { bad.Add("نااندازه: " + tb.Text); continue; }
            if (LayoutField?.GetValue(tb) is not TextLayout tl || ParaField?.GetValue(tl) is not TextParagraphProperties pp) continue;
            if (pp.TextAlignment != tb.TextAlignment) bad.Add($"{pp.TextAlignment}≠{tb.TextAlignment}: {tb.Text}");
        }
        return bad;
    }

    /// <summary>
    /// مقیاسِ نمایشگرِ ویندوز (۱۲۵٪، ۱۵۰٪) در پنجرهٔ بی‌سر. ‎HeadlessWindowImpl‎
    /// مقیاس را فقط‌خواندنی و ۱ نگه می‌دارد؛ همان فیلد نوشته و خبرش داده می‌شود.
    /// </summary>
    internal static void SetScaling(Window win, double scale)
    {
        if (Math.Abs(scale - 1) < 0.001) return;
        var impl = win.PlatformImpl!;
        var f = impl.GetType().GetField("<RenderScaling>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f is null) { Console.WriteLine("  ⚠️ مقیاس عوض نشد (فیلد نیست)"); return; }
        f.SetValue(impl, scale);
        (impl.GetType().GetProperty("ScalingChanged")?.GetValue(impl) as Action<double>)?.Invoke(scale);
    }
}
