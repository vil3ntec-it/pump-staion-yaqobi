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
        PatchColumns(db);
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
            ("WaraqPumps", "SrcKey", "TEXT"),
            ("WaraqPumps", "Worker", "TEXT"),
            ("WaraqPumps", "DateShamsi", "TEXT"),
            ("SafeEntries", "SrcKey", "TEXT"),
            ("TankDips", "BookAdjust", "TEXT NOT NULL DEFAULT '0'"),
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
