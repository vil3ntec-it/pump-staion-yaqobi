using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;

namespace PumpYaqobi.Services.Data;

/// <summary>یک بکاپِ روی دیسک — چه عکسِ خودکارِ روزانه، چه فایلی که کاربر ساخته.</summary>
/// <param name="Path">مسیرِ کاملِ فایل.</param>
/// <param name="Day">تاریخِ شمسیِ روزی که گرفته شده (‎1404/06/16‎).</param>
/// <param name="TakenAt">زمانِ واقعیِ ساخت.</param>
/// <param name="Bytes">اندازه.</param>
public sealed record BackupFile(string Path, string Day, DateTime TakenAt, long Bytes)
{
    public string SizeText => Bytes >= 1024 * 1024
        ? Shamsi.Money(Math.Round(Bytes / 1048576m, 1), 1) + " مگابایت"
        : Shamsi.Money(Math.Round(Bytes / 1024m)) + " کیلوبایت";
}

/// <summary>نتیجهٔ یک بازگردانی — پیامش همان چیزی است که به کاربر نشان داده می‌شود.</summary>
public sealed record RestoreOutcome(bool Ok, string Message, string? SafetyCopy = null, int Records = 0);

/// <summary>
/// ══ بکاپ و بازگردانی — بندِ ۲۳ ══════════════════════════════════════════════
/// نسخهٔ وب سه چیز داشت و هر سه این‌جا هست:
///
///   • ‎_autoDailyBackup‎ — روزی یک عکس، چهارده تای آخر نگه داشته می‌شود.
///   • ‎downloadBackupNow‎ — یک فایل که کاربر جای امن می‌گذارد.
///   • ‎_bkRestoreData‎ / ‎restoreSnapshot‎ — برگرداندنِ همان فایل یا عکس.
///
/// ⚠️ فرقِ اساسی با نسخهٔ وب: آن‌جا بکاپ یک JSON از کلِ ‎DB‎ بود و هر بار باید
/// دوباره به رکورد تبدیل می‌شد — یعنی هر اشتباهِ کوچکِ نگاشت، دادهٔ کاربر را
/// خاموش عوض می‌کرد. این‌جا بکاپ **خودِ فایلِ دیتابیس** است، پس هیچ نگاشتی در
/// کار نیست و برگرداندنش دقیقاً همان چیزی است که بود — تا آخرین رقم.
///
/// عکس‌برداری با ‎VACUUM INTO‎ انجام می‌شود، نه ‎File.Copy‎: کپیِ ساده وسطِ یک
/// نوشتن، فایلی نیم‌بند می‌سازد (WAL روشن است و بخشی از داده هنوز در ‎-wal‎
/// است). ‎VACUUM INTO‎ یک عکسِ یکپارچه و فشرده می‌دهد، حتی اگر همان لحظه
/// چیزی در حال نوشته شدن باشد.
/// </summary>
public sealed class BackupService
{
    /// <summary>چهارده عکسِ آخر — همان عددِ نسخهٔ وب.</summary>
    public const int KeepSnapshots = 14;

    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;

    public BackupService(PumpDbFactory dbf, PermissionService perm)
    { _dbf = dbf; _perm = perm; }

    /// <summary>پوشهٔ عکس‌های خودکار — کنارِ خودِ دیتابیس.</summary>
    public string SnapshotDir =>
        Path.Combine(Path.GetDirectoryName(_dbf.DbPath) ?? ".", "backups");

    /// <summary>نامِ پیشنهادی برای فایلی که کاربر ذخیره می‌کند.</summary>
    public static string SuggestedFileName() =>
        "pump-backup-" + Shamsi.Today().Replace('/', '-') + ".db";

    // ── گرفتن ────────────────────────────────────────────────────────────────

    /// <summary>
    /// یک عکسِ یکپارچه در مسیرِ داده‌شده. اگر فایل باشد، اول برداشته می‌شود
    /// (‎VACUUM INTO‎ روی فایلِ موجود کار نمی‌کند).
    /// </summary>
    public void WriteSnapshot(string target)
    {
        _perm.Require(Permission.Backup);
        WriteSnapshotCore(target);
    }

