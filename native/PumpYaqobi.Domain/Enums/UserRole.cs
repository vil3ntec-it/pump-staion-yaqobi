namespace PumpYaqobi.Domain.Enums;

/// <summary>
/// نقشِ کاربر. بندِ ۱۹: دسترسی باید در لایهٔ سرویس اعمال شود، نه فقط با
/// پنهان کردنِ دکمه — پنهان کردنِ دکمه امنیت نیست.
/// </summary>
public enum UserRole
{
    /// <summary>مدیر (میرزا) — دسترسی کامل.</summary>
    Admin = 1,
    /// <summary>نظاره‌گر — فقط خواندن؛ هیچ نوشتنی مجاز نیست.</summary>
    Viewer = 2,
    /// <summary>کارمند — دسترسی محدود به بخش‌های کاری.</summary>
    Staff = 3
}
