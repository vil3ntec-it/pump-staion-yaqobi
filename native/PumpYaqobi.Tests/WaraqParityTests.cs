using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ورقِ روزانه — «فیِ ورق»، مبلغِ خودکارِ ردیف‌ها، جمع‌ها و کمبودی.
/// ۲۵۰ ورقِ تصادفی که خودِ نسخهٔ وب پاسخشان را داده است.
///
/// این بخش سه‌حالتی است (‎amountAuto‎: true / false / تعریف‌نشده) و هر سه حالت
/// در دادهٔ طلایی هست — چون همین سه‌حالتی بودن جای اشتباه است.
/// </summary>
public class WaraqParityTests
{
    private sealed record Pump(int num, string fuel, double start, double end,
                               double pricePerLiter, double debt);
    private sealed record Txn(string name, double liters, double amount, string type,
                              string fuel, bool? amountAuto);
    private sealed record Shift(List<Pump> pumps, List<Txn> transactions, double fabricDebt,
                                double pricePerLiter, double pricePerLiterDiesel);
    private sealed record Tot(double petrolL, double dieselL, double sales,
                              double debt, double expenses, double declaredDebt);
    private sealed record Sh(double shortage, double excess, double declared, double covered);
    private sealed record Case(Shift before, double repP, double repD, Tot tot, Sh sh, List<Txn> after);

    private static List<Case> Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-waraq.json");
        if (!File.Exists(p)) p = "golden-waraq.json";
        return JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(p))!;
    }

    private static FuelType F(string s) => s == "diesel" ? FuelType.Diesel : FuelType.Petrol;

    private static WaraqShift Map(Shift s)
    {
        var sd = new WaraqShift
        {
            FabricDebt = (decimal)s.fabricDebt,
            PricePerLiter = (decimal)s.pricePerLiter,
            PricePerLiterDiesel = (decimal)s.pricePerLiterDiesel,
        };
        foreach (var p in s.pumps)
            sd.Pumps.Add(new WaraqPump
            {
                Num = p.num, Fuel = F(p.fuel), Start = (decimal)p.start, End = (decimal)p.end,
                PricePerLiter = (decimal)p.pricePerLiter, Debt = (decimal)p.debt,
            });
        foreach (var t in s.transactions)
            sd.Transactions.Add(new WaraqTransaction
            {
                Name = t.name, Liters = (decimal)t.liters, Amount = (decimal)t.amount,
                Type = t.type == "expense" ? WaraqTxnType.Expense : WaraqTxnType.Debt,
                Fuel = F(t.fuel), AmountAuto = t.amountAuto,
            });
        return sd;
    }

    private static void Close(double expected, decimal actual)
    {
        var a = (double)actual;
        var tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Assert.True(Math.Abs(expected - a) <= tol, $"انتظار {expected} بود، {a} آمد");
    }

    [Fact]
    public void RepresentativePrice_MatchesTheHtml()
    {
        var svc = new WaraqService();
        foreach (var c in Load())
        {
            var sd = Map(c.before);
            Close(c.repP, svc.RepPrice(sd, FuelType.Petrol));
            Close(c.repD, svc.RepPrice(sd, FuelType.Diesel));
        }
    }

    [Fact]
    public void ShiftTotalsAndShortage_MatchTheHtml()
    {
        var svc = new WaraqService();
        foreach (var c in Load())
        {
            var sd = Map(c.before);
            var t = svc.ShiftTotals(sd);
            Close(c.tot.petrolL, t.PetrolLiters);
            Close(c.tot.dieselL, t.DieselLiters);
            Close(c.tot.sales, t.Sales);
            Close(c.tot.debt, t.Debt);
            Close(c.tot.expenses, t.Expenses);
            Close(c.tot.declaredDebt, t.DeclaredDebt);

            var sh = svc.Shortage(t);
            Close(c.sh.shortage, sh.Shortage);
            Close(c.sh.excess, sh.Excess);
            Close(c.sh.declared, sh.Declared);
            Close(c.sh.covered, sh.Covered);
        }
    }

    [Fact]
    public void NormalizeTxns_LeavesTheseTheHtmlLeaves()
    {
        var svc = new WaraqService();
        foreach (var c in Load())
        {
            var sd = Map(c.before);
            svc.NormalizeTxns(sd);
            for (var i = 0; i < sd.Transactions.Count; i++)
            {
                Close(c.after[i].amount, sd.Transactions[i].Amount);
                Assert.Equal(c.after[i].amountAuto, sd.Transactions[i].AmountAuto);
            }
        }
    }

    [Fact]
    public void ManualAmount_IsNeverRecalculated()
    {
        var svc = new WaraqService();
        var sd = new WaraqShift { PricePerLiter = 60 };
        sd.Pumps.Add(new WaraqPump { Fuel = FuelType.Petrol, PricePerLiter = 70 });
        sd.Transactions.Add(new WaraqTransaction { Liters = 10, Amount = 123, AmountAuto = false });
        svc.NormalizeTxns(sd);
        Assert.Equal(123m, sd.Transactions[0].Amount);   // نه ۷۰۰
    }
}
