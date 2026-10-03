using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>چه چیزی بیرون برود: یک قرض‌دار، یک شرکت، یا یک بخش — در یک بازهٔ تاریخ.</summary>
/// <param name="Kind">debtor · company · safe · expenses · sarrafi · retail · income · waraq · parcha · invoices</param>
/// <param name="FromKey">کلیدِ تاریخِ آغاز (‎Shamsi.Key‎)؛ ۰ یعنی از اول.</param>
/// <param name="ToKey">کلیدِ تاریخِ پایان، خودش هم شمرده می‌شود؛ ۰ یعنی تا آخر.</param>
public sealed record PortablePick(string Kind, long Id = 0, int FromKey = 0, int ToKey = 0);

/// <summary>یک جدول برای دیدن در اکسل — ستون ⇒ مقدار.</summary>
public sealed record PortableTable(string Entity, IReadOnlyList<string> Columns,
                                   IReadOnlyList<IReadOnlyList<object?>> Rows);

/// <summary>خروجیِ یک بخش: عکسِ قابلِ بازگرداندن + همان ردیف‌ها برای دیدن.</summary>
public sealed record PortableExport(string SnapshotJson, IReadOnlyList<PortableTable> Tables, int RowCount);

/// <summary>نتیجهٔ آوردنِ یک فایلِ بخش.</summary>
public sealed record PortableImport(int Added, int Updated, int Failed, string Why, int SkippedDeleted = 0);

/// <summary>
/// ══ خروجی و ورودیِ یک حساب یا یک بخش (۱۴۰۵/۰۷/۱۹) ══════════════════════════════
///
/// خواستهٔ صاحب ریپو: «یک حساب یا بخش (یک قرض‌دار، مصارفِ یک ماه، یک شرکت…) را
/// برای بازهٔ ماه/سال بیرون بدهم و همان فایل بی خرابی دوباره وارد شود.»
///
/// ⛔ <b>هیچ راهِ نوشتنِ تازه‌ای ساخته نشد</b>: ورودی همان <see cref="ApplyIncoming"/>
/// است که ردیف‌های دستگاهِ دیگر را می‌نشاند و سال‌ها آزموده شده — با شناسهٔ سراسری
/// (‎SyncUid‎)، پدر پیش از فرزند، و کلیدِ خارجی از راهِ شناسهٔ پدر (نه عددِ این
/// کامپیوتر). پس:
///   • ردیفی که هست <b>به‌روز</b> می‌شود، ردیفی که نیست <b>ساخته</b> می‌شود؛
///   • <b>هیچ ردیفی پاک نمی‌شود</b> و ردیفِ پاک‌شده زنده نمی‌شود (‎DeletedAt‎ نمی‌رود)؛
///   • پدرِ هر ردیف (حسابِ ردیفِ قرض، شرکتِ ردیفِ شرکت، شیفتِ پارچه…) خودش همراهِ
///     فایل می‌رود، پس روی کامپیوترِ دیگر هم هیچ ردیفی بی‌پدر نمی‌ماند.
/// و چون آن راه هیچ opی نمی‌سازد، ردیف‌های نشسته پس از ورود op می‌گیرند تا به سرورِ
/// حساب هم برسند (<see cref="StampUpdates"/>).
/// </summary>
public sealed partial class SyncStore
{
    public const string PortableFormat = "pumpyaqobi.part";

    /// <summary>ستون‌هایی که مالِ همین کامپیوترند یا فقط دفترداری‌اند — نه در فایل، نه در اکسل.</summary>
    private static readonly HashSet<string> NotPortable = new(StringComparer.Ordinal)
        { "Id", "SyncUid", "CreatedAt", "UpdatedAt", "DeletedAt" };

