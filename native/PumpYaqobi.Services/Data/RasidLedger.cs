using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ دفترِ رسیدهای سربرگ — نمای سمتِ دیتابیس ═════════════════════════════════
///
/// همان قاعده‌ای که <c>DebtCalculationService</c> در حافظه دارد، این‌جا روی
/// خودِ دیتابیس: هر رسیدِ سربرگ یک رکورد است و چهار عددِ <c>Rasid…</c>ی حساب
/// همیشه برابرِ جمعِ همان رکوردها نگه داشته می‌شوند.
///
/// چرا لازم شد: رسید فقط از سربرگِ صفحهٔ شخص نمی‌آید — تاییدِ یک فاکتورِ تیل
/// هم یک رسید است. اگر آن یکی مستقیم به عددِ حساب اضافه می‌شد، اولین
/// هم‌سطح‌سازیِ دفتر پاکش می‌کرد. حالا آن هم رکوردِ خودش را می‌گیرد (با
/// <see cref="RasidEntry.InvoiceId"/>) و ردیفِ خودش را در جدولِ شخص نشان
/// می‌دهد — همان چیزی که صاحب ریپو خواسته بود: «هر رسید باید ردیفِ خودش را
/// داشته باشد.»
///
/// ⚠️ حساب‌های قدیمی رکوردی ندارند و فقط همان چهار عدد را دارند. پیش از هر
/// جمع‌زدنی یک‌بار از روی همان‌ها دفتر ساخته می‌شود (<see cref="EnsureAsync"/>)،
/// وگرنه هم‌سطح‌سازی رسیدهای قدیمی را صفر می‌کرد.
/// </summary>
public static class RasidLedger
{
    /// <summary>
    /// دفترِ همین حساب؛ اگر خالی باشد یک‌بار از روی چهار عددِ قدیمی ساخته
    /// می‌شود. رکوردهای تازه هنوز ذخیره نشده‌اند — با ‎SaveChanges‎ی صدازننده
    /// می‌نشینند.
    /// </summary>
    public static async Task<List<RasidEntry>> EnsureAsync(
        PumpDbContext db, DebtAccount acc, CancellationToken ct = default)
    {
        var log = await db.RasidEntries.Where(e => e.AccountId == acc.Id).ToListAsync(ct);
        if (log.Count > 0) return log;

        var seeds = new (LedgerMode Unit, FuelType Fuel, decimal Value)[]
        {
            (LedgerMode.Fuel,  FuelType.Petrol, acc.RasidFuelPetrol),
            (LedgerMode.Fuel,  FuelType.Diesel, acc.RasidFuelDiesel),
            (LedgerMode.Money, FuelType.Petrol, acc.RasidMoneyPetrol),
            (LedgerMode.Money, FuelType.Diesel, acc.RasidMoneyDiesel),
        };
        foreach (var (unit, fuel, v) in seeds)
        {
            if (v == 0m) continue;
            var e = new RasidEntry
            {
                AccountId = acc.Id, Unit = unit, Fuel = fuel, Value = v, SortIndex = log.Count,
            };
            db.RasidEntries.Add(e);
            log.Add(e);
        }
        return log;
    }

    /// <summary>ثبتِ یک رسیدِ تازه و هم‌سطح کردنِ چهار عددِ حساب با دفتر.</summary>
    public static async Task<RasidEntry> AddAsync(
        PumpDbContext db, DebtAccount acc, LedgerMode unit, FuelType fuel, decimal value,
        string? dateShamsi = null, long? invoiceId = null, CancellationToken ct = default)
    {
        var log = await EnsureAsync(db, acc, ct);
        var e = new RasidEntry
        {
            AccountId = acc.Id, Unit = unit, Fuel = fuel, Value = value,
            DateShamsi = dateShamsi, InvoiceId = invoiceId, SortIndex = log.Count,
        };
        db.RasidEntries.Add(e);
        log.Add(e);
        Sum(acc, log);
        return e;
    }

    /// <summary>
    /// برداشتنِ رسیدهای یک فاکتور (برگرداندنِ تایید یا حذفِ فاکتور).
    /// ‎true‎ یعنی چیزی برداشته شد.
    /// </summary>
    public static async Task<bool> RemoveByInvoiceAsync(
        PumpDbContext db, DebtAccount acc, long invoiceId, CancellationToken ct = default)
    {
        var log = await EnsureAsync(db, acc, ct);
        var mine = log.Where(e => e.InvoiceId == invoiceId).ToList();
        if (mine.Count == 0) return false;

        foreach (var e in mine) { db.RasidEntries.Remove(e); log.Remove(e); }
        Sum(acc, log);
        return true;
    }

    /// <summary>
    /// پاک کردنِ رسیدهای یک دفتر (تیل یا پول) — وقتی «جدول جدید» ساخته
    /// می‌شود و همان دفتر صفر می‌گردد.
    /// </summary>
    public static async Task ClearUnitAsync(
        PumpDbContext db, DebtAccount acc, LedgerMode unit, CancellationToken ct = default)
    {
        var log = await EnsureAsync(db, acc, ct);
        foreach (var e in log.Where(e => e.Unit == unit).ToList())
        {
            db.RasidEntries.Remove(e);
            log.Remove(e);
        }
        Sum(acc, log);
    }

    /// <summary>چهار عددِ حساب = جمعِ دفتر.</summary>
    private static void Sum(DebtAccount acc, List<RasidEntry> log)
    {
        decimal S(LedgerMode u, FuelType f) =>
            log.Where(e => e.Unit == u && e.Fuel == f).Sum(e => e.Value);

        acc.RasidFuelPetrol = S(LedgerMode.Fuel, FuelType.Petrol);
        acc.RasidFuelDiesel = S(LedgerMode.Fuel, FuelType.Diesel);
        acc.RasidMoneyPetrol = S(LedgerMode.Money, FuelType.Petrol);
        acc.RasidMoneyDiesel = S(LedgerMode.Money, FuelType.Diesel);
    }
}
