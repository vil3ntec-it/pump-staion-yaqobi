using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// دسته‌بندیِ منبع‌به‌منبعِ «ضرر و مفاد از کجا آمده؟» — همان اعدادِ خودِ
/// برنامه، فقط جدا‌جدا.
/// </summary>
public readonly record struct ProfitBreakdown(
    decimal Petrol, decimal Diesel, decimal NoInvoice, decimal Extra,
    decimal Expenses, decimal InvoiceRateDiff);

/// <summary>نتیجهٔ «مفاد / ضرر خالص».</summary>
public readonly record struct ProfitResult(decimal Income, decimal Expenses, decimal Net)
{
    public bool IsProfit => Net >= 0;
}

/// <summary>ورودیِ محاسبهٔ مفاد/ضرر — همه‌اش فقط خوانده می‌شود.</summary>
public sealed class ProfitInput
{
    public IReadOnlyList<ParchaReport> Reports { get; init; } = Array.Empty<ParchaReport>();
    /// <summary>اشخاصِ «بی‌فاکتور» — بردگی‌شان مستقیم درآمد است.</summary>
    public IReadOnlyList<DebtAccount> NoInvoiceAccounts { get; init; } = Array.Empty<DebtAccount>();
    public IReadOnlyList<ExtraIncome> ExtraIncomes { get; init; } = Array.Empty<ExtraIncome>();
    public IReadOnlyList<Expense> Expenses { get; init; } = Array.Empty<Expense>();
    public IReadOnlyList<Invoice> Invoices { get; init; } = Array.Empty<Invoice>();
}

/// <summary>
/// ══ مفاد / ضرر ═════════════════════════════════════════════════════════════
/// رونوشتِ ‎calcPL‎ · ‎_plInvRateDiff‎ · ‎_plBreakdownData‎.
///
/// ⚠️ دو نکته که آسان اشتباه می‌شوند و هر دو خواستهٔ صریحِ صاحب ریپو بوده‌اند:
///
///   ۱) «اختلافِ نرخِ ثبت و تاییدِ فاکتور» یک عددِ علامت‌دار است. مثبت یعنی
///      ضرر (تیل تا روزِ تایید گران شده ولی به نرخِ روزِ ثبت داده‌ایم) و به
///      مصارف می‌رود؛ منفی یعنی مفاد و به درآمدها. هر دو کادر مثبت می‌مانند و
///      «خالص = درآمد − مصارف» همیشه درست در می‌آید.
///
///   ۲) فقط فاکتورِ «تایید‌شده»ی تیلی شمرده می‌شود — فاکتورِ پولی لیتر ندارد
///      و نرخش بی‌معناست.
/// </summary>
public sealed class ProfitLossService
{
    /// <summary>‎_plInvRateDiff‎ — مثبت یعنی ضرر.</summary>
    public static decimal InvoiceRateDiff(IEnumerable<Invoice> invoices)
    {
        decimal sum = 0;
        foreach (var v in invoices)
        {
            if (v is null || v.ByMoney || v.Status != InvoiceStatus.Approved) continue;
            if (v.Liters <= 0) continue;
            var rc = v.RateOnCreate ?? v.PricePerLiter;
            var ra = v.RateOnApprove ?? 0m;
            if (rc <= 0 || ra <= 0) continue;
            sum += (ra - rc) * v.Liters;
        }
        return sum;
    }

    /// <summary>‎_plBreakdownData‎ — همان منبع‌ها، جدا‌جدا.</summary>
    public static ProfitBreakdown Breakdown(ProfitInput db)
    {
        decimal petrol = 0, diesel = 0;
        foreach (var r in db.Reports)
        {
            var p = (r.DayShift?.Profit ?? 0m) + (r.NightShift?.Profit ?? 0m);
            if (r.Fuel == FuelType.Petrol) petrol += p; else diesel += p;
        }

        decimal noinv = 0;
        foreach (var a in db.NoInvoiceAccounts)
            foreach (var row in a.FuelRows) noinv += row.Bardagi;

        var extra = db.ExtraIncomes.Sum(e => e.Amount);
        var exp = db.Expenses.Sum(e => e.Amount);
        return new ProfitBreakdown(petrol, diesel, noinv, extra, exp, InvoiceRateDiff(db.Invoices));
    }

    /// <summary>
    /// ‎calcPL‎ — درآمد، مصارف و خالص. دو عددِ دستیِ خودِ صفحه هم اضافه می‌شوند.
    /// </summary>
    public static ProfitResult Compute(ProfitInput db, decimal manualIncome = 0, decimal manualExpense = 0)
    {
        var b = Breakdown(db);
        var income = b.Petrol + b.Diesel + b.NoInvoice + b.Extra + manualIncome
                   + Math.Max(-b.InvoiceRateDiff, 0m);
        var expenses = b.Expenses + manualExpense + Math.Max(b.InvoiceRateDiff, 0m);
        return new ProfitResult(income, expenses, income - expenses);
    }

    /// <summary>
    /// «خرید عمده از مشتری»: درآمدِ اضافی = مقدار × (نرخِ بازار − فیِ عمده)،
    /// و پولی که به فروشنده می‌دهیم = مقدار × فیِ عمده.
    /// </summary>
    public static (decimal SellerPay, decimal Income) BulkBuy(decimal qty, decimal buy, decimal market)
        => (qty * buy, qty * (market - buy));
}
