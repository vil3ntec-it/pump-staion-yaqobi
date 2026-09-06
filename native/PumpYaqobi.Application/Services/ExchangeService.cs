using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

public readonly record struct ExchangeSummary(
    decimal TotalUsd, decimal TotalBardagi, decimal TotalBardagiUsd, decimal Baqi);

/// <summary>
/// ══ صرافی ═════════════════════════════════════════════════════════════════
/// همهٔ عددهای این بخش دالرند: «مبلغ» به ارزِ خودش وارد می‌شود و با «فی»
/// شکسته می‌شود تا دالر به دست آید.
/// </summary>
public sealed class ExchangeService
{
    /// <summary>
    /// ‎sarrafiBroken(r)‎ = مبلغ ÷ فی. اگر فی صفر باشد ⇒ صفر.
    /// ⚠️ تقسیم بر صفر عمداً صفر می‌دهد، نه بی‌نهایت — مثلِ خودِ HTML.
    /// </summary>
    public decimal ToUsd(ExchangeRow r)
    {
        if (r is null || r.Rate == 0m) return 0m;
        return r.Amount / r.Rate;
    }

    /// <summary>‎sarrafiBaqi(r)‎ = دالرِ شکسته − بردگی.</summary>
    public decimal RowBaqi(ExchangeRow r) => ToUsd(r) - (r?.Bardagi ?? 0m);

    /// <summary>جمع‌های یک ماه — همان چهار عددی که صفحه نشان می‌دهد.</summary>
    public ExchangeSummary Summarize(IEnumerable<ExchangeRow> rows)
    {
        decimal usd = 0, bard = 0, bardUsd = 0, baqi = 0;
        foreach (var r in rows)
        {
            if (r is null) continue;
            usd  += ToUsd(r);
            bard += r.Bardagi;
            baqi += RowBaqi(r);
            // بردگی به دالر: بردگی ÷ فی (فیِ صفر ⇒ صفر)
            bardUsd += r.Rate != 0m ? r.Bardagi / r.Rate : 0m;
        }
        return new ExchangeSummary(usd, bard, bardUsd, baqi);
    }
}
