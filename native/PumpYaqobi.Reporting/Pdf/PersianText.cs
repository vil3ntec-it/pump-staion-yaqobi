namespace PumpYaqobi.Reporting.Pdf;

/// <summary>
/// قالبِ عددها در سندهای چاپ/PDF.
///
/// ⚠️ عمداً ارقامِ لاتین‌اند، نه فارسی: ‎n2fa‎ در نسخهٔ وب دقیقاً
/// ‎Number(n).toLocaleString('en-US')‎ است، پس ورقِ چاپ همیشه «3,356» نشان
/// می‌دهد نه «۳٬۳۵۶». یک‌بار فارسی نوشته شد و ورق با نسخهٔ وب فرق کرد.
/// </summary>
public static class PersianText
{
    /// <summary>‎n2fa(n)‎ — عدد با جداکنندهٔ هزار، ارقامِ لاتین.</summary>
    public static string Num(decimal v, int decimals = 0)
    {
        // عددِ صحیح، اعشارِ بی‌مصرف نگیرد (مثلِ خودِ toLocaleString)
        if (decimals == 0 && v != decimal.Truncate(v))
            return v.ToString("#,##0.##", System.Globalization.CultureInfo.InvariantCulture);
        return v.ToString("N" + decimals, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>‎_tonFmt‎ — وزنِ تن همیشه با سه رقمِ اعشار.</summary>
    public static string Ton(decimal ton) =>
        ton.ToString("N3", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>‎toEnDigits‎ — رقمِ فارسی/عربی ← لاتین، برای تجزیهٔ ورودی.</summary>
    public static string ToEn(string s)
    {
        var b = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            b.Append(c is >= '\u06F0' and <= '\u06F9' ? (char)('0' + (c - '\u06F0'))
                   : c is >= '\u0660' and <= '\u0669' ? (char)('0' + (c - '\u0660')) : c);
        return b.ToString();
    }
}
