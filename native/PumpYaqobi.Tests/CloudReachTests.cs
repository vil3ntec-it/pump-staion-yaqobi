using System.Net;
using System.Net.Http;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «هیچ کلکِ دروغی نباشد که بگوید وصل است» ═══════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۱): «ببین برنامه‌ها چرا به سرور وصل
/// نمی‌شوند، مشکلشان را حل کن و در جا خودکار وصل شوند — و هیچ کلکِ دروغی
/// نباشد که بگوید وصل است.»
///
/// <para>
/// دو چیزِ جدا این‌جا قفل می‌شوند:
/// </para>
/// <list type="number">
///   <item><b>راست‌گویی</b>: <see cref="CloudLink.Reach"/> فقط از جوابِ
///     واقعیِ سرور پر می‌شود. «هنوز نپرسیده‌ایم» سبز نیست.</item>
///   <item><b>خودکار بودن</b>: خودِ برنامه سراغِ ابر می‌رود، بی این‌که کاربر
///     صفحه‌ای را باز کند (<c>StationPublisher.CloudKeepAsync</c>).</item>
/// </list>
///
/// ⚠️ این کلاس حالِ **ایستا** را عوض می‌کند، پس مثلِ بقیه سریال می‌دود.
/// </summary>
[Collection(AppHostCollection.Name)]
public class CloudReachTests : IDisposable
{
    public CloudReachTests() => CloudLink.ResetReach();

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        GC.SuppressFinalize(this);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static void Serve(Func<HttpResponseMessage> make) =>
        CloudLink.TestTransport = (_, _) => Task.FromResult(make());

    // ── ۱) تا نپرسیده‌ایم، هیچ ادعایی نیست ───────────────────────────────

    [Fact]
    public void Ta_Naporsidim_Na_Vasl_Ast_Na_Ghat()
    {
        Assert.Equal(CloudReach.Unknown, CloudLink.Reach);
        Assert.Null(CloudLink.CloudOkAt);
    }

    // ── ۲) جوابِ درست ⇒ وصل ──────────────────────────────────────────────

    [Fact]
    public async Task Javabe_Dorost_Yani_Vasl()
    {
        Serve(() => Json(HttpStatusCode.OK, """{"ok":true,"version":"2.5.5"}"""));

        var (up, ver) = await CloudLink.CloudHealthAsync();

        Assert.True(up);
        Assert.Equal("2.5.5", ver);
        Assert.Equal(CloudReach.Online, CloudLink.Reach);
        Assert.NotNull(CloudLink.CloudOkAt);
    }

    // ── ۳) خطای خودِ سرورِ ما هم یعنی رسیدیم ─────────────────────────────

    /// <summary>
    /// «رمز غلط» یا «این مسیر وجود ندارد» جوابِ **سرورِ خودمان** است — پس
    /// شبکه سالم است و چراغ باید سبز باشد، هرچند خودِ کار نشده.
    /// </summary>
    [Fact]
    public async Task Khataye_Khode_Sarvare_Ma_Ham_Yani_Rasidim()
    {
        Serve(() => Json(HttpStatusCode.Unauthorized,
            """{"error":{"code":"unauthorized","message":"احراز هویت لازم است"}}"""));

        await CloudLink.CloudHealthAsync();

        Assert.Equal(CloudReach.Online, CloudLink.Reach);
    }

    // ── ۴) چیزی که سرورِ ما نیست، «وصل» حساب نمی‌شود ─────────────────────

    /// <summary>
    /// ⛔ همان ۴۰۴ِ بی‌بدنه‌ای که کاربر دید: یک صفحهٔ اچ‌تی‌ام‌ال از پروکسی.
    /// سرورِ ما هیچ‌وقت این را نمی‌دهد، پس سبز کردنِ چراغ با آن **دروغ** است.
    /// </summary>
    [Fact]
    public async Task Javabe_Ghair_Az_Sarvare_Ma_Vasl_Hesab_Nemishavad()
    {
        CloudLink.TestTransport = (_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("<html>404</html>",
                    System.Text.Encoding.UTF8, "text/html"),
            });

        var (up, _) = await CloudLink.CloudHealthAsync();

