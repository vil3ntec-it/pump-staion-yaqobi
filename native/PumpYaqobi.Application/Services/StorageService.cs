using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

public readonly record struct PurchaseNumbers(
    decimal Ton, decimal Liters, decimal TotalUsd, decimal TotalAfn, decimal PerLiter);

/// <param name="DipAdjust">جمعِ اصلاح‌های میله‌زنی — مثبت یعنی مخزن بیشتر داشت.</param>
/// <param name="HasPurchases">برای این سوخت اصلاً خریدی ثبت شده؟</param>
/// <param name="IsNear">هنوز کم نیامده ولی تا ۲۰٪ بالای حد هشدار مانده.</param>
public readonly record struct TankState(
    decimal In, decimal Out, decimal Current, decimal Display, bool IsLow,
    decimal TotalUsd, decimal TotalAfn, decimal DipAdjust = 0m,
    bool HasPurchases = false, bool IsNear = false);

/// <summary>
/// ══ مخزن ══════════════════════════════════════════════════════════════════
/// رونوشتِ ‎confirmAddPurchase‎ و ‎_renderFuelSection‎.
///
///     تن     = کیلو ÷ ۱۰۰۰
///     لیتر   = کیلو ÷ چگالی        (چگالیِ صفر ⇒ صفر)
///     دالر   = تن × فیِ تن
///     افغانی = دالر × نرخِ دالر
///     فی‌لیتر = افغانی ÷ لیتر       (لیترِ صفر ⇒ صفر)
///
/// ⚠️ موجودیِ منفی برای نمایش صفر می‌شود، ولی خودِ موجودیِ واقعی (که ممکن است
/// منفی باشد) پایهٔ هشدارِ کمبود است — همان تفکیکی که نسخهٔ وب داشت.
/// </summary>
public sealed class StorageService
{
    public PurchaseNumbers Compute(decimal kg, decimal density, decimal priceTon, decimal usdRate)
    {
        var ton = kg / 1000m;
        var liters = density > 0m ? kg / density : 0m;
        var usd = ton * priceTon;
        var afn = usd * usdRate;
        var perLiter = liters > 0m ? afn / liters : 0m;
        return new PurchaseNumbers(ton, liters, usd, afn, perLiter);
    }

    /// <summary>عددهای حساب‌شده را روی خودِ خرید می‌نشاند.</summary>
    public void Apply(FuelPurchase p)
    {
        if (p is null) return;
        var n = Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate);
        p.Ton = n.Ton; p.Liters = n.Liters;
        p.TotalUsd = n.TotalUsd; p.TotalAfn = n.TotalAfn; p.PerLiter = n.PerLiter;
    }

    /// <summary>
    /// حالِ مخزن. «فروش» جمعِ ‎sale‎ِ هر دو شیفتِ همهٔ پارچه‌های همان سوخت است.
    /// </summary>
    /// <param name="dips">
    /// میله‌زنی‌ها. تنها چیزی که از آن‌ها به موجودی می‌رسد
    /// <see cref="TankDip.BookAdjust"/> است — همان ‎dipAdj‎ در ‎_fuelStock‎.
    /// </param>
    public TankState Tank(IEnumerable<FuelPurchase> purchases, IEnumerable<ParchaReport> reports,
                          decimal lowThreshold, IEnumerable<TankDip>? dips = null)
    {
        decimal inL = 0, usd = 0, afn = 0;
        var any = false;
        foreach (var p in purchases)
        {
            if (p is null) continue;
            any = true;
            inL += p.Liters; usd += p.TotalUsd; afn += p.TotalAfn;
        }

        decimal outL = 0;
        foreach (var r in reports)
        {
            if (r is null) continue;
            outL += r.DayShift?.Sale ?? 0m;
            outL += r.NightShift?.Sale ?? 0m;
        }

        // ── اصلاحِ میله‌زنی ────────────────────────────────────────────────
        // ‎_fuelStock‎ در نسخهٔ وب سه جزء دارد، نه دو:
        //     موجودی = ورودی − فروش + اصلاحِ میله‌زنی
        // بی جزءِ سوم، «برابر کردنِ دفتر با عددِ واقعی» هیچ اثری نداشت و
        // مخزن و داشبورد و هشدارِ کمبود همان عددِ کهنه را نشان می‌دادند.
        decimal adj = 0;
        if (dips is not null)
            foreach (var d in dips)
                if (d is not null) adj += d.BookAdjust;

        var current = inL - outL + adj;

        // ‎checkLowStock‎ فقط برای سوختی هشدار می‌دهد که خریدی برایش ثبت شده
        var low = (any || outL > 0m) && current <= lowThreshold;
        var near = any && !low && current <= lowThreshold * 1.2m;
        return new TankState(inL, outL, current, Math.Max(0m, current), low, usd, afn,
                             adj, any, near);
    }

    /// <summary>
    /// اختلافِ میله‌زنی با دفتر: مثبت یعنی مخزن بیشتر از دفتر دارد.
    /// </summary>
    public static decimal DipDifference(TankDip d) => d is null ? 0m : d.Measured - d.Expected;
}