    public PortableExport ExportPortable(PortablePick pick)
    {
        using var db = _dbf.Create();
        var tables = DataTables(db);
        var byEntity = tables.ToDictionary(t => t.Entity, StringComparer.Ordinal);
        var got = new Dictionary<string, Dictionary<long, Dictionary<string, object?>>>(StringComparer.Ordinal);

        void Take(string entity, string where, params (string, object?)[] args)
        {
            if (!byEntity.TryGetValue(entity, out var t)) return;
            var rows = ReadRows(db, $"SELECT * FROM \"{t.Table}\" WHERE \"DeletedAt\" IS NULL AND ({where});", args);
            if (!got.TryGetValue(entity, out var bag)) got[entity] = bag = new();
            foreach (var r in rows)
                if (r.TryGetValue("Id", out var idv) && idv is not null)
                    bag[Convert.ToInt64(idv, CultureInfo.InvariantCulture)] = r;
        }
        IEnumerable<long> Ids(string entity) => got.TryGetValue(entity, out var b) ? b.Keys : Enumerable.Empty<long>();
        static string In(IEnumerable<long> ids) { var s = string.Join(",", ids); return s.Length == 0 ? "-1" : s; }

        var dateSql = pick.FromKey > 0 || pick.ToKey > 0
            ? $"\"DateKey\" >= {Math.Max(0, pick.FromKey)} AND \"DateKey\" <= {(pick.ToKey > 0 ? pick.ToKey : int.MaxValue)}"
            : "1=1";

        switch (pick.Kind)
        {
            case "debtor":
                Take("Debtor", "\"Id\" = $id", ("$id", pick.Id));
                Take("DebtAccount", "\"DebtorId\" = $id OR \"MainOfDebtorId\" = $id", ("$id", pick.Id));
                var accts = In(Ids("DebtAccount"));
                Take("DebtRow", $"(\"FuelAccountId\" IN ({accts}) OR \"MoneyAccountId\" IN ({accts})) AND {dateSql}");
                Take("RasidEntry", $"\"AccountId\" IN ({accts})");
                Take("DebtTableArchive", $"\"AccountId\" IN ({accts})");
                Take("Invoice", $"\"DebtAccountId\" IN ({accts}) AND {dateSql}");
                break;
            case "company":
                Take("TilCompany", "\"Id\" = $id", ("$id", pick.Id));
                Take("CompanyRow", $"\"CompanyId\" = $id AND {dateSql}", ("$id", pick.Id));
                Take("CompanyTableArchive", "\"CompanyId\" = $id", ("$id", pick.Id));
                break;
            case "waraq":
                Take("WaraqEntry", dateSql);
                Take("WaraqShift", $"\"WaraqId\" IN ({In(Ids("WaraqEntry"))})");
                var shifts = In(Ids("WaraqShift"));
                Take("WaraqPump", $"\"ShiftId\" IN ({shifts})");
                Take("WaraqTransaction", $"\"ShiftId\" IN ({shifts})");
                break;
            case "parcha": Take("ParchaReport", dateSql); break;
            case "safe": Take("SafeEntry", dateSql); break;
            case "expenses": Take("Expense", dateSql); break;
            case "sarrafi": Take("ExchangeRow", dateSql); break;
            case "retail": Take("RetailRow", dateSql); break;
            case "income": Take("ExtraIncome", dateSql); break;
            case "invoices": Take("Invoice", dateSql); break;
            default: throw new ArgumentException("بخشِ ناشناس: " + pick.Kind);
        }

        //  ⛔ پدرِ هر ردیف همراهِ فایل — تا روی هیچ کامپیوتری ردیفی بی‌پدر نماند
        for (var round = 0; round < 8; round++)
        {
            var need = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
            foreach (var (entity, bag) in got)
            {
                var t = byEntity[entity];
                foreach (var fk in t.Fks)
                    foreach (var row in bag.Values)
                        if (row.TryGetValue(fk.Column, out var v) && v is not null
                            && Convert.ToInt64(v, CultureInfo.InvariantCulture) is var pid && pid > 0
                            && !(got.TryGetValue(fk.ParentEntity, out var pb) && pb.ContainsKey(pid)))
                            (need.TryGetValue(fk.ParentEntity, out var s) ? s : need[fk.ParentEntity] = new()).Add(pid);
            }
            if (need.Count == 0) break;
            foreach (var (entity, ids) in need)
            {
                if (!byEntity.TryGetValue(entity, out var pt)) continue;
                var rows = ReadRows(db, $"SELECT * FROM \"{pt.Table}\" WHERE \"Id\" IN ({In(ids)});");
                if (!got.TryGetValue(entity, out var bag)) got[entity] = bag = new();
                foreach (var r in rows) bag[Convert.ToInt64(r["Id"], CultureInfo.InvariantCulture)] = r;
            }
        }

        //  ── عکس (برای ورودی) و جدول‌ها (برای دیدن)، پدر پیش از فرزند ──
        var snapTables = new JsonObject();
        var display = new List<PortableTable>();
        var count = 0;
        foreach (var t in tables)
        {
            if (!got.TryGetValue(t.Entity, out var bag) || bag.Count == 0) continue;
            var arr = new JsonArray();
            var cols = t.Columns.Where(c => !NotPortable.Contains(c.Name) && t.Fks.All(f => f.Property != c.Name))
                                .Select(c => c.Name).ToList();
            var view = new List<IReadOnlyList<object?>>();
            foreach (var row in bag.Values.OrderBy(r => r.TryGetValue("DateKey", out var dk) ? Convert.ToInt64(dk ?? 0L, CultureInfo.InvariantCulture) : 0L)
                                           .ThenBy(r => Convert.ToInt64(r["Id"], CultureInfo.InvariantCulture)))
            {
                if (!row.TryGetValue("SyncUid", out var u) || u is not string uid || uid.Length == 0) continue;
                var data = new JsonObject();
                foreach (var col in t.Columns)
                {
                    if (NotPortable.Contains(col.Name) || !row.TryGetValue(col.Column, out var v)) continue;
                    data[col.Name] = JsonSerializer.SerializeToNode(OpLog.Plain(FromDb(v, col.Clr)));
                }
                foreach (var fk in t.Fks)
                {
                    string? puid = null;
                    if (row.TryGetValue(fk.Column, out var pv) && pv is not null
                        && Convert.ToInt64(pv, CultureInfo.InvariantCulture) is var pid && pid > 0)
                        puid = got.TryGetValue(fk.ParentEntity, out var pb) && pb.TryGetValue(pid, out var prow)
                            ? prow.GetValueOrDefault("SyncUid") as string : null;
                    data[Sidecar(fk.Property)] = puid;
                }
                arr.Add(new JsonObject { ["id"] = uid, ["data"] = data });
                view.Add(cols.Select(c =>
                {
                    var col = t.Columns.First(x => x.Name == c);
                    return Typed(FromDb(row.GetValueOrDefault(col.Column), col.Clr), col.Clr);
                }).ToList());
                count++;
            }
            if (arr.Count == 0) continue;
            snapTables[t.Entity] = arr;
            display.Add(new PortableTable(t.Entity, cols, view));
        }
        var snap = new JsonObject
        {
            ["format"] = PortableFormat,
            ["v"] = 1,
            ["kind"] = pick.Kind,
            ["from"] = pick.FromKey,
            ["to"] = pick.ToKey,
            ["at"] = AppClock.UnixMs,
            ["tables"] = snapTables,
        };
        return new PortableExport(snap.ToJsonString(), display, count);
    }

