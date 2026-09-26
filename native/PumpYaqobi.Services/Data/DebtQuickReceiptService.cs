using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
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
        LedgerMode unit = LedgerMode.Money, FuelType fuel = FuelType.Petrol,
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
        // حالا که صاحبِ رسید معلوم شد، فقط ردیف‌های **او** خوانده می‌شوند
        await FillRowsAsync(db, person, ct);
        var date = string.IsNullOrWhiteSpace(dateShamsi) ? Shamsi.Today() : dateShamsi!.Trim();
        var text = (note ?? "").Trim();
        var legacyId = "dr" + Guid.NewGuid().ToString("N")[..12];

        // ══ واحدِ رسید ═════════════════════════════════════════════
        //
        //   پول  ⇒  ‎Rasid‎ (افغانی) و ‎Albaqi = -amount‎ — مو‌به‌مو همان رفتاری
        //           که از روزِ اول بود. ⛔ دست نخورد: رسیدهای امروزیِ
        //           مشتری‌ها همه همین‌اند.
        //   تیل  ⇒  ‎RasidFuel‎ (لیتر) و ‎Rasid = 0‎ — تیلی که پس داده شده
        //           بدهیِ پولی را کم نمی‌کند، پس ‎Albaqi‎ صفر می‌ماند.
        //
        // ⚠️ هر دو در دفترِ **تیل** می‌نشینند — همان جایی که نسخهٔ وب هم
        // می‌نشاند (‎placePersonRow‎ بی آرگومانِ ‎unit‎). آن‌چه عوض می‌شود
        // ستون است، نه دفتر.
        var isFuel = unit == LedgerMode.Fuel;

        var row = new DebtRow
        {
            DateShamsi = date,
            DateKey = Shamsi.Key(date),
            Name = text.Length > 0 ? text : "رسید",
            Hawala = "",
            Fuel = fuel,
            Liters = 0m,
            PricePerLiter = 0m,
            Bardagi = 0m,
            Rasid = isFuel ? 0m : amount,
            RasidFuel = isFuel ? amount : 0m,
            Albaqi = isFuel ? 0m : -amount,
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
            Unit = unit,
            Fuel = fuel,
            Amount = amount,
        });

        await db.SaveChangesAsync(ct);
        LastPerson = person;
        return (QuickReceiptResult.Ok, person.Name);
    }

    /// <summary>
    /// قرض‌دارِ آخرین رسیدِ ثبت‌شده، با حساب‌ها و ردیف‌هایش — تا صفحه
    /// الباقیِ او را همان لحظه نشان بدهد (۱۴۰۵/۰۷/۱۴). ⛔ فقط برای دیدن.
    /// </summary>
    public Debtor? LastPerson { get; private set; }

    // ══ رسیدِ چکنه — «اگه چکنه بود، یارو رو با همون اسم پیدا کنه» (۱۴۰۵/۰۷/۱۴) ══
    //
    //  چکنه دفترِ ماهانه‌ای از ردیف‌هاست و «حسابِ» هر کس همهٔ ردیف‌های هم‌نامِ
    //  او. رسید یک ردیفِ تازهٔ همان نام است با ‎Rasid = مبلغ‎ — همان کاری که
    //  کاربر دستی در جدولِ چکنه می‌کرد. ⛔ نامِ نبوده ساخته نمی‌شود (همان
    //  قاعدهٔ قرض‌داران: یک غلطِ املایی پول را به حسابِ کسی می‌برد که نیست).
    //  ⛔ رسیدِ چکنه فقط پول است — دفترِ چکنه ستونِ رسیدِ تیل ندارد.
    //
    //  پیوندِ رسید به ردیفِ چکنه از راهِ ‎SyncUid‎ِ همان ردیف است
    //  (‎LegacyId = "ck:" + SyncUid‎) — بی ستونِ تازه و بی پلهٔ تازهٔ همگام‌سازی.

    /// <summary>پیشوندِ ‎LegacyId‎ِ رسیدِ چکنه.</summary>
    public const string RetailMark = "ck:";

    /// <summary>این رسید مالِ چکنه است؟</summary>
    public static bool IsRetail(DebtQuickReceipt r) =>
        (r.LegacyId ?? "").StartsWith(RetailMark, StringComparison.Ordinal);

    /// <summary>
    /// نامِ چکنه‌ای که با متنِ تایپ‌شده جور است: اول برابرِ کامل، بعد تنها
    /// نامی که با همین آغاز می‌شود. دو نام با یک آغاز ⇒ هیچ‌کدام — حدس نمی‌زنیم.
    /// </summary>
    public static string? MatchRetailName(IEnumerable<string?> names, string typed)
    {
        var q = PostingService.NormFa(typed);
        if (q.Length == 0) return null;
        var all = names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim())
                       .Distinct().ToList();
        var exact = all.FirstOrDefault(n => PostingService.NormFa(n) == q);
        if (exact is not null) return exact;
        var starts = all.Where(n => PostingService.NormFa(n).StartsWith(q, StringComparison.Ordinal)).ToList();
        return starts.Count == 1 ? starts[0] : null;
    }

    /// <summary>نام‌های حساب‌های چکنه — برای تکمیلِ خودکار و پیدا کردن.</summary>
    public async Task<List<string>> RetailNamesAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var names = await db.RetailRows.AsNoTracking()
                            .Where(r => r.Name != null && r.Name != "")
                            .Select(r => r.Name!).Distinct().ToListAsync(ct);
        return names.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct().OrderBy(n => n).ToList();
    }

    /// <summary>
    /// رسیدِ چکنه: یک ردیفِ تازه در دفترِ چکنه با همان نام و ‎Rasid = مبلغ‎.
    /// نتیجه: نامِ حساب و الباقیِ همهٔ ردیف‌های او پس از این رسید.
    /// </summary>
    public async Task<(QuickReceiptResult Result, string Name, decimal Albaqi)> AddRetailAsync(
        string? typedName, decimal amount, RetailService calc, string? dateShamsi = null,
        string? note = null, CancellationToken ct = default)
    {
        var typed = (typedName ?? "").Trim();
        if (typed.Length == 0 || amount == 0m) return (QuickReceiptResult.Incomplete, "", 0m);
        _perm.Require(Permission.EditData);

        await using var db = _dbf.Create();
        var names = await db.RetailRows.AsNoTracking()
                            .Where(r => r.Name != null && r.Name != "")
                            .Select(r => r.Name).Distinct().ToListAsync(ct);
        var name = MatchRetailName(names, typed);
        if (name is null) return (QuickReceiptResult.NotFound, "", 0m);

        var date = string.IsNullOrWhiteSpace(dateShamsi) ? Shamsi.Today() : dateShamsi!.Trim();
        var text = (note ?? "").Trim();
        var uid = PumpYaqobi.Domain.Ulid.New();
        db.RetailRows.Add(new RetailRow
        {
            SyncUid = uid,
            DateShamsi = date,
            DateKey = Shamsi.Key(date),
            MonthKey = Shamsi.MonthKey(date),
            Name = name,
            Fuel = FuelType.Petrol,
            Rasid = amount,
            Note = text.Length > 0 ? text : "رسید",
        });
        db.DebtQuickReceipts.Add(new DebtQuickReceipt
        {
            LegacyId = RetailMark + uid,
            DateShamsi = date,
            DateKey = Shamsi.Key(date),
            MonthKey = Shamsi.MonthKey(date),
            Account = name,
            Note = text,
            Unit = LedgerMode.Money,
            Fuel = FuelType.Petrol,
            Amount = amount,
        });
        await db.SaveChangesAsync(ct);

        //  الباقیِ همهٔ ردیف‌های هم‌نام — هر ماهی که باشند
        var key = PostingService.NormFa(name);
        var mine = (await db.RetailRows.AsNoTracking()
                            .Where(r => r.Name != null && r.Name != "").ToListAsync(ct))
                   .Where(r => PostingService.NormFa(r.Name) == key);
        return (QuickReceiptResult.Ok, name, calc.Summarize(mine).Albaqi);
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

        // ⚠️ ردیفِ این رسید مستقیم از روی ‎SrcKey‎ پیدا می‌شود — که ایندکس دارد.
        // پیش‌تر برای همین یک ردیف، همهٔ قرض‌داران با همهٔ ردیف‌هایشان خوانده
        // می‌شدند تا در حافظه بگردیم؛ نتیجه یکی است، هزینه‌اش نه.
        if (IsRetail(r))
        {
            //  رسیدِ چکنه ⇒ همان یک ردیفِ چکنه (از ‎SyncUid‎ش)
            var uid = r.LegacyId[RetailMark.Length..];
            var row = await db.RetailRows.FirstOrDefaultAsync(x => x.SyncUid == uid, ct);
            if (row is not null) db.RetailRows.Remove(row);
        }
        else
        {
            var srcKey = r.SrcKey;
            var dead = await db.DebtRows.Where(x => x.SrcKey == srcKey).ToListAsync(ct);
            db.DebtRows.RemoveRange(dead);
        }

        await _trash.RememberAsync(db, "debtQuickReceipt",
            (r.Account ?? "") + " — " + Shamsi.Money(r.Amount), r, ct);
        db.DebtQuickReceipts.Remove(r);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// همهٔ قرض‌داران با حساب‌هایشان — ردیابی‌شده — ولی **بی هیچ ردیفی**.
    ///
    /// ⚠️ پیش‌تر هر دو دفترِ همهٔ حساب‌ها هم خوانده می‌شد: با ده هزار قرض‌دار و
    /// یک میلیون ردیف یعنی یک میلیون شیءِ ردیابی‌شده، فقط برای پیدا کردنِ یک
    /// حساب از روی نام — کاری که اصلاً به ردیف‌ها ربطی ندارد.
    ///
    /// ردیف‌ها فقط برای همان یک شخص خوانده می‌شوند (<see cref="FillRowsAsync"/>).
    /// </summary>
    private static async Task<List<Debtor>> LoadPeopleAsync(PumpDbContext db, CancellationToken ct) =>
        await db.Debtors
            .Include(d => d.MainAccount)
            .Include(d => d.SubAccounts)
            .ToListAsync(ct);

    /// <summary>
    /// ردیف‌های **یک** شخص را می‌آورد؛ چون حساب‌ها ردیابی‌شده‌اند، خودِ EF
    /// هر ردیف را به دفترِ خودش وصل می‌کند و بقیهٔ کد فرقی نمی‌فهمد.
    /// </summary>
    private static async Task FillRowsAsync(PumpDbContext db, Debtor person, CancellationToken ct)
    {
        foreach (var a in person.AllAccounts())
        {
            if (a.Id == 0) continue;
            var aid = a.Id;
            await db.DebtRows.Where(r => r.FuelAccountId == aid).LoadAsync(ct);
            await db.DebtRows.Where(r => r.MoneyAccountId == aid).LoadAsync(ct);
        }
    }
}
