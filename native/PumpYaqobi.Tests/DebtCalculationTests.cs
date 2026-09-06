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
/// این آزمون‌ها «رفتارِ نسخهٔ HTML» را قفل می‌کنند، نه سلیقهٔ من را.
/// هر عددِ انتظاری از روی همان فرمولِ استخراج‌شده از index.html حساب شده و
/// نامِ تابعِ اصلی در توضیحِ هر آزمون آمده است.
/// </summary>
public class DebtCalculationTests
{
    private static DebtCalculationService Svc(decimal p = 56m, decimal d = 60m)
        => new(new FixedRates(p, d));

    // ── _personRowBardagi ────────────────────────────────────────────────────
    [Fact] // fuel > 0 ? fuel * fee : 0
    public void RowBardagi_IsLitersTimesManualFee()
        => Assert.Equal(600m, Svc().RowBardagi(new DebtRow { Liters = 10m, PricePerLiter = 60m }));

    [Fact] // فیِ خالی ⇒ بردگیِ صفر (نرخِ خودکار عمداً حذف شده بود)
    public void RowBardagi_WithoutFee_IsZero()
        => Assert.Equal(0m, Svc().RowBardagi(new DebtRow { Liters = 10m, PricePerLiter = null }));

    [Fact] // لیترِ صفر یا منفی ⇒ صفر
    public void RowBardagi_ZeroLiters_IsZero()
        => Assert.Equal(0m, Svc().RowBardagi(new DebtRow { Liters = 0m, PricePerLiter = 60m }));

    [Fact] // ردیفِ دفترِ پول بردگی‌اش دستی است، از لیتر×فی نمی‌آید
    public void RowBardagi_MoneyRow_UsesStoredValue()
        => Assert.Equal(1234m, Svc().RowBardagi(
            new DebtRow { ByMoney = true, Bardagi = 1234m, Liters = 99m, PricePerLiter = 5m }));

    // ── فیصدی: پطرول و دیزل کاملاً جدا ───────────────────────────────────────
    [Fact]
    public void Percent_PetrolAndDiesel_AreIndependent()
    {
        var a = new DebtAccount(); var s = Svc();
        s.SetPercent(a, FuelType.Petrol, 3m);
        s.SetPercent(a, FuelType.Diesel, 7m);
        Assert.Equal(3m, s.PercentOf(a, FuelType.Petrol));
        Assert.Equal(7m, s.PercentOf(a, FuelType.Diesel));
    }

    [Fact] // حسابِ قدیمی که فقط percent دارد ⇒ هر دو سوخت همان را می‌خوانند
    public void Percent_LegacyAccount_FallsBackToShared()
    {
        var a = new DebtAccount { PercentLegacy = 5m }; var s = Svc();
        Assert.Equal(5m, s.PercentOf(a, FuelType.Petrol));
        Assert.Equal(5m, s.PercentOf(a, FuelType.Diesel));
    }

    [Fact] // «هر دو» = میان‌بُر
    public void Percent_Both_SetsEach()
    {
        var a = new DebtAccount(); var s = Svc();
        s.SetPercent(a, null, 4m);
        Assert.Equal(4m, s.PercentOf(a, FuelType.Petrol));
        Assert.Equal(4m, s.PercentOf(a, FuelType.Diesel));
    }

    [Fact] // اولین دست‌زدن ⇒ فیصدیِ مشترکِ قدیمی صریح می‌شود و دیگر زنده نمی‌شود
    public void Percent_FirstWrite_SplitsLegacyValue()
    {
        var a = new DebtAccount { PercentLegacy = 5m }; var s = Svc();
        s.SetPercent(a, FuelType.Diesel, 9m);
        Assert.Equal(5m, s.PercentOf(a, FuelType.Petrol));   // پطرول دست‌نخورده
        Assert.Equal(9m, s.PercentOf(a, FuelType.Diesel));
    }

    // ── _splitTotals ─────────────────────────────────────────────────────────
    [Fact]
    public void SplitTotals_KeepsPetrolAndDieselApart()
    {
        var t = Svc().SplitTotals(new[]
        {
            new DebtRow { Fuel = FuelType.Petrol, Liters = 10m, Rasid = 100m, Albaqi = 5m },
            new DebtRow { Fuel = FuelType.Diesel, Liters =  3m, Rasid =  50m, Albaqi = 2m },
        });
        Assert.Equal(10m, t.Petrol.Liters);
        Assert.Equal(3m,  t.Diesel.Liters);
        Assert.Equal(13m, t.All.Liters);
        Assert.Equal(7m,  t.All.Albaqi);
    }

