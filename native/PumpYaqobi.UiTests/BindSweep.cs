using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using PumpYaqobi.App;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «هیچ باگی مثلِ این نباشد» — هر دکمهٔ مرده در کلِ برنامه (۱۴۰۵/۰۷/۱۶) ══════
///
/// <code>dotnet run --project PumpYaqobi.UiTests -c Release -- bindsweep</code>
///
/// گزارشِ صاحب ریپو: «توی حسابِ قرض‌دار نمی‌ره… ببین هیچ باگی مثلِ این نباشه.»
/// ریشهٔ آن باگ: اتصالِ ‎Command‎ی یک دکمه با خطا تمام می‌شد و دکمه بی‌صدا هیچ
/// کاری نمی‌کرد. این سنجه همان نشانه را در <b>همهٔ</b> برنامه می‌گردد: هر بخش و
/// هر زیربخشِ نوار، و صفحه‌های درونی (حسابِ قرض‌دار، صفحهٔ شرکت، ورق، فاکتور)،
/// با دادهٔ نمونه؛ و هر خطای اتصالِ آوالونیا روی ‎Command‎ یا
/// ‎CommandParameter‎ را سرخ می‌کند. بقیهٔ خطاهای اتصال فقط شمرده و چاپ می‌شوند.
///
/// ⚠️ دو بار: یک بار پس از ساختن، یک بار پس از اسکرول تا ته — دکمه‌هایی که
/// بازیافت می‌شوند (‎CardGrid‎) همان‌جا خراب می‌شدند.
/// </summary>
public static class BindSweep
{
    private static readonly ConcurrentDictionary<string, int> Errors = new();
    private static string _where = "";

