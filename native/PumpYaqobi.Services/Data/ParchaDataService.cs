using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ پارچه‌ها ═══════════════════════════════════════════════════════════════
/// در نسخهٔ وب پارچهٔ پطرول در <c>DB.reports</c> و پارچهٔ دیزل در
/// <c>DB.shifts</c> بود — دو ساختارِ یکسان با دو نام. اینجا یک جدول است و
/// <see cref="ParchaReport.Fuel"/> جدایشان می‌کند؛ هیچ رفتاری عوض نشده.
/// </summary>
public sealed class ParchaDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly ParchaService _calc;

    public ParchaDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash, ParchaService calc)
    { _dbf = dbf; _perm = perm; _trash = trash; _calc = calc; }

    public async Task<List<ParchaReport>> ListAsync(FuelType fuel, string? monthKey,
                                                    CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.Reports.AsNoTracking()
            .Include(r => r.DayShift).Include(r => r.NightShift)
            .Where(r => r.Fuel == fuel);
        if (!string.IsNullOrWhiteSpace(monthKey))
        {
            var from = Shamsi.Key(monthKey + "/01");
            var to = from + 99;
            q = q.Where(r => r.DateKey >= from && r.DateKey <= to);
        }
        return await q.OrderBy(r => r.DateKey).ThenBy(r => r.Id).ToListAsync(ct);
    }

    public async Task<List<string>> MonthsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var keys = await db.Reports.AsNoTracking().Where(r => r.Fuel == fuel && r.DateKey > 0)
                           .Select(r => r.DateKey).Distinct().ToListAsync(ct);
        return keys.Select(k => $"{k / 10000:0000}/{k / 100 % 100:00}")
                   .Distinct().OrderByDescending(x => x).ToList();
    }

    /// <summary>شمارهٔ پارچهٔ بعدی برای همین سوخت — مثلِ ‎fuelReports.length + 1‎.</summary>
    public async Task<int> NextReportNumberAsync(FuelType fuel, CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        return await db.Reports.CountAsync(r => r.Fuel == fuel, ct) + 1;
    }

    public async Task<ParchaReport> AddAsync(FuelType fuel, string dateShamsi, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var r = new ParchaReport
        {
            Fuel = fuel,
            DateShamsi = dateShamsi,
            DateKey = Shamsi.Key(dateShamsi),
            ReportNum = await db.Reports.CountAsync(x => x.Fuel == fuel, ct) + 1,
        };
        db.Reports.Add(r);
        await db.SaveChangesAsync(ct);
        return r;
    }

    /// <summary>
    /// ذخیرهٔ یک شیفت. عددهای حساب‌شده پیش از نوشتن روی خودِ شیفت می‌نشینند،
    /// همان‌طور که ‎saveShift‎ می‌کرد — تا گزارشِ کهنه همان عددِ آن روز را نشان دهد.
    /// </summary>
    public async Task SaveShiftAsync(ParchaReport report, ShiftKind kind, ShiftData shift,
                                     CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        _calc.Apply(shift);

        await using var db = _dbf.Create();
        if (shift.Id == 0) db.ShiftDataSet.Add(shift);
        else { db.ShiftDataSet.Attach(shift); db.Entry(shift).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);

        var rep = await db.Reports.FirstOrDefaultAsync(x => x.Id == report.Id, ct);
        if (rep is null) return;
        if (kind == ShiftKind.Day) rep.DayShiftId = shift.Id; else rep.NightShiftId = shift.Id;
        rep.DateShamsi = report.DateShamsi;
        rep.DateKey = Shamsi.Key(report.DateShamsi);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveReportAsync(ParchaReport r, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var rep = await db.Reports.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (rep is null) return;
        rep.DateShamsi = r.DateShamsi;
        rep.DateKey = Shamsi.Key(r.DateShamsi);
        rep.ReportNum = r.ReportNum;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var r = await db.Reports.Include(x => x.DayShift).Include(x => x.NightShift)
                        .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return;
        await _trash.RememberAsync(db, "parcha", $"پارچهٔ {r.ReportNum} — {r.DateShamsi}", r, ct);
        db.Reports.Remove(r);
        await db.SaveChangesAsync(ct);
    }
}
