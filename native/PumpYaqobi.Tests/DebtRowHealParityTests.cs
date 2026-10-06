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
        return JsonSerializer.Deserialize<List<Case>>(SrcText.Read(p))!;
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
            //  درمانِ «پولیِ خراب» مو‌به‌مو همان نسخهٔ وب است
            Assert.Equal(c.byMoney, r.ByMoney);
            //  ⛔ (۱۴۰۵/۰۷/۲۲) گردِ پول دو رقمِ اعشار است، نه افغانیِ درستِ نسخهٔ وب — صاحب ریپو:
            //  «بردگی تیل آره با اعشاریه حساب بشه و با همون اعشاریه هم ضرب.» پس عدد از خودِ
            //  ورودی‌ها حساب می‌شود، و همان عدد گردشده به افغانی هنوز با نسخهٔ وب یک واحد فاصله ندارد.
            var b = c.before;
            var raw = r.ByMoney ? (decimal)b.bardagi
                    : b.fuel <= 0 ? 0m
                    : (decimal)b.fuel * (b.priceper.ValueKind == JsonValueKind.Number ? (decimal)b.priceper.GetDouble() : 0m);
            var want = DebtCalculationService.RoundMoney(raw);
            Assert.Equal(want, r.Bardagi);
            Assert.Equal(DebtCalculationService.RoundMoney(want - (decimal)b.rasid), r.Albaqi);
            Assert.True(Math.Abs((double)r.Bardagi - c.bardagi) <= 0.5 + 1e-6, $"بردگی {r.Bardagi} از نسخهٔ وب ({c.bardagi}) بیش از نیم افغانی دور است");
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
