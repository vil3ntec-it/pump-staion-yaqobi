using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ آزمونی که روی لینوکس سبز است، روی ویندوز هم سبز باشد ══════════════════
///
/// ساختِ main ِ نسخهٔ ۳.۱.۱۸۶ روی رانرِ ویندوز افتاد، در حالی که همان کد در PR
/// روی لینوکس سبز بود: رانرِ ویندوز (core.autocrlf=true) پرونده‌های کد را CRLF
/// می‌گرفت و آزمونی که «N نویسه بعد از فلان» را می‌خواند آن‌جا تکهٔ کوتاه‌تری
/// می‌دید. زیانش فقط یک سرخی نبود: هیچ نسخه‌ای منتشر نشد.
///
/// ریشه یک جا بسته شد — `.gitattributes` پرونده‌هایی را که آزمون‌ها می‌خوانند
/// روی هر سیستمی LF نگه می‌دارد — و این‌جا قفل می‌شود، هم در خودِ قاعده و هم
/// در **آن‌چه واقعاً روی دیسکِ همین رانر نشسته**.
/// </summary>
public class LineEndingTests
{
    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, ".gitattributes")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static List<string[]> Rules() =>
        File.ReadAllLines(Path.Combine(RepoRoot(), ".gitattributes"))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

    [Theory]
    [InlineData("*.cs")]
    [InlineData("*.axaml")]
    [InlineData("*.yml")]
    [InlineData("*.md")]
    public void KodiKeAzmoonMikhanad_RooyeHarSystemi_LF_Ast(string pattern)
    {
        Assert.Contains(Rules(), p => p.Length >= 2 && p[0] == pattern && p.Contains("eol=lf"));
    }

    /// <summary>
    /// ⛔ آن طرفِ سکه: اسکریپت‌های ویندوز باید CRLF بمانند، وگرنه ‎cmd.exe‎ آن‌ها را
    /// درست اجرا نمی‌کند. قاعدهٔ تازه نباید این یکی را بخورد.
    /// </summary>
    [Theory]
    [InlineData("*.bat")]
    [InlineData("*.cmd")]
    public void EskriptHayeWindows_HanozCRLF_Ast(string pattern)
    {
        Assert.Contains(Rules(), p => p.Length >= 2 && p[0] == pattern && p.Contains("eol=crlf"));
    }

    /// <summary>
    /// خودِ پرونده‌های روی دیسکِ همین رانر — نه فقط متنِ قاعده. روی لینوکس همیشه
    /// سبز است؛ روی رانرِ ویندوز فقط وقتی سبز است که قاعده واقعاً اثر کرده باشد.
    /// </summary>
    [Fact]
    public void ParvandeHayeRooyeDisk_HichCR_Nadarand()
    {
        var root = RepoRoot();
        foreach (var rel in new[]
                 {
                     Path.Combine("native", "PumpYaqobi.App", "Update", "UpdateService.cs"),
                     Path.Combine("native", "PumpYaqobi.App", "Views", "MainWindow.axaml"),
                     Path.Combine(".github", "workflows", "build-native.yml"),
                 })
        {
            var text = File.ReadAllText(Path.Combine(root, rel));
            Assert.False(text.Contains('\r'), $"{rel} روی این دیسک CRLF دارد — .gitattributes اثر نکرده است");
        }
    }
}
