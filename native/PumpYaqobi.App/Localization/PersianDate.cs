using System.Globalization;

namespace PumpYaqobi.App.Localization;

/// <summary>
/// تاریخِ شمسی — همان قالبی که در برنامه استفاده می‌شود: <c>1404/06/15</c>.
/// از <see cref="PersianCalendar"/>ِ خودِ دات‌نت می‌آید، نه از محاسبهٔ دستی.
/// </summary>
public static class PersianDate
{
    private static readonly PersianCalendar Cal = new();

    public static string Of(DateTime d) =>
        $"{Cal.GetYear(d):0000}/{Cal.GetMonth(d):00}/{Cal.GetDayOfMonth(d):00}";

    public static string Today() => Of(DateTime.Now);

    public static int KeyOf(DateTime d) =>
        Cal.GetYear(d) * 10000 + Cal.GetMonth(d) * 100 + Cal.GetDayOfMonth(d);

    private static readonly string[] Days =
        { "یک‌شنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنج‌شنبه", "جمعه", "شنبه" };

    public static string NowLabel()
    {
        var n = DateTime.Now;
        return $"{Days[(int)n.DayOfWeek]}  {Of(n)}  ·  {n:HH:mm:ss}";
    }
}
