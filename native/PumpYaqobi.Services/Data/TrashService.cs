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
    /// رونوشتِ ‎_restoreFromTrash‎ — ولی با ساز و کارِ خودِ این دیتابیس.
    ///
    /// ⚠️ **رکورد دوباره ساخته نمی‌شود، فقط «حذفِ نرم»اش برداشته می‌شود.**
    /// حذف در این برنامه واقعی نیست: ‎PumpDbContext.Stamp‎ هر حذفی را به
    /// ‎DeletedAt = now‎ تبدیل می‌کند و صافیِ سراسری رکورد را پنهان می‌کند. پس
    /// «ساختنِ دوباره از روی JSON» یعنی ردیفِ دوم با همان کلید — و همان
    /// ‎UNIQUE constraint failed‎. رکورد سرِ جایش هست؛ فقط باید دیده شود.
    ///
    /// فرزندان هم با همان مُهرِ زمان برمی‌گردند: ‎Stamp‎ برای کلِ یک ذخیره یک
    /// ‎now‎ می‌گذارد، پس پدر و فرزندانِ همان حذف دقیقاً یک ‎DeletedAt‎ دارند.
    /// این تنها راهی است که فرزندی را که کاربر **جداگانه و پیش‌تر** پاک کرده
    /// بود با پدر برنگرداند.
    ///
    /// خروجی: ‎null‎ یعنی برگشت؛ وگرنه دلیلِ نبرگشتن — جملهٔ آمادهٔ نمایش.
    /// </summary>
    public async Task<string?> RestoreAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();

        var item = await db.Trash.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return "این قلم دیگر در سطل نیست";

        var rowId = PayloadId(item);
        if (rowId is null) return "شناسهٔ رکوردِ اصلی در سطل نیست — بازگردانی ممکن نیست";

        int n;
        try { n = await ReviveAsync(db, item.Kind, rowId.Value, ct); }
        catch (NotSupportedException) { return "این نوع رکورد بازگردانی ندارد: " + (item.Kind ?? ""); }

        if (n == 0) return "رکوردِ اصلی پیدا نشد";

        db.Trash.Remove(item);
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (Exception ex)
        {
            // دلیلِ واقعی معمولاً در استثنای درونی است؛ بی آن، پیام هیچ کمکی نمی‌کند.
            return "برنگشت: " + (ex.InnerException?.Message ?? ex.Message);
        }
    }

    /// <summary>کلیدِ رکوردِ اصلی، از همان JSONی که موقعِ حذف نگه داشته شد.</summary>
    private static long? PayloadId(TrashItem item)
    {
        if (string.IsNullOrEmpty(item.PayloadJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(item.PayloadJson);
            return doc.RootElement.TryGetProperty("Id", out var v) && v.TryGetInt64(out var id) && id > 0
                ? id : null;
        }
        catch { return null; }
    }

    /// <summary>هر نوع، سرِ جای خودش. ‎NotSupportedException‎ یعنی نوعِ ناشناخته.</summary>
    private static async Task<int> ReviveAsync(PumpDbContext db, string? kind, long id, CancellationToken ct)
    {
        switch (kind)
        {
            case "debtor":
            {
                var d = await Find(db.Debtors, id, ct);
                if (d is null) return 0;
                var when = d.DeletedAt;
                d.DeletedAt = null;

                var accounts = await db.DebtAccounts.IgnoreQueryFilters()
                    .Where(a => (a.DebtorId == id || a.MainOfDebtorId == id) && a.DeletedAt == when)
                    .ToListAsync(ct);
                foreach (var a in accounts) a.DeletedAt = null;

                foreach (var a in accounts) await ReviveAccountRows(db, a.Id, when, ct);
                return 1;
            }

            case "debtaccount":
            {
                var a = await Find(db.DebtAccounts, id, ct);
                if (a is null) return 0;
                var when = a.DeletedAt;
                a.DeletedAt = null;
                await ReviveAccountRows(db, a.Id, when, ct);
                return 1;
            }

            case "company":
            {
                var c = await Find(db.TilCompanies, id, ct);
                if (c is null) return 0;
                var when = c.DeletedAt;
                c.DeletedAt = null;
                await ReviveChildren(db.CompanyRows, x => x.CompanyId == id, when, ct);
                return 1;
            }

            case "amanat":
            {
                var acc = await Find(db.AmanatAccounts, id, ct);
                if (acc is null) return 0;
                var when = acc.DeletedAt;
                acc.DeletedAt = null;
                await ReviveChildren(db.AmanatRows, x => x.AccountId == id, when, ct);
                return 1;
            }

            case "waraq":
            {
                var w = await Find(db.WaraqEntries, id, ct);
                if (w is null) return 0;
                var when = w.DeletedAt;
                w.DeletedAt = null;

                var shifts = await db.WaraqShifts.IgnoreQueryFilters()
                    .Where(s => s.WaraqId == id && s.DeletedAt == when).ToListAsync(ct);
                foreach (var s in shifts) s.DeletedAt = null;

                var shiftIds = shifts.Select(s => s.Id).ToList();
                await ReviveChildren(db.WaraqPumps, x => shiftIds.Contains(x.ShiftId), when, ct);
                await ReviveChildren(db.WaraqTransactions, x => shiftIds.Contains(x.ShiftId), when, ct);
                return 1;
            }

            case "parcha":
            {
                var r = await Find(db.Reports, id, ct);
                if (r is null) return 0;
                var when = r.DeletedAt;
                r.DeletedAt = null;

                // شیفتِ روز و شب رکوردِ جدا هستند و با همان حذف رفته‌اند.
                var ids = new List<long>();
                if (r.DayShiftId is { } dayId) ids.Add(dayId);
                if (r.NightShiftId is { } nightId) ids.Add(nightId);
                if (ids.Count > 0)
                    await ReviveChildren(db.ShiftDataSet, x => ids.Contains(x.Id), when, ct);
                return 1;
            }

            case "debtrow":          return await ReviveOne(db.DebtRows, id, ct);
            case "debtQuickReceipt": return await ReviveOne(db.DebtQuickReceipts, id, ct);
            case "companyrow":       return await ReviveOne(db.CompanyRows, id, ct);
            case "amanatrow":        return await ReviveOne(db.AmanatRows, id, ct);
            case "parchaReceipt":    return await ReviveOne(db.ParchaReceipts, id, ct);
            case "purchase":         return await ReviveOne(db.FuelPurchases, id, ct);
            case "invoice":          return await ReviveOne(db.Invoices, id, ct);
            case "safe":             return await ReviveOne(db.SafeEntries, id, ct);
            case "sarrafi":          return await ReviveOne(db.ExchangeRows, id, ct);
            case "expense":          return await ReviveOne(db.Expenses, id, ct);
            case "chakana":          return await ReviveOne(db.RetailRows, id, ct);
            case "extraincome":      return await ReviveOne(db.ExtraIncomes, id, ct);
            case "staff":            return await ReviveOne(db.StaffMembers, id, ct);
            case "staffshort":       return await ReviveOne(db.StaffShortSettles, id, ct);
            case "tanker":           return await ReviveOne(db.TankerUnloads, id, ct);
            case "tankdip":          return await ReviveOne(db.TankDips, id, ct);
            case "camera":           return await ReviveOne(db.Cameras, id, ct);

            default: throw new NotSupportedException(kind ?? "");
        }
    }

    /// <summary>ردیف‌های تیل و پولِ یک حساب — هر دو دفتر، با همان مُهرِ زمان.</summary>
    private static Task ReviveAccountRows(PumpDbContext db, long accountId, DateTime? when,
                                          CancellationToken ct) =>
        ReviveChildren(db.DebtRows,
                       x => x.FuelAccountId == accountId || x.MoneyAccountId == accountId, when, ct);

    private static async Task<T?> Find<T>(DbSet<T> set, long id, CancellationToken ct)
        where T : EntityBase =>
        await set.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);

    private static async Task<int> ReviveOne<T>(DbSet<T> set, long id, CancellationToken ct)
        where T : EntityBase
    {
        var e = await Find(set, id, ct);
        if (e is null) return 0;
        e.DeletedAt = null;
        return 1;
    }

    private static async Task ReviveChildren<T>(DbSet<T> set,
                                                System.Linq.Expressions.Expression<Func<T, bool>> which,
                                                DateTime? when, CancellationToken ct)
        where T : EntityBase
    {
        var rows = await set.IgnoreQueryFilters().Where(which)
                            .Where(x => x.DeletedAt == when).ToListAsync(ct);
        foreach (var r in rows) r.DeletedAt = null;
    }
}
