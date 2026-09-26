using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ صف، تعارض، دلتا و سه روز آفلاین ═══════════════════════════════════
///
/// معیارِ پذیرشِ بندِ ۲۰٫۱۰ پرامپت، همان‌جا که برنامه باید جوابگو باشد:
///
///   • قطعِ اینترنتِ سه‌روزه ⇒ همهٔ تغییرها به ترتیب و بی گم شدن می‌روند
///   • دو دستگاه یک ردیف را عوض کنند ⇒ هیچ فیلدی گم نمی‌شود
///   • دو فروشِ هم‌زمان از دو دستگاه ⇒ موجودی درست کم می‌شود (<c>$inc</c>)
///   • حذف در برابر ویرایش ⇒ ویرایشِ بعد از حذف ردیف را زنده می‌کند
///   • گوشیِ نو ⇒ Snapshot و ادامه از همان‌جا
/// </summary>
public class SyncStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-sync-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;
    private readonly SyncStore _store;

    public SyncStoreTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
        _store = new SyncStore(_dbf);
    }

    public void Dispose()
    {
        OpLog.Enabled = true;
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        GC.SuppressFinalize(this);
    }

    private static JsonElement Json(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    private long AddRow(string title, decimal amount = 100m)
    {
        using var db = _dbf.Create();
        var row = new SafeEntry
        { Title = title, Amount = amount, DateKey = 14050701, MonthKey = "1405/07" };
        db.SafeEntries.Add(row);
        db.SaveChanges();
        return row.Id;
    }

    private string UidOf(long id)
    {
        using var db = _dbf.Create();
        return db.SafeEntries.Single(x => x.Id == id).SyncUid ?? "";
    }

    // ── صف و سه روز آفلاین ────────────────────────────────────────────

    /// <summary>
    /// ⛔ <b>سه روز آفلاین، و هیچ opی گم نمی‌شود.</b> صف در خودِ SQLite
    /// است، پس بسته شدنِ برنامه هم چیزی را نمی‌برد: این سنجه عمداً
    /// <see cref="SyncStore"/> را دور می‌ریزد و از نو می‌سازد.
    /// </summary>
    [Fact]
    public void Se_Rooz_Offline_Hich_Opi_Gom_Nemishavad()
    {
        for (var i = 0; i < 450; i++) AddRow("ردیف " + i);

        //  «برنامه بسته و دوباره باز شد»
        var again = new SyncStore(_dbf);
        Assert.Equal(450, again.Pending());

        //  دسته‌ها به ترتیب و با سقفِ خودِ سرور
        var sent = new List<string>();
        for (var round = 0; round < 3; round++)
        {
            var batch = again.Take();
            Assert.True(batch.Count <= SyncStore.MaxBatch);
            //  ترتیب حفظ شده
            Assert.Equal(batch.OrderBy(x => x.Id).Select(x => x.OpId).ToList(),
                         batch.Select(x => x.OpId).ToList());
            again.MarkResults(batch.ToDictionary(x => x.OpId, _ => "applied"));
            sent.AddRange(batch.Select(x => x.OpId));
        }

        Assert.Equal(0, again.Pending());
        Assert.Equal(450, sent.Distinct().Count());
    }

    /// <summary>
    /// ⛔ فقط <c>applied</c> و <c>duplicate</c> «رفته» می‌شوند — ولی opی که
    /// سرور ردش کرده هم دوباره فرستاده نمی‌شود و دلیلش می‌ماند، وگرنه صف
    /// تا ابد یک opِ خراب را پس و پیش می‌برد.
    /// </summary>
    [Fact]
    public void Natijeye_Server_Rooye_Saf_Minshinad()
    {
        AddRow("یک");
        AddRow("دو");
        var batch = _store.Take();
        Assert.Equal(2, batch.Count);

        _store.MarkResults(new Dictionary<string, string>
        {
            [batch[0].OpId] = "duplicate",
            [batch[1].OpId] = "unknown_table",
        });

        Assert.Equal(0, _store.Pending());
        using var db = _dbf.Create();
        Assert.Equal("", db.SyncOps.Single(x => x.OpId == batch[0].OpId).Rejected);
        Assert.Equal("unknown_table", db.SyncOps.Single(x => x.OpId == batch[1].OpId).Rejected);
    }

    /// <summary>⛔ opی که نرفته هیچ‌وقت هرس نمی‌شود، هر چقدر هم کهنه باشد.</summary>
    [Fact]
    public void Hars_Faghat_Opehaye_Rafte_Ra_Mibarad()
    {
        for (var i = 0; i < 30; i++) AddRow("ردیف " + i);
        var batch = _store.Take();
        _store.MarkResults(batch.Take(20).ToDictionary(x => x.OpId, _ => "applied"));

        _store.Prune(keep: 5);

        Assert.Equal(10, _store.Pending());
        using var db = _dbf.Create();
        //  پنج تای تازه‌ترِ رفته مانده‌اند، پانزده تای کهنه رفته‌اند
        Assert.Equal(15, db.SyncOps.Count());
    }

    // ── opهای رسیده ───────────────────────────────────────────────────

    /// <summary>
    /// ⛔ <b>تعارض سطحِ فیلد است، نه رکورد</b>: دستگاهِ دیگر «عنوان» را عوض
    /// کند و ما «مبلغ» را، هر دو می‌مانند.
    /// </summary>
    [Fact]
    public void Do_Dastgah_Do_Fild_Hich_Kodam_Gom_Nemishavad()
    {
        var id = AddRow("نامِ ما", 500m);
        var uid = UidOf(id);

        //  ما مبلغ را عوض کردیم
        using (var db = _dbf.Create())
        {
            var row = db.SafeEntries.Single(x => x.Id == id);
            row.Amount = 900m;
            db.SaveChanges();
        }

        //  دستگاهِ دیگر عنوان را عوض کرده
        var report = _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-1", nameof(SafeEntry), uid, "update", Json("{\"Title\":\"نامِ او\"}"), 1),
        });

        Assert.Equal(1, report.Applied);
        Assert.Equal(0, report.Failed);

        using var read = _dbf.Create();
        var final = read.SafeEntries.Single(x => x.Id == id);
        Assert.Equal("نامِ او", final.Title);
        Assert.Equal(900m, final.Amount);   // ⛔ مبلغِ ما گم نشد
    }

    /// <summary>
    /// ⛔ <b>دلتا</b> (بندِ ۲۰٫۴): «مقدارِ تازه» نه، «این‌قدر کم شد». بی
    /// این، دو فروشِ هم‌زمان از دو دستگاه یکی‌شان گم می‌شد.
    /// ⚠️ جمعش در C# است، نه <c>CAST(... AS REAL)</c>ی SQLite.
    /// </summary>
    [Fact]
    public void Delta_Do_Foroosh_Hamzaman_Har_Do_Hesab_Mishavand()
    {
        var id = AddRow("موجودی", 1000m);
        var uid = UidOf(id);

        _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-a", nameof(SafeEntry), uid, "update", Json("{\"Amount\":{\"$inc\":-300}}"), 1),
        });
        _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-b", nameof(SafeEntry), uid, "update", Json("{\"Amount\":{\"$inc\":-200}}"), 2),
        });

        using var read = _dbf.Create();
        Assert.Equal(500m, read.SafeEntries.Single(x => x.Id == id).Amount);
    }

    /// <summary>⛔ ویرایشِ پس از حذف، ردیف را <b>زنده</b> می‌کند.</summary>
    [Fact]
    public void Virayesh_Pas_Az_Hazf_Radif_Ra_Zende_Mikonad()
    {
        var id = AddRow("زنده می‌شود");
        var uid = UidOf(id);

        _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-del", nameof(SafeEntry), uid, "delete", Json("{}"), 1),
        });
        using (var gone = _dbf.Create()) Assert.Empty(gone.SafeEntries.ToList());

        _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-alive", nameof(SafeEntry), uid, "update", Json("{\"Title\":\"برگشت\"}"), 2),
        });

        using var read = _dbf.Create();
        var row = Assert.Single(read.SafeEntries.ToList());
        Assert.Equal("برگشت", row.Title);
    }

    /// <summary>
    /// ⛔ <b>opهای رسیده پژواک نمی‌شوند</b>: با SQLِ خام می‌نشینند، پس
    /// هیچ opِ تازه‌ای نمی‌سازند و دوباره به سرور برنمی‌گردند.
    /// </summary>
    [Fact]
    public void Opehaye_Reside_Pejvak_Nemishavand()
    {
        var id = AddRow("ردیف");
        var uid = UidOf(id);
        var before = _store.Pending();

        _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-x", nameof(SafeEntry), uid, "update", Json("{\"Title\":\"از دستگاهِ دیگر\"}"), 7),
        });

        Assert.Equal(before, _store.Pending());
    }

    /// <summary>نامِ جدولِ ناشناس بی‌صدا رد می‌شود، نه با استثنا.</summary>
    [Fact]
    public void Jadvale_Nashenas_Rad_Mishavad_Na_Mishkanad()
    {
        var report = _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-?", "JadvaleNabude", "uid-1", "update", Json("{\"a\":1}"), 1),
        });
        Assert.Equal(0, report.Applied);
        Assert.Equal(1, report.Skipped);
        Assert.Equal(0, report.Failed);
    }

    /// <summary>
    /// نامِ جدول با هر شکلِ نوشتن پذیرفته می‌شود — همان قاعدهٔ
    /// <c>normName</c>ِ سرور.
    /// </summary>
    [Fact]
    public void Name_Jadval_Ba_Har_Shekli_Peyda_Mishavad()
    {
        var id = AddRow("ردیف");
        var uid = UidOf(id);
        var report = _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-n", "safe_entry", uid, "update", Json("{\"Title\":\"جور شد\"}"), 1),
        });
        Assert.Equal(1, report.Applied);
    }

    /// <summary>
    /// ⛔ <b>ردیفِ رسیده با فیلدِ خالی (null) می‌نشیند</b> — هم ردیفِ تازه، هم
    /// ویرایش. تا ۱۴۰۵/۰۷/۱۴ هر دو با «no store type mapping for DBNull»ِ
    /// EF رد می‌شدند و کامپیوترِ دوم دفترِ خالی می‌دید (سنجهٔ `tensync`).
    /// </summary>
    [Fact]
    public void Radife_Reside_Ba_FieldeKhali_Mineshinad()
    {
        var report = _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-new", nameof(SafeEntry), "uid-null-1", "insert",
                Json("{\"Title\":\"از کامپیوترِ الف\",\"Amount\":\"125.5\",\"DateKey\":14050703,\"MonthKey\":\"1405/07\",\"Note\":null,\"SrcKey\":null}"), 1),
        });
        Assert.Equal(0, report.Failed);
        Assert.Equal(1, report.Applied);

        var id = AddRow("با یادداشت");
        using (var db = _dbf.Create()) { var r = db.SafeEntries.Single(x => x.Id == id); r.Note = "پاک می‌شود"; db.SaveChanges(); }
        var upd = _store.ApplyIncoming(new[]
        {
            new IncomingOp("op-clear", nameof(SafeEntry), UidOf(id), "update", Json("{\"Note\":null}"), 2),
        });
        Assert.Equal(0, upd.Failed);

        using var read = _dbf.Create();
        var got = read.SafeEntries.Single(x => x.SyncUid == "uid-null-1");
        Assert.Equal("از کامپیوترِ الف", got.Title);
        Assert.Equal(125.5m, got.Amount);
        Assert.Null(got.Note);
        Assert.Null(read.SafeEntries.Single(x => x.Id == id).Note);
    }

    // ── Snapshot ──────────────────────────────────────────────────────

    /// <summary>
    /// ⛔ «گوشیِ نو»: عکسِ سرور می‌نشیند و cursor جلو می‌رود — و <b>هیچ
    /// ردیفی پاک نمی‌شود</b>، حتی ردیفی که فقط این‌جاست.
    /// </summary>
    [Fact]
    public void Snapshot_Minshinad_Va_Hich_Radifi_Pak_Nemishavad()
    {
        var mine = AddRow("فقط این‌جا");

        var snap = Json("""
        {
          "cursor": 4242,
          "tables": {
            "SafeEntry": [
              { "id": "uid-from-server", "data": { "Title": "از سرور", "Amount": "250", "DateKey": 14050702 } }
            ]
          }
        }
        """);

        var report = _store.RestoreSnapshot(snap);
        Assert.Equal(1, report.Applied);

        using var read = _dbf.Create();
        var rows = read.SafeEntries.ToList();
        Assert.Equal(2, rows.Count);                                      // ردیفِ خودمان نرفت
        Assert.Contains(rows, r => r.Title == "از سرور" && r.Amount == 250m);
        Assert.Contains(rows, r => r.Id == mine);
        Assert.Equal(4242L, _store.State().Cursor);
    }

    // ── بارِ اول ──────────────────────────────────────────────────────

    /// <summary>
    /// ⛔ ردیف‌هایی که <b>پیش از</b> این نسخه ساخته شده‌اند هم باید یک بار
    /// به دفتر بروند. بی این، گوشیِ تازه دفترِ خالی می‌دید.
    /// </summary>
    [Fact]
    public void Bare_Aval_Radifhaye_Ghadimi_Ra_Be_Daftar_Mibarad()
    {
        //  دفتر خاموش ⇒ ردیف‌هایی که هیچ opی ندارند، مثلِ دیتابیسِ امروزِ مشتری
        OpLog.Enabled = false;
        try
        {
            using var db = _dbf.Create();
            for (var i = 0; i < 12; i++)
                db.SafeEntries.Add(new SafeEntry
                {
                    Title = "کهنه " + i, Amount = i, DateKey = 14050700 + i, MonthKey = "1405/07",
                    SyncUid = "old-" + i,
                });
            db.SaveChanges();
        }
        finally { OpLog.Enabled = true; }

        Assert.Equal(0, _store.Pending());

        //  چند دور، تا تمام شود
        for (var i = 0; i < 200 && !_store.SeedStep().Done; i++) { }

        Assert.True(_store.State().SeededAt > 0);
        Assert.Equal(12, _store.Pending());

        using var read = _dbf.Create();
        var ops = read.SyncOps.Where(x => x.TableName == nameof(SafeEntry)).ToList();
        Assert.All(ops, o => Assert.Equal("insert", o.OpType));
        Assert.Equal(12, ops.Select(o => o.RowUid).Distinct().Count());
    }

    // ── پشتیبانِ رمزشده ───────────────────────────────────────────────

    /// <summary>
    /// ⛔ فایلِ دست‌خورده بی‌صدا باز نمی‌شود — برچسبِ AES-GCM خطا می‌دهد.
    /// پشتیبانی که نصفه باشد بدتر از نبودنش است.
    /// </summary>
    [Fact]
    public void Poshtibane_Ramzshode_Baz_Mishavad_Va_Daste_Khorde_Na()
    {
        AddRow("چیزی برای پشتیبان");
        var path = SyncBackup.Write(_dbf, label: "azmoon");
        Assert.NotNull(path);
        Assert.True(File.Exists(path));

        var back = Path.Combine(Path.GetTempPath(), $"pump-restore-{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(SyncBackup.Read(path!, back));
            Assert.True(new FileInfo(back).Length > 0);

            //  یک بایت را خراب می‌کنیم
            var bytes = File.ReadAllBytes(path!);
            bytes[^1] ^= 0xFF;
            var broken = path! + ".broken";
            File.WriteAllBytes(broken, bytes);
            Assert.False(SyncBackup.Read(broken, back));
            try { File.Delete(broken); } catch { }
        }
        finally
        {
            try { File.Delete(back); } catch { }
            try { File.Delete(path!); } catch { }
        }
    }
}
