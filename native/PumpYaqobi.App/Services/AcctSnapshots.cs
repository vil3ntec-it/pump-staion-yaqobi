using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ ساختنِ عکسِ حساب برای کیو‌آر ═══════════════════════════════════════════
///
/// یک جا، برای همهٔ بخش‌هایی که کیو‌آر دارند — تا عددهایی که مشتری روی گوشی
/// می‌بیند مو‌به‌مو همان چیزی باشد که روی صفحهٔ برنامه است.
///
/// ⚠️ هیچ محاسبهٔ تازه‌ای این‌جا نیست: جمع‌ها از ‎DebtCalculationService‎ می‌آیند
/// و ستون‌ها همان ستون‌های جدولِ روی صفحه‌اند.
/// </summary>
public static class AcctSnapshots
{
    /// <summary>حسابِ یک قرض‌دار — همان جدولی که در صفحهٔ شخص دیده می‌شود.</summary>
    public static AcctSnapshot ForDebtAccount(
        string personName, string? accountTitle, DebtAccount acct,
        IReadOnlyList<DebtRow> rows, DebtCalculationService calc)
    {
        var money = acct.Mode.IsMoney();
        var t = calc.SplitTotals(rows);

        // «بردگی» در دفترِ پول افغانی است و در دفترِ تیل لیتر — همان تفکیکی که
        // کلِ برنامه دارد و هیچ‌وقت قاطی نمی‌شود.
        var bord = money ? t.All.Bardagi : t.All.Liters;
        var rasid = money
            ? acct.RasidMoneyPetrol + acct.RasidMoneyDiesel + t.All.Rasid
            : acct.RasidFuelPetrol + acct.RasidFuelDiesel + t.All.Rasid;

        var snap = new AcctSnapshot
        {
            Kind = money ? "قرض‌دار — واحد پول" : "قرض‌دار — واحد تیل",
            Name = personName,
            Account = accountTitle ?? "",
            Unit = money ? "افغانی" : "لیتر",
            Date = Shamsi.Today(),
            Summary =
            {
                new[] { "جمله بردگی", Shamsi.Money(Round(bord)) },
                new[] { "جمله رسید", Shamsi.Money(Round(rasid)) },
                new[] { "الباقی", Shamsi.Money(Round(bord - rasid)) },
            },
            Head = { "تاریخ", "نام", "حواله", "تیل", "مقدار", "فی", "بردگی", "رسید" },
        };

        foreach (var r in rows)
        {
            if (r is null) continue;
            snap.Rows.Add(new[]
            {
                r.DateShamsi ?? "",
                r.Name ?? "",
                r.Hawala ?? "",
                r.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                Shamsi.MoneyOrBlank(r.Liters),
                Shamsi.MoneyOrBlank(r.PricePerLiter ?? 0m),
                Shamsi.MoneyOrBlank(r.Bardagi),
                Shamsi.MoneyOrBlank(r.Rasid),
            });
        }

        return snap;
    }

    /// <summary>حسابِ یک شرکتِ تیل — خریدها و رسیدهایش.</summary>
    public static AcctSnapshot ForCompany(string name, string unit,
                                          IEnumerable<string[]> rows,
                                          IEnumerable<string[]> summary,
                                          IEnumerable<string> head)
    {
        var snap = new AcctSnapshot
        {
            Kind = "شرکت تیل",
            Name = name,
            Unit = unit,
            Date = Shamsi.Today(),
        };
        snap.Head.AddRange(head);
        snap.Summary.AddRange(summary);
        snap.Rows.AddRange(rows);
        return snap;
    }

    private static decimal Round(decimal v) => Math.Round(v, 2);
}
