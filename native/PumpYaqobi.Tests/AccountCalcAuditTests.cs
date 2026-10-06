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

    // ── ۲) قرض‌دار: «مقدار بردگی»ِ ردیف ≠ عددِ ذخیره‌شده و «جمله» ────────────
    [Fact]
    public async Task Person_RowBardagiCell_MatchesStoredValueAndTotal()
    {
        var (host, acct) = await OpenPerson();
        await acct.AddRowCommand.ExecuteAsync(null);
        await acct.AddRowCommand.ExecuteAsync(null);
        foreach (var r in acct.Rows) { r.LitersText = "10.5"; r.PriceText = "33.3"; }
        await acct.FlushAsync();

        //  ۱۰٫۵ × ۳۳٫۳ = ۳۴۹٫۶۵ — روی دیسک ۳۵۰ (‎Round0‎)
        var stored = acct.Rows[0].Entity.Bardagi;
        Assert.Equal(350m, stored);
        Assert.Equal(Shamsi.Money(stored), acct.Rows[0].BardagiText);   // ✖ «349.65»
        // جمعِ دو خانهٔ دیده‌شده = «جمله»ی ته جدول
        Assert.Equal(acct.SumBardagiText,
            Shamsi.Money(acct.Rows.Sum(r => Shamsi.Num(r.BardagiText))));
    }

    // ── ۳) قرض‌دار: بردگیِ دستی پاک شد ⇒ پس از ذخیره ردیف «تیلی» می‌شود ولی
    //       خانهٔ «مقدار بردگی» و «الباقی» خبر نمی‌گیرند و خالی می‌مانند ─────────
    [Fact]
    public async Task Person_ClearingManualBardagi_CellIsNotifiedAfterSave()
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

        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        await row.FlushAsync();

        //  ذخیره (‎NormalizeRow‎) ردیف را «تیلی» کرد: دیسک و جمله ۱٬۰۰۰
        Assert.Equal(1000m, row.Entity.Bardagi);
        Assert.Equal(Shamsi.Money(1000m), acct.SumBardagiText);
        //  ✖ ولی خانهٔ جدول هیچ خبری نگرفت — همان «» روی صفحه می‌ماند
        Assert.Contains(nameof(DebtRowViewModel.BardagiText), raised);
        Assert.Contains(nameof(DebtRowViewModel.AlbaqiText), raised);
    }
}
