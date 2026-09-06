using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

public readonly record struct RetailSummary(
    decimal Liters, decimal Bardagi, decimal Rasid, decimal Albaqi);

/// <summary>
/// ══ چکنه ══════════════════════════════════════════════════════════════════
/// فروشِ خرد. رونوشتِ ‎_chakanaBardagi‎ و جمع‌های ‎renderChakana‎.
///
/// ⚠️ ردیفی که «به پول» ثبت شده، بردگی‌اش همان عددِ نوشته‌شده است — نه
/// لیتر×فی. اگر این را یکی کنیم، حسابِ ردیف‌های پولی خراب می‌شود.
/// </summary>
public sealed class RetailService
{
    /// <summary>‎_chakanaBardagi(e)‎</summary>
    public decimal Bardagi(RetailRow r) =>
        r is null ? 0m : (r.ByMoney ? r.Bardagi : r.Liters * r.PricePerLiter);

    /// <summary>الباقیِ یک ردیف — بردگی − رسید.</summary>
    public decimal Albaqi(RetailRow r) => Bardagi(r) - (r?.Rasid ?? 0m);

    public RetailSummary Summarize(IEnumerable<RetailRow> rows)
    {
        decimal liters = 0, bard = 0, rasid = 0;
        foreach (var r in rows)
        {
            if (r is null) continue;
            if (!r.ByMoney) liters += r.Liters;   // ردیفِ پولی لیتر ندارد
            bard += Bardagi(r);
            rasid += r.Rasid;
        }
        return new RetailSummary(liters, bard, rasid, bard - rasid);
    }
}
