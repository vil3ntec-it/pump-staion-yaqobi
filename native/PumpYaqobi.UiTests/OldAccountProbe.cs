using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «حسابِ قدیمی آزمایشی نگرفت، و اشتراکی که از سرور دادم به برنامه نرسید» ══
///
/// گزارشِ صاحب ریپو پس از به‌روزرسانی به ۳.۱.۱۷۹. سنجه‌های پیشین همه با
/// حسابی کار می‌کردند که **همین برنامه** همان لحظه ساخته بود؛ مشتریِ واقعی
/// اما حسابی دارد که با نسخهٔ پیشین ساخته شده و حالا برنامه را به‌روز کرده.
/// پس این سنجه همان تاریخچه‌ها را روی **پشتهٔ واقعی** می‌سازد — حساب و پمپ
/// با خودِ API سرور، و برنامه با همان چیزی که نسخهٔ پیشین روی دیسک گذاشته —
/// و فقط **حلقهٔ خودِ برنامه** را می‌گذارد کار کند:
///
///   الف) حسابِ واردشده، بی پمپ (نسخهٔ پیشین گامِ پمپ را «بعداً» زده بود)
///   ب)  حسابی که پمپ دارد ولی این کامپیوتر به آن وصل نیست
///   ج)  مدیر از پنل وی‌آی‌پی داد — پیش از آن‌که برنامه وصل شود
///   د)  مدیر از پنل وی‌آی‌پی داد — وقتی برنامه وصل و باز است
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- oldacct &lt;live.json&gt; [پوشهٔ عکس]
/// </summary>
internal static class OldAccountProbe
{
    private static int _bad;
    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(30) };
    private static string _panel = "", _panelToken = "", _mailCodes = "", _pub = "";
    //  ⚠️ «حلقهٔ واقعی»: هیچ دوری با بازتاب صدا زده نمی‌شود — فقط تایمرِ خودِ
    //  برنامه (StationPublisher) باید کار را برساند، در زمانِ واقعی.
    private static bool _real;
    private static Func<bool>? _until;
    private const string Pass = "Pump!1405old";

    public static int Run(string[] args)
    {
        if (args.Length < 2 || !File.Exists(args[1]))
        {
            Console.WriteLine("⚠️ live.json نیست — اول signup-stack.mjs را روشن کنید. رد شد.");
            return 0;
        }
        var live = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
        var pub = new Uri(live.GetProperty("public").GetString()!);
        _pub = pub.ToString().TrimEnd('/');
        _mailCodes = live.GetProperty("mailCodes").GetString()!;
        _panel = live.GetProperty("panel").GetString()!.TrimEnd('/');
        _panelToken = live.GetProperty("panelToken").GetString()!;
        var shots = args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "pump-oldacct");
        _real = args.Contains("real") || Environment.GetEnvironmentVariable("PUMP_OLDACCT_REAL") == "1";
        if (_real) Console.WriteLine("     ⓘ حالتِ حلقهٔ واقعی: فقط تایمرِ خودِ برنامه");
        Directory.CreateDirectory(shots);

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
            var res = await Http.SendAsync(fwd, ct);
            lock (seen) seen.Add($"{req.Method} {req.RequestUri!.AbsolutePath} ⇒ {(int)res.StatusCode}");
            return res;
        };

        var dir = Path.Combine(Path.GetTempPath(), "pump-oldacct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var onlyE = Environment.GetEnvironmentVariable("PUMP_OLDACCT_ONLY") == "e";
        Avalonia.Controls.Window win = null!; MainViewModel vm = null!; AccountSectionViewModel account = null!;
        if (!onlyE) {
        // ── الف) حسابِ واردشده، بی پمپ ────────────────────────────────────
        Console.WriteLine("══ الف) حسابی که با نسخهٔ پیشین ساخته شد و پمپ ندارد — برنامه به‌روز شد");
        var a = MakeAccount("old-a-" + Guid.NewGuid().ToString("N")[..8] + "@example.com", withStation: false);
        SeatOnDisk(a, loginSkipped: true);
        (win, vm, account) = Open();
        Loop(win, 2);
        //  ⚠️ گامِ «حساب آماده» پس از همان مکثِ آرامِ ناشر (۱۲ ثانیه) می‌دود —
        //  پس تا چهل ثانیهٔ واقعی صبر، بی باز کردنِ پروفایل.
        for (var i = 0; i < 400 && AppSettings.Load().CloudDeviceToken.Length == 0; i++) { Pump(win); Thread.Sleep(100); }
        Settle(win);
        Report(win, vm, account, shots, "a-1-loop");
        Check("⛔ حسابِ واردشده بی باز کردنِ پروفایل هم آزمایشی گرفت",
              account.SubActive && account.VipDays >= 29, $"{account.SubPlanText} · {account.VipDays} · {account.PillText}");
        Wait(win, vm.GoAsync(account));
        for (var i = 0; i < 80 && AppSettings.Load().CloudDeviceToken.Length == 0; i++) { Pump(win); Thread.Sleep(100); }
        Report(win, vm, account, shots, "a-2-profile");
        Check("با باز کردنِ پروفایل آزمایشی آمد", account.SubActive && account.VipDays >= 29,
              $"{account.SubPlanText} · {account.VipDays} · {account.PillText}");
        win.Close();

        // ── ب) حساب با پمپ، این کامپیوتر وصل نیست ─────────────────────────
        Console.WriteLine("══ ب) حسابی که پمپ دارد ولی این کامپیوتر به آن وصل نیست");
        var b = MakeAccount("old-b-" + Guid.NewGuid().ToString("N")[..8] + "@example.com", withStation: true);
        SeatOnDisk(b, loginSkipped: false);
        (win, vm, account) = Open();
        var accB = account;
        _until = () => { accB.RefreshAll(); return AppSettings.Load().CloudDeviceToken.Length > 0 && accB.SubActive; };
        Loop(win, 2);
        Report(win, vm, account, shots, "b-loop");
        var f = AppSettings.Load();
        Check("حلقهٔ خودِ برنامه این کامپیوتر را وصل کرد", f.CloudDeviceToken.Length > 0, CloudLink.LastBindWhy);
        Check("⛔ و آزمایشی آمد (بی باز کردنِ پروفایل)", account.SubActive && account.VipDays >= 29,
              $"{account.SubPlanText} · {account.VipDays} · {account.PillText}");

        // ── د) وی‌آی‌پی از پنل، برنامه وصل و باز ───────────────────────────
        Console.WriteLine("══ د) مدیر از پنل وی‌آی‌پی داد — برنامه وصل و باز است");
        var gd = Grant(b.Email);
        Check("پنل اشتراک را ثبت کرد", gd.Length > 0, gd);
        _until = () => { accB.RefreshAll(); return accB.SubKind == "VIP"; };
        Loop(win, 2);
        Report(win, vm, account, shots, "d-vip");
        Wait(win, vm.GoAsync(account));
        Report(win, vm, account, shots, "d-vip-profile");
        Check("⛔ وی‌آی‌پی به برنامه رسید", account.SubKind == "VIP", $"{account.SubPlanText} · {account.PillText}");
        win.Close();

        // ── ج) وی‌آی‌پی از پنل پیش از وصل شدنِ برنامه ──────────────────────
        Console.WriteLine("══ ج) مدیر وی‌آی‌پی داد، بعد کاربر برنامه را باز کرد");
        var c = MakeAccount("old-c-" + Guid.NewGuid().ToString("N")[..8] + "@example.com", withStation: true);
        var gc = Grant(c.Email);
        Check("پنل اشتراک را ثبت کرد", gc.Length > 0, gc);
        SeatOnDisk(c, loginSkipped: false);
        (win, vm, account) = Open();
        var accC = account;
        _until = () => { accC.RefreshAll(); return accC.SubKind == "VIP"; };
        Loop(win, 2);
        Report(win, vm, account, shots, "c-vip");
        Wait(win, vm.GoAsync(account));
        Report(win, vm, account, shots, "c-vip-profile");
        Check("⛔ وی‌آی‌پی به برنامه رسید", account.SubKind == "VIP", $"{account.SubPlanText} · {account.PillText}");
        win.Close();

        }

        // ── ه/و) نصبی که روزی با کدِ شش‌رقمی فعال شده بود، بعد وارد حساب شد ──
        foreach (var (tag, withStation) in new[] { ("e", true), ("f", false) })
        {
            Console.WriteLine(withStation
                ? "══ ه) نصبِ قدیمی با کدِ شش‌رقمی فعال شده بود؛ صاحبش وارد حسابش (با پمپ) شد و مدیر به حسابش VIP داد"
                : "══ و) نصبِ قدیمی با کدِ شش‌رقمی فعال شده بود؛ صاحبش وارد حسابش (بی پمپ) شد، بعد مدیر VIP داد");
            var fresh = AppSettings.Load();
            fresh.CloudAccountToken = ""; fresh.CloudRefreshToken = ""; fresh.CloudUserId = ""; fresh.CloudEmail = "";
            fresh.CloudDeviceToken = ""; fresh.CloudLicense = ""; fresh.CloudStationId = ""; fresh.CloudPublicKey = "";
            fresh.CloudStationCode = ""; fresh.StationCode = ""; fresh.PumpStepDone = false; fresh.LoginSkipped = true;
            fresh.Save();
            CloudLink.AccountHasStation = null;
            var made = Panel(HttpMethod.Post, "/api/account-admin/vip-codes", new { app = "pump", plan = "std", days = 3 });
            var code = made.ValueKind == JsonValueKind.Object && made.TryGetProperty("code", out var cc) ? cc.GetString() ?? "" : "";
            Check("پنل کدِ شش‌رقمی ساخت", code.Length > 0);
            var dev = AppSettings.Load();
            var link = new CloudLink(dev, () => { dev.Save(); return Task.CompletedTask; });
            var act = Task.Run(() => link.ActivateAsync(code, "پمپِ کدی")).GetAwaiter().GetResult();
            Check("نصبِ قدیمی با کد فعال شد (پمپِ بی‌صاحب)", act.Ok, act.Why);
            var codeStation = AppSettings.Load().CloudStationId;
            var e = MakeAccount($"old-{tag}-" + Guid.NewGuid().ToString("N")[..8] + "@example.com", withStation);
            //  همان چیزی که ورودِ نخستین روی دیسک می‌گذارد: حساب، و دستگاهِ قبلی دست‌نخورده
            var fe = AppSettings.Load();
            fe.CloudAccountToken = e.Access; fe.CloudRefreshToken = e.Refresh;
            fe.CloudUserId = e.UserId; fe.CloudEmail = e.Email; fe.LoginSkipped = false;
            fe.Save();
            if (withStation)
            {
                var ge = Grant(e.Email);
                Check("پنل به پمپِ حساب VIP داد", ge.Length > 0, ge);
            }
            (win, vm, account) = Open();
            var accE = account;
            if (!withStation)
            {
                //  حساب باید صاحبِ همان پمپِ کد شود — و بعد مدیر به حسابش VIP بدهد
                var lastAsk = DateTime.MinValue;
                _until = () =>
                {
                    //  ⚠️ پنل سقفِ نرخ دارد — هر ده ثانیه یک بار بپرس
                    if (DateTime.UtcNow - lastAsk < TimeSpan.FromSeconds(10)) return false;
                    lastAsk = DateTime.UtcNow;
                    return Grant(e.Email, probeOnly: true);
                };
                Loop(win, 2);
                Report(win, vm, account, shots, $"{tag}-claimed");
                var gf = Grant(e.Email);
                Check("⛔ پمپِ کد مالِ حساب شد و مدیر با ایمیلِ حساب پیدایش کرد و VIP داد", gf.Length > 0, gf);
            }
            _until = () => { accE.RefreshAll(); return accE.SubKind == "VIP"; };
            Loop(win, 2);
            Report(win, vm, account, shots, $"{tag}-code-then-account");
            Wait(win, vm.GoAsync(account));
            Report(win, vm, account, shots, $"{tag}-code-then-account-profile");
            Console.WriteLine($"     ⓘ پمپِ کد: {codeStation} · پمپِ حالا: {AppSettings.Load().CloudStationId}");
            Check("⛔ VIPِ حساب به نصبِ قدیمیِ کددار هم رسید", account.SubKind == "VIP",
                  $"{account.SubPlanText} · {account.PillText} · {CloudLink.LastBindWhy}");
            win.Close();
        }

        Console.WriteLine("     ⓘ درخواست‌ها: " + string.Join(" · ", seen.TakeLast(80)));
        CloudLink.TestTransport = null;
        Console.WriteLine(_bad == 0 ? "✅ حساب‌های قدیمی هم آزمایشی و اشتراکشان را گرفتند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private sealed record Acct(string Email, string UserId, string Access, string Refresh);

    /// <summary>حساب با خودِ API سرورِ حساب — نه با برنامه (برنامه آن را نساخته).</summary>
    private static Acct MakeAccount(string email, bool withStation)
    {
        var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Post("/api/auth/register/start", new { name = "صاحبِ قدیمی", email, password = Pass, passwordConfirm = Pass, app = "pump" }, null);
        var code = CodeFor(email, sentAt);
        var v = Post("/api/auth/register/verify", new { email, code, app = "pump" }, null);
        var ticket = v.GetProperty("ticket").GetString()!;
        var ver = v.TryGetProperty("terms", out var t) && t.TryGetProperty("version", out var tv) ? tv.GetString() : "";
        var done = Post("/api/auth/register/complete", new
        {
            ticket, name = "صاحبِ قدیمی", password = Pass,
            terms = new { accepted = true, version = ver },
            //  ⚠️ دستگاهِ **دیگری** — این کامپیوتر نباید از همین‌جا وصل شود
            device = new { uid = "other-" + Guid.NewGuid().ToString("N")[..8], name = "old", platform = "windows" },
            app = "pump",
        }, null);
        var access = done.GetProperty("accessToken").GetString()!;
        var refresh = done.GetProperty("refreshToken").GetString()!;
        var uid = done.GetProperty("user").GetProperty("id").ToString();
        if (withStation) Post("/api/pump", new { name = "پمپِ قدیمی" }, access);
        Console.WriteLine($"     ⓘ حساب ساخته شد: {email} · پمپ؟ {withStation}");
        return new Acct(email, uid, access, refresh);
    }

    /// <summary>همان چیزی که نسخهٔ پیشین پس از ورود روی دیسک گذاشته بود — بی توکنِ دستگاه.</summary>
    private static void SeatOnDisk(Acct a, bool loginSkipped)
    {
        var f = AppSettings.Load();
        f.CloudAccountToken = a.Access;
        f.CloudRefreshToken = a.Refresh;
        f.CloudUserId = a.UserId;
        f.CloudEmail = a.Email;
        f.CloudDeviceToken = ""; f.CloudLicense = ""; f.CloudStationId = ""; f.CloudPublicKey = "";
        f.PumpStepDone = false;
        f.LoginSkipped = loginSkipped;
        f.Save();
        CloudLink.AccountHasStation = null;
    }

    private static (Avalonia.Controls.Window, MainViewModel, AccountSectionViewModel) Open()
    {
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);
        //  صفحهٔ اول، نه پروفایل — کاربر فقط برنامه را باز کرده
        Wait(win, vm.GoAsync(vm.Sections.First(x => x.Id != "account" && x.Id != "chat")));
        return (win, vm, (AccountSectionViewModel)vm.Sections.First(s => s.Id == "account"));
    }

    /// <summary>همان دورِ شصت‌ثانیه‌ایِ پس‌زمینه، n بار.</summary>
    private static void Loop(Avalonia.Controls.Window win, int n)
    {
        if (_real)
        {
            //  هر «دور» تا ۷۵ ثانیهٔ واقعی؛ زودتر اگر شرطِ بندِ جاری رسید
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(75 * n) && !(_until?.Invoke() ?? false))
            { Pump(win); Thread.Sleep(200); }
            Console.WriteLine($"     ⓘ پس از {sw.Elapsed.TotalSeconds:0} ثانیهٔ واقعی");
            Settle(win);
            return;
        }
        var m = typeof(StationPublisher).GetMethod("CloudKeepAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var i = 0; i < n; i++)
        {
            var t = (Task)m.Invoke(null, new object[] { CancellationToken.None, false })!;
            try { Wait(win, t); } catch { }
            if (t.Exception is { } ex) Console.WriteLine("     ⓘ دور خطا داد: " + ex.InnerException?.GetType().Name);
        }
        Settle(win);
    }

    private static bool Grant(string email, bool probeOnly) =>
        GrantTarget(email).Length > 0;

    private static string GrantTarget(string email)
    {
        var targets = Panel(HttpMethod.Get, "/api/account-admin/grant-targets?app=pump&q=" + Uri.EscapeDataString(email), null);
        return targets.ValueKind == JsonValueKind.Object && targets.TryGetProperty("items", out var it) && it.GetArrayLength() > 0
            ? it[0].GetProperty("tenantId").ToString() : "";
    }

    private static string Grant(string email)
    {
        var tenant = GrantTarget(email);
        if (tenant.Length == 0) return "";
        var g = Panel(HttpMethod.Post, "/api/account-admin/subs/pump/grant", new { tenantId = tenant, plan = "vip" });
        return g.ValueKind == JsonValueKind.Object && g.TryGetProperty("subscription", out var s) && s.ValueKind == JsonValueKind.Object
            ? s.GetProperty("id").ToString() : "";
    }

    private static void Report(Avalonia.Controls.Window win, MainViewModel vm, AccountSectionViewModel account,
                               string shots, string name)
    {
        account.RefreshAll();
        vm.TickLinkDot();
        Settle(win);
        var f = AppSettings.Load();
        Console.WriteLine($"     ⓘ سربرگ: {account.PillText} · پلن: {account.SubPlanText} · روز: {account.VipDays} · {account.SubKind}");
        Console.WriteLine($"     ⓘ چراغ: {vm.LinkDotBrushKey} · {vm.CloudDotReason}");
        Console.WriteLine($"     ⓘ پروفایل: LinkingNow={account.LinkingNow} · {account.LinkingLine}");
        Console.WriteLine($"     ⓘ دیسک: حساب={f.CloudAccountToken.Length > 0} دستگاه={f.CloudDeviceToken.Length > 0} مجوز={f.CloudLicense.Length > 0} پمپ={f.CloudStationId} · LastBindWhy={CloudLink.LastBindWhy}");
        using var shot = win.CaptureRenderedFrame();
        var path = Path.Combine(shots, name + ".png");
        shot?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    private static JsonElement Post(string path, object body, string? bearer)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, _pub + path)
        { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        req.Headers.TryAddWithoutValidation("X-App-Id", CloudConfig.ApplicationId);
        if (bearer is not null) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        var res = Task.Run(() => Http.SendAsync(req)).GetAwaiter().GetResult();
        var text = Task.Run(() => res.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
        if (!res.IsSuccessStatusCode) Console.WriteLine($"     ⓘ {path} ⇒ {(int)res.StatusCode} {text[..Math.Min(200, text.Length)]}");
        try { return JsonDocument.Parse(text).RootElement.Clone(); } catch { return default; }
    }

    private static JsonElement Panel(HttpMethod m, string path, object? body)
    {
        var req = new HttpRequestMessage(m, _panel + path);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _panelToken);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var res = Task.Run(() => Http.SendAsync(req)).GetAwaiter().GetResult();
        var text = Task.Run(() => res.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
        if (!res.IsSuccessStatusCode) Console.WriteLine($"     ⓘ پنل {m} {path} ⇒ {(int)res.StatusCode} {text[..Math.Min(200, text.Length)]}");
        try { return JsonDocument.Parse(text).RootElement.Clone(); } catch { return default; }
    }

    private static string CodeFor(string email, long sentAt)
    {
        for (var i = 0; i < 120; i++)
        {
            if (File.Exists(_mailCodes))
                foreach (var line in File.ReadAllLines(_mailCodes).Reverse())
                {
                    try
                    {
                        var j = JsonDocument.Parse(line).RootElement;
                        if (j.GetProperty("at").GetInt64() + 2000 < sentAt) continue;
                        if (!j.GetProperty("to").GetString()!.Contains(email, StringComparison.OrdinalIgnoreCase)) continue;
                        var c = j.GetProperty("code").GetString() ?? "";
                        if (c.Length == 6) return c;
                    }
                    catch { }
                }
            Thread.Sleep(250);
        }
        return "";
    }

    private static void Pump(Avalonia.Controls.Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Avalonia.Controls.Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Avalonia.Controls.Window w, Task t)
    {
        for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(5); }
        Settle(w);
    }
}
