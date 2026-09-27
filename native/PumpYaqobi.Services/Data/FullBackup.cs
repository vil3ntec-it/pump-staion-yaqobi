using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ «فایلِ کاملِ برنامه» — همه‌چیز در یک فایل، مثلِ یک فایلِ اکسل ═══════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «مثلِ اکسل که تمامِ اطلاعات را دارد و یارو
/// خیلی آسان می‌تواند اطلاعاتش را توی فلش یا هر جا که خواست ببرد… تمامِ
/// حساب‌ها و تم‌ها و تنظیماتی که درست کرده بود… و اگر آن را توی برنامه آوردم،
/// اطلاعاتِ همان فایل همه‌شان بیاید — با دقت، اطلاعات است و خیلی مهم.»
///
/// شکلِ فایل (یک ‎zip‎ با پسوندِ <see cref="Extension"/>):
/// <code>
///   manifest.json   قالب، نسخهٔ برنامه، زمان، شمارِ ردیفِ هر جدول، اثرِ انگشتِ دفتر
///   pump.db         خودِ دفتر — عکسِ یکپارچهٔ ‎VACUUM INTO‎، همان بکاپِ همیشگی
///   settings.json   تنظیماتِ «قابلِ بردن» (تم، ترتیبِ نوار، پهنای ستون‌ها، …)
/// </code>
///
/// ⛔ <b>دفتر خودِ فایلِ SQLite است، نه JSON.</b> همان قاعدهٔ <see cref="BackupService"/>:
/// هیچ نگاشتی در کار نیست، پس برگرداندنش مو‌به‌مو همان است — تا آخرین رقم.
///
/// ⛔ <b>آوردن فقط پس از چهار سنجش</b> (<see cref="Read"/>): قالب و نسخه ·
/// اثرِ انگشتِ SHA-256ِ دفتر · <c>PRAGMA integrity_check</c> · و شمارِ ردیفِ
/// <b>هر</b> جدول با همان عددی که هنگامِ ساختن نوشته شد. یکی نخواند ⇒ هیچ
/// چیزی دست نمی‌خورد. فایلی که روی فلش نیمه کپی شده یا دست خورده، هرگز
/// جای دفترِ سالم را نمی‌گیرد.
///
/// ⛔ <b>هیچ رازی داخلِ فایل نیست</b> — نه توکنِ حساب، نه توکنِ دستگاه، نه
/// مجوز، نه رمزِ سرورِ خانگی. فهرستِ «قابلِ بردن» را خودِ برنامه می‌سازد
/// (<c>PortableSettings</c>)؛ این کلاس فقط همان متن را جابه‌جا می‌کند.
/// (رمزِ قفلِ برنامه به شکلِ هشِ ‎pbkdf2‎ داخلِ خودِ دفتر است و با دفتر
/// می‌آید — همان که روی کامپیوترِ قبلی بود.)
/// </summary>
public static class FullBackup
{
    /// <summary>پسوندِ فایل — تا در فلش از هر فایلِ دیگری جدا باشد.</summary>
    public const string Extension = ".pumpyaqobi";

    /// <summary>نامِ قالب داخلِ ‎manifest‎ — فایلِ غریبه با پسوندِ درست هم رد می‌شود.</summary>
    public const string FormatName = "pump-yaqobi-full-backup";

    /// <summary>
    /// نسخهٔ قالب. ⚠️ اگر روزی شکلِ فایل عوض شد، این یک پله بالا می‌رود و
    /// نسخهٔ کهنهٔ برنامه فایلِ تازه را <b>رد می‌کند</b>، نه این‌که نیمه بخواند.
    /// </summary>
    public const int FormatVersion = 1;

    private const string ManifestEntry = "manifest.json";
    private const string DbEntry = "pump.db";
    private const string SettingsEntry = "settings.json";

    /// <summary>سقفِ ایمنی برای خودِ دفتر پس از باز شدن — فایلِ ساختگیِ «بمبِ زیپ» فلش را پر نکند.</summary>
    private const long MaxDbBytes = 8L * 1024 * 1024 * 1024;

