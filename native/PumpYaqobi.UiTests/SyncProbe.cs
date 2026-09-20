using System.Net;
using System.Net.Http;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ شش خانهٔ کد و چراغِ نوارِ پایین ═══════════════════════════════════════
///
/// بندهای ۴ و ۱۳ی پرامپتِ ۲۲، با پنجرهٔ واقعی و کلیدِ واقعی:
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- syncui
///
/// ⚠️ <b>ابرِ ساختگی، نه شبکهٔ واقعی</b>: همان
/// <c>CloudLink.TestTransport</c>ی که <c>cloudlogin</c> از آن استفاده
/// می‌کند. پس این سنجه اینترنت نمی‌خواهد و به سرورِ واقعیِ اشتراک هیچ
/// درخواستی نمی‌زند.
///
/// ⚠️ و <c>SyncEngine.Disabled</c> روشن است: پرسشِ این سنجه صفحه است، نه
/// حلقهٔ پس‌زمینه. حلقه سنجهٔ خودش را دارد (آزمون‌های <c>SyncStoreTests</c>).
/// </summary>
internal static class SyncProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}{(detail is null ? "" : " — " + detail)}");
        if (!ok) _bad++;
    }

    private static void Pump(Window win)
    {
        Dispatcher.UIThread.RunJobs();
        _ = win.IsVisible;
        Thread.Sleep(4);
    }

    /// <summary>ابرِ ساختگی — فقط سه مسیرِ ورود با کد.</summary>
    private static void FakeCloud(List<string> seen)
    {
        CloudLink.TestTransport = async (req, ct) =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            seen.Add(path);
            var body = req.Content is null ? "" : await req.Content.ReadAsStringAsync(ct);

            string json;
            var code = HttpStatusCode.OK;
            if (path.EndsWith("/request-code", StringComparison.Ordinal))
                json = "{\"ok\":true,\"request_id\":\"req-1\",\"expires_in\":300," +
                       "\"resend_after\":60,\"masked_email\":\"a***@b.com\"}";
            else if (path.EndsWith("/request-status", StringComparison.Ordinal))
                json = "{\"ok\":true,\"state\":\"sent\",\"attempts\":1,\"can_resend_in\":42}";
            else if (path.EndsWith("/verify", StringComparison.Ordinal))
            {
                //  کدِ غلط ⇒ همان خطایی که سرورِ واقعی می‌دهد
                if (!body.Contains("\"123456\"", StringComparison.Ordinal))
                {
                    code = HttpStatusCode.BadRequest;
                    json = "{\"ok\":false,\"error\":{\"code\":\"CODE_WRONG\"," +
                           "\"message\":\"کد درست نیست\"},\"attempts_left\":4}";
                }
                else
                    json = "{\"ok\":true,\"accessToken\":\"at-1\",\"refreshToken\":\"rt-1\"," +
                           "\"accessExpiresAt\":" + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000) +
                           ",\"user\":{\"id\":\"usr-1\",\"email\":\"a@b.com\",\"name\":\"آزمون\"}}";
            }
            else json = "{\"ok\":true}";

            return new HttpResponseMessage(code)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        };
    }

    public static int Run()
    {
        //  ⛔ حلقهٔ پس‌زمینه این‌جا کاری ندارد
        SyncEngine.Disabled = true;
        CrashGuard.ReportingOff = true;

        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-syncui-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);

        //  ⚠️ هر سنجهٔ رابطی که پنجره می‌سازد با نصبِ پلن‌دار می‌دود — وگرنه
        //  بخش‌های پلن‌دار باز نمی‌شوند و سنجه جای اشتباه را نشان می‌دهد.
        FakeLicense.Grant();

        var seen = new List<string>();
        FakeCloud(seen);

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1366, Height = 768 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234"; LockIn.Wait(vm.Lock);
        for (var i = 0; i < 40; i++) Pump(win);

        var account = vm.Sections.OfType<AccountSectionViewModel>().FirstOrDefault();
        Check("بخشِ پروفایل پیدا شد", account is not null);
        if (account is null) { CloudLink.TestTransport = null; return 1; }

        // ── ۱) درِ سوم: ورود با کدِ ایمیلی ──────────────────────────────
        Console.WriteLine("── ۱) درِ سومِ صفحهٔ ورود: «کدِ ایمیلی»");
        account.SetSignUpCommand.Execute("code");
        Pump(win);
        Check("حالتِ «کدِ ایمیلی» روشن شد", account.IsCodeLogin);
        Check("و نوشتهٔ دکمه عوض شد", account.AccountButtonText.Contains("کد"),
              account.AccountButtonText);
        //  ⛔ راهِ رمز **برداشته نشده**: مشتری‌های امروز با همان وارد می‌شوند
        account.SetSignUpCommand.Execute("no");
        Pump(win);
        Check("و «حساب دارم» هنوز کار می‌کند", account.IsSignIn && !account.IsCodeLogin);
        account.SetSignUpCommand.Execute("code");
        Pump(win);
        Check("و برگشت به «کدِ ایمیلی»", account.IsCodeLogin);

        // ── ۲) ایمیل ⇒ کد ──────────────────────────────────────────────
        Console.WriteLine("── ۲) ایمیل ⇒ کدِ شش‌رقمی");
        account.LoginEmail = "a@b.com";
        account.AccountStepCommand.Execute(null);
        for (var i = 0; i < 60 && account.LoginStep != 2; i++) Pump(win);
        Check("به گامِ کد رفت", account.LoginStep == 2, "گام " + account.LoginStep);
        Check("مسیرِ درستِ سرور زده شد",
              seen.Any(p => p.EndsWith("/api/auth/pump/request-code", StringComparison.Ordinal)),
              string.Join(" · ", seen));
        Check("ایمیلِ پوشاندهٔ خودِ سرور نشان داده می‌شود",
              account.CodeMasked == "a***@b.com", account.CodeMasked);
        Check("شمارشِ معکوس از عددِ خودِ سرور شروع شد",
              account.CodeSeconds is > 0 and <= 60, account.CodeSeconds.ToString());
        Check("و تا تمام نشده، «دوباره بفرست» خاموش است", !account.CanResendCode);

        // ── ۳) شش خانه ─────────────────────────────────────────────────
        Console.WriteLine("── ۳) شش خانه: پرش، Paste، ارقامِ فارسی، ارسالِ خودکار");
        var boxes = account.CodeBoxes;
        boxes.Clear();
        Check("خانه‌ها خالی‌اند", boxes.Code.Length == 0);

        Check("رقم ⇒ پرش به خانهٔ بعد", boxes.Put(0, "9") == 1);
        boxes.Clear();

        boxes.Put(2, "۱۲۳۴۵۶");
        Check("Paste با ارقامِ فارسی، از خانهٔ اول پخش شد", boxes.Code == "123456", boxes.Code);
        Check("و خانهٔ اول «۱» شد", boxes.B1 == "1", boxes.B1);

        //  کدِ غلط: باید پاک شود و صفحه بماند
        boxes.Clear();
        boxes.Fill("000000");
        account.VerifyEmailCommand.Execute(null);
        for (var i = 0; i < 60 && account.Busy; i++) Pump(win);
        for (var i = 0; i < 10; i++) Pump(win);
        Check("کدِ غلط ⇒ همان گام ماند", account.LoginStep == 2, "گام " + account.LoginStep);
        Check("و خانه‌ها پاک شدند", account.CodeBoxes.Code.Length == 0, account.CodeBoxes.Code);
        Check("و پیامِ خودِ سرور دیده می‌شود", account.LoginStatus.Contains("کد درست نیست"),
              account.LoginStatus);

        //  کدِ درست: رقمِ ششم خودش می‌فرستد
        var before = account.LoginStep;
        for (var i = 0; i < 6; i++) boxes.Put(i, ((i % 6) + 1).ToString());
        for (var i = 0; i < 80 && account.LoginStep == before; i++) Pump(win);
        Check("رقمِ ششم خودش فرستاد و نشست ساخته شد", account.LoginStep >= 3,
              "گام " + account.LoginStep);
        Check("توکنِ حساب روی دیسک نشست",
              !string.IsNullOrWhiteSpace(AppSettings.Load().CloudAccountToken));
        Check("و مسیرِ verify زده شد",
              seen.Any(p => p.EndsWith("/api/auth/pump/verify", StringComparison.Ordinal)));

        // ── ۴) چراغِ نوارِ پایین ────────────────────────────────────────
        Console.WriteLine("── ۴) چراغِ همگام‌سازی در نوارِ پایین");
        vm.TickSyncDot();
        Pump(win);
        Check("چراغ پیش از شروع خاکستری است", vm.SyncDotBrushKey == "Pump.Muted",
              vm.SyncDotBrushKey);
        Check("⛔ هیچ نام یا نشانیِ سروری در دلیلش نیست",
              !vm.SyncDotReason.Contains("vill3n") && !vm.SyncDotReason.Contains("http")
              && !vm.SyncDotReason.Contains("://"), vm.SyncDotReason);

        var bar = win.FindControl<Border>("StatusBar");
        Check("نوارِ وضعیت در پنجره هست", bar is not null);
        Check("و پایینِ پنجره می‌نشیند", bar?.VerticalAlignment == Avalonia.Layout.VerticalAlignment.Bottom);
        var gap = win.FindControl<Panel>("StatusGap");
        Check("و جای خالی‌اش هست تا تهِ محتوا زیرش پنهان نشود", gap is not null);

        // ── ۵) صفحهٔ «همگام‌سازی» در تنظیمات ────────────────────────────
        Console.WriteLine("── ۵) تنظیمات ← همگام‌سازی");
        var settings = vm.Sections.FirstOrDefault(s => s.Id == "settings");
        var page = settings?.SubSections.FirstOrDefault(s => s.Id == "sync") as SyncSectionViewModel;
        Check("صفحهٔ همگام‌سازی زیرِ تنظیمات هست", page is not null);
        if (page is not null)
        {
            page.OnActivatedAsync().GetAwaiter().GetResult();
            Pump(win);
            Check("و عددهایش خوانده شدند", page.QueuedText.Length > 0, page.QueuedText);
            Check("⛔ و هیچ کادرِ نشانیِ سروری ندارد", NoAddressBox(),
                  "AccountSectionView/SyncSectionView");
            Check("گزارشِ خطا پیش‌فرض روشن است", page.ReportErrors);
            page.ReportErrors = false;
            Check("و خاموش می‌شود", AppSettings.Load().ReportErrorsOff);
            page.ReportErrors = true;
        }

        CloudLink.TestTransport = null;
        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"✖ {_bad} بند سرخ");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>
    /// ⛔ قاعدهٔ <c>CloudAddressLockTests</c>: هیچ کادری برای نشانیِ سرور.
    /// این‌جا روی خودِ فایلِ صفحه گشته می‌شود، نه روی درختِ کنترل‌ها — چون
    /// کادری که امروز نیست، فردا می‌تواند اضافه شود.
    /// </summary>
    private static bool NoAddressBox()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "PumpYaqobi.App")))
            root = root.Parent;
        if (root is null) return true;

        var file = Path.Combine(root.FullName, "PumpYaqobi.App", "Views", "Sections", "SyncSectionView.axaml");
        if (!File.Exists(file)) return true;
        var src = File.ReadAllText(file);
        return !src.Contains("ServerUrl") && !src.Contains("api.vill3n.top")
            && !src.Contains("نشانیِ سرور\" ") && !src.Contains("Watermark=\"نشانی");
    }
}
