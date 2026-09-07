using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;

namespace PumpYaqobi.Application.Localization;

/// <summary>
/// ══ تاریخ و عددِ فارسی ═════════════════════════════════════════════════════
/// همان قالبی که کاربر سال‌ها با آن کار کرده: <c>1404/06/15</c>.
///
/// ⚠️ مرتب‌سازی و فیلتر روی <see cref="Key"/>ِ عددی انجام می‌شود، نه روی رشته —
/// چون مقایسهٔ رشته‌ای «1404/6/9» را بعد از «1404/06/10» می‌گذارد. این باگ در
/// نسخهٔ HTML بود و اینجا با کلیدِ عددی از ریشه بسته شده است.
/// </summary>
public static class Shamsi
{
    private static readonly PersianCalendar Cal = new();

    public static string Of(DateTime d) =>
        $"{Cal.GetYear(d):0000}/{Cal.GetMonth(d):00}/{Cal.GetDayOfMonth(d):00}";

    public static string Today() => Of(DateTime.Now);

    public static string MonthOf(DateTime d) => $"{Cal.GetYear(d):0000}/{Cal.GetMonth(d):00}";

    public static string ThisMonth() => MonthOf(DateTime.Now);

    /// <summary>ارقامِ فارسی/عربی را به لاتین برمی‌گرداند تا تجزیه شکست نخورد.</summary>
    public static string ToEnDigits(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));      // فارسی
            else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠'))); // عربی
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>«1404/06/15» → 14040615. رشتهٔ ناقص یا خراب → صفر.</summary>
    public static int Key(string? shamsi)
    {
        var s = ToEnDigits(shamsi).Trim();
        if (s.Length == 0) return 0;
        var parts = s.Split('/', '-', '.');
        if (parts.Length < 3) return 0;
        if (!int.TryParse(parts[0], out var y) ||
            !int.TryParse(parts[1], out var m) ||
            !int.TryParse(parts[2], out var d)) return 0;
        if (y is < 1000 or > 9999 || m is < 1 or > 12 || d is < 1 or > 31) return 0;
        return y * 10000 + m * 100 + d;
    }

    public static int Key(DateTime d) =>
        Cal.GetYear(d) * 10000 + Cal.GetMonth(d) * 100 + Cal.GetDayOfMonth(d);

    /// <summary>
    /// ‎_shDateKey(s)‎ — کلیدِ «تقریباً روز»: سال×۳۷۲ + ماه×۳۱ + روز.
    ///
    /// ⚠️ این با <see cref="Key(string)"/> یکی نیست و نباید یکی شود: آن برای
    /// مرتب‌سازی است، این برای **تفریق**. «چند روز است این قرض‌دار هیچ ردیفی
    /// نداشته» از تفاضلِ همین دو کلید درمی‌آید، پس ماه باید ۳۱ واحد باشد نه
    /// ۱۰۰ — وگرنه هر ماه هفتاد روز به سنِ قرض اضافه می‌شد.
    ///
    /// تاریخِ خالی یا خراب صفر می‌دهد، همان‌طور که در نسخهٔ وب بود.
    /// </summary>
    public static int DayKey(string? shamsi)
    {
        var m = DayKeyRx.Match(ToEnDigits(shamsi));
        return m.Success
            ? int.Parse(m.Groups[1].Value) * 372
              + int.Parse(m.Groups[2].Value) * 31
              + int.Parse(m.Groups[3].Value)
            : 0;
    }

    private static readonly Regex DayKeyRx = new(@"(\d{4})\D+(\d{1,2})\D+(\d{1,2})");

    /// <summary>«1404/06/15» → «1404/06». رشتهٔ خراب → رشتهٔ خالی.</summary>
    public static string MonthKey(string? shamsi)
    {
        var k = Key(shamsi);
        return k == 0 ? "" : $"{k / 10000:0000}/{k / 100 % 100:00}";
    }

    private static readonly string[] DayNames =
        { "یک‌شنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنج‌شنبه", "جمعه", "شنبه" };

    public static string DayName(DateTime d) => DayNames[(int)d.DayOfWeek];

    public static readonly string[] MonthNames =
    {
        "حمل", "ثور", "جوزا", "سرطان", "اسد", "سنبله",
        "میزان", "عقرب", "قوس", "جدی", "دلو", "حوت",
    };

    public static string MonthName(int m) => m is >= 1 and <= 12 ? MonthNames[m - 1] : "";

    /// <summary>عددِ پول با جداکنندهٔ هزارگان، بدونِ اعشارِ بی‌مصرف.</summary>
    public static string Money(decimal v) =>
        v == decimal.Truncate(v)
            ? decimal.Truncate(v).ToString("#,##0", CultureInfo.InvariantCulture)
            : v.ToString("#,##0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// عددِ پول با شمارِ اعشارِ ثابت — همتای ‎n2fa(x.toFixed(d))‎ نسخهٔ وب،
    /// که «۰» را هم «0.0» می‌نویسد.
    /// </summary>
    public static string Money(decimal v, int decimals) =>
        v.ToString("#,##0." + new string('0', decimals), CultureInfo.InvariantCulture);

    /// <summary>مقدارِ تایپ‌شده → عدد. مثل <c>parseFloat(x)||0</c>ِ نسخهٔ وب.</summary>
    public static decimal Num(string? s)
    {
        var t = ToEnDigits(s).Replace(",", "").Replace("٬", "").Trim();
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}
