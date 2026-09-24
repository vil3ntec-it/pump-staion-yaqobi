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
        //  ⚠️ همان قاعده: `Reach` استاتیک است و این کلاس پرش می‌کند، پس
        //  بی این خط بندِ ۱ی `CloudReachTests` — که هم‌کالکشن است — به
        //  ترتیبِ اجرا بند می‌شد و **گاهی** سرخ می‌داد.
        CloudLink.ResetReach();
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
    ///
    /// ⚠️ دستگاهِ <b>فعال‌شده</b> سنجیده می‌شود (توکن و مجوز دارد)، چون
    /// همان چیزی است که این قفل از آن محافظت می‌کند. دستگاهی که هیچ‌کدام
    /// را ندارد بندِ بعدی را دارد.
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
            s.CloudDeviceToken = "pd_real";          // و دستگاه فعال است
            s.CloudLicense = "lic.real";
        });

        var r = await link.BindAsync();

        Assert.False(r.Ok);
        Assert.Equal("key_mismatch", r.Code);
        var f = AppSettings.Load();
        Assert.Equal("pk-1", f.CloudPublicKey);
        Assert.Equal("pd_real", f.CloudDeviceToken);
        Assert.Equal("lic.real", f.CloudLicense);
    }

    /// <summary>
    /// ⛔ <b>کلیدی که چیزی را نگه نمی‌دارد، قفل نیست</b> — و این تنها
    /// استثنای TOFU است، فقط در <c>BindAsync</c>.
    ///
    /// نصبی که کلید را قفل کرده ولی نه توکنِ دستگاه دارد و نه مجوز، چیزی
    /// برای محافظت ندارد: آن کلید زباله‌ای از یک تلاشِ ناتمام است. بی این
    /// استثنا، همان نصب <b>برای همیشه</b> <c>key_mismatch</c> می‌گرفت،
    /// دستگاه هیچ‌وقت بند نمی‌شد و دورهٔ آزمایشیِ ۳۰ روزه هم هیچ‌وقت
    /// نمی‌رسید — همان حلقه‌ای که صاحب ریپو در ۱۴۰۵/۰۷/۱۱ با عکس گزارشش
    /// کرد.
    ///
    /// ⚠️ و TOFU همان لحظه از نو بسته می‌شود: کلیدِ تازه می‌نشیند.
    /// </summary>
    [Fact]
    public async Task Bind_DastgaheKhali_KelidRa_DobareGhofl_Mikonad()
    {
        Serve((path, _) => path == "/api/pump/device/bind"
            ? Json(HttpStatusCode.Created, """
                {"deviceToken":"pd_new","station":{"id":"stn-1"},
                 "license":"lic.new","publicKey":"pk-2"}
                """)
            : Json(HttpStatusCode.NotFound, "{}"));

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
            s.CloudPublicKey = "pk-1";   // کلیدِ جامانده، بی توکن و بی مجوز
        });

        var r = await link.BindAsync();

        Assert.True(r.Ok);
        var f = AppSettings.Load();
        Assert.Equal("pk-2", f.CloudPublicKey);      // از نو قفل شد
        Assert.Equal("pd_new", f.CloudDeviceToken);
        Assert.Equal("lic.new", f.CloudLicense);
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
        Serve((path, _) => path == "/api/health"
            ? Json(HttpStatusCode.OK, """{"ok":true,"version":"2.5.5"}""")
            : Json(HttpStatusCode.NotFound,
                """{"error":{"code":"not_found","message":"این مسیر وجود ندارد"}}"""));

        var (link, _) = Link();
        var res = await link.SignInWithPasswordAsync("a@b.com", "ramz-1234");

        Assert.False(res.Ok);
        Assert.Equal("no_route", res.Code);
        Assert.DoesNotContain("رمز", res.Why);
    }

    // ── ۵ب) ۴۰۴ی که «سرور جواب نداد» نیست ───────────────────────────────
    //
    //  ⛔ گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۰۱): زیرِ «ساختنِ حساب و ادامه»
    //  نوشته می‌شد «سرور جواب نداد (404)». آن پیام هم **غلط** بود (سرور
    //  جواب داده بود) و هم **بی‌فایده** (نمی‌گفت چه کار کند). حالا برنامه
    //  خودش از `/api/health` می‌پرسد و همان دو حالِ واقعی را از هم جدا
    //  می‌کند.

    /// <summary>
    /// سرورِ حساب بالا است ولی مسیرِ ثبت‌نام را ندارد ⇒ «سرور را به‌روز کنید»،
    /// با نسخهٔ خودش. ⛔ نه «سرور جواب نداد» و نه «برنامه را به‌روز کنید».
    /// </summary>
    [Fact]
    public async Task SabteNam_404_MigooyadSarvareHesabRaBeRuzKonid()
    {
        Serve((path, _) => path == "/api/health"
            ? Json(HttpStatusCode.OK, """{"ok":true,"version":"2.4.0"}""")
            : new HttpResponseMessage(HttpStatusCode.NotFound)
              { Content = new StringContent("<html>404</html>", System.Text.Encoding.UTF8, "text/html") });

        var (link, _) = Link();
        var res = await link.RegisterStartAsync("haroon", "a@b.com", "ramz-1234");

        Assert.False(res.Ok);
        Assert.Equal("no_route", res.Code);
        Assert.DoesNotContain("سرور جواب نداد", res.Why);
        Assert.Contains("به‌روز", res.Why);
        Assert.Contains("2.4.0", res.Why);                 // نسخهٔ واقعیِ سرور
        Assert.Contains("/api/health", _hits);             // واقعاً پرسید
    }

    /// <summary>
    /// همان ۴۰۴، ولی خودِ سرور هم جواب نمی‌دهد ⇒ جملهٔ دیگری لازم است:
    /// این‌جا به‌روزرسانیِ سرور کاری نمی‌کند، سرویس/دامنه مشکل دارد.
    /// </summary>
    [Fact]
    public async Task SabteNam_404_Va_HealthKhamush_MigooyadNaresidim()
    {
        Serve((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
            { Content = new StringContent("<html>404</html>", System.Text.Encoding.UTF8, "text/html") });

        var (link, _) = Link();
        var res = await link.RegisterStartAsync("haroon", "a@b.com", "ramz-1234");

        Assert.False(res.Ok);
        Assert.Equal("no_server", res.Code);
        Assert.DoesNotContain("سرور جواب نداد", res.Why);
        Assert.DoesNotContain("به‌روز", res.Why);
    }

    /// <summary>
    /// ⚠️ پیامِ ۴۰۴ هیچ نام یا نشانیِ میزبانی نمی‌برد — همان قاعدهٔ همیشه.
    /// </summary>
    [Fact]
    public async Task Payame_404_NameMizban_RaNemibarad()
    {
        Serve((path, _) => path == "/api/health"
            ? Json(HttpStatusCode.OK, """{"ok":true,"version":"2.5.5"}""")
            : Json(HttpStatusCode.NotFound, "{}"));

        var (link, _) = Link();
        var res = await link.RegisterStartAsync("haroon", "a@b.com", "ramz-1234");

        Assert.False(res.Ok);
        Assert.DoesNotContain("vill3n", res.Why);
        Assert.DoesNotContain("http", res.Why);
    }

    /// <summary>
    /// ⛔ و ۴۰۴ِ مسیرهای دیگرِ حساب هم همین‌طور — نه فقط ثبت‌نام. رمزِ
    /// فراموش‌شده هم روی سرورِ کهنه همین حال را داشت.
    /// </summary>
    [Fact]
    public async Task RamzeFaramushShode_404_HamanJomleRaMidahad()
    {
        Serve((path, _) => path == "/api/health"
            ? Json(HttpStatusCode.OK, """{"ok":true,"version":"2.5.5"}""")
            : Json(HttpStatusCode.NotFound, "{}"));

        var (link, _) = Link();
        var res = await link.ForgotPasswordAsync("a@b.com");

        Assert.False(res.Ok);
        Assert.Equal("no_route", res.Code);
        Assert.DoesNotContain("سرور جواب نداد", res.Why);
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

    // ── ۶ب) پمپِ حسابِ تازه — «تا آخرِ مرحله‌ها» ──────────────────────────

    /*
     *  ⛔ **همان جایی که هر کاربرِ تازه‌ای گیر می‌کرد.**
     *
     *  با سرورِ **واقعی** سنجیده شد، نه از روی کد: ثبت‌نامِ سه‌پله‌ای و ورود
     *  هر دو ۲۰۰/۲۰۱ می‌دادند، و بعد `POST /api/pump/device/bind` ۴۰۴ِ
     *  `no_station` می‌گرفت — چون حسابِ تازه **هیچ پمپی ندارد** و
     *  `/api/pump/me` می‌گفت `station: null`. نتیجه: نه توکنِ دستگاه، نه
     *  مجوز، نه کدِ اپِ کارمندان. تنها راهِ ساختنِ پمپ کدِ شش‌رقمی بود —
     *  همان کدی که صاحب ریپو صریح گفت در کار نیست.
     */

    /// <summary>حسابِ بی‌پمپ: پمپ ساخته می‌شود و بعد بند.</summary>
    [Fact]
    public async Task HesabeBiPomp_PompRaMisazad_VaBandMishavad()
    {
        var made = false;
        Serve((path, req) =>
        {
            if (path == "/api/pump/me")
                return Json(HttpStatusCode.OK, made
                    ? """{"station":{"id":"stn-new","code":"p1"},"role":"owner"}"""
                    : """{"station":null,"entitlement":null}""");
            if (path == "/api/pump")
            {
                made = true;
                return Json(HttpStatusCode.Created, """{"station":{"id":"stn-new","code":"p1"}}""");
            }
            if (path == "/api/pump/device/bind")
                return Json(HttpStatusCode.Created, """
                    {"deviceToken":"pd-new","station":{"id":"stn-new","code":"p1"},
                     "license":"","publicKey":"pk-1","entitlement":{"source":"free"}}
                    """);
            return Json(HttpStatusCode.NotFound, "{}");
        });

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
        });

        var r = await link.EnsureStationAsync("پمپ یعقوبی");

        Assert.True(r.Ok, r.Why);
        Assert.Contains("/api/pump", _hits);
        Assert.Contains("/api/pump/device/bind", _hits);
        //  ⚠️ از **دیسک**، نه از شیءِ در حافظه
        var f = AppSettings.Load();
        Assert.Equal("pd-new", f.CloudDeviceToken);
        Assert.Equal("stn-new", f.CloudStationId);
    }

    /// <summary>
    /// ⛔ حسابی که از قبل پمپ دارد، پمپِ دومی نمی‌سازد — «هر حساب یک پمپ»
    /// قاعدهٔ خودِ سرور است و ساختنِ بی‌خبر یعنی حسابی که دیگر نمی‌تواند به
    /// پمپِ واقعی‌اش بپیوندد.
    /// </summary>
    [Fact]
    public async Task HesabiKePompDarad_PompeDovom_Nemisazad()
    {
        Serve((path, _) => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK,
                """{"station":{"id":"stn-7","code":"yaqobi"},"role":"owner"}"""),
            "/api/pump/device/bind" => Json(HttpStatusCode.Created,
                """{"deviceToken":"pd-7","station":{"id":"stn-7"},"publicKey":"pk-1"}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
        });

        var r = await link.EnsureStationAsync("هر چه");

        Assert.True(r.Ok, r.Why);
        Assert.DoesNotContain("/api/pump", _hits.Where(h => h.Length == "/api/pump".Length));
        Assert.Contains("/api/pump/device/bind", _hits);
    }

    /// <summary>
    /// ⚠️ <c>already_member</c> خطا نیست: یعنی پمپ همین حالا هست (دو کلیک،
    /// یا دستگاهِ دیگری که زودتر ساختش). بند شدن باید ادامه پیدا کند.
    /// </summary>
    [Fact]
    public async Task AlreadyMember_Khata_Nist_VaBandShodan_Edame_Midahad()
    {
        Serve((path, _) => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, """{"station":null}"""),
            "/api/pump" => Json(HttpStatusCode.Conflict,
                """{"error":{"code":"already_member","message":"این حساب از قبل عضو یک پمپ است"}}"""),
            "/api/pump/device/bind" => Json(HttpStatusCode.Created,
                """{"deviceToken":"pd-x","station":{"id":"stn-x"},"publicKey":"pk-1"}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
        });

        var r = await link.EnsureStationAsync("پمپ");

        Assert.True(r.Ok, r.Why);
        Assert.Equal("pd-x", AppSettings.Load().CloudDeviceToken);
    }

    /// <summary>بی حساب، هیچ درخواستی زده نمی‌شود.</summary>
    [Fact]
    public async Task EnsureStation_BiHesab_HichDarkhasti_Nemizanad()
    {
        Serve((_, _) => Json(HttpStatusCode.OK, "{}"));

        var (link, _) = Link();
        var r = await link.EnsureStationAsync("پمپ");

        Assert.False(r.Ok);
        Assert.Equal("no_account", r.Code);
        Assert.Empty(_hits);
    }

    /// <summary>
    /// ⛔ قفلِ کلیدِ عمومی این‌جا هم زنده است: ساختنِ پمپ آن را دور نمی‌زند.
    /// </summary>
    [Fact]
    public async Task EnsureStation_KelideDigar_Ra_RadMikonad()
    {
        Serve((path, _) => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, """{"station":{"id":"stn-1"}}"""),
            "/api/pump/device/bind" => Json(HttpStatusCode.Created,
                """{"deviceToken":"pd-bad","station":{"id":"stn-1"},"publicKey":"pk-OTHER"}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });

        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1";
            s.CloudAccessExpiresAt = InAnHour;
            s.CloudPublicKey = "pk-1";
            //  ⚠️ دستگاهِ **فعال‌شده** — همان چیزی که قفل از آن محافظت
            //  می‌کند. دستگاهِ خالی استثنای خودش را دارد
            //  (‎Bind_DastgaheKhali_KelidRa_DobareGhofl_Mikonad‎).
            s.CloudDeviceToken = "pd_real";
            s.CloudLicense = "lic.real";
        });

        var r = await link.EnsureStationAsync("پمپ");

        Assert.False(r.Ok);
        Assert.Equal("key_mismatch", r.Code);
        Assert.Equal("pk-1", AppSettings.Load().CloudPublicKey);
        Assert.Equal("pd_real", AppSettings.Load().CloudDeviceToken);
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

    // ── ۹) دو نمونه، یک نشست — تازه‌سازیِ یگانه در کلِ پروسه ───────────────

    /// <summary>
    /// ⛔ <b>باگِ «گاهی بی‌دلیل از حساب بیرون می‌افتم».</b> حلقهٔ پس‌زمینه و
    /// صفحهٔ پروفایل هر کدام <see cref="CloudLink"/>ِ خودشان را با عکسِ
    /// جداگانه‌ای از تنظیمات دارند، و سرور توکنِ تازه‌سازی را <b>می‌چرخاند</b>
    /// و کهنه را دوباره نمی‌پذیرد. پیش از این، نمونهٔ دوم با توکنِ کهنه ۴۰۱
    /// می‌گرفت و نشستِ سالمِ نمونهٔ اول را از دیسک پاک می‌کرد.
    ///
    /// حالا هر دو با نشستِ <b>زنده</b> تمام می‌کنند و فقط <b>یک</b> تازه‌سازی
    /// زده می‌شود.
    /// </summary>
    [Fact]
    public async Task DoNemune_BaYekNeshast_HichKodam_DigariRa_BiroonNemiandazad()
    {
        var gate = new object();
        var current = "r0";
        var used = new HashSet<string>();
        var valid = new HashSet<string>();
        var n = 0;
        var refreshes = 0;

        CloudLink.TestTransport = async (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            lock (_hits) _hits.Add(path);
            if (path == "/api/auth/refresh")
            {
                var body = await req.Content!.ReadAsStringAsync();
                var rt = JsonDocument.Parse(body).RootElement.GetProperty("refreshToken").GetString();
                //  کمی مکث تا دو درخواست واقعاً روی هم بیفتند
                await Task.Delay(40);
                lock (gate)
                {
                    refreshes++;
                    if (rt != current || used.Contains(rt!))
                        return Json(HttpStatusCode.Unauthorized,
                            """{"error":{"code":"invalid_refresh","message":"نشست منقضی شده است"}}""");
                    used.Add(rt!);
                    n++;
                    current = "r" + n;
                    valid.Add("a" + n);
                    return Json(HttpStatusCode.OK,
                        $$"""{"accessToken":"a{{n}}","refreshToken":"{{current}}","accessExpiresAt":{{InAnHour}}}""");
                }
            }
            if (path == "/api/pump/me")
            {
                lock (gate)
                    return valid.Contains(Bearer(req) ?? "")
                        ? Json(HttpStatusCode.OK, """{"station":{"id":"stn-9"}}""")
                        : Json(HttpStatusCode.Unauthorized,
                               """{"error":{"code":"invalid_token","message":"نشست منقضی شده است"}}""");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        };

        //  هر دو از **یک** دیسک و با توکنِ دسترسیِ منقضی شروع می‌کنند
        var seed = AppSettings.Load();
        seed.CloudAccountToken = "a0";
        seed.CloudRefreshToken = "r0";
        seed.CloudAccessExpiresAt = AnHourAgo;
        seed.Save();

        var f1 = AppSettings.Load();
        var f2 = AppSettings.Load();
        var one = new CloudLink(f1, () => { f1.Save(); return Task.CompletedTask; });
        var two = new CloudLink(f2, () => { f2.Save(); return Task.CompletedTask; });

        var results = await Task.WhenAll(one.HasStationAsync(), two.HasStationAsync());

        Assert.True(results[0]);
        Assert.True(results[1]);
        Assert.Equal(1, refreshes);                     // یک چرخش، نه دو
        Assert.True(one.SignedIn);
        Assert.True(two.SignedIn);

        //  ⛔ و نشستِ روی دیسک زنده است — پاک نشده
        var disk = AppSettings.Load();
        Assert.Equal(current, disk.CloudRefreshToken);
        Assert.Contains(disk.CloudAccountToken, valid);
    }

    /// <summary>
    /// ⛔ «خروج» توکنِ <b>روی دیسک</b> را باطل می‌کند، نه عکسِ کهنهٔ همین
    /// نمونه — وگرنه توکنِ زنده روی سرور نود روز می‌ماند.
    /// </summary>
    [Fact]
    public async Task Khoruj_TokeneRooyeDisk_RaBatelMikonad()
    {
        string? revoked = null;
        Serve((path, req) =>
        {
            if (path == "/api/auth/logout")
                revoked = JsonDocument.Parse(req.Content!.ReadAsStringAsync().Result)
                                      .RootElement.GetProperty("refreshToken").GetString();
            return Json(HttpStatusCode.OK, "{}");
        });

        var stale = AppSettings.Load();
        stale.CloudAccountToken = "a0";
        stale.CloudRefreshToken = "r0";
        stale.Save();
        var link = new CloudLink(stale, () => { stale.Save(); return Task.CompletedTask; });

        //  نمونهٔ دیگری همین حالا چرخاند
        var other = AppSettings.Load();
        other.CloudAccountToken = "a1";
        other.CloudRefreshToken = "r1";
        other.Save();

        await link.SignOutAsync();

        Assert.Equal("r1", revoked);
        Assert.Equal("", AppSettings.Load().CloudRefreshToken);
    }
}
