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
/// یک حساب، سبک — فقط آن‌چه برای «این نام مالِ کدام حساب است و واحدش چیست»
/// لازم است. ⛔ هیچ ردیفی در آن نیست و نباید بیاید: این را صفحهٔ ورق با هر
/// تایپِ کاربر می‌خواهد و خواندنِ ردیف‌ها یعنی همان کندی‌ای که قاعدهٔ سرعتِ
/// این ریپو قدغنش کرده.
/// </summary>
public readonly record struct AccountUnitRow(
    long AccountId, long PersonId, string PersonName, string AccountName,
    bool IsMain, bool HasFuelRows, bool HasMoneyRows, LedgerMode Mode);

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

    /// <summary>
    /// ══ نامِ خوانای یک حساب از روی شناسه‌اش ═══════════════════════════════
    ///
    /// خواستهٔ صاحب ریپو: «اگه یارو با کیو‌آرِ یک حساب اومد، با اسمِ همون
    /// حساب برای میرزا دیده بشه.» کیو‌آر فقط ‎d&lt;شناسه&gt;‎ را می‌برد
    /// (<see cref="Application.Services.PostingService"/> با آن کاری ندارد)،
    /// پس چتِ پشتیبانی تا امروز همان ‎d12‎ی خام را عنوان می‌کرد.
    ///
    /// ⚠️ حسابِ فرعی «شخص › فرعی» می‌شود، نه فقط نامِ فرعی: دو نفر می‌توانند
    /// حسابِ فرعیِ هم‌نام داشته باشند («دکان»، «موتر») و بی نامِ شخص معلوم
    /// نیست پیام از کدام است.
    ///
    /// <returns>خالی یعنی چنین حسابی نیست — فراخوان خودش تصمیم می‌گیرد.</returns>
    /// </summary>
    public async Task<string> AccountLabelAsync(long accountId, CancellationToken ct = default)
    {
        if (accountId <= 0) return "";
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();

        var a = await db.DebtAccounts.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == accountId, ct);
        if (a is null) return "";

        var ownerId = a.MainOfDebtorId ?? a.DebtorId;
        string? owner = ownerId is { } oid
            ? await db.Debtors.AsNoTracking()
                      .Where(d => d.Id == oid).Select(d => d.Name).FirstOrDefaultAsync(ct)
            : null;

        var self = (a.Name ?? "").Trim();
        var who = (owner ?? "").Trim();

        if (who.Length == 0) return self;
        if (a.MainOfDebtorId is not null) return who;            // حسابِ اصلی
        return self.Length == 0 || self == who ? who : who + " › " + self;
    }

    /// <summary>
    /// فقط شمارِ کارت‌های قرض‌دار.
    ///
    /// ⚠️ داشبورد پیش از این ‎ListAsync()‎ می‌زد و بعد ‎.Count‎ می‌گرفت.
    /// </summary>
    public async Task<int> CountAsync(bool noInvoice = false, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.Debtors.AsNoTracking().CountAsync(d => d.IsNoInvoice == noInvoice, ct);
    }

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

    /// <summary>
    /// «بردگیِ» یک ردیف، همان‌طور که کارت‌ها و مفاد/ضرر می‌شمارند.
    /// ⛔ یک جا — هم ‎RollupsAsync‎ و هم ‎NoInvoiceBardagiAsync‎ همین را می‌خوانند،
    /// وگرنه روزی کارتِ شرکت یک عدد می‌گفت و مفادِ همان ماه عددِ دیگر.
    /// </summary>
    private const string BardagiSql = @"ROUND(CASE
                       WHEN r.ByMoney = 1
                            AND NOT (CAST(r.Bardagi AS REAL) = 0 AND CAST(r.Liters AS REAL) > 0)
                       THEN CAST(r.Bardagi AS REAL)
                       WHEN CAST(r.Liters AS REAL) > 0
                       THEN CAST(r.Liters AS REAL) * COALESCE(CAST(r.PricePerLiter AS REAL), 0)
                       ELSE 0 END)";

    /// <summary>
    /// ══ بردگیِ «بی‌فاکتور»ها در یک دوره — برای صفحهٔ مفاد/ضرر ═════════════════
    ///
    /// همان عددی که ‎CardAccountsAsync(noInvoice: true)‎ برای «همه» می‌دهد
    /// (جمعِ دفترِ تیلِ حساب‌های قرض‌دارانِ بی‌فاکتور)، ولی فقط ردیف‌هایی که
    /// ‎DateKey‎شان در بازه است — قاعدهٔ ‎_plBreakdownData‎ی سایت: ردیف با تاریخِ
    /// خودش. <paramref name="keys"/>ِ ‎null‎ یعنی همه.
    /// </summary>
    public async Task<decimal> NoInvoiceBardagiAsync((int Lo, int Hi)? keys, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var range = keys is { } k ? $" AND r.DateKey >= {k.Lo} AND r.DateKey <= {k.Hi}" : "";
        var sql = @"
            SELECT SUM(Bardagi) FROM (
              SELECT " + BardagiSql + @" AS Bardagi
              FROM DebtRows r
              JOIN DebtAccounts a ON a.Id = r.FuelAccountId
              JOIN Debtors d ON d.Id = COALESCE(a.MainOfDebtorId, a.DebtorId)
              WHERE r.DeletedAt IS NULL AND a.DeletedAt IS NULL AND d.DeletedAt IS NULL
                AND d.IsNoInvoice = 1" + range + @"
            );";
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var v = await cmd.ExecuteScalarAsync(ct);
            return v is null || v is DBNull ? 0m : (decimal)Convert.ToDouble(v);
        }
        finally { if (opened) await conn.CloseAsync(); }
    }

    /// <summary>ماه‌هایی که ردیفِ بی‌فاکتور دارند («1405/07») — کشوی دورهٔ مفاد/ضرر.</summary>
    public async Task<List<string>> NoInvoiceMonthsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var ids = await db.Debtors.AsNoTracking().Where(d => d.IsNoInvoice).Select(d => d.Id).ToListAsync(ct);
        if (ids.Count == 0) return new();
        var accts = await db.DebtAccounts.AsNoTracking()
            .Where(a => (a.MainOfDebtorId != null && ids.Contains(a.MainOfDebtorId.Value))
                     || (a.DebtorId != null && ids.Contains(a.DebtorId.Value)))
            .Select(a => a.Id).ToListAsync(ct);
        var keys = await db.DebtRows.AsNoTracking()
            .Where(r => r.FuelAccountId != null && accts.Contains(r.FuelAccountId.Value) && r.DateKey > 0)
            .Select(r => r.DateKey / 100).Distinct().ToListAsync(ct);
        return keys.Select(k => $"{k / 100:0000}/{k % 100:00}").ToList();
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
                     " + BardagiSql + @" AS Bardagi
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

    /// <summary>
    /// ══ همهٔ قرض‌داران با همهٔ ردیف‌هایشان — در چهار کوئری ═══════════════════
    ///
    /// ⚠️ چرا این لازم شد: هر جایی که بخواهد «فهرست + ردیف‌ها» داشته باشد،
    /// وسوسه می‌شود <see cref="LoadFullAsync"/> را در یک حلقه صدا بزند. آن
    /// یعنی چهار رفت‌وبرگشت به دیتابیس <b>برای هر نفر</b> — با پانصد قرض‌دار،
    /// دو هزار کوئری. این‌جا همان کار با چهار کوئریِ ثابت انجام می‌شود، هر چند
    /// نفر که باشند.
    ///
    /// ⚠️ ردیفِ حذف‌شده نمی‌آید (صافیِ سراسریِ EF) و ترتیبِ ردیف‌ها همان
    /// ترتیبِ جدولِ روی صفحه است (‎SortIndex‎ بعد ‎Id‎)، تا هر نمایی که از این
    /// می‌خواند همان چیزی را ببیند که کاربر در برنامه می‌بیند.
    ///
    /// ⚠️ «دفترِ رسیدهای سربرگ» (‎RasidLog‎) پیش‌فرض نمی‌آید: این تابع برای
    /// <b>خواندن و نشان دادن</b> است، نه برای ویرایش. هر کسی که می‌خواهد
    /// چیزی را عوض کند باید از <see cref="LoadFullAsync"/> برود. گزارشی که
    /// تاریخِ واقعیِ هر رسید را لازم دارد («زیان ناشی از افزایش قیمت»)
    /// ‎withReceipts‎ می‌دهد و همان دفتر را هم — باز هم فقط برای خواندن —
    /// با یک پرس‌وجو می‌گیرد.
    /// </summary>
    /// <summary>
    /// ══ «این نام مالِ کدام حساب است، و واحدش تیل است یا پول؟» ═══════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «اسمِ قرض‌دار رو توی نامِ
    /// تراکنش‌ها می‌نویسم و سیستم اتومات تشخیص بده که واحدِ این حساب تیل است
    /// یا پول… این‌ها برای حساب‌های فرعی هم صدق بشه.»
    ///
    /// ⛔ <b>هیچ ردیفی خوانده نمی‌شود.</b> «این دفتر ردیف دارد یا نه» با دو
    /// <c>Distinct</c> روی همان دو ایندکسِ <c>FuelAccountId</c> و
    /// <c>MoneyAccountId</c> درمی‌آید — نه با خواندنِ ردیف‌ها. قاعدهٔ
    /// همیشگیِ این ریپو: «برای یک جمع، همهٔ ردیف‌ها را نخوان.»
    ///
    /// ⚠️ حساب‌های <b>فرعی</b> هم در فهرست‌اند و با همان نامِ خودشان — چون
    /// «حسابِ فرعی هم یک حسابِ کامل است» و کاربر نامِ فرعی را هم در ورق
    /// می‌نویسد.
    ///
    /// ⚠️ صدا زننده باید نتیجه را با <c>PumpDbContext.Version</c> کَش کند؛
    /// این تابع خودش کَشی ندارد.
    /// </summary>
    public async Task<List<AccountUnitRow>> AccountUnitsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();

        var people = await db.Debtors.AsNoTracking()
            .Select(d => new { d.Id, d.Name }).ToListAsync(ct);
        if (people.Count == 0) return new List<AccountUnitRow>();

        var names = people.ToDictionary(p => p.Id, p => p.Name ?? "");

        var accts = await db.DebtAccounts.AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.Mode, a.MainOfDebtorId, a.DebtorId })
            .ToListAsync(ct);

        var withFuel = (await db.DebtRows.AsNoTracking()
            .Where(r => r.FuelAccountId != null).Select(r => r.FuelAccountId!.Value)
            .Distinct().ToListAsync(ct)).ToHashSet();
        var withMoney = (await db.DebtRows.AsNoTracking()
            .Where(r => r.MoneyAccountId != null).Select(r => r.MoneyAccountId!.Value)
            .Distinct().ToListAsync(ct)).ToHashSet();

        var outp = new List<AccountUnitRow>(accts.Count);
        foreach (var a in accts)
        {
            var pid = a.MainOfDebtorId ?? a.DebtorId ?? 0;
            if (pid == 0 || !names.TryGetValue(pid, out var pname)) continue;
            outp.Add(new AccountUnitRow(
                a.Id, pid, pname, a.Name ?? "",
                a.MainOfDebtorId != null,
                withFuel.Contains(a.Id), withMoney.Contains(a.Id), a.Mode));
        }
        return outp;
    }

    public async Task<List<Debtor>> LoadAllAsync(bool noInvoice = false,
                                                 CancellationToken ct = default,
                                                 bool withReceipts = false)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();

        var people = await db.Debtors.AsNoTracking()
            .Where(d => d.IsNoInvoice == noInvoice)
            .OrderBy(d => d.Name).ToListAsync(ct);
        if (people.Count == 0) return people;

        var ids = people.Select(p => p.Id).ToList();
        var mains = await db.DebtAccounts.AsNoTracking()
            .Where(a => a.MainOfDebtorId != null && ids.Contains(a.MainOfDebtorId.Value))
            .ToListAsync(ct);
        var subs = await db.DebtAccounts.AsNoTracking()
            .Where(a => a.DebtorId != null && ids.Contains(a.DebtorId.Value))
            .ToListAsync(ct);

        var byAcct = new Dictionary<long, DebtAccount>();
        foreach (var a in mains) byAcct[a.Id] = a;
        foreach (var a in subs) byAcct[a.Id] = a;
        foreach (var a in byAcct.Values) { a.FuelRows = new(); a.MoneyRows = new(); a.RasidLog = new(); }

        if (byAcct.Count > 0)
        {
            var accIds = byAcct.Keys.ToList();
            var rows = await db.DebtRows.AsNoTracking()
                .Where(r => (r.FuelAccountId != null && accIds.Contains(r.FuelAccountId.Value))
                         || (r.MoneyAccountId != null && accIds.Contains(r.MoneyAccountId.Value)))
                .OrderBy(r => r.SortIndex).ThenBy(r => r.Id)
                .ToListAsync(ct);

            foreach (var r in rows)
            {
                // ⚠️ ترتیبِ این دو مهم است: ردیفِ دفترِ تیل ‎FuelAccountId‎ دارد
                // و ردیفِ دفترِ پول ‎MoneyAccountId‎ — هرگز هر دو.
                if (r.FuelAccountId is { } f && byAcct.TryGetValue(f, out var fa)) fa.FuelRows.Add(r);
                else if (r.MoneyAccountId is { } m && byAcct.TryGetValue(m, out var ma)) ma.MoneyRows.Add(r);
            }

            if (withReceipts)
            {
                var log = await db.RasidEntries.AsNoTracking()
                    .Where(e => accIds.Contains(e.AccountId))
                    .OrderBy(e => e.SortIndex).ThenBy(e => e.Id)
                    .ToListAsync(ct);
                foreach (var e in log)
                    if (byAcct.TryGetValue(e.AccountId, out var a)) a.RasidLog.Add(e);
            }
        }

        var mainOf = new Dictionary<long, DebtAccount>();
        foreach (var a in mains) mainOf[a.MainOfDebtorId!.Value] = a;

        var subsOf = new Dictionary<long, List<DebtAccount>>();
        foreach (var a in subs)
        {
            if (!subsOf.TryGetValue(a.DebtorId!.Value, out var list))
                subsOf[a.DebtorId!.Value] = list = new List<DebtAccount>();
            list.Add(a);
        }

        foreach (var p in people)
        {
            p.MainAccount = mainOf.TryGetValue(p.Id, out var mm) ? mm : new DebtAccount();
            p.SubAccounts = subsOf.TryGetValue(p.Id, out var ss) ? ss : new List<DebtAccount>();
        }
        return people;
    }

    /// <summary>
    /// ══ حساب‌هایی که کیو‌آرِ زنده دارند ═══════════════════════════════════════
    ///
    /// فقط حساب‌هایی که ‎QrKey‎ دارند — یعنی دستِ‌کم یک بار کیو‌آرشان ساخته
    /// شده — با همهٔ ردیف‌های هر دو دفترشان و نامِ صاحبِ حساب. برای انتشارِ
    /// زندهٔ حساب به مشتری (‎AcctLive‎).
    ///
    /// ⚠️ شمارِ کوئری ثابت است (سه تا)، نه «برای هر حساب یکی». و اگر هیچ
    /// حسابی کیو‌آر نداشته باشد با یک کوئریِ خالی برمی‌گردد — حلقهٔ
    /// بیست‌ثانیه‌ای نباید روی پمپی که کیو‌آر نداده هزینه‌ای داشته باشد.
    /// </summary>
    public async Task<List<(Debtor Person, DebtAccount Account)>> LoadQrAccountsAsync(
        CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();

        var accts = await db.DebtAccounts.AsNoTracking()
            .Where(a => a.QrKey != null && a.QrKey != "")
            .ToListAsync(ct);
        if (accts.Count == 0) return new();

        var pids = accts.Select(a => a.MainOfDebtorId ?? a.DebtorId ?? 0).Distinct().ToList();
        var people = await db.Debtors.AsNoTracking()
            .Where(d => pids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        var byAcct = accts.ToDictionary(a => a.Id);
        foreach (var a in accts) { a.FuelRows = new(); a.MoneyRows = new(); a.RasidLog = new(); }
        var accIds = byAcct.Keys.ToList();
        var rows = await db.DebtRows.AsNoTracking()
            .Where(r => (r.FuelAccountId != null && accIds.Contains(r.FuelAccountId.Value))
                     || (r.MoneyAccountId != null && accIds.Contains(r.MoneyAccountId.Value)))
            .OrderBy(r => r.SortIndex).ThenBy(r => r.Id)
            .ToListAsync(ct);
        foreach (var r in rows)
        {
            if (r.FuelAccountId is { } f && byAcct.TryGetValue(f, out var fa)) fa.FuelRows.Add(r);
            else if (r.MoneyAccountId is { } m && byAcct.TryGetValue(m, out var ma)) ma.MoneyRows.Add(r);
        }

        var list = new List<(Debtor, DebtAccount)>();
        foreach (var a in accts)
        {
            var pid = a.MainOfDebtorId ?? a.DebtorId ?? 0;
            if (people.TryGetValue(pid, out var p)) list.Add((p, a));
        }
        return list;
    }

    /// <summary>
    /// شمارِ همهٔ ردیف‌های زندهٔ قرض‌داران — یک ‎COUNT‎ی ساده، بی خواندنِ حتی
    /// یک ردیف.
    ///
    /// ⚠️ برای این است که هیچ‌کس دوباره از راهِ «همهٔ ردیف‌های همهٔ حساب‌ها را
    /// بخوان و بعد ببین چند تا بود» نرود — همان راهی که برنامه را می‌خواباند.
    /// </summary>
    public async Task<int> RowCountAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.DebtRows.AsNoTracking().CountAsync(ct);
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
        MarkOnly(db, d);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAccountAsync(DebtAccount a, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        MarkOnly(db, a);
        await db.SaveChangesAsync(ct);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ⛔ حسابِ بزرگ را با ردیف‌هایش به ‎EF‎ ندهید
    // ══════════════════════════════════════════════════════════════════════════
    //
    //  ‎DbSet.Attach(a)‎ گرافِ موجودیت را می‌پیماید: هر ‎DebtRow‎ی که در
    //  ‎a.FuelRows‎ و ‎a.MoneyRows‎ نشسته باشد ردیابی می‌شود، و بعد
    //  ‎SaveChanges‎ روی همان‌ها ‎DetectChanges‎ می‌دود. برای حسابی که تازه از
    //  ‎LoadFullAsync‎ آمده یعنی **همهٔ** ردیف‌هایش.
    //
    //  عددش را ‎personperf‎ داد: در حسابِ ۵۰٬۰۰۰ ردیفی، یک «مهرِ مهاجرت» که
    //  فقط یک ستونِ ‎bool‎ را عوض می‌کند **۱۲٬۴۵۶ میلی‌ثانیه** طول می‌کشید،
    //  در حالی که ساختنِ هر ۵۰٬۰۰۰ ویومدلِ ردیف ۲۱ میلی‌ثانیه بود. و شکلش
    //  خطی نبود: ۲۰٬۰۰۰ ردیف ۲٬۲۰۲ms و ۵۰٬۰۰۰ ردیف ۱۲٬۴۵۶ms — دو و نیم
    //  برابر ردیف، پنج برابر وقت.
    //
    //  ⚠️ ‎AutoDetectChangesEnabled = false‎ لازم است، نه تجمل: بی آن،
    //  ‎Entry(...)‎ و ‎SaveChanges‎ هر دو ‎DetectChanges‎ می‌زنند و همان
    //  پیمایشِ گراف از درِ دیگر برمی‌گردد.
    //
    //  ⚠️ و چرا بی‌خطر است: ‎State = Modified‎ همهٔ ستون‌های خودِ موجودیت را
    //  «عوض شده» می‌کند، پس ‎UPDATE‎ همان است که بود. ردیف‌ها از این در
    //  ذخیره نمی‌شدند (‎SaveRowAsync‎ کارِ خودش را دارد) — فقط شمرده و
    //  پیموده می‌شدند.
    private static void MarkOnly<T>(PumpDbContext db, T entity) where T : class
    {
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        db.Entry(entity).State = EntityState.Modified;
    }

    /// <summary>
    /// ══ مهاجرتِ رسیدها، یک‌بار و برای همیشه ═════════════════════════════════
    ///
    /// <see cref="PumpYaqobi.Application.Services.DebtCalculationService.MigrateReceiptsToRows"/>
    /// در **حافظه** سه کار می‌کند: ردیف‌های رسید را می‌سازد، ‎RasidLog‎ را
    /// خالی می‌کند و ‎ReceiptsMigrated‎ را راست می‌گذارد. تا امروز فقط کارِ
    /// اول روی دیسک می‌نشست — پس هر بار که همان حساب باز می‌شد، همان
    /// رکوردهای ‎RasidEntries‎ دوباره خوانده و دوباره به ردیف تبدیل می‌شدند.
    ///
    /// یعنی نه فقط کندی (‎enterperf‎: شش ‎INSERT‎ و بیست‌وچهار ‎UPDATE‎ با هر
    /// باز کردن)، بلکه **خرابیِ داده**: رسیدِ مشتری با هر باز کردنِ حساب یک
    /// بار دیگر در جدول می‌نشست.
    ///
    /// پس هر سه کار این‌جا در **یک** ‎SaveChanges‎ می‌نشیند: ردیف‌های تازه،
    /// پاک شدنِ دفترِ کهنه، و مهرِ «مهاجرت شد» روی خودِ حساب.
    /// </summary>
    public async Task CommitReceiptMigrationAsync(
        DebtAccount a, IReadOnlyList<DebtRow> made, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();

        foreach (var r in made)
        {
            r.DateKey = Shamsi.Key(r.DateShamsi);
            if (r.Id == 0) db.DebtRows.Add(r);
            else { db.DebtRows.Attach(r); db.Entry(r).State = EntityState.Modified; }
        }

        if (a.Id != 0)
        {
            var old = await db.RasidEntries.Where(x => x.AccountId == a.Id).ToListAsync(ct);
            if (old.Count > 0) db.RasidEntries.RemoveRange(old);

            //  ⚠️ همان قاعدهٔ ‎MarkOnly‎ — چرایی‌اش آن‌جا نوشته شده. این یکی
            //  بعد از ‎Add‎ی ردیف‌های تازه می‌آید، پس خاموش کردنِ تشخیصِ
            //  خودکار چیزی را از قلم نمی‌اندازد: هر سه کار صریح‌اند.
            MarkOnly(db, a);
        }

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

    /// <summary>
    /// ⚠️ **فقط شمارِ جدول‌های آرشیو** — بی خواندنِ خودِ ردیف‌ها.
    ///
    /// صفحهٔ حساب برای نوشتنِ «🗂️ آرشیو — جدول N» تنها به همین عدد نیاز دارد،
    /// ولی تا امروز <see cref="ListArchivesAsync"/> را صدا می‌زد و
    /// <c>.Count</c> می‌گرفت — یعنی `RowsJson`ِ **همهٔ** جدول‌های آرشیوِ همان
    /// حساب (هر کدام صدها ردیفِ سریال‌شده) با هر باز کردنِ حساب از دیسک
    /// می‌آمد. با ده سال آرشیو، همان چالهٔ همیشگیِ «همهٔ ردیف‌ها را بخوان
    /// برای یک عدد». اسکنِ کاملِ ۱۴۰۵/۰۶/۳۰ پیدایش کرد.
    /// </summary>
    public async Task<int> CountArchivesAsync(long accountId, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.DebtTableArchives.AsNoTracking()
            .CountAsync(x => x.AccountId == accountId, ct);
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

    /// <summary>
    /// ویرایشِ یک جدولِ آرشیو — ‎updateHistoryRow‎ · ‎updateHistoryPercent‎ ·
    /// ‎histRasidBlur‎ی سایت: ردیف‌ها، فیصدی‌ها، رسیدِ سربرگ و یادداشتِ همان آرشیو.
    /// فقط خودِ آرشیو عوض می‌شود؛ جدولِ زنده و بقیهٔ آرشیوها دست نمی‌خورند.
    /// </summary>
    public async Task UpdateArchiveAsync(DebtTableArchive h, IReadOnlyList<DebtRow> rows, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        h.RowsJson = System.Text.Json.JsonSerializer.Serialize(rows, ArchiveJson);
        h.RowCount = rows.Count;
        await using var db = _dbf.Create();
        db.DebtTableArchives.Attach(h);
        db.Entry(h).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
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