    /// <summary>
    /// همان کار، بی اجازه‌خواهی — فقط برای عکسِ **خودکار** و عکسِ ایمنیِ پیش
    /// از بازگردانی.
    ///
    /// ⚠️ چرا اجازه نمی‌خواهد: عکسِ خودکار کارِ خودِ برنامه است نه کارِ کاربر.
    /// اگر اجازه می‌خواست، روزی که کارمند وارد می‌شد هیچ عکسی گرفته نمی‌شد —
    /// یعنی درست همان روزی که بیشتر از همه لازم است. این کار هیچ داده‌ای را
    /// عوض نمی‌کند؛ فقط یک رونوشت در پوشهٔ خودِ برنامه می‌گذارد.
    /// </summary>
    private void WriteSnapshotCore(string target)
    {
        var dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(target)) File.Delete(target);

        using var db = _dbf.Create();
        // نامِ فایل داخلِ رشتهٔ SQL می‌رود، پس تک‌کوتیشن دوتا می‌شود — همان
        // قاعدهٔ خودِ SQLite. (پارامتر این‌جا پذیرفته نمی‌شود.)
        var quoted = target.Replace("'", "''");
        db.Database.ExecuteSqlRaw($"VACUUM INTO '{quoted}';");
    }

    /// <summary>
    /// عکسِ روزانه (‎_autoDailyBackupNow‎). روزی یک‌بار؛ اگر عکسِ امروز هست،
    /// دوباره گرفته می‌شود تا تازه‌ترین حال را داشته باشد.
    ///
    /// خروجی: مسیرِ عکس، یا ‎null‎ اگر نشد. **هرگز استثنا پرتاب نمی‌کند** —
    /// بکاپِ خودکار نباید باز شدنِ برنامه را بشکند.
    /// </summary>
    public string? SnapshotToday()
    {
        try
        {
            var day = Shamsi.Today().Replace('/', '-');
            var target = Path.Combine(SnapshotDir, "pump-" + day + ".db");
            WriteSnapshotCore(target);
            Prune();
            return target;
        }
        catch { return null; }
    }

    /// <summary>کهنه‌ترها را می‌برد و فقط چهارده تای آخر می‌ماند.</summary>
    public void Prune()
    {
        try
        {
            var extra = List().Skip(KeepSnapshots).ToList();
            foreach (var f in extra)
                try { File.Delete(f.Path); } catch { }
        }
        catch { }
    }

    /// <summary>عکس‌های موجود، از تازه به کهنه.</summary>
    public IReadOnlyList<BackupFile> List()
    {
        if (!Directory.Exists(SnapshotDir)) return Array.Empty<BackupFile>();
        var list = new List<BackupFile>();
        foreach (var path in Directory.EnumerateFiles(SnapshotDir, "pump-*.db"))
        {
            FileInfo fi;
            try { fi = new FileInfo(path); } catch { continue; }
            var stem = Path.GetFileNameWithoutExtension(path);
            var day = stem.StartsWith("pump-", StringComparison.Ordinal)
                ? stem[5..].Replace('-', '/') : stem;
            list.Add(new BackupFile(path, day, fi.LastWriteTime, fi.Length));
        }
        return list.OrderByDescending(x => x.TakenAt).ToList();
    }

    // ── خواندن پیش از برگرداندن ──────────────────────────────────────────────

    /// <summary>
    /// این فایل واقعاً بکاپِ همین برنامه است؟ و چند رکورد دارد؟
    ///
    /// ‎-1‎ یعنی «نه» — یا فایلِ SQLite نیست، یا جدول‌های این برنامه را ندارد.
    /// بی این سنجش، کاربر می‌توانست هر فایلی را جایگزینِ حسابِ چندساله‌اش کند.
    /// </summary>
    public static int Inspect(string path)
    {
        try
        {
            if (!File.Exists(path)) return -1;
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            using var con = new SqliteConnection(cs);
            con.Open();

            using var check = con.CreateCommand();
            check.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
                "('Debtors','DebtAccounts','DebtRows','WaraqEntries','SafeEntries');";
            if (Convert.ToInt32(check.ExecuteScalar()) < 5) return -1;

            using var count = con.CreateCommand();
            count.CommandText =
                "SELECT (SELECT COUNT(*) FROM Debtors) + (SELECT COUNT(*) FROM DebtRows)" +
                " + (SELECT COUNT(*) FROM WaraqEntries) + (SELECT COUNT(*) FROM SafeEntries)" +
                " + (SELECT COUNT(*) FROM Expenses) + (SELECT COUNT(*) FROM Invoices);";
            return Convert.ToInt32(count.ExecuteScalar());
        }
        catch { return -1; }
    }

    // ── برگرداندن ────────────────────────────────────────────────────────────

    /// <summary>
    /// ══ بازگردانی ═════════════════════════════════════════════════════════════
    /// ترتیبش عمدی است و هر گامش برای یک خرابیِ واقعی است:
    ///
    ///   ۱. فایل سنجیده می‌شود — بکاپِ این برنامه نباشد، هیچ اتفاقی نمی‌افتد.
    ///   ۲. از حالِ **فعلی** یک عکسِ ایمنی گرفته می‌شود. اگر کاربر بکاپِ اشتباه
    ///      را برگرداند، راهِ برگشت هست. (نسخهٔ وب این را نداشت.)
    ///   ۳. اتصال‌های باز بسته می‌شوند، وگرنه ویندوز اجازهٔ بازنویسیِ فایل را
    ///      نمی‌دهد و بازگردانی خاموش شکست می‌خورد.
    ///   ۴. فایل جایگزین می‌شود و ‎-wal‎/‎-shm‎ی کهنه برداشته می‌شوند — ماندنشان
    ///      یعنی SQLite تغییرهای دیتابیسِ **قبلی** را روی فایلِ تازه بازپخش کند.
    ///   ۵. دیتابیسِ تازه باز و آماده می‌شود (ستون/جدولِ نداشته اضافه شود).
    /// </summary>
    public RestoreOutcome Restore(string path)
    {
        _perm.Require(Permission.Restore);

        var records = Inspect(path);
        if (records < 0)
            return new RestoreOutcome(false, "این فایل بکاپِ این برنامه نیست — چیزی عوض نشد");

        var safety = SafetyCopy();
        if (safety is null && File.Exists(_dbf.DbPath))
            return new RestoreOutcome(false, "عکسِ ایمنی گرفته نشد — بازگردانی انجام نشد");

        try
        {
            SqliteConnection.ClearAllPools();

            File.Copy(path, _dbf.DbPath, overwrite: true);
            foreach (var side in new[] { _dbf.DbPath + "-wal", _dbf.DbPath + "-shm" })
                try { if (File.Exists(side)) File.Delete(side); } catch { }

            _dbf.EnsureReady();
        }
        catch (Exception ex)
        {
            return new RestoreOutcome(false,
                "بازگردانی انجام نشد: " + (ex.InnerException?.Message ?? ex.Message), safety);
        }

        return new RestoreOutcome(true,
            $"✅ بازگردانی شد — {Shamsi.Money(records)} رکورد", safety, records);
    }

    /// <summary>عکسی از حالِ فعلی، پیش از بازگردانی. ‎null‎ یعنی نشد.</summary>
    public string? SafetyCopy()
    {
        try
        {
            if (!File.Exists(_dbf.DbPath)) return null;
            var stem = "پیش‌از‌بازگردانی-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            for (var n = 1; n <= 200; n++)
            {
                var target = Path.Combine(SnapshotDir, n == 1 ? stem + ".db" : $"{stem}-{n}.db");
                if (File.Exists(target)) continue;
                WriteSnapshotCore(target);
                return target;
            }
            return null;
        }
        catch { return null; }
    }
}
