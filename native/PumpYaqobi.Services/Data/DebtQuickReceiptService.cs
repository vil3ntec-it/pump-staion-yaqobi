using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>نتیجهٔ ثبتِ یک رسیدِ نقدی.</summary>
public enum QuickReceiptResult
{
    /// <summary>ثبت شد.</summary>
    Ok,
    /// <summary>نام یا مبلغ هنوز تکمیل نشده — کاری انجام نشد، و خطا هم نیست.</summary>
    Incomplete,
    /// <summary>حسابی با این نام پیدا نشد.</summary>
    NotFound,
}

/// <summary>
/// ══ رسید قرض‌داران ═════════════════════════════════════════════════════════
/// رونوشتِ ‎quickAddDebtRasid‎ · ‎undoDebtRasid‎ · ‎addDebtRasidMonth‎.
///
/// پرداختِ نقدیِ مستقیم به حسابِ یک قرض‌دار، بی هیچ تیل. کاربر نام و مبلغ را
/// می‌نویسد و همان لحظه ثبت می‌شود — **بی هیچ دکمهٔ «ثبت»**، چون نسخهٔ وب هم
/// دکمه‌ای ندارد: به‌محضِ کامل شدنِ نام و مبلغ خودش می‌رود.
///
/// ردیفی که در حساب می‌نشیند این شکل است:
/// <code>
/// { name: note || 'رسید', fuel: 0, priceper: 0, bardagi: 0,
///   rasid: amount, albaqi: -amount, src: 'debtQuick', srcKey: 'debtQuick|&lt;id&gt;' }
/// </code>
///
/// ⚠️ ‎albaqi = -amount‎ عمدی است: پرداخت، بدهی را کم می‌کند. و ردیف در دفترِ
/// **تیل** می‌نشیند نه پول، چون ‎placePersonRow‎ بی آرگومانِ ‎unit‎ صدا زده
/// می‌شود. این عجیب به نظر می‌آید ولی همان کاری است که نسخهٔ وب می‌کند و
/// جمع‌های پولی هم از همان‌جا درست در می‌آیند.
/// </summary>
public sealed class DebtQuickReceiptService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;

    public DebtQuickReceiptService(PumpDbFactory dbf, PermissionService perm, TrashService trash)
    { _dbf = dbf; _perm = perm; _trash = trash; }

    public async Task<List<DebtQuickReceipt>> ListAsync(string? monthKey = null,
                                                        CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.DebtQuickReceipts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(monthKey)) q = q.Where(r => r.MonthKey == monthKey);
        return await q.OrderByDescending(r => r.DateKey).ThenByDescending(r => r.Id).ToListAsync(ct);
    }

    /// <summary>ماه‌هایی که رسید دارند، به‌علاوهٔ ماهِ جاری — تازه‌ترین اول.</summary>
    public async Task<List<string>> MonthsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var keys = await db.DebtQuickReceipts.AsNoTracking()
                           .Where(r => r.MonthKey != null && r.MonthKey != "")
                           .Select(r => r.MonthKey!).Distinct().ToListAsync(ct);
        var now = Shamsi.MonthKey(Shamsi.Today());
        if (now.Length > 0 && !keys.Contains(now)) keys.Add(now);
        return keys.OrderByDescending(k => k).ToList();
    }

    /// <summary>
    /// ══ ‎quickAddDebtRasid‎ ══════════════════════════════════════════════════
    ///
    ///   نام + مبلغ  →  پیدا کردنِ حساب  →  ثبتِ مستقیم در همان حساب
    ///                →  ذخیره  →  کادرها خالی
    ///
    /// حسابِ نبوده ساخته **نمی‌شود** — پیام می‌آید که اول از بخشِ قرض‌داران
    /// اضافه شود. (اگر خودکار ساخته می‌شد، یک غلطِ املایی یک حسابِ تازه
    /// می‌ساخت و پول در حسابِ کسی می‌رفت که وجود ندارد.)
    /// </summary>
    public async Task<(QuickReceiptResult Result, string PersonName)> AddAsync(
        string? typedName, decimal amount, string? dateShamsi = null, string? note = null,
        CancellationToken ct = default)
    {
        var name = (typedName ?? "").Trim();
        // هنوز هر دو کادر تکمیل نشده — بی‌صدا هیچ کاری نمی‌شود، مثلِ نسخهٔ وب
        if (name.Length == 0 || amount == 0m) return (QuickReceiptResult.Incomplete, "");

        _perm.Require(Permission.EditData);

        await using var db = _dbf.Create();
        var people = await LoadPeopleAsync(db, ct);
        var found = PostingService.FindAccountForText(people, name, name);
        if (found is null) return (QuickReceiptResult.NotFound, "");

        var person = found.Value.Person;
        var date = string.IsNullOrWhiteSpace(dateShamsi) ? Shamsi.Today() : dateShamsi!.Trim();
        var text = (note ?? "").Trim();
        var legacyId = "dr" + Guid.NewGuid().ToString("N")[..12];

        var row = new DebtRow
        {
            DateShamsi = date,
            DateKey = Shamsi.Key(date),
            Name = text.Length > 0 ? text : "رسید",
            Hawala = "",
            Liters = 0m,
            PricePerLiter = 0m,
            Bardagi = 0m,
            Rasid = amount,
            Albaqi = -amount,
            Src = "debtQuick",
            SrcKey = "debtQuick|" + legacyId,
        };

        // ⚠️ متنِ راهنما «توضیحات» است، نه نامِ تایپ‌شده — همان آرگومانی که
        // نسخهٔ وب می‌دهد. حسابِ مقصد از پیش پیدا شده، پس این فقط وقتی به کار
        // می‌آید که مقصد حسابِ اصلی باشد و توضیحات نامِ یک حسابِ فرعی را داشته باشد.
        var placed = PostingService.PlaceRow(person, row, text, found.Value.Account);
        if (placed.Id == 0 && placed.FuelAccountId is null && placed.MoneyAccountId is null)
            placed.FuelAccountId = found.Value.Account.Id != 0
                ? found.Value.Account.Id : person.MainAccount.Id;

        db.DebtQuickReceipts.Add(new DebtQuickReceipt
        {
            LegacyId = legacyId,
            DateShamsi = date,
            DateKey = Shamsi.Key(date),
            MonthKey = Shamsi.MonthKey(date),
            Account = person.Name,      // نامِ واقعیِ حساب، نه متنی که تایپ شد
            Note = text,
            Amount = amount,
        });

        await db.SaveChangesAsync(ct);
        return (QuickReceiptResult.Ok, person.Name);
    }

    /// <summary>
    /// ‎undoDebtRasid(id)‎ — رسید از فهرست و اثرش از حسابِ قرض‌دار، هر دو با هم.
    ///
    /// ⚠️ اگر فقط رسید پاک می‌شد، پول در حسابِ طرف می‌ماند و در هیچ فهرستی
    /// دیده نمی‌شد — بدهیِ پاک‌شده‌ای که هیچ‌کس نمی‌داند از کجا آمده.
    /// </summary>
    public async Task<bool> UndoAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var r = await db.DebtQuickReceipts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;

        var people = await LoadPeopleAsync(db, ct);
        foreach (var dead in PostingService.RemoveRowsBySrcKey(people, r.SrcKey))
            db.DebtRows.Remove(dead);

        await _trash.RememberAsync(db, "debtQuickReceipt",
            (r.Account ?? "") + " — " + Shamsi.Money(r.Amount), r, ct);
        db.DebtQuickReceipts.Remove(r);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task<List<Debtor>> LoadPeopleAsync(PumpDbContext db, CancellationToken ct) =>
        await db.Debtors
            .Include(d => d.MainAccount).ThenInclude(a => a!.FuelRows)
            .Include(d => d.MainAccount).ThenInclude(a => a!.MoneyRows)
            .Include(d => d.SubAccounts).ThenInclude(a => a.FuelRows)
            .Include(d => d.SubAccounts).ThenInclude(a => a.MoneyRows)
            .ToListAsync(ct);
}
