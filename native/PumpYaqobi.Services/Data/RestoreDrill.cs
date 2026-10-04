using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>نتیجهٔ یک آزمونِ بازیابی.</summary>
public sealed record DrillResult(bool Ok, string Text, string When, int Tables);

/// <summary>
/// ══ شورا، ب۵ — آزمونِ بازیابیِ ماهانه ═══════════════════════════════════════
///
/// «فقط داشتنِ فایلِ پشتیبان هیچ‌وقت کافی نیست.» ماهی یک بار برای هر دفتر:
/// یک عکسِ تازه گرفته می‌شود، <b>رونوشتش</b> در پوشهٔ موقت باز می‌شود،
/// ‎integrity_check‎ و شمارِ ردیفِ <b>هر</b> جدولِ داده با خودِ دفتر سنجیده
/// می‌شود (همان قاعدهٔ ‎FullBackup.VerifyRestored‎)، و نتیجه در پروفایل می‌نشیند.
/// ⛔ فقط می‌خواند: دفتر و عکس دست نمی‌خورند؛ رونوشتِ موقت پاک می‌شود.
/// ⚠️ اگر کاربر وسطِ سنجش چیزی نوشت، یک بارِ دیگر سنجیده می‌شود — ناجوریِ
/// گذرا هشدارِ دروغ نیست.
/// </summary>
public sealed class RestoreDrill
{
    public const string MonthKeyName = "drill.month", ResultKey = "drill.result";
    private readonly PumpDbFactory _dbf;
    private readonly BackupService _backup;

    public RestoreDrill(PumpDbFactory dbf, BackupService backup) { _dbf = dbf; _backup = backup; }

    /// <summary>
    /// یک فایلِ پشتیبانِ مشخص را با دفترِ زنده می‌سنجد. ‎null‎ یعنی سالم و برابر؛
    /// وگرنه جملهٔ آدمیزادِ نخستین ایراد.
    /// </summary>
    public static string? Verify(string backupPath, string liveDbPath, out int tables)
    {
        tables = 0;
        if (!File.Exists(backupPath)) return "فایلِ پشتیبان پیدا نشد";
        var dir = Path.GetDirectoryName(backupPath) ?? Path.GetTempPath();
        var tmp = Path.Combine(dir, "tmp-drill-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        try
        {
            File.Copy(backupPath, tmp, overwrite: true);
            if (!FullBackup.IntegrityOk(tmp)) return "فایلِ پشتیبان خراب است (integrity_check)";
            if (BackupService.Inspect(tmp) < 0) return "فایلِ پشتیبان دفترِ این برنامه نیست";
            var got = FullBackup.CountRows(tmp);
            var live = FullBackup.CountRows(liveDbPath);
            if (got is null) return "فایلِ پشتیبان خوانده نشد";
            if (live is null) return "دفترِ برنامه خوانده نشد";
            foreach (var (table, n) in live)
            {
                if (FullBackup.Housekeeping.Contains(table) || table is "SyncConflicts") continue;
                tables++;
                if (!got.TryGetValue(table, out var b)) return $"«{table}» در پشتیبان نیست";
                if (b != n) return $"«{table}»: در دفتر {n} ردیف، در پشتیبان {b} ردیف";
            }
            return null;
        }
        catch (Exception ex) { return "پشتیبان باز نشد: " + ex.GetType().Name; }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { tmp, tmp + "-wal", tmp + "-shm" })
                try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    /// <summary>عکسِ تازه و سنجش — دو بار اگر بارِ اول ناجور بود (نوشتنِ هم‌زمان).</summary>
    public DrillResult RunNow()
    {
        string? why = "عکس گرفته نشد";
        var n = 0;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var snap = _backup.SnapshotToday();
            if (snap is null) { why = "عکس گرفته نشد"; continue; }
            why = Verify(snap, _dbf.DbPath, out n);
            if (why is null) break;
        }
        var when = Shamsi.Today();
        var text = why is null
            ? $"✅ آزمونِ بازیابی {when}: آخرین بکاپ باز شد و هر {Shamsi.Money(n)} جدول با دفتر برابر بود"
            : $"❌ آزمونِ بازیابی {when}: {why} — همین حالا یک «💾 ذخیرهٔ فایلِ بکاپ» بگیرید و نگه دارید";
        Save(text, why is null);
        return new DrillResult(why is null, text, when, n);
    }

    /// <summary>آخرین نتیجهٔ ذخیره‌شده (برای پروفایل)؛ ‎null‎ یعنی هنوز سنجیده نشده.</summary>
    public DrillResult? Last()
    {
        try
        {
            using var db = _dbf.Create();
            var r = db.Settings.AsNoTracking().FirstOrDefault(x => x.Key == ResultKey)?.Value;
            if (string.IsNullOrEmpty(r)) return null;
            return new DrillResult(r.StartsWith('✅'), r, "", 0);
        }
        catch { return null; }
    }

    /// <summary>ماهی یک بار برای هر دفتر — ماهِ این‌بار سنجیده شده ⇒ هیچ کاری.</summary>
    public DrillResult? MonthlyOnce()
    {
        try
        {
            var month = Shamsi.MonthKey(Shamsi.Today());
            using (var db = _dbf.Create())
                if (db.Settings.AsNoTracking().Any(x => x.Key == MonthKeyName && x.Value == month)) return null;
            var res = RunNow();
            using (var db = _dbf.Create())
            {
                var s = db.Settings.FirstOrDefault(x => x.Key == MonthKeyName);
                if (s is null) db.Settings.Add(new Setting { Key = MonthKeyName, Value = month }); else s.Value = month;
                db.SaveChanges();
            }
            return res;
        }
        catch { return null; }   // بارِ بعد دوباره
    }

    private void Save(string text, bool ok)
    {
        try
        {
            using var db = _dbf.Create();
            var s = db.Settings.FirstOrDefault(x => x.Key == ResultKey);
            if (s is null) db.Settings.Add(new Setting { Key = ResultKey, Value = text }); else s.Value = text;
            db.SaveChanges();
        }
        catch { }
    }

    /// <summary>کارِ در جریان — سنجه‌ها منتظرش می‌مانند.</summary>
    public Task<DrillResult?>? MonthlyTask { get; private set; }

    /// <summary>روی نخِ دیگر؛ دو صدا زدنِ هم‌زمان یکی می‌شوند.</summary>
    public Task<DrillResult?> StartMonthly()
    {
        lock (this)
        {
            if (MonthlyTask is { IsCompleted: false } running) return running;
            return MonthlyTask = Task.Run(MonthlyOnce);
        }
    }
}
