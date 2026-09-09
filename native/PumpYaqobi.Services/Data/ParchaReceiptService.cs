using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>گزارشِ «ثبت همه» — چند تا رفت، چند تا تکراری بود و چند تا حساب نداشت.</summary>
public readonly record struct PostAllReport(int Ok, int Duplicate, int NotFound);

/// <summary>
/// ══ رسید پارچه‌ها ═══════════════════════════════════════════════════════════
/// صفِ رسیدهایی که هنوز واردِ حسابِ کسی نشده‌اند.
///
/// ثبت‌شدن یعنی: ردیف در حسابِ صاحبش می‌نشیند و از این صف برداشته می‌شود.
/// دو چیز جلوی خرابی را می‌گیرند و هر دو از نسخهٔ وب آمده‌اند:
///   ۱) رسیدی که پیش‌تر از راهِ «ورق روزانه» واردِ حساب شده، دوباره ثبت نمی‌شود.
///   ۲) هر ثبت یک ‎SrcKey‎ دارد؛ ثبتِ دوباره همان ردیف را به‌روز می‌کند، نه
///      اینکه ردیفِ دومی بسازد.
/// </summary>
public sealed class ParchaReceiptService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly DebtorService _debtors;

    public ParchaReceiptService(PumpDbFactory dbf, PermissionService perm, TrashService trash,
                                DebtorService debtors)
    { _dbf = dbf; _perm = perm; _trash = trash; _debtors = debtors; }

    public async Task<List<ParchaReceipt>> ListAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.ParchaReceipts.AsNoTracking()
                       .OrderBy(r => r.DateKey).ThenBy(r => r.Id).ToListAsync(ct);
    }

    public async Task<ParchaReceipt> AddAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        var today = Shamsi.Today();
        var r = new ParchaReceipt { DateShamsi = today, DateKey = Shamsi.Key(today) };
        await using var db = _dbf.Create();
        db.ParchaReceipts.Add(r);
        await db.SaveChangesAsync(ct);
        return r;
    }

    public async Task SaveAsync(ParchaReceipt r, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        r.DateKey = Shamsi.Key(r.DateShamsi);
        await using var db = _dbf.Create();
        if (r.Id == 0) db.ParchaReceipts.Add(r);
        else { db.ParchaReceipts.Attach(r); db.Entry(r).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var r = await db.ParchaReceipts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return;
        await _trash.RememberAsync(db, "parchaReceipt", (r.Account ?? "") + " — " + (r.DateShamsi ?? ""), r, ct);
        db.ParchaReceipts.Remove(r);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>یک رسید را در حسابِ صاحبش ثبت می‌کند و از صف برمی‌دارد.</summary>
    public async Task<(PostResult Result, string PersonName)> PostAsync(
        long receiptId, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var r = await db.ParchaReceipts.FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return (PostResult.NotFound, "");

        var people = await LoadPeopleAsync(db, ct);
        var (res, name) = await PostOneAsync(db, r, people, new HashSet<long>(), ct);
        if (res == PostResult.Ok) db.ParchaReceipts.Remove(r);
        await db.SaveChangesAsync(ct);
        return (res, name);
    }

    /// <summary>«📥 ثبت همه در حساب‌ها» — ردیف‌های بی‌نام دست‌نخورده می‌مانند.</summary>
    public async Task<PostAllReport> PostAllAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var rows = await db.ParchaReceipts.OrderBy(x => x.DateKey).ThenBy(x => x.Id).ToListAsync(ct);
        var people = await LoadPeopleAsync(db, ct);
        var filled = new HashSet<long>();

        int ok = 0, dup = 0, nf = 0;
        foreach (var r in rows)
        {
            // ردیفی که هنوز نامی ندارد، اصلاً امتحان نمی‌شود
            if (string.IsNullOrWhiteSpace(r.Account) && string.IsNullOrWhiteSpace(r.Name)) continue;
            var (res, _) = await PostOneAsync(db, r, people, filled, ct);
            if (res == PostResult.Ok) { ok++; db.ParchaReceipts.Remove(r); }
            else if (res == PostResult.Duplicate) dup++;
            else nf++;
        }
        await db.SaveChangesAsync(ct);
        return new PostAllReport(ok, dup, nf);
    }

    /// <summary>
    /// همهٔ قرض‌داران با حساب‌هایشان — ردیابی‌شده، تا ذخیره بگیرد — ولی
    /// **بی هیچ ردیفی**.
    ///
    /// ⚠️ پیش‌تر هر دو دفترِ همهٔ حساب‌ها هم خوانده می‌شد. با ده هزار قرض‌دار و
    /// یک میلیون ردیف یعنی یک میلیون شیءِ ردیابی‌شده در حافظه — و بعد
    /// ‎SaveChanges‎ باید همان یک میلیون را برای تغییر وارسی کند. همهٔ آن فقط
    /// برای پیدا کردنِ **یک** حساب از روی نام؛ و پیدا کردنِ نام اصلاً به
    /// ردیف‌ها کاری ندارد.
    ///
    /// حالا ردیف‌ها فقط برای همان شخصی خوانده می‌شوند که رسید به نامِ اوست
    /// (<see cref="FillRowsAsync"/>).
    /// </summary>
    private static async Task<List<Debtor>> LoadPeopleAsync(PumpDbContext db, CancellationToken ct) =>
        await db.Debtors
            .Include(d => d.MainAccount)
            .Include(d => d.SubAccounts)
            .ToListAsync(ct);

    /// <summary>
    /// ردیف‌های **یک** شخص را می‌آورد و در حساب‌های خودش می‌نشاند.
    ///
    /// چون حساب‌ها ردیابی‌شده‌اند، خودِ EF ردیفِ تازه‌خوانده را به
    /// ‎FuelRows‎/‎MoneyRows‎ی حسابش وصل می‌کند؛ پس بقیهٔ کد هیچ فرقی نمی‌فهمد.
    ///
    /// ⚠️ ‎filled‎ لازم است: در «ثبت همه» ممکن است ده رسید به نامِ یک نفر باشد
    /// و بی این، ردیف‌های او ده بار خوانده می‌شد.
    /// </summary>
    private static async Task FillRowsAsync(PumpDbContext db, Debtor person,
                                            HashSet<long> filled, CancellationToken ct)
    {
        if (!filled.Add(person.Id)) return;
        foreach (var a in person.AllAccounts())
        {
            if (a.Id == 0) continue;
            var aid = a.Id;
            await db.DebtRows.Where(r => r.FuelAccountId == aid).LoadAsync(ct);
            await db.DebtRows.Where(r => r.MoneyAccountId == aid).LoadAsync(ct);
        }
    }

    private static async Task<(PostResult, string)> PostOneAsync(
        PumpDbContext db, ParchaReceipt r, List<Debtor> people,
        HashSet<long> filled, CancellationToken ct)
    {
        var rawText = (r.Account ?? "") + " " + (r.Name ?? "");
        var clean = string.IsNullOrWhiteSpace(r.Account) ? r.Name : r.Account;
        var found = PostingService.FindAccountForText(people, rawText, clean);
        if (found is null) return (PostResult.NotFound, "");

        var person = found.Value.Person;
        // حالا که صاحبِ رسید معلوم شد، فقط ردیف‌های **او** خوانده می‌شوند
        await FillRowsAsync(db, person, filled, ct);
        var bardagi = Math.Round(PostingService.FuelBardagi(r.Liters, r.PricePerLiter),
                                 0, MidpointRounding.AwayFromZero);
        if (PostingService.AlreadyReceivedFromWaraq(person, r.Liters, bardagi, r.Hawala))
            return (PostResult.Duplicate, person.Name);

        var date = string.IsNullOrWhiteSpace(r.DateShamsi) ? Shamsi.Today() : r.DateShamsi!;
        var row = new DebtRow
        {
            DateShamsi = date,
            DateKey = Shamsi.Key(date),
            // کلمهٔ «دیزل/پطرول» از نامِ نمایشی برداشته می‌شود؛ خودِ نوعِ سوخت
            // از همان متن تشخیص داده شده و در ستونِ خودش می‌نشیند.
            Name = PostingService.StripFuelWords(r.Name),
            Hawala = r.Hawala ?? "",
            Fuel = PostingService.DetectFuelType(rawText),
            Liters = r.Liters,
            PricePerLiter = r.PricePerLiter,
            Bardagi = bardagi,
            Rasid = r.Rasid,
            Albaqi = bardagi - r.Rasid,
            Src = "parcha",
            SrcKey = "parcha|" + (r.LegacyId is { Length: > 0 } ? r.LegacyId : "id" + r.Id),
        };

        var placed = PostingService.PlaceRow(person, row, rawText, found.Value.Account);
        // ردیفِ تازه باید کلیدِ دفترش را داشته باشد تا EF بداند کجا بنشیند
        if (placed.Id == 0 && placed.FuelAccountId is null)
            placed.FuelAccountId = found.Value.Account.Id != 0
                ? found.Value.Account.Id : person.MainAccount.Id;
        return (PostResult.Ok, person.Name);
    }
}
