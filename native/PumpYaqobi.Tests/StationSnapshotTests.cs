using System.Text.Json;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ آن‌چه به گوشیِ کارمند می‌رسد ═══════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «اپ برای اندروید برای کارمندان تا قرض‌داران را چک کنند
/// که موجودی دارند یا نه — که اضافه ندهند — و موجودیِ تیل در مخزن را ببینند»،
/// و «رباتی که هر بخش و هر ماه را جواب بدهد».
///
/// پس این‌جا سنجیده می‌شود که عکسِ منتشرشده واقعاً همان چیزهاست و عددهایش با
/// عددهای خودِ برنامه یکی است — نه یک حسابِ تازه.
/// </summary>
public class StationSnapshotTests
{
    private static AppHost Host()
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-live-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    private static JsonElement Json(Dictionary<string, object?> snap) =>
        JsonDocument.Parse(JsonSerializer.Serialize(snap)).RootElement.Clone();

    /// <summary>
    /// عکس باید <b>سریال‌شدنی</b> باشد — وگرنه روی سرور هیچ‌وقت نمی‌نشیند و
    /// گوشی صفحهٔ خالی می‌بیند.
    /// </summary>
    [Fact]
    public async Task TheSnapshotIsRealJsonWithEverySectionInIt()
    {
        var snap = await StationSnapshot.BuildAsync(Host());
        var j = Json(snap);

        Assert.Equal(StationSnapshot.Version, j.GetProperty("v").GetInt32());
        Assert.True(j.GetProperty("seq").GetInt64() > 0);
        Assert.True(j.TryGetProperty("tank", out _));
        Assert.True(j.TryGetProperty("debtors", out _));

        var sections = j.GetProperty("sections");
        foreach (var id in new[] { "safe", "sarrafi", "expense", "chakana", "extraincome",
                                   "company", "amanat", "invoice", "storage", "staff" })
        {
            Assert.True(sections.TryGetProperty(id, out var sec), "بخشِ " + id + " نیست");
            // شکلِ واحدِ هر بخش — رباتِ گوشی روی همین حساب می‌کند
            foreach (var k in new[] { "t", "head", "rows", "m", "sum" })
                Assert.True(sec.TryGetProperty(k, out _), id + " کلیدِ " + k + " ندارد");
            Assert.Equal(sec.GetProperty("rows").GetArrayLength(),
                         sec.GetProperty("m").GetArrayLength());
        }
    }

    /// <summary>
    /// قفلِ اپ، همان رمزِ برنامهٔ نیتیو است — نه رمزِ جدا، و نه رمزِ خام.
    /// </summary>
    [Fact]
    public async Task TheGateIsTheDesktopPasswordAndNeverThePasswordItself()
    {
        var host = Host();
        var gate = Json(await StationSnapshot.BuildAsync(host)).GetProperty("gate").GetString()!;

        Assert.DoesNotContain("1234", gate);
        Assert.True(PasswordHasher.IsHashed(gate));
        Assert.True(PasswordHasher.Verify("1234", gate));
        Assert.False(PasswordHasher.Verify("4321", gate));

        // رمزِ برنامه که عوض شد، قفلِ اپ هم خودبه‌خود عوض می‌شود
        host.Auth.ChangePassword("admin", "1234", "5678");
        var gate2 = Json(await StationSnapshot.BuildAsync(host)).GetProperty("gate").GetString()!;
        Assert.True(PasswordHasher.Verify("5678", gate2));
        Assert.False(PasswordHasher.Verify("1234", gate2));
    }

