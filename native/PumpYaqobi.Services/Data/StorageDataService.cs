using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ مخزن ══════════════════════════════════════════════════════════════════
/// خریدهای تیل، میله‌زنی و تخلیهٔ تانکر.
///
/// هر خرید دو کارِ جانبی هم دارد، همان‌طور که در نسخهٔ وب داشت:
///   ۱) «فی لیترِ خرید» همان سوخت به‌روز می‌شود.
///   ۲) همان خرید در حسابِ شرکتِ هم‌نامِ فروشنده هم ثبت می‌شود.
/// </summary>
public sealed class StorageDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly StorageService _calc;
    private readonly SettingsService _settings;
    private readonly CompanyDataService _companies;

    public StorageDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash,
                              StorageService calc, SettingsService settings, CompanyDataService companies)
    { _dbf = dbf; _perm = perm; _trash = trash; _calc = calc; _settings = settings; _companies = companies; }

    public async Task<List<FuelPurchase>> PurchasesAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.FuelPurchases.AsNoTracking().Where(p => p.Fuel == fuel)
                       .OrderByDescending(p => p.DateKey).ThenByDescending(p => p.Id).ToListAsync(ct);
    }

    public async Task<List<ParchaReport>> ReportsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.Reports.AsNoTracking()
                       .Include(r => r.DayShift).Include(r => r.NightShift)
                       .Where(r => r.Fuel == fuel).ToListAsync(ct);
    }

    /// <summary>
    /// ثبتِ خرید. عددها حساب و ذخیره می‌شوند، «فی لیترِ خرید» به‌روز می‌شود و
    /// اگر فروشنده نام داشته باشد، همان خرید در حسابِ شرکتِ او هم می‌نشیند.
    /// </summary>
    public async Task<FuelPurchase> AddPurchaseAsync(FuelPurchase p, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        _calc.Apply(p);
        p.DateKey = Shamsi.Key(p.DateShamsi);
        p.LegacyId ??= "fe" + Guid.NewGuid().ToString("N")[..10];

        await using (var db = _dbf.Create())
        {
            db.FuelPurchases.Add(p);
            await db.SaveChangesAsync(ct);
        }

        _settings.Set(p.Fuel == FuelType.Diesel
            ? SettingsService.BuyPerLiterDiesel : SettingsService.BuyPerLiterPetrol, p.PerLiter);

        var seller = (p.Seller ?? "").Trim();
        if (seller.Length > 0)
        {
            var company = await _companies.EnsureByNameAsync(seller, ct);
            await _companies.PutReceiptAsync(company.Id, p.Fuel, new CompanyRow
            {
                Name = seller,
                DateShamsi = p.DateShamsi,
                Ton = p.Ton,
                Usd = p.PriceTon,
                Rate = p.UsdRate,
                SourcePurchaseId = p.LegacyId,
            }, ct);
        }
        return p;
    }

    public async Task UpdatePurchaseAsync(FuelPurchase p, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        _calc.Apply(p);
        p.DateKey = Shamsi.Key(p.DateShamsi);
        await using var db = _dbf.Create();
        db.FuelPurchases.Attach(p);
        db.Entry(p).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePurchaseAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var p = await db.FuelPurchases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return;
        await _trash.RememberAsync(db, "purchase", (p.Seller ?? "") + " — " + (p.DateShamsi ?? ""), p, ct);
        db.FuelPurchases.Remove(p);
        await db.SaveChangesAsync(ct);
    }

    // ── میله‌زنی ───────────────────────────────────────────────────────────
    public async Task<List<TankDip>> DipsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.TankDips.AsNoTracking().Where(d => d.Fuel == fuel)
                       .OrderByDescending(d => d.DateKey).ToListAsync(ct);
    }

    public async Task SaveDipAsync(TankDip d, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        d.DateKey = Shamsi.Key(d.DateShamsi);
        d.MonthKey = Shamsi.MonthKey(d.DateShamsi);
        await using var db = _dbf.Create();
        if (d.Id == 0) db.TankDips.Add(d);
        else { db.TankDips.Attach(d); db.Entry(d).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    // ── تخلیهٔ تانکر ───────────────────────────────────────────────────────
    public async Task<List<TankerUnload>> UnloadsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.TankerUnloads.AsNoTracking().Where(u => u.Fuel == fuel)
                       .OrderByDescending(u => u.DateKey).ToListAsync(ct);
    }

    public async Task SaveUnloadAsync(TankerUnload u, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        u.DateKey = Shamsi.Key(u.DateShamsi);
        u.MonthKey = Shamsi.MonthKey(u.DateShamsi);
        await using var db = _dbf.Create();
        if (u.Id == 0) db.TankerUnloads.Add(u);
        else { db.TankerUnloads.Attach(u); db.Entry(u).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }
}
