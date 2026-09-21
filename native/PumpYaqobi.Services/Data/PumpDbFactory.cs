using PumpYaqobi.Domain.Entities;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ شمارندهٔ دستورهای دیتابیس ═══════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «اگر توی بخشی نیستم، آن بخش فعال
/// نباشد و هیچ مصرفی نداشته باشد — حتی یک درصد.» «مصرف» را نمی‌شود با نگاه
/// کردن به کد ثابت کرد؛ این‌جا هر دستوری که واقعاً به SQLite می‌رسد شمرده
/// می‌شود و سنجشِ ‎idle‎ روی همین عدد قضاوت می‌کند.
///
/// ⚠️ هزینه‌اش یک ‎Interlocked.Increment‎ در هر دستور است — در برابرِ خودِ
/// پرس‌وجو هیچ. برای همین همیشه روشن است و لازم نیست کسی یادش بماند روشنش کند.
/// </summary>
public sealed class DbWatch : DbCommandInterceptor
{
    public static readonly DbWatch Instance = new();

    private static long _count;

    /// <summary>چند دستور تا حالا به دیتابیس رفته.</summary>
    public static long Count => Interlocked.Read(ref _count);

    /// <summary>آخرین دستورها — فقط وقتی <see cref="Recording"/> روشن باشد.</summary>
    public static readonly System.Collections.Concurrent.ConcurrentQueue<string> Log = new();

    /// <summary>ضبطِ متنِ دستورها برای سنجش‌ها. در برنامهٔ واقعی خاموش است.</summary>
    public static bool Recording;

    private static void Seen(DbCommand cmd)
    {
        Interlocked.Increment(ref _count);
        if (!Recording) return;
        Log.Enqueue(cmd.CommandText.Length > 160 ? cmd.CommandText[..160] : cmd.CommandText);
        while (Log.Count > 400) Log.TryDequeue(out _);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { Seen(command); return base.ReaderExecuting(command, eventData, result); }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    { Seen(command); return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    { Seen(command); return base.ScalarExecuting(command, eventData, result); }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    { Seen(command); return base.ScalarExecutingAsync(command, eventData, result, cancellationToken); }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    { Seen(command); return base.NonQueryExecuting(command, eventData, result); }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    { Seen(command); return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken); }
}

/// <summary>
/// یک‌جا ساختنِ اتصالِ دیتابیس. مسیرِ فایل کنارِ دادهٔ کاربر است، نه کنارِ EXE،
/// تا نصبِ دوباره یا به‌روزرسانی داده را نبَرد.
/// </summary>
/// <summary>
/// ══ «قفل بود، کمی صبر کن» — روی هر اتصال ═══════════════════════════════════
///
/// ‎busy_timeout‎ یک تنظیمِ **هر اتصال** است، پس نوشتنش یک بار در
/// ‎EnsureReady‎ هیچ اثری روی اتصال‌های بعدیِ برنامه ندارد (سنجیده شد: صفر).
/// این شنونده با هر بار باز شدنِ اتصال همان را می‌گذارد — پنج ثانیه، که برای
/// نوشتنِ کوتاهِ یک دفتر زیاد هم هست.
/// </summary>
public sealed class BusyWait : DbConnectionInterceptor
{
    public static readonly BusyWait Instance = new();

