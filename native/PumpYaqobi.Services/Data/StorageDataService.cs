using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ مخزن ══════════════════════════════════════════════════════════════════
/// خریدهای تیل، میله‌زنی و تخلیهٔ تانکر.
///
/// هر خرید دو کارِ جانبی هم دارد، همان‌طور که در نسخهٔ وب داشت:
///   ۱) «فی لیترِ خرید» همان سوخت به‌روز می‌شود.
///   ۲) همان خرید در حسابِ شرکتِ هم‌نامِ فروشنده هم ثبت می‌شود.
/// </summary>
public sealed class StorageDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly StorageService _calc;
    private readonly SettingsService _settings;
    private readonly CompanyDataService _companies;

    public StorageDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash,
                              StorageService calc, SettingsService settings, CompanyDataService companies)
    { _dbf = dbf; _perm = perm; _trash = trash; _calc = calc; _settings = settings; _companies = companies; }

    public async Task<List<FuelPurchase>> PurchasesAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.FuelPurchases.AsNoTracking().Where(p => p.Fuel == fuel)
                       .OrderByDescending(p => p.DateKey).ThenByDescending(p => p.Id).ToListAsync(ct);
    }

    /// <summary>
    /// قیمتِ خریدِ هر لیترِ هر خرید — فقط چهار ستون (۱۴۰۵/۰۷/۱۸). برای «سودِ واقعی»ِ مفاد و
    /// ضرر و «تبدیلِ تیل»؛ فقط خریدهایی که قیمت دارند.
    /// </summary>
    public async Task<List<PumpYaqobi.Application.Services.BuyPrice>> BuyPricesAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var rows = await db.FuelPurchases.AsNoTracking()
                           .Select(p => new { p.Fuel, p.DateKey, p.Id, p.PerLiter }).ToListAsync(ct);
        return rows.Where(r => r.PerLiter > 0)
                   .Select(r => new PumpYaqobi.Application.Services.BuyPrice(r.Fuel, r.DateKey, r.Id, r.PerLiter))
                   .ToList();
    }

    /// <summary>هر دو تیل، به ترتیبِ ثبت — برای کادرِ «خریدهای شرکت» و «جستجوی خرید».</summary>
    public async Task<List<FuelPurchase>> AllPurchasesAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.FuelPurchases.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);
    }

    public async Task<List<ParchaReport>> ReportsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.Reports.AsNoTracking()
                       .Include(r => r.DayShift).Include(r => r.NightShift)
                       .Where(r => r.Fuel == fuel).ToListAsync(ct);
    }

    /// <summary>
    /// ثبتِ خرید. عددها حساب و ذخیره می‌شوند، «فی لیترِ خرید» به‌روز می‌شود و
    /// اگر فروشنده نام داشته باشد، همان خرید در حسابِ شرکتِ او هم می‌نشیند.
    /// </summary>
    public async Task<FuelPurchase> AddPurchaseAsync(FuelPurchase p, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        _calc.Apply(p);
        p.DateKey = Shamsi.Key(p.DateShamsi);
        p.LegacyId ??= "fe" + Guid.NewGuid().ToString("N")[..10];

        await using (var db = _dbf.Create())
        {
            db.FuelPurchases.Add(p);
            await db.SaveChangesAsync(ct);
        }

        RememberPerLiter(p);

        await LinkToCompanyAsync(p, ct);
        return p;
    }

    /// <summary>
    /// «فی لیترِ خرید» (فایدهٔ فی لیترِ پارچه) — فقط از خریدی که قیمت دارد.
    ///
    /// ⛔ از ۱۴۰۵/۰۷/۱۶ قیمتِ هر تن و نرخِ دالر اختیاری‌اند (خواستهٔ صاحب ریپو:
    /// «شاید شرکت نگفته باشد»). خریدِ بی‌قیمت فی‌لیترِ صفر دارد، و نشاندنِ آن
    /// صفر یعنی فایدهٔ همهٔ پارچه‌های بعدی کلِ فروش خوانده شود. پس صفر هیچ‌وقت
    /// نمی‌نشیند؛ عددِ پیشین می‌ماند تا قیمت با ✏️ نوشته شود.
    /// </summary>
    private void RememberPerLiter(FuelPurchase p)
    {
        if (p.PerLiter <= 0m) return;
        _settings.Set(p.Fuel == FuelType.Diesel
            ? SettingsService.BuyPerLiterDiesel : SettingsService.BuyPerLiterPetrol, p.PerLiter);
    }

    /// <summary>
    /// ‎syncPurchaseToCompany(entry)‎ — یک خرید را در حسابِ شرکتِ هم‌نامِ
    /// فروشنده می‌نشاند. شرکت اگر نباشد ساخته می‌شود، و اگر باشد همان به کار
    /// می‌رود (تطبیقِ چهارپله‌ای) تا شرکتِ تکراری ساخته نشود.
    /// </summary>
    private async Task<bool> LinkToCompanyAsync(FuelPurchase p, CancellationToken ct)
    {
        var seller = (p.Seller ?? "").Trim();
        if (seller.Length == 0) return false;

        var company = await _companies.EnsureByNameAsync(seller, ct);
        await _companies.PutReceiptAsync(company.Id, p.Fuel, new CompanyRow
        {
            Name = seller,
            DateShamsi = p.DateShamsi,
            Ton = p.Ton,
            Usd = p.PriceTon,
            Rate = p.UsdRate,
            SourcePurchaseId = p.LegacyId,
        }, ct);
        return true;
    }

    /// <summary>
    /// ══ ‎syncAllPurchasesToCompanies()‎ ═════════════════════════════════════
    /// هر خریدی که هنوز در حسابِ هیچ شرکتی نیست (خریدهای کهنه یا جامانده)
    /// خودکار به حسابِ شرکتِ هم‌نامِ فروشنده اضافه می‌شود.
    ///
    /// دو چیز از قلم نمی‌افتد:
    ///   • خریدی که از پیش وصل است دوباره وصل نمی‌شود (‎SourcePurchaseId‎).
    ///   • خریدی که کاربر خودش ردیفش را از حساب پاک کرده، برنمی‌گردد
    ///     (فهرستِ «دستی جدا شده»). بی این، کاربر هرگز نمی‌توانست ردیفی را
    ///     برای همیشه بردارد.
    /// </summary>
    public async Task<int> SyncAllPurchasesToCompaniesAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);

        List<FuelPurchase> entries;
        HashSet<string> linked;
        await using (var db = _dbf.Create())
        {
            entries = await db.FuelPurchases.AsNoTracking().OrderBy(e => e.Id).ToListAsync(ct);
            if (entries.Count == 0) return 0;
            linked = (await db.CompanyRows.AsNoTracking()
                              .Where(r => r.SourcePurchaseId != null)
                              .Select(r => r.SourcePurchaseId!).ToListAsync(ct))
                     .ToHashSet(StringComparer.Ordinal);
        }

        var unlinked = await _companies.UnlinkedPurchasesAsync(ct);

        var added = 0;
        foreach (var e in entries)
        {
            var key = e.LegacyId ?? "";
            if (key.Length == 0) continue;
            if (linked.Contains(key)) continue;
            if (unlinked.Contains(key)) continue;      // کاربر خودش برداشته
            if (string.IsNullOrWhiteSpace(e.Seller)) continue;
            if (await LinkToCompanyAsync(e, ct)) added++;
        }
        return added;
    }

    /// <summary>
    /// ویرایشِ یک خرید — و رساندنِ همان تغییر به حسابِ شرکت.
    ///
    /// ⛔ <b>تا امروز نمی‌رسید.</b> تُن، فیِ تن، نرخِ دالر، فروشنده و نوعِ تیل
    /// را می‌شد عوض کرد و حسابِ شرکت تا ابد عددِ کهنه را نگه می‌داشت — بی هیچ
    /// نشانه‌ای. دقیقاً همان «یک عددِ اشتباه در حساب» که نباید بماند.
    ///
    /// ⚠️ <b>خریدی که کاربر خودش ردیفش را از حساب برداشته، برنمی‌گردد.</b>
    /// همان قاعدهٔ «دستی جدا شده»: فقط خریدی دوباره نوشته می‌شود که همین
    /// حالا ردیفی در حسابِ شرکتی دارد. و فروشنده که خالی شود، ردیف برداشته
    /// می‌شود، نه این‌که با نامِ خالی بماند.
    /// </summary>
    public async Task UpdatePurchaseAsync(FuelPurchase p, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        _calc.Apply(p);
        p.DateKey = Shamsi.Key(p.DateShamsi);
        await using (var db = _dbf.Create())
        {
            db.FuelPurchases.Attach(p);
            db.Entry(p).State = EntityState.Modified;
            await db.SaveChangesAsync(ct);
        }

        //  قیمتِ تازهٔ **تازه‌ترین** خریدِ همین تیل ⇒ فی‌لیترِ خرید هم تازه (همان
        //  کاری که ثبت می‌کرد)؛ ویرایشِ خریدِ کهنه عددِ امروز را عوض نمی‌کند.
        await using (var db = _dbf.Create())
        {
            var newest = await db.FuelPurchases.AsNoTracking().Where(x => x.Fuel == p.Fuel)
                                 .OrderByDescending(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync(ct);
            if (newest == p.Id) RememberPerLiter(p);
        }

        if (!await _companies.HasPurchaseRowAsync(p.LegacyId, ct))
        {
            //  ⛔ فروشنده‌ای که پس از ثبت نوشته شد ⇒ همان لحظه در حسابِ شرکتش —
            //  مگر کاربر خودش ردیفِ این خرید را از حساب برداشته باشد («دستی جدا شده»).
            if (string.IsNullOrWhiteSpace(p.Seller) || string.IsNullOrEmpty(p.LegacyId)) return;
            if ((await _companies.UnlinkedPurchasesAsync(ct)).Contains(p.LegacyId)) return;
            await LinkToCompanyAsync(p, ct);
            return;
        }
        if (string.IsNullOrWhiteSpace(p.Seller)) { await _companies.UnlinkPurchaseAsync(p.LegacyId, ct); return; }

        //  ⛔ خریدِ پیوندخورده همان شرکتش را نگه می‌دارد (۱۴۰۵/۰۷/۱۸) — شرکت دیگر از
        //  روی نامِ تایپی از نو جسته نمی‌شود، پس ویرایشِ قیمت یا نام هیچ‌وقت
        //  شرکتِ تازه یا تکراری نمی‌سازد (`EditTarget`).
        var link = await _companies.PurchaseLinkAsync(p.LegacyId, ct);
        if (link is null) { await LinkToCompanyAsync(p, ct); return; }
        var decision = EditTarget(link, p.Seller!, await _companies.CompaniesOnlyAsync(ct));
        if (decision.RenameTo is { } nm) await _companies.RenameAsync(link.CompanyId, nm, ct);
        await PutIntoAsync(decision.CompanyId, p, ct);
    }

    /// <summary>
    /// ══ ویرایشِ خریدِ پیوندخورده ⇒ کدام شرکت (خالص، آزمون‌دار) ══════════════
    ///
    ///   • نامِ فروشنده عوض نشده (یا فقط قیمت و تُن) ⇒ همان شرکت.
    ///   • نام عوض شده و دقیقاً نامِ شرکتِ <b>دیگرِ موجودی</b> است ⇒ ردیف به آن
    ///     شرکت می‌رود (هیچ شرکتی ساخته نمی‌شود).
    ///   • نام عوض شده و شرکت فقط همین خرید را دارد ⇒ همان شرکت نامش درست
    ///     می‌شود (ویرایشِ همان شرکت، نه ساختنِ دومی).
    ///   • نام عوض شده ولی شرکت ردیف‌های دیگری هم دارد ⇒ همان شرکت می‌ماند و نامِ
    ///     شرکت دست نمی‌خورد (حسابِ دیگران عوض نمی‌شود)؛ فقط ردیفِ همین خرید نامِ تازه
    ///     را می‌گیرد.
    /// ⛔ در هیچ حالتی شرکتِ تازه ساخته نمی‌شود.
    /// </summary>
    public static (long CompanyId, string? RenameTo) EditTarget(PurchaseLink link, string seller,
                                                               IEnumerable<TilCompany> companies)
    {
        var q = CompanyDataService.NormalizeName(seller);
        if (q.Length == 0 || q == CompanyDataService.NormalizeName(link.RowName)
                          || q == CompanyDataService.NormalizeName(link.CompanyName))
            return (link.CompanyId, null);

        var exact = companies.FirstOrDefault(c => c.Id != link.CompanyId
                                                 && CompanyDataService.NormalizeName(c.Name) == q);
        if (exact is not null) return (exact.Id, null);

        return link.HasOtherRows ? (link.CompanyId, null) : (link.CompanyId, seller.Trim());
    }

    private Task PutIntoAsync(long companyId, FuelPurchase p, CancellationToken ct) =>
        _companies.PutReceiptAsync(companyId, p.Fuel, new CompanyRow
        {
            Name = (p.Seller ?? "").Trim(),
            DateShamsi = p.DateShamsi,
            Ton = p.Ton,
            Usd = p.PriceTon,
            Rate = p.UsdRate,
            SourcePurchaseId = p.LegacyId,
        }, ct);

    public async Task DeletePurchaseAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var p = await db.FuelPurchases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return;
        await _trash.RememberAsync(db, "purchase", (p.Seller ?? "") + " — " + (p.DateShamsi ?? ""), p, ct);
        db.FuelPurchases.Remove(p);
        await db.SaveChangesAsync(ct);

        // ⛔ ردیفِ حسابِ شرکت عمداً دست‌نخورده می‌ماند — همان چیزی که نسخهٔ
        // وب هم در پرسشِ قبل از حذف صریح می‌گوید: «ردیفِ همین خرید در
        // حساب شرکت دست‌نخورده می‌ماند — اگر آن را هم نمی‌خواهید، از داخلِ حساب
        // شرکت جداگانه پاکش کنید». دفترِ شرکت حسابِ دادوستد است، نه آینهٔ
        // فهرستِ مخزن؛ برداشتنِ خودکارش یعنی بدهیِ واقعیِ شرکت بی‌خبر کم شود.
        // سنجه‌اش: PurchaseCompanyParityTests.DeletingAPurchaseLeavesTheCompanyRowAlone
    }

    /// <summary>جمعِ دو ستونِ شیفت‌ها برای یک تیل — همان چیزی که مخزن و مفاد/ضرر واقعاً می‌خواهند.</summary>
    public readonly record struct ShiftSums(decimal Sale, decimal Profit);

    /// <summary>
    /// ══ فقط دو ستون، نه کلِ پارچه ═════════════════════════════════════════
    ///
    /// «فروش»ِ مخزن و «مفاد»ِ صفحهٔ مفاد/ضرر هر دو یک جمع‌اند روی
    /// <see cref="ShiftData.Sale"/> و <see cref="ShiftData.Profit"/>ِ هر دو
    /// شیفتِ همهٔ پارچه‌های یک تیل. تا ۱۴۰۵/۰۷/۱۳ هر دو صفحه برای همین دو
    /// عدد <see cref="ReportsAsync"/> را می‌زدند: **همهٔ** پارچه‌های پنج سال
    /// با هر دو شیفت به شیءِ کامل (سه موجودیت برای هر روز) — با هر فعال‌سازی،
    /// و در مخزن با هر ویرایشِ هر خرید. و چون SQLite کارِ «async»ش را روی
    /// همان نخِ صداکننده می‌کند، آن خواندن روی نخِ رابط بود.
    ///
    /// این‌جا فقط چهار ستون از هر پارچه خوانده می‌شود (‎LEFT JOIN‎ی که خودِ
    /// EF برای نویگیشنِ تهی‌پذیر می‌سازد) و جمع در حافظه با ‎decimal‎ است —
    /// ⚠️ نه ‎SUM‎ و نه ‎CAST‎ به SQLite داده نمی‌شود: مبلغ‌ها متن ذخیره
    /// می‌شوند (همان قاعدهٔ ‎LedgerService.SumAsync‎).
    /// </summary>
    public Task<ShiftSums> ShiftSumsAsync(FuelType fuel, CancellationToken ct = default) =>
        ShiftSumsAsync(fuel, null, ct);

    /// <summary>
    /// همان جمع، فقط برای پارچه‌هایی که ‎DateKey‎شان در بازهٔ <paramref name="keys"/>
    /// است — دورهٔ ماه/سالِ صفحهٔ مفاد/ضرر. ‎null‎ یعنی همه (همان بالا).
    /// </summary>
    public async Task<ShiftSums> ShiftSumsAsync(FuelType fuel, (int Lo, int Hi)? keys,
                                                CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.Reports.AsNoTracking().Where(r => r.Fuel == fuel);
        if (keys is { } k) q = q.Where(r => r.DateKey >= k.Lo && r.DateKey <= k.Hi);
        var rows = await q
                           .Select(r => new
                           {
                               DaySale = (decimal?)r.DayShift!.Sale,
                               DayProfit = (decimal?)r.DayShift!.Profit,
                               NightSale = (decimal?)r.NightShift!.Sale,
                               NightProfit = (decimal?)r.NightShift!.Profit,
                           })
                           .ToListAsync(ct);
        decimal sale = 0, profit = 0;
        foreach (var r in rows)
        {
            sale += (r.DaySale ?? 0m) + (r.NightSale ?? 0m);
            profit += (r.DayProfit ?? 0m) + (r.NightProfit ?? 0m);
        }
        return new ShiftSums(sale, profit);
    }

    /// <summary>ماه‌هایی که پارچه دارند («1405/07») — فقط برای کشوی دورهٔ مفاد/ضرر.</summary>
    public async Task<List<string>> ReportMonthsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var keys = await db.Reports.AsNoTracking().Where(r => r.DateKey > 0)
                           .Select(r => r.DateKey / 100).Distinct().ToListAsync(ct);
        return keys.Select(k => $"{k / 100:0000}/{k % 100:00}").ToList();
    }

    // ── میله‌زنی ───────────────────────────────────────────────────────────
    public async Task<List<TankDip>> DipsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.TankDips.AsNoTracking().Where(d => d.Fuel == fuel)
                       .OrderByDescending(d => d.DateKey).ToListAsync(ct);
    }

    public async Task SaveDipAsync(TankDip d, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        d.DateKey = Shamsi.Key(d.DateShamsi);
        d.MonthKey = Shamsi.MonthKey(d.DateShamsi);
        await using var db = _dbf.Create();
        if (d.Id == 0) db.TankDips.Add(d);
        else { db.TankDips.Attach(d); db.Entry(d).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    // ── تخلیهٔ تانکر ───────────────────────────────────────────────────────
    public async Task<List<TankerUnload>> UnloadsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.TankerUnloads.AsNoTracking().Where(u => u.Fuel == fuel)
                       .OrderByDescending(u => u.DateKey).ToListAsync(ct);
    }

    public async Task SaveUnloadAsync(TankerUnload u, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        u.DateKey = Shamsi.Key(u.DateShamsi);
        u.MonthKey = Shamsi.MonthKey(u.DateShamsi);
        await using var db = _dbf.Create();
        if (u.Id == 0) db.TankerUnloads.Add(u);
        else { db.TankerUnloads.Attach(u); db.Entry(u).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    // ── مخزن‌های شماره‌دار (۱۴۰۵/۰۷/۲۲) ─────────────────────────────────────
    //  ⛔ هیچ‌کدام به موجودیِ کلِ تیل دست نمی‌زند: آن همان ‎StorageService.Tank‎ است. این‌ها فقط
    //  همان موجودی را میانِ مخزن‌ها تقسیم می‌کنند (‎TankSplitService‎).

    public async Task<List<FuelTank>> TanksAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.FuelTanks.AsNoTracking().Where(t => t.Fuel == fuel)
                       .OrderBy(t => t.Num).ThenBy(t => t.Id).ToListAsync(ct);
    }

    /// <summary>مخزنِ تازه یا ویرایش. ⛔ شمارهٔ تکراری در همان تیل پذیرفته نمی‌شود.</summary>
    public async Task<string?> SaveTankAsync(FuelTank t, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        if (t.Num <= 0) return "شمارهٔ مخزن باید بزرگ‌تر از صفر باشد";
        if (t.Capacity < 0m) return "ظرفیت منفی نمی‌شود";
        await using var db = _dbf.Create();
        if (await db.FuelTanks.AnyAsync(x => x.Fuel == t.Fuel && x.Num == t.Num && x.Id != t.Id, ct))
            return "مخزنِ شمارهٔ " + t.Num + " از قبل هست";
        if (t.Id == 0) db.FuelTanks.Add(t);
        else { db.FuelTanks.Attach(t); db.Entry(t).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>حذفِ مخزن — سهم‌های خرید در آن به «تقسیم‌نشده» برمی‌گردند (یعنی مخزنِ اول).</summary>
    public async Task DeleteTankAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var t = await db.FuelTanks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        var now = AppClock.UtcNow;
        foreach (var f in await db.TankFills.Where(f => f.TankId == id).ToListAsync(ct)) f.DeletedAt = now;
        t.DeletedAt = now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>سهم‌های همهٔ خریدهای یک تیل — ‎PurchaseId ⇒ (TankId ⇒ لیتر)‎.</summary>
    public async Task<Dictionary<long, Dictionary<long, decimal>>> FillsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var rows = await db.TankFills.AsNoTracking()
            .Where(f => f.Purchase != null && f.Purchase.Fuel == fuel)
            .Select(f => new { f.PurchaseId, f.TankId, f.Liters }).ToListAsync(ct);
        return rows.GroupBy(r => r.PurchaseId)
                   .ToDictionary(g => g.Key, g => g.GroupBy(x => x.TankId).ToDictionary(x => x.Key, x => x.Sum(y => y.Liters)));
    }

    /// <summary>
    /// سهم‌های یک خرید را جایگزین می‌کند. ⛔ جمع از لیترِ خرید بیشتر نمی‌شود و منفی نمی‌پذیرد؛
    /// هر چه کمتر بماند «تقسیم‌نشده» است و به مخزنِ اول می‌رود.
    /// </summary>
    public async Task<string?> SetFillsAsync(long purchaseId, IReadOnlyDictionary<long, decimal> byTank, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        if (byTank.Values.Any(v => v < 0m)) return "لیترِ مخزن منفی نمی‌شود";
        await using var db = _dbf.Create();
        var p = await db.FuelPurchases.FirstOrDefaultAsync(x => x.Id == purchaseId, ct);
        if (p is null) return "این خرید دیگر نیست";
        if (byTank.Values.Sum() > p.Liters + 0.005m)
            return "جمعِ لیترِ مخزن‌ها (" + Shamsi.Money(byTank.Values.Sum()) + ") از لیترِ خرید (" + Shamsi.Money(Math.Round(p.Liters, 2)) + ") بیشتر است";
        var valid = await db.FuelTanks.Where(t => t.Fuel == p.Fuel).Select(t => t.Id).ToListAsync(ct);
        var now = AppClock.UtcNow;
        foreach (var f in await db.TankFills.Where(f => f.PurchaseId == purchaseId).ToListAsync(ct)) f.DeletedAt = now;
        foreach (var (tankId, liters) in byTank)
            if (liters > 0m && valid.Contains(tankId))
                db.TankFills.Add(new TankFill { PurchaseId = purchaseId, TankId = tankId, Liters = liters });
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>
    /// حالِ هر مخزنِ یک تیل: خریدها (با سهم‌هایشان) آمدن، فروشِ هر روزِ پارچه‌ها و اصلاحِ
    /// میله‌زنی رفتن — به ترتیبِ تاریخ (‎TankSplitService‎). بی مخزنِ تعریف‌شده ⇒ فهرستِ خالی.
    /// </summary>
    public async Task<List<TankLevel>> TankLevelsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var tanks = await db.FuelTanks.AsNoTracking().Where(t => t.Fuel == fuel)
                            .Select(t => new TankDef(t.Id, t.Num, t.Capacity)).ToListAsync(ct);
        if (tanks.Count == 0) return new List<TankLevel>();

        var purchases = await db.FuelPurchases.AsNoTracking().Where(p => p.Fuel == fuel)
                                .Select(p => new { p.Id, p.DateKey, p.Liters }).ToListAsync(ct);
        var fills = await db.TankFills.AsNoTracking()
            .Where(f => f.Purchase != null && f.Purchase.Fuel == fuel)
            .Select(f => new { f.PurchaseId, f.TankId, f.Liters }).ToListAsync(ct);
        var byPurchase = fills.GroupBy(f => f.PurchaseId).ToDictionary(g => g.Key, g => g.ToList());

        var ins = new List<TankIn>();
        foreach (var p in purchases)
        {
            decimal given = 0m;
            if (byPurchase.TryGetValue(p.Id, out var fs))
                foreach (var f in fs) { ins.Add(new TankIn(p.DateKey, f.TankId, f.Liters)); given += f.Liters; }
            if (p.Liters - given != 0m) ins.Add(new TankIn(p.DateKey, null, p.Liters - given));
        }

        var sales = await db.Reports.AsNoTracking().Where(r => r.Fuel == fuel)
            .Select(r => new { r.DateKey, Day = (decimal?)r.DayShift!.Sale, Night = (decimal?)r.NightShift!.Sale })
            .ToListAsync(ct);
        var outs = sales.Select(r => new TankOut(r.DateKey, (r.Day ?? 0m) + (r.Night ?? 0m))).ToList();
        var dips = await db.TankDips.AsNoTracking().Where(d => d.Fuel == fuel && d.BookAdjust != 0m)
                           .Select(d => new { d.DateKey, d.BookAdjust }).ToListAsync(ct);
        outs.AddRange(dips.Select(d => new TankOut(d.DateKey, -d.BookAdjust)));

        return TankSplitService.Split(tanks, ins, outs);
    }
}
