using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// «خوددرمانیِ ردیفِ قرض‌دار» — گردکردنِ بردگی و الباقی و درستِ کردنِ ردیفِ
/// اشتباه علامت‌خوردهٔ «پولی». ۶۰۰ ردیفِ تصادفی که خودِ نسخهٔ وب پاسخشان را
/// داده است (‎_round0‎ · ‎_personRowBardagi‎ در یک کرومیومِ واقعی).
///
/// اهمیتش: عددِ «الباقی» روی همین می‌ایستد و هر انحرافِ یک واحدی، حسابِ
/// قرض‌دار را با نسخه‌ای که کاربر سال‌ها دیده فرق می‌دهد.
/// </summary>
public class DebtRowHealParityTests
{
    private sealed record Before(bool byMoney, string ftype, double fuel,
                                 JsonElement priceper, double bardagi, double rasid, double albaqi);
    private sealed record Case(Before before, bool byMoney, double bardagi, double albaqi);

    private static List<Case> Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-debtrow.json");
        if (!File.Exists(p)) p = "golden-debtrow.json";
        return JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(p))!;
    }

    private static DebtRow Map(Before b) => new()
    {
        ByMoney = b.byMoney,
        Fuel = b.ftype == "diesel" ? FuelType.Diesel : FuelType.Petrol,
        Liters = (decimal)b.fuel,
        // «فیِ خالی» در نسخهٔ وب رشتهٔ '' است؛ اینجا null یعنی همان.
        PricePerLiter = b.priceper.ValueKind == JsonValueKind.Number
            ? (decimal)b.priceper.GetDouble() : null,
        Bardagi = (decimal)b.bardagi,
        Rasid = (decimal)b.rasid,
        Albaqi = (decimal)b.albaqi,
    };

    [Fact]
    public void NormalizeRow_MatchesTheHtmlExactly()
    {
        var svc = new DebtCalculationService(new ZeroRates());
        var n = 0;
        foreach (var c in Load())
        {
            var r = Map(c.before);
            svc.NormalizeRow(r);
            Assert.Equal(c.byMoney, r.ByMoney);
            Assert.Equal(c.bardagi, (double)r.Bardagi, 6);
            Assert.Equal(c.albaqi, (double)r.Albaqi, 6);
            n++;
        }
        Assert.Equal(600, n);
    }

    [Fact]
    public void Round0_RoundsHalvesAwayFromZero_LikeToFixed()
    {
        Assert.Equal(3m, DebtCalculationService.Round0(2.5m));
        Assert.Equal(-3m, DebtCalculationService.Round0(-2.5m));   // ⚠️ نه ۲- 
        Assert.Equal(2m, DebtCalculationService.Round0(2.4m));
    }

    private sealed class ZeroRates : IUnionRateProvider
    {
        public decimal UnionRate(FuelType fuel) => 0m;
    }
}
