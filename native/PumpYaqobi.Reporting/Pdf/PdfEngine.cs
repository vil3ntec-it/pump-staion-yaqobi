using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>
/// ══ موتورِ PDF — بومی، بدونِ مرورگر (بندِ ۲۲) ══════════════════════════════
/// نسخهٔ HTML برای هر PDF یک پنجرهٔ چاپِ مرورگر باز می‌کرد. این‌جا سند مستقیم
/// ساخته می‌شود: بدونِ WebView، بدونِ Chromium، بدونِ HTML.
///
/// فونتِ وزیرمتن داخلِ خودِ اسمبلی جاسازی شده تا:
///   • روی هر ویندوزی، حتی بدونِ نصبِ فونت، فارسی درست چاپ شود
///   • خروجی همیشه یک‌شکل باشد و به فونت‌های سیستم وابسته نباشد
///
/// ⚠️ نکته‌ای که یک‌بار وقت گرفت: فونتِ داخلِ نسخهٔ HTML به دو «زیرمجموعه» شکسته
/// شده بود — یکی فقط حروفِ عربی/فارسی (۱۴۲ نویسه) و یکی فقط لاتین (۹۵ نویسه).
/// اگر فقط یکی برداشته شود، PDF ساخته می‌شود ولی نیمی از متن *نامرئی* است
/// (اولین خروجی دقیقاً همین بود: جدول و خط‌ها درست، ولی هیچ حرفِ فارسی).
/// این فایل حاصلِ ادغامِ هر دو زیرمجموعه است: ۵۹۰ نویسه، هم فارسی هم لاتین.
/// </summary>
public static class PdfEngine
{
    private static bool _ready;
    private static readonly object _lock = new();

    /// <summary>یک‌بار در آغازِ برنامه صدا زده می‌شود.</summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_ready) return;
            QuestPDF.Settings.License = LicenseType.Community;
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.EndsWith(".ttf")))
            {
                using var s = asm.GetManifestResourceStream(name);
                if (s is null) continue;
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                // ⚠️ با نامِ خودِ فونت ثبت می‌شود، نه نامِ دلخواه: نامِ خانوادگیِ
                // داخلِ فایل «Vazirmatn» است و QuestPDF با همان می‌گرددش.
                FontManager.RegisterFont(new MemoryStream(ms.ToArray()));
            }
            _ready = true;
        }
    }

    public const string Font = "Vazirmatn";
}
