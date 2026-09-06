using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ حاضری و معاش ═══════════════════════════════════════════════════════════
/// کارمندان، حاضریِ روزانه، پرداختِ معاش و کمبودی.
/// «معاشِ همین ماه» فقط یک‌بار ثبت می‌شود — همان قاعدهٔ نسخهٔ وب.
/// </summary>
public sealed class AttendanceDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;

    public AttendanceDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash)
    { _dbf = dbf; _perm = perm; _trash = trash; }

    public async Task<List<StaffMember>> StaffAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.StaffMembers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
    }

    public async Task<List<AttendanceRow>> RowsAsync(string monthKey, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        var from = Shamsi.Key(monthKey + "/01");
        await using var db = _dbf.Create();
        return await db.Attendance.AsNoTracking()
                       .Where(r => r.DateKey >= from && r.DateKey <= from + 99)
                       .OrderBy(r => r.DateKey).ToListAsync(ct);
    }

    public async Task<List<SalaryPayment>> PaymentsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.SalaryPayments.AsNoTracking().ToListAsync(ct);
    }

    public async Task<List<StaffShortage>> ShortagesAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.StaffShortages.AsNoTracking().OrderByDescending(s => s.DateKey).ToListAsync(ct);
    }

    public async Task<StaffMember> AddStaffAsync(string name, decimal salary, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var s = new StaffMember { Name = name.Trim(), Salary = salary, PayDay = 1 };
        db.StaffMembers.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }

    public async Task UpdateStaffAsync(StaffMember s, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        db.StaffMembers.Attach(s);
        db.Entry(s).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteStaffAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var s = await db.StaffMembers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return;
        await _trash.RememberAsync(db, "staff", s.Name ?? "", s, ct);
        db.StaffMembers.Remove(s);      // حاضری‌هایش با کلیدِ خارجی می‌روند
        await db.SaveChangesAsync(ct);
    }

    /// <summary>ثبتِ «آمدن» یا «رفتن»ِ امروز. اگر از قبل ثبت شده باشد دست نمی‌خورد.</summary>
    public async Task<AttendanceRow> MarkAsync(long staffId, bool arriving, string hhmm,
                                               CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        var today = Shamsi.Today();
        var key = Shamsi.Key(today);
        await using var db = _dbf.Create();
        var row = await db.Attendance.FirstOrDefaultAsync(r => r.StaffId == staffId && r.DateKey == key, ct);
        if (row is null)
        {
            row = new AttendanceRow { StaffId = staffId, DateShamsi = today, DateKey = key };
            db.Attendance.Add(row);
        }
        if (arriving) { if (string.IsNullOrEmpty(row.In)) row.In = hhmm; }
        else { if (string.IsNullOrEmpty(row.Out)) row.Out = hhmm; }
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task SaveRowAsync(AttendanceRow r, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        r.DateKey = Shamsi.Key(r.DateShamsi);
        await using var db = _dbf.Create();
        if (r.Id == 0) db.Attendance.Add(r);
        else { db.Attendance.Attach(r); db.Entry(r).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>پرداختِ معاشِ یک ماه — دوباره ثبت نمی‌شود.</summary>
    public async Task<bool> PaySalaryAsync(long staffId, string monthKey, decimal amount,
                                           CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        if (await db.SalaryPayments.AnyAsync(p => p.StaffId == staffId && p.MonthKey == monthKey, ct))
            return false;
        db.SalaryPayments.Add(new SalaryPayment
        {
            StaffId = staffId, MonthKey = monthKey,
            DateShamsi = Shamsi.Today(), Amount = amount,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SaveShortageAsync(StaffShortage s, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        s.DateKey = Shamsi.Key(s.DateShamsi);
        await using var db = _dbf.Create();
        if (s.Id == 0) db.StaffShortages.Add(s);
        else { db.StaffShortages.Attach(s); db.Entry(s).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }
}
