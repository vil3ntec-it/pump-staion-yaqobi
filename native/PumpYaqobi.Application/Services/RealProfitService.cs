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

    public static RealProfit Compute(IEnumerable<WaraqSaleLine> lines, IReadOnlyList<BuyPrice> prices,
                                     decimal fallbackPetrol, decimal fallbackDiesel, decimal expenses)
    {
        decimal pl = 0, pm = 0, pc = 0, pu = 0, dl = 0, dm = 0, dc = 0, du = 0;
        foreach (var l in lines)
        {
            pl += l.PetrolLiters; pm += l.PetrolMoney;
            dl += l.DieselLiters; dm += l.DieselMoney;
            if (l.PetrolLiters != 0)
            {
                if (PriceAt(prices, FuelType.Petrol, l.DateKey, fallbackPetrol) is { } bp) pc += l.PetrolLiters * bp;
                else pu += l.PetrolLiters;
            }
            if (l.DieselLiters != 0)
            {
                if (PriceAt(prices, FuelType.Diesel, l.DateKey, fallbackDiesel) is { } bd) dc += l.DieselLiters * bd;
                else du += l.DieselLiters;
            }
        }
        return new RealProfit(new RealFuel(pl, pm, pc, pu), new RealFuel(dl, dm, dc, du), expenses);
    }
}

/// <summary>یک تیل: لیتر، پولِ فروش، بهای خرید، و لیترهایی که قیمتِ خرید ندارند.</summary>
public readonly record struct RealFuel(decimal Liters, decimal Sales, decimal Cost, decimal UnpricedLiters)
{
    public decimal PricedLiters => Liters - UnpricedLiters;
    public decimal Gross => Sales - Cost;
    /// <summary>میانگینِ فیِ فروشِ هر لیتر.</summary>
    public decimal SalePerLiter => Liters == 0 ? 0 : Sales / Liters;
    /// <summary>میانگینِ قیمتِ خریدِ هر لیتر (فقط لیترهای قیمت‌دار).</summary>
    public decimal BuyPerLiter => PricedLiters == 0 ? 0 : Cost / PricedLiters;
    /// <summary>سودِ هر لیتر = فیِ فروش − قیمتِ خرید (فقط اگر قیمتِ خرید هست).</summary>
    public decimal ProfitPerLiter => PricedLiters == 0 ? 0 : SalePerLiter - BuyPerLiter;
}

public readonly record struct RealProfit(RealFuel Petrol, RealFuel Diesel, decimal Expenses)
{
    public decimal Sales => Petrol.Sales + Diesel.Sales;
    public decimal Cost => Petrol.Cost + Diesel.Cost;
    public decimal Gross => Sales - Cost;
    public decimal Net => Gross - Expenses;
    public decimal UnpricedLiters => Petrol.UnpricedLiters + Diesel.UnpricedLiters;
}
