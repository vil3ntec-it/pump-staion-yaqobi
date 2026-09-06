using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

public readonly record struct ShiftNumbers(
    decimal Sale, decimal Money, decimal Profit, decimal Available);

/// <summary>
/// خروجیِ کاملِ ‎calcShift‎ — هر چیزی که آن تابع در نسخهٔ وب حساب می‌کرد،
/// از جمله عددی که در کادرِ «فایدهٔ فی‌لیتر» می‌نشاند.
/// </summary>
public readonly record struct ShiftCalc(
    decimal Sale, decimal Money, decimal BuyPerLiter,
    decimal ProfitPer, decimal ProfitPerBox, decimal Profit,
    decimal Debt, decimal Available, bool ProfitPerIsAuto)
{
    // ── نوشتهٔ خانه‌های خودکار ────────────────────────────────────────────
    // شرطِ «—» مو‌به‌مو همانِ نسخهٔ وب است و به «خالی بودنِ کادرها» ربطی
    // ندارد: هر خانه با عددِ خودش سنجیده می‌شود، و «پول موجود» با money نه
    // با available — پس قرضِ بیشتر از فروش، عددِ منفیِ قرمز نشان می‌دهد،
    // نه «—». این‌ها این‌جا نشسته‌اند (نه در ViewModel) تا آزمونِ برابری
    // بتواند دقیقاً همان چیزی را بسنجد که روی صفحه دیده می‌شود.

    public string SaleText => Sale > 0m ? Shamsi.Money(Sale) : "—";
    public string MoneyText => Money > 0m ? Shamsi.Money(Money) : "—";
    public string ProfitText => Profit > 0m ? Shamsi.Money(Profit) : "—";
    public string AvailableText => Money > 0m ? Shamsi.Money(Available) : "—";

    /// <summary>
    /// ‎availEl.style.color = available >= 0 ? var(--green) : var(--red)‎
    ///
    /// ⚠️ رنگ **بی‌قید** نشانده می‌شود، حتی وقتی نوشتهٔ خانه «—» است (یعنی
    /// ‎money‎ مثبت نیست). یک‌بار این‌جا شرطِ ‎Money > 0‎ گذاشته بودم که در
    /// نسخهٔ وب نیست؛ همان ۱۴۶ حالتِ گرفته‌شده از خودِ صفحه نشان داد که رنگِ
    /// سرخ در آن حالت‌ها هم نشسته است.
    /// </summary>
    public bool AvailableIsNegative => Available < 0m;

    /// <summary>دو حالتِ ‎p-buy-per-lbl‎ در نسخهٔ وب.</summary>
    public string BuyPerLabel =>
        BuyPerLiter > 0m
            ? "فی خرید: " + Shamsi.Money(ParchaService.Fixed1(BuyPerLiter), 1)
              + " ؋  —  فایده فی لیتر: " + Shamsi.Money(ParchaService.Fixed1(ProfitPer), 1) + " ؋"
            : "هنوز خریدی در مخزن ثبت نشده";
}

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

    /// <summary>
    /// ══ ‎calcShift(p)‎ — خط‌به‌خط ══════════════════════════════════════════
    /// <code>
    /// const sale  = end - start;
    /// const money = sale * price;
    /// const buyPerLiter = DB['buyPerLiter_' + fuelType] || 0;
    /// let profitPer = 0;
    /// if (price &gt; 0 &amp;&amp; buyPerLiter &gt; 0) {
    ///   profitPer = price - buyPerLiter;
    ///   profitPerEl.value = profitPer.toFixed(1);
    /// } else { profitPer = parseFloat(profitPerEl.value) || 0; }
    /// const profit    = sale * profitPer;
    /// const available = money - debt;
    /// </code>
    ///
    /// دو نکتهٔ ریز که عمداً همان‌طور نگه داشته شده‌اند:
    ///
    ///  ۱. وقتی فیِ فروش و فیِ خرید هر دو مثبت‌اند، عددِ کادر **بی‌چون‌وچرا**
    ///     بازنویسی می‌شود؛ حتی اگر کاربر خودش عددی نوشته باشد. در نسخهٔ وب
    ///     ‎onProfitPerManual‎ هم بلافاصله ‎calcShift‎ را صدا می‌زد و همان
    ///     عددِ دستی را پس می‌زد. پس «فایدهٔ دستی» فقط وقتی می‌مانَد که هنوز
    ///     خریدی در مخزن ثبت نشده یا فی فروش صفر است.
    ///
    ///  ۲. ‎profit‎ی که همان لحظه نشان داده می‌شود با عددِ **گرد نشده** حساب
    ///     می‌شود، ولی ‎saveShift‎ بعداً عددِ **کادر** (گردشده با toFixed(1))
    ///     را می‌خوانَد و ذخیره می‌کند. برای همین هر دو برمی‌گردند:
    ///     <see cref="ShiftCalc.ProfitPer"/> برای نمایش و
    ///     <see cref="ShiftCalc.ProfitPerBox"/> برای ذخیره.
    /// </summary>
    /// <param name="boxProfitPer">عددی که همین حالا در کادرِ فایده نوشته شده.</param>
    public ShiftCalc CalcShift(decimal start, decimal end, decimal price, decimal debt,
                               decimal buyPerLiter, decimal boxProfitPer)
    {
        var sale = end - start;
        var money = sale * price;

        decimal profitPer;
        decimal box;
        bool auto;
        if (price > 0m && buyPerLiter > 0m)
        {
            profitPer = price - buyPerLiter;
            box = Fixed1(profitPer);
            auto = true;
        }
        else
        {
            profitPer = boxProfitPer;
            box = boxProfitPer;
            auto = false;
        }

        return new ShiftCalc(sale, money, buyPerLiter, profitPer, box,
                             sale * profitPer, debt, money - debt, auto);
    }

    /// <summary>همتای ‎Number.prototype.toFixed(1)‎ — گردِ نیم به بالا.</summary>
    public static decimal Fixed1(decimal v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);

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
