using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «برنامه به سرورِ خانگی نمی‌رسد و خانه‌اش سبز نمی‌شود» (۱۴۰۵/۰۷/۱۳) ══════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- lanreach &lt;مسیرِ homelab-panel/server&gt; [پوشهٔ عکس]
///
/// خودِ سرورِ خانگیِ واقعی (پنل، کشفِ خودکار روی UDP ‎4702‎، و درِ ثبتِ پمپ) را
/// بالا می‌آورد و خودِ برنامهٔ پمپ را رویش می‌گذارد — دو بار:
///
///   الف) پنلِ **کهنه**: فقط روی ‎127.0.0.1‎ گوش می‌دهد (همان کاری که مرکز فرمانِ
///        ویندوز تا ۱.۵۰.۱۱ می‌کرد) و برنامه نشانیِ کارتِ شبکه را ذخیره کرده.
///        پیش از اصلاح: سرخ برای همیشه. حالا: خودش نشانیِ رسیدنی را پیدا
///        می‌کند و سبز می‌شود.
///   ب)  پنلِ **تازه**: روی کارتِ شبکه گوش می‌دهد ⇒ نشانیِ شبکه رسیدنی است و
///        همان به گوشی‌ها داده می‌شود؛ و نگهبانِ پنل اتصالِ بیرون از شبکهٔ
///        خانه را می‌بندد.
///
/// ⚠️ نشانیِ ماشینِ آزمون (سندباکس ‎192.0.2.x‎) خصوصی نیست، پس «شبکهٔ خانه» با
/// ‎HLP_PANEL_NETS‎ به نگهبان گفته می‌شود — همان درِ مستندی که صاحبِ شبکهٔ
/// غیرعادی هم دارد.
/// </summary>
public static class LanReachProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public static int Run(string[] args)
    {
        if (args.Length < 2 || !File.Exists(Path.Combine(args[1], "src", "index.js")))
        {
            Console.WriteLine("⚠️ مسیرِ homelab-panel/server داده نشد — رد شد.");
            return 0;
        }
        var serverDir = Path.GetFullPath(args[1]);
        var shots = args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "pump-lanreach");
        Directory.CreateDirectory(shots);

        var lan = LanAddress();
        Console.WriteLine("── نشانیِ شبکهٔ این ماشین: " + (lan?.ToString() ?? "هیچ"));
        if (lan is null) { Console.WriteLine("⚠️ کارتِ شبکه‌ای نیست — رد شد."); return 0; }
        var nets = lan + "/32";
        var port = FreePort();

        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-lanreach-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        if (AppHost.Current.Auth.NeedsFirstRun()) AppHost.Current.Auth.CreateFirstAdmin("1234");

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        // ══ الف) پنلِ کهنه: فقط ‎127.0.0.1‎ ═════════════════════════════════════
        Console.WriteLine("── الف) پنلِ کهنه — فقط روی ‎127.0.0.1‎ (مرکز فرمانِ ویندوز تا ۱.۵۰.۱۱)");
        var data = Path.Combine(Path.GetTempPath(), "lanreach-panel-" + Guid.NewGuid().ToString("N"));
        var panel = StartPanel(serverDir, data, port, host: "127.0.0.1", nets: nets);
        try
        {
            Check("پنلِ واقعی بالا آمد", WaitHealth($"http://127.0.0.1:{port}"));
            var lanUrl = $"http://{lan}:{port}";
            Check("⇒ بازسازیِ باگ: نشانیِ شبکه «اتصال رد شد» می‌گیرد", !Answers(lanUrl), lanUrl);
            var card = Task.Run(() => ServerFinder.FindAsync()).GetAwaiter().GetResult();
            Check("ولی کشفِ خودکار همان نشانیِ شبکه را هم می‌گوید", card.Any(c => c.Url == lanUrl),
                  string.Join(" · ", card.Select(c => c.Url)));
            var pick = Task.Run(() => ServerFinder.FindReachableAsync()).GetAwaiter().GetResult();
            Check("⛔ و حالا نشانیِ **رسیدنی** برداشته می‌شود، نه اولین جواب", pick?.Url == $"http://127.0.0.1:{port}",
                  pick?.Url ?? "—");

            //  برنامه‌ای که نشانیِ مُرده را ذخیره کرده — درست همان حالی که چراغ سرخ ماند
            var enrolled = Enroll($"http://127.0.0.1:{port}", "lanreach-old");
            var f = AppSettings.Load();
            f.ServerUrl = lanUrl;
            f.ServerToken = enrolled.token;
            f.ServerReadKey = enrolled.read;
            f.StationCode = "lanreach-old";
            f.Save();

            var win = new MainWindow { Width = 1366, Height = 768 };
            win.Show(); Pump(win);
            var vm = (MainViewModel)win.DataContext!;
            vm.Lock.Password = "1234"; LockIn.Wait(vm.Lock);
            for (var i = 0; i < 40; i++) Pump(win);
            var pub = AppHost.Current.PublisherIfStarted;
            Check("انتشارکنندهٔ ایستگاه بالا آمد", pub is not null);
            if (pub is null) return 1;

            var green = WaitGreen(win, vm, pub, TimeSpan.FromSeconds(60));
            var after = AppSettings.Load();
            Check("⛔ چراغِ سرورِ خانگی **خودش** سبز شد — بی هیچ کلیکی", green, vm.ServerDotBrushKey + " · " + vm.ServerDotReason);
            Check("و نشانیِ مُرده با نشانیِ رسیدنی جایگزین شد", after.ServerUrl == $"http://127.0.0.1:{port}", after.ServerUrl);
            Check("⛔ و همان پوشه و همان رمز ماند (دفتر جابه‌جا نشد)",
                  after.StationCode == "lanreach-old" && after.ServerToken == enrolled.token, after.StationCode);
            Check("⛔ و به گوشی‌ها ‎127.0.0.1‎ داده نمی‌شود", !ServerFinder.IsLoopbackUrl(HomeLink.ShareUrl(AppHost.Current)),
                  HomeLink.ShareUrl(AppHost.Current));
            OpenTip(win, vm);
            Shot(win, shots, "lan-1-old-panel-repaired");
            win.Close();
        }
        finally { Stop(panel); }

        // ══ ب) پنلِ تازه: روی شبکه، با نگهبان ═════════════════════════════════
        Console.WriteLine("── ب) پنلِ تازه — روی کارتِ شبکه (مرکز فرمانِ ۱.۵۰.۱۲)");
        var data2 = Path.Combine(Path.GetTempPath(), "lanreach-panel-" + Guid.NewGuid().ToString("N"));
        //  همان تصمیمِ خودِ سرور: پوستهٔ کهنه ‎127.0.0.1‎ می‌دهد و سرور بازش می‌کند
        var panel2 = StartPanel(serverDir, data2, port, host: "127.0.0.1", nets: nets, packaged: true);
        try
        {
            Check("پنل بالا آمد", WaitHealth($"http://127.0.0.1:{port}"));
            var lanUrl = $"http://{lan}:{port}";
            Check("⛔ پوستهٔ کهنه ‎127.0.0.1‎ گفت، ولی سرور روی شبکه هم گوش می‌دهد", Answers(lanUrl), lanUrl);

            var f = AppSettings.Load();
            f.ServerUrl = ""; f.ServerToken = ""; f.ServerReadKey = ""; f.ServerLanUrl = ""; f.StationCode = "";
            f.Save();
            //  ⚠️ نشانی در **دو** جا می‌نشیند (فایل و دفتر — ‎HomeLink.Url‎)؛ نصبِ تازه هیچ‌کدام را ندارد
            try { AppHost.Current.Settings.Set(PumpYaqobi.Services.Data.SettingsService.ServerUrl, ""); } catch { }
            var win = new MainWindow { Width = 1366, Height = 768 };
            win.Show(); Pump(win);
            var vm = (MainViewModel)win.DataContext!;
            vm.Lock.Password = "1234"; LockIn.Wait(vm.Lock);
            for (var i = 0; i < 40; i++) Pump(win);
            var pub = AppHost.Current.PublisherIfStarted!;
            //  نصبِ تازه: هیچ نشانی‌ای ندارد و کاربر روی چراغ می‌زند
            Wait(win, vm.CheckLinksCommand.ExecuteAsync(null));
            var green = WaitGreen(win, vm, pub, TimeSpan.FromSeconds(60));
            var after = AppSettings.Load();
            Check("⛔ نصبِ تازه بی هیچ نشانی خودش پیدا و وصل شد (کلیکِ چراغ دیگر بن‌بست نیست)", green,
                  vm.ServerDotBrushKey + " · " + vm.ServerDotReason);
            Check("و نشانیِ **شبکه** را برداشت — همانی که به گوشی‌ها هم می‌رسد", after.ServerUrl == lanUrl, after.ServerUrl);
            Check("و کدِ پوشه مالِ همین کامپیوتر است، نه «pump1»", after.StationCode.StartsWith("d-"), after.StationCode);
            OpenTip(win, vm);
            Shot(win, shots, "lan-2-new-panel-green");
            win.Close();
        }
        finally { Stop(panel2); }

        // ══ ج) نگهبانِ پنل: بیرون از شبکهٔ خانه ⇒ بسته ═══════════════════════
        Console.WriteLine("── ج) نگهبانِ پنل — بیرون از شبکهٔ خانه بسته است");
        var data3 = Path.Combine(Path.GetTempPath(), "lanreach-panel-" + Guid.NewGuid().ToString("N"));
        var panel3 = StartPanel(serverDir, data3, port, host: "0.0.0.0", nets: "");
        try
        {
            Check("پنل بالا آمد", WaitHealth($"http://127.0.0.1:{port}"));
            var lanUrl = $"http://{lan}:{port}";
            var isPrivate = PumpYaqobi.UiTests.LanReachProbe.IsPrivate(lan);
            if (isPrivate)
            {
                Check("نشانیِ این ماشین خصوصی است ⇒ باید باز باشد", Answers(lanUrl), lanUrl);
            }
            else
            {
                Check($"⛔ نشانیِ {lan} شبکهٔ خانه نیست ⇒ پنل پیش از هر بایتی می‌بندد", !Answers(lanUrl), lanUrl);
                Check("ولی خودِ همین کامپیوتر همیشه باز است", Answers($"http://127.0.0.1:{port}"));
            }
        }
        finally { Stop(panel3); }

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ برنامه به سرورِ خانگی می‌رسد و چراغش سبز می‌شود" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    internal static bool IsPrivate(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
    }

    private static IPAddress? LanAddress()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var a in nic.GetIPProperties().UnicastAddresses)
                if (a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)) return a.Address;
        }
        return null;
    }

    private static Process StartPanel(string dir, string data, int port, string host, string nets, bool packaged = false)
    {
        Directory.CreateDirectory(data);
        var psi = new ProcessStartInfo("node", "--disable-warning=ExperimentalWarning src/index.js")
        {
            WorkingDirectory = dir, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        psi.Environment["HLP_PORT"] = port.ToString();
        psi.Environment["HLP_HOST"] = host;
        psi.Environment["HLP_DATA_DIR"] = data;
        psi.Environment["HLP_SITES_ROOT"] = Path.Combine(data, "sites");
        psi.Environment["HLP_SITESYNC"] = "0";
        psi.Environment["HLP_TUNNEL"] = "0";
        psi.Environment["HLP_AI_ENABLED"] = "0";
        psi.Environment["HLP_ACCOUNT_API"] = "0";
        psi.Environment["HLP_ACCOUNT_AUTOSTART"] = "0";
        psi.Environment["HLP_AUTOMATION"] = "0";
        psi.Environment["HLP_PANEL_NETS"] = nets;
        if (packaged) psi.Environment["HLP_APP_LAYOUT"] = "packaged";
        var p = Process.Start(psi)!;
        p.OutputDataReceived += (_, _) => { };
        p.ErrorDataReceived += (_, _) => { };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        return p;
    }

    private static void Stop(Process p)
    {
        try { if (!p.HasExited) { p.Kill(entireProcessTree: true); p.WaitForExit(5000); } } catch { }
    }

    private static bool WaitHealth(string url)
    {
        var till = DateTime.UtcNow + TimeSpan.FromSeconds(40);
        while (DateTime.UtcNow < till) { if (Answers(url)) return true; Thread.Sleep(300); }
        return false;
    }

    private static bool Answers(string url)
    {
        try { using var r = Http.GetAsync(url + "/health").GetAwaiter().GetResult(); return (int)r.StatusCode < 500; }
        catch { return false; }
    }

    private static (string token, string read) Enroll(string url, string code)
    {
        using var r = Http.PostAsync(url + "/api/stations/enroll",
            new StringContent("{\"code\":\"" + code + "\",\"name\":\"lanreach\"}", System.Text.Encoding.UTF8, "application/json"))
            .GetAwaiter().GetResult();
        using var d = System.Text.Json.JsonDocument.Parse(r.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        string S(string k) => d.RootElement.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
        return (S("token"), S("readKey"));
    }

    private static bool WaitGreen(Window win, MainViewModel vm, StationPublisher pub, TimeSpan max)
    {
        var till = DateTime.UtcNow + max;
        while (DateTime.UtcNow < till)
        {
            Wait(win, pub.KeepLinkAsync());
            Pump(win);
            vm.TickServerDot(); vm.TickLinkDot();
            if (vm.ServerDotBrushKey == "Pump.Ok") return true;
            Thread.Sleep(400);
        }
        return false;
    }

    private static void OpenTip(Window win, MainViewModel vm)
    {
        Pump(win);
        var dot = win.GetVisualDescendants().OfType<Button>()
                     .FirstOrDefault(b => b.Command == vm.CheckLinksCommand && b.IsEffectivelyVisible);
        if (dot is not null) { ToolTip.SetIsOpen(dot, true); Pump(win); }
    }

    private static void Shot(Window win, string dir, string name)
    {
        using var f = win.CaptureRenderedFrame();
        var path = Path.Combine(dir, name + ".png");
        f?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Wait(Window win, Task t)
    {
        for (var i = 0; i < 20000 && !t.IsCompleted; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(2); }
        Dispatcher.UIThread.RunJobs();
        if (t.IsFaulted) throw t.Exception!;
    }
}
