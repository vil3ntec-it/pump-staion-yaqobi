using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>یک ردیفِ تاریخچه — رونوشتِ همان شیءی که ‎_hcItem‎ می‌ساخت.</summary>
/// <param name="Kind">کدام دفتر: ‎safe‎، ‎debt‎، ‎waraq‎…</param>
/// <param name="DateShamsi">تاریخِ خودِ رکورد، همان‌طور که نوشته شده.</param>
/// <param name="DateKey">کلیدِ عددیِ تاریخ — برای مرتب‌سازی و فیلترِ ماه.</param>
/// <param name="Amount">‎null‎ یعنی این ردیف عدد ندارد (ورق، حاضری).</param>
/// <param name="Tone">‎in‎ سبز · ‎out‎ نارنجی · خالی، بی‌رنگ.</param>
/// <param name="Cells">
/// خانه‌های جدولِ همان بخش، به ترتیبِ <see cref="HistoryService.ColumnsOf"/> —
/// ‎null‎ یعنی این بخش جدولِ کلی (تاریخ · چه بود · توضیح · مبلغ) را دارد.
/// </param>
public sealed record HistoryRow(
    string Kind, string DateShamsi, int DateKey,
    string Title, string Detail, decimal? Amount, string Unit, string Tone,
    IReadOnlyList<string>? Cells = null)
{
    public string AmountText => Amount is null ? "" : Shamsi.Money(Amount.Value) + Unit;

    /// <summary>«1405/06» — گروهِ ماهِ همین ردیف؛ خالی یعنی بی‌تاریخ.</summary>
    public string MonthKey => DateKey <= 0 ? "" : $"{DateKey / 10000:0000}/{DateKey / 100 % 100:00}";
}

/// <summary>
/// یک ستونِ جدولِ تاریخچهٔ یک بخش.
/// </summary>
/// <param name="Wide">پهنای ستاره‌ای (نام، توضیح) — بقیه هم‌قدِ محتوا.</param>
/// <param name="Brush">
/// رنگِ نوشته: کلیدِ منبعِ تم (‎Pump.Ok‎…)، یا ‎"tone"‎ یعنی رنگِ «آمد/رفت»ِ
/// همان ردیف، یا خالی یعنی رنگِ معمولی.
/// </param>
public sealed record HistoryCol(string Header, bool Wide = false, string Brush = "");

/// <summary>یک کارتِ صفحهٔ «تاریخچه‌ها».</summary>
public sealed record HistoryKind(string Key, string Label, int Count, string LatestDate);

/// <summary>
/// ══ تاریخچه‌ها ═════════════════════════════════════════════════════════════
/// رونوشتِ ‎HC_KINDS‎ · ‎_hcFeed‎ · ‎renderHistoryCenter‎ · ‎renderHistoryView‎.
///
/// یک دفترچهٔ **فقط‌خواندنی**: هر بخش تاریخچهٔ خودش را دارد، از تازه به کهنه،
/// و هیچ چیزی این‌جا عوض یا پاک نمی‌شود. کارش این است که کاربر بتواند بپرسد
/// «این ماه در گاوصندوق چه گذشت؟» بی آنکه لای جدول‌های کاری بگردد.
///
/// ⚠️ این‌جا هیچ محاسبهٔ تازه‌ای اختراع نمی‌شود: بردگیِ صرافی، بردگیِ چکنه،
/// دالرِ ردیفِ شرکت و بخارِ تیلِ امانت همه از همان سرویس‌های محاسباتیِ خودِ
/// برنامه می‌آیند. اگر روزی یکی از آن فرمول‌ها عوض شود، تاریخچه هم با آن
/// عوض می‌شود — نه اینکه عددِ دومی و متفاوتی نشان بدهد.
/// </summary>
public sealed class HistoryService
{
    /// <summary>ترتیب و برچسبِ کارت‌ها — مو‌به‌مو مثلِ ‎HC_KINDS‎.</summary>
    public static readonly (string Key, string Label)[] Kinds =
    {
        ("safe",    "🏦 گاوصندوق"),
        ("amanat",  "🛢️ تیل امانت"),
        ("sarrafi", "💱 صرافی"),
        ("storage", "🛢️ مخزن"),
        ("debt",    "👥 قرض‌داران"),
        ("rasid",   "🧾 رسیدهای قرض‌داران"),
        ("chakana", "🧾 چکنه"),
        ("company", "🏭 شرکت‌های تیل"),
        ("expense", "💸 مصارف"),
        ("waraq",   "📝 ورق‌ها"),
        ("invoice", "🧾 فاکتورها"),
        ("shift",   "📋 پارچه‌ها"),
        ("attend",  "🕒 حاضری"),
    };

