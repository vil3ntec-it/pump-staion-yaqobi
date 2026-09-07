using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace PumpYaqobi.Services.Vision;

/// <summary>
/// ══ خواندنِ کیو‌آر ══════════════════════════════════════════════════════════
/// رونوشتِ ‎_camDecodeCanvas‎ · ‎camAddByQRImage‎.
///
/// نسخهٔ وب اول ‎BarcodeDetector‎ِ مرورگر را امتحان می‌کرد و اگر نبود
/// ‎jsQR‎ را از اینترنت می‌گرفت. این‌جا هیچ‌کدام لازم نیست: رمزگشا داخلِ خودِ
/// برنامه است و بی‌اینترنت هم کار می‌کند.
///
/// ⚠️ عکس پیش از خواندن کوچک می‌شود (مثلِ ‎M = 1200‎ی نسخهٔ وب): عکسِ ۱۲ مگاپیکسلیِ
/// گوشی هم کندتر خوانده می‌شود هم بدتر — نویزِ ریزِ دوربین الگو را می‌شکند.
/// </summary>
public static class QrReader
{
    /// <summary>بزرگ‌ترین ضلعِ عکس پیش از خواندن — همان ‎M‎ی نسخهٔ وب.</summary>
    public const int MaxSide = 1200;

    /// <summary>
    /// خواندنِ کیو‌آر از پیکسل‌های خام (BGRA، بی‌فاصلهٔ سطر).
    /// ‎null‎ یعنی در این تصویر کیو‌آری نبود.
    /// </summary>
    public static string? DecodeBgra(byte[] bgra, int width, int height)
    {
        if (bgra is null || width <= 0 || height <= 0) return null;
        if (bgra.Length < width * height * 4) return null;

        var source = new RGBLuminanceSource(bgra, width, height,
                                            RGBLuminanceSource.BitmapFormat.BGRA32);
        var hints = new Dictionary<DecodeHintType, object>
        {
            // کیو‌آرِ چاپ‌شده روی کاغذ کج و کم‌نور عکس گرفته می‌شود؛ بی این،
            // نصفِ عکس‌های واقعی خوانده نمی‌شوند.
            [DecodeHintType.TRY_HARDER] = true,
        };

        // ⚠️ دو بار: یک‌بار عادی و یک‌بار وارونه. کیو‌آرِ سفید روی زمینهٔ تیره
        // (که روی برچسبِ خیلی از دوربین‌ها هست) فقط با وارونه خوانده می‌شود.
        return Try(source, hints) ?? Try(source.invert(), hints);
    }

    private static string? Try(LuminanceSource source, IDictionary<DecodeHintType, object> hints)
    {
        try
        {
            var result = new QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(source)), hints);
            var text = result?.Text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            // ZXing برای «پیدا نشد» ‎null‎ می‌دهد، ولی عکسِ خراب می‌تواند استثنا بدهد.
            return null;
        }
    }

    /// <summary>
    /// خواندنِ کیو‌آر از یک فایلِ عکس (PNG/JPEG/…).
    /// ‎null‎ یعنی عکس خوانده نشد یا کیو‌آری در آن نبود.
    ///
    /// ⚠️ همیشه از راهِ ‎ScalePixels‎ می‌رود، حتی وقتی عکس کوچک است: همان یک
    /// فراخوانی هم اندازه را کم می‌کند و هم قالبِ پیکسل را به BGRA می‌برد، پس
    /// عکسِ خاکستری یا ۱۶بیتیِ دوربین هم بی‌دردسر خوانده می‌شود.
    /// </summary>
    public static string? DecodeFile(string path)
    {
        try
        {
            using var raw = SKBitmap.Decode(path);
            if (raw is null || raw.Width <= 0 || raw.Height <= 0) return null;

            var side = Math.Max(raw.Width, raw.Height);
            var scale = side > MaxSide ? (double)MaxSide / side : 1.0;
            var w = Math.Max(1, (int)Math.Round(raw.Width * scale));
            var h = Math.Max(1, (int)Math.Round(raw.Height * scale));

            using var dst = new SKBitmap(
                new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            if (!raw.ScalePixels(dst, SKFilterQuality.High)) return null;

            return DecodeBgra(dst.Bytes, dst.Width, dst.Height);
        }
        catch
        {
            return null;
        }
    }
}
