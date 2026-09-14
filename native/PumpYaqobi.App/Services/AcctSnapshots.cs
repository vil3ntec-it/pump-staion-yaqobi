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
        var pctP = calc.PercentOf(acct, FuelType.Petrol);
        var pctD = calc.PercentOf(acct, FuelType.Diesel);

        // دفترِ فعال از ردیف‌هایی که به ما داده شد (یعنی همان چیزی که همین
        // حالا روی صفحه است)، و دفترِ دیگر از خودِ حساب.
        var fuelRows  = money ? (IReadOnlyList<DebtRow>)acct.FuelRows  : rows;
        var moneyRows = money ? rows : (IReadOnlyList<DebtRow>)acct.MoneyRows;

        var snap = new AcctSnapshot
        {
            Kind = money ? "قرض‌دار — واحد پول" : "قرض‌دار — واحد تیل",
            Name = personName,
            Account = accountTitle ?? "",
            Unit = money ? "افغانی" : "لیتر",
            Date = Shamsi.Today(),
        };

        // ⚠️ ترتیب مهم است: دفترِ **فعال** اول می‌آید، تا صفحه که تبِ اول را
        // باز می‌کند همان دفتری را نشان بدهد که روی کامپیوتر باز بود.
        var active = Book(money, money ? moneyRows : fuelRows, pctP, pctD, calc);
        var other  = Book(!money, money ? fuelRows : moneyRows, pctP, pctD, calc);

        snap.Books.Add(active);
        // دفترِ دومی که هیچ ردیف و هیچ عددی ندارد فقط کیو‌آر را چاق می‌کند و
        // به مشتری یک تبِ خالی نشان می‌دهد.
        if (HasAnything(other)) snap.Books.Add(other);

        // ══ جای قدیمی، دست‌نخورده ══════════════════════════════════════════
        // صفحه‌های قدیمی و کدهای چاپ‌شده همین را می‌خوانند.
        snap.Summary.AddRange(active.Summary);
        snap.Head.AddRange(active.Head);
        snap.Rows.AddRange(active.Rows);

        return snap;
    }

    /// <summary>یک دفتر از یک حساب — خلاصه، تفکیکِ تیل، جدول و آرشیوِ ماه‌ها.</summary>
    private static AcctBook Book(bool money, IReadOnlyList<DebtRow> rows,
                                 decimal pctP, decimal pctD,
                                 DebtCalculationService calc)
    {
        var t = calc.SplitTotals(rows);

        // ── هر تیل با فیصدیِ خودش ───────────────────────────────────────────
        // عینِ ‎PersonViewModel.Remainder(fuel)‎. «بردگی» در دفترِ پول افغانی
        // است و در دفترِ تیل لیتر — همان تفکیکی که کلِ برنامه دارد.
        var p = Leg(t.Petrol, money, pctP);
        var d = Leg(t.Diesel, money, pctD);

        var book = new AcctBook
        {
            Title = money ? "واحد پول" : "واحد تیل",
            Unit = money ? "افغانی" : "لیتر",
            DateCol = 0,
            FuelCol = 3,
            Summary =
            {
                new[] { "جمله بردگی", Shamsi.Money(p.Bord + d.Bord) },
                new[] { "جمله رسید", Shamsi.Money(p.Rasid + d.Rasid) },
                new[] { "فیصدی ما", Shamsi.Money(p.Comm + d.Comm) },
                new[] { "الباقی", Shamsi.Money(p.Albaqi + d.Albaqi) },
            },
            // ══ «شرکت‌ها نمی‌دانند دیزل چقدر از من می‌خواهند یا پطرول چقدر» ══
            // همین جدول جوابِ آن است: هر تیل، چهار عددِ خودش.
            FuelHead = { "تیل", "بردگی", "رسید", "فیصدی", "الباقی" },
            Fuels =
            {
                Fuel("پطرول", p, pctP),
                Fuel("دیزل", d, pctD),
            },
            // ⚠️ همان ستونی که در جدولِ برنامه دیده می‌شود، نه هر دو —
            // ‎ShowRasidColumn‎ / ‎ShowRasidFuelColumn‎.
            Head = { "تاریخ", "نام", "حواله", "تیل",
                     money ? "مقدار (افغانی)" : "مقدار تیل",
                     "فی", "بردگی", money ? "رسید" : "رسید تیل" },
        };

        // ماه ⇒ [بردگی, رسید]. ⚠️ روی **همهٔ** ردیف‌ها، پیش از هر کم شدنی
        // برای جا شدن در کیو‌آر — وگرنه ماه‌های قدیمی از آرشیو هم می‌افتادند.
        var months = new Dictionary<string, decimal[]>();
        var order = new List<string>();

        foreach (var r in rows)
        {
            if (r is null) continue;
            book.Rows.Add(new[]
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

            var key = Shamsi.MonthKey(r.DateShamsi);
            if (key.Length == 0) continue;
            if (!months.TryGetValue(key, out var acc)) { months[key] = acc = new decimal[2]; order.Add(key); }
            acc[0] += money ? r.Bardagi : r.Liters;
            acc[1] += money ? r.Rasid : r.RasidFuel;
        }

        foreach (var key in order.OrderBy(k => k, StringComparer.Ordinal))
        {
            var acc = months[key];
            book.Archive.Add(new[]
            {
                key,
                Shamsi.Money(acc[0]),
                Shamsi.Money(acc[1]),
                Shamsi.Money(acc[0] - acc[1]),
            });
        }

        return book;
    }

    /// <summary>یک ردیفِ تفکیکِ تیل — با فیصدیِ خودش کنارِ نام.</summary>
    private static string[] Fuel(string name,
        (decimal Bord, decimal Rasid, decimal Comm, decimal Albaqi) t, decimal pct)
        => new[]
        {
            pct == 0m ? name : name + " (٪" + Shamsi.Money(pct) + ")",
            Shamsi.Money(t.Bord), Shamsi.Money(t.Rasid),
            Shamsi.Money(t.Comm), Shamsi.Money(t.Albaqi),
        };

    /// <summary>
    /// دفتری که نه ردیفی دارد نه عددی — یعنی اصلاً وجود ندارد و نباید تبِ
    /// خالی بسازد.
    /// </summary>
    private static bool HasAnything(AcctBook b)
        => b.Rows.Count > 0 || b.Summary.Any(s => Shamsi.Num(s[1]) != 0m);

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

    // ══════════════════════════════════════════════════════════════════════
    //  شرکتِ تیل
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// حسابِ یک شرکتِ تیل — خریدها، پرداخت‌ها، تفکیکِ پطرول/دیزل و آرشیوِ ماه‌ها.
    ///
    /// گزارشِ صاحب ریپو: «شرکت‌ها نمی‌دانند دیزل چقدر از من می‌خواهند یا پطرول
    /// چقدر.» کیو‌آر تا امروز فقط یک جمعِ کل داشت؛ حالا هر تیل ردیفِ خودش را
    /// دارد.
    ///
    /// ⚠️ هیچ حسابی این‌جا نیست: هر عدد از <see cref="CompanyService"/> می‌آید،
    /// همان سرویسی که خودِ صفحهٔ شرکت از آن می‌خواند. تفکیکِ تیل هم فقط یعنی
    /// همان ‎Summarize‎ روی زیرمجموعهٔ ردیف‌ها.
    /// </summary>
    public static AcctSnapshot ForCompany(TilCompany company, CompanyService calc)
    {
        var name = company.Name ?? "";
        var all = company.Rows.OrderBy(r => r.DateKey).ThenBy(r => r.SortIndex).ToList();
        var s = calc.Summarize(company, all);

        var book = new AcctBook
        {
            Title = "خرید تیل",
            Unit = "افغانی",
            DateCol = 0,
            FuelCol = 2,
            Summary =
            {
                new[] { "کلِ دالر", Shamsi.Money(Math.Round(s.TotalUsd, 2), 2) + " $" },
                new[] { "کلِ افغانی", Shamsi.Money(Math.Round(s.TotalAfn, 0)) },
                new[] { "پرداخت‌شده", Shamsi.Money(Math.Round(s.PaidAfn, 0)) },
                new[] { "الباقی", Shamsi.Money(Math.Round(s.AlbaqiAfn, 0)) },
            },
            FuelHead = { "تیل", "تن", "کلِ افغانی", "پرداخت‌شده", "الباقی" },
            Head = { "تاریخ", "نام", "تیل", "تن", "دالر", "نرخ", "پول" },
        };

        foreach (var (fuel, label) in new[] { (FuelType.Petrol, "پطرول"), (FuelType.Diesel, "دیزل") })
        {
            var part = all.Where(r => r.Fuel == fuel).ToList();
            // ⚠️ نرخِ تبدیل از **کلِ** شرکت می‌آید، نه از همین تکه: نرخ مالِ
            // شرکت است و اگر جدا حساب می‌شد، جمعِ دو تیل با کلِ افغانی
            // نمی‌خواند.
            var ps = calc.Summarize(company, part);
            book.Fuels.Add(new[]
            {
                label,
                Shamsi.Money(Math.Round(part.Sum(calc.Ton), 2), 2),
                Shamsi.Money(Math.Round(ps.TotalAfn, 0)),
                Shamsi.Money(Math.Round(ps.PaidAfn, 0)),
                Shamsi.Money(Math.Round(ps.AlbaqiAfn, 0)),
            });
        }

        var months = new Dictionary<string, decimal[]>();
        var order = new List<string>();

        foreach (var r in all)
        {
            book.Rows.Add(new[]
            {
                r.DateShamsi ?? "",
                r.Name ?? "",
                r.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                Shamsi.MoneyOrBlank(calc.Ton(r)),
                Shamsi.MoneyOrBlank(r.Usd),
                Shamsi.MoneyOrBlank(r.Rate),
                Shamsi.MoneyOrBlank(r.Poul),
            });

            var key = Shamsi.MonthKey(r.DateShamsi);
            if (key.Length == 0) continue;
            if (!months.TryGetValue(key, out var acc)) { months[key] = acc = new decimal[2]; order.Add(key); }
            acc[0] += calc.TotalAfn(r);
            acc[1] += calc.PaidAfn(r, s.ConvRate);
        }

        foreach (var key in order.OrderBy(k => k, StringComparer.Ordinal))
        {
            var acc = months[key];
            book.Archive.Add(new[]
            {
                key,
                Shamsi.Money(Math.Round(acc[0], 0)),
                Shamsi.Money(Math.Round(acc[1], 0)),
                Shamsi.Money(Math.Round(acc[0] - acc[1], 0)),
            });
        }

        var snap = new AcctSnapshot
        {
            Kind = "شرکت تیل",
            Name = name,
            Unit = "افغانی",
            Date = Shamsi.Today(),
        };
        snap.Books.Add(book);
        snap.Summary.AddRange(book.Summary);
        snap.Head.AddRange(book.Head);
        snap.Rows.AddRange(book.Rows);
        return snap;
    }
}