    /// <summary>
    /// عددِ قرض‌دار در گوشی = عددِ همان حساب در برنامه. اگر این دو از هم جدا
    /// شوند، کارمند به کسی تیل می‌دهد که نباید.
    /// </summary>
    [Fact]
    public async Task ADebtorReachesThePhoneWithTheSameNumbersAsTheApp()
    {
        var host = Host();
        var person = await host.Debtors.AddDebtorAsync("هارون", "0700", false);
        var full = (await host.Debtors.LoadFullAsync(person.Id))!;
        var acct = full.MainAccount;

        await host.Debtors.SaveRowAsync(new DebtRow
        {
            FuelAccountId = acct.Id, Fuel = FuelType.Petrol,
            Liters = 300m, RasidFuel = 100m, DateShamsi = "1405/06/10",
        });

        var j = Json(await StationSnapshot.BuildAsync(host));
        var d = j.GetProperty("debtors").EnumerateArray()
                 .Single(x => x.GetProperty("name").GetString() == "هارون");

        Assert.Equal(person.Id, d.GetProperty("id").GetInt64());
        var a = d.GetProperty("accounts")[0];
        Assert.Equal("fuel", a.GetProperty("unit").GetString());
        Assert.Single(a.GetProperty("rows").EnumerateArray());

        // «الباقی» همان ۳۰۰ − ۱۰۰ است (فیصدیِ این حساب صفر است)
        var albaqi = a.GetProperty("sum").EnumerateArray()
                      .Single(b => b[0].GetString() == "الباقی")[1].GetString();
        Assert.Equal(200m, PumpYaqobi.Application.Localization.Shamsi.Num(albaqi));
    }

    /// <summary>مخزن: همان عددی که بخشِ مخزن و داشبورد نشان می‌دهند.</summary>
    [Fact]
    public async Task TheTankStockReachesThePhone()
    {
        var host = Host();
        await host.StorageData.AddPurchaseAsync(new FuelPurchase
        {
            Fuel = FuelType.Petrol, Kg = 8000m, Density = 0.8m,
            PriceTon = 700m, UsdRate = 70m, DateShamsi = "1405/06/01",
        });

        var tank = Json(await StationSnapshot.BuildAsync(host)).GetProperty("tank");
        Assert.Equal(10000d, tank.GetProperty("petrol").GetProperty("in").GetDouble(), 2);
        Assert.Equal(10000d, tank.GetProperty("petrol").GetProperty("current").GetDouble(), 2);
        Assert.False(tank.GetProperty("petrol").GetProperty("low").GetBoolean());
        Assert.True(tank.TryGetProperty("diesel", out _));
    }

    /// <summary>
    /// ⚠️ «چیزی عوض شده؟» نباید به زمان نگاه کند، وگرنه هر بیست ثانیه کلِ
    /// داده بی‌دلیل روی شبکه می‌رود.
    /// </summary>
    [Fact]
    public async Task TwoSnapshotsOfTheSameDataLookIdenticalToThePublisher()
    {
        var host = Host();
        var a = await StationSnapshot.BuildAsync(host);

        // همان داده، فقط با مهرِ زمانِ دیگر — دقیقاً کاری که حلقهٔ بیست‌ثانیه‌ای
        // هر بار می‌کند.
        var b = new Dictionary<string, object?>(a)
        {
            ["seq"] = (long)a["seq"]! + 9999,
            ["at"] = "1499/01/01",
            ["atUtc"] = DateTime.UtcNow.AddHours(3).ToString("O"),
        };
        Assert.Equal(StationPublisher.HashOf(a), StationPublisher.HashOf(b));

        // ولی اگر واقعاً چیزی عوض شود، حتماً دیده می‌شود
        await host.Debtors.AddDebtorAsync("کسی تازه", null, false);
        var c = await StationSnapshot.BuildAsync(host);
        Assert.NotEqual(StationPublisher.HashOf(a), StationPublisher.HashOf(c));
    }

    /// <summary>
    /// شاخهٔ انتشار جداست تا نسخهٔ وبِ قدیمی که کلِ ‎stations/&lt;کد&gt;‎ را یک‌جا
    /// ‎set‎ می‌کند، آن را پاک نکند.
    /// </summary>
    [Fact]
    public void ThePublishPathNeverCollidesWithTheOldSiteBranch()
    {
        Assert.Equal("stations/pump1-live", StationPublisher.PathOf("pump1"));
        Assert.Equal("stations/pump1-live", StationPublisher.PathOf(""));
        Assert.Equal("stations/pump1-live", StationPublisher.PathOf(null));
        Assert.Equal("stations/kabul-live", StationPublisher.PathOf(" kabul "));
    }

