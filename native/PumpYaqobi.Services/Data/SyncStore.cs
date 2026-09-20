using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>یک opی که از سرور رسیده — همان شکلِ <c>GET /api/sync/v1/pull</c>.</summary>
public sealed record IncomingOp(string OpId, string Table, string RowUid, string Type,
                                JsonElement Fields, long ServerSeq);

/// <summary>نتیجهٔ اعمالِ یک دسته opِ رسیده.</summary>
public sealed record ApplyReport(int Applied, int Skipped, int Failed, string LastWhy);

/// <summary>
/// ══ دفترِ همگام‌سازی روی همین کامپیوتر ══════════════════════════════════
///
/// دو کار، و هیچ کارِ دیگری:
///   • opهای <b>ما</b> را برمی‌دارد، مهرِ «رفت» می‌زند و کهنه‌ها را هرس می‌کند؛
///   • opهای <b>دستگاه‌های دیگر</b> را روی دفتر می‌نشاند.
///
/// ⛔ <b>هیچ تصمیمِ شبکه‌ای این‌جا نیست.</b> این کلاس نمی‌داند اینترنت هست
/// یا نه و هیچ‌وقت HTTP نمی‌زند — <see cref="PumpYaqobi.App"/> این کار را
/// می‌کند. پس آزمونش بی شبکه و بی پنجره می‌دود.
///
/// ⛔ <b>اعمالِ opهای رسیده با SQLِ خام است، نه با EF.</b> عمدی: اگر از راهِ
/// <c>SaveChanges</c> می‌رفت، خودِ <see cref="OpLog"/> برایشان op می‌ساخت و
/// هر تغییرِ دستگاهِ دیگر دوباره به سرور برمی‌گشت — پژواکِ بی‌پایان.
/// ⚠️ در عوض <see cref="PumpDbContext.Bump"/> دستی زده می‌شود، همان قاعدهٔ
/// همیشگیِ نوشتنِ خام.
/// </summary>
public sealed class SyncStore
{
    private readonly PumpDbFactory _dbf;

    public SyncStore(PumpDbFactory dbf) => _dbf = dbf;

    /// <summary>بیشینهٔ opی که در یک دسته می‌رود — همان سقفِ سرور.</summary>
    public const int MaxBatch = 200;

    /// <summary>چند opِ رفته نگه داشته شود، برای دیدن در «جزئیات».</summary>
    public const int KeepSynced = 2_000;

    // ── حال ────────────────────────────────────────────────────────────

    /// <summary>ردیفِ حال — اگر نبود، ساخته می‌شود.</summary>
    public SyncStateRow State()
    {
        using var db = _dbf.Create();
        return Load(db);
    }

    private static SyncStateRow Load(PumpDbContext db)
    {
        var row = db.SyncState.FirstOrDefault(x => x.Id == 1);
        if (row is not null) return row;
        row = new SyncStateRow { Id = 1 };
        db.SyncState.Add(row);
        Quiet(db);
        return row;
    }

    /// <summary>حال را عوض می‌کند — تنها راهِ نوشتنش.</summary>
    public void Update(Action<SyncStateRow> edit)
    {
        using var db = _dbf.Create();
        var row = Load(db);
        edit(row);
        Quiet(db);
    }

    /// <summary>
    /// ذخیره بی ساختنِ op.
    ///
    /// ⚠️ حالِ همگام‌سازی دادهٔ کاربر نیست؛ اگر op می‌ساخت، هر push خودش
    /// یک opِ تازه می‌زد و صف هیچ‌وقت خالی نمی‌شد.
    /// </summary>
    private static void Quiet(PumpDbContext db)
    {
        var was = OpLog.Enabled;
        OpLog.Enabled = false;
        try { db.SaveChanges(); } finally { OpLog.Enabled = was; }
    }

    // ── صفِ فرستادنی ───────────────────────────────────────────────────

    /// <summary>چند op هنوز نرفته.</summary>
    public int Pending()
    {
        using var db = _dbf.Create();
        return db.SyncOps.Count(x => !x.Synced);
    }

