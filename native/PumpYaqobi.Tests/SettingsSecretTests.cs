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
//  ⚠️ `AppSettings.DirOverride` **استاتیک** است و xUnit کلاس‌ها را موازی
//  می‌دواند؛ بی این نشان، این کلاس و هر کلاسِ دیگری که همان را عوض
//  می‌کند روی هم می‌نویسند و آزمون‌ها **گاهی** سرخ می‌شوند.
[Collection(AppHostCollection.Name)]
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

/// <summary>
/// ══ فایلِ تنظیمات: یا کاملِ تازه، یا کاملِ کهنه — هیچ‌وقت نیمه ══════════════
///
/// خواستهٔ صریحِ صاحب ریپو (بندهای ۱۲ و ۱۹): «برنامه ناگهانی بسته شده ·
/// کامپیوتر Restart شده · بستنِ برنامه هنگامِ Request · دو درخواستِ Sync
/// هم‌زمان… در هیچ‌کدام برنامه نباید Crash یا داده‌ای را از دست بدهد.»
///
/// ⛔ **باگی که این‌ها گرفتند**: `File.WriteAllText` فایل را اول **خالی**
/// می‌کند و بعد می‌نویسد. مردنِ برنامه وسطِ همان لحظه یعنی `settings.json`ِ
/// نصفه، و `Load` یک تنظیماتِ **خالی** برمی‌گرداند — یعنی رفتنِ توکنِ
/// دستگاه، رفتنِ قفلِ ضدِ کرک (TOFU)، و برگشتنِ کدِ پمپ به `pump1`ِ
/// پیش‌فرض (پس نوشتن روی پوشهٔ **اشتباهِ** سرور).
/// </summary>
//  ⚠️ `AppSettings.DirOverride` **استاتیک** است و xUnit کلاس‌ها را موازی
//  می‌دواند؛ بی این نشان، این کلاس و هر کلاسِ دیگری که همان را عوض
//  می‌کند روی هم می‌نویسند و آزمون‌ها **گاهی** سرخ می‌شوند.
[Collection(AppHostCollection.Name)]
public class SettingsDurabilityTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    public SettingsDurabilityTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-dur-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private string Path_ => Path.Combine(_dir, "settings.json");

    /// <summary>⭐ نیمه‌ماندنِ فایل، دیگر اشتراکِ کاربر را نمی‌برد.</summary>
    [Fact]
    public void FayleNimeMande_Az_NosekheyeSalem_Khande_Mishavad()
    {
        //  یک ذخیرهٔ سالم، بعد یکی دیگر تا `.bak` هم ساخته شود
        new AppSettings
        {
            CloudDeviceToken = "dev-token", CloudPublicKey = "PIN-TOFU",
            StationCode = "yaqobi", ThemeId = "gold",
        }.Save();
        AppSettings.Load().Save();

        //  «برنامه وسطِ نوشتن بسته شد» — فایلِ اصلی نصفه
        var good = File.ReadAllText(Path_);
        File.WriteAllText(Path_, good[..(good.Length / 2)]);

        var after = AppSettings.Load();
        Assert.Equal("dev-token", after.CloudDeviceToken);   // اشتراک سرِ جایش
        Assert.Equal("PIN-TOFU", after.CloudPublicKey);      // قفلِ ضدِ کرک سرِ جایش
        Assert.Equal("yaqobi", after.StationCode);           // نه `pump1`ِ پیش‌فرض
        Assert.Equal("gold", after.ThemeId);
    }

    /// <summary>فایلِ خالی هم همان‌طور.</summary>
    [Fact]
    public void FayleKhali_Ham_Az_Bak_Khande_Mishavad()
    {
        new AppSettings { CloudDeviceToken = "dev-token", StationCode = "yaqobi" }.Save();
        AppSettings.Load().Save();
        File.WriteAllText(Path_, "");

        Assert.Equal("dev-token", AppSettings.Load().CloudDeviceToken);
    }

    /// <summary>
    /// ⚠️ سه نخ هم‌زمان این فایل را می‌نویسند (رابط، `StationPublisher`ِ
    /// بیست‌ثانیه‌ای، و `BackupPusher`). پیش از این، از ۴۰ ذخیرهٔ هم‌زمان
    /// **۱۸ تا** خراب یا خالی خوانده می‌شدند.
    /// </summary>
    [Fact]
    public async Task ChehelZakhireyeHamzaman_HichKhandane_Kharabi_Nemidahad()
    {
        new AppSettings { CloudDeviceToken = "dev-token" }.Save();

        var bad = 0;
        await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Task.Run(() =>
        {
            var f = AppSettings.Load();
            f.StationCode = "s" + i;
            f.Save();
            if (AppSettings.Load().CloudDeviceToken != "dev-token") Interlocked.Increment(ref bad);
        })));

        //  ⚠️ «۳۸ تا» به تنهایی هیچ چیزی نمی‌گوید. اگر باز هم سرخ شد، لاگِ
        //  CI باید بگوید روی دیسک چه مانده — وگرنه سیزنِ بعدی هم حدس می‌زند.
        var onDisk = File.Exists(Path_) ? File.ReadAllText(Path_) : "«فایل نیست»";
        Assert.True(bad == 0,
            $"{bad} خواندن از ۴۰ تا توکن را ندید.\n"
            + $"آخرین خواندن: «{AppSettings.Load().CloudDeviceToken}»\n"
            + $"روی دیسک: {onDisk[..Math.Min(400, onDisk.Length)]}");
        //  و هیچ فایلِ موقتی جا نمانده
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    /// <summary>
    /// ⭐ **یک رمزگشاییِ ناموفق، رازِ سالمِ روی دیسک را پاک نمی‌کند.**
    ///
    /// باگی که ساختِ ویندوزِ CI گرفت (اجرای #128): بلوکی که باز نمی‌شد،
    /// توکن را در حافظه خالی می‌کرد و ذخیرهٔ بعدی همان خالی را روی دیسک
    /// می‌نوشت — یعنی **یک** لغزشِ گذرا، اشتراکِ کاربر را برای همیشه
    /// می‌برد و کدِ شش‌رقمی دوباره خواسته می‌شد.
    /// </summary>
    [Fact]
    public void BolokeBazNashode_PakNemishavad()
    {
        new AppSettings { CloudDeviceToken = "dev-token", StationCode = "yaqobi" }.Save();

        //  «انگار کلید عوض شده» — یک بایتِ بلوک را خراب می‌کنیم
        //  ⚠️ با `text.Replace` نه: `System.Text.Json` نویسهٔ `+`ِ base64 را
        //  `\u002B` می‌نویسد، پس رشتهٔ خوانده‌شده در متنِ فایل پیدا نمی‌شود
        //  و آزمون بی‌صدا هیچ‌کاری نمی‌کرد (خودش گرفتش).
        var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(Path_))!;
        var blob = doc["CloudDeviceTokenEnc"].GetString()!;
        var body = Convert.FromBase64String(blob["enc:v1:".Length..]);
        body[^1] ^= 0xFF;
        var broken = "enc:v1:" + Convert.ToBase64String(body);
        doc["CloudDeviceTokenEnc"] = JsonSerializer.SerializeToElement(broken);
        File.WriteAllText(Path_, JsonSerializer.Serialize(doc));

        var f = AppSettings.Load();
        //  در حافظه «توکن ندارم» — پس برنامه ورود می‌خواهد و با زبالهٔ
        //  رمزنشده به سرور نمی‌زند
        Assert.Equal("", f.CloudDeviceToken);
        Assert.Equal("yaqobi", f.StationCode);

        //  ⛔ ولی ذخیرهٔ بعدی بلوک را پاک نمی‌کند
        f.Save();
        Assert.Equal(broken, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(Path_))!["CloudDeviceTokenEnc"].GetString());

        //  و رازِ تازه که آمد، بلوکِ کهنه دور انداخته می‌شود
        f.CloudDeviceToken = "token-taze";
        f.Save();
        Assert.NotEqual(broken, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(Path_))!["CloudDeviceTokenEnc"].GetString());
        Assert.Equal("token-taze", AppSettings.Load().CloudDeviceToken);
    }

    /// <summary>
    /// ⭐ **فایلِ قفل‌شده، پیش‌فرض‌ها را روی دفتر نمی‌نویسد.**
    ///
    /// روی ویندوز ضدِ ویروس و نمایه‌سازِ سیستم یک لحظه دستهٔ فایل را نگه
    /// می‌دارند. تا امروز همان یک لحظه یعنی `Load`ی که تنظیماتِ **خالی**
    /// می‌داد و `Save`ِ بعدی آن را روی دادهٔ واقعی می‌نوشت: کدِ پمپ به
    /// `pump1` برمی‌گشت — یعنی نوشتن روی پوشهٔ **اشتباهِ** سرور.
    /// </summary>
    [Fact]
    public void FayleGhoflShode_PishFarzRa_Nemineviseh()
    {
        new AppSettings
        {
            CloudDeviceToken = "dev-token", CloudPublicKey = "PIN-TOFU", StationCode = "yaqobi",
        }.Save();

        AppSettings blind;
        //  دستهٔ انحصاری — دقیقاً همان کاری که ضدِ ویروس یک لحظه می‌کند
        using (new FileStream(Path_, FileMode.Open, FileAccess.Read, FileShare.None))
            blind = AppSettings.Load();

        //  برنامه باز می‌شود (استثنا نمی‌دهد) ولی چیزی هم نمی‌داند
        Assert.Equal("", blind.CloudDeviceToken);

        //  ⛔ و مهم‌تر: نوشتنش هیچ کاری نمی‌کند
        blind.StationCode = "pump1";
        blind.Save();

        var real = AppSettings.Load();
        Assert.Equal("dev-token", real.CloudDeviceToken);
        Assert.Equal("PIN-TOFU", real.CloudPublicKey);
        Assert.Equal("yaqobi", real.StationCode);

        //  و نمونهٔ سالم مثلِ همیشه می‌نویسد
        real.StationCode = "yaqobi-2";
        real.Save();
        Assert.Equal("yaqobi-2", AppSettings.Load().StationCode);
    }

    /// <summary>
    /// ⭐ **نوشتنِ در صف در پوشهٔ کسِ دیگری نمی‌نشیند.**
    ///
    /// <c>SaveSoon</c> ششصد میلی‌ثانیه بعد می‌نویسد. اگر تا آن لحظه
    /// <see cref="AppSettings.DirOverride"/> عوض شده باشد، آن نوشتن در دفترِ
    /// پوشهٔ <b>تازه</b> می‌نشست — و یک بار همین شد: توکنِ یک کلاسِ آزمون در
    /// دفترِ کلاسِ دیگری پیدا شد و CI سرخ شد، ولی فقط در یکی از دو اجرا.
    ///
    /// ⚠️ مقدارِ راحتی (تم، آخرین بخش) گم شدنش اشکالی ندارد؛ نشستنش در دفترِ
    /// اشتباه دارد.
    /// </summary>
    /// <summary>
    /// ⛔ حلقهٔ عکسِ ایستگاهِ آزمونِ <b>قبلی</b> هم نباید در پوشهٔ این یکی
    /// بنویسد. بی این، سنجهٔ پایین گاهی سرخ می‌شد — نه به‌خاطرِ
    /// <c>SaveSoon</c>ِ خودش، به‌خاطرِ <c>LicenseClock.Tick</c>ِ حلقه‌ای که
    /// آزمونِ دیگری راه انداخته و نبسته بود.
    /// </summary>
    [Fact]
    public void HalgheyeNasher_DarAzmoonha_Khamoosh_Ast() =>
        Assert.True(PumpYaqobi.App.Services.StationPublisher.Disabled);

    [Fact]
    public void NevashtaneDarSaf_DarPushehyeDigari_Nemineshinad()
    {
        new AppSettings { CloudDeviceToken = "asli", ThemeId = "blue" }.Save();

        //  یک نوشتنِ در صف، برای همین پوشه
        new AppSettings { CloudDeviceToken = "dar-saf", ThemeId = "gold" }.SaveSoon();

        var digar = Path.Combine(Path.GetTempPath(), "pump-dur2-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(digar);
        try
        {
            //  پوشه عوض شد، پیش از رسیدنِ نوبت
            AppSettings.DirOverride = digar;
            Thread.Sleep(1200);

            //  ⛔ هیچ چیزی در پوشهٔ تازه ننشسته
            Assert.False(File.Exists(Path.Combine(digar, "settings.json")));
            Assert.Equal("", AppSettings.Load().CloudDeviceToken);
        }
        finally
        {
            AppSettings.DirOverride = _dir;
            try { Directory.Delete(digar, true); } catch { }
        }

        //  و دفترِ خودمان هم دست‌نخورده مانده
        Assert.Equal("asli", AppSettings.Load().CloudDeviceToken);
    }

    /// <summary>
    /// ⛔ <b>نوشتنِ در صف، نوشتهٔ کسِ دیگری را پاک نمی‌کند.</b>
    ///
    /// ‎SaveSoon()‎ یک عکسِ کهنه را ششصد میلی‌ثانیه نگه می‌دارد. اگر در همان
    /// فاصله کسِ دیگری چیزی روی دیسک بنویسد، نوشتنِ در صف آن را <b>پاک
    /// می‌کرد</b> — چون کلِ شیء را می‌برد.
    ///
    /// و این فرضی نبود: «‹ برگشت به برنامه» ایمیل و نام و نشانِ
    /// ‎LoginSkipped‎ را می‌نوشت و بعد ‎GoHome()‎ «آخرین بخش» را در صف
    /// می‌گذاشت؛ ششصد میلی‌ثانیه بعد هر سه از بین می‌رفتند و <b>دیوارِ ورود
    /// دوباره برمی‌گشت</b>. بندِ ۱۴ی ‎verify‎ همین را گرفت.
    ///
    /// ⚠️ و ترتیب عمدی است: نوبت <b>بعد</b> از آن ذخیرهٔ بادوام گذاشته
    /// می‌شود — همان‌طور که در خودِ برنامه می‌افتد. اگر پیش از آن باشد،
    /// ‎Save()‎ خودش نوبت را لغو می‌کند و آزمون هیچ چیزی را نمی‌سنجد.
    /// </summary>
    [Fact]
    public void NevashtaneDarSaf_NeveshteyeKaseDigari_RaPakNemikonad()
    {
        //  حالِ کهنه‌ای که کسی در دست دارد (مثلِ ‎MainViewModel._settings‎)
        var kohne = AppSettings.Load();
        kohne.ThemeId = "gold";
        kohne.LastSection = "safe";

        //  یک ذخیرهٔ بادوام از جای دیگر — ایمیل و نشانِ «بی حساب ادامه بده»
        var taze = AppSettings.Load();
        taze.CloudEmail = "test@gmail.com";
        taze.CloudName = "هارون یعقوبی";
        taze.LoginSkipped = true;
        taze.Save();

        //  و حالا نوبتِ در صف — **بعد** از آن ذخیره
        kohne.SaveSoon();
        Thread.Sleep(1200);

        var f = AppSettings.Load();
        //  ⛔ هیچ‌کدام پاک نشدند
        Assert.Equal("test@gmail.com", f.CloudEmail);
        Assert.Equal("هارون یعقوبی", f.CloudName);
        Assert.True(f.LoginSkipped);
        //  …و مقدارِ راحتی هم نشست
        Assert.Equal("gold", f.ThemeId);
        Assert.Equal("safe", f.LastSection);
    }

    /// <summary>هیچ‌وقت استثنا بیرون نمی‌دهد — حتی وقتی پوشه رفته باشد.</summary>
    [Fact]
    public void Zakhire_Hichvaght_Estesna_Partab_Nemikonad()
    {
        Directory.Delete(_dir, true);
        var ex = Record.Exception(() => new AppSettings { ThemeId = "gold" }.Save());
        Assert.Null(ex);
    }
}

