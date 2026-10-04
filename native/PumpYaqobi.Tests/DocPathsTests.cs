using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// شورا، ج۱ — هر مسیری که ‎README.md‎ و ‎ARCHITECTURE-fa.md‎ نام می‌برند واقعاً
/// هست. سندی که آدم را دنبالِ فایلی بفرستد که نیست، از نبودنش بدتر است.
/// </summary>
public class DocPathsTests
{
    private static string Repo => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static IEnumerable<object[]> Docs => new[]
    {
        new object[] { "README.md" },
        new object[] { "native/docs/ARCHITECTURE-fa.md" },
    };

    [Theory]
    [MemberData(nameof(Docs))]
    public void HarMasireNamborde_Hast(string doc)
    {
        var text = SrcText.Read(Path.Combine(Repo, doc));
        var paths = Regex.Matches(text, "`([^`\\s]+)`").Select(m => m.Groups[1].Value)
            .Where(p => p.Contains('/') && !p.Contains("://") && !p.StartsWith("/api") && !p.StartsWith("?"))
            .Distinct().ToList();
        Assert.True(paths.Count >= (doc == "README.md" ? 5 : 30), $"{doc}: فقط {paths.Count} مسیر پیدا شد");
        var missing = paths.Where(p => !File.Exists(Path.Combine(Repo, p)) && !Directory.Exists(Path.Combine(Repo, p))).ToList();
        Assert.True(missing.Count == 0, doc + " ⇒ نیست: " + string.Join(" · ", missing));
    }

    [Fact]
    public void HarPusheyeRishe_DarReadme_Ast()
    {
        var readme = SrcText.Read(Path.Combine(Repo, "README.md"));
        var dirs = Directory.GetDirectories(Repo).Select(Path.GetFileName)
            .Where(d => d is not null && !d.StartsWith('.') && d != "node_modules").ToList();
        var missing = dirs.Where(d => !readme.Contains("`" + d + "/`", StringComparison.Ordinal)).ToList();
        Assert.True(missing.Count == 0, "پوشهٔ ریشهٔ بی‌توضیح در README: " + string.Join(" · ", missing));
    }
}
