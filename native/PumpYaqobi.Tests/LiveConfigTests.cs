using System.Net;
using System.Net.Http;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «تنظیماتِ زنده» ═════════════════════════════════════════════════════════
///
/// صاحب ریپو (۱۴۰۵/۰۷/۱۷): «برنامه رو جوری کن که بعدن اگه قابلیتی خاستم بتونم
/// راحت روش اجرا کنم… لایف اپدیت باشه… روی سرور فشاری نیاد… هیچ منطقی رو دست
/// نزن و خراب نکن.»
///
/// آن‌چه قفل می‌شود: نسخه از همان پاسخِ <c>/rate</c> خوانده می‌شود؛ تا عوض نشده
/// <b>هیچ</b> درخواستی نمی‌رود؛ وقتی عوض شد یک بار و فقط با توکنِ همین دستگاه؛
/// مقدارها روی دیسک می‌مانند؛ فایلِ خراب یعنی «هیچ مقداری»، نه خرابی.
/// </summary>
//  ⚠️ `AppSettings.DirOverride` و `LiveConfig` استاتیک‌اند — همان مجموعه‌ای که بقیه دارند
[Collection(AppHostCollection.Name)]
public class LiveConfigTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    public LiveConfigTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-live-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        LiveConfig.ResetForTests();
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        LiveConfig.ResetForTests();
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static JsonElement J(string s) => JsonDocument.Parse(s).RootElement;

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public void BiNoskhe_HichChiziAvazNemishavad_VaPishfarzMimanad()
    {
        //  پاسخِ سرورِ حسابِ کهنه: کلیدِ liveConfig ندارد
        LiveConfig.NoteServerVersion(J("""{"ok":true,"cmd":null}"""));
        Assert.False(LiveConfig.NeedsFetch);
        Assert.Equal("پیش‌فرض", LiveConfig.GetString("any.key", "پیش‌فرض"));
        Assert.True(LiveConfig.GetBool("any.flag", true));
        Assert.Null(LiveConfig.GetDecimal("any.num"));
    }

    [Fact]
    public void Barge_Neshasteh_Mishavad_VaRuyeDisk_Mimanad()
    {
        var changed = 0;
        void On() => changed++;
        LiveConfig.Changed += On;
        try
        {
            Assert.True(LiveConfig.Apply(J("""{"version":"3.1","values":{"banner.text":"سلام","feature.x":true,"rate.step":"2.5","n":7}}""")));
            //  همان برگه دوباره ⇒ «عوض نشد» و هیچ خبری
            Assert.False(LiveConfig.Apply(J("""{"version":"3.1","values":{"banner.text":"سلام","feature.x":true,"rate.step":"2.5","n":7}}""")));
        }
        finally { LiveConfig.Changed -= On; }
        Assert.Equal(1, changed);
        Assert.Equal("سلام", LiveConfig.GetString("banner.text"));
        Assert.True(LiveConfig.GetBool("feature.x", false));
        Assert.Equal(2.5m, LiveConfig.GetDecimal("rate.step"));
        Assert.Equal(7m, LiveConfig.GetDecimal("n"));

        //  بسته و باز شدنِ برنامه ⇒ از دیسک
        LiveConfig.ResetForTests();
        Assert.Equal("3.1", LiveConfig.Version);
        Assert.Equal("سلام", LiveConfig.GetString("banner.text"));
    }

    [Fact]
    public void FileKharab_YaniHichMeghdari_NaKharabi()
    {
        File.WriteAllText(Path.Combine(_dir, "live-config.json"), "{ not json");
        LiveConfig.ResetForTests();
        Assert.Equal("", LiveConfig.Version);
        Assert.Equal("x", LiveConfig.GetString("banner.text", "x"));
    }

    [Fact]
    public async Task TaNoskheAvazNashode_HichDarkhastiNemiravad_VaBadYekBar()
    {
        var hits = new List<(string Path, string Auth)>();
        CloudLink.TestTransport = (req, _) =>
        {
            hits.Add((req.RequestUri!.AbsolutePath, req.Headers.Authorization?.ToString() ?? ""));
            return Task.FromResult(req.RequestUri!.AbsolutePath == "/api/pump/device/rate"
                ? Json("""{"ok":true,"cmd":null,"liveConfig":"4.2"}""")
                : Json("""{"ok":true,"version":"4.2","values":{"feature.x":true}}"""));
        };
        var cloud = new CloudLink(new AppSettings { CloudDeviceToken = "pd_test_device" }, () => Task.CompletedTask);

        //  هنوز نسخه‌ای گفته نشده ⇒ صفر درخواست
        Assert.False(await cloud.LiveConfigTickAsync());
        Assert.Empty(hits);

        //  پرسشِ دقیقه‌ایِ همیشگی نسخه را می‌آورد
        Assert.Null(await cloud.RateCommandAsync());
        Assert.True(LiveConfig.NeedsFetch);
        Assert.True(await cloud.LiveConfigTickAsync());
        Assert.Equal(("/api/pump/device/live-config"), hits[^1].Path);
        Assert.Contains("pd_test_device", hits[^1].Auth);
        Assert.True(LiveConfig.GetBool("feature.x", false));

        //  همان نسخه دوباره ⇒ دیگر درخواستی برای برگه نیست
        var before = hits.Count;
        Assert.Null(await cloud.RateCommandAsync());
        Assert.False(LiveConfig.NeedsFetch);
        Assert.False(await cloud.LiveConfigTickAsync());
        Assert.Equal(before + 1, hits.Count);   // ⇐ فقط همان پرسشِ /rate
    }

    [Fact]
    public async Task BiDastgah_HichDarkhastiNemiravad()
    {
        var n = 0;
        CloudLink.TestTransport = (_, _) => { n++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); };
        LiveConfig.NoteServerVersion(J("""{"liveConfig":"9.9"}"""));
        var cloud = new CloudLink(new AppSettings(), () => Task.CompletedTask);
        Assert.False(await cloud.LiveConfigTickAsync());
        Assert.Equal(0, n);
    }
}
