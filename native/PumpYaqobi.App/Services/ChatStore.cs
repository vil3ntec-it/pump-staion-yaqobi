using Microsoft.Data.Sqlite;

namespace PumpYaqobi.App.Services;

/// <summary>یک پیامِ ذخیره‌شده روی همین کامپیوتر.</summary>
/// <param name="Key">یکتا در کلِ فایل — ‎cloud:&lt;id&gt;‎ · ‎grp:&lt;seq&gt;‎ · ‎sup:&lt;id&gt;‎.</param>
/// <param name="Thread">گفت‌وگو — ‎acct:d12‎ · ‎group‎ · ‎support‎.</param>
/// <param name="Anchor">
/// لحظه‌ای که شمارشِ ۱۵ روز از آن است. همان ‎At‎، مگر پیام از سطل برگشته
/// باشد — آن‌وقت لحظهٔ برگشت، تا بلافاصله دوباره به سطل نرود.
/// </param>
/// <param name="TrashedAt">۰ یعنی در سطل نیست.</param>
public sealed record ChatRow(
    string Key, string Thread, long Seq, string Sender, string Text, string Kind,
    string? MediaId, bool Mine, long At, bool Deleted = false,
    long Anchor = 0, long TrashedAt = 0, bool Pending = false);

/// <summary>سرنوشتِ یک پیام در هر جاروی ماندگاری.</summary>
public enum ChatFate { Keep, Trash, Purge }

/// <summary>
/// ══ پیام‌ها روی خودِ این کامپیوتر — ۱۵ روز، سطل، ۱۵ روز، حذف ═══════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «این‌ها روی سرور فضایی رو نگیره، این‌ها
/// توی برنامه باشن، و همه پیام‌ها بعدِ ۱۵ روز برن سطل آشغالی و ۱۵ روز هم تو
/// آشغالی بمونه و بعدش حذفِ کامل.»
///
/// <list type="bullet">
/// <item>⛔ <b>فایلِ جدا، نه دفترِ حساب‌ها</b> (‎chat.db‎ کنارِ ‎pump.db‎ی همان
/// حساب): چت دادهٔ حسابی نیست، نباید در همگام‌سازی و پشتیبانِ حساب‌ها قاطی شود،
/// و هیچ دستورش در ‎DbWatch‎ی دفتر شمرده نمی‌شود — پس قاعدهٔ «بخشِ پنهان صفر
/// دستورِ دیتابیس» سرِ جایش است.</item>
/// <item>⛔ <b>سرور فقط رله است</b>: پیام و رسانه همان لحظهٔ رسیدن این‌جا
/// می‌نشینند (رسانه در ‎chat-media/‎)، پس وقتی سرور پس از ۱۵ روز پاکشان کرد،
/// نسخهٔ این کامپیوتر سرِ جایش است.</item>
/// <item>⛔ <see cref="Decide"/> تنها جای تصمیمِ ماندگاری است و خالص است.</item>
/// </list>
/// </summary>
public sealed class ChatStore
{
    /// <summary>روزهایی که پیام در گفت‌وگو می‌ماند.</summary>
    public const int ActiveDays = 15;

    /// <summary>روزهایی که پیام در سطل می‌ماند، پیش از حذفِ کامل.</summary>
    public const int TrashDays = 15;

    private const long DayMs = 86_400_000L;

    private readonly string _cs;

