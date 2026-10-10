using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>یک خریدِ مخزن برای قیمتِ خریدِ هر لیتر — فقط همان چهار ستون.</summary>
public readonly record struct BuyPrice(FuelType Fuel, int DateKey, long Id, decimal PerLiter);

/// <summary>
/// ══ سودِ واقعی (۱۴۰۵/۰۷/۱۸) ═════════════════════════════════════════════════
/// خواستهٔ صاحب ریپو: «سودِ واقعی باید از مبلغِ کلِ فروشِ تیل تفکیک شود… سودِ هر لیتر،
/// هزینه‌ها، سودِ ناخالص و سودِ خالص جداگانه… بر اساسِ قیمتِ خریدِ واقعیِ تیل و
/// هزینه‌های ثبت‌شده، تا مبلغِ فروش با سود اشتباه گرفته نشود.»
///
///   فروش        = لیترِ فروخته‌شدهٔ ورق‌ها × فیِ فروش (همان ‎SalesLinesAsync‎ِ مفاد و ضرر)
///   بهای خرید   = همان لیترها × قیمتِ خریدِ هر لیترِ **همان روز** (‎PriceAt‎)
///   سودِ ناخالص = فروش − بهای خرید
///   سودِ خالص   = سودِ ناخالص − مصارفِ ثبت‌شده
///
/// ⛔ قیمتِ خرید از خریدهای مخزن است، نه نرخِ اتحادیه: برای فروشِ هر روز، تازه‌ترین خریدِ
/// همان تیل تا آن روز؛ اگر پیش از آن خریدی نبود، نخستین خریدِ پس از آن؛ و اگر مخزن هیچ
/// خریدی از آن تیل نداشت، فیِ خریدِ ذخیره‌شده (‎buyPerLiter‎). هیچ‌کدام ⇒ آن لیترها
/// «بی‌قیمتِ خرید» شمرده و جدا گفته می‌شوند — صفر جای قیمت نمی‌نشیند.
/// ⛔ خالص است و هیچ چیزی نمی‌نویسد؛ عددِ کادرِ «مفاد / ضرر خالص» را عوض نمی‌کند.
/// </summary>
public static class RealProfitService
{
    /// <summary>قیمتِ خریدِ هر لیترِ ‎fuel‎ برای فروشِ روزِ ‎dateKey‎ — یا ‎null‎ اگر هیچ قیمتی نیست.</summary>
    public static decimal? PriceAt(IEnumerable<BuyPrice> prices, FuelType fuel, int dateKey, decimal fallback = 0m)
    {
        BuyPrice? before = null, after = null;
        foreach (var p in prices)
        {
            if (p.Fuel != fuel || p.PerLiter <= 0) continue;
            if (p.DateKey <= dateKey || dateKey <= 0)
            {
                if (before is not { } b || (p.DateKey, p.Id).CompareTo((b.DateKey, b.Id)) > 0) before = p;
            }
            else if (after is not { } a || (p.DateKey, p.Id).CompareTo((a.DateKey, a.Id)) < 0) after = p;
        }
        if (before is { } x) return x.PerLiter;
        if (after is { } y) return y.PerLiter;
        return fallback > 0 ? fallback : null;
    }

    /// <summary>
    /// فایدهٔ هر لیتر — فیِ فروش منهای قیمتِ خرید، گرد به یک رقم: **همان** عددِ کادرِ «فایده فی
    /// لیتر (افغانی) — اتومات از مخزن»ِ پارچه (‎ParchaService.CalcShift‎ ⇒ ‎Fixed1‎).
    /// </summary>
    public static decimal ProfitPer(decimal salePerLiter, decimal buyPerLiter) =>
        ParchaService.Fixed1(salePerLiter - buyPerLiter);

    public static RealProfit Compute(IEnumerable<WaraqSaleLine> lines, IReadOnlyList<BuyPrice> prices,
                                     decimal fallbackPetrol, decimal fallbackDiesel, decimal expenses)
    {
        decimal pl = 0, pm = 0, pc = 0, pu = 0, pp = 0, dl = 0, dm = 0, dc = 0, du = 0, dp = 0;
        foreach (var l in lines)
        {
            pl += l.PetrolLiters; pm += l.PetrolMoney;
            dl += l.DieselLiters; dm += l.DieselMoney;
            if (l.PetrolLiters != 0)
            {
                if (PriceAt(prices, FuelType.Petrol, l.DateKey, fallbackPetrol) is { } bp)
                {
                    pc += l.PetrolLiters * bp;
                    pp += l.PetrolLiters * ProfitPer(l.PetrolMoney / l.PetrolLiters, bp);
                }
                else pu += l.PetrolLiters;
            }
            if (l.DieselLiters != 0)
            {
                if (PriceAt(prices, FuelType.Diesel, l.DateKey, fallbackDiesel) is { } bd)
                {
                    dc += l.DieselLiters * bd;
                    dp += l.DieselLiters * ProfitPer(l.DieselMoney / l.DieselLiters, bd);
                }
                else du += l.DieselLiters;
            }
        }
        return new RealProfit(new RealFuel(pl, pm, pc, pu, pp), new RealFuel(dl, dm, dc, du, dp), expenses);
    }

