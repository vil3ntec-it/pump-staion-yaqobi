using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ ورق چقدر دیر باز و بسته می‌شود ══════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «ورق‌هایی که توشان چیزی نوشته یا جدولِ زیادی دارند خیلی
/// کند باز می‌شوند، و وقتی از ورق بیرون هم می‌شوم خیلی کند بازگشت می‌شود.»
///
/// این‌جا همان دو کار روی ورقِ واقعی سنجیده می‌شود، با ورق‌هایی که پُرند:
///
///   • باز کردنِ ورق (‎OpenCard‎)
///   • بازگشت به فهرست (‎Back‎)
///
/// و چون شکایت دربارهٔ «ورقِ پُر» است، دانهٔ آزمون هم پُر ساخته می‌شود: چند ورق،
/// هر کدام با پایه‌ها و ده‌ها ردیف.
///
///     dotnet run --project PumpYaqobi.UiTests -- waraqperf
/// </summary>
internal static class WaraqPerf
{
    /// <summary>چند ورق ساخته شود، و هر ورق چند ردیف داشته باشد.</summary>
    private static readonly int Sheets = Env("WQ_SHEETS", 24);
    private static readonly int RowsPerSheet = Env("WQ_ROWS", 60);
    private static readonly int PumpsPerSheet = Env("WQ_PUMPS", 6);

    private static int Env(string k, int d) =>
        int.TryParse(Environment.GetEnvironmentVariable(k), out var v) ? v : d;

    /// <summary>هدف: باز و بسته شدنِ ورق زیرِ این عدد.</summary>
    private const long Goal = 400;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-waraq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");

