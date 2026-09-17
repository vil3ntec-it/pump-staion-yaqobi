namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ همان قاعده‌هایی که سرور دارد — ولی پیش از رفتن ══════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «قبل از ارسالِ درخواست، ایمیل بررسی شود، فیلدهای
/// ضروری بررسی شوند، فاصله‌های اضافی مدیریت شوند… <b>اما هرگز فقط به
/// اعتبارسنجیِ فرانت‌اند اعتماد نکن؛ تمامِ اعتبارسنجی‌های مهم باید دوباره در
/// بک‌اند انجام شوند.</b>»
///
/// پس این‌جا هیچ تصمیمِ امنیتی‌ای گرفته نمی‌شود — سرور هر کدام را دوباره
/// می‌سنجد (<c>v.email</c> و <c>pw.checkStrength</c>). کارِ این‌ها فقط این
/// است که کاربر برای یک اشتباهِ دیدنی منتظرِ رفت‌وبرگشتِ شبکه نماند و پیامِ
/// فارسیِ روشن ببیند.
///
/// ⚠️ متن‌ها عمداً <b>همان چیزی</b> است که سرور می‌گوید
/// (<c>shop/server/src/lib/password.js</c>)، وگرنه کاربر دو پیامِ متفاوت
/// برای یک اشتباه می‌دید.
/// </summary>
public static class LoginRules
{
    /// <summary>
    /// ایرادِ ایمیل، یا <c>null</c> اگر ایرادی نبود.
    ///
    /// ⚠️ قاعدهٔ قبلی «‎@‎ داشته باشد و به ‎@‎ ختم نشود» بود و این‌ها را
    /// می‌پذیرفت: <c>@x.com</c> (بی نامِ کاربر)، <c>a@b</c> (بی دامنه) و
    /// <c>a b@c.com</c> (با فاصله). هر سه را سرور رد می‌کرد و کاربر یک
    /// خطای گنگ می‌گرفت.
    /// </summary>
    public static string? BadEmail(string? email)
    {
        var s = (email ?? "").Trim();
        if (s.Length == 0) return "ایمیل را بنویسید.";
        if (s.Any(char.IsWhiteSpace)) return "ایمیل نباید فاصله داشته باشد.";

        var at = s.IndexOf('@');
        if (at <= 0 || at != s.LastIndexOf('@')) return "ایمیل درست نیست — باید یک «@» داشته باشد.";

        var host = s[(at + 1)..];
        //  دامنه باید نقطه‌ای داشته باشد که نه اولش است نه آخرش — یعنی
        //  «‎a@b‎» و «‎a@b.‎» رد می‌شوند ولی «‎a@b.co‎» می‌گذرد.
        var dot = host.IndexOf('.');
        if (dot <= 0 || dot == host.Length - 1) return "ایمیل درست نیست — دامنه‌اش کامل نیست.";
        return null;
    }

    /// <summary>
    /// ایرادِ رمز، یا <c>null</c>. رونوشتِ <c>pw.checkStrength</c>ِ سرور.
    ///
    /// ⚠️ تا امروز برنامه فقط بلندی را می‌سنجید، پس رمزِ «۱۲۳۴۵۶۷۸» از
    /// این‌جا رد می‌شد و سرور با <c>weak_password</c> برش می‌گرداند.
    /// </summary>
    public static string? WeakPassword(string? plain)
    {
        var s = plain ?? "";
        if (s.Length < 8) return "رمز دستِ‌کم هشت نویسه باشد.";
        if (s.All(char.IsDigit)) return "رمز نباید فقط عدد باشد.";
        if (Weak.Contains(s.ToLowerInvariant())) return "این رمز خیلی ساده است.";
        return null;
    }

    private static readonly HashSet<string> Weak = new(StringComparer.Ordinal)
    { "password", "12345678", "qwertyui", "admin123", "11111111" };
}