    /// <summary>نامِ پیشنهادی: «پمپ-یعقوبی-۱۴۰۵-۰۷-۱۵.pumpyaqobi».</summary>
    public static string SuggestedFileName(string? pumpName = null)
    {
        var name = string.IsNullOrWhiteSpace(pumpName) ? "پمپ یعقوبی" : pumpName.Trim();
        //  ⚠️ نویسه‌های ممنوعِ **ویندوز**، نه فقط سیستمِ همین لحظه — فایل روی فلش
        //  به کامپیوترِ دیگر می‌رود.
        foreach (var c in Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*")) name = name.Replace(c, ' ');
        name = string.Join('-', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length > 40) name = name[..40];
        return name + "-" + Shamsi.Today().Replace('/', '-') + Extension;
    }

    // ══ ساختن ══════════════════════════════════════════════════════════════

    /// <summary>
    /// فایلِ کامل را می‌سازد. اول در یک فایلِ موقتِ کنارِ مقصد، بعد جابه‌جایی —
    /// پس فلشی که وسطِ کار کشیده شود، فایلِ قبلیِ همان نام را خراب نمی‌کند.
    /// </summary>
    /// <param name="backup">همان سرویسِ بکاپ — اجازهٔ <c>Backup</c> را خودش می‌خواهد.</param>
    /// <param name="target">مسیرِ فایلِ خروجی.</param>
    /// <param name="appVersion">نسخهٔ همین برنامه — برنامهٔ کهنه‌تر فایل را رد می‌کند.</param>
    /// <param name="settingsJson">تنظیماتِ «قابلِ بردن»؛ خالی ⇒ فقط دفتر.</param>
    /// <param name="pumpName">نامِ پمپ — فقط برای نشان دادن هنگامِ آوردن.</param>
    public static FullBackupInfo Write(BackupService backup, string target, string appVersion,
                                       string? settingsJson, string? pumpName = null)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(target)) ?? ".";
        Directory.CreateDirectory(dir);

