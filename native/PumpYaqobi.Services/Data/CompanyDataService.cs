using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ شرکت‌های تیل ═══════════════════════════════════════════════════════════
/// هر شرکت دو دفترِ جدا دارد: پطرول و دیزل. مثلِ قرض‌داران، این دو هرگز با هم
/// جمع نمی‌شوند مگر جایی که صریحاً «هر دو» خواسته شده باشد.
/// </summary>
public sealed class CompanyDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;

    public CompanyDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash)
    { _dbf = dbf; _perm = perm; _trash = trash; }

    public async Task<List<TilCompany>> ListAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.TilCompanies.AsNoTracking().Include(c => c.Rows)
                       .OrderBy(c => c.Name).ToListAsync(ct);
    }

    public async Task<TilCompany?> LoadAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.TilCompanies.AsNoTracking().Include(c => c.Rows)
                       .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<TilCompany> AddAsync(string name, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var c = new TilCompany { Name = name.Trim(), LegacyId = "c" + Guid.NewGuid().ToString("N")[..10] };
        db.TilCompanies.Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }

    public async Task UpdateAsync(TilCompany c, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        db.TilCompanies.Attach(c);
        db.Entry(c).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveRowAsync(CompanyRow r, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        r.DateKey = Shamsi.Key(r.DateShamsi);
        await using var db = _dbf.Create();
        if (r.Id == 0) db.CompanyRows.Add(r);
        else { db.CompanyRows.Attach(r); db.Entry(r).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteRowAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var r = await db.CompanyRows.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return;
        await _trash.RememberAsync(db, "companyrow", (r.Name ?? "") + " — " + (r.DateShamsi ?? ""), r, ct);
        db.CompanyRows.Remove(r);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var c = await db.TilCompanies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return;
        await _trash.RememberAsync(db, "company", c.Name ?? "", c, ct);
        db.TilCompanies.Remove(c);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// شرکتِ هم‌نامِ فروشنده — با نرمال‌سازیِ نام (ی/ي، ک/ك، نیم‌فاصله) تا
    /// حسابِ تکراری ساخته نشود؛ همان کاری که ‎_findCompanyByName‎ می‌کرد.
    /// </summary>
    public static string NormalizeName(string? s) =>
        (s ?? "").Replace('ي', 'ی').Replace('ك', 'ک').Replace('‌', ' ')
                 .Trim().Replace("  ", " ");

    public async Task<TilCompany> EnsureByNameAsync(string name, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        var key = NormalizeName(name);
        await using var db = _dbf.Create();
        var all = await db.TilCompanies.Include(c => c.Rows).ToListAsync(ct);
        var found = all.FirstOrDefault(c => NormalizeName(c.Name) == key);
        if (found is not null) return found;

        var c2 = new TilCompany { Name = name.Trim(), LegacyId = "c" + Guid.NewGuid().ToString("N")[..10] };
        db.TilCompanies.Add(c2);
        await db.SaveChangesAsync(ct);
        return c2;
    }

    /// <summary>
    /// رسیدِ خودکار در نخستین ردیفِ خالی می‌نشیند؛ اگر همه پر بودند، ردیفِ تازه.
    /// ‎_putReceiptInCompany‎ — هیچ ردیفِ پری بازنویسی نمی‌شود.
    /// </summary>
    public async Task PutReceiptAsync(long companyId, FuelType fuel, CompanyRow receipt,
                                      CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var rows = await db.CompanyRows.Where(r => r.CompanyId == companyId && r.Fuel == fuel)
                           .OrderBy(r => r.SortIndex).ToListAsync(ct);
        var empty = rows.FirstOrDefault(r => r.IsEmpty);
        if (empty is not null)
        {
            empty.DateShamsi = receipt.DateShamsi; empty.DateKey = Shamsi.Key(receipt.DateShamsi);
            empty.Name = receipt.Name; empty.Poul = receipt.Poul;
            empty.PoulCurrency = receipt.PoulCurrency; empty.Rate = receipt.Rate;
            empty.SourceReceiptId = receipt.SourceReceiptId;
        }
        else
        {
            receipt.CompanyId = companyId;
            receipt.Fuel = fuel;
            receipt.SortIndex = rows.Count;
            receipt.DateKey = Shamsi.Key(receipt.DateShamsi);
            db.CompanyRows.Add(receipt);
        }
        await db.SaveChangesAsync(ct);
    }
}
