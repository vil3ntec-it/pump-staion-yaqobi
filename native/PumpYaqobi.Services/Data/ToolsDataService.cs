using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ ابزارهای «فقط نگاه» و دفترهای کوچکشان ══════════════════════════════════
/// خوراکِ چهار بخشی که هیچ‌کدام دفترِ تازه‌ای نمی‌سازند و همه‌شان خلاصهٔ ثبت‌های
/// موجودند: قرض‌های کهنه، کمبودیِ کارمندان، تاریخچهٔ نرخ و گزارشِ ماهانه —
/// به‌علاوهٔ دو دفترِ کوچکی که خودشان می‌نویسند: تخلیهٔ تانکر و تسویهٔ کمبودی.
///
/// ⚠️ هیچ‌کدام از این‌ها به حسابِ کسی دست نمی‌زنند. اگر روزی عددی این‌جا با
/// بخشِ خودش فرق کرد، اشتباه از این‌جاست نه از آن‌جا.
/// </summary>
public sealed class ToolsDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly AgingService _aging;
    private readonly StaffShortService _staff;
    private readonly MonthReportService _month;

    public ToolsDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash,
                            AgingService aging, StaffShortService staff, MonthReportService month)
    { _dbf = dbf; _perm = perm; _trash = trash; _aging = aging; _staff = staff; _month = month; }

    // ── قرض‌های کهنه ────────────────────────────────────────────────────────
    /// <summary>
    /// همهٔ قرض‌داران با همهٔ حساب‌ها و ردیف‌هایشان، سنجیده با تاریخِ امروز.
    ///
    /// ⚠️ ردیف‌ها لازم‌اند و کنار گذاشته نمی‌شوند: هم الباقی از آن‌ها می‌آید و
    /// هم «چند روز بی‌حرکت» از تاریخِ آخرین ردیف.
    /// </summary>
    public async Task<List<AgingRow>> AgingAsync(AgingFilter filter, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        var people = await LoadDebtorsAsync(ct);
        return _aging.Rows(people, filter, Shamsi.Today());
    }

    /// <summary>
    /// ══ سه کوئریِ صاف، بی هیچ ‎Include‎ ══════════════════════════════════════
    ///
    /// «قرض‌های کهنه» واقعاً همهٔ ردیف‌ها را لازم دارد، پس این‌جا چیزی برای
    /// نخواندن نیست — ولی **شکلِ** خواندن هزینه دارد. سنجشِ کارایی همین را
    /// نشان داد:
    ///
    ///     خواندنِ صافِ ۵۰٬۰۰۰ ردیف                    ۴۴۴ ms
    ///     همان ردیف‌ها از راهِ ‎Include‎              ۵٬۶۲۶ ms
    ///
    /// دقیقاً همان چیزی که در ‎DebtorService.LoadFullAsync‎ هم دیده شد. پس
    /// این‌جا هم سه کوئریِ ساده زده می‌شود — اشخاص، حساب‌ها، ردیف‌ها — و
    /// وصل کردنشان در حافظه با یک فرهنگِ کلید انجام می‌شود.
    ///
    /// ⚠️ ردیف‌ها بی هیچ صافی خوانده می‌شوند و ردیفِ حساب‌های «بی‌فاکتور» در
    /// حافظه کنار گذاشته می‌شود. صافیِ ‎Contains‎ روی SQLite به ‎json_each‎
    /// ترجمه می‌شود و ایندکس را از دست می‌دهد — یعنی همان کلِ جدول، ولی
    /// گران‌تر.
    /// </summary>
    private async Task<List<Debtor>> LoadDebtorsAsync(CancellationToken ct)
    {
        await using var db = _dbf.Create();
        var people = await db.Debtors.AsNoTracking().Where(d => !d.IsNoInvoice)
                             .OrderBy(d => d.Name).ToListAsync(ct);
        if (people.Count == 0) return people;

        var byId = people.ToDictionary(p => p.Id);
        var byAccount = new Dictionary<long, DebtAccount>();

        foreach (var a in await db.DebtAccounts.AsNoTracking().ToListAsync(ct))
        {
            if (a.MainOfDebtorId is { } m && byId.TryGetValue(m, out var owner)) owner.MainAccount = a;
            else if (a.DebtorId is { } s && byId.TryGetValue(s, out var p2)) p2.SubAccounts.Add(a);
            else continue;                      // حسابِ یک شخصِ بی‌فاکتور — به ما ربطی ندارد
            byAccount[a.Id] = a;
        }

        foreach (var r in await db.DebtRows.AsNoTracking().ToListAsync(ct))
        {
            if (r.FuelAccountId is { } f && byAccount.TryGetValue(f, out var fa)) fa.FuelRows.Add(r);
            else if (r.MoneyAccountId is { } n && byAccount.TryGetValue(n, out var ma)) ma.MoneyRows.Add(r);
        }
        return people;
    }

    // ── کمبودی/اضافیِ کارمندان ─────────────────────────────────────────────
    public async Task<List<StaffShortRow>> StaffShortAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        // ⚠️ ‎AsSplitQuery‎: «پمپ‌ها» و «تراکنش‌ها» دو مجموعهٔ کنارِ هم زیرِ یک
        // شیفت‌اند؛ در یک کوئری، هر پمپ در هر تراکنش ضرب می‌شود.
        var entries = await db.WaraqEntries.AsNoTracking().AsSplitQuery()
            .Include(w => w.Shifts).ThenInclude(s => s.Pumps)
            .Include(w => w.Shifts).ThenInclude(s => s.Transactions)
            .OrderByDescending(w => w.DateKey).ToListAsync(ct);
        var settles = await db.StaffShortSettles.AsNoTracking().ToListAsync(ct);
        return _staff.Rows(entries, settles);
    }

    public async Task<List<StaffShortSettle>> SettlesAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.StaffShortSettles.AsNoTracking()
                       .OrderByDescending(s => s.Id).ToListAsync(ct);
    }

    /// <summary>
    /// ثبتِ «رسیدِ کمبودی» یا «پرداختِ اضافی».
    ///
    /// مبلغِ بیشتر از باقی‌مانده پذیرفته نمی‌شود — همان مرزی که نسخهٔ وب پیش از
    /// ثبت می‌گذاشت. برگشتِ ‎false‎ یعنی مبلغ نامعتبر بود و چیزی نوشته نشد.
    /// </summary>
    public async Task<bool> SettleAsync(StaffShortRow row, StaffSettleKind kind, decimal amount,
                                        CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        var cap = kind == StaffSettleKind.Excess ? row.RemainExcess : row.RemainShort;
        var amt = TankDipService.JsRound(amount);
        if (amt <= 0m || amt > cap) return false;

        var today = Shamsi.Today();
        await using var db = _dbf.Create();
        db.StaffShortSettles.Add(new StaffShortSettle
        {
            LegacyId = "ss" + DateTime.UtcNow.Ticks.ToString("x"),
            NameKey = row.Key, Name = row.Name, Kind = kind, Amount = amt,
            DateShamsi = today, DateKey = Shamsi.Key(today), MonthKey = Shamsi.MonthKey(today),
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DeleteSettleAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        await using var db = _dbf.Create();
        var row = await db.StaffShortSettles.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is null) return;
        await _trash.RememberAsync(db, "staffshort",
            (row.Name ?? "") + " — " + Shamsi.Money(row.Amount), row, ct);
        db.StaffShortSettles.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    // ── تخلیهٔ تانکر ───────────────────────────────────────────────────────
    public async Task<List<TankerUnload>> UnloadsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.TankerUnloads.AsNoTracking()
                       .OrderByDescending(u => u.DateKey).ThenByDescending(u => u.Id)
                       .ToListAsync(ct);
    }

    /// <summary>
    /// ثبتِ یک تخلیه. بارنامه و تحویل هر دو لازم‌اند — همان شرطِ نسخهٔ وب،
    /// چون با یکی از این دو «کم‌آمد» بی‌معناست.
    /// </summary>
    public async Task<TankerUnload?> AddUnloadAsync(FuelType fuel, decimal manifest, decimal actual,
                                                    string? driver, string? note,
                                                    CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        if (manifest <= 0m || actual <= 0m) return null;

        var today = Shamsi.Today();
        var u = new TankerUnload
        {
            Fuel = fuel, Manifest = manifest, Actual = actual, Driver = driver, Note = note,
            DateShamsi = today, DateKey = Shamsi.Key(today), MonthKey = Shamsi.MonthKey(today),
        };
        await using var db = _dbf.Create();
        db.TankerUnloads.Add(u);
        await db.SaveChangesAsync(ct);
        return u;
    }

    public async Task DeleteUnloadAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        await using var db = _dbf.Create();
        var row = await db.TankerUnloads.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (row is null) return;
        await _trash.RememberAsync(db, "tanker",
            Shamsi.Money(row.Manifest) + " → " + Shamsi.Money(row.Actual), row, ct);
        db.TankerUnloads.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>حذفِ یک میله‌زنی — با خودش اصلاحِ دفترش هم برمی‌گردد،
    /// چون اصلاح روی همان رکورد نشسته (‎BookAdjust‎) نه در فهرستی جدا.</summary>
    public async Task DeleteDipAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        await using var db = _dbf.Create();
        var row = await db.TankDips.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (row is null) return;
        await _trash.RememberAsync(db, "tankdip", Shamsi.Money(row.Measured) + " لیتر", row, ct);
        db.TankDips.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    // ── تاریخچهٔ نرخِ اتحادیه ───────────────────────────────────────────────
    /// <summary>تازه‌ترین اول — همان ترتیبی که نسخهٔ وب با ‎unshift‎ می‌ساخت.</summary>
    public async Task<List<RateHistoryEntry>> RateHistoryAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.RateHistory.AsNoTracking()
                       .OrderByDescending(r => r.Id).ToListAsync(ct);
    }

    /// <summary>
    /// ثبتِ نرخِ تازه — فقط اگر واقعاً عوض شده باشد.
    ///
    /// ⚠️ نرخِ صفر و نرخِ برابر با آخرین ثبتِ همان سوخت نوشته نمی‌شوند، وگرنه
    /// هر بار ذخیرهٔ تنظیمات یک ردیفِ تکراری می‌ساخت و تاریخچه بی‌مصرف می‌شد.
    /// فهرست روی ۳۰۰ ردیفِ آخر می‌ماند، مثل نسخهٔ وب.
    /// </summary>
    public async Task<bool> RecordRateAsync(FuelType fuel, decimal rate, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManageSettings);
        if (!(rate > 0m)) return false;

        await using var db = _dbf.Create();
        var last = await db.RateHistory.AsNoTracking().Where(r => r.Fuel == fuel)
                           .OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        if (last is not null && last.Rate == rate) return false;

        var today = Shamsi.Today();
        db.RateHistory.Add(new RateHistoryEntry
        {
            Fuel = fuel, Rate = rate,
            DateShamsi = today, DateKey = Shamsi.Key(today), MonthKey = Shamsi.MonthKey(today),
        });
        await db.SaveChangesAsync(ct);

        var extra = await db.RateHistory.OrderByDescending(r => r.Id).Skip(300).ToListAsync(ct);
        if (extra.Count > 0)
        {
            db.RateHistory.RemoveRange(extra);
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    // ── گزارش ماهانه ───────────────────────────────────────────────────────
    /// <summary>همهٔ ثبت‌هایی که گزارشِ ماه از آن‌ها جمع می‌زند — یک‌بار خوانده می‌شوند.</summary>
    public async Task<MonthReportSource> MonthSourceAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return new MonthReportSource(
            await db.Reports.AsNoTracking()
                    .Include(r => r.DayShift).Include(r => r.NightShift).ToListAsync(ct),
            await db.Expenses.AsNoTracking().ToListAsync(ct),
            await db.ExtraIncomes.AsNoTracking().ToListAsync(ct),
            await db.FuelPurchases.AsNoTracking().ToListAsync(ct),
            await db.DebtQuickReceipts.AsNoTracking().ToListAsync(ct),
            await db.SafeEntries.AsNoTracking().ToListAsync(ct),
            await db.TankerUnloads.AsNoTracking().ToListAsync(ct));
    }

    public List<string> MonthKeys(MonthReportSource s) => _month.AllKeys(s, Shamsi.Today());
    public MonthReport Month(MonthReportSource s, string key) => _month.Compute(s, key);
}
