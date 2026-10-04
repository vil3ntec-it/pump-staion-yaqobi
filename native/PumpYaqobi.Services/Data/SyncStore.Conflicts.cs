using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ شورا، ب۱ — تعارضِ دو کامپیوتر دیده شود، نه بی‌صدا ═════════════════════
///
/// تا ۳.۱.۲۴۰ <c>ApplyOne</c> opِ رسیده را روی ردیف می‌نوشت بی آن‌که ببیند
/// همان فیلد این‌جا هم عوض شده: «آخرین نوشتن برنده»، بی هیچ خبری — و گاهی
/// حتی وارونه (فرستادن پیش از گرفتن است، پس opِ <b>کهنه‌ترِ</b> دستگاهِ دیگر
/// روی مقدارِ <b>تازه‌ترِ</b> ما می‌نشست و دو کامپیوتر برای همیشه دو عدد
/// داشتند).
///
/// قاعده، همان ترتیبِ سرور (آخرین opِ سرور برنده است):
///   • همان فیلد در یک opِ <b>نرفتهٔ</b> ما هست ⇒ رسیده می‌نشیند و آن فیلد از
///     opِ ما برداشته می‌شود (پس دو کامپیوتر به یک عدد می‌رسند)؛ مقدارِ ما در
///     <see cref="SyncConflict"/> می‌ماند.
///   • همان فیلد در همین دور <b>رفته</b> و این op پیش از آن روی سرور نشسته
///     (‎ServerSeq ≤ مکان‌نمای فرستادن‎) ⇒ مالِ ما روی سرور جلوتر است و همین‌جا
///     می‌ماند؛ مقدارِ رسیده در تعارض می‌ماند.
/// ⛔ هیچ مقداری پاک نمی‌شود و «برگردان» یک opِ تازه است، نه پاک کردنِ چیزی.
/// ⚠️ دلتا (<c>$inc</c>) تعارض نیست — دو کم‌شدن هر دو حساب می‌شوند.
/// </summary>
public sealed partial class SyncStore
{
    /// <summary>‎"جدول|ردیف|فیلد"‎ ⇒ (مقدارِ خامی که همین دور فرستادیم، ‎server_seq‎ِ همان op).</summary>
    private readonly Dictionary<string, (string Raw, long Seq)> _justPushed = new(StringComparer.Ordinal);

    /// <summary>شمارِ تعارض‌هایی که از بالا آمدنِ برنامه دیده شد.</summary>
    public int Conflicts { get; private set; }

    /// <summary>
    /// پس از فرستادنِ موفق: این فیلدها همین حالا با این شماره‌ها روی سرور نشستند.
    /// ‎SyncEngine‎ پایانِ همین دور <see cref="ClearPushed"/> را می‌زند.
    /// opی که سرور شماره‌اش را نگفت (سرورِ کهنه) شمرده نمی‌شود — همان رفتارِ پیشین.
    /// </summary>
    public void NotePushed(IEnumerable<SyncOp> ops, IReadOnlyDictionary<string, long> seqs)
    {
        foreach (var op in ops)
        {
            if (op.OpType != "update" && op.OpType != "insert") continue;
            if (!seqs.TryGetValue(op.OpId, out var seq) || seq <= 0) continue;
            if (JsonNode.Parse(op.FieldsJson ?? "{}") is not JsonObject o) continue;
            foreach (var kv in o)
            {
                if (kv.Key.EndsWith('@')) continue;
                var key = op.TableName + "|" + op.RowUid + "|" + kv.Key;
                if (!_justPushed.TryGetValue(key, out var had) || had.Seq < seq)
                    _justPushed[key] = (kv.Value?.ToJsonString() ?? "null", seq);
            }
        }
    }

    /// <summary>پایانِ دور — «همین حالا رفته» دیگر معنا ندارد.</summary>
    public void ClearPushed() => _justPushed.Clear();

    /// <summary>تعارض‌های باز، تازه‌ترین اول.</summary>
    public IReadOnlyList<SyncConflict> OpenConflicts()
    {
        using var db = _dbf.Create();
        return db.SyncConflicts.AsNoTracking().Where(x => x.State == 0).OrderByDescending(x => x.Id).ToList();
    }

    /// <summary>شمارِ تعارض‌های باز — ارزان، برای نوار.</summary>
    public int OpenConflictCount()
    {
        using var db = _dbf.Create();
        return db.SyncConflicts.Count(x => x.State == 0);
    }

