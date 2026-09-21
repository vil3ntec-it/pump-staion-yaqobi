using System.Net;
using System.Net.Http;
using System.Text;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Update;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ به‌روزرسانی، این بار با **رفتار** ═══════════════════════════════════════
///
/// ⛔ تا امروز سی‌ویک آزمون دربارهٔ به‌روزرسانی داشتیم و **هیچ‌کدام** نه
/// <c>CheckAsync</c> را می‌دواند و نه <c>DownloadAsync</c> را: همه رشته‌های
/// سورس و خودِ رکورد را می‌سنجیدند. پس دقیقاً همان چیزی که کاربر می‌دید —
/// «دکمه کار نمی‌کند، بررسی نمی‌کند، دانلود هم نیست» — هیچ سنجه‌ای نداشت و
/// بی‌صدا از CI رد می‌شد. این فایل همان شکاف را می‌بندد.
///
/// ⚠️ هیچ درخواستِ واقعی‌ای زده نمی‌شود: <see cref="UpdateService.TestTransport"/>
/// همان الگوی <c>CloudLink.TestTransport</c> است.
///
/// ⚠️ <c>[Collection]</c> لازم است — <c>DownloadAsync</c> در
/// <c>AppSettings.Dir</c> می‌نویسد و این کلاس <c>DirOverride</c> را عوض
/// می‌کند (قاعدهٔ <c>SettingsCollectionRuleTests</c>).
/// </summary>
[Collection(AppHostCollection.Name)]
public class UpdateBehaviourTests : IDisposable
{
    private readonly string? _oldDir;
    private readonly string _dir;

    public UpdateBehaviourTests()
    {
        _oldDir = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-upd-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        AppBase.LocalIdOverride = "abc12345";
    }

    public void Dispose()
    {
        UpdateService.TestTransport = null;
        AppBase.LocalIdOverride = null;
        // ⚠️ مقدارِ **قبلی** برمی‌گردد، نه null — وگرنه آزمونِ بعدی به
        // تنظیماتِ واقعیِ خودِ کاربر می‌نویسد.
        AppSettings.DirOverride = _oldDir;
        try { Directory.Delete(_dir, true); } catch { }
    }

