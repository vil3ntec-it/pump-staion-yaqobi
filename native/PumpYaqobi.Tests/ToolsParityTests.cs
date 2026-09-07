using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class ZeroRates : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// ══ ابزارهای بندِ ۱۹ ═══════════════════════════════════════════════════════
/// میله‌زنی، قرض‌های کهنه، کمبودیِ کارمندان و گزارشِ ماهانه — همه با پاسخِ
/// خودِ <c>index.html</c> در یک کرومیومِ واقعی سنجیده می‌شوند
/// (<c>native/tools/gen-golden.mjs</c> → <c>golden-tools.json</c>).
///
/// این چهار بخش «فقط جمع می‌زنند»، و دقیقاً به همین دلیل خطایشان دیر پیدا
/// می‌شود: عددِ گزارشِ ماه را کسی همان روز با دست نمی‌شمارد.
/// </summary>
public class ToolsParityTests
{
    // ── شکلِ دادهٔ طلایی ────────────────────────────────────────────────────
    private sealed record DipCase(string fuel, double capacity, double book, double actual,
                                  double pct, double empty, double receivable,
                                  double diff, double allowed, bool over);

    private sealed record GRow(string date, string ftype, double fuel, double rasid,
                               double rasidFuel, double bardagi, double albaqi);

    private sealed record GAcct(List<GRow> rows, List<GRow> moneyRows,
                                double rasidFuelP, double rasidFuelD);

    private sealed record GPerson(string id, string name, string phone, string mode,
                                  List<GAcct> subs, List<GRow> rows, List<GRow> moneyRows,
                                  double rasidFuelP, double rasidFuelD);

    private sealed record AgingOut(string id, double albaqi, bool money,
                                   double bardagi, double rasid, int days);

    private sealed record AgingCase(string today, List<GPerson> persons,
                                    List<AgingOut> all, List<AgingOut> fuel, List<AgingOut> money);

    private sealed record GPump(int num, string fuel, double start, double end,
                                double pricePerLiter, double debt);
    private sealed record GTxn(string name, double liters, double amount, string type,
                               string fuel, bool? amountAuto);
    private sealed record GShift(string workerName, List<GPump> pumps, List<GTxn> transactions,
                                 double fabricDebt, double pricePerLiter, double pricePerLiterDiesel);
    private sealed record GWaraq(string id, string date, GShift? day, GShift? night);
    private sealed record GSettle(string id, string key, string name, double amount,
                                  string? type, string date, long at);
    private sealed record StaffOut(string key, string name, int shifts, double @short, double excess,
                                   double paidShort, double paidExcess,
                                   double remainShort, double remainExcess);
    private sealed record StaffCase(List<GWaraq> entries, List<GSettle> settles, List<StaffOut> rows);

    private sealed record MShift(double money, double sale, double profit);
    private sealed record MReport(string fuel, string date, MShift? day, MShift? night);
    private sealed record MDieselShift(string fuel, string date, double money, double sale, double profit);
    private sealed record MDated(string date, double amount);
    private sealed record MPurchase(string date, string fuelType, double liters, double totalAFN);
    private sealed record MSafe(string date, string currency, string type, double amount);
    private sealed record MTanker(string date, string fuel, double manifest, double actual);
    private sealed record MDb(List<MReport> reports, List<MDieselShift> shifts, List<MDated> expenses,
                              List<MDated> extraIncomes, List<MPurchase> fuelEntries,
                              List<MDated> debtQuickReceipts, List<MSafe> safeEntries,
                              List<MTanker> tankerLogs);
    private sealed record MFuel(double a, double l, double p, int c);
    private sealed record MBuy(double l, double a);
    private sealed record MOut(MFuel petrol, MFuel diesel, double exp, double extra,
                               MBuy buyP, MBuy buyD, double rasid, int rasidC,
                               double sin, double sout, int tkCount, double tkShort,
                               double sales, double liters, double profit, double net);
    private sealed record MonthCase(string today, MDb db, List<string> keys, string key,
                                    string prevKey, MOut cur, MOut prev, int? growth);

    private sealed record Golden(List<DipCase> dip, List<AgingCase> aging,
                                 List<StaffCase> staffShort, List<MonthCase> month);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-tools.json");
        if (!File.Exists(p)) p = "golden-tools.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    private static void Close(double expected, decimal actual)
    {
        var a = (double)actual;
        var tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Assert.True(Math.Abs(expected - a) <= tol, $"انتظار {expected} بود، {a} آمد");
    }

