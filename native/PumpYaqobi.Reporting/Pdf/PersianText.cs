namespace PumpYaqobi.Reporting.Pdf;

/// <summary>
/// کمک‌کارهای متنِ فارسی برای سند.
/// نسخهٔ HTML اعدادِ فارسی را با ‎n2fa‎ می‌ساخت و همان شکل باید در PDF هم بیاید.
/// </summary>
public static class PersianText
{
    private static readonly string[] Fa = { "۰","۱","۲","۳","۴","۵","۶","۷","۸","۹" };

    /// <summary>‎n2fa‎ — عدد با جداکنندهٔ هزار و ارقامِ فارسی.</summary>
    public static string Num(decimal v, int decimals = 0)
    {
        var s = v.ToString("N" + decimals, System.Globalization.CultureInfo.InvariantCulture);
        return ToFa(s);
    }

    public static string ToFa(string s)
    {
        var b = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) b.Append(c is >= '0' and <= '9' ? Fa[c - '0'] : c.ToString());
        return b.ToString();
    }

    /// <summary>‎toEnDigits‎ — برعکس، برای وقتی که باید عدد را پارس کنیم.</summary>
    public static string ToEn(string s)
    {
        var b = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            b.Append(c is >= '۰' and <= '۹' ? (char)('0' + (c - '۰'))
                   : c is >= '٠' and <= '٩' ? (char)('0' + (c - '٠')) : c);
        return b.ToString();
    }
}
