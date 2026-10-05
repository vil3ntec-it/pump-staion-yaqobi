using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;
using Xunit.Abstractions;

namespace PumpYaqobi.Tests;

[Collection(AppHostCollection.Name)]
public class TwoComputersSyncTests : IDisposable
{
    private readonly ITestOutputHelper _o;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pump-2pc-{Guid.NewGuid():N}");
    private readonly string? _was = AppSettings.DirOverride;
    public TwoComputersSyncTests(ITestOutputHelper o) { _o = o; Directory.CreateDirectory(_dir); }
    public void Dispose()
    {
        CloudLink.TestTransport = null; OpLog.Enabled = true;
        AppSettings.DirOverride = _was; SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    // ---------- fake server, same semantics as shop/server/src/lib/sync-v1.js push/pull ----------
    private sealed record Op(long Seq, string Device, string OpId, string Table, string Row, string Type, JsonNode? Fields);
    private readonly List<Op> _log = new();
    private static HttpResponseMessage J(object o) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json") };

    private Task<HttpResponseMessage> Server(HttpRequestMessage req, CancellationToken ct)
    {
        var path = req.RequestUri!.AbsolutePath;
        var q = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
        if (path == "/api/sync/v1/push")
        {
            var body = JsonNode.Parse(req.Content!.ReadAsStringAsync().Result)!;
            var dev = body["device_id"]!.GetValue<string>();
            var results = new List<object>();
            foreach (var o in body["ops"]!.AsArray())
            {
                var id = o!["op_id"]!.GetValue<string>();
                var seq = _log.Count + 1;
                _log.Add(new Op(seq, dev, id, o["table"]!.GetValue<string>(), o["row_id"]!.GetValue<string>(),
                    o["type"]!.GetValue<string>(), o["fields"]?.DeepClone()));
                results.Add(new { op_id = id, status = "applied", server_seq = seq });
            }
            return Task.FromResult(J(new { ok = true, results, applied = results.Count, cursor = _log.Count, schema_version = 1 }));
        }
        if (path == "/api/sync/v1/pull")
        {
            var since = long.Parse(q["since"] ?? "0"); var dev = q["device_id"]!; var lim = int.Parse(q["limit"] ?? "500");
            var rows = _log.Where(x => x.Seq > since && x.Device != dev).Take(lim + 1).ToList();
            var more = rows.Count > lim; var page = more ? rows.Take(lim).ToList() : rows;
            var cursor = more ? page[^1].Seq : Math.Max(_log.Count, since);
            return Task.FromResult(J(new
            {
                ok = true, cursor, has_more = more,
                ops = page.Select(x => new { op_id = x.OpId, server_seq = x.Seq, table = x.Table, row_id = x.Row, type = x.Type, fields = x.Fields }),
            }));
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
    }

    private (AppHost H, SyncEngine E) Pc(string name, string token)
    {
        var d = Path.Combine(_dir, name); Directory.CreateDirectory(d);
        AppSettings.DirOverride = d;
        var host = new AppHost(Path.Combine(d, "pump.db"));
        var f = AppSettings.Load(); f.CloudDeviceToken = token; f.CloudUserId = "u-1"; f.Save();
        return (host, new SyncEngine(host) { RunWhenDisabled = true });
    }

    private static async Task Drain(SyncEngine e, AppHost h, int max = 400)
    {
        for (var i = 0; i < max; i++)
        {
            await e.SyncNowAsync();
            var s = new SyncStore(h.Db).State();
            if (s.SeededAt > 0 && e.Queued == 0 && s.PrimedAt > 0) break;
        }
    }

    /// Scenario 1: rows that existed before sync (no op) and were EDITED before seeding.
    [Fact]
    public async Task S1_EditedBeforeSeed_RowNeverFullySent()
    {
        CloudLink.TestTransport = Server;
        var (ha, ea) = Pc("a", "pd_aaaaaaaaaaaaaaaaaaaa");
        long debtorA;
        //  pre-sync rows: no ops
        OpLog.Enabled = false;
        using (var db = ha.Db.Create())
        {
            var d = new Debtor { Name = "Karim", LegacyId = "k1" };
            for (var i = 0; i < 30; i++) d.MainAccount.FuelRows.Add(new DebtRow { Name = "r" + i, DateShamsi = "1405/06/10", Liters = 1 });
            db.Debtors.Add(d); db.SaveChanges(); debtorA = d.Id;
        }
        OpLog.Enabled = true;
        //  one normal edit before the first sync round (e.g. header stamp / any edit while signed-out)
        using (var db = ha.Db.Create())
        {
            var acc = db.DebtAccounts.Single();
            acc.Note = "edited";
            db.SaveChanges();
        }
        await Drain(ea, ha);
        _o.WriteLine($"A pushed {_log.Count} ops: " + string.Join(",", _log.GroupBy(x => x.Table + ":" + x.Type).Select(g => g.Key + "=" + g.Count())));
        foreach (var x in _log.Where(x => x.Table == "DebtAccount")) _o.WriteLine($"  DebtAccount op {x.Type}: {x.Fields}");

        var (hb, eb) = Pc("b", "pd_bbbbbbbbbbbbbbbbbbbb");
        await Drain(eb, hb);
        using var r = hb.Db.Create();
        var debtor = r.Debtors.Single();
        var main = r.DebtAccounts.IgnoreQueryFilters().Where(x => x.MainOfDebtorId == debtor.Id).ToList();
        var allAcc = r.DebtAccounts.IgnoreQueryFilters().ToList();
        var rows = r.DebtRows.IgnoreQueryFilters().Count();
        _o.WriteLine($"B: debtors={r.Debtors.Count()} accounts={allAcc.Count} mainOfDebtor={main.Count} rows={rows} lastErr='{eb.LastError}'");
        foreach (var a in allAcc) _o.WriteLine($"  B acct id={a.Id} MainOf={a.MainOfDebtorId} DebtorId={a.DebtorId} Name='{a.Name}' Note='{a.Note}'");
        Assert.Single(main); // expected to FAIL if bug
    }

    /// Scenario 2: server order child-before-parent (pre-Rank seeds / pre-seed queued ops) separated by > 10,000 ops.
    [Fact]
    public async Task S2_ParentFarAfterChild_DroppedAfterDeferMaxTries()
    {
        CloudLink.TestTransport = Server;
        //  build real ops on a scratch ledger to get correct field shapes
        var (ha, ea) = Pc("a", "pd_aaaaaaaaaaaaaaaaaaaa");
        using (var db = ha.Db.Create())
        {
            var d = new Debtor { Name = "Karim", LegacyId = "k1" };
            for (var i = 0; i < 5; i++) d.MainAccount.FuelRows.Add(new DebtRow { Name = "r" + i, DateShamsi = "1405/06/10", Liters = 1 });
            db.Debtors.Add(d); db.SaveChanges();
        }
        var ops = new SyncStore(ha.Db).Take();
        Op Mk(SyncOp o, long seq) => new(seq, "A", o.OpId, o.TableName, o.RowUid, o.OpType, JsonNode.Parse(o.FieldsJson));
        //  alphabetical (pre-1405/07/14) order: DebtAccount, DebtRow ... filler ... Debtor
        foreach (var o in ops.Where(x => x.TableName != nameof(Debtor))) _log.Add(Mk(o, _log.Count + 1));
        for (var i = 0; i < 11_000; i++)
            _log.Add(new Op(_log.Count + 1, "A", "f" + i, "Expense", "fx" + i, "insert", JsonNode.Parse("{\"Title\":\"x\",\"DateShamsi\":\"1405/01/01\"}")));
        foreach (var o in ops.Where(x => x.TableName == nameof(Debtor))) _log.Add(Mk(o, _log.Count + 1));

        var (hb, eb) = Pc("b", "pd_bbbbbbbbbbbbbbbbbbbb");
        await Drain(eb, hb, 2000);
        using var r = hb.Db.Create();
        _o.WriteLine($"B: debtors={r.Debtors.Count()} accounts={r.DebtAccounts.IgnoreQueryFilters().Count()} rows={r.DebtRows.IgnoreQueryFilters().Count()} expenses={r.Expenses.IgnoreQueryFilters().Count()} cursor={new SyncStore(hb.Db).State().Cursor} err='{eb.LastError}'");
        Assert.Equal(5, r.DebtRows.IgnoreQueryFilters().Count()); // expected FAIL if bug
    }

    [Fact]
    public async Task S3_CurrentVersion_SignedOutThenSignIn() => await S3(true);
    [Fact] public async Task S3b_NoUpdate() => await S3(false);
    private async Task S3(bool upd)
    {
        CloudLink.TestTransport = Server;
        var d0 = Path.Combine(_dir, "a"); Directory.CreateDirectory(d0); AppSettings.DirOverride = d0;
        var ha = new AppHost(Path.Combine(d0, "pump.db"));
        var ea = new SyncEngine(ha) { RunWhenDisabled = true };
        using (var db = ha.Db.Create())
        {
            var d = new Debtor { Name = "Karim", LegacyId = "k1" };
            db.Debtors.Add(d); db.SaveChanges();
        }
        using (var db = ha.Db.Create())
        {
            var acc = db.DebtAccounts.Single();
            for (var i = 0; i < 20; i++) db.DebtRows.Add(new DebtRow { FuelAccountId = acc.Id, Name = "r" + i, DateShamsi = "1405/06/10", Liters = 1 });
            if (upd) acc.ReceiptsMigrated = true;
            db.SaveChanges();
        }
        await ea.SyncNowAsync(); // not signed in: idle
        var f = AppSettings.Load(); f.CloudDeviceToken = "pd_aaaaaaaaaaaaaaaaaaaa"; f.CloudUserId = "u-1"; f.Save();
        await Drain(ea, ha);
        _o.WriteLine($"A pushed {_log.Count}: " + string.Join(",", _log.GroupBy(x => x.Table + ":" + x.Type).Select(g => g.Key + "=" + g.Count())));
        var (hb, eb) = Pc("b", "pd_bbbbbbbbbbbbbbbbbbbb");
        await Drain(eb, hb);
        using var r = hb.Db.Create();
        foreach (var x in _log.Where(x=>x.Table!="DebtRow")) _o.WriteLine($"  srv {x.Seq} {x.Table} {x.Type} {x.Row} {x.Fields?.ToJsonString()[..Math.Min(300, x.Fields?.ToJsonString().Length ?? 0)]}");
        _o.WriteLine($"B debtors={r.Debtors.IgnoreQueryFilters().Count()} accts={r.DebtAccounts.IgnoreQueryFilters().Count()} rows={r.DebtRows.IgnoreQueryFilters().Count()} err='{eb.LastError}' light={eb.Light} reason={eb.Reason}");
        foreach (var a in r.DebtAccounts.IgnoreQueryFilters().ToList()) _o.WriteLine($"  B acct {a.Id} mainOf={a.MainOfDebtorId} del={a.DeletedAt}");
        var debtor = r.Debtors.Single();
        var acct = r.DebtAccounts.Single(x => x.MainOfDebtorId == debtor.Id);
        _o.WriteLine($"B rows={r.DebtRows.Count(x => x.FuelAccountId == acct.Id)} ledger={hb.Db.DbPath}");
        Assert.Equal(20, r.DebtRows.Count(x => x.FuelAccountId == acct.Id));
    }

    /// ⛔ بذرِ ترمیم (۱۴۰۵/۰۷/۲۰): سرور opهای خرابِ نسخهٔ پیشین را دارد (پیوندِ پدر نیست،
    /// کلیدِ موقتِ منفی). کامپیوترِ منبع که به‌روز شد، همه را یک بار کامل می‌فرستد؛
    /// کامپیوترِ تازه دادهٔ درست می‌گیرد — و آن‌چه خودش از سرور گرفته را پس نمی‌فرستد.
    [Fact]
    public async Task S4_RepairSeed_FixesServerDataFromOlderVersion()
    {
        CloudLink.TestTransport = Server;
        var (ha, ea) = Pc("a", "pd_aaaaaaaaaaaaaaaaaaaa");
        using (var db = ha.Db.Create())
        {
            var d = new Debtor { Name = "Karim", LegacyId = "k1" };
            for (var i = 0; i < 12; i++) d.MainAccount.FuelRows.Add(new DebtRow { Name = "r" + i, DateShamsi = "1405/06/10", Liters = 1 });
            db.Debtors.Add(d); db.SaveChanges();
        }
        await Drain(ea, ha);

        //  همان خرابیِ نسخهٔ پیشین روی سرور: حساب بی پیوندِ پدر و با کلیدِ موقتِ منفی
        for (var i = 0; i < _log.Count; i++)
        {
            var x = _log[i];
            if (x.Table != nameof(DebtAccount) || x.Fields is not JsonObject f) continue;
            var g = (JsonObject)f.DeepClone();
            g.Remove("MainOfDebtorId@");
            g["MainOfDebtorId"] = -9223372036854774806;
            _log[i] = x with { Fields = g };
        }

        //  کامپیوترِ تازه پیش از ترمیم: همان علامتِ گزارشِ صاحب ریپو
        var (hb, eb) = Pc("b", "pd_bbbbbbbbbbbbbbbbbbbb");
        await Drain(eb, hb);
        using (var r0 = hb.Db.Create())
        {
            var dbt = r0.Debtors.Single();
            Assert.False(r0.DebtAccounts.Any(x => x.MainOfDebtorId == dbt.Id), "پیش از ترمیم باید خراب باشد (وگرنه آزمون چیزی نمی‌سنجد)");
        }

        //  منبع به نسخهٔ تازه می‌رود ⇒ یک بار بذرِ ترمیم
        AppSettings.DirOverride = Path.Combine(_dir, "a");
        new SyncStore(ha.Db).Update(x => x.RepairSeed = 0);
        var before = _log.Count;
        await Drain(ea, ha);
        Assert.True(_log.Count > before, "ترمیم باید دوباره بفرستد");
        Assert.Contains(_log.Skip(before), x => x.Table == nameof(DebtAccount) && x.Fields?["MainOfDebtorId@"] is not null);

        //  کامپیوترِ تازه ترمیم را می‌گیرد؛ ترمیمِ خودش هیچ قرض‌داری را پس نمی‌فرستد
        AppSettings.DirOverride = Path.Combine(_dir, "b");
        var mark = _log.Count;
        await Drain(eb, hb);
        Assert.DoesNotContain(_log.Skip(mark), x => x.Device.Contains("bbbb") || x.Table == nameof(Debtor));
        using var r = hb.Db.Create();
        var debtor = r.Debtors.Single();
        var acct = r.DebtAccounts.Single(x => x.MainOfDebtorId == debtor.Id);
        Assert.Equal(12, r.DebtRows.Count(x => x.FuelAccountId == acct.Id));
    }
}