    /// <summary>
    /// مقدارِ برگهٔ دیدنیِ اکسل با نوعِ واقعیِ ستون: مبلغ‌ها متن ذخیره می‌شوند
    /// («12960.0»)، پس به عدد برمی‌گردند تا اکسل عدد ببیند و جمع بزند؛ نوعِ شمارشی
    /// همان شمارش. ⛔ فقط برای دیدن است — برگهٔ پنهان همان مقدارِ خامِ دفتر را دارد.
    /// </summary>
    private static object? Typed(object? v, Type clr)
    {
        var t = Nullable.GetUnderlyingType(clr) ?? clr;
        try
        {
            if (v is null) return null;
            if (t.IsEnum) return Enum.ToObject(t, Convert.ToInt64(v, CultureInfo.InvariantCulture));
            if (t == typeof(decimal) && v is string ds
                && decimal.TryParse(ds, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
            if (t == typeof(bool)) return Convert.ToBoolean(v, CultureInfo.InvariantCulture);
        }
        catch { }
        return v;
    }

    /// <summary>
    /// فایلِ یک بخش را می‌نشاند — شرحِ قاعده‌ها بالای همین کلاس.
    /// ⚠️ پیش از این، صدا زننده یک پشتیبان می‌گیرد (‎SyncBackup‎/‎BackupService‎).
    /// </summary>
    public PortableImport ImportPortable(JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object
            || !snapshot.TryGetProperty("format", out var f) || f.GetString() != PortableFormat
            || !snapshot.TryGetProperty("tables", out var tables) || tables.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("این فایل، فایلِ بخشِ همین برنامه نیست");

        var ops = new List<IncomingOp>();
        var existing = new HashSet<string>(StringComparer.Ordinal);
        var skipped = 0;
        using (var db = _dbf.Create())
        {
            var map = DataTables(db).ToDictionary(x => Norm(x.Entity), x => x, StringComparer.Ordinal);
            foreach (var t in tables.EnumerateObject())
            {
                if (!map.TryGetValue(Norm(t.Name), out var ti) || t.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (var row in t.Value.EnumerateArray())
                {
                    var uid = row.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() ?? "" : "";
                    if (uid.Length == 0 || !row.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) continue;
                    //  ⛔ ‎DeletedAt‎ هرگز از فایل نمی‌آید — ورودی چیزی را نه پاک می‌کند نه زنده
                    var clean = new JsonObject();
                    foreach (var p in data.EnumerateObject())
                        if (!NotPortable.Contains(p.Name)) clean[p.Name] = JsonNode.Parse(p.Value.GetRawText());
                    //  ⛔ ردیفی که این‌جا پاک شده با آوردنِ فایل زنده نمی‌شود — ‎ApplyIncoming‎
                    //  برای همگام‌سازی «ویرایش پس از حذف» را زنده می‌کند و این‌جا آن قاعده نیست.
                    var here = ScalarLong(db, $"SELECT CASE WHEN \"DeletedAt\" IS NULL THEN 1 ELSE 2 END FROM \"{ti.Table}\" WHERE \"SyncUid\" = $u LIMIT 1;", ("$u", uid));
                    if (here == 2) { skipped++; continue; }
                    using var doc = JsonDocument.Parse(clean.ToJsonString());
                    ops.Add(new IncomingOp("", ti.Entity, uid, "update", doc.RootElement.Clone(), 0));
                    if (here is not null) existing.Add(ti.Entity + "|" + uid);
                }
            }
        }

        var rep = ApplyIncoming(ops);
        var failed = new HashSet<string>(rep.FailedOps.Select(o => o.Table + "|" + o.RowUid), StringComparer.Ordinal);
        var done = ops.Where(o => !failed.Contains(o.Table + "|" + o.RowUid)).Select(o => (o.Table, o.RowUid)).ToList();
        StampUpdates(done);
        var updated = done.Count(d => existing.Contains(d.Table + "|" + d.RowUid));
        return new PortableImport(done.Count - updated, updated, rep.Failed, rep.LastWhy, skipped);
    }

    /// <summary>
    /// ردیف‌هایی که از راهِ ‎ApplyIncoming‎ نشستند op ندارند؛ برای هر کدام یک «update»ِ
    /// کامل ساخته می‌شود تا به سرورِ حساب و دستگاه‌های دیگر هم برسد (کلیدِ خارجی
    /// هنگامِ فرستادن با شناسهٔ پدر همراه می‌شود — ‎AttachParents‎).
    /// </summary>
    private void StampUpdates(IReadOnlyList<(string Table, string Uid)> rows)
    {
        if (rows.Count == 0) return;
        using var db = _dbf.Create();
        var map = DataTables(db).ToDictionary(x => x.Entity, StringComparer.Ordinal);
        var now = AppClock.UnixMs;
        foreach (var grp in rows.GroupBy(r => r.Table))
        {
            if (!map.TryGetValue(grp.Key, out var t)) continue;
            foreach (var chunk in grp.Select(g => g.Uid).Chunk(400))
            {
                var args = chunk.Select((u, i) => ($"$u{i}", (object?)u)).ToArray();
                var list = string.Join(",", args.Select(a => a.Item1));
                foreach (var row in ReadRows(db, $"SELECT * FROM \"{t.Table}\" WHERE \"SyncUid\" IN ({list});", args))
                {
                    var uid = (string)row["SyncUid"]!;
                    var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var col in t.Columns)
                    {
                        if (col.Name is "Id" or "SyncUid") continue;
                        if (row.TryGetValue(col.Column, out var v)) fields[col.Name] = OpLog.Plain(FromDb(v, col.Clr));
                    }
                    var json = JsonSerializer.Serialize(fields);
                    db.SyncOps.Add(new SyncOp
                    {
                        OpId = Ulid.New(now), TableName = t.Entity, RowUid = uid, OpType = "update",
                        FieldsJson = json, Hash = OpLog.HashOf(t.Entity, uid, "update", json),
                        ClientTs = now, SchemaVersion = OpLog.SchemaVersion,
                    });
                }
            }
        }
        Quiet(db);
    }
}