    /// <summary>
    /// دستهٔ بعدی، به ترتیبِ ساخت.
    ///
    /// ⚠️ ترتیب مهم است و <c>Id</c> (نه <c>OpId</c>) ملاک است: هر دو یک
    /// ترتیب می‌دهند، ولی عدد ایندکسِ ارزان‌تری دارد.
    /// </summary>
    public IReadOnlyList<SyncOp> Take(int max = MaxBatch)
    {
        using var db = _dbf.Create();
        return db.SyncOps.AsNoTracking()
                 .Where(x => !x.Synced)
                 .OrderBy(x => x.Id)
                 .Take(Math.Clamp(max, 1, MaxBatch))
                 .ToList();
    }

    /// <summary>
    /// جوابِ سرور برای یک دسته.
    ///
    /// ⛔ فقط <c>applied</c> و <c>duplicate</c> «رفته» می‌شوند — همان قاعدهٔ
    /// بندِ ۲۰٫۲. <c>rejected</c> هم دیگر فرستاده نمی‌شود (سرور همان را
    /// دوباره هم رد می‌کند) ولی دلیلش روی ردیف می‌ماند تا در «جزئیات»
    /// دیده شود؛ بی آن، صف تا ابد یک opِ خراب را پس و پیش می‌برد.
    /// </summary>
    public void MarkResults(IReadOnlyDictionary<string, string> results)
    {
        if (results.Count == 0) return;
        using var db = _dbf.Create();
        var ids = results.Keys.ToList();
        var rows = db.SyncOps.Where(x => ids.Contains(x.OpId)).ToList();
        foreach (var row in rows)
        {
            var status = results[row.OpId];
            if (status is "applied" or "duplicate") { row.Synced = true; row.Rejected = ""; }
            else { row.Synced = true; row.Rejected = status; }
        }
        Quiet(db);
    }

    /// <summary>تلاشِ ناموفق — فقط شمرده می‌شود، هیچ opی دور ریخته نمی‌شود.</summary>
    public void CountAttempt(IEnumerable<string> opIds)
    {
        var ids = opIds.ToList();
        if (ids.Count == 0) return;
        using var db = _dbf.Create();
        foreach (var row in db.SyncOps.Where(x => ids.Contains(x.OpId)).ToList()) row.Attempts++;
        Quiet(db);
    }

    /// <summary>
    /// هرسِ دفتر — فقط opهای <b>رفته</b>، و همیشه تازه‌ترین‌ها می‌مانند.
    /// ⛔ opی که نرفته هیچ‌وقت پاک نمی‌شود، هر چقدر هم کهنه باشد.
    /// </summary>
    public int Prune(int keep = KeepSynced)
    {
        using var db = _dbf.Create();
        var cut = db.SyncOps.Where(x => x.Synced).OrderByDescending(x => x.Id)
                    .Skip(Math.Max(0, keep)).Select(x => x.Id).FirstOrDefault();
        if (cut == 0) return 0;
        var n = db.SyncOps.Where(x => x.Synced && x.Id <= cut).ExecuteDelete();
        if (n > 0) PumpDbContext.Bump();
        return n;
    }

    // ── بارِ اول: ردیف‌هایی که پیش از Sync وجود داشتند ─────────────────

    /// <summary>نتیجهٔ یک دورِ «بارِ اول».</summary>
    public sealed record SeedReport(bool Done, int Made, string Table);