    // ── الباقی ───────────────────────────────────────────────────────────────
    [Fact] // لیترِ الباقی = بردهٔ تیل − رسیدِ تیل، هر سوخت جدا
    public void Balances_SubtractFuelReceiptsPerFuel()
    {
        var acc = new DebtAccount { RasidFuelPetrol = 4m, RasidFuelDiesel = 1m };
        acc.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 10m, Albaqi = 300m });
        acc.FuelRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 5m,  Albaqi = 200m });
        var b = Svc().Balances(new[] { acc });
        Assert.Equal(6m, b.Petrol);
        Assert.Equal(4m, b.Diesel);
        Assert.Equal(10m, b.Fuel);
        Assert.Equal(500m, b.Money);
    }

    [Fact] // ⚠️ دفترِ پول نباید «تیلِ مصرف‌شده» را زیاد کند (کارِ _noFuel)
    public void Balances_MoneyLedgerDoesNotAddLiters()
    {
        var acc = new DebtAccount();
        acc.MoneyRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 999m, Albaqi = 100m, ByMoney = true });
        var b = Svc().Balances(new[] { acc });
        Assert.Equal(0m, b.Petrol);
        Assert.Equal(100m, b.Money);
    }

    [Fact] // حسابِ اصلی و فرعی با هم جمع می‌شوند
    public void Balances_IncludeSubAccounts()
    {
        var p = new Debtor();
        p.MainAccount.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 10m });
        var sub = new DebtAccount { LegacySubId = "s1" };
        sub.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 5m });
        p.SubAccounts.Add(sub);
        Assert.Equal(15m, Svc().Balances(p.AllAccounts()).Petrol);
    }

    // ── حالِ کارت (debtFuelStatus) ────────────────────────────────────────────
    [Fact] // رسیدِ صفر ولی برده ⇒ همان لحظه «تمام‌شده»
    public void Status_ZeroDepositButUsed_IsOut()
    {
        var acc = new DebtAccount();
        acc.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 10m });
        Assert.Equal(DebtStatus.Out, Svc().Status(new[] { acc }).Petrol);
    }

    [Fact] // مانده ≤ ۲۰٪ ⇒ «کم مانده»
    public void Status_TwentyPercentOrLess_IsLow()
    {
        var acc = new DebtAccount { RasidFuelPetrol = 100m };
        acc.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 80m });
        Assert.Equal(DebtStatus.Low, Svc().Status(new[] { acc }).Petrol);
    }

    [Fact]
    public void Status_PlentyLeft_IsOk()
    {
        var acc = new DebtAccount { RasidFuelPetrol = 100m };
        acc.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 10m });
        Assert.Equal(DebtStatus.Ok, Svc().Status(new[] { acc }).Petrol);
    }

    [Fact] // هیچ حرکتی نبوده ⇒ هیچ رنگی
    public void Status_NothingAtAll_IsNone()
        => Assert.Equal(DebtStatus.None, Svc().Status(new[] { new DebtAccount() }).Worst);

    [Fact] // پطرولِ تمام‌شده نباید دیزلِ سالم را خراب نشان دهد و برعکس
    public void Status_FuelsAreReportedSeparately()
    {
        var acc = new DebtAccount { RasidFuelPetrol = 0m, RasidFuelDiesel = 100m };
        acc.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 10m });
        acc.FuelRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 10m });
        var st = Svc().Status(new[] { acc });
        Assert.Equal(DebtStatus.Out, st.Petrol);
        Assert.Equal(DebtStatus.Ok,  st.Diesel);
        Assert.Equal(DebtStatus.Out, st.Worst);
    }

    // ── بندِ ۸: معادلِ سوخت ──────────────────────────────────────────────────
    [Fact] // ۱۵۰۰۰ ÷ ۵۶ = ۲۶۷٫۸۶  (مثالِ خودِ صاحب ریپو)
    public void MoneyToLiters_MatchesTheStatedExample()
        => Assert.Equal(267.86m, Svc(56m, 60m).MoneyToLiters(15000m, FuelType.Petrol));

    [Fact] // با عوض شدنِ نرخ، معادل خودش عوض می‌شود (ذخیره نمی‌شود)
    public void MoneyToLiters_FollowsTheUnionRate()
        => Assert.Equal(250m, Svc(60m, 60m).MoneyToLiters(15000m, FuelType.Petrol));

    [Fact] // نرخِ صفر ⇒ صفر، نه تقسیم بر صفر
    public void MoneyToLiters_ZeroRate_IsZeroNotCrash()
        => Assert.Equal(0m, Svc(0m, 0m).MoneyToLiters(15000m, FuelType.Petrol));

    // ── بندِ ۱۰: پطرول/دیزل دیگر رشته نیستند ─────────────────────────────────
    [Fact]
    public void LegacyFuelStrings_MapExactlyLikeTheHtml()
    {
        Assert.Equal(FuelType.Diesel, FuelTypeExtensions.FromLegacy("diesel"));
        Assert.Equal(FuelType.Petrol, FuelTypeExtensions.FromLegacy("petrol"));
        Assert.Equal(FuelType.Petrol, FuelTypeExtensions.FromLegacy(null));    // مثل HTML
        Assert.Equal(FuelType.Petrol, FuelTypeExtensions.FromLegacy(""));      // مثل HTML
        Assert.Equal(FuelType.Petrol, FuelTypeExtensions.FromLegacy("garbage"));
    }
}