        Assert.False(up);
        Assert.Equal(CloudReach.Offline, CloudLink.Reach);
        Assert.Null(CloudLink.CloudOkAt);
    }

    // ── ۴ب) درگاهِ سرورِ خانگی می‌گوید «سرورِ حساب روشن نیست» ⇒ قطع ─────────

    /// <summary>
    /// ⛔ با عکس دیده شد (۱۴۰۵/۰۷/۰۲): درگاهِ پنلِ خانگی وقتی shop/server
    /// خاموش است ۵۰۳ی با شکلِ خودمان می‌دهد (<c>account_server_down</c>) و
    /// چراغ آن را «سرورِ ما جواب داد» می‌شمرد — سبز، در حالی که هر ورودی همین
    /// خطا را می‌گرفت. جوابِ درگاه جوابِ سرورِ حساب نیست.
    /// </summary>
    [Fact]
    public async Task Dargah_Miguyad_Sarvare_Hesab_Khamush_Ast_Yani_Ghat()
    {
        Serve(() => Json(HttpStatusCode.ServiceUnavailable,
            """{"error":{"code":"account_server_down","message":"سرورِ حساب روی سرورِ خانگی روشن نیست."}}"""));

        var (up, _) = await CloudLink.CloudHealthAsync();

        Assert.False(up);
        Assert.Equal(CloudReach.Offline, CloudLink.Reach);
        Assert.Null(CloudLink.CloudOkAt);
        Assert.Contains("روشن نیست", CloudLink.CloudWhy);
        Assert.True(CloudLink.IsDownCode("account_server_unreachable"));
        Assert.False(CloudLink.IsDownCode("unauthorized"));
    }

    // ── ۵) بی‌اینترنت ⇒ قطع، نه «وصل» ────────────────────────────────────

    [Fact]
    public async Task Bi_Internet_Ghat_Ast()
    {
        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("no network");

        var (up, _) = await CloudLink.CloudHealthAsync();

        Assert.False(up);
        Assert.Equal(CloudReach.Offline, CloudLink.Reach);
    }

    // ── ۶) قطع شدن، آخرین تاییدِ واقعی را پاک نمی‌کند ────────────────────

    /// <summary>
    /// کاربر باید بداند «الان نمی‌رسیم» با «هیچ‌وقت نرسیدیم» فرق دارد.
    /// </summary>
    [Fact]
    public async Task Ghat_Shodan_Akharin_Tayide_Vaghei_Ra_Negah_Midarad()
    {
        Serve(() => Json(HttpStatusCode.OK, """{"ok":true,"version":"2.5.5"}"""));
        await CloudLink.CloudHealthAsync();
        var first = CloudLink.CloudOkAt;
        Assert.NotNull(first);

        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("no network");
        await CloudLink.CloudHealthAsync();

        Assert.Equal(CloudReach.Offline, CloudLink.Reach);
        Assert.Equal(first, CloudLink.CloudOkAt);      // همان لحظهٔ واقعی
        Assert.NotEqual("", CloudLink.CloudWhy);       // و دلیلش را می‌گوید
    }

    // ── ۷) خودکار بودن، روی خودِ سورس ────────────────────────────────────

    private static string Root
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
                d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("ریشهٔ پروژه پیدا نشد");
        }
    }

    private static string App(string rel) =>
        File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", rel));

    /// <summary>
    /// ⛔ حلقهٔ پس‌زمینه باید **خودش** سراغِ ابر برود. تا دیروز هیچ تماسی با
    /// ابر نبود مگر کاربر صفحهٔ پروفایل را باز می‌کرد، و نصبی که حساب داشت
    /// ولی پروفایل را ندیده بود تا ابد بند نمی‌شد.
    /// </summary>
    [Fact]
    public void Barname_Khodash_Soraghe_Abr_Miravad()
    {
        var src = App(Path.Combine("Services", "StationPublisher.cs"));

        Assert.Contains("CloudTick", src);
        Assert.Contains("CloudKeepAsync", src);
        Assert.Contains("await cloud.HomeFromAccountAsync(ct)", src);
        Assert.Contains("await CloudLink.CloudHealthAsync(ct)", src);
    }

    /// <summary>
    /// ⛔ چراغِ ابر از `Reach` می‌خواند، نه از «توکنی روی دیسک هست».
    /// و پیش‌فرضش خاکستری است، نه سبز.
    /// </summary>
    [Fact]
    public void Charaghe_Abr_Az_Javabe_Vaghei_Harf_Mizanad()
    {
        var vm = App(Path.Combine("ViewModels", "MainViewModel.cs"));

        Assert.Contains("CloudDotBrushKey", vm);
        Assert.Contains("Services.CloudLink.Reach", vm);
        Assert.Contains("_cloudDotBrushKey = \"Pump.Muted\"", vm);

        //  ⚠️ از ۱۴۰۵/۰۷/۱۰ این چراغ **خودش** در سربرگ نیست — با چراغِ
        //  سرورِ خانگی یکی شده (خواستهٔ صریحِ صاحب ریپو: «یکی باشه اصلی»).
        //  ولی آن‌چه این بند نگه می‌داشت عوض نشده: حالِ ابر همچنان از
        //  `Reach` می‌آید و همچنان به چراغ می‌رسد.
        Assert.Contains("TickCloudDot();", vm);
        var xaml = App(Path.Combine("Views", "MainWindow.axaml"));
        Assert.Contains("LinkDotBrushKey", xaml);
        Assert.Contains("CheckLinksCommand", xaml);
    }

    /// <summary>
    /// ⛔ و صفحهٔ پروفایل «وارد شده‌اید» را از روی فایلِ روی دیسک نمی‌گوید.
    /// </summary>
    [Fact]
    public void Profile_Halate_Vaghei_Ra_Migooyad()
    {
        var vm = App(Path.Combine("ViewModels", "Sections", "AccountSectionViewModel.cs"));

        Assert.Contains("CloudNote()", vm);
        Assert.Contains("CloudReach.Offline", vm);
        //  ⛔ همان خطِ کهنه که بی هیچ سنجشی رشتهٔ خالی می‌گذاشت
        Assert.DoesNotContain("AccountStatus = SignedIn ? \"\"", vm);
    }
}