    /// <summary>
    /// ══ خواندنِ دسته‌جمعی، همان چیزی که تک‌تک می‌داد ═══════════════════════
    ///
    /// ⚠️ ‎LoadAllAsync‎ برای این هست که کسی ‎LoadFullAsync‎ را در حلقه صدا
    /// نزند (چهار کوئری برای هر نفر). ولی «سریع‌تر» فقط وقتی ارزش دارد که
    /// <b>همان</b> را بدهد — پس این‌جا دو راه کنارِ هم گذاشته می‌شوند.
    /// </summary>
    [Fact]
    public async Task LoadingEveryoneAtOnceGivesTheSameThingAsOneByOne()
    {
        var host = Host();

        var a = await host.Debtors.AddDebtorAsync("هارون", "0700", false);
        var b = await host.Debtors.AddDebtorAsync("محمد", null, false);
        await host.Debtors.AddDebtorAsync("بی‌فاکتور", null, true);   // نباید بیاید

        var aMain = (await host.Debtors.LoadFullAsync(a.Id))!.MainAccount;
        var sub = await host.Debtors.AddSubAccountAsync(a.Id, "حسابِ دوم");

        await host.Debtors.SaveRowAsync(new DebtRow
        { FuelAccountId = aMain.Id, Fuel = FuelType.Petrol, Liters = 100m, RasidFuel = 40m, SortIndex = 0 });
        await host.Debtors.SaveRowAsync(new DebtRow
        { FuelAccountId = aMain.Id, Fuel = FuelType.Diesel, Liters = 50m, SortIndex = 1 });
        await host.Debtors.SaveRowAsync(new DebtRow
        { MoneyAccountId = sub.Id, Fuel = FuelType.Petrol, ByMoney = true, Bardagi = 900m, SortIndex = 0 });

        var all = await host.Debtors.LoadAllAsync(false);
        Assert.Equal(2, all.Count);
        Assert.DoesNotContain(all, p => p.Name == "بی‌فاکتور");

        foreach (var lite in new[] { a, b })
        {
            var one = (await host.Debtors.LoadFullAsync(lite.Id))!;
            var many = all.Single(p => p.Id == lite.Id);

            Assert.Equal(one.AllAccounts().Count(), many.AllAccounts().Count());

            // ⚠️ دو دفتر جدا می‌مانند — ردیفِ پولی نباید در دفترِ تیل بنشیند
            foreach (var (x, y) in one.AllAccounts().Zip(many.AllAccounts()))
            {
                Assert.Equal(x.Id, y.Id);
                Assert.Equal(x.FuelRows.Count, y.FuelRows.Count);
                Assert.Equal(x.MoneyRows.Count, y.MoneyRows.Count);
            }
        }

        var haroun = all.Single(p => p.Name == "هارون");
        Assert.Equal(2, haroun.MainAccount.FuelRows.Count);
        Assert.Empty(haroun.MainAccount.MoneyRows);
        Assert.Single(haroun.SubAccounts);
        Assert.Single(haroun.SubAccounts[0].MoneyRows);
        Assert.Empty(haroun.SubAccounts[0].FuelRows);

        // و شمارنده هم همان سه ردیف را می‌بیند، بی خواندنشان
        Assert.Equal(3, await host.Debtors.RowCountAsync());
    }

    /// <summary>ماهِ هر ردیف از تاریخِ شمسیِ خودش درمی‌آید — ورودیِ «ماه فلان»ِ ربات.</summary>
    [Theory]
    [InlineData("1405/06/22", "1405/06")]
    [InlineData("1404/12/01", "1404/12")]
    [InlineData("1405/06", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void TheMonthOfARowIsReadFromItsOwnDate(string? date, string expected)
        => Assert.Equal(expected, StationSnapshot.MonthOf(date));
}