    // ══ کمک‌کارها ═══════════════════════════════════════════════════════════

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Text(string body, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "text/plain") };

    private static HttpResponseMessage Bytes(byte[] body)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    /// <summary>پاسخِ درِ اول با یک نسخهٔ دلخواه و دو بسته.</summary>
    private static string Feed(string tag, string baseId, long small = 6_880_476, long full = 110_058_211) => $$"""
        {
          "tag_name": "{{tag}}",
          "body": "یادداشت",
          "assets": [
            { "name": "PumpYaqobi-app-{{baseId}}.zip", "size": {{small}},
              "browser_download_url": "https://x/small.zip" },
            { "name": "PumpYaqobi-Setup.exe", "size": {{full}},
              "browser_download_url": "https://x/setup.exe" }
          ]
        }
        """;

    private static string LocalBase() => AppBase.LocalId;

    /// <summary>
    /// درِ اول (فهرستِ انتشار) با درِ دوم (فایلِ ساده) از هم جدا می‌شود.
    ///
    /// ⚠️ عمداً با **مسیر** سنجیده می‌شود، نه با نامِ میزبان: قاعدهٔ
    /// <c>UpdateTests.NoUserFacingFileMentionsTheRepository</c> می‌گوید نشانیِ
    /// منبع تنها در <c>UpdateService.cs</c> نوشته می‌شود، و همان آزمون این
    /// فایل را هم می‌گردد (یک بار همین‌جا سرخ شد).
    /// </summary>
    private static bool IsFeed(string url) => url.EndsWith("/releases/latest");

    // ══ ۱) درِ اول ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ANewerReleaseIsFound_AndTheSmallPackageIsPreferred()
    {
        var b = LocalBase();     // «abc12345» — همان پایهٔ نصبِ ساختگی

        UpdateService.TestTransport = (_, _) => Task.FromResult(Json(Feed("v99.9.9", b)));

        var info = await new UpdateService().CheckAsync();

        Assert.False(info.Failed);
        Assert.True(info.Available);
        Assert.Equal("99.9.9", info.LatestVersion);
        Assert.Equal("https://x/small.zip", info.DownloadUrl);
        Assert.True(info.IsSmallPackage);
    }

    [Fact]
    public async Task TheSameVersionIsReportedAsUpToDate_NotAsAFailure()
    {
        UpdateService.TestTransport = (_, _) =>
            Task.FromResult(Json(Feed("v" + AppVersion.Current, LocalBase())));

        var info = await new UpdateService().CheckAsync();

        Assert.False(info.Failed);
        Assert.False(info.Available);
        Assert.Contains("به‌روز است", info.StatusText);
    }

    [Fact]
    public async Task AnUnmatchedBaseFallsBackToTheFullInstaller()
    {
        UpdateService.TestTransport = (_, _) =>
            Task.FromResult(Json(Feed("v99.9.9", "ffffffff-not-ours")));

        var info = await new UpdateService().CheckAsync();

        Assert.True(info.Available);
        Assert.Equal("https://x/setup.exe", info.DownloadUrl);
        Assert.False(info.IsSmallPackage);
    }

    // ══ ۲) درِ دوم — همان چیزی که سقفِ نرخ را دور می‌زند ═══════════════════

    [Fact]
    public async Task WhenTheApiIsRateLimited_TheSecondDoorStillFindsTheVersion()
    {
        var hits = new List<string>();
        UpdateService.TestTransport = (req, _) =>
        {
            var url = req.RequestUri!.ToString();
            hits.Add(url);
            if (IsFeed(url))
                return Task.FromResult(Json("""{"message":"rate limit"}""", HttpStatusCode.Forbidden));
            if (url.EndsWith("version.txt")) return Task.FromResult(Text("99.9.9\n"));
            if (url.EndsWith("base.txt")) return Task.FromResult(Text(LocalBase()));
            return Task.FromResult(Text("", HttpStatusCode.NotFound));
        };

        var info = await new UpdateService().CheckAsync();

        Assert.False(info.Failed);
        Assert.True(info.Available);
        Assert.Equal("99.9.9", info.LatestVersion);
        Assert.Contains(hits, h => h.EndsWith("version.txt"));
        // ⛔ نشانیِ درِ دوم باید روی همان برچسبِ چرخشی باشد
        Assert.Contains(hits, h => h.Contains("/releases/download/desktop-latest/"));
    }

    [Fact]
    public async Task WhenBothDoorsAreShut_ItSaysSo_AndNeverClaimsUpToDate()
    {
        UpdateService.TestTransport = (_, _) => throw new HttpRequestException("no network");

        var info = await new UpdateService().CheckAsync();

        Assert.True(info.Failed);
        Assert.False(info.Available);
        Assert.StartsWith("❌", info.StatusText);
        Assert.Equal("Pump.Danger", info.StatusBrushKey);
        // ⛔ همان دروغی که کاربر را روی ۳.۱.۱۴۴ نگه داشت
        Assert.DoesNotContain("برنامه به‌روز است", info.StatusText);
        // ⚠️ و نامِ میزبان در پیام نیست
        Assert.DoesNotContain("github", info.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ⛔ گزارشِ صاحب سامانه با عکس (۱۴۰۵/۰۷/۱۰): روی ۳.۱.۱۵۱ پیامِ
    /// «سرورِ به‌روزرسانی جواب نداد — وقت تمام شد» را دید و پرسید «مگه از
    /// سرور اپدیت می‌گرفت؟» — یعنی آن را <b>سرورِ خانگیِ پمپ</b> خواند.
    ///
    /// این برنامه سه سرور دارد (خانگی · حساب · به‌روزرسانی) و کاربر قرار
    /// نیست از روی یک واژه حدس بزند کدام‌شان خراب است. پس جملهٔ مهلت دیگر
    /// واژهٔ «سرور» ندارد و صریح می‌گوید کارِ اینترنت است.
    ///
    /// ⚠️ و ادعای قدیمی ضعیف نشد، از درِ تازه گرفته شد: «نامِ میزبان نیست»
    /// و «‹به‌روز است› نمی‌گوید» هر دو این‌جا هم خواسته می‌شوند.
    /// </summary>
    [Fact]
    public async Task ATimeoutBlamesTheInternet_NotThePumpsOwnServer()
    {
        UpdateService.TestTransport = (_, _) => throw new TaskCanceledException("timeout");

        var info = await new UpdateService().CheckAsync();

        Assert.True(info.Failed);
        Assert.False(info.Available);
        Assert.Equal("Pump.Danger", info.StatusBrushKey);

        //  ⛔ واژه‌ای که کاربر را دنبالِ سرورِ خانگی فرستاد
        Assert.DoesNotContain("سرورِ به‌روزرسانی", info.StatusText);
        //  و می‌گوید کارِ اینترنت است
        Assert.Contains("اینترنت", info.StatusText);
        //  ⛔ و صریح می‌گوید به سرورِ خانگیِ پمپ ربطی ندارد
        Assert.Contains("ربطی ندارد", info.StatusText);

        //  ادعاهای قدیمی، دست‌نخورده
        Assert.DoesNotContain("برنامه به‌روز است", info.StatusText);
        Assert.DoesNotContain("github", info.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ATaglessReleaseIsNotTreatedAsUpToDate()
    {
        // برچسبِ بی‌شماره (انتشارِ اپِ گوشی) ⇒ درِ دوم، نه «به‌روز است»
        UpdateService.TestTransport = (req, _) =>
        {
            var url = req.RequestUri!.ToString();
            if (IsFeed(url)) return Task.FromResult(Json(Feed("kar-latest", LocalBase())));
            if (url.EndsWith("version.txt")) return Task.FromResult(Text("99.9.9"));
            if (url.EndsWith("base.txt")) return Task.FromResult(Text(LocalBase()));
            return Task.FromResult(Text("", HttpStatusCode.NotFound));
        };

        var info = await new UpdateService().CheckAsync();

        Assert.True(info.Available);
        Assert.Equal("99.9.9", info.LatestVersion);
    }

    // ══ ۳) دانلود ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ACompleteDownloadLandsOnDisk()
    {
        var body = new byte[4096];
        Random.Shared.NextBytes(body);
        UpdateService.TestTransport = (_, _) => Task.FromResult(Bytes(body));

        var info = new UpdateInfo(true, "1.0.0", "99.9.9", "https://x/small.zip", body.Length, null, true);
        var path = await new UpdateService().DownloadAsync(info, null);

        Assert.NotNull(path);
        Assert.Equal(body.Length, new FileInfo(path!).Length);
        Assert.Equal(body, await File.ReadAllBytesAsync(path!));
    }

    [Fact]
    public async Task ATruncatedDownloadIsRejected_AndLeavesNothingBehind()
    {
        var body = new byte[1000];
        UpdateService.TestTransport = (_, _) => Task.FromResult(Bytes(body));

        // سرور ۱۰۰۰ بایت داد ولی انتشار ۵۰۰۰ گفته بود
        var info = new UpdateInfo(true, "1.0.0", "99.9.9", "https://x/small.zip", 5000, null, true);
        var path = await new UpdateService().DownloadAsync(info, null);

        Assert.Null(path);
        var dir = Path.Combine(AppSettings.Dir, "updates");
        if (Directory.Exists(dir))
            Assert.Empty(Directory.GetFiles(dir, "*.zip*"));
    }

    [Fact]
    public async Task ADownloadThatThrowsReturnsNull_AndNeverEscapes()
    {
        // ⛔ این همان «دانلود هم نیست» بود: استثنا از فرمان بیرون می‌زد و
        // کاربر هیچ پیامی نمی‌دید.
        UpdateService.TestTransport = (_, _) => throw new HttpRequestException("cut");

        var info = new UpdateInfo(true, "1.0.0", "99.9.9", "https://x/small.zip", 10, null, true);
        var path = await new UpdateService().DownloadAsync(info, null);

        Assert.Null(path);
    }

    [Fact]
    public async Task TheSecondDoorTakesTheSizeFromTheResponse()
    {
        // درِ دوم اندازه را نمی‌داند (SizeBytes = 0) و باید باز هم فایلِ
        // کامل را بپذیرد
        var body = new byte[2048];
        UpdateService.TestTransport = (_, _) => Task.FromResult(Bytes(body));

        var info = new UpdateInfo(true, "1.0.0", "99.9.9", "https://x/small.zip", 0, null, true);
        var path = await new UpdateService().DownloadAsync(info, null);

        Assert.NotNull(path);
        Assert.Equal(body.Length, new FileInfo(path!).Length);
    }

    // ══ ۴) لغو ═════════════════════════════════════════════════════════════

    [Fact]
    public async Task ACancelledCheckDoesNotClaimAnything()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        UpdateService.TestTransport = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Json(Feed("v99.9.9", LocalBase())));
        };

        var info = await new UpdateService().CheckAsync(cts.Token);

        Assert.True(info.Failed);
        Assert.False(info.Available);
    }
}
