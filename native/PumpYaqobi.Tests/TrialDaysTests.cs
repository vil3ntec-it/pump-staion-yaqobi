using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «حساب ساختم و کد را زدم، ولی ۳۰ روزِ آزمایشی را ندادند» (۱۴۰۵/۰۷/۱۳) ══
///
/// با سرورهای واقعی سنجیده شد (سنجهٔ ‎signuptrial‎: همان راهِ کاربر از خودِ
/// صفحهٔ ورود ⇒ کدِ ایمیل ⇒ نامِ پمپ):
///
///   سرورِ حساب ۲.۱۰.۰   ⇒ دورهٔ آزمایشی ۳۰ روز — پنل «۳۰ روز مانده»، برنامه «۲۹»
///   سرورِ حساب ۲.۷.۰    ⇒ فقط ۱۴ روز (پیش‌فرضِ قدیمیِ خودِ سرور)، برنامه «۱۳»
///   ۲.۷.۰ ⇒ ۲.۱۰.۰ روی همان داده ⇒ همان پمپ ۳۰ روزِ کامل را گرفت
///
/// یعنی کمبودِ روزها کارِ **سرورِ کهنه** بود، نه برنامه. ولی دو چیزِ برنامه هم
/// کاربر را گمراه می‌کرد و این‌جا قفل شده‌اند.
/// </summary>
public class TrialDaysTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    /// <summary>
    /// ⛔ روزِ مانده رو به بالا شمرده می‌شود، مثلِ خودِ سرور
    /// (<c>daysLeft = Math.ceil(...)</c>). رو به پایین بود و دورهٔ سی‌روزه
    /// همان لحظهٔ ساختنِ پمپ «۲۹ روز» خوانده می‌شد.
    /// </summary>
    [Fact]
    public void RoozeMande_RooBeBala_MesleSarvar()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        var at = vm.IndexOf("VipDays = check.Valid", StringComparison.Ordinal);
        Assert.True(at > 0, "محاسبهٔ روزِ مانده پیدا نشد");
        var line = vm[at..vm.IndexOf(';', at)];
        Assert.Contains("Math.Ceiling", line);
        Assert.DoesNotContain("86_400_000L)", line);

        //  همان قاعده‌ای که بقیهٔ برنامه از قبل داشت
        Assert.Contains("Math.Ceiling((st.EntitledUntil - st.NowMs) / 86_400_000d)",
            Read("PumpYaqobi.App", "ViewModels", "Sections", "VipSectionViewModel.cs"));
    }

    /// <summary>
    /// سی روزِ دقیق (همان ثانیه‌ای که پمپ ساخته شد) باید «۳۰» باشد، و یک ساعت
    /// مانده «۱»، نه «۰». همان فرمولِ پروفایل، جدا سنجیده.
    /// </summary>
    [Theory]
    [InlineData(30 * 86_400_000L, 30)]
    [InlineData(30 * 86_400_000L - 60_000L, 30)]
    [InlineData(3_600_000L, 1)]
    [InlineData(0L, 0)]
    public void Formool_ZarbeRooz(long leftMs, int days)
    {
        const long now = 1_790_000_000_000L;
        var ends = now + leftMs;
        var got = ends > now ? (int)Math.Ceiling((ends - now) / 86_400_000d) : 0;
        Assert.Equal(days, got);
    }

    /// <summary>
    /// ⛔ «کدِ شش‌رقمی را بزنید» دیگر راهِ فعال شدن نیست — و کاربر را دنبالِ
    /// کدی می‌فرستاد که ندارد. این کامپیوتر با <b>ورود به حساب و نامِ پمپ</b>
    /// خودش بند می‌شود و دورهٔ آزمایشی همان‌جا می‌آید.
    /// </summary>
    [Fact]
    public void HichJomleyi_BeKodeShishRaghmi_Nemifrestad()
    {
        var files = new[]
        {
            Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs"),
            Read("PumpYaqobi.App", "ViewModels", "Sections", "VipSectionViewModel.cs"),
            Read("PumpYaqobi.App", "Services", "AcctLive.cs"),
            Read("PumpYaqobi.App", "Services", "CloudLink.cs"),
        };
        foreach (var src in files)
        {
            Assert.DoesNotContain("\"هنوز فعال نشده — کدِ شش‌رقمیِ اشتراک را بزنید.\"", src);
            Assert.DoesNotContain("\"فعال — با کدِ شش‌رقمی\"", src);
            Assert.DoesNotContain(": \"کدِ شش‌رقمی را بزنید\"", src);
            Assert.DoesNotContain("کدِ شش‌رقمی را در «پروفایل» بزنید", src);
            Assert.DoesNotContain("کدِ شش‌رقمیِ همین پمپ را بزنید", src);
            Assert.DoesNotContain("تا برنامه با کدِ شش‌رقمیِ اشتراک فعال نشود", src);
            Assert.DoesNotContain("دوباره با کدِ شش‌رقمیِ همان پمپ فعال کنید", src);
        }
        //  و راهِ درست گفته می‌شود
        Assert.Contains("از «حساب و ورود» وارد شوید و نامِ پمپ را بزنید",
            Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs"));
        //  ⚠️ خرج کردنِ کدِ اشتراک برداشته نشد — فقط دیگر راهِ اصلی خوانده نمی‌شود
        Assert.Contains("RedeemSubCommand",
            Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml"));
    }
}
