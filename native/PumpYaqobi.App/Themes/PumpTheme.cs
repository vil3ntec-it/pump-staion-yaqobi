using Avalonia;
using Avalonia.Media;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// ══ تم‌های برنامه ══════════════════════════════════════════════════════════
/// دقیقاً همان چهار تمی که صاحب ریپو نگه داشت — نه یکی بیشتر، نه کمتر:
///   dark-amber  «دارک مود طلایی مشکی»
///   deep-ocean  «اقیانوس عمیق»
///   light-cream «لایت مود طلایی سلطنتی»
///   burgundy    «شرابی کرم»
///
/// رنگ‌ها مو‌به‌مو از متغیرهای CSSِ همان تم‌ها در index.html برداشته شده‌اند تا
/// نسخهٔ نیتیو هم‌رنگِ نسخه‌ای باشد که کاربر سال‌ها با آن کار کرده است.
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
    Color[] HeaderBg,   // گرادیانِ افقیِ سربرگ
    Color[] BannerBg,
    Color[] NavBg,
    Color[] AppBg,      // گرادیانِ عمودیِ بوم
    Color[] AccentGrad) // گرادیانِ دکمه‌های اصلی
{
    public static Color C(string hex) => Color.Parse(hex);

    public static readonly PumpTheme DarkAmber = new(
        "dark-amber", "دارک مود طلایی مشکی", true,
        C("#0a0806"), C("#0e0b07"), C("#171208"), C("#3a2c14"),
        C("#f5eee0"), C("#8c8272"), C("#c9bb9c"), C("#ffb43d"), C("#1a1102"),
        C("#e8a020"), C("#57400f"),
        new[] { C("#0a0703"), C("#150f06"), C("#0a0703") },
        new[] { C("#080602"), C("#120c05"), C("#080602") },
        new[] { C("#070502"), C("#0f0a04"), C("#070502") },
        new[] { C("#131417"), C("#0d0e10"), C("#0a0b0d"), C("#060708") },
        new[] { C("#ffd27a"), C("#ffb43d"), C("#f5872b") });

    public static readonly PumpTheme DeepOcean = new(
        "deep-ocean", "اقیانوس عمیق", true,
        C("#00223f"), C("#012a4a"), C("#063a61"), C("#1c5280"),
        C("#eef3f8"), C("#9fb6cc"), C("#c9c1b0"), C("#c9c1b0"), C("#00223f"),
        C("#c9c1b0"), C("#4b5a66"),
        new[] { C("#001c35"), C("#013058"), C("#001c35") },
        new[] { C("#00182e"), C("#01294b"), C("#00182e") },
        new[] { C("#001529"), C("#012241"), C("#001529") },
        new[] { C("#013056"), C("#002240"), C("#001a30"), C("#001120") },
        new[] { C("#e6e0d2"), C("#c9c1b0"), C("#a89e88") });

    public static readonly PumpTheme LightCream = new(
        "light-cream", "لایت مود طلایی سلطنتی", false,
        C("#fbf4e7"), C("#fffdf8"), C("#fff8ec"), C("#e4d4b6"),
        C("#241a0c"), C("#7a6a52"), C("#5a4a30"), C("#d97706"), C("#2a1a02"),
        C("#b45f00"), C("#d9b98a"),
        new[] { C("#f7e0c2"), C("#fdf1e2"), C("#f7e0c2") },
        new[] { C("#f9e7d0"), C("#fef4e8"), C("#f9e7d0") },
        new[] { C("#fbf1dc"), C("#fff6e6"), C("#fbf1dc") },
        new[] { C("#f8f2e6"), C("#f1e9da"), C("#eae0cd"), C("#e3d7c1") },
        new[] { C("#ffd27a"), C("#f59e0b"), C("#d97706") });

    public static readonly PumpTheme Burgundy = new(
        "burgundy", "شرابی کرم", false,
        C("#fff3e1"), C("#fffbf5"), C("#fdf4e8"), C("#e6ccbd"),
        C("#2a0c0c"), C("#8a5a52"), C("#6f0000"), C("#8a0d0d"), C("#ffffff"),
        C("#7a0000"), C("#d8a9a0"),
        new[] { C("#f6dfcd"), C("#fdeee0"), C("#f6dfcd") },
        new[] { C("#f3d9c6"), C("#fbe9da"), C("#f3d9c6") },
        new[] { C("#fbe8d6"), C("#fff6ea"), C("#fbe8d6") },
        new[] { C("#fff3e1"), C("#f8e8d6"), C("#f0dcc6"), C("#ecd3bc") },
        new[] { C("#b34a4a"), C("#6f0000"), C("#4d0000") });

    public static readonly PumpTheme[] All = { DarkAmber, DeepOcean, LightCream, Burgundy };

    public static PumpTheme ById(string? id) =>
        All.FirstOrDefault(t => t.Id == id) ?? DarkAmber;
}
