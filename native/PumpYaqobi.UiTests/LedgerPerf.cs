using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ چرا گاوصندوق، صرافی و مصارف دیر باز می‌شوند ═══════════════════════════
///
/// گزارشِ صاحب ریپو: «بخشِ صرافی و مصارف و گاوصندوق خیلی دیر باز می‌شوند.»
///
/// ⚠️ و چرا <see cref="PerfAudit"/> این را نمی‌دید: دادهٔ آزمونش ‎MonthKey‎
/// نداشت. این سه بخش ماه‌به‌ماه فیلتر می‌شوند
/// (‎WHERE MonthKey = '1405/06'‎)، پس هر سه هزار ردیفِ ساختگی از صافی
/// می‌افتادند و سنجش روی جدولِ <b>خالی</b> انجام می‌شد — «۲۸۹ میلی‌ثانیه»
/// گزارش می‌شد در حالی که هیچ ردیفی روی صفحه نبود.
///
/// این‌جا همان بخش‌ها با دادهٔ <b>واقعاً دیده‌شونده</b> سنجیده می‌شوند، و وقت
/// به سه تکه شکسته می‌شود تا معلوم شود کجا می‌رود:
///
///     منویِ ماه‌ها (SQL) · خواندنِ ردیف‌ها (SQL) · ساختنِ صفحه (UI)
///
/// هر ماه اندازهٔ خودش را دارد، پس با عوض کردنِ ماه همان مسیرِ واقعیِ
/// «باز شدنِ بخش» با سه بارِ متفاوت سنجیده می‌شود.
///
///     dotnet run --project PumpYaqobi.UiTests -- ledgerperf
/// </summary>
internal static class LedgerPerf
{
    /// <summary>
    /// ماهِ جاری بزرگ‌ترین بار را می‌گیرد. بقیه منحنی را نشان می‌دهند: مرزِ
    /// رشدِ جدول ۸۰ ردیف است، پس ۵۰ و ۸۰ «آزاد» و ۲۰۰ و ۳٬۰۰۰ «تنگ»اند.
    /// </summary>
    private static readonly int[] Sizes = { 3_000, 200, 80, 50 };

    private static readonly (string Section, string Table, string Title)[] Targets =
    {
        ("safe",     "SafeEntries",  "گاوصندوق"),
        ("sarrafi",  "ExchangeRows", "صرافی"),
        ("expenses", "Expenses",     "مصارف"),
    };

    /// <summary>هدف: هر بخش، با هر باری، زیرِ این عدد باز شود.</summary>
    private const long Goal = 400;

    /// <summary>ماهی که هیچ ردیفی ندارد — برای صفر کردنِ جدول بینِ دو سنجش.</summary>
    private const string EmptyMonth = "1300/01";

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");

        var months = MonthsBack(Sizes.Length);
        Seed(file, months);

