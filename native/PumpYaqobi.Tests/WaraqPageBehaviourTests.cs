using PumpYaqobi.Application.Localization;
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
    /// ⛔ (۱۴۰۵/۰۷/۱۸) دو جدول پس از «➕ ردیف»، «➕➕ چندتایی» و حذف **همان** تقسیمی را دارند
    /// که پس از «خروج و ورود» درمی‌آید (‎ceil(n/2)‎)، ترتیبِ ردیف‌ها هیچ‌جا عوض نمی‌شود، و هر
    /// ردیفِ تازه دستِ‌بالا یک ردیف را از مرزِ دو جدول جابه‌جا می‌کند. پیش از این همهٔ ردیف‌های
    /// تازه تهِ جدولِ دوم می‌نشستند و زیرِ جدولِ اول سفید می‌ماند تا ورودِ دوباره.
    /// </summary>
    [Fact]
    public async Task RadifeTaze_VaHazf_DoJadval_HamanTaghsimeVorudeDobare()
    {
        var (host, page) = await Open();
        void Same()
        {
            var all = page.TxnsFirst.Concat(page.TxnsSecond).ToList();
            Assert.Equal(page.Txns, all);                                       // ترتیبِ داده
            Assert.Equal((int)Math.Ceiling(all.Count / 2.0), page.TxnsFirst.Count);
        }
        Same();
        for (var i = 0; i < 3; i++)
        {
            var before = page.TxnsFirst.ToList();
            await page.AddTxnCommand.ExecuteAsync(null);
            Same();
            Assert.True(page.TxnsFirst.Take(before.Count).SequenceEqual(before));  // ردیفِ جدولِ اول نپرید
            Assert.True(page.TxnsFirst.Count - before.Count is 0 or 1);
        }
        await page.AddRowsAsync(10);
        Same();
        await page.DeleteTxnCommand.ExecuteAsync(page.TxnsFirst[1]);
        Same();
        await page.DeleteTxnCommand.ExecuteAsync(page.TxnsSecond[^1]);
        Same();

        // همان ورق از نو باز شود ⇒ همان دو جدول
        var full = (await host.WaraqData.LoadAsync(page.Entity.Id))!;
        var re = new WaraqPageViewModel(host, full, new WaraqSectionViewModel(host));
        Assert.Equal(page.TxnsFirst.Select(r => r.Entity.Id), re.TxnsFirst.Select(r => r.Entity.Id));
        Assert.Equal(page.TxnsSecond.Select(r => r.Entity.Id), re.TxnsSecond.Select(r => r.Entity.Id));
        // شمارهٔ ستونِ «#» پیوسته است
        Assert.Equal(Enumerable.Range(1, page.Txns.Count).Select(i => Shamsi.Money(i)),
                     page.TxnsFirst.Concat(page.TxnsSecond).Select(r => r.Index));
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
