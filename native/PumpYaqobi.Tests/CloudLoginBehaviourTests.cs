using System.Net;
using System.Text;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — ورود، مجوز و کیو‌آر با <b>رفتار</b> ═══════════════════════════
///
/// چهار قاعده که تا امروز فقط با گشتنِ رشته در سورس قفل بودند: رمزِ حساب روی
/// دیسک نمی‌نشیند، ۴۲۹ جملهٔ آدمیزاد می‌دهد، مُهرِ «از آینده» تازه‌سازیِ مجوز را
/// خاموش نمی‌کند، و کیو‌آرِ زنده کدِ پمپِ <b>همان حساب</b> را می‌برد.
/// </summary>
[Collection(AppHostCollection.Name)]
public class CloudLoginBehaviourTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-clb-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _was = AppSettings.DirOverride;
    private readonly List<string> _hits = new();

    public CloudLoginBehaviourTests()
    {
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        CloudLink.ResetReach();
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        AppSettings.DirOverride = _was;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private void Serve(Func<string, HttpResponseMessage> handler) =>
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            lock (_hits) _hits.Add(path);
            return Task.FromResult(handler(path));
        };

    private static CloudLink Link(Action<AppSettings> seed)
    {
        var f = AppSettings.Load();
        seed(f);
        f.Save();
        return new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>⛔ رمزِ حساب در هیچ فایلی از پوشهٔ داده نمی‌نشیند — نه تنظیمات، نه هیچ چیزِ دیگر.</summary>
    [Fact]
    public async Task RamzeHesab_RooyeDisk_NemiNeshinad()
    {
        const string pass = "Zx-ramz-7731-yekta";
        Serve(path => path == "/api/auth/login"
            ? Json(HttpStatusCode.OK, "{\"accessToken\":\"acc-1\",\"refreshToken\":\"ref-1\",\"accessExpiresAt\":"
                + (Now + 3_600_000) + ",\"user\":{\"id\":\"u-1\",\"email\":\"a@x.com\",\"name\":\"a\"}}")
            : Json(HttpStatusCode.NotFound, "{}"));
        var link = Link(_ => { });

        var r = await link.SignInWithPasswordAsync("a@x.com", pass);
        Assert.True(r.Ok, r.Why);
        Assert.Contains("/api/auth/login", _hits);
        Assert.Equal("acc-1", AppSettings.Load().CloudAccountToken);   // ورود واقعاً نشست

        foreach (var file in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
            Assert.DoesNotContain(pass, File.ReadAllText(file));
    }

    /// <summary>⛔ ۴۲۹ِ سرور ⇒ «تلاشِ زیاد — چند دقیقه صبر کنید»، نه «رمز غلط».</summary>
    [Fact]
    public async Task ChaharSadBistONoh_PayameAdamizad()
    {
        Serve(_ => Json((HttpStatusCode)429, """{"error":{"code":"rate_limited","message":"Too many"}}"""));
        var link = Link(_ => { });

        var r = await link.SignInWithPasswordAsync("a@x.com", "ramz-1234");

        Assert.False(r.Ok);
        Assert.Contains("تلاشِ زیاد", r.Why);
        Assert.DoesNotContain("Too many", r.Why);
    }

    /// <summary>
    /// ⛔ مُهرِ «آخرین تازه‌سازی» از آینده (ساعتِ ویندوز روزی جلو بود) یعنی
    /// «همین حالا»، نه «هنوز نرسیده» — و مُهرِ تازهٔ واقعی هیچ درخواستی نمی‌زند.
    /// </summary>
    [Fact]
    public async Task MohreAyande_TazeSaziyeMajvoz_Ra_KhamushNemikonad()
    {
        Serve(_ => Json(HttpStatusCode.ServiceUnavailable, "{}"));

        var fresh = Link(s => { s.CloudDeviceToken = "pd_1"; s.CloudSyncedAt = Now - 60_000; });
        await fresh.KeepLicenseFreshAsync();
        Assert.Empty(_hits);

        var future = Link(s => { s.CloudDeviceToken = "pd_1"; s.CloudSyncedAt = Now + 3 * 86_400_000L; });
        await future.KeepLicenseFreshAsync();
        Assert.NotEmpty(_hits);
    }

    /// <summary>
    /// ⛔ کیو‌آرِ زندهٔ حسابِ قرض‌دار کدِ پمپِ <b>همان حساب</b> (از سرورِ حساب)
    /// را می‌برد، نه کدی که روی این کامپیوتر نوشته شده.
    /// </summary>
    [Fact]
    public async Task KiuAreZende_KodePompeHamanHesab_RaMibarad()
    {
        var host = new AppHost(Path.Combine(_dir, "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        var d = await host.Debtors.AddDebtorAsync("کریم", "", false);
        var acct = (await host.Debtors.LoadFullAsync(d.Id))!.AllAccounts().First();

        var f = AppSettings.Load();
        f.StationCode = "mahalli-1";         //  پوشهٔ سرورِ خانگی، با رمزش
        f.ServerToken = "tok-1";
        f.CloudStationCode = "Stn-Hesab-42";
        f.Save();

        var frag = await AcctLive.EnsureAsync(host, acct);
        Assert.StartsWith("s=stn-hesab-42&", frag);
        Assert.DoesNotContain("mahalli", frag);

        //  و شرکتِ تیل هم همان
        var co = await host.Companies.AddAsync("شرکتِ الف");
        var coFrag = await AcctLive.EnsureAsync(host, co);
        Assert.StartsWith("s=stn-hesab-42&", coFrag);
    }
}
