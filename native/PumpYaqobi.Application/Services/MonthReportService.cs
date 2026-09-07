using System.Text.RegularExpressions;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>فروشِ یک سوخت در یک ماه.</summary>
public readonly record struct MonthFuel(decimal Amount, decimal Liters, decimal Profit, int Parcha);

/// <summary>خریدِ یک سوخت در یک ماه.</summary>
public readonly record struct MonthBuy(decimal Liters, decimal Amount);

/// <summary>همهٔ عددهای «گزارش ماهانه» — رونوشتِ بستهٔ ‎_mrCompute(key)‎.</summary>
public readonly record struct MonthReport(
    MonthFuel Petrol, MonthFuel Diesel,
    decimal Expenses, decimal Extra,
    MonthBuy BuyPetrol, MonthBuy BuyDiesel,
    decimal Rasid, int RasidCount,
    decimal SafeBardagi, decimal SafeMandagi,
    int TankerCount, decimal TankerShort,
    decimal Sales, decimal Liters, decimal Profit, decimal Net);

/// <summary>هرچه گزارشِ ماهانه می‌خواند — همه فقط خوانده می‌شوند.</summary>
public sealed record MonthReportSource(
    IReadOnlyList<ParchaReport> Reports,
    IReadOnlyList<Expense> Expenses,
    IReadOnlyList<ExtraIncome> ExtraIncomes,
    IReadOnlyList<FuelPurchase> Purchases,
    IReadOnlyList<DebtQuickReceipt> Receipts,
    IReadOnlyList<SafeEntry> SafeEntries,
    IReadOnlyList<TankerUnload> TankerUnloads);

/// <summary>
/// ══ گزارش پایان ماه ════════════════════════════════════════════════════════
/// رونوشتِ ‎_mrAllKeys‎ · ‎_mrPrevKey‎ · ‎_mrCompute‎ · ‎_dashGrowth‎.
///
/// این بخش **هیچ چیزی نمی‌نویسد**. فقط ثبت‌های موجود را جمع می‌زند، پس هر عددش
/// را می‌شود در بخشِ خودش دوباره پیدا کرد. اگر روزی عددی این‌جا با آن بخش فرق
/// کرد، اشتباه از این‌جاست.
/// </summary>
public sealed class MonthReportService
{
    /// <summary>
    /// ‎_monthKey(dateStr)‎ — عمداً رونوشتِ سهل‌گیرِ نسخهٔ وب، نه
    /// <see cref="Shamsi.MonthKey"/>ِ سخت‌گیر: آن سه‌پاره و سالِ معتبر می‌خواهد
    /// و «1405/06» را دور می‌ریزد؛ این همان را «1405/06» می‌خواند. گزارشِ ماه
    /// باید همان ردیف‌هایی را بشمارد که نسخهٔ وب می‌شمرد.
    /// </summary>
    public static string MonthKeyOf(string? dateStr)
    {
        var p = Shamsi.ToEnDigits(dateStr).Split('/');
        if (p.Length < 2) return NoDate;
        var m = int.TryParse(p[1], out var mm) ? mm : 0;
        return p[0] + "/" + m.ToString("00");
    }

    public const string NoDate = "بدون تاریخ";

    private static readonly Regex HasDigit = new(@"\d");

    /// <summary>‎_mrAllKeys()‎ — هر ماهی که ثبتی دارد، تازه‌ترین اول.</summary>
    public List<string> AllKeys(MonthReportSource s, string today)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        void Add(string? d)
        {
            var k = MonthKeyOf(d);
            if (k != NoDate && HasDigit.IsMatch(k)) keys.Add(k);
        }

        foreach (var r in s.Reports) if (r is not null) Add(r.DateShamsi);
        foreach (var e in s.Expenses) if (e is not null) Add(e.DateShamsi);
        foreach (var e in s.ExtraIncomes) if (e is not null) Add(e.DateShamsi);
        foreach (var e in s.Purchases) if (e is not null) Add(e.DateShamsi);
        foreach (var r in s.Receipts) if (r is not null) Add(r.DateShamsi);
        foreach (var e in s.SafeEntries) if (e is not null) Add(e.DateShamsi);
        foreach (var t in s.TankerUnloads) if (t is not null) Add(t.DateShamsi);

