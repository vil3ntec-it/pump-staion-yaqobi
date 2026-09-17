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

    /// <summary>
    /// ══ دو گامِ ثبت‌نام، همان‌طور که صاحب ریپو شمرد ══════════════════════
    ///
    /// «اول اسم، ایمیل، رمز، تکرارِ رمز؛ بعد برود بخشِ بعدی: کدِ شش‌رقمی و
    /// تاییدِ آن از سرور و اسمِ پمپ و لوکیشنِ پمپ. همین و بعد هم تمام.»
    /// </summary>
    [Fact]
    public void SabtName_SeGam_Ast_VaKodeEmail_Darad()
    {
        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");

        //  گامِ ۱ — چهار کادرِ خواسته‌شده، و رمز **واقعاً** رمز است
        Assert.Contains("Binding LoginName", xaml);
        Assert.Contains("Binding LoginEmail", xaml);
        Assert.Contains("Binding LoginPassword}", xaml);
        Assert.Contains("Binding LoginPassword2", xaml);
        Assert.Contains("PasswordChar=\"•\"", xaml);
        //  «حساب داری یا نه» — دو راهِ دیدنی
        Assert.Contains("Content=\"حساب می‌سازم\"", xaml);
        Assert.Contains("Content=\"حساب دارم\"", xaml);
        Assert.Contains("SetSignUpCommand", xaml);
        Assert.Contains("AccountStepCommand", xaml);

        //  گامِ ۲ — کد و تاییدش از سرور، نامِ پمپ و لوکیشن
        Assert.Contains("Binding LoginCode", xaml);
        Assert.Contains("Binding LoginPump", xaml);
        Assert.Contains("Binding LoginLocation", xaml);
        Assert.Contains("VerifyCodeCommand", xaml);
        //  و گامِ سوم «تمام» است
        Assert.Contains("Binding StepDone", xaml);

        //  ⛔ و هیچ راهِ سرویسِ بیرونی‌ای نیست (خواستهٔ صاحب ریپو: «هیچ
        //  پکنه‌ای نباشد، نه از گوگل و نه غیره»)
        Assert.DoesNotContain("SignInCommand", xaml);

        //  ⚠️ گامِ «کدِ ایمیل» — سرور حسابِ بی تأییدِ ایمیل نمی‌سازد
        Assert.Contains("Binding EmailCode", xaml);
        Assert.Contains("VerifyEmailCommand", xaml);
        Assert.Contains("ResendEmailCodeCommand", xaml);
        //  و پذیرشِ شرایط، که خودِ سرور اجباری‌اش کرده
        Assert.Contains("Binding AcceptTerms", xaml);
        Assert.Contains("LoadTermsCommand", xaml);

        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        //  حساب از راهِ سه‌پلهٔ خودِ سرور ساخته می‌شود
        Assert.Contains("Cloud.RegisterStartAsync(name, email, pass)", vm);
        Assert.Contains("Cloud.RegisterVerifyAsync((LoginEmail ?? \"\").Trim(), code)", vm);
        Assert.Contains("Cloud.RegisterCompleteAsync((LoginName ?? \"\").Trim(), pass, AcceptTerms)", vm);
        Assert.Contains("Cloud.SignInWithPasswordAsync(email, pass)", vm);
        //  چهار گام: حساب · کدِ ایمیل · پمپ · تمام
        Assert.Contains("public bool StepEmailCode => LoginStep == 2;", vm);
        Assert.Contains("public bool StepPump => LoginStep == 3;", vm);
        Assert.Contains("public bool StepDone => LoginStep == 4;", vm);
        //  کد همان کدِ اشتراک است و نام و لوکیشنِ پمپ همراهش می‌روند
        Assert.Contains("Cloud.RedeemAsync(code, pump, where)", vm);
        Assert.Contains("SettingsService.StationName, pump", vm);
        Assert.Contains("SettingsService.StationAddress, where", vm);
    }

    /// <summary>
    /// ⛔ **رمز هیچ‌جا روی این کامپیوتر نمی‌نشیند** — نه خام، نه هش. فقط به
    /// ابر می‌رود و توکنِ نشست برمی‌گردد، و بعد از هر گام پاک می‌شود.
    /// </summary>
    [Fact]
    public void RamzeAbr_RoyeDisk_ZakhireNemishavad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        //  هیچ‌جا رمز در تنظیمات نوشته نمی‌شود
        Assert.DoesNotContain("CloudPassword", vm);
        Assert.DoesNotContain("f.Password", vm);
        //  و در هر دو مسیر پاک می‌شود
        //  سه جا رمز را از حافظهٔ صفحه پاک می‌کند: ورود، تاییدِ کدِ ایمیل،
        //  و «بعداً». (پلهٔ یکِ ثبت‌نام عمداً پاکش نمی‌کند، چون پلهٔ سوم
        //  همان رمز را می‌خواهد.)
        Assert.Equal(3, vm.Split("LoginPassword = \"\"; LoginPassword2 = \"\";").Length - 1);

        var settings = Read("PumpYaqobi.App", "Services", "AppSettings.cs");
        Assert.DoesNotContain("Password", settings);

        var link = Read("PumpYaqobi.App", "Services", "CloudLink.cs");
        //  رمز فقط در بدنهٔ همان دو درخواست است
        Assert.Contains("\"/api/auth/login\"", link);
        Assert.DoesNotContain("_settings.CloudPassword", link);
        //  و اگر سرور این راه را نداشت، «رمز غلط» نمی‌گوید
        Assert.Contains("no_route", link);
    }

    /// <summary>
    /// ⚠️ **آدمک‌های عکسِ مرجع در صفحه‌اند** — خواستهٔ صریحِ صاحب ریپو
    /// (۱۴۰۵/۰۶/۲۸): «همین مدل باشد و این آدمک‌ها هم باشند؛ اصلاً همین عکس
    /// باید باشد.» و همان سه قاعدهٔ «یک درصدِ ثانیه هم اپ را کند نکند» سرِ
    /// جایش است: فقط در فعال‌سازیِ همان صفحه، روی نخِ دیگر، به پهنای نمایش،
    /// و یک بار برای همیشه.
    /// </summary>
    [Fact]
    public void Akse_Adamakha_Hast_Va_HichHazineyeShoruAppNadarad()
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

        //  (۴) ⚠️ **«فوری»**: عکس پیش از ردیف‌های تب‌ها خوانده می‌شود.
        //  خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۹) «آن عکسِ آدم‌ها را فوری کن». سنجهٔ
        //  `loginart` عددش را داد: خودِ عکس ۳ تا ۴ میلی‌ثانیه است و هیچ
        //  دستورِ دیتابیسی نمی‌زند، ولی `LoadRowsAsync` سه پرس‌وجو دارد.
        var act = vm[activate..];
        var iArt = act.IndexOf("await LoadArtAsync();", StringComparison.Ordinal);
        var iRows = act.IndexOf("await LoadRowsAsync();", StringComparison.Ordinal);
        Assert.True(iArt > 0 && iRows > iArt,
                    "عکس باید پیش از ردیف‌های تب‌ها خوانده شود، نه پشتِ سه پرس‌وجو.");

        //  خودِ فایلِ عکس هم کوچک است — ۱۵۰ کیلوبایت سقفِ خودمان
        var art = new FileInfo(Path.Combine(Root, "PumpYaqobi.App", "Assets", "login-art.jpg"));
        Assert.True(art.Exists, "عکسِ صفحهٔ ورود نیست");
        Assert.True(art.Length < 150 * 1024, $"عکس بزرگ است: {art.Length} بایت");

        //  و عکس واقعاً در صفحه کشیده می‌شود
        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        Assert.Contains("<Image Source=\"{Binding LoginArt}\"", xaml);
    }

    /// <summary>
    /// ⛔ **از عکس فقط آدمک‌ها بریده می‌شوند** — نه نوشته‌های انگلیسی‌اش و نه
    /// آن دو نشانِ «App Store / Google Play»: «هیچ پکنه‌ای نباشد، نه از گوگل
    /// و نه غیره.» فرم، فرمِ خودِ برنامه است، نه فرمِ داخلِ عکس.
    /// </summary>
    [Fact]
    public void Akse_FaghatAdamakha_Ast_VaHichPaknehyi_Nadarad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        Assert.Contains("ArtCrop = new(52, 86, 368, 396)", vm);

        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        //  ⚠️ دنبالِ **خودِ صفحه** می‌گردیم، نه توضیح‌های بالای فایل: همان
        //  توضیح که می‌گوید «هیچ دکمهٔ گوگلی نیست» خودش واژه را دارد.
        var body = System.Text.RegularExpressions.Regex.Replace(
            xaml, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        foreach (var bad in new[] { "گوگل", "Google", "جیمیل", "Apple", "Facebook", "App Store" })
            Assert.DoesNotContain(bad, body);
        Assert.DoesNotContain("SignInCommand", body);

        //  ویومدل هم دیگر آن فرمان را ندارد
        Assert.DoesNotContain("private Task SignInAsync()", vm);
        Assert.DoesNotContain("GoogleSignIn.RunAsync", vm);

        //  ⚠️ صفحهٔ ورود **تمامِ صفحه** است، نه یک کارتِ کوچک (خواستهٔ
        //  ۱۴۰۵/۰۶/۲۹: «کلِ صفحه را بگیرد… این‌جوری کوچک نباشد»)؛ فرم بغلِ
        //  عکس است، با پهنای صریحِ خودش.
        Assert.Contains("<Grid ColumnDefinitions=\"*,440\">", xaml);
        Assert.DoesNotContain("Grid Width=\"980\"", xaml);
        //  و راهِ برگشت برای کسی که نمی‌خواهد ثبت‌نام کند
        Assert.Contains("CloseLoginCommand", xaml);
    }

    /// <summary>
    /// ⚠️ **صفحهٔ ورود اولویت دارد و تمامِ صفحه است** — «وقتی پروفایل را کلیک
    /// می‌کنم این صفحهٔ لاگین اولویت باشد و تمامِ صفحه همین را نشان بدهد برای
    /// کسانی که حساب ندارند، و برای کسانی که دارند پروفایل همان مشخصات را
    /// نشان بدهد.» پس دو صفحه روی هم‌اند و هر لحظه فقط یکی دیده می‌شود.
    /// </summary>
    [Fact]
    public void SafheyeVorud_Olaviat_Darad_Va_TamameSafhe_Ast()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        Assert.Contains("public bool ShowLoginPage => LoginStep < 4;", vm);
        Assert.Contains("public bool ShowProfilePage => !ShowLoginPage;", vm);
        //  «بعداً»ی گامِ دو هم هست، وگرنه صفحهٔ ورود یک دیوار می‌شد
        Assert.Contains("private void SkipPump()", vm);
        Assert.Contains("private void OpenAccountPage()", vm);

        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        Assert.Contains("IsVisible=\"{Binding ShowLoginPage}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowProfilePage}\"", xaml);
        Assert.Contains("SkipPumpCommand", xaml);
        Assert.Contains("OpenAccountPageCommand", xaml);

        //  ⚠️ هر دو داخلِ یک ‎Panel‎ اند و با ‎IsVisible‎ جا عوض می‌کنند، پس
        //  هیچ‌کدام کارِ دیگری را انجام نمی‌دهد.
        Assert.Contains("<Panel>", xaml);
    }

    /// <summary>
    /// ⚠️ **«تمام صفحه» یعنی فقط همین صفحه — نه سربرگ، نه نوارِ جمله‌ها، نه
    /// نوارِ بخش‌ها.** جملهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «تمام صفحه منظورم
    /// فقط همین را نشان بده در صفحه، نه بخش‌ها باشند نه غیره.»
    ///
    /// دو چیز این را می‌سازند و هر دو این‌جا قفل‌اند:
    /// ۱) صفحهٔ ورود **بیرونِ** <c>SectionPage</c> است، پس نه سربرگِ بخش دارد
    ///    نه کارتِ بخش؛
    /// ۲) <c>IsPageOpen</c> با خودِ <c>ShowLoginPage</c> یکی می‌شود، و پوستهٔ
    ///    پنجره از همان می‌فهمد که پنهان شود
    ///    (<c>MainViewModel.IsChromeVisible</c>).
    /// </summary>
    [Fact]
    public void SafheyeVorud_TamameSafhe_Ast_VaPostePanjere_PenhanMishavad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        //  دو جا: یکی با عوض شدنِ گام، یکی صریح در ‎ShowLogin‎ (گامِ یک
        //  مقدارِ پیش‌فرض است و خبری نمی‌دهد)
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(vm, @"IsPageOpen = ShowLoginPage;").Count);

        var main = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("IsChromeVisible => Content?.IsPageOpen != true", main);

        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        //  ریشهٔ فایل ‎Panel‎ است، و ‎SectionPage‎ فقط دورِ خودِ پروفایل
        var root = xaml.IndexOf("<Panel>", System.StringComparison.Ordinal);
        var section = xaml.IndexOf("<c:SectionPage", System.StringComparison.Ordinal);
        var login = xaml.IndexOf("Name=\"LoginPage\"", System.StringComparison.Ordinal);
        Assert.True(root > 0 && login > root, "صفحهٔ ورود باید داخلِ همان ‎Panel‎ی ریشه باشد.");
        Assert.True(section > login, "‎SectionPage‎ باید **پس از** صفحهٔ ورود و فقط دورِ پروفایل باشد.");
        Assert.Contains("<c:SectionPage Header=\"پروفایل\" IsVisible=\"{Binding ShowProfilePage}\">", xaml);

        //  و بلندی از خودِ پنجره می‌آید (صفحه‌ها داخلِ ‎StackPanel‎اند و
        //  بلندیِ خودکار دارند)، بی هیچ شنوندهٔ ‎LayoutUpdated‎ی
        var code = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml.cs");
        Assert.Contains("TopLevel.ClientSizeProperty", code);
        Assert.DoesNotContain("LayoutUpdated", code);
    }

    /// <summary>
    /// ⚠️ **رنگ‌بندی همان قالبی است که صاحب ریپو داد — و فقط در صفحهٔ ورود.**
    /// جملهٔ خودش: «من رنگ‌های همان سایت را گفتم بگیر، نه که شبیه آن بسازی…
    /// و رنگ را گفتم فقط توی صفحهٔ لاگینِ حساب باشد، نه جای دیگر.»
    /// </summary>
    [Fact]
    public void RangBandi_FaghatDarSafheyeVorud_Ast()
    {
        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        var palette = new[]
        {
            "#f6e3d8",                          // بوم
            "#ff7a59", "#e8458b", "#7b3fe4",    // گرادیان
            "#2b1640",                          // نوشتهٔ تیره و دکمهٔ اصلی
            "#4a3b52", "#7a6a82",               // متن و کم‌رنگ
            "#c2185b",                          // لینک
        };
        foreach (var hex in palette) Assert.Contains(hex, xaml);

        //  کارتِ سفید با گوشهٔ ۲۸ و همان سایهٔ بنفش، و دکمهٔ اصلی با گوشهٔ ۴۰
        Assert.Contains("CornerRadius=\"28\"", xaml);
        Assert.Contains("0 20 50 0 #2E7B3FE4", xaml);

        //  ⛔ و هیچ‌کدام در **صفحهٔ پروفایل** نیست: آن‌جا تمِ خودِ برنامه است
        var profile = xaml[xaml.IndexOf("👤 خودِ پروفایل", StringComparison.Ordinal)..];
        foreach (var hex in palette)
            Assert.DoesNotContain(hex, profile);
        //  پروفایل همچنان از تمِ برنامه رنگ می‌گیرد
        Assert.Contains("{DynamicResource Pump.Muted}", xaml);

        //  ⛔ و به تم‌های برنامه هم نرفته‌اند
        var theme = Read("PumpYaqobi.App", "Themes", "PumpTheme.cs");
        Assert.DoesNotContain("f6e3d8", theme);
        Assert.DoesNotContain("7b3fe4", theme);
    }

    /// <summary>
    /// ⛔ **ذخیرهٔ تنظیماتِ ابر باید همان شیئی باشد که عوض شده.**
    ///
    /// باگی که سنجهٔ `cloudlogin` گرفت: پاسخِ ذخیرهٔ `CloudLink` یک
    /// `AppSettings.Load()`ِ **تازه** بود، پس توکنِ حساب و توکنِ دستگاه و
    /// مجوز که روی شیءِ خودِ `CloudLink` نشسته بودند، با نوشتنِ همان شیءِ
    /// تازه **دور ریخته می‌شدند** — ثبت‌نام و فعال‌سازی روی دیسک نمی‌نشست و
    /// با هر بار باز شدنِ برنامه کاربر باید دوباره کد می‌زد.
    /// </summary>
    [Fact]
    public void TanzimateAbr_HamanShey_Zakhire_Mishavad()
    {
        foreach (var f in new[]
        {
            Path.Combine("ViewModels", "Sections", "AccountSectionViewModel.cs"),
            Path.Combine("ViewModels", "Sections", "ChatSectionViewModel.cs"),
            Path.Combine("Services", "StationPublisher.cs"),
        })
        {
            var src = Read("PumpYaqobi.App", f);
            Assert.DoesNotContain("AppSettings.Load().Save()", src);
            if (src.Contains("new CloudLink("))
                Assert.Contains("file.Save()", src);
        }
    }

    /// <summary>
    /// ⛔ **میانبری که نویسه را بخورد، یعنی «برنامه چیزی نمی‌نویسد».**
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «این کادرِ ایمیل ایمیل نیست و هیچی توش
    /// نوشته نمی‌شود؛ + @ # ﷼ ( ) ؟ ؛ : , . توی هیچ‌کدام نوشته نمی‌شوند.»
    /// ریشه: `Shift+عدد` (میانبرِ حذفِ ردیف) کلید را `Handled` می‌کرد و روی
    /// ویندوز کلیدِ خورده‌شده `WM_CHAR` نمی‌سازد — یعنی ردیفِ عددها با Shift
    /// هیچ نویسه‌ای تایپ نمی‌کرد: انگلیسی `@ # $ % ( )` و فارسی/دری
    /// `، ؛ ؟ ﷼ ٪ × ) (`.
    /// </summary>
    [Fact]
    public void ShiftAdad_DakheleKadreTypAn_MianborNist()
    {
        var src = Read("PumpYaqobi.App", "Services", "Shortcuts.cs");
        Assert.Contains("private static bool TypingInBox(object? sender)", src);
        //  فقط شاخهٔ Shift این قید را می‌خواهد — Ctrl و Alt نویسه نمی‌سازند
        Assert.Contains("if (shift && !alt && !TypingInBox(sender))", src);
        //  و میانبرها برداشته نشده‌اند
        Assert.Contains("if (ctrl && !alt)", src);
        Assert.Contains("if (alt && !ctrl)", src);
    }

    /// <summary>
    /// ⛔ **قفلِ اشتراک روی «عکسِ زنده» است، نه روی خودِ اتصالِ خانگی.**
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «سرور روشن است اما پمپ بنزین می‌گوید
    /// خاموش است.» ریشه: برنامهٔ بی‌اشتراک در نخستین خطِ `PublishOnceAsync`
    /// برمی‌گشت، پس هیچ‌وقت به سرورِ خانگی وصل نمی‌شد و چراغِ سربرگ «جواب
    /// نمی‌دهد» می‌گفت در حالی که سرور روشن بود.
    /// </summary>
    [Fact]
    public void CheraghSarvar_HaghighatRaMigooyad()
    {
        var src = Read("PumpYaqobi.App", "Services", "StationPublisher.cs");
        //  اتصال جدا از انتشار است و حلقه هر دو را می‌زند
        Assert.Contains("public async Task<bool> KeepLinkAsync(", src);
        Assert.Contains("await KeepLinkAsync(false, ct)", src);
        //  و اتصال زودتر از بیست ثانیه سنجیده می‌شود
        Assert.Contains("LinkTick = TimeSpan.FromSeconds(5)", src);
        //  ⚠️ ولی قفلِ اشتراک روی خودِ انتشار سرِ جایش است
        Assert.Contains("Entitlements.Allows(Entitlements.Kar)", src);
        //  و قطعیِ ناگهانی زود دوباره می‌گردد، نه پنج دقیقه بعد
        Assert.Contains("EnrollRetryLost = TimeSpan.FromSeconds(30)", src);

        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        //  چراغ دلیل و آخرین وصل را می‌گوید، و کلیک همان لحظه می‌گردد
        Assert.Contains("private async Task CheckServerAsync()", vm);
        Assert.Contains("KeepLinkAsync(force: true)", vm);
        Assert.Contains("آخرین وصل", vm);
        //  ⛔ و هیچ نام/نشانیِ سروری در چراغ نوشته نمی‌شود (قاعدهٔ ۱۴۰۵/۰۶/۲۶)
        var dot = vm.Split("public void TickServerDot()")[1].Split("[RelayCommand]")[0];
        Assert.DoesNotContain("Url", dot);

        var xaml = Read("PumpYaqobi.App", "Views", "MainWindow.axaml");
        Assert.Contains("CheckServerCommand", xaml);
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
