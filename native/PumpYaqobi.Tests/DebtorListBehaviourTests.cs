using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — فهرستِ قرض‌داران با <b>رفتار</b>، نه با متنِ DebtSectionViewModel.cs ══
///
/// دو قاعدهٔ دادهٔ ۱۴۰۵/۰۷/۱۶: حسابِ تکراری ساخته نمی‌شود — حتی وقتی جست‌وجو
/// آن حساب را از جلوی چشم برداشته — و «✕»ِ کارت بی پرسش هیچ حسابی را نمی‌برد.
/// </summary>
[Collection(AppHostCollection.Name)]
public class DebtorListBehaviourTests : IDisposable
{
    public void Dispose()
    {
        Dialogs.ConfirmHook = null;
        Dialogs.PromptHook = null;
        GC.SuppressFinalize(this);
    }

    private static async Task<(AppHost Host, DebtSectionViewModel Sec)> Open(params string[] names)
    {
        var host = new AppHost(Path.Combine(Path.GetTempPath(),
            "pump-dlb-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        foreach (var n in names) await host.Debtors.AddDebtorAsync(n, "", false);
        var sec = new DebtSectionViewModel(host);
        await sec.EnsureLoadedAsync();
        await sec.RefreshAsync();
        return (host, sec);
    }

    [Fact]
    public async Task Tekrari_AzHame_SanjideMishavad_NaFaghatAzJostoju()
    {
        var (host, sec) = await Open("کریم", "احمد");
        sec.Search = "احمد";
        Assert.DoesNotContain(sec.Cards, c => c.Name == "کریم");     //  کریم از جلوی چشم رفته

        sec.NewName = "کریم";
        await sec.AddDebtorCommand.ExecuteAsync(null);

        Assert.Equal(2, await host.Debtors.CountAsync());

        sec.NewName = "رحیم";                                         //  سنجه دندان دارد: نامِ تازه ساخته می‌شود
        await sec.AddDebtorCommand.ExecuteAsync(null);
        Assert.Equal(3, await host.Debtors.CountAsync());
    }

    [Fact]
    public async Task Hazf_Miporsad_VaBaNa_HichNemiravad()
    {
        var (host, sec) = await Open("کریم");
        var card = Assert.Single(sec.Cards);

        Dialogs.ConfirmHook = (_, _) => false;
        await sec.DeleteDebtorCommand.ExecuteAsync(card);
        Assert.Equal(1, await host.Debtors.CountAsync());

        Dialogs.ConfirmHook = (_, _) => true;
        await sec.DeleteDebtorCommand.ExecuteAsync(card);
        Assert.Equal(0, await host.Debtors.CountAsync());
    }
}
