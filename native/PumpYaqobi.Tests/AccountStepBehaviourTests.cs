using System.Net;
using System.Text;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — «گامِ پمپ» و «تمام» با <b>رفتارِ</b> خودِ صفحهٔ پروفایل ═══════
///
/// دو قاعدهٔ ۱۴۰۵/۰۷/۱۰–۱۳ که تا امروز فقط با گشتنِ رشته در سورسِ
/// <c>AccountSectionViewModel</c> قفل بودند: واردشده همیشه «تمام» است (بند شدنِ
/// دستگاه دیوار نیست)، و گامِ پمپی که سرور خطا داد ولی پمپ را ساخته بود، مهرِ
/// ماندگار روی دیسک می‌زند — وگرنه «هر بار اسمِ پمپ را می‌خواهد» برمی‌گشت.
/// </summary>
[Collection(AppHostCollection.Name)]
public class AccountStepBehaviourTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-asb-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _was = AppSettings.DirOverride;
    private readonly AppHost _host;

    public AccountStepBehaviourTests()
    {
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
        CloudLink.ResetReach();
        CloudLink.TestTransport = (_, _) => Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{}"));
        //  ⚠️ صفحهٔ پروفایل از ‎AppHost.Current‎ هم می‌خواند، پس میزبانِ مشترک —
        //  و بیرونِ ‎_dir‎، چون پس از این آزمون هم زنده می‌ماند.
        _host = AppHost.Start(Path.Combine(Path.GetTempPath(), "pump-shared-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (_host.Auth.NeedsFirstRun()) _host.Auth.CreateFirstAdmin("1234");
        if (_host.Auth.HasPassword()) _host.Auth.SignIn("admin", "1234"); else _host.Auth.OpenWithoutPassword();
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        AppSettings.DirOverride = _was;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static void Seed(Action<AppSettings> a)
    {
        var f = AppSettings.Load();
        a(f);
        f.Save();
    }

    /// <summary>همان کاری که باز کردنِ صفحهٔ پروفایل می‌کند.</summary>
    private async Task<int> StepAsync()
    {
        var vm = new AccountSectionViewModel(_host);
        await vm.OnActivatedAsync();
        return vm.LoginStep;
    }

    private static long InAnHour => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;

    /// <summary>
    /// ⛔ واردشده، حتی با دستگاهِ بندنشده و بی پمپ، «تمام» است؛ بی‌حساب گامِ
    /// یک، مگر خودش «بعداً» زده باشد.
    /// </summary>
    [Fact]
    public async Task VaredShode_HamisheTamam_Ast()
    {
        Seed(f => { f.CloudAccountToken = "acc-1"; f.CloudAccessExpiresAt = InAnHour; f.CloudDeviceToken = ""; });
        Assert.Equal(4, await StepAsync());

        Seed(f => { f.CloudAccountToken = ""; f.LoginSkipped = false; });
        Assert.Equal(1, await StepAsync());

        Seed(f => f.LoginSkipped = true);
        Assert.Equal(4, await StepAsync());
    }

    /// <summary>
    /// ⛔ سرور سرِ ساختنِ پمپ خطا داد، ولی پمپ روی حساب هست ⇒ گام تمام و
    /// <b>مهرِ ماندگار روی دیسک</b>؛ صفحهٔ تازهٔ پروفایل دیگر نامِ پمپ نمی‌خواهد.
    /// </summary>
    [Fact]
    public async Task GamePomp_KhatayeSarvar_AmmaPompHast_MohreMandegar()
    {
        var meCalls = 0;
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/api/pump/me")
                return Task.FromResult(Interlocked.Increment(ref meCalls) == 1
                    ? Json(HttpStatusCode.OK, """{"station":null}""")
                    : Json(HttpStatusCode.OK, """{"station":{"id":"stn-1","name":"پمپِ کریم"}}"""));
            if (path == "/api/pump" && req.Method == HttpMethod.Post)
                return Task.FromResult(Json(HttpStatusCode.InternalServerError,
                    """{"error":{"code":"internal","message":"خطای داخلی سرور"}}"""));
            return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{}"));
        };
        Seed(f => { f.CloudAccountToken = "acc-1"; f.CloudAccessExpiresAt = InAnHour; f.PumpStepDone = false; });
        var vm = new AccountSectionViewModel(_host) { LoginPump = "پمپِ کریم" };

        await vm.FinishPumpCommand.ExecuteAsync(null);

        Assert.True(meCalls >= 2, "از سرور پرسیده نشد پمپ هست یا نه");
        Assert.Equal(4, vm.LoginStep);
        Assert.True(AppSettings.Load().PumpStepDone);
    }
    /// <summary>
    /// ⛔ روی پمپِ فعال‌نشده کارتِ اشتراک خالی نمی‌ماند — هر خانه حرفی دارد
    /// («فعال نشده»، «—»)، و تلفن و نشانیِ نداشته «—» است، نه هیچ.
    /// </summary>
    [Fact]
    public async Task KarteEshterak_RooyePompeFaalNashode_KhaliNemimanad()
    {
        Seed(f => { f.CloudAccountToken = "acc-1"; f.CloudAccessExpiresAt = InAnHour; f.CloudDeviceToken = ""; f.CloudLicense = ""; });
        var vm = new AccountSectionViewModel(_host);
        await vm.OnActivatedAsync();

        Assert.Equal("فعال نشده", vm.SubPlanText);
        Assert.Equal("—", vm.SubEndsText);
        Assert.Equal("—", vm.SubDaysText);
        Assert.False(string.IsNullOrWhiteSpace(vm.SubSourceText));
        Assert.False(string.IsNullOrWhiteSpace(vm.PumpPhone));
        Assert.False(string.IsNullOrWhiteSpace(vm.PumpAddress));
    }

    /// <summary>
    /// ⛔ «ثبت نیست» برای هر حال جملهٔ خودش را دارد — بی‌حساب به ورود، حسابِ
    /// بی‌پمپ به پروفایل — و هیچ‌کدام کاربر را به «ساختنِ پمپ» نمی‌فرستد.
    /// </summary>
    [Fact]
    public void SabtNist_HarHal_JomleyeKhodash()
    {
        var was = CloudLink.AccountHasStation;
        try
        {
            Assert.Contains("وارد حساب نشده‌اید", PumpYaqobi.App.ViewModels.MainViewModel.UnboundWhy(false));
            CloudLink.AccountHasStation = false;
            Assert.Contains("پروفایل", PumpYaqobi.App.ViewModels.MainViewModel.UnboundWhy(true));
            CloudLink.AccountHasStation = true;
            var bound = PumpYaqobi.App.ViewModels.MainViewModel.UnboundWhy(true);
            Assert.DoesNotContain("وارد حساب نشده‌اید", bound);
            foreach (var t in new[] { false, true })
                Assert.DoesNotContain("نامِ پمپ را بنویسید", PumpYaqobi.App.ViewModels.MainViewModel.UnboundWhy(t));
        }
        finally { CloudLink.AccountHasStation = was; }
    }
}
