using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>یک موردِ پیداشده در «جستجوی خرید» — ‎items[]‎ی ‎runCmpSearch‎.</summary>
/// <param name="IsPurchase">خریدِ مخزن (📦) یا ردیفِ جدولِ شرکت.</param>
/// <param name="ArchiveId">صفر یعنی جدولِ فعلی؛ وگرنه شناسهٔ جدولِ آرشیو.</param>
public sealed record PurchaseHit(
    bool IsPurchase, long PurchaseId, long CompanyId, long ArchiveId, int RowIndex,
    string Name, FuelType Fuel, string Date, decimal Ton, decimal Usd, decimal Afn, string Where);

/// <summary>
/// ══ خریدهای مخزنِ یک شرکت، و «جستجوی خرید» ═══════════════════════════════
/// رونوشتِ ‎_companyPurchases‎ · ‎_cmpCollect‎ · ‎runCmpSearch‎ · ‎_csQtyOk‎ ·
/// ‎_csDateNorm‎ی سایت. فقط می‌خوانَد — هیچ داده‌ای عوض نمی‌شود.
///
/// خریدِ مخزن با **نامِ فروشنده** به شرکت می‌رسد (همان قاعدهٔ سایت)، و بازه‌اش
/// با شمارهٔ خرید: «زنده» یعنی بعد از آخرین «جدول جدید»، و «آرشیو» یعنی بینِ
/// دو نقطهٔ شمارشِ همان جدول.
/// </summary>
public sealed class CompanyPurchaseService
{
    private readonly CompanyService _calc;
    public CompanyPurchaseService(CompanyService calc) => _calc = calc;

    public static bool SameName(string? a, string? b) =>
        PostingService.NormFa(a) == PostingService.NormFa(b) && PostingService.NormFa(a).Length > 0;

    /// <summary>خریدهای یک شرکت در یک بازه — ‎after &lt; Id ≤ before‎ (‎before‎ صفر یعنی بی‌سقف).</summary>
    public static List<FuelPurchase> Of(IEnumerable<FuelPurchase> all, string? companyName, FuelType fuel,
                                        long after = 0, long before = 0)
    {
        var key = PostingService.NormFa(companyName);
        if (key.Length == 0) return new();
        return all.Where(e => e is not null && e.Fuel == fuel && PostingService.NormFa(e.Seller) == key
                              && e.Id > after && (before <= 0 || e.Id <= before))
                  .OrderBy(e => e.Id).ToList();
    }

    /// <summary>خریدهای «زنده»ی یک شرکت — بعد از نقطهٔ شمارشِ همان تیل.</summary>
    public static List<FuelPurchase> Live(IEnumerable<FuelPurchase> all, TilCompany c, FuelType fuel) =>
        Of(all, c.Name, fuel, fuel == FuelType.Diesel ? c.PurchaseCheckpointDiesel : c.PurchaseCheckpointPetrol);

    /// <summary>
    /// ردیف‌های «دستی»ِ یک دفتر: آن‌هایی که به هیچ خریدِ مخزنی وصل نیستند (یا
    /// خریدشان دیگر وجود ندارد) — ‎isManual‎ی سایت.
    /// </summary>
    public static List<CompanyRow> Manual(IEnumerable<CompanyRow> rows, IEnumerable<FuelPurchase> all)
    {
        var ids = all.Select(e => e.LegacyId ?? "").Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
        return rows.Where(r => string.IsNullOrWhiteSpace(r.SourcePurchaseId) || !ids.Contains(r.SourcePurchaseId!)).ToList();
    }

    // ── جستجو ────────────────────────────────────────────────────────────

    /// <summary>‎_csQtyOk‎ — عدد هم «کیلو» فهمیده می‌شود هم «تن»: ۱۲۳۰۰ و ۱۲٫۳ هر دو همان بار.</summary>
    /// ⚠️ ردیفِ بی‌مقدار (رسیدِ خالی) با هیچ عددی جور نیست — وگرنه تلورانسِ ۵۱
    /// کیلوییِ سایت هر عددِ کوچکی را به ردیف‌های صفر می‌چسباند.
    public static bool QtyOk(decimal? q, decimal ton) =>
        q is null || (ton > 0m && (Math.Abs(ton - q.Value) <= 0.051m || Math.Abs(ton * 1000m - q.Value) <= 51m));

