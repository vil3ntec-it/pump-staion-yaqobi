using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ دفترهای ماهانه ═════════════════════════════════════════════════════════
/// خواندن/نوشتنِ هر جدولی که «ردیفِ تاریخ‌دار» است. یک‌جا نوشته می‌شود تا
/// همهٔ این بخش‌ها یک رفتار داشته باشند: همان فیلترِ ماه، همان مرتب‌سازیِ
/// عددیِ تاریخ، همان اجازه‌ها و همان سطلِ زباله.
/// </summary>
public sealed class LedgerService<T> where T : EntityBase, ILedgerRow, new()
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly string _kind;
    private readonly Func<T, string> _label;
    private readonly Permission _write;
    private readonly Permission _delete;

    /// <param name="writePermission">
    /// اجازه‌ای که برای افزودن/ویرایش لازم است. پیش‌فرض ‎EditData‎ (کارمند هم
    /// می‌تواند)، ولی دفترهایی که نسخهٔ وب ‎requireAdmin()‎ رویشان دارد با
    /// <see cref="Permission.ManagerOnly"/> ساخته می‌شوند.
    /// </param>
    public LedgerService(PumpDbFactory dbf, PermissionService perm, TrashService trash,
                         string kind, Func<T, string> label,
                         Permission writePermission = Permission.EditData)
    {
        _dbf = dbf; _perm = perm; _trash = trash; _kind = kind; _label = label;
        _write = writePermission;
        // حذف دستِ‌کم به‌سختیِ نوشتن است: اگر نوشتن مدیر می‌خواهد، حذف هم.
        _delete = writePermission == Permission.ManagerOnly
            ? Permission.ManagerOnly : Permission.DeleteData;
    }

    public async Task<List<T>> ListAsync(string? monthKey, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.Set<T>().AsNoTracking().AsQueryable();
        // ⚠️ سه حالت، همان سه گزینهٔ کشویِ سایت:
        //   «1405/07» یک ماه · «1405/» همهٔ ماه‌های آن سال · خالی همهٔ سال‌ها
        if (!string.IsNullOrWhiteSpace(monthKey))
        {
            if (monthKey.EndsWith('/'))
                q = q.Where(x => x.MonthKey != null && x.MonthKey.StartsWith(monthKey));
            else
                q = q.Where(x => x.MonthKey == monthKey);
        }
        return await q.OrderBy(x => x.DateKey).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<List<string>> MonthsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.Set<T>().AsNoTracking()
            .Where(x => x.MonthKey != null && x.MonthKey != "")
            .Select(x => x.MonthKey!).Distinct().OrderByDescending(x => x).ToListAsync(ct);
    }

    public async Task<T> AddAsync(T row, CancellationToken ct = default)
    {
        _perm.Require(_write);
        Normalize(row);
        await using var db = _dbf.Create();
        db.Set<T>().Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task UpdateAsync(T row, CancellationToken ct = default)
    {
        _perm.Require(_write);
        Normalize(row);
        await using var db = _dbf.Create();
        db.Set<T>().Update(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(_delete);
        await using var db = _dbf.Create();
        var row = await db.Set<T>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        await _trash.RememberAsync(db, _kind, _label(row), row, ct);
        db.Set<T>().Remove(row);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>کلیدهای تاریخ همیشه از روی رشتهٔ تاریخ ساخته می‌شوند — یک‌جا.</summary>
    public static void Normalize(T row)
    {
        if (string.IsNullOrWhiteSpace(row.DateShamsi)) row.DateShamsi = Shamsi.Today();
        row.DateKey = Shamsi.Key(row.DateShamsi);
        row.MonthKey = Shamsi.MonthKey(row.DateShamsi);
    }
}
