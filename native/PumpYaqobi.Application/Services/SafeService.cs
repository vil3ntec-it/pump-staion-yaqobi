using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

/// <summary>جمعِ چندارزی — هر ارز جدا نگه داشته می‌شود، هرگز با هم جمع نمی‌شوند.</summary>
public readonly record struct MoneyPair(decimal Afn, decimal Usd)
{
    public static MoneyPair operator +(MoneyPair a, MoneyPair b) => new(a.Afn + b.Afn, a.Usd + b.Usd);
    public static MoneyPair operator -(MoneyPair a, MoneyPair b) => new(a.Afn - b.Afn, a.Usd - b.Usd);
}

public readonly record struct SafeSummary(MoneyPair Bardagi, MoneyPair Mandagi, MoneyPair Net);

/// <summary>
/// ══ گاوصندوق ══════════════════════════════════════════════════════════════
/// رونوشتِ منطقِ ‎renderSafe‎ در HTML.
///
/// ⚠️ جهتِ «موجودی خالص» عمداً همین است: ماندگی اضافه می‌کند و بردگی کم.
/// پیش از این برعکس بود و گزارشِ صاحب ریپو همین بود: «آن مقدار دالر را بردم،
/// باز هم می‌گوید این‌قدر دالر داری؛ باید صفر می‌شد چون همه‌اش را بردگی گرفتم.»
///
/// ⚠️ دو ارز هرگز با نرخ به هم تبدیل نمی‌شوند — تا خطای نرخ وارد حساب نشود.
/// </summary>
public sealed class SafeService
{
    /// <summary>‎_safeAddAmt‎ — افزودنِ یک ردیف به بستهٔ همان ارزش.</summary>
    private static MoneyPair Add(MoneyPair p, SafeEntry e) =>
        e.Currency == Domain.Entities.Currency.Usd
            ? p with { Usd = p.Usd + e.Amount }
            : p with { Afn = p.Afn + e.Amount };

    /// <summary>
    /// جمع‌های یک ماه (یا هر مجموعه‌ای که به آن داده شود).
    /// سه کادرِ بالای صفحه دقیقاً همین سه عدد را نشان می‌دهند.
    /// </summary>
    public SafeSummary Summarize(IEnumerable<SafeEntry> entries)
    {
        var bard = new MoneyPair();
        var mand = new MoneyPair();
        foreach (var e in entries)
        {
            if (e is null) continue;
            if (e.Kind == SafeEntryKind.Bardagi) bard = Add(bard, e);
            else mand = Add(mand, e);
        }
        // موجودی خالص = ماندگی − بردگی (جهتِ درست، توضیحِ بالا)
        return new SafeSummary(bard, mand, mand - bard);
    }
}
