using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

/// <summary>یک ماه در پروندهٔ قرض‌دار: «برد» (لیتر یا افغانی) و رسیدِ همان ماه.</summary>
public readonly record struct DossierMonth(string Key, decimal Taken, decimal Paid, int Receipts);

/// <param name="Months">تازه‌ترین ماه اول؛ ردیفِ بی‌تاریخ آخر، زیرِ «بدون تاریخ».</param>
/// <param name="AvgGapDays">میانگینِ فاصلهٔ دو رسیدِ پشتِ سرِ هم (روز) — با کمتر از دو روزِ رسید، null.</param>
public sealed record DebtorDossier(
    IReadOnlyList<DossierMonth> Months,
    decimal Taken, decimal Paid, int ReceiptCount, decimal AvgReceipt,
    decimal? AvgGapDays, string? FirstDate, string? LastReceiptDate);

/// <summary>
/// ══ شورا، چ۵ — «پروندهٔ قرض‌دار» ═══════════════════════════════════════════════
///
/// فقط‌خواندنی، از ردیف‌های همان حسابی که باز است. «برد» و «رسید» همان دو ستونی‌اند
/// که سربرگِ حساب از آن‌ها الباقی می‌سازد (<c>PersonViewModel.Remainder</c>): دفترِ
/// پول ⇒ ‎Bardagi‎ و ‎Rasid‎؛ دفترِ تیل ⇒ ‎Liters‎ و ‎RasidFuel‎. ⛔ حالِ فعلی (الباقی)
/// این‌جا حساب نمی‌شود — صدا زننده همان عددِ سربرگ را می‌دهد، وگرنه دو جای تصمیم.
/// </summary>
public static class DebtorDossierService
{
    public static DebtorDossier Build(IEnumerable<DebtRow> rows, bool isMoney)
    {
        var list = rows.Where(r => r is not null).ToList();
        decimal TakenOf(DebtRow r) => isMoney ? r.Bardagi : r.Liters;
        decimal PaidOf(DebtRow r) => isMoney ? r.Rasid : r.RasidFuel;

        var months = list
            .GroupBy(r => Shamsi.ToDate(r.DateShamsi) is null
                          ? MonthReportService.NoDate
                          : MonthReportService.MonthKeyOf(r.DateShamsi))
            .Select(g => new DossierMonth(g.Key, g.Sum(TakenOf), g.Sum(PaidOf), g.Count(r => PaidOf(r) > 0m)))
            .Where(m => m.Taken != 0m || m.Paid != 0m)
            .OrderBy(m => m.Key == MonthReportService.NoDate ? 1 : 0)
            .ThenByDescending(m => m.Key, StringComparer.Ordinal)
            .ToList();

        var receipts = list.Where(r => PaidOf(r) > 0m).ToList();
        var paid = receipts.Sum(PaidOf);
        var avg = receipts.Count == 0 ? 0m : paid / receipts.Count;

        // روزهای رسید (یک روز با چند رسید یک روز است)، به ترتیبِ زمان
        var days = receipts.Select(r => Shamsi.ToDate(r.DateShamsi))
                           .Where(d => d is not null).Select(d => d!.Value.Date)
                           .Distinct().OrderBy(d => d).ToList();
        decimal? gap = days.Count < 2
            ? null
            : (decimal)(days[^1] - days[0]).TotalDays / (days.Count - 1);

        var dated = list.Select(r => Shamsi.ToDate(r.DateShamsi)).Where(d => d is not null)
                        .Select(d => d!.Value).ToList();
        return new DebtorDossier(
            months, list.Sum(TakenOf), paid, receipts.Count, avg, gap,
            dated.Count == 0 ? null : Shamsi.Of(dated.Min()),
            days.Count == 0 ? null : Shamsi.Of(days[^1]));
    }
}
