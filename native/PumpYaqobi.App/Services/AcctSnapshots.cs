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
///
/// ══ گزارشِ صاحب ریپو: «کیو‌آر یک چیزِ اشتباه نشان می‌دهد» ═══════════════════
///
/// سه اشتباه بود و هر سه همین‌جا بود. عددِ کیو‌آر با عددِ خودِ برنامه نمی‌خواند:
///
///  ۱) <b>رسید دو بار شمرده می‌شد.</b> چهار عددِ ‎Rasid…‎ی حساب دیگر «منبع»
///     نیستند، فقط جمعِ ردیف‌های همان دفترند (‎SyncReceiptTotals‎). کیو‌آر
///     آن‌ها را <i>به‌علاوهٔ</i> جمعِ ستونِ رسید می‌گذاشت، یعنی دو برابر.
///
///  ۲) <b>در دفترِ تیل ستونِ اشتباه خوانده می‌شد.</b> برنامه در دفترِ تیل
///     ‎RasidFuel‎ را نشان می‌دهد و در دفترِ پول ‎Rasid‎ را (‎HeadRasid‎). کیو‌آر
///     در هر دو حالت ‎Rasid‎ را می‌خواند — یعنی در حسابِ تیل عددِ یک دفترِ
///     دیگر را با لیتر جمع می‌کرد.
///
///  ۳) <b>فیصدی اصلاً در الباقی نبود.</b> فرمولِ خودِ برنامه (و سایت،
///     ‎_updatePersonTotals‎) این است و نه ‎بردگی − رسید‎:
///
///         فیصدی  = رسید × ٪
///         الباقی = بردگی + فیصدی − رسید
///
///     و فیصدیِ پطرول و دیزل هیچ ربطی به هم ندارند، پس هر تیل با فیصدیِ
///     <i>خودش</i> حساب می‌شود و بعد جمع می‌شوند.
/// </summary>
public static class AcctSnapshots
{
    /// <summary>
    /// حسابِ یک قرض‌دار — همان جدولی که در صفحهٔ شخص دیده می‌شود.
    /// </summary>
    /// <param name="rows">
    /// ⚠️ ردیف‌های <b>یک</b> دفتر — همان ‎acct.ActiveRows()‎. دو دفترِ «واحد
    /// تیل» و «واحد پول» کاملاً جدا هستند و قاطی کردنشان عددِ بی‌معنی می‌دهد
    /// (لیترِ یکی با افغانیِ دیگری). برای همین <see cref="ForDebtAccount(string,string?,DebtAccount,DebtCalculationService)"/>
    /// هست تا صداکننده لازم نباشد خودش انتخاب کند.
    /// </param>
    public static AcctSnapshot ForDebtAccount(
        string personName, string? accountTitle, DebtAccount acct,
        IReadOnlyList<DebtRow> rows, DebtCalculationService calc)
    {
        var money = acct.Mode.IsMoney();
        var t = calc.SplitTotals(rows);

        // ── هر تیل با فیصدیِ خودش ───────────────────────────────────────────
        // عینِ ‎PersonViewModel.Remainder(fuel)‎. «بردگی» در دفترِ پول افغانی
        // است و در دفترِ تیل لیتر — همان تفکیکی که کلِ برنامه دارد.
        var (bordP, rasidP, commP, remP) = Leg(t.Petrol, money, calc.PercentOf(acct, FuelType.Petrol));
        var (bordD, rasidD, commD, remD) = Leg(t.Diesel, money, calc.PercentOf(acct, FuelType.Diesel));

        var snap = new AcctSnapshot
        {
            Kind = money ? "قرض‌دار — واحد پول" : "قرض‌دار — واحد تیل",
            Name = personName,
            Account = accountTitle ?? "",
            Unit = money ? "افغانی" : "لیتر",
            Date = Shamsi.Today(),
            Summary =
            {
                new[] { "جمله بردگی", Shamsi.Money(bordP + bordD) },
                new[] { "جمله رسید", Shamsi.Money(rasidP + rasidD) },
                new[] { "فیصدی ما", Shamsi.Money(commP + commD) },
                new[] { "الباقی", Shamsi.Money(remP + remD) },
            },
            // ⚠️ همان ستونی که در جدولِ برنامه دیده می‌شود، نه هر دو —
            // ‎ShowRasidColumn‎ / ‎ShowRasidFuelColumn‎.
            Head = { "تاریخ", "نام", "حواله", "تیل",
                     money ? "مقدار (افغانی)" : "مقدار تیل",
                     "فی", "بردگی", money ? "رسید" : "رسید تیل" },
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
                Shamsi.MoneyOrBlank(money ? r.Rasid : r.RasidFuel),
            });
        }

        return snap;
    }

    /// <summary>
    /// همان بالایی، ولی دفترِ درست را <b>خودش</b> برمی‌دارد.
    ///
    /// راهِ امن برای هر جایی که فقط حساب را دارد (کارتِ قرض‌دار). پیش از این
    /// کارت هر دو دفتر را به هم می‌چسباند و عددِ کیو‌آرش با عددِ داخلِ حساب
    /// فرق می‌کرد.
    /// </summary>
    public static AcctSnapshot ForDebtAccount(
        string personName, string? accountTitle, DebtAccount acct, DebtCalculationService calc)
        => ForDebtAccount(personName, accountTitle, acct, acct.ActiveRows(), calc);

    /// <summary>
    /// چهار عددِ یک تیل: بردگی، رسید، فیصدی و الباقی — عینِ سربرگِ برنامه.
    /// </summary>
    private static (decimal Bord, decimal Rasid, decimal Comm, decimal Albaqi)
        Leg(FuelTotals t, bool money, decimal pct)
    {
        var bord = money ? t.Bardagi : t.Liters;
        var rasid = money ? t.Rasid : t.RasidFuel;
        var comm = DebtCalculationService.Round0(rasid * pct / 100m);
        return (bord, rasid, comm, DebtCalculationService.Round0(bord + comm - rasid));
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
}
