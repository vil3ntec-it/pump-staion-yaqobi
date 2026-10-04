using System.Text.RegularExpressions;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ پیامِ خطا برای کاربر — و گزارشِ خطا برای سرور ═════════════════════════
///
/// ⛔ <b>متنِ خامِ استثنا به کاربر نمی‌رسد.</b> <c>ex.Message</c>ِ دات‌نت و
/// کتابخانه‌ها انگلیسی است و می‌تواند مسیرِ کاملِ فایل (با نامِ کاربرِ
/// ویندوز)، نامِ میزبان، نشانیِ سرور یا جزئیاتِ SQL داشته باشد — همان
/// قاعده‌ای که از ۱۴۰۵/۰۶/۳۰ برای ورود نوشته شده («پیامِ خامِ استثنا به کاربر
/// نمی‌رسد») و تا امروز در توست‌ها و چند صفحه رعایت نمی‌شد.
///
/// پس هر خطا به یک جملهٔ ثابتِ فارسی بر اساسِ <b>نوعش</b> ترجمه می‌شود، و
/// متنِ خام فقط در <c>crash.log</c>ِ همین کامپیوتر می‌ماند.
///
/// ⚠️ <b>یک استثنا</b>: پیامی که <b>خودِ این برنامه</b> برای کاربر نوشته
/// (مثلاً «رمزِ فعلی درست نیست» یا «حساب پیدا نشد») — یعنی استثنایی که از
/// اسمبلی‌های <c>PumpYaqobi</c> آمده و متنش فارسی است — همان‌طور نشان داده
/// می‌شود. آن جمله‌ها از اول برای چشمِ کاربر نوشته شده‌اند و ترجمه‌شان به یک
/// جملهٔ کلی فقط کمکی را که می‌کردند می‌برد.
/// </summary>
public static class ErrorText
{
    public const string FileProblem =
        "فایل در دسترس نیست — شاید برنامهٔ دیگری بازش کرده یا اجازهٔ نوشتن در آن پوشه نیست.";

    public const string DatabaseProblem =
        "دیتابیس همین حالا جواب نداد — دوباره امتحان کنید؛ اگر ماند، برنامه را ببندید و باز کنید.";

    public const string NetworkProblem =
        "به سرور نرسیدیم — اینترنت یا شبکه را بررسی کنید و دوباره بزنید.";

    public const string Cancelled = "کار نیمه‌کاره ماند — دوباره بزنید.";

    public const string Generic =
        "این کار انجام نشد — جزئیاتش در گزارشِ خطای همین کامپیوتر ثبت شد.";

    /// <summary>کوتاه‌ترین جملهٔ فارسی‌ای که به دردِ کاربر می‌خورد — بی هیچ متنِ خام.</summary>
    public static string Friendly(Exception? ex)
    {
        var e = Unwrap(ex);
        if (e is null) return Generic;

        //  پیامِ خودِ این برنامه، به فارسی — همان‌طور
        if (Ours(e)) return e.Message.Trim();

        for (var x = e; x is not null; x = x.InnerException)
        {
            var name = x.GetType().Name;
            if (name is "SqliteException" or "DbUpdateException" or "DbUpdateConcurrencyException")
                return DatabaseProblem;
            if (x is HttpRequestException or System.Net.Sockets.SocketException
                  or System.Net.WebSockets.WebSocketException or TimeoutException)
                return NetworkProblem;
            if (x is IOException or UnauthorizedAccessException) return FileProblem;
        }
        if (e is OperationCanceledException) return Cancelled;
        return Generic;
    }

    private static Exception? Unwrap(Exception? ex)
    {
        var e = ex;
        while (e is AggregateException a && a.InnerException is not null) e = a.InnerException;
        while (e is System.Reflection.TargetInvocationException t && t.InnerException is not null) e = t.InnerException;
        return e;
    }

    /// <summary>
    /// این پیام را خودِ برنامه برای کاربر نوشته؟ — استثنایی از اسمبلی‌های
    /// خودمان، با متنی که حرفِ فارسی دارد. (پیامِ انگلیسیِ یک کتابخانه که از
    /// لایهٔ ما رد شده، این شرط را ندارد.)
    /// </summary>
    private static bool Ours(Exception e) =>
        (e.Source ?? "").StartsWith("PumpYaqobi", StringComparison.Ordinal)
        && e.Message.Length is > 0 and < 300
        && e.Message.Any(c => c is >= '؀' and <= 'ۿ');

    // ══ گزارش به سرور — بی هیچ چیزِ شناسایی‌کننده ═══════════════════════════

    /// <summary>
    /// متنِ خام (پیام یا ردپا) برای <b>گزارشِ خطا به سرور</b>: مسیرِ پوشهٔ
    /// کاربر، نامِ کامپیوتر و نامِ کاربرِ ویندوز جایشان را به نشانه‌ای ثابت
    /// می‌دهند (<c>%USERPROFILE%</c> · <c>%COMPUTERNAME%</c> · <c>%USERNAME%</c>).
    ///
    /// ⚠️ ردپای دات‌نت مسیرِ فایل‌ها را دارد و مسیرِ پوشهٔ کاربر نامِ کاربرِ
    /// ویندوز را — یعنی بی این، هر گزارشِ خطا می‌گفت این پمپ مالِ کیست.
    /// </summary>
    public static string Scrub(string? text)
    {
        var t = text ?? "";
        if (t.Length == 0) return t;
        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (profile.Length > 3)
            {
                t = t.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
                t = t.Replace(profile.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            }
            t = Word(t, Environment.MachineName, "%COMPUTERNAME%");
            t = Word(t, Environment.UserName, "%USERNAME%");
        }
        catch { /* پاک‌سازیِ ناتمام بهتر از نفرستادنِ هیچ است — ولی بیشتر از این نه */ }
        return t;
    }

    private static string Word(string text, string? name, string mark)
    {
        //  نامِ خیلی کوتاه (مثلاً «a») بخشی از هر واژه‌ای است؛ آن را دست نمی‌زنیم
        if (string.IsNullOrWhiteSpace(name) || name.Length < 3) return text;
        return Regex.Replace(text, @"(?<![\p{L}\p{N}_])" + Regex.Escape(name) + @"(?![\p{L}\p{N}_])",
                             mark, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
