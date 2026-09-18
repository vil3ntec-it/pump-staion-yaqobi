using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «حسابی که ردیف‌هایش زیاد است دیر باز می‌شود» ════════════════════════════
///
/// ‎perf‎ یک عدد می‌دهد و بس: «باز کردنِ حسابی با ۵۰٬۰۰۰ ردیف — ۱۴٬۲۶۰ ms».
/// با یک عدد نمی‌شود فهمید هزینه **کجاست** و **چه شکلی** دارد، و قاعدهٔ همین
/// ریپو هم همین است: «اول بساز که ببینی؛ حدس نزن».
///
/// پس این سنجه همان یک کار را با **شش اندازه** می‌سنجد و هر بار سه تکه‌اش را
/// جدا می‌کند:
///
///   • خواندن   — ‎LoadFullAsync‎ (دیتابیس)
///   • ویومدل   — ‎Load‎ی صفحهٔ شخص (ساختنِ ردیف‌ها و جمع‌ها)
///   • چیدمان   — ‎UpdateLayout‎ تا صفحه آرام بگیرد
///
/// و ستونِ آخر تصمیم‌گیر است: **میکروثانیه به ازای هر ردیف**. اگر این عدد با
/// بزرگ‌تر شدنِ حساب ثابت بماند هزینه خطی است (ناچار)، و اگر بالا برود یعنی
/// جایی کارِ مربعی هست — و همان‌جا باید درست شود، نه سقفِ سنجه.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- personperf
/// </summary>
internal static class PersonPerf
{
    /// <summary>از «یک ماهِ معمولی» تا «ده سال در یک حساب».</summary>
    private static readonly int[] Sizes = { 500, 2_000, 5_000, 10_000, 20_000, 50_000 };

    /// <summary>سقفِ «آشکارا خراب» — همان سقفِ ‎perf‎.</summary>
    private const long Broken = 3_000;

    /// <summary>ردیفِ زندهٔ بیشینه — پنجرهٔ چسبان باید ثابت نگهش دارد.</summary>
    private const int LiveGoal = 80;

    /// <summary>‎personperf trace‎ — فقط حسابِ بزرگ، سه بار، برای نمونه‌بردار.</summary>
    internal static bool Trace;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-personperf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");
        Seed(file);

        AppHost.Start(file);
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

