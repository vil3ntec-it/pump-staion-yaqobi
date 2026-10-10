using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>══ تبدیلِ تیل — با مثالِ خودِ صاحب ریپو (۱۴۰۵/۰۷/۱۸) ══</summary>
public class FuelConversionTests
{
    [Fact]
    public void Mesal_SadLitrPetrol_HaftadVaHashtad_HashtadoHaftoNim()
    {
        var r = FuelConversionService.Compute(100m, 70m, 80m, 0m);
        Assert.True(r.Ok);
        Assert.Equal(7000m, r.FromValue);
        Assert.Equal(87.5m, r.Allowed);          // نه ۱۰۰
        Assert.Equal(87.5m, r.Delivered);        // تحویلِ نانوشته = همان مجاز
        Assert.Equal(FuelConversionService.StatusOk, r.Status);
        Assert.Equal(0m, r.ProfitLoss);
    }

    [Fact]
    public void Ezafe_SorkhVaZararDaghigh()
    {
        var r = FuelConversionService.Compute(100m, 70m, 80m, 100m);
        Assert.True(r.Over);
        Assert.Equal(FuelConversionService.StatusOver, r.Status);
        Assert.Equal(12.5m, r.ExtraLiters);
        Assert.Equal(1000m, r.Loss);             // ۱۲٫۵ لیتر × ۸۰
        Assert.Equal(-1000m, r.ProfitLoss);
    }

    [Fact]
    public void Kamtar_Sud_VaDoJahatYeksan()
    {
        var r = FuelConversionService.Compute(100m, 70m, 80m, 80m);
        Assert.Equal(FuelConversionService.StatusUnder, r.Status);
        Assert.Equal(600m, r.ProfitLoss);        // ۷٫۵ × ۸۰

        // دیزل ⇐ پطرول: همان فرمول
        var d = FuelConversionService.Compute(87.5m, 80m, 70m, 0m);
        Assert.Equal(7000m, d.FromValue);
        Assert.Equal(100m, d.Allowed);
    }

    [Fact]
    public void BiGheymat_NatijeNadarad()
    {
        Assert.False(FuelConversionService.Compute(100m, 0m, 80m, 0m).Ok);
        Assert.False(FuelConversionService.Compute(100m, 70m, 0m, 0m).Ok);
        Assert.False(FuelConversionService.Compute(0m, 70m, 80m, 0m).Ok);
    }

    /// <summary>فیِ خودکار: تازه‌ترین خریدِ همان تیل تا امروز؛ نبود ⇒ نخستین پس از آن؛ تیلِ دیگر و صفر هرگز.</summary>
    [Fact]
    public void FiAzMakhzan_TazeTarinKharidTaEmruz()
    {
        var p = new[]
        {
            new BuyPrice(FuelType.Petrol, 14050701, 1, 68m),
            new BuyPrice(FuelType.Petrol, 14050710, 2, 70m),
            new BuyPrice(FuelType.Petrol, 14050720, 3, 75m),   // پس از امروز
            new BuyPrice(FuelType.Diesel, 14050705, 4, 80m),
            new BuyPrice(FuelType.Diesel, 14050709, 5, 0m),    // بی فی ⇒ نادیده
        };
        Assert.Equal(70m, ProfitSectionViewModel.LatestBuy(p, FuelType.Petrol, 14050715)?.PerLiter);
        Assert.Equal(80m, ProfitSectionViewModel.LatestBuy(p, FuelType.Diesel, 14050715)?.PerLiter);
        Assert.Equal(68m, ProfitSectionViewModel.LatestBuy(p, FuelType.Petrol, 14050601)?.PerLiter);
        Assert.Null(ProfitSectionViewModel.LatestBuy(p[..3], FuelType.Diesel, 14050715));
    }
}
