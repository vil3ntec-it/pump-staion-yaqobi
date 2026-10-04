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
    /// <summary>شمارِ تعارض‌هایی که از بالا آمدنِ برنامه دیده شد.</summary>
    public int Conflicts { get; private set; }

    /// <summary>
    /// پس از فرستادنِ موفق: این opها با این شماره‌ها روی سرور نشستند.
    /// ⛔ شورا د۱ — شماره <b>روی خودِ op در دیسک</b> می‌نشیند (<see cref="SyncOp.ServerSeq"/>)،
    /// نه در حافظهٔ یک دور: گرفتن هر سی ثانیه است، پس دوری که می‌فرستد خیلی وقت‌ها
    /// نمی‌گیرد، و برنامه می‌تواند میانِ فرستادن و گرفتن بسته شود. پیش از این ردِ
    /// «همین حالا رفته» با پایانِ همان دور پاک می‌شد و opِ کهنه‌ترِ کامپیوترِ دیگر
    /// دورِ بعد روی مقدارِ تازه‌ترِ ما می‌نشست — دو عدد برای همیشه، بی هیچ ردپایی.
    /// opی که سرور شماره‌اش را نگفت (سرورِ کهنه) شمرده نمی‌شود — همان رفتارِ پیشین.
    /// </summary>
    public void NotePushed(IEnumerable<SyncOp> ops, IReadOnlyDictionary<string, long> seqs)
    {
        var want = ops.Where(o => seqs.TryGetValue(o.OpId, out var q) && q > 0).Select(o => o.OpId).ToList();
        if (want.Count == 0) return;
        using var db = _dbf.Create();
        foreach (var chunk in want.Chunk(500))
            foreach (var row in db.SyncOps.Where(x => chunk.Contains(x.OpId)).ToList())
                row.ServerSeq = seqs[row.OpId];
        Quiet(db);
    }

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
        //  ‎"جدول|ردیف|فیلد"‎ ⇒ (مقدارِ خامی که فرستادیم، ‎server_seq‎ِ همان op) — از دیسک
        private readonly Dictionary<string, (string Raw, long Seq)> _pushed = new(StringComparer.Ordinal);
        private readonly long _at;
        private readonly HashSet<SyncOp> _dirty = new();
        public int Made { get; private set; }

        public ConflictCtx(PumpDbContext db, IEnumerable<(string Table, string Uid)> rows, long cursor, DateTime now)
        {
            _db = db;
            _at = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            var uids = rows.Select(r => r.Uid).Distinct().ToList();
            if (uids.Count == 0) return;
            //  ⚡ فقط ردیف‌های همین دسته — صفِ بلند خوانده نمی‌شود
            foreach (var chunk in uids.Chunk(500))
            {
                var list = db.SyncOps.Where(o => o.OpType != "delete" && chunk.Contains(o.RowUid)
                                                 && (!o.Synced || o.ServerSeq > cursor)).ToList();
                foreach (var o in list.Where(o => o.Synced && o.ServerSeq > cursor))
                {
                    //  ⛔ د۱ — رفته و هنوز پس از مکان‌نما: گرفتن هنوز از آن نگذشته
                    if (JsonNode.Parse(o.FieldsJson ?? "{}") is not JsonObject pf) continue;
                    foreach (var kv in pf)
                    {
                        if (kv.Key.EndsWith('@')) continue;
                        var k = o.TableName + "|" + o.RowUid + "|" + kv.Key;
                        if (!_pushed.TryGetValue(k, out var had) || had.Seq < o.ServerSeq)
                            _pushed[k] = (kv.Value?.ToJsonString() ?? "null", o.ServerSeq);
                    }
                }
                foreach (var o in list.Where(o => !o.Synced && o.OpType == "update"))
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

            //  ۱) رفته و گرفتن هنوز از آن نگذشته ⇒ ترتیبِ سرور تصمیم می‌گیرد (همان قاعدهٔ ‎applyOp‎ِ سرور)
            //  ⛔ د۱: فقط جایی که مالِ ما <b>جلوتر</b> است تصمیم این‌جاست. رسیدهٔ پس از مالِ ما
            //  همان راهِ عادی را می‌رود — ردِ ما روی دیسک می‌ماند و نباید هر ویرایشِ بعدیِ
            //  کامپیوترِ دیگر را «تعارض» بخواند.
            if (op.ServerSeq > 0 && _pushed.TryGetValue(table + "|" + op.RowUid + "|" + field, out var mine)
                && op.ServerSeq < mine.Seq)
            {
                //  این op پیش از مالِ ما نشسته بود ⇒ روی سرور مالِ ما جلوتر است و همین‌جا می‌ماند
                var mineObj = new JsonObject { [field] = JsonNode.Parse(mine.Raw) }.ToJsonString();
                if (!Same(mine.Raw, remoteRaw)) Add(table, op, field, mineObj, remoteRaw, "local");
                return false;
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
