using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
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

    /// <summary>
    /// برداشتنِ یک ردیفِ حساب.
    ///
    /// ⚠️ اگر ردیف از «خریدِ مخزن» آمده باشد، شمارهٔ آن خرید در فهرستِ
    /// «دستی جدا شده» ثبت می‌شود — وگرنه هم‌گام‌سازیِ خودکار همان ردیف را
    /// دفعهٔ بعد برمی‌گرداند و کاربر هرگز نمی‌تواند از دستش خلاص شود.
    /// خودِ خرید در بخشِ مخزن دست‌نخورده می‌ماند (خواستهٔ صریحِ صاحب ریپو:
    /// این دو حذف از هم جدا شدند).
    /// </summary>
    public async Task DeleteRowAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var r = await db.CompanyRows.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return;
        await _trash.RememberAsync(db, "companyrow", (r.Name ?? "") + " — " + (r.DateShamsi ?? ""), r, ct);

        if (!string.IsNullOrWhiteSpace(r.SourcePurchaseId))
            MarkUnlinked(db, r.SourcePurchaseId!);

        db.CompanyRows.Remove(r);
        await db.SaveChangesAsync(ct);
    }

    // ══ خریدهای «دستی جدا شده» ═════════════════════════════════════════════
    // همان ‎DB.purchaseUnlinked‎ی نسخهٔ وب: فهرستِ شماره‌های خریدی که کاربر
    // ردیفشان را از حساب شرکت پاک کرده. فهرست در همان جدولِ تنظیمات می‌نشیند
    // چون یک فهرستِ ساده است، نه دادهٔ مالی.
    //
    // ⚠️ نوشتنش عمداً از ‎SettingsService.Set‎ نمی‌گذرد: آن اجازهٔ «تنظیمات» را
    // می‌خواهد، ولی کاری که این‌جا انجام شده «حذفِ ردیف» است و اجازه‌اش همان
    // بالا گرفته شده. کاربری که اجازهٔ حذف دارد نباید برای ثبتِ نشانهٔ همان
    // حذف به اجازهٔ دیگری نیاز داشته باشد.
    public const string UnlinkedKey = "purchaseUnlinked";

    private static void MarkUnlinked(Persistence.PumpDbContext db, string purchaseId)
    {
        var row = db.Settings.FirstOrDefault(x => x.Key == UnlinkedKey);
        var set = Split(row?.Value);
        if (!set.Add(purchaseId)) return;
        var joined = string.Join(",", set);
        if (row is null) db.Settings.Add(new Setting { Key = UnlinkedKey, Value = joined });
        else row.Value = joined;
    }

    private static HashSet<string> Split(string? v) =>
        (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .ToHashSet(StringComparer.Ordinal);

    /// <summary>شماره‌های خریدی که کاربر خودش از حساب شرکت برداشته.</summary>
    public async Task<HashSet<string>> UnlinkedPurchasesAsync(CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        var row = await db.Settings.AsNoTracking()
                          .FirstOrDefaultAsync(x => x.Key == UnlinkedKey, ct);
        return Split(row?.Value);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        // ردیف‌ها هم همراه می‌آیند تا «بازگرداندن از سطل» شرکتِ خالی ندهد.
        var c = await db.TilCompanies.Include(x => x.Rows)
                        .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return;
        await _trash.RememberAsync(db, "company", c.Name ?? "", c, ct);
        db.TilCompanies.Remove(c);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// نرمال‌سازیِ نام — همان ‎normFa‎ی نسخهٔ وب که تطبیقِ نامِ قرض‌داران هم از
    /// آن می‌گذرد: رقمِ لاتین، ی/ک فارسی، بی‌اعراب، فاصله‌های یکی.
    ///
    /// ⚠️ پیش از این یک نسخهٔ ضعیف‌ترِ محلی این‌جا بود که فقط ی/ک و نیم‌فاصله
    /// را می‌گرفت و ‎Replace("  ", " ")‎ هم سه فاصله را دو تا می‌کرد نه یکی.
    /// نتیجه‌اش شرکتِ تکراری بود.
    /// </summary>
    public static string NormalizeName(string? s) => PostingService.NormFa(s);

    /// <summary>
    /// ══ ‎_findCompanyByName(rawName)‎ — چهار پله، به همان ترتیب ═════════════
    ///
    ///   ۱. تطابقِ دقیقِ نرمال‌شده.
    ///   ۲. نامِ شرکت با همین متن **شروع** شود — و اگر چند تا، **کوتاه‌ترین**
    ///      (خاص‌ترین) برنده. مثلاً «ح قادر» برای «ح قادر و شیر آقا».
    ///   ۳. زیررشته در هر دو جهت — و این‌جا برعکس، **بلندترین** برنده.
    ///   ۴. کلمه‌به‌کلمه: همهٔ کلمه‌های نامِ شرکت در متن باشند.
    ///
    /// ⚠️ متنِ کوتاه‌تر از دو نویسه اصلاً تطبیق داده نمی‌شود؛ وگرنه «ح» به
    /// اولین شرکتی که «ح» دارد می‌چسبید.
    ///
    /// ⚠️ پله‌های ۲ و ۳ عمداً خلافِ هم‌اند (کوتاه‌ترین در برابر بلندترین) و
    /// این اشتباه نیست: در «شروع می‌شود» نامِ کوتاه‌تر خاص‌تر است، ولی در
    /// «زیررشته» نامِ بلندتر اطلاعاتِ بیشتری را تطبیق داده.
    /// </summary>
    public static TilCompany? FindByName(IEnumerable<TilCompany> companies, string? rawName)
    {
        var list = companies?.Where(c => c is not null).ToList() ?? new List<TilCompany>();
        if (list.Count == 0) return null;

        var q = NormalizeName(rawName);
        if (q.Length < 2) return null;

        // ۱) تطابقِ دقیق
        var exact = list.FirstOrDefault(c => NormalizeName(c.Name) == q);
        if (exact is not null) return exact;

        // ۲) نامِ شرکت با متن شروع می‌شود — کوتاه‌ترین برنده
        TilCompany? prefixBest = null;
        var prefixLen = int.MaxValue;
        foreach (var c in list)
        {
            var nm = NormalizeName(c.Name);
            if (nm.Length >= 2 && nm.StartsWith(q, StringComparison.Ordinal) && nm.Length < prefixLen)
            { prefixBest = c; prefixLen = nm.Length; }
        }
        if (prefixBest is not null) return prefixBest;

        // ۳) زیررشته در هر دو جهت — بلندترین برنده
        TilCompany? best = null;
        var bestLen = 0;
        foreach (var c in list)
        {
            var nm = NormalizeName(c.Name);
            if (nm.Length >= 2 && (q.Contains(nm, StringComparison.Ordinal)
                                   || nm.Contains(q, StringComparison.Ordinal))
                && nm.Length > bestLen)
            { best = c; bestLen = nm.Length; }
        }
        if (best is not null) return best;

        // ۴) کلمه‌به‌کلمه
        var hay = " " + q + " ";
        foreach (var c in list)
        {
            var nm = NormalizeName(c.Name);
            if (nm.Length == 0) continue;
            var parts = nm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(x => x.Length >= 2).ToArray();
            var hit = parts.Length > 0 && parts.All(part =>
                hay.Contains(" " + part, StringComparison.Ordinal)
                || hay.Contains(part + " ", StringComparison.Ordinal));
            if (hit && nm.Length > bestLen) { best = c; bestLen = nm.Length; }
        }
        return best;
    }

    /// <summary>شرکتِ هم‌نامِ فروشنده؛ اگر نبود، ساخته می‌شود.</summary>
    public async Task<TilCompany> EnsureByNameAsync(string name, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var all = await db.TilCompanies.Include(c => c.Rows).ToListAsync(ct);
        var found = FindByName(all, name);
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
            // ⚠️ همهٔ خانه‌ها کپی می‌شوند. یک‌بار این‌جا فقط تاریخ و نام و پول
            // نوشته می‌شد و ‎Ton‎ و ‎Usd‎ و ‎SourcePurchaseId‎ جا می‌ماندند —
            // یعنی خریدی که در ردیفِ خالیِ حسابِ شرکت می‌نشست، تُن و فیِ تنش
            // را از دست می‌داد و دیگر به خودِ خرید هم وصل نبود. بی‌صدا، چون
            // ردیف ظاهراً ثبت شده بود.
            empty.DateShamsi = receipt.DateShamsi; empty.DateKey = Shamsi.Key(receipt.DateShamsi);
            empty.Name = receipt.Name;
            empty.Kg = receipt.Kg;
            empty.Ton = receipt.Ton;
            empty.Usd = receipt.Usd;
            empty.Rate = receipt.Rate;
            empty.Poul = receipt.Poul;
            empty.PoulCurrency = receipt.PoulCurrency;
            empty.PayRate = receipt.PayRate;
            empty.Note = receipt.Note;
            empty.SourcePurchaseId = receipt.SourcePurchaseId;
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
