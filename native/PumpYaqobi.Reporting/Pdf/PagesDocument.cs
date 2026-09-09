using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>
/// ══ سندی از ورق‌های برگزیده ═════════════════════════════════════════════════
///
/// وقتی کاربر «بازهٔ ورق‌ها»، «صفحاتِ انتخابی»، «چند نسخه» یا «ترتیبِ وارونه» را
/// خواسته باشد، خروجی دیگر خودِ سندِ اصلی نیست — باید همان ورق‌ها، به همان
/// ترتیب، در یک فایل بنشینند.
///
/// موتورِ سند (QuestPDF) برشِ ورق ندارد؛ پس ورق‌های برگزیده از روی **تصویرِ
/// خودشان** چیده می‌شوند. یعنی چیزی که چاپ می‌شود مو‌به‌مو همان است که در
/// پیش‌نمایش دیده‌اید.
///
/// ⚠️ این کلاس عمداً کنارِ موتورِ چاپ است، نه داخلِ پنجرهٔ پیش‌نمایش: هر سه
/// خروجی — پیش‌نمایش، PDF و چاپِ مستقیم — باید از یک منبع بیایند، و آزمون هم
/// باید بتواند بی هیچ پنجره‌ای بسنجدش.
///
/// ⚠️ اندازهٔ هر صفحه از خودِ تصویر خوانده می‌شود، نه از یک ثابتِ A4: با هر
/// کاغذ و هر جهتی، ورقِ خروجی دقیقاً هم‌اندازهٔ ورقِ اصلی درمی‌آید.
/// </summary>
public sealed class PagesDocument : IDocument
{
    private readonly IReadOnlyList<byte[]> _images;
    private readonly int _dpi;

    public PagesDocument(IReadOnlyList<byte[]> pngPages, int dpi)
    {
        _images = pngPages;
        _dpi = Math.Clamp(dpi, 72, 400);
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        foreach (var png in _images)
        {
            var (w, h) = SizePt(png);
            container.Page(p =>
            {
                p.Size(w, h, Unit.Point);
                p.Margin(0);
                p.PageColor(Colors.White);
                p.Content().Image(png).FitArea();
            });
        }
    }

    /// <summary>اندازهٔ صفحه به «پوینت» از روی پیکسلِ تصویر: ‎px ÷ dpi × 72‎.</summary>
    private (float W, float H) SizePt(byte[] png)
    {
        var (pw, ph) = PngSize(png);
        if (pw <= 0 || ph <= 0) return (595f, 842f);      // A4، اگر تصویر خوانده نشد
        return (pw / (float)_dpi * 72f, ph / (float)_dpi * 72f);
    }

    /// <summary>
    /// عرض و بلندای یک PNG، مستقیم از سرآیندش.
    ///
    /// ⚠️ عمداً بی هیچ کتابخانهٔ تصویری: این‌جا لایهٔ گزارش است و نباید به
    /// رابطِ کاربری (و پلتفرمِ گرافیکیِ راه‌اندازی‌شده) بند باشد — وگرنه همین
    /// کد در آزمونِ بی‌پنجره اجرا نمی‌شد.
    ///
    /// ساختارِ PNG ثابت است: هشت بایتِ امضا، بعد طولِ بخش و نامش (‎IHDR‎)، و
    /// بعد عرض و بلندا، هر کدام چهار بایت و «بزرگ‌سرِ» (big-endian).
    /// </summary>
    public static (int W, int H) PngSize(byte[] png)
    {
        if (png.Length < 24) return (0, 0);
        if (png[0] != 0x89 || png[1] != 'P' || png[2] != 'N' || png[3] != 'G') return (0, 0);

        static int Be(byte[] b, int at) =>
            (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

        return (Be(png, 16), Be(png, 20));
    }
}
