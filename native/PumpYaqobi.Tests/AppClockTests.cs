using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «وقتی تاریخ رو عوض می‌کنم تمامِ سیستم به هم می‌خوره» (۱۴۰۵/۰۷/۱۵) ═══════════
///
/// خواستهٔ صاحب ریپو: «با تغییرِ تاریخ جلو و عقب اشتراکِ وی‌آی‌پی یا دور زده می‌شه
/// یا درجا ختم می‌شه و سرور رو هم بهم می‌زنه… تاریخ یک چیزِ نمایشی است نه این که
/// بخواد ماه‌های بخش‌ها یا جای دیگه رو بهم بزنه… و برنامه خودش ماه و سال و روز رو
/// از اینترنت ببینه.»
///
/// هر آزمون ساعتِ ویندوز را <b>واقعاً</b> جابه‌جا می‌کند (‎AppClock.WallSource‎) و
/// شمارندهٔ یکنواخت را جداگانه جلو می‌برد (‎AppClock.MonoSource‎) — همان دو چیزی
/// که روی کامپیوترِ واقعی از هم جدا حرکت می‌کنند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class AppClockTests : IDisposable
{
    private static readonly DateTime Real = new(2026, 9, 27, 9, 0, 0, DateTimeKind.Utc);   // ۵ میزان ۱۴۰۵
    private DateTime _wall = Real;
    private long _mono = 1_000_000;

    public AppClockTests()
    {
        AppClock.ResetForTests();
        AppClock.WallSource = () => _wall;
        AppClock.MonoSource = () => _mono;
        TimeSync.ResetForTests();
    }

    public void Dispose()
    {
        AppClock.ResetForTests();
        TimeSync.BootId = () => "";
        Entitlements.Now = () => AppClock.UnixMs;
        LicenseClock.ForgetRunning();
    }

    private void Pass(TimeSpan t) { _mono += (long)t.TotalMilliseconds; _wall += t; }

    // ── ۱) ساعتِ ویندوز وسطِ کار جلو و عقب می‌رود؛ برنامه نه ───────────────

    [Fact]
    public void SaateWindowsJeloVaAghab_TarikheBarname_TakanNemikhorad()
    {
        var start = AppClock.UnixMs;
        Assert.Equal(Real, AppClock.UtcNow);

        _wall = Real.AddYears(1);                      // کاربر تاریخ را یک سال جلو برد
        Pass(TimeSpan.FromMinutes(5));
        Assert.Equal(start + 5 * 60_000, AppClock.UnixMs);
        Assert.Equal(Shamsi.Of(Real.ToLocalTime()), Shamsi.Today());

        _wall = Real.AddYears(-2);                     // و بعد دو سال عقب
        Pass(TimeSpan.FromMinutes(5));
        Assert.Equal(start + 10 * 60_000, AppClock.UnixMs);
        Assert.Equal(Shamsi.MonthOf(Real.ToLocalTime()), Shamsi.ThisMonth());
    }

    [Fact]
    public void LarzesheKuchak_DonbalMishavad_TaSaateInternetNayamade()
    {
        var start = AppClock.UnixMs;
        _wall = _wall.AddSeconds(40);                  // همگام‌سازیِ خودکارِ ویندوز
        Assert.Equal(start + 40_000, AppClock.UnixMs);

        //  با ساعتِ اینترنت، ساعتِ ویندوز دیگر هیچ اثری ندارد — حتی لرزشِ کوچک
        AppClock.Accept(new DateTimeOffset(Real).ToUnixTimeMilliseconds(), _mono);
        _wall = _wall.AddSeconds(50);
        Assert.Equal(new DateTimeOffset(Real).ToUnixTimeMilliseconds(), AppClock.UnixMs);
    }

    // ── ۲) ساعتِ اینترنت حرفِ آخر است ───────────────────────────────────

    [Fact]
    public void SaateInternet_TarikheVaghei_Va_SaateWindowsRaMigooyad()
    {
        _wall = Real.AddDays(40);                      // برنامه با ساعتِ اشتباه بالا آمد
        Assert.Equal(Real.AddDays(40), AppClock.UtcNow);

        var req = new HttpRequestMessage(HttpMethod.Get, CloudConfig.Url("/api/health"));
        var res = new HttpResponseMessage(HttpStatusCode.OK);
        res.Headers.Date = new DateTimeOffset(Real);
        TimeSync.From(req, res, _mono);

        Assert.True(AppClock.Trusted);
        Assert.Equal(Real.AddMilliseconds(500), AppClock.UtcNow);
        Assert.True(AppClock.WallIsOff);
        Assert.Contains("جلو است", MainViewModel.SkewText(AppClock.WallSkewMs));
        Assert.Contains("40", MainViewModel.SkewText(AppClock.WallSkewMs));
    }

    [Fact]
    public void FaghatHttps_Va_FaghatSarvareHesabYaGithub_SaatMidahand()
    {
        foreach (var url in new[]
                 {
                     "http://api.vill3n.top/api/health",        // بی رمزگذاری
                     "https://192.168.1.10:4700/health",         // سرورِ خانگی (همان ساعتِ اشتباه)
                     "https://evil.example.com/",                // هر جای دیگر
                 })
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Headers.Date = new DateTimeOffset(Real.AddYears(-3));
            TimeSync.From(req, res, _mono);
            Assert.False(AppClock.Trusted, url);
        }

        //  پاسخی که رفت‌وبرگشتش بیش از ده ثانیه طول کشید هم نه
        var slow = new HttpRequestMessage(HttpMethod.Get, "https://github.com/");
        var r2 = new HttpResponseMessage(HttpStatusCode.OK);
        r2.Headers.Date = new DateTimeOffset(Real);
        var sent = _mono;
        _mono += 15_000;
        TimeSync.From(slow, r2, sent);
        Assert.False(AppClock.Trusted);

        Assert.True(TimeSync.IsTrusted(new Uri("https://github.com/x")));
        Assert.True(TimeSync.IsTrusted(new Uri(CloudConfig.Url("/api/pump/me"))));
    }

    // ── ۳) بسته و باز کردنِ برنامه روی همان روشن‌بودنِ کامپیوتر ─────────

    [Fact]
    public void BazKardaneDobare_BiInternet_SaateInternetRaNegahMidarad()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-clock-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var keep = AppSettings.DirOverride;
        AppSettings.DirOverride = dir;
        try
        {
            TimeSync.BootId = () => "w42";
            var req = new HttpRequestMessage(HttpMethod.Get, "https://github.com/");
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            res.Headers.Date = new DateTimeOffset(Real);
            TimeSync.From(req, res, _mono);
            Assert.True(File.Exists(Path.Combine(dir, "clock.json")));

            //  برنامه بسته شد؛ کاربر تاریخِ ویندوز را یک ماه عقب برد؛ برنامه باز شد
            AppClock.ResetForTests();
            _mono += 3_600_000;
            _wall = Real.AddMonths(-1);
            AppClock.WallSource = () => _wall;
            AppClock.MonoSource = () => _mono;
            TimeSync.Restore();
            Assert.True(AppClock.Trusted);
            Assert.Equal(Real.AddHours(1).AddMilliseconds(500), AppClock.UtcNow);

            //  خاموش و روشن شد (شناسهٔ دیگر) ⇒ فقط «عقب‌تر از آخرین ساعتِ اینترنت نه»
            AppClock.ResetForTests();
            _wall = Real.AddMonths(-1);
            AppClock.WallSource = () => _wall;
            AppClock.MonoSource = () => 5_000;
            TimeSync.BootId = () => "w43";
            TimeSync.Restore();
            Assert.False(AppClock.Trusted);
            Assert.True(AppClock.UtcNow >= Real);
        }
        finally
        {
            AppSettings.DirOverride = keep;
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // ── ۴) اشتراک: نه «درجا ختم»، نه «دور زدن» ───────────────────────────

    private static (ECDsa, string) Server()
    {
        var k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (k, Convert.ToBase64String(k.ExportSubjectPublicKeyInfo()));
    }

    private static string Sign(ECDsa key, object payload)
    {
        static string B64(byte[] b) => Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var h = B64(Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"TLIC"}"""));
        var b = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var s = key.SignData(Encoding.UTF8.GetBytes($"{h}.{b}"), HashAlgorithmName.SHA256,
                             DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{h}.{b}.{B64(s)}";
    }

    private static AppSettings Vip(long iat)
    {
        var (key, pub) = Server();
        return new AppSettings
        {
            CloudDeviceUid = "pc-clock",
            CloudDeviceToken = "dev",
            CloudStationId = "stn_clock",
            CloudPublicKey = pub,
            CloudLicense = Sign(key, new Dictionary<string, object?>
            {
                ["iss"] = "tohid-license-server", ["aud"] = "tohid-pump-app",
                ["duid"] = "pc-clock", ["stn"] = "stn_clock",
                ["iat"] = iat, ["nbf"] = iat - 60_000, ["exp"] = iat + 10L * 86_400_000,
                ["sub_ends"] = iat + 365L * 86_400_000,
                ["feat"] = Entitlements.Paid, ["plan_title"] = "vip",
            }),
        };
    }

    [Fact]
    public void JeloBordaneSaat_EshterakRaDarjaTamamNemikonad()
    {
        var keep = Entitlements.Unlocked;
        Entitlements.Unlocked = false;
        try
        {
            Entitlements.Now = () => AppClock.UnixMs;
            var f = Vip(AppClock.UnixMs);
            Assert.True(Entitlements.State(f).Allows(Entitlements.Dashboard));

            _wall = Real.AddYears(1);                   // تاریخِ ویندوز یک سال جلو
            Pass(TimeSpan.FromMinutes(1));
            LicenseClock.Tick(f);                       // حلقهٔ پس‌زمینه کف را جلو می‌برد…
            Assert.True(f.ClockFloorMs < new DateTimeOffset(Real.AddDays(1)).ToUnixTimeMilliseconds(),
                        "کفِ ساعتِ مجوز با ساعتِ جلورفتهٔ ویندوز یک سال جلو رفت");
            var st = Entitlements.State(f);
            Assert.True(st.Open, "ساعتِ ویندوزِ جلورفته اشتراکِ سالم را درجا بست");
            Assert.True(st.Allows(Entitlements.Dashboard));

            //  ⚠️ و همان سنجه با ساعتِ خام (رفتارِ پیش از ۱۴۰۵/۰۷/۱۵): بسته می‌شد
            var raw = new DateTimeOffset(_wall).ToUnixTimeMilliseconds();
            Assert.False(LicenseGuard.CheckStored(f, raw).Valid);
        }
        finally { Entitlements.Unlocked = keep; }
    }

    [Fact]
    public void AghabBordaneSaat_EshterakeTamamShodeRaZendeNemikonad()
    {
        var keep = Entitlements.Unlocked;
        Entitlements.Unlocked = false;
        try
        {
            Entitlements.Now = () => AppClock.UnixMs;
            //  مجوزی که یک ماه پیش صادر شد و بیست روز است منقضی شده
            var f = Vip(AppClock.UnixMs - 30L * 86_400_000);
            f.CloudLicense = f.CloudLicense;   // همان
            Assert.False(Entitlements.State(f).Open);

            _wall = Real.AddDays(-25);                  // تاریخِ ویندوز عقب، به روزِ «معتبر»
            Pass(TimeSpan.FromSeconds(5));
            Assert.False(Entitlements.State(f).Open, "عقب بردنِ ساعتِ ویندوز مجوزِ منقضی را زنده کرد");
        }
        finally { Entitlements.Unlocked = keep; }
    }

    [Fact]
    public void KafeAyandeh_BaMajvozeTazeVaSaateInternet_PaeenMiayad()
    {
        //  نصبی که پیش از این اصلاح ساعتش را جلو برده بود: کفِ روی دیسک یک سال جلوست
        var real = new DateTimeOffset(Real).ToUnixTimeMilliseconds();
        var f = new AppSettings { ClockFloorMs = real + 365L * 86_400_000 };
        AppClock.Accept(real, _mono);                  // ساعتِ اینترنت آمد
        LicenseClock.Anchor(f, real - 5_000, fresh: true);   // و مجوزِ تازهٔ امضاشده
        Assert.Equal(real - 5_000, f.ClockFloorMs);
    }

    [Fact]
    public void MohreAyandeh_TazeSaziyeMajvozRaKhamushNemikonad()
    {
        var src = Read("PumpYaqobi.App", "Services", "CloudLink.cs");
        Assert.Contains("var due = _settings.CloudSyncedAt <= 0 || since < 0 || since >= (long)LicenseTick.TotalMilliseconds;", src);
    }

    // ── ۵) هیچ پاکِ همیشگی‌ای با ساعتِ نامطمئن ─────────────────────────

    [Fact]
    public void BiSaateMotmaen_ChatPakeHamishegiNemishavad()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-chatclock-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var s = new ChatStore(dir);
            const long day = 86_400_000;
            var t0 = new DateTimeOffset(Real).ToUnixTimeMilliseconds();
            s.Upsert(new ChatRow("k1", "t", 1, "x", "hi", "text", null, false, t0, Anchor: t0));
            s.Sweep(t0 + 16 * day);                     // به سطل — برگشت‌پذیر
            var (_, purged) = s.Sweep(t0 + 400 * day);   // ساعتِ ویندوز یک سال جلو، بی اینترنت
            Assert.Equal(0, purged);
            Assert.False(AppClock.SafeToPurge);
            AppClock.Accept(t0 + 40 * day, _mono);
            Assert.True(AppClock.SafeToPurge);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        var trash = Read("PumpYaqobi.App", "ViewModels", "Sections", "TrashSectionViewModel.cs");
        Assert.Contains("if (AppClock.SafeToPurge) try { await _host.Trash.PruneAsync(); }", trash);
    }

    // ── ۶) ترمزها با شمارندهٔ یکنواخت ────────────────────────────────────

    [Fact]
    public void Mono_BaSaateWindowsHichRabtiNadarad()
    {
        var a = AppClock.Mono;
        _wall = Real.AddYears(-5);
        _mono += 600_000;
        Assert.Equal(TimeSpan.FromMinutes(10), AppClock.Mono - a);
        _wall = Real.AddYears(5);
        _mono += 1;
        Assert.True(AppClock.Mono > a);
    }

    // ── ۷) تاریخ با اسلش ───────────────────────────────────────────────────

    [Fact]
    public void Tarikh_BaEslash_NaNoghte()
    {
        var t = MainViewModel.HeaderDate(new DateTime(2026, 9, 27, 9, 0, 0)).Trim('⁧', '⁩');
        Assert.Equal("یک‌شنبه میزان 1405/7/5", t);
        Assert.DoesNotContain(".", t);
    }

    // ── ۸) ⛔ هیچ فایلی در برنامه ساعتِ خام نمی‌خواند ─────────────────────

    [Fact]
    public void HichSaateKham_DarBarname_Nist()
    {
        var root = Root();
        var raw = new Regex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b");
        var bad = new List<string>();
        foreach (var proj in new[] { "PumpYaqobi.App", "PumpYaqobi.Application", "PumpYaqobi.Services",
                                     "PumpYaqobi.Domain", "PumpYaqobi.Infrastructure",
                                     "PumpYaqobi.Persistence", "PumpYaqobi.Reporting" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, proj), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
                if (file.EndsWith("AppClock.cs")) continue;          // تنها خوانندهٔ ساعتِ ویندوز
                var n = 0;
                foreach (var line in File.ReadLines(file))
                {
                    n++;
                    var code = line.Split("//")[0];
                    if (code.TrimStart().StartsWith("///") || code.TrimStart().StartsWith("*")) continue;
                    if (raw.IsMatch(code)) bad.Add(Path.GetRelativePath(root, file) + ":" + n + "  " + line.Trim());
                }
            }
        }
        Assert.True(bad.Count == 0, "ساعتِ خامِ ویندوز — از AppClock بخوانید:\n" + string.Join("\n", bad));
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));
}

/// <summary>
/// ══ «پیامِ رو مخ هر بار میاد و بسته نمی‌شه» (۱۴۰۵/۰۷/۱۵) ═════════════════════
/// </summary>
[Collection(AppHostCollection.Name)]
public class SeenNoticesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-seen-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _keep = AppSettings.DirOverride;

    public SeenNoticesTests()
    {
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _keep;
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void PayameModir_YekBar_Na_BaHarBazShodan()
    {
        Assert.False(SeenNotices.Seen("ntc_1"));
        SeenNotices.MarkSeen("ntc_1");
        Assert.True(SeenNotices.Seen("ntc_1"));      // اجرای بعدیِ برنامه همان فایل را می‌خواند

        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Services", "SyncEngine.cs"));
        Assert.Contains("if (SeenNotices.Seen(n.Id)) { await MarkReadAsync(cloud, n.Id, ct); continue; }", src);
        Assert.Contains("await cloud.NoticeReadAsync(id, ct);", src);
    }

    [Fact]
    public void Bastan_YaniBastan_TaHaleEshterakAvazShavad()
    {
        SeenNotices.DismissedBanner = "closed";
        Assert.Equal("closed", SeenNotices.DismissedBanner);
        var vm = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "MainViewModel.cs"));
        var i = vm.IndexOf("private void CloseNotice()", StringComparison.Ordinal);
        var body = vm[i..vm.IndexOf("\n    }", i)];
        Assert.Contains("SeenNotices.DismissedBanner = SoftLock.BannerKind();", body);
        Assert.Contains("NoticeText = \"\";", body);
        Assert.DoesNotContain("NoticeText = SoftLock.Banner()", vm);
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
