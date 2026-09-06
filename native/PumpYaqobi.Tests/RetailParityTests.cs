using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// برابریِ عددیِ «چکنه» با نسخهٔ HTML.
/// ۲۰۰ حالتِ تصادفیِ قطعی که <c>native/tools/gen-golden.mjs</c> با صدا زدنِ
/// خودِ <c>_chakanaBardagi</c> در یک کرومیومِ واقعی ساخته است.
/// </summary>
public class RetailParityTests
{
    private sealed record Row(string date, string name, bool byMoney, double fuel,
                              double priceper, double bardagi, double rasid);
    private sealed record PerRow(double bardagi);
    private sealed record Case(List<Row> rows, List<PerRow> perRow,
                               double totBord, double totRasid, double totAlb);

    private static List<Case> Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-chakana.json");
        if (!File.Exists(p)) p = "golden-chakana.json";
        return JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(p))!;
    }

    private static RetailRow Map(Row r) => new()
    {
        DateShamsi = r.date,
        Name = r.name,
        ByMoney = r.byMoney,
        Liters = (decimal)r.fuel,
        PricePerLiter = (decimal)r.priceper,
        Bardagi = (decimal)r.bardagi,
        Rasid = (decimal)r.rasid,
    };

    [Fact]
    public void RowBardagi_MatchesTheHtmlExactly()
    {
        var svc = new RetailService();
        var n = 0;
        foreach (var c in Load())
            for (var i = 0; i < c.rows.Count; i++, n++)
                Assert.Equal(c.perRow[i].bardagi, (double)svc.Bardagi(Map(c.rows[i])), 6);
        Assert.True(n > 500, "دادهٔ طلایی کم است: " + n);
    }

    [Fact]
    public void Totals_MatchTheHtmlExactly()
    {
        var svc = new RetailService();
        foreach (var c in Load())
        {
            var s = svc.Summarize(c.rows.Select(Map));
            Assert.Equal(c.totBord, (double)s.Bardagi, 6);
            Assert.Equal(c.totRasid, (double)s.Rasid, 6);
            Assert.Equal(c.totAlb, (double)s.Albaqi, 6);
        }
    }

    [Fact]
    public void MoneyRow_UsesItsOwnBardagi_NotLitresTimesPrice()
    {
        var svc = new RetailService();
        var r = new RetailRow { ByMoney = true, Bardagi = 500, Liters = 100, PricePerLiter = 60 };
        Assert.Equal(500m, svc.Bardagi(r));       // نه ۶۰۰۰
        Assert.Equal(0m, svc.Summarize(new[] { r }).Liters);   // ردیفِ پولی لیتر ندارد
    }
}
