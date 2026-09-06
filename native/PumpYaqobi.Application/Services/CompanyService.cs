using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

public readonly record struct CompanySummary(
    decimal TotalUsd, decimal TotalAfn, decimal PaidAfn, decimal PaidUsd,
    decimal AlbaqiAfn, decimal AlbaqiUsd, decimal ConvRate);

/// <summary>
/// ══ شرکت‌های تیل ═══════════════════════════════════════════════════════════
/// رونوشتِ ‎cmpTon‎ · ‎cmpTotalUsd‎ · ‎cmpAfnTotal‎ · ‎cmpPoulAfn‎ · ‎cmpPoulUsd‎ ·
/// ‎cmpAlbaqi‎ · ‎cmpAlbaqiUsd‎ · ‎_companyConvRateOf‎.
///
/// ⚠️ نکتهٔ گزارش‌شدهٔ صاحب ریپو: «چرا الباقیِ دالر با افغانی برابر نیست؟»
/// ریشه‌اش این بود که یک سمت با «نرخ پول» و سمتِ دیگر با «نرخِ ردیف» حساب
/// می‌شد. حالا هر دو سمت با یک نرخ حساب می‌شوند و همیشه
/// «الباقیِ دالری × نرخ = الباقیِ افغانی» است. این را عوض نکنید.
/// </summary>
public sealed class CompanyService
{
    /// <summary>‎cmpTon(r)‎ — عددِ واردشده کیلوست؛ تن = کیلو ÷ ۱۰۰۰.
    /// خریدهای واقعی خودشان «تن» ذخیره کرده‌اند و همان می‌ماند.</summary>
    public decimal Ton(CompanyRow r) => r is null ? 0m : (r.Ton != 0m ? r.Ton : r.Kg / 1000m);

    public decimal TotalUsd(CompanyRow r) => Ton(r) * (r?.Usd ?? 0m);

    public decimal TotalAfn(CompanyRow r) => TotalUsd(r) * (r?.Rate ?? 0m);

    /// <summary>رسید به افغانی. رسیدِ دالری با نرخِ ردیف، بعد نرخِ پول، بعد نرخِ شرکت.</summary>
    public decimal PaidAfn(CompanyRow r, decimal fallbackRate)
    {
        if (r is null) return 0m;
        if (r.PoulCurrency != Currency.Usd) return r.Poul;
        var rate = r.Rate != 0m ? r.Rate : (r.PayRate ?? 0m) != 0m ? r.PayRate!.Value : fallbackRate;
        return r.Poul * rate;
    }

    /// <summary>معادلِ دالریِ رسید — با همان نرخی که سمتِ افغانی به کار می‌برد.</summary>
    public decimal PaidUsd(CompanyRow r, decimal fallbackRate)
    {
        if (r is null) return 0m;
        if (r.PoulCurrency == Currency.Usd) return r.Poul;
        var rate = r.Rate != 0m ? r.Rate : fallbackRate;
        return rate != 0m ? r.Poul / rate : 0m;
    }

    public decimal AlbaqiAfn(CompanyRow r, decimal fallbackRate) => TotalAfn(r) - PaidAfn(r, fallbackRate);

    public decimal AlbaqiUsd(CompanyRow r, decimal fallbackRate) => TotalUsd(r) - PaidUsd(r, fallbackRate);

    /// <summary>
    /// ‎_companyConvRateOf‎ — نرخِ تبدیلِ مؤثر: نرخِ دستیِ شرکت، وگرنه میانگینِ
    /// خریدها، وگرنه نخستین نرخی که در ردیف‌ها پیدا شود.
    /// </summary>
    public decimal ConvRate(TilCompany? company, IEnumerable<CompanyRow> rows)
    {
        if (company is null) return 0m;
        var list = rows as IList<CompanyRow> ?? rows.ToList();
        decimal sumUsd = 0, sumAfn = 0;
        foreach (var r in list) { sumUsd += TotalUsd(r); sumAfn += TotalAfn(r); }

        var rate = (company.UsdRate ?? 0m) != 0m ? company.UsdRate!.Value
                 : sumUsd > 0m ? sumAfn / sumUsd : 0m;
        if (rate == 0m)
            foreach (var r in list)
            {
                var rr = (r.PayRate ?? 0m) != 0m ? r.PayRate!.Value : r.Rate;
                if (rr != 0m) { rate = rr; break; }
            }
        return rate;
    }

    /// <summary>جمع‌های یک دفترِ شرکت (پطرول یا دیزل، یا هر دو با هم).</summary>
    public CompanySummary Summarize(TilCompany company, IEnumerable<CompanyRow> rows)
    {
        var list = rows as IList<CompanyRow> ?? rows.ToList();
        var rate = ConvRate(company, list);
        decimal usd = 0, afn = 0, paidAfn = 0, paidUsd = 0;
        foreach (var r in list)
        {
            usd += TotalUsd(r);
            afn += TotalAfn(r);
            paidAfn += PaidAfn(r, rate);
            paidUsd += PaidUsd(r, rate);
        }
        return new CompanySummary(usd, afn, paidAfn, paidUsd, afn - paidAfn, usd - paidUsd, rate);
    }

    /// <summary>ردیف‌های یک دفترِ شرکت.</summary>
    public static IEnumerable<CompanyRow> RowsOf(TilCompany c, FuelType fuel) =>
        c.Rows.Where(r => r.Fuel == fuel).OrderBy(r => r.SortIndex).ThenBy(r => r.Id);
}
