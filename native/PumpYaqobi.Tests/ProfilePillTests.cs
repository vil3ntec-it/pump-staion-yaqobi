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
        //  ورود با گوگل و اشتراک همان‌جا ماندند
        Assert.Contains("SignInCommand", xaml);
        Assert.Contains("RedeemSubCommand", xaml);
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
}
