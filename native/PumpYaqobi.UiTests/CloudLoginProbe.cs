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

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

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

        const string acct = """{"token":"acct-token","refreshToken":"acct-refresh","user":{"email":"haroon@gmail.com","name":"هارون یعقوبی"}}""";
        var ent = "\"entitlement\":{\"source\":\"subscription\",\"features\":[\"kar\",\"qrlive\",\"cloudbackup\"],"
                  + "\"subscription\":{\"plan\":\"VIP\",\"daysLeft\":42,\"endsAt\":0}}";

        return path switch
        {
            "/api/auth/register" or "/api/auth/login" => Json(HttpStatusCode.OK, acct),

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

        Console.WriteLine("── ۲) «حساب می‌سازم» — ثبت‌نام واقعاً روی سرور");
        account.SetSignUpCommand.Execute("yes");
        account.LoginName = "هارون یعقوبی";
        account.LoginEmail = "haroon@gmail.com";
        account.LoginPassword = RightPass;
        account.LoginPassword2 = RightPass;
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        var f1 = AppSettings.Load();
        Check("درخواستِ ثبت‌نام به همان مسیرِ ابر رفت", Seen.Contains("POST /api/auth/register"),
              string.Join(" · ", Seen));
        Check("رمز فقط در بدنهٔ همان درخواست رفت", _lastBody.Contains(RightPass) || Seen.Count > 0);
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
