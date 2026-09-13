using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ عکسِ زندهٔ ایستگاه — خوراکِ اپِ کارمندان و ربات ═════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «اپِ اندروید برای کارمندان تا قرض‌داران را چک کنند
/// که موجودی دارند یا نه — که اضافه ندهند — و موجودیِ تیل در مخزن را ببینند…
/// و یک رباتِ قدرتمند که هرچه از برنامه خواست سریع بدهد: هر شخص که اسم برد،
/// هر بخش، هر ماه و همهٔ بخش‌ها… و هر تغییری که در اپ می‌شود در ربات هم باشد.»
///
/// پس این کلاس یک <b>عکسِ کاملِ خواندنی</b> از برنامه می‌سازد و
/// <see cref="StationPublisher"/> آن را روی سرورِ خانگی می‌گذارد. ربات موتورِ
/// جست‌وجوی همین عکس است.
///
/// ══ سه قاعده که هرگز شکسته نمی‌شوند ════════════════════════════════════════
///
///  ۱) <b>هیچ منطقِ حسابی این‌جا نیست.</b> هر عددی که بیرون می‌رود از خودِ
///     سرویس‌های برنامه گرفته می‌شود (<see cref="DebtCalculationService"/>،
///     <see cref="SafeService"/>، <see cref="CompanyService"/> …). این‌جا فقط
///     «شکل» عوض می‌شود، نه عدد — وگرنه دو حقیقتِ جدا می‌ساختیم و همان چیزی
///     می‌شد که در کیو‌آر پیش آمد.
///
///  ۲) <b>دو دفترِ «واحد تیل» و «واحد پول» هرگز قاطی نمی‌شوند.</b>
///
///  ۳) <b>فقط خواندنی.</b> این عکس هیچ‌وقت به برنامه برنمی‌گردد؛ اپِ کارمندان
///     چیزی نمی‌نویسد. راهِ داده یک‌طرفه است: نیتیو ⟶ سرور ⟶ گوشی.
/// </summary>
public static class StationSnapshot
{
    /// <summary>شمارهٔ شکلِ داده — اگر روزی شکل عوض شد، گوشی بفهمد.</summary>
    public const int Version = 1;

