using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// جمع‌های کارتِ یک قرض‌دار — همان بستهٔ ‎_debtSumFigures(p)‎.
///
/// ⚠️ «الباقی» دو معنیِ کاملاً جدا دارد و هیچ‌وقت قاطی نمی‌شوند:
/// در حسابِ واحدِ پول افغانی است، و در حسابِ واحدِ تیل لیتر.
/// <paramref name="IsMoney"/> می‌گوید کدام.
/// </summary>
public readonly record struct DebtSumFigures(
    bool IsMoney, decimal Bardagi, decimal Rasid, decimal Albaqi);

/// <summary>یک ردیفِ «قرض‌های کهنه».</summary>
/// <param name="DaysIdle">
/// چند روز است هیچ ردیفِ تازه‌ای ندارد. ‎-1‎ یعنی هیچ ردیفش تاریخ ندارد
/// («بی‌تاریخ») — همان ‎days = -1‎ی نسخهٔ وب، که در مرتب‌سازی هم آخر می‌افتد.
/// </param>
public readonly record struct AgingRow(Debtor Person, DebtSumFigures Figures, int DaysIdle);

/// <summary>کدام قرض‌داران: همه، فقط واحدِ تیل، یا فقط واحدِ پول.</summary>
public enum AgingFilter { All = 0, Fuel = 1, Money = 2 }

/// <summary>
/// ══ قرض‌های کهنه ═══════════════════════════════════════════════════════════
/// رونوشتِ ‎_agingRows(filter)‎.
///
/// فقط کسانی می‌آیند که هنوز الباقیِ مثبت دارند، و بی‌حرکت‌ترین‌ها اول. «بی‌حرکت»
/// یعنی از تاریخِ آخرین ردیفِ حسابش (هر حسابی، اصلی یا فرعی، تیل یا پول) چند
/// روز گذشته — نه از تاریخِ ساختِ حساب.
///
/// این بخش هیچ چیزی نمی‌نویسد؛ فقط نگاه می‌کند.
/// </summary>
public sealed class AgingService
{
    private readonly DebtCalculationService _calc;

    public AgingService(DebtCalculationService calc) => _calc = calc;

    /// <summary>‎_debtSumFigures(p)‎ — واحدِ حسابِ اصلی تعیین می‌کند کدام عددها.</summary>
    public DebtSumFigures Figures(Debtor p)
    {
        if (p is null) return default;
        var t = _calc.SumTotals(p.AllAccounts()).All;
        if (p.MainAccount.Mode.IsMoney())
            return new DebtSumFigures(true, t.Bardagi, t.Rasid, t.Albaqi);

        decimal rasidL = 0m;
        foreach (var a in p.AllAccounts()) rasidL += a.RasidFuelPetrol + a.RasidFuelDiesel;
        return new DebtSumFigures(false, t.Liters, rasidL, t.Liters - rasidL);
    }

    /// <summary>‎_agingRows(filter)‎ — بی‌حرکت‌ترین اول.</summary>
    /// <param name="today">تاریخِ شمسیِ امروز؛ مبنای «چند روز بی‌حرکت».</param>
    public List<AgingRow> Rows(IEnumerable<Debtor> people, AgingFilter filter, string today)
    {
        var todayK = Shamsi.DayKey(today);
        var rows = new List<AgingRow>();

        foreach (var p in people)
        {
            if (p is null) continue;
            var f = Figures(p);
            if (!(f.Albaqi > 0m)) continue;
            if (filter == AgingFilter.Money && !f.IsMoney) continue;
            if (filter == AgingFilter.Fuel && f.IsMoney) continue;

            var lastK = 0;
            foreach (var a in p.AllAccounts())
            {
                foreach (var r in a.FuelRows)
                { var k = Shamsi.DayKey(r?.DateShamsi); if (k > lastK) lastK = k; }
                foreach (var r in a.MoneyRows)
                { var k = Shamsi.DayKey(r?.DateShamsi); if (k > lastK) lastK = k; }
            }

            rows.Add(new AgingRow(p, f, lastK > 0 ? Math.Max(0, todayK - lastK) : -1));
        }

        // ⚠️ مرتب‌سازیِ پایدار لازم است: در نسخهٔ وب ‎Array.sort‎ پایدار است و
        // قرض‌دارانی که هم‌سن‌اند به همان ترتیبِ فهرستِ اصلی می‌مانند.
        return rows.OrderByDescending(r => r.DaysIdle).ToList();
    }
}
