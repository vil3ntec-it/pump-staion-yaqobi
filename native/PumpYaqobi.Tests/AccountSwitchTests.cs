using System.Net;
using System.Net.Http;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «حسابِ دیگری از این دستگاه وارد شد» ════════════════════════════════════
///
/// بندِ ۱۲ی خواستهٔ صاحب ریپو: «بررسی کن اگر یک حساب از دستگاه دیگری وارد
/// شود چه اتفاقی می‌افتد. نباید اطلاعاتِ حسابِ قبلی باقی بماند · اطلاعاتِ
/// محلیِ حسابِ قبلی با حسابِ جدید مخلوط شود · Syncِ حسابِ قبلی با حسابِ جدید
/// ترکیب شود · Tokenِ حسابِ قبلی اشتباهاً برای حسابِ جدید استفاده شود.»
/// و بندِ ۸: «هنگامِ تغییرِ حساب یا خروج، اطلاعاتِ حسابِ قبلی نباید به حسابِ
/// بعدی نشت کند.»
///
/// ⛔ <b>نشتی که این فایل قفلش می‌کند</b>: ورودِ یک حسابِ دیگر روی همین نصب
/// فقط چهار فیلدِ حساب را عوض می‌کرد. توکنِ <b>دستگاه</b>، شناسهٔ پمپ، مجوز،
/// کلیدِ عمومی، کدِ اپِ کارمندان، نشانی و رمزِ سرورِ خانگی و مُهرِ ارفاق همه
/// می‌ماندند — پس عکسِ حساب‌ها به پوشهٔ ابریِ <b>پمپِ قبلی</b> می‌رفت و
/// اشتراک و کدِ او روی پروفایلِ حسابِ تازه دیده می‌شد.
///
/// ⚠️ و طرفِ دیگرِ سکه هم این‌جا قفل است: <b>ورودِ دوبارهٔ همان حساب</b> هیچ
/// چیزی را باز نمی‌کند. قاعدهٔ «مشتریِ امروزی نباید با یک به‌روزرسانی از
/// پمپش جدا شود» از قفلِ ناخواسته مهم‌تر است.
/// </summary>
//  ⚠️ `AppSettings.DirOverride` **استاتیک** است و xUnit کلاس‌ها را موازی
//  می‌دواند؛ بی این نشان، این کلاس پوشهٔ تنظیمات را زیرِ پای کلاسِ دیگری
//  عوض می‌کند (و کلیدِ کَش‌شدهٔ `SecretStore` را هم باطل می‌کند). همین یک
//  خطِ جامانده بود که `SettingsDurabilityTests` را **گاهی** سرخ می‌کرد.
[Collection(AppHostCollection.Name)]
public class AccountSwitchTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    public AccountSwitchTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-switch-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private readonly List<string> _hits = new();

    private void Serve(Func<string, HttpResponseMessage> handler) =>
        CloudLink.TestTransport = (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            _hits.Add(path);
            return Task.FromResult(handler(path));
        };

    private static long InAnHour => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3_600_000;

    /// <summary>پاسخِ ورود — به همان شکلی که <c>shop</c> می‌دهد.</summary>
    private static string LoginPayload(string id, string mail) =>
        "{\"accessToken\":\"acc-new\",\"refreshToken\":\"ref-new\",\"accessExpiresAt\":" + InAnHour
        + ",\"user\":{\"id\":\"" + id + "\",\"email\":\"" + mail + "\",\"name\":\"نامِ تازه\"}}";

    /// <summary>نصبی که به پمپِ «الف» بند است و اشتراک هم دارد.</summary>
    private (CloudLink Link, AppSettings File) BoundToPumpA(string? userId, string mail)
    {
        var f = AppSettings.Load();
        f.CloudUserId = userId ?? "";
        f.CloudEmail = mail;
        f.CloudName = "صاحبِ پمپِ الف";
        f.CloudDeviceToken = "dev-alef";
        f.CloudStationId = "stn-alef";
        f.CloudLicense = "licence-alef";
        f.CloudPublicKey = "key-alef";
        f.CloudAccessCode = "K7PM3XQ2";
        f.EntitledUntil = InAnHour;
        f.EntitledPlan = "vip";
        f.ServerUrl = "http://192.168.1.50:4701";
        f.ServerToken = "write-alef";
        f.ServerReadKey = "read-alef";
        f.ServerId = "srv-alef";
        f.Save();
        return (new CloudLink(f, () => { f.Save(); return Task.CompletedTask; }), f);
    }

    // ── ۱) حسابِ دیگر ⇒ بندهای پمپِ قبلی باز می‌شوند ─────────────────────

    [Fact]
    public async Task HesabeDigar_BandhayePompeGhabli_BazMishavand()
    {
        Serve(_ => Json(HttpStatusCode.OK, LoginPayload("user-be", "be@gmail.com")));
        var (link, _) = BoundToPumpA("user-alef", "alef@gmail.com");

        var res = await link.SignInWithPasswordAsync("be@gmail.com", "ramz-1234");
        Assert.True(res.Ok, res.Why);

        var f = AppSettings.Load();
        //  ⭐ هیچ بندی از پمپِ الف نماند
        Assert.Equal("", f.CloudDeviceToken);
        Assert.Equal("", f.CloudStationId);
        Assert.Equal("", f.CloudLicense);
        Assert.Equal("", f.CloudPublicKey);
        Assert.Equal("", f.CloudAccessCode);
        Assert.Equal("", f.ServerUrl);
        Assert.Equal("", f.ServerToken);
        Assert.Equal("", f.ServerReadKey);
        Assert.Equal("", f.ServerId);
        Assert.Equal(0, f.EntitledUntil);
        Assert.Equal("", f.EntitledPlan);

        //  و حسابِ تازه سرِ جایش نشست
        Assert.Equal("acc-new", f.CloudAccountToken);
        Assert.Equal("user-be", f.CloudUserId);
        Assert.Equal("be@gmail.com", f.CloudEmail);

        //  و برنامه می‌داند که باید به کاربر بگوید
        Assert.True(link.AccountSwitched);
    }

    /// <summary>
    /// ⛔ و دفتر یک بیت هم دست نمی‌خورد — از روی خودِ سورس، نه از روی
    /// امید: هیچ‌کدام از دو تابعِ این مسیر نامی از دیتابیس نمی‌برند.
    /// </summary>
    [Fact]
    public void JabejayiyeHesab_DaftarRa_DastNemizanad()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Services", "CloudLink.cs"));
        var body = Between(src, "private async Task ReleaseIfOtherAccountAsync", "public bool AccountSwitched")
                 + Between(src, "public async Task ForgetStationAsync", "HomeFromAccountAsync");
        var code = string.Join('\n', body.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///")));

        foreach (var forbidden in new[] { "PumpDbContext", "DbFactory", "SaveChangesAsync", "Debtor", "Ledger" })
            Assert.DoesNotContain(forbidden, code);
    }

    // ── ۲) همان حساب ⇒ هیچ چیزی باز نمی‌شود ──────────────────────────────

    [Fact]
    public async Task HamanHesab_HichBandi_BazNemishavad()
    {
        Serve(_ => Json(HttpStatusCode.OK, LoginPayload("user-alef", "alef@gmail.com")));
        var (link, _) = BoundToPumpA("user-alef", "alef@gmail.com");

        Assert.True((await link.SignInWithPasswordAsync("alef@gmail.com", "ramz-1234")).Ok);

        var f = AppSettings.Load();
        Assert.Equal("dev-alef", f.CloudDeviceToken);
        Assert.Equal("stn-alef", f.CloudStationId);
        Assert.Equal("K7PM3XQ2", f.CloudAccessCode);
        Assert.Equal("read-alef", f.ServerReadKey);
        Assert.False(link.AccountSwitched);
    }

    /// <summary>
    /// ⚠️ ایمیلِ عوض‌شدهٔ <b>همان</b> حساب جابه‌جایی نیست — شناسه ملاک است،
    /// نه ایمیل. (خودِ کاربر می‌تواند ایمیلِ حسابش را عوض کند.)
    /// </summary>
    [Fact]
    public async Task HamanShenase_BaEmaileTaze_JabejayiNist()
    {
        Serve(_ => Json(HttpStatusCode.OK, LoginPayload("user-alef", "alef-new@gmail.com")));
        var (link, _) = BoundToPumpA("user-alef", "alef@gmail.com");

        Assert.True((await link.SignInWithPasswordAsync("alef-new@gmail.com", "ramz-1234")).Ok);

        var f = AppSettings.Load();
        Assert.Equal("dev-alef", f.CloudDeviceToken);
        Assert.Equal("alef-new@gmail.com", f.CloudEmail);
        Assert.False(link.AccountSwitched);
    }

    // ── ۳) نصبِ کهنه‌ای که شناسه ندارد ⇒ ایمیل ملاک است ──────────────────

    [Fact]
    public async Task NasbeKohne_BiShenase_BaEmail_Tashkhis_MidahaD()
    {
        Serve(_ => Json(HttpStatusCode.OK, LoginPayload("user-be", "be@gmail.com")));
        var (link, _) = BoundToPumpA(null, "alef@gmail.com");

        Assert.True((await link.SignInWithPasswordAsync("be@gmail.com", "ramz-1234")).Ok);

        Assert.Equal("", AppSettings.Load().CloudDeviceToken);
        Assert.True(link.AccountSwitched);
    }

    [Fact]
    public async Task NasbeKohne_BiShenase_HamanEmail_HichChizi_BazNemishavad()
    {
        Serve(_ => Json(HttpStatusCode.OK, LoginPayload("user-alef", "ALEF@gmail.com")));
        var (link, _) = BoundToPumpA(null, "alef@gmail.com");

        Assert.True((await link.SignInWithPasswordAsync("alef@gmail.com", "ramz-1234")).Ok);

        //  ⚠️ بزرگی و کوچکیِ حروف ایمیل را عوض نمی‌کند
        Assert.Equal("dev-alef", AppSettings.Load().CloudDeviceToken);
        Assert.False(link.AccountSwitched);
    }

    /// <summary>
    /// ⚠️ هشدارِ یک جابه‌جایی نباید سرِ ورودهای بعدی هم تکرار شود — وگرنه
    /// کاربر هر بار فکر می‌کند دوباره چیزی پاک شد.
    /// </summary>
    [Fact]
    public async Task Hoshdar_DarVorudeBadi_TekrarNemishavad()
    {
        Serve(_ => Json(HttpStatusCode.OK, LoginPayload("user-be", "be@gmail.com")));
        var (link, _) = BoundToPumpA("user-alef", "alef@gmail.com");

        Assert.True((await link.SignInWithPasswordAsync("be@gmail.com", "ramz-1234")).Ok);
        Assert.True(link.AccountSwitched);

        //  همان حساب، بارِ دوم
        Assert.True((await link.SignInWithPasswordAsync("be@gmail.com", "ramz-1234")).Ok);
        Assert.False(link.AccountSwitched);
    }

    // ── ۴) نخستین ورودِ یک نصبِ تازه ⇒ چیزی برای باز کردن نیست ────────────

    [Fact]
    public async Task NasbeTaze_NokhostinVorud_HichHoshdari_Nemidahad()
    {
        Serve(_ => Json(HttpStatusCode.OK, LoginPayload("user-alef", "alef@gmail.com")));
        var f = AppSettings.Load(); f.Save();
        var link = new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });

        Assert.True((await link.SignInWithPasswordAsync("alef@gmail.com", "ramz-1234")).Ok);
        Assert.False(link.AccountSwitched);
        Assert.Equal("user-alef", AppSettings.Load().CloudUserId);
    }

    // ── ۵) جدا کردنِ دستیِ دستگاه از پمپ ─────────────────────────────────

    /// <summary>
    /// ⛔ تا امروز این کار <b>هیچ دکمه‌ای نداشت</b>: برنامه سرِ ورودِ حسابِ
    /// پمپِ دیگر می‌گفت «این دستگاه را از پمپِ فعلی جدا کنید» و راهش وجود
    /// نداشت. حالا فرمانِ <c>ForgetPumpCommand</c> در خودِ پروفایل است.
    /// </summary>
    [Fact]
    public void JodaKardaneDasti_DarKhodePerofile_Hast()
    {
        var vm = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections",
                                               "AccountSectionViewModel.cs"));
        Assert.Contains("ForgetPumpAsync", vm);
        Assert.Contains("Cloud.ForgetStationAsync()", vm);
        //  ⚠️ و پیش از انجام پرسیده می‌شود
        Assert.Contains("Dialogs.ConfirmAsync", Between(vm, "private Task ForgetPumpAsync", "RefreshAll();"));

        var view = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Views", "Sections",
                                                 "AccountSectionView.axaml"));
        Assert.Contains("ForgetPumpCommand", view);
        //  فقط وقتی بندی هست
        Assert.Contains("IsVisible=\"{Binding PumpBound}\"", view);
    }

    // ── ۶) ترمزِ «ارسالِ بی‌نهایتِ» کدِ ایمیل ─────────────────────────────

    /// <summary>
    /// بندِ آخرِ ورود: «درخواست‌های ورود قابلِ سوءاستفاده و ارسالِ بی‌نهایت
    /// نباشند.» هر زدنِ این دو دکمه یک <b>ایمیل</b> می‌فرستد و سقفِ سرور
    /// پنج تا در پانزده دقیقه است، پس زدنِ پشتِ سرِ هم خودِ کاربر را از
    /// ثبت‌نامش بیرون می‌انداخت.
    /// </summary>
    [Fact]
    public async Task KodeEmail_PoshteSareHam_Nemiravad()
    {
        Serve(_ => Json(HttpStatusCode.OK, """{"ok":true,"sent":true}"""));
        var f = AppSettings.Load(); f.Save();
        var link = new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });

        Assert.True((await link.RegisterStartAsync("هارون", "haroon@gmail.com", "ramz-1234")).Ok);
        Assert.Single(_hits);

        //  ⭐ دومی هیچ درخواستی نمی‌زند و می‌گوید چند ثانیه صبر کن
        var again = await link.RegisterStartAsync("هارون", "haroon@gmail.com", "ramz-1234");
        Assert.False(again.Ok);
        Assert.Equal("too_soon", again.Code);
        Assert.Contains("ثانیه", again.Why);
        Assert.Single(_hits);

        //  ⚠️ ولی ایمیلِ دیگری ربطی به این یکی ندارد
        Assert.True((await link.RegisterStartAsync("کریم", "karim@gmail.com", "ramz-1234")).Ok);
        Assert.Equal(2, _hits.Count);

        //  و «رمزِ فراموش‌شده» ترمزِ خودش را دارد
        Assert.True((await link.ForgotPasswordAsync("haroon@gmail.com")).Ok);
        Assert.Equal(3, _hits.Count);
        Assert.False((await link.ForgotPasswordAsync("haroon@gmail.com")).Ok);
        Assert.Equal(3, _hits.Count);
    }

    /// <summary>
    /// ⚠️ درخواستی که به سرور <b>نرسید</b> هیچ ایمیلی نفرستاده، پس نباید
    /// کاربر را یک دقیقه معطل کند — وگرنه کسی که مودمش خاموش بوده، بعد از
    /// روشن شدنش هم یک دقیقه گیر می‌کرد.
    /// </summary>
    [Fact]
    public async Task ErsaleNashode_TormozRa_NemizanaD()
    {
        var fail = true;
        Serve(_ => fail ? Json(HttpStatusCode.ServiceUnavailable, "{}")
                        : Json(HttpStatusCode.OK, """{"ok":true}"""));
        var f = AppSettings.Load(); f.Save();
        var link = new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });

        Assert.False((await link.RegisterStartAsync("هارون", "haroon@gmail.com", "ramz-1234")).Ok);
        fail = false;
        Assert.True((await link.RegisterStartAsync("هارون", "haroon@gmail.com", "ramz-1234")).Ok);
        Assert.Equal(2, _hits.Count);
    }

    // ── ابزار ───────────────────────────────────────────────────────────

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Between(string s, string from, string to)
    {
        var a = s.IndexOf(from, StringComparison.Ordinal);
        Assert.True(a >= 0, "پیدا نشد: " + from);
        var b = s.IndexOf(to, a, StringComparison.Ordinal);
        Assert.True(b > a, "پیدا نشد: " + to);
        return s[a..b];
    }
}