    public ChatStore(string dir)
    {
        Dir = dir;
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(MediaDir);
        //  ⚠️ ‎Pooling=False‎: فایل پس از هر کار رها می‌شود، پس جابه‌جاییِ حساب
        //  (‎UseLedgerOf‎) و پشتیبان‌گیری از پوشه روی ویندوز قفل نمی‌خورند.
        _cs = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dir, "chat.db"),
            Pooling = false,
        }.ToString();
        Exec("""
            CREATE TABLE IF NOT EXISTS Msgs(
              Key TEXT PRIMARY KEY, Thread TEXT NOT NULL, Seq INTEGER NOT NULL DEFAULT 0,
              Sender TEXT NOT NULL DEFAULT '', Text TEXT NOT NULL DEFAULT '', Kind TEXT NOT NULL DEFAULT 'text',
              MediaId TEXT NULL, Mine INTEGER NOT NULL DEFAULT 0, At INTEGER NOT NULL DEFAULT 0,
              Deleted INTEGER NOT NULL DEFAULT 0, Anchor INTEGER NOT NULL DEFAULT 0,
              TrashedAt INTEGER NOT NULL DEFAULT 0, Pending INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS IX_Msgs_Thread ON Msgs(Thread, At);
            CREATE TABLE IF NOT EXISTS Meta(K TEXT PRIMARY KEY, V TEXT NOT NULL);
            """);
    }

    public string Dir { get; }
    public string MediaDir => Path.Combine(Dir, "chat-media");

    // ══ قاعدهٔ ماندگاری ═════════════════════════════════════════════════════

    /// <summary>
    /// ⛔ تنها جای تصمیم. پیامِ بیرونِ سطل پس از ‎ActiveDays‎ از ‎Anchor‎ به سطل
    /// می‌رود؛ پیامِ درونِ سطل پس از ‎TrashDays‎ از ‎TrashedAt‎ کاملاً پاک می‌شود.
    /// </summary>
    public static ChatFate Decide(long anchor, long trashedAt, long nowMs)
    {
        if (trashedAt > 0)
            return nowMs - trashedAt >= TrashDays * DayMs ? ChatFate.Purge : ChatFate.Keep;
        return nowMs - anchor >= ActiveDays * DayMs ? ChatFate.Trash : ChatFate.Keep;
    }

    /// <summary>روزِ ماندهٔ یک پیام پیش از رفتن به سطل (یا از سطل به حذف).</summary>
    public static int DaysLeft(long anchor, long trashedAt, long nowMs)
    {
        var end = trashedAt > 0 ? trashedAt + TrashDays * DayMs : anchor + ActiveDays * DayMs;
        return (int)Math.Max(0, Math.Ceiling((end - nowMs) / (double)DayMs));
    }

    /// <summary>
    /// یک جارو: پیرها به سطل، پیرهای سطل به حذف — و رسانه‌ای که دیگر هیچ
    /// پیامی به آن اشاره نمی‌کند از دیسک پاک می‌شود.
    /// </summary>
    public (int Trashed, int Purged) Sweep(long nowMs)
    {
        var trashCut = nowMs - ActiveDays * DayMs;
        var purgeCut = nowMs - TrashDays * DayMs;
        var purgedMedia = new List<string>();
        int trashed, purged;
        using (var c = Open())
        using (var tx = c.BeginTransaction())
        {
            using (var q = c.CreateCommand())
            {
                q.Transaction = tx;
                q.CommandText = "SELECT MediaId FROM Msgs WHERE TrashedAt > 0 AND TrashedAt <= $p AND MediaId IS NOT NULL";
                q.Parameters.AddWithValue("$p", purgeCut);
                using var r = q.ExecuteReader();
                while (r.Read()) purgedMedia.Add(r.GetString(0));
            }
            purged = Run(c, tx, "DELETE FROM Msgs WHERE TrashedAt > 0 AND TrashedAt <= $p", ("$p", purgeCut));
            trashed = Run(c, tx, "UPDATE Msgs SET TrashedAt = $n WHERE TrashedAt = 0 AND Anchor <= $t",
                ("$n", nowMs), ("$t", trashCut));
            tx.Commit();
        }
        foreach (var id in purgedMedia.Distinct())
            if (!MediaReferenced(id)) DropMedia(id);
        return (trashed, purged);
    }

    // ══ نوشتن ════════════════════════════════════════════════════════════════

    /// <summary>
    /// نشاندن یا به‌روز کردنِ یک پیام. ⚠️ پیامی که در سطل است با رسیدنِ دوباره
    /// (مثلاً صندوقی که از اول پرسیده شد) از سطل بیرون نمی‌آید و ‎Anchor‎ش عوض
    /// نمی‌شود — فقط متن و حالِ «پاک شد» تازه می‌شود.
    /// </summary>
    public void Upsert(ChatRow m)
    {
        var anchor = m.Anchor > 0 ? m.Anchor : m.At;
        using var c = Open();
        Run(c, null, """
            INSERT INTO Msgs(Key,Thread,Seq,Sender,Text,Kind,MediaId,Mine,At,Deleted,Anchor,TrashedAt,Pending)
            VALUES($k,$th,$s,$se,$t,$ki,$m,$mi,$a,$d,$an,0,$p)
            ON CONFLICT(Key) DO UPDATE SET
              Seq=excluded.Seq, Sender=excluded.Sender, Text=excluded.Text, Kind=excluded.Kind,
              MediaId=excluded.MediaId, Deleted=excluded.Deleted, Pending=excluded.Pending,
              At=CASE WHEN excluded.At > 0 THEN excluded.At ELSE Msgs.At END
            """,
            ("$k", m.Key), ("$th", m.Thread), ("$s", m.Seq), ("$se", m.Sender), ("$t", m.Text),
            ("$ki", m.Kind), ("$m", (object?)m.MediaId ?? DBNull.Value), ("$mi", m.Mine ? 1 : 0),
            ("$a", m.At), ("$d", m.Deleted ? 1 : 0), ("$an", anchor), ("$p", m.Pending ? 1 : 0));
    }

    /// <summary>کلیدِ یک پیامِ درراه عوض می‌شود — وقتی سرور شمارهٔ واقعی‌اش را داد.</summary>
    public void Rekey(string oldKey, ChatRow real)
    {
        using var c = Open();
        Run(c, null, "DELETE FROM Msgs WHERE Key = $k", ("$k", oldKey));
        c.Dispose();
        Upsert(real);
    }

    public void Delete(string key)
    {
        using var c = Open();
        Run(c, null, "DELETE FROM Msgs WHERE Key = $k", ("$k", key));
    }

    /// <summary>از سطل برمی‌گردد و از همین لحظه ۱۵ روزِ دیگر می‌ماند.</summary>
    public bool Restore(string key, long nowMs)
    {
        using var c = Open();
        return Run(c, null, "UPDATE Msgs SET TrashedAt = 0, Anchor = $n WHERE Key = $k AND TrashedAt > 0",
            ("$n", nowMs), ("$k", key)) > 0;
    }

    // ══ خواندن ═══════════════════════════════════════════════════════════════

    /// <summary>پیام‌های یک گفت‌وگو — بیرونِ سطل یا فقط درونِ سطل.</summary>
    public List<ChatRow> Rows(string thread, bool trash = false, int limit = 500)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT * FROM (SELECT * FROM Msgs WHERE Thread = $th AND "
                      + (trash ? "TrashedAt > 0" : "TrashedAt = 0")
                      + " ORDER BY At DESC, Seq DESC LIMIT $l) ORDER BY At ASC, Seq ASC";
        q.Parameters.AddWithValue("$th", thread);
        q.Parameters.AddWithValue("$l", limit);
        return Read(q);
    }

    /// <summary>همهٔ گفت‌وگوهایی که پیامِ بیرونِ سطل دارند.</summary>
    public List<string> Threads()
    {
        var list = new List<string>();
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT Thread FROM Msgs WHERE TrashedAt = 0 GROUP BY Thread ORDER BY MAX(At) DESC";
        using var r = q.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    public int TrashCount(string thread)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT COUNT(*) FROM Msgs WHERE Thread = $th AND TrashedAt > 0";
        q.Parameters.AddWithValue("$th", thread);
        return Convert.ToInt32(q.ExecuteScalar());
    }

    public string Meta(string key, string def = "")
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT V FROM Meta WHERE K = $k";
        q.Parameters.AddWithValue("$k", key);
        return q.ExecuteScalar() as string ?? def;
    }

    public void SetMeta(string key, string value)
    {
        using var c = Open();
        Run(c, null, "INSERT INTO Meta(K,V) VALUES($k,$v) ON CONFLICT(K) DO UPDATE SET V = excluded.V",
            ("$k", key), ("$v", value));
    }

    public long MetaLong(string key) => long.TryParse(Meta(key), out var n) ? n : 0;

    // ══ رسانه ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// ⛔ نامِ فایل فقط از شناسهٔ پاک‌شده ساخته می‌شود (‎[A-Za-z0-9_-]‎) — شناسه از
    /// سرور و از پیامِ مشتری می‌آید و نباید بتواند بیرونِ این پوشه بنویسد.
    /// </summary>
    public static bool SafeId(string? id) =>
        id is { Length: > 0 and <= 80 }
        && System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,80}$");

    public string? SaveMedia(string mediaId, string mime, byte[] bytes)
    {
        if (!SafeId(mediaId) || bytes.Length == 0) return null;
        var path = Path.Combine(MediaDir, mediaId + ExtOf(mime));
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>فایلِ محلیِ یک رسانه، اگر هست.</summary>
    public string? MediaPath(string? mediaId)
    {
        if (!SafeId(mediaId) || !Directory.Exists(MediaDir)) return null;
        return Directory.EnumerateFiles(MediaDir, mediaId + ".*").FirstOrDefault();
    }

    private bool MediaReferenced(string mediaId)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT COUNT(*) FROM Msgs WHERE MediaId = $m";
        q.Parameters.AddWithValue("$m", mediaId);
        return Convert.ToInt32(q.ExecuteScalar()) > 0;
    }

    private void DropMedia(string mediaId)
    {
        if (!SafeId(mediaId)) return;
        foreach (var f in Directory.EnumerateFiles(MediaDir, mediaId + ".*"))
            try { File.Delete(f); } catch { /* فایلِ باز — جاروی بعد */ }
    }

    public static string ExtOf(string mime) => (mime ?? "").ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", "image/gif" => ".gif",
        "video/mp4" => ".mp4", "video/webm" => ".webm", "video/quicktime" => ".mov",
        "audio/mp4" => ".m4a", "audio/mpeg" => ".mp3", "audio/wav" or "audio/x-wav" => ".wav",
        "audio/ogg" => ".ogg", "audio/webm" => ".weba", _ => ".bin",
    };

    // ══ درون ═══════════════════════════════════════════════════════════════════

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        return c;
    }

    private void Exec(string sql)
    {
        using var c = Open();
        using var q = c.CreateCommand();
        q.CommandText = sql;
        q.ExecuteNonQuery();
    }

    private static int Run(SqliteConnection c, SqliteTransaction? tx, string sql, params (string, object)[] args)
    {
        using var q = c.CreateCommand();
        q.Transaction = tx;
        q.CommandText = sql;
        foreach (var (k, v) in args) q.Parameters.AddWithValue(k, v);
        return q.ExecuteNonQuery();
    }

    private static List<ChatRow> Read(SqliteCommand q)
    {
        var list = new List<ChatRow>();
        using var r = q.ExecuteReader();
        while (r.Read())
            list.Add(new ChatRow(
                r.GetString(r.GetOrdinal("Key")), r.GetString(r.GetOrdinal("Thread")),
                r.GetInt64(r.GetOrdinal("Seq")), r.GetString(r.GetOrdinal("Sender")),
                r.GetString(r.GetOrdinal("Text")), r.GetString(r.GetOrdinal("Kind")),
                r.IsDBNull(r.GetOrdinal("MediaId")) ? null : r.GetString(r.GetOrdinal("MediaId")),
                r.GetInt64(r.GetOrdinal("Mine")) == 1, r.GetInt64(r.GetOrdinal("At")),
                r.GetInt64(r.GetOrdinal("Deleted")) == 1, r.GetInt64(r.GetOrdinal("Anchor")),
                r.GetInt64(r.GetOrdinal("TrashedAt")), r.GetInt64(r.GetOrdinal("Pending")) == 1));
        return list;
    }
}
