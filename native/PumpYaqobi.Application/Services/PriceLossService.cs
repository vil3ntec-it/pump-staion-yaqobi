using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>نمای گزارشیِ یک فاکتور — ‎_plInvView(v)‎ی نسخهٔ وب.</summary>
public sealed record PlInvoice(
    long Id, int No, string Date, FuelType Fuel, decimal Liters,
    decimal Rc, decimal Ra, bool Approved, decimal Amount,
    bool HasDiff, decimal Dpl, decimal Diff, decimal NowRate, decimal Pdpl, decimal PendDiff)
{
    /// <summary>فقط ضرر — عددِ منفی «زیان» نیست.</summary>
    public decimal Loss => Diff > 0m ? Diff : 0m;
    public decimal Gain => Diff < 0m ? -Diff : 0m;
}

/// <summary>یک تکه از یک برداشت که یک رسیدِ مشخص تسویه‌اش کرده.</summary>
public sealed record PlPart(
    decimal Qty, string Date, bool Hdr, int InvNo, bool SameDay,
    decimal Ref, string RefOn, bool RefLocked, bool NoRate, bool NoSale, string Why,
    decimal Principal, decimal RefValue, decimal Loss);

/// <summary>گزارشِ یک برداشت — ‎_plRowReport‎.</summary>
public sealed record PlItem(
    FuelType Fuel, string Date, decimal Liters, decimal Sale, IReadOnlyList<PlPart> Parts,
    decimal Remain, bool IsInvoice, int InvNo, string Note, string Acct);

/// <summary>جمع‌های یک قرض‌دار — ‎t‎ در ‎_plPersonReport‎.</summary>
public sealed class PlTotals
{
    public int N, SingleN, InvRowN, LossN, SameDayN, NoRateN, PaidN;
    public decimal Fuel, Principal, RefValue, Loss, OpenFuel;
}

/// <summary>جمعِ «زیانِ اختلافِ نرخِ فاکتور» — ‎invSum‎.</summary>
public sealed class PlInvSum
{
    public decimal Loss, Gain, PendLoss;
    public int N, PendN;
}

/// <summary>یک قرض‌دار در گزارش (یا نامی که فقط فاکتور دارد و هنوز حساب ندارد).</summary>
public sealed record PlPerson(
    string Key, long? Id, string Name,
    IReadOnlyList<PlItem> Items, PlTotals T, IReadOnlyList<PlInvoice> Inv, PlInvSum Iv)
{
    public bool HasOpenFuel => T.OpenFuel > 0m;
    public bool HasPaid => T.PaidN > 0;
}

/// <summary>جمع‌های کلِ گزارش — ‎g‎ در ‎_plReportAll‎.</summary>
public sealed class PlGrand
{
    public int Persons, LossPersons, N, SingleN, InvN, NoRateN, InvLossN, PendN;
    public decimal Fuel, Principal, RefValue, Loss, OpenFuel, InvLoss, InvGain, PendLoss;
}

public sealed record PriceLossReport(IReadOnlyList<PlPerson> List, PlGrand G);