/// <summary>
/// ══ «کلاسی که پوشهٔ تنظیمات را عوض می‌کند باید سریال باشد» ══════════════════
///
/// این قاعده از ۱۴۰۵/۰۶/۳۰ در `CLAUDE.md` نوشته شده بود و **همان روز دوباره
/// شکست**: کلاسِ تازهٔ `AccountSwitchTests` بی نشان اضافه شد و
/// `SettingsDurabilityTests` را گاهی سرخ می‌کرد — روی برنچ سبز، روی `main`
/// سرخ.
///
/// یادآوری در یک فایلِ متنی کافی نیست؛ پس از امروز خودِ سورس سنجیده می‌شود.
/// </summary>
public class SettingsCollectionRuleTests
{
    private static string Root
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.Tests")))
                d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("ریشهٔ پروژه پیدا نشد");
        }
    }

    [Fact]
    public void HarKelasi_KePoosheRaAvazMikonad_SerialAst()
    {
        var bad = new List<string>();

        foreach (var file in Directory.GetFiles(Path.Combine(Root, "PumpYaqobi.Tests"), "*.cs"))
        {
            var src = File.ReadAllText(file);
            if (!src.Contains("DirOverride") && !src.Contains("AppHost.Start")) continue;

            //  هر اعلانِ کلاس، با هر چه بالایش نوشته شده
            var m = System.Text.RegularExpressions.Regex.Matches(
                src, @"((?:\[[^\]]*\]\s*)*)public\s+(?:sealed\s+)?class\s+(\w+)");

            for (var i = 0; i < m.Count; i++)
            {
                var attrs = m[i].Groups[1].Value;
                var name = m[i].Groups[2].Value;
                var from = m[i].Index + m[i].Length;
                var to = i + 1 < m.Count ? m[i + 1].Index : src.Length;
                var body = src[from..to];

                //  خودِ این سنجه آن نام‌ها را فقط برای گشتن دارد
                if (name == nameof(SettingsCollectionRuleTests)) continue;
                if (!body.Contains("DirOverride") && !body.Contains("AppHost.Start")) continue;
                if (!attrs.Contains("Collection("))
                    bad.Add($"{Path.GetFileName(file)} ⇒ {name}");
            }
        }

        Assert.True(bad.Count == 0,
            "این کلاس‌ها `AppSettings.DirOverride` را عوض می‌کنند ولی\n"
            + "`[Collection(AppHostCollection.Name)]` ندارند، پس موازی با\n"
            + "کلاس‌های دیگر روی همان متغیرِ استاتیک می‌نویسند:\n  "
            + string.Join("\n  ", bad));
    }
}
