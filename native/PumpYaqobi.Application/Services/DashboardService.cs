using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>بازهٔ نوارِ نمودار — همان چهار تبِ «روزانه/هفتگی/ماهانه/سالانه».</summary>
public enum DashRange { Day = 1, Week = 2, Month = 3, Year = 4 }

/// <summary>فیلترِ سوختِ داشبورد — «همه / پطرول / دیزل».</summary>
public enum DashFuel { All = 0, Petrol = 1, Diesel = 2 }

/// <summary>یک اندازه‌گیری در یک سطل: مبلغ، لیتر، مفاد، تعدادِ ثبت (‎_dashZ‎).</summary>
public sealed class DashCell
{
    public decimal Afn, Liters, Profit;
    public int Count;
}

/// <summary>یک سطلِ نوار (‎_dashShell‎) — دو سوخت، مصارف و جریانِ گاوصندوق.</summary>
public sealed class DashBucket
{
    public string Label = "", Full = "";
    public DashCell Petrol = new(), Diesel = new();
    public decimal Exp, SafeIn, SafeOut;
}

/// <summary>خروجیِ ‎_dashSeries‎: سطل‌ها به‌علاوهٔ خانهٔ «حالا».</summary>
public sealed class DashSeries
{
    public required IReadOnlyList<DashBucket> Buckets { get; init; }
    public required int Slot { get; init; }
    public required (int Y, int M, int D) Today { get; init; }
}

/// <summary>دادهٔ کارتِ قرض‌داران (‎_dashDebtInfo‎).</summary>
public readonly record struct DashDebtInfo(decimal Total, int Persons, int Invoices, int Companies);

/// <summary>موجودیِ گاوصندوق به تفکیکِ ارز (‎_dashSafeBalance‎).</summary>
public readonly record struct DashSafeBalance(decimal Afn, decimal Usd);

/// <summary>مصارفِ امروز / هفته / ماه / سال (‎_dashExpQuick‎).</summary>
public readonly record struct DashExpQuick(decimal Day, decimal Week, decimal Month, decimal Year);

/// <summary>یک مخزن روی دایرهٔ داشبورد (‎_dashTankInfo‎).</summary>
public readonly record struct DashTank(FuelType Fuel, string Name, decimal Current, decimal Capacity, decimal Raw);

/// <summary>ورودیِ خامِ داشبورد — همه‌اش «فقط خواندنی» است، مثل نسخهٔ وب.</summary>
public sealed class DashInput
{
    public IReadOnlyList<ParchaReport> Reports { get; init; } = Array.Empty<ParchaReport>();
    public IReadOnlyList<Expense> Expenses { get; init; } = Array.Empty<Expense>();
    public IReadOnlyList<SafeEntry> SafeEntries { get; init; } = Array.Empty<SafeEntry>();
}

/// <summary>
/// ══ داشبورد ═════════════════════════════════════════════════════════════════
/// همان محاسبه‌هایی که ‎renderDashboard‎ و یارانش در نسخهٔ وب انجام می‌دهند —
/// خط‌به‌خط، با همان گِردکردن‌ها و همان قاعدهٔ «نشانگرِ حالا روی نوار».
///
/// ⚠️ این سرویس هیچ‌چیز نمی‌نویسد. داشبورد در نسخهٔ وب هم فقط خواننده است.
/// </summary>
public sealed class DashboardService
{
    private readonly Func<DateTime> _now;

    public DashboardService(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    // ── تاریخ ────────────────────────────────────────────────────────────────

    /// <summary>‎_dashParseDate‎ — «۱۴۰۵/۰۶/۱۵» → (y, m, d).</summary>
    public static (int Y, int M, int D) ParseDate(string? d)
    {
        var p = Shamsi.ToEnDigits(d ?? "").Split('/');
        int At(int i) => i < p.Length && int.TryParse(p[i], out var v) ? v : 0;
        return (At(0), At(1), At(2));
    }

    /// <summary>‎_dashDayKey‎.</summary>
    public static int DayKey(int y, int m, int d) => y * 10000 + m * 100 + d;

    /// <summary>‎_dashDayAt(offset)‎ — روزِ شمسیِ ‎offset‎ روز دورتر از امروز.</summary>
    public (int Y, int M, int D) DayAt(int offset) => ParseDate(Shamsi.Of(_now().Date.AddDays(offset)));

    /// <summary>‎_dashCols()‎ — روز ۷ · هفته ۷ · ماه ۱۲ · سال ۶.</summary>
    public static int Cols(DashRange r) => r switch
    {
        DashRange.Month => 12,
        DashRange.Year => 6,
        _ => 7,
    };

    /// <summary>
    /// ‎_dashTodaySlot(n)‎ — خانهٔ «حالا» روی نوار. نوار سرِ جایش می‌ماند و فقط
    /// نشانگر هر دوره یک خانه جلو می‌رود؛ به آخر که رسید از سر شروع می‌کند.
    /// </summary>
    public int TodaySlot(DashRange range, int n)
    {
        if (n < 1) return 0;
        var d = _now();
        var dayNo = (long)Math.Floor(new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc)
                                     .Subtract(DateTime.UnixEpoch).TotalMilliseconds / 864e5);
        long seq;
        if (range == DashRange.Week) seq = (long)Math.Floor(dayNo / 7.0);
        else if (range is DashRange.Month or DashRange.Year)
        {
            var t = ParseDate(Shamsi.Of(_now()));
            seq = range == DashRange.Month ? (long)t.Y * 12 + t.M : t.Y;
        }
        else seq = dayNo;
        return (int)(((seq % n) + n) % n);
    }

