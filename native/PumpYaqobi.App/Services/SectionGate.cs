using System.Collections.Generic;
using PumpYaqobi.Application.Security;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ بخش‌هایی که موقتاً از دسترس بیرون‌اند (۱۴۰۵/۰۷/۱۵) ═════════════════════
/// خواستهٔ صریحِ صاحب ریپو: «بخشِ تیل امانت را فعلاً غیرِ قابلِ دسترسی کن و
/// بگو در حالِ ساخت است… با دو سه بار زدن رویش یک رمز بیاید، رمز ۶۰۰۸، برای
/// خودم دیده شود… و بخشِ دوربین‌ها را غیرفعال کن: توی برنامه باشد ولی دیده
/// نشود.»
///
/// ⛔ <b>تنها جای این تصمیم همین‌جاست.</b> هیچ داده‌ای پاک نمی‌شود، هیچ منطقی
/// خاموش نمی‌شود و خودِ بخش‌ها سرِ جایشان در ‎MainViewModel.Sections‎ می‌مانند —
/// فقط درِ ورود بسته است. برگرداندن = برداشتنِ شناسه از این دو فهرست.
///
///  • <see cref="Hidden"/>  — در نوار نیست و ‎GoAsync‎ بازش نمی‌کند (دوربین‌ها).
///  • <see cref="Building"/> — در نوار هست، ولی زدنش فقط «در حالِ ساخت» می‌گوید؛
///    سه بار زدنِ پشتِ سرِ هم ⇒ رمزِ توسعه ⇒ برای همین اجرا باز.
///
/// ⚠️ رمز خام این‌جا نیست؛ فقط هشِ PBKDF2ِ همان ‎PasswordHasher‎ برنامه.
/// ⚠️ «باز بودن» در حافظه است، نه روی دیسک: اجرای بعدی دوباره می‌پرسد.
/// </summary>
public static class SectionGate
{
    /// <summary>فقط سنجه‌ها و آزمون‌ها: همه‌چیز باز، تا همان بخش‌ها سنجیده شوند.</summary>
    public static bool TestOpenAll { get; set; }

    public static readonly IReadOnlySet<string> Hidden = new HashSet<string> { "cameras" };
    public static readonly IReadOnlySet<string> Building = new HashSet<string> { "amanat" };

    /// <summary>چند بار زدنِ پشتِ سرِ هم تا رمز پرسیده شود.</summary>
    public const int TapsForPin = 3;
    /// <summary>فاصلهٔ بیشینهٔ دو زدن تا «پشتِ سرِ هم» شمرده شود.</summary>
    public static readonly TimeSpan TapWindow = TimeSpan.FromSeconds(4);

    public const string BuildingText =
        "🚧 این بخش در حالِ ساخت و بهتر شدن است — در به‌روزرسانی‌های بعدی برمی‌گردد";

    //  هشِ رمزِ توسعه (PBKDF2-SHA256، ۲۱۰٬۰۰۰ دور) — رمزِ خام در کد نیست
    private const string DevPinHash =
        "pbkdf2$sha256$210000$Nz5g71TLiWPSQ3GiVNd7HA==$KNQ9Y6O5Y23LdkNaTyrRM7L9rGDiZsy37nd6Tiio3n8=";

    private static bool _devOpen;
    private static int _taps;
    private static DateTime _lastTap;
    private static int _wrong;
    private static DateTime _waitUntil;

    public static bool IsHidden(string id) => !TestOpenAll && Hidden.Contains(id);

    public static bool IsBuilding(string id) => !TestOpenAll && !_devOpen && Building.Contains(id);

    /// <summary>یک زدن روی بخشِ در حالِ ساخت؛ شمارِ زدن‌های پشتِ سرِ هم را برمی‌گرداند.</summary>
    public static int Tap(DateTime now)
    {
        if (now - _lastTap > TapWindow) _taps = 0;
        _lastTap = now;
        return ++_taps;
    }

    public static void ResetTaps() => _taps = 0;

    /// <summary>ثانیه‌های ماندهٔ ترمزِ حدس (پس از پنج رمزِ نادرست).</summary>
    public static int WaitSeconds(DateTime now) =>
        now < _waitUntil ? (int)Math.Ceiling((_waitUntil - now).TotalSeconds) : 0;

    /// <summary>رمزِ توسعه؛ درست ⇒ بخش‌های در حالِ ساخت برای همین اجرا باز می‌شوند.</summary>
    public static bool TryDevUnlock(string? pin, DateTime now)
    {
        if (WaitSeconds(now) > 0) return false;
        pin = Latin(pin ?? "").Trim();
        if (PasswordHasher.Verify(pin, DevPinHash)) { _devOpen = true; _wrong = 0; return true; }
        if (++_wrong >= 5) { _waitUntil = now.AddSeconds(60); _wrong = 0; }
        return false;
    }

    //  رقمِ فارسی و عربی هم پذیرفته است — صفحه‌کلیدِ فارسی «۶۰۰۸» می‌زند
    private static string Latin(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c is >= '\u06F0' and <= '\u06F9' ? (char)('0' + (c - '\u06F0'))
                    : c is >= '\u0660' and <= '\u0669' ? (char)('0' + (c - '\u0660'))
                    : c);
        return sb.ToString();
    }

    /// <summary>فقط آزمون‌ها: حالِ حافظه را از نو.</summary>
    public static void ResetForTests()
    {
        _devOpen = false; _taps = 0; _lastTap = default; _wrong = 0; _waitUntil = default;
    }
}
