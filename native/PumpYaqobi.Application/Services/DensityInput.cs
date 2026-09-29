using System.Globalization;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// ══ «ثقلت» بی نقطه (۱۴۰۵/۰۷/۱۷) ═══════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «۰.۷۳۰ را این مدلی نمی‌خواهم بزنم؛ می‌خواهم ۰۷۳۰ را
/// بزنم و آن نقطهٔ وسطِ صفر و هفت خودش بیاید.» ثقلتِ تیل همیشه کمتر از یک
/// است (۰٫۷ تا ۰٫۹)، پس رقم‌هایی که با صفر شروع می‌شوند یا بزرگ‌تر از ده‌اند
/// و نقطه ندارند، بی‌ابهام «صفر ممیز همان رقم‌ها»یند.
///
/// ⚠️ ثقلتی که کاربر با نقطه نوشته هیچ‌وقت دست نمی‌خورد، و حسابِ خرید
/// (‎PurchaseCalc.Compute‎) هیچ تغییری نکرد — این فقط خواندنِ کادر است.
/// </summary>
public static class DensityInput
{
    /// <summary>
    /// متنی که کادر هنگامِ تایپ نشان می‌دهد: «07» ⇐ «0.7»، «0730» ⇐ «0.730».
    /// «0» تنها همان «0» می‌ماند تا رقمِ دوم بیاید؛ متنِ نقطه‌دار دست نمی‌خورد.
    /// </summary>
    public static string Typed(string? raw)
    {
        var s = Shamsi.ToEnDigits(raw ?? "").Trim().Replace('٫', '.');
        if (s.Length < 2 || s.Contains('.')) return s;
        if (s[0] == '0' && s.All(char.IsDigit)) return "0." + s[1..];
        return s;
    }

    /// <summary>عددِ ثقلت از متنِ کادر — با همان قاعده، برای متنِ چسبانده یا بی‌نقطه.</summary>
    public static decimal Parse(string? raw)
    {
        var s = Shamsi.ToEnDigits(raw ?? "").Trim().Replace('٫', '.');
        if (s.Length == 0) return 0m;
        if (!s.Contains('.'))
        {
            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && (digits[0] == '0' ? digits.Length > 1 : digits.Length >= 2))
            {
                var frac = digits.TrimStart('0');
                if (digits[0] != '0') frac = digits;
                var lead = digits[0] == '0' ? digits.Length - 1 : digits.Length;
                if (decimal.TryParse(frac.Length == 0 ? "0" : frac, NumberStyles.None,
                                     CultureInfo.InvariantCulture, out var n))
                    return n / Pow10(lead);
            }
        }
        return Shamsi.Num(s);
    }

    private static decimal Pow10(int n)
    {
        var p = 1m;
        for (var i = 0; i < n; i++) p *= 10m;
        return p;
    }
}
