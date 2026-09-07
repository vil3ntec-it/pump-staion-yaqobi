using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پهنای ستونِ محتوا ═══════════════════════════════════════════════════════
/// در نسخهٔ وب ‎.main‎ عرضش ۱۰۰۰ پیکسل است و وسط می‌ایستد. فقط دوازده بخشِ
/// جدولی کلاسِ ‎sec-wide‎ می‌گیرند، داشبورد تا ۱۴۴۰ کش می‌آید، و مودالِ حساب
/// (شخص/شرکت/ورق) صریحاً تمام‌صفحه است.
///
/// صاحب ریپو همین ستونِ وسط‌چین را «خیلی بهتر» خواند و خواست عینِ نسخهٔ وب
/// باشد. این آزمون همان قاعده را قفل می‌کند تا با یک دستکاریِ بعدی برنگردد.
/// </summary>
public class LayoutMetricsTests
{
    private sealed class Fake : SectionViewModel
    {
        public Fake(string id) : base(id, id, id) { }
    }

    [Theory]
    // همان فهرستِ ‎_WIDE‎ نسخهٔ وب
    [InlineData("rasid")]
    [InlineData("debtrasid")]
    [InlineData("chakana")]
    [InlineData("oldloans")]
    [InlineData("oldloansmoney")]
    [InlineData("debtsum")]
    [InlineData("debtsummoney")]
    [InlineData("priceloss")]
    [InlineData("plsource")]
    [InlineData("plperson")]
    [InlineData("invrate")]
    public void TheWideTableSectionsFillTheWindow(string id)
        => Assert.Equal(double.PositiveInfinity, new Fake(id).ContentMaxWidth);

    /// <summary>داشبورد تمام‌عرض است ولی تا ۱۴۴۰ — وگرنه کارت‌هایش کش می‌آیند.</summary>
    [Fact]
    public void TheDashboardIsWideButCappedAt1440()
        => Assert.Equal(1440, new Fake("dashboard").ContentMaxWidth);

    [Theory]
    [InlineData("shifts")]
    [InlineData("debt")]
    [InlineData("waraq")]
    [InlineData("storage")]
    [InlineData("amanat")]
    [InlineData("safe")]
    [InlineData("profit")]
    public void EveryOtherSectionKeepsTheCentredThousandPixelColumn(string id)
        => Assert.Equal(1000, new Fake(id).ContentMaxWidth);

    /// <summary>
    /// با باز شدنِ حسابِ درونِ بخش، ستون تمام‌عرض می‌شود — جدولِ حسابِ شخص
    /// ده‌ها ستون دارد و در ۱۰۰۰ پیکسل نصفش بیرون می‌ماند.
    /// </summary>
    [Fact]
    public void AnOpenAccountPageFillsTheWindow()
    {
        var s = new Fake("debt");
        Assert.Equal(1000, s.ContentMaxWidth);

        s.IsPageOpen = true;
        Assert.Equal(double.PositiveInfinity, s.ContentMaxWidth);

        s.IsPageOpen = false;
        Assert.Equal(1000, s.ContentMaxWidth);
    }

    /// <summary>
    /// عوض شدنِ پهنا باید خبر بدهد، وگرنه پنجره همان پهنای قبلی را نگه می‌دارد
    /// و باز کردنِ حساب هیچ اثری ندارد.
    /// </summary>
    [Fact]
    public void ChangingTheOpenPageRaisesTheWidthChange()
    {
        var s = new Fake("debt");
        var seen = new List<string?>();
        s.PropertyChanged += (_, e) => seen.Add(e.PropertyName);

        s.IsPageOpen = true;

        Assert.Contains(nameof(SectionViewModel.ContentMaxWidth), seen);
    }
}
