using System.Net.Http;
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
/// ══ «حساب ساختم و کدِ شش‌رقمی را زدم، ولی ۳۰ روزِ آزمایشی را ندادند» ════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «ببین مشکل از برنامه است یا سرور که نداد،
/// عکس بگیر.» پس همان راهِ کاربر، قدم‌به‌قدم، از **خودِ صفحهٔ ورودِ برنامه**
/// — نه از راهِ API:
///
///   ۱) «حساب می‌سازم»: نام، ایمیل، رمز، تکرار، شرایط ⇒ کد به ایمیل
///   ۲) همان کدی که به صندوقِ ایمیل رسید ⇒ حساب ساخته شد
///   ۳) نامِ پمپ ⇒ پمپ روی سرور ساخته و این کامپیوتر بند می‌شود
///   ۴) پروفایل: «آزمایشی · ۳۰ روز»؟ — و همان چیزی که پنلِ مدیر می‌بیند
///
/// ⚠️ سرورها واقعی‌اند و بیرون از این فرآیند روشن‌اند (‎signup-stack.mjs‎: پنلِ
/// خانگی + سرورِ حسابِ واقعی روی PGlite + یک صندوقِ ایمیلِ کوچک). تنها چیزی
/// که عوض می‌شود مقصدِ ابر است (‎CloudLink.TestTransport‎ ⇒ پورتِ عمومیِ همان
/// پنل)؛ نشانیِ قفل‌شدهٔ کد دست نمی‌خورد.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- signuptrial &lt;live.json&gt; [پوشهٔ عکس]
/// </summary>
internal static class SignUpTrialProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    private static Uri? _pub;
    private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(30) };

    public static int Run(string[] args)
    {
        if (args.Length < 2 || !File.Exists(args[1]))
        {
            Console.WriteLine("⚠️ live.json نیست — اول signup-stack.mjs را روشن کنید. رد شد.");
            return 0;
        }
        var live = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
        var pub = new Uri(live.GetProperty("public").GetString()!);
        _pub = pub;
        var mailCodes = live.GetProperty("mailCodes").GetString()!;
        var shots = args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "pump-signuptrial");
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

        //  ⚠️ ‎PUMP_SIGNUP_DIR‎: همان نصب در اجرای بعدی — برای «سرورِ حساب به‌روز شد،
        //  پمپی که از قبل ساخته شده چه می‌گیرد؟» (‎PUMP_SIGNUP_REFRESH=1‎)
        var dir = Environment.GetEnvironmentVariable("PUMP_SIGNUP_DIR") is { Length: > 0 } keep
            ? keep : Path.Combine(Path.GetTempPath(), "pump-signuptrial-" + Guid.NewGuid().ToString("N"));
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
        var live2 = live;
        if (Environment.GetEnvironmentVariable("PUMP_SIGNUP_REFRESH") == "1")
        {
            //  همان نصب، سرورِ حسابِ تازه: کاربر فقط «گرفتنِ دوبارهٔ اطلاعات» را می‌زند
            Console.WriteLine("── پمپی که پیش از به‌روزرسانیِ سرورِ حساب ساخته شده بود");
            Wait(win, vm.GoAsync(account));
            Console.WriteLine($"     ⓘ پیش از تازه‌سازی: {account.SubPlanText} · {account.SubDaysText} · {account.PillText}");
            Wait(win, account.RefreshSubCommand.ExecuteAsync(null));
            Wait(win, vm.GoAsync(account));
            Console.WriteLine($"     ⓘ پس از تازه‌سازی: {account.SubPlanText} · {account.SubDaysText} · تا {account.SubEndsText} · {account.PillText}");
            Check("پس از به‌روز شدنِ سرورِ حساب، همان پمپ سی روزِ کامل را گرفت", account.SubActive && account.VipDays == 30,
                  account.VipDays.ToString());
            Shot(win, shots, "upgrade-profile");
            CloudLink.TestTransport = null;
            return _bad == 0 ? 0 : 1;
        }
        //  ⚠️ ‎PUMP_SIGNUP_SKIP=1‎: همان حالِ صاحب ریپو (۱۴۰۵/۰۷/۱۳، عکسِ پروفایل) —
        //  حساب ساخته شد، گامِ «نامِ پمپ» با «بعداً» رد شد. چراغ و پروفایل چه
        //  می‌گویند، و آیا از خودِ پروفایل می‌شود پمپ ساخت و سی روز را گرفت؟
        if (Environment.GetEnvironmentVariable("PUMP_SIGNUP_SKIP") == "1")
        {
            SkipThenProfile(win, vm, account, mailCodes, shots);
            Console.WriteLine("     ⓘ درخواست‌ها: " + string.Join(" · ", seen));
            CloudLink.TestTransport = null;
            Console.WriteLine(_bad == 0 ? "✅ حسابِ بی‌پمپ راست گفته شد و از پروفایل پمپ ساخته شد" : $"❌ {_bad} ایراد");
            return _bad == 0 ? 0 : 1;
        }
        SignUpOnce(win, vm, account, live2, mailCodes, shots, "");
        //  ⚠️ همان کامپیوتر، حسابِ دوم — همان کاری که صاحبِ پمپ در آزمایش‌هایش کرد
        if (Environment.GetEnvironmentVariable("PUMP_SIGNUP_TWICE") == "1")
        {
            Console.WriteLine("══ حسابِ دوم روی همین نصب («حساب و ورود» ⇒ «حساب می‌سازم»)");
            account.OpenAccountPageCommand.Execute(null);
            Settle(win);
            SignUpOnce(win, vm, account, live2, mailCodes, shots, "b-");
        }

        Console.WriteLine("     ⓘ درخواست‌ها: " + string.Join(" · ", seen));
        CloudLink.TestTransport = null;
        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ حسابِ تازه از خودِ برنامه ⇒ دورهٔ آزمایشیِ ۳۰ روزه" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void SignUpOnce(Avalonia.Controls.Window win, MainViewModel vm, AccountSectionViewModel account,
                                   JsonElement live, string mailCodes, string shots, string tag)
    {
            var email = "trial-" + Guid.NewGuid().ToString("N")[..10] + "@example.com";
            const string pass = "Pump!1405trial";

            Console.WriteLine("── ۱) «حساب می‌سازم» — از خودِ صفحهٔ ورود");
            Wait(win, vm.GoAsync(account));
            account.SetSignUpCommand.Execute("yes");
            account.LoginName = "صاحبِ پمپِ آزمایشی";
            account.LoginEmail = email;
            account.LoginPassword = pass;
            account.LoginPassword2 = pass;
            account.AcceptTerms = true;
            Settle(win);
            Shot(win, shots, tag + "signup-1-account");
            var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Wait(win, account.AccountStepCommand.ExecuteAsync(null));
            Check("کد به ایمیل رفت و صفحه به گامِ کد رسید", account.StepEmailCode,
                  "گام " + account.LoginStep + " · " + account.LoginStatus);

            var code = "";
            for (var i = 0; i < 120 && code.Length == 0; i++)
            {
                Pump(win);
                if (File.Exists(mailCodes))
                    foreach (var line in File.ReadAllLines(mailCodes).Reverse())
                    {
                        try
                        {
                            var j = JsonDocument.Parse(line).RootElement;
                            if (j.GetProperty("at").GetInt64() + 2000 < sentAt) continue;
                            if (!j.GetProperty("to").GetString()!.Contains(email, StringComparison.OrdinalIgnoreCase)) continue;
                            code = j.GetProperty("code").GetString() ?? "";
                            if (code.Length == 6) break;
                        }
                        catch { /* خطِ نیمه‌نوشته */ }
                    }
                if (code.Length == 0) Thread.Sleep(250);
            }
            Check("کدِ شش‌رقمی به صندوقِ همین ایمیل رسید", code.Length == 6, code);
            Shot(win, shots, tag + "signup-2-code");

            Console.WriteLine("── ۲) کدِ ایمیل ⇒ حساب — و همین‌جا تمام (پمپ و این کامپیوتر خودکار)");
            account.EmailCode = code;
            Wait(win, account.VerifyEmailCommand.ExecuteAsync(null));
            Settle(win);
            var f = AppSettings.Load();
            //  ⛔ «همین که یارو حسابِ کاربری برای خودش درست کرد تموم حساب درست شده» (۱۴۰۵/۰۷/۱۳)
            Check("⛔ پس از کدِ ایمیل هیچ گامِ دیگری نیست (نه نامِ پمپ، نه ثبتِ کامپیوتر)",
                  !account.ShowLoginPage && !account.StepPump, "گام " + account.LoginStep + " · " + account.LoginStatus);
            Check("پمپ خودکار روی سرور ساخته شد", !string.IsNullOrWhiteSpace(f.CloudStationId), f.CloudStationId);
            Check("این کامپیوتر خودکار به پمپ وصل شد (توکنِ دستگاه)", !string.IsNullOrWhiteSpace(f.CloudDeviceToken),
                  CloudLink.LastBindWhy);
            Check("مجوزِ امضاشده آمد", !string.IsNullOrWhiteSpace(f.CloudLicense));
            Check("کارتِ «در حالِ وصل شدن» دیده نمی‌شود", !account.LinkingNow, account.LinkingLine);
            Shot(win, shots, tag + "signup-3-done");

            Console.WriteLine("── ۴) پروفایل — همان چیزی که کاربر می‌بیند");
            Wait(win, vm.GoAsync(account));
            Settle(win);
            string Line() => $"{account.SubPlanText} · {account.SubDaysText} · تا {account.SubEndsText} · سربرگ: {account.PillText} · {account.SubStatus}";
            Console.WriteLine("     ⓘ " + Line());
            Check("دورهٔ آزمایشی فعال است", account.SubActive, Line());
            Check("«آزمایشی» خوانده می‌شود", account.SubKind == "آزمایشی", Line());
            Check("سی روز — همان عددی که پنلِ مدیر می‌گوید (نه ۲۹، نه ۱۴)", account.VipDays == 30, account.VipDays.ToString());
            Check("سربرگ «آزمایشی · N روز» می‌گوید", account.PillText.StartsWith("آزمایشی"), account.PillText);
            Check("قفل‌ها باز: فهرستِ قابلیت‌ها از خودِ مجوز", Entitlements.State().Features.Count >= 5,
                  string.Join(",", Entitlements.State().Features));
            Shot(win, shots, tag + "signup-4-profile");

            //  همان چیزی که پنلِ مدیر برای همین حساب می‌بیند
            if (live.TryGetProperty("panel", out var pEl) && live.TryGetProperty("panelToken", out var tEl))
            {
                var req = new HttpRequestMessage(HttpMethod.Get, pEl.GetString()!.TrimEnd('/')
                    + "/api/account-admin/customers?app=pump&q=" + Uri.EscapeDataString(email));
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + tEl.GetString());
                var res = Task.Run(() => Http.SendAsync(req)).GetAwaiter().GetResult();
                var text = Task.Run(() => res.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(shots, "panel-customers.json"), text);
                Console.WriteLine($"     ⓘ پنل ⇒ {(int)res.StatusCode} {text[..Math.Min(400, text.Length)]}");
            }
    }

    private static string CodeFor(Avalonia.Controls.Window win, string mailCodes, string email, long sentAt)
    {
        for (var i = 0; i < 120; i++)
        {
            Pump(win);
            if (File.Exists(mailCodes))
                foreach (var line in File.ReadAllLines(mailCodes).Reverse())
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

    /// <summary>
    /// ⛔ حسابی که **پیش از این** ساخته شده و پمپ ندارد (همان عکسِ صاحب ریپو)
    /// — ورود با آن ⇒ پمپ و این کامپیوتر خودکار، بی کارت و بی دکمه.
    /// </summary>
    private static void SkipThenProfile(Avalonia.Controls.Window win, MainViewModel vm, AccountSectionViewModel account,
                                        string mailCodes, string shots)
    {
        var email = "nopump-" + Guid.NewGuid().ToString("N")[..10] + "@example.com";
        const string pass = "Pump!1405trial";
        Console.WriteLine("── ۱) حسابی بی پمپ، بیرون از این برنامه ساخته شده");
        var baseUrl = _pub!.ToString().TrimEnd('/');
        string Post(string path, object body)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + path)
            { Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json") };
            req.Headers.TryAddWithoutValidation("X-App-Id", "tohid-pump-app");
            var res = Task.Run(() => Http.SendAsync(req)).GetAwaiter().GetResult();
            return Task.Run(() => res.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
        }
        var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Post("/api/auth/register/start", new { name = "محمد هارون", email, password = pass, passwordConfirm = pass, app = "pump" });
        var code = CodeFor(win, mailCodes, email, sentAt);
        var verify = JsonDocument.Parse(Post("/api/auth/register/verify", new { email, code, app = "pump" })).RootElement;
        var ticket = verify.TryGetProperty("ticket", out var t) ? t.GetString() : "";
        var terms = verify.TryGetProperty("terms", out var tv) && tv.TryGetProperty("version", out var tvv) ? tvv.GetString() : "";
        var done = Post("/api/auth/register/complete", new { ticket, name = "محمد هارون", password = pass,
            terms = new { accepted = true, version = terms }, app = "pump" });
        Check("حساب (بی پمپ) ساخته شد", done.Contains("accessToken"), done[..Math.Min(160, done.Length)]);

        Console.WriteLine("── ۲) ورود با همان حساب در برنامه ⇒ همه‌چیز خودکار");
        Wait(win, vm.GoAsync(account));
        if (!account.ShowLoginPage) { account.OpenAccountPageCommand.Execute(null); Settle(win); }
        account.SetSignUpCommand.Execute("no");
        account.LoginEmail = email;
        account.LoginPassword = pass;
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        Wait(win, vm.GoAsync(account));
        vm.TickLinkDot();
        Settle(win);
        var f = AppSettings.Load();
        Console.WriteLine($"     ⓘ {account.SubPlanText} · {account.SubDaysText} · {account.PillText} · کدِ پمپ: {account.PumpCodeLine}");
        Console.WriteLine($"     ⓘ چراغ: {vm.LinkDotBrushKey} · {vm.LinkDotReason.Replace('\n', ' ')}");
        Check("⛔ «نامِ پمپ» خواسته نشد", !account.ShowLoginPage && !account.StepPump, "گام " + account.LoginStep + " · " + account.LoginStatus);
        Check("پمپ خودکار ساخته شد، با نامِ صاحبِ حساب", !string.IsNullOrWhiteSpace(f.CloudStationId), f.CloudStationId);
        Check("این کامپیوتر وصل شد", !string.IsNullOrWhiteSpace(f.CloudDeviceToken), CloudLink.LastBindWhy);
        Check("سی روزِ آزمایشی فعال شد", account.SubActive && account.VipDays == 30, account.VipDays.ToString());
        Check("هیچ کارتِ «ساختن/ثبت» نیست", !account.LinkingNow, account.LinkingLine);
        Check("⛔ چراغِ سرورِ حساب سبز است", vm.CloudDotBrushKey == "Pump.Ok", vm.CloudDotBrushKey + " · " + vm.CloudDotReason);
        Shot(win, shots, "nopump-2-created");
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
