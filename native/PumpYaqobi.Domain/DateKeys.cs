namespace PumpYaqobi.Domain;

/// <summary>
/// ══ شورا، ج۶ — تاریخ یک شکلِ کانونی ═════════════════════════════════════════
///
/// هر ردیف سه ستونِ تاریخ دارد: <c>DateShamsi</c> (همان متنی که کاربر نوشت)،
/// <c>DateKey</c> (‎14050720‎) و گاهی <c>MonthKey</c> (‎1405/07‎). تا امروز آن دو
/// کلید در سی‌وچند جای کد، هر کدام جدا، از متن ساخته می‌شدند — و هر راهی که
/// فراموش می‌کرد، ردیفی با کلیدِ کهنه یا صفر می‌گذاشت که در ماهِ خودش دیده
/// نمی‌شد.
///
/// ⛔ <b>این‌جا تنها قاعدهٔ «متن ⇒ کلید» است.</b> <c>PumpDbContext</c> با هر
/// ذخیره کلیدها را از متنِ همان ردیف می‌سازد (<c>DateKey</c> منبعِ حقیقتِ
/// مرتب‌سازی و ماه، <c>MonthKey</c> مشتقِ آن) و <c>Shamsi.Key</c>/<c>MonthKey</c>
/// همین را صدا می‌زنند. متنِ کاربر هرگز عوض نمی‌شود.
/// </summary>
public static class DateKeys
{
    /// <summary>«1404/06/15» (یا با رقمِ فارسی/عربی، «-» یا «.») ⇒ ‎14040615‎؛ خراب ⇒ ‎0‎.</summary>
    public static int Key(string? shamsi)
    {
        if (string.IsNullOrEmpty(shamsi)) return 0;
        var chars = new char[shamsi.Length];
        for (var i = 0; i < shamsi.Length; i++)
        {
            var ch = shamsi[i];
            chars[i] = ch is >= '۰' and <= '۹' ? (char)('0' + (ch - '۰'))
                     : ch is >= '٠' and <= '٩' ? (char)('0' + (ch - '٠'))
                     : ch;
        }
        var s = new string(chars).Trim();
        if (s.Length == 0) return 0;
        var parts = s.Split('/', '-', '.');
        if (parts.Length < 3) return 0;
        if (!int.TryParse(parts[0], out var y) ||
            !int.TryParse(parts[1], out var m) ||
            !int.TryParse(parts[2], out var d)) return 0;
        if (y is < 1000 or > 9999 || m is < 1 or > 12 || d is < 1 or > 31) return 0;
        return y * 10000 + m * 100 + d;
    }

    /// <summary>‎14040615‎ ⇒ «1404/06»؛ صفر ⇒ «».</summary>
    public static string Month(int key) =>
        key == 0 ? "" : $"{key / 10000:0000}/{key / 100 % 100:00}";
}
