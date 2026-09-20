using System.Diagnostics;
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
/// ══ «هر بخش رو باز می‌کنم جدول‌ها یک ثانیه بعد میان» ═══════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۵): «برنامه کند باز می‌شه… و الان هر بخش رو باز
/// می‌کنم جدول‌ها یک ثانیه بعد میان که خیلی روی مخ من است و این مشکل از بیخ
/// باید درست بشه.»
///
/// <para>
/// ⚠️ <b>و این سنجه عمداً میلی‌ثانیه را ملاک نمی‌گیرد.</b> قاعدهٔ این ریپو از
/// ۱۴۰۵/۰۶/۲۷: «سنجهٔ اصلی «ردیفِ زنده» است، نه میلی‌ثانیه — وقت روی ماشینِ
/// CI نوسان دارد؛ عددِ ساختاری دروغ نمی‌گوید.» پس چیزی که این‌جا شمرده
/// می‌شود یک چیزِ ساختاری است:
/// </para>
///
/// <code>
///     «پس از اولین چیدمانِ بخشِ تازه، چند ردیف روی صفحه است؟»
/// </code>
///
/// صفر یعنی کاربر یک <b>جدولِ خالی</b> دید و ردیف‌ها بعداً آمدند — یعنی دقیقاً
/// همان چیزی که گزارش شد. عدد دروغ نمی‌گوید و به سرعتِ ماشین هم بند نیست.
///
/// <para>
/// ⛔ <b>و بازدیدِ دوم سنجیده می‌شود، نه اول.</b> بارِ اول داده باید از
/// SQLite خوانده شود و یک فریمِ خالی ناگزیر است. شکایتِ صاحب ریپو «هر بخش رو
/// باز می‌کنم» بود — یعنی رفت‌وآمد بینِ بخش‌ها، که داده‌اش از قبل خوانده شده.
/// همان‌جا بود که دو چیز ردیف‌ها را عقب می‌انداخت:
/// </para>
///
/// <list type="number">
///   <item><c>MainViewModel.GoAsync</c> «آخرین بخش» را با یک
///   <c>Save()</c>ِ بادوام (<c>Flush(true)</c> + <c>File.Replace</c>) روی
///   <b>نخِ رابط</b> می‌نوشت، درست بینِ نشان دادنِ صفحه و خواندنِ داده‌اش.</item>
///   <item><c>ExcelGrid</c> فهرستِ پارک‌شده‌اش را یک نوبتِ ‎Dispatcher‎
///   <b>دیرتر</b> برمی‌گرداند، پس یک چیدمانِ کاملِ خالی جلوتر می‌افتاد.</item>
/// </list>
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- sectionopen
/// </summary>
internal static class SectionOpen
{
    /// <summary>سقفِ زمانی — تورِ ایمنی است، نه ملاکِ اصلی.</summary>
    private const long Goal = 400;

    private static int _bad;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-sectionopen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");
        YearsAudit.Seed(file);

        AppHost.Start(file);
        //  با نصبِ پلن‌دار، وگرنه داشبورد و مفاد/ضرر و تاریخچه‌ها قفل‌اند
        FakeLicense.Grant();

        var host = AppHost.Current;
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        var vm = (MainViewModel)win.DataContext!;
        win.Show(); Pump(win);
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234";
        LockIn.Wait(vm.Lock);
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        Settle(win);

        //  بخش‌هایی که جدول دارند — همان‌هایی که شکایت درباره‌شان بود
        var ids = new[] { "safe", "sarrafi", "expenses", "noinv", "attendance" };
        var pages = ids
            .Select(id => vm.Sections.FirstOrDefault(s => s.Id == id))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        if (pages.Count < 2)
        {
            Console.WriteLine("❌ بخشِ جدول‌داری پیدا نشد — سنجه بی‌معنا است.");
            return 1;
        }

        //  ══ بارِ اول: همه یک بار باز می‌شوند تا داده‌شان خوانده شود ══
        foreach (var p in pages) { Wait(win, vm.GoAsync(p)); Settle(win); }

        Console.WriteLine();
        Console.WriteLine("بخش                    ردیف در فریمِ اول   ردیفِ نهایی   وقت   دستورِ دیتابیس");
        Console.WriteLine(new string('-', 78));

        //  ══ بازدیدِ دوم — همان کاری که کاربر می‌کند ══
        for (var i = 0; i < pages.Count; i++)
        {
            //  از بخشِ دیگری می‌آییم، وگرنه ‎GoAsync‎ مسیرِ «همین بخش» را می‌رود
            var other = pages[(i + 1) % pages.Count];
            Wait(win, vm.GoAsync(other)); Settle(win);

            var target = pages[i];
            var q0 = PumpYaqobi.Services.Data.DbWatch.Count;
            var sw = Stopwatch.StartNew();

            //  ⚠️ فقط **یک** پاسِ چیدمان: همان فریمِ اولی که کاربر می‌بیند.
            //  ‎RunJobs()‎ی بی‌پارامتر این‌جا ممنوع است — زنجیرهٔ
            //  چیدمان ⇄ تکهٔ رشد را یک‌جا می‌دواند و «فریمِ اول» را دروغ
            //  می‌کند (همان تله‌ای که در ‎ledgerperf‎ نوشته شده).
            var task = vm.GoAsync(target);
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
            win.UpdateLayout();
            var firstFrame = LiveRows(win);

            Wait(win, task);
            Settle(win);
            sw.Stop();

            var final = LiveRows(win);
            var queries = PumpYaqobi.Services.Data.DbWatch.Count - q0;

            //  ⛔ بخشی که اصلاً ردیفی ندارد سنجیده نمی‌شود — «صفر در فریمِ
            //  اول» آن‌جا درست است، نه ایراد.
            var judged = final > 0;
            var ok = !judged || firstFrame > 0;
            if (!ok) _bad++;
            if (sw.ElapsedMilliseconds > Goal) { _bad++; }

            Console.WriteLine($"{target.Title,-22} {firstFrame,12} {final,13} {sw.ElapsedMilliseconds,6:N0} ms {queries,10:N0}"
                + (judged ? (firstFrame > 0 ? "" : "   ✖ جدولِ خالی") : "   (بی ردیف)"));
        }

        Console.WriteLine();
        Console.WriteLine(_bad == 0
            ? "✅ هر بخش با ردیف‌هایش باز می‌شود — هیچ فریمِ خالی‌ای دیده نشد"
            : $"❌ {_bad} ایراد — جدول باید در **همان** فریمِ اول باشد، نه یک پاس بعد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>ردیف‌هایی که واقعاً در درختِ پنجره‌اند.</summary>
    private static int LiveRows(Window w) =>
        w.GetVisualDescendants().OfType<Avalonia.Controls.DataGridRow>().Count();

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }

    private static void Wait(Window w, Task t)
    {
        var sw = Stopwatch.StartNew();
        while (!t.IsCompleted && sw.ElapsedMilliseconds < 30_000)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(1); }
    }

    private static void Settle(Window w)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 200 && sw.ElapsedMilliseconds < 20_000; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
