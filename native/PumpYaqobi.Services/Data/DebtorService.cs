using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>جمعِ آمادهٔ یک (حساب × دفتر × سوخت) — از خودِ SQLite.</summary>
public readonly record struct AccountRollup(long AccountId, bool Money, FuelType Fuel, FuelTotals Totals);

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

    /// <summary>
    /// یک قرض‌دار با همهٔ حساب‌ها و همهٔ ردیف‌هایش — برای صفحهٔ شخص.
    ///
    /// ══ چرا هیچ ‎Include‎ای این‌جا نیست ══════════════════════════════════════
    ///
    /// سنجشِ کارایی (‎PerfAudit‎) دو بار همین‌جا را لو داد:
    ///
    ///   • با یک کوئریِ واحد و شش ‎Include‎ی مجموعه‌ای، نتیجه ضربِ دکارتیِ آن
    ///     مجموعه‌ها بود و خواندنِ حسابی با پنجاه هزار ردیف **۳۵۶ ثانیه** طول
    ///     می‌کشید.
    ///   • با ‎AsSplitQuery‎ ضربِ دکارتی رفت ولی هنوز **۶٫۳ ثانیه** بود، در
    ///     حالی که خواندنِ مستقیمِ همان پنجاه هزار ردیف تنها **۰٫۴ ثانیه**
    ///     طول می‌کشید. یعنی شش ثانیه‌اش خرجِ خودِ شکلِ ‎Include‎ بود، نه
    ///     خرجِ ردیف‌ها.
    ///
    /// پس ردیف‌ها دیگر از راهِ ناوبری خوانده نمی‌شوند: هر دفتر یک کوئریِ
    /// سادهٔ خودش دارد که مستقیم روی ایندکسِ ‎FuelAccountId‎ /
    /// ‎MoneyAccountId‎ می‌نشیند، و بعد در حافظه به حسابِ خودش وصل می‌شود.
    ///
    /// ⚠️ عمداً به‌جای ‎ids.Contains(...)‎ برای هر حساب یک کوئریِ ‎== a.Id‎
    /// زده می‌شود. حساب‌های یک شخص انگشت‌شمارند، ولی ‎Contains‎ روی SQLite به
    /// ‎json_each‎ ترجمه می‌شود و همان ایندکس را از دست می‌دهد — یعنی دوباره
    /// خواندنِ کلِ جدول.
    ///
    /// ⚠️ ترتیبِ ردیف‌ها این‌جا تحمیل نمی‌شود (مثلِ نسخهٔ ‎Include‎دار): هر
    /// جدولی که ترتیب لازم دارد خودش ‎SortIndex‎ و بعد ‎Id‎ را مرتب می‌کند.
    /// مرتب‌سازیِ SQL روی پنجاه هزار ردیف فقط هزینهٔ بی‌جا بود.
    /// </summary>
    public async Task<Debtor?> LoadFullAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();

        var person = await db.Debtors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (person is null) return null;

        var main = await db.DebtAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.MainOfDebtorId == id, ct);
        var subs = await db.DebtAccounts.AsNoTracking()
            .Where(a => a.DebtorId == id).ToListAsync(ct);

        person.MainAccount = main ?? new DebtAccount();
        person.SubAccounts = subs;

        foreach (var a in person.AllAccounts())
        {
            if (a.Id == 0) { a.FuelRows = new(); a.MoneyRows = new(); a.RasidLog = new(); continue; }
            var aid = a.Id;
            a.FuelRows = await db.DebtRows.AsNoTracking()
                .Where(r => r.FuelAccountId == aid).ToListAsync(ct);
            a.MoneyRows = await db.DebtRows.AsNoTracking()
                .Where(r => r.MoneyAccountId == aid).ToListAsync(ct);
            // دفترِ رسیدهای سربرگ — بی این، سربرگ و جدول دو حقیقتِ جدا می‌شدند
            a.RasidLog = await db.RasidEntries.AsNoTracking()
                .Where(r => r.AccountId == aid).ToListAsync(ct);
        }

        return person;
    }

    /// <summary>
    /// ══ کارت‌های قرض‌داران، بی خواندنِ حتی یک ردیف ═══════════════════════════
    ///
    /// خواستهٔ صاحب ریپو: «حتی اگر ۱۰۰۰۰ قرض‌دار داشتم با جدول‌هایی از صدهزار
    /// یا یک میلیون ردیف، نباید کند شود؛ همه‌چیز باید در صدمِ ثانیه باز شود.»
    ///
    /// راهِ پیشین (‎AccountsByDebtorAsync‎، که دیگر نیست) برای کشیدنِ فهرست
    /// **همهٔ ردیف‌های همهٔ حساب‌ها** را می‌خواند. با ده هزار قرض‌دار و یک
    /// میلیون ردیف یعنی یک میلیون شیء در حافظه، فقط برای این‌که روی هر کارت سه
    /// عدد بنویسیم. همان جایی بود که برنامه می‌ایستاد — پس آن تابع برداشته شد
    /// تا کسی دوباره از همان راه نرود.
    ///
    /// این‌جا جمع‌ها را **خودِ SQLite** می‌زند: یک ‎GROUP BY‎ روی حساب و دفتر و
    /// سوخت. بعد برای هر ترکیب یک «ردیفِ خلاصه» ساخته می‌شود و به همان حساب
    /// داده، پس <see cref="DebtCalculationService"/> هیچ فرقی نمی‌فهمد و هیچ
    /// فرمولی عوض نمی‌شود — همان عددهای دیروز، بی خواندنِ ردیف‌ها.
    ///
    /// ⚠️ «خوددرمانیِ ردیف» (‎NormalizeRow‎) داخلِ همین SQL آمده، وگرنه عددِ
    /// کارت با عددِ داخلِ حساب فرق می‌کرد:
    ///   • ردیفی که «پولی» علامت خورده ولی بردگی‌اش صفر و لیتر دارد، ردیفِ تیل است
    ///   • بردگی = لیتر × فی (در دفترِ تیل) و الباقی = بردگی − رسید، هر دو گِرد
    ///
    /// ⚠️ ردیفِ حذف‌شده شمرده نمی‌شود (‎DeletedAt IS NULL‎) — همان صافیِ سراسریِ
    /// EF، این‌جا دستی نوشته شده چون کوئری خام است.
    /// </summary>
    public async Task<Dictionary<long, List<DebtAccount>>> CardAccountsAsync(
        bool noInvoice = false, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();

        var ids = await db.Debtors.AsNoTracking()
            .Where(d => d.IsNoInvoice == noInvoice).Select(d => d.Id).ToListAsync(ct);

        // حساب‌ها بدونِ هیچ ردیفی
        var mains = await db.DebtAccounts.AsNoTracking()
            .Where(a => a.MainOfDebtorId != null && ids.Contains(a.MainOfDebtorId.Value))
            .ToListAsync(ct);
        var subs = await db.DebtAccounts.AsNoTracking()
            .Where(a => a.DebtorId != null && ids.Contains(a.DebtorId.Value))
            .ToListAsync(ct);

        var map = ids.ToDictionary(i => i, _ => new List<DebtAccount>());
        var byId = new Dictionary<long, DebtAccount>();
        foreach (var a in mains) { map[a.MainOfDebtorId!.Value].Add(a); byId[a.Id] = a; }
        foreach (var a in subs) { map[a.DebtorId!.Value].Add(a); byId[a.Id] = a; }

        foreach (var g in await RollupsAsync(db, ct))
        {
            if (!byId.TryGetValue(g.AccountId, out var acc)) continue;
            var row = new DebtRow
            {
                Fuel = g.Fuel,
                Liters = g.Totals.Liters,
                Rasid = g.Totals.Rasid,
                RasidFuel = g.Totals.RasidFuel,
                Albaqi = g.Totals.Albaqi,
                Bardagi = g.Totals.Bardagi,
                ByMoney = g.Money,
            };
            if (g.Money) acc.MoneyRows.Add(row); else acc.FuelRows.Add(row);
        }
        return map;
    }

    /// <summary>جمعِ هر (حساب × دفتر × سوخت) — یک‌بار، از خودِ دیتابیس.</summary>
    private static async Task<List<AccountRollup>> RollupsAsync(
        PumpDbContext db, CancellationToken ct)
    {
        const string sql = @"
            SELECT AccountId, Money, Fuel,
                   SUM(Liters)          AS SumLiters,
                   SUM(Rasid)           AS SumRasid,
                   SUM(RasidFuel)       AS SumRasidFuel,
                   SUM(Bardagi)         AS SumBardagi,
                   SUM(ROUND(Bardagi - Rasid)) AS SumAlbaqi
            FROM (
              SELECT COALESCE(r.FuelAccountId, r.MoneyAccountId) AS AccountId,
                     CASE WHEN r.FuelAccountId IS NULL THEN 1 ELSE 0 END AS Money,
                     r.Fuel AS Fuel,
                     CASE WHEN r.FuelAccountId IS NULL THEN 0
                          ELSE CAST(r.Liters AS REAL) END AS Liters,
                     CAST(r.Rasid AS REAL) AS Rasid,
                     CASE WHEN r.FuelAccountId IS NULL THEN 0
                          ELSE CAST(r.RasidFuel AS REAL) END AS RasidFuel,
                     ROUND(CASE
                       WHEN r.ByMoney = 1
                            AND NOT (CAST(r.Bardagi AS REAL) = 0 AND CAST(r.Liters AS REAL) > 0)
                       THEN CAST(r.Bardagi AS REAL)
                       WHEN CAST(r.Liters AS REAL) > 0
                       THEN CAST(r.Liters AS REAL) * COALESCE(CAST(r.PricePerLiter AS REAL), 0)
                       ELSE 0 END) AS Bardagi
              FROM DebtRows r
              WHERE r.DeletedAt IS NULL
                AND (r.FuelAccountId IS NOT NULL OR r.MoneyAccountId IS NOT NULL)
            )
            GROUP BY AccountId, Money, Fuel;";

        var list = new List<AccountRollup>();
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var totals = new FuelTotals(
                    Liters: Dec(r, 3), Rasid: Dec(r, 4), RasidFuel: Dec(r, 5),
                    Albaqi: Dec(r, 7), Bardagi: Dec(r, 6));
                list.Add(new AccountRollup(
                    r.GetInt64(0),
                    r.GetInt64(1) == 1,
                    r.GetInt64(2) == (long)FuelType.Diesel ? FuelType.Diesel : FuelType.Petrol,
                    totals));
            }
        }
        finally { if (opened) await conn.CloseAsync(); }
        return list;

        static decimal Dec(System.Data.Common.DbDataReader r, int i) =>
            r.IsDBNull(i) ? 0m : (decimal)Convert.ToDouble(r.GetValue(i));
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
        var d = await db.Debtors.AsSplitQuery()
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
        var a = await db.DebtAccounts.AsSplitQuery()
                        .Include(x => x.FuelRows).Include(x => x.MoneyRows)
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
        var a = await db.DebtAccounts.AsSplitQuery()
                        .Include(x => x.FuelRows).Include(x => x.MoneyRows)
                        .FirstOrDefaultAsync(x => x.Id == accountId, ct);
        if (a is null || a.MainOfDebtorId != null) return;   // حسابِ اصلی حذف نمی‌شود
        await _trash.RememberAsync(db, "debtaccount", a.Name ?? "", a, ct);
        db.DebtAccounts.Remove(a);
        await db.SaveChangesAsync(ct);
    }
}