    // ── سطل‌ها ───────────────────────────────────────────────────────────────

    private sealed record Made(List<DashBucket> Buckets, Func<(int Y, int M, int D), int> Assign,
                               (int Y, int M, int D) Today, int Slot);

    private Made MakeBuckets(DashRange range)
    {
        var today = ParseDate(Shamsi.Of(_now()));
        var n = Cols(range);
        var slot = TodaySlot(range, n);
        int oFirst = -slot - 1, oLast = n - 1 - slot;   // اندیس ۰ فقط پایهٔ رشد است
        int At(int o) => o - oFirst;

        var buckets = new List<DashBucket>();
        Func<(int Y, int M, int D), int> assign;

        if (range == DashRange.Day)
        {
            var idx = new Dictionary<int, int>();
            for (var o = oFirst; o <= oLast; o++)
            {
                var p = DayAt(o);
                idx[DayKey(p.Y, p.M, p.D)] = At(o);
                var lbl = o switch
                {
                    0 => "امروز", -1 => "دیروز", -2 => "پریروز",
                    1 => "فردا", 2 => "پس‌فردا",
                    _ => p.M + "/" + p.D,
                };
                buckets.Add(new DashBucket { Label = lbl, Full = $"{p.Y}/{p.M}/{p.D}" });
            }
            assign = p => idx.TryGetValue(DayKey(p.Y, p.M, p.D), out var i) ? i : -1;
        }
        else if (range == DashRange.Week)
        {
            var idx = new Dictionary<int, int>();
            for (var o = oFirst; o <= oLast; o++)
            {
                var days = new List<(int Y, int M, int D)>();
                for (var d = 0; d < 7; d++)
                {
                    var p = DayAt(o * 7 - 6 + d);
                    idx[DayKey(p.Y, p.M, p.D)] = At(o);
                    days.Add(p);
                }
                var lbl = o switch
                {
                    0 => "این هفته", -1 => "هفته قبل", 1 => "هفته بعد",
                    _ => o < 0 ? (-o) + " هفته قبل" : o + " هفته بعد",
                };
                buckets.Add(new DashBucket
                {
                    Label = lbl,
                    Full = $"{days[0].M}/{days[0].D} تا {days[6].M}/{days[6].D}",
                });
            }
            assign = p => idx.TryGetValue(DayKey(p.Y, p.M, p.D), out var i) ? i : -1;
        }
        else if (range == DashRange.Month)
        {
            (int Y, int M) MAt(int o)
            {
                int m = today.M + o, y = today.Y;
                while (m > 12) { m -= 12; y++; }
                while (m < 1) { m += 12; y--; }
                return (y, m);
            }
            for (var o = oFirst; o <= oLast; o++)
            {
                var mm = MAt(o);
                var nm = Shamsi.MonthName(mm.M);
                buckets.Add(new DashBucket { Label = nm, Full = nm + " " + mm.Y });
            }
            assign = p =>
            {
                for (var o = oFirst; o <= oLast; o++)
                {
                    var mm = MAt(o);
                    if (p.Y == mm.Y && p.M == mm.M) return At(o);
                }
                return -1;
            };
        }
        else
        {
            for (var o = oFirst; o <= oLast; o++)
                buckets.Add(new DashBucket { Label = (today.Y + o).ToString(), Full = "سال " + (today.Y + o) });
            assign = p =>
            {
                var i = At(p.Y - today.Y);
                return i >= 0 && i <= oLast - oFirst ? i : -1;
            };
        }

        return new Made(buckets, assign, today, slot);
    }

