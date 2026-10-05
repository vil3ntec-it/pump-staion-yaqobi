using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «نوع را عوض کنی، پارچهٔ روزِ قبل دوباره می‌آید؛ پاک هم کنی باز همان» (۱۴۰۵/۰۷/۲۰) ══
///
/// گزارشِ صاحب ریپو: «بخش پارچه‌ها وقتی نوع رو عوض کنی دوبار پارچه خودکار از روز
/// قبل رو میاره داخل؛ پاک هم کنی باز هم نوع رو عوض و دوباره به اول بیاری همونه.»
/// رفتار با ویومدلِ واقعی و SQLiteِ واقعی سنجیده می‌شود، نه با متنِ سورس.
/// </summary>
public class ParchaFuelSwitchTests
{
    private static AppHost Host()
    {
        var host = new AppHost(Path.Combine(Path.GetTempPath(),
            "pump-pfs-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    private static async Task SaveDay(AppHost host, string date)
    {
        var res = await host.ParchaData.SaveShiftFlowAsync(new ShiftSaveRequest(
            Fuel: FuelType.Petrol, Kind: ShiftKind.Day, DateShamsi: date,
            Name: "کریم", PumpNum: 1, Start: 1000m, End: 1500m, Price: 70m,
            Debt: 0m, BoxProfitPer: 0m, AvailMan: 0m, Note: "",
            BuyPerLiter: 0m, ForceNew: false));
        Assert.True(res.Ok, res.Error);
    }

    /// <summary>پطرول ⇒ دیزل ⇒ پطرول، و منتظرِ خواندنِ دوباره.</summary>
    private static async Task SwitchAwayAndBack(ParchaSectionViewModel vm)
    {
        vm.IsDiesel = true;
        await vm.ReloadAsync();
        vm.IsDiesel = false;
        await vm.ReloadAsync();
        await Task.Delay(300);   //  بارِ پس‌زمینهٔ ‎OnIsDieselChanged‎ هم تمام شود
    }

    private static bool Empty(ShiftFormViewModel f) =>
        f.Name.Length == 0 && f.PumpNum.Length == 0 && f.Start.Length == 0 && f.End.Length == 0;

    [Fact]
    public async Task PaarcheyeRuzeGhabl_BaAvazShodaneNoo_DarKartNemiNeshinad()
    {
        var host = Host();
        await SaveDay(host, Shamsi.Of(AppClock.Now.AddDays(-1)));

        var vm = new ParchaSectionViewModel(host);
        await vm.EnsureLoadedAsync();
        Assert.True(Empty(vm.Day), "پارچهٔ دیروز در کارتِ امروز نشست: «" + vm.Day.Name + "»");

        await SwitchAwayAndBack(vm);
        Assert.True(Empty(vm.Day), "با عوض شدنِ نوع، پارچهٔ دیروز دوباره آمد: «" + vm.Day.Name + "»");
        Assert.True(Empty(vm.Night));
    }

    [Fact]
    public async Task KarteiKeKarbarPakKard_BaAvazShodaneNoo_PakMimanad()
    {
        var host = Host();
        await SaveDay(host, Shamsi.Today());

        var vm = new ParchaSectionViewModel(host);
        await vm.EnsureLoadedAsync();
        Assert.Equal("کریم", vm.Day.Name);          //  پارچهٔ امروز همان‌جاست

        vm.Day.Name = ""; vm.Day.PumpNum = ""; vm.Day.Start = ""; vm.Day.End = "";
        await SwitchAwayAndBack(vm);
        Assert.True(Empty(vm.Day), "کارتی که کاربر پاک کرد با برگشتِ نوع دوباره پر شد: «" + vm.Day.Name + "»");
    }

    [Fact]
    public async Task PaarcheyeEmruz_BaAvazShodaneNoo_SareJayashMimanad()
    {
        var host = Host();
        await SaveDay(host, Shamsi.Today());

        var vm = new ParchaSectionViewModel(host);
        await vm.EnsureLoadedAsync();
        await SwitchAwayAndBack(vm);
        Assert.Equal("کریم", vm.Day.Name);
        Assert.Equal("1", vm.Day.PumpNum);
    }
}
