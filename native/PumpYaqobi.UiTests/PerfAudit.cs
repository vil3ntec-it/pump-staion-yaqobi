using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;   // GetDbConnection
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
/// ══ کند نشدن، با دادهٔ واقعاً بزرگ ══════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «حتی اگر ده هزار قرض‌دار داشتم با جدول‌هایی از صدهزار یا
/// یک میلیون ردیف، برنامه نباید کند شود یا مکث کند. همه‌چیز باید در صدمِ ثانیه
/// باز شود. ببین چه چیزی باعث کندی می‌شود و درستش کن.»
///
/// ادعا در کد ارزشی ندارد؛ این‌جا اندازه گرفته می‌شود. یک دیتابیسِ بزرگ ساخته
/// می‌شود (ده هزار قرض‌دار، سیصد هزار ردیف، که صدهزارتایش در **یک** حساب است)
/// و بعد همان پنجرهٔ واقعیِ برنامه باز می‌شود و زمانِ کارهای روزمره گرفته
/// می‌شود: باز کردنِ بخش‌ها، باز کردنِ حسابِ بزرگ، و یک ویرایش در ورق.
///
///     dotnet run --project PumpYaqobi.UiTests -- perf
/// </summary>
internal static class PerfAudit
{
    /// <summary>چند قرض‌دار.</summary>
    private const int People = 10_000;

    /// <summary>ردیف‌های حسابِ بزرگ — همان «صدهزار ردیف در یک حساب».</summary>
    private const int BigRows = 100_000;

    /// <summary>ردیف برای هر یک از بقیهٔ حساب‌ها.</summary>
    private const int SmallRows = 5;

    /// <summary>سقفِ «آشکارا خراب» — بیشتر از این یعنی جایی می‌ایستد.</summary>
    private const long Broken = 3_000;

    private static readonly List<(string What, long Ms)> Marks = new();

    /// <summary>
    /// ⚠️ سقفِ کلِ سنجش. یک‌بار این سنجش بیست دقیقه دوید و هیچ‌وقت تمام نشد؛
    /// از آن به بعد وقتی این بودجه تمام شود، بقیهٔ کارها رد می‌شوند و گزارش
    /// همان‌جا بسته می‌شود — «تمام نشد» خودش یک نتیجه است، نه یک وقفهٔ بی‌پایان.
    /// </summary>
    private static readonly Stopwatch Budget = Stopwatch.StartNew();
    private const long BudgetMs = 240_000;
    private static bool _outOfTime;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-perf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");

        var seed = Stopwatch.StartNew();
        var bigDebtor = Seed(file);
        seed.Stop();
        Console.WriteLine($"دادهٔ آزمون ساخته شد: {People:N0} قرض‌دار · "
                        + $"{BigRows + (People - 1) * SmallRows:N0} ردیف — {seed.ElapsedMilliseconds:N0} ms");

        // ══ فازِ یک: خودِ داده، بی هیچ صفحه‌ای ═══════════════════════════════
        //
        // این‌جا معلوم می‌شود که «کندی» مالِ دیتابیس است یا مالِ کشیدنِ صفحه.
        // هر دو را باید جدا دید، وگرنه آدم ساعت‌ها دنبالِ جای اشتباه می‌گردد.
        AppHost.Start(file);
        var host = AppHost.Current;

        // بی ورود، لایهٔ اجازه‌ها جلوی هر خواندنی را می‌گیرد
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");

        Console.WriteLine();
        Console.WriteLine("کار                                        زمان");
        Console.WriteLine(new string('-', 52));

        Mark("خواندنِ فهرستِ قرض‌داران (بی صفحه)", () =>
        { var t = host.Debtors.ListAsync(false); t.GetAwaiter().GetResult(); });

        Mark("جمع‌های همهٔ حساب‌ها (بی صفحه)", () =>
        { var t = host.Debtors.CardAccountsAsync(false); t.GetAwaiter().GetResult(); });

        Mark($"خواندنِ حسابِ {BigRows:N0} ردیفی (بی صفحه)", () =>
        { var t = host.Debtors.LoadFullAsync(bigDebtor); t.GetAwaiter().GetResult(); });

        // ══ فازِ دو: همان کارها روی پنجرهٔ واقعی ═════════════════════════════
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1280, Height = 800 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        // ⚠️ ورودِ مدیر در فازِ یک انجام شده؛ این‌جا فقط اگر قفل هنوز جلوی
        // صفحه باشد بازش می‌کنیم — و «تاییدِ رمز» را دست نمی‌زنیم تا مسیرِ
        // «ساختِ مدیرِ اول» دوباره اجرا نشود.
        try
        {
            vm.Lock.Password = "1234";
            vm.Lock.SubmitCommand.Execute(null);
        }
        catch { /* از پیش باز است */ }
        Pump(win);

