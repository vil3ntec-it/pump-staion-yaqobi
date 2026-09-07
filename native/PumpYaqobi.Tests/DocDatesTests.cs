using PumpYaqobi.Application.Localization;
using PumpYaqobi.Reporting.Pdf;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// پانویسِ تاریخِ سندها — رونوشتِ ‎allDates()‎ نسخهٔ وب.
/// </summary>
public class DocDatesTests
{
    /// <summary>۶ سپتامبر ۲۰۲۶ = ۱۵ سنبلهٔ ۱۴۰۵ = ۲۴ ربیع‌الاولِ ۱۴۴۸.</summary>
    [Fact]
    public void Line_HasTheThreeCalendars()
    {
        var d = new DateTime(2026, 9, 6);

        Assert.Equal("1405/06/15", DocDates.Shamsi(d));
        Assert.Equal("2026/09/06", DocDates.Miladi(d));

        // ⚠️ روزِ قمری عمداً «حدودی» سنجیده می‌شود: جدولِ ام‌القُریِ دات‌نت و
        // جدولِ ICUِ مرورگر گاهی یک روز فرق دارند و بستنِ آزمون روی یک عددِ
        // دقیق، روزی بی هیچ باگی سرخ می‌شد. سال و ماه باید مو‌به‌مو درست
        // باشند — آن‌ها هرگز فرق نمی‌کنند.
        var qa = DocDates.Qamari(d).Split('/');
        Assert.Equal("1448", qa[0]);
        Assert.Equal("03", qa[1]);
        Assert.InRange(int.Parse(qa[2]), 23, 25);

        Assert.Equal(DocDates.Shamsi(d) + "  ·  " + DocDates.Qamari(d) + "  ·  " + DocDates.Miladi(d),
                     DocDates.Line(d));
    }

    /// <summary>
    /// بیرونِ بازهٔ تقویمِ ام‌القُری «—» می‌آید، نه خطا: ورق باید چاپ شود حتی
    /// اگر تاریخِ قمری‌اش در دسترس نباشد — همان کاری که نسخهٔ وب با ‎null‎
    /// می‌کرد.
    /// </summary>
    [Fact]
    public void Qamari_DoesNotThrowOutsideItsRange()
    {
        Assert.Equal("—", DocDates.Qamari(new DateTime(1500, 1, 1)));
        Assert.Contains("—", DocDates.Line(new DateTime(1500, 1, 1)));
    }

    /// <summary>‎_monthLabel('1405/06')‎ → «سنبله 1405».</summary>
    [Theory]
    [InlineData("1405/06", "سنبله 1405")]
    [InlineData("1405/1", "حمل 1405")]
    [InlineData("1404/12", "حوت 1404")]
    public void MonthLabel_MatchesTheWebVersion(string key, string expected) =>
        Assert.Equal(expected, Shamsi.MonthLabel(key));

    /// <summary>کلیدِ خراب همان‌طور که هست برمی‌گردد — عنوانِ سند بی‌چیز نماند.</summary>
    [Fact]
    public void MonthLabel_KeepsAnUnreadableKeyAsIs()
    {
        Assert.Equal("خراب", Shamsi.MonthLabel("خراب"));
        Assert.Equal("", Shamsi.MonthLabel(null));
    }
}
