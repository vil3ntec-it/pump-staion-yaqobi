using System.Net;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «VIP · ۳۵۷ روز · بدون ورود» و همگام‌سازیِ ‎403 plan_no_services‎ (۱۴۰۵/۰۷/۲۱) ══
///
/// گزارشِ صاحب ریپو با عکس: سربرگ از کدِ بی‌اینترنت «وی‌آی‌پی» می‌گفت و
/// «وضعِ اتصال» «۵۵۷ در صف — این کار در پلنِ شما نیست (خدماتِ سرور)». یعنی
/// سرورِ حساب برای همان پمپ خدماتِ سرور را باز نکرده بود، و برنامه:
///   ۱) پاسخِ ‎200 covered‎ را «رسید» می‌شمرد و برای همیشه دیگر نمی‌پرسید؛
///   ۲) ‎plan_no_services‎ِ سرور را هیچ‌وقت به کدِ بی‌اینترنت ربط نمی‌داد؛
///   ۳) جملهٔ عمومیِ پلن را نشان می‌داد، نه دلیلِ واقعی.
/// هر سه رفتاری سنجیده می‌شوند — با سرورِ ساختگی (‎CloudLink.TestTransport‎).
/// </summary>
[Collection(AppHostCollection.Name)]
public class OfflineServicesGapTests : IDisposable
{
    //  همان کد و کلیدِ ‎OfflineKeyV2Tests‎ (خودِ کدِ جاوااسکریپتِ سرورِ حساب ساخته)
    private const string Spki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEoGvbjXcCZpEEWcuDCBH9xN5FX/zlspcf+Ns4awh2RZpx3kA6kZBfg+1CbMPaNqyyp1v9/BQnGrZgw5Spp2EZ6g==";
    private const string MachineGuid = "TEST-MACHINE-GUID-0001";
    private const string Vip = "08163-B936D-C8V53-P0000-000A1-C60T0-Z20N8-B2HQP-ZZ7WZ-TMPP9-1JKES-YYEMP-867SV-NFR1H-YHJ51-QC330-14Q4M-VECKP-Y5AZ6-SYRGP-4V4YN-BRGSW-R30YR-C3EVZ-YC137-ZA5G0-CG7VE-CQM";
    private const long IssuedDayMs = 1789948800000;
    private const long Day = 86_400_000;

    private readonly string _dir;
    private readonly string? _was;
    private readonly List<string> _hits = new();

