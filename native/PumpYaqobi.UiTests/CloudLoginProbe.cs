using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «تست بزن ببین لاگین می‌شود، کد بزنی چه می‌شود، حساب ساخته می‌شود یا نه» ══
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸). بندِ ۱۴ در <c>verify</c> فرم و
/// خطاها را می‌سنجد ولی هیچ‌وقت به سرور نمی‌رسد؛ این سنجه همان کار را
/// **تا تهِ خط** می‌برد: ثبت‌نام، ورود با رمزِ غلط و درست، زدنِ کدِ
/// شش‌رقمی، ساخته شدنِ حساب و اشتراک، و باز شدنِ قفل‌های ابری.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- cloudlogin
///
/// ⚠️ **سرورِ ابرِ واقعی در کار نیست و نباید باشد**: هر اجرا یک کدِ الکی به
/// سرورِ اشتراک فرستادن غلط است، و سندباکس هم به `api.vill3n.top` نمی‌رسد.
/// پس یک ابرِ ساختگی در همین فرآیند جواب می‌دهد
/// (<see cref="CloudLink.TestTransport"/>) که **دقیقاً همان شکلِ پاسخی** را
/// می‌دهد که `docs/PUMP-fa.md` می‌گوید — مجوزش هم واقعاً با ES256 امضا
/// می‌شود، پس <see cref="LicenseGuard"/> هم واقعاً سنجیده می‌شود.
/// </summary>
internal static class CloudLoginProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}{(detail is null ? "" : " — " + detail)}");
        if (!ok) _bad++;
    }

    // ── ابرِ ساختگی ─────────────────────────────────────────────────────

    private static ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private static string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    /// <summary>رمزی که ابرِ ساختگی درست می‌داند — هر چیزِ دیگری «غلط» است.</summary>
    private const string RightPass = "ramz-1234";

    /// <summary>کدی که «به ایمیل رفته» — سنجه همان را می‌زند.</summary>
    private const string EmailCode = "424242";

    /// <summary>پس از ساختنِ حساب، همان ایمیل دوباره ثبت نمی‌شود.</summary>
    private static bool _emailTaken;

    /// <summary>
    /// ⚠️ **توکنِ دسترسی مرده است** — یعنی سرور به هر کسی جز دارندهٔ توکنِ
    /// تازه ۴۰۱ می‌دهد. همان چیزی که یک ساعت پس از ورود واقعاً رخ می‌دهد
    /// (<c>ACCESS_TOKEN_TTL_MIN = 60</c>).
    /// </summary>
    private static bool _accessDead;

    /// <summary>توکنی که ابرِ ساختگی پس از تازه‌سازی می‌دهد.</summary>
    private const string FreshToken = "acct-token-2";

    /// <summary>توکنِ تازه‌سازی هم باطل است؟ (نشستِ واقعاً مرده)</summary>
    private static bool _refreshDead;

    /// <summary>روی سرور خروج ثبت شد؟</summary>
    private static bool _loggedOut;

    /// <summary>رمزِ تازه‌ای که از راهِ «فراموشی» گذاشته شد.</summary>
    private static string _newPass = "";

    private static readonly List<string> Seen = new();
    private static string _lastBody = "";
    private static string _serverKey = "";      // کلیدی که ابر می‌دهد (برای سنجشِ جعل)

    private static string Sign(object payload)
    {
        static string B64(byte[] b) =>
            Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var head = B64(Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"JWT"}"""));
        var body = B64(JsonSerializer.SerializeToUtf8Bytes(payload));
        var sig = _key.SignData(Encoding.UTF8.GetBytes($"{head}.{body}"),
                                HashAlgorithmName.SHA256,
                                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{head}.{body}.{B64(sig)}";
    }

    private static string License(string duid, string station)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Sign(new
        {
            iss = CloudConfig.Issuer,
            aud = CloudConfig.Audience,
            duid,
            stn = station,
            nbf = now - 60_000,
            exp = now + 7L * 24 * 3600 * 1000,
            sub_ends = now + 42L * 24 * 3600 * 1000,
            plan_title = "VIP",
            feat = new[] { Entitlements.Kar, Entitlements.QrLive, Entitlements.CloudBackup },
            core = Array.Empty<string>(),
        });
    }

    private static string StartOk() =>
        "{\"ok\":true,\"step\":\"verify\",\"email\":\"haroon@gmail.com\",\"devCode\":\"" + EmailCode + "\"}";

    /// <summary>پلهٔ سه — بی بلیت و بی پذیرشِ شرایط، حسابی ساخته نمی‌شود.</summary>
    private static HttpResponseMessage Complete()
    {
        if (!_lastBody.Contains("tkt-1"))
            return Json(HttpStatusCode.Unauthorized,
                """{"error":{"message":"مهلت ثبت‌نام تمام شد","code":"register_ticket_invalid"}}""");
        if (!_lastBody.Contains("\"accepted\":true"))
            return Json(HttpStatusCode.BadRequest,
                """{"error":{"message":"برای ساختن حساب باید شرایط و ضوابط را بپذیرید","code":"terms_required"}}""");
        _emailTaken = true;
        return Json(HttpStatusCode.Created,
            "{\"created\":true,\"accessToken\":\"acct-token\",\"refreshToken\":\"acct-refresh\","
            + "\"user\":{\"email\":\"haroon@gmail.com\",\"name\":\"هارون یعقوبی\"}}");
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>خروج — سرور هر دو توکن را باطل می‌کند.</summary>
    private static HttpResponseMessage LoggedOut()
    {
        //  ⚠️ توکنِ تازه‌سازی باید واقعاً در بدنه آمده باشد، وگرنه سرور
        //  چیزی برای باطل کردن ندارد و نشستِ نودروزه زنده می‌ماند.
        _loggedOut = _lastBody.Contains("acct-refresh");
        return Json(HttpStatusCode.OK, """{"ok":true}""");
    }

    /// <summary>
    /// رمزِ تازه با کدِ ایمیل. سرورِ واقعی همان‌جا وارد هم می‌کند و همهٔ
    /// نشست‌های قبلی را می‌بندد.
    /// </summary>
    private static HttpResponseMessage ResetPass()
    {
        if (!_lastBody.Contains("\"code\":\"" + EmailCode + "\""))
            return Json(HttpStatusCode.BadRequest,
                """{"error":{"message":"کد درست نیست","code":"otp_bad"}}""");
        try
        {
            using var doc = JsonDocument.Parse(_lastBody);
            _newPass = doc.RootElement.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
        }
        catch { }
        return Json(HttpStatusCode.OK,
            "{\"accessToken\":\"acct-token-3\",\"refreshToken\":\"acct-refresh-3\",\"accessExpiresAt\":"
            + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000)
            + ",\"user\":{\"email\":\"haroon@gmail.com\",\"name\":\"هارون یعقوبی\"}}");
    }

    private static async Task<HttpResponseMessage> Cloud(HttpRequestMessage req, CancellationToken ct)
    {
        var path = req.RequestUri!.AbsolutePath;
        Seen.Add(req.Method + " " + path);
        _lastBody = req.Content is null ? "" : await req.Content.ReadAsStringAsync(ct);

        //  ⚠️ نشانی باید همان نشانیِ قفل‌شده باشد — اگر روزی از تنظیمات
        //  خوانده شود، همین‌جا دیده می‌شود.
        if (req.RequestUri.GetLeftPart(UriPartial.Authority)
            != new Uri(CloudConfig.BaseUrl).GetLeftPart(UriPartial.Authority))
            return Json(HttpStatusCode.BadGateway, """{"error":{"message":"نشانی عوض شده","code":"bad_host"}}""");

        var uid = "";
        var stn = "stn-1";
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(_lastBody) ? "{}" : _lastBody);
            if (doc.RootElement.TryGetProperty("device", out var d)
                && d.TryGetProperty("uid", out var u) && u.ValueKind == JsonValueKind.String)
                uid = u.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("password", out var pw)
                && pw.ValueKind == JsonValueKind.String && pw.GetString() != RightPass
                && path == "/api/auth/login")
                return Json(HttpStatusCode.Unauthorized,
                            """{"error":{"message":"ایمیل یا رمز درست نیست","code":"bad_credentials"}}""");
            if (doc.RootElement.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
                && c.GetString() != "654321" && path.Contains("/device/"))
                return Json(HttpStatusCode.NotFound,
                            """{"error":{"message":"این کد پیدا نشد","code":"bad_code"}}""");
        }
        catch { }

        //  ⚠️ **همان شکلی که سرورِ واقعی می‌دهد**: نشست در `accessToken`
        //  است، نه `token` — با آزمونِ خودِ سرور دیده شد
        //  (`shop/server/test/pump-account.test.js`). اگر این‌جا `token`
        //  بگذاریم، سنجه سبزِ دروغ می‌دهد و باگِ کاربر پیدا نمی‌شود.
        const string acct = """{"accessToken":"acct-token","refreshToken":"acct-refresh","user":{"email":"haroon@gmail.com","name":"هارون یعقوبی"}}""";
        var ent = "\"entitlement\":{\"source\":\"subscription\",\"features\":[\"kar\",\"qrlive\",\"cloudbackup\"],"
                  + "\"subscription\":{\"plan\":\"VIP\",\"daysLeft\":42,\"endsAt\":0}}";

        //  ══ توکنِ منقضی ══════════════════════════════════════════════
        //  ⚠️ همان رفتارِ سرورِ واقعی: هر مسیرِ **حساب‌دار** با توکنِ کهنه
        //  ۴۰۱ِ `invalid_token` می‌گیرد. مسیرهای `device` جدا هستند —
        //  توکنِ دستگاه انقضا ندارد.
        var bearer = req.Headers.TryGetValues("Authorization", out var hv)
            ? hv.First().Replace("Bearer ", "") : "";
        if (_accessDead && path == "/api/pump/me" && bearer != FreshToken)
            return Json(HttpStatusCode.Unauthorized,
                """{"error":{"message":"نشست منقضی شده است، دوباره وارد شوید","code":"invalid_token"}}""");

        return path switch
        {
            //  ══ نشستِ تازه ═══════════════════════════════════════════
            "/api/auth/refresh" => _refreshDead
                ? Json(HttpStatusCode.Unauthorized,
                    """{"error":{"message":"نشست منقضی شده است، دوباره وارد شوید","code":"invalid_token"}}""")
                : Json(HttpStatusCode.OK,
                    "{\"accessToken\":\"" + FreshToken + "\",\"accessExpiresAt\":"
                    + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000) + "}"),

            "/api/auth/logout" => LoggedOut(),

            //  ══ رمزِ فراموش‌شده ══════════════════════════════════════
            //  ⚠️ جوابِ «این ایمیل هست» و «نیست» عمداً یکی است — همان کاری
            //  که سرورِ واقعی می‌کند تا فهرستِ ایمیل‌ها لو نرود.
            "/api/auth/password/forgot" => Json(HttpStatusCode.OK,
                """{"ok":true,"sent":true,"email":"haroon@gmail.com","resendSeconds":60}"""),
            "/api/auth/password/reset" => ResetPass(),

            //  ⛔ درِ یک‌مرحله‌ای عمداً بسته است — همان جوابِ سرورِ واقعی
            "/api/auth/register" => Json(HttpStatusCode.Forbidden,
                """{"error":{"message":"ثبت‌نام بدونِ تأییدِ ایمیل ممکن نیست — برنامه را به‌روز کنید","code":"verification_required"}}"""),

            //  راهِ سه‌پله
            "/api/auth/register/start" => _emailTaken
                ? Json(HttpStatusCode.Conflict,
                    """{"error":{"message":"این ایمیل از قبل ثبت شده است","code":"already_registered"}}""")
                : Json(HttpStatusCode.Created, StartOk()),
            "/api/auth/register/verify" => _lastBody.Contains("\"code\":\"" + EmailCode + "\"")
                ? Json(HttpStatusCode.OK,
                    """{"ok":true,"step":"location","ticket":"tkt-1","terms":{"version":"1","title":"شرایط","sections":[{"title":"یک","body":"متنِ یک"}]}}""")
                : Json(HttpStatusCode.BadRequest,
                    """{"error":{"message":"کد درست نیست","code":"otp_bad"}}"""),
            "/api/auth/register/complete" => Complete(),

            "/api/auth/terms" => Json(HttpStatusCode.OK,
                """{"version":"1","title":"شرایط و ضوابط","sections":[{"title":"یک","body":"متنِ یک"}]}"""),

            "/api/auth/login" => Json(HttpStatusCode.OK, acct),

            "/api/pump/device/activate" => Json(HttpStatusCode.OK,
                "{\"deviceToken\":\"dev-token\",\"publicKey\":\"" + (_serverKey.Length > 0 ? _serverKey : PublicKey)
                + "\",\"license\":\"" + License(uid, stn) + "\",\"station\":{\"id\":\"" + stn + "\"},"
                + ent + "}"),

            "/api/pump/device/redeem" => Json(HttpStatusCode.OK,
                "{\"license\":\"" + License(CloudConfig.DeviceUid(AppSettings.Load()), stn) + "\"," + ent + "}"),

            "/api/pump/device/me" => Json(HttpStatusCode.OK,
                "{\"station\":{\"id\":\"" + stn + "\"}," + ent + "}"),

            "/api/pump/device/license" => Json(HttpStatusCode.OK,
                "{\"license\":\"" + License(CloudConfig.DeviceUid(AppSettings.Load()), stn) + "\"}"),

            "/api/pump/device/access-code" => Json(HttpStatusCode.OK, """{"code":"K7PM3XQ2"}"""),

            //  ⚠️ همان شکلی که سرورِ واقعی می‌دهد و `kar/cloud.js` هم می‌خواند:
            //  نشانیِ خانه زیرِ `home` است، نه تخت.
            "/api/pump/me" => Json(HttpStatusCode.OK,
                "{\"station\":{\"id\":\"stn-1\",\"code\":\"yaqobi\",\"name\":\"پمپ یعقوبی\"},\"role\":\"owner\","
                + "\"home\":{\"url\":\"http://192.168.1.50:4701\",\"readKey\":\"read-key\",\"station\":\"yaqobi\"}}"),

            _ => Json(HttpStatusCode.NotFound, """{"error":{"message":"مسیر نیست","code":"no_route"}}"""),
        };
    }

    // ── خودِ سنجش ──────────────────────────────────────────────────────

    public static int Run()
    {
        //  ابرِ ساختگی، پیش از هر کاری
        CloudLink.TestTransport = Cloud;
        _emailTaken = false;

        //  دفترِ پاک و حسابِ پاک — وگرنه «فعال‌شده»ی اجرای قبلی همه را دروغ می‌کند
        var f = AppSettings.Load();
        f.CloudAccountToken = ""; f.CloudRefreshToken = "";
        f.CloudDeviceToken = ""; f.CloudPublicKey = ""; f.CloudLicense = "";
        f.CloudStationId = ""; f.CloudEmail = ""; f.CloudName = "";
        f.CloudAccessCode = ""; f.EntitledUntil = 0; f.LoginSkipped = false;
        f.Save();
        Entitlements.TestDeny = false;

        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-cloudlogin-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1366, Height = 768 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234"; vm.Lock.SubmitCommand.Execute(null);
        Wait(win, Task.CompletedTask);
        var host = AppHost.Current;

        var account = (AccountSectionViewModel)vm.Sections.First(s => s.Id == "account");
        Wait(win, vm.GoAsync(account));
        for (var i = 0; i < 30; i++) Pump(win);

        Console.WriteLine("── ۱) صفحهٔ ورود، چون هنوز حسابی نیست");
        Check("صفحهٔ ورود اولویت دارد", account.ShowLoginPage && !account.ShowProfilePage);
        Check("گامِ اول، گامِ حساب است", account.StepAccount, "گامِ " + account.LoginStep);

        Console.WriteLine("── ۲) «حساب می‌سازم» — سه پله، همان‌طور که سرور می‌خواهد");
        account.SetSignUpCommand.Execute("yes");
        account.LoginName = "هارون یعقوبی";
        account.LoginEmail = "haroon@gmail.com";
        account.LoginPassword = RightPass;
        account.LoginPassword2 = RightPass;

        //  ⚠️ بی پذیرشِ شرایط، هیچ درخواستی هم نمی‌رود — سرور خودش
        //  `terms_required` می‌دهد، ولی نباید کار به سرور بکشد.
        account.AcceptTerms = false;
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        for (var i = 0; i < 10; i++) Pump(win);
        Check("بی پذیرشِ شرایط، گام جلو نرفت", account.StepAccount
              && account.LoginStatus.Contains("شرایط"), account.LoginStatus);
        Check("و هیچ درخواستی هم به سرور نرفت", !Seen.Contains("POST /api/auth/register/start"));

        account.AcceptTerms = true;
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("⛔ درِ یک‌مرحله‌ای زده نشد (بسته است)", !Seen.Contains("POST /api/auth/register"),
              string.Join(" · ", Seen));
        Check("پلهٔ یک رفت و کد به ایمیل فرستاده شد",
              Seen.Contains("POST /api/auth/register/start") && account.StepEmailCode,
              "گامِ " + account.LoginStep + " · " + account.LoginStatus);
        Check("رمز در بدنهٔ همان درخواست رفت", _lastBody.Contains(RightPass));
        Check("⛔ ولی روی دیسک نماند", !File.ReadAllText(SettingsPath()).Contains(RightPass));
        Check("⛔ و هیچ توکنی هنوز ساخته نشده", AppSettings.Load().CloudAccountToken.Length == 0);

        //  کدِ غلط حساب نمی‌سازد
        account.EmailCode = "111111";
        Wait(win, account.VerifyEmailCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("کدِ ایمیلِ غلط رد شد و در همان گام ماند",
              account.StepEmailCode && AppSettings.Load().CloudAccountToken.Length == 0,
              account.LoginStatus);

        account.EmailCode = EmailCode;
        Wait(win, account.VerifyEmailCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        var f1 = AppSettings.Load();
        Check("پلهٔ دو و سه رفتند", Seen.Contains("POST /api/auth/register/verify")
              && Seen.Contains("POST /api/auth/register/complete"), string.Join(" · ", Seen));
        Check("توکنِ حساب نشست ⇒ حساب ساخته شد", f1.CloudAccountToken == "acct-token", f1.CloudAccountToken);
        Check("نام و ایمیلِ حساب از خودِ سرور آمدند",
              f1.CloudName == "هارون یعقوبی" && f1.CloudEmail == "haroon@gmail.com",
              f1.CloudName + " · " + f1.CloudEmail);
        Check("⛔ رمز روی دیسک نماند", !File.ReadAllText(SettingsPath()).Contains(RightPass));
        Check("⛔ و در حافظهٔ صفحه هم نماند",
              account.LoginPassword.Length == 0 && account.LoginPassword2.Length == 0);
        Check("رفت به گامِ پمپ", account.StepPump, "گامِ " + account.LoginStep);
        Check("و پروفایل نامِ حساب را نشان می‌دهد", account.AccountEmail == "haroon@gmail.com",
              account.UserLine + " · " + account.AccountEmail);

        Console.WriteLine("── ۳) «حساب دارم» — رمزِ غلط رد می‌شود، رمزِ درست وارد");
        account.BackToAccountCommand.Execute(null);
        account.SetSignUpCommand.Execute("no");
        account.LoginPassword = "ramze-ghalat";
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("پیامِ خودِ سرور نشان داده شد، نه پیامِ ساختگی",
              account.LoginStatus.Contains("ایمیل یا رمز درست نیست"), account.LoginStatus);
        Check("و در گامِ یک ماند", account.StepAccount, "گامِ " + account.LoginStep);

        account.LoginPassword = RightPass;
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("با رمزِ درست وارد شد", Seen.Contains("POST /api/auth/login") && account.StepPump,
              "گامِ " + account.LoginStep);
        Check("⚠️ و نشست از فیلدِ accessToken خوانده شد (نه token)",
              AppSettings.Load().CloudAccountToken == "acct-token",
              AppSettings.Load().CloudAccountToken);

        Console.WriteLine("── ۴) کدِ شش‌رقمیِ غلط — هیچ چیزی فعال نمی‌شود");
        account.LoginPump = "پمپ یعقوبی";
        account.LoginLocation = "هرات، جادهٔ کندهار";
        account.LoginCode = "111111";
        Wait(win, account.VerifyCodeCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("کدِ غلط با پیامِ خودِ سرور رد شد", account.LoginStatus.Contains("این کد پیدا نشد"),
              account.LoginStatus);
        Check("⛔ و هیچ توکنِ دستگاهی ساخته نشد",
              string.IsNullOrWhiteSpace(AppSettings.Load().CloudDeviceToken));
        Check("و در گامِ دو ماند", account.StepPump);

        Console.WriteLine("── ۵) کدِ درست — فعال‌سازی، اشتراک، و باز شدنِ قفل‌ها");
        account.LoginCode = "654321";
        Wait(win, account.VerifyCodeCommand.ExecuteAsync(null));
        for (var i = 0; i < 40; i++) Pump(win);
        var f2 = AppSettings.Load();
        Check("درخواستِ فعال‌سازی رفت", Seen.Contains("POST /api/pump/device/activate"));
        Check("توکنِ دستگاه نشست", f2.CloudDeviceToken == "dev-token", f2.CloudDeviceToken);
        Check("کلیدِ عمومیِ سرور قفل شد", f2.CloudPublicKey == PublicKey);
        Check("مجوزِ امضاشده نشست", f2.CloudLicense.Split('.').Length == 3);
        Check("شناسهٔ پمپ از سرور آمد", f2.CloudStationId == "stn-1", f2.CloudStationId);
        Check("نام و لوکیشنِ پمپ ذخیره شدند",
              host.Settings.GetString(SettingsService.StationName) == "پمپ یعقوبی"
              && host.Settings.GetString(SettingsService.StationAddress) == "هرات، جادهٔ کندهار");
        Check("و نامِ پمپ همراهِ همان درخواست به ابر رفت",
              Seen.Contains("POST /api/pump/device/activate"));

        //  ⚠️ همان چیزی که <b>کاربر</b> می‌بیند، نه متغیرهای درونی
        Check("گامِ «تمام» و خودِ پروفایل دیده می‌شود",
              account.StepDone && account.ShowProfilePage && !account.ShowLoginPage);
        Check("کارتِ اشتراک پلن را نشان می‌دهد", account.SubPlanText.Contains("VIP"), account.SubPlanText);
        Check("و روزهای مانده را", account.SubDaysText.Length > 0 && account.VipActive,
              account.SubDaysText);
        Check("دکمهٔ سربرگ هم VIP شد", account.PillText.Contains("VIP"), account.PillText);

        //  خودِ مجوز: امضا، دستگاه، پمپ — با همان سنجهٔ برنامه
        var check = LicenseGuard.Check(f2.CloudLicense, f2.CloudPublicKey,
                                       CloudConfig.DeviceUid(f2), f2.CloudStationId,
                                       DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Check("مجوز از سنجشِ خودِ برنامه سالم بیرون آمد", check.Valid, check.Reason);
        Check("هر سه کارِ ابری باز شدند",
              Entitlements.Allows(Entitlements.Kar)
              && Entitlements.Allows(Entitlements.QrLive)
              && Entitlements.Allows(Entitlements.CloudBackup),
              account.AccessKarText + " · " + account.AccessQrText + " · " + account.AccessBackupText);
        Check("و پشتیبانی — که هیچ‌وقت قفل نمی‌شود", Entitlements.Allows(Entitlements.Support));

        //  ⚠️ **همان باگی که این سنجه گرفت**: تا دیروز همهٔ این‌ها فقط در
        //  حافظه می‌نشستند و روی دیسک نمی‌رفتند، پس با بسته و باز شدنِ
        //  برنامه کاربر باید دوباره کدِ شش‌رقمی می‌زد.
        var fresh = new CloudLink(AppSettings.Load(), () => Task.CompletedTask);
        Check("⭐ با بسته و باز شدنِ برنامه هم فعال می‌ماند (از روی دیسک)",
              fresh.Activated && fresh.Verify().Valid, fresh.Verify().Reason);

        Console.WriteLine("── ۶) کدِ اپِ کارمندان از ابر می‌آید");
        Wait(win, account.LoadAccessCodeCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("کد گرفته شد و دیده می‌شود", account.HasAccessCode && account.AccessCodeDisplay.Length > 0,
              account.AccessCodeDisplay);

        Console.WriteLine("── ۷) نشانیِ سرورِ خانگی خودش از حساب می‌آید (کادری در کار نیست)");
        Wait(win, account.PullHomeCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("از /api/pump/me گرفته شد", Seen.Contains("GET /api/pump/me"));
        //  ⚠️ نشانیِ سرورِ خانگی در تنظیماتِ **دفتر** می‌نشیند
        //  (`SettingsService`)، نه در `settings.json`ِ ابر — این دو یکی
        //  نیستند و نباید قاطی شوند.
        Check("و در تنظیماتِ دفتر نشست (نه در تنظیماتِ ابر)",
              host.Settings.GetString(SettingsService.ServerUrl).Contains("192.168.1.50")
              && host.Settings.GetString(SettingsService.SyncCode) == "read-key",
              host.Settings.GetString(SettingsService.ServerUrl));
        Check("و پمپِ وصل‌شده نشان داده شد", account.StationLine.Contains("yaqobi"),
              account.StationLine);

        Console.WriteLine("── ۸) سرورِ جعلی با کلیدِ دیگر — همان قفلِ ضدِ کرک");
        _serverKey = Convert.ToBase64String(
            ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportSubjectPublicKeyInfo());
        //  ⚠️ یک برنامهٔ «تازه» (همان تنظیماتِ روی دیسک، بی توکنِ دستگاه) تا
        //  مسیرِ فعال‌سازی دوباره برود — همان کاری که یک کرکِ واقعی می‌کند:
        //  سرورِ خودش را بالا می‌آورد و کلیدِ خودش را می‌دهد.
        var f3 = AppSettings.Load();
        f3.CloudDeviceToken = "";
        var rogue = new CloudLink(f3, () => { f3.Save(); return Task.CompletedTask; });
        CloudResult res = default!;
        Wait(win, Task.Run(async () => res = await rogue.ActivateAsync("654321", "پمپ یعقوبی", "هرات")));
        Check("کلیدِ متفاوت رد شد (key_mismatch)", !res.Ok && res.Code == "key_mismatch",
              res.Code + " · " + res.Why);
        Check("⛔ و کلیدِ قفل‌شده عوض نشد", AppSettings.Load().CloudPublicKey == PublicKey);
        Check("⛔ و مجوزِ جعلی هیچ‌وقت ذخیره نشد",
              string.IsNullOrWhiteSpace(f3.CloudDeviceToken));
        _serverKey = "";

        // ══ ۹) توکنِ یک‌ساعته منقضی می‌شود — و خودش تازه می‌شود ═══════════
        //
        //  ⛔ **همان باگی که برنامه را یک‌ساعته خاموش می‌کرد.** سرور به توکنِ
        //  دسترسی دقیقاً یک ساعت عمر می‌دهد و `refreshToken` را نود روز.
        //  برنامه `refreshToken` را ذخیره می‌کرد ولی **هیچ‌جا نمی‌خواندش**،
        //  پس یک ساعت پس از ورود `/api/pump/me` و کدِ اپِ کارمندان بی‌صدا
        //  می‌مردند و دیگر هیچ‌وقت درست نمی‌شدند — در حالی که پروفایل
        //  همچنان «وارد شده‌اید» می‌گفت.
        Console.WriteLine("── ۹) توکنِ منقضی — خودش تازه می‌شود و کاربر هیچ نمی‌فهمد");
        _accessDead = true;
        Seen.Clear();
        host.Settings.Set(SettingsService.ServerUrl, "");
        Wait(win, account.PullHomeCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);

        Check("به ۴۰۱ خورد و همان‌جا نشستِ تازه گرفت", Seen.Contains("POST /api/auth/refresh"));
        Check("و دوباره زد — نه یک بار، دو بار",
              Seen.Count(x => x == "GET /api/pump/me") == 2,
              Seen.Count(x => x == "GET /api/pump/me") + " بار");
        Check("⭐ و کار **گرفت** — کاربر هیچ خطایی ندید",
              account.StationLine.Contains("yaqobi"), account.StationLine);
        Check("توکنِ تازه روی دیسک نشست",
              AppSettings.Load().CloudAccountToken == FreshToken);
        Check("و هنوز وارد است", account.SignedIn);
        Check("⚠️ و حلقه نزد — فقط یک بار تازه کرد",
              Seen.Count(x => x == "POST /api/auth/refresh") == 1);
        _accessDead = false;

        // ══ ۱۰) توکن‌ها روی دیسک خام نیستند ══════════════════════════════
        Console.WriteLine("── ۱۰) توکن‌ها روی دیسک خام نیستند");
        var onDisk = File.ReadAllText(SettingsPath());
        Check("⛔ توکنِ حساب در متنِ فایل پیدا نمی‌شود", !onDisk.Contains(FreshToken));
        Check("⛔ توکنِ تازه‌سازی هم نه", !onDisk.Contains("acct-refresh"));
        Check("⛔ توکنِ دستگاه هم نه", !onDisk.Contains("dev-token"));
        Check("و کلیدِ خامِ کهنه دیگر در فایل نوشته نمی‌شود",
              !onDisk.Contains("\"CloudAccountToken\":"));
        Check("⭐ ولی خودِ برنامه همان‌ها را می‌خواند",
              AppSettings.Load().CloudDeviceToken == "dev-token"
              && AppSettings.Load().CloudAccountToken == FreshToken);

        // ══ ۱۱) خروج — روی سرور هم، نه فقط این‌جا ════════════════════════
        //
        //  ⛔ پیش از این خروج **فقط محلی** بود: توکنِ دسترسی و توکنِ
        //  تازه‌سازی روی سرور زنده می‌ماندند (تازه‌سازی تا نود روز)، پس
        //  «خروج»ِ کاربر جلوی کسی را که آن رشته را برداشته بود نمی‌گرفت.
        Console.WriteLine("── ۱۱) خروج از حساب — نشست روی سرور هم باطل می‌شود");
        Seen.Clear();
        _loggedOut = false;
        //  توکنِ تازه‌سازی همان `acct-refresh`ِ ورود است
        Wait(win, account.SignOutCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);

        Check("به سرور خبر داد", Seen.Contains("POST /api/auth/logout"));
        Check("⭐ و توکنِ تازه‌سازی را هم فرستاد تا باطل شود", _loggedOut);
        var afterOut = AppSettings.Load();
        Check("توکن‌های نشست پاک شدند",
              afterOut.CloudAccountToken.Length == 0 && afterOut.CloudRefreshToken.Length == 0);
        Check("⛔ ولی اشتراکِ دستگاه دست نخورد", afterOut.CloudDeviceToken == "dev-token");
        Check("و صفحه برگشت به همان صفحهٔ ورود، نه پروفایلِ خالی",
              account.ShowLoginPage && account.StepAccount, "گامِ " + account.LoginStep);

        // ══ ۱۲) رمزم را فراموش کرده‌ام ═══════════════════════════════════
        //
        //  ⚠️ این راه روی سرور از قبل بود (`/api/auth/password/forgot` و
        //  `/password/reset`) و فقط برنامهٔ نیتیو هیچ‌وقت صدایش نزده بود —
        //  پس کسی که رمزش را گم می‌کرد هیچ راهی جز ساختنِ حسابِ تازه نداشت.
        Console.WriteLine("── ۱۲) رمزِ فراموش‌شده — کد به ایمیل، رمزِ تازه، و ورود");
        Seen.Clear();
        account.OpenForgotCommand.Execute(null);
        Pump(win);
        Check("صفحهٔ بازیابی باز شد و تمامِ پنجره را گرفت",
              account.StepForgot && account.ShowLoginPage);

        account.LoginEmail = "نه-ایمیل";
        Wait(win, account.SendResetCodeCommand.ExecuteAsync(null));
        Check("ایمیلِ غلط رد شد و هیچ درخواستی نرفت",
              !Seen.Contains("POST /api/auth/password/forgot") && account.LoginStatus.StartsWith("❌"),
              account.LoginStatus);

        account.LoginEmail = "haroon@gmail.com";
        Wait(win, account.SendResetCodeCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("کد فرستاده شد", Seen.Contains("POST /api/auth/password/forgot"));
        Check("⚠️ و پیام نگفت این ایمیل حساب دارد یا نه",
              account.LoginStatus.Contains("اگر"), account.LoginStatus);
        Check("نیمهٔ دومِ فرم باز شد", account.ResetSent);

        //  رمزِ ضعیف — همان قاعدهٔ خودِ سرور، پیش از رفتن
        account.ResetCode = EmailCode;
        account.ResetPass = "12345678"; account.ResetPass2 = "12345678";
        Wait(win, account.ResetPasswordCommand.ExecuteAsync(null));
        Check("رمزِ «فقط عدد» رد شد و به سرور نرسید",
              !Seen.Contains("POST /api/auth/password/reset") && account.LoginStatus.Contains("عدد"),
              account.LoginStatus);

        //  دو رمزِ ناهم‌خوان
        account.ResetPass = "ramz-tazeh-1"; account.ResetPass2 = "ramz-tazeh-2";
        Wait(win, account.ResetPasswordCommand.ExecuteAsync(null));
        Check("دو رمزِ ناهم‌خوان رد شدند",
              !Seen.Contains("POST /api/auth/password/reset"), account.LoginStatus);

        //  و حالا درست
        account.ResetPass2 = "ramz-tazeh-1";
        Wait(win, account.ResetPasswordCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        Check("رمزِ تازه نشست", Seen.Contains("POST /api/auth/password/reset"));
        Check("و همان رمز به سرور رفت", _newPass == "ramz-tazeh-1");
        Check("⭐ و همان‌جا وارد شد — رمز را دوباره نپرسید", account.SignedIn);
        Check("⛔ رمزِ تازه روی دیسک ننشست",
              !File.ReadAllText(SettingsPath()).Contains("ramz-tazeh-1"));
        Check("⛔ و در حافظهٔ صفحه هم نماند",
              account.ResetPass.Length == 0 && account.ResetPass2.Length == 0);
        Check("رفت به گامِ پمپ", account.LoginStep == 3, "گامِ " + account.LoginStep);

        // ══ ۱۳) نشستِ واقعاً مرده ⇒ برنامه اعتراف می‌کند ══════════════════
        //
        //  ⚠️ اگر خودِ `refreshToken` هم باطل باشد، پروفایل نباید تا ابد
        //  «وارد شده‌اید» بگوید در حالی که هیچ دکمه‌ای کار نمی‌کند.
        Console.WriteLine("── ۱۳) نشستِ واقعاً مرده — برنامه دروغ نمی‌گوید");
        _accessDead = true; _refreshDead = true;
        Seen.Clear();
        Wait(win, account.PullHomeCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);

        Check("تلاشِ تازه‌سازی رفت و رد شد", Seen.Contains("POST /api/auth/refresh"));
        Check("⭐ و نشست پاک شد — دیگر «وارد شده‌اید» نمی‌گوید",
              AppSettings.Load().CloudAccountToken.Length == 0);
        Check("⛔ ولی اشتراکِ دستگاه باز هم دست نخورد",
              AppSettings.Load().CloudDeviceToken == "dev-token");
        _accessDead = false; _refreshDead = false;

        // ══ ۱۴) قطعیِ اینترنت هیچ‌کس را بیرون نمی‌اندازد ══════════════════
        Console.WriteLine("── ۱۴) بی‌اینترنت — نشست پاک نمی‌شود");
        var f4 = AppSettings.Load();
        f4.CloudAccountToken = "acct-token"; f4.CloudRefreshToken = "acct-refresh";
        f4.CloudAccessExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000;  // منقضی
        f4.Save();
        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("شبکه نیست");
        var offline = new CloudLink(AppSettings.Load(), () => Task.CompletedTask);
        (bool Ok, string Url, string Key, string Stn, string Why) off = default;
        Wait(win, Task.Run(async () => off = await offline.HomeFromAccountAsync()));
        Check("نشد، و دلیلش هم روشن است", !off.Ok && off.Why.Contains("اینترنت"), off.Why);
        Check("⭐ ولی نشست سرِ جایش ماند — یک قطعیِ مودم کسی را بیرون نمی‌اندازد",
              AppSettings.Load().CloudAccountToken == "acct-token"
              && AppSettings.Load().CloudRefreshToken == "acct-refresh");
        CloudLink.TestTransport = Cloud;

        CloudLink.TestTransport = null;
        Console.WriteLine();
        Console.WriteLine(_bad == 0
            ? "✅ ثبت‌نام، ورود، کدِ شش‌رقمی و اشتراک — همه با سرورِ ساختگی تا تهِ کار رفتند"
            : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static string SettingsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "PumpYaqobi", "settings.json");

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Wait(Window win, Task t)
    {
        for (var i = 0; i < 4000 && !t.IsCompleted; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(2);
        }
        Dispatcher.UIThread.RunJobs();
        if (t.IsFaulted) throw t.Exception!;
    }
}