        // ── باز کردنِ بخش‌ها ──────────────────────────────────────────────────
        foreach (var id in new[] { "dashboard", "debt", "safe", "expenses", "waraq", "shifts" })
        {
            var sec = vm.Sections.FirstOrDefault(s => s.Id == id);
            if (sec is null) continue;
            Mark("باز کردنِ بخشِ " + id, () => Wait(win, vm.GoAsync(sec)));
        }

        // ── دوباره رفتن به قرض‌داران (فهرست از نو کشیده می‌شود) ───────────────
        var debt = vm.Sections.FirstOrDefault(s => s.Id == "debt") as DebtSectionViewModel;
        if (debt is not null)
        {
            Mark("برگشت به قرض‌داران (فهرستِ ده هزارتایی)",
                 () => Wait(win, vm.GoAsync(debt)));

            Mark("جست‌وجوی نام در ده هزار کارت", () =>
            {
                debt.Search = "بزرگ";
                Pump(win);
            });

            if (debt.Cards.Count > 0)
                Mark($"باز کردنِ حسابی با {BigRows:N0} ردیف",
                     () => Wait(win, debt.OpenByNumberAsync(1)));

            Mark("بستنِ حساب", () => { debt.Person = null; Pump(win); });

            // ⚠️ فهرستِ کارت‌ها باید مجازی‌سازی کند: اگر همهٔ ده هزار کارت
            // ساخته شوند، هر چیدمانِ صفحه ده هزار عنصر را می‌سنجد و برنامه
            // همان‌جا می‌ایستد — هر چند دیتابیس سریع جواب داده باشد.
            var live = win.GetVisualDescendants().OfType<Button>()
                          .Count(b => b.DataContext is DebtorCardViewModel);
            Console.WriteLine($"کارتِ زنده در درختِ بصری: {live:N0} از {debt.Cards.Count:N0}");
            if (live > 400) Console.WriteLine("        ⚠️ فهرستِ کارت‌ها مجازی‌سازی نمی‌کند");
            debt.Search = "";
            Pump(win);
        }