    /// <summary>‎_csDateNorm‎ — «1405/05/13» و «۱۴۰۵/۵/۱۳» یکی حساب شوند.</summary>
    public static string DateNorm(string? s)
    {
        var parts = Shamsi.ToEnDigits(s ?? "").Split(new[] { '/', '-', '.', ' ', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                          .Where(x => x.All(char.IsDigit)).Select(x => int.Parse(x).ToString());
        return string.Join("/", parts);
    }

    public static bool DateOk(string dateQuery, string? d) =>
        dateQuery.Length == 0 || DateNorm(d).Contains(dateQuery, StringComparison.Ordinal);

    /// <summary>
    /// ‎runCmpSearch‎ — مقدار (کیلو یا تن) و/یا تاریخ، با فیلترِ تیل، در خریدهای
    /// مخزن و ردیف‌های جدولِ فعلی و جدول‌های آرشیوِ شرکت‌ها. هر خرید یک‌بار:
    /// ردیفی که به خریدِ پیداشده وصل است دوباره نمی‌آید.
    /// </summary>
    public List<PurchaseHit> Search(IEnumerable<FuelPurchase> purchases, IEnumerable<TilCompany> companies,
                                    IEnumerable<(CompanyTableArchive Archive, IReadOnlyList<CompanyRow> Rows)> archives,
                                    decimal? qty, string? dateRaw, FuelType? fuel, long? onlyCompany)
    {
        var dq = DateNorm(dateRaw);
        var items = new List<PurchaseHit>();
        if (qty is null && dq.Length == 0) return items;

        var cos = companies.Where(c => onlyCompany is null || c.Id == onlyCompany).ToList();
        var coName = onlyCompany is null ? null : PostingService.NormFa(cos.FirstOrDefault()?.Name);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // ۱) خریدهای مخزن
        foreach (var e in purchases)
        {
            if (e is null) continue;
            if (fuel is not null && e.Fuel != fuel) continue;
            if (coName is not null && PostingService.NormFa(e.Seller) != coName) continue;
            if (!DateOk(dq, e.DateShamsi)) continue;
            var ton = e.Ton != 0m ? e.Ton : e.Kg / 1000m;
            if (!QtyOk(qty, ton)) continue;
            if (e.LegacyId is { Length: > 0 } lid) seen.Add(lid);
            var c = companies.FirstOrDefault(x => SameName(x.Name, e.Seller));
            items.Add(new PurchaseHit(true, e.Id, c?.Id ?? 0, 0, 0, string.IsNullOrWhiteSpace(e.Seller) ? "—" : e.Seller!,
                                      e.Fuel, e.DateShamsi ?? "", ton, e.TotalUsd, e.TotalAfn, "📦 خرید مخزن"));
        }

        // ۲) ردیف‌های جدولِ حسابِ شرکت — جدولِ فعلی و جدول‌های آرشیو
        void Scan(TilCompany c, IReadOnlyList<CompanyRow> rows, FuelType fu, string where, long archiveId)
        {
            if (fuel is not null && fu != fuel) return;
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r is null) continue;
                if (r.SourcePurchaseId is { Length: > 0 } sp && seen.Contains(sp)) continue;
                var ton = _calc.Ton(r);
                var nm = (r.Name ?? "").Trim();
                if (ton == 0m && nm.Length == 0) continue;
                if (!DateOk(dq, r.DateShamsi)) continue;
                if (!QtyOk(qty, ton)) continue;
                items.Add(new PurchaseHit(false, 0, c.Id, archiveId, i, nm.Length > 0 ? nm : c.Name ?? "—",
                                          fu, r.DateShamsi ?? "", ton, _calc.TotalUsd(r), _calc.TotalAfn(r), where));
            }
        }
        var arcList = archives.ToList();
        foreach (var c in cos)
        {
            Scan(c, CompanyService.RowsOf(c, FuelType.Petrol).ToList(), FuelType.Petrol, "📋 جدول فعلی پطرول", 0);
            Scan(c, CompanyService.RowsOf(c, FuelType.Diesel).ToList(), FuelType.Diesel, "📋 جدول فعلی دیزل", 0);
            foreach (var (h, rows) in arcList.Where(a => a.Archive.CompanyId == c.Id))
                Scan(c, rows, h.Fuel, "🗂️ جدول آرشیو" + (string.IsNullOrWhiteSpace(h.CreatedShamsi) ? "" : " — " + h.CreatedShamsi), h.Id);
        }
        return items;
    }
}
