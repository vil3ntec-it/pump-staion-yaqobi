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
    }
}
