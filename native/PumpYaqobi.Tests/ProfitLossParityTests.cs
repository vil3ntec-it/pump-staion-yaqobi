using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// «مفاد / ضرر» — همان اعدادی که ‎calcPL‎ در یک کرومیومِ واقعی می‌دهد.
///
/// جای لغزشش «اختلافِ نرخِ ثبت و تاییدِ فاکتور» است: یک عددِ علامت‌دار که
/// مثبتش به مصارف می‌رود و منفی‌اش به درآمدها، ولی هر دو کادر باید مثبت
/// بمانند و «خالص = درآمد − مصارف» درست در بیاید.
/// </summary>
public class ProfitLossParityTests
{
    private sealed record Shift(double profit);
    private sealed record Report(string fuel, string date, Shift day, Shift night);
    private sealed record DieselShift(string fuel, string date, double profit);
    private sealed record NoInvRow(string date, double bardagi);
    private sealed record NoInvPerson(List<NoInvRow> rows);
    private sealed record Amounted(string date, double amount);
    private sealed record Inv(bool by_money, string status, double liters,
                              double price_per_liter, double? rate_on_create,
                              double rate_on_approve, string approved_at);
    private sealed record Bd(double petrol, double diesel, double noinv, double extra,
                             double exp, double invDiff);
    private sealed record Case(List<Report> reports, List<DieselShift> shifts,
                               List<NoInvPerson> noinv, List<Amounted> extras,
                               List<Amounted> exps, List<Inv> invs,
                               double manualIn, double manualExp,
                               double diff, Bd bd, double income, double expenses, double net);
    private sealed record BulkCase(double qty, double buy, double market, double seller, double income);
    private sealed record Golden(List<Case> cases, List<BulkCase> bulk);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-profit.json");
        if (!File.Exists(p)) p = "golden-profit.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    private static void Close(double expected, decimal actual, string what)
    {
        var tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Assert.True(Math.Abs(expected - (double)actual) <= tol,
            $"{what}: انتظار {expected} بود، {actual} آمد");
    }

    private static ProfitInput Build(Case c)
    {
        var reports = c.reports.Select(r => new ParchaReport
        {
            Fuel = FuelType.Petrol, DateShamsi = r.date,
            DayShift = new ShiftData { Profit = (decimal)r.day.profit },
            NightShift = new ShiftData { Profit = (decimal)r.night.profit },
        }).Concat(c.shifts.Select(s => new ParchaReport
        {
            Fuel = FuelType.Diesel, DateShamsi = s.date,
            DayShift = new ShiftData { Profit = (decimal)s.profit },
        })).ToList();

        return new ProfitInput
        {
            Reports = reports,
            NoInvoiceAccounts = c.noinv.Select(p => new DebtAccount
            {
                FuelRows = p.rows.Select(r => new DebtRow
                { DateShamsi = r.date, Bardagi = (decimal)r.bardagi }).ToList(),
            }).ToList(),
            ExtraIncomes = c.extras.Select(e => new ExtraIncome
            { DateShamsi = e.date, Amount = (decimal)e.amount }).ToList(),
            Expenses = c.exps.Select(e => new Expense
            { DateShamsi = e.date, Amount = (decimal)e.amount }).ToList(),
            Invoices = c.invs.Select(v => new Invoice
            {
                ByMoney = v.by_money,
                Status = v.status == "approved" ? InvoiceStatus.Approved : InvoiceStatus.Pending,
                Liters = (decimal)v.liters,
                PricePerLiter = (decimal)v.price_per_liter,
                RateOnCreate = v.rate_on_create is null ? null : (decimal)v.rate_on_create.Value,
                RateOnApprove = (decimal)v.rate_on_approve,
            }).ToList(),
        };
    }

    [Fact]
    public void InvoiceRateDifference_MatchesTheWeb()
    {
        foreach (var c in Load().cases)
            Close(c.diff, ProfitLossService.InvoiceRateDiff(Build(c).Invoices), "اختلافِ نرخِ فاکتور");
    }

    [Fact]
    public void Breakdown_MatchesTheWeb()
    {
        foreach (var c in Load().cases)
        {
            var b = ProfitLossService.Breakdown(Build(c));
            Close(c.bd.petrol, b.Petrol, "مفادِ پطرول");
            Close(c.bd.diesel, b.Diesel, "مفادِ دیزل");
            Close(c.bd.noinv, b.NoInvoice, "بی‌فاکتورها");
            Close(c.bd.extra, b.Extra, "درآمدِ اضافی");
            Close(c.bd.exp, b.Expenses, "مصارف");
            Close(c.bd.invDiff, b.InvoiceRateDiff, "اختلافِ نرخِ فاکتور");
        }
    }

    [Fact]
    public void IncomeExpensesAndNet_MatchTheWeb()
    {
        foreach (var c in Load().cases)
        {
            var r = ProfitLossService.Compute(Build(c), (decimal)c.manualIn, (decimal)c.manualExp);
            Close(c.income, r.Income, "درآمدها");
            Close(c.expenses, r.Expenses, "مصارف");
            Close(c.net, r.Net, "خالص");
            // هر دو کادر همیشه مثبت‌اند و خالص واقعاً تفریقشان است
            Assert.Equal(r.Income - r.Expenses, r.Net);
        }
    }

    [Fact]
    public void BulkBuy_MatchesTheWeb()
    {
        foreach (var b in Load().bulk)
        {
            var (seller, income) = ProfitLossService.BulkBuy(
                (decimal)b.qty, (decimal)b.buy, (decimal)b.market);
            Close(b.seller, seller, "پولِ فروشنده");
            Close(b.income, income, "درآمدِ اضافی");
        }
    }

    /// <summary>
    /// نرخِ بالا رفته ⇒ ضرر ⇒ به مصارف. نرخِ پایین آمده ⇒ مفاد ⇒ به درآمدها.
    /// این را جدا هم قفل می‌کنیم چون علامتش آسان برعکس می‌شود.
    /// </summary>
    [Fact]
    public void ARisenRateIsALoss_AndAFallenRateIsAGain()
    {
        ProfitInput With(decimal onCreate, decimal onApprove) => new()
        {
            Invoices = new[] { new Invoice
            {
                Status = InvoiceStatus.Approved, Liters = 100,
                RateOnCreate = onCreate, RateOnApprove = onApprove,
            } },
        };

        var loss = ProfitLossService.Compute(With(60, 70));
        Assert.Equal(1000m, loss.Expenses);      // (۷۰−۶۰)×۱۰۰ به مصارف
        Assert.Equal(0m, loss.Income);
        Assert.Equal(-1000m, loss.Net);

        var gain = ProfitLossService.Compute(With(70, 60));
        Assert.Equal(1000m, gain.Income);        // به درآمدها، و باز هم مثبت
        Assert.Equal(0m, gain.Expenses);
        Assert.Equal(1000m, gain.Net);
    }
}
