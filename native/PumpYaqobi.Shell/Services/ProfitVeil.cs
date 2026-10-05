using PumpYaqobi.Services.Security;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ رمزِ «مفاد/ضرر» عددِ مفاد را هر جا که هست می‌پوشاند ══════════════════════
///
/// صاحب ریپو (۱۴۰۵/۰۷/۱۶): «مفاد و ضرر تنها آن‌جا دیده نمی‌شود — در داشبورد،
/// در نوارِ عددهای بالا و در ورق هم هست؛ با قفل شدن باید این‌ها هم قفل شوند.»
///
/// ⛔ <b>تنها جای این تصمیم</b> همین است: تا رمزِ «مفاد/ضرر» زده نشده (همان
/// <see cref="SectionLockService.NeedsUnlock"/>)، هر جای دیگری که عددِ مفاد را
/// نشان می‌دهد به‌جایش <see cref="Mask"/> می‌گذارد. ⛔ هیچ حسابی عوض نمی‌شود —
/// عدد حساب می‌شود و فقط دیده نمی‌شود. رمز که در بخشِ مفاد زده شد، همه‌جا باز
/// می‌شود و «🔒 دوباره قفل کن» همه‌جا را می‌بندد (<see cref="Changed"/>).
/// </summary>
public static class ProfitVeil
{
    public const string Mask = "🔒 •••";
    public const string Tip = "مفاد پشتِ رمزِ «مفاد/ضرر» است — در همان بخش رمز بزنید تا همه‌جا دیده شود";

    /// <summary>عددِ مفاد همین حالا پنهان است؟</summary>
    public static bool Hidden
    {
        get
        {
            //  ⛔ پلنِ بی مفاد (استاندارد، ۱۴۰۵/۰۷/۲۰) همه‌جا تار است — هیچ رمزی بازش نمی‌کند.
            //  ⚠️ ‎PlanDenies‎ نه ‎Allows‎: فقط پلنی که صریحاً مفاد ندارد؛ بی‌مجوز همان رفتارِ پیشین.
            try
            {
                return Entitlements.PlanDenies(Entitlements.Profit)
                       || AppHost.Current?.Locks.NeedsUnlock(SectionLockService.Profit) == true;
            }
            catch { return false; }
        }
    }

    /// <summary>همان متن، یا ‎Mask‎ اگر پنهان است.</summary>
    public static string Show(string text) => Hidden ? Mask : text;

    /// <summary>قفلِ «مفاد/ضرر» باز یا بسته شد — هر جا که عدد نشان می‌دهد تازه شود.</summary>
    public static event Action? Changed;

    private static SectionLockService? _hooked;

    /// <summary>یک بار پس از بالا آمدنِ میزبان (و پس از عوض شدنِ دفتر) صدا زده می‌شود.</summary>
    public static void Hook(SectionLockService locks)
    {
        if (ReferenceEquals(_hooked, locks)) return;
        if (_hooked is not null) _hooked.Changed -= OnLock;
        _hooked = locks;
        locks.Changed += OnLock;
    }

    private static void OnLock(string id)
    {
        if (id != SectionLockService.Profit) return;
        try { Changed?.Invoke(); } catch { }
    }
}