    /// <summary>
    /// ══ ردیف‌های از پیش موجود را یک بار به دفتر می‌برد ═══════════════════
    ///
    /// دیتابیسِ یک پمپِ واقعی پنج سال داده دارد و هیچ‌کدام op ندارند، چون
    /// پیش از این نسخه ساخته شده‌اند. بی این پاس، «همگام‌سازی» یعنی فقط
    /// چیزهایی که از فردا عوض شوند — و گوشیِ تازه دفترِ خالی می‌دید.
    ///
    /// ⚠️ <b>تکه‌تکه</b> و از بیرون صدا زده می‌شود، نه یک‌جا: صدهزار ردیف
    /// در یک تراکنش یعنی چند ثانیه قفلِ دیتابیس، درست وسطِ کارِ کاربر.
    /// هر بار یک تکه، و <c>SeedCursor</c> می‌گوید کجا بودیم.
    ///
    /// ⚠️ هر ردیف یک <c>insert</c> می‌گیرد — همان چیزی که سرور با
    /// <c>op_id</c>ِ یکتا یک بار می‌نشاند؛ اجرای دوباره چیزی را دو بار
    /// نمی‌کند.
    /// </summary>
    public SeedReport SeedStep(int batch = 300)
    {
        using var db = _dbf.Create();
        var state = Load(db);
        if (state.SeededAt > 0) return new SeedReport(true, 0, "");

        var tables = DataTables(db);
        var done = new HashSet<string>(
            state.SeedCursor.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

        foreach (var t in tables)
        {
            if (done.Contains(t.Entity)) continue;

            var made = SeedTable(db, t, batch);
            if (made > 0)
            {
                Quiet(db);
                return new SeedReport(false, made, t.Entity);
            }

            //  این جدول تمام شد
            done.Add(t.Entity);
            state.SeedCursor = string.Join(',', done);
            Quiet(db);
            return new SeedReport(false, 0, t.Entity);
        }

        state.SeededAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        state.SeedCursor = "";
        Quiet(db);
        return new SeedReport(true, 0, "");
    }

    private static int SeedTable(PumpDbContext db, TableInfo t, int batch)
    {
        //  ردیف‌هایی که هنوز هیچ opی ندارند
        var rows = ReadRows(db,
            $"SELECT * FROM \"{t.Table}\" WHERE \"SyncUid\" IS NOT NULL AND \"SyncUid\" <> '' " +
            $"AND \"SyncUid\" NOT IN (SELECT \"RowUid\" FROM \"SyncOps\" WHERE \"TableName\" = $t) " +
            $"ORDER BY \"Id\" LIMIT {Math.Clamp(batch, 1, 2000)};",
            ("$t", t.Entity));

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var made = 0;
        foreach (var row in rows)
        {
            if (!row.TryGetValue("SyncUid", out var uidRaw) || uidRaw is not string uid || uid.Length == 0) continue;

            var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var col in t.Columns)
            {
                if (col.Name is "Id" or "SyncUid") continue;
                if (!row.TryGetValue(col.Column, out var v)) continue;
                fields[col.Name] = OpLog.Plain(FromDb(v, col.Clr));
            }

            var json = JsonSerializer.Serialize(fields);
            db.SyncOps.Add(new SyncOp
            {
                OpId = Ulid.New(now),
                TableName = t.Entity,
                RowUid = uid,
                OpType = "insert",
                FieldsJson = json,
                Hash = OpLog.HashOf(t.Entity, uid, "insert", json),
                ClientTs = now,
                SchemaVersion = OpLog.SchemaVersion,
            });
            made++;
        }
        return made;
    }

    // ── opهای رسیده ────────────────────────────────────────────────────

