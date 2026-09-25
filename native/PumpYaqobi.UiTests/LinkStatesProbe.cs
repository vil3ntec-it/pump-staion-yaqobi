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
        Check("چراغ سبز نیست", vm.LinkDotBrushKey != "Pump.Ok", vm.LinkDotReason);
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
        SignUp(win, vm, account, email, finishPump: true);
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
        Check("⛔ پروفایل راهِ ورود/ساختنِ حساب را نشان می‌دهد", account.ShowLoginPage || account.NeedsPump,
              $"گام {account.LoginStep} · NeedsPump={account.NeedsPump} · SignedIn={account.SignedIn}");

        // ── د) همان ایمیل، حسابِ تازه، روی همین نصب ─────────────────────────
        Console.WriteLine("══ د) دوباره با همان ایمیل حساب ساخته شد (همین نصب)");
        //  ⚠️ ترمزِ ۶۰ ثانیه‌ایِ «کدِ دوباره به همان ایمیل» (`ResendWaitSeconds`)
        //  عمدی است؛ کاربرِ واقعی هم دقیقه‌ها بعد دوباره ثبت‌نام می‌کند.
        for (var i = 0; i < 62 * 4; i++) { Pump(win); Thread.Sleep(250); }
        if (!account.ShowLoginPage) { account.OpenAccountPageCommand.Execute(null); Settle(win); }
        SignUp(win, vm, account, email, finishPump: true);
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
        Report(win, vm, account, shots, "e-before-click");
        Check("⛔ پروفایل کارتِ «ثبتِ همین کامپیوتر» را نشان می‌دهد (نه «ساختنِ پمپ»)",
              account.NeedsBind && !account.NeedsPump, $"NeedsBind={account.NeedsBind} NeedsPump={account.NeedsPump}");
        Check("⛔ چراغ «پمپ بسازید» نمی‌گوید به حسابی که پمپ دارد", !vm.CloudDotReason.Contains("پمپ را بسازید")
              && !vm.CloudDotReason.Contains("نامِ پمپ"), vm.CloudDotReason);
        //  همان کاری که کاربر می‌کند: کلیک روی چراغ
        Wait(win, vm.CheckLinksCommand.ExecuteAsync(null));
        vm.GetType().GetField("_boundAt", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, DateTime.MinValue);
        vm.TickLinkDot();
        Settle(win);
        Report(win, vm, account, shots, "e-after-click");
        f = AppSettings.Load();
        Check("⛔ کلیکِ چراغ همین حالا این کامپیوتر را ثبت کرد", f.CloudDeviceToken.Length > 0, CloudLink.LastBindWhy);
        Check("چراغِ سرورِ حساب سبز شد", vm.CloudDotBrushKey == "Pump.Ok", vm.CloudDotReason);

        Console.WriteLine("     ⓘ درخواست‌ها: " + string.Join(" · ", seen.TakeLast(60)));
        CloudLink.TestTransport = null;
        Console.WriteLine(_bad == 0 ? "✅ هر حالِ «ثبت نشده» راست گفته شد و راهِ کارکننده داشت" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>همان دورِ شصت‌ثانیه‌ایِ پس‌زمینه — همین حالا.</summary>
    private static void Keep(Avalonia.Controls.Window win)
    {
        var m = typeof(StationPublisher).GetMethod("CloudKeepAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var t = (Task)m.Invoke(null, new object[] { CancellationToken.None })!;
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
        Console.WriteLine($"     ⓘ پروفایل: گام {account.LoginStep} · ورود؟ {account.ShowLoginPage} · SignedIn={account.SignedIn} · NeedsPump={account.NeedsPump} · NeedsBind={account.NeedsBind} · {account.CloudLine}");
        Console.WriteLine($"     ⓘ دیسک: حساب={f.CloudAccountToken.Length > 0} دستگاه={f.CloudDeviceToken.Length > 0} پمپ={f.CloudStationId} PumpStepDone={f.PumpStepDone} LoginSkipped={f.LoginSkipped} · LastBindWhy={CloudLink.LastBindWhy}");
        Shot(win, shots, name);
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
                               string email, bool finishPump)
    {
        const string pass = "Pump!1405link";
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
        Check("حساب ساخته شد و صفحه به گامِ پمپ رسید", account.StepPump, "گام " + account.LoginStep + " · " + account.LoginStatus);
        if (!finishPump) return;
        account.LoginPump = "پمپ آزمونِ اتصال";
        Wait(win, account.FinishPumpCommand.ExecuteAsync(null));
        Settle(win);
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