    /// <summary>‎_dashSeries()‎ — یک منبعِ دادهٔ مشترک برای همهٔ کارت‌ها و نمودارها.</summary>
    public DashSeries Series(DashRange range, DashInput db)
    {
        var mk = MakeBuckets(range);
        var b = mk.Buckets;

        foreach (var r in db.Reports)
        {
            if (r.DateShamsi is null or "") continue;
            var i = mk.Assign(ParseDate(r.DateShamsi));
            if (i < 0) continue;
            // پطرول دو شیفت دارد (روز و شب)؛ دیزل در نسخهٔ وب یک ردیفِ DB.shifts
            // است و این‌جا همان گزارش با شیفتِ روزش نمایندگی می‌شود.
            var t = r.Fuel == FuelType.Petrol ? b[i].Petrol : b[i].Diesel;
            foreach (var sh in new[] { r.DayShift, r.NightShift })
            {
                if (sh is null) continue;
                t.Afn += sh.Money; t.Liters += sh.Sale; t.Profit += sh.Profit; t.Count++;
            }
        }

        foreach (var e in db.Expenses)
        {
            if (e.DateShamsi is null or "") continue;
            var i = mk.Assign(ParseDate(e.DateShamsi));
            if (i >= 0) b[i].Exp += e.Amount;
        }

        foreach (var e in db.SafeEntries)
        {
            // جریانِ افغانی؛ دالر فقط در موجودیِ کل شمرده می‌شود
            if (e.DateShamsi is null or "" || e.Currency == Currency.Usd) continue;
            var i = mk.Assign(ParseDate(e.DateShamsi));
            if (i < 0) continue;
            if (e.Kind == SafeEntryKind.Bardagi) b[i].SafeIn += e.Amount;
            else b[i].SafeOut += e.Amount;
        }

        return new DashSeries { Buckets = b, Slot = mk.Slot, Today = mk.Today };
    }

    /// <summary>‎_dashPick‎ — اعمالِ فیلترِ سوخت روی یک سطل.</summary>
    public static DashCell Pick(DashBucket b, DashFuel f) => f switch
    {
        DashFuel.Petrol => b.Petrol,
        DashFuel.Diesel => b.Diesel,
        _ => new DashCell
        {
            Afn = b.Petrol.Afn + b.Diesel.Afn,
            Liters = b.Petrol.Liters + b.Diesel.Liters,
            Profit = b.Petrol.Profit + b.Diesel.Profit,
            Count = b.Petrol.Count + b.Diesel.Count,
        },
    };

    /// <summary>
    /// ‎_dashGrowth‎ — درصدِ رشد. ‎null‎ یعنی «داده کافی نیست» (قبلی صفر، فعلی ناصفر).
    /// گِردکردن مثل ‎Math.round‎ِ جاوااسکریپت است: نیم به سمتِ بالا، نه به زوج.
    /// </summary>
    public static int? Growth(decimal cur, decimal prev)
    {
        if (prev == 0) return cur != 0 ? null : 0;
        return (int)Math.Floor((cur - prev) / prev * 100m + 0.5m);
    }

    /// <summary>‎_dashFuelWord‎.</summary>
    public static string FuelWord(DashFuel f) => f switch
    {
        DashFuel.Petrol => "⛽ پطرول",
        DashFuel.Diesel => "🟤 دیزل",
        _ => "⛽🟤 هر دو سوخت",
    };

    /// <summary>‎_dashSafeBalance‎ — ماندگی منهای بردگی، به تفکیکِ ارز.</summary>
    public static DashSafeBalance SafeBalance(IEnumerable<SafeEntry> entries)
    {
        decimal afn = 0, usd = 0;
        foreach (var e in entries)
        {
            var v = (e.Kind == SafeEntryKind.Bardagi ? -1 : 1) * e.Amount;
            if (e.Currency == Currency.Usd) usd += v; else afn += v;
        }
        return new DashSafeBalance(afn, usd);
    }

    /// <summary>‎_dashExpQuick‎ — مصارفِ امروز / ۷ روزِ اخیر / ماهِ جاری / سالِ جاری.</summary>
    public DashExpQuick ExpQuick(IEnumerable<Expense> expenses)
    {
        var today = ParseDate(Shamsi.Of(_now()));
        var week = new HashSet<int>();
        for (var k = 6; k >= 0; k--)
        {
            var p = DayAt(-k);
            week.Add(DayKey(p.Y, p.M, p.D));
        }
        var tKey = DayKey(today.Y, today.M, today.D);
        decimal day = 0, wk = 0, month = 0, year = 0;
        foreach (var e in expenses)
        {
            if (e.DateShamsi is null or "") continue;
            var p = ParseDate(e.DateShamsi);
            var k = DayKey(p.Y, p.M, p.D);
            if (p.Y == today.Y) { year += e.Amount; if (p.M == today.M) month += e.Amount; }
            if (week.Contains(k)) wk += e.Amount;
            if (k == tKey) day += e.Amount;
        }
        return new DashExpQuick(day, wk, month, year);
    }

    /// <summary>
    /// ‎_dashTankInfo‎ — ظرفیتِ تنظیم‌شده، وگرنه بالاگردِ هزارِ بزرگ‌ترینِ
    /// (موجودی، ۴×آستانه، ۱۰۰۰).
    /// </summary>
    public static DashTank Tank(FuelType fuel, decimal stock, decimal? setCapacity, decimal threshold)
    {
        var cap = setCapacity is > 0 ? setCapacity.Value
                : Math.Ceiling(Math.Max(Math.Max(stock, threshold * 4), 1000m) / 1000m) * 1000m;
        var name = fuel == FuelType.Petrol ? "⛽ پطرول" : "🟤 دیزل";
        return new DashTank(fuel, name, Math.Max(0, stock), cap, stock);
    }
}
