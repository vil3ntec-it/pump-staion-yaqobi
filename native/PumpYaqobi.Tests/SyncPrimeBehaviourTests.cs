using System.Net;
using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — پردهٔ «آوردنِ اطلاعاتِ حساب» با <b>موتورِ واقعی</b> ══════════
///
/// سه قاعدهٔ ۱۴۰۵/۰۷/۱۱ و ۰۷/۱۴ که تا امروز فقط با جای چند رشته در
/// <c>SyncEngine.cs</c> قفل بودند: پرده دیوار نیست (هر شکست می‌بردش و دیگر
/// برنمی‌گردد)، پرده تا پایانِ <b>همهٔ</b> صفحه‌های گرفتن می‌ماند و بعد مهرِ
/// <c>PrimedAt</c> می‌خورد، و «ادامه در پس‌زمینه» خبرِ «رسید» را نمی‌بلعد.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SyncPrimeBehaviourTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pump-syncprime-{Guid.NewGuid():N}");
    private readonly string? _was = AppSettings.DirOverride;

    public SyncPrimeBehaviourTests() => Directory.CreateDirectory(_dir);

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

    private sealed record Run(AppHost Host, SyncEngine Engine, List<bool> Curtain, List<bool> Finished);

    private Run Bound()
    {
        var host = new AppHost(Path.Combine(_dir, "pump.db"));
        var f = AppSettings.Load();
        f.CloudDeviceToken = "pd_prime";
        f.CloudUserId = "u-1";
        f.Save();
        var engine = new SyncEngine(host) { RunWhenDisabled = true };
        var curtain = new List<bool>();
        var finished = new List<bool>();
        engine.Changed += () => { if (curtain.Count == 0 || curtain[^1] != engine.Priming) curtain.Add(engine.Priming); };
        engine.PrimeFinished += (ok, _, _) => finished.Add(ok);
        return new Run(host, engine, curtain, finished);
    }

    private static long PrimedAt(AppHost h) => new SyncStore(h.Db).State().PrimedAt;

    /// <summary>
    /// ⛔ <b>پرده دیوار نیست</b>: نرسیدن به سرور همان دور پرده را می‌برد، خبرِ
    /// «نرسید» یک بار می‌رود، و رسیدنِ بعدی در همین اجرا پرده را برنمی‌گرداند.
    /// </summary>
    [Fact]
    public async Task NaresidaneSarvar_Parde_RaMibarad_VaDigarBarnemigardad()
    {
        var reach = false;
        CloudLink.TestTransport = (req, _) =>
            Task.FromResult(req.RequestUri!.AbsolutePath == "/api/sync/v1/pull"
                ? reach
                    ? Json(HttpStatusCode.OK, """{"ops":[],"cursor":0,"has_more":false}""")
                    : Json(HttpStatusCode.ServiceUnavailable,
                        """{"error":{"code":"account_server_down","message":"سرورِ حساب روشن نیست"}}""")
                : Json(HttpStatusCode.NotFound, "{}"));
        var r = Bound();

        for (var i = 0; i < 100 && r.Finished.Count == 0; i++) await r.Engine.SyncNowAsync();

        Assert.Equal(new[] { false }, r.Finished);              // یک خبر، «نرسید»
        Assert.Contains(true, r.Curtain);                        // پرده واقعاً آمده بود
        Assert.False(r.Engine.Priming);                          // و رفت
        Assert.Equal(0, PrimedAt(r.Host));                       // ولی «گرفتم» مهر نخورد

        reach = true;
        for (var i = 0; i < 5; i++) await r.Engine.SyncNowAsync();

        Assert.False(r.Engine.Priming);
        Assert.Equal(1, r.Curtain.Count(x => x));                // ⛔ یک بار در هر اجرا
        Assert.Single(r.Finished);
    }

    /// <summary>
    /// ⛔ <b>پرده تا پایانِ گرفتن می‌ماند، نه تا پایانِ صفحهٔ اول</b> — و فقط
    /// آن‌وقت <c>PrimedAt</c> مهر می‌خورد و «رسید» یک بار می‌رود.
    /// </summary>
    [Fact]
    public async Task Parde_TaAkharinSafhe_Mimanad_VaMohrMikhorad()
    {
        var page = 0;
        CloudLink.TestTransport = (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath != "/api/sync/v1/pull")
                return Task.FromResult(Json(HttpStatusCode.NotFound, "{}"));
            page++;
            return Task.FromResult(page < 3
                ? Json(HttpStatusCode.OK, $$"""{"ops":[],"cursor":{{page}},"has_more":true}""")
                : Json(HttpStatusCode.OK, """{"ops":[],"cursor":3,"has_more":false}"""));
        };
        var r = Bound();

        //  تا صفحهٔ دوم: پرده هنوز هست و هیچ خبری نرفته
        for (var i = 0; i < 100 && page < 2; i++) await r.Engine.SyncNowAsync();
        Assert.Equal(2, page);
        Assert.True(r.Engine.Priming, "پرده پس از صفحهٔ نخست رفت — همان باگِ tensync");
        Assert.Empty(r.Finished);
        Assert.Equal(0, PrimedAt(r.Host));

        for (var i = 0; i < 10 && r.Finished.Count == 0; i++) await r.Engine.SyncNowAsync();

        Assert.Equal(new[] { true }, r.Finished);
        Assert.False(r.Engine.Priming);
        Assert.True(PrimedAt(r.Host) > 0);

        //  و با گرفتنِ بعدی دوباره نمی‌آید
        for (var i = 0; i < 3; i++) await r.Engine.SyncNowAsync();
        Assert.Equal(1, r.Curtain.Count(x => x));
        Assert.Single(r.Finished);
    }

    /// <summary>
    /// ⛔ «ادامه در پس‌زمینه» فقط پرده را می‌برد: گرفتن ادامه می‌یابد و خبرِ
    /// «رسید» (که بخشِ جلوی چشم را از نو می‌خواند) باز هم یک بار می‌رود.
    /// </summary>
    [Fact]
    public async Task AdameDarPasZamine_KhabareResid_RaNemibalad()
    {
        var page = 0;
        CloudLink.TestTransport = (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath != "/api/sync/v1/pull")
                return Task.FromResult(Json(HttpStatusCode.NotFound, "{}"));
            page++;
            return Task.FromResult(page < 2
                ? Json(HttpStatusCode.OK, """{"ops":[],"cursor":1,"has_more":true}""")
                : Json(HttpStatusCode.OK, """{"ops":[],"cursor":2,"has_more":false}"""));
        };
        var r = Bound();

        for (var i = 0; i < 100 && page < 1; i++) await r.Engine.SyncNowAsync();
        Assert.True(r.Engine.Priming);

        r.Engine.DismissPrime();
        Assert.False(r.Engine.Priming);

        for (var i = 0; i < 10 && r.Finished.Count == 0; i++) await r.Engine.SyncNowAsync();

        Assert.Equal(new[] { true }, r.Finished);
        Assert.False(r.Engine.Priming);                          // و پرده هرگز برنگشت
        Assert.Equal(1, r.Curtain.Count(x => x));
        Assert.True(PrimedAt(r.Host) > 0);
    }
}
