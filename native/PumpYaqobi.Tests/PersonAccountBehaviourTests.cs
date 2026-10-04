using PumpYaqobi.Application.Localization;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — حسابِ قرض‌دار با <b>رفتار</b>، نه با متنِ PersonViewModel.cs ══
///
/// سه قاعدهٔ پولی که تا امروز فقط با گشتنِ رشته در سورس قفل بودند:
/// ردیفِ تازه در فیلترِ «دیزل» دیزل است (بندِ ۱۴۰۵/۰۷/۱۶ — وگرنه لیترش در دفترِ
/// پطرول حساب می‌شد)، ردیفِ تازه پس از حذف «بیشینه + ۱» است نه «شمار»، و
/// «الباقی»ِ سربرگ همان فرمولِ سایت است (برد + فیصدی − رسید).
/// </summary>
public class PersonAccountBehaviourTests
{
    private static AppHost Host()
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-pab-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    private static async Task<(AppHost Host, AccountViewModel Acct)> Open()
    {
        var host = Host();
        var sec = new DebtSectionViewModel(host);
        await sec.EnsureLoadedAsync();
        await host.Debtors.AddDebtorAsync("کریم", "", false);
        await sec.RefreshAsync();
        await sec.OpenByNumberAsync(1);
        var person = Assert.IsType<PersonViewModel>(sec.ActivePage);
        return (host, person.Current!);
    }

    private static async Task<List<PumpYaqobi.Domain.Entities.DebtRow>> RowsOnDisk(AppHost host)
    {
        var d = (await host.Debtors.ListAsync()).Single();
        var full = await host.Debtors.LoadFullAsync(d.Id);
        return full!.AllAccounts().SelectMany(a => a.ActiveRows()).ToList();
    }

    /// <summary>⛔ فیلترِ «دیزل» ⇒ ردیفِ تازه دیزل است — روی دیسک هم.</summary>
    [Fact]
    public async Task RadifeTaze_DarFiltereDiesel_Diesel_Ast()
    {
        var (host, acct) = await Open();
        acct.RowFilter = "diesel";
        await acct.AddRowCommand.ExecuteAsync(null);
        acct.RowFilter = "petrol";
        await acct.AddRowCommand.ExecuteAsync(null);

        var rows = (await RowsOnDisk(host)).OrderBy(r => r.SortIndex).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(FuelType.Diesel, rows[0].Fuel);
        Assert.Equal(FuelType.Petrol, rows[1].Fuel);
    }

    /// <summary>
    /// ⛔ پس از حذفِ ردیفِ وسط، ردیفِ تازه «بیشینه + ۱» می‌گیرد — با «شمار»
    /// همان جای ردیفِ آخر را می‌گرفت و با باز شدنِ دوباره جای دیگری می‌نشست.
    /// </summary>
    [Fact]
    public async Task RadifeTaze_PasAzHazf_TahJadval_Mineshinad()
    {
        var (host, acct) = await Open();
        for (var i = 0; i < 3; i++) await acct.AddRowCommand.ExecuteAsync(null);
        await acct.DeleteRowCommand.ExecuteAsync(acct.Rows[1]);
        await acct.AddRowCommand.ExecuteAsync(null);
        var newest = acct.Rows[^1].Entity.Id;

        var rows = await RowsOnDisk(host);
        Assert.Equal(3, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => r.SortIndex).Distinct().Count());
        Assert.Equal(newest, rows.OrderBy(r => r.SortIndex).ThenBy(r => r.Id).Last().Id);
    }

    /// <summary>
    /// ⛔ الباقیِ سربرگ = برد + (رسید × ٪) − رسید — فرمولِ سایت، و رسید فقط یک بار.
    /// </summary>
    [Fact]
    public async Task AlbaqiyeSarbarg_FormuleSayt_Ast()
    {
        var (_, acct) = await Open();
        Assert.False(acct.IsMoney);
        await acct.AddRowCommand.ExecuteAsync(null);
        await acct.AddRowCommand.ExecuteAsync(null);
        acct.Rows[0].LitersText = "1000";
        acct.Rows[1].RasidFuelText = "400";
        acct.PercentPetrol = 5m;

        //  برد ۱۰۰۰ + فیصدی ۲۰ − رسید ۴۰۰ = ۶۲۰
        Assert.Equal(Shamsi.Money(620m), acct.HeadPetrolAlbaqiText);
        Assert.Equal(Shamsi.Money(20m), acct.HeadPetrolCommText);
    }

    /// <summary>
    /// ⛔ <b>دفترِ پول رسیدِ پول را می‌شمارد، دفترِ تیل رسیدِ تیل را</b> — هر کدام
    /// فقط خودش. (بازبینیِ ۱۴۰۵/۰۷/۲۰: جانشینِ رفتاری فقط دفترِ تیل را داشت.)
    /// </summary>
    [Fact]
    public async Task RasideSarbarg_DarDaftarePool_RasidePool_Ast()
    {
        var (_, acct) = await Open();
        acct.IsMoney = true;
        await acct.AddRowCommand.ExecuteAsync(null);
        await acct.AddRowCommand.ExecuteAsync(null);
        acct.Rows[0].ManualBardagiText = "8000";
        acct.Rows[1].RasidText = "3000";
        acct.Rows[1].RasidFuelText = "7";

        Assert.Equal(Shamsi.Money(3000m), acct.HeadPetrolRasidText);
        Assert.Equal(Shamsi.Money(5000m), acct.HeadPetrolAlbaqiText);    //  ۸۰۰۰ − ۳۰۰۰
    }
}
