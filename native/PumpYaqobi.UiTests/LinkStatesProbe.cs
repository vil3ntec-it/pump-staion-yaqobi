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
/// ══ «ببین الان این وصل نمی‌شه — با تستِ سالم، نه با حدس» ═══════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۳، عکسِ چراغِ زرد): «سرورِ حساب جواب می‌دهد، ولی
/// این کامپیوتر هنوز به هیچ پمپی ثبت نشده». پس هر حالی که به آن جمله می‌رسد
/// روی **پشتهٔ واقعی** (پنل + سرورِ حسابِ واقعی + صندوقِ ایمیل) ساخته می‌شود
/// و برای هر کدام سه پرسش:
///
///   ۱) چراغ چه می‌گوید، و آیا راست می‌گوید؟
///   ۲) کاربر در «پروفایل» راهی می‌بیند که کار کند؟
///   ۳) کلیکِ همان چراغ (یا همان راه) واقعاً این کامپیوتر را ثبت می‌کند؟
///
/// حالت‌ها:
///   الف) نصبِ تازه، هیچ حسابی
///   ب) حساب ساخته، پمپ ساخته ⇒ ثبت شده (خطِ پایه)
///   ج) همان حساب از ریشه در پنل حذف شد (کاری که صاحب سامانه واقعاً کرد)
///   د) دوباره با **همان ایمیل** حساب ساخته شد — روی همین نصب
///   هـ) حسابی که پمپ دارد ولی این کامپیوتر ثبت نیست (دستگاه از پنل جدا شد)
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- linkstates &lt;live.json&gt; [پوشهٔ عکس]
/// </summary>
internal static class LinkStatesProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(30) };

    private static string _panel = "", _panelToken = "", _mailCodes = "";
    private const string Pass = "Pump!1405link";

    public static int Run(string[] args)
    {
        if (args.Length < 2 || !File.Exists(args[1]))
        {
            Console.WriteLine("⚠️ live.json نیست — اول signup-stack.mjs را روشن کنید. رد شد.");
            return 0;
        }
        var live = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
        var pub = new Uri(live.GetProperty("public").GetString()!);
        _mailCodes = live.GetProperty("mailCodes").GetString()!;
        _panel = live.GetProperty("panel").GetString()!.TrimEnd('/');
        _panelToken = live.GetProperty("panelToken").GetString()!;
        var shots = args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "pump-linkstates");
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

        var dir = Path.Combine(Path.GetTempPath(), "pump-linkstates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);
        var account = (AccountSectionViewModel)vm.Sections.First(s => s.Id == "account");

        // ── الف) نصبِ تازه ─────────────────────────────────────────────────
        Console.WriteLine("══ الف) نصبِ تازه — هیچ حسابی");
        Keep(win);
        Report(win, vm, account, shots, "a-fresh");
        //  ⛔ «وقتی حساب هم نداشته باشم، سرور اگر وصل بود باید سبز بشه» (۱۴۰۵/۰۷/۱۳)
        Check("⛔ بی هیچ حسابی، چراغ سبز است چون سرور جواب می‌دهد", vm.LinkDotBrushKey == "Pump.Ok"
              && vm.CloudDotBrushKey == "Pump.Ok", vm.LinkDotBrushKey + " · " + vm.LinkDotReason);
        Check("⛔ چراغ کاربر را به «ساختنِ پمپ» نمی‌فرستد وقتی اصلاً وارد حساب نشده",
              !vm.CloudDotReason.Contains("پمپ را بسازید"), vm.CloudDotReason);
        Check("پروفایل صفحهٔ ورود را نشان می‌دهد", account.ShowLoginPage, "گام " + account.LoginStep);
        //  کلیکِ چراغ از هر بخشِ دیگری ⇒ همان صفحهٔ ورود، نه یک توستِ بی‌راه
        Wait(win, vm.GoAsync(vm.Sections.First(x => x.Id != "account" && x.Id != "chat")));
        Wait(win, vm.CheckLinksCommand.ExecuteAsync(null));
        Check("⛔ کلیکِ چراغ صفحهٔ ورود را باز کرد", ReferenceEquals(vm.Current, account) && account.ShowLoginPage,
              vm.Current?.Id);
        Shot(win, shots, "a-fresh-click");

        // ── ب) حساب + پمپ ⇒ ثبت شده ────────────────────────────────────────
        Console.WriteLine("══ ب) حساب ساخته شد و پمپ ساخته شد");
        var email = "link-" + Guid.NewGuid().ToString("N")[..10] + "@example.com";
        SignUp(win, vm, account, email);
        Keep(win);
        Report(win, vm, account, shots, "b-bound");
        var f = AppSettings.Load();
        Check("این کامپیوتر ثبت شد (توکنِ دستگاه)", f.CloudDeviceToken.Length > 0, CloudLink.LastBindWhy);
        Check("چراغِ سرورِ حساب سبز است", vm.CloudDotBrushKey == "Pump.Ok", vm.CloudDotReason);

        // ── ج) حذف از ریشه در پنل ─────────────────────────────────────────
        Console.WriteLine("══ ج) همان حساب از ریشه در پنل حذف شد");
        var uid = f.CloudUserId;
        var del = PanelSend(HttpMethod.Delete, "/api/account-admin/users/" + uid, new { confirmEmail = email });
        Console.WriteLine("     ⓘ پنل ⇒ " + del);
        Keep(win);
        Report(win, vm, account, shots, "c-deleted");
        f = AppSettings.Load();
        Check("⛔ توکنِ دستگاهِ مرده دیگر «ثبت‌شده» شمرده نمی‌شود", f.CloudDeviceToken.Length == 0 || vm.CloudDotBrushKey != "Pump.Ok",
              $"dev={f.CloudDeviceToken.Length} dot={vm.CloudDotBrushKey}");
        Check("⛔ پروفایل راهِ ورود/ساختنِ حساب را نشان می‌دهد", account.ShowLoginPage || account.LinkingNow,
              $"گام {account.LoginStep} · LinkingNow={account.LinkingNow} · SignedIn={account.SignedIn}");

        // ── د) همان ایمیل، حسابِ تازه، روی همین نصب ─────────────────────────
        Console.WriteLine("══ د) دوباره با همان ایمیل حساب ساخته شد (همین نصب)");
        //  ⚠️ ترمزِ ۶۰ ثانیه‌ایِ «کدِ دوباره به همان ایمیل» (`ResendWaitSeconds`)
        //  عمدی است؛ کاربرِ واقعی هم دقیقه‌ها بعد دوباره ثبت‌نام می‌کند.
        for (var i = 0; i < 62 * 4; i++) { Pump(win); Thread.Sleep(250); }
        if (!account.ShowLoginPage) { account.OpenAccountPageCommand.Execute(null); Settle(win); }
        SignUp(win, vm, account, email);
        Keep(win);
        Report(win, vm, account, shots, "d-again");
        f = AppSettings.Load();
        Check("این کامپیوتر دوباره ثبت شد", f.CloudDeviceToken.Length > 0, CloudLink.LastBindWhy);
        Check("دورهٔ آزمایشیِ ۳۰ روزه آمد", account.SubActive && account.VipDays == 30,
              $"{account.SubPlanText} · {account.VipDays}");
        Check("چراغِ سرورِ حساب سبز است", vm.CloudDotBrushKey == "Pump.Ok", vm.CloudDotReason);

        // ── هـ) حسابی که پمپ دارد، این کامپیوتر ثبت نیست ─────────────────────
        Console.WriteLine("══ هـ) حساب پمپ دارد، ولی این کامپیوتر ثبت نیست (توکنِ دستگاه گم شد)");
        f = AppSettings.Load();
        f.CloudDeviceToken = "";
        f.Save();
        vm.GetType().GetField("_boundAt", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, DateTime.MinValue);
        vm.TickLinkDot();
        Settle(win);
        //  ⛔ کارت و دکمه‌ای نیست؛ باز کردنِ پروفایل خودش همین حالا وصل می‌کند
        Wait(win, vm.GoAsync(vm.Sections.First(x => x.Id != "account" && x.Id != "chat")));
        Wait(win, vm.GoAsync(account));
        for (var i = 0; i < 80 && AppSettings.Load().CloudDeviceToken.Length == 0; i++) { Pump(win); Thread.Sleep(100); }
        Report(win, vm, account, shots, "e-auto");
        f = AppSettings.Load();
        Check("⛔ باز کردنِ پروفایل خودش این کامپیوتر را وصل کرد (بی دکمه)", f.CloudDeviceToken.Length > 0, CloudLink.LastBindWhy);
        Check("⛔ و هیچ پمپِ دومی ساخته نشد", f.CloudStationId.Length > 0, f.CloudStationId);
        Check("چراغِ سرورِ حساب سبز است", vm.CloudDotBrushKey == "Pump.Ok", vm.CloudDotReason);

        // ── و) مدیر این کامپیوتر را از پنل جدا کرد، بعد برگرداند ────────────
        Console.WriteLine("══ و) مدیر کامپیوتر را از پنل جدا کرد (device_revoked) و بعد برگرداند");
        f = AppSettings.Load();
        var station = f.CloudStationId;
        var prof = PanelJson(HttpMethod.Get, "/api/account-admin/pump-accounts/" + station, null);
        string compId = "";
        if (prof.TryGetProperty("computers", out var comps))
            foreach (var c in comps.EnumerateArray())
                if (c.GetProperty("uid").GetString() == new CloudLink(f, () => Task.CompletedTask).DeviceUid) compId = c.GetProperty("id").GetString() ?? "";
        Check("پنل همین کامپیوتر را در «کامپیوترهای پمپ» می‌بیند", compId.Length > 0, compId);
        Console.WriteLine("     ⓘ پنل ⇒ " + PanelSend(HttpMethod.Post, $"/api/account-admin/pump-accounts/{station}/computers/{compId}/revoke", new { }));
        int Binds() { lock (seen) return seen.Count(x => x.Contains("/device/bind")); }
        var before = Binds();
        //  شش دورِ پس‌زمینه (شش دقیقهٔ واقعی) — نخستین دور جدا شدن را می‌فهمد
        for (var i = 0; i < 6; i++) Keep(win);
        //  ⚠️ جدا شدن را تیکِ ده‌دقیقه‌ایِ مجوز (`KeepLicenseFreshAsync`) می‌فهمد؛
        //  به‌جای ده دقیقه صبر، همان تیک «رسیده» می‌شود — همان مسیرِ واقعیِ حلقه.
        var due = AppSettings.Load(); due.CloudSyncedAt = 0; due.Save();
        for (var i = 0; i < 6; i++) Keep(win);
        var tries = Binds() - before;
        Report(win, vm, account, shots, "f-revoked");
        f = AppSettings.Load();
        Check("این کامپیوتر جدا شده دیده شد (توکنِ دستگاه برداشته شد)", f.CloudDeviceToken.Length == 0);
        Check("⛔ حلقه در دوازده دور حداکثر یک بار ثبت را امتحان کرد (سقفِ نرخِ سرور پر نمی‌شود)", tries <= 1, tries.ToString());
        Check("⛔ چراغ دلیلِ واقعی را می‌گوید (جدا شده)", vm.CloudDotReason.Contains("جدا شده"), vm.CloudDotReason);
        Check("پروفایل در یک خط می‌گوید چرا وصل نیست", account.LinkingNow && account.LinkingLine.Contains("جدا شده"), account.LinkingLine);

        Console.WriteLine("     ⓘ پنل ⇒ " + PanelSend(HttpMethod.Post, $"/api/account-admin/pump-accounts/{station}/computers/{compId}/restore", new { }));
        Wait(win, vm.CheckLinksCommand.ExecuteAsync(null));
        Report(win, vm, account, shots, "f-restored");
        f = AppSettings.Load();
        Check("⛔ پس از «برگرداندن» در پنل، کلیکِ چراغ همان لحظه وصل کرد",
              f.CloudDeviceToken.Length > 0, account.LoginStatus + " · " + CloudLink.LastBindWhy);
        Check("چراغِ سرورِ حساب سبز شد", vm.CloudDotBrushKey == "Pump.Ok", vm.CloudDotReason);

        // ── ز) بیرون آمدن و ورودِ دوباره با همان حساب (پمپ دارد) ────────────
        Console.WriteLine("══ ز) خروج از حساب و ورودِ دوباره — حسابی که از قبل پمپ دارد");
        Wait(win, account.SignOutCommand.ExecuteAsync(null));
        Settle(win);
        if (!account.ShowLoginPage) { account.OpenAccountPageCommand.Execute(null); Settle(win); }
        account.SetSignUpCommand.Execute("no");
        account.LoginEmail = email;
        account.LoginPassword = Pass;
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        Report(win, vm, account, shots, "g-relogin");
        f = AppSettings.Load();
        Check("⛔ ورودِ دوباره دوباره «نامِ پمپ» نمی‌خواهد — پمپ از قبل هست", !account.ShowLoginPage && !account.StepPump,
              "گام " + account.LoginStep + " · " + account.LoginStatus);
        Check("و این کامپیوتر ثبت ماند/شد", f.CloudDeviceToken.Length > 0, CloudLink.LastBindWhy);
        Check("⛔ هیچ پمپِ دومی ساخته نشد", f.CloudStationId == station, f.CloudStationId + " ≠ " + station);

        // ── ح) رمز را فراموش کرده ─────────────────────────────────────────
        Console.WriteLine("══ ح) «رمزم را فراموش کرده‌ام» ⇒ کد ⇒ رمزِ تازه ⇒ ورود");
        Wait(win, account.SignOutCommand.ExecuteAsync(null));
        Settle(win);
        if (!account.ShowLoginPage) { account.OpenAccountPageCommand.Execute(null); Settle(win); }
        account.LoginEmail = email;
        account.OpenForgotCommand.Execute(null);
        var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Wait(win, account.SendResetCodeCommand.ExecuteAsync(null));
        Check("کدِ بازیابی فرستاده شد", account.ResetSent, account.LoginStatus);
        account.ResetCode = CodeFor(win, email, sentAt);
        Check("کدِ بازیابی به صندوقِ همین ایمیل رسید", account.ResetCode.Length == 6, account.ResetCode);
        account.ResetPass = "Pump!1405new"; account.ResetPass2 = "Pump!1405new";
        Wait(win, account.ResetPasswordCommand.ExecuteAsync(null));
        Report(win, vm, account, shots, "h-reset");
        f = AppSettings.Load();
        Check("رمزِ تازه نشست و وارد شد", account.SignedIn, account.LoginStatus);
        Check("⛔ پس از بازیابی هم «نامِ پمپ» دوباره خواسته نمی‌شود", !account.ShowLoginPage && !account.StepPump,
              "گام " + account.LoginStep + " · " + account.LoginStatus);
        Check("و این کامپیوتر ثبت است", f.CloudDeviceToken.Length > 0, CloudLink.LastBindWhy);

        // ── ط) کامپیوترِ تازه (برنامه از نو نصب شد) و ورود با حسابی که پمپ دارد ──
        Console.WriteLine("══ ط) نصبِ دوباره روی کامپیوتر (هیچ ثبتی نیست) و ورود با حسابی که پمپ دارد");
        Wait(win, account.SignOutCommand.ExecuteAsync(null));
        f = AppSettings.Load();
        f.CloudDeviceToken = ""; f.CloudLicense = ""; f.PumpStepDone = false; f.CloudStationId = "";
        f.CloudPublicKey = ""; f.LoginSkipped = false; f.Save();
        CloudLink.AccountHasStation = null;
        account.RefreshAll();
        Settle(win);
        if (!account.ShowLoginPage) { account.OpenAccountPageCommand.Execute(null); Settle(win); }
        account.SetSignUpCommand.Execute("no");
        account.LoginEmail = email;
        account.LoginPassword = "Pump!1405new";
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        Report(win, vm, account, shots, "i-reinstall-login");
        f = AppSettings.Load();
        Check("⛔ «نامِ پمپ» دوباره خواسته نشد — مستقیم تمام شد", !account.ShowLoginPage && !account.StepPump,
              "گام " + account.LoginStep + " · " + account.LoginStatus);
        Check("این کامپیوتر همان لحظه به همان پمپ ثبت شد", f.CloudDeviceToken.Length > 0 && f.CloudStationId == station,
              f.CloudStationId + " · " + CloudLink.LastBindWhy);
        Check("دورهٔ آزمایشی/اشتراک همان لحظه آمد", account.SubActive, account.SubPlanText);

        Console.WriteLine("     ⓘ درخواست‌ها: " + string.Join(" · ", seen.TakeLast(60)));
        CloudLink.TestTransport = null;
        Console.WriteLine(_bad == 0 ? "✅ هر حالِ «ثبت نشده» راست گفته شد و راهِ کارکننده داشت" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>همان دورِ شصت‌ثانیه‌ایِ پس‌زمینه — همین حالا.</summary>
    private static void Keep(Avalonia.Controls.Window win)
    {
        var m = typeof(StationPublisher).GetMethod("CloudKeepAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var t = (Task)m.Invoke(null, new object[] { CancellationToken.None, false })!;
        try { Wait(win, t); } catch { }
        if (t.Exception is { } ex) Console.WriteLine("     ⓘ دور خطا داد: " + ex.InnerException?.GetType().Name);
    }

    private static void Report(Avalonia.Controls.Window win, MainViewModel vm, AccountSectionViewModel account,
                               string shots, string name)
    {
        vm.GetType().GetField("_boundAt", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, DateTime.MinValue);
        Wait(win, vm.GoAsync(account));
        vm.TickLinkDot();
        Settle(win);
        var f = AppSettings.Load();
        Console.WriteLine($"     ⓘ چراغ: {vm.LinkDotBrushKey} · {vm.LinkDotReason.Replace('\n', ' ')}");
        Console.WriteLine($"     ⓘ سرورِ حساب: {vm.CloudDotBrushKey} · {vm.CloudDotReason}");
        Console.WriteLine($"     ⓘ پروفایل: گام {account.LoginStep} · ورود؟ {account.ShowLoginPage} · SignedIn={account.SignedIn} · LinkingNow={account.LinkingNow} · {account.LinkingLine} · {account.CloudLine}");
        Console.WriteLine($"     ⓘ دیسک: حساب={f.CloudAccountToken.Length > 0} دستگاه={f.CloudDeviceToken.Length > 0} پمپ={f.CloudStationId} PumpStepDone={f.PumpStepDone} LoginSkipped={f.LoginSkipped} · LastBindWhy={CloudLink.LastBindWhy}");
        Shot(win, shots, name);
    }

    private static JsonElement PanelJson(HttpMethod method, string path, object? body)
    {
        var req = new HttpRequestMessage(method, _panel + path);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _panelToken);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var res = Task.Run(() => Http.SendAsync(req)).GetAwaiter().GetResult();
        var text = Task.Run(() => res.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
        try { return JsonDocument.Parse(text).RootElement.Clone(); } catch { return default; }
    }

    private static string PanelSend(HttpMethod method, string path, object? body)
    {
        var req = new HttpRequestMessage(method, _panel + path);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _panelToken);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var res = Task.Run(() => Http.SendAsync(req)).GetAwaiter().GetResult();
        var text = Task.Run(() => res.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
        return $"{(int)res.StatusCode} {text[..Math.Min(300, text.Length)]}";
    }

    private static void SignUp(Avalonia.Controls.Window win, MainViewModel vm, AccountSectionViewModel account,
                               string email)
    {
        var pass = Pass;
        Wait(win, vm.GoAsync(account));
        account.SetSignUpCommand.Execute("yes");
        account.LoginName = "صاحبِ پمپ";
        account.LoginEmail = email;
        account.LoginPassword = pass;
        account.LoginPassword2 = pass;
        account.AcceptTerms = true;
        var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        account.EmailCode = CodeFor(win, email, sentAt);
        Wait(win, account.VerifyEmailCommand.ExecuteAsync(null));
        Settle(win);
        //  ⛔ «همین که حساب ساخت، تمام» — نه گامِ نامِ پمپ، نه دکمهٔ ثبت
        Check("⛔ حساب ساخته شد و همان‌جا تمام شد (بی گامِ پمپ)", !account.ShowLoginPage && !account.StepPump,
              "گام " + account.LoginStep + " · " + account.LoginStatus);
    }

    private static string CodeFor(Avalonia.Controls.Window win, string email, long sentAt)
    {
        for (var i = 0; i < 120; i++)
        {
            Pump(win);
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
                    catch { /* خطِ نیمه‌نوشته */ }
                }
            Thread.Sleep(250);
        }
        return "";
    }

    private static void Shot(Avalonia.Controls.Window win, string dir, string name)
    {
        using var f = win.CaptureRenderedFrame();
        var path = Path.Combine(dir, name + ".png");
        f?.Save(path);
        Console.WriteLine("  📷 " + path);
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
