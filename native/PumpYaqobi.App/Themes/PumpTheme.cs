using Avalonia;
using Avalonia.Media;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// ══ تم‌های برنامه ══════════════════════════════════════════════════════════
/// دو تمی که صاحب ریپو از روی نمونه‌ها انتخاب کرد — نه یکی بیشتر، نه کمتر:
///   marble    «مرمرِ شرابی»      روشن، خاکستریِ گرمِ مرمری با شرابیِ سیر
///   obsidian  «اُبسیدینِ نیلی»   تیره، سیاهِ متمایل به نیلی با تاکیدِ یاسی
///
/// چهار تمِ قبلی (طلاییِ مشکی، اقیانوس، لایتِ سلطنتی، شرابیِ کرم) به خواستهٔ
/// صریحِ صاحب ریپو برداشته شدند: «اون چهار تای داخل اپ رو نمیخام».
///
/// ⚠️ سه قاعده‌ای که این پالت‌ها را «گران‌قیمت» می‌کند و نباید شکسته شود:
///   ۱) رنگِ تاکید خسیس خرج می‌شود — تبِ فعال، دکمهٔ اصلی، عددهای کلیدی. بس.
///   ۲) هر سطح با یک خطِ نازک و یک پلهٔ روشناییِ کم جدا می‌شود، نه با رنگِ پررنگ.
///   ۳) هیچ سایه و گرادیانی روی چیزهای تکرارشونده (کارت، ردیف) نمی‌نشیند —
///      در Avalonia هر سایه یک لایهٔ رندرِ جداست و صدتا از آن، بخش را کند می‌کند.
///      برای همین این‌جا گرادیان فقط برای سربرگ/نوار/دکمهٔ اصلی تعریف شده است.
///
/// «نور پس‌زمینه» (hexfx/atmo) عمداً وجود ندارد — پاک شده و برنمی‌گردد.
/// </summary>
public sealed record PumpTheme(
    string Id,
    string Title,
    bool IsDark,
    Color Dark,        // بومِ صفحه
    Color Panel,       // سطحِ پنل
    Color Card,        // سطحِ کارت/جدول
    Color Border,
    Color Text,
    Color Muted,
    Color Label,
    Color Accent,
    Color OnAccent,
    Color HeaderTitle,
    Color ChromeBorder,
    Color HeadBand,    // نوارِ سرِ جدول و ردیفِ «جمله»
    Color OnHeadBand,  // نوشتهٔ روی همان نوار
    Color[] HeaderBg,   // گرادیانِ افقیِ سربرگ
    Color[] BannerBg,
    Color[] NavBg,
    Color[] AppBg,      // گرادیانِ عمودیِ بوم
    Color[] AccentGrad) // گرادیانِ دکمه‌های اصلی
{
    public static Color C(string hex) => Color.Parse(hex);

    /// <summary>
    /// «مرمرِ شرابی» — خاکستریِ گرمِ مرمری با شرابیِ سیر.
    ///
    /// نوارِ سرِ جدول شرابیِ **خاکستری‌شده** است (‎#4a2a31‎) نه شرابیِ سیاه:
    /// گزارشِ صاحب ریپو روی نمونه «اون جمله و سر جدول رو کمی صاف‌تر کن خیلی
    /// تیره است». همین رنگ هم قاب می‌سازد هم چشم را نمی‌زند.
    /// </summary>
    public static readonly PumpTheme Marble = new(
        "marble", "مرمرِ شرابی", false,
        C("#f3f1f1"), C("#ffffff"), C("#ffffff"), C("#e2dcdc"),
        C("#1f1618"), C("#68585a"), C("#7a2233"), C("#8c2233"), C("#ffffff"),
        C("#1f1618"), C("#e2dcdc"),
        C("#4a2a31"), C("#f7eef0"),
        new[] { C("#ffffff"), C("#faf7f7"), C("#ffffff") },
        new[] { C("#fbf9f9"), C("#ffffff"), C("#fbf9f9") },
        new[] { C("#ffffff"), C("#fdfbfb"), C("#ffffff") },
        new[] { C("#f7f5f5"), C("#f3f1f1"), C("#efecec"), C("#eae7e7") },
        new[] { C("#b0384a"), C("#8c2233"), C("#6d1626") });

    /// <summary>
    /// «اُبسیدینِ نیلی» — سیاهِ متمایل به نیلی با تاکیدِ یاسی.
    ///
    /// روی تمِ تیره نوارِ سرِ جدول **روشن‌تر** از سطحِ جدول است (‎#232a4d‎)، نه
    /// تیره‌تر: در تیرگی، بالا بردنِ نور مرز می‌سازد و پایین بردنش سوراخ.
    /// </summary>
    public static readonly PumpTheme Obsidian = new(
        "obsidian", "اُبسیدینِ نیلی", true,
        C("#0b0d17"), C("#12162a"), C("#141931"), C("#242b47"),
        C("#e9ecf8"), C("#8e97bd"), C("#b9bdf0"), C("#8b8cff"), C("#0a0b1a"),
        C("#dfe2ff"), C("#242b47"),
        C("#232a4d"), C("#eceefc"),
        new[] { C("#151a33"), C("#101427"), C("#151a33") },
        new[] { C("#10142a"), C("#141a36"), C("#10142a") },
        new[] { C("#0d1122"), C("#11162c"), C("#0d1122") },
        new[] { C("#12162c"), C("#0e1121"), C("#0b0d17"), C("#090b14") },
        new[] { C("#a6a7ff"), C("#8b8cff"), C("#6f70e6") });

    public static readonly PumpTheme[] All = { Marble, Obsidian };

    public static PumpTheme ById(string? id) =>
        All.FirstOrDefault(t => t.Id == id) ?? Marble;
}
