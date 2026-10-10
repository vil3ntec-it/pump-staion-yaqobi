namespace PumpYaqobi.Application.Services;

/// <summary>یک ورودِ تیل برای قیمتِ کشیدن — کدام خرید، کِی، چند لیتر، فیِ لیترِ خرید.</summary>
public readonly record struct DrawIn(int DateKey, long PurchaseId, decimal Liters, decimal PerLiter);

/// <summary>
/// ══ فیِ خریدِ «همان خریدی که الان کشیده می‌شود» (۱۴۰۵/۰۷/۲۲) ══
/// خواستهٔ صاحب ریپو: «از همون خریدی که کشیده می‌شه، پطرول و دیزل جدا.» خریدها به ترتیبِ آمدن
/// (روز، بعد شناسه) پشتِ هم‌اند و فروش از قدیمی‌ترین کم می‌شود؛ خریدی که هنوز از آن تیل مانده
/// همان است که کشیده می‌شود و فیِ او فیِ خریدِ امروز است.
/// ⛔ خریدِ بی‌قیمت (فیِ صفر) هیچ‌وقت فی نمی‌شود — خریدِ قیمت‌دارِ بعدی، وگرنه پیشین.
/// همه تمام شده ⇒ تازه‌ترین خریدِ قیمت‌دار. هیچ خریدِ قیمت‌داری ⇒ ‎null‎ (عددِ پیشین می‌ماند).
/// </summary>
public static class DrawPriceService
{
    public static decimal? Current(IEnumerable<DrawIn> ins, decimal consumed)
    {
        var list = ins.Where(i => i.Liters > 0m).OrderBy(i => i.DateKey).ThenBy(i => i.PurchaseId).ToList();
        if (list.Count == 0) return null;
        var left = Math.Max(0m, consumed);
        var at = list.Count;                       // همه تمام شد
        for (var k = 0; k < list.Count; k++)
        {
            if (left < list[k].Liters) { at = k; break; }
            left -= list[k].Liters;
        }
        for (var k = at; k < list.Count; k++) if (list[k].PerLiter > 0m) return list[k].PerLiter;
        for (var k = Math.Min(at, list.Count) - 1; k >= 0; k--) if (list[k].PerLiter > 0m) return list[k].PerLiter;
        return null;
    }
}
