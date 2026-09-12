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

    public async Task<Invoice> AddAsync(Invoice v, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        v.InvoiceNumber = v.InvoiceNumber > 0 ? v.InvoiceNumber : await NextNumberAsync(ct);
        v.DateShamsi ??= Shamsi.Today();
        v.DateKey = Shamsi.Key(v.DateShamsi);
        v.ByMoney = IsMoneyOnly(v);
        v.RateOnCreate ??= v.PricePerLiter;
        v.LegacyId ??= "inv" + Guid.NewGuid().ToString("N")[..10];

        await using var db = _dbf.Create();
        db.Invoices.Add(v);
        await db.SaveChangesAsync(ct);
        return v;
    }

    public async Task UpdateAsync(Invoice v, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        v.DateKey = Shamsi.Key(v.DateShamsi);
        v.ByMoney = IsMoneyOnly(v);
        await using var db = _dbf.Create();
        db.Invoices.Attach(v);
        db.Entry(v).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// تاییدِ فاکتور: بخشِ پولی به دفترِ پولِ حساب و بخشِ تیل به «رسیدِ تیل»
    /// همان حساب می‌رود. حسابِ قرض‌دار اگر نباشد، از روی نامِ فاکتور ساخته می‌شود.
    /// </summary>
    public async Task ApproveAsync(long invoiceId, decimal todayRate, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var v = await db.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
        if (v is null || v.Status == InvoiceStatus.Approved) return;

        var account = await EnsureAccountAsync(db, v, ct);

        v.Status = InvoiceStatus.Approved;
        v.ApprovedAtUtc = DateTime.UtcNow;
        v.RateOnCreate ??= v.PricePerLiter;
        v.RateOnApprove = todayRate;
        v.DebtAccountId = account.Id;

        // ── بخشِ پولی: همیشه در دفترِ «واحد پول» ──
        if (v.Amount > 0m)
        {
            var row = await db.DebtRows.FirstOrDefaultAsync(r => r.InvoiceId == v.Id, ct);
            if (row is null)
            {
                row = new DebtRow { MoneyAccountId = account.Id, InvoiceId = v.Id };
                db.DebtRows.Add(row);
            }
            row.MoneyAccountId = account.Id;
            row.FuelAccountId = null;               // اگر از نسخه‌های قبل در دفترِ تیل مانده بود
            row.DateShamsi = v.DateShamsi;
            row.DateKey = Shamsi.Key(v.DateShamsi);
            row.Name = $"{v.CustomerName} - فاکتور شماره {v.InvoiceNumber}"
                       + (string.IsNullOrWhiteSpace(v.VehicleType) ? "" : " — " + v.VehicleType);
            row.Fuel = v.Fuel;
            row.ByMoney = true;
            row.Liters = 0m;
            row.PricePerLiter = null;
            row.Bardagi = 0m;
            row.Rasid = v.Amount;
            row.Albaqi = -v.Amount;
        }

        // ── بخشِ تیل: «مقدار رسیدِ تیل»ِ همان حساب ──
        //
        // ⚠️ دیگر مستقیم به عددِ حساب اضافه نمی‌شود: رسید رکوردِ خودش را در
        // دفترِ رسید می‌گیرد و عددِ سربرگ جمعِ همان دفتر می‌شود. این‌طور
        // (خواستهٔ صریحِ صاحب ریپو) رسیدِ فاکتور هم ردیفِ خودش را در جدولِ
        // شخص دارد و برگرداندنِ تایید دقیقاً همان یکی را برمی‌دارد.
        if (!v.ByMoney && v.Liters > 0m)
        {
            // رسیدِ فاکتور هم یک ردیفِ واقعی است، مثلِ هر رسیدِ دیگری — نه یک
            // عددِ جدا روی خودِ حساب. پس در جدولِ شخص دیده می‌شود، در جمله
            // شمرده می‌شود و برگرداندنِ تایید همان ردیف را برمی‌دارد.
            var row = new DebtRow
            {
                FuelAccountId = account.Id,
                InvoiceId = v.Id,
                Fuel = v.Fuel,
                RasidFuel = v.Liters,
                DateShamsi = v.DateShamsi,
                DateKey = Shamsi.Key(v.DateShamsi),
                Name = $"رسیدِ فاکتور شماره {v.InvoiceNumber}",
                Albaqi = -v.Liters,
                SortIndex = account.FuelRows.Count,
            };
            db.DebtRows.Add(row);
            v.PostedFuelLiters = v.Liters;
        }

        db.Audit.Add(new AuditEntry { Action = "invoice-approve", Target = v.InvoiceNumber.ToString() });
        // کشِ رسیدِ حساب از روی ردیف‌ها تازه شود — تنها راهِ درستِ نوشتنِ آن چهار عدد
        await ReceiptSync.FromRowsAsync(db, account, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>برگرداندنِ تایید — دقیقاً همان مقداری که اضافه شده بود پس گرفته می‌شود.</summary>
    public async Task RevertAsync(long invoiceId, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
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
        _perm.Require(Permission.DeleteData);
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
        // ⚠️ **همهٔ** ردیف‌های این فاکتور، نه اولی: یک فاکتور می‌تواند هم بخشِ
        // پولی داشته باشد و هم بخشِ تیل، و از امروز هر دو ردیفِ خودشان را
        // دارند. با ‎FirstOrDefault‎ یکی‌شان جا می‌ماند و رسیدش هرگز پس گرفته
        // نمی‌شد.
        var rows = await db.DebtRows.Where(r => r.InvoiceId == v.Id).ToListAsync(ct);
        if (rows.Count > 0) db.DebtRows.RemoveRange(rows);
        var row = rows.FirstOrDefault();

        if (v.PostedFuelLiters is > 0m && v.DebtAccountId is not null)
        {
            var acc = await db.DebtAccounts.FirstOrDefaultAsync(a => a.Id == v.DebtAccountId, ct);
            if (acc is not null)
            {
                // ردیفِ رسیدِ همین فاکتور برداشته می‌شود (‎row‎ی بالا با همان
                // ‎InvoiceId‎ ساخته شده و درست بالاتر پاک شد اگر بود).
                //
                // فاکتورهای تاییدشدهٔ **پیش از** این تغییر ردیفی ندارند و
                // عددشان مستقیم روی حساب نشسته بود؛ آن‌ها همان راهِ قدیمی را
                // می‌گیرند تا عددشان درست پس گرفته شود.
                if (row is null)
                {
                    if (v.Fuel == FuelType.Diesel)
                        acc.RasidFuelDiesel = Math.Max(0m, acc.RasidFuelDiesel - v.PostedFuelLiters.Value);
                    else
                        acc.RasidFuelPetrol = Math.Max(0m, acc.RasidFuelPetrol - v.PostedFuelLiters.Value);
                }
                else await ReceiptSync.FromRowsAsync(db, acc, ct);
            }
        }
        v.PostedFuelLiters = null;
    }

    /// <summary>حسابِ مقصد — از روی «نامِ حساب»ِ فاکتور، وگرنه نامِ مشتری.</summary>
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
        if (person?.MainAccount is not null) return person.MainAccount;

        var acc = new DebtAccount { Mode = LedgerMode.Fuel };
        var fresh = new Debtor
        {
            Name = name.Length == 0 ? "مشتریِ فاکتور" : name,
            LegacyId = "d" + Guid.NewGuid().ToString("N")[..10],
            MainAccount = acc,
        };
        db.Debtors.Add(fresh);
        await db.SaveChangesAsync(ct);
        return acc;
    }
}
