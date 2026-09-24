using System.Net.Http;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «برنامه به سرور وصل نمی‌شود» — با سرورهای **واقعی**، نه ساختگی ═══════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «ببین چرا وصل نمی‌شود، درستش کن که آسان وصل
/// شود، با تست — و از جای وضعیتِ سرور در برنامه و از خودِ برنامهٔ سرور عکس بگیر.»
///
/// سنجه‌های دیگر (‎serverdot‎، ‎cloudlogin‎) سرورِ ساختگی دارند. این یکی به
/// **پنلِ خانگیِ واقعی** (ریپوی ‎server‎) و **سرورِ حسابِ واقعی** (‎shop‎) وصل
/// می‌شود که خارج از این فرآیند روشن‌اند (‎livestack.mjs‎)، و برنامه را
/// **بی هیچ نشانی‌ای** راه می‌اندازد:
///
///   ۱) ورود با ایمیل و رمز از خودِ صفحهٔ ورود   ⇒ درگاهِ پنل ⇒ سرورِ حساب
///   ۲) گامِ پمپ (ساختن و بند شدنِ دستگاه)
///   ۳) برنامه **خودش** سرورِ خانگی را با بستهٔ UDP پیدا می‌کند، ثبت می‌شود و وصل
///   ۴) چراغِ سربرگ سبز، و عکسش
///
/// ⚠️ تنها چیزی که عوض می‌شود مقصدِ ابر است (‎CloudLink.TestTransport‎ ⇒ پورتِ
/// عمومیِ همان پنل، یعنی همان جایی که تونل می‌رسد). نشانیِ قفل‌شدهٔ کد دست
/// نمی‌خورد.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- livestack &lt;live.json&gt; [پوشهٔ عکس]
/// </summary>
internal static class LiveStackProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(30) };

    public static int Run(string[] args)
    {
        if (args.Length < 2 || !File.Exists(args[1]))
        {
            Console.WriteLine("⚠️ live.json نیست — اول livestack.mjs را روشن کنید. رد شد.");
            return 0;
        }
        Console.WriteLine("── سنجهٔ زنده: " + args[1]);
        var live = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
        var pub = new Uri(live.GetProperty("public").GetString()!);
        var email = live.GetProperty("email").GetString()!;
        var pass = live.GetProperty("password").GetString()!;
        var shots = args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "pump-livestack");
        Directory.CreateDirectory(shots);

        //  ⚠️ فقط مقصدِ ابر: همان درخواست، همان سرآیندها، به پورتِ عمومیِ پنل
        var seen = new List<string>();
        CloudLink.TestTransport = async (req, ct) =>
        {
            var to = new UriBuilder(req.RequestUri!) { Scheme = pub.Scheme, Host = pub.Host, Port = pub.Port }.Uri;
            var fwd = new HttpRequestMessage(req.Method, to);
            foreach (var h in req.Headers) fwd.Headers.TryAddWithoutValidation(h.Key, h.Value);
            if (req.Content is not null)
            {
                var bytes = await req.Content.ReadAsByteArrayAsync(ct);
                fwd.Content = new ByteArrayContent(bytes);
                foreach (var h in req.Content.Headers) fwd.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            lock (seen) seen.Add(req.Method + " " + req.RequestUri!.AbsolutePath);
            return await Http.SendAsync(fwd, ct);
        };

        var dir = Path.Combine(Path.GetTempPath(), "pump-livestack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        var f0 = AppSettings.Load();
        Check("نصبِ تازه: هیچ نشانیِ سرورِ خانگی‌ای نوشته نشده", string.IsNullOrWhiteSpace(f0.ServerUrl), f0.ServerUrl);

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);

        Console.WriteLine("── ۱) ورود با ایمیل و رمز — از خودِ صفحهٔ ورود");
        var account = (AccountSectionViewModel)vm.Sections.First(s => s.Id == "account");
        Wait(win, vm.GoAsync(account));
        account.SetSignUpCommand.Execute("no");
        account.LoginEmail = email;
        account.LoginPassword = pass;
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        Settle(win);
        Check("سرورِ حسابِ واقعی رمز را پذیرفت و به گامِ پمپ رفت", account.StepPump,
              "گام " + account.LoginStep + " · " + account.LoginStatus);
        Check("درخواست به همان مسیرِ واقعی رفت", seen.Contains("POST /api/auth/login"), string.Join(" · ", seen));

        Console.WriteLine("── ۲) گامِ پمپ — ساختن و بند شدنِ دستگاه");
        account.LoginPump = "پمپ یعقوبی — سنجهٔ زنده";
        Wait(win, account.FinishPumpCommand.ExecuteAsync(null));
        Settle(win);
        var f1 = AppSettings.Load();
        Check("توکنِ دستگاه از سرورِ واقعی آمد", !string.IsNullOrWhiteSpace(f1.CloudDeviceToken));
        Check("و کلیدِ عمومیِ مجوز قفل شد", !string.IsNullOrWhiteSpace(f1.CloudPublicKey));
        Check("گامِ ورود تمام شد", !account.ShowLoginPage, "گام " + account.LoginStep + " · " + account.LoginStatus);

        Console.WriteLine("── ۳) سرورِ خانگی — بی هیچ نشانی، خودش پیدا و وصل می‌شود");
        var found = Task.Run(() => ServerFinder.FindFirstAsync()).GetAwaiter().GetResult();
        Check("بستهٔ UDP جواب گرفت (کشفِ خودکار)", found is not null, found?.Url ?? "—");
        var green = false;
        var till = DateTime.UtcNow + TimeSpan.FromSeconds(75);
        while (DateTime.UtcNow < till)
        {
            Pump(win);
            vm.TickServerDot(); vm.TickCloudDot(); vm.TickLinkDot();
            if (vm.ServerDotBrushKey == "Pump.Ok" && vm.CloudDotBrushKey == "Pump.Ok") { green = true; break; }
            Thread.Sleep(250);
        }
        var pubNow = AppHost.Current.PublisherIfStarted;
        Console.WriteLine($"     ⓘ ناشر: {(pubNow is null ? "روشن نشد" : "روشن")} · تنظیم: {pubNow?.Configured}");
        if (vm.ServerDotBrushKey != "Pump.Ok")
        {
            var why = Task.Run(() => StationLink.EnsureAsync(AppHost.Current)).GetAwaiter().GetResult();
            Console.WriteLine("     ⓘ ثبتِ دستی برای عیب‌یابی: " + why);
        }
        var f2 = AppSettings.Load();
        Check("برنامه خودش ثبت شد (نشانی و رمز از خودِ سرور)",
              !string.IsNullOrWhiteSpace(f2.ServerUrl) && !string.IsNullOrWhiteSpace(f2.ServerToken), f2.ServerUrl);
        Check("چراغِ سرورِ خانگی سبز", vm.ServerDotBrushKey == "Pump.Ok", vm.ServerDotBrushKey + " · " + vm.ServerDotReason);
        Check("چراغِ سرورِ حساب سبز", vm.CloudDotBrushKey == "Pump.Ok", vm.CloudDotBrushKey + " · " + vm.CloudDotReason);
        Check("⇒ چراغِ یگانهٔ سربرگ سبز", green && vm.LinkDotBrushKey == "Pump.Ok", vm.LinkDotBrushKey + " · " + vm.LinkDotReason);
        Check("⛔ و هیچ نشانیِ سروری در نوشتهٔ چراغ نیست",
              !vm.LinkDotReason.Contains("127.0.0.1") && !vm.LinkDotReason.Contains("http") && !vm.LinkDotReason.Contains("vill3n"),
              vm.LinkDotReason);
        File.WriteAllText(Path.Combine(shots, "station.txt"), HomeLink.StationCode(AppHost.Current));

        //  عکس: سربرگ با کادرِ توضیحِ همان چراغ
        Wait(win, vm.GoAsync(vm.Sections.First(s => s.Id == "safe")));
        Settle(win);
        var dot = win.GetVisualDescendants().OfType<Button>()
                     .FirstOrDefault(b => b.Command == vm.CheckLinksCommand && b.IsEffectivelyVisible);
        if (dot is not null) { ToolTip.SetIsOpen(dot, true); Settle(win); }
        Shot(win, shots, "app-server-light");
        if (dot is not null) ToolTip.SetIsOpen(dot, false);

        //  برای عکسِ خودِ پنل: برنامه چند ثانیه وصل می‌ماند (‎PUMP_LIVE_HOLD‎، ثانیه)
        if (int.TryParse(Environment.GetEnvironmentVariable("PUMP_LIVE_HOLD"), out var hold) && hold > 0)
        {
            Console.WriteLine("     ⓘ " + hold + " ثانیه وصل می‌ماند");
            var end = DateTime.UtcNow.AddSeconds(hold);
            while (DateTime.UtcNow < end) { Pump(win); Thread.Sleep(100); }
        }

        CloudLink.TestTransport = null;
        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ برنامه بی هیچ نشانی به هر دو سرورِ واقعی وصل شد" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Shot(Window win, string dir, string name)
    {
        using var f = win.CaptureRenderedFrame();
        var path = Path.Combine(dir, name + ".png");
        f?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(5); }
        Settle(w);
    }
}
