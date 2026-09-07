using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ فاکتورها ══════════════════════════════════════════════════════════════
/// یک فاکتور می‌تواند هم بخشِ پولی داشته باشد و هم بخشِ تیل، و هر دو باید
/// در حساب دیده شوند (باگِ ۱۲ِ نسخهٔ وب).
///
/// ⚠️ بخشِ پولیِ فاکتور همیشه در دفترِ «واحد پول» می‌نشیند، هرگز در دفترِ تیل.
/// یک‌بار به «دفترِ فعال» می‌رفت و اگر حساب روی واحدِ تیل بود، «مقدار رسیدِ
/// تیل» بی‌جهت عدد می‌گرفت — همان باگی که گزارش شد.
///
/// ⚠️ آنچه با تایید به حساب اضافه می‌شود روی خودِ فاکتور یادداشت می‌شود
/// (<see cref="Invoice.PostedFuelLiters"/>) تا با برگشت، دقیقاً همان مقدار
/// پس گرفته شود — نه یک عددِ حساب‌شدهٔ دوباره.
/// </summary>
public sealed class InvoiceService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly DebtorService _debtors;

    public InvoiceService(PumpDbFactory dbf, PermissionService perm, TrashService trash, DebtorService debtors)
    { _dbf = dbf; _perm = perm; _trash = trash; _debtors = debtors; }

    public async Task<List<Invoice>> ListAsync(InvoiceStatus? status = null, string? search = null,
                                               CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.Invoices.AsNoTracking().AsQueryable();
        if (status is not null) q = q.Where(v => v.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(v => (v.CustomerName != null && v.CustomerName.Contains(s))
                          || (v.Phone != null && v.Phone.Contains(s)));
        }
        return await q.OrderByDescending(v => v.InvoiceNumber).ToListAsync(ct);
    }

    /// <summary>شمارهٔ یکتای بعدی — هیچ‌وقت تکراری نمی‌شود.</summary>
    public async Task<int> NextNumberAsync(CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        return await db.Invoices.AnyAsync(ct) ? await db.Invoices.MaxAsync(v => v.InvoiceNumber, ct) + 1 : 1;
    }

    /// <summary>‎by_money‎ — فاکتوری که فقط مبلغ دارد، نه فی و لیتر.</summary>
    public static bool IsMoneyOnly(Invoice v) =>
        v.Amount > 0m && !(v.PricePerLiter > 0m && v.Liters > 0m);

    /// <summary>
    /// ‎invSubmit‎ — ثبتِ فاکتورِ تازه، همیشه در حالتِ «در صفِ تایید».
    ///
    /// ⚠️ شمارهٔ تکراری رد می‌شود، نه اینکه بی‌صدا قبول شود: شمارهٔ فاکتور
    /// سندِ کاغذیِ مشتری است و دو فاکتور با یک شماره یعنی حسابِ قابلِ دفاع
    /// نداریم.
    /// </summary>
    public async Task<Invoice> AddAsync(Invoice v, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        v.InvoiceNumber = v.InvoiceNumber > 0 ? v.InvoiceNumber : await NextNumberAsync(ct);
        v.DateShamsi ??= Shamsi.Today();
        v.DateKey = Shamsi.Key(v.DateShamsi);
        v.ByMoney = IsMoneyOnly(v);
        v.RateOnCreate ??= v.PricePerLiter;
        v.LegacyId ??= "inv" + Guid.NewGuid().ToString("N")[..10];

        await using var db = _dbf.Create();
        if (await db.Invoices.AnyAsync(x => x.InvoiceNumber == v.InvoiceNumber, ct))
            throw new InvalidOperationException(
                "فاکتور شماره " + v.InvoiceNumber + " قبلاً ثبت شده — شماره دیگری انتخاب کنید");
        db.Invoices.Add(v);
        await db.SaveChangesAsync(ct);
        return v;
    }

    /// <summary>
    /// ‎invSaveEdits‎ — ویرایشِ فاکتور.
    ///
    /// ⚠️ اگر فاکتور از پیش تایید شده باشد، اثرش روی حسابِ قرض‌دار هم باید
    /// هم‌گام شود: اول برداشته می‌شود، بعد با عددهای تازه دوباره می‌نشیند.
    /// بی این، عوض کردنِ لیترِ یک فاکتورِ تاییدشده عددِ کهنه را در حساب جا
    /// می‌گذاشت و هیچ‌کس نمی‌فهمید از کجا آمده.
    /// </summary>
    public async Task UpdateAsync(Invoice v, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);

        await using var db = _dbf.Create();
        var row = await db.Invoices.FirstOrDefaultAsync(x => x.Id == v.Id, ct);
        if (row is null) return;

        // ⚠️ فقط همان خانه‌هایی که فرمِ ویرایش دارد نوشته می‌شوند.
        // یک‌بار کلِ شیء با ‎State = Modified‎ روی ردیف نشانده می‌شد؛ چون شیءِ
        // دستِ صفحه نسخهٔ **پیش از تایید** بود، همان نوشتن دفترچهٔ ثبت را هم
        // پاک می‌کرد (‎PostedFuelLiters‎ و ‎DebtAccountId‎ صفر می‌شدند) و
        // بعدش دیگر چیزی برای پس گرفتن نبود — لیتر برای همیشه در حساب می‌ماند.
        row.CustomerName = v.CustomerName;
        row.DebtAlias = v.DebtAlias;
        row.Fuel = v.Fuel;
        row.PricePerLiter = Math.Max(0m, v.PricePerLiter);
        row.Liters = Math.Max(0m, v.Liters);
        row.Amount = Math.Max(0m, v.Amount);
        row.VehicleType = v.VehicleType;
        row.Phone = v.Phone;
        row.Note = v.Note;
        row.DateShamsi = v.DateShamsi;
        row.DateKey = Shamsi.Key(v.DateShamsi);
        row.ByMoney = IsMoneyOnly(row);

        if (row.Status == InvoiceStatus.Approved)
        {
            await UnpostAsync(db, row, ct);      // با حسابِ **قبلی** پس گرفته می‌شود
            // ‎invSaveEdits‎ صریحاً ‎v.debt_target_id = null‎ می‌گذارد: با هر
            // ویرایش، حسابِ مقصد دوباره از روی نام پیدا می‌شود. پس اگر نامِ
            // مشتری عوض شده باشد، فاکتور به حسابِ درست می‌رود نه حسابِ کهنه.
            row.DebtAccountId = null;
            var account = await EnsureAccountAsync(db, row, ct);
            await PostAsync(db, row, account, ct);
        }

        await db.SaveChangesAsync(ct);

        // شیءِ دستِ صفحه هم تازه شود تا نمای بعدی درست باشد
        v.ByMoney = row.ByMoney;
        v.DateKey = row.DateKey;
        v.Status = row.Status;
        v.DebtAccountId = row.DebtAccountId;
        v.PostedFuelLiters = row.PostedFuelLiters;
        v.PostedFuelType = row.PostedFuelType;
    }

    /// <summary>
    /// تاییدِ فاکتور: بخشِ پولی به دفترِ پولِ حساب و بخشِ تیل به «رسیدِ تیل»
    /// همان حساب می‌رود. حسابِ قرض‌دار اگر نباشد، از روی نامِ فاکتور ساخته می‌شود.
    /// </summary>
    public async Task ApproveAsync(long invoiceId, decimal todayRate, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        await using var db = _dbf.Create();
        var v = await db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
        if (v is null || v.Status == InvoiceStatus.Approved) return;

        var account = await EnsureAccountAsync(db, v, ct);

        v.Status = InvoiceStatus.Approved;
        v.ApprovedAtUtc = DateTime.UtcNow;
        v.RateOnCreate ??= v.PricePerLiter;
        v.RateOnApprove = todayRate;

        await PostAsync(db, v, account, ct);

        db.Audit.Add(new AuditEntry { Action = "invoice-approve", Target = v.InvoiceNumber.ToString() });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// ══ ‎_invPostToDebt(v)‎ ══════════════════════════════════════════════════
    /// نشاندنِ اثرِ یک فاکتورِ تاییدشده روی حسابِ قرض‌دار. یک فاکتور می‌تواند
    /// **هر دو** بخش را داشته باشد و هر دو باید شمرده شوند:
    ///
    ///   • بخشِ پولی (مبلغ) → یک ردیفِ «رسید» در دفترِ **واحد پول**.
    ///     ⚠️ همیشه دفترِ پول، نه دفترِ فعال. یک‌بار به دفترِ فعال می‌رفت و
    ///     اگر حساب روی واحدِ تیل بود، رسیدِ پولی واردِ جدولِ تیل می‌شد و
    ///     «مقدار رسید تیل» بی‌جهت عدد می‌گرفت.
    ///
    ///   • بخشِ تیل (لیتر) → «مقدار رسیدِ تیل»ِ حساب، **به‌علاوهٔ** یک ردیفِ
    ///     نمایشی در جدول تا کاربر ببیند چند لیتر و از کدام فاکتور رسیده.
    ///
    /// ⚠️ صفر شدنِ هر بخش یعنی ردیفش باید **برداشته** شود، نه اینکه ردیفِ صفر
    /// بماند — وگرنه با ویرایشِ فاکتور، ردیفِ کهنه در حساب جا می‌مانَد.
    /// </summary>
    private static async Task PostAsync(Persistence.PumpDbContext db, Invoice v,
                                        DebtAccount account, CancellationToken ct)
    {
        v.DebtAccountId = account.Id;

        var label = (string.IsNullOrWhiteSpace(v.CustomerName) ? "" : v.CustomerName + " - ")
                  + "فاکتور شماره " + v.InvoiceNumber
                  + (string.IsNullOrWhiteSpace(v.VehicleType) ? "" : " — " + v.VehicleType);
        var date = v.DateShamsi ?? Shamsi.Today();

        // ── بخشِ پولی ──
        var moneyRow = await db.DebtRows.FirstOrDefaultAsync(r => r.InvoiceId == v.Id, ct);
        if (v.Amount > 0m)
        {
            if (moneyRow is null)
            {
                moneyRow = new DebtRow { InvoiceId = v.Id };
                db.DebtRows.Add(moneyRow);
            }
            moneyRow.MoneyAccountId = account.Id;
            moneyRow.FuelAccountId = null;          // اگر از نسخه‌های قبل در دفترِ تیل مانده بود
            moneyRow.DateShamsi = date;
            moneyRow.DateKey = Shamsi.Key(date);
            moneyRow.Name = label;
            moneyRow.Fuel = v.Fuel;
            moneyRow.ByMoney = true;
            moneyRow.Liters = 0m;
            moneyRow.PricePerLiter = null;
            moneyRow.Bardagi = 0m;
            moneyRow.Rasid = v.Amount;
            moneyRow.Albaqi = -v.Amount;
        }
        else if (moneyRow is not null)
        {
            db.DebtRows.Remove(moneyRow);           // مبلغ حذف شد → ردیفش هم برود
        }

        // ── بخشِ تیل ──
        var fuelRow = await db.DebtRows.FirstOrDefaultAsync(r => r.InvoiceFuelId == v.Id, ct);
        if (v.Liters > 0m)
        {
            if (v.Fuel == FuelType.Diesel) account.RasidFuelDiesel += v.Liters;
            else account.RasidFuelPetrol += v.Liters;
            v.PostedFuelLiters = v.Liters;
            v.PostedFuelType = v.Fuel;      // برای پس گرفتنِ درست، حتی اگر بعداً عوض شود

            if (fuelRow is null)
            {
                fuelRow = new DebtRow { InvoiceFuelId = v.Id };
                db.DebtRows.Add(fuelRow);
            }
            fuelRow.FuelAccountId = account.Id;
            fuelRow.MoneyAccountId = null;
            fuelRow.DateShamsi = date;
            fuelRow.DateKey = Shamsi.Key(date);
            fuelRow.Name = label;
            fuelRow.Hawala = "فاکتور " + v.InvoiceNumber;
            fuelRow.Fuel = v.Fuel;
            fuelRow.ByMoney = false;
            // فقط ستونِ «رسید تیل» — بردگی و فی و رسیدِ پول صفر می‌مانند تا
            // هیچ محاسبه‌ای عوض نشود
            fuelRow.Liters = 0m;
            fuelRow.PricePerLiter = null;
            fuelRow.Bardagi = 0m;
            fuelRow.Rasid = 0m;
            fuelRow.RasidFuel = v.Liters;
            fuelRow.Albaqi = 0m;
        }
        else if (fuelRow is not null)
        {
            db.DebtRows.Remove(fuelRow);            // لیتر حذف شد → ردیفِ نمایشی هم برود
        }
    }

    /// <summary>برگرداندنِ تایید — دقیقاً همان مقداری که اضافه شده بود پس گرفته می‌شود.</summary>
    public async Task RevertAsync(long invoiceId, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        await using var db = _dbf.Create();
        var v = await db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
        if (v is null) return;

        await UnpostAsync(db, v, ct);
        v.Status = InvoiceStatus.Pending;
        v.ApprovedAtUtc = null;
        db.Audit.Add(new AuditEntry { Action = "invoice-revert", Target = v.InvoiceNumber.ToString() });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long invoiceId, CancellationToken ct = default)
    {
        _perm.Require(Permission.ManagerOnly);
        await using var db = _dbf.Create();
        var v = await db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
        if (v is null) return;
        await UnpostAsync(db, v, ct);
        await _trash.RememberAsync(db, "invoice", "فاکتور شماره " + v.InvoiceNumber, v, ct);
        db.Invoices.Remove(v);
        await db.SaveChangesAsync(ct);
    }

    private static async Task UnpostAsync(Persistence.PumpDbContext db, Invoice v, CancellationToken ct)
    {
        // هر دو ردیف: پولی (InvoiceId) و نمایشیِ تیل (InvoiceFuelId)
        var rows = await db.DebtRows
            .Where(r => r.InvoiceId == v.Id || r.InvoiceFuelId == v.Id).ToListAsync(ct);
        if (rows.Count > 0) db.DebtRows.RemoveRange(rows);

        if (v.PostedFuelLiters is > 0m && v.DebtAccountId is not null)
        {
            var acc = await db.DebtAccounts.FirstOrDefaultAsync(a => a.Id == v.DebtAccountId, ct);
            if (acc is not null)
            {
                // ⚠️ نوعِ تیلِ **ثبت‌شده**، نه نوعِ تیلِ همین لحظه
                var posted = v.PostedFuelType ?? v.Fuel;
                if (posted == FuelType.Diesel)
                    acc.RasidFuelDiesel = Math.Max(0m, acc.RasidFuelDiesel - v.PostedFuelLiters.Value);
                else
                    acc.RasidFuelPetrol = Math.Max(0m, acc.RasidFuelPetrol - v.PostedFuelLiters.Value);
            }
        }
        v.PostedFuelLiters = null;
        v.PostedFuelType = null;
    }

    /// <summary>
    /// ══ ‎_ensureDebtPersonForInvoice(v)‎ ═══════════════════════════════════
    /// حسابِ مقصد **از روی نام** تعیین می‌شود: «نامِ حساب»ِ فاکتور اگر نوشته
    /// شده باشد، وگرنه نامِ مشتری. حسابِ هم‌نام اگر نبود، ساخته می‌شود.
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «همان نام مشتری را می‌زنم، بس است — همان باید
    /// کارش را بکند». کشویِ «ثبت در حساب» به همین دلیل برداشته شده.
    ///
    /// ⚠️ شمارهٔ تماسِ فاکتور هم به حساب منتقل می‌شود، ولی فقط اگر حساب خودش
    /// شماره نداشته باشد — شمارهٔ ثبت‌شدهٔ کاربر با یک فاکتور بازنویسی نمی‌شود.
    /// </summary>
    private static async Task<DebtAccount> EnsureAccountAsync(Persistence.PumpDbContext db, Invoice v,
                                                              CancellationToken ct)
    {
        if (v.DebtAccountId is not null)
        {
            var found = await db.DebtAccounts.FirstOrDefaultAsync(a => a.Id == v.DebtAccountId, ct);
            if (found is not null) return found;
        }

        var name = (string.IsNullOrWhiteSpace(v.DebtAlias) ? v.CustomerName : v.DebtAlias)?.Trim() ?? "";
        var key = CompanyDataService.NormalizeName(name);

        var people = await db.Debtors.Include(d => d.MainAccount).ToListAsync(ct);
        var person = people.FirstOrDefault(d => CompanyDataService.NormalizeName(d.Name) == key);
        if (person?.MainAccount is not null)
        {
            CopyPhone(v, person);
            return person.MainAccount;
        }

        var acc = new DebtAccount { Mode = LedgerMode.Fuel };
        var fresh = new Debtor
        {
            Name = name.Length == 0 ? "مشتریِ فاکتور" : name,
            LegacyId = "d" + Guid.NewGuid().ToString("N")[..10],
            MainAccount = acc,
        };
        CopyPhone(v, fresh);
        db.Debtors.Add(fresh);
        await db.SaveChangesAsync(ct);
        return acc;
    }

    private static void CopyPhone(Invoice v, Debtor p)
    {
        if (!string.IsNullOrWhiteSpace(v.Phone) && string.IsNullOrWhiteSpace(p.Phone))
            p.Phone = v.Phone;
    }
}
