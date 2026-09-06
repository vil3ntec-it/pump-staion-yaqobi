using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ گاوصندوق ══════════════════════════════════════════════════════════════
/// خواندن/نوشتنِ ردیف‌های گاوصندوق. محاسبه در <see cref="SafeService"/>ِ
/// لایهٔ Application است و اینجا فقط داده جابه‌جا می‌شود — تا منطقِ آزموده
/// یک نسخه بیشتر نداشته باشد.
///
/// هر تغییری اجازه می‌خواهد (بندِ ۴۱) و هر حذفی به سطلِ زباله می‌رود، نه به نیستی.
/// </summary>
public sealed class SafeDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;

    public SafeDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash)
    { _dbf = dbf; _perm = perm; _trash = trash; }

    /// <summary>ردیف‌های یک ماه (یا همه، اگر ماه خالی باشد) — مرتب بر کلیدِ عددیِ تاریخ.</summary>
    public async Task<List<SafeEntry>> ListAsync(string? monthKey, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.SafeEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(monthKey)) q = q.Where(x => x.MonthKey == monthKey);
        return await q.OrderBy(x => x.DateKey).ThenBy(x => x.Id).ToListAsync(ct);
    }

    /// <summary>ماه‌هایی که اصلاً ردیفی دارند — برای کشویِ «ماه».</summary>
    public async Task<List<string>> MonthsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.SafeEntries.AsNoTracking()
            .Where(x => x.MonthKey != null && x.MonthKey != "")
            .Select(x => x.MonthKey!).Distinct()
            .OrderByDescending(x => x).ToListAsync(ct);
    }

    public async Task<SafeEntry> AddAsync(SafeEntry e, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        Normalize(e);
        await using var db = _dbf.Create();
        db.SafeEntries.Add(e);
        await db.SaveChangesAsync(ct);
        return e;
    }

    public async Task UpdateAsync(SafeEntry e, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        Normalize(e);
        await using var db = _dbf.Create();
        db.SafeEntries.Update(e);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var row = await db.SafeEntries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        await _trash.RememberAsync(db, "safe", (row.Title ?? "") + " — " + Shamsi.Money(row.Amount), row, ct);
        db.SafeEntries.Remove(row);          // حذفِ نرم؛ در DbContext به DeletedAt تبدیل می‌شود
        await db.SaveChangesAsync(ct);
    }

    /// <summary>کلیدِ تاریخ و ماه همیشه از روی رشتهٔ تاریخ ساخته می‌شوند — یک‌جا، نه در هر صدا زدن.</summary>
    private static void Normalize(SafeEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.DateShamsi)) e.DateShamsi = Shamsi.Today();
        e.DateKey = Shamsi.Key(e.DateShamsi);
        e.MonthKey = Shamsi.MonthKey(e.DateShamsi);
    }
}
