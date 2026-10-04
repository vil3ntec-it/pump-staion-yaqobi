using PumpYaqobi.App.Services;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Security;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// صاحب ریپو (۱۴۰۵/۰۷/۱۶): «مفاد و ضرر تنها آن‌جا دیده نمی‌شود — در داشبورد،
/// نوارِ عددهای بالا و ورق هم هست؛ با قفل شدن این‌ها هم باید قفل شوند.» و
/// «حاشیه‌های ورق را تنظیم می‌کنم، اعمال نمی‌شود» · «کادرِ وزنِ مخزن بزرگ‌تر است».
/// </summary>
[Collection(AppHostCollection.Name)]
public class ProfitVeilTests
{
    private static AppHost Host()
    {
        var host = AppHost.Start(
            Path.Combine(Path.GetTempPath(), "pump-veil-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        foreach (var id in SectionLockService.Ids) host.Locks.ClearPassword(id);
        return host;
    }

    [Fact]
    public void RamzeMafad_HameJa_MipooshadVaBaRamz_HameJa_Baz()
    {
        var host = Host();
        try
        {
            Assert.False(ProfitVeil.Hidden);
            Assert.Equal("1,200 افغانی", ProfitVeil.Show("1,200 افغانی"));

            var fired = 0;
            void On() => fired++;
            ProfitVeil.Changed += On;
            try
            {
                host.Locks.SetPassword(SectionLockService.Profit, "9999");
                host.Locks.Relock(SectionLockService.Profit);
                Assert.True(ProfitVeil.Hidden);
                Assert.Equal(ProfitVeil.Mask, ProfitVeil.Show("1,200 افغانی"));

                Assert.True(host.Locks.Unlock(SectionLockService.Profit, "9999"));
                Assert.False(ProfitVeil.Hidden);
                Assert.True(fired >= 1, "باز شدن خبر نداد — نوار و داشبورد تازه نمی‌شدند");
            }
            finally { ProfitVeil.Changed -= On; }
        }
        finally { host.Locks.ClearPassword(SectionLockService.Profit); }
    }

    [Fact]
    public void HarJayeMafad_AzProfitVeil_Mikhanad()
    {
        string R(params string[] p) => SrcText.Read(Path.Combine(new[] { Root() }.Concat(p).ToArray()));
        Assert.Contains("Banner[2].Value = ProfitVeil.Show(_bannerProfit);", R("PumpYaqobi.App", "ViewModels", "MainViewModel.cs"));
        var dash = R("PumpYaqobi.App", "ViewModels", "Sections", "DashboardSectionViewModel.cs");
        Assert.Contains("TrendProfitShown => ProfitVeil.Hidden", dash);
        Assert.Contains("ProfitVeil.Show(Money(v.Profit))", dash);
        Assert.Contains("Values=\"{Binding TrendProfitShown}\"", R("PumpYaqobi.App", "Views", "Sections", "DashboardSectionView.axaml"));
        var parcha = R("PumpYaqobi.App", "ViewModels", "Sections", "ParchaSectionViewModel.cs");
        Assert.Contains("public string ProfitText => ProfitVeil.Show(N.ProfitText);", parcha);
        Assert.Contains("public string ProfitText => ProfitVeil.Show(_profit);", parcha);
        Assert.Contains("(\"فایده\", ProfitVeil.Show(", parcha);
        Assert.Contains("HideProfit: ProfitVeil.Hidden", parcha);
        Assert.Contains("|| ProfitVeil.Hidden", R("PumpYaqobi.App", "ViewModels", "Sections", "MonthReportSectionViewModel.cs"));
    }

    [Fact]
    public void HashiyeyeVaraq_VirgulMomayezAst_VaBijaPayamDarad()
    {
        var vm = new PumpYaqobi.App.Printing.PrintSetupViewModel(PageSetup.Default) { Left = "۱۲,۵", Top = "40" };
        Assert.Null(vm.Problem());
        var b = vm.Build();
        Assert.Equal(12.5m, b.MarginLeft);
        Assert.Equal(40m, b.Margins().Top);
        Assert.Equal("custom", b.MarginPreset);
        var big = new PumpYaqobi.App.Printing.PrintSetupViewModel(PageSetup.Default) { Left = "120", Right = "120" };
        Assert.NotNull(big.Problem());
    }

    [Fact]
    public void KadreVazneMakhzan_HamGhadeDigaran()
    {
        var x = SrcText.Read(Path.Combine(Root(), "PumpYaqobi.App", "Views", "Sections", "StorageSectionView.axaml"));
        Assert.Contains("StringFormat=وزن · {0} تن}\" Classes=\"label\"", x);
        Assert.DoesNotContain("StringFormat={}{0} تن}\" Classes=\"muted\"", x);
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }
}
