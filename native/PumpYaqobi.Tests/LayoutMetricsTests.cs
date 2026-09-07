using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پهنای ستونِ محتوا ═══════════════════════════════════════════════════════
///
/// تا دیروز این‌جا ستونِ ۱۰۰۰ پیکسلیِ وسط‌چینِ ‎.main‎ِ نسخهٔ وب تقلید می‌شد و
/// فقط چند بخشِ جدولی تمام‌عرض بودند. صاحب ریپو با عکس نشان داد که نتیجه‌اش
/// روی لپ‌تاپ چه شد: «چپ و راستِ هر بخش را ببینی تمام صفحه نیستن» — دو نوارِ
/// خالی کنارِ هر بخش.
///
/// حالا قاعده یکی است و استثنا ندارد: <b>هر بخش تمامِ عرضِ پنجره</b>. این
/// آزمون همان را قفل می‌کند تا با یک دستکاریِ بعدی ستونِ باریک برنگردد.
/// </summary>
public class LayoutMetricsTests
{
    private sealed class Fake : SectionViewModel
    {
        public Fake(string id) : base(id, id, id) { }
    }

    [Theory]
    [InlineData("dashboard")]
    [InlineData("shifts")]
    [InlineData("debt")]
    [InlineData("waraq")]
    [InlineData("storage")]
    [InlineData("amanat")]
    [InlineData("safe")]
    [InlineData("profit")]
    [InlineData("rasid")]
    [InlineData("debtrasid")]
    [InlineData("chakana")]
    [InlineData("oldloans")]
    [InlineData("invrate")]
    [InlineData("settings")]
    public void EverySectionFillsTheWindow(string id)
        => Assert.Equal(double.PositiveInfinity, new Fake(id).ContentMaxWidth);

    /// <summary>باز شدنِ حسابِ درونِ بخش هم چیزی را تنگ‌تر نمی‌کند.</summary>
    [Fact]
    public void AnOpenAccountPageAlsoFillsTheWindow()
    {
        var s = new Fake("debt") { IsPageOpen = true };
        Assert.Equal(double.PositiveInfinity, s.ContentMaxWidth);
    }

    /// <summary>
    /// عوض شدنِ پهنا باید خبر بدهد. حالا عدد ثابت است، ولی خبرش باید بماند —
    /// پوستهٔ برنامه به همین بند است و اگر روزی قاعده برگردد، بی‌خبر می‌شکند.
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
