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
public sealed record HistoryRow(
    string Kind, string DateShamsi, int DateKey,
    string Title, string Detail, decimal? Amount, string Unit, string Tone)
{
    public string AmountText => Amount is null ? "" : Shamsi.Money(Amount.Value) + Unit;

    /// <summary>«1405/06» — گروهِ ماهِ همین ردیف؛ خالی یعنی بی‌تاریخ.</summary>
    public string MonthKey => DateKey <= 0 ? "" : $"{DateKey / 10000:0000}/{DateKey / 100 % 100:00}";
}

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

    public HistoryService(PumpDbFactory dbf, PermissionService perm,
                          ExchangeService exchange, RetailService retail,
                          CompanyService company, AmanatService amanat,
                          AmanatDataService amanatData)
    {
        _dbf = dbf; _perm = perm; _exchange = exchange; _retail = retail;
        _company = company; _amanat = amanat; _amanatData = amanatData;
    }

    /// <summary>شمار و تازه‌ترین تاریخِ هر بخش — خوراکِ کارت‌های صفحهٔ اول.</summary>
    public async Task<List<HistoryKind>> CardsAsync(CancellationToken ct = default)
    {
        var all = new List<HistoryKind>(Kinds.Length);
        foreach (var (key, label) in Kinds)
        {
            var rows = await FeedAsync(key, ct);
            all.Add(new HistoryKind(key, label, rows.Count,
                                    rows.Count > 0 ? rows[0].DateShamsi : ""));
        }
        return all;
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
            var cur = r.Currency switch
            {
                ExchangeCurrency.Kaldar => "کالدار",
                ExchangeCurrency.Afghani => "افغانی",
                _ => "تومان",
            };

            // یک سطرِ صرافی می‌تواند دو ردیفِ تاریخچه بدهد: رسید و بردگی.
            if (usd != 0m)
                list.Add(new HistoryRow("sarrafi", r.DateShamsi ?? "", r.DateKey, title,
                    "💵 رسید — " + Shamsi.Money(r.Amount) + " " + cur
                        + (r.Rate != 0m ? " · نرخ " + Shamsi.Money(r.Rate) : ""),
                    Math.Round(usd, 2), " $", "in"));

            if (r.Bardagi != 0m)
                list.Add(new HistoryRow("sarrafi", r.DateShamsi ?? "", r.DateKey, title,
                    "📤 بردگی", r.Bardagi, " $", "out"));

            if (usd == 0m && r.Bardagi == 0m && r.Amount != 0m)
                list.Add(new HistoryRow("sarrafi", r.DateShamsi ?? "", r.DateKey, title,
                    "بدون نرخ", r.Amount, " " + cur, ""));
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

    private async Task<List<HistoryRow>> CompanyAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var names = await db.TilCompanies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name ?? "", ct);
        var list = new List<HistoryRow>();
        foreach (var r in await db.CompanyRows.AsNoTracking().ToListAsync(ct))
        {
            var usd = _company.TotalUsd(r);
            if (usd == 0m && r.Poul == 0m) continue;

            var bits = new List<string>();
            if (usd != 0m) bits.Add("📦 " + Shamsi.Money(usd, 1) + " $");
            if (r.Poul != 0m) bits.Add("💵 رسید " + Shamsi.Money(r.Poul));

            var name = names.TryGetValue(r.CompanyId, out var n) ? n : "";
            list.Add(new HistoryRow("company", r.DateShamsi ?? "", r.DateKey,
                name + (string.IsNullOrWhiteSpace(r.Name) ? "" : " — " + r.Name),
                string.Join(" · ", bits), Math.Round(_company.TotalAfn(r)), " افغانی",
                usd != 0m ? "out" : "in"));
        }
        return list;
    }

    private static async Task<List<HistoryRow>> ExpenseAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var list = new List<HistoryRow>();
        foreach (var e in await db.Expenses.AsNoTracking().ToListAsync(ct))
            list.Add(new HistoryRow("expense", e.DateShamsi ?? "", e.DateKey,
                string.IsNullOrWhiteSpace(e.Title) ? "مصرف" : e.Title!,
                e.Note ?? "", Math.Round(e.Amount), " افغانی", "out"));
        return list;
    }

    private static async Task<List<HistoryRow>> WaraqAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        var entries = await db.WaraqEntries.AsNoTracking().AsSplitQuery()
            .Include(w => w.Shifts).ThenInclude(s => s.Pumps)
            .Include(w => w.Shifts).ThenInclude(s => s.Transactions)
            .ToListAsync(ct);

        var list = new List<HistoryRow>();
        foreach (var w in entries)
        {
            var pumps = w.Shifts.Sum(s => s.Pumps.Count);
            var txns = w.Shifts.Sum(s => s.Transactions.Count(t => !string.IsNullOrWhiteSpace(t.Name)));
            list.Add(new HistoryRow("waraq", w.DateShamsi ?? "", w.DateKey,
                "📝 ورق " + (w.DateShamsi ?? ""),
                Shamsi.Money(pumps) + " پایه · " + Shamsi.Money(txns) + " تراکنش",
                null, "", ""));
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
                if (shift.Sale != 0m) bits.Add("🛢️ " + Shamsi.Money(Math.Round(shift.Sale)) + " لیتر");

                list.Add(new HistoryRow("shift", rep.DateShamsi ?? "", rep.DateKey,
                    "📋 پارچه " + (rep.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول"),
                    string.Join(" · ", bits), Math.Round(shift.Money), " افغانی", "in"));
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
