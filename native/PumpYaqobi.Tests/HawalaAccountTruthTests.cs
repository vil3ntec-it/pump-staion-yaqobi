using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «تیل دارد، هم حواله هم طرف، اما می‌گوید تیل ندارد» (۱۴۰۵/۰۷/۲۸) ══════════════
/// گزارشِ صاحب ریپو. حسابِ تیلی (رسیدِ تیل، برداشت با فی) دفترِ پولش همیشه «تمام» است —
/// پولی رسید نشده و بردگی هست — و ‎HawalaCalc.AccountOut‎ همان را «قرض‌دار تیل ندارد»
/// می‌خواند. ⛔ فقط دفترِ خودِ حساب (‎Mode‎) و فقط تیلِ همان حواله سقف است.
/// </summary>
public class HawalaAccountTruthTests
{
    private sealed class Rates : IUnionRateProvider { public decimal UnionRate(FuelType fuel) => 60m; }

    private static DebtRow Take(decimal liters, string hawala, FuelType f = FuelType.Petrol) => new()
    {
        Hawala = hawala, Liters = liters, Fuel = f, DateKey = 14050710,
        Bardagi = liters * 68m, Albaqi = liters * 68m,
    };

    [Fact]
    public void FuelAccount_WithFuelLeft_HawalaIsNotBurned()
    {
        var calc = new DebtCalculationService(new Rates());
        var a = new DebtAccount { Mode = LedgerMode.Fuel, RasidFuelPetrol = 550m, ReceiptsMigrated = true };
        a.FuelRows.Add(Take(106m, "733"));
        a.FuelRows.Add(Take(400m, ""));
        var raw = calc.Status(new[] { a });
        //  ریشه: دفترِ پولِ حسابِ تیلی (رسیدِ پول صفر، بردگی با فی) همیشه «تمام» است
        Assert.Equal(DebtStatus.Out, raw.Money);
        var st = HawalaCalc.ForMode(raw, a.Mode);
        var q = new HawalaQuota { Number = "733", Liters = 300m };
        var s = HawalaCalc.Of(q, a.FuelRows, st);
        //  ۵۵۰ رسید، ۵۰۶ برده ⇒ ۴۴ لیتر هنوز دارد؛ حواله ۱۹۴ لیتر مانده و باز است
        Assert.NotEqual(HawalaCalc.State.Burned, s.State);
        Assert.Equal(194m, s.Left);
        Assert.False(s.Alert);
    }

    [Fact]
    public void FuelAccount_ReallyOutOfFuel_StillBurns()
    {
        var calc = new DebtCalculationService(new Rates());
        var a = new DebtAccount { Mode = LedgerMode.Fuel, RasidFuelPetrol = 500m, ReceiptsMigrated = true };
        a.FuelRows.Add(Take(106m, "733"));
        a.FuelRows.Add(Take(400m, ""));
        var st = HawalaCalc.ForMode(calc.Status(new[] { a }), a.Mode);
        var s = HawalaCalc.Of(new HawalaQuota { Number = "733", Liters = 300m }, a.FuelRows, st);
        //  ۵۰۰ رسید، ۵۰۶ برده ⇒ واقعاً تیل ندارد ⇒ «سوخت»، همان قاعدهٔ پیشین
        Assert.Equal(HawalaCalc.State.Burned, s.State);
    }

    [Fact]
    public void MoneyAccount_UsesMoneyLedgerOnly()
    {
        var calc = new DebtCalculationService(new Rates());
        var a = new DebtAccount { Mode = LedgerMode.Money, ReceiptsMigrated = true };
        a.FuelRows.Add(Take(10m, "5"));
        var raw = calc.Status(new[] { a });
        var st = HawalaCalc.ForMode(raw, a.Mode);
        Assert.Equal(DebtStatus.None, st.Petrol);
        Assert.Equal(DebtStatus.None, st.Diesel);
        Assert.Equal(raw.Money, st.Money);
    }

    [Theory]
    [InlineData(10, false)]   // ۲۰٪
    [InlineData(69, false)]
    [InlineData(70, true)]    // «وقتی به ۷۰ تا ۱۰۰ رفت» ⇒ نقطهٔ سرخ
    [InlineData(100, true)]
    public void RedDot_From70Percent(int used, bool alert)
    {
        var a = new DebtAccount { Mode = LedgerMode.Fuel, RasidFuelPetrol = 1000m, ReceiptsMigrated = true };
        a.FuelRows.Add(Take(used, "1"));
        var calc = new DebtCalculationService(new Rates());
        var st = HawalaCalc.ForMode(calc.Status(new[] { a }), a.Mode);
        Assert.Equal(alert, HawalaCalc.Of(new HawalaQuota { Number = "1", Liters = 100m }, a.FuelRows, st).Alert);
    }
}
