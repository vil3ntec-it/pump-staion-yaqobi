using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «سرور روشن است اما برنامه می‌گوید خاموش است» ═══════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «سرور روشن است اما پمپ بنزین می‌گوید خاموش
/// است؛ آن‌جوری هست که هر ثانیه چک کند و بگردد که وصل است یا نه؟»
///
/// این سنجه یک **سرورِ خانگیِ واقعی** (وب‌سوکت روی ‎127.0.0.1‎) بالا می‌آورد و
/// چراغِ سربرگ را می‌سنجد: روشن ⇒ سبز، خاموش ⇒ سرخ، دوباره روشن ⇒ سبز.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- serverdot
///
/// ⚠️ **بی اشتراک هم سنجیده می‌شود** (`Entitlements.TestDeny`): همان باگ
/// اصلی این بود که برنامهٔ بی‌اشتراک هیچ‌وقت وصل نمی‌شد، پس چراغ «خاموش»
/// می‌گفت در حالی که سرور روشن بود. قفلِ اشتراک روی **عکسِ زنده** است، نه
/// روی خودِ اتصال.
/// </summary>
internal static class ServerDotProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}{(detail is null ? "" : " — " + detail)}");
        if (!ok) _bad++;
    }

    // ── سرورِ خانگیِ ساختگی ─────────────────────────────────────────────

    private sealed class FakeHome
    {
        private HttpListener? _listener;
        public int Port { get; }
        public int Opened { get; private set; }

        public FakeHome(int port) => Port = port;

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        public void Stop()
        {
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
        }

        private async Task AcceptAsync()
        {
            var listener = _listener;
            while (listener is not null && listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); }
                catch { return; }
                if (!ctx.Request.IsWebSocketRequest) { ctx.Response.StatusCode = 400; ctx.Response.Close(); continue; }
                _ = Task.Run(() => ServeAsync(ctx));
            }
        }

        private async Task ServeAsync(HttpListenerContext ctx)
        {
            WebSocket ws;
            try { ws = (await ctx.AcceptWebSocketAsync(null)).WebSocket; }
            catch { return; }
            Opened++;
            try
            {
                //  سرورِ واقعی اول ‎connected‎ می‌گوید — همان چیزی که
                //  `HomeSync.TryOpenAsync` منتظرش است.
                await SendAsync(ws, "{\"op\":\"connected\"}");

                var buf = new byte[16 * 1024];
                while (ws.State == WebSocketState.Open)
                {
                    var res = await ws.ReceiveAsync(buf, CancellationToken.None);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    var text = Encoding.UTF8.GetString(buf, 0, res.Count);
                    //  هر درخواستِ شناسه‌دار یک ‎ack‎ می‌خواهد، وگرنه طرفِ
                    //  مقابل تا تایم‌اوت منتظر می‌ماند.
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        if (doc.RootElement.TryGetProperty("id", out var id) && id.TryGetInt32(out var n))
                            await SendAsync(ws, "{\"op\":\"ack\",\"id\":" + n + ",\"ok\":true}");
                    }
                    catch { }
                }
            }
            catch { }
            finally { try { ws.Dispose(); } catch { } }
        }

        private static Task SendAsync(WebSocket ws, string json) =>
            ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static int Run()
    {
        var port = FreePort();
        var home = new FakeHome(port);
        home.Start();

        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-serverdot-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        //  ⛔ نصبِ تازه از ۱۴۰۵/۰۷/۰۷ **بی‌رمز** باز می‌شود و صفحهٔ قفل ندارد.
        //  این سنجه همان مسیرِ رمزدار را می‌سنجد، پس رمز را خودش می‌گذارد.
        if (AppHost.Current.Auth.NeedsFirstRun()) AppHost.Current.Auth.CreateFirstAdmin("1234");

        //  ⚠️ **پس از** `AppHost.Start`: خودش `AppSettings.DirOverride` را به
        //  پوشهٔ همین دیتابیسِ آزمون می‌برد، پس نوشتنِ پیش از آن به فایلِ
        //  دیگری می‌رفت و بی‌اثر بود (خودِ همین سنجه گرفتش).
        var f = AppSettings.Load();
        f.ServerUrl = $"ws://127.0.0.1:{port}";
        f.ServerToken = "app-token";
        f.StationCode = "yaqobi";
        f.Save();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var host = AppHost.Current;
        //  ⚠️ نشانی و رمز از همان `settings.json` خوانده می‌شوند
        //  (`HomeLink.Url`/`Token` به آن برمی‌گردند)، پس پیش از ورود هم
        //  نیازی به نوشتن در تنظیماتِ دفتر نیست.

        var win = new MainWindow { Width = 1366, Height = 768 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; LockIn.Wait(vm.Lock);
        for (var i = 0; i < 40; i++) Pump(win);

        //  ⚠️ **بی اشتراک** — همان حالی که باگ در آن دیده شد
        Entitlements.TestDeny = true;

        var pub = host.PublisherIfStarted;
        Check("انتشارکنندهٔ ایستگاه بالا آمده است", pub is not null);
        if (pub is null) { home.Stop(); return 1; }
        Check("نشانیِ سرور تنظیم شده است (چراغ خاکستری نیست)", pub.Configured);

        Console.WriteLine("── ۱) سرور روشن است ⇒ چراغ باید سبز شود");
        var greenOn = WaitDot(win, vm, pub, "Pump.Ok", TimeSpan.FromSeconds(20));
        Check("چراغ سبز شد", greenOn, vm.ServerDotBrushKey + " · " + vm.ServerDotReason);
        Check("و بی اشتراک هم وصل شد (قفل روی عکسِ زنده است، نه اتصال)",
              greenOn && !Entitlements.Allows(Entitlements.Kar));
        Check("سرورِ ساختگی واقعاً یک اتصال دید", home.Opened > 0, home.Opened + " اتصال");

        Console.WriteLine("── ۲) سرور خاموش شد ⇒ چراغ باید سرخ شود");
        home.Stop();
        var red = WaitDot(win, vm, pub, "Pump.Danger", TimeSpan.FromSeconds(30));
        Check("چراغ سرخ شد", red, vm.ServerDotBrushKey);
        Check("و دلیلش «آخرین وصل» را می‌گوید",
              !red || vm.ServerDotReason.Contains("آخرین وصل"), vm.ServerDotReason);
        Check("و می‌گوید خودش دوباره می‌گردد",
              !red || vm.ServerDotReason.Contains("هر پنج ثانیه"), vm.ServerDotReason);

        Console.WriteLine("── ۳) سرور دوباره روشن شد ⇒ چراغ خودش سبز می‌شود");
        var home2 = new FakeHome(port);
        home2.Start();
        var greenBack = WaitDot(win, vm, pub, "Pump.Ok", TimeSpan.FromSeconds(25));
        Check("بی هیچ کلیکی، خودش دوباره وصل شد", greenBack,
              vm.ServerDotBrushKey + " · " + home2.Opened + " اتصال");

        Console.WriteLine("── ۴) کلیکِ روی چراغ ⇒ همین حالا بررسی");
        home2.Stop();
        WaitDot(win, vm, pub, "Pump.Danger", TimeSpan.FromSeconds(30));
        var home3 = new FakeHome(port);
        home3.Start();
        Wait(win, vm.CheckServerCommand.ExecuteAsync(null));
        for (var i = 0; i < 20; i++) Pump(win);
        vm.TickServerDot();
        Check("کلیک همان لحظه وصل کرد (بی انتظار)", vm.ServerDotBrushKey == "Pump.Ok",
              vm.ServerDotBrushKey);
        home3.Stop();

        Entitlements.TestDeny = false;
        Console.WriteLine();
        Console.WriteLine(_bad == 0
            ? "✅ چراغِ سرور حقیقت را می‌گوید: روشن ⇒ سبز، خاموش ⇒ سرخ، و خودش هر پنج ثانیه می‌گردد"
            : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>تا وقتی چراغ همان رنگ شود پمپ می‌کند — یا وقت تمام شود.</summary>
    private static bool WaitDot(Window win, MainViewModel vm, StationPublisher pub, string key, TimeSpan max)
    {
        var till = DateTime.UtcNow + max;
        while (DateTime.UtcNow < till)
        {
            Dispatcher.UIThread.RunJobs();
            vm.TickServerDot();
            if (vm.ServerDotBrushKey == key) return true;
            Thread.Sleep(50);
        }
        vm.TickServerDot();
        return vm.ServerDotBrushKey == key;
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
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
        for (var i = 0; i < 4000 && !t.IsCompleted; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(2); }
        Dispatcher.UIThread.RunJobs();
        if (t.IsFaulted) throw t.Exception!;
    }
}
