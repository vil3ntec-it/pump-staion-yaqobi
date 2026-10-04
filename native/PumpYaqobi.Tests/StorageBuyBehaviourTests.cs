using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — «ثبتِ خرید»ِ مخزن با <b>رفتار</b>، نه با متنِ StorageSectionViewModel.cs ══
///
/// دو قاعدهٔ ۱۴۰۵/۰۷/۱۳ و ۰۷/۱۶: وزن به <b>کیلو</b> است (نه تن × ۱۰۰۰)، و فقط وزن
/// و ثقلت لازم‌اند — قیمت و نرخ اختیاری‌اند و نبودنشان خرید را نگه نمی‌دارد.
/// </summary>
public class StorageBuyBehaviourTests
{
    private static AppHost Host()
    {
        var host = new AppHost(Path.Combine(Path.GetTempPath(),
            "pump-sbb-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    [Fact]
    public async Task Kharid_BeKilo_VaBiGheymat_SabtMishavad()
    {
        var host = Host();
        var vm = new StorageSectionViewModel(host);
        vm.OpenBuyCommand.Execute(null);
        vm.BuyKg = "20000";
        vm.BuyDensity = "0.73";

        await vm.SaveBuyCommand.ExecuteAsync(null);

        var p = Assert.Single(await host.StorageData.AllPurchasesAsync());
        Assert.Equal(20000m, p.Kg);              //  کیلو همان کیلو — نه × ۱۰۰۰
        Assert.Equal(0.73m, p.Density);
        Assert.Equal(0m, p.PriceTon);
        Assert.Equal(FuelType.Petrol, p.Fuel);
    }

    [Theory]
    [InlineData("", "0.73")]
    [InlineData("20000", "")]
    public async Task BiVazn_YaBiSaghlat_HichChiziSabtNemishavad(string kg, string density)
    {
        var host = Host();
        var vm = new StorageSectionViewModel(host);
        vm.OpenBuyCommand.Execute(null);
        vm.BuyKg = kg;
        vm.BuyDensity = density;
        vm.BuyPriceTon = "900";
        vm.BuyUsdRate = "70";

        await vm.SaveBuyCommand.ExecuteAsync(null);

        Assert.Empty(await host.StorageData.AllPurchasesAsync());
    }
}
