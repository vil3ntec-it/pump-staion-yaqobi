using System.Net.Http;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Avalonia.Headless;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ کدِ اشتراکِ آفلاین — روی پشتهٔ واقعی (۱۴۰۵/۰۷/۱۵) ══════════════════════
///
/// «یک کد برای اشتراک می‌سازی… یک گیرنده برای برنامه تا کد رو بزنم درجا
/// قفل‌ها باز بشه و بدون نت هم اشتراک داده بشه، و اگه یارو اینترنت پیدا
/// کرد… سرور همون کد رو ببینه و بگه آره این حساب اشتراک داره.»
///
///   الف) کامپیوترِ هرگز‌آنلاین‌نشده، **هیچ اینترنتی**: قفل‌ها بسته
///   ب)   پنلِ واقعی با کدِ کامپیوترِ همین‌جا کدِ وی‌آی‌پی می‌سازد ⇒ زدن در
///        خودِ صفحهٔ «اشتراک و پلن‌ها» ⇒ قفل‌ها همان لحظه باز
///   ج)   فایلِ ‎.pumpkey‎ِ پنل (دائمی) از «انتخابِ فایل»
///   د)   کدِ کامپیوترِ دیگر ⇒ رد، و کدِ قبلی دست نمی‌خورد
///   ه)   اینترنت آمد، حساب با پمپ ⇒ حلقهٔ خودِ برنامه کد را به سرور می‌برد ⇒
///        پنل «سرور دید» و اشتراکِ پمپِ حساب «دائمی»
///   و)   مدیر کد را باطل کرد ⇒ برنامه کد را کنار می‌گذارد
///
/// ⚠️ کلیدِ داخلِ برنامه همان کاری است که ساختِ CI می‌کند: کلیدِ عمومی از
/// <c>/api/license/public-key</c>ِ همین پشته ⇒ <c>CloudConfig.TestOfflineKeys</c>.
///
///     oldacct &lt;live.json&gt; &lt;پوشه&gt; offline
/// </summary>
internal static partial class OldAccountProbe
{
    private static int OfflineCodes(string shots)
    {
        var net = CloudLink.TestTransport!;
        var online = false;
        CloudLink.TestTransport = (req, ct) => online ? net(req, ct) : throw new HttpRequestException("offline");

        //  کلیدِ داخلِ برنامه — همان گامِ «کلیدِ کدِ آفلاین»ِ build-native.yml
        var pk = Task.Run(() => new HttpClient().GetStringAsync(_pub + "/api/license/public-key")).GetAwaiter().GetResult();
        var spki = JsonDocument.Parse(pk).RootElement.GetProperty("publicKey").GetString()!;
        CloudConfig.TestOfflineKeys = new Dictionary<string, string> { ["#1"] = spki };
        Entitlements.Unlocked = false;

        var f0 = AppSettings.Load();
        f0.CloudPublicKey = ""; f0.CloudDeviceToken = ""; f0.CloudLicense = ""; f0.OfflineCode = "";
        f0.Save();

        var computer = OfflineKey.ComputerCode();
        Check("این کامپیوتر کدِ کامپیوتر دارد", computer.Length == 19, computer);
        Console.WriteLine($"     ⓘ کدِ کامپیوتر: {computer}");

        // ── الف ───────────────────────────────────────────────────────────
        Console.WriteLine("══ الف) کامپیوترِ بی اینترنت و بی حساب");
        var (win, vm, account) = Open();
        Wait(win, vm.GoAsync(account));
        var vip = (VipSectionViewModel)account.SubSections.First(s => s.Id == "vip");
        account.ShowSubCommand.Execute(vip);
        Settle(win);
        Check("کدِ کامپیوتر در صفحهٔ «اشتراک و پلن‌ها» دیده می‌شود", vip.ComputerCode == computer, vip.ComputerCode);
        Check("پیش از کد: اپِ کارمندان و داشبورد بسته",
            !Entitlements.Allows(Entitlements.Kar) && !Entitlements.Allows(Entitlements.Dashboard));
        Shot(win, shots, "off-a-before");

        // ── ب ─────────────────────────────────────────────────────────────
        Console.WriteLine("══ ب) پنل کدِ وی‌آی‌پی ساخت ⇒ در برنامه زده شد (هنوز بی اینترنت)");
        var made = Panel(HttpMethod.Post, "/api/account-admin/offline-codes",
            new { plan = "vip", computer, days = 365, note = "کامپیوترِ بی‌نت" });
        var vipCode = made.ValueKind == JsonValueKind.Object ? made.GetProperty("code").GetString() ?? "" : "";
        Check("پنل کدِ آفلاین ساخت", vipCode.Length > 150, vipCode);
        vip.OfflineInput = vipCode;
        vip.ApplyOfflineCommand.Execute(null);
        Settle(win);
        var st = Entitlements.State();
        Check("⛔ بی اینترنت، قفل‌ها همان لحظه باز شدند",
            Entitlements.Allows(Entitlements.Kar) && Entitlements.Allows(Entitlements.Dashboard)
            && Entitlements.Allows(Entitlements.Profit), $"{st.PlanTitle} · {vip.OfflineStatus}");
        Check("صفحه می‌گوید چه شد", vip.OfflineStatus.StartsWith("✅") && vip.OfflineCurrent.Contains("وی‌آی‌پی"),
            vip.OfflineStatus + " | " + vip.OfflineCurrent);
        Shot(win, shots, "off-b-applied");
        account.RefreshAll();
        Check("سربرگ هم می‌گوید چه دارد (نه «وارد نشده»)", account.PillText.StartsWith("VIP"),
            account.PillText + " · " + account.PillSub);
        //  کلِ صفحه، تا کارتِ کدِ آفلاین هم در عکس باشد
        win.Height = 2300; Settle(win);
        Shot(win, shots, "off-b-card");
        win.Height = 900; Settle(win);

        // ── ج ─────────────────────────────────────────────────────────────
        Console.WriteLine("══ ج) فایلِ ‎.pumpkey‎ِ دائمی از «انتخابِ فایل»");
        var perm = Panel(HttpMethod.Post, "/api/account-admin/offline-codes", new { plan = "perm", computer });
        var file = Path.Combine(shots, "perm.pumpkey");
        File.WriteAllText(file, perm.GetProperty("file").GetRawText());
        var permSerial = perm.GetProperty("offline").GetProperty("serial").GetString()!;
        var permId = perm.GetProperty("offline").GetProperty("id").GetString()!;
        Dialogs.PickFileHook = _ => file;
        try { vip.PickOfflineFileCommand.Execute(null); for (var i = 0; i < 50; i++) Settle(win); }
        finally { Dialogs.PickFileHook = null; }
        Check("⛔ فایلِ ‎.pumpkey‎ پذیرفته شد — دائمی", OfflineKey.Stored(AppSettings.Load(), Entitlements.Now()).Permanent,
            vip.OfflineStatus);

        // ── د ─────────────────────────────────────────────────────────────
        Console.WriteLine("══ د) کدِ کامپیوترِ دیگر");
        var otherPc = OfflineKey.ComputerCode("m-" + new string('7', 32));
        var other = Panel(HttpMethod.Post, "/api/account-admin/offline-codes", new { plan = "vip", computer = otherPc, days = 30 });
        vip.OfflineInput = other.GetProperty("code").GetString()!;
        vip.ApplyOfflineCommand.Execute(null);
        Settle(win);
        Check("⛔ کدِ کامپیوترِ دیگر رد شد و کدِ دائمیِ این‌جا سرِ جایش ماند",
            vip.OfflineStatus.Contains("کامپیوترِ دیگری") && OfflineKey.Stored(AppSettings.Load(), Entitlements.Now()).Permanent,
            vip.OfflineStatus);
        Shot(win, shots, "off-d-other");
        win.Close();

        // ── ه ─────────────────────────────────────────────────────────────
        Console.WriteLine("══ ه) اینترنت آمد و حساب (با پمپ) — سرور همان کد را می‌بیند");
        online = true;
        var acct = MakeAccount("offline-" + Guid.NewGuid().ToString("N")[..8] + "@example.com", withStation: true);
        SeatOnDisk(acct, loginSkipped: false);
        (win, vm, account) = Open();
        _until = () => AppSettings.Load().OfflineCodeRedeemed.StartsWith(permSerial, StringComparison.Ordinal);
        Loop(win, 3);
        var fe = AppSettings.Load();
        Check("⛔ برنامه کد را به سرورِ حساب برد", fe.OfflineCodeRedeemed.StartsWith(permSerial + "@")
            && !fe.OfflineCodeRedeemed.Contains('!'), fe.OfflineCodeRedeemed + " · " + CloudLink.LastBindWhy);
        var list = Panel(HttpMethod.Get, "/api/account-admin/offline-codes", null);
        var row = list.GetProperty("codes").EnumerateArray().FirstOrDefault(c => c.GetProperty("serial").GetString() == permSerial);
        Check("⛔ پنل: «سرور دید» روی همان کد", row.ValueKind == JsonValueKind.Object
            && row.GetProperty("redeemedAt").ValueKind == JsonValueKind.Number, row.ToString());
        Wait(win, vm.GoAsync(account));
        account.RefreshAll();
        Report(win, vm, account, shots, "off-e-online");
        Check("⛔ اشتراکِ پمپِ حساب روی سرور «دائمی» شد و به برنامه رسید",
            account.SubKind == "دائمی" || account.PillText.Contains("دائمی"),
            $"{account.SubKind} · {account.PillText} · {account.SubPlanText}");

        // ── و ─────────────────────────────────────────────────────────────
        Console.WriteLine("══ و) مدیر کد را باطل کرد");
        Panel(HttpMethod.Post, $"/api/account-admin/offline-codes/{permId}/revoke", new { });
        var fr = AppSettings.Load();
        fr.OfflineCodeRedeemed = "";      // دوباره بپرسد، مثلِ کامپیوترِ دیگری که کد را دارد
        fr.Save();
        _until = () => AppSettings.Load().OfflineCode.Length == 0;
        Loop(win, 3);
        Check("⛔ کدِ باطل‌شده از این کامپیوتر هم برداشته شد", AppSettings.Load().OfflineCode.Length == 0,
            AppSettings.Load().OfflineCodeRedeemed);
        win.Close();

        CloudConfig.TestOfflineKeys = null;
        CloudLink.TestTransport = null;
        Console.WriteLine(_bad == 0 ? "✅ کدِ اشتراکِ آفلاین از پنل تا برنامه و برگشت به سرور کار کرد" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Shot(Avalonia.Controls.Window win, string shots, string name)
    {
        Settle(win);
        using var shot = win.CaptureRenderedFrame();
        var path = Path.Combine(shots, name + ".png");
        shot?.Save(path);
        Console.WriteLine("  📷 " + path);
    }
}
