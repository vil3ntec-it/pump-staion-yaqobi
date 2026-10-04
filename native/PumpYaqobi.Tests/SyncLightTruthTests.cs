using System.Net;
using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — چراغِ همگام‌سازی دربارهٔ <b>حالا</b> حرف می‌زند، با موتورِ واقعی ══
///
/// همان زنجیرهٔ ۱۴۰۵/۰۷/۱۱ (یک نرسیدنِ گذرا ⇒ صفِ خالی ⇒ چراغِ سرخِ جاویدان)
/// که تا امروز فقط با جای چند رشته در <c>SyncEngine.cs</c> قفل بود. این‌جا یک
/// <see cref="SyncEngine"/>ِ واقعی روی دفترِ موقت، با سرورِ ساختگیِ
/// درون‌فرآیندی، دو دور می‌دود و خودِ چراغ خوانده می‌شود.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SyncLightTruthTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pump-synclight-{Guid.NewGuid():N}");
    private readonly string? _was = AppSettings.DirOverride;

    public SyncLightTruthTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        AppSettings.DirOverride = _was;
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private (AppHost Host, SyncEngine Engine) Bound()
    {
        var host = new AppHost(Path.Combine(_dir, "pump.db"));
        var f = AppSettings.Load();
        f.CloudDeviceToken = "pd_sync";
        f.CloudUserId = "u-1";
        f.Save();
        return (host, new SyncEngine(host) { RunWhenDisabled = true });
    }

    /// <summary>
    /// ⛔ <b>گرفتنِ موفق، خطای دورِ قبل را پاک می‌کند</b> — حتی وقتی صف خالی
    /// است و بلوکِ فرستادن اصلاً نمی‌دود.
    /// </summary>
    [Fact]
    public async Task YekNaresidan_BadGereftaneMovaffagh_CheraghSabzMishavad()
    {
        var reach = false;
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/api/sync/v1/pull")
                return Task.FromResult(reach
                    ? Json(HttpStatusCode.OK, """{"ops":[],"cursor":0,"has_more":false}""")
                    : Json(HttpStatusCode.ServiceUnavailable,
                        """{"error":{"code":"account_server_down","message":"سرورِ حساب روشن نیست"}}"""));
            return Task.FromResult(Json(HttpStatusCode.NotFound, "{}"));
        };
        var (_, engine) = Bound();

        //  دورِ «بارِ اول» دفترِ خالی را آماده می‌کند؛ تا به گرفتن برسد چند دور
        var trail = new List<string>();
        for (var i = 0; i < 100 && engine.LastError.Length == 0; i++) { await engine.SyncNowAsync(); trail.Add(engine.Light + ":" + engine.Reason); }
        Assert.True(engine.LastError.Length > 0, string.Join(" | ", trail));
        Assert.NotEqual(SyncLight.Synced, engine.Light);
        Assert.Equal(0, engine.Queued);                       // ⚠️ صف خالی — همان حالِ باگ

        reach = true;
        var ok = await engine.SyncNowAsync();

        Assert.True(ok, engine.Reason);
        Assert.Equal(SyncLight.Synced, engine.Light);
        Assert.Equal("", engine.LastError);
    }

    /// <summary>
    /// ⛔ ولی خطای <b>واقعیِ همین دور</b> (opی که رسید و ننشست) پاک نمی‌شود —
    /// چراغ سبزِ دروغ نمی‌دهد.
    /// </summary>
    [Fact]
    public async Task OpeNaneshasteh_HamanDor_CheraghRaSabzNemikonad()
    {
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/api/sync/v1/pull")
                return Task.FromResult(Json(HttpStatusCode.OK, """
                    {"ops":[{"op_id":"op-c","table":"DebtAccount","row_id":"uid-acct","type":"insert",
                             "fields":{"Name":"فرزند","MainOfDebtorId":1,"MainOfDebtorId@":"uid-nabude"},
                             "server_seq":5}],
                     "cursor":5,"has_more":false}
                    """));
            return Task.FromResult(Json(HttpStatusCode.NotFound, "{}"));
        };
        var (_, engine) = Bound();

        var trail = new List<string>();
        for (var i = 0; i < 100 && engine.LastError.Length == 0; i++) { await engine.SyncNowAsync(); trail.Add(engine.Light + ":" + engine.Reason); }

        Assert.True(engine.LastError.Contains("ننشست"), engine.LastError + " || " + string.Join(" | ", trail));
        Assert.Equal(SyncLight.Failed, engine.Light);
    }
}