        var debt = (DebtSectionViewModel)vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(debt)); Settle(win);

        // ══ حالتِ پروفایل: فقط حسابِ بزرگ، چند بار — برای ‎dotnet-trace‎ ══════
        //     dotnet-trace collect --format speedscope -- \
        //       dotnet run --project PumpYaqobi.UiTests -c Release -- personperf trace
        if (Trace)
        {
            var big = debt.Cards.FirstOrDefault(c => c.Entity.Name == Name(Sizes[^1]));
            if (big is null) { Console.WriteLine("کارتِ حسابِ بزرگ پیدا نشد"); return 1; }
            var no = debt.Cards.IndexOf(big) + 1;
            for (var i = 0; i < 3; i++)
            {
                debt.PersonOpen = false; Settle(win);
                var t = Stopwatch.StartNew();
                var o = debt.OpenByNumberAsync(no);
                while (!o.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
                t.Stop();
                Console.WriteLine($"دورِ {i + 1}: باز کردنِ حسابِ {Sizes[^1]:N0} ردیفی {t.ElapsedMilliseconds:N0} ms");
            }
            debt.PersonOpen = false; Settle(win);
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("ردیف      خواندن   ویومدل   چیدمان     همه   ردیفِ زنده   دستور   میکروثانیه/ردیف");
        Console.WriteLine(new string('-', 88));

        var bad = new List<(int Size, long Ms)>();
        var fatRows = new List<(int Size, int Live)>();
        var shape = new List<(int Size, double Per)>();

        foreach (var size in Sizes)
        {
            var card = debt.Cards.FirstOrDefault(c => c.Entity.Name == Name(size));
            if (card is null) { Console.WriteLine($"{size,-9} — کارتش پیدا نشد"); continue; }

            debt.PersonOpen = false; Settle(win);

            //  ① خواندن — همان پرس‌وجویی که ‎OpenAsync‎ هم می‌زند
            var q0 = PumpYaqobi.Services.Data.DbWatch.Count;
            var read = Stopwatch.StartNew();
            var task = host.Debtors.LoadFullAsync(card.Entity.Id);
            while (!task.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
            read.Stop();

            //  ② ویومدل — باز کردنِ واقعی (که خودش دوباره می‌خواند؛ عددِ
            //     خواندن از آن کم می‌شود تا فقط کارِ ویومدل بماند)
            var work = Stopwatch.StartNew();
            var open = debt.OpenByNumberAsync(debt.Cards.IndexOf(card) + 1);
            while (!open.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
            work.Stop();

            //  ③ چیدمان
            var lay = Stopwatch.StartNew(); Settle(win); lay.Stop();

            var live = win.GetVisualDescendants().OfType<DataGridRow>().Count();
            var queries = PumpYaqobi.Services.Data.DbWatch.Count - q0;
            var model = Math.Max(0, work.ElapsedMilliseconds - read.ElapsedMilliseconds);
            var all = work.ElapsedMilliseconds + lay.ElapsedMilliseconds;
            var per = all * 1000.0 / size;

            Console.WriteLine($"{size,-9:N0}{read.ElapsedMilliseconds,7:N0}{model,9:N0}"
                            + $"{lay.ElapsedMilliseconds,9:N0}{all,8:N0}{live,12}{queries,8}{per,15:N1}");

            shape.Add((size, per));
            if (all > Broken) bad.Add((size, all));
            //  ⚠️ سنجهٔ **ساختاری** — همان قاعدهٔ همیشگیِ این ریپو: وقت روی
            //  ماشینِ CI نوسان دارد، «چند ردیف ساخته شد» نه.
            if (live > LiveGoal) fatRows.Add((size, live));
        }

        debt.PersonOpen = false; Settle(win);

        Console.WriteLine();

        //  ══ شکلِ هزینه ══════════════════════════════════════════════════════
        //  عددِ «میکروثانیه به ازای ردیف» با بزرگ شدنِ حساب باید **ثابت** بماند.
        //  دو برابر شدنش یعنی کارِ مربعی، و آن ریشه است نه نشانه.
        if (shape.Count >= 2)
        {
            var first = shape[0].Per;
            var last = shape[^1].Per;
            var grow = first > 0 ? last / first : 0;
            Console.WriteLine($"شکلِ هزینه: از {shape[0].Size:N0} تا {shape[^1].Size:N0} ردیف، "
                            + $"هزینهٔ هر ردیف {grow:N1} برابر شد "
                            + (grow > 1.6 ? "— ⚠️ خطی نیست، جایی کارِ مربعی هست" : "— خطی"));
        }

        Console.WriteLine();
        if (bad.Count == 0 && fatRows.Count == 0)
        {
            Console.WriteLine($"✅ باز کردنِ حساب با هر اندازه‌ای زیرِ {Broken:N0} ms ماند، "
                            + $"و ردیفِ زنده زیرِ {LiveGoal}");
            return 0;
        }
        foreach (var (size, ms) in bad)
            Console.WriteLine($"❌ حسابِ {size:N0} ردیفی: {ms:N0} ms (سقف {Broken:N0})");
        foreach (var (size, live) in fatRows)
            Console.WriteLine($"❌ حسابِ {size:N0} ردیفی: {live:N0} ردیفِ زنده (سقف {LiveGoal}) "
                            + "— پنجرهٔ چسبان مرده است");
        return 1;
    }

    private static string Name(int size) => "حسابِ " + size + " ردیفی";

    // ══ دادهٔ آزمون ═══════════════════════════════════════════════════════════

    /// <summary>
    /// یک قرض‌دار به ازای هر اندازه — همه در یک دیتابیس، تا هر شش اندازه در
    /// **یک فرآیند** سنجیده شوند (آوالونیا در هر فرآیند یک بار راه می‌افتد).
    /// </summary>
    private static void Seed(string file)
    {
        var dbf = new PumpYaqobi.Services.Data.PumpDbFactory(file);
        dbf.EnsureReady();

        using var db = dbf.Create();
        var conn = db.Database.GetDbConnection();
        conn.Open();
        PerfAudit.Exec(conn, "PRAGMA synchronous=OFF;");
        PerfAudit.Exec(conn, "PRAGMA foreign_keys=OFF;");
        using var tx = conn.BeginTransaction();

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        using var person = new PerfAudit.Insert(conn, "Debtors");
        using var account = new PerfAudit.Insert(conn, "DebtAccounts");
        using var row = new PerfAudit.Insert(conn, "DebtRows");

        var i = 0;
        foreach (var size in Sizes)
        {
            i++;
            var name = Name(size);
            person.Set("Name", name);
            person.Set("LegacyId", "pp" + i);
            person.Set("IsNoInvoice", 0);
            person.Set("CreatedAt", now);
            person.Set("UpdatedAt", now);
            var pid = person.Run();

            account.Set("MainOfDebtorId", pid);
            account.Set("Name", name);
            account.Set("Mode", 1);
            account.Set("CreatedAt", now);
            account.Set("UpdatedAt", now);
            var aid = account.Run();

            for (var k = 0; k < size; k++)
            {
                row.Set("FuelAccountId", aid);
                row.Set("SortIndex", k);
                row.Set("DateShamsi", "1405/06/18");
                row.Set("DateKey", 14050618);
                row.Set("Name", "بردگیِ " + k);
                row.Set("Fuel", k % 3 == 0 ? 2 : 1);
                row.Set("Liters", (k % 50 + 1).ToString());
                row.Set("PricePerLiter", "62");
                row.Set("Bardagi", ((k % 50 + 1) * 62).ToString());
                row.Set("Rasid", "0");
                row.Set("RasidFuel", "0");
                row.Set("Albaqi", ((k % 50 + 1) * 62).ToString());
                row.Set("ByMoney", 0);
                row.Set("CreatedAt", now);
                row.Set("UpdatedAt", now);
                row.Run();
            }
        }

        tx.Commit();
        conn.Close();
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }

    private static void Settle(Window w)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 400 && sw.ElapsedMilliseconds < 60_000; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            if (w.IsMeasureValid && w.IsArrangeValid
                && !Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.Background)) break;
        }
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(2); }
        Pump(w);
    }
}
