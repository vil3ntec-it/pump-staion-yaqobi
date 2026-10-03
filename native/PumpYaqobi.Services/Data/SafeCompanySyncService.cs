using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ گاوصندوق ← حسابِ شرکت (۱۴۰۵/۰۷/۱۹) ══════════════════════════════════════
/// رونوشتِ ‎_syncSafeEntryToCompany(e)‎ِ نسخهٔ وب.
///
/// گزارشِ صاحب ریپو: «اگر از گاوصندوق بردگی‌ای به نامِ یک شرکت ثبت شود، باید
/// به‌عنوان رسید با همان واحد و ارز (دالر یا افغانی) در حسابِ همان شرکت ثبت
/// شود — نه مثلِ صرافی رفتار کند، نه گم شود.» ستونِ ‎CompanyRow.SourceReceiptId‎
/// از روزِ اول برای همین ساخته شده بود ولی هیچ‌کس پرش نمی‌کرد.
///
///   • فقط «بردگی» — ماندگی پولی است که در گاوصندوق مانده، نه پرداخت به کسی.
///   • نامِ شرکت از «نام»ِ ردیف، نوعِ تیل از باقیِ همان متن (همان ‎Detect‎ِ صرافی).
///   • ⛔ <b>ارز همان ارزِ ردیفِ گاوصندوق</b> است: افغانی ⇒ پولِ افغانی، دالر ⇒
///     پولِ دالری. صرافی همیشه دالر می‌نشاند؛ این‌جا هرگز تبدیلی نیست.
///   • ⛔ شرکتِ نبوده ساخته نمی‌شود (همان قاعدهٔ صرافی و نسخهٔ وب).
///   • ردیف از هر شرکت/دفترِ تیلِ دیگر برداشته و سرِ جایش به‌روز می‌شود
///     (‎CompanyDataService.PutReceiptAsync‎ — همان یک قاعده، دو بار نوشته نشد).
///   • کلید ‎"safe|" + SyncUid‎ است، نه ‎Id‎ِ محلی: روی دو کامپیوترِ یک پمپ،
///     ویرایشِ همان ردیف همان رسید را به‌روز کند، نه رسیدِ دوم بسازد.
/// </summary>
public sealed class SafeCompanySyncService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly CompanyDataService _companies;

    public SafeCompanySyncService(PumpDbFactory dbf, PermissionService perm, CompanyDataService companies)
    { _dbf = dbf; _perm = perm; _companies = companies; }

    public static string KeyOf(SafeEntry e) => "safe|" + (string.IsNullOrEmpty(e.SyncUid) ? "id" + e.Id : e.SyncUid);

    public async Task<ExchangeLinkResult> SyncAsync(SafeEntry e, CancellationToken ct = default)
    {
        if (e is null) return ExchangeLinkResult.Empty;
        _perm.Require(Permission.EditData);

        if (string.IsNullOrEmpty(e.SyncUid) && e.Id > 0)
        {
            await using var db0 = _dbf.Create();
            e.SyncUid = await db0.SafeEntries.Where(x => x.Id == e.Id).Select(x => x.SyncUid).FirstOrDefaultAsync(ct);
        }
        var key = KeyOf(e);
        var title = (e.Title ?? "").Trim();

        List<TilCompany> companies;
        await using (var db = _dbf.Create())
            companies = await db.TilCompanies.AsNoTracking().ToListAsync(ct);
        var (matched, fuel) = e.Kind == SafeEntryKind.Bardagi
            ? ExchangeCompanySyncService.Detect(companies, title)
            : (null, Domain.Enums.FuelType.Petrol);

        if (matched is null || e.Amount == 0m)
        {
            await UnlinkAsync(e, ct);
            return matched is null && e.Kind == SafeEntryKind.Bardagi && title.Length > 0 && e.Amount != 0m
                ? ExchangeLinkResult.NotFound
                : ExchangeLinkResult.Empty;
        }

        await _companies.PutReceiptAsync(matched.Id, fuel, new CompanyRow
        {
            DateShamsi = e.DateShamsi ?? "",
            Name = "🏦 " + title + " از گاوصندوق",
            Poul = e.Amount,
            PoulCurrency = e.Currency,
            Note = e.Note ?? "",
            SourceReceiptId = key,
        }, ct);
        return ExchangeLinkResult.Linked;
    }

    /// <summary>حذف یا «دیگر بردگیِ شرکت نیست» ⇒ رسیدِ خودکارش از حسابِ شرکت هم می‌رود.</summary>
    public async Task UnlinkAsync(SafeEntry e, CancellationToken ct = default)
    {
        if (e is null) return;
        _perm.Require(Permission.EditData);
        var key = KeyOf(e);
        await using var db = _dbf.Create();
        var rows = await db.CompanyRows.Where(r => r.SourceReceiptId == key).ToListAsync(ct);
        if (rows.Count == 0) return;
        db.CompanyRows.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
