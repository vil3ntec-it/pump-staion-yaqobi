using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «کسی نتواند کرک کند یا دور بزند» ═══════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو. این‌جا با رمزنگاریِ واقعی ساخته و سنجیده می‌شود،
/// نه با مجوزهای دست‌ساز: یک جفت‌کلیدِ P-256 می‌سازیم، دقیقاً همان‌طور که
/// سرور امضا می‌کند امضا می‌کنیم، و بعد راه‌های دور زدن را یکی‌یکی امتحان
/// می‌کنیم.
///
/// ⚠️ اگر روزی کسی یکی از این قیدها را بردارد، همین‌جا قرمز می‌شود.
/// </summary>
public class LicenseGuardTests
{
    private const string Device = "pc-abc123";
    private const string Station = "stn_test_1";

    /// <summary>جفت‌کلیدِ تازه — همان منحنی و همان شکلی که سرور دارد.</summary>
    private static (ECDsa key, string publicB64) NewServer()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        return (key, pub);
    }

    /// <summary>مجوزی دقیقاً به شکلی که سرور می‌سازد.</summary>
    private static string Sign(ECDsa key, object payload)
    {
        static string B64(byte[] b) =>
            Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var header = B64(Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"TLIC"}"""));
        var body = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signing = Encoding.UTF8.GetBytes($"{header}.{body}");
        //  امضای خام (r||s) — همان چیزی که سرور با dsaEncoding: 'ieee-p1363' می‌سازد
        var sig = key.SignData(signing, HashAlgorithmName.SHA256,
                               DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{body}.{B64(sig)}";
    }

    private static Dictionary<string, object?> GoodPayload(long now) => new()
    {
        ["iss"] = "tohid-license-server",
        ["aud"] = "tohid-pump-app",
        ["duid"] = Device,
        ["stn"] = Station,
        ["sub"] = Station,
        ["iat"] = now,
        ["nbf"] = now - 60_000,
        ["exp"] = now + 10L * 24 * 3600 * 1000,
        ["sub_ends"] = now + 30L * 24 * 3600 * 1000,
        ["feat"] = new[] { "safe", "cloud", "invoice" },
        ["core"] = new[] { "dashboard", "settings", "debtors" },
        ["plan_title"] = "ماهانه",
    };

    // ── مجوزِ درست ─────────────────────────────────────────────────────

    [Fact]
    public void MajozeDorost_Paziroftе_Mishavad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (key, pub) = NewServer();
        var token = Sign(key, GoodPayload(now));

        var r = LicenseGuard.Check(token, pub, Device, Station, now);

        Assert.True(r.Valid, r.Reason);
        Assert.Contains("safe", r.Features);
        Assert.Contains("cloud", r.Features);
        Assert.Equal("ماهانه", r.PlanTitle);
    }

    // ── راه‌های دور زدن ────────────────────────────────────────────────

    [Fact]
    public void SarvareSakhtegi_BaKelideKhodash_RadMishavad()
    {
        //  ⚠️ مهم‌ترین سنجه.
        //  کسی سرورِ خودش را بالا می‌آورد، کلیدِ خودش را می‌دهد و مجوزِ
        //  خودش را امضا می‌کند. اگر کلیدِ عمومی قفل نمی‌شد، همین کار
        //  کلِ قفل را بی‌اثر می‌کرد.
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (_, realPub) = NewServer();
        var (fakeKey, _) = NewServer();

        var forged = Sign(fakeKey, GoodPayload(now));

        var r = LicenseGuard.Check(forged, realPub, Device, Station, now);
        Assert.False(r.Valid);
        Assert.Contains("امضا", r.Reason);
    }

    [Fact]
    public void DastKariyeMohtava_EmzaRaMishkanad()
    {
        //  کسی payload را باز می‌کند، «cloud» را اضافه می‌کند و دوباره
        //  می‌بندد — بی این‌که بتواند امضا کند.
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (key, pub) = NewServer();

        var payload = GoodPayload(now);
        payload["feat"] = new[] { "safe" };
        var token = Sign(key, payload);

        var parts = token.Split('.');
        var tampered = JsonSerializer.Serialize(new Dictionary<string, object?>(payload)
        {
            ["feat"] = new[] { "safe", "cloud", "invoice", "staff" },
        });
        var swapped = Convert.ToBase64String(Encoding.UTF8.GetBytes(tampered))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var r = LicenseGuard.Check($"{parts[0]}.{swapped}.{parts[2]}", pub, Device, Station, now);
        Assert.False(r.Valid);
    }

    [Fact]
    public void MajozeComputereDigar_KarNemikonad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (key, pub) = NewServer();
        var token = Sign(key, GoodPayload(now));

        //  همان مجوز، روی کامپیوترِ دیگری
        var r = LicenseGuard.Check(token, pub, "pc-someone-else", Station, now);
        Assert.False(r.Valid);
        Assert.Contains("کامپیوترِ دیگری", r.Reason);
    }

    [Fact]
    public void MajozePompeDigar_KarNemikonad()
    {
        //  دو پمپ روی یک کامپیوتر: مجوزِ پمپِ اشتراک‌دار نباید پمپِ
        //  بی‌اشتراک را باز کند.
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (key, pub) = NewServer();
        var token = Sign(key, GoodPayload(now));

        var r = LicenseGuard.Check(token, pub, Device, "stn_other_pump", now);
        Assert.False(r.Valid);
        Assert.Contains("پمپِ دیگری", r.Reason);
    }

    [Fact]
    public void MajozeShop_RooyeBarnameyePomp_Nemineshinad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (key, pub) = NewServer();

        var payload = GoodPayload(now);
        payload["aud"] = "tohid-shop-app";      // مجوزِ بخشِ دکان
        var token = Sign(key, payload);

        var r = LicenseGuard.Check(token, pub, Device, Station, now);
        Assert.False(r.Valid);
        Assert.Contains("برنامهٔ دیگری", r.Reason);
    }

    [Fact]
    public void MajozeMonghazi_RadMishavad()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (key, pub) = NewServer();
        var token = Sign(key, GoodPayload(now));

        //  یازده روز بعد — مجوز ده‌روزه است
        var later = now + 11L * 24 * 3600 * 1000;
        var r = LicenseGuard.Check(token, pub, Device, Station, later);
        Assert.False(r.Valid);
        Assert.Contains("منقضی", r.Reason);
    }

    [Fact]
    public void BiKelideGhoflShode_HichMajozi_PaziroftеNemishavad()
    {
        //  پیش از اولین فعال‌سازی، هیچ مجوزی نباید پذیرفته شود — وگرنه
        //  کسی می‌توانست فایلِ تنظیمات را با مجوزِ ساختگی پر کند.
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (key, _) = NewServer();
        var token = Sign(key, GoodPayload(now));

        var r = LicenseGuard.Check(token, "", Device, Station, now);
        Assert.False(r.Valid);
        Assert.Contains("قفل نشده", r.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    public void MajozeBadghvare_RadMishavad(string bad)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (_, pub) = NewServer();
        Assert.False(LicenseGuard.Check(bad, pub, Device, Station, now).Valid);
    }

    // ── بخش‌های همیشه‌باز ──────────────────────────────────────────────

    [Fact]
    public void BakhshhayeHamishebaz_BaMajozeKharab_HamBazand()
    {
        //  صاحبِ پمپ باید همیشه دفترِ خودش را ببیند. گروگان گرفتنِ داده
        //  سریع‌ترین راهِ از دست دادنِ اعتماد است.
        var core = new[] { "dashboard", "settings", "debtors" };
        var broken = LicenseCheck.Fail("هر دلیلی");

        Assert.True(LicenseGuard.Allows(broken, "debtors", core));
        Assert.True(LicenseGuard.Allows(broken, "dashboard", core));
        Assert.False(LicenseGuard.Allows(broken, "safe", core));
        Assert.False(LicenseGuard.Allows(broken, "cloud", core));
    }

    [Fact]
    public void BaMajozeDorost_BakhshhayePooli_BazMishavand()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (key, pub) = NewServer();
        var ok = LicenseGuard.Check(Sign(key, GoodPayload(now)), pub, Device, Station, now);
        var core = new[] { "dashboard", "settings", "debtors" };

        Assert.True(LicenseGuard.Allows(ok, "safe", core));
        Assert.True(LicenseGuard.Allows(ok, "cloud", core));
        //  چیزی که در مجوز نیست، باز نمی‌شود
        Assert.False(LicenseGuard.Allows(ok, "sarrafi", core));
    }

    // ── شناسهٔ دستگاه ──────────────────────────────────────────────────

    [Fact]
    public void ShenasayeDastgah_Sabet_Mimanad()
    {
        var s = new AppSettings();
        var first = CloudConfig.DeviceUid(s);
        var second = CloudConfig.DeviceUid(s);

        Assert.Equal(first, second);
        Assert.StartsWith("pc-", first);
        //  و ذخیره شده، پس اجرای بعدیِ برنامه همان را می‌بیند
        Assert.Equal(first, s.CloudDeviceUid);
    }

    [Fact]
    public void NeshaniyeAbr_GhofleAst()
    {
        //  ⚠️ اگر روزی کسی این را به تنظیمات ببرد، همین‌جا قرمز می‌شود.
        Assert.Equal("https://api.vill3n.top", CloudConfig.BaseUrl);
        Assert.Equal("tohid-pump-app", CloudConfig.Audience);
    }
}

/// <summary>
/// ══ «نشانی توی خودِ برنامه باشد و دیده نشود» ═══════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو. این سنجه‌ها روی خودِ فایل‌ها می‌گردند، چون چیزی که
/// باید ثابت شود «نبودن» است — و نبودن را با اجرا نمی‌شود دید.
///
/// ⚠️ اگر روزی کسی نشانیِ ابر را به تنظیمات ببرد، همین‌جا قرمز می‌شود.
/// </summary>
public class CloudAddressLockTests
{
    private static string Root
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
                d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("ریشهٔ پروژه پیدا نشد");
        }
    }

    private static string Read(string rel) =>
        File.ReadAllText(Path.Combine(Root, rel));

    [Fact]
    public void NeshaniyeAbr_DarKhodeCode_Ast()
    {
        var src = Read("PumpYaqobi.App/Services/CloudConfig.cs");
        Assert.Contains("\"https://api.vill3n.top\"", src);
        Assert.Contains("const string BaseUrl", src);
    }

    [Fact]
    public void NeshaniyeAbr_AzTanzimat_Khandeh_Nemishavad()
    {
        //  ⚠️ قلبِ ماجرا: اگر BaseUrl از تنظیمات یا محیط خوانده شود، هر
        //  کسی می‌تواند برنامه را به سرورِ خودش ببرد و مجوزِ ساختگیِ خودش
        //  را قبول کند.
        var src = Read("PumpYaqobi.App/Services/CloudConfig.cs");
        var after = src[src.IndexOf("BaseUrl", StringComparison.Ordinal)..];
        var line = after[..after.IndexOf(';', StringComparison.Ordinal)];

        Assert.DoesNotContain("Settings", line);
        Assert.DoesNotContain("Environment.GetEnvironmentVariable", line);
        Assert.DoesNotContain("Config", line[7..]);   // خودِ نامِ کلاس را نشمار
    }

    [Fact]
    public void SafheyeTanzimat_KadreNeshaniyeAbr_Nadarad()
    {
        //  کادرِ نشانیِ سرورِ خانگی سرِ جایش است و باید باشد — آن چیزِ
        //  دیگری است. ولی هیچ کادری نباید نشانیِ **ابر** را بگیرد.
        var xaml = Read("PumpYaqobi.App/Views/Sections/SettingsSectionView.axaml");
        Assert.DoesNotContain("api.vill3n.top", xaml);
        Assert.DoesNotContain("CloudBaseUrl", xaml);
        Assert.DoesNotContain("CloudUrl", xaml);

        //  ⚠️ و از این به بعد، هیچ‌کدامِ این‌ها هم در تنظیمات نیستند.
        //  خواستهٔ صریحِ صاحب ریپو: «ادرس سرور از تو تنظیمات پاک بشه و
        //  دیده نشه… رمز نخاد… مشخصات پمپ رو هم از دید کاربر حذف کن.»
        //  هر کدام که برگردد، همین‌جا قرمز می‌شود.
        Assert.DoesNotContain("Binding ServerUrl", xaml);
        Assert.DoesNotContain("Binding SyncCode", xaml);
        Assert.DoesNotContain("Binding ViewerUrl", xaml);
        Assert.DoesNotContain("Binding StationCode", xaml);
        Assert.DoesNotContain("Binding StationName", xaml);
        Assert.DoesNotContain("Binding PairPin", xaml);

        //  اشتراک رفت به صفحهٔ «حسابِ من» — همان‌جا باید باشد
        var account = Read("PumpYaqobi.App/Views/Sections/AccountSectionView.axaml");
        Assert.Contains("SubCode", account);
        Assert.Contains("RedeemSubCommand", account);

        //  …ولی صفحهٔ حساب هم کادرِ نشانی ندارد و نباید داشته باشد
        Assert.DoesNotContain("api.vill3n.top", account);
        Assert.DoesNotContain("Binding ServerUrl", account);
        Assert.DoesNotContain("Binding SyncCode", account);
    }

    /// <summary>
    /// ورود با گوگل باید واقعاً در برنامه باشد — نه فقط در اپِ کارمندان.
    /// خواستهٔ صریحِ صاحب ریپو: «صفحهٔ لاگین با جیمیل هم داخلِ اپ نیست.»
    /// </summary>
    [Fact]
    public void SafheyeHesab_VorudBaGoogle_Darad()
    {
        var xaml = Read("PumpYaqobi.App/Views/Sections/AccountSectionView.axaml");
        Assert.Contains("SignInCommand", xaml);
        Assert.Contains("ورود با گوگل", xaml);
        //  پروفایل: نام و ایمیلِ حساب
        Assert.Contains("AccountEmail", xaml);

        //  ⚠️ رمزِ گوگل هیچ‌وقت داخلِ برنامه تایپ نمی‌شود: ورود از مرورگرِ
        //  سیستم می‌رود و با PKCE برمی‌گردد، بی هیچ رازِ کلاینتی.
        var src = Read("PumpYaqobi.App/Services/GoogleSignIn.cs");
        Assert.Contains("code_challenge", src);
        Assert.Contains("S256", src);
        //  ⚠️ دنبالِ **به‌کار رفتنش** می‌گردیم، نه دنبالِ خودِ واژه: توضیحِ
        //  بالای فایل هم همین کلمه را دارد و آن اشکالی ندارد.
        Assert.DoesNotContain("[\"client_secret\"]", src);
        Assert.DoesNotContain("client_secret=", src);
    }

    [Fact]
    public void Tanzimat_NeshaniyeAbr_Negah_Nemidarad()
    {
        var src = Read("PumpYaqobi.App/Services/AppSettings.cs");
        Assert.DoesNotContain("api.vill3n.top", src);
        //  توکن و کلید و مجوز نگه داشته می‌شوند — آن‌ها را سرور داده،
        //  نه کاربر
        Assert.Contains("CloudDeviceToken", src);
        Assert.Contains("CloudPublicKey", src);
    }
}
