using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class Rates : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// ══ آزمونِ برابریِ عددی با نسخهٔ HTML ══════════════════════════════════════
/// بندِ ۷: «منطق مالی نباید تغییر کند.»
///
/// این‌جا حرفِ من ملاک نیست. ۲۰۰ حالتِ تصادفی (ولی قطعی) ساخته شد و داخلِ خودِ
/// index.html در یک کرومیومِ واقعی به توابعِ اصلیِ برنامه داده شد:
///     _debtBalancesOf · debtFuelStatus · _personRowBardagi
/// خروجی‌شان در golden-from-html.json ذخیره شد و این آزمون همان ورودی‌ها را به
/// C# می‌دهد و عددها را مو‌به‌مو مقایسه می‌کند.
///
/// اگر روزی کسی فرمولی را «بهتر» کند، همین آزمون قرمز می‌شود.
/// </summary>
public class GoldenParityTests
{
    private sealed record Row(string ftype, double fuel, double priceper, double rasid,
                              double rasidFuel, double albaqi, double bardagi, bool byMoney = false);
    private sealed record Acc(List<Row> rows, List<Row> moneyRows, double rasidFuelP, double rasidFuelD,
                              double rasidMoneyP, double rasidMoneyD, string mode,
                              double? percentP, double? percentD, double? percent);
    private sealed record Bal(double money, double petrol, double diesel, double fuel);
    private sealed record St(string worst, string petrol, string diesel, string money);
    private sealed record Case(List<Acc> accs, Bal bal, St st, List<double> rowBardagi);

    private static List<Case> Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-from-html.json");
        if (!File.Exists(p)) p = "golden-from-html.json";
        return JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(p))!;
    }

    private static DebtAccount ToAccount(Acc a)
    {
        var acc = new DebtAccount
        {
            Mode = LedgerModeExtensions.FromLegacy(a.mode),
            RasidFuelPetrol = (decimal)a.rasidFuelP,
            RasidFuelDiesel = (decimal)a.rasidFuelD,
            RasidMoneyPetrol = (decimal)a.rasidMoneyP,
            RasidMoneyDiesel = (decimal)a.rasidMoneyD,
            PercentPetrol = a.percentP.HasValue ? (decimal)a.percentP.Value : null,
            PercentDiesel = a.percentD.HasValue ? (decimal)a.percentD.Value : null,
            PercentLegacy = a.percent.HasValue ? (decimal)a.percent.Value : null,
        };
        foreach (var r in a.rows) acc.FuelRows.Add(ToRow(r, false));
        foreach (var r in a.moneyRows) acc.MoneyRows.Add(ToRow(r, true));
        return acc;
    }

    private static DebtRow ToRow(Row r, bool money) => new()
    {
        Fuel = FuelTypeExtensions.FromLegacy(r.ftype),
        Liters = (decimal)r.fuel,
        PricePerLiter = (decimal)r.priceper,
        Rasid = (decimal)r.rasid,
        RasidFuel = (decimal)r.rasidFuel,
        Albaqi = (decimal)r.albaqi,
        Bardagi = (decimal)r.bardagi,
        ByMoney = money,
    };

    private static DebtStatus ParseStatus(string s) => s switch
    {
        "out" => DebtStatus.Out, "low" => DebtStatus.Low,
        "ok" => DebtStatus.Ok, _ => DebtStatus.None
    };

    [Fact]
    public void Balances_MatchTheHtmlExactly()
    {
        var svc = new DebtCalculationService(new Rates());
        int n = 0;
        foreach (var c in Load())
        {
            var accs = c.accs.Select(ToAccount).ToList();
            var got = svc.Balances(accs);
            Assert.Equal((decimal)c.bal.money,  got.Money,  2);
            Assert.Equal((decimal)c.bal.petrol, got.Petrol, 2);
            Assert.Equal((decimal)c.bal.diesel, got.Diesel, 2);
            Assert.Equal((decimal)c.bal.fuel,   got.Fuel,   2);
            n++;
        }
        Assert.Equal(200, n);
    }

    [Fact]
    public void Status_MatchesTheHtmlExactly()
    {
        var svc = new DebtCalculationService(new Rates());
        foreach (var c in Load())
        {
            var got = svc.Status(c.accs.Select(ToAccount));
            Assert.Equal(ParseStatus(c.st.petrol), got.Petrol);
            Assert.Equal(ParseStatus(c.st.diesel), got.Diesel);
            Assert.Equal(ParseStatus(c.st.money),  got.Money);
            Assert.Equal(ParseStatus(c.st.worst),  got.Worst);
        }
    }

    [Fact]
    public void RowBardagi_MatchesTheHtmlExactly()
    {
        var svc = new DebtCalculationService(new Rates());
        foreach (var c in Load())
        {
            var rows = c.accs[0].rows;
            for (int i = 0; i < rows.Count; i++)
                Assert.Equal((decimal)c.rowBardagi[i], svc.RowBardagi(ToRow(rows[i], false)), 2);
        }
    }
}
