using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ مبلغ = مقدار × فی — «آخرین چیزی که نوشتی برنده است» (۱۴۰۵/۰۷/۲۲) ══════════
///
/// گزارشِ صاحب ریپو: «مقدارِ تیل را می‌زنم اما با فی ضرب نمی‌شود و به مبلغ اضافه
/// نمی‌شود.» مبلغی که یک بار دستی نوشته شده بود ردیف را برای همیشه دستی می‌کرد،
/// و پاک کردنِ مبلغ آن را تا ابد صفر نگه می‌داشت. با خودِ صفحهٔ ورق و دیسکِ واقعی.
/// </summary>
public class WaraqAutoAmountTests
{
    private static async Task<(AppHost Host, WaraqPageViewModel Page)> Open(decimal price = 80m)
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-wam-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        var w = await host.WaraqData.OpenOrCreateAsync("1405/07/20", null);
        var shift = w.Shifts.First(s => s.Kind == ShiftKind.Day);
        await host.WaraqData.SavePumpAsync(new WaraqPump { ShiftId = shift.Id, SortIndex = 0, Num = 1, Fuel = FuelType.Petrol, PricePerLiter = price, Start = 0, End = 100 });
        var full = (await host.WaraqData.LoadAsync(w.Id))!;
        var page = new WaraqPageViewModel(host, full, new WaraqSectionViewModel(host));
        if (page.IsNight) page.IsNight = false;
        return (host, page);
    }

    private static async Task<WaraqTxnViewModel> NewRow(WaraqPageViewModel page)
    {
        await page.AddTxnCommand.ExecuteAsync(null);
        var r = page.Txns[^1];
        r.Name = "کریم";
        return r;
    }

    private static async Task<decimal> OnDisk(AppHost host, WaraqTxnViewModel r, WaraqPageViewModel page)
    {
        await page.FlushAsync();                    // همان «بیرون رفتن از ورق»
        var full = (await host.WaraqData.LoadAsync(await ShiftWaraq(host, r)))!;
        var t = full.Shifts.SelectMany(s => s.Transactions).Single(x => x.Id == r.Entity.Id);
        return new PumpYaqobi.Application.Services.WaraqService().TxnAmount(full.Shifts.Single(s => s.Id == t.ShiftId), t);
    }

    private static async Task<long> ShiftWaraq(AppHost host, WaraqTxnViewModel r)
    {
        foreach (var w in await host.WaraqData.ListAsync(null))
            if ((await host.WaraqData.LoadAsync(w.Id))!.Shifts.Any(s => s.Id == r.Entity.ShiftId)) return w.Id;
        return 0;
    }

    [Fact]
    public async Task Meghdar_BaFi_ZarbMishavad_VaRuyeDiskMiresad()
    {
        var (host, page) = await Open();
        var r = await NewRow(page);
        r.LitersText = "10";
        Assert.Equal("800", r.AmountText);
        Assert.Equal(800m, await OnDisk(host, r, page));
    }

    [Fact]
    public async Task PasAzMablagheDasti_MeghdareTaze_DobareZarbMishavad()
    {
        var (host, page) = await Open();
        var r = await NewRow(page);
        r.LitersText = "10";
        r.AmountText = "125";                       // مبلغِ دستی (یا نیمه‌مانده از باگِ پیشین)
        Assert.Equal("125", r.AmountText);
        r.LitersText = "20";                        // کاربر مقدار را دوباره نوشت
        Assert.Equal("1,600", r.AmountText);
        Assert.Equal(1600m, await OnDisk(host, r, page));
    }

    [Fact]
    public async Task HamanMeghdar_DobareNeveshte_HamZarbMishavad()
    {
        var (host, page) = await Open();
        var r = await NewRow(page);
        r.LitersText = "10";
        r.AmountText = "125";
        r.LitersText = "10";                        // همان عدد، دوباره
        Assert.Equal("800", r.AmountText);
        Assert.Equal(800m, await OnDisk(host, r, page));
    }

    [Fact]
    public async Task MablagheKhali_DobareKhodkarMishavad_NaSefr()
    {
        var (host, page) = await Open();
        var r = await NewRow(page);
        r.LitersText = "10";
        r.AmountText = "500";
        r.AmountText = "";                          // پاک کرد
        Assert.Equal("800", r.AmountText);
        Assert.Equal(800m, await OnDisk(host, r, page));
    }

    [Fact]
    public async Task MablagheDasti_BaAvazShodaneFi_DastNemikhorad()
    {
        var (host, page) = await Open();
        var r = await NewRow(page);
        r.LitersText = "10";
        r.AmountText = "750";                       // تخفیف، عمداً دستی
        page.Pumps[0].Price = 90m;
        Assert.Equal("750", r.AmountText);
        Assert.Equal(750m, await OnDisk(host, r, page));
    }

    [Fact]
    public async Task MablagheKhodkar_BaAvazShodaneFi_AvazMishavad()
    {
        var (host, page) = await Open();
        var r = await NewRow(page);
        r.LitersText = "10";
        page.Pumps[0].Price = 90m;
        Assert.Equal("900", r.AmountText);
        Assert.Equal(900m, await OnDisk(host, r, page));
    }
}