/// <summary>
/// ══ 📉 زیان ناشی از افزایش قیمت — فقط بخش «قرض‌داران»، فقط گزارش ═══════════
/// رونوشتِ ‎_plInvIndex‎ · ‎_plInvView‎ · ‎_plRateIndex‎ · ‎_plRateOn‎ ·
/// ‎_plReceipts‎ · ‎_plAllocate‎ · ‎_plRowReport‎ · ‎_plPersonReport‎ ·
/// ‎_plReportAll‎ی نسخهٔ وب.
///
/// وقتی مشتری تیل را قرضی می‌برد، تا روزی که بدهی‌اش را بپردازد ممکن است
/// قیمتِ تیل بالا رفته باشد؛ آن تفاوت، زیانِ ماست:
///
///     زیان = (نرخِ اتحادیه در روزِ رسید − نرخِ خودِ برداشت) × لیتر   (منفی ⇒ صفر)
///
/// قانون‌های سفت‌وسخت (خواستهٔ صاحب ریپو، همان‌که در سایت هم نوشته شده):
///   • هیچ عددی از این‌جا وارد بدهی، الباقی، موجودی یا حسابداری نمی‌شود.
///   • این سرویس **هیچ چیزی نمی‌نویسد** و فقط از داده‌ای که به آن داده شده
///     می‌خواند. نرخِ روز فقط از تاریخچهٔ اتحادیه و فقط برای تاریخِ واقعیِ
///     همان رسید خوانده می‌شود — نرخِ امروز جای نرخِ رسیدهای قدیمی نمی‌نشیند.
///   • برداشت و رسیدِ همان روز ⇒ زیان صفر. نرخِ روزِ رسیدِ کمتر یا برابر ⇒ صفر.
///   • هر رسید فقط یک‌بار خرج می‌شود: قدیمی‌ترین قرض اول، و «نصفه» هم پذیرفته
///     می‌شود تا زیان دقیقاً روی همان مقداری حساب شود که آن رسید بسته.
///
/// ⚠️ نسخهٔ وب سه فیلدِ ‎lossPaidDate/lossPaidRate/lossLegacy‎ را روی ردیف
/// مهر می‌زد. این‌جا آن مهر نیست (برنامهٔ نیتیو رسید را یک ردیفِ واقعی با
/// تاریخِ خودش ثبت می‌کند — <see cref="DebtCalculationService.MigrateReceiptsToRows"/>)،
/// پس رسیدِ داخلِ خودِ ردیف با تاریخِ همان ردیف حساب می‌شود؛ درست همان کاری که
/// سایت برای ردیفِ مهرنخورده می‌کند.
/// </summary>
public sealed class PriceLossService
{
    private const decimal Eps = 0.000001m;

    private sealed class RatePoint
    {
        public int Key; public string Date = ""; public decimal Rate; public long At;
    }

    private sealed class RateIndex
    {
        public List<RatePoint> Petrol = new(), Diesel = new();
        public int Today;
        public decimal NowPetrol, NowDiesel;
    }

    private readonly record struct RateOn(decimal Rate, string On, bool Exact, bool Missing, bool Locked, string Why);

    private sealed class InvIndex
    {
        public Dictionary<long, Invoice> ById = new();
        public Dictionary<long, List<Invoice>> ByPerson = new();
        public Dictionary<string, List<Invoice>> ByName = new();
        public Dictionary<string, string> OnlyNames = new();
    }

    private sealed class Alloc
    {
        public decimal Fuel, Sale, Settled;
        public List<(decimal Qty, string Date, decimal LockRate, int InvNo, bool Hdr)> Events = new();
    }

    private sealed class Receipt
    {
        public decimal V; public string At = ""; public long? InvId;
    }

    /// <summary>
    /// گزارشِ همهٔ قرض‌داران — ‎_plReportAll()‎.
    /// </summary>
    /// <param name="debtors">قرض‌داران با همهٔ حساب‌ها و ردیف‌ها (و دفترِ رسیدهای سربرگ، اگر دارند).</param>
    /// <param name="invoices">همهٔ فاکتورها.</param>
    /// <param name="rates">تاریخچهٔ نرخِ اتحادیه — ترتیبش مهم نیست.</param>
    /// <param name="unionPetrol">نرخِ اتحادیهٔ همین لحظه — پطرول.</param>
    /// <param name="unionDiesel">نرخِ اتحادیهٔ همین لحظه — دیزل.</param>
    /// <param name="today">تاریخِ امروز (شمسی).</param>
    public PriceLossReport Build(IReadOnlyList<Debtor> debtors, IReadOnlyList<Invoice> invoices,
                                 IReadOnlyList<RateHistoryEntry> rates,
                                 decimal unionPetrol, decimal unionDiesel, string today)
    {
        var ix = BuildInvIndex(debtors, invoices);
        var idx = BuildRateIndex(rates, unionPetrol, unionDiesel, today);
        var list = new List<PlPerson>();
        var g = new PlGrand();

        static PlInvSum InvSum(IEnumerable<PlInvoice> inv)
        {
            var a = new PlInvSum();
            foreach (var x in inv)
            {
                a.Loss += x.Loss; a.Gain += x.Gain;
                if (x.Loss > 0m) a.N++;
                if (!x.Approved && x.PendDiff > 0m) { a.PendN++; a.PendLoss += x.PendDiff; }
            }
            return a;
        }
        void AddInv(PlInvSum s)
        {
            g.InvLoss += s.Loss; g.InvGain += s.Gain; g.InvLossN += s.N;
            g.PendN += s.PendN; g.PendLoss += s.PendLoss;
        }

        foreach (var p in debtors)
        {
            if (p is null) continue;
            var (items, t) = PersonReport(idx, ix, p);
            var inv = InvoicesFor(ix, p.Id, p.Name, idx);
            if (t.N == 0 && inv.Count == 0) continue;
            var s = InvSum(inv);
            list.Add(new PlPerson(p.Id.ToString(), p.Id, string.IsNullOrWhiteSpace(p.Name) ? "—" : p.Name,
                                  items, t, inv, s));
            g.Persons++; g.N += t.N; g.SingleN += t.SingleN; g.InvN += inv.Count;
            g.Fuel += t.Fuel; g.Principal += t.Principal; g.RefValue += t.RefValue;
            g.Loss += t.Loss; g.OpenFuel += t.OpenFuel; g.NoRateN += t.NoRateN;
            AddInv(s);
            if (t.Loss > 0m || s.Loss > 0m) g.LossPersons++;
        }

        // کسانی که فقط فاکتورِ در صفِ تایید دارند و هنوز حسابِ قرض‌داری برایشان نیست
        foreach (var nm in ix.OnlyNames.Values)
        {
            var inv = InvoicesFor(ix, null, nm, idx);
            if (inv.Count == 0) continue;
            var s = InvSum(inv);
            g.InvN += inv.Count; AddInv(s);
            if (s.Loss > 0m) g.LossPersons++;
            list.Add(new PlPerson("name:" + PostingService.NormFa(nm), null, nm,
                                  Array.Empty<PlItem>(), new PlTotals(), inv, s));
        }

        var sorted = list
            .OrderByDescending(x => x.T.Loss + x.Iv.Loss)
            .ThenByDescending(x => x.T.Principal)
            .ThenByDescending(x => x.T.OpenFuel)
            .ToList();
        return new PriceLossReport(sorted, g);
    }

