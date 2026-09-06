using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// «تیل امانت» — بخار، سهم، فیصدیِ لازم و باقیِ تیل.
/// ۴۰۰ ردیفِ تصادفی که خودِ ‎amRowCalc‎ در یک کرومیومِ واقعی پاسخشان را داده.
///
/// این پرریسک‌ترین فرمولِ برنامه است: توانِ ۲، تقسیمِ اعشاری و چند قاعدهٔ
/// «فقط یک‌جا شمرده شود». برابریِ عددی این‌جا از هر جای دیگری مهم‌تر است.
/// </summary>
public class AmanatParityTests
{
    private sealed record Settings(double basePct, double refTemp, double tDouble,
                                   double fPetrol, double fDiesel, double tankFactor,
                                   double handlingPct, double profitPct, double defTemp, double safetyPct);
    private sealed record Acc(string fuel, JsonElement myPct);
    private sealed record Row(double days, double liters, JsonElement taken, JsonElement temp,
                              JsonElement basePct, JsonElement actual, string state,
                              string date, string closeDate);
    private sealed record Calc(double liters, double taken, double days, double temp, double @base,
                               bool closed, double loss, double lossPct, double rest,
                               bool hasActual, double? actual, double? realLoss, double? diff,
                               double? myPct, double? targetL, double? needPct, double? askPct,
                               double? netIfMy, double? askL, double? netIfAsk);
    private sealed record Case(Acc acc, Row row, Calc c);
    private sealed record Golden(Settings settings, List<Case> cases);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-amanat.json");
        if (!File.Exists(p)) p = "golden-amanat.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    private static decimal? Opt(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number ? (decimal)e.GetDouble() : null;

    private static void Close(double? expected, decimal? actual, string what)
    {
        if (expected is null) { Assert.Null(actual); return; }
        Assert.NotNull(actual);
        var a = (double)actual!.Value;
        // بردباریِ نسبی: نسخهٔ وب با double حساب می‌کند و این‌جا با decimal
        var tol = Math.Max(1e-6, Math.Abs(expected.Value) * 1e-9);
        Assert.True(Math.Abs(expected.Value - a) <= tol,
            $"{what}: انتظار {expected} بود، {a} آمد");
    }

    [Fact]
    public void RowCalc_MatchesTheHtmlExactly()
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
            };
            var row = new AmanatRow
            {
                Days = (decimal)k.row.days,
                Liters = (decimal)k.row.liters,
                Taken = Opt(k.row.taken),
                Temp = Opt(k.row.temp),
                BasePct = Opt(k.row.basePct),
                Actual = Opt(k.row.actual),
                State = k.row.state == "closed" ? AmanatRowState.Closed : AmanatRowState.Open,
            };

            var c = svc.RowCalc(row, acc, s, autoDays: 0m);
            Close(k.c.loss, c.Loss, "بخار");
            Close(k.c.lossPct, c.LossPct, "درصدِ بخار");
            Close(k.c.rest, c.Rest, "باقی تیل");
            Close(k.c.targetL, c.TargetL, "سهمِ من");
            Close(k.c.needPct, c.NeedPct, "فیصدیِ لازم");
            Close(k.c.askPct, c.AskPct, "فیصدیِ گِردشده");
            Close(k.c.netIfMy, c.NetIfMy, "به من می‌رسد");
            Close(k.c.askL, c.AskL, "لیترِ فیصدیِ گِردشده");
            Close(k.c.netIfAsk, c.NetIfAsk, "به من می‌رسد با فیصدیِ گِردشده");
            Close(k.c.realLoss, c.RealLoss, "کمبودیِ واقعی");
            Close(k.c.diff, c.Diff, "اختلافِ واقعی و تخمین");
            Assert.Equal(k.c.hasActual, c.HasActual);
            Assert.Equal(k.c.closed, c.Closed);
        }
    }

    [Fact]
    public void EvaporationIsTakenOnlyFromYourShare_NotFromTheCustomersFuel()
    {
        // گزارشِ صاحب ریپو: «فقط برای "به من می‌رسد" کم بشه بسه».
        var svc = new AmanatService();
        var s = AmanatSettings.Default;
        var acc = new AmanatAccount { Fuel = FuelType.Petrol, MyPct = 3m };
        var row = new AmanatRow { Liters = 100000m, Days = 30m, Temp = 25m };

        var c = svc.RowCalc(row, acc, s, 0m);
        Assert.Equal(100000m - 3000m, c.Rest);            // بخار در «باقی تیل» نیست
        Assert.Equal(3000m - c.Loss, c.NetIfMy);          // بخار فقط از سهمِ خودتان
    }

    [Fact]
    public void RequiredPercent_IsTargetPlusEvaporation()
    {
        var svc = new AmanatService();
        var acc = new AmanatAccount { Fuel = FuelType.Petrol, MyPct = 3m };
        var row = new AmanatRow { Liters = 100000m, Days = 30m, Temp = 25m };
        var c = svc.RowCalc(row, acc, AmanatSettings.Default, 0m);
        Assert.Equal(3m + c.LossPct, c.NeedPct);
        Assert.True(c.AskPct >= c.NeedPct);               // به بالا گِرد می‌شود
    }

    [Fact]
    public void LossNeverExceedsTheFuelItself()
    {
        var svc = new AmanatService();
        var s = AmanatSettings.Default with { BasePct = 500m };
        Assert.Equal(1000m, svc.Loss(1000m, 3650m, 50m, FuelType.Petrol, null, s));
    }
}

/// <summary>
/// حاضری — ساعتِ کارِ شیفتِ شب. این تنها جای این بخش است که می‌تواند اشتباه
/// شود و در نسخهٔ وب هم با یک شرطِ صریح درست شده بود.
/// </summary>
public class AttendanceTests
{
    [Fact]
    public void NightShift_WrapsPastMidnight()
    {
        var svc = new AttendanceService();
        Assert.Equal(12m, svc.Hours(new AttendanceRow { In = "19:00", Out = "07:00" }));
        Assert.Equal(12m, svc.Hours(new AttendanceRow { In = "07:00", Out = "19:00" }));
        Assert.Equal(0m, svc.Hours(new AttendanceRow { In = "07:00" }));
        Assert.Equal(0m, svc.Hours(new AttendanceRow()));
        Assert.Equal(0.5m, svc.Hours(new AttendanceRow { In = "23:45", Out = "00:15" }));
    }

    [Fact]
    public void PersianDigits_AreAccepted()
    {
        var svc = new AttendanceService();
        Assert.Equal(2m, svc.Hours(new AttendanceRow { In = "۰۸:۰۰", Out = "۱۰:۰۰" }));
    }
}
