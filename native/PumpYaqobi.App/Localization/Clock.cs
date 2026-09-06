using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Localization;

/// <summary>نوشتهٔ ساعت و تاریخِ سربرگ.</summary>
public static class Clock
{
    public static string Now()
    {
        var n = DateTime.Now;
        return $"{Shamsi.DayName(n)}  {Shamsi.Of(n)}  ·  {n:HH:mm:ss}";
    }
}
