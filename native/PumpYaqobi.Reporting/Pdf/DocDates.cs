using System.Globalization;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>
/// ══ تاریخِ پانویسِ سندها ════════════════════════════════════════════════════
/// رونوشتِ ‎allDates()‎ نسخهٔ وب: شمسی · قمری · میلادی، با همان جداکنندهٔ «·».
///
/// ⚠️ قمری از تقویمِ «ام‌القُری» گرفته می‌شود، همان چیزی که نسخهٔ وب با
/// ‎'en-US-u-ca-islamic-umalqura'‎ می‌خواست — نه <c>HijriCalendar</c>ِ ساده که
/// گاهی یک روز فرق می‌کند.
/// </summary>
public static class DocDates
{
    private static readonly PersianCalendar Fa = new();
    private static readonly UmAlQuraCalendar Qa = new();

    /// <summary>«1405/06/15  ·  1448/03/24  ·  2026/09/06»</summary>
    public static string Line(DateTime? when = null)
    {
        var d = when ?? DateTime.Now;
        return Shamsi(d) + "  ·  " + Qamari(d) + "  ·  " + Miladi(d);
    }

    public static string Shamsi(DateTime d) =>
        $"{Fa.GetYear(d):0000}/{Fa.GetMonth(d):00}/{Fa.GetDayOfMonth(d):00}";

    public static string Miladi(DateTime d) => $"{d.Year:0000}/{d.Month:00}/{d.Day:00}";

    /// <summary>
    /// تاریخِ قمری. بازهٔ تقویمِ ام‌القُری محدود است (۱۹۰۰ تا ۲۰۷۷ میلادی)؛
    /// بیرونِ آن به‌جای انداختنِ خطا خالی برمی‌گردد — دقیقاً مثلِ نسخهٔ وب که
    /// در مرورگرِ ناآشنا ‎null‎ می‌داد و پانویس بی‌قمری چاپ می‌شد.
    /// </summary>
    public static string Qamari(DateTime d)
    {
        try { return $"{Qa.GetYear(d):0000}/{Qa.GetMonth(d):00}/{Qa.GetDayOfMonth(d):00}"; }
        catch (ArgumentOutOfRangeException) { return "—"; }
    }
}
