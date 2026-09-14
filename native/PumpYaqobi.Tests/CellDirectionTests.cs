using Avalonia.Media;
using PumpYaqobi.App.Controls;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ جهتِ کادرِ تایپ، و باگی که دو بار برگشت ══════════════════════════════════
///
/// گزارشِ صاحب ریپو، بارِ دوم: «موقعِ تایپ کردن می‌خواهم یک جهت بروم — یا چپ یا
/// راست — آن‌وقت باگ می‌خورد و برعکس می‌رود.»
///
/// کلِ پنجره راست‌به‌چپ است، پس کادرِ تایپ هم همان را به ارث می‌برد و آوالونیا
/// کُرسر را بر اساسِ جهتِ **پایهٔ کادر** می‌بَرد. عددی که چپ‌به‌راست دیده می‌شود
/// در یک کادرِ راست‌به‌چپ، هر کلیدش وارونه حس می‌شود. چارهٔ همان کارِ وب
/// ‎dir="auto"‎ است: جهت از **نخستین حرفِ قویِ** خودِ نوشته بیاید.
///
/// ⚠️ و باگ دقیقاً همین‌جا بود: رقمِ فارسی ‎۰..۹‎ (‎U+06F0..U+06F9‎) و
/// جداکنندهٔ ‎٬‎ (‎U+066C‎) داخلِ بازهٔ «عربی/فارسی» می‌افتند، پس ‎۱۲٬۳۴۵‎
/// «فارسی» شمرده می‌شد و کادر راست‌به‌چپ می‌گشت. با رقمِ لاتین درست بود و
/// همین سال‌ها پنهانش کرد.
///
/// رفتارِ واقعی‌اش را ‎dotnet run --project PumpYaqobi.UiTests -- keys‎ روی
/// پنجرهٔ واقعی می‌سنجد (کُرسر را می‌بَرد و جایش را می‌خواند). این‌جا خودِ
/// قاعده قفل می‌شود.
/// </summary>
public class CellDirectionTests
{
    private static FlowDirection Dir(string? s) => ExcelGrid.DirectionOf(s);

    /// <summary>رقم — از هر خطی که باشد — جهت را تعیین نمی‌کند.</summary>
    [Theory]
    [InlineData("12,345")]          // رقمِ لاتین
    [InlineData("۱۲۳۴۵")]           // رقمِ فارسی
    [InlineData("۱۲٬۳۴۵")]          // رقمِ فارسی با جداکنندهٔ ٬
    [InlineData("١٢٣٤٥")]           // رقمِ عربی
    [InlineData("۱۴۰۵/۰۶/۲۳")]      // تاریخِ شمسی
    [InlineData("-۲۵٫۵")]           // منفی و اعشاری
    [InlineData("")]
    [InlineData(null)]
    public void NumbersAreNeverRightToLeft(string? text)
        => Assert.Equal(FlowDirection.LeftToRight, Dir(text));

    /// <summary>حرفِ فارسی/عربی، هر جای نوشته که باشد، جهت را راست‌به‌چپ می‌کند.</summary>
    [Theory]
    [InlineData("برق دکان")]
    [InlineData("۵ لیتر")]          // رقم اول است ولی حرف تعیین می‌کند
    [InlineData("۱۲٬۳۴۵ افغانی")]
    [InlineData("قرض ۲")]
    public void PersianTextIsRightToLeft(string text)
        => Assert.Equal(FlowDirection.RightToLeft, Dir(text));

    /// <summary>حرفِ لاتین هم حرفِ قوی است — نخستینش برنده است.</summary>
    [Theory]
    [InlineData("Petrol")]
    [InlineData("۵ Litre")]
    public void LatinTextIsLeftToRight(string text)
        => Assert.Equal(FlowDirection.LeftToRight, Dir(text));

    /// <summary>
    /// ⚠️ «نخستین حرفِ قوی» یعنی همان اولی، نه اکثریت — عینِ ‎dir="auto"‎ی
    /// مرورگر. اگر روزی کسی این را به «هر کدام بیشتر بود» عوض کند، نامِ
    /// فارسی‌ای که با یک واژهٔ انگلیسی شروع شود جهتش می‌پرد.
    /// </summary>
    [Fact]
    public void TheFirstStrongLetterWins()
    {
        Assert.Equal(FlowDirection.LeftToRight, Dir("Toyota کرولا سفید"));
        Assert.Equal(FlowDirection.RightToLeft, Dir("کرولا Toyota سفید"));
    }
}
