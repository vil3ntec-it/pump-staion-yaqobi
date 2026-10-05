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
/// ══ سه پلن، با خودِ برنامه روی پشتهٔ واقعی (۱۴۰۵/۰۷/۲۰) ════════════════════
///
/// «ببین همه درست شدن یا نه — حدس نزن، با دقت و با تست.» پس این‌جا نه مجوزِ
/// ساختگی، نه سرورِ ساختگی: پنلِ واقعی + سرورِ حسابِ واقعی (‎signup-stack.mjs‎)،
/// و <b>خودِ برنامه</b> که ثبت‌نام می‌کند، پمپ می‌سازد و مجوز را از سرور می‌گیرد.
/// مدیر از <b>همان درِ پنل</b> پلن را عوض می‌کند و برنامه، بی نصبِ چیزی، باید:
///
///   استاندارد — اشتراک را بگیرد؛ داشبورد و تاریخچه باز؛ مفاد/ضرر تار؛ کیو‌آر،
///                اپ و بات بسته؛ و <b>هیچ درخواستی</b> برای همگام‌سازی و عکسِ زنده
///                به سرور نرود.
///   وی‌آی‌پی  — همه باز، و همگام‌سازی واقعاً به سرور برود.
///   دائمی     — همه باز با «خدماتِ سرور تا …»؛ قطعِ خدمات از پنل ⇒ فقط
///                خدماتِ سرور می‌روند و مفاد/ضرر و بقیه می‌مانند؛ تمدید ⇒ برمی‌گردد.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- plantiers &lt;live.json&gt;
/// </summary>
internal static class PlanTiersLiveProbe
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
    private const string Pass = "Pump!1405tier";
    private static readonly List<string> Seen = new();

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
            lock (Seen) Seen.Add($"{req.Method} {req.RequestUri!.AbsolutePath} ⇒ {(int)res.StatusCode}");
            return res;
        };

        //  ⛔ هارنسِ این پروژه همگام‌سازی را برای همهٔ سنجه‌ها خاموش می‌کند (‎Program.cs‎).
        //  این‌جا باید روشن باشد، وگرنه «استاندارد هیچ درخواستی نفرستاد» سبزِ دروغ است.
        SyncEngine.Disabled = false;
        var dir = Path.Combine(Path.GetTempPath(), "pump-plantiers-" + Guid.NewGuid().ToString("N"));
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
        var profit = (ProfitSectionViewModel)vm.Sections.First(s => s.Id == "profit");

        // ── حساب و پمپ، با خودِ برنامه ────────────────────────────────────
        Console.WriteLine("══ حساب و پمپ از خودِ برنامه");
        var email = "tier-" + Guid.NewGuid().ToString("N")[..10] + "@example.com";
        SignUp(win, vm, account, email);
        Keep(win);
        Check("این کامپیوتر به پمپِ حساب ثبت شد", AppSettings.Load().CloudDeviceToken.Length > 0, CloudLink.LastBindWhy);
        var targets = PanelJson(HttpMethod.Get, "/api/account-admin/grant-targets?app=pump&q=" + Uri.EscapeDataString(email), null);
        var tenant = targets.TryGetProperty("items", out var items) && items.GetArrayLength() > 0
            ? items[0].GetProperty("tenantId").GetString() ?? "" : "";
        Check("پنل پمپِ همین حساب را با ایمیل پیدا کرد", tenant.Length > 0, targets.ToString()[..Math.Min(200, targets.ToString().Length)]);

        // ── استاندارد ────────────────────────────────────────────────────
        Console.WriteLine("══ استاندارد");
        Console.WriteLine("     ⓘ پنل ⇒ " + PanelSend(HttpMethod.Post, "/api/account-admin/subs/pump/grant", new { tenantId = tenant, plan = "std" }));
        Refresh(win);
        var lic = LicenseGuard.CheckStored(AppSettings.Load());
        Check("⛔ استاندارد: اشتراک از سرور رسید (مجوزِ امضاشدهٔ «std»)", lic.SignatureOk && lic.Plan == "std", $"{lic.SignatureOk} · {lic.Plan}");
        Check("استاندارد: داشبورد و تاریخچه باز", Entitlements.Allows(Entitlements.Dashboard) && Entitlements.Allows(Entitlements.History));
        Check("⛔ استاندارد: کیو‌آر، اپِ گوشی و بات بسته",
              !Entitlements.Allows(Entitlements.QrLive) && !Entitlements.Allows(Entitlements.Kar) && !Entitlements.Allows(Entitlements.Online));
        Check("⛔ استاندارد: مفاد/ضرر تار است (پرده با دلیلِ پلن)", profit.Veiled && profit.PlanVeiled, profit.VeilText);
        Console.WriteLine("     ⓘ پروفایل: " + account.SubPlanText + " · " + account.SubServicesText);
        var (sync, state, all) = Traffic(win);
        Check("⛔ استاندارد: همگام‌سازی هیچ درخواستی به سرور نفرستاد", sync == 0, sync.ToString());
        Check("⛔ استاندارد: عکسِ زنده/حالِ مخزن هیچ درخواستی به سرور نفرستاد", state == 0, string.Join(" · ", all));
        TickDots(win, vm);
        Check("استاندارد: چراغِ سرورِ خانگی خاکستری با دلیلِ پلن، نه «خراب»",
              vm.ServerDotBrushKey == "Pump.Muted" && vm.ServerDotReason.Contains("در پلنِ شما نیست"), vm.ServerDotReason);
        Check("و موتورِ همگام‌سازی همین را می‌گوید", AppHost.Current.Sync.Reason.Contains("در پلنِ شما نیست"), AppHost.Current.Sync.Reason);

        // ── وی‌آی‌پی ─────────────────────────────────────────────────────
        Console.WriteLine("══ وی‌آی‌پی");
        Console.WriteLine("     ⓘ پنل ⇒ " + PanelSend(HttpMethod.Post, "/api/account-admin/subs/pump/grant", new { tenantId = tenant, plan = "vip" }));
        Refresh(win);
        lic = LicenseGuard.CheckStored(AppSettings.Load());
        Check("وی‌آی‌پی: مجوزِ تازه بی نصبِ چیزی رسید", lic.SignatureOk && lic.Plan == "vip", lic.Plan);
        Check("وی‌آی‌پی: کیو‌آر، اپ، بات/خدماتِ سرور، مفاد، داشبورد و تاریخچه همه باز",
              new[] { Entitlements.QrLive, Entitlements.Kar, Entitlements.Online, Entitlements.Profit, Entitlements.Dashboard, Entitlements.History }
              .All(Entitlements.Allows));
        Check("وی‌آی‌پی: مفاد/ضرر بی پردهٔ پلن", !profit.PlanVeiled, profit.VeilText);
        (sync, _, all) = Traffic(win);
        Check("وی‌آی‌پی: همگام‌سازی واقعاً به سرور رفت", sync > 0, string.Join(" · ", all));
        Check("وی‌آی‌پی: موتورِ همگام‌سازی دیگر «در پلنِ شما نیست» نمی‌گوید", !AppHost.Current.Sync.Reason.Contains("پلن"), AppHost.Current.Sync.Reason);
        Check("⛔ و هیچ‌کدامش ۴۰۳ِ «پلن» نگرفت", !all.Any(x => x.Contains("/api/sync/") && x.EndsWith("403")), string.Join(" · ", all));

        // ── دائمی ────────────────────────────────────────────────────────
        Console.WriteLine("══ دائمی");
        var g = PanelJson(HttpMethod.Post, "/api/account-admin/subs/pump/grant", new { tenantId = tenant, plan = "perm" });
        var permId = g.TryGetProperty("subscription", out var sub) ? sub.GetProperty("id").GetString() ?? "" : "";
        Check("پنل اشتراکِ دائمی داد", permId.Length > 0, g.ToString()[..Math.Min(200, g.ToString().Length)]);
        Refresh(win);
        lic = LicenseGuard.CheckStored(AppSettings.Load());
        var yearOut = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeMilliseconds();
        Check("دائمی: همه باز، و «خدماتِ سرور» حدودِ یک سال", lic.Plan == "perm"
              && Entitlements.Allows(Entitlements.Online) && Entitlements.Allows(Entitlements.Profit)
              && Math.Abs(lic.ServicesEndsAt - yearOut) < 3L * 86_400_000, $"{lic.Plan} · svc={lic.ServicesEndsAt}");
        Check("پروفایل «خدماتِ سرور تا …» را نشان می‌دهد", account.SubServicesText.Contains("تا"), account.SubServicesText);

        Console.WriteLine("     ⓘ پنل (قطعِ خدمات) ⇒ " + PanelSend(HttpMethod.Post,
            $"/api/account-admin/subs/pump/{permId}/services", new { until = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds() }));
        Refresh(win);
        Check("⛔ دائمی، خدمات تمام: خدماتِ سرور بسته", !Entitlements.Allows(Entitlements.Online) && !Entitlements.Allows(Entitlements.Kar));
        Check("⛔ ولی مفاد/ضرر، داشبورد و تاریخچه بی‌محدودیت می‌مانند",
              Entitlements.Allows(Entitlements.Profit) && Entitlements.Allows(Entitlements.Dashboard)
              && Entitlements.Allows(Entitlements.History) && !profit.PlanVeiled, profit.VeilText);
        Check("پروفایل می‌گوید خدمات تمام شده و تمدید لازم است", account.SubServicesText.Contains("تمام"), account.SubServicesText);
        (sync, state, all) = Traffic(win);
        Check("⛔ دائمیِ بی‌خدمات: همگام‌سازی و عکسِ زنده هیچ درخواستی نفرستادند", sync == 0 && state == 0, string.Join(" · ", all));

        Console.WriteLine("     ⓘ پنل (تمدیدِ یک ماه) ⇒ " + PanelSend(HttpMethod.Post,
            $"/api/account-admin/subs/pump/{permId}/services", new { amount = 1, unit = "month" }));
        Refresh(win);
        Check("دائمی پس از تمدید: خدماتِ سرور همان دقیقه برگشت", Entitlements.Allows(Entitlements.Online) && Entitlements.Allows(Entitlements.Kar));
        (sync, _, all) = Traffic(win);
        Check("و همگام‌سازی دوباره به سرور رفت", sync > 0, string.Join(" · ", all));

        CloudLink.TestTransport = null;
        Console.WriteLine(_bad == 0 ? "✅ هر سه پلن روی پشتهٔ واقعی همان‌طور رفتار کرد که خواسته شده" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>همان تیکِ «مجوز رسیده؟» که حلقهٔ پس‌زمینه هر ده دقیقه می‌زند — همین حالا.</summary>
    private static void Refresh(Avalonia.Controls.Window win)
    {
        var due = AppSettings.Load(); due.CloudSyncedAt = 0; due.Save();
        Keep(win);
        AccountRefresh(win);
    }

    private static void AccountRefresh(Avalonia.Controls.Window win)
    {
        var vm = (MainViewModel)win.DataContext!;
        ((AccountSectionViewModel)vm.Sections.First(s => s.Id == "account")).RefreshAll();
        Settle(win);
    }

    /// <summary>چند دورِ همگام‌سازی و یک انتشارِ عکسِ زنده؛ شمارِ درخواست‌هایشان.</summary>
    private static (int sync, int state, List<string> all) Traffic(Avalonia.Controls.Window win)
    {
        int before; lock (Seen) before = Seen.Count;
        for (var i = 0; i < 3; i++) Wait(win, AppHost.Current.Sync.SyncNowAsync());
        try { Wait(win, AppHost.Current.Publisher.PublishOnceAsync(force: true)); } catch { }
        List<string> now; lock (Seen) now = Seen.Skip(before).ToList();
        return (now.Count(x => x.Contains("/api/sync/")),
                now.Count(x => x.Contains("/device/state") || x.Contains("/device/files") || x.Contains("/device/backups")),
                now);
    }

    private static void TickDots(Avalonia.Controls.Window win, MainViewModel vm)
    {
        vm.GetType().GetField("_boundAt", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, DateTime.MinValue);
        vm.TickServerDot();
        Settle(win);
    }

    private static void Keep(Avalonia.Controls.Window win)
    {
        var m = typeof(StationPublisher).GetMethod("CloudKeepAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var t = (Task)m.Invoke(null, new object[] { CancellationToken.None, false })!;
        try { Wait(win, t); } catch { }
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
        var j = PanelJson(method, path, body);
        var s = j.ValueKind == JsonValueKind.Undefined ? "(بی پاسخِ JSON)" : j.ToString();
        return s[..Math.Min(160, s.Length)];
    }

    private static void SignUp(Avalonia.Controls.Window win, MainViewModel vm, AccountSectionViewModel account, string email)
    {
        Wait(win, vm.GoAsync(account));
        account.SetSignUpCommand.Execute("yes");
        account.LoginName = "صاحبِ پمپ";
        account.LoginEmail = email;
        account.LoginPassword = Pass;
        account.LoginPassword2 = Pass;
        account.AcceptTerms = true;
        var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Wait(win, account.AccountStepCommand.ExecuteAsync(null));
        account.EmailCode = CodeFor(win, email, sentAt);
        Wait(win, account.VerifyEmailCommand.ExecuteAsync(null));
        Settle(win);
        Check("حساب ساخته شد", !account.ShowLoginPage, "گام " + account.LoginStep + " · " + account.LoginStatus);
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