    private static void Apply(System.Data.Common.DbConnection conn)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA busy_timeout=5000;";
            cmd.ExecuteNonQuery();
        }
        catch { /* اگر نشد، همان رفتارِ قبلی — هیچ‌وقت جلوی کار را نمی‌گیرد */ }
    }

    public override void ConnectionOpened(System.Data.Common.DbConnection connection,
                                          ConnectionEndEventData eventData)
    {
        Apply(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override Task ConnectionOpenedAsync(System.Data.Common.DbConnection connection,
                                               ConnectionEndEventData eventData,
                                               CancellationToken cancellationToken = default)
    {
        Apply(connection);
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}

public sealed class PumpDbFactory
{
    public PumpDbFactory(string? dbPath = null)
    {
        DbPath = dbPath ?? DefaultPath;
        var dir = Path.GetDirectoryName(DbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public string DbPath { get; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PumpYaqobi", "pump.db");

    /// <summary>
    /// ⚠️ **چرا ‎busy_timeout‎ صریح**: سنجیده شد که روی اتصالِ واقعیِ برنامه
    /// ‎foreign_keys=1‎ است (خودِ ارائه‌دهنده روشنش می‌کند) ولی
    /// ‎busy_timeout=0‎. یعنی انتظارِ «دیتابیس قفل است» تنها به حلقهٔ تلاشِ
    /// دوبارهٔ خودِ ‎Microsoft.Data.Sqlite‎ (به اندازهٔ ‎CommandTimeout‎) سپرده
    /// بود، نه به خودِ اتصال. با نوشتنِ برنامه از یک طرف و حلقهٔ انتشارِ
    /// ایستگاه از طرفِ دیگر، این همان چیزی است که روزی «database is locked»
    /// می‌شود. ‎Pooling‎ هم صریح روشن است تا هر ‎Create()‎ اتصالِ تازه از صفر
    /// باز نکند.
    ///
    /// ⚠️ ‎journal_mode=WAL‎ این‌جا لازم نیست چون در **خودِ فایل** می‌ماند
    /// (‎EnsureReady‎ یک بار می‌نویسدش)؛ ولی ‎busy_timeout‎ مالِ هر اتصال است
    /// و باید در رشتهٔ اتصال بیاید، وگرنه فقط روی همان یک اتصالِ
    /// ‎EnsureReady‎ می‌نشیند.
    /// </summary>
    private string ConnectionString =>
        $"Data Source={DbPath};Pooling=True;Default Timeout=30";

    public PumpDbContext Create()
    {
        var opts = new DbContextOptionsBuilder<PumpDbContext>()
            .UseSqlite(ConnectionString, o => o.CommandTimeout(30))
            .AddInterceptors(DbWatch.Instance, BusyWait.Instance)
            .Options;
        return new PumpDbContext(opts);
    }

    /// <summary>ساختِ دیتابیس اگر نباشد + روشن کردنِ کلیدِ خارجی و WAL.</summary>
    public void EnsureReady()
    {
        using var db = Create();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        db.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");

        // ══ وصله‌ها فقط یک بار برای هر ساختِ برنامه ══════════════════════════
        // هر سه وصله نقشهٔ کاملِ EF را می‌سازند و ده‌ها دستور را با استثنای
        // «از پیش هست» می‌دوانند — با هر بار باز شدنِ برنامه، حتی وقتی هیچ
        // چیزی عوض نشده. مهرِ ساخت (‎ModuleVersionId‎ی همین اسمبلی، که با هر
        // کامپایل عوض می‌شود) در جدولِ تنظیمات می‌نشیند؛ تا وقتی همان است،
        // دیتابیس همان است که همین ساخت یک بار وصله زده. نسخهٔ تازه ⇒ مهرِ
        // تازه ⇒ یک بارِ دیگر، و بعد دوباره آرام.
        // مهر شمارِ جدول‌ها و ایندکس‌های خودِ فایل را هم دارد: جدولی که (به هر
        // دلیل) افتاده باشد شمار را عوض می‌کند و وصله دوباره می‌رود.
        var stamp = typeof(PumpDbFactory).Assembly.ManifestModule.ModuleVersionId.ToString("N")
                    + ":" + SchemaObjects(db);
        if (ReadStamp(db) == stamp) return;

        PatchTables(db);
        PatchColumns(db);
        PatchSyncUid(db);
        PatchIndexes(db);
        WriteStamp(db, typeof(PumpDbFactory).Assembly.ManifestModule.ModuleVersionId.ToString("N")
                       + ":" + SchemaObjects(db));
    }

    /// <summary>شمارِ جدول‌ها و ایندکس‌های فایل — بخشِ دومِ مهر.</summary>
    private static long SchemaObjects(PumpDbContext db)
    {
        try
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            var opened = cmd.Connection!.State != System.Data.ConnectionState.Open;
            if (opened) cmd.Connection.Open();
            try
            {
                cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type IN ('table','index');";
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
            finally { if (opened) cmd.Connection.Close(); }
        }
        catch { return -1; }
    }

    private const string StampKey = "schema.stamp";

    private static string? ReadStamp(PumpDbContext db)
    {
        try
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            var opened = cmd.Connection!.State != System.Data.ConnectionState.Open;
            if (opened) cmd.Connection.Open();
            try
            {
                cmd.CommandText = "SELECT \"Value\" FROM \"Settings\" WHERE \"Key\" = @k LIMIT 1;";
                var prm = cmd.CreateParameter(); prm.ParameterName = "@k"; prm.Value = StampKey;
                cmd.Parameters.Add(prm);
                return cmd.ExecuteScalar() as string;
            }
            finally { if (opened) cmd.Connection.Close(); }
        }
        catch { return null; }   // جدولِ تنظیمات هنوز نیست ⇒ وصله بزن
    }

    private static void WriteStamp(PumpDbContext db, string stamp)
    {
        try
        {
            var row = db.Settings.FirstOrDefault(s => s.Key == StampKey);
            if (row is null) db.Settings.Add(new Setting { Key = StampKey, Value = stamp });
            else row.Value = stamp;
            db.SaveChanges();
        }
        catch { /* دفعهٔ بعد دوباره وصله می‌زند — بی‌ضرر */ }
    }

    /// <summary>
    /// ══ ایندکس‌های تازه روی دیتابیسِ قدیمی ═══════════════════════════════════
    ///
    /// ‎PatchTables‎ عمداً هر دستوری را که جدولش از پیش هست رد می‌کند — و
    /// ‎CREATE INDEX‎ هم یکی از همان‌هاست. نتیجه‌اش این بود که هر ایندکسی که
    /// بعد از انتشارِ اول به مدل اضافه شده، روی دیتابیسِ کاربر **ساخته
    /// نمی‌شد**. برنامه کار می‌کرد ولی هر جست‌وجو کلِ جدول را می‌خواند؛ با
    /// صدهزار ردیف همان می‌شود مکثی که کاربر حس می‌کند.
    ///
    /// این‌جا هر ‎CREATE INDEX‎ی که در نقشهٔ EF هست اجرا می‌شود. اگر ایندکس از
    /// پیش باشد، SQLite خطا می‌دهد و همان‌جا بی‌صدا رد می‌شود — هیچ داده‌ای
    /// لمس نمی‌شود.
    /// </summary>
    private static void PatchIndexes(PumpDbContext db)
    {
        foreach (var raw in db.Database.GenerateCreateScript()
                              .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var stmt = raw.Trim();
            if (!stmt.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)) continue;
            if (stmt.IndexOf("INDEX", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (stmt.IndexOf("TABLE", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            try { db.Database.ExecuteSqlRaw(stmt + ";"); }
            catch { /* از پیش هست */ }
        }
    }

    /// <summary>
    /// ══ جدول‌های تازه روی دیتابیسِ قدیمی ═══════════════════════════════════
    /// <c>EnsureCreated()</c> یا کلِ دیتابیس را می‌سازد یا — اگر از پیش باشد —
    /// هیچ نمی‌کند. پس موجودیتی که بعداً اضافه شود، روی نصبِ کاربر جدولی
    /// ندارد و اولین Query با «no such table» می‌شکند.
    ///
    /// این‌جا خودِ EF نقشهٔ کاملِ ساخت را می‌دهد
    /// (<c>GenerateCreateScript</c>) و فقط دستورهای مربوط به جدول‌هایی که
    /// **نیستند** اجرا می‌شوند. جدولِ موجود اصلاً لمس نمی‌شود، پس هیچ داده‌ای
    /// در خطر نیست.
    /// </summary>
    private static void PatchTables(PumpDbContext db)
    {
        var have = ExistingTables(db);
        var script = db.Database.GenerateCreateScript();

        foreach (var raw in script.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var stmt = raw.Trim();
            if (stmt.Length == 0) continue;

            var table = TableOf(stmt);
            if (table is null || have.Contains(table)) continue;

            try { db.Database.ExecuteSqlRaw(stmt + ";"); }
            catch { /* دستورِ تکراری یا ناشناخته نباید برنامه را بشکند */ }
        }
    }

    private const RegexOptions RxOpts = RegexOptions.IgnoreCase | RegexOptions.Singleline;

    /// <summary>نامِ جدولِ یک دستورِ ‎CREATE TABLE‎ یا ‎CREATE INDEX … ON‎.</summary>
    private static string? TableOf(string stmt)
    {
        var m = Regex.Match(stmt, "^CREATE\\s+TABLE\\s+\"(?<t>[^\"]+)\"", RxOpts);
        if (m.Success) return m.Groups["t"].Value;

        m = Regex.Match(stmt,
            "^CREATE\\s+(?:UNIQUE\\s+)?INDEX\\s+\"[^\"]+\"\\s+ON\\s+\"(?<t>[^\"]+)\"", RxOpts);
        return m.Success ? m.Groups["t"].Value : null;
    }

    private static HashSet<string> ExistingTables(PumpDbContext db)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        var opened = cmd.Connection!.State != System.Data.ConnectionState.Open;
        if (opened) cmd.Connection.Open();
        try
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using var r = cmd.ExecuteReader();
            while (r.Read()) set.Add(r.GetString(0));
        }
        finally { if (opened) cmd.Connection.Close(); }
        return set;
    }

    /// <summary>
    /// ══ ستون‌های تازه روی دیتابیسِ قدیمی ══════════════════════════════════
    /// <c>EnsureCreated()</c> فقط جدولِ **نبوده** را می‌سازد؛ به جدولِ موجود
    /// دست نمی‌زند. پس اگر روزی ستونی به یک موجودیت اضافه شود، دیتابیسِ
    /// کاربری که از پیش برنامه را دارد آن ستون را ندارد و هر Query با
    /// «no such column» می‌شکند — دادهٔ کاربر سالم است ولی برنامه بالا نمی‌آید.
    ///
    /// این‌جا همان ستون‌ها با <c>ALTER TABLE … ADD COLUMN</c> اضافه می‌شوند.
    /// در SQLite افزودنِ ستونِ nullable کارِ لحظه‌ای است و هیچ داده‌ای را
    /// بازنویسی نمی‌کند. هر ورودی «یک‌بار مصرف» است: اگر ستون باشد، رد می‌شود.
    /// </summary>
    private static void PatchColumns(PumpDbContext db)
    {
        // (جدول، ستون، نوع) — هر ستونی که پس از انتشارِ اول اضافه شده
        var wanted = new (string Table, string Column, string Type)[]
        {
            // ستونِ «واحد»ِ تراکنش‌های ورق — ‎t.unit‎ی سایت. صفر یعنی «تیل»،
            // پس دادهٔ کهنه بی هیچ کاری همان پیش‌فرضِ درست را می‌گیرد.
            ("WaraqTransactions", "Unit", "INTEGER NOT NULL DEFAULT 0"),
            ("WaraqPumps", "SrcKey", "TEXT"),
            ("WaraqPumps", "Worker", "TEXT"),
            ("WaraqPumps", "DateShamsi", "TEXT"),
            ("SafeEntries", "SrcKey", "TEXT"),
            ("Expenses", "SrcKey", "TEXT"),
            ("TankDips", "BookAdjust", "TEXT NOT NULL DEFAULT '0'"),
            ("TankerUnloads", "Manifest", "TEXT NOT NULL DEFAULT '0'"),
            ("TankerUnloads", "Actual", "TEXT NOT NULL DEFAULT '0'"),
            ("DebtAccounts", "MoneyDeposit", "TEXT"),
            ("DebtAccounts", "ReceiptsMigrated", "INTEGER NOT NULL DEFAULT 0"),
            ("ExchangeRows", "LegacyId", "TEXT"),
            ("CompanyRows", "SourceExchangeId", "TEXT"),
            ("Expenses", "SalaryStaffId", "INTEGER"),
            ("Expenses", "SalaryMonth", "TEXT"),
            // «جدول جدید»ِ شرکت — نقطهٔ شمارشِ خریدهای هر تیل (‎purchaseCheckpoint‎)
            ("TilCompanies", "PurchaseCheckpointPetrol", "INTEGER NOT NULL DEFAULT 0"),
            ("TilCompanies", "PurchaseCheckpointDiesel", "INTEGER NOT NULL DEFAULT 0"),
            // کیو‌آرِ زنده — رمزِ هر حساب؛ خالی یعنی کیو‌آری ساخته نشده
            ("DebtAccounts", "QrKey", "TEXT"),
            ("TilCompanies", "QrKey", "TEXT"),
            // واحدِ رسیدِ قرض‌داران — ۲ یعنی ‎LedgerMode.Money‎، پس هر ردیفِ
            // کهنه بی هیچ کاری همان «پول»ِ درست را می‌گیرد؛ ۱ یعنی پطرول.
            ("DebtQuickReceipts", "Unit", "INTEGER NOT NULL DEFAULT 2"),
            ("DebtQuickReceipts", "Fuel", "INTEGER NOT NULL DEFAULT 1"),
            // نوعِ تیلِ ردیفِ «رسید پارچه‌ها». ⛔ ‎NULL‎پذیر است و پیش‌فرض ندارد:
            // ‎NULL‎ یعنی «خودکار». با ‎DEFAULT 1‎ هر رسیدِ دیزلیِ ثبت‌شده یک‌شبه پطرول می‌شد.
            ("ParchaReceipts", "Fuel", "INTEGER"),
            // نشانِ «شروعِ پایه از پایهٔ قبلی کمتر بود» روی ردیفِ ورق — فقط رنگ،
            // هیچ محاسبه‌ای از آن نمی‌گذرد. ۰ یعنی سالم، پس ردیفِ کهنه سرخ نمی‌شود.
            ("WaraqPumps", "LowBase", "INTEGER NOT NULL DEFAULT 0"),
            // ⛔ «این دفترِ همگام‌سازی مالِ کدام حساب است» — بی این، ورود با
            // حسابِ دیگر روی همین نصب دادهٔ این کامپیوتر را هیچ‌وقت به حسابِ
            // تازه نمی‌رساند. خالی یعنی «نمی‌دانیم» و هیچ چیزی را از نو
            // نمی‌کند، پس نصبِ امروزی بی‌دردسر بالا می‌آید. (`SyncStore.BindTo`)
            ("SyncState", "AccountId", "TEXT NOT NULL DEFAULT ''"),
            // ⛔ «نخستین گرفتنِ کاملِ دفترِ این حساب تمام شد یا نه» — پردهٔ
            // «آوردنِ اطلاعاتِ حساب» از همین می‌آید. صفر یعنی هنوز نه، ولی
            // نصبِ امروزی که از قبل همگام است ‎Cursor > 0‎ دارد و پرده‌ای
            // نمی‌بیند (`SyncEngine.PrimeWanted`).
            ("SyncState", "PrimedAt", "INTEGER NOT NULL DEFAULT 0"),
        };

        foreach (var (table, column, type) in wanted)
        {
            if (!TableExists(db, table)) continue;
            if (ColumnExists(db, table, column)) continue;
            db.Database.ExecuteSqlRaw($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type};");
        }
    }

    /// <summary>
    /// ══ ستونِ <c>SyncUid</c> روی هر جدولِ داده — مهاجرتِ Sync v1 ═══════════
    ///
    /// بندِ ۲۰٫۱ پرامپت شناسهٔ ULID برای هر ردیف می‌خواهد. کلیدِ اصلیِ این
    /// برنامه شمارهٔ خودافزا است و <b>عوض نمی‌شود</b> (چرایش در
    /// <c>native/docs/SYNC-fa.md</c>)؛ به‌جایش هر ردیف یک شناسهٔ رشته‌ایِ
    /// ثابت می‌گیرد که کنارِ کلید می‌نشیند.
    ///
    /// ── چرا هیچ داده‌ای در خطر نیست ────────────────────────────────────
    /// افزودنِ ستونِ nullable در SQLite جدول را بازنویسی نمی‌کند و لحظه‌ای
    /// است؛ و پر کردنش <b>یک</b> <c>UPDATE</c> برای هر جدول است، نه یکی
    /// برای هر ردیف: شناسهٔ ردیف‌های کهنه از «ریشهٔ همین نصب + شمارهٔ ردیف»
    /// ساخته می‌شود. یکتا در جدول (چون <c>Id</c> یکتاست) و یکتا میانِ
    /// دستگاه‌ها (چون ریشه تصادفی و مالِ همین نصب است).
    ///
    /// ⚠️ ردیف‌های <b>تازه</b> ULIDِ واقعی می‌گیرند
    /// (<see cref="PumpYaqobi.Domain.Ulid"/>). ردیف‌های کهنه ترتیبِ زمانی
    /// ندارند، و لازم هم ندارند: ترتیب را <c>server_seq</c>ِ سرور می‌گوید.
    ///
    /// ⛔ ریشه یک بار ساخته می‌شود و <b>هیچ‌وقت عوض نمی‌شود</b>. عوض شدنش
    /// یعنی همان ردیف‌ها بارِ دوم با شناسهٔ دیگری به سرور می‌روند — یعنی
    /// کلِ دفتر دو برابر.
    /// </summary>
    private void PatchSyncUid(PumpDbContext db)
    {
        var seed = SyncUidSeed(db);
        if (seed.Length == 0) return;

        //  ⛔ **پشتیبانِ رمزشده پیش از مهاجرت** (بندِ ۹ی پرامپتِ ۲۲). فقط
        //  وقتی واقعاً کاری در پیش است: اگر ستون از پیش هست، مهاجرتی در
        //  کار نیست و یک نسخهٔ تکراری فقط دیسک می‌خورد.
        if (NeedsSyncUid(db)) SyncBackup.Write(this, label: "pre-sync");

        foreach (var et in db.Model.GetEntityTypes())
        {
            if (!typeof(EntityBase).IsAssignableFrom(et.ClrType)) continue;
            var table = et.GetTableName();
            if (string.IsNullOrEmpty(table) || !TableExists(db, table)) continue;

            try
            {
                if (!ColumnExists(db, table, "SyncUid"))
                    db.Database.ExecuteSqlRaw($"ALTER TABLE \"{table}\" ADD COLUMN \"SyncUid\" TEXT;");

                //  یک دستور برای کلِ جدول — نه یکی برای هر ردیف
                db.Database.ExecuteSqlRaw(
                    $"UPDATE \"{table}\" SET \"SyncUid\" = '{seed}' || \"Id\" " +
                    "WHERE \"SyncUid\" IS NULL OR \"SyncUid\" = '';");

                //  «این ردیف کدام است؟» — پرس‌وجوی هر opی که از سرور می‌آید
                db.Database.ExecuteSqlRaw(
                    $"CREATE INDEX IF NOT EXISTS \"IX_{table}_SyncUid\" ON \"{table}\" (\"SyncUid\");");
            }
            catch { /* جدولی که ستونِ Id ندارد یا دستِ سیستم است — بی‌ضرر رد شود */ }
        }
    }

    /// <summary>جدولی هست که هنوز ستونِ <c>SyncUid</c> ندارد؟</summary>
    private static bool NeedsSyncUid(PumpDbContext db)
    {
        foreach (var et in db.Model.GetEntityTypes())
        {
            if (!typeof(EntityBase).IsAssignableFrom(et.ClrType)) continue;
            var table = et.GetTableName();
            if (string.IsNullOrEmpty(table) || !TableExists(db, table)) continue;
            if (!ColumnExists(db, table, "SyncUid")) return true;
        }
        return false;
    }

    /// <summary>
    /// ریشهٔ شناسه‌های کهنه. یک بار ساخته و در <c>SyncState</c> نگه داشته
    /// می‌شود؛ بارهای بعد همان برمی‌گردد.
    /// </summary>
    private static string SyncUidSeed(PumpDbContext db)
    {
        try
        {
            var row = db.SyncState.FirstOrDefault(x => x.Id == 1);
            if (row is not null && !string.IsNullOrEmpty(row.UidSeed)) return row.UidSeed;

            //  بیست‌وشش نویسهٔ ULID + یک خطِ تیره ⇒ شناسه‌ای که با ULIDهای
            //  تازه قاطی نمی‌شود و از هشتاد نویسهٔ سقفِ سرور هم نمی‌گذرد.
            var seed = PumpYaqobi.Domain.Ulid.New() + "-";
            if (row is null) db.SyncState.Add(new SyncStateRow { Id = 1, UidSeed = seed });
            else row.UidSeed = seed;
            //  ⚠️ دفترِ تغییرات این‌جا خاموش است: مهاجرت خودش «تغییرِ کاربر»
            //  نیست و صدهزار opِ بی‌مصرف می‌ساخت.
            var was = OpLog.Enabled;
            OpLog.Enabled = false;
            try { db.SaveChanges(); } finally { OpLog.Enabled = was; }
            return seed;
        }
        catch { return ""; }
    }

    private static bool TableExists(PumpDbContext db, string table)
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        var opened = cmd.Connection!.State != System.Data.ConnectionState.Open;
        if (opened) cmd.Connection.Open();
        try
        {
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n LIMIT 1;";
            var p = cmd.CreateParameter(); p.ParameterName = "$n"; p.Value = table; cmd.Parameters.Add(p);
            return cmd.ExecuteScalar() is not null;
        }
        finally { if (opened) cmd.Connection.Close(); }
    }

    private static bool ColumnExists(PumpDbContext db, string table, string column)
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        var opened = cmd.Connection!.State != System.Data.ConnectionState.Open;
        if (opened) cmd.Connection.Open();
        try
        {
            cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        finally { if (opened) cmd.Connection.Close(); }
    }
}