        var work = Path.Combine(Path.GetTempPath(), "pyq-full-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var partial = target + ".part";
        try
        {
            var db = Path.Combine(work, DbEntry);
            backup.WriteSnapshot(db);                  // اجازهٔ Backup همین‌جا خواسته می‌شود

            var counts = CountRows(db) ?? throw new InvalidOperationException("دفترِ ساخته‌شده خوانده نشد");
            var sha = Sha256(db);
            var bytes = new FileInfo(db).Length;

            var manifest = new JsonObject
            {
                ["format"] = FormatName,
                ["formatVersion"] = FormatVersion,
                ["appVersion"] = appVersion,
                ["createdUtc"] = DateTime.UtcNow.ToString("O"),
                ["shamsi"] = Shamsi.Today(),
                ["time"] = DateTime.Now.ToString("HH:mm"),
                ["pumpName"] = pumpName ?? "",
                ["db"] = new JsonObject { ["sha256"] = sha, ["bytes"] = bytes },
                ["tables"] = ToJson(counts),
                ["hasSettings"] = !string.IsNullOrWhiteSpace(settingsJson),
            };

            if (File.Exists(partial)) File.Delete(partial);
            using (var zip = ZipFile.Open(partial, ZipArchiveMode.Create))
            {
                WriteText(zip, ManifestEntry, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                zip.CreateEntryFromFile(db, DbEntry, CompressionLevel.Optimal);
                if (!string.IsNullOrWhiteSpace(settingsJson)) WriteText(zip, SettingsEntry, settingsJson);
            }

            //  ⛔ پیش از جابه‌جایی، همان فایل دوباره خوانده و سنجیده می‌شود —
            //  «ساخته شد» یعنی «خوانده می‌شود»، نه «چیزی روی دیسک نوشته شد».
            var check = Read(partial, appVersion);
            if (!check.Ok) throw new InvalidOperationException("فایلِ ساخته‌شده سنجش را رد نکرد: " + check.Why);
            check.Dispose();

            File.Move(partial, target, overwrite: true);
            return new FullBackupInfo(true, "", appVersion, Shamsi.Today(), DateTime.Now.ToString("HH:mm"),
                                      pumpName ?? "", counts, new FileInfo(target).Length, settingsJson, null);
        }
        finally
        {
            try { if (File.Exists(partial)) File.Delete(partial); } catch { }
            try { Directory.Delete(work, recursive: true); } catch { }
        }
    }

    // ══ خواندن و سنجیدن ══════════════════════════════════════════════════════

    /// <summary>
    /// فایل را باز می‌کند و <b>همهٔ</b> سنجش‌ها را پیش از هر نوشتنی می‌زند.
    /// هیچ‌وقت استثنا بیرون نمی‌دهد؛ رد شدن = <see cref="FullBackupInfo.Ok"/> نادرست
    /// با دلیلی که به کاربر نشان داده می‌شود.
    /// </summary>
    /// <remarks>
    /// ⚠️ نتیجه یک فایلِ موقت (دفترِ بازشده) در خود دارد؛ پس از کار
    /// <see cref="FullBackupInfo.Dispose"/> شود.
    /// </remarks>
    public static FullBackupInfo Read(string path, string currentAppVersion)
    {
        string? work = null;
        try
        {
            if (!File.Exists(path)) return Fail("فایل پیدا نشد");

            using var zip = OpenZip(path);
            if (zip is null) return Fail("این فایل باز نشد — یا فایلِ کاملِ برنامهٔ پمپ نیست، یا ناقص کپی شده (مثلاً فلش وسطِ کپی کشیده شد)");

            var mEntry = zip.GetEntry(ManifestEntry);
            var dEntry = zip.GetEntry(DbEntry);
            if (mEntry is null || dEntry is null) return Fail("این فایل، فایلِ کاملِ برنامهٔ پمپ نیست");
            if (mEntry.Length > 4 * 1024 * 1024) return Fail("فهرستِ داخلِ فایل خراب است");

            JsonObject manifest;
            try
            {
                manifest = JsonNode.Parse(ReadText(mEntry)) as JsonObject
                           ?? throw new FormatException();
            }
            catch { return Fail("فهرستِ داخلِ فایل خوانده نشد — فایل خراب است"); }

            if ((string?)manifest["format"] != FormatName)
                return Fail("این فایل، فایلِ کاملِ برنامهٔ پمپ نیست");

            var fv = (int?)manifest["formatVersion"] ?? 0;
            if (fv <= 0) return Fail("فهرستِ داخلِ فایل خراب است");
            if (fv > FormatVersion)
                return Fail("این فایل با نسخهٔ تازه‌ترِ برنامه ساخته شده — اول برنامه را به‌روز کنید");

            var appVer = (string?)manifest["appVersion"] ?? "";
            if (IsNewer(appVer, currentAppVersion))
                return Fail($"این فایل با نسخهٔ {appVer} ساخته شده و این برنامه {currentAppVersion} است — اول برنامه را به‌روز کنید");

            var expectSha = (string?)manifest["db"]?["sha256"] ?? "";
            var expectBytes = (long?)manifest["db"]?["bytes"] ?? -1;
            if (expectSha.Length != 64 || expectBytes <= 0) return Fail("فهرستِ داخلِ فایل خراب است");
            if (dEntry.Length != expectBytes || dEntry.Length > MaxDbBytes)
                return Fail("دفترِ داخلِ فایل ناقص است — شاید کپیِ فلش نیمه‌کاره مانده");

            if (manifest["tables"] is not JsonObject tablesNode || tablesNode.Count == 0)
                return Fail("فهرستِ داخلِ فایل خراب است");
            var expected = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var (k, v) in tablesNode)
            {
                if (v is null) return Fail("فهرستِ داخلِ فایل خراب است");
                expected[k] = (long)v;
            }

            // ── دفتر را جدا باز کن — ⚠️ فقط همین یک نامِ ثابت، هرگز نامی از خودِ زیپ
            //    (پس «../../» داخلِ فایل هیچ‌جا نوشته نمی‌شود).
            work = Path.Combine(Path.GetTempPath(), "pyq-in-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            var db = Path.Combine(work, DbEntry);
            using (var src = dEntry.Open())
            using (var dst = File.Create(db))
            {
                CopyCapped(src, dst, expectBytes);
            }

            if (new FileInfo(db).Length != expectBytes)
                return Fail("دفترِ داخلِ فایل ناقص است — شاید کپیِ فلش نیمه‌کاره مانده", work);
            if (!string.Equals(Sha256(db), expectSha, StringComparison.OrdinalIgnoreCase))
                return Fail("دفترِ داخلِ فایل با اثرِ انگشتش نمی‌خواند — فایل دست خورده یا خراب است", work);

            if (!IntegrityOk(db)) return Fail("دفترِ داخلِ فایل خراب است (سنجشِ SQLite رد کرد)", work);
            if (BackupService.Inspect(db) < 0) return Fail("این فایل دفترِ برنامهٔ پمپ ندارد", work);

            var actual = CountRows(db);
            if (actual is null) return Fail("دفترِ داخلِ فایل خوانده نشد", work);
            foreach (var (table, n) in expected)
            {
                if (!actual.TryGetValue(table, out var got) || got != n)
                    return Fail($"شمارِ ردیف‌های «{table}» با فهرستِ فایل نمی‌خواند — فایل دست خورده است", work);
            }

            string? settings = null;
            var sEntry = zip.GetEntry(SettingsEntry);
            if (sEntry is not null && sEntry.Length < 16 * 1024 * 1024)
            {
                var raw = ReadText(sEntry);
                try { if (JsonNode.Parse(raw) is JsonObject) settings = raw; } catch { }
            }

            return new FullBackupInfo(true, "", appVer,
                (string?)manifest["shamsi"] ?? "", (string?)manifest["time"] ?? "",
                (string?)manifest["pumpName"] ?? "", actual, new FileInfo(path).Length, settings, db, work);
        }
        catch (Exception e)
        {
            return Fail("فایل خوانده نشد: " + e.GetType().Name, work);
        }
    }

    /// <summary>
    /// دفترِ داخلِ فایلی که <see cref="Read"/> پذیرفته، جای دفترِ فعلی می‌نشیند —
    /// با همان <see cref="BackupService.Restore"/>ِ همیشگی (عکسِ ایمنیِ حالِ فعلی
    /// پیش از جایگزینی، و اجازهٔ <c>Restore</c>).
    /// </summary>
    public static RestoreOutcome Restore(BackupService backup, FullBackupInfo info)
    {
        if (!info.Ok || info.DbPath is null || !File.Exists(info.DbPath))
            return new RestoreOutcome(false, "فایل سنجیده نشده — چیزی عوض نشد");
        return backup.Restore(info.DbPath);
    }

    /// <summary>
    /// جدول‌هایی که بازکردنِ دفتر (<c>EnsureReady</c>) خودش در آن‌ها می‌نویسد —
    /// مُهرِ اسکیما و حالِ همگام‌سازی — پس شمارشان پس از جایگزینی می‌تواند
    /// یکی دو ردیف فرق کند. ⛔ هیچ جدولِ داده‌ای این‌جا نیست.
    /// </summary>
    public static readonly IReadOnlySet<string> Housekeeping =
        new HashSet<string>(StringComparer.Ordinal) { "Settings", "SyncState", "SyncOps", "__EFMigrationsHistory" };

    /// <summary>
    /// ══ سنجشِ پس از آوردن ══ «همه‌شان آمدند؟» — شمارِ ردیفِ <b>هر</b> جدولِ
    /// دادهٔ دفترِ فعلی با همان عددِ فایل. ‎null‎ یعنی همه یکی‌اند؛ وگرنه نامِ
    /// نخستین جدولِ ناجور.
    /// </summary>
    public static string? VerifyRestored(string liveDbPath, FullBackupInfo info)
    {
        var live = CountRows(liveDbPath);
        if (live is null) return "دفترِ تازه خوانده نشد";
        foreach (var (table, n) in info.Tables)
        {
            if (Housekeeping.Contains(table)) continue;
            if (!live.TryGetValue(table, out var got) || got != n)
                return $"«{table}»: در فایل {n} ردیف، در برنامه {got} ردیف";
        }
        return null;
    }

    // ══ ابزارها ══════════════════════════════════════════════════════════════

    /// <summary>شمارِ ردیفِ هر جدولِ کاربری. ‎null‎ یعنی خوانده نشد.</summary>
    public static Dictionary<string, long>? CountRows(string dbPath)
    {
        try
        {
            using var con = ReadOnly(dbPath);
            var names = new List<string>();
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                using var r = cmd.ExecuteReader();
                while (r.Read()) names.Add(r.GetString(0));
            }
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var n in names)
            {
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM \"" + n.Replace("\"", "\"\"") + "\";";
                result[n] = Convert.ToInt64(cmd.ExecuteScalar());
            }
            return result;
        }
        catch { return null; }
    }

    private static bool IntegrityOk(string dbPath)
    {
        try
        {
            using var con = ReadOnly(dbPath);
            using var cmd = con.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            return string.Equals(cmd.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static SqliteConnection ReadOnly(string dbPath)
    {
        //  ⚠️ Pooling=False: دفترِ موقت باید همان لحظه آزاد شود تا پاک شود
        //  (روی ویندوز فایلِ باز پاک نمی‌شود).
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false,
        }.ToString();
        var con = new SqliteConnection(cs);
        con.Open();
        return con;
    }

    public static string Sha256(string file)
    {
        using var s = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }

    /// <summary>نسخهٔ <paramref name="a"/> از <paramref name="b"/> تازه‌تر است؟ ناخوانا ⇒ نه.</summary>
    public static bool IsNewer(string a, string b) =>
        Version.TryParse(a, out var va) && Version.TryParse(b, out var vb) && va > vb;

    private static ZipArchive? OpenZip(string path)
    {
        try { return ZipFile.OpenRead(path); } catch { return null; }
    }

    private static void CopyCapped(Stream src, Stream dst, long cap)
    {
        var buf = new byte[81920];
        long total = 0;
        int n;
        while ((n = src.Read(buf, 0, buf.Length)) > 0)
        {
            total += n;
            if (total > cap) throw new InvalidDataException("بیش از اندازهٔ اعلام‌شده");
            dst.Write(buf, 0, n);
        }
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(text);
    }

    private static string ReadText(ZipArchiveEntry e)
    {
        using var r = new StreamReader(e.Open(), Encoding.UTF8);
        return r.ReadToEnd();
    }

    private static JsonObject ToJson(Dictionary<string, long> counts)
    {
        var o = new JsonObject();
        foreach (var (k, v) in counts) o[k] = v;
        return o;
    }

    private static FullBackupInfo Fail(string why, string? work = null)
    {
        if (work is not null) Cleanup(work);
        return new FullBackupInfo(false, why, "", "", "", "", new Dictionary<string, long>(), 0, null, null);
    }

    internal static void Cleanup(string? work)
    {
        if (work is null) return;
        try { SqliteConnection.ClearAllPools(); } catch { }
        try { Directory.Delete(work, recursive: true); } catch { }
    }
}

/// <summary>
/// نتیجهٔ ساختن یا خواندنِ «فایلِ کامل». <see cref="Ok"/> نادرست ⇒ <see cref="Why"/>
/// همان جمله‌ای است که به کاربر نشان داده می‌شود.
/// </summary>
public sealed record FullBackupInfo(
    bool Ok, string Why, string AppVersion, string Shamsi, string Time, string PumpName,
    IReadOnlyDictionary<string, long> Tables, long FileBytes, string? SettingsJson, string? DbPath,
    string? WorkDir = null) : IDisposable
{
    /// <summary>شمارِ همهٔ ردیف‌های دفتر.</summary>
    public long TotalRows => Tables.Values.Sum();

    /// <summary>چند ردیفِ یک جدول — نبود ⇒ صفر.</summary>
    public long Rows(string table) => Tables.TryGetValue(table, out var n) ? n : 0;

    /// <summary>دفترِ موقتِ بازشده پاک می‌شود.</summary>
    public void Dispose() => FullBackup.Cleanup(WorkDir);
}
