using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class FixedRates : IUnionRateProvider
{
    private readonly decimal _p, _d;
    public FixedRates(decimal p, decimal d) { _p = p; _d = d; }
    public decimal UnionRate(FuelType fuel) => fuel == FuelType.Diesel ? _d : _p;
}

/// <summary>
/// ══ ۷۰٪ · ۹۰٪ · تمام شد · اضافه داده شد — و مخزنی که می‌گوید چند لیتر مانده ═══
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «هشدارِ مخزن اومد، نگفت اون مقدار مونده، فقط
/// عدد رو گفت… اگه قرض‌داری به ۷۰ فیصد از حسابش می‌رسید بگه متوجه باشه، و تا
/// ۹۰، و اگه تموم شد بگه تموم شده، و اگه اضافه داد بازپرسی کنه که چرا اضافه
/// دادی… یک اعلامیه بده که تیل بگیرید یا به یارو تیل ندید… یکم ریس‌مدلی.»
/// </summary>
public class DebtUsageAlertTests
{
    private static DebtCalculationService Svc() => new(new FixedRates(56m, 60m));

    private static DebtAccount Acc(decimal deposit, decimal used)
    {
        var a = new DebtAccount { RasidFuelPetrol = deposit };
        if (used > 0m) a.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = used });
        return a;
    }

    /// <summary>درصد و مانده از همان سنجشِ حالِ کارت.</summary>
    [Fact]
    public void Usage_DarsadVaMande_AzHamanSanjesh()
    {
        var u = Svc().Usage(new[] { Acc(200m, 150m) }).Petrol;
        Assert.True(u.Any);
        Assert.Equal(75m, u.Percent);
        Assert.Equal(50m, u.Remaining);
        Assert.False(Svc().Usage(new[] { new DebtAccount() }).Petrol.Any);
    }

    private static Dictionary<string, object?> Person(string st, double dep, double used)
        => new()
        {
            ["id"] = 7L, ["name"] = "کریم", ["stP"] = st, ["stD"] = "none", ["stM"] = "none",
            ["use"] = new Dictionary<string, object?> { ["stP"] = new[] { dep, used } },
        };

    private static Dictionary<string, object?> One(Dictionary<string, object?> p)
        => (Dictionary<string, object?>)Assert.Single(StationSnapshot.Alerts(new List<object?> { p }, null))!;

    private static string S(Dictionary<string, object?> a, string k) => a[k] as string ?? "";

    [Theory]
    [InlineData("ok", 100.0, 60.0, "")]      // ۶۰٪ ⇒ ساکت
    [InlineData("ok", 100.0, 70.0, "w70")]   // ۷۰٪ ⇒ متوجه باشید
    [InlineData("low", 100.0, 85.0, "w70")]
    [InlineData("low", 100.0, 92.0, "w90")]  // ۹۰٪ ⇒ حساب را پر کند
    [InlineData("out", 100.0, 100.0, "out")] // تمام شد
    [InlineData("out", 100.0, 125.0, "over")] // اضافه داده شد ⇒ بازپرسی
    [InlineData("ok", 100.0, 130.0, "")]     // تسویه‌شده (حالِ کارت سالم) ⇒ ساکت
    public void Pele_Ha(string st, double dep, double used, string level)
    {
        var list = StationSnapshot.Alerts(new List<object?> { Person(st, dep, used) }, null);
        if (level.Length == 0) { Assert.Empty(list); return; }
        var a = (Dictionary<string, object?>)Assert.Single(list)!;
        Assert.Equal("d7-stP-" + level, S(a, "k"));
        Assert.Equal(level is "out" or "over" ? "out" : "low", S(a, "s"));
        Assert.False(string.IsNullOrWhiteSpace(S(a, "a")), "دستورِ کار نیامد");
    }

    [Fact]
    public void Matn_HaMande_RaMigooyad_VaDastoor_Dard()
    {
        var w70 = One(Person("ok", 200, 150));
        Assert.Contains("75٪", S(w70, "t"), StringComparison.Ordinal);
        Assert.Contains("50 لیتر مانده", S(w70, "t"), StringComparison.Ordinal);
        Assert.Contains("متوجهِ کریم باشید", S(w70, "a"), StringComparison.Ordinal);

        var w90 = One(Person("low", 200, 190));
        Assert.Contains("فقط 10 لیتر مانده", S(w90, "t"), StringComparison.Ordinal);
        Assert.Contains("بیشتر از 10 لیتر به او ندهید", S(w90, "a"), StringComparison.Ordinal);

        var out_ = One(Person("out", 200, 200));
        Assert.Contains("تمام شد", S(out_, "t"), StringComparison.Ordinal);
        Assert.Contains("دیگر پطرول ندهید", S(out_, "a"), StringComparison.Ordinal);

        var over = One(Person("out", 200, 225));
        Assert.Contains("25 لیتر بیشتر از حسابش", S(over, "t"), StringComparison.Ordinal);
        Assert.Contains("بازپرسی کنید", S(over, "a"), StringComparison.Ordinal);
        Assert.Contains("چرا", S(over, "a"), StringComparison.Ordinal);
    }

    /// <summary>عکسِ کهنه (بی ‎use‎) همان رفتارِ قبلی را دارد.</summary>
    [Fact]
    public void BiUse_HamanRaftareGhabli()
    {
        var p = new Dictionary<string, object?> { ["id"] = 3L, ["name"] = "نبی", ["stP"] = "low" };
        var a = One(p);
        Assert.Equal("d3-stP-low", S(a, "k"));
    }

    /// <summary>«فقط عدد رو گفت» — حالا «لیتر مانده» و حدِ هشدار و کارِ بعدی.</summary>
    [Fact]
    public void Makhzan_LiterMande_VaHad_VaDastoor()
    {
        var tank = new Dictionary<string, object?>
        {
            ["petrol"] = new Dictionary<string, object?> { ["low"] = true, ["show"] = 850.0, ["threshold"] = 1000.0 },
            ["diesel"] = new Dictionary<string, object?> { ["low"] = true, ["show"] = 0.0, ["threshold"] = 1000.0 },
        };
        var list = StationSnapshot.Alerts(null, tank).Cast<Dictionary<string, object?>>().ToList();
        Assert.Contains("850 لیتر مانده", S(list[0], "t"), StringComparison.Ordinal);
        Assert.Contains("حدِ هشدار 1,000 لیتر", S(list[0], "t"), StringComparison.Ordinal);
        Assert.Contains("سفارش بدهید", S(list[0], "a"), StringComparison.Ordinal);
        Assert.Contains("خالی شد", S(list[1], "t"), StringComparison.Ordinal);
        Assert.Contains("همین حالا دیزل سفارش بدهید", S(list[1], "a"), StringComparison.Ordinal);
    }

    /// <summary>دستورِ کار تا سرور می‌رود و بدهیِ شرکت‌ها هم.</summary>
    [Fact]
    public void DastoorVaBedehi_BeServer_Miravand()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var pub = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Services", "StationPublisher.cs"));
        Assert.Contains("a = a.Action", pub, StringComparison.Ordinal);
        Assert.Contains("StationSnapshot.OweAsync(", pub, StringComparison.Ordinal);
        var link = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Services", "CloudLink.cs"));
        Assert.Contains("body[\"owe\"]", link, StringComparison.Ordinal);
        var item = AlertWatch.FromSnapshot(new List<object?>
        {
            new Dictionary<string, object?> { ["k"] = "d1-stP-w70", ["s"] = "low", ["t"] = "x", ["a"] = "متوجه باشید" },
        });
        Assert.Equal("متوجه باشید", Assert.Single(item).Action);
    }
}