    public OfflineServicesGapTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-offgap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        CloudConfig.MachineIdOverride = () => MachineGuid;
        CloudConfig.TestOfflineKeys = new Dictionary<string, string> { ["#1"] = Spki };
        CloudConfig.TestLicenseKeys = new Dictionary<string, string>();
        CloudLink.ResetOfflineServerState();
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        CloudLink.ResetOfflineServerState();
        AppSettings.DirOverride = _was;
        CloudConfig.MachineIdOverride = null;
        CloudConfig.TestOfflineKeys = null;
        CloudConfig.TestLicenseKeys = null;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private void Serve(Func<string, HttpResponseMessage> handler)
    {
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            lock (_hits) _hits.Add(path);
            return Task.FromResult(handler(path));
        };
    }

    private int Hits(string path) { lock (_hits) return _hits.Count(h => h == path); }

    /// <summary>نصبِ بندشده به پمپی، بی ورود به حساب، با کدِ بی‌اینترنتِ وی‌آی‌پی.</summary>
    private (AppSettings f, CloudLink link) VipInstall()
    {
        var f = AppSettings.Load();
        f.CloudDeviceToken = "dev-token";
        f.CloudStationId = "st_1";
        f.OfflineCode = Vip;
        f.Save();
        var c = OfflineKey.Stored(f, IssuedDayMs + Day);
        Assert.True(c.Valid, "پیش‌شرط: کدِ وی‌آی‌پی روی همین کامپیوتر پذیرفته است — " + c.Why);
        return (f, new CloudLink(f, () => { f.Save(); return Task.CompletedTask; }));
    }

    private const string Offline = "/api/pump/device/offline-code";
    private const string NoServices =
        "{\"error\":{\"code\":\"plan_no_services\",\"message\":\"این کار در پلنِ شما نیست (خدماتِ سرور) — برای وی‌آی‌پی، یا تمدیدِ خدماتِ دائمی، با پشتیبانی تماس بگیرید.\"}}";

    [Fact]
    public async Task Covered_BiKhadamat_RasidShemordeh_Nemishavad()
    {
        //  سرورِ حسابِ ۲.۱۱.۲۴: «covered»، و فهرستِ پمپ هیچ خدماتِ سروری ندارد
        Serve(p => p == Offline
            ? Json(HttpStatusCode.OK, "{\"status\":\"covered\",\"granted\":false,\"features\":[\"dashboard\",\"history\"],\"entitlement\":{\"features\":[\"dashboard\",\"history\"]}}")
            : Json(HttpStatusCode.NotFound, "{\"error\":{\"code\":\"not_found\",\"message\":\"x\"}}"));
        var (f, link) = VipInstall();

        await link.RedeemOfflineAsync();
        Assert.Equal(1, Hits(Offline));
        //  ⛔ «رسید» نیست: نشانِ «سرور هم دید» نخورد، و دلیل گفته می‌شود
        Assert.DoesNotContain("@st_1", AppSettings.Load().OfflineCodeRedeemed.Replace("!", "?"));
        Assert.Contains("خدماتِ سرور", CloudLink.OfflineServerWhy);
        Assert.Contains("به‌روز", CloudLink.OfflineServerWhy);
    }

    [Fact]
    public async Task PlanNoServices_AzHamgamSazi_KodRaDobareMifereste()
    {
        //  نصبی که نسخهٔ پیشین «covered» را رسید شمرده بود
        var (f, link) = VipInstall();
        var serial = OfflineKey.Stored(f, IssuedDayMs + Day).Serial;
        f.OfflineCodeRedeemed = serial + "@st_1";
        f.Save();
        var granted = false;
        Serve(p => p switch
        {
            "/api/sync/v1/push" => granted ? Json(HttpStatusCode.OK, "{\"applied\":0,\"results\":[]}") : Json(HttpStatusCode.Forbidden, NoServices),
            Offline => Json(HttpStatusCode.OK, "{\"status\":\"granted\",\"granted\":true,\"features\":[\"cloud\",\"messenger\",\"kar_app\"]}"),
            _ => Json(HttpStatusCode.NotFound, "{\"error\":{\"code\":\"not_found\",\"message\":\"x\"}}"),
        });

        //  بی خبرِ سرور، نشانِ روی دیسک یعنی «دیگر نپرس»
        await link.RedeemOfflineAsync();
        Assert.Equal(0, Hits(Offline));

        var push = await link.SyncPushAsync(new List<PumpYaqobi.Domain.Entities.SyncOp>
        {
            new() { OpId = "op1", TableName = "Debtors", RowUid = "r1", OpType = "upsert", ClientTs = 1, FieldsJson = "{}" },
        }, 557);
        Assert.False(push.Ok);
        Assert.Equal("plan_no_services", push.Code);

        //  ⛔ سرور گفت «خدماتِ سرور نداری» و کدِ وی‌آی‌پی این‌جاست ⇒ دوباره می‌رود
        await link.RedeemOfflineAsync();
        Assert.Equal(1, Hits(Offline));
        Assert.Equal("", CloudLink.OfflineServerWhy);
        //  و فقط یک بار در هر پنجره — سیلِ درخواست نمی‌سازد
        await link.SyncPushAsync(new List<PumpYaqobi.Domain.Entities.SyncOp>
        {
            new() { OpId = "op2", TableName = "Debtors", RowUid = "r1", OpType = "upsert", ClientTs = 2, FieldsJson = "{}" },
        }, 557);
        await link.RedeemOfflineAsync();
        Assert.Equal(1, Hits(Offline));
    }

    [Fact]
    public async Task JomleyeHamgamSazi_DalileVaghei_NaJomleyePlan()
    {
        var (f, link) = VipInstall();
        Serve(p => p == Offline
            ? Json(HttpStatusCode.Conflict, "{\"error\":{\"code\":\"code_used_elsewhere\",\"message\":\"این کد پیش از این روی پمپِ دیگری ثبت شده است\"}}")
            : Json(HttpStatusCode.Forbidden, NoServices));
        await link.RedeemOfflineAsync();

        var text = link.ServicesDeniedReason("این کار در پلنِ شما نیست (خدماتِ سرور)");
        Assert.Contains("کدِ بی‌اینترنتِ وی‌آی‌پی هنوز روی سرور ننشسته", text);
        Assert.Contains("پمپِ دیگری", text);
        Assert.DoesNotContain("در پلنِ شما نیست", text);

        //  بی کدِ بی‌اینترنت، همان جملهٔ خودِ سرور — حدسی ساخته نمی‌شود
        f.OfflineCode = "";
        f.Save();
        Assert.Equal("پیامِ سرور", link.ServicesDeniedReason("پیامِ سرور"));
    }

    [Fact]
    public async Task BandNashode_DalilashGofteMishavad()
    {
        var f = AppSettings.Load();
        f.OfflineCode = Vip;
        f.Save();
        Serve(_ => Json(HttpStatusCode.NotFound, "{}"));
        var link = new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });
        await link.RedeemOfflineAsync();
        Assert.Contains("بند نیست", CloudLink.OfflineServerWhy);
        Assert.Empty(_hits);
    }
}
