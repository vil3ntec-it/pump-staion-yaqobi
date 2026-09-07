using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ دفترِ دوربین‌ها ═════════════════════════════════════════════════════════
/// رونوشتِ ‎_cams‎ · ‎camConfirmAdd‎ · ‎camDelete‎.
///
/// ⚠️ افزودن و حذف پشتِ اجازهٔ مدیر است: در نسخهٔ وب هر سهِ ‎camAddManual‎،
/// ‎camConfirmAdd‎ و ‎camDelete‎ با ‎requireAdmin()‎ شروع می‌شوند. نظاره‌گر
/// می‌بیند ولی دست نمی‌زند.
/// </summary>
public sealed class CameraDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;

    public CameraDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash)
    { _dbf = dbf; _perm = perm; _trash = trash; }

    public async Task<List<Camera>> ListAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.Cameras.AsNoTracking()
                       .OrderBy(c => c.SortIndex).ThenBy(c => c.Id).ToListAsync(ct);
    }

    /// <summary>
    /// ‎camConfirmAdd‎ — بی لینک ثبت نمی‌شود، و نامِ خالی نامِ پیش‌فرض می‌گیرد.
    /// برگشتِ ‎null‎ یعنی لینک خالی بود.
    /// </summary>
    public async Task<Camera?> AddAsync(string? name, string? url, string? note = null,
                                        CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        var u = (url ?? "").Trim();
        if (u.Length == 0) return null;

        await using var db = _dbf.Create();
        var count = await db.Cameras.CountAsync(ct);
        var cam = new Camera
        {
            LegacyId = "c" + DateTime.UtcNow.Ticks.ToString("x"),
            Name = string.IsNullOrWhiteSpace(name) ? CameraService.DefaultName(count) : name!.Trim(),
            Url = u,
            Note = string.IsNullOrWhiteSpace(note) ? null : note!.Trim(),
            SortIndex = count,
        };
        db.Cameras.Add(cam);
        await db.SaveChangesAsync(ct);
        return cam;
    }

    public async Task UpdateAsync(Camera cam, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        await using var db = _dbf.Create();
        db.Cameras.Attach(cam);
        db.Entry(cam).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        await using var db = _dbf.Create();
        var cam = await db.Cameras.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cam is null) return;
        await _trash.RememberAsync(db, "camera", (cam.Name ?? "") + " — " + (cam.Url ?? ""), cam, ct);
        db.Cameras.Remove(cam);
        await db.SaveChangesAsync(ct);
    }
}
