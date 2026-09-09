using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ قرض‌داران ══════════════════════════════════════════════════════════════
/// حساسیت‌های همیشگیِ این بخش که در نسخهٔ نیتیو هم باید سرِ جایشان بمانند:
///
///   • «واحد پول» و «واحد تیل» دو دفترِ کاملاً جدا هستند
///     (<see cref="DebtAccount.FuelRows"/> و <see cref="DebtAccount.MoneyRows"/>).
///     جمع‌های نمایشی نباید روی محاسبهٔ الباقی اثر بگذارند.
///   • فیصدیِ پطرول و دیزل دو چیزِ جدا هستند؛ هرگز یکی نمی‌شوند.
///   • ساختنِ حسابِ فرعی باید فوراً در فهرست دیده شود — همان باگی که در
///     نسخهٔ وب گزارش شد و ریشه‌اش این بود که فهرست از نو کشیده نمی‌شد.
/// </summary>
public sealed class DebtorService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;

    public DebtorService(PumpDbFactory dbf, PermissionService perm, TrashService trash)
    { _dbf = dbf; _perm = perm; _trash = trash; }

    /// <summary>فهرستِ کارت‌ها. ردیف‌ها بار نمی‌شوند — فقط چیزی که کارت لازم دارد.</summary>
    public async Task<List<Debtor>> ListAsync(bool noInvoice = false, string? search = null,
                                              CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.Debtors.AsNoTracking().Where(d => d.IsNoInvoice == noInvoice);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(d => d.Name!.Contains(s) || (d.Phone != null && d.Phone.Contains(s)));
        }
        return await q.OrderBy(d => d.Name).ToListAsync(ct);
    }

    /// <summary>یک قرض‌دار با همهٔ حساب‌ها و همهٔ ردیف‌هایش — برای مودالِ شخص.</summary>
    public async Task<Debtor?> LoadFullAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.Debtors.AsNoTracking()
            .Include(d => d.MainAccount).ThenInclude(a => a!.FuelRows)
            .Include(d => d.MainAccount).ThenInclude(a => a!.MoneyRows)
            .Include(d => d.SubAccounts).ThenInclude(a => a.FuelRows)
            .Include(d => d.SubAccounts).ThenInclude(a => a.MoneyRows)
            // دفترِ رسیدهای سربرگ — بی این، سربرگ و جدول دو حقیقتِ جدا می‌شدند
            .Include(d => d.MainAccount).ThenInclude(a => a!.RasidLog)
            .Include(d => d.SubAccounts).ThenInclude(a => a.RasidLog)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    /// <summary>حساب‌های همهٔ قرض‌داران، برای نشانِ حال (سبز/زرد/قرمز) روی کارت‌ها.</summary>
    public async Task<Dictionary<long, List<DebtAccount>>> AccountsByDebtorAsync(
        bool noInvoice = false, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var ids = await db.Debtors.AsNoTracking()
            .Where(d => d.IsNoInvoice == noInvoice).Select(d => d.Id).ToListAsync(ct);

        var mains = await db.DebtAccounts.AsNoTracking()
            .Include(a => a.FuelRows).Include(a => a.MoneyRows)
            .Where(a => a.MainOfDebtorId != null && ids.Contains(a.MainOfDebtorId.Value))
            .ToListAsync(ct);
        var subs = await db.DebtAccounts.AsNoTracking()
            .Include(a => a.FuelRows).Include(a => a.MoneyRows)
            .Where(a => a.DebtorId != null && ids.Contains(a.DebtorId.Value))
            .ToListAsync(ct);

        var map = ids.ToDictionary(i => i, _ => new List<DebtAccount>());
        foreach (var a in mains) map[a.MainOfDebtorId!.Value].Add(a);
        foreach (var a in subs) map[a.DebtorId!.Value].Add(a);
        return map;
    }

    public async Task<Debtor> AddDebtorAsync(string name, string? phone, bool noInvoice,
                                             CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var d = new Debtor
        {
            Name = name.Trim(),
            Phone = phone,
            IsNoInvoice = noInvoice,
            LegacyId = "d" + Guid.NewGuid().ToString("N")[..10],
            MainAccount = new DebtAccount { Mode = LedgerMode.Fuel },
        };
        db.Debtors.Add(d);
        await db.SaveChangesAsync(ct);
        return d;
    }

    /// <summary>
    /// حسابِ فرعیِ تازه. در نسخهٔ وب پس از ساختن، کارتِ فهرست تازه نمی‌شد و
    /// کاربر فکر می‌کرد چیزی ساخته نشده؛ اینجا فهرست از خودِ دیتابیس خوانده
    /// می‌شود، پس چنین چیزی ممکن نیست.
    /// </summary>
    public async Task<DebtAccount> AddSubAccountAsync(long debtorId, string? title,
                                                      CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var n = await db.DebtAccounts.CountAsync(a => a.DebtorId == debtorId, ct);
        var a = new DebtAccount
        {
            DebtorId = debtorId,
            Name = string.IsNullOrWhiteSpace(title) ? $"حسابِ فرعیِ {n + 1}" : title.Trim(),
            LegacySubId = "s" + Guid.NewGuid().ToString("N")[..8],
            Mode = LedgerMode.Fuel,
        };
        db.DebtAccounts.Add(a);
        await db.SaveChangesAsync(ct);
        return a;
    }

    public async Task UpdateDebtorAsync(Debtor d, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        db.Debtors.Attach(d);
        db.Entry(d).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAccountAsync(DebtAccount a, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        db.DebtAccounts.Attach(a);
        db.Entry(a).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>ذخیرهٔ یک ردیف — فقط همان ردیف، نه کلِ حساب.</summary>
    public async Task SaveRowAsync(DebtRow r, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        r.DateKey = Shamsi.Key(r.DateShamsi);
        await using var db = _dbf.Create();
        if (r.Id == 0) db.DebtRows.Add(r);
        else { db.DebtRows.Attach(r); db.Entry(r).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteRowAsync(long rowId, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var r = await db.DebtRows.FirstOrDefaultAsync(x => x.Id == rowId, ct);
        if (r is null) return;
        await _trash.RememberAsync(db, "debtrow", (r.Name ?? "") + " — " + (r.DateShamsi ?? ""), r, ct);
        db.DebtRows.Remove(r);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteDebtorAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        // ⚠️ حساب‌ها و ردیف‌ها هم خوانده می‌شوند — نه برای حذف (آن را خودِ
        // پایگاه با cascade می‌کند) بلکه برای **سطلِ زباله**: اگر فقط خودِ شخص
        // در سطل بنشیند، «بازگرداندن» شخصی بی‌حساب و بی‌ردیف پس می‌دهد.
        var d = await db.Debtors
                        .Include(x => x.MainAccount).ThenInclude(a => a!.FuelRows)
                        .Include(x => x.MainAccount).ThenInclude(a => a!.MoneyRows)
                        .Include(x => x.SubAccounts).ThenInclude(a => a.FuelRows)
                        .Include(x => x.SubAccounts).ThenInclude(a => a.MoneyRows)
                        .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return;
        await _trash.RememberAsync(db, "debtor", d.Name ?? "", d, ct);
        db.Debtors.Remove(d);
        await db.SaveChangesAsync(ct);
    }

    // ══ جدول‌های آرشیو — ‎newPersonTable()‎ و ‎acct.tableHistory‎ ═══════════════
    //
    // «جدول جدید» جدولِ زنده را عکس می‌گیرد، در آرشیو می‌گذارد و جدول را خالی
    // می‌کند. دو نکتهٔ حساس، هر دو از خودِ سایت:
    //
    //   • فقط **دفترِ واحدِ فعال** پاک می‌شود. دفترِ آن‌یکی واحد (پول یا تیل)
    //     دست‌نخورده می‌ماند — دو دفترِ کاملاً جدا هستند.
    //   • رسیدهای سربرگ هم صفر می‌شوند، چون با همان جدول رفتند. اگر نمی‌شدند،
    //     رسیدِ جدولِ آرشیوشده روی جدولِ نو دوباره شمرده می‌شد.

    private static readonly System.Text.Json.JsonSerializerOptions ArchiveJson =
        new() { WriteIndented = false };

    /// <summary>عکس گرفتن از جدولِ زنده و خالی کردنِ آن — ‎newPersonTable()‎.</summary>
    public async Task<DebtTableArchive> ArchiveTableAsync(long accountId, string createdShamsi,
                                                          CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var a = await db.DebtAccounts.Include(x => x.FuelRows).Include(x => x.MoneyRows)
                        .FirstOrDefaultAsync(x => x.Id == accountId, ct)
                ?? throw new InvalidOperationException("حساب پیدا نشد");

        var money = a.Mode.IsMoney();
        var rows = money ? a.MoneyRows : a.FuelRows;

        var snap = new DebtTableArchive
        {
            AccountId = a.Id,
            CreatedShamsi = createdShamsi,
            IsMoney = money,
            PercentPetrol = a.PercentPetrol ?? a.PercentLegacy,
            PercentDiesel = a.PercentDiesel ?? a.PercentLegacy,
            RasidFuelPetrol = a.RasidFuelPetrol,
            RasidFuelDiesel = a.RasidFuelDiesel,
            RasidMoneyPetrol = a.RasidMoneyPetrol,
            RasidMoneyDiesel = a.RasidMoneyDiesel,
            Note = a.Note,
            RowCount = rows.Count,
            RowsJson = System.Text.Json.JsonSerializer.Serialize(rows, ArchiveJson),
        };
        db.DebtTableArchives.Add(snap);

        // ⚠️ فهرستِ ناوبری عمداً پاک نمی‌شود: ردیف‌ها همین حالا «حذف‌شده» علامت
        // خورده‌اند و دست زدن به ناوبری، EF را به‌جای حذف به «قطعِ رابطه»
        // می‌اندازد (کلیدِ خارجی null و خطای NOT NULL).
        db.DebtRows.RemoveRange(rows);
        // رسید ستونِ خودِ همین ردیف‌هاست، پس با رفتنِ جدول خودش می‌رود. فقط
        // کشِ چهار عددِ حساب باید همان‌جا صفر شود (عکسشان در آرشیو ماند).
        if (money) { a.RasidMoneyPetrol = 0m; a.RasidMoneyDiesel = 0m; }
        else { a.RasidFuelPetrol = 0m; a.RasidFuelDiesel = 0m; }
        a.Note = null;

        await db.SaveChangesAsync(ct);
        return snap;
    }

    /// <summary>فهرستِ جدول‌های آرشیوِ یک حساب — تازه‌ترین اول، مثل سایت.</summary>
    public async Task<List<DebtTableArchive>> ListArchivesAsync(long accountId,
                                                                CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.DebtTableArchives.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.Id).ToListAsync(ct);
    }

    /// <summary>ردیف‌های داخلِ یک آرشیو — فقط برای دیدن، بی کلید و بی ذخیره.</summary>
    public static List<DebtRow> ArchiveRows(DebtTableArchive h)
    {
        try
        {
            var rows = System.Text.Json.JsonSerializer.Deserialize<List<DebtRow>>(h.RowsJson ?? "[]");
            return rows ?? new List<DebtRow>();
        }
        catch { return new List<DebtRow>(); }
    }

    public async Task DeleteArchiveAsync(long archiveId, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var h = await db.DebtTableArchives.FirstOrDefaultAsync(x => x.Id == archiveId, ct);
        if (h is null) return;
        await _trash.RememberAsync(db, "debtarchive", h.CreatedShamsi ?? "", h, ct);
        db.DebtTableArchives.Remove(h);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAccountAsync(long accountId, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var a = await db.DebtAccounts
                        .Include(x => x.FuelRows).Include(x => x.MoneyRows)
                        .FirstOrDefaultAsync(x => x.Id == accountId, ct);
        if (a is null || a.MainOfDebtorId != null) return;   // حسابِ اصلی حذف نمی‌شود
        await _trash.RememberAsync(db, "debtaccount", a.Name ?? "", a, ct);
        db.DebtAccounts.Remove(a);
        await db.SaveChangesAsync(ct);
    }
}
