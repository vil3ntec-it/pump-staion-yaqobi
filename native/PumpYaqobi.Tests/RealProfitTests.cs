using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ 💰 سودِ واقعی — فروش جدا از سود، با قیمتِ خریدِ واقعیِ مخزن (۱۴۰۵/۰۷/۱۸) ══
/// </summary>
public class RealProfitTests
{
    private static WaraqSaleLine Line(int key, decimal pl, decimal pPrice, decimal dl = 0, decimal dPrice = 0) =>
        new($"{key / 10000}/{key / 100 % 100:00}/{key % 100:00}", key, ShiftKind.Day, pl, pl * pPrice, dl, dl * dPrice);

    [Fact]
    public void Forush_MenhayeKharid_MenhayeMasaref()
    {
        var prices = new List<BuyPrice> { new(FuelType.Petrol, 14050701, 1, 70m) };
        var r = RealProfitService.Compute(new[] { Line(14050705, 100m, 80m) }, prices, 0, 0, 300m);
        Assert.Equal(8000m, r.Sales);           // مبلغِ فروش
        Assert.Equal(7000m, r.Cost);            // بهای خرید
        Assert.Equal(1000m, r.Gross);           // سودِ ناخالص
        Assert.Equal(700m, r.Net);              // سودِ خالص
        Assert.Equal(10m, r.Petrol.ProfitPerLiter);
        Assert.Equal(0m, r.UnpricedLiters);
    }

    [Fact]
    public void GheymateKharid_AzTazeTarinKharideTaHamanRuz()
    {
        var prices = new List<BuyPrice>
        {
            new(FuelType.Petrol, 14050701, 1, 70m),
            new(FuelType.Petrol, 14050710, 2, 75m),
            new(FuelType.Diesel, 14050701, 3, 80m),
        };
        Assert.Equal(70m, RealProfitService.PriceAt(prices, FuelType.Petrol, 14050705));
        Assert.Equal(75m, RealProfitService.PriceAt(prices, FuelType.Petrol, 14050715));
        Assert.Equal(75m, RealProfitService.PriceAt(prices, FuelType.Petrol, 14050710));
        // پیش از نخستین خرید ⇒ نخستین خرید، نه صفر
        Assert.Equal(70m, RealProfitService.PriceAt(prices, FuelType.Petrol, 14050601));
        // دیزل قیمتِ خودش را دارد، نه پطرول
        Assert.Equal(80m, RealProfitService.PriceAt(prices, FuelType.Diesel, 14050715));
    }

    [Fact]
    public void BiKharid_FiZakhireShode_VaBiHichKodam_JodaGofteMishavad()
    {
        var none = new List<BuyPrice>();
        Assert.Equal(72m, RealProfitService.PriceAt(none, FuelType.Petrol, 14050705, 72m));
        Assert.Null(RealProfitService.PriceAt(none, FuelType.Petrol, 14050705));

        var r = RealProfitService.Compute(new[] { Line(14050705, 100m, 80m, 50m, 90m) }, none, 72m, 0m, 0m);
        Assert.Equal(7200m, r.Petrol.Cost);
        Assert.Equal(0m, r.Diesel.Cost);
        Assert.Equal(50m, r.UnpricedLiters);     // دیزلِ بی‌قیمت شمرده نشد، ولی پنهان هم نیست
        Assert.Equal(0m, r.Diesel.ProfitPerLiter);
    }

    [Fact]
    public void ZararDarad_VaGheymateFurusheMotefavet()
    {
        var prices = new List<BuyPrice> { new(FuelType.Diesel, 14050701, 1, 85m) };
        var lines = new[] { Line(14050702, 0, 0, 100m, 80m), Line(14050703, 0, 0, 100m, 90m) };
        var r = RealProfitService.Compute(lines, prices, 0, 0, 500m);
        Assert.Equal(17000m, r.Sales);
        Assert.Equal(17000m, r.Cost);
        Assert.Equal(0m, r.Gross);
        Assert.Equal(-500m, r.Net);
        Assert.Equal(85m, r.Diesel.SalePerLiter);
    }
}
