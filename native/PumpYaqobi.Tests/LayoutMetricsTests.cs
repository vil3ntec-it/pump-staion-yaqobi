using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پهنای ستونِ محتوا ═══════════════════════════════════════════════════════
///
/// سایت یک ستونِ وسط‌چینِ ۱۰۰۰ پیکسلی دارد
/// (‎.main{padding:16px;max-width:1000px;margin:0 auto}‎) و فقط فهرستِ
/// ‎_WIDE‎ از آن بیرون می‌زند (‎body.sec-wide .main{max-width:none}‎).
///
/// این آزمون دو بار عوض شده و هر بار چون از حافظه ساخته شده بود:
///   • اول «همه ۱۰۰۰، چند تا پهن» — با فهرستِ حدسی.
///   • بعد «همه پهن، بی استثنا» — که وارونهٔ کارِ سایت بود.
/// حالا از خودِ ‎index.html‎ اندازه گرفته شده. اگر روزی عوضش کردید، اول
/// ‎_WIDE‎ی سایت را بخوانید.
/// </summary>
public class LayoutMetricsTests
{
    private sealed class Fake : SectionViewModel
    {
        public Fake(string id) : base(id, id, id) { }
        public void Open(bool v) => IsPageOpen = v;
    }

    /// <summary>دوازده‌تای ‎_WIDE‎ — آن‌هایی که در نیتیو همتا دارند.</summary>
    [Theory]
    [InlineData("dashboard")]
    [InlineData("rasid")]
    [InlineData("debtrasid")]
    [InlineData("chakana")]
    [InlineData("oldloans")]
    [InlineData("invrate")]
    public void WideSectionsFillTheWindow(string id)
        => Assert.Equal(double.PositiveInfinity, new Fake(id).ContentMaxWidth);

    /// <summary>بقیه در همان ستونِ ۱۰۰۰ پیکسلی می‌مانند — پارچه‌ها هم.</summary>
    [Theory]
    [InlineData("shifts")]
    [InlineData("waraq")]
    [InlineData("debt")]
    [InlineData("storage")]
    [InlineData("amanat")]
    [InlineData("safe")]
    [InlineData("sarrafi")]
    [InlineData("expenses")]
    [InlineData("attendance")]
    [InlineData("noinv")]
    [InlineData("profit")]
    [InlineData("invoices")]
    [InlineData("settings")]
    [InlineData("history")]
    [InlineData("cameras")]
    public void EverySectionElseKeepsTheThousandPixelColumn(string id)
        => Assert.Equal(1000, new Fake(id).ContentMaxWidth);

    /// <summary>
    /// ⚠️ ولی صفحهٔ درونیِ باز (حسابِ شخص، شرکت، ورق، امانت) تمام‌عرض می‌شود —
    /// در سایت هم آن‌ها ‎modal-overlay‎ی تمام‌پنجره‌اند، نه محتوای ‎.main‎.
    /// </summary>
    [Fact]
    public void AnOpenAccountPageFillsTheWindow()
    {
        var s = new Fake("debt");
        Assert.Equal(1000, s.ContentMaxWidth);
        s.Open(true);
        Assert.Equal(double.PositiveInfinity, s.ContentMaxWidth);
    }
}