    /// <summary>
    /// نمودارِ «فایده فی لیتر»: هر روزِ فروش یک نقطه — فیِ فروش و فایدهٔ هر لیترِ هر تیل
    /// (همان ‎ProfitPer‎ روی هر شیفت، میانگینِ وزنی با لیتر). تازه‌ترین روز اول (قاعدهٔ
    /// ‎SparkChart‎: «امروز باید اول باشد»)، حداکثر ‎max‎ روز.
    /// </summary>
    public static IReadOnlyList<RealDay> Daily(IEnumerable<WaraqSaleLine> lines, IReadOnlyList<BuyPrice> prices,
                                               decimal fallbackPetrol, decimal fallbackDiesel, int max = 31)
    {
        var days = new List<RealDay>();
        foreach (var g in lines.GroupBy(l => l.DateKey > 0 ? l.DateKey.ToString() : l.DateShamsi)
                               .OrderByDescending(g => g.Max(l => l.DateKey)).Take(max))
        {
            var p = Compute(g, prices, fallbackPetrol, fallbackDiesel, 0m);
            days.Add(new RealDay(g.First().DateShamsi, g.Max(l => l.DateKey), p.Petrol, p.Diesel));
        }
        return days;
    }
}

/// <summary>یک روزِ نمودارِ «فایده فی لیتر».</summary>
public readonly record struct RealDay(string DateShamsi, int DateKey, RealFuel Petrol, RealFuel Diesel);

/// <summary>یک تیل: لیتر، پولِ فروش، بهای خرید، و لیترهایی که قیمتِ خرید ندارند.</summary>
/// <remarks>
/// ‎Profit‎ = جمعِ «لیتر × فایدهٔ فی لیتر»ِ هر شیفت — همان ضربی که پارچه با کادرِ «فایده فی لیتر»
/// می‌کند (‎ShiftCalc.Profit‎)، نه «فروش منهای خرید»ِ بی‌گرد.
/// </remarks>
public readonly record struct RealFuel(decimal Liters, decimal Sales, decimal Cost, decimal UnpricedLiters,
                                       decimal Profit = 0m)
{
    public decimal PricedLiters => Liters - UnpricedLiters;
    public decimal Gross => Sales - Cost;
    /// <summary>میانگینِ فیِ فروشِ هر لیتر.</summary>
    public decimal SalePerLiter => Liters == 0 ? 0 : Sales / Liters;
    /// <summary>میانگینِ قیمتِ خریدِ هر لیتر (فقط لیترهای قیمت‌دار).</summary>
    public decimal BuyPerLiter => PricedLiters == 0 ? 0 : Cost / PricedLiters;
    /// <summary>سودِ هر لیتر = فیِ فروش − قیمتِ خرید (فقط اگر قیمتِ خرید هست).</summary>
    public decimal ProfitPerLiter => PricedLiters == 0 ? 0 : SalePerLiter - BuyPerLiter;
    /// <summary>فایدهٔ هر لیتر همان‌طور که پارچه می‌گوید: ‎Profit ÷ لیترهای قیمت‌دار‎.</summary>
    public decimal ProfitPerLiterBox => PricedLiters == 0 ? 0 : Profit / PricedLiters;
}

public readonly record struct RealProfit(RealFuel Petrol, RealFuel Diesel, decimal Expenses)
{
    public decimal Sales => Petrol.Sales + Diesel.Sales;
    public decimal Cost => Petrol.Cost + Diesel.Cost;
    public decimal Gross => Sales - Cost;
    public decimal Net => Gross - Expenses;
    public decimal UnpricedLiters => Petrol.UnpricedLiters + Diesel.UnpricedLiters;
    /// <summary>فایده = لیتر × فایدهٔ فی لیتر، هر دو تیل — عددِ بزرگِ نمای «سودِ واقعی».</summary>
    public decimal Profit => Petrol.Profit + Diesel.Profit;
}
