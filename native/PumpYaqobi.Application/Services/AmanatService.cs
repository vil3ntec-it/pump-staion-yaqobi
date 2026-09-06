using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// ضریب‌های «تیل امانت». پیش‌فرض‌ها همان AM_DEFAULTSِ نسخهٔ وب‌اند و منابعشان
/// در همان‌جا نوشته شده — دست‌کاری‌شان عددِ بخار را عوض می‌کند.
/// </summary>
public sealed record AmanatSettings(
    decimal BasePct = 0.02m,   // ٪ بخار در ماه، در دمای مرجع
    decimal RefTemp = 20m,     // دمای مرجع (°C)
    decimal TDouble = 10m,     // هر چند درجه، بخار دو برابر می‌شود
    decimal FPetrol = 1.00m,
    decimal FDiesel = 0.02m,   // دیزل در برابر پطرول ناچیز
    decimal TankFactor = 1.0m,
    decimal HandlingPct = 0m,  // خطای اندازه‌گیری و ریخت‌وپاش — بخار نیست
    decimal ProfitPct = 2m,
    decimal DefTemp = 25m,
    decimal SafetyPct = 25m)
{
    public static readonly AmanatSettings Default = new();
}

/// <summary>خروجیِ حسابِ یک ردیفِ امانت — همان چیزی که ‎amRowCalc‎ برمی‌گرداند.</summary>
public readonly record struct AmanatRowCalc(
    decimal Liters, decimal Taken, decimal Days, decimal Temp, decimal Base, bool Closed,
    decimal Loss, decimal LossPct, decimal Rest,
    bool HasActual, decimal? Actual, decimal? RealLoss, decimal? Diff,
    decimal? MyPct, decimal? TargetL, decimal? NeedPct, decimal? AskPct,
    decimal? NetIfMy, decimal? AskL, decimal? NetIfAsk);

public readonly record struct AmanatAccountCalc(
    int Count, int Open, decimal Liters, decimal LitersOpen, decimal Taken,
    decimal Loss, decimal LossPct, decimal Rest, decimal Share,
    bool HasActual, decimal Actual, decimal RealLoss, decimal RealDiff);

/// <summary>
/// ══ تیل امانت ═════════════════════════════════════════════════════════════
/// رونوشتِ ‎amTempFactor‎ · ‎amFuelFactor‎ · ‎amLoss‎ · ‎amThermal‎ ·
/// ‎amRowCalc‎ · ‎amAccCalc‎.
///
/// دو قاعده که در نسخهٔ وب با خونِ دل به دست آمده‌اند و اینجا هم دست‌نخورده‌اند:
///
///   ۱) «فیصدیِ لازم = فیصدیِ هدف + درصدِ بخار». بخار از سهمِ شما کم می‌شود،
///      پس فیصدی باید به همان اندازه بالاتر خواسته شود.
///   ۲) «باقی تیل = رسید − برده‌شده − سهمِ شما» — بخار این‌جا کم نمی‌شود.
///      بخار فقط از سهمِ خودتان خورده می‌شود: «به من می‌رسد = سهم − بخار».
///      یک‌بار بخار در هر دو جا کم شده بود و صاحب ریپو گزارشش کرد.
/// </summary>
public sealed class AmanatService
{
    /// <summary>‎amTempFactor(T, s)‎ — هر ‎tDouble‎ درجه، بخار دو برابر.</summary>
    public double TempFactor(decimal temp, AmanatSettings s)
    {
        var step = s.TDouble > 0m ? s.TDouble : 10m;
        var f = Math.Pow(2, (double)(temp - s.RefTemp) / (double)step);
        return double.IsFinite(f) && f > 0 ? f : 0;
    }

    public decimal FuelFactor(FuelType fuel, AmanatSettings s) =>
        fuel == FuelType.Diesel ? s.FDiesel : s.FPetrol;

    /// <summary>‎amLoss‎ — بخار هرگز از خودِ تیل بیشتر نمی‌شود.</summary>
    public decimal Loss(decimal liters, decimal days, decimal? temp, FuelType fuel,
                        decimal? basePct, AmanatSettings s)
    {
        var l = Math.Max(0m, liters);
        var d = Math.Max(0m, days);
        var p = basePct ?? s.BasePct;
        var t = temp ?? s.DefTemp;

        var loss = (double)l * (double)(p / 100m) * (double)(d / 30m)
                   * TempFactor(t, s) * (double)FuelFactor(fuel, s) * (double)s.TankFactor;
        if (!double.IsFinite(loss) || loss < 0) loss = 0;
        var res = (decimal)loss;
        return res > l ? l : res;
    }

    /// <summary>ضریب‌های انبساطِ گرمایی — بخار نیست و با سرد شدن برمی‌گردد.</summary>
    private const decimal ThermalPetrol = 0.0012m;
    private const decimal ThermalDiesel = 0.00085m;
    private const decimal ThermalRefTemp = 15.6m;

