using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// خریدِ تیل و حالِ مخزن — ۳۰۰ خریدِ تصادفی از خودِ نسخهٔ وب.
/// چگالیِ صفر و لیترِ صفر عمداً در دادهٔ طلایی هستند، چون همان‌جا تقسیم بر صفر
/// کمین کرده است.
/// </summary>
public class StorageParityTests
{
    private sealed record Case(double kg, double density, double priceTon, double usdRate,
                               double ton, double liters, double totalUSD, double totalAFN, double perLiter);

    private static List<Case> Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-purchase.json");
        if (!File.Exists(p)) p = "golden-purchase.json";
        return JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(p))!;
    }

    private static void Close(double expected, decimal actual)
    {
        var a = (double)actual;
        var tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Assert.True(Math.Abs(expected - a) <= tol, $"انتظار {expected} بود، {a} آمد");
    }

    [Fact]
    public void PurchaseNumbers_MatchTheHtmlExactly()
    {
        var svc = new StorageService();
        foreach (var c in Load())
        {
            var n = svc.Compute((decimal)c.kg, (decimal)c.density, (decimal)c.priceTon, (decimal)c.usdRate);
            Close(c.ton, n.Ton);
            Close(c.liters, n.Liters);
            Close(c.totalUSD, n.TotalUsd);
            Close(c.totalAFN, n.TotalAfn);
            Close(c.perLiter, n.PerLiter);
        }
    }

    [Fact]
    public void ZeroDensity_GivesZeroLitres_NotInfinity()
    {
        var n = new StorageService().Compute(30000m, 0m, 700m, 70m);
        Assert.Equal(0m, n.Liters);
        Assert.Equal(0m, n.PerLiter);
    }

    [Fact]
    public void Tank_SubtractsBothShiftsOfEveryReport_AndFloorsTheDisplay()
    {
        var svc = new StorageService();
        var buys = new[] { new FuelPurchase { Liters = 1000, TotalUsd = 10, TotalAfn = 700 } };
        var reps = new[]
        {
            new ParchaReport { DayShift = new ShiftData { Sale = 700 }, NightShift = new ShiftData { Sale = 500 } },
        };
        var t = svc.Tank(buys, reps, lowThreshold: 100m);
        Assert.Equal(1000m, t.In);
        Assert.Equal(1200m, t.Out);
        Assert.Equal(-200m, t.Current);     // موجودیِ واقعی می‌تواند منفی باشد
        Assert.Equal(0m, t.Display);        // ولی عددِ نمایشی هرگز منفی نیست
        Assert.True(t.IsLow);
    }

    [Fact]
    public void EmptyTank_WithNoPurchasesAndNoSales_IsNotFlaggedLow()
    {
        var t = new StorageService().Tank(Array.Empty<FuelPurchase>(), Array.Empty<ParchaReport>(), 100m);
        Assert.False(t.IsLow);
    }
}
