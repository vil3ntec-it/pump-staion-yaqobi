using System.Text.RegularExpressions;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// مخزنِ یک تیل در یک ماه — موجودیِ اولِ ماه، خرید، فروش، اصلاحِ میله‌زنی و
/// موجودیِ آخرِ ماه (همه به لیتر).
/// ⛔ همیشه: <c>Closing = Opening + Bought − Sold + DipAdjust</c>.
/// </summary>
public readonly record struct TankMonth(
    FuelType Fuel, decimal Opening, decimal Bought, decimal Sold, decimal DipAdjust, decimal Closing);

/// <summary>
/// ══ شورا، چ۵ — «گزارشِ ماهانهٔ پمپ» برای بانک و اتحادیه ═════════════════════════
///
/// فقط‌خواندنی و بی هیچ فرمولِ تازه: موجودیِ مخزن از همان
/// <see cref="StorageService.Tank(IEnumerable{FuelPurchase}, IEnumerable{ParchaReport}, decimal, IEnumerable{TankDip}?)"/>
/// است که بخشِ مخزن نشان می‌دهد، فقط روی ردیف‌های «تا پیش از این ماه» و «تا
/// آخرِ این ماه». ماهِ هر ردیف از همان <see cref="MonthReportService.MonthKeyOf"/>ِ
/// گزارشِ پایانِ ماه، پس هر دو گزارش یک ردیف را در یک ماه می‌شمارند.
///
/// ⚠️ ردیفِ بی‌تاریخ (یا تاریخِ ناخوانا) «پیش از همهٔ ماه‌ها» شمرده می‌شود: بخشِ
/// مخزن آن را در موجودی می‌شمارد، پس اگر این‌جا کنار می‌رفت موجودیِ آخرِ ماهِ جاری
/// با عددِ بخشِ مخزن نمی‌خواند.
/// </summary>
public sealed class PumpMonthlyService
{
    private static readonly Regex Valid = new(@"^\d{4}/\d{2}$");
    private readonly StorageService _storage = new();

    /// <summary>−۱ پیش از ماه (یا بی‌تاریخ) · ۰ همین ماه · ۱ پس از ماه.</summary>
    public static int Where(string? date, string monthKey)
    {
        var k = MonthReportService.MonthKeyOf(date);
        if (!Valid.IsMatch(k)) return -1;
        var c = string.CompareOrdinal(k, monthKey);
        return c < 0 ? -1 : c == 0 ? 0 : 1;
    }

    public TankMonth Tank(FuelType fuel, string monthKey,
                          IEnumerable<FuelPurchase> purchases, IEnumerable<ParchaReport> reports,
                          IEnumerable<TankDip>? dips = null)
    {
        var p = purchases.Where(x => x is not null && x.Fuel == fuel)
                         .Select(x => (x, w: Where(x.DateShamsi, monthKey))).ToList();
        var r = reports.Where(x => x is not null && x.Fuel == fuel)
                       .Select(x => (x, w: Where(x.DateShamsi, monthKey))).ToList();
        var d = (dips ?? Array.Empty<TankDip>()).Where(x => x is not null && x.Fuel == fuel)
                       .Select(x => (x, w: Where(x.DateShamsi, monthKey))).ToList();

        TankState Upto(Func<int, bool> keep) => _storage.Tank(
            p.Where(t => keep(t.w)).Select(t => t.x),
            r.Where(t => keep(t.w)).Select(t => t.x), 0m,
            d.Where(t => keep(t.w)).Select(t => t.x));

        var before = Upto(w => w < 0);
        var month = Upto(w => w == 0);
        var after = Upto(w => w <= 0);
        return new TankMonth(fuel, before.Current, month.In, month.Out, month.DipAdjust, after.Current);
    }
}
