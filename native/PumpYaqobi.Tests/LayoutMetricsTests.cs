using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پهنای ستونِ محتوا ═══════════════════════════════════════════════════════
///
/// ⚠️ این قاعده را **صاحب ریپو** تعیین کرده، نه سایت:
///     «فقط و فقط بخشِ پارچه‌ها و بخشِ فاکتورها تمام صفحه نباشند؛
///      بقیهٔ همهٔ بخش‌ها تمام صفحه باشند.»
///
/// این خانه چند بار عوض شده؛ پیش از دست زدن به آن، همین جمله را بخوانید.
/// </summary>
public class LayoutMetricsTests
{
    private sealed class Fake : SectionViewModel
    {
        public Fake(string id) : base(id, id, id) { }
        public void Open(bool v) => IsPageOpen = v;
    }

    /// <summary>دو بخشِ فرمی — ستونِ باریک.</summary>
    [Theory]
    [InlineData("shifts")]
    [InlineData("invoices")]
    public void OnlyParchaAndInvoicesAreNarrow(string id)
        => Assert.Equal(1000, new Fake(id).ContentMaxWidth);

    /// <summary>بقیه — بی استثنا تمام‌عرض.</summary>
    [Theory]
    [InlineData("dashboard")]
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
    [InlineData("settings")]
    [InlineData("history")]
    [InlineData("cameras")]
    [InlineData("rasid")]
    [InlineData("debtrasid")]
    [InlineData("chakana")]
    [InlineData("oldloans")]
    [InlineData("invrate")]
    public void EverySectionElseFillsTheWindow(string id)
        => Assert.Equal(double.PositiveInfinity, new Fake(id).ContentMaxWidth);

    /// <summary>صفحهٔ درونیِ باز، حتی در آن دو بخش، تمام‌عرض می‌شود.</summary>
    [Fact]
    public void AnOpenPageFillsTheWindowEvenInANarrowSection()
    {
        var s = new Fake("invoices");
        Assert.Equal(1000, s.ContentMaxWidth);
        s.Open(true);
        Assert.Equal(double.PositiveInfinity, s.ContentMaxWidth);
    }
}
