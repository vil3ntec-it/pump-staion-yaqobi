using System.Net;
using System.Net.Http;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ نشستِ ابر: انقضا، تازه‌سازی، ۴۰۱ و خروج ═══════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (بندهای ۷، ۱۱ و ۱۲): «Token چه مدت معتبر است،
/// چگونه منقضی می‌شود، چگونه Refresh می‌شود، بعد از Logout چه اتفاقی
/// می‌افتد، و Token قدیمی قابلِ سوءاستفاده نباشد… Token منقضی شده · Token
/// خراب است · اینترنت قطع است · سرور خطا می‌دهد.»
///
/// ⛔ **باگی که این فایل قفلش می‌کند**: سرور به توکنِ دسترسی دقیقاً یک ساعت
/// عمر می‌دهد (<c>ACCESS_TOKEN_TTL_MIN = 60</c>) و <c>refreshToken</c> را
/// نود روز. برنامه <c>refreshToken</c> را ذخیره می‌کرد ولی <b>هیچ‌جا
/// نمی‌خواندش</b> و هیچ ۴۰۱ی را هم نمی‌فهمید — یعنی یک ساعت پس از ورود،
/// هر کارِ حسابی بی‌صدا می‌مرد و دیگر هیچ‌وقت درست نمی‌شد، در حالی که
/// پروفایل همچنان «وارد شده‌اید» می‌گفت.
///
/// ⚠️ سرورِ ساختگی است، ولی <b>شکلِ پاسخِ واقعی</b> را می‌دهد — از روی خودِ
/// <c>shop/server/src/routes/auth.js</c> خوانده شده، نه از روی حدس.
/// </summary>
//  ⚠️ `AppSettings.DirOverride` **استاتیک** است و xUnit کلاس‌ها را موازی
//  می‌دواند؛ بی این نشان، این کلاس و هر کلاسِ دیگری که همان را عوض
//  می‌کند روی هم می‌نویسند و آزمون‌ها **گاهی** سرخ می‌شوند.
[Collection(AppHostCollection.Name)]
public class CloudSessionTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    /// <summary>هر مسیری که زده شد، به ترتیب — تا بشود شمرد و دید.</summary>
    private readonly List<string> _hits = new();

    public CloudSessionTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-sess-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    /// <summary>سرورِ ساختگی: مسیر ⇒ پاسخ.</summary>
    private void Serve(Func<string, HttpRequestMessage, HttpResponseMessage> handler)
    {
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            _hits.Add(path);
            return Task.FromResult(handler(path, req));
        };
    }

    private static string? Bearer(HttpRequestMessage r) =>
        r.Headers.TryGetValues("Authorization", out var v)
            ? v.First().Replace("Bearer ", "") : null;

    private (CloudLink Link, AppSettings Settings) Link(Action<AppSettings>? seed = null)
    {
        var f = AppSettings.Load();
        seed?.Invoke(f);
        f.Save();
        return (new CloudLink(f, () => { f.Save(); return Task.CompletedTask; }), f);
    }

    private static long InAnHour => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;
    private static long AnHourAgo => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 3_600_000;

    // ── ۱) توکنِ منقضی خودش تازه می‌شود ──────────────────────────────────

    /// <summary>
    /// ⭐ همان باگِ اصلی: توکن یک ساعته منقضی شده، سرور ۴۰۱ می‌دهد، و برنامه
    /// باید <b>یک بار</b> تازه کند و دوباره بزند — نه این‌که شکست بخورد.
    /// </summary>
    [Fact]
    public async Task Token401_YekBar_TazeMishavad_VaDobareMizanad()
    {
        Serve((path, req) => path switch
        {
            "/api/auth/refresh" =>
                Json(HttpStatusCode.OK,
                     $$"""{"accessToken":"acc-2","accessExpiresAt":{{InAnHour}}}"""),
            "/api/pump/me" => Bearer(req) == "acc-2"
                ? Json(HttpStatusCode.OK,
                       """{"home":{"url":"http://192.168.1.50:4701","readKey":"rk","station":"yaqobi"}}""")
                : Json(HttpStatusCode.Unauthorized,
                       """{"error":{"code":"invalid_token","message":"نشست منقضی شده است"}}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        var (link, f) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";          // کهنه
            s.CloudRefreshToken = "ref-1";
            s.CloudAccessExpiresAt = InAnHour;      // «هنوز وقت دارد» — پس راهِ ۴۰۱ می‌رود
        });

        var (ok, url, readKey, station, why) = await link.HomeFromAccountAsync();

        Assert.True(ok, why);
        Assert.Equal("http://192.168.1.50:4701", url);
        Assert.Equal("rk", readKey);
        Assert.Equal("yaqobi", station);

        //  ⚠️ دقیقاً همین ترتیب: زدن، خوردن به ۴۰۱، تازه کردن، دوباره زدن
        Assert.Equal(new[] { "/api/pump/me", "/api/auth/refresh", "/api/pump/me" }, _hits);
        //  و توکنِ تازه روی دیسک نشست
        Assert.Equal("acc-2", AppSettings.Load().CloudAccountToken);
    }

    /// <summary>
    /// انقضا را از قبل می‌دانیم ⇒ اصلاً آن درخواستِ حتماً-شکست زده نمی‌شود.
    /// </summary>
    [Fact]
    public async Task Ghabl_Az_Enghza_Khodash_TazeMikonad()
    {
        Serve((path, req) => path switch
        {
            "/api/auth/refresh" =>
                Json(HttpStatusCode.OK, $$"""{"accessToken":"acc-2","accessExpiresAt":{{InAnHour}}}"""),
            "/api/pump/me" when Bearer(req) == "acc-2" =>
                Json(HttpStatusCode.OK, """{"home":{"url":"http://x","readKey":"","station":"s"}}"""),
            _ => Json(HttpStatusCode.Unauthorized, "{}"),
        });

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudRefreshToken = "ref-1";
            s.CloudAccessExpiresAt = AnHourAgo;     // از قبل مرده
        });

        var (ok, _, _, _, why) = await link.HomeFromAccountAsync();
        Assert.True(ok, why);
        //  تازه‌سازی **اول** آمده، و فقط یک بار به ‎me‎ زده شده
        Assert.Equal(new[] { "/api/auth/refresh", "/api/pump/me" }, _hits);
    }

    // ── ۲) توکنِ تازه‌سازیِ مرده ⇒ نشست پاک می‌شود ────────────────────────

    /// <summary>
    /// ⚠️ اگر خودِ <c>refreshToken</c> هم باطل باشد، برنامه باید <b>اعتراف
    /// کند</b> و نشست را پاک کند — وگرنه پروفایل تا ابد «وارد شده‌اید»
    /// می‌گفت و هیچ دکمه‌ای کار نمی‌کرد.
    /// </summary>
    [Fact]
    public async Task RefreshTokene_Morde_Neshast_Ra_Pak_Mikonad()
    {
        Serve((_, _) => Json(HttpStatusCode.Unauthorized,
            """{"error":{"code":"invalid_token","message":"نشست منقضی شده است، دوباره وارد شوید"}}"""));

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudRefreshToken = "ref-dead";
            s.CloudAccessExpiresAt = InAnHour;
            s.CloudDeviceToken = "dev-token";       // اشتراک، که نباید دست بخورد
            s.CloudEmail = "haroon@gmail.com";
        });

        var (ok, _, _, _, _) = await link.HomeFromAccountAsync();
        Assert.False(ok);
        Assert.False(link.SignedIn);

        var f = AppSettings.Load();
        Assert.Equal("", f.CloudAccountToken);
        Assert.Equal("", f.CloudRefreshToken);
        Assert.Equal(0, f.CloudAccessExpiresAt);
        //  ⛔ ولی اشتراکِ دستگاه و ایمیل می‌مانند: اشتراک ربطی به نشست ندارد،
        //  و ایمیل کادرِ صفحهٔ ورود را پر می‌کند.
        Assert.Equal("dev-token", f.CloudDeviceToken);
        Assert.Equal("haroon@gmail.com", f.CloudEmail);
    }

    /// <summary>
    /// ⛔ **بی‌اینترنت هیچ‌وقت کسی را از حسابش بیرون نمی‌اندازد.** این برنامه
    /// اساساً آفلاین است؛ یک قطعیِ مودم نباید نشست را پاک کند.
    /// </summary>
    [Fact]
    public async Task Bi_Internet_Neshast_Pak_Nemishavad()
    {
        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("no network");

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudRefreshToken = "ref-1";
            s.CloudAccessExpiresAt = AnHourAgo;
        });

        var (ok, _, _, _, why) = await link.HomeFromAccountAsync();
        Assert.False(ok);
        Assert.Contains("اینترنت", why);

        //  ⭐ و همه‌چیز سرِ جایش ماند
        var f = AppSettings.Load();
        Assert.Equal("acc-1", f.CloudAccountToken);
        Assert.Equal("ref-1", f.CloudRefreshToken);
        Assert.True(link.SignedIn);
    }

    /// <summary>سرورِ خراب (۵۰۰) هم نشست را پاک نمی‌کند.</summary>
    [Fact]
    public async Task Sarvare_Kharab_Neshast_Ra_Pak_Nemikonad()
    {
        Serve((_, _) => Json(HttpStatusCode.InternalServerError, """{"error":{"code":"internal"}}"""));

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudRefreshToken = "ref-1";
            s.CloudAccessExpiresAt = InAnHour;
        });

        var (ok, _, _, _, _) = await link.HomeFromAccountAsync();
        Assert.False(ok);
        Assert.True(link.SignedIn);
        Assert.Equal("acc-1", AppSettings.Load().CloudAccountToken);
    }

    /// <summary>
    /// ⚠️ حلقه نمی‌زند: اگر بعد از تازه‌سازی هم ۴۰۱ گرفتیم، یعنی نشست واقعاً
    /// باطل است و باید بایستیم — نه این‌که تا ابد بزنیم.
    /// </summary>
    [Fact]
    public async Task Halghe_Nemizanad_Faghat_YekBar()
    {
        Serve((path, _) => path == "/api/auth/refresh"
            ? Json(HttpStatusCode.OK, $$"""{"accessToken":"acc-2","accessExpiresAt":{{InAnHour}}}""")
            : Json(HttpStatusCode.Unauthorized, "{}"));

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudRefreshToken = "ref-1";
            s.CloudAccessExpiresAt = InAnHour;
        });

        await link.HomeFromAccountAsync();
        Assert.Equal(new[] { "/api/pump/me", "/api/auth/refresh", "/api/pump/me" }, _hits);
        Assert.Equal(2, _hits.Count(h => h == "/api/pump/me"));
    }

    // ── ۲ب) بند شدن با حساب — بی هیچ کدی ────────────────────────────────

    /// <summary>
    /// ⛔ <b>«اشتراک رو من به حسابِ یارو از سرور می‌دم… من یادم نمیاد که برای
    /// اشتراک کدی گفته باشم.»</b>
    ///
    /// تا دیروز تنها راهِ گرفتنِ توکنِ دستگاه و مجوز، کدِ شش‌رقمی بود. یعنی
    /// صاحبِ پمپی که اشتراکش را مدیر روی <b>حسابش</b> گذاشته بود، بی کد
    /// نمی‌توانست برنامه را راه بیندازد.
    /// </summary>
    [Fact]
    public async Task Bind_BaHesab_Tokene_Dastgah_VaMojavez_Migirad()
    {
        Serve((path, req) => path == "/api/pump/device/bind" && Bearer(req) == "acc-1"
            ? Json(HttpStatusCode.Created, """
                {"deviceToken":"pd_from_account","station":{"id":"stn-9","code":"yaqobi"},
                 "license":"lic.from.account","publicKey":"pk-1",
                 "entitlement":{"source":"subscription"},
                 "subscription":{"active":true,"plan":"std","daysLeft":365}}
                """)
            : Json(HttpStatusCode.NotFound, "{}"));

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
        });

        var r = await link.BindAsync();

        Assert.True(r.Ok, r.Why);
        //  ⚠️ از **دیسک** خوانده می‌شود، نه از شیءِ در حافظه: یک بار همین
        //  باگ بود که توکن تا بسته شدنِ برنامه کار می‌کرد و بعد نه
        var f = AppSettings.Load();
        Assert.Equal("pd_from_account", f.CloudDeviceToken);
        Assert.Equal("stn-9", f.CloudStationId);
        Assert.Equal("lic.from.account", f.CloudLicense);
        Assert.Equal("pk-1", f.CloudPublicKey);
        Assert.True(link.Activated);
    }

    /// <summary>
    /// ⛔ قفلِ کلیدِ عمومی (TOFU) این‌جا هم هست — وگرنه سرورِ ساختگی با
    /// کلیدِ خودش می‌توانست مجوزِ خودش را امضا کند.
    /// </summary>
    [Fact]
    public async Task Bind_KelideDigar_Ra_RadMikonad()
    {
        Serve((path, _) => path == "/api/pump/device/bind"
            ? Json(HttpStatusCode.Created, """
                {"deviceToken":"pd_evil","station":{"id":"stn-9"},
                 "license":"lic.evil","publicKey":"pk-EVIL"}
                """)
            : Json(HttpStatusCode.NotFound, "{}"));

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
            s.CloudPublicKey = "pk-1";               // از قبل قفل شده
        });

        var r = await link.BindAsync();

        Assert.False(r.Ok);
        Assert.Equal("key_mismatch", r.Code);
        var f = AppSettings.Load();
        Assert.Equal("pk-1", f.CloudPublicKey);
        Assert.True(string.IsNullOrEmpty(f.CloudDeviceToken));
    }

    /// <summary>بی حساب، هیچ درخواستی هم زده نمی‌شود.</summary>
    [Fact]
    public async Task Bind_BiHesab_HichDarkhasti_Nemizanad()
    {
        Serve((_, _) => Json(HttpStatusCode.OK, "{}"));
        var (link, _) = Link();

        var r = await link.BindAsync();

        Assert.False(r.Ok);
        Assert.Equal("no_account", r.Code);
        Assert.Empty(_hits);
    }

    /// <summary>
    /// ⚠️ سرور توکن نداد ⇒ «نیمه‌کاره» نمی‌مانیم: نه توکنی می‌نشیند و نه
    /// مجوزی.
    /// </summary>
    [Fact]
    public async Task Bind_BiTokene_Dastgah_Nimekare_Nemimanad()
    {
        Serve((path, _) => path == "/api/pump/device/bind"
            ? Json(HttpStatusCode.Created, """{"station":{"id":"stn-9"},"license":"lic.x"}""")
            : Json(HttpStatusCode.NotFound, "{}"));

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
        });

        var r = await link.BindAsync();

        Assert.False(r.Ok);
        Assert.Equal("no_device_token", r.Code);
        Assert.False(link.Activated);
    }

    /// <summary>
    /// نصبی که فقط وارد حساب شده، سرِ گرفتنِ نشانیِ خانگی <b>خودش</b> بند
    /// می‌شود — کاربر هیچ کدی نمی‌زند.
    /// </summary>
    [Fact]
    public async Task Vorud_BeHesab_Dastgah_Ra_Khodash_Band_Mikonad()
    {
        Serve((path, _) => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, """
                {"station":{"id":"stn-9","code":"yaqobi"},
                 "home":{"url":"http://192.168.1.50:4701","readKey":"rk","station":"yaqobi"}}
                """),
            "/api/pump/device/bind" => Json(HttpStatusCode.Created,
                """{"deviceToken":"pd_auto","station":{"id":"stn-9"},"license":"lic.auto","publicKey":"pk-1"}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
        });

        var (ok, url, _, _, why) = await link.HomeFromAccountAsync();

        Assert.True(ok, why);
        Assert.Equal("http://192.168.1.50:4701", url);
        Assert.Contains("/api/pump/device/bind", _hits);
        Assert.Equal("pd_auto", AppSettings.Load().CloudDeviceToken);
    }

    /// <summary>
    /// ⛔ و <b>پس از</b> سنجشِ «این حساب مالِ پمپِ دیگری است» — وگرنه ورود با
    /// حسابِ پمپِ دیگر همین دستگاه را به آن پمپ می‌بست.
    /// </summary>
    [Fact]
    public async Task Hesabe_PompeDigar_Dastgah_Ra_Band_Nemikonad()
    {
        Serve((path, _) => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK,
                """{"station":{"id":"stn-OTHER"},"home":{"url":"http://x","readKey":"","station":"o"}}"""),
            _ => Json(HttpStatusCode.Created, """{"deviceToken":"pd_wrong"}"""),
        });

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
            s.CloudStationId = "stn-MINE";           // این دستگاه روی پمپِ خودش قفل است
        });

        var (ok, _, _, _, why) = await link.HomeFromAccountAsync();

        Assert.False(ok);
        Assert.Contains("پمپِ دیگری", why);
        Assert.DoesNotContain("/api/pump/device/bind", _hits);
        Assert.True(string.IsNullOrEmpty(AppSettings.Load().CloudDeviceToken));
    }

    /// <summary>
    /// دستگاهی که از قبل بند است دوباره بند نمی‌شود — نه درخواستِ اضافه، نه
    /// توکنِ تازه‌ای که توکنِ کهنه را باطل کند.
    /// </summary>
    [Fact]
    public async Task Dastgahe_Band_Shode_Dobare_Band_Nemishavad()
    {
        Serve((path, _) => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK,
                """{"station":{"id":"stn-9"},"home":{"url":"http://x","readKey":"","station":"s"}}"""),
            _ => Json(HttpStatusCode.Created, """{"deviceToken":"pd_new"}"""),
        });

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
            s.CloudDeviceToken = "pd_old";
            s.CloudStationId = "stn-9";
        });

        var (ok, _, _, _, why) = await link.HomeFromAccountAsync();

        Assert.True(ok, why);
        Assert.DoesNotContain("/api/pump/device/bind", _hits);
        Assert.Equal("pd_old", AppSettings.Load().CloudDeviceToken);
    }

    // ── ۳) خروج، روی سرور هم ─────────────────────────────────────────────

    /// <summary>
    /// ⛔ **خروج پیش از این فقط محلی بود** — توکنِ دسترسی و توکنِ تازه‌سازی
    /// روی سرور زنده می‌ماندند (تازه‌سازی تا نود روز)، پس «خروج» جلوی کسی
    /// را که آن رشته را برداشته بود نمی‌گرفت.
    /// </summary>
    [Fact]
    public async Task Khoruj_Neshast_Ra_RoyeSarvar_Ham_Batel_Mikonad()
    {
        string? sentRefresh = null, sentBearer = null;
        CloudLink.TestTransport = async (req, _) =>
        {
            _hits.Add(req.RequestUri!.AbsolutePath);
            var body = req.Content is null ? "{}" : await req.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            sentRefresh = doc.RootElement.TryGetProperty("refreshToken", out var r) ? r.GetString() : null;
            sentBearer = Bearer(req);
            return Json(HttpStatusCode.OK, """{"ok":true}""");
        };

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudRefreshToken = "ref-1";
            s.CloudAccessExpiresAt = InAnHour;
            s.CloudDeviceToken = "dev-token";
        });

        await link.SignOutAsync();

        Assert.Contains("/api/auth/logout", _hits);
        Assert.Equal("ref-1", sentRefresh);         // تا سرور همان را باطل کند
        Assert.Equal("acc-1", sentBearer);

        var f = AppSettings.Load();
        Assert.Equal("", f.CloudAccountToken);
        Assert.Equal("", f.CloudRefreshToken);
        Assert.Equal(0, f.CloudAccessExpiresAt);
        //  ⛔ و اشتراکِ دستگاه دست نخورد — خروج از حساب، اشتراک را نمی‌برد
        Assert.Equal("dev-token", f.CloudDeviceToken);
    }

    /// <summary>
    /// ⚠️ سرور که نبود، خروجِ محلی باید باز هم انجام شود: کسی که اینترنت
    /// ندارد هم باید بتواند از حسابش بیرون بیاید.
    /// </summary>
    [Fact]
    public async Task Khoruj_Bi_Internet_Ham_Anjam_Mishavad()
    {
        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("no network");

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudRefreshToken = "ref-1";
        });

        await link.SignOutAsync();
        Assert.False(link.SignedIn);
        Assert.Equal("", AppSettings.Load().CloudAccountToken);
    }

    // ── ۴) رمزِ فراموش‌شده ───────────────────────────────────────────────

    /// <summary>
    /// راهی که سرور از قبل داشت و برنامه هیچ‌وقت صدایش نزده بود.
    /// سرور پس از گذاشتنِ رمزِ تازه <b>خودش وارد هم می‌کند</b>.
    /// </summary>
    [Fact]
    public async Task RamzeFaramushShode_KodMirad_VaRamzeTaze_Neshast_Midahad()
    {
        Serve((path, _) => path switch
        {
            "/api/auth/password/forgot" => Json(HttpStatusCode.OK,
                """{"ok":true,"sent":true,"email":"haroon@gmail.com","resendSeconds":60}"""),
            "/api/auth/password/reset" => Json(HttpStatusCode.OK,
                "{\"accessToken\":\"acc-new\",\"refreshToken\":\"ref-new\","
                + "\"accessExpiresAt\":" + InAnHour + ","
                + "\"user\":{\"email\":\"haroon@gmail.com\",\"name\":\"هارون یعقوبی\"}}"),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        var (link, _) = Link();

        Assert.True((await link.ForgotPasswordAsync("haroon@gmail.com")).Ok);
        Assert.Contains("/api/auth/password/forgot", _hits);

        var done = await link.ResetPasswordAsync("haroon@gmail.com", "123456", "tazeh-ramz-1");
        Assert.True(done.Ok, done.Why);

        //  ⭐ و همان‌جا وارد شد — کاربر رمزِ تازه‌اش را دوباره تایپ نمی‌کند
        Assert.True(link.SignedIn);
        var f = AppSettings.Load();
        Assert.Equal("acc-new", f.CloudAccountToken);
        Assert.Equal("ref-new", f.CloudRefreshToken);
        Assert.Equal("haroon@gmail.com", f.CloudEmail);
        //  ⛔ و رمز هیچ‌جا روی دیسک ننشست
        Assert.DoesNotContain("tazeh-ramz-1", File.ReadAllText(Path.Combine(_dir, "settings.json")));
    }

    /// <summary>کدِ غلط ⇒ پیامِ خودِ سرور، و هیچ نشستی ساخته نمی‌شود.</summary>
    [Fact]
    public async Task RamzeTaze_BaKodeGhalat_HichNeshasti_Nemisazad()
    {
        Serve((_, _) => Json(HttpStatusCode.BadRequest,
            """{"error":{"code":"otp_invalid","message":"کد درست نیست"}}"""));

        var (link, _) = Link();
        var res = await link.ResetPasswordAsync("haroon@gmail.com", "999999", "tazeh-ramz-1");

        Assert.False(res.Ok);
        Assert.Equal("کد درست نیست", res.Why);
        Assert.Equal("otp_invalid", res.Code);
        Assert.False(link.SignedIn);
    }

    /// <summary>کدِ سه‌رقمی اصلاً به سرور نمی‌رسد.</summary>
    [Fact]
    public async Task Kode_SeRaghami_Be_Sarvar_Nemiresad()
    {
        Serve((_, _) => Json(HttpStatusCode.OK, "{}"));
        var (link, _) = Link();

        Assert.False((await link.ResetPasswordAsync("a@b.com", "123", "tazeh-ramz-1")).Ok);
        Assert.Empty(_hits);
    }

    // ── ۵) پیامِ خطاها — بی درزِ اطلاعات ─────────────────────────────────

    /// <summary>
    /// ۴۲۹ی سرور («ده ورود در ربع ساعت») باید پیامِ آدمیزاد بدهد، نه
    /// «سرور جواب نداد (429)».
    /// </summary>
    [Fact]
    public async Task TalasheZiad_Payame_Roshan_Midahad()
    {
        Serve((_, _) => Json(HttpStatusCode.TooManyRequests, "{}"));
        var (link, _) = Link();

        var res = await link.SignInWithPasswordAsync("a@b.com", "ramz-1234");
        Assert.False(res.Ok);
        Assert.Contains("تلاشِ زیاد", res.Why);
        Assert.Equal("429", res.Code);
    }

    /// <summary>
    /// ⚠️ پیامِ خامِ استثنا هیچ‌وقت به کاربر نشان داده نمی‌شود — ممکن است
    /// نشانی، نامِ میزبان یا جزئیاتِ TLS داشته باشد.
    /// </summary>
    [Fact]
    public async Task Payame_Khame_Estesna_Be_Karbar_Nemiresad()
    {
        CloudLink.TestTransport = (_, _) =>
            throw new InvalidOperationException("https://secret-internal-host/x failed");

        var (link, _) = Link();
        var res = await link.SignInWithPasswordAsync("a@b.com", "ramz-1234");

        Assert.False(res.Ok);
        Assert.DoesNotContain("secret-internal-host", res.Why);
        Assert.Equal("error", res.Code);
    }

    /// <summary>
    /// «این راه روی سرور نیست» از روی کدِ خودِ HTTP فهمیده می‌شود، نه از
    /// گشتنِ رشتهٔ «404» در متنِ فارسی — سرورِ به‌روز پیامِ خودش را می‌دهد.
    /// </summary>
    [Fact]
    public async Task Masire_Naboode_NoRoute_Midahad_NeKe_RamzeGhalat()
    {
        Serve((_, _) => Json(HttpStatusCode.NotFound,
            """{"error":{"code":"not_found","message":"این مسیر وجود ندارد"}}"""));

        var (link, _) = Link();
        var res = await link.SignInWithPasswordAsync("a@b.com", "ramz-1234");

        Assert.False(res.Ok);
        Assert.Equal("no_route", res.Code);
        Assert.DoesNotContain("رمز", res.Why);
    }

    // ── ۶) بی نشست، هیچ درخواستی نمی‌رود ─────────────────────────────────

    [Fact]
    public async Task Bi_Neshast_Hich_Darkhasti_Nemirad()
    {
        Serve((_, _) => Json(HttpStatusCode.OK, "{}"));
        var (link, _) = Link();

        var (ok, _, _, _, _) = await link.HomeFromAccountAsync();
        Assert.False(ok);
        Assert.Empty(_hits);
    }

    // ── ۷) قاعده‌های فرم ─────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("haroon")]
    [InlineData("@gmail.com")]          // بی نامِ کاربر
    [InlineData("haroon@")]
    [InlineData("haroon@gmail")]        // بی دامنهٔ کامل
    [InlineData("haroon@gmail.")]
    [InlineData("har oon@gmail.com")]   // فاصله
    [InlineData("a@b@c.com")]           // دو تا @
    public void Emaile_Ghalat_Rad_Mishavad(string bad) => Assert.NotNull(LoginRules.BadEmail(bad));

    [Theory]
    [InlineData("haroon@gmail.com")]
    [InlineData("a.b+tag@sub.example.co")]
    [InlineData("  haroon@gmail.com  ")]   // فاصله‌های دورش خودشان می‌روند
    public void Emaile_Dorost_Migozarad(string good) => Assert.Null(LoginRules.BadEmail(good));

    /// <summary>
    /// ⚠️ همان قاعدهٔ خودِ سرور (<c>pw.checkStrength</c>) — تا امروز برنامه
    /// فقط بلندی را می‌سنجید، پس «۱۲۳۴۵۶۷۸» از این‌جا رد می‌شد و سرور با
    /// <c>weak_password</c> برش می‌گرداند.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("kootah")]
    [InlineData("12345678")]     // فقط عدد
    [InlineData("987654321")]
    [InlineData("password")]
    [InlineData("PassWord")]     // بزرگ و کوچک هم همان است
    public void Ramze_Zaif_Rad_Mishavad(string bad) => Assert.NotNull(LoginRules.WeakPassword(bad));

    [Theory]
    [InlineData("ramz-1234")]
    [InlineData("haroon2026")]
    public void Ramze_Khoob_Migozarad(string good) => Assert.Null(LoginRules.WeakPassword(good));
}
