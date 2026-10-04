using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — ورقِ روزانه با <b>رفتار</b>، نه با متنِ WaraqSectionViewModel.cs ══
///
/// دو قاعدهٔ دادهٔ ۱۴۰۵/۰۷/۱۲ و ۰۷/۱۸ که تا امروز فقط با گشتنِ رشته در سورس
/// قفل بودند: ردیف‌ها زیرِ دستِ کاربر جابه‌جا نمی‌شوند، و تیلِ پایه‌ای که از
/// پارچه آمده در ورق عوض نمی‌شود.
/// </summary>
public class WaraqPageBehaviourTests
{
    private static async Task<(AppHost Host, WaraqPageViewModel Page)> Open(Func<AppHost, WaraqEntry, Task>? seed = null)
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-wpb-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        var w = await host.WaraqData.OpenOrCreateAsync("1405/07/20", null);
        if (seed is not null) await seed(host, w);
        var full = (await host.WaraqData.LoadAsync(w.Id))!;
        return (host, new WaraqPageViewModel(host, full, new WaraqSectionViewModel(host)));
    }

    /// <summary>
    /// ⛔ «➕ ردیف» ته جدولِ دوم می‌نشیند و حذف فقط همان ردیف را برمی‌دارد —
    /// هیچ ردیفِ دیگری از جدولی به جدولِ دیگر نمی‌پرد.
    /// </summary>
    [Fact]
    public async Task RadifeTaze_VaHazf_HichRadifeDigariRa_JabejaNemikonad()
    {
        var (_, page) = await Open();
        Assert.NotEmpty(page.TxnsFirst);
        Assert.NotEmpty(page.TxnsSecond);
        var first = page.TxnsFirst.ToList();
        var second = page.TxnsSecond.ToList();

        await page.AddTxnCommand.ExecuteAsync(null);
        await page.AddTxnCommand.ExecuteAsync(null);
        Assert.Equal(first, page.TxnsFirst);
        Assert.Equal(second, page.TxnsSecond.Take(second.Count));
        Assert.Equal(second.Count + 2, page.TxnsSecond.Count);
        var added = page.TxnsSecond.Skip(second.Count).ToList();

        var gone = first[1];
        await page.DeleteTxnCommand.ExecuteAsync(gone);
        Assert.Equal(first.Where(r => r != gone), page.TxnsFirst);
        Assert.Equal(second.Concat(added), page.TxnsSecond);
    }

    /// <summary>
    /// ⛔ تیلِ پایه‌ای که از پارچه آمده در ورق عوض نمی‌شود؛ پایهٔ دستی می‌شود.
    /// </summary>
    [Fact]
    public async Task TileParcha_DarVaraq_AvazNemishavad()
    {
        var (_, page) = await Open(async (host, w) =>
        {
            var shift = w.Shifts.First(s => s.Kind == ShiftKind.Day);
            await host.WaraqData.SavePumpAsync(new WaraqPump { ShiftId = shift.Id, SortIndex = 0, Num = 1, Fuel = FuelType.Petrol, SrcKey = "parcha:1:day" });
            await host.WaraqData.SavePumpAsync(new WaraqPump { ShiftId = shift.Id, SortIndex = 1, Num = 2, Fuel = FuelType.Petrol });
        });
        var fromParcha = page.Pumps.Single(p => p.FromParcha);
        var manual = page.Pumps.Single(p => !p.FromParcha);

        fromParcha.ToggleFuelCommand.Execute(null);
        manual.ToggleFuelCommand.Execute(null);

        Assert.Equal(FuelType.Petrol, fromParcha.Fuel);
        Assert.Equal(FuelType.Diesel, manual.Fuel);
    }
}