        AppHost.Start(file);
        var host = AppHost.Current;

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1280, Height = 800 };
        win.Show();
        Pump(win);

        // ⚠️ از همان صفحهٔ قفلِ واقعی وارد می‌شویم، نه از درِ پشتیِ
        // ‎Auth.SignIn‎: رویدادِ «وارد شد» همان‌جا شلیک می‌شود و کارهای پس از
        // ورود — از جمله پیش‌ساختنِ صفحه‌ها — واقعاً اجرا می‌گردند. با ورودِ
        // مستقیم، سنجش چیزی را می‌سنجید که کاربر هرگز تجربه نمی‌کند.
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Pump(win);

        // ⚠️ مثلِ کاربرِ واقعی یک لحظه صبر می‌کنیم: برنامه پس از ورود صفحه‌ها
        // را در پس‌زمینه از پیش می‌سازد (‎PrewarmViews‎) و کاربر هیچ‌وقت در
        // همان میلی‌ثانیهٔ اول کلیک نمی‌کند. بی این صبر، سنجش هزینه‌ای را
        // می‌شمارد که در عمل پیش از کلیک پرداخت شده است.
        Settle(win, TimeSpan.FromSeconds(3));

        Console.WriteLine();
        Console.WriteLine("بخش        ردیف     ماه‌ها(SQL)  ردیف‌ها(SQL)  باز شدن   ردیفِ زنده  بلندیِ جدول");
        Console.WriteLine(new string('-', 88));

        var bad = new List<string>();

        foreach (var (id, _, title) in Targets)
        {
            var sec = vm.Sections.First(s => s.Id == id);
            var rowHost = (IRowBatchHost)sec;

            // ⚠️ یک‌بار «سرد» باز می‌شود و جدا گزارش می‌گردد: آن بار شاملِ
            // ساختنِ خودِ صفحه و قالب‌هایش است و ربطی به شمارِ ردیف ندارد.
            // اگر با بقیه قاطی شود، هزینهٔ یک‌بارهٔ ساختِ صفحه به گردنِ
            // ردیف‌ها نوشته می‌شود و آدم دنبالِ جای اشتباه می‌گردد.
            var cold = Time(() => Wait(win, vm.GoAsync(sec)));
            Console.WriteLine($"{title,-10} {"(بازِ اول)",6}   {"",8}     {"",8}     "
                            + $"{cold,7:N0} ms");

            for (var k = 0; k < months.Length; k++)
            {
                // ⚠️ اول به یک ماهِ خالی می‌رویم و بعد به ماهِ هدف: وگرنه
                // اگر ماه از قبل همان باشد، ‎OnMonthChanged‎ اصلاً شلیک
                // نمی‌کند و «صفر میلی‌ثانیه»ی دروغ گزارش می‌شود.
                SetMonth(sec, EmptyMonth);
                Settle(win, rowHost, 0);

                // همهٔ اندازه‌ها «عوض کردنِ ماه»اند — همان ‎ReloadRowsAsync‎ی
                // واحدی که باز شدنِ بخش هم از آن می‌گذرد، ولی بی هزینهٔ
                // یک‌بارهٔ ساختِ صفحه.
                SetMonth(sec, months[k]);
                var open = Time(() => Settle(win, rowHost, Sizes[k]));

                var sql = Sql(host, id, months[k]);
                var grid = Grid(win);
                var live = grid?.GetVisualDescendants()
                               .OfType<Avalonia.Controls.DataGridRow>().Count() ?? 0;

                Console.WriteLine($"{title,-10} {Sizes[k],6:N0}  {sql.Months,8:N0} ms  {sql.Rows,8:N0} ms  "
                                + $"{open,7:N0} ms  {live,8:N0}  {grid?.Bounds.Height ?? 0,8:N0}px");

                if (open > Goal) bad.Add($"{title} با {Sizes[k]:N0} ردیف: {open:N0} ms");
                if (live > 200) bad.Add($"{title} با {Sizes[k]:N0} ردیف: {live:N0} ردیفِ زنده — مجازی‌سازی نمی‌کند");
            }
        }

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine($"✅ هر سه بخش زیرِ {Goal} ms باز می‌شوند و مجازی‌سازی می‌کنند");
            return 0;
        }

        Console.WriteLine($"❌ این‌ها از هدفِ {Goal} ms ردند یا مجازی‌سازی نمی‌کنند:");
        foreach (var b in bad) Console.WriteLine("   • " + b);
        return 1;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  اندازه‌گیری
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>همان دو کوئریِ بخش، بی هیچ صفحه‌ای — تا «SQL» از «UI» جدا شود.</summary>
    private static (long Months, long Rows) Sql(AppHost h, string id, string month)
    {
        switch (id)
        {
            case "safe":
                return (Time(() => h.SafeLedger.MonthsAsync().GetAwaiter().GetResult()),
                        Time(() => h.SafeLedger.ListAsync(month).GetAwaiter().GetResult()));
            case "sarrafi":
                return (Time(() => h.ExchangeLedger.MonthsAsync().GetAwaiter().GetResult()),
                        Time(() => h.ExchangeLedger.ListAsync(month).GetAwaiter().GetResult()));
            default:
                return (Time(() => h.ExpenseLedger.MonthsAsync().GetAwaiter().GetResult()),
                        Time(() => h.ExpenseLedger.ListAsync(month).GetAwaiter().GetResult()));
        }
    }

    /// <summary>
    /// ⚠️ ‎Month‎ روی کلاسِ **جنریکِ** ‎LedgerSectionViewModel&lt;,&gt;‎ است و
    /// این‌جا سه بخش با سه جنریکِ متفاوت داریم؛ بازتاب کوتاه‌ترین راهِ درست
    /// است و چون فقط ابزارِ سنجش است، هیچ کدِ برنامه‌ای را آلوده نمی‌کند.
    /// </summary>
    private static void SetMonth(object section, string month) =>
        section.GetType().GetProperty("Month", BindingFlags.Public | BindingFlags.Instance)
               ?.SetValue(section, month);

    /// <summary>
    /// ⚠️ فقط جدولِ **دیده‌شونده**: از وقتی همهٔ بخش‌ها با هم در درختِ بصری
    /// می‌مانند و فقط یکی‌شان دیده می‌شود (‎SectionViewModel.IsShown‎)،
    /// ‎FirstOrDefault‎ی ساده جدولِ یک بخشِ پنهان را برمی‌داشت — و آن‌وقت
    /// سنجش می‌گفت «۰ ردیفِ زنده و ۴۷ پیکسل»، که مالِ همان جدولِ پنهان بود.
    /// </summary>
    private static Avalonia.Controls.DataGrid? Grid(Window w) =>
        w.GetVisualDescendants().OfType<Avalonia.Controls.DataGrid>()
         .FirstOrDefault(g => g.IsEffectivelyVisible);

    /// <summary>
    /// عوض کردنِ ماه ‎async void‎ است (‎_ = ReloadRowsAsync()‎)، پس نمی‌شود
    /// منتظرش ماند — تا وقتی شمارِ ردیف به عددِ همان ماه برسد پمپ می‌کنیم.
    /// </summary>
    private static void Settle(Window w, IRowBatchHost host, int want)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            if (host.RowCount == want) break;
            Thread.Sleep(2);
        }
        Pump(w);
    }

    /// <summary>تا ته‌نشین شدنِ کارهای پس‌زمینه (یا سررسیدِ مهلت) پمپ می‌کند.</summary>
    private static void Settle(Window w, TimeSpan budget)
    {
        var end = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Thread.Sleep(5);
        }
    }

    private static long Time(Action a)
    {
        var sw = Stopwatch.StartNew();
        a();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static long Time(Func<object> a) => Time(() => { _ = a(); });

    // ══════════════════════════════════════════════════════════════════════
    //  دادهٔ آزمون — ⚠️ با ‎MonthKey‎، وگرنه بخش خالی سنجیده می‌شود
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>ماهِ جاری و ‎n−1‎ ماهِ پیش از آن.</summary>
    private static string[] MonthsBack(int n)
    {
        var now = Shamsi.ToEnDigits(Shamsi.ThisMonth()).Split('/');
        var y = int.Parse(now[0]);
        var m = int.Parse(now[1]);
        var outp = new string[n];
        for (var i = 0; i < n; i++)
        {
            outp[i] = $"{y:0000}/{m:00}";
            if (--m < 1) { m = 12; y--; }
        }
        return outp;
    }

    private static void Seed(string file, string[] months)
    {
        var dbf = new PumpYaqobi.Services.Data.PumpDbFactory(file);
        dbf.EnsureReady();

        using var db = dbf.Create();
        var conn = db.Database.GetDbConnection();
        conn.Open();
        Exec(conn, "PRAGMA synchronous=OFF;");
        Exec(conn, "PRAGMA foreign_keys=OFF;");
        using var tx = conn.BeginTransaction();

        foreach (var (_, table, _) in Targets)
            for (var k = 0; k < months.Length; k++)
                Fill(conn, table, months[k], Sizes[k]);

        tx.Commit();
        conn.Close();
    }

    private static void Fill(DbConnection conn, string table, string month, int n)
    {
        var cols = Columns(conn, table);
        var named = new[] { "DateShamsi", "DateKey", "MonthKey", "Name", "Title", "Description",
                            "Note", "Amount", "Rate", "Bardagi", "Liters", "PricePerLiter",
                            "SortIndex", "Kind", "Currency", "CreatedAt", "UpdatedAt" }
                    .Where(cols.Contains).ToArray();

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO \"{table}\" ({string.Join(",", named.Select(c => '"' + c + '"'))}) "
                        + $"VALUES ({string.Join(",", named.Select(c => "@" + c))})";
        foreach (var c in named)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@" + c;
            cmd.Parameters.Add(p);
        }

        for (var i = 0; i < n; i++)
        {
            var date = month + "/" + ((i % 28) + 1).ToString("00");
            foreach (DbParameter p in cmd.Parameters)
                p.Value = p.ParameterName switch
                {
                    "@DateShamsi" => date,
                    "@DateKey" => Shamsi.Key(date),
                    "@MonthKey" => month,
                    "@Name" or "@Title" or "@Description" => "قلمِ " + i,
                    "@Note" => "",
                    "@Amount" or "@Bardagi" => ((i % 500 + 1) * 100).ToString(),
                    "@Rate" or "@PricePerLiter" => "62",
                    "@Liters" => (i % 90 + 1).ToString(),
                    "@SortIndex" => i,
                    "@Kind" or "@Currency" => (i % 2) + 1,
                    _ => (object)now,
                };
            cmd.ExecuteNonQuery();
        }
    }

    private static HashSet<string> Columns(DbConnection c, string table)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var r = cmd.ExecuteReader();
        while (r.Read()) set.Add(r.GetString(1));
        return set;
    }

    private static void Exec(DbConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Thread.Sleep(2);
        }
        Pump(w);
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
        }
    }
}
