using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «پروفایل» بغلِ تم، و کدِ پمپ برای اپِ کارمندان ═══════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «برنامهٔ نیتیو یک بخشِ جدید بالای
/// صفحه بغلِ تم بگذار به اسمِ پروفایل که آن‌جا هم بتواند لاگین با جیمیل را
/// انجام بدهد و هم VIP و مدتش را ببیند… برای هر پمپ یک کد باشد که هر کسی
/// برنامه را نصب می‌کند باید آن کد را بزند.»
///
/// این‌ها روی خودِ سورس می‌گردند، مثلِ ‎CloudAddressLockTests‎: ظاهرِ سربرگ
/// بی پلتفرمِ آوالونیا ساخته نمی‌شود، ولی سیم‌کشی‌اش قابلِ خواندن است.
/// </summary>
public class ProfilePillTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    [Fact]
    public void DokmeyeProfile_BaghaleTem_Ast()
    {
        var xaml = Read("PumpYaqobi.App", "Views", "MainWindow.axaml");
        var theme = xaml.IndexOf("ItemsSource=\"{Binding Themes}\"", StringComparison.Ordinal);
        var pill = xaml.IndexOf("Classes=\"pill-btn profile\"", StringComparison.Ordinal);
        Assert.True(theme > 0, "کادرِ تم در سربرگ نیست");
        Assert.True(pill > theme, "دکمهٔ پروفایل باید بعد از کادرِ تم بنشیند — «بغلِ تم»");
        //  و پیش از نشانِ نقش، تا واقعاً کنارِ تم باشد نه تهِ ردیف
        var role = xaml.IndexOf("Binding RoleText", StringComparison.Ordinal);
        Assert.True(pill < role, "دکمهٔ پروفایل باید پیش از نشانِ نقش باشد");

        //  به بخشِ حساب می‌رود و همان لحظه VIP و روزهایش را می‌گوید
        Assert.Contains("CommandParameter=\"{Binding Account}\"", xaml);
        Assert.Contains("Binding Account.PillText", xaml);
        Assert.Contains("Binding Account.VipActive", xaml);
    }

    [Fact]
    public void SafheyeProfile_KodePump_Darad_Va_KadreNeshani_Nadarad()
    {
        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        Assert.Contains("Header=\"پروفایل\"", xaml);
        Assert.Contains("AccessCodeDisplay", xaml);
        Assert.Contains("CopyAccessCodeCommand", xaml);
        Assert.Contains("ShowAccessQrCommand", xaml);
        Assert.Contains("RotateAccessCodeCommand", xaml);
        //  اشتراک همان‌جا ماند
        Assert.Contains("RedeemSubCommand", xaml);
        //  ⛔ ورود با گوگل به خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸) از این
        //  صفحه برداشته شد: «هیچ پکنه‌ای نباشد، نه از گوگل و نه غیره.»
        Assert.DoesNotContain("SignInCommand", xaml);
        //  ⛔ کادرِ نشانی همچنان هیچ‌جا نیست
        Assert.DoesNotContain("Binding ServerUrl", xaml);
        Assert.DoesNotContain("Binding SyncCode", xaml);
        Assert.DoesNotContain("api.vill3n.top", xaml);
    }

    [Fact]
    public void KodePump_AzServerMiAyad_NaAzKarbar()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        //  هیچ کادرِ تایپی برای کد نیست: فقط از ‎AccessCodeAsync‎ می‌آید
        Assert.Contains("Cloud.AccessCodeAsync()", vm);
        Assert.Contains("Cloud.AccessCodeAsync(rotate: true)", vm);
        Assert.DoesNotContain("Binding AccessCode}", Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml"));

        var link = Read("PumpYaqobi.App", "Services", "CloudLink.cs");
        Assert.Contains("/api/pump/device/access-code", link);
        Assert.Contains("/api/pump/device/access-code/rotate", link);
    }

    [Fact]
    public void FormatAccessCode_KhatTire_Migozarad()
    {
        Assert.Equal("K7PM-3XQ2", CloudLink.FormatAccessCode("k7pm3xq2"));
        Assert.Equal("ABC", CloudLink.FormatAccessCode("abc"));
        Assert.Equal("", CloudLink.FormatAccessCode(""));
    }

    [Fact]
    public void LinkeKodePump_HichRamziNadarad()
    {
        var url = KarLink.ForCode("k7pm-3xq2");
        Assert.Equal("https://yaqobipump.top/kar/?code=K7PM3XQ2", url);
        Assert.DoesNotContain("token", url);
        Assert.DoesNotContain("server", url);
        //  کنارِ صفحهٔ حساب، اگر جای دیگری باشد
        Assert.Equal("https://x.example/kar/?code=K7PM3XQ2", KarLink.ForCode("K7PM3XQ2", "https://x.example/view/"));
    }

    [Fact]
    public void Tanzimat_KodePump_RaNegahMidarad()
    {
        var s = Read("PumpYaqobi.App", "Services", "AppSettings.cs");
        Assert.Contains("CloudAccessCode", s);
    }

    /// <summary>
    /// ══ هیچ خانه‌ای در پروفایل خالی نمی‌ماند ══════════════════════════════
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «بخشِ پروفایل هنوز درست نشده برایم.»
    /// ریشه‌اش یک بازگشتِ زودهنگام بود: روی پمپی که هنوز با کدِ شش‌رقمی فعال
    /// نشده (یعنی حالِ عادیِ یک نصبِ تازه) ‎ShowSubscription‎ پیش از پر کردنِ
    /// چهار خانهٔ کارتِ اشتراک برمی‌گشت و کارت **خالی** دیده می‌شد.
    ///
    /// این آزمون همان ترتیب را روی سورس قفل می‌کند، و «—»ی مشخصاتِ پمپ را هم.
    /// </summary>
    [Fact]
    public void KarteEshterak_RoyePompeFaalNashode_KhaliNemimanad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");

        var fill = vm.IndexOf("ShowSubDetails(check, file);", StringComparison.Ordinal);
        var bail = vm.IndexOf("SubStatus = \"هنوز فعال نشده", StringComparison.Ordinal);
        Assert.True(fill > 0, "‎ShowSubDetails‎ صدا زده نمی‌شود");
        Assert.True(bail > 0, "حالتِ «فعال نشده» پیدا نشد");
        Assert.True(fill < bail,
            "‎ShowSubDetails‎ باید **پیش از** بازگشتِ «هنوز فعال نشده» بدود، وگرنه "
            + "روی نصبِ تازه چهار خانهٔ کارتِ اشتراک خالی می‌مانند");

        //  تلفن و نشانیِ نداشته «—» می‌شوند، نه هیچ
        Assert.Contains("PumpPhone = Dash(", vm);
        Assert.Contains("PumpAddress = Dash(", vm);
    }

    /// <summary>
    /// ⛔ «پیام‌رسان» و «پروفایل» دو جا نباشند — خواستهٔ صریحِ صاحب ریپو
    /// (۱۴۰۵/۰۶/۲۶): «دو بخش دو جا هستند، آن بالا هستند، این‌جا هم نمی‌خواهد
    /// باشند.» هر دو دکمهٔ سربرگِ خودشان را دارند، پس از نوار برداشته شدند —
    /// ولی خودِ بخش‌ها سرِ جایشان در ‎Sections‎ می‌مانند (‎NavOrderTests‎).
    /// </summary>
    [Fact]
    public void NavarBedoneChatVaProfile_Ast()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        var xaml = Read("PumpYaqobi.App", "Views", "MainWindow.axaml");

        Assert.Contains("public IReadOnlyList<SectionViewModel> NavSections", vm);
        Assert.Contains("s.Id is not (\"chat\" or \"account\")", vm);
        Assert.Contains("ItemsSource=\"{Binding NavSections}\"", xaml);
        Assert.DoesNotContain("<ItemsControl ItemsSource=\"{Binding Sections}\">", xaml);
    }

    /// <summary>
    /// ● چراغِ سرور بغلِ نامِ پمپ — و هیچ نام/نشانیِ سروری کنارش.
    /// خواستهٔ صاحب ریپو: «یک نقطه که سبز یا سرخ شود و دلیلش را بگوید؛ اسم و
    /// آدرسِ سرور داخلش نوشته نباشد.»
    /// </summary>
    [Fact]
    public void CheraghEServer_BaghaleNamePomp_Ast()
    {
        //  ⚠️ از ۱۴۰۵/۰۷/۱۰ **یک** چراغ است، نه دو (خواستهٔ صریحِ صاحب
        //  ریپو). آن‌چه این بند نگه می‌داشت عوض نشده: چراغ درست بعدِ نامِ
        //  پمپ است و دلیلش فقط در ToolTip می‌آید، بی هیچ نشانی.
        var xaml = Read("PumpYaqobi.App", "Views", "MainWindow.axaml");
        var title = xaml.IndexOf("Text=\"پمپ یعقوبی\"", StringComparison.Ordinal);
        var dot = xaml.IndexOf("Binding LinkDotBrushKey", StringComparison.Ordinal);
        Assert.True(title > 0 && dot > title, "چراغ باید درست بعد از نامِ پمپ بیاید");
        Assert.Contains("ToolTip.Tip=\"{Binding LinkDotReason}\"", xaml);

        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("public void TickServerDot()", vm);
        //  دلیل‌ها متن‌اند، نه نشانی: هیچ‌کدام ‎http‎ یا نامِ میزبان ندارند
        var reasons = vm.Split("TickServerDot()")[1].Split("}")[0];
        Assert.DoesNotContain("http", reasons);
        //  ⛔ و همان قاعده روی متنِ چراغِ یکی‌شده هم هست
        var linked = vm.Split("public void TickLinkDot()")[1].Split("[RelayCommand]")[0];
        Assert.DoesNotContain("http", linked);
    }

    /// <summary>
    /// ⛔ نامِ اشتراک همان است که واقعاً هست (۱۴۰۵/۰۷/۱۳، با سرورِ واقعی دیده
    /// شد): دورهٔ آزمایشیِ حسابِ تازه «VIP» نیست و پلنِ استاندارد هم نه. مجوزِ
    /// سرورِ حساب برای اشتراکِ خریده‌شده **کدِ پلن** را می‌فرستد (`vip`)، نه عنوانش.
    /// </summary>
    [Theory]
    [InlineData("دوره‌ی آزمایشی", "آزمایشی")]
    [InlineData("std", "استاندارد")]
    [InlineData("vip", "VIP")]
    [InlineData("VIP", "VIP")]
    [InlineData("perm", "دائمی")]
    [InlineData("", "VIP")]
    public void NameEshterak_Haman_Ast_Ke_Hast(string planTitle, string kind)
    {
        Assert.Equal(kind, PumpYaqobi.App.ViewModels.Sections.AccountSectionViewModel.KindOf(planTitle));
    }

    [Fact]
    public void Sarbarg_Va_Profile_HameyeEshterakhara_VIP_Nemikhanand()
    {
        var main = Read("PumpYaqobi.App", "Views", "MainWindow.axaml");
        Assert.DoesNotContain("<TextBlock Text=\"VIP\"", main);
        var profile = Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");
        Assert.DoesNotContain("💎 اشتراکِ VIP", profile);
        //  و سرورِ حساب هر بار که مجوز عوض شد خبر می‌دهد، نه فقط سرِ باز کردنِ پروفایل
        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("CloudLink.LicenseChanged +=", vm);
    }
}
