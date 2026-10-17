using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ⛽ تیل چکنهٔ حواله (۱۴۰۵/۰۷/۲۵) ════════════════════════════════════════════
/// «حواله ۵۰ لیتر؛ یارو هر روز ۵ لیتر می‌برد… ببینم چند برداشته شده و چقدر مانده؛ ۷۰ درصد
/// که رفت و وقتی تمام شد نقطهٔ سرخ؛ چند حوالهٔ هم‌مدل روی هم اثر نگذارند.»
/// (سنجهٔ پنجرهٔ واقعی: ‎UiTests hawala‎)
/// </summary>
public class HawalaCalcTests
{
    private static DebtRow Row(string? hawala, decimal liters, int day = 1) =>
        new() { Hawala = hawala, Liters = liters, DateShamsi = $"1405/07/{day:00}", DateKey = 14050700 + day, Name = "روز " + day };

    [Fact]
    public void Takings_CountOnlyTheSameNumber()
    {
        var rows = new[] { Row("42", 5, 1), Row("۴۲", 5, 2), Row("43", 7, 3), Row("", 9, 4), Row("42 · 1234", 3, 5), Row("142", 4, 6) };
        var t = HawalaCalc.Takings("42", rows);
        Assert.Equal(3, t.Count);
        Assert.Equal(13m, t.Sum(x => x.Liters));
        Assert.Equal(new[] { 1, 2, 5 }, t.Select(x => x.DateKey - 14050700));
    }

    [Theory]
    [InlineData(50, 0, HawalaCalc.State.Ok)]
    [InlineData(50, 34, HawalaCalc.State.Ok)]
    [InlineData(50, 35, HawalaCalc.State.Warn)]     // ۷۰٪
    [InlineData(50, 49, HawalaCalc.State.Warn)]
    [InlineData(50, 50, HawalaCalc.State.Done)]
    [InlineData(50, 60, HawalaCalc.State.Done)]
    public void State_RedAt70AndDone(decimal total, decimal used, HawalaCalc.State want)
    {
        var s = HawalaCalc.Of(total, used);
        Assert.Equal(want, s.State);
        Assert.Equal(total - used, s.Left);
        Assert.Equal(want != HawalaCalc.State.Ok, s.Alert);
    }

    [Fact]
    public void Quota_UsesItsOwnRowsOnly()
    {
        var q = new HawalaQuota { Number = "7", Liters = 20m };
        var mine = new[] { Row("7", 5), Row("7", 5) };
        Assert.Equal(10m, HawalaCalc.Of(q, mine).Used);
        Assert.Equal(0m, HawalaCalc.Of(q, Array.Empty<DebtRow>()).Used);
    }

    /// <summary>ورق شمارهٔ حواله را در ستونِ حوالهٔ ردیف می‌نشاند — همان که این‌جا شمرده می‌شود.</summary>
    [Fact]
    public void WaraqName_FeedsTheSameColumn()
    {
        var hw = PostingService.ExtractHawala("محمد هارون حواله ۴۲ پطرول");
        Assert.True(HawalaCalc.Matches(hw.Num, "42"));
    }
}
