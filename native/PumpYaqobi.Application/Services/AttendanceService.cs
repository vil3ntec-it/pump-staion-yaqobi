using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

public readonly record struct StaffMonth(
    int Days, decimal Hours, decimal Salary, bool Paid, decimal Shortage);

/// <summary>
/// ══ حاضری و معاش ═══════════════════════════════════════════════════════════
/// رونوشتِ ‎_attHours‎ و جمع‌های صفحهٔ حاضری.
///
/// ⚠️ شیفتِ شب: اگر ساعتِ رفتن از ساعتِ آمدن کوچک‌تر باشد، ۲۴ ساعت اضافه
/// می‌شود. بدونِ آن، شیفتِ ۱۹:۰۰ تا ۰۷:۰۰ منفیِ دوازده ساعت می‌شد.
/// </summary>
public sealed class AttendanceService
{
    /// <summary>‎_attHours(r)‎ — ساعتِ کارِ یک روز. ناقص یعنی صفر.</summary>
    public decimal Hours(AttendanceRow r)
    {
        if (r is null) return 0m;
        var i = Minutes(r.In);
        var o = Minutes(r.Out);
        if (i is null || o is null) return 0m;
        var mins = o.Value - i.Value;
        if (mins < 0) mins += 24 * 60;      // شیفتِ شب
        return mins / 60m;
    }

    private static decimal? Minutes(string? hhmm)
    {
        if (string.IsNullOrWhiteSpace(hhmm)) return null;
        var parts = Localization.Shamsi.ToEnDigits(hhmm).Split(':');
        if (parts.Length < 2) return null;
        if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return null;
        return h * 60 + m;
    }

    /// <summary>جمعِ یک ماهِ یک کارمند.</summary>
    public StaffMonth Month(StaffMember staff, IEnumerable<AttendanceRow> rows,
                            IEnumerable<SalaryPayment> payments, IEnumerable<StaffShortage> shortages,
                            string monthKey)
    {
        var days = 0;
        decimal hours = 0;
        foreach (var r in rows)
        {
            if (r is null || r.StaffId != staff.Id) continue;
            if (Localization.Shamsi.MonthKey(r.DateShamsi) != monthKey) continue;
            days++;
            hours += Hours(r);
        }

        var paid = payments.Any(p => p is not null && p.StaffId == staff.Id && p.MonthKey == monthKey);
        var shortage = shortages
            .Where(s => s is not null && s.StaffId == staff.Id
                        && Localization.Shamsi.MonthKey(s.DateShamsi) == monthKey)
            .Sum(s => s.Amount - s.Paid);

        return new StaffMonth(days, hours, staff.Salary, paid, shortage);
    }
}
