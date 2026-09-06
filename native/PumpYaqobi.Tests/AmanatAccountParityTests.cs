using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// «تیل امانت» در سطحِ حساب — همان جمع‌هایی که کارتِ هر حساب نشان می‌دهد.
///
/// جای لغزشِ اصلی این است: «فیصدیِ لازم» در سطحِ حساب روی درصدِ بخارِ کلِ
/// حساب حساب می‌شود، نه جمعِ ردیف‌به‌ردیف. ۲۰۰ حسابِ تصادفی که خودِ
/// ‎amAccCalc‎ در یک کرومیومِ واقعی پاسخشان را داده.
/// </summary>
public class AmanatAccountParityTests
{
    private sealed record Settings(double basePct, double refTemp, double tDouble,
                                   double fPetrol, double fDiesel, double tankFactor,
                                   double handlingPct, double profitPct, double defTemp, double safetyPct);
    private sealed record Row(double days, double liters, JsonElement taken, JsonElement temp,
                              JsonElement basePct, JsonElement actual, string state,
                              string date, string closeDate);
    private sealed record Acc(string fuel, JsonElement myPct, JsonElement rate, List<Row> rows);
    private sealed record Tot(int n, int open, double liters, double litersOpen, double taken,
                              double loss, double lossPct, double rest, double share,
                              bool hasActual, double actual, double realLoss, double realDiff,
                              double? myPct, double? targetL, double? needPct, double? askPct,
                              double? askL, double? netIfMy, double? netIfAsk,
                              double? rate, double? lossMoney, double? myMoney, double? diffMoney);
    private sealed record Case(Acc acc, Tot t);
    private sealed record Golden(Settings settings, List<Case> cases);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-amanat-acc.json");
        if (!File.Exists(p)) p = "golden-amanat-acc.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    private static decimal? Opt(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number ? (decimal)e.GetDouble() : null;

    private static void Close(double? expected, decimal? actual, string what)
    {
        if (expected is null) { Assert.Null(actual); return; }
        Assert.NotNull(actual);
        var a = (double)actual!.Value;
        var tol = Math.Max(1e-6, Math.Abs(expected.Value) * 1e-9);
        Assert.True(Math.Abs(expected.Value - a) <= tol,
            $"{what}: انتظار {expected} بود، {a} آمد");
    }

    [Fact]
    public void AccountCalc_MatchesTheHtmlExactly()
    {
        var g = Load();
        var s = new AmanatSettings(
            (decimal)g.settings.basePct, (decimal)g.settings.refTemp, (decimal)g.settings.tDouble,
            (decimal)g.settings.fPetrol, (decimal)g.settings.fDiesel, (decimal)g.settings.tankFactor,
            (decimal)g.settings.handlingPct, (decimal)g.settings.profitPct,
            (decimal)g.settings.defTemp, (decimal)g.settings.safetyPct);
        var svc = new AmanatService();

        foreach (var k in g.cases)
        {
            var acc = new AmanatAccount
            {
                Fuel = k.acc.fuel == "diesel" ? FuelType.Diesel : FuelType.Petrol,
                MyPct = Opt(k.acc.myPct),
                Rate = Opt(k.acc.rate),
                Rows = k.acc.rows.Select(r => new AmanatRow
                {
                    Days = (decimal)r.days,
                    Liters = (decimal)r.liters,
                    Taken = Opt(r.taken),
                    Temp = Opt(r.temp),
                    BasePct = Opt(r.basePct),
                    Actual = Opt(r.actual),
                    State = r.state == "closed" ? AmanatRowState.Closed : AmanatRowState.Open,
                }).ToList(),
            };

            var t = svc.AccountCalc(acc, s, _ => 0m);
            Assert.Equal(k.t.n, t.Count);
            Assert.Equal(k.t.open, t.Open);
            Assert.Equal(k.t.hasActual, t.HasActual);
            Close(k.t.liters, t.Liters, "رسیدِ کل");
            Close(k.t.litersOpen, t.LitersOpen, "رسیدِ باز");
            Close(k.t.taken, t.Taken, "برده‌شده");
            Close(k.t.loss, t.Loss, "بخار");
            Close(k.t.lossPct, t.LossPct, "درصدِ بخار");
            Close(k.t.rest, t.Rest, "باقی تیل");
            Close(k.t.share, t.Share, "سهمِ من");
            Close(k.t.myPct, t.MyPct, "فیصدیِ من");
            Close(k.t.targetL, t.TargetL, "هدفِ لیتری");
            Close(k.t.needPct, t.NeedPct, "فیصدیِ لازم");
            Close(k.t.askPct, t.AskPct, "فیصدیِ گِردشده");
            Close(k.t.askL, t.AskL, "لیترِ فیصدیِ گِردشده");
            Close(k.t.netIfMy, t.NetIfMy, "به من می‌رسد");
            Close(k.t.netIfAsk, t.NetIfAsk, "به من می‌رسد با فیصدیِ گِردشده");
            Close(k.t.rate, t.Rate, "نرخ");
            Close(k.t.lossMoney, t.LossMoney, "پولِ بخار");
            Close(k.t.myMoney, t.MyMoney, "پولِ سهمِ من");
            Close(k.t.diffMoney, t.DiffMoney, "پولِ اختلاف");
            if (k.t.hasActual)
            {
                Close(k.t.actual, t.Actual, "موجودیِ واقعی");
                Close(k.t.realLoss, t.RealLoss, "کمبودیِ واقعی");
                Close(k.t.realDiff, t.RealDiff, "اختلافِ واقعی و تخمین");
            }
        }
    }
}