    /// <summary>«همین بماند» — فقط ردپا بسته می‌شود؛ هیچ داده‌ای عوض نمی‌شود.</summary>
    public bool KeepConflict(long id)
    {
        using var db = _dbf.Create();
        var c = db.SyncConflicts.FirstOrDefault(x => x.Id == id && x.State == 0);
        if (c is null) return false;
        c.State = 1;
        db.SaveChanges();
        return true;
    }

    /// <summary>
    /// «مقدارِ دیگر را برگردان» — مقداری که در دفتر <b>نماند</b> روی ردیف می‌نشیند
    /// و یک opِ تازه می‌سازد، پس روی همهٔ کامپیوترها همگام می‌شود.
    /// ⛔ پاک کردنی در کار نیست؛ ردیفِ پاک‌شده هم با همین زنده نمی‌شود.
    /// </summary>
    public bool RestoreConflict(long id)
    {
        using var db = _dbf.Create();
        var c = db.SyncConflicts.FirstOrDefault(x => x.Id == id && x.State == 0);
        if (c is null) return false;
        var t = DataTables(db).FirstOrDefault(x => x.Entity == c.TableName);
        var col = t?.Columns.FirstOrDefault(x => x.Name == c.Field);
        if (t is null || col is null) return false;
        var rowId = ScalarLong(db, $"SELECT \"Id\" FROM \"{t.Table}\" WHERE \"SyncUid\" = $u AND \"DeletedAt\" IS NULL LIMIT 1;", ("$u", c.RowUid));
        if (rowId is null) return false;

        //  آن‌چه برمی‌گردد: مقدارِ ما اگر رسیده نشسته بود، و رسیده اگر مالِ ما مانده بود
        var fields = c.Winner == "remote"
            ? (JsonNode.Parse(c.LocalJson) as JsonObject ?? new JsonObject())
            : new JsonObject { [c.Field] = JsonNode.Parse(c.RemoteJson) };
        if (!fields.TryGetPropertyValue(c.Field, out var vNode)) return false;
        using var doc = JsonDocument.Parse(vNode?.ToJsonString() ?? "null");
        var value = Raw(doc.RootElement.Clone(), col.Clr);

        var now = AppClock.UtcNow;
        Exec(db, $"UPDATE \"{t.Table}\" SET \"{col.Column}\" = {{0}}, \"UpdatedAt\" = {{1}} WHERE \"Id\" = {{2}};",
             value, DbTime(now), rowId.Value);
        var json = fields.ToJsonString();
        var ms = AppClock.UnixMs;
        db.SyncOps.Add(new SyncOp
        {
            OpId = Ulid.New(ms), TableName = t.Entity, RowUid = c.RowUid, OpType = "update",
            FieldsJson = json, Hash = OpLog.HashOf(t.Entity, c.RowUid, "update", json),
            ClientTs = ms, SchemaVersion = OpLog.SchemaVersion,
        });
        c.State = 2;
        Quiet(db);
        PumpDbContext.Bump();
        return true;
    }

    /// <summary>کارِ یک دسته: opهای نرفتهٔ همان ردیف‌ها، و تعارض‌های تازه.</summary>
    private sealed class ConflictCtx
    {
        private readonly PumpDbContext _db;
        private readonly Dictionary<string, List<(SyncOp Op, JsonObject Fields)>> _pending = new(StringComparer.Ordinal);
        private readonly IReadOnlyDictionary<string, (string Raw, long Seq)> _pushed;
        private readonly long _at;
        private readonly HashSet<SyncOp> _dirty = new();
        public int Made { get; private set; }

        public ConflictCtx(PumpDbContext db, IEnumerable<(string Table, string Uid)> rows,
                           IReadOnlyDictionary<string, (string Raw, long Seq)> pushed, DateTime now)
        {
            _db = db; _pushed = pushed;
            _at = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            var uids = rows.Select(r => r.Uid).Distinct().ToList();
            if (uids.Count == 0) return;
            //  ⚡ فقط ردیف‌های همین دسته — صفِ بلند خوانده نمی‌شود
            foreach (var chunk in uids.Chunk(500))
            {
                var list = db.SyncOps.Where(o => !o.Synced && o.OpType == "update" && chunk.Contains(o.RowUid)).ToList();
                foreach (var o in list)
                {
                    if (JsonNode.Parse(o.FieldsJson ?? "{}") is not JsonObject f) continue;
                    var key = o.TableName + "|" + o.RowUid;
                    if (!_pending.TryGetValue(key, out var l)) _pending[key] = l = new();
                    l.Add((o, f));
                }
            }
        }

