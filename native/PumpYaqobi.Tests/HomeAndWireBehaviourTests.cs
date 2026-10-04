using System.Net;
using System.Text;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — دو قاعدهٔ «داده به کجا می‌رود» با <b>رفتار</b> ══════════════
///
/// تا امروز هر دو فقط با گشتنِ رشته در سورس قفل بودند:
/// «به گوشی هرگز ‎127.0.0.1‎ داده نمی‌شود» (‎HomeReachTests‎، ۱۴۰۵/۰۷/۱۳) و
/// «بدنهٔ درخواستِ سرورِ حساب فارسی را ‎\uXXXX‎ نمی‌کند» (‎TenYearsTests‎،
/// ۱۴۰۵/۰۷/۱۴ — سه برابرِ حجم، و سقفِ دو مگابایتیِ سرور).
/// </summary>
[Collection(AppHostCollection.Name)]
public class HomeAndWireBehaviourTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-hwb-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _was = AppSettings.DirOverride;

    public HomeAndWireBehaviourTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        AppSettings.DirOverride = _was;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// ⛔ <b>سرور روی همین کامپیوتر است ⇒ کیو‌آرِ کارمند نشانیِ شبکه را می‌برد</b>،
    /// نه ‎127.0.0.1‎ — که روی گوشی یعنی «خودِ گوشی». و سروری که از اول نشانیِ
    /// شبکه دارد دست نمی‌خورد.
    /// </summary>
    [Fact]
    public void BeGooshi_Hargez127_DadeNemishavad()
    {
        var host = new AppHost(Path.Combine(_dir, "pump.db"));
        var f = AppSettings.Load();
        f.ServerUrl = "http://127.0.0.1:4700";
        f.ServerLanUrl = "http://192.168.1.5:4700";
        f.ServerReadKey = "rk-1";
        f.StationCode = "p1";
        f.Save();

        Assert.Equal("http://192.168.1.5:4700", HomeLink.ShareUrl(host));
        var qr = KarLink.Build(host);
        Assert.NotNull(qr);
        Assert.Contains("192.168.1.5", qr);
        Assert.DoesNotContain("127.0.0.1", qr);

        f = AppSettings.Load();
        f.ServerUrl = "http://192.168.1.9:4700";
        f.Save();
        Assert.Equal("http://192.168.1.9:4700", HomeLink.ShareUrl(host));
    }

    /// <summary>
    /// ⛔ <b>نامِ فارسی همان‌طور که هست می‌رود</b> (UTF-8)، نه ‎پ…‎ — و
    /// نامِ فیلدها همان camelCase.
    /// </summary>
    [Fact]
    public async Task BadaneyeDarkhast_FarsiRa_Farar_Nemidahad()
    {
        AppSettings.DirOverride = _dir;
        string? body = null;
        CloudLink.TestTransport = async (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/pump" && req.Method == HttpMethod.Post)
                body = Encoding.UTF8.GetString(await req.Content!.ReadAsByteArrayAsync());
            var json = req.RequestUri!.AbsolutePath == "/api/pump/me"
                ? """{"station":null}"""
                : """{"error":{"code":"x","message":"x"}}""";
            return new HttpResponseMessage(req.RequestUri!.AbsolutePath == "/api/pump/me"
                    ? HttpStatusCode.OK : HttpStatusCode.BadRequest)
                { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        };
        var f = AppSettings.Load();
        f.CloudAccountToken = "acc-1";
        f.CloudAccessExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;
        f.Save();
        var link = new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });

        await link.EnsureStationAsync("پمپِ کریم");

        Assert.NotNull(body);
        Assert.Contains("پمپِ کریم", body);
        Assert.DoesNotContain("\\u06", body);
        Assert.Contains("\"name\"", body);
    }
}
