using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، الف۲ — تبخیرِ امانت با ‎decimal‎ (۱۴۰۵/۰۷/۱۹) ═══════════════════════
///
/// پیش از این ‎TempFactor‎ و ‎Loss‎ با ‎double‎ حساب می‌شدند. چهارصد حالتِ
/// ‎AmanatParityTests‎ (پاسخِ خودِ سایت) همچنان سبزند؛ این‌جا سه چیزِ دیگر:
/// ضریب‌های توانِ درست <b>دقیق</b>اند، عددِ امروز (گردشده به دو رقم) با همان
/// فرمولِ ‎double‎ِ پیشین یکی است، و هیچ ‎double‎ در خودِ سرویس نمانده.
/// </summary>
public class AmanatDecimalTests
{
    private static readonly AmanatService Calc = new();

    [Theory]
    [InlineData(20, 1)]
    [InlineData(30, 2)]
    [InlineData(40, 4)]
    [InlineData(10, 0.5)]
    [InlineData(0, 0.25)]
    public void ZaribeDama_TavaneDorost_Daghigh_Ast(int temp, double want)
    {
        var f = Calc.TempFactor(temp, AmanatSettings.Default);
        Assert.Equal((decimal)want, f);
    }

    /// <summary>فرمولِ پیشین، مو‌به‌مو — مرجعِ «عددِ امروز».</summary>
    private static decimal OldLoss(decimal l, decimal d, decimal t, FuelType fuel, decimal p, AmanatSettings s)
    {
        var step = s.TDouble > 0m ? s.TDouble : 10m;
        var f = Math.Pow(2, (double)(t - s.RefTemp) / (double)step);
        var ff = fuel == FuelType.Diesel ? s.FDiesel : s.FPetrol;
        var loss = (double)l * (double)(p / 100m) * (double)(d / 30m) * f * (double)ff * (double)s.TankFactor;
        if (!double.IsFinite(loss) || loss < 0) loss = 0;
        var res = (decimal)loss;
        return res > l ? l : res;
    }

    public static IEnumerable<object[]> Cases()
    {
        decimal[] temps = [5m, 18m, 20m, 25m, 33.5m];
        decimal[] days = [1m, 7m, 30m, 95m];
        var i = 0;
        foreach (var t in temps)
            foreach (var d in days)
            {
                var fuel = i++ % 2 == 0 ? FuelType.Petrol : FuelType.Diesel;
                yield return [12_500m + i * 731m, d, t, fuel];
            }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AdadeEmruz_BaDoRaghham_HamanAst(decimal liters, decimal days, decimal temp, FuelType fuel)
    {
        var s = AmanatSettings.Default;
        var now = Calc.Loss(liters, days, temp, fuel, null, s);
        var old = OldLoss(liters, days, temp, fuel, s.BasePct, s);
        Assert.Equal(Math.Round(old, 2, MidpointRounding.AwayFromZero),
                     Math.Round(now, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void LitereBozorg_Va_DamayeBala_NemiShekanad()
    {
        var s = AmanatSettings.Default;
        Assert.Equal(1e20m, Calc.Loss(1e20m, 3000m, 2000m, FuelType.Petrol, 50m, s));
        Assert.Equal(0m, Calc.Loss(1000m, 30m, -2000m, FuelType.Petrol, null, s));
    }

    [Fact]
    public void DarKhodeSarvis_HichDoubleNist()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "PumpYaqobi.sln"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var src = File.ReadAllText(Path.Combine(root!, "PumpYaqobi.Application", "Services", "AmanatService.cs"));
        Assert.DoesNotContain("double", src);
        Assert.DoesNotContain("Math.Pow", src);
    }
}
