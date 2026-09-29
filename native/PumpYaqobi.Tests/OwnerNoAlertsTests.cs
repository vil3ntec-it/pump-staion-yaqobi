using System.Text.Json;
using PumpYaqobi.App;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «روی حسابِ خودم هشداری نیاید» (۱۴۰۵/۰۷/۱۷) ══════════════════════════════
///
/// خواستهٔ صاحب ریپو: «من خودم صاحبِ پمپ استم… نمی‌خواهم روی حسابم هشداری
/// بیاید… ربات تلگرام و هشدار دیوانه‌ام نکند.» <see cref="Debtor.NoAlerts"/>
/// در همان تنها سازندهٔ فهرست (<c>StationSnapshot.Alerts</c>) دیده می‌شود، پس
/// زنگِ داشبورد، توستِ میرزا، سرور و بات همه با هم ساکت می‌شوند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class OwnerNoAlertsTests
{
    private static Dictionary<string, object?> Person(long id, string name, bool mute) => new()
    {
        ["id"] = id, ["name"] = name, ["stP"] = "out", ["stD"] = "low", ["stM"] = "out", ["mute"] = mute,
    };

    [Fact]
    public void Mute_HichHoshdariNemisazad_VaBaghiyeHaSarejayeshan()
    {
        var alerts = StationSnapshot.Alerts(
            new List<object?> { Person(1, "صاحب", true), Person(2, "کریم", false) }, null)
            .Cast<Dictionary<string, object?>>().ToList();

        Assert.NotEmpty(alerts);
        Assert.All(alerts, a => Assert.StartsWith("d2-", (string)a["k"]!));
        Assert.DoesNotContain(alerts, a => (a["n"] as string) == "صاحب");
    }

    [Fact]
    public async Task NoAlerts_RoyeDisk_Mimanad_VaAzAksHoshdarNemiravad()
    {
        var host = new AppHost(Path.Combine(Path.GetTempPath(),
            "pump-mute-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");

        async Task<Debtor> Make(string name)
        {
            var person = await host.Debtors.AddDebtorAsync(name, "", false);
            var full = (await host.Debtors.LoadFullAsync(person.Id))!;
            //  بی سپرده و ۳۰۰ لیتر برده ⇒ «تمام شد» — همان حالی که هشدار می‌سازد
            await host.Debtors.SaveRowAsync(new DebtRow
            {
                FuelAccountId = full.MainAccount.Id, Fuel = FuelType.Petrol,
                Liters = 300m, RasidFuel = 100m, DateShamsi = "1405/06/10",
            });
            return full;
        }

        var owner = await Make("صاحبِ پمپ");
        var other = await Make("کریم");

        //  همان درِ منوی راست‌کلیکِ کارت — فقط همین ستون
        await host.Debtors.SetNoAlertsAsync(owner.Id, true);
        var back = (await host.Debtors.LoadFullAsync(owner.Id))!;
        Assert.True(back.NoAlerts);
        Assert.Equal("صاحبِ پمپ", back.Name);                                // چیزِ دیگری دست نخورد

        var snap = await StationSnapshot.BuildAsync(host);
        var j = JsonDocument.Parse(JsonSerializer.Serialize(snap)).RootElement;

        var debtors = j.GetProperty("debtors").EnumerateArray().ToList();
        Assert.True(debtors.Single(d => d.GetProperty("id").GetInt64() == owner.Id).GetProperty("mute").GetBoolean());
        Assert.False(debtors.Single(d => d.GetProperty("id").GetInt64() == other.Id).GetProperty("mute").GetBoolean());

        var keys = j.GetProperty("alerts").EnumerateArray().Select(a => a.GetProperty("k").GetString()!).ToList();
        Assert.Contains(keys, k => k.StartsWith("d" + other.Id + "-"));      // سنجه دندان دارد
        Assert.DoesNotContain(keys, k => k.StartsWith("d" + owner.Id + "-"));
    }

    /// <summary>
    /// ۱۴۰۵/۰۷/۱۸: «بی‌هشدار» بیرون از حساب، با راست‌کلیک روی کارتِ قرض‌دار —
    /// و دیگر در نوارِ داخلِ حساب نیست.
    /// </summary>
    [Fact]
    public void BiHoshdar_BaRastKlikeKart_NaDarNavareHesab()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../PumpYaqobi.App"));
        var card = File.ReadAllText(Path.Combine(root, "Views/Sections/DebtSectionView.axaml"));
        var person = File.ReadAllText(Path.Combine(root, "Views/Sections/PersonView.axaml"));
        Assert.Contains("<Button.ContextMenu>", card);
        Assert.Contains("Owner.ToggleMuteCommand", card);
        Assert.DoesNotContain("IsChecked=\"{Binding NoAlerts}\"", person);
    }
}
