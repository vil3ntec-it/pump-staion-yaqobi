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
    [InlineData("https://pump.example.com", "https://pump.example.com/#roview-debt-5-pdf")]
    [InlineData("https://pump.example.com/", "https://pump.example.com/#roview-debt-5-pdf")]
    // بی «http» گوشی نشانی را باز نمی‌کند و متن می‌بیند
    [InlineData("pump.example.com", "https://pump.example.com/#roview-debt-5-pdf")]
    // هش یا پرسشِ قبلی نباید جای مالِ ما را بگیرد
    [InlineData("https://pump.example.com/#roview-debt-9-pdf", "https://pump.example.com/#roview-debt-5-pdf")]
    [InlineData("https://pump.example.com/?x=1", "https://pump.example.com/#roview-debt-5-pdf")]
    public void FullUrlIsOpenableOnAPhone(string page, string expected)
        => Assert.Equal(expected, AcctLink.FullUrl(page, 5));

    /// <summary>
    /// ⚠️ نشانیِ سرور و رمز هم باید داخلِ لینک باشند.
    ///
    /// گوشیِ مشتری این صفحه را تا امروز باز نکرده، پس چیزی در حافظه‌اش نیست و
    /// نمی‌داند به کدام سرور وصل شود — صفحه‌ای خالی می‌بیند. خودِ سایت هم در
    /// ‎copyShareLink‎ همین کار را می‌کند و صفحه سرِ بارگیری برشان می‌دارد.
    /// </summary>
    [Fact]
    public void ServerAndTokenRideAlongInTheLink()
    {
        var url = AcctLink.FullUrl("https://pump.example.com", 5, null, "debt",
                                   "wss://api.example.com", "s3cret")!;
        Assert.Equal(
            "https://pump.example.com/?server=wss%3A%2F%2Fapi.example.com&token=s3cret#roview-debt-5-pdf",
            url);

        // بی رمز، فقط سرور
        var noTok = AcctLink.FullUrl("https://pump.example.com", 5, null, "debt",
                                     "wss://api.example.com", "")!;
        Assert.Equal("https://pump.example.com/?server=wss%3A%2F%2Fapi.example.com#roview-debt-5-pdf",
                     noTok);

        // و در هر حال، تکهٔ حساب باید هنوز خوانده شود
        var back = AcctLink.Parse(url);
        Assert.NotNull(back);
        Assert.Equal(5, back!.Value.PersonId);
    }

    /// <summary>بی نشانیِ صفحه، کیو‌آری ساخته نمی‌شود — نه یک لینکِ نصفه.</summary>
    [Fact]
    public void NoPageMeansNoLink()
    {
        Assert.Null(AcctLink.FullUrl(null, 5));
        Assert.Null(AcctLink.FullUrl("   ", 5));
        Assert.Null(AcctLink.FullUrl("#only-a-hash", 5));
    }

    /// <summary>و نشانیِ کامل هم باید به همان حساب برگردد.</summary>
    [Fact]
    public void FullUrlRoundTrips()
    {
        var url = AcctLink.FullUrl("https://pump.example.com", 77, "s5", "debt",
                                   "wss://api.example.com", "tok")!;
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
