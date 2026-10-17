using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ ⛽ تیل چکنهٔ حواله — نوشتن و خواندنِ سقفِ حواله‌های یک حساب ═══════════════════
/// شرحِ کامل بالای <see cref="HawalaQuota"/> و <see cref="HawalaCalc"/>.
/// </summary>
public sealed class HawalaService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;

    public HawalaService(PumpDbFactory dbf, PermissionService perm) { _dbf = dbf; _perm = perm; }

    public async Task<List<HawalaQuota>> ListAsync(long accountId, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.HawalaQuotas.AsNoTracking().Where(x => x.AccountId == accountId)
                       .OrderBy(x => x.SortIndex).ThenBy(x => x.Id).ToListAsync(ct);
    }

    /// <summary>
    /// همهٔ ردیف‌های حساب — جدولِ زنده و جدول‌های آرشیو. ⚠️ آرشیو هم، وگرنه «جدول جدید»
    /// برداشت‌های گذشته را از حواله پاک می‌کرد و مانده دروغ می‌گفت.
    /// </summary>
    public async Task<List<DebtRow>> AccountRowsAsync(long accountId, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var rows = await db.DebtRows.AsNoTracking()
                           .Where(r => (r.FuelAccountId == accountId || r.MoneyAccountId == accountId)
                                       && r.Hawala != null && r.Hawala != "")
                           .ToListAsync(ct);
        var arcs = await db.DebtTableArchives.AsNoTracking().Where(x => x.AccountId == accountId)
                           .Select(x => new DebtTableArchive { Id = x.Id, RowsJson = x.RowsJson }).ToListAsync(ct);
        foreach (var a in arcs)
            rows.AddRange(DebtorService.ArchiveRows(a).Where(r => !string.IsNullOrWhiteSpace(r.Hawala)));
        return rows;
    }

    /// <returns>خطا (فارسی) یا ‎null‎.</returns>
    public async Task<string?> AddAsync(long accountId, string number, decimal liters, string? note,
                                        CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        var n = HawalaCalc.Norm(number);
        if (n.Length == 0) return "شمارهٔ حواله را بنویسید";
        if (liters <= 0m) return "مقدارِ تیلِ حواله را بنویسید";
        await using var db = _dbf.Create();
        var all = await db.HawalaQuotas.Where(x => x.AccountId == accountId).ToListAsync(ct);
        if (all.Any(x => HawalaCalc.Norm(x.Number) == n)) return "حوالهٔ " + n + " در همین حساب از پیش ثبت است";
        db.HawalaQuotas.Add(new HawalaQuota
        {
            AccountId = accountId, Number = n, Liters = liters,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            DateShamsi = Shamsi.Today(),
            SortIndex = all.Count == 0 ? 1 : all.Max(x => x.SortIndex) + 1,
        });
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task UpdateAsync(long id, decimal liters, string? note, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var q = await db.HawalaQuotas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return;
        q.Liters = liters;
        q.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var q = await db.HawalaQuotas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return;
        db.HawalaQuotas.Remove(q);          // حذفِ نرم — ‎PumpDbContext‎
        await db.SaveChangesAsync(ct);
    }

    /// <summary>نقطهٔ سرخِ دکمه: دستِ‌کم یک حواله ۷۰٪ رفته یا تمام شده؟ (بی حواله ⇒ یک پرسشِ ارزان)</summary>
    /// <remarks>
    /// ⚡ حالِ حساب (<paramref name="account"/>) <b>فقط اگر حواله‌ای هست</b> حساب می‌شود — با هر
    /// باز شدنِ حساب صدا زده می‌شود، و حسابِ بی‌حواله (بیشترِ حساب‌ها) نباید حتی یک دور روی
    /// ردیف‌هایش بزند (‎scrollperf‎: «حسابِ قرض‌دار، بارِ دوم»).
    /// </remarks>
    public async Task<(int Count, int Alerts)> AlertAsync(long accountId, Func<DebtStatusInfo>? account = null,
                                                          CancellationToken ct = default)
    {
        var qs = await ListAsync(accountId, ct);
        if (qs.Count == 0) return (0, 0);
        var rows = await AccountRowsAsync(accountId, ct);
        var st = account?.Invoke();
        return (qs.Count, qs.Count(q => HawalaCalc.Of(q, rows, st).Alert));
    }
}
