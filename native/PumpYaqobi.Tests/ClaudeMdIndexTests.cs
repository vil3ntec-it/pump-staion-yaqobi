using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// شورا، ج۲ — CLAUDE.md شاخصِ قاعده‌های زنده است و شرحِ کامل در
/// native/docs/HISTORY-fa.md. این سنجه‌ها می‌گویند هیچ چیزی بی‌صدا گم نشده:
/// هر بخشِ تاریخچه در شاخص عنوان دارد، هر خطِ ⛔ِ شاخص عیناً در تاریخچه هست،
/// تاریخچه کوتاه نشده، و هر نامِ آزمونِ شاخص واقعاً وجود دارد.
/// </summary>
public class ClaudeMdIndexTests
{
    static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "CLAUDE.md"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("ریشهٔ مخزن پیدا نشد");
    }

    static string Read(string rel) => SrcText.Read(Path.Combine(Root(), rel)).Replace("\r\n", "\n");

    static string Norm(string s) => Regex.Replace(s, @"\s+", " ").Trim();

    static IEnumerable<string> Titles(string text) =>
        text.Split('\n').Where(l => l.StartsWith("## ") || l.StartsWith("### "))
            .Select(l => Regex.Replace(l.TrimStart('#').Trim(), @" — آزمون: .*$", ""));

    [Fact]
    public void HarBakhsheTarikhche_DarShakhes_Onvan_Darad()
    {
        var index = Titles(Read("CLAUDE.md")).ToHashSet();
        var missing = Read("native/docs/HISTORY-fa.md").Split('\n')
            .Where(l => l.StartsWith("## ")).Select(l => l[3..].Trim())
            .Where(t => !index.Contains(t)).ToList();
        Assert.True(missing.Count == 0, "بخشِ بی‌عنوان در شاخص:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void HarKhatteGhadghanShakhes_EynanDarTarikhche_Ast()
    {
        var hist = Norm(Read("native/docs/HISTORY-fa.md"));
        var bad = new List<string>();
        foreach (var l in Read("CLAUDE.md").Split('\n'))
        {
            if (!l.StartsWith("- ⛔")) continue;
            var t = Regex.Replace(l[2..], @" \(\+\d+ ⛔ در تاریخچه\)$", "");
            t = t.TrimEnd('…');
            // فقط پیشوندی که پیش از بُرش بود؛ «- » و نشانهٔ بُرش در تاریخچه نیستند.
            if (!hist.Contains(Norm(t))) bad.Add(l.Length > 90 ? l[..90] : l);
        }
        Assert.True(bad.Count == 0, "خطِ ⛔ که در تاریخچه نیست:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void Tarikhche_KutahNashode_Va_Shakhes_KutahAst()
    {
        var hist = Read("native/docs/HISTORY-fa.md");
        Assert.True(hist.Split('\n').Length >= 9300, "تاریخچه کوتاه شده");
        Assert.True(Regex.Matches(hist, "⛔").Count >= 991, "خطوطِ ⛔ِ تاریخچه کم شده");
        Assert.True(Read("CLAUDE.md").Split('\n').Length < 900, "شاخص دوباره دراز شده — شرح جایش در تاریخچه است");
    }

    [Fact]
    public void HarNameAzmoon_DarShakhes_VaghanHast()
    {
        var exist = Directory.EnumerateFiles(Path.Combine(Root(), "native", "PumpYaqobi.Tests"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(SrcText.Read(f), @"class ([A-Za-z0-9]+Tests)\b").Select(m => m.Groups[1].Value))
            .ToHashSet();
        var named = Regex.Matches(Read("CLAUDE.md"), @"`([A-Z][A-Za-z0-9]*Tests)(?:\.[A-Za-z0-9_]+)?`")
            .Select(m => m.Groups[1].Value).Distinct().Where(n => !exist.Contains(n)).ToList();
        Assert.True(named.Count == 0, "آزمونِ نبوده در شاخص: " + string.Join(" · ", named));
    }
}