        /// <summary>
        /// این فیلدِ رسیده بنشیند؟ ‎false‎ یعنی مالِ ما روی سرور جلوتر است.
        /// هر ناجوری یک <see cref="SyncConflict"/> می‌سازد.
        /// </summary>
        public bool TakeRemote(string table, IncomingOp op, string field, JsonElement remote)
        {
            if (remote.ValueKind == JsonValueKind.Object && remote.TryGetProperty("$inc", out _)) return true;
            var remoteRaw = remote.GetRawText();

            //  ۱) همین دور رفته ⇒ ترتیبِ سرور تصمیم می‌گیرد (همان قاعدهٔ ‎applyOp‎ِ سرور)
            if (op.ServerSeq > 0 && _pushed.TryGetValue(table + "|" + op.RowUid + "|" + field, out var mine))
            {
                var mineObj = new JsonObject { [field] = JsonNode.Parse(mine.Raw) }.ToJsonString();
                if (op.ServerSeq < mine.Seq)
                {
                    //  این op پیش از مالِ ما نشسته بود ⇒ روی سرور مالِ ما جلوتر است و همین‌جا می‌ماند
                    if (!Same(mine.Raw, remoteRaw)) Add(table, op, field, mineObj, remoteRaw, "local");
                    return false;
                }
                //  پس از مالِ ما نشسته ⇒ رسیده جلوتر است؛ مالِ ما در ردپا
                if (!Same(mine.Raw, remoteRaw)) Add(table, op, field, mineObj, remoteRaw, "remote");
                return true;
            }

            //  ۲) هنوز نرفته ⇒ رسیده می‌نشیند، فیلد از opِ ما برداشته می‌شود، مالِ ما در ردپا
            if (_pending.TryGetValue(table + "|" + op.RowUid, out var list))
            {
                foreach (var (o, f) in list)
                {
                    if (!f.TryGetPropertyValue(field, out var lv)) continue;
                    var localRaw = lv?.ToJsonString() ?? "null";
                    var keep = new JsonObject { [field] = JsonNode.Parse(localRaw) };
                    if (f.TryGetPropertyValue(field + "@", out var sv)) keep[field + "@"] = JsonNode.Parse(sv?.ToJsonString() ?? "null");
                    if (!Same(localRaw, remoteRaw)) Add(table, op, field, keep.ToJsonString(), remoteRaw, "remote");
                    f.Remove(field);
                    f.Remove(field + "@");
                    _dirty.Add(o);
                }
            }
            return true;
        }

        private static bool Same(string a, string b)
        {
            if (a == b) return true;
            try
            {
                using var x = JsonDocument.Parse(a);
                using var y = JsonDocument.Parse(b);
                return Num(x.RootElement) is { } p && Num(y.RootElement) is { } q ? p == q
                     : x.RootElement.ValueKind == y.RootElement.ValueKind && x.RootElement.ToString() == y.RootElement.ToString();
            }
            catch (JsonException) { return false; }

            //  مبلغ‌ها متن یا عددند — ‎"5"‎ و ‎5.0‎ یک مقدارند
            static decimal? Num(JsonElement e) =>
                e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var d) ? d
                : e.ValueKind == JsonValueKind.String && decimal.TryParse(e.GetString(), System.Globalization.NumberStyles.Number,
                      System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : null;
        }

        private void Add(string table, IncomingOp op, string field, string local, string remote, string winner)
        {
            _db.SyncConflicts.Add(new SyncConflict
            {
                TableName = table, RowUid = op.RowUid, Field = field, LocalJson = local, RemoteJson = remote,
                Winner = winner, RemoteOpId = op.OpId, At = _at,
            });
            Made++;
        }

        /// <summary>opهای کوتاه‌شده و تعارض‌ها — opی که هیچ فیلدی برایش نماند دیگر نمی‌رود.</summary>
        public void Save()
        {
            if (_dirty.Count == 0 && Made == 0) return;
            foreach (var o in _dirty)
            {
                var f = _pending[o.TableName + "|" + o.RowUid].First(x => ReferenceEquals(x.Op, o)).Fields;
                if (f.Count == 0) { _db.SyncOps.Remove(o); continue; }
                o.FieldsJson = f.ToJsonString();
                o.Hash = OpLog.HashOf(o.TableName, o.RowUid, o.OpType, o.FieldsJson);
            }
            Quiet(_db);
        }
    }
}
