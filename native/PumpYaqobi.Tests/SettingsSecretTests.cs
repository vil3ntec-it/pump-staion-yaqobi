using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ توکن‌ها دیگر خام روی دیسک نمی‌نشینند ═══════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «اگر برنامه از Token استفاده می‌کند، ذخیره‌سازیِ
/// آن را در محلِ امنِ مناسبِ سیستم‌عامل بررسی کن و آن را داخلِ فایلِ متنیِ
/// ساده یا تنظیماتِ قابلِ مشاهده ذخیره نکن.»
///
/// تا پیش از این، <c>%APPDATA%\PumpYaqobi\settings.json</c> این چهار تا را
/// خام نگه می‌داشت: توکنِ حساب، توکنِ تازه‌سازی (که روی سرور <b>نود روز</b>
/// عمر دارد)، توکنِ دستگاه و رمزِ نوشتنِ سرورِ خانگی.
///
/// ⚠️ سنجه روی خودِ <b>فایل</b> می‌گردد، نه روی شیءِ حافظه: «رمز شد» را فقط
/// متنِ روی دیسک ثابت می‌کند.
/// </summary>
public class SettingsSecretTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    public SettingsSecretTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-secret-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private string FileText() => File.ReadAllText(Path.Combine(_dir, "settings.json"));

    // ── ۱) رفت و برگشت ────────────────────────────────────────────────────

    [Fact]
    public void Tokenha_RoyeDisk_Kham_Nemimanand()
    {
        new AppSettings
        {
            CloudAccountToken = "acct-secret-123",
            CloudRefreshToken = "refresh-secret-456",
            CloudDeviceToken = "device-secret-789",
            ServerToken = "home-write-secret",
        }.Save();

        var raw = FileText();
        //  ⛔ هیچ‌کدام از چهار راز نباید در متنِ فایل پیدا شود
        Assert.DoesNotContain("acct-secret-123", raw);
        Assert.DoesNotContain("refresh-secret-456", raw);
        Assert.DoesNotContain("device-secret-789", raw);
        Assert.DoesNotContain("home-write-secret", raw);

        //  و کلیدِ خامِ کهنه هم دیگر در فایل نوشته نمی‌شود
        Assert.DoesNotContain("\"CloudAccountToken\":", raw);
        Assert.DoesNotContain("\"CloudRefreshToken\":", raw);
        Assert.DoesNotContain("\"CloudDeviceToken\":", raw);
        Assert.DoesNotContain("\"ServerToken\":", raw);

        //  ولی خودِ برنامه همان‌ها را دست‌نخورده می‌خواند
        var back = AppSettings.Load();
        Assert.Equal("acct-secret-123", back.CloudAccountToken);
        Assert.Equal("refresh-secret-456", back.CloudRefreshToken);
        Assert.Equal("device-secret-789", back.CloudDeviceToken);
        Assert.Equal("home-write-secret", back.ServerToken);
    }

    /// <summary>خالی باید خالی بماند، وگرنه <c>Activated</c> دروغ می‌گوید.</summary>
    [Fact]
    public void Khali_Khali_Mimanad()
    {
        new AppSettings().Save();
        var back = AppSettings.Load();
        Assert.Equal("", back.CloudAccountToken);
        Assert.Equal("", back.CloudDeviceToken);
        Assert.False(SecretStore.IsProtected(""));
    }

    // ── ۲) مهاجرت: نصبِ امروزی نباید از حساب بیرون بیفتد ──────────────────

    /// <summary>
    /// ⚠️ **مهم‌ترین بندِ این فایل.** کاربری که همین حالا برنامه را دارد،
    /// توکن‌هایش خام در فایل است. اگر به‌روزرسانی آن‌ها را نادیده می‌گرفت،
    /// همهٔ مشتری‌های امروز یک‌شبه از حساب بیرون می‌افتادند و باید دوباره
    /// کدِ شش‌رقمیِ اشتراک را می‌زدند.
    /// </summary>
    [Fact]
    public void FayleKohne_KhamAst_VaBazHamKhandeMishavad()
    {
        var legacy = """
        {
          "ThemeId": "gold",
          "ServerToken": "old-home-token",
          "CloudDeviceToken": "old-device-token",
          "CloudAccountToken": "old-acct-token",
          "CloudRefreshToken": "old-refresh-token",
          "CloudEmail": "haroon@gmail.com",
          "StationCode": "yaqobi"
        }
        """;
        File.WriteAllText(Path.Combine(_dir, "settings.json"), legacy);

        var read = AppSettings.Load();
        Assert.Equal("old-acct-token", read.CloudAccountToken);
        Assert.Equal("old-refresh-token", read.CloudRefreshToken);
        Assert.Equal("old-device-token", read.CloudDeviceToken);
        Assert.Equal("old-home-token", read.ServerToken);
        //  و بقیهٔ تنظیمات هم سرِ جایشان
        Assert.Equal("gold", read.ThemeId);
        Assert.Equal("yaqobi", read.StationCode);

        //  و با اولین ذخیره، خودشان رمز می‌شوند و کلیدِ خام از فایل می‌رود
        read.Save();
        var raw = FileText();
        Assert.DoesNotContain("old-acct-token", raw);
        Assert.DoesNotContain("old-device-token", raw);
        Assert.Equal("old-acct-token", AppSettings.Load().CloudAccountToken);
    }

    /// <summary>
    /// ⚠️ ترتیبِ کلیدها در فایل نباید مهم باشد: هیچ‌کدام از دو راه
    /// («رمزی» و «کهنهٔ خام») چیزی را <b>خالی</b> نمی‌کند، فقط پر می‌کند.
    /// </summary>
    [Fact]
    public void HarDoKelid_KenareHam_Ham_MoshkeliNist()
    {
        var mixed = $$"""
        {
          "CloudAccountTokenEnc": {{JsonSerializer.Serialize(SecretStore.Protect("new-token"))}},
          "CloudAccountToken": "stale-plain-token"
        }
        """;
        File.WriteAllText(Path.Combine(_dir, "settings.json"), mixed);
        //  رمزی اول آمده و پر کرده، پس خامِ کهنه نادیده گرفته می‌شود
        Assert.Equal("new-token", AppSettings.Load().CloudAccountToken);
    }

    // ── ۳) خودِ صندوق ─────────────────────────────────────────────────────

    [Fact]
    public void SecretStore_RaftOBargasht_Doroste()
    {
        var sealed_ = SecretStore.Protect("hello-token");
        Assert.True(SecretStore.IsProtected(sealed_));
        Assert.DoesNotContain("hello-token", sealed_);
        Assert.Equal("hello-token", SecretStore.Unprotect(sealed_));

        //  دوباره رمز کردنِ چیزی که رمز است، آن را دولایه نمی‌کند
        Assert.Equal(sealed_, SecretStore.Protect(sealed_));
        //  و مقدارِ خامِ کهنه همان‌طور که هست برمی‌گردد
        Assert.Equal("plain-old", SecretStore.Unprotect("plain-old"));
    }

    /// <summary>
    /// دست‌خوردگی ⇒ خالی، نه یک رشتهٔ بی‌معنا. برنامه باید دوباره ورود
    /// بخواهد، نه این‌که با زبالهٔ رمزنشده به سرور بزند.
    /// </summary>
    [Fact]
    public void DastKhorde_Khali_Barmigardad()
    {
        var sealed_ = SecretStore.Protect("hello-token");
        var body = Convert.FromBase64String(sealed_["enc:v1:".Length..]);
        body[^1] ^= 0xFF;                       // یک بایت خراب
        var broken = "enc:v1:" + Convert.ToBase64String(body);
        Assert.Equal("", SecretStore.Unprotect(broken));
        Assert.Equal("", SecretStore.Unprotect("enc:v1:نه-base64"));
    }
}
