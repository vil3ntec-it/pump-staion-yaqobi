using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ سه چیزی که با اشتراک کار می‌کنند ══════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «کیو‌آر، اپِ کارمندان و ربات، و
/// بک‌اپِ ابری بی اشتراک کار نکنند… پشتیبانی باشد چون یکی از واجبات است…
/// و نمی‌خواهم کسی که اشتراک خریده با یک باگ خراب شود.»
///
/// این آزمون هر چهار جملهٔ بالا را جدا می‌سنجد، با مجوزِ **واقعیِ امضاشده**
/// (همان جفت‌کلیدِ P-256ی سرور) نه با پرچمِ دست‌ساز.
/// </summary>
public class EntitlementsTests : IDisposable
{
    private const string Device = "pc-entitle-1";
    private const string Station = "stn_entitle_1";

    private readonly long _now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public EntitlementsTests() => Entitlements.Now = () => _now;
    public void Dispose() =>
        Entitlements.Now = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // ── ساختنِ مجوز، دقیقاً همان‌طور که سرور می‌سازد ────────────────────

    private static (ECDsa key, string publicB64) NewServer()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
    }

    private static string Sign(ECDsa key, object payload)
    {
        static string B64(byte[] b) =>
            Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var header = B64(Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"TLIC"}"""));
        var body = B64(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var sig = key.SignData(Encoding.UTF8.GetBytes($"{header}.{body}"),
                               HashAlgorithmName.SHA256,
                               DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{body}.{B64(sig)}";
    }

    /// <summary>یک برنامهٔ فعال‌شده با مجوزی که <paramref name="feat"/> را دارد.</summary>
    private AppSettings Activated(string[]? feat, long expOffsetMs = 10L * 24 * 3600 * 1000,
                                  long subOffsetMs = 30L * 24 * 3600 * 1000)
    {
        var (key, pub) = NewServer();
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = "tohid-license-server",
            ["aud"] = "tohid-pump-app",
            ["duid"] = Device,
            ["stn"] = Station,
            ["iat"] = _now,
            ["nbf"] = _now - 60_000,
            ["exp"] = _now + expOffsetMs,
            ["sub_ends"] = _now + subOffsetMs,
            ["core"] = new[] { "dashboard", "settings", "debtors" },
            ["plan_title"] = "آزمون",
        };
        if (feat is not null) payload["feat"] = feat;

        return new AppSettings
        {
            CloudDeviceUid = Device,
            CloudDeviceToken = "dev-token",
            CloudStationId = Station,
            CloudPublicKey = pub,
            CloudLicense = Sign(key, payload),
        };
    }

    // ── ۱) پشتیبانی هیچ‌وقت قفل نمی‌شود ────────────────────────────────

    [Fact]
    public void SupportIsNeverLocked()
    {
        //  نه با اشتراکِ فعال، نه بی آن، نه با برنامهٔ فعال‌نشده.
        Assert.True(Entitlements.State(new AppSettings()).Allows(Entitlements.Support));
        Assert.True(Entitlements.State(Activated(Array.Empty<string>()))
                                .Allows(Entitlements.Support));
        Assert.True(Entitlements.Allows(Entitlements.Support));
    }

    // ── ۲) بی اشتراک، هر سه بسته‌اند ───────────────────────────────────

    [Fact]
    public void WithoutAnySubscriptionTheThreeCloudThingsAreClosed()
    {
        var st = Entitlements.State(new AppSettings());

        Assert.True(st.NotActivated);
        foreach (var f in Entitlements.Paid) Assert.False(st.Allows(f));

        //  و جمله‌اش به کاربر می‌گوید چه کند، نه این‌که ساکت بماند
        Assert.Contains("اشتراک", st.Why(Entitlements.TitleOf(Entitlements.Kar)));
    }

    // ── ۳) پلنِ پایه: فهرستِ خالی یعنی «هیچ‌کدام» ───────────────────────

    [Fact]
    public void TheBasicPlanSendsAnEmptyListAndGetsNoneOfThem()
    {
        var st = Entitlements.State(Activated(Array.Empty<string>()));

        Assert.True(st.Open);                       // اشتراک هست
        foreach (var f in Entitlements.Paid) Assert.False(st.Allows(f));
    }

    [Fact]
    public void AVipPlanOpensExactlyWhatItNames()
    {
        var st = Entitlements.State(Activated(new[] { Entitlements.Kar, Entitlements.QrLive }));

        Assert.True(st.Allows(Entitlements.Kar));
        Assert.True(st.Allows(Entitlements.QrLive));
        Assert.False(st.Allows(Entitlements.CloudBackup));
    }

    // ── ۳ب) نامِ قابلیت روی سرور ──────────────────────────────────────────

    /// <summary>
    /// ⭐ <b>باگی که نزدیک بود همهٔ مشتری‌های پولی را قفل کند.</b>
    ///
    /// کاتالوگِ پمپ روی سرور (‎lib/features.js‎ در ریپوی ‎shop‎) این نام‌ها
    /// را دارد: <c>kar_app</c> · <c>bot</c> · <c>cloud</c> — نه
    /// <c>kar</c>/<c>qrlive</c>/<c>cloudbackup</c>ی که این برنامه می‌پرسد.
    ///
    /// پس روزی که سرور فهرستِ واقعیِ پلن را بفرستد، یک مشتریِ <b>وی‌آی‌پی</b>
    /// هر سه را <b>قفل</b> می‌دید. این سنجه دقیقاً همان فهرستی را می‌دهد که
    /// سرورِ امروز می‌سازد.
    /// </summary>
    [Fact]
    public void NamhayeSarvar_HamanSeKar_RaBazMikonand()
    {
        //  همان چیزی که سرور برای پلنِ کاملِ پمپ می‌فرستد
        var st = Entitlements.State(Activated(new[]
        {
            "safe", "sarrafi", "chakana", "invoice", "storage", "staff",
            "companies", "amanat", "kar_app", "bot", "cloud", "multi_device",
        }));

        Assert.True(st.Open);
        Assert.True(st.Allows(Entitlements.Kar));          // ⇐ kar_app
        Assert.True(st.Allows(Entitlements.QrLive));       // ⇐ cloud
        Assert.True(st.Allows(Entitlements.CloudBackup));  // ⇐ cloud
    }

    /// <summary>
    /// ⛔ و هم‌معنی‌ها <b>چیزی را باز نمی‌کنند که نباید</b>: پلنی که فقط
    /// اپِ کارمندان را دارد، پوشهٔ ابری را ندارد.
    /// </summary>
    [Fact]
    public void FaghatKarApp_AbrRaBazNemikonad()
    {
        var st = Entitlements.State(Activated(new[] { "kar_app" }));

        Assert.True(st.Allows(Entitlements.Kar));
        Assert.False(st.Allows(Entitlements.QrLive));
        Assert.False(st.Allows(Entitlements.CloudBackup));
    }

    /// <summary>
    /// ⚠️ مجوزِ نسلِ اول ‎feat‎ نداشت. نبودنِ فهرست یعنی «پلنِ کامل»، نه
    /// «هیچ‌چیز» — وگرنه با همین یک تغییر، همهٔ مشتری‌های امروز صبح خاموش
    /// می‌شدند.
    /// </summary>
    [Fact]
    public void AnOldLicenseWithNoFeatureListIsTreatedAsTheFullPlan()
    {
        var st = Entitlements.State(Activated(null));
        foreach (var f in Entitlements.Paid) Assert.True(st.Allows(f));
    }

    // ── ۴) «با یک باگ خراب نشود» ───────────────────────────────────────

    /// <summary>
    /// مجوز منقضی شده (برنامه دو هفته آفلاین بوده) ⇒ هنوز باز است. این همان
    /// خواستهٔ صریحِ صاحب ریپو است.
    /// </summary>
    [Fact]
    public void AnExpiredLicenseStillWorksInsideTheGrace()
    {
        var f = Activated(new[] { Entitlements.Kar }, expOffsetMs: -3600_000, subOffsetMs: -3600_000);
        //  یک ساعت پیش تمام شده و همان لحظه به چشمِ خودمان دیده بودیم
        f.EntitledUntil = _now - 3600_000;

        var st = Entitlements.State(f);
        Assert.False(st.Open);
        Assert.True(st.InGrace);
        Assert.True(st.GraceDaysLeft is >= 13 and <= 14);
        foreach (var x in Entitlements.Paid) Assert.True(st.Allows(x));
    }

    [Fact]
    public void AfterTheGraceIsOverItReallyCloses()
    {
        //  ⚠️ ده دقیقه پیش، نه یک میلی‌ثانیه: ‎LicenseGuard‎ یک دقیقه ارفاقِ
        //  ساعت دارد، پس «۱ میلی‌ثانیه پیش» هنوز معتبر است (آزمون همین را گرفت).
        var f = Activated(new[] { Entitlements.Kar }, expOffsetMs: -600_000, subOffsetMs: -600_000);
        f.EntitledUntil = _now - (long)Entitlements.Grace.TotalMilliseconds - 1;

        var st = Entitlements.State(f);
        Assert.False(st.InGrace);
        foreach (var x in Entitlements.Paid) Assert.False(st.Allows(x));
        Assert.Contains("تمام شده", st.Why("کیو‌آر"));
    }

    /// <summary>
    /// سرور «فعال» گفته ولی مجوزِ تازه نرسیده (باگِ صدورِ مجوز، یا پاسخِ
    /// نیمه) ⇒ باز می‌ماند. لایهٔ اولِ همان سه‌لایه.
    /// </summary>
    [Fact]
    public void WhenTheServerSaysActiveTheMissingLicenseDoesNotCloseAnything()
    {
        var f = new AppSettings { CloudDeviceUid = Device, CloudDeviceToken = "dev-token" };
        var live = new PumpSubscription(true, "cloud", "VIP", 30, _now + 30L * 24 * 3600 * 1000,
                                       new[] { Entitlements.QrLive });

        var st = Entitlements.State(f, live);
        Assert.True(st.Open);
        Assert.True(st.Allows(Entitlements.QrLive));
        Assert.False(st.Allows(Entitlements.Kar));       // پلن همین یکی را داده
    }

    // ── ۵) مُهرِ ارفاق فقط جلو می‌رود ──────────────────────────────────

    [Fact]
    public void TheRememberedStampNeverMovesBackwards()
    {
        var f = new AppSettings { EntitledUntil = _now + 1_000_000, EntitledPlan = "VIP" };

        //  پاسخِ نیمهٔ سرور («فعال نیست») نباید مُهر را عقب ببرد
        Entitlements.Remember(f, PumpSubscription.None, null);
        Assert.Equal(_now + 1_000_000, f.EntitledUntil);

        //  ولی اشتراکِ تازه جلو می‌بردش
        Entitlements.Remember(f, new PumpSubscription(
            true, "cloud", "VIP", 60, _now + 9_000_000, Array.Empty<string>()), null);
        Assert.Equal(_now + 9_000_000, f.EntitledUntil);
    }
}
