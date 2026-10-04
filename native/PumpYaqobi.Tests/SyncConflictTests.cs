using System.Text.Json;
using Microsoft.Data.Sqlite;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ب۱ — تعارضِ دو کامپیوتر دیده شود، نه بی‌صدا ═════════════════════
///
/// دو دفترِ واقعیِ SQLite و یک «سرور»ِ ساختگی با همان قاعدهٔ سرورِ حساب
/// (‎server_seq‎ِ افزایشی، pull فقط opهای دستگاه‌های دیگر، آخرین op برنده).
/// هر دو یک خانه را عوض می‌کنند ⇒ یکی تعارض می‌بیند، هر دو به <b>یک</b> عدد
/// می‌رسند، هیچ مقداری بی ردپا گم نمی‌شود، و «برگردان» روی هر دو همگام می‌شود.
/// </summary>
[Collection(OpLogCollection.Name)]
public class SyncConflictTests : IDisposable
{
    private readonly List<string> _files = new();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in _files.SelectMany(f => new[] { f, f + "-wal", f + "-shm" }))
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private sealed class FakeServer
    {
        public readonly List<(long Seq, string Device, SyncOp Op)> Log = new();
        public Dictionary<string, long> Push(string device, IReadOnlyList<SyncOp> ops)
        {
            var seqs = new Dictionary<string, long>();
            foreach (var o in ops) { var s = Log.Count + 1; Log.Add((s, device, o)); seqs[o.OpId] = s; }
            return seqs;
        }
        public List<IncomingOp> Pull(string device, long cursor) =>
            Log.Where(x => x.Seq > cursor && x.Device != device).Select(x =>
            {
                using var d = JsonDocument.Parse(x.Op.FieldsJson);
                return new IncomingOp(x.Op.OpId, x.Op.TableName, x.Op.RowUid, x.Op.OpType, d.RootElement.Clone(), x.Seq);
            }).ToList();
    }

    private sealed class Pc
    {
        public readonly PumpDbFactory Db;
        public readonly SyncStore Store;
        public readonly string Name;
        public long Cursor;
        public Pc(string file, string name) { Db = new PumpDbFactory(file); Db.EnsureReady(); Store = new SyncStore(Db); Name = name; }

        /// <summary>یک دورِ موتور: اول فرستادن، بعد گرفتن — همان ترتیبِ ‎SyncEngine‎.</summary>
        public void Step(FakeServer s, bool push = true)
        {
            if (push)
            {
                var batch = Store.Take();
                if (batch.Count > 0)
                {
                    var seqs = s.Push(Name, batch);
                    Store.MarkResults(batch.ToDictionary(b => b.OpId, _ => "applied"));
                    Store.NotePushed(batch, seqs);
                }
            }
            var got = s.Pull(Name, Cursor);
            if (got.Count > 0) { Store.ApplyIncoming(got); Cursor = got.Max(g => g.ServerSeq); }
            Store.ClearPushed();
        }

        public SafeEntry Row(string uid)
        {
            using var db = Db.Create();
            return db.SafeEntries.Single(x => x.SyncUid == uid);
        }

        public void Edit(string uid, Action<SafeEntry> a)
        {
            using var db = Db.Create();
            var r = db.SafeEntries.Single(x => x.SyncUid == uid);
            a(r);
            db.SaveChanges();
        }
    }

    private Pc NewPc(string name)
    {
        var f = Path.Combine(Path.GetTempPath(), $"pump-conflict-{name}-{Guid.NewGuid():N}.db");
        _files.Add(f);
        return new Pc(f, name);
    }

    private static string Seed(Pc a)
    {
        using var db = a.Db.Create();
        var row = new SafeEntry { Title = "اول", Amount = 100m, DateKey = 14050701, MonthKey = "1405/07" };
        db.SafeEntries.Add(row);
        db.SaveChanges();
        return row.SyncUid!;
    }

    /// <summary>
    /// ⛔ هر دو آفلاین «مبلغ» را عوض کردند. فرستادن پیش از گرفتن است، پس پیش از
    /// این opِ <b>کهنه‌ترِ</b> الف روی مقدارِ تازه‌ترِ ب می‌نشست و دو کامپیوتر
    /// برای همیشه دو عدد داشتند.
    /// </summary>
    [Fact]
    public void DoKampyuter_YekKhane_HarDoYekAdad_VaRadpaMimanad()
    {
        var server = new FakeServer();
        var a = NewPc("A"); var b = NewPc("B");
        var uid = Seed(a);
        a.Step(server); b.Step(server);
        Assert.Equal(100m, b.Row(uid).Amount);

        a.Edit(uid, r => r.Amount = 900m);
        b.Edit(uid, r => r.Amount = 700m);

        a.Step(server);          // الف می‌فرستد (۹۰۰)
        b.Step(server);          // ب می‌فرستد (۷۰۰، تازه‌تر) و بعد ۹۰۰ِ کهنه‌ترِ الف را می‌گیرد
        a.Step(server);          // الف ۷۰۰ را می‌گیرد

        Assert.Equal(700m, a.Row(uid).Amount);
        Assert.Equal(700m, b.Row(uid).Amount);      // ⛔ یک عدد — همان که سرور دارد

        //  ⛔ ۹۰۰ گم نشد: ب ردپایش را دارد
        var c = Assert.Single(b.Store.OpenConflicts());
        Assert.Equal("Amount", c.Field);
        Assert.Equal("local", c.Winner);
        Assert.Contains("900", c.RemoteJson);

        //  «مقدارِ دیگر را برگردان» ⇒ یک opِ تازه، روی هر دو
        Assert.True(b.Store.RestoreConflict(c.Id));
        b.Step(server); a.Step(server);
        Assert.Equal(900m, a.Row(uid).Amount);
        Assert.Equal(900m, b.Row(uid).Amount);
        Assert.Empty(b.Store.OpenConflicts());
    }

    /// <summary>
    /// الف هنوز نفرستاده که op ب می‌رسد ⇒ رسیده می‌نشیند، فیلد از opِ الف برداشته
    /// می‌شود (پس دو کامپیوتر به یک عدد می‌رسند)، و مقدارِ الف در ردپا می‌ماند.
    /// فیلدِ دیگرِ همان op دست نمی‌خورد.
    /// </summary>
    [Fact]
    public void OpeNarafte_ResideMineshinad_MaledeMaDarRadpa()
    {
        var server = new FakeServer();
        var a = NewPc("A"); var b = NewPc("B");
        var uid = Seed(a);
        a.Step(server); b.Step(server);

        a.Edit(uid, r => { r.Title = "عنوانِ الف"; r.Amount = 250m; });
        b.Edit(uid, r => r.Title = "عنوانِ ب");
        b.Step(server);
        a.Step(server, push: false);                 // الف اول می‌گیرد (آفلاینِ فرستادن)

        Assert.Equal("عنوانِ ب", a.Row(uid).Title);
        var c = Assert.Single(a.Store.OpenConflicts());
        Assert.Equal("remote", c.Winner);
        Assert.Contains("عنوانِ الف", JsonDocument.Parse(c.LocalJson).RootElement.GetProperty("Title").GetString());

        a.Step(server); b.Step(server);
        Assert.Equal("عنوانِ ب", b.Row(uid).Title);   // ⛔ الف عنوانِ کهنه‌اش را دوباره نفرستاد
        Assert.Equal(250m, b.Row(uid).Amount);        // ولی مبلغش رفت

        Assert.True(a.Store.KeepConflict(c.Id));       // «همین بماند»: هیچ داده‌ای عوض نمی‌شود
        Assert.Equal("عنوانِ ب", a.Row(uid).Title);
        Assert.Empty(a.Store.OpenConflicts());
    }

    /// <summary>هم‌مقدار تعارض نیست؛ و دلتا (‎$inc‎) هرگز تعارض نیست.</summary>
    [Fact]
    public void HamMeghdar_VaDelta_TaarozNist()
    {
        var server = new FakeServer();
        var a = NewPc("A"); var b = NewPc("B");
        var uid = Seed(a);
        a.Step(server); b.Step(server);
        a.Edit(uid, r => r.Title = "یکی");
        b.Edit(uid, r => r.Title = "یکی");
        a.Step(server); b.Step(server); a.Step(server);
        Assert.Empty(a.Store.OpenConflicts());
        Assert.Empty(b.Store.OpenConflicts());
    }
}