    private static FuelType F(string? s) => s == "diesel" ? FuelType.Diesel : FuelType.Petrol;

    // ── ۱) میله‌زنی ────────────────────────────────────────────────────────
    [Fact]
    public void TankDip_MatchesTheHtmlExactly()
    {
        var svc = new TankDipService();
        foreach (var c in Load().dip)
        {
            // ظرفیتِ صفر در دادهٔ طلایی یعنی «کاربر ننوشته» — همان‌جا هم
            // ‎__tankInfo‎ صفر داده بود، پس این‌جا هم عیناً صفر داده می‌شود.
            var t = new TankInfo(F(c.fuel), "", "", (decimal)c.book, (decimal)c.capacity,
                                 c.capacity > 0, 500m);
            var n = svc.Calc(t, (decimal)c.actual);
            Close(c.pct, n.Percent);
            Close(c.empty, n.Empty);
            Close(c.receivable, n.Receivable);
            Close(c.diff, n.Diff);
            Close(c.allowed, n.Allowed);
            Assert.Equal(c.over, n.Over);
        }
    }

    [Fact]
    public void DefaultCapacity_RoundsUpToTheNextThousand()
    {
        Assert.Equal(4000m, TankDipService.DefaultCapacity(0m, 1000m));
        Assert.Equal(12000m, TankDipService.DefaultCapacity(11200m, 500m));
        Assert.Equal(1000m, TankDipService.DefaultCapacity(0m, 0m));
    }

    /// <summary>
    /// ‎Math.round‎ِ جاوااسکریپت نصفه‌ها را به بالا می‌برد، حتی منفی‌ها را.
    /// اگر روزی کسی این را با ‎AwayFromZero‎ عوض کند، همین آزمون قرمز می‌شود.
    /// </summary>
    [Fact]
    public void JsRound_SendsNegativeHalvesUpwards()
    {
        Assert.Equal(-2m, TankDipService.JsRound(-2.5m));
        Assert.Equal(3m, TankDipService.JsRound(2.5m));
        Assert.Equal(-3m, TankDipService.JsRound(-2.6m));
    }

    // ── ۲) قرض‌های کهنه ────────────────────────────────────────────────────
    private static DebtRow Row(GRow r) => new()
    {
        DateShamsi = r.date, Fuel = F(r.ftype), Liters = (decimal)r.fuel,
        Rasid = (decimal)r.rasid, RasidFuel = (decimal)r.rasidFuel,
        Bardagi = (decimal)r.bardagi, Albaqi = (decimal)r.albaqi,
    };

    private static void Fill(DebtAccount a, List<GRow>? fuel, List<GRow>? money,
                             double rasidP, double rasidD)
    {
        foreach (var r in fuel ?? new()) a.FuelRows.Add(Row(r));
        foreach (var r in money ?? new()) a.MoneyRows.Add(Row(r));
        a.RasidFuelPetrol = (decimal)rasidP;
        a.RasidFuelDiesel = (decimal)rasidD;
    }

    private static Debtor Person(GPerson p)
    {
        var d = new Debtor { LegacyId = p.id, Name = p.name, Phone = p.phone };
        d.MainAccount.Mode = LedgerModeExtensions.FromLegacy(p.mode);
        Fill(d.MainAccount, p.rows, p.moneyRows, p.rasidFuelP, p.rasidFuelD);
        foreach (var s in p.subs ?? new())
        {
            // ⚠️ حسابِ فرعی است، پس ‎LegacySubId‎ لازم است — وگرنه ‎IsMain‎ راست
            // می‌شود. در نسخهٔ وب هم واحدِ زیرحساب از شخص به ارث می‌رسید.
            var a = new DebtAccount { LegacySubId = "s", Mode = d.MainAccount.Mode };
            Fill(a, s.rows, s.moneyRows, s.rasidFuelP, s.rasidFuelD);
            d.SubAccounts.Add(a);
        }
        return d;
    }

    private static void CheckAging(AgingService svc, AgingCase c, AgingFilter f, List<AgingOut> want)
    {
        var people = c.persons.Select(Person).ToList();
        var got = svc.Rows(people, f, c.today);
        Assert.Equal(want.Count, got.Count);
        for (var i = 0; i < want.Count; i++)
        {
            Assert.Equal(want[i].id, got[i].Person.LegacyId);
            Assert.Equal(want[i].money, got[i].Figures.IsMoney);
            Close(want[i].albaqi, got[i].Figures.Albaqi);
            Close(want[i].bardagi, got[i].Figures.Bardagi);
            Close(want[i].rasid, got[i].Figures.Rasid);
            Assert.Equal(want[i].days, got[i].DaysIdle);
        }
    }