    /// <summary>‎amThermal‎ — مثبت یعنی گرم‌تر از مرجع و لیترِ بیشتر.</summary>
    public (decimal Liters, decimal Pct, decimal Temp) Thermal(
        decimal liters, decimal? temp, FuelType fuel, AmanatSettings s)
    {
        var l = Math.Max(0m, liters);
        var t = temp ?? s.DefTemp;
        var k = fuel == FuelType.Diesel ? ThermalDiesel : ThermalPetrol;
        return (l * k * (t - ThermalRefTemp), k * (t - ThermalRefTemp) * 100m, t);
    }

    /// <summary>
    /// ‎amRowCalc‎. «مدت زمان» اگر دستی نوشته نشده باشد از تاریخِ ردیف تا
    /// امروز (یا تا تاریخِ بسته‌شدن) شمرده می‌شود.
    /// </summary>
    public AmanatRowCalc RowCalc(AmanatRow row, AmanatAccount acc, AmanatSettings s, decimal autoDays)
    {
        var fuel = acc?.Fuel ?? FuelType.Petrol;
        var closed = row.State == AmanatRowState.Closed;
        var days = Math.Max(0m, row.Days ?? autoDays);

        var liters = Math.Max(0m, row.Liters ?? 0m);
        var taken = Math.Max(0m, row.Taken ?? 0m);
        var temp = row.Temp ?? s.DefTemp;
        var basePct = row.BasePct ?? s.BasePct;

        // بخار روی «رسیدِ تیل» حساب می‌شود، نه روی باقی‌مانده
        var loss = Loss(liters, days, temp, fuel, basePct, s);
        var lossPct = liters > 0m ? loss / liters * 100m : 0m;

        var hasActual = row.Actual.HasValue;
        decimal? actual = hasActual ? row.Actual!.Value : null;
        decimal? realLoss = hasActual ? liters - taken - actual!.Value : null;
        decimal? diff = hasActual ? realLoss!.Value - loss : null;

        decimal? myPct = acc is null ? null : acc.MyPct;
        decimal? targetL = myPct is null ? null : liters * myPct.Value / 100m;
        decimal? needPct = myPct is null ? null : myPct.Value + lossPct + s.HandlingPct;
        decimal? askPct = needPct is null ? null : Math.Ceiling(needPct.Value * 10m) / 10m;
        decimal? netIfMy = targetL is null ? null : targetL.Value - loss;
        decimal? askL = askPct is null ? null : liters * askPct.Value / 100m;
        decimal? netIfAsk = askL is null ? null : askL.Value - loss;

        // ⚠️ بخار این‌جا کم نمی‌شود — فقط سهمِ شما
        var rest = liters - taken - (targetL ?? 0m);

        return new AmanatRowCalc(liters, taken, days, temp, basePct, closed,
            loss, lossPct, rest, hasActual, actual, realLoss, diff,
            myPct, targetL, needPct, askPct, netIfMy, askL, netIfAsk);
    }

    /// <summary>شمارِ خودکارِ روز — از تاریخِ ردیف تا امروز، یا تا روزِ بسته‌شدن.</summary>
    public static decimal AutoDays(DateTime? start, DateTime? close, DateTime today)
    {
        if (start is null) return 0m;
        var end = close ?? today;
        if (end < start.Value) end = start.Value;
        return (decimal)Math.Round((end - start.Value).TotalDays);
    }

    /// <summary>‎amAccCalc‎ — جمعِ سربرگِ کادرِ حساب.</summary>
    public AmanatAccountCalc AccountCalc(AmanatAccount acc, AmanatSettings s,
                                         Func<AmanatRow, decimal> autoDaysOf)
    {
        int n = 0, open = 0;
        decimal liters = 0, litersOpen = 0, taken = 0, loss = 0, rest = 0, share = 0;
        bool hasActual = false;
        decimal actual = 0, realLoss = 0, realDiff = 0;

        foreach (var r in acc.Rows)
        {
            var c = RowCalc(r, acc, s, autoDaysOf(r));
            n++;
            if (!c.Closed) { open++; litersOpen += c.Liters; }
            liters += c.Liters; taken += c.Taken; loss += c.Loss; rest += c.Rest;
            share += c.TargetL ?? 0m;
            if (c.HasActual)
            {
                hasActual = true;
                actual += c.Actual!.Value; realLoss += c.RealLoss!.Value; realDiff += c.Diff!.Value;
            }
        }

        var lossPct = liters > 0m ? loss / liters * 100m : 0m;
        return new AmanatAccountCalc(n, open, liters, litersOpen, taken, loss, lossPct,
            rest, share, hasActual, actual, realLoss, realDiff);
    }
}
