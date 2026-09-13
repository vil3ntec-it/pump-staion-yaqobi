using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class NoRates : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// ══ عددِ کیو‌آر = عددِ خودِ برنامه ═══════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «کیو‌آر که اسکن می‌شود باید <b>دقیقاً</b> حسابِ همان طرف
/// را — همان‌طور که در برنامهٔ نیتیو است — نشان دهد، نه یک چیزِ اشتباه.»
///
/// پس این‌جا عددهای عکسِ حساب با همان فرمولی سنجیده می‌شوند که سربرگِ صفحهٔ
/// شخص از آن می‌خواند (‎PersonViewModel.Remainder‎):
///
///     فیصدی  = رسید × ٪         (٪ِ هر تیل، جدا از آن یکی)
///     الباقی = بردگی + فیصدی − رسید
///
/// و «رسید» در دفترِ تیل ستونِ ‎RasidFuel‎ است و در دفترِ پول ستونِ ‎Rasid‎ —
/// دو دفترِ کاملاً جدا.
/// </summary>
public class AcctQrNumbersTests
{
    private static DebtCalculationService Calc() => new(new NoRates());

    /// <summary>همان راهی که خودِ برنامه نوشتهٔ کادر را دوباره عدد می‌کند.</summary>
    private static decimal Num(string text) => PumpYaqobi.Application.Localization.Shamsi.Num(text);

    private static string Box(AcctSnapshot s, string label) =>
        s.Summary.First(b => b[0] == label)[1];

    /// <summary>دفترِ تیل: «رسید تیل» خوانده می‌شود و فیصدی در الباقی هست.</summary>
    [Fact]
    public void FuelLedgerUsesTheFuelReceiptColumnAndTheCommission()
    {
        var acct = new DebtAccount { Mode = LedgerMode.Fuel, PercentPetrol = 10m, PercentDiesel = 0m };
        acct.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 1000m, RasidFuel = 400m, Rasid = 7777m });
        acct.FuelRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 500m, RasidFuel = 100m });

        // ⚠️ چهار عددِ ‎Rasid…‎ی حساب فقط «کش»ِ جمعِ ردیف‌هایند؛ اگر کیو‌آر
        // آن‌ها را هم اضافه کند، رسید دو برابر می‌شود.
        Calc().SyncReceiptTotals(acct);

        var snap = AcctSnapshots.ForDebtAccount("هارون", null, acct, Calc());

        Assert.Equal(1500m, Num(Box(snap, "جمله بردگی")));   // لیتر، نه افغانی
        Assert.Equal(500m, Num(Box(snap, "جمله رسید")));     // RasidFuel، نه Rasid
        Assert.Equal(40m, Num(Box(snap, "فیصدی ما")));       // 400×۱۰٪ ؛ دیزل ٪ ندارد
        // پطرول: 1000 + 40 − 400 = 640 ؛ دیزل: 500 + 0 − 100 = 400
        Assert.Equal(1040m, Num(Box(snap, "الباقی")));

        // و ستونِ آخرِ جدول هم همان ستونِ دفترِ تیل است
        Assert.Equal("رسید تیل", snap.Head[^1]);
        Assert.Equal("400", new string(snap.Rows[0][^1].Where(char.IsDigit).ToArray()));
    }

    /// <summary>دفترِ پول: «بردگی» افغانی است و «رسید» ستونِ پولی.</summary>
    [Fact]
    public void MoneyLedgerUsesTheMoneyColumns()
    {
        var acct = new DebtAccount { Mode = LedgerMode.Money, PercentPetrol = 0m, PercentDiesel = 0m };
        acct.MoneyRows.Add(new DebtRow { Fuel = FuelType.Petrol, ByMoney = true, Bardagi = 9000m, Rasid = 2000m });

        var snap = AcctSnapshots.ForDebtAccount("هارون", null, acct, Calc());

        Assert.Equal(9000m, Num(Box(snap, "جمله بردگی")));
        Assert.Equal(2000m, Num(Box(snap, "جمله رسید")));
        Assert.Equal(7000m, Num(Box(snap, "الباقی")));
        Assert.Equal("رسید", snap.Head[^1]);
        Assert.Equal("افغانی", snap.Unit);
    }

    /// <summary>
    /// ⚠️ دو دفتر هرگز قاطی نمی‌شوند. حسابِ «واحد تیل» که ردیفِ پولی هم دارد،
    /// باید فقط دفترِ تیلش را نشان دهد — وگرنه افغانی با لیتر جمع می‌شد.
    /// همان باگی که در کیو‌آرِ کارتِ قرض‌دار بود.
    /// </summary>
    [Fact]
    public void TheTwoLedgersNeverMix()
    {
        var acct = new DebtAccount { Mode = LedgerMode.Fuel };
        acct.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 200m, RasidFuel = 50m });
        acct.MoneyRows.Add(new DebtRow { Fuel = FuelType.Petrol, ByMoney = true, Bardagi = 999999m, Rasid = 888888m });

        var snap = AcctSnapshots.ForDebtAccount("هارون", null, acct, Calc());

        Assert.Single(snap.Rows);
        Assert.Equal(200m, Num(Box(snap, "جمله بردگی")));
        Assert.Equal(50m, Num(Box(snap, "جمله رسید")));
        Assert.Equal(150m, Num(Box(snap, "الباقی")));
    }

    /// <summary>
    /// فیصدیِ پطرول و دیزل هیچ ربطی به هم ندارند — هر کدام روی الباقیِ خودش.
    /// </summary>
    [Fact]
    public void EachFuelUsesItsOwnPercent()
    {
        var acct = new DebtAccount { Mode = LedgerMode.Fuel, PercentPetrol = 5m, PercentDiesel = 20m };
        acct.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 0m, RasidFuel = 1000m });
        acct.FuelRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 0m, RasidFuel = 1000m });

        var snap = AcctSnapshots.ForDebtAccount("هارون", null, acct, Calc());

        // 1000×۵٪ + 1000×۲۰٪ = 250
        Assert.Equal(250m, Num(Box(snap, "فیصدی ما")));
        // (0+50−1000) + (0+200−1000) = −1750
        Assert.Equal(-1750m, Num(Box(snap, "الباقی")));
    }
}
