using PumpYaqobi.Application.Localization;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>ممیزیِ محاسبه — حسابِ قرض‌دار و حسابِ شرکت (بازتولیدِ باگ‌ها).</summary>
public class AccountCalcAuditTests
{
    private static AppHost Host()
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-audit-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    private static async Task<(AppHost Host, AccountViewModel Acct)> OpenPerson()
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

    // ── ۱) شرکت: ردیفِ کهنهٔ «کیلو» — پاک کردنِ «خرید (تن)» پاک نمی‌کند ──────
    [Fact]
    public async Task Company_LegacyKgRow_ClearingTon_ClearsTheCell()
    {
        var host = Host();
        var c = await host.Companies.AddAsync("شرکت");
        var legacy = new CompanyRow
        {
            CompanyId = c.Id, Fuel = FuelType.Petrol, DateShamsi = "1405/07/01",
            Kg = 5000m, Ton = 0m, Usd = 700m, Rate = 70m,
        };
        await host.Companies.SaveRowAsync(legacy);

        var full = (await host.Companies.LoadAsync(c.Id))!;
        var sec = new CompanySectionViewModel(host);
        var page = new CompanyPageViewModel(host, full, sec);
        var row = Assert.Single(page.Rows);
        Assert.Equal("5", row.TonText);                // ۵۰۰۰ کیلو = ۵ تن

        row.TonText = "";                               // کاربر خانه را پاک می‌کند
        await row.FlushAsync();
        Assert.Equal("", row.TonText);                  // ✖ هنوز «5»
        Assert.Equal(Shamsi.Money(0m), row.TotalUsdText);

        // و ردیفی که کاربر عددِ دیگری زد و بعد پاکش کرد، به «5»ِ کهنه برنمی‌گردد
        row.TonText = "3";
        row.TonText = "";
        await row.FlushAsync();
        var back = (await host.Companies.LoadAsync(c.Id))!.Rows.Single();
        Assert.Equal(0m, host.Company.Ton(back));
    }

    // ── ۲) قرض‌دار: بردگیِ دستی پاک شد ⇒ همان لحظه «لیتر × فی»، نه خانهٔ خالی ──────
    [Fact]
    public async Task Person_ClearingManualBardagi_ShowsLitersTimesPriceAtOnce()
    {
        var (_, acct) = await OpenPerson();
        await acct.AddRowCommand.ExecuteAsync(null);
        var row = acct.Rows[0];
        row.LitersText = "10";
        row.PriceText = "100";
        row.BardagiText = "1500";                       // دستی
        await row.FlushAsync();
        Assert.Equal(Shamsi.Money(1500m), acct.SumBardagiText);

        row.BardagiText = "";                           // پاک کرد
        Assert.Equal(Shamsi.Money(1000m), row.BardagiText);   // همان لحظه دوباره «لیتر × فی»

        await row.FlushAsync();
        Assert.Equal(1000m, row.Entity.Bardagi);        // دیسک و جمله همان
        Assert.Equal(Shamsi.Money(1000m), acct.SumBardagiText);
        Assert.Equal(Shamsi.Money(1000m), row.BardagiText);
    }
}