    [Fact]
    public void Aging_MatchesTheHtmlExactly()
    {
        var svc = new AgingService(new DebtCalculationService(new ZeroRates()));
        foreach (var c in Load().aging)
        {
            CheckAging(svc, c, AgingFilter.All, c.all);
            CheckAging(svc, c, AgingFilter.Fuel, c.fuel);
            CheckAging(svc, c, AgingFilter.Money, c.money);
        }
    }

    // ── ۳) کمبودیِ کارمندان ───────────────────────────────────────────────
    private static WaraqShift Shift(GShift s, ShiftKind kind)
    {
        var sd = new WaraqShift
        {
            Kind = kind, WorkerName = s.workerName,
            FabricDebt = (decimal)s.fabricDebt,
            PricePerLiter = (decimal)s.pricePerLiter,
            PricePerLiterDiesel = (decimal)s.pricePerLiterDiesel,
        };
        foreach (var p in s.pumps ?? new())
            sd.Pumps.Add(new WaraqPump
            {
                Num = p.num, Fuel = F(p.fuel), Start = (decimal)p.start, End = (decimal)p.end,
                PricePerLiter = (decimal)p.pricePerLiter, Debt = (decimal)p.debt,
            });
        foreach (var t in s.transactions ?? new())
            sd.Transactions.Add(new WaraqTransaction
            {
                Name = t.name, Liters = (decimal)t.liters, Amount = (decimal)t.amount,
                Type = t.type == "expense" ? WaraqTxnType.Expense : WaraqTxnType.Debt,
                Fuel = F(t.fuel), AmountAuto = t.amountAuto,
            });
        return sd;
    }

    [Fact]
    public void StaffShortage_MatchesTheHtmlExactly()
    {
        var svc = new StaffShortService(new WaraqService());
        foreach (var c in Load().staffShort)
        {
            var entries = new List<WaraqEntry>();
            foreach (var w in c.entries)
            {
                var e = new WaraqEntry { LegacyId = w.id, DateShamsi = w.date };
                // ترتیب مهم است: نسخهٔ وب اول ‎day‎ را می‌بیند و بعد ‎night‎ —
                // و «نامِ ثبت‌شده» نامِ اولین شیفتی است که دیده شده.
                if (w.day is not null) e.Shifts.Add(Shift(w.day, ShiftKind.Day));
                if (w.night is not null) e.Shifts.Add(Shift(w.night, ShiftKind.Night));
                entries.Add(e);
            }

            var settles = c.settles.Select(s => new StaffShortSettle
            {
                LegacyId = s.id, NameKey = s.key, Name = s.name, Amount = (decimal)s.amount,
                // ثبتِ بی‌نوع «رسیدِ کمبودی» است — ‎(s.type || 'short')‎ .
                Kind = s.type == "excess" ? StaffSettleKind.Excess : StaffSettleKind.Short,
                DateShamsi = s.date,
            }).ToList();

            var got = svc.Rows(entries, settles);
            Assert.Equal(c.rows.Count, got.Count);
            for (var i = 0; i < c.rows.Count; i++)
            {
                Assert.Equal(c.rows[i].key, got[i].Key);
                Assert.Equal(c.rows[i].name, got[i].Name);
                Assert.Equal(c.rows[i].shifts, got[i].Shifts);
                Close(c.rows[i].@short, got[i].Short);
                Close(c.rows[i].excess, got[i].Excess);
                Close(c.rows[i].paidShort, got[i].PaidShort);
                Close(c.rows[i].paidExcess, got[i].PaidExcess);
                Close(c.rows[i].remainShort, got[i].RemainShort);
                Close(c.rows[i].remainExcess, got[i].RemainExcess);
            }
        }
    }