    /// <summary>
    /// ══ ستون‌های هر بخش (۱۴۰۵/۰۷/۱۲) ═════════════════════════════════════════
    ///
    /// خواستهٔ صاحب ریپو: تاریخچهٔ هر بخش همان چیزهایی را جدا جدا نشان بدهد که
    /// آن بخش واقعاً دارد — نه یک ستونِ «توضیح» که همه‌چیز در آن ریخته شده:
    ///   پارچه‌ها ⇐ کارمند · روز/شب · شمارهٔ پایه · شروع · ختم
    ///   ورق‌ها   ⇐ جملهٔ هر شیفت: شمار و مبلغِ قرض، شمار و مبلغِ مصرف، فروش
    ///   صرافی    ⇐ تحویل به صرافی یا بردگیِ پمپ · مبلغ · فی · دالر
    ///   شرکت‌ها  ⇐ بردگیِ پمپ از شرکت (تیل، دالر) و رسیدِ پمپ = بردگیِ شرکت
    /// بقیهٔ بخش‌ها همان جدولِ کلی را دارند (‎null‎).
    ///
    /// ⛔ **هیچ عددِ تازه‌ای این‌جا ساخته نمی‌شود**: هر خانه از همان سرویسِ
    /// محاسباتیِ خودِ بخش می‌آید (‎ExchangeService.ToUsd‎ · ‎CompanyService‎ ·
    /// ‎WaraqService.ShiftTotals‎) — همان عددی که صفحهٔ خودِ بخش نشان می‌دهد.
    /// </summary>
    public static IReadOnlyList<HistoryCol>? ColumnsOf(string kind) => kind switch
    {
        "shift" => new HistoryCol[]
        {
            new("تاریخ"), new("پارچه"), new("تیل"), new("شیفت"), new("کارمند", Wide: true),
            new("شمارهٔ پایه"), new("شروعِ پایه"), new("ختمِ پایه"),
            new("لیتر"), new("پول", Brush: "Pump.Ok"),
        },
        "waraq" => new HistoryCol[]
        {
            new("تاریخ"), new("شیفت"), new("کارمندان", Wide: true),
            new("شمارِ قرض"), new("جملهٔ قرض", Brush: "Pump.Danger"),
            new("شمارِ مصرف"), new("جملهٔ مصرف", Brush: "Pump.Purple"),
            new("لیترِ فروش"), new("جملهٔ فروش", Brush: "Pump.Ok"),
        },
        "sarrafi" => new HistoryCol[]
        {
            new("تاریخ"), new("چه شد"), new("توضیح", Wide: true),
            new("مبلغ"), new("واحد"), new("فی"), new("دالر", Brush: "tone"),
        },
        "company" => new HistoryCol[]
        {
            new("تاریخ"), new("شرکت"), new("تیل"), new("شرح", Wide: true), new("تن"),
            new("بردگیِ پمپ از شرکت ($)", Brush: "Pump.Accent"), new("به افغانی"),
            new("رسیدِ پمپ = بردگیِ شرکت", Brush: "Pump.Ok"), new("از کجا"),
        },
        _ => null,
    };

    public static string LabelOf(string key)
    {
        foreach (var (k, l) in Kinds) if (k == key) return l;
        return "🕘 تاریخچه";
    }

    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly ExchangeService _exchange;
    private readonly RetailService _retail;
    private readonly CompanyService _company;
    private readonly AmanatService _amanat;
    private readonly AmanatDataService _amanatData;
    private readonly WaraqService _waraq;

    public HistoryService(PumpDbFactory dbf, PermissionService perm,
                          ExchangeService exchange, RetailService retail,
                          CompanyService company, AmanatService amanat,
                          AmanatDataService amanatData, WaraqService? waraq = null)
    {
        _dbf = dbf; _perm = perm; _exchange = exchange; _retail = retail;
        _company = company; _amanat = amanat; _amanatData = amanatData;
        _waraq = waraq ?? new WaraqService();
    }

    /// <summary>
    /// شمار و تازه‌ترین تاریخِ هر بخش — خوراکِ کارت‌های صفحهٔ اول.
    ///
    /// ⚠️ این‌جا ‎FeedAsync‎ صدا زده **نمی‌شود**: آن یکی هر ردیفِ هر دفتر را با
    /// همهٔ ستون‌ها و ‎Include‎ها و متن‌سازی می‌خواند و با پنج سال داده ۳٫۵ ثانیه
    /// طول می‌کشید — فقط برای این‌که سیزده عدد روی کارت بنشیند. هر بخش این‌جا
    /// تنها ستون‌هایی را می‌خواند که قاعدهٔ «این ردیف شمرده می‌شود؟»ِ همان
    /// دفتر به آن نیاز دارد، پس عددِ کارت با شمارِ ردیف‌های ‎FeedAsync‎ یکی
    /// می‌ماند (آزمونِ ‎TheCardsMatchTheFeed‎).
    /// </summary>
    public async Task<List<HistoryKind>> CardsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();

