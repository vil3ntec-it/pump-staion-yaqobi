using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ آغازِ برنامه: لودینگ ← قفل ← برنامه ══════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «هنگام اجرای لودینگ، کاربر به بخش‌های مختلف برنامه منتقل
/// می‌شود، صفحات مختلف نمایش داده می‌شوند و در نهایت دوباره به صفحه قفل
/// برمی‌گردد.»
///
/// این سنجش همان شش سناریویی را می‌آزماید که خواسته شد:
///
///   ۱) اجرای سرد: لودینگِ ساده ⇐ صفحهٔ قفل
///   ۲) رمزِ درست: صفحهٔ اصلی، بی لودینگِ دوباره
///   ۳) رمزِ غلط: ماندن روی صفحهٔ قفل
///   ۴) جابه‌جایی بینِ همهٔ بخش‌ها: بی لودینگِ سراسری و بی برگشت به قفل
///   ۵) خروج و ورودِ دوباره: قفل حفظ می‌شود، لودینگ دوباره اجرا نمی‌شود
///   ۶) هیچ صفحهٔ محافظت‌شده‌ای پیش از رمز ساخته یا دیده نمی‌شود
///
///     dotnet run --project PumpYaqobi.UiTests -- warm
/// </summary>
internal static class WarmAudit
{
    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-warm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        var vm = (MainViewModel)win.DataContext!;
        var bad = new List<string>();