    // ── ۰) نمایهٔ فاکتورها — یک‌بار در هر گزارش ─────────────────────────────
    private static InvIndex BuildInvIndex(IReadOnlyList<Debtor> debtors, IReadOnlyList<Invoice> invoices)
    {
        var ix = new InvIndex();
        var have = new HashSet<string>();
        var acctOwner = new Dictionary<long, long>();
        foreach (var p in debtors)
        {
            if (p is null) continue;
            var k = PostingService.NormFa(p.Name);
            if (k.Length > 0) have.Add(k);
            foreach (var a in p.AllAccounts()) if (a.Id != 0) acctOwner[a.Id] = p.Id;
        }
        foreach (var v in invoices)
        {
            if (v is null) continue;
            ix.ById[v.Id] = v;
            if (v.ByMoney || !(v.Liters > 0m)) continue;
            if (v.DebtAccountId is { } aid && acctOwner.TryGetValue(aid, out var pid))
            {
                if (!ix.ByPerson.TryGetValue(pid, out var l)) ix.ByPerson[pid] = l = new();
                l.Add(v);
            }
            var nk = PostingService.NormFa(v.CustomerName);
            if (nk.Length > 0)
            {
                if (!ix.ByName.TryGetValue(nk, out var l)) ix.ByName[nk] = l = new();
                l.Add(v);
                if (!have.Contains(nk) && !ix.OnlyNames.ContainsKey(nk))
                    ix.OnlyNames[nk] = string.IsNullOrWhiteSpace(v.CustomerName) ? "—" : v.CustomerName!;
            }
        }
        return ix;
    }

    /// <summary>‎_plInvView(v)‎ — هر دو نرخ از قبل روی خودِ فاکتور قفل شده‌اند.</summary>
    public static PlInvoice InvoiceView(Invoice v, decimal unionPetrol, decimal unionDiesel)
    {
        var liters = v.Liters;
        var rc = v.RateOnCreate ?? v.PricePerLiter;
        var approved = v.Status == InvoiceStatus.Approved;
        var ra = approved ? v.RateOnApprove ?? 0m : 0m;
        var both = approved && rc > 0m && ra > 0m;
        var dpl = both ? ra - rc : 0m;
        var diff = dpl * liters;
        var now = approved ? 0m : (v.Fuel == FuelType.Diesel ? unionDiesel : unionPetrol);
        var pdpl = !approved && rc > 0m && now > 0m ? now - rc : 0m;
        return new PlInvoice(v.Id, v.InvoiceNumber, v.DateShamsi ?? "", v.Fuel, liters,
                             rc, ra, approved, rc * liters, both, dpl, diff, now, pdpl, pdpl * liters);
    }