        var all = new List<HistoryKind>(Kinds.Length);
        foreach (var (key, label) in Kinds)
        {
            var stamps = key switch
            {
                "safe"    => await SafeStampsAsync(db, ct),
                "amanat"  => await AmanatStampsAsync(db, ct),
                "sarrafi" => await ExchangeStampsAsync(db, ct),
                "storage" => await Stamps(db.FuelPurchases.AsNoTracking().Select(e => new Stamp(e.DateShamsi, e.DateKey)), ct),
                "debt"    => await DebtStampsAsync(db, ct),
                "rasid"   => await Stamps(db.DebtQuickReceipts.AsNoTracking()
                                 .Where(r => r.Amount != 0m || (r.Account != null && r.Account.Trim() != ""))
                                 .Select(r => new Stamp(r.DateShamsi, r.DateKey)), ct),
                "chakana" => await RetailStampsAsync(db, ct),
                "company" => await CompanyStampsAsync(db, ct),
                "expense" => await Stamps(db.Expenses.AsNoTracking().Select(e => new Stamp(e.DateShamsi, e.DateKey)), ct),
                "waraq"   => await Stamps(db.WaraqShifts.AsNoTracking()
                                 .Where(s => s.Pumps.Any() || s.Transactions.Any(t => t.Name != null && t.Name.Trim() != ""))
                                 .Select(s => new Stamp(s.Waraq!.DateShamsi, s.Waraq!.DateKey)), ct),
                "invoice" => await Stamps(db.Invoices.AsNoTracking().Select(v => new Stamp(v.DateShamsi, v.DateKey)), ct),
                "shift"   => await ShiftStampsAsync(db, ct),
                "attend"  => await Stamps(db.Attendance.AsNoTracking().Select(a => new Stamp(a.DateShamsi, a.DateKey)), ct),
                _         => new List<Stamp>(),
            };
            all.Add(Card(key, label, stamps));
        }
        return all;
    }

    /// <summary>تاریخِ یک ردیفِ شمرده‌شده — تنها چیزی که کارت لازم دارد.</summary>
    private readonly record struct Stamp(string? DateShamsi, int DateKey);

    private static async Task<List<Stamp>> Stamps(IQueryable<Stamp> q, CancellationToken ct)
        => await q.ToListAsync(ct);

    /// <summary>همان «تازه به کهنه»ِ ‎FeedAsync‎: تازه‌ترین تاریخ، اولین ردیف با بزرگ‌ترین کلید.</summary>
    private static HistoryKind Card(string key, string label, List<Stamp> stamps)
    {
        if (stamps.Count == 0) return new HistoryKind(key, label, 0, "");
        var best = stamps[0];
        foreach (var s in stamps) if (s.DateKey > best.DateKey) best = s;
        return new HistoryKind(key, label, stamps.Count, best.DateShamsi ?? "");
    }

    private static Task<List<Stamp>> SafeStampsAsync(Persistence.PumpDbContext db, CancellationToken ct)
        => Stamps(db.SafeEntries.AsNoTracking()
            .Where(e => e.Amount != 0m || (e.Title != null && e.Title.Trim() != ""))
            .Select(e => new Stamp(e.DateShamsi, e.DateKey)), ct);

    /// <summary>‎RowCalc‎ لیتر را ‎Max(0, Liters)‎ می‌گیرد و صفر را رد می‌کند ⇒ فقط لیترِ مثبت.</summary>
    private static Task<List<Stamp>> AmanatStampsAsync(Persistence.PumpDbContext db, CancellationToken ct)
        => Stamps(db.AmanatRows.AsNoTracking()
            .Where(r => r.Liters != null && r.Liters > 0m)
            .Select(r => new Stamp(r.DateShamsi, r.DateKey)), ct);

    private async Task<List<Stamp>> ExchangeStampsAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var rows = await db.ExchangeRows.AsNoTracking()
            .Select(r => new { r.DateShamsi, r.DateKey, r.Amount, r.Rate, r.Bardagi, r.Description })
            .ToListAsync(ct);
        var list = new List<Stamp>(rows.Count);
        foreach (var r in rows)
        {
            var usd = _exchange.ToUsd(new ExchangeRow { Amount = r.Amount, Rate = r.Rate });
            if (usd == 0m && r.Bardagi == 0m && r.Amount == 0m && string.IsNullOrWhiteSpace(r.Description)) continue;
            var st = new Stamp(r.DateShamsi, r.DateKey);
            if (usd != 0m) list.Add(st);
            if (r.Bardagi != 0m) list.Add(st);
            if (usd == 0m && r.Bardagi == 0m && r.Amount != 0m) list.Add(st);
        }
        return list;
    }

    /// <summary>ضربِ اعشاری را به SQLite نمی‌دهیم (مبلغ‌ها متن‌اند)؛ همان قاعدهٔ ‎SumAsync‎.</summary>
    private static async Task<List<Stamp>> DebtStampsAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var rows = await db.DebtRows.AsNoTracking()
            .Select(r => new { r.DateShamsi, r.DateKey, r.ByMoney, r.Bardagi, r.Liters, r.PricePerLiter, r.Rasid })
            .ToListAsync(ct);
        var list = new List<Stamp>(rows.Count);
        foreach (var r in rows)
        {
            var bardagi = r.ByMoney ? r.Bardagi : r.Liters * (r.PricePerLiter ?? 0m);
            if (bardagi == 0m && r.Rasid == 0m && r.Liters == 0m) continue;
            list.Add(new Stamp(r.DateShamsi, r.DateKey));
        }
        return list;
    }

    private async Task<List<Stamp>> RetailStampsAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var rows = await db.RetailRows.AsNoTracking()
            .Select(e => new { e.DateShamsi, e.DateKey, e.ByMoney, e.Bardagi, e.Liters, e.PricePerLiter, e.Rasid, e.Name })
            .ToListAsync(ct);
        var list = new List<Stamp>(rows.Count);
        foreach (var e in rows)
        {
            var bord = _retail.Bardagi(new RetailRow
                { ByMoney = e.ByMoney, Bardagi = e.Bardagi, Liters = e.Liters, PricePerLiter = e.PricePerLiter });
            if (bord == 0m && e.Rasid == 0m && e.Liters == 0m && string.IsNullOrWhiteSpace(e.Name)) continue;
            list.Add(new Stamp(e.DateShamsi, e.DateKey));
        }
        return list;
    }

    private async Task<List<Stamp>> CompanyStampsAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var rows = await db.CompanyRows.AsNoTracking()
            .Select(r => new { r.DateShamsi, r.DateKey, r.Ton, r.Kg, r.Usd, r.Poul })
            .ToListAsync(ct);
        var list = new List<Stamp>(rows.Count);
        foreach (var r in rows)
        {
            var usd = _company.TotalUsd(new CompanyRow { Ton = r.Ton, Kg = r.Kg, Usd = r.Usd });
            if (usd == 0m && r.Poul == 0m) continue;
            list.Add(new Stamp(r.DateShamsi, r.DateKey));
        }
        return list;
    }

    /// <summary>هر گزارش تا دو ردیف: شیفتِ روز و شیفتِ شب، اگر باشند.</summary>
    private static async Task<List<Stamp>> ShiftStampsAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var rows = await db.Reports.AsNoTracking()
            .Select(r => new { r.DateShamsi, r.DateKey, Day = r.DayShiftId != null, Night = r.NightShiftId != null })
            .ToListAsync(ct);
        var list = new List<Stamp>(rows.Count * 2);
        foreach (var r in rows)
        {
            if (r.Day) list.Add(new Stamp(r.DateShamsi, r.DateKey));
            if (r.Night) list.Add(new Stamp(r.DateShamsi, r.DateKey));
        }
        return list;
    }

    /// <summary>
    /// ردیف‌های یک بخش، از تازه به کهنه. بی‌تاریخ‌ها ته فهرست — همان ترتیبِ
    /// ‎out.sort‎ی نسخهٔ وب.
    /// </summary>
    public async Task<List<HistoryRow>> FeedAsync(string kind, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();

        var rows = kind switch
        {
            "safe"    => await SafeAsync(db, ct),
            "amanat"  => await AmanatAsync(db, ct),
            "sarrafi" => await ExchangeAsync(db, ct),
            "storage" => await StorageAsync(db, ct),
            "debt"    => await DebtAsync(db, ct),
            "rasid"   => await ReceiptsAsync(db, ct),
            "chakana" => await RetailAsync(db, ct),
            "company" => await CompanyAsync(db, ct),
            "expense" => await ExpenseAsync(db, ct),
            "waraq"   => await WaraqAsync(db, ct),
            "invoice" => await InvoiceAsync(db, ct),
            "shift"   => await ShiftAsync(db, ct),
            "attend"  => await AttendanceAsync(db, ct),
            _         => new List<HistoryRow>(),
        };

        return rows.OrderByDescending(r => r.DateKey).ToList();
    }

    // ══ هر دفتر ════════════════════════════════════════════════════════════

    private static async Task<List<HistoryRow>> SafeAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var list = new List<HistoryRow>();
        foreach (var e in await db.SafeEntries.AsNoTracking().ToListAsync(ct))
        {
            if (e.Amount == 0m && string.IsNullOrWhiteSpace(e.Title)) continue;
            // «بردگی» یعنی پول به گاوصندوق آمده — همان ‎isIn‎ی نسخهٔ وب.
            var isIn = e.Kind == SafeEntryKind.Bardagi;
            list.Add(new HistoryRow("safe", e.DateShamsi ?? "", e.DateKey,
                string.IsNullOrWhiteSpace(e.Title) ? (isIn ? "بردگی" : "ماندگی") : e.Title!,
                (isIn ? "⬅️ بردگی" : "➡️ ماندگی") + (string.IsNullOrWhiteSpace(e.Note) ? "" : " · " + e.Note),
                Math.Round(e.Amount), e.Currency == Currency.Usd ? " $" : " افغانی",
                isIn ? "in" : "out"));
        }
        return list;
    }

    private async Task<List<HistoryRow>> AmanatAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var list = new List<HistoryRow>();
        var settings = _amanatData.Settings();
        var accounts = await db.AmanatAccounts.AsNoTracking().Include(a => a.Rows).ToListAsync(ct);

        foreach (var acc in accounts)
        {
            var who = string.IsNullOrWhiteSpace(acc.Name) ? "حسابِ بی‌نام" : acc.Name!.Trim();
            foreach (var r in acc.Rows)
            {
                var days = AmanatService.AutoDays(
                    DateOf(r.DateShamsi),
                    r.State == AmanatRowState.Closed ? DateOf(r.CloseDate) : null,
                    DateTime.Now);
                var c = _amanat.RowCalc(r, acc, settings, days);
                if (c.Liters == 0m) continue;

                var bits = new List<string>
                {
                    "⬅️ رسید تیل امانت",
                    acc.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول",
                };
                if (!string.IsNullOrWhiteSpace(r.Name)) bits.Add(r.Name!);
                if (!string.IsNullOrWhiteSpace(r.ToAccount)) bits.Add("به حسابهٔ " + r.ToAccount);
                bits.Add(Shamsi.Money(c.Days) + " روز");
                bits.Add(Shamsi.Money(c.Temp, 1) + "°C");
                bits.Add(c.Closed ? "بسته" : "در مخزن (باز)");
                if (c.Taken != 0m) bits.Add("برده شده " + Shamsi.Money(c.Taken, 2) + " لیتر");
                bits.Add("کمبودی " + Shamsi.Money(c.LossPct, 3) + "٪ (" + Shamsi.Money(c.Loss, 2) + " لیتر)");
                bits.Add("باقی " + Shamsi.Money(c.Rest, 2) + " لیتر");

                list.Add(new HistoryRow("amanat", r.DateShamsi ?? "", r.DateKey,
                    who + " — رسید تیل امانت", string.Join(" · ", bits),
                    Math.Round(c.Liters, 2), " لیتر", "in"));
            }
        }
        return list;
    }

    private async Task<List<HistoryRow>> ExchangeAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var list = new List<HistoryRow>();
        foreach (var r in await db.ExchangeRows.AsNoTracking().ToListAsync(ct))
        {
            var usd = _exchange.ToUsd(r);
            if (usd == 0m && r.Bardagi == 0m && r.Amount == 0m
                && string.IsNullOrWhiteSpace(r.Description)) continue;

            var title = string.IsNullOrWhiteSpace(r.Description) ? "صرافی" : r.Description!;
            var note = string.IsNullOrWhiteSpace(r.Description) ? "—" : r.Description!.Trim();
            var cur = r.Currency switch
            {
                ExchangeCurrency.Kaldar => "کالدار",
                ExchangeCurrency.Afghani => "افغانی",
                _ => "تومان",
            };

            // ══ یک سطرِ صرافی می‌تواند دو ردیفِ تاریخچه بدهد ══════════════
            //
            // گزارش‌های صاحب ریپو (۱۴۰۵/۰۷/۰۶ و ۱۴۰۵/۰۷/۱۲): «نشان بده تحویل شده
            // یا گرفته، مبلغ چقدر بوده، فی چقدر و چند دالر شده بود — و بردگیِ
            // پمپ یا تحویل به صرافی‌ها هم نوشته باشد، هر چه شده بود دقیق.»
            //
            // همان دو ستونِ خودِ صفحهٔ صرافی: «رسید به صرافی» (مبلغ ÷ فی = دالر)
            // و «بردگیِ پمپ بنزین ($)». ⛔ هیچ عددی عوض نشد — همان ‎ToUsd‎ و
            // همان ‎Bardagi‎.
            if (usd != 0m)
                list.Add(new HistoryRow("sarrafi", r.DateShamsi ?? "", r.DateKey,
                    "💵 تحویل به صرافی — " + title,
                    Shamsi.Money(r.Amount) + " " + cur
                        + (r.Rate != 0m ? " · فی " + Shamsi.Money(r.Rate) : "")
                        + " ⇐ " + Shamsi.Money(Math.Round(usd, 2)) + " $",
                    Math.Round(usd, 2), " $", "in",
                    new[]
                    {
                        Date(r.DateShamsi), "💵 تحویل به صرافی", note,
                        Shamsi.Money(r.Amount), cur, Shamsi.Money(r.Rate),
                        Shamsi.Money(Math.Round(usd, 2)) + " $",
                    }));

            if (r.Bardagi != 0m)
                list.Add(new HistoryRow("sarrafi", r.DateShamsi ?? "", r.DateKey,
                    "📤 بردگیِ پمپ از صرافی — " + title,
                    "پمپ از صرافی برد", r.Bardagi, " $", "out",
                    new[]
                    {
                        Date(r.DateShamsi), "📤 بردگیِ پمپ از صرافی", note,
                        "—", "دالر", "—", Shamsi.Money(r.Bardagi) + " $",
                    }));

            if (usd == 0m && r.Bardagi == 0m && r.Amount != 0m)
                list.Add(new HistoryRow("sarrafi", r.DateShamsi ?? "", r.DateKey,
                    "💵 تحویل به صرافی — " + title,
                    "فی نوشته نشده، پس معادلِ دالری حساب نشد", r.Amount, " " + cur, "",
                    new[]
                    {
                        Date(r.DateShamsi), "💵 تحویل به صرافی", note,
                        Shamsi.Money(r.Amount), cur, "فی نوشته نشده", "—",
                    }));
        }
        return list;
    }

    private static async Task<List<HistoryRow>> StorageAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var list = new List<HistoryRow>();
        foreach (var e in await db.FuelPurchases.AsNoTracking().ToListAsync(ct))
            list.Add(new HistoryRow("storage", e.DateShamsi ?? "", e.DateKey,
                (e.Fuel == FuelType.Diesel ? "🟤 خرید دیزل" : "⛽ خرید پطرول")
                    + (string.IsNullOrWhiteSpace(e.Seller) ? "" : " — " + e.Seller),
                Shamsi.Money(Math.Round(e.Liters)) + " لیتر · " + Shamsi.Money(e.TotalUsd, 1) + " $"
                    + (e.PerLiter != 0m ? " · فی لیتر " + Shamsi.Money(e.PerLiter, 1) : ""),
                Math.Round(e.TotalAfn), " افغانی", "out"));
        return list;
    }

    private static async Task<List<HistoryRow>> DebtAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        // نامِ شخص و زیرحساب از دو جدول می‌آید؛ یک‌بار خوانده و در حافظه نگاشت
        // می‌شود تا برای هزاران ردیف، هزاران کوئری نرود.
        var debtors = await db.Debtors.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name ?? "", ct);
        var accounts = await db.DebtAccounts.AsNoTracking().ToListAsync(ct);
        var byId = accounts.ToDictionary(a => a.Id);

        string Who(long? acctId)
        {
            if (acctId is null || !byId.TryGetValue(acctId.Value, out var a)) return "—";
            var ownerId = a.MainOfDebtorId ?? a.DebtorId;
            var owner = ownerId is not null && debtors.TryGetValue(ownerId.Value, out var n) ? n : "";
            // حسابِ اصلی فقط نامِ شخص است؛ زیرحساب «شخص › زیرحساب».
            return a.MainOfDebtorId is not null || string.IsNullOrWhiteSpace(a.Name)
                ? owner : owner + " › " + a.Name;
        }

        var list = new List<HistoryRow>();
        foreach (var r in await db.DebtRows.AsNoTracking().ToListAsync(ct))
        {
            var bardagi = r.ByMoney ? r.Bardagi : r.Liters * (r.PricePerLiter ?? 0m);
            if (bardagi == 0m && r.Rasid == 0m && r.Liters == 0m) continue;

            var bits = new List<string>();
            if (r.Liters != 0m)
                bits.Add("🛢️ " + Shamsi.Money(r.Liters) + " لیتر"
                    + (r.PricePerLiter is { } pp && pp != 0m ? " × فی " + Shamsi.Money(pp) : ""));
            if (r.Rasid != 0m) bits.Add("💵 رسید " + Shamsi.Money(Math.Round(r.Rasid)));
            bits.Add(r.ByMoney ? "واحد پول" : "واحد تیل");

            var who = Who(r.ByMoney ? r.MoneyAccountId : r.FuelAccountId);
            list.Add(new HistoryRow("debt", r.DateShamsi ?? "", r.DateKey,
                who + (string.IsNullOrWhiteSpace(r.Name) ? "" : " — " + r.Name),
                string.Join(" · ", bits), Math.Round(bardagi), " افغانی", "out"));
        }
        return list;
    }

    private static async Task<List<HistoryRow>> ReceiptsAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var list = new List<HistoryRow>();
        foreach (var r in await db.DebtQuickReceipts.AsNoTracking().ToListAsync(ct))
        {
            if (r.Amount == 0m && string.IsNullOrWhiteSpace(r.Account)) continue;
            list.Add(new HistoryRow("rasid", r.DateShamsi ?? "", r.DateKey,
                "🧾 رسید — " + (string.IsNullOrWhiteSpace(r.Account) ? "—" : r.Account!),
                string.IsNullOrWhiteSpace(r.Note) ? "رسید نقدیِ قرض‌دار" : r.Note!,
                Math.Round(r.Amount), " افغانی", "in"));
        }
        return list;
    }

    private async Task<List<HistoryRow>> RetailAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var list = new List<HistoryRow>();
        var today = DateTime.Now.Date;
        foreach (var e in await db.RetailRows.AsNoTracking().ToListAsync(ct))
        {
            var bord = _retail.Bardagi(e);
            if (bord == 0m && e.Rasid == 0m && e.Liters == 0m && string.IsNullOrWhiteSpace(e.Name))
                continue;

            var bits = new List<string>();
            if (e.Liters != 0m)
                bits.Add("🛢️ " + Shamsi.Money(e.Liters) + " لیتر"
                    + (e.PricePerLiter != 0m ? " × فی " + Shamsi.Money(e.PricePerLiter) : ""));
            if (e.Rasid != 0m) bits.Add("💵 رسید " + Shamsi.Money(Math.Round(e.Rasid)));
            var albaqi = _retail.Albaqi(e);
            bits.Add("الباقی " + Shamsi.Money(Math.Round(albaqi)));

            var ago = AgoText(e.DateShamsi, today);
            if (ago.Length > 0) bits.Add("⏳ مدت برد: " + ago);

            list.Add(new HistoryRow("chakana", e.DateShamsi ?? "", e.DateKey,
                "🧾 چکنه — " + (string.IsNullOrWhiteSpace(e.Name) ? "بی‌نام" : e.Name!.Trim()),
                string.Join(" · ", bits), Math.Round(bord), " افغانی",
                albaqi > 0m ? "out" : "in"));
        }
        return list;
    }

    /// <summary>
    /// ══ شرکت‌ها — «بردگی‌های من و بردگی‌های شرکت» (۱۴۰۵/۰۷/۱۲) ═══════════════
    ///
    /// هر ردیفِ حسابِ شرکت دو طرف دارد، همان دو طرفِ صفحهٔ خودِ شرکت:
    ///   • **بردگیِ پمپ از شرکت** — تیلی که پمپ خریده: تن × قیمتِ تن = دالر
    ///     (و به افغانی با نرخِ همان ردیف)،
    ///   • **رسیدِ پمپ = بردگیِ شرکت** — پولی که پمپ به شرکت داده، به ارزِ خودش
    ///     (افغانی یا دالر). «رسیدِ من، بردگیِ او می‌شود.»
    /// ⚠️ تا امروز ارزِ رسید نوشته نمی‌شد و رسیدِ دالری افغانی خوانده می‌شد.
    /// ⛔ عددها همان ‎CompanyService.Ton/TotalUsd/TotalAfn‎.
    /// </summary>
    private async Task<List<HistoryRow>> CompanyAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var names = await db.TilCompanies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name ?? "", ct);
        var list = new List<HistoryRow>();
        foreach (var r in await db.CompanyRows.AsNoTracking().ToListAsync(ct))
        {
            var usd = _company.TotalUsd(r);
            if (usd == 0m && r.Poul == 0m) continue;

            var poulCur = r.PoulCurrency == Currency.Usd ? " $" : " افغانی";
            var bits = new List<string>();
            if (usd != 0m) bits.Add("📦 بردگیِ پمپ " + Shamsi.Money(usd, 1) + " $");
            if (r.Poul != 0m) bits.Add("💵 رسیدِ پمپ " + Shamsi.Money(r.Poul) + poulCur);

            var name = names.TryGetValue(r.CompanyId, out var n) ? n : "";
            var ton = _company.Ton(r);
            var afn = _company.TotalAfn(r);
            var from = r.SourcePurchaseId is not null ? "📦 خریدِ مخزن"
                     : r.SourceExchangeId is not null ? "💱 از صرافی"
                     : r.SourceReceiptId is not null ? "🏦 از گاوصندوق"
                     : "✍️ دستی";
            list.Add(new HistoryRow("company", r.DateShamsi ?? "", r.DateKey,
                name + (string.IsNullOrWhiteSpace(r.Name) ? "" : " — " + r.Name),
                string.Join(" · ", bits), Math.Round(afn), " افغانی",
                usd != 0m ? "out" : "in",
                new[]
                {
                    Date(r.DateShamsi), name.Length > 0 ? name : "—",
                    r.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول",
                    string.IsNullOrWhiteSpace(r.Name) ? "—" : r.Name!.Trim(),
                    ton == 0m ? "—" : Shamsi.Money(ton, 3),
                    usd == 0m ? "—" : Shamsi.Money(usd, 1) + " $",
                    afn == 0m ? "—" : Shamsi.Money(Math.Round(afn)),
                    r.Poul == 0m ? "—" : Shamsi.Money(r.Poul) + poulCur,
                    from,
                }));
        }
        return list;
    }

    /// <summary>
    /// ══ مصارف ═════════════════════════════════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «برای مصارف هم [دقیق] نیست.»
    ///
    /// حق داشت: ستونِ شرح فقط یادداشتِ کاربر بود و مصرفی که خودِ برنامه ساخته
    /// (معاشِ کارمند، ردیفِ مصرفِ یک ورق) از مصرفِ دستی جدا نمی‌شد. حالا
    /// **منبعِ** هر مصرف نوشته می‌شود — همان چیزی که خودِ رکورد از قبل داشت و
    /// هیچ‌جا دیده نمی‌شد.
    ///
    /// ⛔ هیچ مبلغی عوض نشد و هیچ ردیفی کم و زیاد نشد.
    /// </summary>
    private static async Task<List<HistoryRow>> ExpenseAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var staff = await db.StaffMembers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name ?? "", ct);
        var list = new List<HistoryRow>();
        foreach (var e in await db.Expenses.AsNoTracking().ToListAsync(ct))
        {
            var bits = new List<string>();
            if (e.SalaryStaffId is long sid)
                bits.Add("👤 معاشِ " + (staff.TryGetValue(sid, out var nm) && nm.Length > 0 ? nm : "کارمند")
                         + (string.IsNullOrWhiteSpace(e.SalaryMonth)
                                ? "" : " — " + Shamsi.MonthLabel(e.SalaryMonth)));
            else if (!string.IsNullOrWhiteSpace(e.SrcKey))
                bits.Add("📝 از ورقِ روزانه");
            else
                bits.Add("✍️ مصرفِ دستی");

            if (!string.IsNullOrWhiteSpace(e.Note)) bits.Add(e.Note!.Trim());

            list.Add(new HistoryRow("expense", e.DateShamsi ?? "", e.DateKey,
                string.IsNullOrWhiteSpace(e.Title) ? "مصرف" : e.Title!,
                string.Join(" · ", bits), Math.Round(e.Amount), " افغانی", "out"));
        }
        return list;
    }

    /// <summary>
    /// ══ ورق‌ها — جملهٔ هر شیفت، نه ردیف‌به‌ردیف (۱۴۰۵/۰۷/۱۲) ═══════════════════
    ///
    /// خواستهٔ صاحب ریپو: «شمارِ قرض، شمارِ مصارف، مقدارِ مصارف، مقدارِ فروش و
    /// مقدارِ قرض‌ها — همه را جمله نشان بده، نه یکی یکی.» پس یک ردیف برای هر
    /// **شیفت** (روز و شب هرگز با هم جمع نمی‌شوند — همان قاعدهٔ صفحهٔ ورق).
    ///
    /// ⛔ جمع‌ها از ‎WaraqService.ShiftTotals‎ است — همان شش کادرِ «خلاصه شیفت»ِ
    /// صفحهٔ ورق. شمار: ردیفِ قرضی که نام یا مبلغ دارد، و ردیفِ مصرفی که نام
    /// دارد — مو‌به‌مو همان ردیف‌هایی که در آن جمع شمرده می‌شوند.
    /// ⚠️ شیفتی که نه پایه دارد نه ردیفِ نام‌دار نمی‌آید (کارت هم همان را می‌شمارد).
    /// </summary>
    private async Task<List<HistoryRow>> WaraqAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var entries = await db.WaraqEntries.AsNoTracking().AsSplitQuery()
            .Include(w => w.Shifts).ThenInclude(s => s.Pumps)
            .Include(w => w.Shifts).ThenInclude(s => s.Transactions)
            .ToListAsync(ct);

        var list = new List<HistoryRow>();
        foreach (var w in entries)
            foreach (var sd in w.Shifts.OrderBy(x => x.Kind == ShiftKind.Night ? 1 : 0))
            {
                if (sd.Pumps.Count == 0 && !sd.Transactions.Any(t => !string.IsNullOrWhiteSpace(t.Name))) continue;

                var t = _waraq.ShiftTotals(sd);
                var debtN = sd.Transactions.Count(x => x.Type == WaraqTxnType.Debt
                                                 && (!string.IsNullOrWhiteSpace(x.Name) || _waraq.TxnAmount(sd, x) != 0m));
                var expN = sd.Transactions.Count(x => x.Type != WaraqTxnType.Debt && !string.IsNullOrWhiteSpace(x.Name));
                var workers = string.Join("، ", sd.Pumps.Select(p => p.Worker?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct());
                if (workers.Length == 0) workers = string.IsNullOrWhiteSpace(sd.WorkerName) ? "—" : sd.WorkerName!.Trim();
                var when = sd.Kind == ShiftKind.Night ? "🌙 شب" : "☀️ روز";
                var liters = t.PetrolLiters + t.DieselLiters;
                var sales = Math.Round(t.Sales, 0, MidpointRounding.AwayFromZero);

                list.Add(new HistoryRow("waraq", w.DateShamsi ?? "", w.DateKey,
                    "📝 ورق " + (w.DateShamsi ?? "") + " — " + when,
                    Shamsi.Money(debtN) + " قرض (" + Shamsi.Money(Math.Round(t.Debt)) + ") · "
                        + Shamsi.Money(expN) + " مصرف (" + Shamsi.Money(Math.Round(t.Expenses)) + ") · فروش "
                        + Shamsi.Money(sales),
                    sales, " افغانی", "in",
                    new[]
                    {
                        Date(w.DateShamsi), when, workers,
                        Shamsi.Money(debtN), Shamsi.Money(Math.Round(t.Debt, 0, MidpointRounding.AwayFromZero)) + " افغانی",
                        Shamsi.Money(expN), Shamsi.Money(Math.Round(t.Expenses, 0, MidpointRounding.AwayFromZero)) + " افغانی",
                        Shamsi.Money(liters) + " لیتر", Shamsi.Money(sales) + " افغانی",
                    }));
            }
        return list;
    }

    private static async Task<List<HistoryRow>> InvoiceAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var list = new List<HistoryRow>();
        foreach (var v in await db.Invoices.AsNoTracking().ToListAsync(ct))
        {
            var total = v.Liters * v.PricePerLiter + v.Amount;

            var bits = new List<string>
            {
                v.Status == InvoiceStatus.Approved ? "✅ تایید شده" : "🟡 در صف",
            };
            if (v.Liters != 0m)
                bits.Add("🛢️ " + Shamsi.Money(v.Liters) + " لیتر"
                    + (v.PricePerLiter != 0m ? " × فی " + Shamsi.Money(v.PricePerLiter) : "")
                    + (v.Fuel == FuelType.Diesel ? " (دیزل)" : " (پطرول)"));
            if (v.Amount != 0m) bits.Add("💵 مبلغ " + Shamsi.Money(Math.Round(v.Amount)));
            if (!string.IsNullOrWhiteSpace(v.VehicleType)) bits.Add("🚚 " + v.VehicleType);

            list.Add(new HistoryRow("invoice", v.DateShamsi ?? "", v.DateKey,
                "🧾 فاکتور " + Shamsi.Money(v.InvoiceNumber) + " — "
                    + (string.IsNullOrWhiteSpace(v.CustomerName) ? "—" : v.CustomerName!),
                string.Join(" · ", bits), Math.Round(total), " افغانی", "in"));
        }
        return list;
    }

    /// <summary>
    /// ══ پارچه‌ها (۱۴۰۵/۰۷/۱۲) ═══════════════════════════════════════════════
    ///
    /// «اسمِ کارمند، روز یا شب، شمارهٔ پایه، شروع و ختمِ پایه‌ها را ثبت کند که
    /// آدم بفهمد چی به چیه.» — هر کدام ستونِ خودش. ⛔ هیچ منطقی دست نخورد: همان
    /// دو ردیف برای هر پارچه (روز و شب) و همان پول و لیترِ خودِ پارچه.
    /// </summary>
    private static async Task<List<HistoryRow>> ShiftAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var reports = await db.Reports.AsNoTracking()
            .Include(r => r.DayShift).Include(r => r.NightShift).ToListAsync(ct);

        var list = new List<HistoryRow>();
        foreach (var rep in reports)
        {
            foreach (var (shift, when) in new (ShiftData?, string)[]
                     { (rep.DayShift, "☀️ روز"), (rep.NightShift, "🌙 شب") })
            {
                if (shift is null) continue;
                var bits = new List<string>();
                if (!string.IsNullOrWhiteSpace(shift.Name)) bits.Add("👤 " + shift.Name);
                bits.Add(when);
                if (shift.PumpNum > 0) bits.Add("پایهٔ " + Shamsi.Money(shift.PumpNum));
                bits.Add("شروع " + Shamsi.Money(shift.Start) + " · ختم " + Shamsi.Money(shift.End));
                if (shift.Sale != 0m) bits.Add("🛢️ " + Shamsi.Money(Math.Round(shift.Sale)) + " لیتر");

                var fuel = rep.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
                list.Add(new HistoryRow("shift", rep.DateShamsi ?? "", rep.DateKey,
                    "📋 پارچه " + fuel,
                    string.Join(" · ", bits), Math.Round(shift.Money), " افغانی", "in",
                    new[]
                    {
                        Date(rep.DateShamsi), rep.ReportNum > 0 ? Shamsi.Money(rep.ReportNum) : "—",
                        fuel, when, string.IsNullOrWhiteSpace(shift.Name) ? "—" : shift.Name!.Trim(),
                        shift.PumpNum > 0 ? Shamsi.Money(shift.PumpNum) : "—",
                        Shamsi.Money(shift.Start), Shamsi.Money(shift.End),
                        Shamsi.Money(Math.Round(shift.Sale)) + " لیتر",
                        Shamsi.Money(Math.Round(shift.Money)) + " افغانی",
                    }));
            }
        }
        return list;
    }

    private static async Task<List<HistoryRow>> AttendanceAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var staff = await db.StaffMembers.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name ?? "", ct);
        var list = new List<HistoryRow>();
        foreach (var a in await db.Attendance.AsNoTracking().ToListAsync(ct))
        {
            var bits = new List<string>();
            if (!string.IsNullOrWhiteSpace(a.In)) bits.Add("🟢 آمدن " + a.In);
            if (!string.IsNullOrWhiteSpace(a.Out)) bits.Add("🔴 رفتن " + a.Out);
            if (!string.IsNullOrWhiteSpace(a.Note)) bits.Add(a.Note!);

            var name = staff.TryGetValue(a.StaffId, out var n) && n.Length > 0 ? n : "کارمند";
            list.Add(new HistoryRow("attend", a.DateShamsi ?? "", a.DateKey,
                "🕒 " + name, bits.Count > 0 ? string.Join(" · ", bits) : "حاضری",
                null, "", ""));
        }
        return list;
    }

    // ══ ابزارهای کوچک ══════════════════════════════════════════════════════

    private static string Date(string? shamsi) => string.IsNullOrWhiteSpace(shamsi) ? "بی‌تاریخ" : shamsi!;

    /// <summary>تاریخِ شمسیِ نوشته‌شده → میلادیِ واقعی. ‎null‎ یعنی بی‌تاریخ.</summary>
    private static DateTime? DateOf(string? shamsi)
    {
        var k = Shamsi.Key(shamsi);
        if (k == 0) return null;
        try
        {
            var cal = new System.Globalization.PersianCalendar();
            return cal.ToDateTime(k / 10000, k / 100 % 100, k % 100, 0, 0, 0, 0);
        }
        catch { return null; }
    }

    /// <summary>«همین امروز» / «۱۲ روز گذشته» — همان ‎_hcAgoText‎.</summary>
    private static string AgoText(string? shamsi, DateTime today)
    {
        var d = DateOf(shamsi);
        if (d is null) return "";
        var days = (int)Math.Round((today - d.Value).TotalDays);
        if (days < 0) days = 0;
        return days == 0 ? "همین امروز" : Shamsi.Money(days) + " روز گذشته";
    }
}