        // ماهِ جاری همیشه هست، حتی اگر هنوز هیچ ثبتی نداشته باشد — وگرنه
        // کشویِ ماه در روزِ اولِ ماه خالی می‌ماند.
        keys.Add(MonthKeyOf(today));

        return keys.OrderByDescending(k => k, StringComparer.Ordinal).ToList();
    }

    /// <summary>‎_mrPrevKey(key)‎ — ماهِ پیش؛ از حمل به حوتِ سالِ قبل.</summary>
    public static string PrevKey(string key)
    {
        var p = (key ?? "").Split('/');
        var y = p.Length > 0 && int.TryParse(p[0], out var yy) ? yy : 0;
        var m = p.Length > 1 && int.TryParse(p[1], out var mm) ? mm : 0;
        m--;
        if (m < 1) { m = 12; y--; }
        return y + "/" + m.ToString("00");
    }

    /// <summary>
    /// درصدِ رشدِ نسبت به ماهِ قبل — همان ‎_dashGrowth‎ که داشبورد هم از آن
    /// استفاده می‌کند. عمداً دوباره نوشته نشده: یک فرمول، یک جا.
    /// </summary>
    public static int? Growth(decimal cur, decimal prev) => DashboardService.Growth(cur, prev);

    /// <summary>‎_mrCompute(key)‎.</summary>
    public MonthReport Compute(MonthReportSource s, string key)
    {
        bool InMonth(string? d) => MonthKeyOf(d) == key;

        decimal pa = 0, pl = 0, pp = 0; var pc = 0;
        decimal da = 0, dl = 0, dp = 0; var dc = 0;

        foreach (var r in s.Reports)
        {
            if (r is null || !InMonth(r.DateShamsi)) continue;
            foreach (var sh in new[] { r.DayShift, r.NightShift })
            {
                if (sh is null) continue;
                if (r.Fuel == FuelType.Diesel)
                { da += sh.Money; dl += sh.Sale; dp += sh.Profit; dc++; }
                else
                { pa += sh.Money; pl += sh.Sale; pp += sh.Profit; pc++; }
            }
        }

        decimal exp = 0, extra = 0, rasid = 0;
        var rasidC = 0;
        foreach (var e in s.Expenses) if (e is not null && InMonth(e.DateShamsi)) exp += e.Amount;
        foreach (var e in s.ExtraIncomes) if (e is not null && InMonth(e.DateShamsi)) extra += e.Amount;
        foreach (var r in s.Receipts)
            if (r is not null && InMonth(r.DateShamsi)) { rasid += r.Amount; rasidC++; }

        decimal bpl = 0, bpa = 0, bdl = 0, bda = 0;
        foreach (var e in s.Purchases)
        {
            if (e is null || !InMonth(e.DateShamsi)) continue;
            if (e.Fuel == FuelType.Diesel) { bdl += e.Liters; bda += e.TotalAfn; }
            else                            { bpl += e.Liters; bpa += e.TotalAfn; }
        }

        // ⚠️ دالر عمداً شمرده نمی‌شود: گزارشِ ماه افغانی است و دو ارز هرگز با
        // نرخ به هم تبدیل نمی‌شوند. همان کارِ نسخهٔ وب.
        decimal sBard = 0, sMand = 0;
        foreach (var e in s.SafeEntries)
        {
            if (e is null || e.Currency == Currency.Usd || !InMonth(e.DateShamsi)) continue;
            if (e.Kind == SafeEntryKind.Bardagi) sBard += e.Amount; else sMand += e.Amount;
        }

        var tkCount = 0;
        decimal tkShort = 0;
        foreach (var t in s.TankerUnloads)
        {
            if (t is null || !InMonth(t.DateShamsi)) continue;
            tkCount++;
            var diff = t.Actual - t.Manifest;
            if (diff < 0m) tkShort += -diff;
        }

        var sales = pa + da;
        var liters = pl + dl;
        var profit = pp + dp;

        return new MonthReport(
            new MonthFuel(pa, pl, pp, pc), new MonthFuel(da, dl, dp, dc),
            exp, extra, new MonthBuy(bpl, bpa), new MonthBuy(bdl, bda),
            rasid, rasidC, sBard, sMand, tkCount, tkShort,
            sales, liters, profit, profit + extra - exp);
    }
}
