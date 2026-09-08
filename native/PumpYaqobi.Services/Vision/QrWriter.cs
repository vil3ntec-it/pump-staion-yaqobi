using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace PumpYaqobi.Services.Vision;

/// <summary>
/// ══ ساختنِ کیو‌آر ══════════════════════════════════════════════════════════
/// همتای <c>new QRCode(wrap, {...})</c>ی نسخهٔ وب (کتابخانهٔ qrcode.js).
///
/// رمزگشا (<see cref="QrReader"/>) از اول در برنامه بود، ولی هیچ راهی برای
/// <b>ساختنِ</b> کیو‌آر نبود — یعنی «📲 کیو‌آرِ» هر حساب که در سایت هست، در
/// برنامهٔ نیتیو اصلاً وجود نداشت.
///
/// ⚠️ بی‌اینترنت کار می‌کند: ZXing داخلِ خودِ بسته است و هیچ چیزی از شبکه
/// گرفته نمی‌شود.
/// </summary>
public static class QrWriter
{
    /// <summary>اندازهٔ پیش‌فرض — همان ۲۲۰ پیکسلِ نسخهٔ وب.</summary>
    public const int DefaultSize = 220;

    /// <summary>
    /// کیو‌آرِ <paramref name="text"/> را به‌صورت PNG برمی‌گرداند.
    /// متنِ خالی ‎null‎ می‌دهد.
    /// </summary>
    public static byte[]? EncodePng(string? text, int size = DefaultSize)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (size < 64) size = 64;

        var writer = new QRCodeWriter();
        var opts = new EncodingOptions
        {
            Width = size,
            Height = size,
            // حاشیهٔ سفیدِ دورِ کد. بی آن، خواننده‌ها روی کاغذِ چاپی کد را
            // پیدا نمی‌کنند — همان «quiet zone»ی که استاندارد می‌خواهد.
            Margin = 1,
        };

        BitMatrix matrix;
        try { matrix = writer.encode(text, BarcodeFormat.QR_CODE, size, size, opts.Hints); }
        catch { return null; }

        var w = matrix.Width;
        var h = matrix.Height;

        using var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                bmp.SetPixel(x, y, matrix[x, y] ? SKColors.Black : SKColors.White);

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray();
    }
}