    private sealed class Sink : Avalonia.Logging.ILogSink
    {
        public bool IsEnabled(Avalonia.Logging.LogEventLevel level, string area) =>
            level >= Avalonia.Logging.LogEventLevel.Warning && area == Avalonia.Logging.LogArea.Binding;
        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string t) => Add(source, Array.Empty<object?>());
        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string t, params object?[] v) => Add(source, v);

        private static void Add(object? source, object?[] v)
        {
            var prop = v.Length > 0 ? v[0]?.ToString() ?? "" : "";
            var expr = v.Length > 1 ? v[1]?.ToString() ?? "" : "";
            var msg = v.Length > 0 ? v[^1]?.ToString() ?? "" : "";
            var key = $"{prop}|{expr}|{source?.GetType().Name}|{Trim(msg)}";
            Errors.AddOrUpdate(key, 1, (_, n) => n + 1);
            if (IsCommand(prop) && source is Button b) Suspects.Add((new WeakReference<Button>(b), key));
        }
    }

    private static readonly ConcurrentDictionary<string, string> Where = new();
    private static readonly ConcurrentBag<(WeakReference<Button> Button, string Key)> Suspects = new();

    /// <summary>
    /// «مرده» یعنی: خطای اتصالِ فرمان داشت، <b>جلوی چشم است</b> و فرمانش خالی است —
    /// همان نشانهٔ کارتِ قرض‌دار. دکمهٔ پنهان (مثلِ ‎A−/A+‎ی صفحه‌های درونی که عمداً
    /// با ‎FallbackValue=False‎ پنهان‌اند) مرده شمرده نمی‌شود.
    /// </summary>
    private static void CollectDead()
    {
        foreach (var (w, key) in Suspects)
            if (w.TryGetTarget(out var b) && b.IsEffectivelyVisible && b.Bounds.Width > 0 && b.Command is null
                && !Where.ContainsKey(key))
                Where[key] = _where + " · «" + (b.Content as string ?? b.GetType().Name) + "»";
    }
    private static bool IsCommand(string prop) => prop is "Command" or "CommandParameter";
    private static string Trim(string s) => s.Length > 160 ? s[..160] + "…" : s;

    public static int Run(string[] args)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-bind-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        Avalonia.Logging.Logger.Sink = new Sink();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Round14Probe.Settle(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Round14Probe.Settle(win);
        MarketingShots.Fill(AppHost.Current);
        Round14Probe.Settle(win);
        var scroll = win.FindControl<ScrollViewer>("PageScroll");

        var timings = new List<(string Where, long Ms, long Db)>();
        void Visit(string where, Action? open = null)
        {
            _where = where;
            var db0 = PumpYaqobi.Services.Data.DbWatch.Count;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { open?.Invoke(); } catch (Exception ex) { Errors.TryAdd($"|{where}|سنجه|{ex.GetType().Name}: {Trim(ex.Message)}", 1); }
            Round14Probe.Settle(win);
            sw.Stop();
            timings.Add((where, sw.ElapsedMilliseconds, PumpYaqobi.Services.Data.DbWatch.Count - db0));
            CollectDead();
            if (scroll is not null)
            {
                for (var y = 0.0; y <= scroll.Extent.Height; y += 600) { scroll.Offset = new Vector(0, y); Round14Probe.Settle(win); CollectDead(); }
                scroll.Offset = default;
                Round14Probe.Settle(win);
            }
        }

        foreach (var sec in vm.NavSections.ToList())
        {
            Visit(sec.Title, () => Round14Probe.Wait(win, vm.GoAsync(sec)));
            foreach (var sub in sec.SubSections.ToList())
            {
                Visit(sec.Title + " › " + sub.Title, () =>
                {
                    sec.OpenSub = sub;
                    if (vm.LastSubOpen is { } t) Round14Probe.Wait(win, t);
                });
                sec.OpenSub = null;
                Round14Probe.Settle(win);
            }
        }

        //  صفحه‌های درونی
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is DebtSectionViewModel debt)
        {
            Visit("قرض‌داران", () => Round14Probe.Wait(win, vm.GoAsync(debt)));
            var first = AppHost.Current.Debtors.ListAsync().GetAwaiter().GetResult().FirstOrDefault();
            if (first is not null)
            {
                Visit("حسابِ قرض‌دار", () => Round14Probe.Wait(win, debt.OpenPersonAsync(first.Id)));
                debt.CloseOpenPage();
            }
        }
        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is WaraqSectionViewModel waraq)
        {
            Visit("ورق‌ها", () => Round14Probe.Wait(win, vm.GoAsync(waraq)));
            if (waraq.Sheets.FirstOrDefault() is { } sheet)
            {
                Visit("ورق", () => waraq.OpenCommand.Execute(sheet));
                waraq.CloseOpenPage();
            }
        }
        if (vm.Sections.FirstOrDefault(s => s.Id == "invoices") is InvoiceSectionViewModel inv)
        {
            Visit("فاکتورها", () => Round14Probe.Wait(win, vm.GoAsync(inv)));
            Visit("فاکتورها › در صف", () => inv.OpenListCommand.Execute("pending"));
            if (inv.Rows.FirstOrDefault() is { } row) Visit("برگهٔ فاکتور", () => inv.OpenDetailCommand.Execute(row));
            inv.CloseListCommand.Execute(null);
        }
        Round14Probe.Settle(win);

        Console.WriteLine();
        Console.WriteLine("── کندترین باز شدن‌ها (وقت · دستورِ دیتابیس) ──");
        foreach (var t in timings.OrderByDescending(t => t.Ms).Take(12))
            Console.WriteLine($"  {t.Ms,6}ms  {t.Db,4} دستور  {t.Where}");
        var slow = timings.Where(t => t.Ms > 1500).ToList();

        //  خطای بی‌صاحب (‎Task‎ی که کسی نخواند، نخِ رابط) در ‎crash.log‎ِ همین پوشه
        var crashPath = Path.Combine(AppSettings.Dir, "crash.log");
        var crash = File.Exists(crashPath) ? File.ReadAllText(crashPath) : "";
        if (crash.Length > 0)
        {
            Console.WriteLine("── crash.log ──");
            Console.WriteLine(crash.Length > 6000 ? crash[..6000] + "…" : crash);
        }

        Console.WriteLine();
        var cmd = Errors.Where(e => IsCommand(e.Key.Split('|')[0])).ToList();
        foreach (var e in cmd.Where(e => !Where.ContainsKey(e.Key)))
            Console.WriteLine($"  · (پنهان یا گذرا، نه مرده) ×{e.Value} {e.Key}");
        cmd = cmd.Where(e => Where.ContainsKey(e.Key)).ToList();
        var other = Errors.Where(e => !IsCommand(e.Key.Split('|')[0])).OrderByDescending(e => e.Value).ToList();
        Console.WriteLine($"خطاهای اتصالِ دیگر (فقط گزارش): {other.Count} گونه");
        foreach (var e in other.Take(40)) Console.WriteLine($"  · ×{e.Value} {e.Key}");
        Console.WriteLine();
        var bad = 0;
        foreach (var t in slow) { Console.WriteLine($"✖ کند: {t.Where} — {t.Ms}ms"); bad++; }
        if (crash.Length > 0) { Console.WriteLine("✖ crash.log خالی نیست — خطای بی‌صاحب هنگامِ گشتن"); bad++; }
        if (cmd.Count == 0 && bad == 0)
        {
            Console.WriteLine("✅ هیچ دکمه‌ای در کلِ برنامه فرمانِ خالی/خطادار ندارد");
            return 0;
        }
        if (cmd.Count == 0) return 1;
        foreach (var e in cmd)
            Console.WriteLine($"✖ ×{e.Value} [{(Where.TryGetValue(e.Key, out var w) ? w : "?")}] {e.Key}");
        Console.WriteLine($"❌ {cmd.Count} گونه دکمهٔ مرده");
        return 1;
    }
}
