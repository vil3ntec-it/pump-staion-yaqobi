using System.Net;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — حلقهٔ «گامِ پمپ» با <b>رفتار</b>، نه با متنِ CloudLink.cs ══════
///
/// همان سه شکایتِ ۱۴۰۵/۰۷/۱۱ («دوباره نامِ پمپ را می‌خواهد»، «فعال نشده»،
/// «۳۰ روزِ رایگان نیامد») که تا امروز فقط با گشتنِ رشته در سورس قفل بودند.
/// این‌جا با سرورِ ساختگیِ درون‌فرآیندی (<c>CloudLink.TestTransport</c>) و
/// خواندنِ تنظیماتِ <b>روی دیسک</b> سنجیده می‌شوند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class PumpBindBehaviourTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-bindb-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _was = AppSettings.DirOverride;
    private readonly List<string> _hits = new();

    public PumpBindBehaviourTests()
    {
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        CloudLink.ResetReach();
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private void Serve(Func<string, HttpRequestMessage, HttpResponseMessage> handler) =>
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            lock (_hits) _hits.Add(path);
            return Task.FromResult(handler(path, req));
        };

    private static (CloudLink Link, AppSettings Settings) Link(Action<AppSettings> seed)
    {
        var f = AppSettings.Load();
        seed(f);
        f.Save();
        return (new CloudLink(f, () => { f.Save(); return Task.CompletedTask; }), f);
    }

    private static long InAnHour => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;

    private static string LoginOk(string userId) =>
        "{\"accessToken\":\"acc-" + userId + "\",\"refreshToken\":\"ref-" + userId
        + "\",\"accessExpiresAt\":" + InAnHour + ",\"user\":{\"id\":\"" + userId
        + "\",\"email\":\"" + userId + "@x.com\",\"name\":\"" + userId + "\"}}";

    /// <summary>
    /// ⛔ <b>کلیدِ عمومی هم یک «بند» است.</b> نصبی که کلیدِ پمپِ حسابِ قبلی را
    /// قفل کرده ولی هنوز توکن نگرفته بود، با ورودِ حسابِ دیگر کلید را <b>رها</b>
    /// می‌کند — و بند شدنِ بعدی دیگر <c>key_mismatch</c> نمی‌گیرد.
    /// </summary>
    [Fact]
    public async Task HesabeDigar_KelideJamandeh_RaRahaMikonad_VaBandMishavad()
    {
        Serve((path, _) => path switch
        {
            "/api/auth/login" => Json(HttpStatusCode.OK, LoginOk("u-B")),
            "/api/pump/device/bind" => Json(HttpStatusCode.Created,
                """{"deviceToken":"pd_B","station":{"id":"stn-B"},"license":"lic.B","publicKey":"pk-B"}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });
        var (link, _) = Link(s =>
        {
            s.CloudUserId = "u-A";
            s.CloudPublicKey = "pk-A";       // جامانده از پمپِ حسابِ قبلی، بی توکن
            s.PumpStepDone = true;
        });

        var login = await link.SignInWithPasswordAsync("u-B@x.com", "ramz-1234");
        Assert.True(login.Ok, login.Why);
        Assert.True(link.AccountSwitched);

        var disk = AppSettings.Load();
        Assert.Equal("", disk.CloudPublicKey);          // رها شد
        Assert.False(disk.PumpStepDone);                // حسابِ تازه ⇒ گامِ پمپ از نو

        var bind = await link.BindAsync();
        Assert.True(bind.Ok, bind.Why);
        Assert.Equal("pk-B", AppSettings.Load().CloudPublicKey);
    }

    /// <summary>
    /// ⛔ <b>جابه‌جاییِ حساب فقط بندها را باز می‌کند، نه دفتر را</b> — هیچ
    /// قرض‌دار و هیچ ردیفی کم نمی‌شود (پیش از این فقط با گشتنِ نامِ
    /// ‎PumpDbContext/Debtor‎ در بدنهٔ ‎ForgetStationAsync‎ قفل بود).
    /// </summary>
    [Fact]
    public async Task HesabeDigar_Daftar_Ra_DastNemizanad()
    {
        var host = AppHost.Start(Path.Combine(Path.GetTempPath(), "pump-shared-" + Guid.NewGuid().ToString("N"), "pump.db"));   // ⚠️ بیرونِ ‎_dir‎: میزبانِ مشترک پس از این آزمون هم زنده است
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        if (host.Auth.HasPassword()) host.Auth.SignIn("admin", "1234"); else host.Auth.OpenWithoutPassword();
        var mine = await host.Debtors.AddDebtorAsync("ت۳-" + Guid.NewGuid().ToString("N")[..6], "", false);
        var acct = (await host.Debtors.LoadFullAsync(mine.Id))!.AllAccounts().First();
        await host.Debtors.SaveRowAsync(new PumpYaqobi.Domain.Entities.DebtRow
        {
            DateShamsi = "1405/07/20", Liters = 300m, FuelAccountId = acct.Id,
        });
        var debtorsBefore = await host.Debtors.CountAsync();
        var rowsBefore = await host.Debtors.RowCountAsync();

        Serve((path, _) => path == "/api/auth/login"
            ? Json(HttpStatusCode.OK, LoginOk("u-B"))
            : Json(HttpStatusCode.NotFound, "{}"));
        var (link, _) = Link(s =>
        {
            s.CloudUserId = "u-A";
            s.CloudPublicKey = "pk-A";
            s.CloudDeviceToken = "pd_A";
            s.CloudStationId = "stn-A";
            s.PumpStepDone = true;
        });

        var login = await link.SignInWithPasswordAsync("u-B@x.com", "ramz-1234");
        Assert.True(login.Ok, login.Why);
        Assert.True(link.AccountSwitched);
        Assert.Equal("", AppSettings.Load().CloudDeviceToken);      // بند واقعاً باز شد

        Assert.Equal(debtorsBefore, await host.Debtors.CountAsync());
        Assert.Equal(rowsBefore, await host.Debtors.RowCountAsync());
        Assert.NotNull(await host.Debtors.LoadFullAsync(mine.Id));
    }

    /// <summary>
    /// و ورودِ دوبارهٔ <b>همان</b> حساب هیچ بندی را باز نمی‌کند.
    /// </summary>
    [Fact]
    public async Task HamanHesab_HichBandi_RaBazNemikonad()
    {
        Serve((path, _) => path == "/api/auth/login"
            ? Json(HttpStatusCode.OK, LoginOk("u-A"))
            : Json(HttpStatusCode.NotFound, "{}"));
        var (link, _) = Link(s =>
        {
            s.CloudUserId = "u-A";
            s.CloudPublicKey = "pk-A";
            s.CloudDeviceToken = "pd_A";
            s.CloudStationId = "stn-A";
            s.PumpStepDone = true;
        });

        var login = await link.SignInWithPasswordAsync("u-A@x.com", "ramz-1234");
        Assert.True(login.Ok, login.Why);
        Assert.False(link.AccountSwitched);

        var disk = AppSettings.Load();
        Assert.Equal("pk-A", disk.CloudPublicKey);
        Assert.Equal("pd_A", disk.CloudDeviceToken);
        Assert.Equal("stn-A", disk.CloudStationId);
        Assert.True(disk.PumpStepDone);
    }

    /// <summary>
    /// ⛔ <b>استثنای «کلیدِ بی‌مصرف» فقط در <c>BindAsync</c> است.</b> فعال‌سازی
    /// با کد — حتی روی دستگاهی که توکن ندارد — کلیدِ ناجور را رد می‌کند و هیچ
    /// چیزی نمی‌نشاند.
    /// </summary>
    [Fact]
    public async Task FaalSazi_KelideNajur_RaHamishehRadMikonad()
    {
        Serve((path, _) => path == "/api/pump/device/activate"
            ? Json(HttpStatusCode.Created,
                """{"deviceToken":"pd_x","station":{"id":"stn-x"},"license":"lic.x","publicKey":"pk-OTHER"}""")
            : Json(HttpStatusCode.NotFound, "{}"));
        var (link, _) = Link(s => s.CloudPublicKey = "pk-1");

        var r = await link.ActivateAsync("123456");

        Assert.False(r.Ok);
        Assert.Equal("key_mismatch", r.Code);
        var disk = AppSettings.Load();
        Assert.Equal("pk-1", disk.CloudPublicKey);
        Assert.Equal("", disk.CloudDeviceToken);
        Assert.Equal("", disk.CloudLicense);
    }

    /// <summary>
    /// ⛔ <b>شکستِ بند شدن بلعیده نمی‌شود</b>: حلقهٔ پس‌زمینه نتیجه را نگه
    /// می‌دارد تا پروفایل دلیلِ واقعی را بگوید — و پیروزیِ بعدی پاکش می‌کند.
    /// </summary>
    [Fact]
    public async Task ShekasteBand_DalileshMimanad_VaPiruzi_PakashMikonad()
    {
        var bindOk = false;
        Serve((path, _) => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, """{"station":{"id":"stn-9","code":"yaqobi"}}"""),
            "/api/pump/device/bind" => bindOk
                ? Json(HttpStatusCode.Created,
                    """{"deviceToken":"pd_ok","station":{"id":"stn-9"},"license":"lic.ok","publicKey":"pk-1"}""")
                : Json(HttpStatusCode.Forbidden,
                    """{"error":{"code":"device_limit","message":"سقفِ کامپیوترهای این پمپ پر است"}}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });
        var (link, _) = Link(s => { s.CloudAccountToken = "acc-1"; s.CloudAccessExpiresAt = InAnHour; });

        await link.HomeFromAccountAsync();
        Assert.Contains("سقفِ کامپیوترهای این پمپ پر است", CloudLink.LastBindWhy);
        Assert.Equal("", AppSettings.Load().CloudDeviceToken);

        //  ⛔ و چراغ همان دلیل را می‌گوید (بازبینیِ ۱۴۰۵/۰۷/۲۰: آزمونِ سورسِ قدیم
        //  این را داشت و جانشینِ رفتاری‌اش نداشت)
        var hadStation = CloudLink.AccountHasStation;
        try
        {
            CloudLink.AccountHasStation = true;
            Assert.Contains("سقفِ کامپیوترهای این پمپ پر است",
                PumpYaqobi.App.ViewModels.MainViewModel.UnboundWhy(true));
        }
        finally { CloudLink.AccountHasStation = hadStation; }

        //  کلیکِ کاربر (ترمزِ ده‌دقیقه‌ای را نمی‌خورد) پس از رفعِ مشکل
        bindOk = true;
        CloudLink.ResetReach();
        await link.HomeFromAccountAsync();
        Assert.Equal("", CloudLink.LastBindWhy);
        Assert.Equal("pd_ok", AppSettings.Load().CloudDeviceToken);
    }

    /// <summary>
    /// ⛔ <b>«کلیدِ بی‌مصرف» یعنی نه توکن و نه مجوز.</b> نصبی که مجوز دارد
    /// (حتی بی توکنِ دستگاه) کلیدش قفل است و کلیدِ ناجورِ سرور را رد می‌کند —
    /// بی این، یک سرورِ ساختگی مجوزِ خودش را جای مجوزِ امضاشده می‌نشاند.
    /// (بازبینیِ ۱۴۰۵/۰۷/۲۰: شرطِ ‎CloudLicense‎ دیگر هیچ آزمونی نداشت.)
    /// </summary>
    [Fact]
    public async Task KelideNajur_BaMajvozeTanha_RahaNemishavad()
    {
        Serve((path, _) => path switch
        {
            "/api/pump/me" => Json(HttpStatusCode.OK, """{"station":{"id":"stn-9","code":"yaqobi"}}"""),
            "/api/pump/device/bind" => Json(HttpStatusCode.Created,
                """{"deviceToken":"pd_x","station":{"id":"stn-9"},"license":"lic.y","publicKey":"pk-2"}"""),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });
        var (link, _) = Link(s =>
        {
            s.CloudAccountToken = "acc-1"; s.CloudAccessExpiresAt = InAnHour;
            s.CloudPublicKey = "pk-1"; s.CloudLicense = "lic.x"; s.CloudDeviceToken = "";
        });

        var r = await link.BindAsync();

        Assert.False(r.Ok);
        Assert.Equal("key_mismatch", r.Code);
        var disk = AppSettings.Load();
        Assert.Equal("pk-1", disk.CloudPublicKey);
        Assert.Equal("", disk.CloudDeviceToken);
    }

    /// <summary>پروفایل هم همان دلیل را نشان می‌دهد — همان خطِ آزمونِ پیشین.</summary>
    [Fact]
    public void Profile_HamanDalil_RaNeshanMidahad() =>
        Assert.Contains("CloudLink.LastBindWhy", SrcText.Read(Path.Combine(Native(), "PumpYaqobi.App",
            "ViewModels", "Sections", "AccountSectionViewModel.cs")));

    private static string Native([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, ".."));
}