        // ⚠️ **پیش از** هر پمپی گوش می‌دهیم: پنجره گرم کردن را در سازنده‌اش صف
        // می‌کند و نخستین ‎RunJobs()‎ کلش را تا ته می‌بَرد. یک بار همین‌طور شد
        // و سنجش گفت «پرده اصلاً نیامد» در حالی که آمده و رفته بود.
        var sawSplash = false;
        var lockDuringSplash = false;
        var splashOpaque = true;
        var splashOnTop = true;
        var strayedDuringSplash = false;
        var phases = new List<string>();

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.Phase)) return;
            phases.Add(vm.Phase.ToString());
        };

        // ══ هر پاسِ چیدمان، پرده را بازرسی می‌کنیم ════════════════════════
        //
        // ⚠️ «پوسته دیده می‌شود؟» سنجهٔ درستی **نیست**: پوسته حینِ لودینگ عمداً
        // چیده می‌شود (وگرنه گرم نمی‌شود) و پردهٔ مات رویش است. چیزی که واقعاً
        // باید سنجیده شود این است که پرده جلوی همه‌چیز را بگیرد — و همان‌جا
        // بود که باگ نشست: پرده ‎Pump.Bg‎ی نبوده را می‌خواست، رنگش ‎null‎
        // می‌شد و کاملاً شفاف بود.
        //
        // پس دو چیزِ عینی سنجیده می‌شود: رنگِ پرده واقعاً مات است؟ و آخرین
        // فرزندِ ریشه است (یعنی روی همه)؟
        win.LayoutUpdated += (_, _) =>
        {
            if (!vm.IsStarting) return;
            sawSplash = true;
            if (vm.IsLockVisible) lockDuringSplash = true;

            // هیچ ناوبری‌ای نباید رخ داده باشد
            if (vm.Current is not null) strayedDuringSplash = true;

            // پرده را با نامش پیدا می‌کنیم، نه با حدس
            var splash = win.GetVisualDescendants().OfType<Border>()
                            .FirstOrDefault(b => b.Name == "Splash");
            if (splash?.Parent is not Panel root) { splashOpaque = false; return; }

            // ۱) رنگش واقعاً مات است؟ (‎Pump.Bg‎ی نبوده این‌جا گیر می‌افتد)
            var alphas = splash.Background switch
            {
                ISolidColorBrush sb => new[] { sb.Color.A },
                IGradientBrush gb => gb.GradientStops.Select(g => g.Color.A).ToArray(),
                _ => new byte[] { 0 },
            };
            if (alphas.Length == 0 || alphas.Any(a => a < 255)) splashOpaque = false;

            // ۲) هیچ چیزِ دیده‌شونده‌ای بعد از آن نیست؟ (یعنی روی همه است)
            var at = root.Children.IndexOf(splash);
            for (var k = at + 1; k < root.Children.Count; k++)
                if (root.Children[k].IsVisible) splashOnTop = false;
        };

        win.Show();

        // ── ۱) اجرای سرد ──────────────────────────────────────────────────
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 120_000)
        {
            Dispatcher.UIThread.RunJobs();
            win.UpdateLayout();
            if (vm.Phase != MainViewModel.AppPhase.Starting) break;
            Thread.Sleep(1);
        }
        sw.Stop();

        var all = vm.AllPages;

        Console.WriteLine();
        Console.WriteLine("۱) اجرای سرد");
        Console.WriteLine($"   پردهٔ لودینگ دیده شد؟           {Yn(sawSplash)}");
        Console.WriteLine($"   رنگِ پرده واقعاً مات است؟        {Yn(splashOpaque)}   (باید «بله» باشد)");
        Console.WriteLine($"   پرده روی همه‌چیز است؟           {Yn(splashOnTop)}   (باید «بله» باشد)");
        Console.WriteLine($"   ناوبری‌ای رخ داد؟               {Yn(strayedDuringSplash)}   (باید «نه» باشد)");
        Console.WriteLine($"   صفحهٔ قفل پشتش دیده شد؟         {Yn(lockDuringSplash)}   (باید «نه» باشد)");
        Console.WriteLine($"   پس از لودینگ رفت به            {vm.Phase}");
        Console.WriteLine($"   چند صفحه گرم شد                {vm.Warm.Warmed} از {all.Count}");
        Console.WriteLine($"   چقدر طول کشید                  {sw.ElapsedMilliseconds:N0} ms");

        if (!sawSplash) bad.Add("پردهٔ لودینگ اصلاً نیامد");
        if (!splashOpaque) bad.Add("پردهٔ لودینگ مات نیست — همه‌چیز از پشتش دیده می‌شود");
        if (!splashOnTop) bad.Add("پردهٔ لودینگ روی همه‌چیز نیست");
        if (strayedDuringSplash) bad.Add("حینِ لودینگ ناوبری رخ داد (Current عوض شد)");
        if (lockDuringSplash) bad.Add("صفحهٔ قفل حینِ لودینگ دیده می‌شد");
        if (vm.Phase != MainViewModel.AppPhase.Locked) bad.Add($"پس از لودینگ به {vm.Phase} رفت، نه قفل");
        if (vm.Warm.Warmed < all.Count) bad.Add($"فقط {vm.Warm.Warmed} صفحه از {all.Count} گرم شد");

        // ── ۶) هیچ چیزِ محافظت‌شده‌ای پیش از رمز ───────────────────────────
        var liveGrids = win.GetVisualDescendants().OfType<DataGrid>()
                           .Count(g => g.IsEffectivelyVisible);
        var liveRows = win.GetVisualDescendants().OfType<DataGridRow>()
                          .Count(r => r.IsEffectivelyVisible);
        Console.WriteLine();
        Console.WriteLine("۶) پیش از رمز، روی صفحه");
        Console.WriteLine($"   جدولِ دیده‌شونده                {liveGrids}   (باید صفر باشد)");
        Console.WriteLine($"   ردیفِ دیده‌شونده                {liveRows}   (باید صفر باشد)");
        if (liveGrids > 0) bad.Add($"{liveGrids} جدولِ محافظت‌شده پیش از رمز دیده می‌شود");
        if (liveRows > 0) bad.Add($"{liveRows} ردیفِ محافظت‌شده پیش از رمز دیده می‌شود");

        // ── ۳) رمزِ غلط ───────────────────────────────────────────────────
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);      // رمز را می‌سازد
        Pump(win);
        vm.SignOutCommand.Execute(null);
        Pump(win);

        vm.Lock.Password = "9999";
        vm.Lock.SubmitCommand.Execute(null);
        Pump(win);

        Console.WriteLine();
        Console.WriteLine("۳) رمزِ غلط");
        Console.WriteLine($"   حالا در                        {vm.Phase}   (باید قفل باشد)");
        if (vm.Phase != MainViewModel.AppPhase.Locked) bad.Add("با رمزِ غلط از قفل رد شد");

        // ── ۲) رمزِ درست ──────────────────────────────────────────────────
        var splashAgain = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Phase)
                && vm.Phase == MainViewModel.AppPhase.Starting) splashAgain = true;
        };

        vm.Lock.Password = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        for (var i = 0; i < 60; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }

        Console.WriteLine();
        Console.WriteLine("۲) رمزِ درست");
        Console.WriteLine($"   حالا در                        {vm.Phase}");
        Console.WriteLine($"   لودینگ دوباره آمد؟             {Yn(splashAgain)}   (باید «نه» باشد)");
        if (vm.Phase != MainViewModel.AppPhase.Ready) bad.Add("با رمزِ درست به صفحهٔ اصلی نرفت");
        if (splashAgain) bad.Add("پس از رمز، لودینگ دوباره آمد");

        // ── ۴) جابه‌جایی بینِ همهٔ بخش‌ها ──────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("۴) جابه‌جایی بینِ بخش‌ها");
        Console.WriteLine("بخش                              باز کردن   بازگشت به داشبورد");
        Console.WriteLine(new string('-', 70));

        const long goal = 400;
        var home = vm.Sections.First(s => s.Id == "dashboard");
        var strayed = false;

        foreach (var sec in vm.Sections)
        {
            var open = Time(() => { Wait(win, vm.GoAsync(sec)); Settle(win); });
            if (vm.Phase != MainViewModel.AppPhase.Ready) strayed = true;

            var back = ReferenceEquals(sec, home)
                ? 0
                : Time(() => { Wait(win, vm.GoAsync(home)); Settle(win); });
            if (vm.Phase != MainViewModel.AppPhase.Ready) strayed = true;

            var worst = Math.Max(open, back);
            Console.WriteLine($"{Pad(sec.Title, 32)} {open,8:N0} ms {back,14:N0} ms   "
                            + (worst <= goal ? "✔" : "✘"));
            if (open > goal) bad.Add($"{sec.Title}: باز کردن {open:N0} ms");
            if (back > goal) bad.Add($"{sec.Title} ← داشبورد: {back:N0} ms");
        }

        if (strayed) bad.Add("جابه‌جایی بینِ بخش‌ها برنامه را از حالتِ Ready بیرون برد");
        if (splashAgain) bad.Add("جابه‌جایی بینِ بخش‌ها لودینگِ سراسری آورد");

        // ── ۵) خروج و ورودِ دوباره ────────────────────────────────────────
        var warmedBefore = vm.Warm.Warmed;
        vm.SignOutCommand.Execute(null);
        Pump(win);
        var lockedAfterSignOut = vm.Phase == MainViewModel.AppPhase.Locked;

        vm.Lock.Password = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        for (var i = 0; i < 40; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }

        Console.WriteLine();
        Console.WriteLine("۵) خروج و ورودِ دوباره");
        Console.WriteLine($"   پس از خروج در                  {(lockedAfterSignOut ? "قفل" : "جای دیگر")}");
        Console.WriteLine($"   پس از ورودِ دوباره در          {vm.Phase}");
        Console.WriteLine($"   گرم کردن دوباره اجرا شد؟       {Yn(vm.Warm.Warmed != warmedBefore)}   (باید «نه» باشد)");
        if (!lockedAfterSignOut) bad.Add("خروج به صفحهٔ قفل برنگشت");
        if (vm.Phase != MainViewModel.AppPhase.Ready) bad.Add("ورودِ دوباره کار نکرد");
        if (vm.Warm.Warmed != warmedBefore) bad.Add("گرم کردن دوباره اجرا شد — یعنی هر ورود حافظه می‌گیرد");

        Console.WriteLine();
        Console.WriteLine("مسیرِ حالت‌ها: " + string.Join(" ← ", phases));
        Console.WriteLine($"حافظهٔ برنامه: {GC.GetTotalMemory(true) / 1024 / 1024:N0} MB");

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine("✅ لودینگ ← قفل ← برنامه، و هیچ‌کدام سرِ جای آن یکی نمی‌نشیند");
            return 0;
        }
        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    private static string Yn(bool b) => b ? "بله" : "نه";

    private static long Time(Action a)
    {
        var sw = Stopwatch.StartNew();
        a();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static string Pad(string s, int n) => s.Length >= n ? s[..n] : s + new string(' ', n - s.Length);

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(1); }
        Pump(w);
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 60; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            if (w.IsMeasureValid && w.IsArrangeValid) return;
        }
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
