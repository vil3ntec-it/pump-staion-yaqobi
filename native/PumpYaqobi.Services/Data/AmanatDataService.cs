using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ تیل امانت ═════════════════════════════════════════════════════════════
/// کادرهای حسابِ امانت و ردیف‌هایشان. ضریب‌های محاسبه در تنظیمات می‌نشینند
/// تا هم قابلِ تغییر باشند و هم یک نسخه بیشتر نداشته باشند.
/// </summary>
public sealed class AmanatDataService
{
    private const string SettingsKey = "amanat.settings";

    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly SettingsService _settings;

    public AmanatDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash,
                             SettingsService settings)
    { _dbf = dbf; _perm = perm; _trash = trash; _settings = settings; }

    public Application.Services.AmanatSettings Settings()
    {
        var raw = _settings.Get(SettingsKey);
        if (string.IsNullOrWhiteSpace(raw)) return Application.Services.AmanatSettings.Default;
        try
        {
            return System.Text.Json.JsonSerializer
                .Deserialize<Application.Services.AmanatSettings>(raw)
                ?? Application.Services.AmanatSettings.Default;
        }
        catch { return Application.Services.AmanatSettings.Default; }
    }

    public void SaveSettings(Application.Services.AmanatSettings s) =>
        _settings.Set(SettingsKey, System.Text.Json.JsonSerializer.Serialize(s));

    public async Task<List<AmanatAccount>> ListAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.AmanatAccounts.AsNoTracking().Include(a => a.Rows)
                       .Where(a => a.Fuel == fuel).OrderBy(a => a.Name).ToListAsync(ct);
    }

    public async Task<AmanatAccount> AddAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var a = new AmanatAccount
        {
            Fuel = fuel,
            Name = "",
            LegacyId = "AMA" + Guid.NewGuid().ToString("N")[..8],
        };
        a.Rows.Add(new AmanatRow { SortIndex = 0, DateShamsi = Shamsi.Today(), DateKey = Shamsi.Key(Shamsi.Today()) });
        db.AmanatAccounts.Add(a);
        await db.SaveChangesAsync(ct);
        return a;
    }

    public async Task UpdateAccountAsync(AmanatAccount a, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        db.AmanatAccounts.Attach(a);
        db.Entry(a).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveRowAsync(AmanatRow r, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        r.DateKey = Shamsi.Key(r.DateShamsi);
        await using var db = _dbf.Create();
        if (r.Id == 0) db.AmanatRows.Add(r);
        else { db.AmanatRows.Attach(r); db.Entry(r).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteRowAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var r = await db.AmanatRows.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return;
        await _trash.RememberAsync(db, "amanatrow", (r.Name ?? "") + " — " + (r.DateShamsi ?? ""), r, ct);
        db.AmanatRows.Remove(r);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAccountAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var a = await db.AmanatAccounts.Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return;
        await _trash.RememberAsync(db, "amanat", a.Name ?? "", a, ct);
        db.AmanatAccounts.Remove(a);
        await db.SaveChangesAsync(ct);
    }
}
