using System.Text.RegularExpressions;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Views;
using PumpYaqobi.Reporting.Pdf;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// شورا، ث۱ — «شروعِ سریع». رفتارِ کاملش با پنجره و کلیدِ واقعی در سنجهٔ
/// ‎quickstart‎ است؛ این‌جا قاعدهٔ خالص و پیوندِ هر گام با کادرِ هدفش.
/// </summary>
public class QuickStartTests
{
    [Fact]
    public void PanjGam_BeTartib_Va_HarKodam_AzHaghighat()
    {
        var none = QuickStart.Steps(false, false, false, false, false);
        Assert.Equal(new[] { "account", "pumpname", "capacity", "parcha", "waraq" }, none.Select(s => s.Id));
        Assert.False(QuickStart.AllDone(none));
        Assert.All(none, s => Assert.Equal("⬜", s.Mark));

        var some = QuickStart.Steps(true, false, true, false, false);
        Assert.Equal(new[] { true, false, true, false, false }, some.Select(s => s.Done));
        Assert.True(QuickStart.AllDone(QuickStart.Steps(true, true, true, true, true)));
    }

    [Fact]
    public void NamePishfarz_Name_Nist()
    {
        Assert.False(QuickStart.HasPumpName(""));
        Assert.False(QuickStart.HasPumpName("  "));
        Assert.False(QuickStart.HasPumpName("پمپ بنزین"));
        Assert.False(QuickStart.HasPumpName("پمپ یعقوبی"));     // پیش‌فرضِ کهنه
        Assert.True(QuickStart.HasPumpName("پمپ مرکزی"));
    }

    static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "CLAUDE.md"))) d = d.Parent;
        return d!.FullName;
    }

    [Fact]
    public void HarGam_KadreHadafeKhodRa_DarHamanBakhsh_Darad()
    {
        var views = Path.Combine(Root(), "native", "PumpYaqobi.App", "Views", "Sections");
        var files = new Dictionary<string, string>
        {
            ["account"] = "AccountSectionView.axaml", ["storage"] = "StorageSectionView.axaml",
            ["shifts"] = "ParchaSectionView.axaml", ["waraq"] = "WaraqSectionView.axaml",
        };
        foreach (var s in QuickStart.Steps(false, false, false, false, false))
        {
            var xaml = SrcText.Read(Path.Combine(views, files[s.Section]));
            Assert.Contains($"c:Spot.Id=\"{s.Spot}\"", xaml);
        }
    }

    [Fact]
    public void BargeyePdf_FaghatKelidhayeVaghei_RaMiguyad()
    {
        var known = ShortcutsWindow.All.Select(r => r.Key).ToHashSet();
        var text = string.Join("\n", QuickStartReport.Steps.Select(s => s.Keys));
        foreach (Match m in Regex.Matches(text, @"Ctrl \+ [A-Za-z]+|F\d+"))
            Assert.Contains(m.Value, known);
        Assert.Equal(7, QuickStartReport.Steps.Count);
    }
}
