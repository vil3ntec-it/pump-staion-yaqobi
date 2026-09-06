using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// چهار عددِ هر پارچه (فروش، پول، فایده، رسیده) مو‌به‌مو مثلِ ‎saveShift‎.
/// ۴۰۰ شیفتِ تصادفی از خودِ نسخهٔ وب.
/// </summary>
public class ParchaParityTests
{
    private sealed record Case(double start, double end, double price, double profitPer,
                               double debt, double sale, double money, double profit, double available);

    private static List<Case> Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-parcha.json");
        if (!File.Exists(p)) p = "golden-parcha.json";
        return JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(p))!;
    }

    private static void Close(double expected, decimal actual)
    {
        var a = (double)actual;
        var tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Assert.True(Math.Abs(expected - a) <= tol, $"انتظار {expected} بود، {a} آمد");
    }

    [Fact]
    public void ShiftNumbers_MatchTheHtmlExactly()
    {
        var svc = new ParchaService();
        foreach (var c in Load())
        {
            var n = svc.Compute((decimal)c.start, (decimal)c.end, (decimal)c.price,
                                (decimal)c.profitPer, (decimal)c.debt);
            Close(c.sale, n.Sale);
            Close(c.money, n.Money);
            Close(c.profit, n.Profit);
            Close(c.available, n.Available);
        }
    }

    [Fact]
    public void EndLowerThanStart_IsRejected_LikeTheHtml()
    {
        Assert.False(ParchaService.IsValid("احمد", 100m, 90m));
        Assert.False(ParchaService.IsValid("", 100m, 200m));
        Assert.True(ParchaService.IsValid("احمد", 100m, 100m));
    }

    [Fact]
    public void Summarize_AddsBothShiftsOfEveryReport()
    {
        var svc = new ParchaService();
        var rep = new ParchaReport
        {
            DayShift = new ShiftData { Start = 0, End = 100, Price = 60, ProfitPer = 2, Debt = 1000 },
            NightShift = new ShiftData { Start = 100, End = 300, Price = 60, ProfitPer = 2, Debt = 500 },
        };
        var t = svc.Summarize(new[] { rep });
        Assert.Equal(300m, t.Sale);
        Assert.Equal(18000m, t.Money);
        Assert.Equal(600m, t.Profit);
        Assert.Equal(1500m, t.Debt);
        Assert.Equal(16500m, t.Available);
    }
}
