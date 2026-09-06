using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// ══ مصارف ═════════════════════════════════════════════════════════════════
/// جمعِ ساده — ولی همین‌جا می‌ماند تا هیچ صفحه‌ای جمعِ خودش را دستی نزند و
/// هر جا «کلِ مصارفِ ماه» لازم شد، همین یک عدد باشد.
/// </summary>
public sealed class ExpenseService
{
    public decimal Total(IEnumerable<Expense> rows) =>
        rows.Where(r => r is not null).Sum(r => r.Amount);

    /// <summary>مصارفِ یک ماه، برای صفحهٔ «مفاد/ضرر».</summary>
    public decimal TotalOfMonth(IEnumerable<Expense> rows, string monthKey) =>
        rows.Where(r => r is not null && r.MonthKey == monthKey).Sum(r => r.Amount);
}
