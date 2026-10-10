namespace PumpYaqobi.Application.Services;

/// <summary>یک مخزن برای محاسبه — شناسه، شماره (ترتیبِ کشیدن) و ظرفیت.</summary>
public readonly record struct TankDef(long Id, int Num, decimal Capacity);

/// <summary>آمدنِ تیل به مخزن در یک روز. ‎TankId = null‎ ⇒ کوچک‌ترین مخزن (خریدِ تقسیم‌نشده).</summary>
public readonly record struct TankIn(int DateKey, long? TankId, decimal Liters);

/// <summary>
/// رفتنِ تیل در یک روز — فروشِ پارچه‌ها (مثبت)، یا اصلاحِ میله‌زنی (‎Liters‎ منفی یعنی مخزن
/// بیشتر داشت و به مخزنِ در حالِ کشیدن برمی‌گردد).
/// </summary>
public readonly record struct TankOut(int DateKey, decimal Liters);

/// <summary>حالِ یک مخزن پس از همهٔ رفت‌وآمدها.</summary>
public readonly record struct TankLevel(long Id, int Num, decimal Capacity, decimal Current, bool Active)
{
    /// <summary>برای نمایش: منفی ⇒ صفر.</summary>
    public decimal Display => Current < 0m ? 0m : Current;
    public decimal Percent => Capacity > 0m ? Math.Min(100m, Math.Max(0m, Display / Capacity * 100m)) : 0m;
}

/// <summary>
/// ══ تقسیمِ موجودیِ یک تیل میانِ مخزن‌های شماره‌دار (۱۴۰۵/۰۷/۲۲) ══════════════════
/// خواستهٔ صاحب ریپو: «خرید را در مخزن‌های مختلف می‌برم… تیل از اولی کم می‌شود، کم شد هشدار
/// می‌رود، و وقتی تمام شد روی مخزنِ دوم می‌رود و از آن‌جا می‌دهد.»
///
///   • هر روز اول آمدن‌ها (خرید) و بعد رفتن‌ها (فروش) — به ترتیبِ تاریخ.
///   • فروش از **کوچک‌ترین شماره‌ای که تیل دارد** کشیده می‌شود؛ تمام شد ⇒ مخزنِ بعدی.
///   • ⛔ جمعِ مخزن‌ها همیشه همان موجودیِ کلِ همیشگی است (خرید − فروش ± میله‌زنی): کسریِ
///     بیش از همهٔ تیلِ موجود روی مخزنِ اول منفی می‌نشیند، نه این‌که گم شود.
///   • خالص است و هیچ چیزی نمی‌نویسد.
/// </summary>
public static class TankSplitService
{
    public static List<TankLevel> Split(IReadOnlyList<TankDef> tanks, IEnumerable<TankIn> ins, IEnumerable<TankOut> outs)
    {
        var order = tanks.OrderBy(t => t.Num).ThenBy(t => t.Id).ToList();
        if (order.Count == 0) return new List<TankLevel>();
        var level = order.ToDictionary(t => t.Id, _ => 0m);
        var first = order[0].Id;

        long ActiveId() => order.FirstOrDefault(t => level[t.Id] > 0m) is { Id: > 0 } a ? a.Id : first;

        void Take(decimal liters)
        {
            foreach (var t in order)
            {
                if (liters <= 0m) return;
                var have = level[t.Id];
                if (have <= 0m) continue;
                var take = Math.Min(have, liters);
                level[t.Id] = have - take;
                liters -= take;
            }
            if (liters > 0m) level[first] -= liters;
        }

        var days = ins.Select(i => (i.DateKey, Kind: 0, In: (TankIn?)i, Out: (TankOut?)null))
            .Concat(outs.Select(o => (o.DateKey, Kind: 1, In: (TankIn?)null, Out: (TankOut?)o)))
            .OrderBy(e => e.DateKey).ThenBy(e => e.Kind);
        foreach (var e in days)
        {
            if (e.In is { } i)
            {
                var id = i.TankId is { } tid && level.ContainsKey(tid) ? tid : first;
                level[id] += i.Liters;
            }
            else if (e.Out is { } o)
            {
                if (o.Liters >= 0m) Take(o.Liters);
                else level[ActiveId()] += -o.Liters;
            }
        }

        var active = ActiveId();
        return order.Select(t => new TankLevel(t.Id, t.Num, t.Capacity, level[t.Id], t.Id == active)).ToList();
    }
}
