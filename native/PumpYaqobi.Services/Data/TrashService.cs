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

    // ══ ماندگاری ══════════════════════════════════════════════════════════════
    // نسخهٔ وب پانزده روز نگه می‌داشت (‎_TRASH_DAYS = 15‎) و بعد خودش پاک
    // می‌کرد. همان عدد، تا کاربر همان رفتاری را ببیند که به آن عادت دارد.
    public const int RetentionDays = 15;

    /// <summary>چند روز دیگر این قلم خودش پاک می‌شود (‎_trashDaysLeft‎).</summary>
    public static int DaysLeft(TrashItem item)
    {
        var elapsed = (DateTime.UtcNow - item.DeletedAtUtc).TotalDays;
        var left = Math.Ceiling(RetentionDays - elapsed);
        return left < 0 ? 0 : (int)left;
    }

    /// <summary>
    /// پاک کردنِ قلم‌هایی که پانزده روزشان تمام شده (‎_cleanTrash‎).
    ///
    /// خودکار است و اجازه نمی‌خواهد: کارِ خودِ سطل است، نه کارِ کاربر. اگر
    /// اجازه می‌خواست، سطلِ کارمند هرگز پاک نمی‌شد و برای همیشه بزرگ می‌ماند.
    /// </summary>
    public async Task<int> PruneAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        await using var db = _dbf.Create();
        var old = await db.Trash.Where(x => x.DeletedAtUtc < cutoff).ToListAsync(ct);
        if (old.Count == 0) return 0;
        db.Trash.RemoveRange(old);
        await db.SaveChangesAsync(ct);
        return old.Count;
    }

    /// <summary>خالی کردنِ کلِ سطل (‎_emptyTrash‎) — کارِ مدیر است.</summary>
    public async Task<int> EmptyAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.PurgeData);
        await using var db = _dbf.Create();
        var all = await db.Trash.ToListAsync(ct);
        if (all.Count == 0) return 0;
        db.Trash.RemoveRange(all);
        await db.SaveChangesAsync(ct);
        return all.Count;
    }

    /// <summary>نامِ فارسیِ نوعِ رکورد (‎_TRASH_KIND_LABEL‎).</summary>
    public static string KindLabel(string? kind) => kind switch
    {
        "debtor"           => "👥 حسابِ قرض‌دار",
        "debtaccount"      => "👥 زیرحساب",
        "debtrow"          => "👥 ردیفِ حساب",
        "debtQuickReceipt" => "🧾 رسیدِ قرض‌دار",
        "company"          => "🏭 شرکتِ تیل",
        "companyrow"       => "🏭 ردیفِ شرکت",
        "waraq"            => "📝 ورقِ روزانه",
        "parcha"           => "📋 پارچه",
        "parchaReceipt"    => "🧾 رسیدِ پارچه",
        "purchase"         => "🛢️ خریدِ تیل",
        "amanat"           => "🛢️ حسابِ تیل امانت",
        "amanatrow"        => "🛢️ ردیفِ تیل امانت",
        "invoice"          => "🧾 فاکتور",
        "safe"             => "🏦 گاوصندوق",
        "sarrafi"          => "💱 صرافی",
        "expense"          => "💸 مصرف",
        "chakana"          => "🧾 چکنه",
        "extraincome"      => "➕ درآمدِ اضافی",
        "staff"            => "🧑‍💼 کارمند",
        "staffshort"       => "👷 تسویهٔ کمبودیِ کارمند",
        "tanker"           => "🚚 تخلیهٔ تانکر",
        "tankdip"          => "📏 میله‌زنی",
        "camera"           => "📷 دوربین",
        _                  => kind ?? "",
    };

    /// <summary>
    /// ══ بازگرداندن ════════════════════════════════════════════════════════════
    /// رونوشتِ ‎_restoreFromTrash‎: رکورد از JSONِ همان لحظهٔ حذف ساخته و سرِ
    /// جای خودش برگردانده می‌شود.
    ///
    /// ⚠️ **کلیدها دست نمی‌خورند.** رکورد با همان ‎Id‎ی که داشت برمی‌گردد، نه
    /// با کلیدِ تازه — وگرنه فرزندانش (ردیف‌های حساب، پایه‌های ورق…) که در همان
    /// بسته‌اند به پدرِ تازه وصل نمی‌شدند و همه بی‌خانه می‌ماندند. کلید هم آزاد
    /// است، چون خودِ رکورد پاک شده بود.
    ///
    /// خروجی: ‎null‎ یعنی برگشت؛ وگرنه دلیلِ نبرگشتن — جملهٔ آماده برای نشان دادن.
    /// </summary>
    public async Task<string?> RestoreAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();

        var item = await db.Trash.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return "این قلم دیگر در سطل نیست";

        if (!Put(db, item))
            return "این نوع رکورد بازگردانی ندارد: " + (item.Kind ?? "");

        db.Trash.Remove(item);
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (Exception ex)
        {
            // دلیلِ واقعی معمولاً در استثنای درونی است (کلیدِ تکراری، کلیدِ
            // خارجیِ گم‌شده). بی آن، پیام هیچ کمکی نمی‌کند.
            return "برنگشت: " + (ex.InnerException?.Message ?? ex.Message);
        }
    }

    /// <summary>هر نوع، به جدولِ خودش. ‎false‎ یعنی نوعِ ناشناخته.</summary>
    private bool Put(PumpDbContext db, TrashItem item) => item.Kind switch
    {
        "debtor"           => Revive(db.Debtors, item),
        "debtaccount"      => Revive(db.DebtAccounts, item),
        "debtrow"          => Revive(db.DebtRows, item),
        "debtQuickReceipt" => Revive(db.DebtQuickReceipts, item),
        "company"          => Revive(db.TilCompanies, item),
        "companyrow"       => Revive(db.CompanyRows, item),
        "waraq"            => Revive(db.WaraqEntries, item),
        "parcha"           => Revive(db.Reports, item),
        "parchaReceipt"    => Revive(db.ParchaReceipts, item),
        "purchase"         => Revive(db.FuelPurchases, item),
        "amanat"           => Revive(db.AmanatAccounts, item),
        "amanatrow"        => Revive(db.AmanatRows, item),
        "invoice"          => Revive(db.Invoices, item),
        "safe"             => Revive(db.SafeEntries, item),
        "sarrafi"          => Revive(db.ExchangeRows, item),
        "expense"          => Revive(db.Expenses, item),
        "chakana"          => Revive(db.RetailRows, item),
        "extraincome"      => Revive(db.ExtraIncomes, item),
        "staff"            => Revive(db.StaffMembers, item),
        "staffshort"       => Revive(db.StaffShortSettles, item),
        "tanker"           => Revive(db.TankerUnloads, item),
        "tankdip"          => Revive(db.TankDips, item),
        "camera"           => Revive(db.Cameras, item),
        _                  => false,
    };

    private bool Revive<T>(DbSet<T> set, TrashItem item) where T : class
    {
        var entity = Payload<T>(item);
        if (entity is null) return false;
        set.Add(entity);
        return true;
    }
}