    /// <summary>
    /// عکسِ تازه. ‎gate‎ هشِ رمزِ مدیر است (هرگز خودِ رمز) — قفلِ اپِ کارمندان.
    /// </summary>
    public static async Task<Dictionary<string, object?>> BuildAsync(
        AppHost host, CancellationToken ct = default)
    {
        var s = host.Settings;

        // یک‌بار سنجیده می‌شود و هم به خودِ عکس می‌رود و هم به سازندهٔ قرض‌داران
        var detailed = await host.Debtors.RowCountAsync(ct) <= RowBudget;

        var snap = new Dictionary<string, object?>
        {
            ["v"] = Version,
            // ⚠️ ‎_seq‎ باید همیشه بالا برود، وگرنه گیرنده عکسِ کهنه را قبول
            // می‌کند. میلی‌ثانیهٔ یونیکس همیشه صعودی است.
            ["seq"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["at"] = Shamsi.Today(),
            ["atUtc"] = DateTime.UtcNow.ToString("O"),
            ["gate"] = host.Auth.AdminPasswordHash() ?? "",
            ["station"] = new Dictionary<string, object?>
            {
                ["name"] = s.GetString(SettingsService.StationName),
                ["address"] = s.GetString(SettingsService.StationAddress),
                ["phone"] = s.GetString(SettingsService.StationPhone),
                ["ratePetrol"] = D(s.GetDecimal(SettingsService.UnionRatePetrol)),
                ["rateDiesel"] = D(s.GetDecimal(SettingsService.UnionRateDiesel)),
            },
            ["tank"] = await TankAsync(host, ct),
            ["debtors"] = await DebtorsAsync(host, detailed, ct),
            // ‎false‎ یعنی دفتر آن‌قدر بزرگ است که ردیف‌ها به گوشی برده نشدند —
            // فقط جمع‌ها. اپ همین را به کارمند می‌گوید، نه جدولِ خالی.
            ["detail"] = detailed,
            ["sections"] = await SectionsAsync(host, ct),
        };
        return snap;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  مخزن — «موجودیِ تیل در مخزن» که کارمند باید ببیند
    // ══════════════════════════════════════════════════════════════════════

    private static async Task<Dictionary<string, object?>> TankAsync(AppHost host, CancellationToken ct)
    {
        var threshold = host.Settings.GetDecimal(SettingsService.LowStockThreshold, 1000m);
        var tank = new Dictionary<string, object?>();
        foreach (var (key, fuel) in new[] { ("petrol", FuelType.Petrol), ("diesel", FuelType.Diesel) })
        {
            var purchases = await host.StorageData.PurchasesAsync(fuel, ct);
            var reports = await host.StorageData.ReportsAsync(fuel, ct);
            var dips = await host.StorageData.DipsAsync(fuel, ct);

            // ⚠️ همان تابعی که خودِ بخشِ مخزن و داشبورد از آن می‌خوانند
            var t = host.Storage.Tank(purchases, reports, threshold, dips);
            tank[key] = new Dictionary<string, object?>
            {
                ["in"] = D(t.In),
                ["out"] = D(t.Out),
                ["current"] = D(t.Current),
                ["show"] = D(t.Display),
                ["low"] = t.IsLow,
                ["near"] = t.IsNear,
                ["threshold"] = D(threshold),
            };
        }
        return tank;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  قرض‌داران — «موجودی دارند یا نه، که اضافه ندهند»
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ══ سقفِ ردیف‌هایی که به گوشی برده می‌شود ═══════════════════════════════
    ///
    /// ⚠️ این عدد یک تجمل نیست، ترمزِ همان چاله‌ای است که یک‌بار برنامه را
    /// خواباند: «همهٔ ردیف‌های همهٔ حساب‌ها را بخوان». با ده هزار قرض‌دار و یک
    /// میلیون ردیف، خواندنِ همه‌شان هر بیست ثانیه یعنی برنامهٔ روی کامپیوتر
    /// کند شود — درست همان چیزی که نباید بشود.
    ///
    /// پس دو حالت هست و هر دو کار می‌کنند:
    ///   • زیرِ سقف ⇒ هر حساب جدولِ کاملش را هم با خودش می‌برد.
    ///   • بالای سقف ⇒ فقط جمع‌ها و حال و الباقی می‌روند (که کارمند برای
    ///     «موجودی دارد یا نه» همان را لازم دارد) و اپ خودش می‌گوید که
    ///     ردیف‌ها در این عکس نیستند.
    ///
    /// در هر دو حالت، <b>حال و الباقیِ همهٔ قرض‌داران</b> می‌رود — چون همان
    /// چیزی است که جلوی «تیلِ اضافه دادن» را می‌گیرد. و در هر دو حالت،
    /// عددها با یک ‎GROUP BY‎ی خودِ SQLite درمی‌آیند، نه با خواندنِ ردیف‌ها.
    /// </summary>
    public const int RowBudget = 25_000;

    private static async Task<List<object?>> DebtorsAsync(AppHost host, bool detailed,
                                                         CancellationToken ct)
    {
        var calc = host.Debt;
        var people = new List<object?>();

        foreach (var noInv in new[] { false, true })
        {
            var list = await host.Debtors.ListAsync(noInv, null, ct);

            // ⚠️ یک کوئری برای همه — جمع‌ها را خودِ SQLite می‌زند و هیچ ردیفی
            // خوانده نمی‌شود. همان راهی که ‎CardAccountsAsync‎ برای کارت‌ها
            // ساخته شده بود.
            var cards = await host.Debtors.CardAccountsAsync(noInv, ct);

            foreach (var lite in list)
            {
                ct.ThrowIfCancellationRequested();

                var accounts = cards.TryGetValue(lite.Id, out var rolled)
                    ? rolled : new List<DebtAccount>();

                if (detailed)
                {
                    var full = await host.Debtors.LoadFullAsync(lite.Id, ct);
                    if (full is not null) accounts = full.AllAccounts().ToList();
                }
                if (accounts.Count == 0) continue;

                // ⚠️ حال و الباقی از خودِ سرویس — همان عددی که روی کارتِ
                // برنامه دیده می‌شود، نه یک حسابِ تازه.
                var st = calc.Status(accounts);
                var bal = calc.Balances(accounts);

                people.Add(new Dictionary<string, object?>
                {
                    ["id"] = lite.Id,
                    ["name"] = lite.Name,
                    ["phone"] = lite.Phone ?? "",
                    ["noinv"] = lite.IsNoInvoice,
                    ["status"] = StatusText(st.Worst),
                    ["stP"] = StatusText(st.Petrol),
                    ["stD"] = StatusText(st.Diesel),
                    ["stM"] = StatusText(st.Money),
                    ["bal"] = new Dictionary<string, object?>
                    {
                        ["money"] = D(bal.Money),
                        ["petrol"] = D(bal.Petrol),
                        ["diesel"] = D(bal.Diesel),
                    },
                    ["accounts"] = accounts.Select(a => Account(lite.Name, a, calc, detailed)).ToList(),
                });
            }
        }
        return people;
    }

    /// <summary>یک حساب — همان جدول و همان چهار عددِ سربرگ.</summary>
    /// <param name="withRows">
    /// ⚠️ در حالتِ خلاصه <b>حتماً</b> باید ‎false‎ باشد: ردیف‌هایی که
    /// ‎CardAccountsAsync‎ می‌سازد «ردیفِ جمع»اند نه ردیفِ واقعی، و نشان
    /// دادنشان یعنی جدولی که هیچ‌جای برنامه وجود ندارد. جمع‌هایشان اما
    /// دقیقاً درست است — کارت‌های خودِ برنامه هم از همان می‌خوانند.
    /// </param>
    private static Dictionary<string, object?> Account(string personName, DebtAccount a,
                                                       DebtCalculationService calc, bool withRows)
    {
        // ⚠️ عینِ کیو‌آر: همان ‎AcctSnapshots‎، تا عددِ گوشیِ کارمند و عددِ
        // کیو‌آرِ مشتری و عددِ خودِ برنامه هر سه یکی باشند.
        var snap = AcctSnapshots.ForDebtAccount(personName, a.IsMain ? null : a.Name, a, calc);
        return new Dictionary<string, object?>
        {
            ["id"] = a.Id,
            ["title"] = a.IsMain ? "" : (a.Name ?? ""),
            ["sub"] = a.LegacySubId ?? "",
            ["unit"] = a.Mode.IsMoney() ? "money" : "fuel",
            ["pctP"] = D(calc.PercentOf(a, FuelType.Petrol)),
            ["pctD"] = D(calc.PercentOf(a, FuelType.Diesel)),
            ["sum"] = snap.Summary,
            ["head"] = snap.Head,
            ["rows"] = withRows ? snap.Rows : new List<string[]>(),
        };
    }

    private static string StatusText(DebtStatus s) => s switch
    {
        DebtStatus.Out => "out",
        DebtStatus.Low => "low",
        DebtStatus.Ok => "ok",
        _ => "none",
    };

    // ══════════════════════════════════════════════════════════════════════
    //  بقیهٔ بخش‌ها — «هر بخش، هر ماه» که ربات باید جواب بدهد
    // ══════════════════════════════════════════════════════════════════════
    //
    //  هر بخش یک شکلِ واحد دارد تا ربات لازم نباشد برای هر کدام کدِ جدا
    //  داشته باشد:
    //      { t: عنوان, head: [ستون‌ها], rows: [[خانه‌ها]], m: [ماهِ هر ردیف],
    //        sum: [[برچسب, مقدار]] }
    //  ‎m[i]‎ ماهِ ردیفِ ‎i‎ است («1405/06») — با همین، ربات «ماه فلان» را
    //  فیلتر می‌کند بی این‌که تاریخ را دوباره بخواند.

    private static async Task<Dictionary<string, object?>> SectionsAsync(AppHost host, CancellationToken ct)
    {
        var out_ = new Dictionary<string, object?>();

        // ── گاوصندوق ────────────────────────────────────────────────────────
        var safe = await host.SafeLedger.ListAsync(null, ct);
        var safeSum = host.Safe.Summarize(safe);
        out_["safe"] = Section("گاوصندوق",
            new[] { "تاریخ", "نوع", "شرح", "مقدار", "واحد" },
            safe.Select(e => new[]
            {
                e.DateShamsi ?? "",
                e.Kind == SafeEntryKind.Bardagi ? "بردگی" : "ماندگی",
                e.Title ?? "",
                Shamsi.Money(e.Amount),
                e.Currency == Currency.Usd ? "دالر" : "افغانی",
            }),
            safe.Select(e => e.MonthKey ?? ""),
            new[]
            {
                new[] { "بردگی افغانی", Shamsi.Money(safeSum.Bardagi.Afn) },
                new[] { "ماندگی افغانی", Shamsi.Money(safeSum.Mandagi.Afn) },
                new[] { "خالص افغانی", Shamsi.Money(safeSum.Net.Afn) },
                new[] { "خالص دالر", Shamsi.Money(safeSum.Net.Usd) },
            });

        // ── صرافی ───────────────────────────────────────────────────────────
        var ex = await host.ExchangeLedger.ListAsync(null, ct);
        var exSum = host.Exchange.Summarize(ex);
        out_["sarrafi"] = Section("صرافی",
            new[] { "تاریخ", "شرح", "مبلغ", "واحد", "فی", "دالر", "بردگی", "باقی" },
            ex.Select(r => new[]
            {
                r.DateShamsi ?? "", r.Description ?? "", Shamsi.Money(r.Amount),
                r.Currency switch
                {
                    ExchangeCurrency.Afghani => "افغانی",
                    ExchangeCurrency.Kaldar => "کلدار",
                    _ => "تومان",
                },
                Shamsi.Money(r.Rate), Shamsi.Money(host.Exchange.ToUsd(r)),
                Shamsi.Money(r.Bardagi), Shamsi.Money(host.Exchange.RowBaqi(r)),
            }),
            ex.Select(r => r.MonthKey ?? ""),
            new[]
            {
                new[] { "جمله دالر", Shamsi.Money(exSum.TotalUsd) },
                new[] { "جمله بردگی", Shamsi.Money(exSum.TotalBardagi) },
                new[] { "باقی", Shamsi.Money(exSum.Baqi) },
            });

        // ── مصارف ───────────────────────────────────────────────────────────
        var exp = await host.ExpenseLedger.ListAsync(null, ct);
        out_["expense"] = Section("مصارف",
            new[] { "تاریخ", "عنوان", "مقدار", "یادداشت" },
            exp.Select(r => new[]
            {
                r.DateShamsi ?? "", r.Title ?? "", Shamsi.Money(r.Amount), r.Note ?? "",
            }),
            exp.Select(r => r.MonthKey ?? ""),
            new[] { new[] { "جمله مصارف", Shamsi.Money(host.Expenses.Total(exp)) } });

        // ── چکنه ────────────────────────────────────────────────────────────
        var ret = await host.RetailLedger.ListAsync(null, ct);
        var retSum = host.Retail.Summarize(ret);
        out_["chakana"] = Section("چکنه",
            new[] { "تاریخ", "نام", "تیل", "مقدار", "فی", "بردگی", "رسید", "الباقی" },
            ret.Select(r => new[]
            {
                r.DateShamsi ?? "", r.Name ?? "",
                r.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                Shamsi.MoneyOrBlank(r.Liters), Shamsi.MoneyOrBlank(r.PricePerLiter),
                Shamsi.Money(host.Retail.Bardagi(r)), Shamsi.MoneyOrBlank(r.Rasid),
                Shamsi.Money(host.Retail.Albaqi(r)),
            }),
            ret.Select(r => r.MonthKey ?? ""),
            new[]
            {
                new[] { "جمله لیتر", Shamsi.Money(retSum.Liters) },
                new[] { "جمله بردگی", Shamsi.Money(retSum.Bardagi) },
                new[] { "جمله رسید", Shamsi.Money(retSum.Rasid) },
                new[] { "الباقی", Shamsi.Money(retSum.Albaqi) },
            });

        // ── عایدات اضافی ────────────────────────────────────────────────────
        var extra = await host.ExtraIncomeLedger.ListAsync(null, ct);
        out_["extraincome"] = Section("عایدات",
            new[] { "تاریخ", "فروشنده", "مقدار", "خرید", "بازار", "مبلغ" },
            extra.Select(r => new[]
            {
                r.DateShamsi ?? "", r.Seller ?? "", Shamsi.MoneyOrBlank(r.Qty),
                Shamsi.MoneyOrBlank(r.Buy), Shamsi.MoneyOrBlank(r.Market), Shamsi.Money(r.Amount),
            }),
            extra.Select(r => r.MonthKey ?? ""),
            new[] { new[] { "جمله", Shamsi.Money(extra.Sum(r => r.Amount)) } });

        // ── شرکت‌های تیل ────────────────────────────────────────────────────
        out_["company"] = await CompaniesAsync(host, ct);

        // ── امانت ───────────────────────────────────────────────────────────
        out_["amanat"] = await AmanatAsync(host, ct);

        // ── فاکتورها ────────────────────────────────────────────────────────
        var invoices = await host.Invoices.ListAsync(null, null, ct);
        out_["invoice"] = Section("فاکتورها",
            new[] { "شماره", "تاریخ", "مشتری", "تیل", "مقدار", "فی", "مبلغ", "حال" },
            invoices.Select(v => new[]
            {
                Shamsi.Money(v.InvoiceNumber), v.DateShamsi ?? "", v.CustomerName ?? "",
                v.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                Shamsi.MoneyOrBlank(v.Liters), Shamsi.MoneyOrBlank(v.PricePerLiter),
                Shamsi.Money(v.Amount),
                v.Status == InvoiceStatus.Approved ? "تایید شده" : "در انتظار",
            }),
            invoices.Select(v => MonthOf(v.DateShamsi)),
            new[]
            {
                new[] { "شمارِ فاکتور", Shamsi.Money(invoices.Count) },
                new[] { "جمله مبلغ", Shamsi.Money(invoices.Sum(v => v.Amount)) },
            });

        // ── خریدهای مخزن ────────────────────────────────────────────────────
        var buys = new List<FuelPurchase>();
        buys.AddRange(await host.StorageData.PurchasesAsync(FuelType.Petrol, ct));
        buys.AddRange(await host.StorageData.PurchasesAsync(FuelType.Diesel, ct));
        out_["storage"] = Section("خریدِ تیل",
            new[] { "تاریخ", "تیل", "فروشنده", "کیلو", "لیتر", "فی لیتر", "جمله افغانی" },
            buys.Select(p => new[]
            {
                p.DateShamsi ?? "", p.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                p.Seller ?? "", Shamsi.Money(p.Kg), Shamsi.Money(p.Liters),
                Shamsi.Money(p.PerLiter), Shamsi.Money(p.TotalAfn),
            }),
            buys.Select(p => MonthOf(p.DateShamsi)),
            new[]
            {
                new[] { "جمله لیتر", Shamsi.Money(buys.Sum(p => p.Liters)) },
                new[] { "جمله افغانی", Shamsi.Money(buys.Sum(p => p.TotalAfn)) },
            });

        // ── کارمندان ────────────────────────────────────────────────────────
        var staff = await host.Attendance.StaffAsync(ct);
        out_["staff"] = Section("کارمندان",
            new[] { "نام", "معاش", "شروع", "پایان", "یادداشت" },
            staff.Select(m => new[]
            {
                m.Name ?? "", Shamsi.Money(m.Salary),
                m.ShiftIn ?? "", m.ShiftOut ?? "", m.Note ?? "",
            }),
            staff.Select(_ => ""),
            new[] { new[] { "شمارِ کارمند", Shamsi.Money(staff.Count) } });

        return out_;
    }

    private static async Task<Dictionary<string, object?>> CompaniesAsync(AppHost host, CancellationToken ct)
    {
        var rows = new List<string[]>();
        var months = new List<string>();
        decimal albaqiAfn = 0, albaqiUsd = 0;

        // ⚠️ ‎ListAsync‎ خودش ردیف‌ها را با ‎Include‎ می‌آورد؛ خواندنِ دوبارهٔ
        // هر شرکت فقط یک رفت‌وبرگشتِ اضافه به دیتابیس بود.
        foreach (var c in await host.Companies.ListAsync(ct))
        {
            foreach (var fuel in new[] { FuelType.Petrol, FuelType.Diesel })
            {
                var list = CompanyService.RowsOf(c, fuel).ToList();
                if (list.Count == 0) continue;
                var sum = host.Company.Summarize(c, list);
                albaqiAfn += sum.AlbaqiAfn;
                albaqiUsd += sum.AlbaqiUsd;
                rows.Add(new[]
                {
                    c.Name ?? "", fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                    Shamsi.Money(sum.TotalUsd), Shamsi.Money(sum.TotalAfn),
                    Shamsi.Money(sum.PaidAfn), Shamsi.Money(sum.AlbaqiAfn),
                    Shamsi.Money(sum.AlbaqiUsd),
                });
                months.Add("");
            }
        }

        return Section("شرکت‌های تیل",
            new[] { "شرکت", "تیل", "جمله دالر", "جمله افغانی", "پرداخت", "الباقی افغانی", "الباقی دالر" },
            rows, months,
            new[]
            {
                new[] { "الباقیِ همه (افغانی)", Shamsi.Money(albaqiAfn) },
                new[] { "الباقیِ همه (دالر)", Shamsi.Money(albaqiUsd) },
            });
    }

    private static async Task<Dictionary<string, object?>> AmanatAsync(AppHost host, CancellationToken ct)
    {
        var rows = new List<string[]>();
        var months = new List<string>();
        foreach (var fuel in new[] { FuelType.Petrol, FuelType.Diesel })
            foreach (var a in await host.Amanat.ListAsync(fuel, ct))
                foreach (var r in a.Rows.OrderBy(x => x.SortIndex).ThenBy(x => x.Id))
                {
                    rows.Add(new[]
                    {
                        a.Name ?? "", fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                        r.DateShamsi ?? "", r.Name ?? "",
                        Shamsi.MoneyOrBlank(r.Liters ?? 0m), Shamsi.MoneyOrBlank(r.Taken ?? 0m),
                        r.CloseDate ?? "",
                    });
                    months.Add(MonthOf(r.DateShamsi));
                }

        return Section("امانت",
            new[] { "حساب", "تیل", "تاریخ", "نام", "مقدار", "برداشت", "بستن" },
            rows, months,
            new[] { new[] { "شمارِ ردیف", Shamsi.Money(rows.Count) } });
    }

    // ── ابزارِ کوچک ────────────────────────────────────────────────────────

    private static Dictionary<string, object?> Section(
        string title, IEnumerable<string> head, IEnumerable<string[]> rows,
        IEnumerable<string> months, IEnumerable<string[]> totals) =>
        new()
        {
            ["t"] = title,
            ["head"] = head.ToList(),
            ["rows"] = rows.ToList(),
            ["m"] = months.ToList(),
            ["sum"] = totals.ToList(),
        };

    /// <summary>«1405/06/22» ⇒ «1405/06». خالی یعنی تاریخ نداشت.</summary>
    public static string MonthOf(string? dateShamsi)
    {
        var d = (dateShamsi ?? "").Trim();
        var cut = d.LastIndexOf('/');
        return cut > 0 && d.Count(c => c == '/') >= 2 ? d[..cut] : "";
    }

    /// <summary>
    /// ⚠️ ‎decimal‎ در JSON به ‎double‎ می‌رود و رقمِ پول را می‌شکند. عددها
    /// این‌جا با دو رقمِ اعشار گرد و به ‎double‎ی امن تبدیل می‌شوند — همان
    /// دقتی که خودِ صفحه نشان می‌دهد. عددهای «نمایشی» جدا از این، رشته‌اند.
    /// </summary>
    private static double D(decimal v) => (double)Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