    // ── ۴) گزارش ماهانه ───────────────────────────────────────────────────
    private static MonthReportSource Source(MDb db)
    {
        var reports = new List<ParchaReport>();
        foreach (var r in db.reports)
            reports.Add(new ParchaReport
            {
                Fuel = F(r.fuel), DateShamsi = r.date,
                DayShift = r.day is null ? null : new ShiftData
                { Money = (decimal)r.day.money, Sale = (decimal)r.day.sale, Profit = (decimal)r.day.profit },
                NightShift = r.night is null ? null : new ShiftData
                { Money = (decimal)r.night.money, Sale = (decimal)r.night.sale, Profit = (decimal)r.night.profit },
            });

        // ⚠️ دیزل در نسخهٔ وب در ‎DB.shifts‎ بود و هر رکوردش یک شیفتِ تنها —
        // این‌جا همان می‌شود یک پارچهٔ دیزل با فقط شیفتِ روز. جمع‌ها و شمارشش
        // مو‌به‌مو یکی است.
        foreach (var s in db.shifts)
            reports.Add(new ParchaReport
            {
                Fuel = F(s.fuel), DateShamsi = s.date,
                DayShift = new ShiftData
                { Money = (decimal)s.money, Sale = (decimal)s.sale, Profit = (decimal)s.profit },
            });

        return new MonthReportSource(
            reports,
            db.expenses.Select(e => new Expense { DateShamsi = e.date, Amount = (decimal)e.amount }).ToList(),
            db.extraIncomes.Select(e => new ExtraIncome { DateShamsi = e.date, Amount = (decimal)e.amount }).ToList(),
            db.fuelEntries.Select(e => new FuelPurchase
            {
                DateShamsi = e.date, Fuel = F(e.fuelType),
                Liters = (decimal)e.liters, TotalAfn = (decimal)e.totalAFN,
            }).ToList(),
            db.debtQuickReceipts.Select(r => new DebtQuickReceipt
            { DateShamsi = r.date, Amount = (decimal)r.amount }).ToList(),
            db.safeEntries.Select(e => new SafeEntry
            {
                DateShamsi = e.date,
                Currency = e.currency == "usd" ? Currency.Usd : Currency.Afn,
                Kind = e.type == "bardagi" ? SafeEntryKind.Bardagi : SafeEntryKind.Mandagi,
                Amount = (decimal)e.amount,
            }).ToList(),
            db.tankerLogs.Select(t => new TankerUnload
            {
                DateShamsi = t.date, Fuel = F(t.fuel),
                Manifest = (decimal)t.manifest, Actual = (decimal)t.actual,
            }).ToList());
    }

    private static void CheckMonth(MOut want, MonthReport got)
    {
        Close(want.petrol.a, got.Petrol.Amount);
        Close(want.petrol.l, got.Petrol.Liters);
        Close(want.petrol.p, got.Petrol.Profit);
        Assert.Equal(want.petrol.c, got.Petrol.Parcha);
        Close(want.diesel.a, got.Diesel.Amount);
        Close(want.diesel.l, got.Diesel.Liters);
        Close(want.diesel.p, got.Diesel.Profit);
        Assert.Equal(want.diesel.c, got.Diesel.Parcha);
        Close(want.exp, got.Expenses);
        Close(want.extra, got.Extra);
        Close(want.buyP.l, got.BuyPetrol.Liters);
        Close(want.buyP.a, got.BuyPetrol.Amount);
        Close(want.buyD.l, got.BuyDiesel.Liters);
        Close(want.buyD.a, got.BuyDiesel.Amount);
        Close(want.rasid, got.Rasid);
        Assert.Equal(want.rasidC, got.RasidCount);
        // ‎sin‎ در نسخهٔ وب جمعِ ردیف‌های «بردگی» است و ‎sout‎ بقیه — نامش آن‌جا
        // «ورود/خروج» بود، ولی عددش همین است.
        Close(want.sin, got.SafeBardagi);
        Close(want.sout, got.SafeMandagi);
        Assert.Equal(want.tkCount, got.TankerCount);
        Close(want.tkShort, got.TankerShort);
        Close(want.sales, got.Sales);
        Close(want.liters, got.Liters);
        Close(want.profit, got.Profit);
        Close(want.net, got.Net);
    }

    [Fact]
    public void MonthReport_MatchesTheHtmlExactly()
    {
        var svc = new MonthReportService();
        foreach (var c in Load().month)
        {
            var src = Source(c.db);
            Assert.Equal(c.keys, svc.AllKeys(src, c.today));
            Assert.Equal(c.prevKey, MonthReportService.PrevKey(c.key));
            CheckMonth(c.cur, svc.Compute(src, c.key));
            CheckMonth(c.prev, svc.Compute(src, c.prevKey));
            Assert.Equal(c.growth, MonthReportService.Growth(
                (decimal)c.cur.net, (decimal)c.prev.net));
        }
    }

    [Fact]
    public void PrevKey_WrapsToTheYearBefore()
    {
        Assert.Equal("1404/12", MonthReportService.PrevKey("1405/01"));
        Assert.Equal("1405/05", MonthReportService.PrevKey("1405/06"));
    }
}
