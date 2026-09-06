using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ سطلِ زباله ═════════════════════════════════════════════════════════════
/// هر حذفی اول اینجا یک رونوشت می‌گذارد. نسخهٔ HTML همین را داشت
/// (<c>DB.trash</c>) و کاربر رویش حساب باز کرده — پس اینجا هم هست.
///
/// خودِ رکورد به‌صورت JSON نگه داشته می‌شود تا سطل با هر جدولی کار کند و
/// افزودنِ جدولِ تازه نیازی به دست زدن به سطل نداشته باشد.
/// </summary>
public sealed class TrashService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    };

    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly IUserSession _session;

    public TrashService(PumpDbFactory dbf, PermissionService perm, IUserSession session)
    { _dbf = dbf; _perm = perm; _session = session; }

    /// <summary>روی همان تراکنشِ حذف نوشته می‌شود تا «حذف شد ولی سطل خالی ماند» ممکن نباشد.</summary>
    public Task RememberAsync<T>(PumpDbContext db, string kind, string label, T payload, CancellationToken ct = default)
    {
        db.Trash.Add(new TrashItem
        {
            Kind = kind,
            Label = label,
            PayloadJson = JsonSerializer.Serialize(payload, Json),
            DeletedBy = _session.UserName,
        });
        return Task.CompletedTask;
    }

    public async Task<List<TrashItem>> ListAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.Trash.AsNoTracking().OrderByDescending(x => x.DeletedAtUtc).Take(500).ToListAsync(ct);
    }

    /// <summary>پاک کردنِ همیشگیِ یک قلم از سطل — کارِ مدیر است.</summary>
    public async Task PurgeAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.PurgeData);
        await using var db = _dbf.Create();
        var row = await db.Trash.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return;
        db.Trash.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public T? Payload<T>(TrashItem item) =>
        string.IsNullOrEmpty(item.PayloadJson) ? default : JsonSerializer.Deserialize<T>(item.PayloadJson, Json);
}
