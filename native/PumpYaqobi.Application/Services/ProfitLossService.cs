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

    // ══ همان عددها، بی خواندنِ کلِ ردیف‌ها ═══════════════════════════════════
    //
    //  قاعدهٔ همیشگیِ این ریپو: «برای یک جمع، همهٔ ردیف‌ها را نخوان». صفحهٔ
    //  مفاد/ضرر تا ۱۴۰۵/۰۷/۱۳ با هر فعال‌سازی **همهٔ** مصارف، همهٔ درآمدهای
    //  اضافی و همهٔ پارچه‌های پنج سال را به شیءِ کامل می‌خواند تا چهار عدد
    //  جمع بزند — و چون SQLite کارِ «async»ش را روی همان نخِ صداکننده انجام
    //  می‌دهد، آن خواندن روی **نخِ رابط** بود: همان مکثی که کاربر درست
    //  سرِ اسکرولِ صفحه حس می‌کرد.
    //
    //  هر کدام از این چهار تا اگر پر باشد، جای فهرستِ هم‌نامش می‌نشیند.
    //  خالی ⇒ همان فهرست جمع می‌شود (آزمون‌های برابری با سایت دست‌نخورده).
    /// <summary>جمعِ ‎Amount‎ِ همهٔ درآمدهای اضافی — از یک ستون، نه از ردیف‌ها.</summary>
    public decimal? ExtraIncomeSum { get; init; }
    /// <summary>جمعِ ‎Amount‎ِ همهٔ مصارف — از یک ستون، نه از ردیف‌ها.</summary>
    public decimal? ExpenseSum { get; init; }
    /// <summary>جمعِ مفادِ هر دو شیفتِ پارچه‌های پطرول.</summary>
    public decimal? ShiftProfitPetrol { get; init; }
    /// <summary>جمعِ مفادِ هر دو شیفتِ پارچه‌های دیزل.</summary>
    public decimal? ShiftProfitDiesel { get; init; }
    /// <summary>
    /// همان ‎InvoiceRateDiff‎، ولی از پنج ستونِ فاکتورهای تاییدشده
    /// (<see cref="InvoiceRate"/>)، نه از شیءِ کاملِ همهٔ فاکتورها.
    /// </summary>
    public decimal? InvoiceRateDiffSum { get; init; }
}

/// <summary>
/// فقط آن پنج ستونی از فاکتور که ‎InvoiceRateDiff‎ می‌خواند — تا صفحهٔ
/// مفاد/ضرر مجبور نباشد شیءِ کاملِ همهٔ فاکتورها را بخواند.
/// </summary>
public readonly record struct InvoiceRate(
    bool ByMoney, InvoiceStatus Status, decimal Liters,
    decimal PricePerLiter, decimal? RateOnCreate, decimal? RateOnApprove);

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
            if (v is null) continue;
            sum += RateDiffOf(new InvoiceRate(v.ByMoney, v.Status, v.Liters, v.PricePerLiter,
                                              v.RateOnCreate, v.RateOnApprove));
        }
        return sum;
    }

    /// <summary>همان قاعده، روی پنج ستونِ برگزیده — ⛔ دو نسخهٔ قاعده نیست: هر دو از ‎RateDiffOf‎ می‌گذرند.</summary>
    public static decimal InvoiceRateDiff(IEnumerable<InvoiceRate> invoices)
    {
        decimal sum = 0;
        foreach (var v in invoices) sum += RateDiffOf(v);
        return sum;
    }

    private static decimal RateDiffOf(in InvoiceRate v)
    {
        if (v.ByMoney || v.Status != InvoiceStatus.Approved) return 0m;
        if (v.Liters <= 0) return 0m;
        var rc = v.RateOnCreate ?? v.PricePerLiter;
        var ra = v.RateOnApprove ?? 0m;
        if (rc <= 0 || ra <= 0) return 0m;
        return (ra - rc) * v.Liters;
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
        //  جمعِ از-پیش-خوانده جای پیمایشِ فهرست می‌نشیند (شرحش بالای ‎ProfitInput‎)
        if (db.ShiftProfitPetrol is { } sp) petrol = sp;
        if (db.ShiftProfitDiesel is { } sd) diesel = sd;

        decimal noinv = 0;
        foreach (var a in db.NoInvoiceAccounts)
            foreach (var row in a.FuelRows) noinv += row.Bardagi;

        var extra = db.ExtraIncomeSum ?? db.ExtraIncomes.Sum(e => e.Amount);
        var exp = db.ExpenseSum ?? db.Expenses.Sum(e => e.Amount);
        var rate = db.InvoiceRateDiffSum ?? InvoiceRateDiff(db.Invoices);
        return new ProfitBreakdown(petrol, diesel, noinv, extra, exp, rate);
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
