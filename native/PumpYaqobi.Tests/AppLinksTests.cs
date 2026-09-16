using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ لینکِ اپِ گوشی، کدِ پمپ، کارتِ ورود و بخشِ وی‌آی‌پی ═══════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «لینکِ دانلودِ اپِ اندروید و لینکِ
/// برنامهٔ آیفون را توی یک بخشِ جدید توی تنظیمات بزار که کپی کنم یا به
/// کارفرما تو واتساپ بفرستم… و کدِ برنامه که حساب‌های همان پمپ را نشان
/// می‌دهد توی همان بخشِ جدید هم دیده شود و توی پروفایل هم دیده شود… و بخشِ
/// وی‌آی‌پی را هم اعمال کن که من ببینم و تست کنم… و پروفایل یک بخش بزار
/// بخشِ لاگین با ایمیل با اضافه کردنِ کدِ شش‌رقمی و زدنِ اسم و اسمِ پمپش…
/// و ببین آن عکس باعثِ کند شدنِ اپ یک درصدِ ثانیه هم نشود.»
///
/// این‌ها روی خودِ سورس و روی خودِ توابع می‌گردند — ظاهرِ آوالونیا بی پلتفرم
/// ساخته نمی‌شود، ولی سیم‌کشی‌اش خواندنی است (همان روشِ ‎ProfilePillTests‎).
/// </summary>
public class AppLinksTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string RepoRead(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root, ".." }.Concat(parts).ToArray()));

    // ── ۱) دو لینک ─────────────────────────────────────────────────────────

    /// <summary>
    /// لینکِ اندروید روی دامنهٔ خودِ پمپ است و هیچ رمزی ندارد — کاغذ/واتساپ
    /// دستِ چند نفر می‌گردد.
    /// </summary>
    [Fact]
    public void LinkeAndroid_RoyeDamaneyeKhodePompAst_VaHichRamziNadarad()
    {
        var url = KarLink.ApkUrl();
        Assert.Equal("https://yaqobipump.top/downloads/PumpYaqobiKar.apk", url);
        Assert.DoesNotContain("token", url);
        Assert.DoesNotContain("server", url);

        //  و اگر روزی سایت جای دیگری رفت، خودش دنبالش می‌رود
        Assert.Equal("https://x.example/downloads/PumpYaqobiKar.apk",
                     KarLink.ApkUrl("https://x.example/view/"));
    }

    /// <summary>آیفون فایلِ نصب ندارد — همان صفحهٔ <c>kar/</c>.</summary>
    [Fact]
    public void LinkeIphone_HamanSafheyeKarAst()
    {
        Assert.Equal("https://yaqobipump.top/kar/", KarLink.IphoneUrl());
        Assert.Equal(KarLink.BaseOf(""), KarLink.IphoneUrl());
    }

    /// <summary>
    /// ⛔ فایلِ نصبِ سایتِ قدیم (<c>android-latest</c> ⇒ <c>PumpYaqobi.apk</c>)
    /// هیچ‌وقت به کاربر داده نمی‌شود — قاعدهٔ ۱۴۰۵/۰۶/۲۴.
    /// </summary>
    [Fact]
    public void ApkeSayteGhadim_HichJa_DadeNemishavad()
    {
        //  ⚠️ روی خودِ لینک‌ها می‌سنجیم، نه روی متنِ توضیحات: نامِ انتشارِ
        //  قدیم در کامنتِ «این را ندهید» هست و باید هم باشد.
        Assert.Contains("PumpYaqobiKar.apk", KarLink.ApkUrl());
        Assert.DoesNotContain("android-latest", KarLink.ApkUrl());
        Assert.DoesNotContain("github", KarLink.ApkUrl());

        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AppsSectionViewModel.cs");
        Assert.Contains("KarLink.ApkUrl(", vm);
        //  و هیچ نشانیِ دستی‌ای در خودِ ظاهر نیست (جز فضای‌نامِ آوالونیا)
        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AppsSectionView.axaml")
            .Replace("https://github.com/avaloniaui", "");
        Assert.DoesNotContain("https://", xaml);
    }

    /// <summary>
    /// و همان فایل واقعاً کنارِ سایت گذاشته می‌شود، وگرنه لینک ۴۰۴ می‌داد.
    /// </summary>
    [Fact]
    public void WorkflowSafhe_FaileNasbeKar_RaKenareSayt_Migozarad()
    {
        var yml = RepoRead(".github", "workflows", "deploy-pages.yml");
        Assert.Contains("grab('kar-latest', '.apk', 'PumpYaqobiKar.apk')", yml);
    }

    // ── ۲) پیامِ آمادهٔ واتساپ ─────────────────────────────────────────────

    [Fact]
    public void PayameAmade_HarDoLink_VaKod_RaDarad()
    {
        var text = KarLink.ShareText("k7pm3xq2", "پمپ یعقوبی");
        Assert.Contains(KarLink.ApkUrl(), text);
        Assert.Contains(KarLink.IphoneUrl(), text);
        Assert.Contains("K7PM-3XQ2", text);
        Assert.Contains("پمپ یعقوبی", text);
        //  ⛔ هیچ رمزی
        Assert.DoesNotContain("token", text);

        //  بی کد هم پیام سالم است — فقط خطِ کد ندارد
        var noCode = KarLink.ShareText("", "پمپ یعقوبی");
        Assert.Contains(KarLink.ApkUrl(), noCode);
        Assert.DoesNotContain("کدِ پمپ:", noCode);
    }

    // ── ۳) صفحهٔ تازه در تنظیمات ───────────────────────────────────────────

    [Fact]
    public void SafheyeApp_DarTanzimat_DareChaharomAst()
    {
        var hub = Read("PumpYaqobi.App", "Views", "Sections", "SettingsSectionView.axaml");
        Assert.Contains("CommandParameter=\"{Binding AppsPage}\"", hub);
        //  و بعد از هر سه کارتِ قبلی می‌نشیند
        var trash = hub.IndexOf("{Binding TrashPage}", StringComparison.Ordinal);
        var apps = hub.IndexOf("{Binding AppsPage}", StringComparison.Ordinal);
        Assert.True(trash > 0 && apps > trash, "کارتِ اپِ گوشی باید بعد از سطلِ زباله باشد");

        var view = Read("PumpYaqobi.App", "Views", "Sections", "AppsSectionView.axaml");
        Assert.Contains("CopyAndroidCommand", view);
        Assert.Contains("CopyIphoneCommand", view);
        Assert.Contains("CopyCodeCommand", view);
        Assert.Contains("CopyShareCommand", view);
        //  ⛔ هیچ کادرِ تایپی برای کد و هیچ رمزی
        Assert.DoesNotContain("PasswordChar", view);
        Assert.DoesNotContain("Binding ServerUrl", view);
        Assert.DoesNotContain("Binding SyncCode", view);
    }

    // ── ۴) کارتِ ورودِ پروفایل ─────────────────────────────────────────────

    [Fact]
    public void KarteVorud_Email_KodeShishRaghmi_Nam_VaNamePomp_Darad()
    {
        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        Assert.Contains("Binding LoginEmail", xaml);
        Assert.Contains("Binding LoginCode", xaml);
        Assert.Contains("Binding LoginName", xaml);
        Assert.Contains("Binding LoginPump", xaml);
        Assert.Contains("SubmitLoginCommand", xaml);
        //  ورود با گوگل سرِ جایش ماند — و رمزی تایپ نمی‌شود
        Assert.Contains("SignInCommand", xaml);
        Assert.DoesNotContain("PasswordChar", xaml);

        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        //  کد همان کدِ اشتراک است، پس از همان درِ همیشگی می‌رود
        Assert.Contains("Cloud.RedeemAsync(code)", vm);
        //  نام و نامِ پمپ بی کد هم ذخیره می‌شوند
        Assert.Contains("SettingsService.StationName, pump", vm);
    }

    /// <summary>
    /// ⚠️ «آن عکس یک درصدِ ثانیه هم اپ را کند نکند»: سه قاعده روی سورس قفل
    /// شده — فقط در فعال‌سازیِ همان صفحه، روی نخِ دیگر، و با پهنای نمایش.
    /// </summary>
    [Fact]
    public void Akse_KarteVorud_HichHazineyeShoruAppNadarad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");

        //  (۱) فقط از ‎OnActivatedAsync‎ — یعنی وقتی کاربر واقعاً آمد.
        //  پردهٔ لودینگ فقط ‎EnsureLoadedAsync‎ را می‌زند.
        var calls = vm.Split("LoadArtAsync()").Length - 1;
        Assert.Equal(2, calls);                       // تعریف + یک صدا زدن
        var activate = vm.IndexOf("public override async Task OnActivatedAsync()",
                                  StringComparison.Ordinal);
        Assert.True(activate > 0);
        Assert.Contains("await LoadArtAsync();", vm[activate..]);
        Assert.DoesNotContain("LoadArtAsync", vm[..activate]
            .Split("private async Task LoadArtAsync")[0]);

        //  (۲) روی نخِ دیگر و (۳) به پهنای نمایش، نه اندازهٔ اصلی
        Assert.Contains("await Task.Run(() =>", vm);
        Assert.Contains("Bitmap.DecodeToWidth(s, ArtWidth)", vm);

        //  ⚠️ بریدن **بعد از** ‎await‎ است، یعنی روی نخِ رابط: ‎CroppedBitmap‎
        //  یک ‎AvaloniaObject‎ است و روی نخِ دیگر «Call from invalid thread»
        //  می‌دهد (همین باگ یک بار عکس را کاملاً ناپدید کرد).
        var body = vm.Split("await Task.Run(() =>")[1].Split("});")[0];
        Assert.DoesNotContain("CroppedBitmap", body);
        Assert.Contains("new CroppedBitmap(full, box)", vm);
        //  و یک بار برای همیشه
        Assert.Contains("private static IImage? _art;", vm);

        //  خودِ فایلِ عکس هم کوچک است — ۱۵۰ کیلوبایت سقفِ خودمان
        var art = new FileInfo(Path.Combine(Root, "PumpYaqobi.App", "Assets", "login-art.jpg"));
        Assert.True(art.Exists, "عکسِ کارتِ ورود نیست");
        Assert.True(art.Length < 150 * 1024, $"عکس بزرگ است: {art.Length} بایت");
    }

    /// <summary>
    /// ⚠️ «آن نوشته‌های عکس هم نباشد» — خواستهٔ صریحِ صاحب ریپو. عکسِ اصلی یک
    /// کارتِ تبلیغاتی است («Gas Station / Learn More»)؛ فقط پنجرهٔ خودِ پمپ
    /// نشان داده می‌شود و فرم **روی همان عکس** می‌نشیند.
    /// </summary>
    [Fact]
    public void Akse_FaghatKhodePompAst_VaFarmRoyeHamanAks()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        //  پنجرهٔ بریدن هست و نوشته‌های سمتِ راستِ عکس (از ۴۳۲ به بعد) را نمی‌گیرد
        Assert.Contains("ArtCrop = new(56, 42, 376, 412)", vm);

        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        //  فرم یک پنلِ شناور **داخلِ همان قاب** است، نه ستونی کنارِ عکس
        var grid = xaml.Split("Classes=\"logincard\"")[1].Split("<!-- ══ آواتار")[0];
        var img = grid.IndexOf("<Image Source=\"{Binding LoginArt}\"", StringComparison.Ordinal);
        var form = grid.IndexOf("Background=\"{DynamicResource Pump.Card}\"", StringComparison.Ordinal);
        Assert.True(img > 0 && form > img, "پنلِ فرم باید بعد از عکس بیاید تا رویش کشیده شود");
        //  و اندازهٔ قاب صریح است، وگرنه فرم تمامِ عکس را می‌پوشاند (یک بار شد)
        Assert.Contains("<Grid Width=\"880\" Height=\"552\">", grid);
        //  کادرها گِردند، نه مربعی
        Assert.Contains("Selector=\"Border.logincard TextBox\"", xaml);
        Assert.Contains("CornerRadius\" Value=\"14\"", xaml);
    }

    // ── ۵) بخشِ وی‌آی‌پی ───────────────────────────────────────────────────

    [Fact]
    public void BakhsheVip_HaleEshterak_VaPlanha_RaMigoyad()
    {
        var view = Read("PumpYaqobi.App", "Views", "Sections", "VipSectionView.axaml");
        Assert.Contains("Binding KarText", view);
        Assert.Contains("Binding QrText", view);
        Assert.Contains("Binding BackupText", view);
        Assert.Contains("Binding SupportText", view);
        Assert.Contains("ToggleTestCommand", view);

        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "VipSectionViewModel.cs");
        //  چهار پلن، و قیمتِ هر پلنِ پولی خالی می‌ماند
        Assert.Contains("\"آزمایشی\"", vm);
        Assert.Contains("\"پایه\", \"—\"", vm);
        Assert.Contains("\"VIP\", \"—\"", vm);
        Assert.Contains("\"مالکیت (یک‌بار)\", \"—\"", vm);
        //  هیچ تصمیمی این‌جا گرفته نمی‌شود — فقط ‎Entitlements‎ خوانده می‌شود
        Assert.Contains("Entitlements.State(file)", vm);
    }

    /// <summary>
    /// کلیدِ آزمایش <b>فقط می‌بندد</b> — پس راهِ دور زدنِ اشتراک نیست.
    /// </summary>
    [Fact]
    public void KelideAzmayesh_FaghatMibandad_HichVaghtBazNemikonad()
    {
        try
        {
            //  بی اشتراک: با آزمایش هم بسته می‌ماند (باز نمی‌کند)
            var none = Entitlements.State(new AppSettings());
            Assert.False(none.Allows(Entitlements.Kar));
            Entitlements.TestDeny = true;
            Assert.False(none.Allows(Entitlements.Kar));

            //  و پشتیبانی با آزمایش هم باز است — «یکی از واجبات است»
            Assert.True(none.Allows(Entitlements.Support));
            Assert.True(Entitlements.Allows(Entitlements.Support));
        }
        finally { Entitlements.TestDeny = false; }
    }
}
