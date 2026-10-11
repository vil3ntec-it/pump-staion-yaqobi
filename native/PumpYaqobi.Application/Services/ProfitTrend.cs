namespace PumpYaqobi.Application.Services;

/// <summary>یک مبلغِ تاریخ‌دار — درآمدِ اضافی یا مصرف — فقط همان دو ستون.</summary>
public readonly record struct DatedAmount(int DateKey, decimal Amount);

/// <summary>یک نقطهٔ نمودارِ مفاد/ضرر: برچسب و مفادِ خالصِ همان روز یا ماه.</summary>
public readonly record struct TrendPoint(string Label, decimal Net);

/// <summary>
/// ══ نمودارِ «مفاد / ضرر خالص» — از عددهای واقعی (۱۴۰۵/۰۷/۲۲) ══
///  صاحب ریپو: «شکلِ چارت خیلی داغون است و به درد نخور». نمودارِ کادرِ بالا تا امروز
///  سیزده عددِ ثابتِ تزئینی بود (صعودی برای مفاد، نزولی برای ضرر) — هیچ ربطی به داده نداشت.
///  حالا هر نقطه = فروشِ ورق‌ها (پطرول + دیزل) + درآمدِ اضافی − مصارفِ همان روز.
///  ⛔ فرمولِ مفاد عوض نشد: همان سه جزءِ ‎ProfitLossService.Breakdown‎، فقط روز به روز.
///  «بی‌فاکتور» و «فرقِ نرخِ فاکتور» تاریخِ روزانه ندارند و در نقطه‌ها نیستند.
///  دوره‌ای بیش از ۶۲ روز ⇒ هر نقطه یک ماه. ترتیب: تازه‌ترین اول (چپ در نمودارِ راست‌به‌چپ).
/// </summary>
public static class ProfitTrend
{
    public const int MaxDays = 62;

    public static IReadOnlyList<TrendPoint> Build(IEnumerable<WaraqSaleLine> sales,
        IEnumerable<DatedAmount> extras, IEnumerable<DatedAmount> expenses)
    {
        var day = new SortedDictionary<int, decimal>();
        void Add(int key, decimal v) { if (key <= 0) return; day[key] = day.GetValueOrDefault(key) + v; }
        foreach (var l in sales) Add(l.DateKey, l.PetrolMoney + l.DieselMoney);
        foreach (var e in extras) Add(e.DateKey, e.Amount);
        foreach (var e in expenses) Add(e.DateKey, -e.Amount);

        IEnumerable<TrendPoint> pts;
        if (day.Count > MaxDays)
            pts = day.GroupBy(kv => kv.Key / 100)
                     .Select(g => new TrendPoint($"{g.Key / 100}/{g.Key % 100:00}", g.Sum(kv => kv.Value)));
        else
            pts = day.Select(kv => new TrendPoint($"{kv.Key / 100 % 100:00}/{kv.Key % 100:00}", kv.Value));
        return pts.Reverse().ToList();
    }
}
