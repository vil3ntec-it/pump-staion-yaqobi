using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

public readonly record struct ShiftNumbers(
    decimal Sale, decimal Money, decimal Profit, decimal Available);

public readonly record struct ParchaTotals(
    decimal Sale, decimal Money, decimal Debt, decimal Available, decimal Profit);

/// <summary>
/// ══ پارچه‌ها ═══════════════════════════════════════════════════════════════
/// رونوشتِ ‎saveShift‎ در نسخهٔ وب:
///
///     فروش   = ختمِ پایه − شروعِ پایه
///     پول    = فروش × فی
///     فایده  = فروش × فایدهٔ فی‌لیتر
///     رسیده  = پول − قرض
///
/// ⚠️ «ختم کمتر از شروع» در نسخهٔ وب اجازهٔ ذخیره نمی‌گرفت. اینجا هم
/// <see cref="IsValid"/> همان را می‌گوید — نه اینکه بی‌صدا عددِ منفی بسازیم.
/// </summary>
public sealed class ParchaService
{
    public ShiftNumbers Compute(decimal start, decimal end, decimal price,
                                decimal profitPer, decimal debt)
    {
        var sale = end - start;
        var money = sale * price;
        return new ShiftNumbers(sale, money, sale * profitPer, money - debt);
    }

    public ShiftNumbers Compute(ShiftData s) =>
        s is null ? default : Compute(s.Start, s.End, s.Price, s.ProfitPer, s.Debt);

    /// <summary>همان بررسی‌ای که نسخهٔ وب پیش از ذخیره می‌کرد.</summary>
    public static bool IsValid(string? name, decimal start, decimal end) =>
        !string.IsNullOrWhiteSpace(name) && end >= start;

    /// <summary>مقدارهای حساب‌شده را روی خودِ شیفت می‌نشاند (مثلِ shiftData).</summary>
    public void Apply(ShiftData s)
    {
        if (s is null) return;
        var n = Compute(s);
        s.Sale = n.Sale;
        s.Money = n.Money;
        s.Profit = n.Profit;
        s.Available = n.Available;
    }

    /// <summary>جمعِ چند پارچه — پایهٔ کادرهای بالای صفحه و گزارشِ ماهانه.</summary>
    public ParchaTotals Summarize(IEnumerable<ParchaReport> reports)
    {
        decimal sale = 0, money = 0, debt = 0, avail = 0, profit = 0;
        foreach (var r in reports)
        {
            if (r is null) continue;
            foreach (var s in new[] { r.DayShift, r.NightShift })
            {
                if (s is null) continue;
                var n = Compute(s);
                sale += n.Sale; money += n.Money; profit += n.Profit;
                debt += s.Debt; avail += n.Available;
            }
        }
        return new ParchaTotals(sale, money, debt, avail, profit);
    }
}
