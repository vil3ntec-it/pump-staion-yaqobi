using PumpYaqobi.Domain.Entities;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// یک‌جا ساختنِ اتصالِ دیتابیس. مسیرِ فایل کنارِ دادهٔ کاربر است، نه کنارِ EXE،
/// تا نصبِ دوباره یا به‌روزرسانی داده را نبَرد.
/// </summary>
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

    public PumpDbContext Create()
    {
        var opts = new DbContextOptionsBuilder<PumpDbContext>()
            .UseSqlite($"Data Source={DbPath}")
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
        };

        foreach (var (table, column, type) in wanted)
        {
            if (!TableExists(db, table)) continue;
            if (ColumnExists(db, table, column)) continue;
            db.Database.ExecuteSqlRaw($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type};");
        }
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