    /// <summary>
    /// opهای دستگاه‌های دیگر را می‌نشاند — <b>به ترتیب</b>، و هر کدام جدا.
    ///
    /// ⛔ یک opِ خراب کلِ دسته را نمی‌خواباند: هر کدام تنها است و شکستش
    /// شمرده می‌شود. وگرنه یک ردیفِ ناجور، همگام‌سازی را برای همیشه
    /// می‌ایستاند.
    /// </summary>
    public ApplyReport ApplyIncoming(IReadOnlyList<IncomingOp> ops)
    {
        if (ops.Count == 0) return new ApplyReport(0, 0, 0, "");

        using var db = _dbf.Create();
        var map = DataTables(db).ToDictionary(x => Norm(x.Entity), x => x, StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        int applied = 0, skipped = 0, failed = 0;
        var why = "";

        foreach (var op in ops)
        {
            if (!map.TryGetValue(Norm(op.Table), out var t) || op.RowUid.Length == 0) { skipped++; continue; }
            try
            {
                if (ApplyOne(db, t, op, now)) applied++; else skipped++;
            }
            catch (Exception ex)
            {
                failed++;
                //  ⚠️ پیامِ خام به کاربر نمی‌رسد؛ فقط نوعش، برای «جزئیات»
                why = $"{t.Entity}: {ex.GetType().Name}";
            }
        }

        if (applied > 0) PumpDbContext.Bump();
        return new ApplyReport(applied, skipped, failed, why);
    }

    private static bool ApplyOne(PumpDbContext db, TableInfo t, IncomingOp op, DateTime now)
    {
        var id = ScalarLong(db, $"SELECT \"Id\" FROM \"{t.Table}\" WHERE \"SyncUid\" = $u LIMIT 1;", ("$u", op.RowUid));

        if (op.Type == "delete")
        {
            if (id is null) return false;
            Exec(db, $"UPDATE \"{t.Table}\" SET \"DeletedAt\" = {{0}}, \"UpdatedAt\" = {{1}} WHERE \"Id\" = {{2}};",
                 DbTime(now), DbTime(now), id.Value);
            return true;
        }

        //  فیلدها ⇒ ستون‌های واقعیِ همین جدول. هر نامِ ناشناس نادیده می‌رود.
        var sets = new List<string>();
        var args = new List<object?>();
        if (op.Fields.ValueKind == JsonValueKind.Object)
        {
            foreach (var f in op.Fields.EnumerateObject())
            {
                var col = t.Columns.FirstOrDefault(c => string.Equals(c.Name, f.Name, StringComparison.Ordinal));
                if (col is null || col.Name is "Id" or "SyncUid") continue;

                var value = Value(db, t, col, id, f.Value);
                sets.Add($"\"{col.Column}\" = {{{args.Count}}}");
                args.Add(value);
            }
        }

        if (id is null)
        {
            //  ردیفِ تازه از دستگاهِ دیگر
            var cols = new List<string> { "\"SyncUid\"", "\"CreatedAt\"", "\"UpdatedAt\"" };
            var marks = new List<string>();
            var ins = new List<object?> { op.RowUid, DbTime(now), DbTime(now) };
            for (var i = 0; i < 3; i++) marks.Add("{" + i + "}");

            if (op.Fields.ValueKind == JsonValueKind.Object)
            {
                foreach (var f in op.Fields.EnumerateObject())
                {
                    var col = t.Columns.FirstOrDefault(c => string.Equals(c.Name, f.Name, StringComparison.Ordinal));
                    if (col is null || col.Name is "Id" or "SyncUid" or "CreatedAt" or "UpdatedAt") continue;
                    marks.Add("{" + ins.Count + "}");
                    cols.Add($"\"{col.Column}\"");
                    ins.Add(Value(db, t, col, null, f.Value));
                }
            }

            //  ⛔ **ستونِ اجباری که در op نیامده، باید مقدارِ خالی بگیرد.**
            //  جدول‌های EF برای هر ویژگیِ غیرقابل‌تهی `NOT NULL` می‌سازند و
            //  هیچ پیش‌فرضی ندارند؛ بی این، هر ردیفِ تازه‌ای که از دستگاهِ
            //  دیگر می‌آمد با «NOT NULL constraint failed» رد می‌شد — یعنی
            //  ردیفی که روی یک گوشی ساخته شده هیچ‌وقت این‌جا نمی‌نشست.
            var have = new HashSet<string>(cols.Select(c => c.Trim('"')), StringComparer.Ordinal);
            foreach (var col in t.Columns)
            {
                if (col.Nullable || col.Name == "Id" || have.Contains(col.Column)) continue;
                marks.Add("{" + ins.Count + "}");
                cols.Add($"\"{col.Column}\"");
                ins.Add(Blank(col.Clr, now));
            }

            Exec(db, $"INSERT INTO \"{t.Table}\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", marks)});",
                 ins.ToArray());
            return true;
        }

        //  ویرایشی که هیچ ستونِ شناخته‌شده‌ای ندارد، چیزی برای نوشتن ندارد
        if (sets.Count == 0) return false;

        //  ⚠️ **ویرایشِ پس از حذف، ردیف را زنده می‌کند** — همان قاعدهٔ بندِ
        //  ۲۰٫۴ی سرور. بی این، دو دستگاه می‌توانستند سرِ یک ردیف به دو حالِ
        //  جدا برسند.
        sets.Add("\"DeletedAt\" = NULL");
        sets.Add($"\"UpdatedAt\" = {{{args.Count}}}");
        args.Add(DbTime(now));

        Exec(db, $"UPDATE \"{t.Table}\" SET {string.Join(", ", sets)} WHERE \"Id\" = {{{args.Count}}};",
             args.Append((object?)id.Value).ToArray());
        return true;
    }

    /// <summary>
    /// مقدارِ یک فیلدِ رسیده، آمادهٔ نشستن در ستون.
    ///
    /// ⚠️ <c>{"$inc": n}</c> دلتا است (بندِ ۲۰٫۴): «مقدارِ تازه» نیست، بلکه
    /// «این‌قدر کم/زیاد شد». پس مقدارِ فعلی خوانده و جمع می‌شود.
    /// ⛔ جمعش در C# است، نه <c>CAST(... AS REAL)</c>ی SQLite — مبالغ متن
    /// ذخیره می‌شوند و شناور کردنشان برای حساب‌داری خطرناک است.
    /// </summary>
    private static object? Value(PumpDbContext db, TableInfo t, ColumnInfo col, long? id, JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("$inc", out var inc)
            && inc.ValueKind == JsonValueKind.Number)
        {
            var delta = inc.GetDecimal();
            var current = 0m;
            if (id is not null)
            {
                var raw = Scalar(db, $"SELECT \"{col.Column}\" FROM \"{t.Table}\" WHERE \"Id\" = {{0}};", id.Value);
                if (raw is string rs) decimal.TryParse(rs, NumberStyles.Any, CultureInfo.InvariantCulture, out current);
                else if (raw is long rl) current = rl;
                else if (raw is double rd) current = (decimal)rd;
            }
            var sum = current + delta;
            return col.Clr == typeof(decimal) || col.Clr == typeof(decimal?)
                ? sum.ToString(CultureInfo.InvariantCulture)
                : (object)(double)sum;
        }

        return Raw(v, col.Clr);
    }

    /// <summary>JSON ⇒ چیزی که SQLite می‌پذیرد، با نگاه به نوعِ خودِ ستون.</summary>
    private static object? Raw(JsonElement v, Type clr)
    {
        var bare = Nullable.GetUnderlyingType(clr) ?? clr;
        switch (v.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined:
                return null;
            case JsonValueKind.True: return 1L;
            case JsonValueKind.False: return 0L;
            case JsonValueKind.Number:
                if (bare == typeof(decimal)) return v.GetDecimal().ToString(CultureInfo.InvariantCulture);
                if (bare == typeof(double) || bare == typeof(float)) return v.GetDouble();
                return v.TryGetInt64(out var n) ? n : v.GetDouble();
            case JsonValueKind.String:
                var s = v.GetString() ?? "";
                //  ⚠️ تاریخ به شکلی می‌رود که خودِ ارائه‌دهندهٔ SQLite
                //  می‌نویسد؛ وگرنه EF سرِ خواندن همان ردیف می‌شکست.
                if (bare == typeof(DateTime)
                    && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                    return DbTime(dt);
                return s;
            default:
                return v.GetRawText();
        }
    }

    /// <summary>
    /// مقدارِ «خالی» برای یک ستونِ اجباری که در op نیامده.
    ///
    /// ⚠️ عمداً خنثی است، نه حدسی: صفر، رشتهٔ خالی، و زمانِ همین حالا. اگر
    /// همان فیلد بعداً از دستگاهِ سازنده‌اش برسد، روی همین می‌نشیند.
    /// </summary>
    private static object? Blank(Type clr, DateTime now)
    {
        var bare = Nullable.GetUnderlyingType(clr) ?? clr;
        if (bare == typeof(string)) return "";
        if (bare == typeof(decimal)) return "0";
        if (bare == typeof(DateTime)) return DbTime(now);
        if (bare == typeof(double) || bare == typeof(float)) return 0d;
        if (bare == typeof(byte[])) return Array.Empty<byte>();
        if (bare == typeof(Guid)) return Guid.Empty.ToString("N");
        //  عدد، بولی و enum — همه در SQLite عددند
        return 0L;
    }

    /// <summary>همان شکلی که ارائه‌دهندهٔ SQLite تاریخ را می‌نویسد.</summary>
    private static string DbTime(DateTime t) =>
        (t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : t)
        .ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);

    /// <summary>مقدارِ خامِ دیتابیس ⇒ چیزی که <see cref="OpLog.Plain"/> بفهمد.</summary>
    private static object? FromDb(object? v, Type clr)
    {
        if (v is null || v is DBNull) return null;
        var bare = Nullable.GetUnderlyingType(clr) ?? clr;
        if (bare == typeof(bool) && v is long bl) return bl != 0;
        if (bare == typeof(DateTime) && v is string ds
            && DateTime.TryParse(ds, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return v;
    }

    /// <summary>نتیجهٔ بازگردانی از عکسِ سرور.</summary>
    public sealed record RestoreReport(int Applied, int Skipped, int Failed);

    /// <summary>
    /// ══ عکسِ سرور را روی دفترِ محلی می‌نشاند — بندِ ۲۰٫۵ ═══════════════════
    ///
    /// «گوشیِ نو / کامپیوترِ تازه»: یک بار همهٔ ردیف‌های زنده، و از آن پس
    /// فقط تغییرها.
    ///
    /// ⛔ <b>هیچ ردیفی پاک نمی‌شود.</b> آن‌چه روی سرور هست می‌نشیند یا
    /// به‌روز می‌شود؛ ردیفی که فقط این‌جاست دست نمی‌خورد. پاک کردنِ دفترِ
    /// محلی بر اساسِ «روی سرور نبود» یعنی یک بار خطای شبکه = دفترِ خالی.
    ///
    /// ⚠️ صدا زننده باید <b>پیش از</b> این یک پشتیبان گرفته باشد
    /// (<see cref="SyncBackup"/>) — این تابع خودش پشتیبان نمی‌گیرد، چون
    /// از دو جا صدا زده می‌شود و دو نسخهٔ پشتیبان بی‌جا بود.
    /// </summary>
    public RestoreReport RestoreSnapshot(JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object) return new RestoreReport(0, 0, 0);

        var ops = new List<IncomingOp>();
        if (snapshot.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Object)
        {
            foreach (var t in tables.EnumerateObject())
            {
                if (t.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (var row in t.Value.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object) continue;
                    var uid = row.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String
                        ? i.GetString() ?? "" : "";
                    if (uid.Length == 0) continue;
                    if (!row.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) continue;
                    //  «update» هر دو کار را می‌کند: ردیفِ نبوده ساخته می‌شود
                    ops.Add(new IncomingOp("", t.Name, uid, "update", data.Clone(), 0));
                }
            }
        }

        var applied = ApplyIncoming(ops);
        if (snapshot.TryGetProperty("cursor", out var c) && c.ValueKind == JsonValueKind.Number
            && c.TryGetInt64(out var cursor) && cursor > 0)
            Update(x => x.Cursor = cursor);

        return new RestoreReport(applied.Applied, applied.Skipped, applied.Failed);
    }

    // ── نقشهٔ جدول‌ها ──────────────────────────────────────────────────

    /// <summary>یک ستون — نامِ ویژگی، نامِ ستون، نوعش، و این‌که تهی می‌پذیرد یا نه.</summary>
    public sealed record ColumnInfo(string Name, string Column, Type Clr, bool Nullable);

    /// <summary>یک جدولِ داده — نامِ موجودیت (که سرور می‌شناسد) و جدولِ واقعی.</summary>
    public sealed record TableInfo(string Entity, string Table, IReadOnlyList<ColumnInfo> Columns);

    /// <summary>
    /// جدول‌هایی که همگام می‌شوند — همان‌هایی که <see cref="OpLog.Local"/>
    /// کنارشان نگذاشته.
    /// </summary>
    public static IReadOnlyList<TableInfo> DataTables(PumpDbContext db)
    {
        var list = new List<TableInfo>();
        foreach (var et in db.Model.GetEntityTypes())
        {
            if (!typeof(EntityBase).IsAssignableFrom(et.ClrType)) continue;
            var name = et.ClrType.Name;
            if (OpLog.Local.Contains(name)) continue;
            var table = et.GetTableName();
            if (string.IsNullOrEmpty(table)) continue;

            var cols = new List<ColumnInfo>();
            foreach (var p in et.GetProperties())
            {
                var column = p.GetColumnName();
                if (string.IsNullOrEmpty(column)) continue;
                cols.Add(new ColumnInfo(p.Name, column, p.ClrType, p.IsNullable));
            }
            list.Add(new TableInfo(name, table, cols));
        }
        return list.OrderBy(x => x.Entity, StringComparer.Ordinal).ToList();
    }

    /// <summary>همان قاعدهٔ <c>normName</c>ِ سرور — حروفِ کوچک، بی جداکننده.</summary>
    public static string Norm(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    // ── SQLِ خام ───────────────────────────────────────────────────────

    private static void Exec(PumpDbContext db, string sql, params object?[] args)
    {
        db.Database.ExecuteSqlRaw(sql, args.Select(a => a ?? DBNull.Value).ToArray());
    }

    private static object? Scalar(PumpDbContext db, string sql, params object?[] args)
    {
        //  ⚠️ `ExecuteSqlRaw` مقدار برنمی‌گرداند، پس این یکی از درِ خودِ
        //  اتصال می‌رود — همان الگوی `PumpDbFactory`.
        var text = sql;
        for (var i = 0; i < args.Length; i++) text = text.Replace("{" + i + "}", "$a" + i);
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        var opened = cmd.Connection!.State != System.Data.ConnectionState.Open;
        if (opened) cmd.Connection.Open();
        try
        {
            cmd.CommandText = text;
            for (var i = 0; i < args.Length; i++) Add(cmd, "$a" + i, args[i]);
            var v = cmd.ExecuteScalar();
            return v is DBNull ? null : v;
        }
        finally { if (opened) cmd.Connection.Close(); }
    }

    private static long? ScalarLong(PumpDbContext db, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        var opened = cmd.Connection!.State != System.Data.ConnectionState.Open;
        if (opened) cmd.Connection.Open();
        try
        {
            cmd.CommandText = sql;
            foreach (var (name, value) in args) Add(cmd, name, value);
            var v = cmd.ExecuteScalar();
            if (v is null || v is DBNull) return null;
            return Convert.ToInt64(v, CultureInfo.InvariantCulture);
        }
        finally { if (opened) cmd.Connection.Close(); }
    }

    private static List<Dictionary<string, object?>> ReadRows(
        PumpDbContext db, string sql, params (string Name, object? Value)[] args)
    {
        var rows = new List<Dictionary<string, object?>>();
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        var opened = cmd.Connection!.State != System.Data.ConnectionState.Open;
        if (opened) cmd.Connection.Open();
        try
        {
            cmd.CommandText = sql;
            foreach (var (name, value) in args) Add(cmd, name, value);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
        }
        finally { if (opened) cmd.Connection.Close(); }
        return rows;
    }

    private static void Add(DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