    private static List<PlInvoice> InvoicesFor(InvIndex ix, long? personId, string? name, RateIndex idx)
    {
        var seen = new HashSet<long>();
        var outp = new List<PlInvoice>();
        void Take(List<Invoice>? arr)
        {
            if (arr is null) return;
            foreach (var v in arr)
                if (seen.Add(v.Id)) outp.Add(InvoiceView(v, idx.NowPetrol, idx.NowDiesel));
        }
        if (personId is { } pid && ix.ByPerson.TryGetValue(pid, out var byP)) Take(byP);
        var nk = PostingService.NormFa(name);
        if (nk.Length > 0 && ix.ByName.TryGetValue(nk, out var byN)) Take(byN);
        return outp.OrderByDescending(x => x.No).ToList();
    }

    // ── ۱) نرخِ اتحادیه در یک تاریخِ مشخص ──────────────────────────────────
    private static RateIndex BuildRateIndex(IReadOnlyList<RateHistoryEntry> rates,
                                            decimal unionPetrol, decimal unionDiesel, string today)
    {
        var idx = new RateIndex { Today = Shamsi.Key(today), NowPetrol = unionPetrol, NowDiesel = unionDiesel };
        foreach (var h in rates)
        {
            if (h is null || !(h.Rate > 0m)) continue;
            var key = Shamsi.Key(h.DateShamsi);
            if (key == 0) continue;
            (h.Fuel == FuelType.Diesel ? idx.Diesel : idx.Petrol)
                .Add(new RatePoint { Key = key, Date = h.DateShamsi ?? "", Rate = h.Rate, At = h.Id });
        }
        // تاریخچه فقط «تغییرها» را ثبت می‌کند؛ ترتیب: تاریخ، بعد ترتیبِ ثبت
        idx.Petrol.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.At.CompareTo(b.At));
        idx.Diesel.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.At.CompareTo(b.At));
        return idx;
    }

    private static RateOn RateAt(RateIndex idx, FuelType fuel, string date)
    {
        var key = Shamsi.Key(date);
        if (key == 0) return new RateOn(0m, "", false, true, false, "تاریخِ رسید ثبت نشده");
        var arr = fuel == FuelType.Diesel ? idx.Diesel : idx.Petrol;
        // ⚠️ جست‌وجوی دودویی، نه پیمایشِ خطی: پنج سال تاریخچهٔ روزانه × ده‌ها
        // هزار رسید، خطی یک ثانیه می‌شد. فهرست از ‎BuildRateIndex‎ مرتب است و
        // «آخرین رکورد با کلیدِ ≤ تاریخ» همان رکوردِ معتبرِ آن روز است.
        var lo = 0; var hi = arr.Count - 1; var hit = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (arr[mid].Key <= key) { hit = mid; lo = mid + 1; } else hi = mid - 1;
        }
        if (hit >= 0)
            return new RateOn(arr[hit].Rate, arr[hit].Date, arr[hit].Key == key, false, false, "");
        // امروز نرخِ اتحادیهٔ همین لحظه، خودش رکوردِ همین تاریخ است
        var now = fuel == FuelType.Diesel ? idx.NowDiesel : idx.NowPetrol;
        if (key == idx.Today && now > 0m) return new RateOn(now, date, true, false, false, "");
        return new RateOn(0m, "", false, true, false, "نرخ اتحادیه برای این تاریخ موجود نیست");
    }

    // ── ۲) همهٔ رسیدهای یک دفترِ حساب، با تاریخِ واقعیِ خودشان ───────────────
    private static List<Receipt> Receipts(DebtAccount acct, List<DebtRow> rows, bool money, FuelType fuel)
    {
        var outp = new List<Receipt>();
        decimal invSum = 0m;
        foreach (var r in rows)
        {
            if (r is null) continue;
            if (r.Liters > 0m) continue;             // ردیفِ برداشت است، نه رسید
            if (r.Fuel != fuel) continue;
            var v = money ? r.Rasid : r.RasidFuel;
            if (!(v > 0m)) continue;
            // سهمِ فاکتور از عددِ سربرگ فقط در دفترِ تیل جمع می‌شود
            if (r.InvoiceId is not null && !money) invSum += v;
            outp.Add(new Receipt { V = v, At = r.DateShamsi ?? "", InvId = r.InvoiceId });
        }

        var unit = money ? LedgerMode.Money : LedgerMode.Fuel;
        if (acct.RasidLog is { Count: > 0 })
        {
            foreach (var e in acct.RasidLog)
                if (e is not null && e.Unit == unit && e.Fuel == fuel && e.Value > 0m)
                    outp.Add(new Receipt { V = e.Value, At = e.DateShamsi ?? "" });
        }
        else if (!acct.ReceiptsMigrated)
        {
            // حسابِ قدیمی بدونِ دفتر — همان عددِ سربرگ، منهای سهمِ فاکتورها.
            // حسابِ مهاجرت‌کرده رسیدهایش را به ردیف برده و همان بالا شمرده شد.
            var isD = fuel == FuelType.Diesel;
            var head = money ? (isD ? acct.RasidMoneyDiesel : acct.RasidMoneyPetrol)
                             : (isD ? acct.RasidFuelDiesel : acct.RasidFuelPetrol);
            var v = head - invSum;
            if (v > Eps) outp.Add(new Receipt { V = v, At = "" });
        }
        return outp.OrderBy(x => Shamsi.Key(x.At)).ToList();
    }

    /// <summary>‎_lossSaleRate(r)‎ — فیِ واقعیِ همین ردیف (بردگی ÷ لیتر)، وگرنه فیِ دستی.</summary>
    public static decimal SaleRate(DebtRow r)
    {
        if (r.Liters > 0m && r.Bardagi > 0m) return r.Bardagi / r.Liters;
        return r.PricePerLiter is > 0m ? r.PricePerLiter.Value : 0m;
    }

    // ── ۳) کدام رسید، کدام برداشت را تسویه کرده ────────────────────────────
    private static List<Alloc> Allocate(DebtAccount acct, List<DebtRow> rows, bool money, InvIndex ix)
    {
        var outp = rows.Select(r => new Alloc { Fuel = r?.Liters ?? 0m, Sale = r is null ? 0m : SaleRate(r) }).ToList();
        foreach (var ft in new[] { FuelType.Petrol, FuelType.Diesel })
        {
            var order = Enumerable.Range(0, rows.Count)
                .Where(i => rows[i] is not null && outp[i].Fuel > 0m && rows[i].Fuel == ft)
                .OrderBy(i => Shamsi.Key(rows[i].DateShamsi)).ThenBy(i => i)
                .ToList();
            if (order.Count == 0) continue;

            // الف) رسیدی که داخلِ خودِ ردیف نوشته شده — با تاریخِ همان ردیف
            foreach (var i in order)
            {
                var x = outp[i]; var r = rows[i];
                var left = x.Fuel - x.Settled;
                if (!(left > Eps)) continue;
                var own = r.RasidFuel + (x.Sale > 0m ? r.Rasid / x.Sale : 0m);
                if (!(own > Eps)) continue;
                var q = Math.Min(left, own);
                x.Settled += q;
                x.Events.Add((q, r.DateShamsi ?? "", 0m, 0, false));
            }

            // ب) رسیدهای سربرگ و فاکتورها — از قدیمی‌ترین رسید، روی قدیمی‌ترین قرضِ باقی‌مانده
            // ⚠️ ‎first‎ روی اولین برداشتی می‌ایستد که هنوز باقی دارد: رسیدها از
            // قدیمی به نو روی همان ترتیب می‌نشینند، پس ردیف‌های پیش از آن برای
            // همیشه تسویه‌اند و هر رسید نباید از صفر دوباره از رویشان رد شود
            // (با پانصد ردیف و صدها رسید، مربعی می‌شد).
            var first = 0;
            foreach (var e in Receipts(acct, rows, money, ft))
            {
                var pool = e.V;
                Invoice? inv = e.InvId is { } id && ix.ById.TryGetValue(id, out var vv) ? vv : null;
                var lockRate = inv is null ? 0m : inv.RateOnCreate ?? inv.PricePerLiter;
                var invNo = inv?.InvoiceNumber ?? 0;
                while (first < order.Count && !(outp[order[first]].Fuel - outp[order[first]].Settled > Eps)) first++;
                for (var n = first; n < order.Count && pool > Eps; n++)
                {
                    var x = outp[order[n]];
                    var left = x.Fuel - x.Settled;
                    if (!(left > Eps)) continue;
                    decimal q;
                    if (money)
                    {
                        if (!(x.Sale > 0m)) continue;     // بی‌فی نمی‌شود افغانی را به لیتر برد
                        q = Math.Min(left, pool / x.Sale);
                        pool -= q * x.Sale;
                    }
                    else
                    {
                        q = Math.Min(left, pool);
                        pool -= q;
                    }
                    if (!(q > Eps)) continue;
                    x.Settled += q;
                    x.Events.Add((q, e.At, lockRate, invNo, true));
                }
            }
        }
        return outp;
    }

    // ── ۴) گزارشِ یک برداشت ─────────────────────────────────────────────────
    private static PlItem RowReport(RateIndex idx, InvIndex ix, DebtRow r, Alloc alloc, string acctLabel)
    {
        var sale = alloc.Sale;
        var wKey = Shamsi.Key(r.DateShamsi);
        var parts = alloc.Events.Select(ev =>
        {
            var sameDay = wKey != 0 && Shamsi.Key(ev.Date) == wKey;
            var rr = RateAt(idx, r.Fuel, ev.Date);
            if (rr.Missing && ev.LockRate > 0m)
                rr = new RateOn(ev.LockRate, ev.Date, true, false, true, "");
            var refRate = rr.Rate;
            var noRate = !(refRate > 0m);
            var noSale = !(sale > 0m);
            var diff = !sameDay && !noRate && !noSale && refRate > sale ? refRate - sale : 0m;
            return new PlPart(ev.Qty, ev.Date, ev.Hdr, ev.InvNo, sameDay,
                              refRate, rr.On, rr.Locked, noRate, noSale, rr.Why,
                              noSale ? 0m : sale * ev.Qty,
                              noRate ? 0m : refRate * ev.Qty,
                              diff * ev.Qty);
        }).OrderBy(p => Shamsi.Key(p.Date)).ToList();

        var remain = alloc.Fuel - alloc.Settled;
        var isInv = r.InvoiceId is not null;
        var invNo = isInv && ix.ById.TryGetValue(r.InvoiceId!.Value, out var v) ? v.InvoiceNumber : 0;
        return new PlItem(r.Fuel, r.DateShamsi ?? "", alloc.Fuel, sale, parts,
                          remain > Eps ? remain : 0m, isInv, invNo, r.Name ?? "", acctLabel);
    }

    // ── ۵) گزارشِ یک قرض‌دار (حسابِ اصلی + همهٔ فرعی‌ها، هر دو دفتر) ────────
    private static (List<PlItem> Items, PlTotals T) PersonReport(RateIndex idx, InvIndex ix, Debtor p)
    {
        var items = new List<PlItem>();
        void Each(DebtAccount? acct, string label)
        {
            if (acct is null) return;
            foreach (var (rows, money) in new[] { (acct.FuelRows, false), (acct.MoneyRows, true) })
            {
                if (rows is null || rows.Count == 0) continue;
                var alloc = Allocate(acct, rows, money, ix);
                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    if (r is null || !(r.Liters > 0m)) continue;   // ردیفِ بی‌لیتر، برداشتِ تیل نیست
                    items.Add(RowReport(idx, ix, r, alloc[i], label));
                }
            }
        }
        Each(p.MainAccount, "");
        foreach (var s in p.SubAccounts)
            if (s is not null) Each(s, string.IsNullOrWhiteSpace(s.Name) ? "حساب" : s.Name!);
        items = items.OrderBy(i => Shamsi.Key(i.Date)).ToList();

        var t = new PlTotals { N = items.Count };
        foreach (var it in items)
        {
            if (it.IsInvoice) t.InvRowN++; else t.SingleN++;
            t.OpenFuel += it.Remain;
            foreach (var pr in it.Parts)
            {
                if (pr.NoRate || pr.NoSale) { t.NoRateN++; continue; }
                t.PaidN++;
                t.Fuel += pr.Qty; t.Principal += pr.Principal; t.RefValue += pr.RefValue;
                if (pr.SameDay) t.SameDayN++;
                if (pr.Loss > 0m) { t.Loss += pr.Loss; t.LossN++; }
            }
        }
        return (items, t);
    }
}
