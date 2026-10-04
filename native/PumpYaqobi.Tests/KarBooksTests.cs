using System.Text.Json;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// «نمی‌شود واحدِ تیل یا پول را عوض کرد» (صاحب ریپو، ۱۴۰۵/۰۷/۱۶): عکسِ گوشی
/// فقط دفترِ فعالِ هر حساب را داشت. حالا ‎books‎ هر دو دفتر را می‌برد، و نسخهٔ
/// سرورِ حساب ردیف‌های هر دو را هم بر‌می‌دارد تا بار دو برابر نشود.
///
/// ⛔ شورا ت۳: از ۱۴۰۵/۰۷/۲۰ با <b>رفتار</b> — عکسِ واقعیِ یک دفترِ واقعی، نه
/// گشتنِ رشته در StationSnapshot.cs.
/// </summary>
public class KarBooksTests
{
    [Fact]
    public async Task Aks_HarDoDaftar_RaMibarad_VaAbr_RadifHayeHarDo_RaBarmidarad()
    {
        var host = new AppHost(Path.Combine(Path.GetTempPath(),
            "pump-books-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        var d = await host.Debtors.AddDebtorAsync("کریم", "", false);
        var a = (await host.Debtors.LoadFullAsync(d.Id))!.MainAccount;
        Assert.False(a.Mode.IsMoney());
        await host.Debtors.SaveRowAsync(new DebtRow
        {
            FuelAccountId = a.Id, Fuel = FuelType.Petrol, Liters = 120m, DateShamsi = "1405/07/10",
        });
        await host.Debtors.SaveRowAsync(new DebtRow
        {
            MoneyAccountId = a.Id, ByMoney = true, Fuel = FuelType.Petrol, Liters = 50m, PricePerLiter = 80m, Bardagi = 4000m,
            DateShamsi = "1405/07/11",
        });

        var snap = await StationSnapshot.BuildAsync(host);
        var books = Books(snap);
        Assert.Equal(2, books.Count);
        var money = books.Single(b => b.GetProperty("money").GetBoolean());
        var fuel = books.Single(b => !b.GetProperty("money").GetBoolean());
        Assert.False(money.GetProperty("on").GetBoolean());     //  حساب در واحدِ تیل است
        Assert.True(fuel.GetProperty("on").GetBoolean());
        Assert.NotEqual(0, fuel.GetProperty("r").GetArrayLength());
        Assert.NotEqual(0, money.GetProperty("r").GetArrayLength());

        //  ⛔ نسخهٔ سرورِ حساب، وقتی جا نیست: ردیف‌های هر دو دفتر می‌روند — و عکسِ
        //  سرورِ خانگی (همان شیء) دست نمی‌خورد
        var cloud = Books(StationSnapshot.ForCloud(snap, budget: 1));
        Assert.Equal(2, cloud.Count);
        Assert.All(cloud, b => Assert.Equal(0, b.GetProperty("r").GetArrayLength()));
        Assert.All(Books(snap), b => Assert.NotEqual(0, b.GetProperty("r").GetArrayLength()));
    }

    private static List<JsonElement> Books(Dictionary<string, object?> snap) =>
        JsonDocument.Parse(JsonSerializer.Serialize(snap)).RootElement
            .GetProperty("debtors").EnumerateArray().Single()
            .GetProperty("accounts").EnumerateArray().Single()
            .GetProperty("books").EnumerateArray().Select(b => b.Clone()).ToList();
}
