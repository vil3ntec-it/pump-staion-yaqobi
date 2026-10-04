using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Update;

/// <summary>
/// ══ «برنامه برای نصب بسته می‌شود» — پرسشِ بکاپ سرِ راه نمی‌ایستد (۱۴۰۵/۰۷/۲۰) ══
/// گزارشِ صاحب ریپو: «وقتی برنامه را از داخل نصب و راه‌اندازیِ مجدد می‌کنم برنامه
/// بسته می‌شود و دوباره باز نمی‌شود — روی بعضی کامپیوترها.»
///
/// ریشه: بستنِ برنامه پرسشِ «پیش از بستن بکاپ گرفته شود؟» را باز می‌کرد (فقط
/// وقتی در همان اجرا چیزی نوشته شده — یعنی «بعضی کامپیوترها»). برنامه پشتِ آن
/// پنجره باز می‌ماند، نصاب نمی‌توانست ببندش، و نمونهٔ تازه‌ای که نصاب باز
/// می‌کرد قفلِ تک‌نمونه را باز می‌دید و بیرون می‌رفت. کاربر که پاسخ می‌داد،
/// برنامه بسته می‌شد و هیچ چیزی دوباره باز نمی‌شد.
///
/// ⛔ فقط آن پرسش رد می‌شود — هر نوشتهٔ در صف همچنان پیش از بستن روی دیسک
/// می‌نشیند، و نصاب خودش پیش از نصب بکاپ می‌گیرد.
/// ⚠️ مهلت دارد: «نصب از فایل» ویزاردی است که کاربر می‌تواند لغو کند؛ پس از
/// آن، بستنِ عادی دوباره می‌پرسد.
/// </summary>
public static class UpdateExit
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private static DateTime? _armedAt;

    public static void Arm() => _armedAt = AppClock.Mono;

    public static bool Active => Armed(_armedAt, AppClock.Mono);

    /// <summary>خالص: پرسشِ بکاپ رد شود؟</summary>
    public static bool Armed(DateTime? armedAt, DateTime now) =>
        armedAt is { } at && now >= at && now - at <= Window;

    /// <summary>فقط برای آزمون‌ها.</summary>
    public static void TestReset() => _armedAt = null;
}
