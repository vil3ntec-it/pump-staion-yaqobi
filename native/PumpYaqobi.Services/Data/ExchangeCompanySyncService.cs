using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>نتیجهٔ پیوندِ یک سطرِ صرافی با حسابِ شرکت.</summary>
public enum ExchangeLinkResult
{
    /// <summary>در توضیحات نامی نوشته نشده — کاری لازم نیست.</summary>
    Empty,
    /// <summary>بردگی در حسابِ همان شرکت نشست.</summary>
    Linked,
    /// <summary>نام نوشته شده ولی شرکتی با آن نام نیست.</summary>
    NotFound,
}

/// <summary>
/// ══ صرافی ← حسابِ شرکت ═════════════════════════════════════════════════════
/// رونوشتِ ‎syncSarrafiToCompany(row)‎.
///
/// نفرِ شرکت به صرافی می‌رود و «بردگی» را می‌گیرد؛ همان بردگی به‌صورت **دالری**
/// به‌عنوان رسید در حسابِ همان شرکت می‌نشیند و از باقی‌ماندهٔ دالرِ شرکت کم
/// می‌شود. نامِ شرکت از متنِ «توضیحات» خوانده می‌شود و نوعِ تیل از همان متن.
///
/// چهار قاعده که اگر یکی‌شان نباشد حسابِ شرکت خراب می‌شود:
///
///   ۱. ردیفِ خودکارِ همین سطر از هر شرکتی که دیگر هدف نیست پاک می‌شود —
///      وگرنه با عوض کردنِ نام، بردگی در حسابِ شرکتِ قبلی هم می‌ماند و دو بار
///      شمرده می‌شود.
///   ۲. اگر نوعِ تیل عوض شود، ردیف از دفترِ قبلی برداشته می‌شود.
///   ۳. ردیفِ موجود **سرِ جایش** به‌روز می‌شود، نه اینکه پاک و ته جدول اضافه شود.
///   ۴. بردگیِ صفر یعنی ردیف باید برداشته شود، نه ردیفِ صفر بماند.
///
/// ⚠️ شرکتِ نبوده **ساخته نمی‌شود** — برخلافِ «خریدِ مخزن». نسخهٔ وب صریح
/// نوشته: «بخش‌های صرافی/گاوصندوق/ورق دیگر حساب نمی‌سازند».
/// </summary>
public sealed class ExchangeCompanySyncService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;

    public ExchangeCompanySyncService(PumpDbFactory dbf, PermissionService perm)
    { _dbf = dbf; _perm = perm; }

    /// <summary>
    /// ‎_detectCompanyAndFuel(text)‎ — شرکت از کلِ متن، ولی نوعِ تیل فقط از
    /// **باقی‌ماندهٔ** متن پس از برداشتنِ نامِ شرکت.
    ///
    /// ⚠️ اگر نوعِ تیل را از کلِ متن بخوانیم، شرکتی که «دیزل» در نامش هست
    /// (مثلاً «شرکت دیزل هرات») همیشه دیزل تشخیص داده می‌شود، حتی وقتی
    /// کاربر پطرول نوشته باشد.
    /// </summary>
    public static (TilCompany? Company, FuelType Fuel) Detect(
        IEnumerable<TilCompany> companies, string? text)
    {
        var t = (text ?? "").Trim();
        var matched = CompanyDataService.FindByName(companies, t);

        var rest = t;
        if (matched is not null)
        {
            var nm = (matched.Name ?? "").Trim();
            if (nm.Length > 0)
                rest = string.Join(" ", PostingService.NormFa(t)
                    .Split(PostingService.NormFa(nm)));
        }
        return (matched, PostingService.DetectFuelType(rest));
    }

    /// <summary>‎syncSarrafiToCompany(row)‎ — یک سطرِ صرافی.</summary>
    public async Task<ExchangeLinkResult> SyncAsync(ExchangeRow row, CancellationToken ct = default)
    {
        if (row is null) return ExchangeLinkResult.Empty;
        _perm.Require(Permission.EditData);

        if (string.IsNullOrEmpty(row.LegacyId))
            row.LegacyId = "sf" + Guid.NewGuid().ToString("N")[..12];

        var usd = row.Bardagi;
        var desc = (row.Description ?? "").Trim();

        await using var db = _dbf.Create();
        var companies = await db.TilCompanies.Include(c => c.Rows).ToListAsync(ct);
        var (matched, fuel) = Detect(companies, desc);

        // ۱) ردیفِ خودکارِ این سطر از هر شرکتِ دیگری پاک شود
        foreach (var c in companies)
        {
            if (matched is not null && c.Id == matched.Id) continue;
            foreach (var dead in c.Rows.Where(r => r.SourceExchangeId == row.LegacyId).ToList())
            { c.Rows.Remove(dead); db.CompanyRows.Remove(dead); }
        }

        var status = ExchangeLinkResult.Empty;

        if (matched is not null)
        {
            var mine = matched.Rows.Where(r => r.SourceExchangeId == row.LegacyId).ToList();

            // ۲) نوعِ تیل عوض شده؟ ردیفِ دفترِ دیگر برداشته شود
            foreach (var dead in mine.Where(r => r.Fuel != fuel).ToList())
            { matched.Rows.Remove(dead); db.CompanyRows.Remove(dead); mine.Remove(dead); }

            var existing = mine.FirstOrDefault(r => r.Fuel == fuel);

            if (usd != 0m)
            {
                var name = desc.Length > 0 ? "💱 " + desc + " از صرافی" : "بردگی از صرافی";
                if (existing is not null)
                {
                    // ۳) سرِ جایش به‌روز می‌شود — SortIndex دست نمی‌خورد
                    existing.DateShamsi = row.DateShamsi ?? "";
                    existing.DateKey = Shamsi.Key(row.DateShamsi);
                    existing.Name = name;
                    existing.Ton = 0m; existing.Kg = 0m; existing.Usd = 0m; existing.Rate = 0m;
                    existing.Poul = usd;
                    existing.PoulCurrency = Currency.Usd;
                    existing.Note = desc;
                }
                else
                {
                    var fresh = new CompanyRow
                    {
                        CompanyId = matched.Id,
                        Fuel = fuel,
                        DateShamsi = row.DateShamsi ?? "",
                        DateKey = Shamsi.Key(row.DateShamsi),
                        Name = name,
                        Poul = usd,
                        PoulCurrency = Currency.Usd,
                        Note = desc,
                        SourceExchangeId = row.LegacyId,
                    };
                    PutInFirstEmpty(matched, fuel, fresh, db);
                }
                status = ExchangeLinkResult.Linked;
            }
            else if (existing is not null)
            {
                // ۴) بردگی صفر شد → ردیفِ خودکار برداشته شود
                matched.Rows.Remove(existing);
                db.CompanyRows.Remove(existing);
            }
        }
        else if (desc.Length > 0 && usd != 0m)
        {
            // شرکتِ نبوده ساخته نمی‌شود — فقط خبر داده می‌شود
            status = ExchangeLinkResult.NotFound;
        }

        await db.SaveChangesAsync(ct);
        return status;
    }

    /// <summary>
    /// حذفِ یک سطرِ صرافی — ردیفِ خودکارش در حسابِ شرکت هم می‌رود.
    /// وگرنه بردگی در حسابِ شرکت می‌ماند بی آنکه سطری پشتش باشد.
    /// </summary>
    public async Task UnlinkAsync(string? legacyId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(legacyId)) return;
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var rows = await db.CompanyRows.Where(r => r.SourceExchangeId == legacyId).ToListAsync(ct);
        if (rows.Count == 0) return;
        db.CompanyRows.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>‎_putReceiptInCompany‎ — نخستین ردیفِ خالی، وگرنه ردیفِ تازه.</summary>
    private static void PutInFirstEmpty(TilCompany company, FuelType fuel, CompanyRow fresh,
                                        Persistence.PumpDbContext db)
    {
        var rows = company.Rows.Where(r => r.Fuel == fuel)
                          .OrderBy(r => r.SortIndex).ThenBy(r => r.Id).ToList();
        var empty = rows.FirstOrDefault(r => r.IsEmpty);
        if (empty is not null)
        {
            empty.DateShamsi = fresh.DateShamsi; empty.DateKey = fresh.DateKey;
            empty.Name = fresh.Name; empty.Poul = fresh.Poul;
            empty.PoulCurrency = fresh.PoulCurrency; empty.Note = fresh.Note;
            empty.SourceExchangeId = fresh.SourceExchangeId;
            return;
        }
        fresh.SortIndex = rows.Count;
        company.Rows.Add(fresh);
        db.CompanyRows.Add(fresh);
    }
}
