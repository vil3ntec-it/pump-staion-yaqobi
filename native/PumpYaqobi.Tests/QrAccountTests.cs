using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Vision;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ کیو‌آرِ حساب و اسکنش ═══════════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «اسکنر که اصلاً وجود ندارد، آن باید باشد — ببین کجاها
/// اسکنِ هر حساب را جدا داشت در سایت، در این هم باشد.»
///
/// ⚠️ مهم‌ترین چیزی که این‌جا قفل می‌شود، <b>شکلِ نشانی</b> است. کیو‌آرهای
/// چاپ‌شدهٔ سایت همین شکل را دارند؛ اگر عوض شود، آن کاغذها از کار می‌افتند.
/// </summary>
public class QrAccountTests
{
    [Fact]
    public void LinkShapeMatchesTheWebsite()
    {
        // _acctHash(type, id, subId) → '#roview-' + type + '-' + id + ('~'+sub) + '-pdf'
        Assert.Equal("#roview-debt-7-pdf", AcctLink.Build(7));
        Assert.Equal("#roview-debt-7~s1a2b3c4d-pdf", AcctLink.Build(7, "s1a2b3c4d"));
    }

    [Fact]
    public void ParseReadsBothPlainAndFullUrls()
    {
        var a = AcctLink.Parse("#roview-debt-7-pdf");
        Assert.NotNull(a);
        Assert.Equal(7, a!.Value.PersonId);
        Assert.Null(a.Value.SubId);
        Assert.Equal("debt", a.Value.Type);

        // کیو‌آرهای سایت نشانیِ کامل دارند، نه فقط تکهٔ هش.
        // ⚠️ میزبانِ این نمونه عمداً ساختگی است: قاعدهٔ ‎NoUserFacingFileMentionsTheRepository‎
        // می‌گوید نامِ مخزن جز در ‎UpdateService.cs‎ هیچ‌جا نیاید. آن‌چه این‌جا
        // سنجیده می‌شود تکهٔ ‎#roview…‎ است، نه میزبان.
        var b = AcctLink.Parse("https://example.invalid/app/#roview-debt-42~s99-pdf");
        Assert.NotNull(b);
        Assert.Equal(42, b!.Value.PersonId);
        Assert.Equal("s99", b.Value.SubId);
    }

    [Fact]
    public void NonAccountTextIsRejected()
    {
        Assert.Null(AcctLink.Parse(null));
        Assert.Null(AcctLink.Parse(""));
        Assert.Null(AcctLink.Parse("rtsp://192.168.1.9/stream"));      // کیو‌آرِ دوربین
        Assert.Null(AcctLink.Parse("#roview-debt-abc-pdf"));           // شناسهٔ غیرعددی
    }

    /// <summary>
    /// رفت‌وبرگشتِ واقعی: کیو‌آر ساخته می‌شود، بعد <b>همان بایت‌ها</b> دوباره
    /// خوانده می‌شوند. ادعا نیست — اگر رمزگذار و رمزگشا با هم نخوانند،
    /// این‌جا قرمز می‌شود.
    /// </summary>
    [Fact]
    public void EncodeThenDecodeGivesTheSameLink()
    {
        var link = AcctLink.Build(123, "s7f3a1b2");
        var png = QrWriter.EncodePng(link);

        Assert.NotNull(png);
        Assert.True(png!.Length > 100, "کیو‌آری ساخته نشد");

        var back = QrReader.DecodeImageBytes(png);
        Assert.Equal(link, back);

        // و همان چیزی که خوانده شد، دوباره به همان حساب برمی‌گردد
        var parsed = AcctLink.Parse(back);
        Assert.NotNull(parsed);
        Assert.Equal(123, parsed!.Value.PersonId);
        Assert.Equal("s7f3a1b2", parsed.Value.SubId);
    }

    /// <summary>
    /// ⚠️ کیو‌آرِ حساب برای <b>خودِ قرض‌دار</b> است: می‌فرستیمش و او با گوشیِ
    /// خودش حسابش را زنده می‌بیند. پس نشانی باید کامل و باز‌شدنی باشد.
    /// تکهٔ ‎#roview…‎ی تنها روی گوشیِ مشتری هیچ کاری نمی‌کند.
    /// </summary>
    [Theory]
    [InlineData("http://192.168.1.50:8080", "http://192.168.1.50:8080/#roview-debt-5-pdf")]
    [InlineData("http://192.168.1.50:8080/", "http://192.168.1.50:8080/#roview-debt-5-pdf")]
    // بی «http» گوشی نشانی را باز نمی‌کند و متن می‌بیند
    [InlineData("192.168.1.50:8080", "http://192.168.1.50:8080/#roview-debt-5-pdf")]
    // هشِ قبلی نباید دو تا بشود
    [InlineData("http://pump.local/#roview-debt-9-pdf", "http://pump.local/#roview-debt-5-pdf")]
    public void FullUrlIsOpenableOnAPhone(string server, string expected)
        => Assert.Equal(expected, AcctLink.FullUrl(server, 5));

    /// <summary>بی نشانیِ سرور، کیو‌آری ساخته نمی‌شود — نه یک لینکِ نصفه.</summary>
    [Fact]
    public void NoServerMeansNoLink()
    {
        Assert.Null(AcctLink.FullUrl(null, 5));
        Assert.Null(AcctLink.FullUrl("   ", 5));
        Assert.Null(AcctLink.FullUrl("#only-a-hash", 5));
    }

    /// <summary>و نشانیِ کامل هم باید به همان حساب برگردد.</summary>
    [Fact]
    public void FullUrlRoundTrips()
    {
        var url = AcctLink.FullUrl("http://192.168.1.50:8080", 77, "s5")!;
        var back = AcctLink.Parse(QrReader.DecodeImageBytes(QrWriter.EncodePng(url)));
        Assert.NotNull(back);
        Assert.Equal(77, back!.Value.PersonId);
        Assert.Equal("s5", back.Value.SubId);
    }

    [Fact]
    public void EmptyTextMakesNoCode()
    {
        Assert.Null(QrWriter.EncodePng(null));
        Assert.Null(QrWriter.EncodePng("   "));
    }
}
