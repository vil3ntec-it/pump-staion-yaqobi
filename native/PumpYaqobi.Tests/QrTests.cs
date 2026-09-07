using PumpYaqobi.Services.Vision;
using Xunit;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بندِ ۱۵: خواندنِ کیو‌آر ══════════════════════════════════════════════════
/// رمزگشا داخلِ خودِ برنامه است، نه در سی‌دی‌ان — پس بی‌اینترنت هم کار می‌کند.
///
/// آزمون یک کیو‌آرِ واقعی می‌سازد و همان را با رمزگشای برنامه می‌خواند. عمداً
/// از فایلِ عکس نمی‌گذرد: آن یک لایهٔ دیگر (کدگشای PNG/JPEG) است و این آزمون
/// خودِ رمزگشا را می‌سنجد.
/// </summary>
public class QrTests
{
    /// <summary>
    /// یک کیو‌آرِ واقعی → پیکسل‌های BGRA، همان‌طور که از یک عکس می‌آید.
    ///
    /// ⚠️ ‎CHARACTER_SET = UTF-8‎ لازم است: کدگذارِ کیو‌آر بی این، متن را
    /// ISO-8859-1 می‌نویسد و هر حرفِ فارسی «؟» می‌شود — آن‌وقت آزمون به‌جای
    /// رمزگشا، کدگذارِ خودش را می‌سنجید. با این هینت یک نشانِ ECI داخلِ خودِ
    /// کیو‌آر می‌نشیند و رمزگشا از همان‌جا می‌فهمد متن UTF-8 است.
    /// </summary>
    private static (byte[] Pixels, int Width, int Height) Render(string text, int size = 300,
                                                                 bool inverted = false)
    {
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = 2,
            [EncodeHintType.CHARACTER_SET] = "UTF-8",
        };
        var matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, size, size, hints);

        var w = matrix.Width;
        var h = matrix.Height;
        var px = new byte[w * h * 4];
        var i = 0;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var dark = matrix[x, y];
            if (inverted) dark = !dark;
            var v = dark ? (byte)0 : (byte)255;
            px[i++] = v; px[i++] = v; px[i++] = v; px[i++] = 255;
        }
        return (px, w, h);
    }

    [Fact]
    public void ACameraLink_SurvivesTheRoundTrip()
    {
        const string link = "http://192.168.1.9/cgi-bin/snapshot.cgi?channel=1&user=admin";
        var (px, w, h) = Render(link);
        Assert.Equal(link, QrReader.DecodeBgra(px, w, h));
    }

    /// <summary>کیو‌آرِ فارسی هم باید سالم برگردد — نامِ دوربین‌ها فارسی است.</summary>
    [Fact]
    public void PersianText_SurvivesTheRoundTrip()
    {
        const string text = "دوربینِ درِ ورودی — پمپ یعقوبی";
        var (px, w, h) = Render(text);
        Assert.Equal(text, QrReader.DecodeBgra(px, w, h));
    }

    /// <summary>
    /// برچسبِ خیلی از دوربین‌ها کیو‌آرِ سفید روی زمینهٔ تیره دارد. بی خواندنِ
    /// وارونه، همان‌ها هیچ‌وقت خوانده نمی‌شدند.
    /// </summary>
    [Fact]
    public void InvertedQr_IsAlsoRead()
    {
        const string link = "rtsp://admin:1234@192.168.1.10:554/stream1";
        var (px, w, h) = Render(link, inverted: true);
        Assert.Equal(link, QrReader.DecodeBgra(px, w, h));
    }

    [Fact]
    public void APlainImage_GivesNull_NotAnException()
    {
        var px = new byte[64 * 64 * 4];
        for (var i = 0; i < px.Length; i++) px[i] = 255;
        Assert.Null(QrReader.DecodeBgra(px, 64, 64));
    }

    [Fact]
    public void BrokenInput_GivesNull()
    {
        Assert.Null(QrReader.DecodeBgra(Array.Empty<byte>(), 10, 10));
        Assert.Null(QrReader.DecodeBgra(new byte[400], 0, 0));
        Assert.Null(QrReader.DecodeBgra(new byte[10], 100, 100));   // بافرِ کوچک‌تر از تصویر
    }

    [Fact]
    public void AMissingFile_GivesNull_NotAnException()
    {
        Assert.Null(QrReader.DecodeFile(Path.Combine(Path.GetTempPath(), "no-such-qr-file.png")));
    }
}
