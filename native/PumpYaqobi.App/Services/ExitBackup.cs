using PumpYaqobi.Persistence;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «پیش از خروج، اگر لازم است، بکاپ بگیرم؟» (۱۴۰۵/۰۷/۱۹) ═══════════════════
///
/// «لازم» یعنی: از آخرین بکاپِ همین اجرا (یا از ورود) دفتر واقعاً عوض شده —
/// همان ‎PumpDbContext.Version‎ که هر ترمزِ «داده عوض نشده» از آن می‌خواند.
/// باز کردن و نگاه کردن هیچ پرسشی نمی‌سازد.
///
/// ⛔ فقط می‌پرسد؛ «نه» یعنی همان بستنِ همیشگی، و هر نوشته‌ای پیش از پرسش روی
/// دیسک نشسته است (‎FlushEverythingAsync‎). ⚠️ در آزمون‌ها و سنجه‌ها خاموش است
/// (‎Disabled‎) — پنجرهٔ بی‌سر نباید سرِ بستن منتظرِ کلیک بماند.
/// </summary>
public static class ExitBackup
{
    public static bool Disabled { get; set; }

    private static long _mark = -1;

    /// <summary>«از این‌جا به بعد را بشمار» — پس از ورود و پس از هر بکاپ.</summary>
    public static void Mark() => Interlocked.Exchange(ref _mark, PumpDbContext.Version);

    /// <summary>خالص، برای آزمون: پیش از ورود هیچ؛ بعد فقط اگر شماره جلو رفته.</summary>
    public static bool Changed(long mark, long now) => mark >= 0 && now != mark;

    public static bool Needed(bool askEnabled) =>
        !Disabled && askEnabled && Changed(Interlocked.Read(ref _mark), PumpDbContext.Version);
}