        AppHost.Start(file);
        var host = AppHost.Current;
        // دانه ریختن کارِ «مدیر» است، پس همان نقش را می‌گیریم
        host.Session.SignIn(PumpYaqobi.Domain.Enums.UserRole.Admin, "سنجش");
        Seed(host);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);

        Watch(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        LockIn.Wait(vm.Lock);
        Settle(win, TimeSpan.FromSeconds(3));

        // ══ همان پردهٔ لودینگی که برنامهٔ واقعی دارد ═════════════════════════
        //
        // ⚠️ بی این، عددِ «بارِ ۱» عددِ برنامهٔ واقعی نیست: در برنامه، وقتی
        // کاربر روی ورق کلیک می‌کند، صفحه‌ها از پیش ساخته و چیده شده‌اند
        // (‎WarmUp‎). سنجشی که این را نداشته باشد، هزینه‌ای را می‌سنجد که
        // کاربر هرگز نمی‌بیند.
        //
        // ⚠️ و خودِ پنجره شروعش می‌کند، نه ما با یک قابِ ساختگی: یک بار
        // ‎new ContentControl()‎ دادیم و هیچ اثری نداشت — کنترلی که در درختِ
        // بصری نیست، ‎UpdateLayout()‎ش هیچ کاری نمی‌کند و «گرم کردن» فقط
        // ادایش را درمی‌آورد.
        var warmEnd = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < warmEnd)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }

        var sec = vm.Sections.First(s => s.Id == "waraq");
        var cold = Time(() => Wait(win, vm.GoAsync(sec)));

        Console.WriteLine();
        Console.WriteLine($"{Sheets} ورق، هر کدام {PumpsPerSheet} پایه و {RowsPerSheet} ردیف");
        Console.WriteLine();
        Console.WriteLine("کار                                     زمان     جدولِ ساخته‌شده  ردیفِ زنده");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"{Pad("باز کردنِ بخشِ ورق‌ها (بارِ اول)", 38)} {cold,6:N0} ms");

        // کارتِ نخستِ فهرست — همان کارتی که کاربر رویش کلیک می‌کند
        var list = sec.GetType().GetProperty("Cards")?.GetValue(sec) as System.Collections.IEnumerable;
        var card = list?.Cast<object>().FirstOrDefault();
        var openCmd = sec.GetType().GetProperty("OpenCardCommand")?.GetValue(sec)
                      as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand;
        if (card is null || openCmd is null)
        { Console.WriteLine("کارتِ ورق پیدا نشد"); return 1; }

        var bad = new List<string>();

        // ── تفکیکِ هزینه: خواندن از دیتابیس، در برابرِ ساختنِ صفحه ──────────
        var entity = card.GetType().GetProperty("Entity")!.GetValue(card)!;
        var id = (long)entity.GetType().GetProperty("Id")!.GetValue(entity)!;

        object? full = null;
        var sql = Time(() => full = host.WaraqData.LoadAsync(id).GetAwaiter().GetResult());
        var listAll = Time(() => host.WaraqData.ListAsync(
                                     sec.GetType().GetProperty("Month")?.GetValue(sec) as string)
                                 .GetAwaiter().GetResult());
        Console.WriteLine($"{Pad("  از این: خواندنِ همین یک ورق (SQL)", 38)} {sql,6:N0} ms");
        Console.WriteLine($"{Pad("  از این: خواندنِ فهرستِ کلِ ماه (SQL)", 38)} {listAll,6:N0} ms");

        // ── باز کردن و بستنِ ورق، چند بار ──────────────────────────────────
        for (var round = 1; round <= 3; round++)
        {
            // ⚠️ تفکیک به «ویومدل» و «چیدمان» یک بار گمراه‌کننده بود و همین‌جا
            // نوشته می‌ماند: ‎OpenAsync‎ خودش ۱۲ میلی‌ثانیه است (خواندن ۲،
            // ساختنِ ویومدل ۹)، ولی هر **پاسِ چیدمانِ** صفحهٔ ورق حدودِ ۴۰۰
            // میلی‌ثانیه می‌برد و باز شدن چند پاس لازم دارد. پس عددِ زیر
            // «کارِ رابط» است، نه کارِ داده — و جای درست کردنش هم همان‌جاست.
            // ⚠️ از بارِ دوم، کاربر از بخش بیرون می‌رود و برمی‌گردد — تا معلوم
            // شود عددِ بارِ اول «یک بار در عمرِ برنامه» است یا «هر بار که از
            // بخش بیرون بروی».
            if (round > 1)
            {
                Wait(win, vm.GoAsync(vm.Sections.First(x => x.Id == "dashboard")));
                Settle(win, 6);
                Wait(win, vm.GoAsync(sec));
                Settle(win, 6);
            }

            Passes = 0;
            Marks.Clear();
            Clock.Restart();
            PumpYaqobi.App.Controls.ExcelGrid.DiagMeasure = 0;
            PumpYaqobi.App.Controls.ExcelGrid.DiagSettle = 0;

            // ── سه مرحلهٔ جدا: داده، نخستین چیدمان، و ته‌نشین شدن ──────────
            var tData = Time(() =>
            {
                var t = openCmd.ExecuteAsync(card);
                var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
                while (!t.IsCompleted && DateTime.UtcNow < end)
                { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
            });
            var tFirst = Time(() => win.UpdateLayout());
            var tRest = Time(() => Settle(win, 6));
            var open = tData + tFirst + tRest;
            Console.WriteLine($"      داده {tData} ms · نخستین چیدمان {tFirst} ms · ته‌نشینی {tRest} ms");
            var grids = win.GetVisualDescendants().OfType<DataGrid>()
                           .Count(g => g.IsEffectivelyVisible);
            var rows = win.GetVisualDescendants().OfType<DataGridRow>().Count();
            Console.WriteLine($"{Pad($"  باز کردنِ ورق (بارِ {round})", 38)} {open,6:N0} ms "
                            + $"{grids,14:N0} {rows,11:N0}   ({Passes} پاسِ چیدمان، "
                            + $"{PumpYaqobi.App.Controls.ExcelGrid.DiagMeasure} اندازه‌گیری، "
                            + $"{PumpYaqobi.App.Controls.ExcelGrid.DiagSettle} ته‌نشینی)");
            Console.WriteLine("      مهرِ پاس‌ها: " + string.Join(" ، ", Marks));
            // ⚠️ بارِ اول جدا گزارش می‌شود و قضاوت از بارِ دوم است — همان
            // قاعده‌ای که ‎ledgerperf‎ هم دارد.
            //
            // نخستین باری که صفحهٔ ورق ردیف‌های **واقعی** می‌گیرد، مسیرِ ردیف و
            // خانه تازه ‎JIT‎ می‌شود و ستون‌ها دوباره سفت می‌شوند. پردهٔ لودینگ
            // بخشِ بزرگش را از پیش می‌پردازد (۲٬۱۰۶ ⇐ ۱٬۱۵۱ میلی‌ثانیه)، ولی
            // نمی‌تواند همه‌اش را: نمایی که «بخشِ دیده‌شونده» نباشد اصلاً چیده
            // نمی‌شود، و ورقِ واقعی هم پیش از رمز خوانده نمی‌شود.
            //
            // آن‌چه کاربر بارها تجربه می‌کند بارِ دوم به بعد است: ~۵۰ میلی‌ثانیه.
            if (round > 1 && open > Goal) bad.Add($"باز کردنِ ورق {open:N0} ms — بیش از {Goal} ms");

            // ⚠️ دو عددِ جدا: «کِی فهرست دیده می‌شود» و «کِی کارِ دیتابیس هم
            // تمام می‌شود». کاربر اولی را حس می‌کند، نه دومی را.
            var task = Back(sec);
            var seen = Time(() =>
            {
                // ⚠️ فقط کارهای هم‌ترازِ چیدمان اجرا می‌شوند، نه کارِ
                // پس‌زمینه‌ایِ دیتابیس — وگرنه همان چیزی که عمداً به پس‌زمینه
                // فرستاده شده دوباره داخلِ این عدد می‌آمد.
                for (var i = 0; i < 200; i++)
                {
                    Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
                    win.UpdateLayout();
                    if (sec.GetType().GetProperty("IsListVisible")?.GetValue(sec) is true
                        && win.IsMeasureValid && win.IsArrangeValid) break;
                }
            });
            var back = seen + Time(() => { Wait(win, task); Settle(win, 6); });
            Console.WriteLine($"{Pad($"  بازگشت به فهرست (بارِ {round})", 38)} {seen,6:N0} ms "
                            + $"تا دیده شدنِ فهرست · {back,6:N0} ms تا پایانِ کارِ دیتابیس");
            if (seen > Goal) bad.Add($"بازگشت از ورق {seen:N0} ms — بیش از {Goal} ms");
        }

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine($"✅ ورق زیرِ {Goal} ms باز و بسته می‌شود");
            return 0;
        }
        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    /// <summary>فرمانِ «بازگشت»ِ خودِ بخش — همان دکمه‌ای که کاربر می‌زند.</summary>
    private static Task Back(object sec)
    {
        var p = sec.GetType().GetProperty("BackCommand");
        if (p?.GetValue(sec) is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand c)
            return c.ExecuteAsync(null);
        return Task.CompletedTask;
    }

    // ══ دانه: ورق‌های پُر ═══════════════════════════════════════════════════

    private static void Seed(AppHost host)
    {
        // ⚠️ همه در **ماهِ جاری**: فهرستِ ورق‌ها ماه‌به‌ماه فیلتر می‌شود و
        // ورقی در ماهِ دیگر اصلاً در کارت‌ها نمی‌آید — یک بار همین باعث شد
        // سنجش بگوید «کارتِ ورق پیدا نشد».
        var today = Shamsi.Today();
        var head = today[..7];

        for (var s = 0; s < Sheets; s++)
        {
            var date = $"{head}/{(s % 28) + 1:00}";

            var w = host.WaraqData.OpenOrCreateAsync(date, "پمپ یعقوبی").GetAwaiter().GetResult();
            var full = host.WaraqData.LoadAsync(w.Id).GetAwaiter().GetResult();
            var sd = full?.Shifts.FirstOrDefault();
            if (sd is null) continue;

            for (var p = 1; p <= PumpsPerSheet; p++)
                host.WaraqData.SavePumpAsync(new WaraqPump
                {
                    ShiftId = sd.Id, SortIndex = p,
                    Num = p, Worker = "کارمند " + p,
                    Fuel = p % 2 == 0 ? FuelType.Diesel : FuelType.Petrol,
                    Start = 10_000m * p, End = 10_000m * p + 500m,
                    PricePerLiter = 67m, Note = "۰۶:۰۰ تا ۱۸:۰۰",
                }).GetAwaiter().GetResult();

            for (var i = 1; i <= RowsPerSheet; i++)
                host.WaraqData.SaveTxnAsync(new WaraqTransaction
                {
                    ShiftId = sd.Id, SortIndex = i,
                    Name = "مشتری شمارهٔ " + i, Liters = 10m * i,
                    Fuel = i % 3 == 0 ? FuelType.Diesel : FuelType.Petrol,
                    Type = i % 4 == 0 ? WaraqTxnType.Expense : WaraqTxnType.Debt,
                    AmountAuto = true,
                }).GetAwaiter().GetResult();
        }
    }

    // ── ابزار ─────────────────────────────────────────────────────────────

    private static long Time(Action a)
    {
        var sw = Stopwatch.StartNew();
        a();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static string Pad(string s, int n) =>
        s.Length >= n ? s[..n] : s + new string(' ', n - s.Length);

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(2); }
        Pump(w);
    }

    /// <summary>چند پاسِ چیدمان لازم شد تا صفحه واقعاً ته‌نشین شود.</summary>
    internal static int Passes;

    /// <summary>
    /// ⚠️ شمردنِ پاس‌ها از **بیرون** کار نمی‌کرد و یک بار گمراه کرد.
    ///
    /// نوشتنِ «تا وقتی ‎IsMeasureValid‎ دروغ است پمپ کن» همیشه صفر می‌داد، چون
    /// خودِ ‎Dispatcher.RunJobs()‎ کارِ چیدمان را انجام می‌دهد و پیش از آن که ما
    /// نگاه کنیم پرچم را دوباره درست می‌کند. پس شمارش باید سرِ منبع باشد:
    /// ‎LayoutUpdated‎ دقیقاً یک بار به ازای هر پاسِ کاملِ چیدمان شلیک می‌شود.
    /// </summary>
    private static void Watch(Window w)
    {
        w.LayoutUpdated += (_, _) =>
        {
            Passes++;
            Marks.Add(Clock.ElapsedMilliseconds);
        };
    }

    /// <summary>مهرِ زمانیِ هر پاس — تا بشود دید وقت کجا رفته.</summary>
    internal static readonly List<long> Marks = new();
    internal static readonly Stopwatch Clock = Stopwatch.StartNew();

    /// <summary>
    /// ⚠️ تا **آرام شدن**، نه شمارِ ثابتِ پاس.
    ///
    /// پیش از این ‎Settle(win, 6)‎ یعنی ۴۸ پاسِ چیدمانِ اجباری، و چون هر پاس چند
    /// میلی‌ثانیه است، خودِ سنجش عددها را باد می‌کرد. این‌جا تا وقتی چیزی برای
    /// چیدن هست پمپ می‌شود و همان لحظه که آرام شد می‌ایستد.
    /// </summary>
    private static void Settle(Window w, int _)
    {
        for (var i = 0; i < 200; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            if (w.IsMeasureValid && w.IsArrangeValid)
            {
                // یک پاسِ خالیِ دیگر: کارهای پس‌زمینه‌ای (مثلِ ‎LazyBox‎) ممکن است
                // تازه چیدمان را دوباره باطل کنند.
                Dispatcher.UIThread.RunJobs();
                w.UpdateLayout();
                if (w.IsMeasureValid && w.IsArrangeValid) return;
            }
        }
    }

    private static void Settle(Window w, TimeSpan budget)
    {
        var end = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
