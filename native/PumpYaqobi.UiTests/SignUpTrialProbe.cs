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

            Console.WriteLine("── ۲) کدِ ایمیل ⇒ حساب");
            account.EmailCode = code;
            Wait(win, account.VerifyEmailCommand.ExecuteAsync(null));
            Check("کد پذیرفته شد، حساب ساخته شد و صفحه به گامِ پمپ رفت", account.StepPump,
                  "گام " + account.LoginStep + " · " + account.LoginStatus);
            Shot(win, shots, tag + "signup-3-pump");

            Console.WriteLine("── ۳) نامِ پمپ ⇒ پمپ روی سرور و بند شدنِ این کامپیوتر");
            account.LoginPump = "پمپ آزمایشیِ سی‌روزه";
            Wait(win, account.FinishPumpCommand.ExecuteAsync(null));
            Settle(win);
            var f = AppSettings.Load();
            Check("گامِ ورود تمام شد", !account.ShowLoginPage, "گام " + account.LoginStep + " · " + account.LoginStatus);
            Check("پمپ روی سرور ساخته شد", !string.IsNullOrWhiteSpace(f.CloudStationId), f.CloudStationId);
            Check("این کامپیوتر به پمپ بند شد (توکنِ دستگاه)", !string.IsNullOrWhiteSpace(f.CloudDeviceToken),
                  CloudLink.LastBindWhy);
            Check("مجوزِ امضاشده آمد", !string.IsNullOrWhiteSpace(f.CloudLicense));

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

    private static void SkipThenProfile(Avalonia.Controls.Window win, MainViewModel vm, AccountSectionViewModel account,
                                        string mailCodes, string shots)
    {
        var email = "nopump-" + Guid.NewGuid().ToString("N")[..10] + "@example.com";
        const string pass = "Pump!1405trial";
        Console.WriteLine("── ۱) حساب ساخته می‌شود و گامِ پمپ با «بعداً» رد می‌شود");
        Wait(win, vm.GoAsync(account));
        account.SetSignUpCommand.Execute("yes");
        account.LoginName = "محمد هارون";
        account.LoginEmail = email;
        account.LoginPassword = pass;
        account.LoginPassword2 = pass;
        account.AcceptTerms = true;
        var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        account.EmailCode = CodeFor(win, mailCodes, email, sentAt);
        Wait(win, account.VerifyEmailCommand.ExecuteAsync(null));
        Check("حساب ساخته شد و صفحه به گامِ پمپ رسید", account.StepPump, "گام " + account.LoginStep + " · " + account.LoginStatus);
        account.SkipPumpCommand.Execute(null);
        Settle(win);

        Console.WriteLine("── ۲) پروفایل و چراغ — همان عکسِ صاحب ریپو");
        Wait(win, vm.GoAsync(account));
        //  حلقهٔ شصت‌ثانیه‌ای را جلو بیندازیم: همان «سرورِ حساب تایید کرد»
        Wait(win, account.RefreshSubCommand.ExecuteAsync(null));
        Wait(win, vm.GoAsync(account));
        vm.TickLinkDot();
        Settle(win);
        var f = AppSettings.Load();
        Console.WriteLine($"     ⓘ کدِ پمپ: {account.PumpCodeLine} · سرورِ حساب: {account.CloudLine} · پلن: {account.SubPlanText}");
        Console.WriteLine($"     ⓘ چراغ: {vm.LinkDotBrushKey} · {vm.LinkDotReason.Replace('\n', ' ')}");
        Check("پمپی روی سرور نیست (همان حالِ عکس)", string.IsNullOrWhiteSpace(f.CloudStationId));
        Check("⛔ چراغ «هر دو وصل‌اند» نمی‌گوید وقتی این کامپیوتر به هیچ پمپی ثبت نشده",
              vm.LinkDotBrushKey != "Pump.Ok" && vm.LinkDotReason.Contains("پمپ"), vm.LinkDotBrushKey + " · " + vm.LinkDotReason);
        //  ⚠️ با بازتاب، تا همین سنجه روی نسخهٔ پیشین هم کامپایل شود و «پیش از» را نشان دهد
        bool NeedsPump() => account.GetType().GetProperty("NeedsPump")?.GetValue(account) is true;
        Check("⛔ پروفایل راهِ ساختنِ پمپ را نشان می‌دهد", NeedsPump(), NeedsPump().ToString());
        Shot(win, shots, "nopump-1-profile");

        Console.WriteLine("── ۳) از خودِ پروفایل: نامِ پمپ ⇒ ساختن ⇒ سی روز");
        account.LoginPump = "پمپ دولتی";
        if (account.GetType().GetProperty("CreatePumpHereCommand")?.GetValue(account)
            is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand create)
            Wait(win, create.ExecuteAsync(null));
        else { Check("دکمهٔ «ساختنِ پمپ» در پروفایل هست", false, "نیست"); return; }
        Wait(win, vm.GoAsync(account));
        vm.TickLinkDot();
        Settle(win);
        f = AppSettings.Load();
        Console.WriteLine($"     ⓘ {account.SubPlanText} · {account.SubDaysText} · {account.PillText} · کدِ پمپ: {account.PumpCodeLine}");
        Console.WriteLine($"     ⓘ چراغ: {vm.LinkDotBrushKey} · {vm.LinkDotReason.Replace('\n', ' ')}");
        Check("پمپ روی سرور ساخته شد", !string.IsNullOrWhiteSpace(f.CloudStationId), f.CloudStationId);
        Check("این کامپیوتر بند شد", !string.IsNullOrWhiteSpace(f.CloudDeviceToken), CloudLink.LastBindWhy);
        Check("سی روزِ آزمایشی فعال شد", account.SubActive && account.VipDays == 30, account.VipDays.ToString());
        Check("کارتِ «پمپ بسازید» رفت", !NeedsPump());
        Check("⛔ چراغِ سرورِ حساب حالا سبز است (سرورِ خانگی در این آزمون نیست، پس کلِ چراغ زرد)",
              vm.CloudDotBrushKey == "Pump.Ok", vm.CloudDotBrushKey + " · " + vm.CloudDotReason);
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
