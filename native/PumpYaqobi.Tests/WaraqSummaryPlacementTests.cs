using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ جای شش کادرِ «خلاصه شیفت» ══════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «اون شش تا پایینِ این جدولِ تراکنش‌ها استن.» حق داشت —
/// در برنامه بالای صفحه بودند (‎SectionPage.Summary‎) ولی در سایت ته صفحه‌اند:
/// ‎index.html‎ خط ۱۹۹۷۴ عنوانِ ‎#wq-sum-shift-title‎ و شش ‎.stat-box‎ را
/// **بعد از** دو جدولِ تراکنش می‌آورد.
///
/// آزمون متنی است چون چیزی که برگشتنی است همین است: کسی صفحه را دوباره
/// می‌چیند و کادرها بی‌صدا به بالا برمی‌گردند. آزمونِ رفتاری این را نمی‌گیرد،
/// چون جای یک کادرِ نمایشی هیچ محاسبه‌ای را نمی‌شکند.
/// </summary>
public class WaraqSummaryPlacementTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string View() =>
        File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Views", "Sections", "WaraqPageView.axaml"));

    /// <summary>
    /// همان ویو بی کامنت‌ها. ⚠️ برای «این چیز دیگر نباید باشد» حتماً از این
    /// استفاده کنید: کامنت‌های این ریپو نامِ همان چیزِ برداشته‌شده را در
    /// توضیحشان دارند و آزمون با متنِ خام، خودِ توضیح را «هنوز هست» می‌خواند.
    /// </summary>
    private static string Bare() => Regex.Replace(View(), "<!--.*?-->", "", RegexOptions.Singleline);

    /// <summary>کادرها دیگر بالای صفحه نیستند.</summary>
    [Fact]
    public void TheSixBoxesAreNotInThePageHeaderAnyMore()
        => Assert.DoesNotContain("SectionPage.Summary", Bare());

    /// <summary>و پایین‌ترند از جدولِ تراکنش‌ها — همان ترتیبِ سایت.</summary>
    [Fact]
    public void TheSixBoxesComeAfterTheTransactionsTable()
    {
        var x = Bare();
        var txns = x.LastIndexOf("TxnsSecond", StringComparison.Ordinal);
        var summary = x.IndexOf("SummaryTitle", StringComparison.Ordinal);
        Assert.True(txns > 0, "جدولِ دومِ تراکنش‌ها پیدا نشد");
        Assert.True(summary > txns, "کادرهای خلاصه باید بعد از جدولِ تراکنش‌ها بیایند");
    }

    /// <summary>هر شش برچسب، با همان نشانه و همان نامِ سایت.</summary>
    [Theory]
    [InlineData("⛽ جمله بطرول")]
    [InlineData("🟤 جمله دیزل")]
    [InlineData("🟣 جمله مصرف")]
    [InlineData("💳 جمله قرض")]
    [InlineData("📊 جمله فروش")]
    public void EverySiteLabelIsThere(string label) => Assert.Contains(label, Bare());

    /// <summary>
    /// کادرِ ششم برچسبش ثابت نیست — در سایت «⚠️ کمبودی» و «✅ اضافی» می‌شود،
    /// پس این‌جا هم باید از ویومدل بیاید.
    /// </summary>
    [Fact]
    public void TheShortageBoxTakesItsLabelFromTheViewModel()
        => Assert.Contains("{Binding ShortageBoxLabel}", Bare());

    /// <summary>
    /// جدولِ قرائت پمپ‌ها ردیفِ «جمله این شیفت»ِ خودش را دارد و نوار صریحاً
    /// می‌گوید مالِ کدام جدول است — در این صفحه سه جدول هست.
    /// </summary>
    [Fact]
    public void ThePumpTableKeepsItsOwnTotalsRow()
    {
        var x = Bare();
        Assert.Contains("c:TotalsBar", x);
        Assert.Contains("Grid=\"{Binding #PumpGrid}\"", x);
        Assert.Contains("جمله این شیفت", x);
    }
}
