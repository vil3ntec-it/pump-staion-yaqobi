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

    /// <summary>
    /// ══ «pump1 را حذف کن؛ برای هر حساب یک ایدی» (۱۴۰۵/۰۷/۱۳) — با سرورهای واقعی ══
    ///
    /// ۱) کدِ پوشهٔ سرورِ خانگی همان کدی است که سرورِ حساب برای پمپِ همین حساب
    ///    ساخته — نه «pump1».
    /// ۲) نصبِ کهنه‌ای که هنوز روی پوشهٔ «pump1» است، خودش به پوشهٔ حسابش
    ///    می‌رود و دوباره سبز می‌شود — بی هیچ کاری از کاربر.
    /// </summary>
    private static void PerAccountCode(Window win, MainViewModel vm, string homeUrl)
    {
        Console.WriteLine("── ۳ب) هر حساب، کدِ خودش — «pump1» دیگر نیست");
        var f = AppSettings.Load();
        Check("سرورِ حساب کدِ پمپِ همین حساب را داد", f.CloudStationCode.Length > 0, f.CloudStationCode);
        Check("⛔ کدِ پوشهٔ سرورِ خانگی همان کدِ حساب است", f.StationCode == f.CloudStationCode,
              f.StationCode + " / " + f.CloudStationCode);
        Check("⛔ و «pump1» نیست", !string.Equals(f.StationCode, "pump1", StringComparison.OrdinalIgnoreCase), f.StationCode);

        //  نصبِ کهنه را بسازیم: پوشهٔ «pump1» روی همان سرورِ خانگی، و رمزش روی این نصب
        var b = StationLink.HttpBase(homeUrl);
        using var enrollRes = Http.PostAsync(b + "/api/stations/enroll",
            new StringContent("{\"code\":\"pump1\",\"name\":\"پمپِ کهنه\"}", System.Text.Encoding.UTF8, "application/json"))
            .GetAwaiter().GetResult();
        var body = enrollRes.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!enrollRes.IsSuccessStatusCode)
        {
            Console.WriteLine("     ⓘ پوشهٔ pump1 روی این سرور از قبل گرفته است — نصبِ کهنه با رمزِ ساختگی: " + body);
        }
        using var doc = JsonDocument.Parse(enrollRes.IsSuccessStatusCode ? body : "{}");
        string S(string k) => doc.RootElement.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        var old = AppSettings.Load();
        var accountCode = old.CloudStationCode;
        old.StationCode = "pump1";
        old.ServerToken = S("token").Length > 0 ? S("token") : "t-legacy-" + Guid.NewGuid().ToString("N")[..8];
        old.ServerReadKey = S("readKey");
        old.Save();
        Check("نصبِ کهنه روی «pump1» ساخته شد", AppSettings.Load().StationCode == "pump1");
        Check("⇒ برنامه می‌داند باید جابه‌جا شود", StationLink.NeedsMove(AppSettings.Load()));

        var pub = AppHost.Current.PublisherIfStarted;
        var till = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        var moved = false;
        while (DateTime.UtcNow < till)
        {
            if (pub is not null) Wait(win, pub.KeepLinkAsync(true));
            Pump(win);
            vm.TickServerDot(); vm.TickCloudDot(); vm.TickLinkDot();
            var now = AppSettings.Load();
            if (!StationLink.NeedsMove(now) && now.StationCode != "pump1" && vm.ServerDotBrushKey == "Pump.Ok") { moved = true; break; }
            Thread.Sleep(500);
        }
        var after = AppSettings.Load();
        //  ⚠️ پوشهٔ خودِ حساب را همین نصب در گامِ ۳ ساخته بود و رمزش را ما با رمزِ
        //  «pump1» جایگزین کردیم، پس سرور درست «گرفته است» می‌گوید و جایگزینِ
        //  **همان حساب** (‎<کدِ حساب>-<دستگاه>‎) می‌نشیند. ملاک «مالِ همین حساب» است.
        var mineNow = after.StationCode == accountCode || after.StationCode.StartsWith(accountCode + "-");
        Check("⛔ خودش به پوشهٔ حسابش رفت — بی هیچ کاری از کاربر", moved && mineNow, after.StationCode);
        Check("و رمزِ پوشهٔ «pump1» دیگر روی این نصب نیست",
              after.ServerToken.Length > 0 && after.ServerToken != old.ServerToken);
        Check("چراغِ سرورِ خانگی دوباره سبز", vm.ServerDotBrushKey == "Pump.Ok", vm.ServerDotBrushKey + " · " + vm.ServerDotReason);
        //  پوشهٔ همین حساب واقعاً روی سرورِ خانگی هست و عکسِ زنده به آن رسید
        var liveOk = false;
        var rk = after.ServerReadKey;
        var liveTill = DateTime.UtcNow + TimeSpan.FromSeconds(40);
        while (!liveOk && DateTime.UtcNow < liveTill)
        {
            if (pub is not null) Wait(win, pub.PublishOnceAsync(true));
            var url = StationLink.LiveUrl(homeUrl, after.StationCode, rk);
            if (url is not null)
            {
                using var r = Http.GetAsync(url).GetAwaiter().GetResult();
                liveOk = r.IsSuccessStatusCode && r.Content.ReadAsStringAsync().GetAwaiter().GetResult().Contains("sections");
            }
            if (!liveOk) Thread.Sleep(1000);
        }
        Check("⛔ عکسِ زنده در پوشهٔ **همین حساب** روی سرورِ خانگی نشست", liveOk, after.StationCode);

        //  و اپِ کارمندان همان پوشه را از سرورِ حساب می‌گیرد، نه کدِ خام را
        var joinOk = false; var joinSaw = "";
        var joinTill = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!joinOk && DateTime.UtcNow < joinTill)
        {
            if (pub is not null) Wait(win, pub.PublishOnceAsync(true));
            var fs = AppSettings.Load();
            var cl = new CloudLink(fs, () => { fs.Save(); return Task.CompletedTask; });
            var ac = Task.Run(() => cl.AccessCodeAsync()).GetAwaiter().GetResult();
            if (ac.Ok && ac.Code.Length > 0)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, new Uri(PubBase, "/api/pump/public/join"))
                { Content = new StringContent("{\"code\":\"" + ac.Code + "\"}", System.Text.Encoding.UTF8, "application/json") };
                using var jr = Http.SendAsync(req).GetAwaiter().GetResult();
                var jt = jr.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (jr.IsSuccessStatusCode)
                {
                    using var jd = JsonDocument.Parse(jt);
                    joinSaw = jd.RootElement.GetProperty("home").GetProperty("station").GetString() ?? "";
                    joinOk = joinSaw == after.StationCode;
                }
                else joinSaw = jt;
            }
            if (!joinOk) Thread.Sleep(1500);
        }
        Check("⛔ گوشیِ کارمند همان پوشه را می‌گیرد که برنامه رویش می‌نویسد", joinOk,
              joinSaw + " / " + after.StationCode);
    }

    /// <summary>پورتِ عمومیِ پنل — همان چیزی که تونل می‌بیند.</summary>
    private static Uri PubBase = null!;

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
        PubBase = pub;
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

        PerAccountCode(win, vm, found?.Url ?? f2.ServerUrl);

        //  عکس: سربرگ با کادرِ توضیحِ همان چراغ
        Wait(win, vm.GoAsync(vm.Sections.First(s => s.Id == "safe")));
        Settle(win);
        var dot = win.GetVisualDescendants().OfType<Button>()
                     .FirstOrDefault(b => b.Command == vm.CheckLinksCommand && b.IsEffectivelyVisible);
        if (dot is not null) { ToolTip.SetIsOpen(dot, true); Settle(win); }
        Shot(win, shots, "app-server-light");
        if (dot is not null) ToolTip.SetIsOpen(dot, false);

        SubscriptionRoundTrip(win, vm, live, shots, email);

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

    /// <summary>
    /// ══ ۴) اشتراکی که مدیر در پنل می‌دهد و برمی‌دارد — در برنامه ═══════════
    ///
    /// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «ببین با دقت می‌گیرد اشتراک را، توی
    /// برنامه هم می‌گوید چقدر مانده یا فعال شده یا که نه.»
    ///
    /// ⛔ کارِ پنل از **همان دری** زده می‌شود که صاحبِ سامانه در مرورگر می‌زند
    /// (‎/api/account-admin/…‎ روی پورتِ پنل)، و برنامه از **راهِ خودش** —
    /// حلقهٔ پس‌زمینهٔ ‎StationPublisher‎ — می‌گیردش؛ هیچ صدا زدنِ دستی‌ای
    /// در کار نیست. «رسید» یعنی همان چیزی که کاربر روی صفحه می‌بیند.
    /// </summary>
    private static void SubscriptionRoundTrip(Window win, MainViewModel vm, JsonElement live, string shots, string email)
    {
        if (!live.TryGetProperty("panel", out var pEl) || !live.TryGetProperty("panelToken", out var tEl))
        {
            Console.WriteLine("⚠️ نشانی یا توکنِ پنل در live.json نیست — بخشِ اشتراک رد شد.");
            return;
        }
        var panel = pEl.GetString()!.TrimEnd('/');
        var token = tEl.GetString()!;
        JsonElement Panel(HttpMethod m, string path, object? body = null)
        {
            var req = new HttpRequestMessage(m, panel + path);
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            if (body is not null)
                req.Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            var res = Task.Run(() => Http.SendAsync(req)).GetAwaiter().GetResult();
            var text = Task.Run(() => res.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode) Console.WriteLine($"     ⓘ پنل {m} {path} ⇒ {(int)res.StatusCode} {text[..Math.Min(200, text.Length)]}");
            try { return JsonDocument.Parse(text).RootElement.Clone(); } catch { return default; }
        }

        var account = (AccountSectionViewModel)vm.Sections.First(s => s.Id == "account");
        string Line() => $"{account.SubPlanText} · {account.SubDaysText} · تا {account.SubEndsText} · سربرگ: {account.PillText}";

        /*
         *  «رسید» فقط با **حلقهٔ خودِ برنامه**: تیکِ ابرش شصت‌ثانیه‌ای است،
         *  پس تا نود ثانیه صبر می‌کنیم. ⚠️ و خودِ صفحه دوباره فعال **نمی‌شود**
         *  — سربرگ باید بی باز کردنِ پروفایل تازه شود، وگرنه کاربر عددِ کهنه
         *  را می‌بیند و گمان می‌کند اشتراک نرسید.
         */
        bool WaitFor(Func<bool> ok, int seconds = 95)
        {
            var end = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < end)
            {
                Pump(win);
                if (ok()) return true;
                Thread.Sleep(250);
            }
            return ok();
        }

        Console.WriteLine("── ۴) اشتراک از پنل ⇒ برنامه (با حلقهٔ خودِ برنامه، بی صدا زدنِ دستی)");
        Wait(win, vm.GoAsync(account));
        Settle(win);
        Console.WriteLine("     ⓘ پیش از دادن: " + Line());
        Check("پیش از هر اشتراک، دورهٔ آزمایشیِ حسابِ تازه دیده می‌شود (نه «فعال نشده»)",
              account.SubActive && account.SubDaysText != "—", Line());
        Check("⛔ و «آزمایشی» خوانده می‌شود، نه «VIP»",
              account.SubKind == "آزمایشی" && account.PillText.StartsWith("آزمایشی"), Line());
        Shot(win, shots, "app-sub-1-trial");

        var targets = Panel(HttpMethod.Get, "/api/account-admin/grant-targets?app=pump&q=" + Uri.EscapeDataString(email));
        var tenant = targets.ValueKind == JsonValueKind.Object && targets.TryGetProperty("items", out var it) && it.GetArrayLength() > 0
            ? it[0].GetProperty("tenantId").GetString() ?? "" : "";
        Check("پنل پمپِ همین حساب را با ایمیل پیدا کرد", tenant.Length > 0);
        if (tenant.Length == 0) return;

        //  ── الف) دادنِ وی‌آی‌پی ─────────────────────────────────────────
        var t0 = DateTime.UtcNow;
        var g = Panel(HttpMethod.Post, "/api/account-admin/subs/pump/grant", new { tenantId = tenant, plan = "vip" });
        var subId = g.ValueKind == JsonValueKind.Object && g.TryGetProperty("subscription", out var s) && s.ValueKind == JsonValueKind.Object
            ? (s.TryGetProperty("id", out var id) ? id.ToString() : "") : "";
        Check("پنل اشتراکِ وی‌آی‌پی را ثبت کرد", subId.Length > 0, subId);
        var got = WaitFor(() => account.SubKind == "VIP");
        Console.WriteLine($"     ⓘ پس از {(DateTime.UtcNow - t0).TotalSeconds:0} ثانیه: " + Line());
        Check("برنامه خودش وی‌آی‌پی را گرفت (پلن در پروفایل)", got, Line());
        Check("روزِ مانده نزدیکِ یک سال است", account.VipDays is >= 360 and <= 366, account.VipDays.ToString());
        Check("سربرگ بی باز کردنِ دوبارهٔ پروفایل تازه شد", account.PillText == $"VIP · {account.VipDays} روز", account.PillText);
        Check("قفل‌ها باز: فهرستِ قابلیت‌های مجوز از خودِ پلن", Entitlements.State().Features.Count >= 5,
              string.Join(",", Entitlements.State().Features));
        Shot(win, shots, "app-sub-2-vip");

        //  ── ب) تمدید یک ماه ─────────────────────────────────────────────
        var before = account.VipDays;
        Panel(HttpMethod.Post, $"/api/account-admin/subs/pump/{subId}/extend", new { amount = 1, unit = "month" });
        got = WaitFor(() => account.VipDays >= before + 27);
        Check("تمدیدِ یک ماه در برنامه دیده شد", got, $"{before} ⇒ {account.VipDays}");
        if (account.SubKind != "VIP") { Check("⛔ بی وی‌آی‌پیِ رسیده، لغو سنجیدنی نیست", false); return; }

        //  ── ج) لغو ────────────────────────────────────────────────────
        t0 = DateTime.UtcNow;
        Panel(HttpMethod.Post, $"/api/account-admin/subs/pump/{subId}/status", new { status = "cancelled" });
        got = WaitFor(() => account.SubKind != "VIP");
        Console.WriteLine($"     ⓘ پس از {(DateTime.UtcNow - t0).TotalSeconds:0} ثانیه: " + Line());
        Check("لغو در برنامه دیده شد (وی‌آی‌پی رفت)", got, Line());
        Check("⛔ پس از لغو، «۳۶۵ روز مانده»ی کهنه نمانده", account.VipDays < 300, Line());
        //  ⛔ و دورهٔ آزمایشی **برنمی‌گردد**: تا ۱۴۰۵/۰۷/۱۳ لغوِ اشتراکِ حسابِ
        //  تازه برنامه را به «آزمایشی · ۲۹ روز» با همهٔ قابلیت‌ها می‌برد، یعنی
        //  «حذفِ اشتراک» در ماهِ اول هیچ اثری نداشت.
        Check("⛔ پس از لغو، دورهٔ آزمایشی برنگشت — اشتراکی نیست", !account.SubActive && account.PillText == "پروفایل", Line());
        Shot(win, shots, "app-sub-3-cancelled");
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