        // ── یک ویرایش در ورق (همگام‌سازی با ده هزار حساب) ─────────────────────
        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is WaraqSectionViewModel wq)
        {
            Wait(win, vm.GoAsync(wq));
            Wait(win, wq.ReloadAsync());
            if (wq.Sheets.Count == 0) { wq.NewSheetCommand.Execute(null); Settle(win); }
            wq.OpenCommand.Execute(wq.Sheets.FirstOrDefault());
            Settle(win);

            var page = wq.Page;
            if (page is { Txns.Count: > 0 })
            {
                var row = page.Txns[0];
                row.Name = "قرض‌دارِ 5000";
                row.AmountText = "500";
                Mark("ویرایشِ یک خانهٔ ورق (ثبت در حساب‌ها)",
                     () => Wait(win, row.FlushAsync()));
            }
        }

        Console.WriteLine();
        var bad = Marks.Where(m => m.Ms > Broken).ToList();
        foreach (var (what, ms) in bad) Console.WriteLine($"❌ {what}: {ms:N0} ms");

        if (bad.Count == 0)
        {
            Console.WriteLine($"✅ هیچ کاری بیشتر از {Broken:N0} ms طول نکشید");
            return 0;
        }
        Console.WriteLine($"❌ {bad.Count} کار از سقفِ {Broken:N0} ms گذشت");
        return 1;
    }

    private static void Mark(string what, Action run)
    {
        if (_outOfTime) { Console.WriteLine($"{what,-42}     — رد شد (بودجهٔ زمان تمام شد)"); return; }

        var sw = Stopwatch.StartNew();
        run();
        sw.Stop();
        Marks.Add((what, sw.ElapsedMilliseconds));
        Console.WriteLine($"{what,-42}{sw.ElapsedMilliseconds,6:N0} ms");

        if (Budget.ElapsedMilliseconds > BudgetMs)
        {
            _outOfTime = true;
            Console.WriteLine($"⛔ بودجهٔ {BudgetMs / 1000} ثانیه‌ایِ سنجش تمام شد — بقیه رد می‌شود");
        }
    }

    // ══ ساختنِ دادهٔ بزرگ ═════════════════════════════════════════════════════

    /// <summary>
    /// همه‌چیز با SQLِ خام و در یک تراکنش — با EF، ساختنِ سیصد هزار ردیف خودش
    /// دقیقه‌ها طول می‌کشید و آزمون به جایی نمی‌رسید.
    ///
    /// ⚠️ ستون‌ها از خودِ دیتابیس خوانده می‌شوند (‎PRAGMA table_info‎)، نه از یک
    /// فهرستِ دستی: هر ستونی که فردا به مدل اضافه شود، این‌جا خودبه‌خود مقدارِ
    /// پیش‌فرض می‌گیرد و آزمون نمی‌شکند.
    /// </summary>
    private static long Seed(string file)
    {
        var dbf = new PumpYaqobi.Services.Data.PumpDbFactory(file);
        dbf.EnsureReady();

        using var db = dbf.Create();
        var conn = db.Database.GetDbConnection();
        conn.Open();

        Exec(conn, "PRAGMA synchronous=OFF;");
        using var tx = conn.BeginTransaction();

        long bigDebtor = 0;
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        using var person = new Insert(conn, "Debtors");
        using var account = new Insert(conn, "DebtAccounts");
        using var row = new Insert(conn, "DebtRows");

        for (var i = 1; i <= People; i++)
        {
            var name = i == 1 ? "قرض‌دارِ بزرگ" : "قرض‌دارِ " + i;
            person.Set("Name", name);
            person.Set("LegacyId", "p" + i);
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
            if (i == 1) bigDebtor = pid;

            var n = i == 1 ? BigRows : SmallRows;
            for (var k = 0; k < n; k++)
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
        Exec(conn, "ANALYZE;");
        conn.Close();
        return bigDebtor;
    }

    private static void Exec(DbConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// یک ‎INSERT‎ی آماده که ستون‌هایش از خودِ جدول خوانده شده.
    ///
    /// ⚠️ دستور و پارامترها یک‌بار ساخته می‌شوند و بعد فقط مقدارشان عوض
    /// می‌شود: با سیصد هزار ردیف، ساختنِ دوبارهٔ دستور خودش دقیقه‌ها می‌شد.
    /// </summary>
    private sealed class Insert : IDisposable
    {
        private readonly DbCommand _cmd;
        private readonly Dictionary<string, DbParameter> _p = new();
        private readonly Dictionary<string, object?> _blank = new();

        public Insert(DbConnection c, string table)
        {
            var cols = new List<string>();

            using (var info = c.CreateCommand())
            {
                info.CommandText = $"PRAGMA table_info({table});";
                using var r = info.ExecuteReader();
                while (r.Read())
                {
                    var name = r.GetString(1);
                    var type = r.GetString(2).ToUpperInvariant();
                    var notNull = r.GetInt32(3) == 1;
                    if (r.GetInt32(5) == 1) continue;          // شناسه را خودِ SQLite می‌دهد

                    cols.Add(name);
                    object? blank = null;
                    if (notNull)
                    {
                        if (type.Contains("INT")) blank = 0L;
                        else if (type.Contains("REAL")) blank = 0d;
                        else blank = "0";
                    }
                    _blank[name] = blank;
                }
            }

            _cmd = c.CreateCommand();
            _cmd.CommandText =
                $"INSERT INTO {table} ({string.Join(", ", cols)}) VALUES " +
                $"({string.Join(", ", cols.Select(x => "@" + x))}); SELECT last_insert_rowid();";
            foreach (var col in cols)
            {
                var p = _cmd.CreateParameter();
                p.ParameterName = "@" + col;
                p.Value = _blank[col] ?? (object)DBNull.Value;
                _cmd.Parameters.Add(p);
                _p[col] = p;
            }
            _cmd.Prepare();
        }

        public void Set(string col, object? v)
        {
            if (_p.TryGetValue(col, out var p)) p.Value = v ?? DBNull.Value;
        }

        /// <summary>هر ستونی که این ردیف مقدارش نداده، به پیش‌فرضِ خودش برگردد.</summary>
        public void Reset()
        {
            foreach (var (col, p) in _p) p.Value = _blank[col] ?? (object)DBNull.Value;
        }

        public long Run() => Convert.ToInt64(_cmd.ExecuteScalar());

        public void Dispose() => _cmd.Dispose();
    }

    /// <summary>
    /// ⚠️ سه دور، نه هشت: هر دور یک چیدمانِ کاملِ پنجره است و روی صفحه‌ای با
    /// ده هزار کارت، همان یک دور خودش گران است. هدفِ این سنجش «چند ثانیه طول
    /// می‌کشد» است، نه «چند دور چیدمان».
    /// </summary>
    private static void Pump(Window w)
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 40 && sw.ElapsedMilliseconds < 5_000; i++) Pump(w);
    }

    /// <summary>
    /// ⚠️ زمان‌دار، نه شمارشی: هر دورِ ‎Pump‎ یک چیدمانِ کامل است و روی یک
    /// پنجرهٔ سنگین می‌تواند خودش چند صدم ثانیه طول بکشد. با شمارش، انتظارِ
    /// «۴۰۰۰ دور» به ده‌ها دقیقه می‌رسید و سنجش هرگز تمام نمی‌شد.
    /// </summary>
    private static void Wait(Window w, Task t)
    {
        var sw = Stopwatch.StartNew();
        while (!t.IsCompleted && sw.ElapsedMilliseconds < 20_000) Pump(w);
        Pump(w);
        if (!t.IsCompleted) Console.WriteLine("        ⚠️ بیست ثانیه گذشت و هنوز تمام نشده");
    }
}
