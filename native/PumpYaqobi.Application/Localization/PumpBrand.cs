namespace PumpYaqobi.Application.Localization;

/// <summary>
/// ══ نامی که برنامه با آن دیده می‌شود (۱۴۰۵/۰۷/۱۵) ══════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «برنامه اسمش پمپ یعقوبی نباشد، پمپ بنزین خالی
/// نوشته باشد، و یارو وقتی حساب می‌زند و اسمِ پمپ را می‌نویسد، اسمِ پمپ
/// همان باشد.»
///
/// پس <b>یک جا</b>: نامِ پمپی که کاربر نوشته (<c>stationName</c>ِ تنظیمات)،
/// وگرنه <see cref="Default"/>. سربرگ، عنوانِ پنجره، صفحهٔ قفل، پرده‌ها،
/// اعلان‌ها و عنوانِ هر PDF از همین می‌خوانند.
///
/// ⛔ نامِ خام هیچ‌جا دوباره نوشته نمی‌شود؛ ⛔ و شناسه‌های درونی (‎PumpYaqobi.exe‎،
/// پوشهٔ ‎%AppData%\PumpYaqobi‎، ‎User-Agent‎) عمداً دست نخوردند — عوض شدنشان
/// یعنی دفتری که پیدا نمی‌شود و به‌روزرسانی‌ای که نمی‌رسد.
/// </summary>
public static class PumpBrand
{
    /// <summary>نامِ پیش‌فرض — تا کاربر نامِ پمپش را ننوشته.</summary>
    public const string Default = "پمپ بنزین";

    private static string _name = Default;

    /// <summary>نامی که همین حالا دیده می‌شود.</summary>
    public static string Name => _name;

    /// <summary>با عوض شدنِ نام (روی هر نخی).</summary>
    public static event Action? Changed;

    /// <summary>نامِ پمپِ ذخیره‌شده را می‌نشاند؛ خالی یا فقط فاصله ⇒ پیش‌فرض.</summary>
    public static void Set(string? stationName)
    {
        var n = Of(stationName);
        if (n == _name) return;
        _name = n;
        Changed?.Invoke();
    }

    /// <summary>قاعده، خالص: نامِ نوشته‌شده، وگرنه پیش‌فرض. «پمپ یعقوبی»ِ کهنه هم پیش‌فرض است.</summary>
    public static string Of(string? stationName)
    {
        var n = (stationName ?? "").Trim();
        //  ⚠️ نصب‌های پیشین «پمپ یعقوبی» را به‌عنوانِ پیش‌فرض در خانهٔ نام دیده‌اند
        //  (پروفایل آن را می‌نوشت)، نه به‌عنوانِ نامِ واقعیِ پمپ.
        return n.Length == 0 || n == LegacyDefault ? Default : n;
    }

    /// <summary>نامِ پیش‌فرضِ کهنه — فقط برای شناختنش.</summary>
    public const string LegacyDefault = "پمپ یعقوبی";
}
