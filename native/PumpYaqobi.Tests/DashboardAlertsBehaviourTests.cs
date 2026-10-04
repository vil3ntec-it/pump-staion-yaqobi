using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — زنگِ داشبورد از <b>همان یک فهرست</b>، با رفتار ═══════════════
///
/// قاعدهٔ ۱۴۰۵/۰۷/۱۴ («یک فهرستِ هشدار»): زنگ و فهرستِ داشبورد همان
/// <c>AppHost.LiveAlerts</c> را می‌خوانند که به سرور و بات می‌رود — نه قاعدهٔ
/// خودشان. تا امروز فقط با گشتنِ رشته در DashboardSectionViewModel.cs قفل بود.
/// </summary>
[Collection(AppHostCollection.Name)]
public class DashboardAlertsBehaviourTests
{
    [Fact]
    public async Task Zang_AzHamanFehresteHoshdar_MikhAnad()
    {
        var host = AppHost.Start(Path.Combine(Path.GetTempPath(), "pump-shared-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        if (host.Auth.HasPassword()) host.Auth.SignIn("admin", "1234"); else host.Auth.OpenWithoutPassword();

        var name = "زنگ-" + Guid.NewGuid().ToString("N")[..6];
        var d = await host.Debtors.AddDebtorAsync(name, "", false);
        var a = (await host.Debtors.LoadFullAsync(d.Id))!.MainAccount;
        //  بی سپرده و ۳۰۰ لیتر برده ⇒ «تمام شد»
        await host.Debtors.SaveRowAsync(new DebtRow
        {
            FuelAccountId = a.Id, Fuel = FuelType.Petrol, Liters = 300m, RasidFuel = 100m, DateShamsi = "1405/07/10",
        });

        await host.LiveAlerts.CheckAsync(host, force: true);
        var list = host.LiveAlerts.Current;
        Assert.Contains(list, x => x.Text.Contains(name));

        var dash = (DashboardSectionViewModel)new MainViewModel().Sections.Single(s => s.Id == "dashboard");
        dash.ApplyAlerts();

        Assert.Equal(list.Count, dash.BellCount);
        Assert.Contains(dash.Alerts, x => x.Text.Contains(name));
        Assert.All(dash.Alerts, x => Assert.Contains(list, l => l.Text == x.Text));
    }
}
