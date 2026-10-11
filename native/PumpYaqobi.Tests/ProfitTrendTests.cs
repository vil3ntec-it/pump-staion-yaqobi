using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ نمودارِ «مفاد / ضرر خالص» از داده‌ی واقعی (۱۴۰۵/۰۷/۲۲) ══
///  صاحب ریپو: «شکلِ چارت خیلی داغون است و به درد نخور». تا امروز سیزده عددِ ثابتِ تزئینی بود.
/// </summary>
public class ProfitTrendTests
{
    private static WaraqSaleLine Sale(int key, decimal p, decimal d) =>
        new($"{key / 10000}/{key / 100 % 100:00}/{key % 100:00}", key, ShiftKind.Day, 0, p, 0, d);

    [Fact]
    public void Har_rooz_forosh_be_ezafe_daramad_menhaye_masraf_tazetarin_aval()
    {
        var t = ProfitTrend.Build(
            new[] { Sale(14050701, 100, 50), Sale(14050701, 10, 0), Sale(14050703, 40, 0) },
            new[] { new DatedAmount(14050703, 5) },
            new[] { new DatedAmount(14050702, 30), new DatedAmount(14050703, 15) });
        Assert.Equal(new[] { "07/03", "07/02", "07/01" }, t.Select(x => x.Label));
        Assert.Equal(new[] { 30m, -30m, 160m }, t.Select(x => x.Net));   //  روزِ ضرر منفی می‌ماند
    }

    [Fact]
    public void Dore_boland_har_noqte_yek_mah()
    {
        var sales = Enumerable.Range(0, 70).Select(i => Sale(14040101 + (i / 30) * 100 + i % 30, 10, 0));
        var t = ProfitTrend.Build(sales, Array.Empty<DatedAmount>(), Array.Empty<DatedAmount>());
        Assert.Equal(new[] { "1404/03", "1404/02", "1404/01" }, t.Select(x => x.Label));
        Assert.Equal(700m, t.Sum(x => x.Net));
    }

    [Fact]
    public void Namudar_dige_adade_sabete_tazyini_nadarad()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        var src = SrcText.Read(Path.Combine(d!.FullName, "PumpYaqobi.App", "ViewModels", "Sections", "ProfitSectionViewModel.cs"));
        Assert.DoesNotContain("4, 16, 10, 28", src);
        Assert.Contains("ProfitTrend.Build", src);
    }
}
