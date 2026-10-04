using System.Linq;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پروانهٔ اختصاصی · سایتی که کدِ برنامه را منتشر نمی‌کند · خزندهٔ هوش مصنوعی ══
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۶): «برنامه کرک نشه… هوش مصنوعی نتونه برنامه رو
/// ببینه… برنامه سیستم کپی‌رایت داشته باشه.» سه سوراخِ واقعی پیدا شد و بسته شد:
///   ۱) پروانهٔ مخزن «Unlicense» بود — یعنی کد رسماً به مالکیتِ عمومی داده شده بود.
///   ۲) سایت (`path: '.'`) کلِ مخزن را منتشر می‌کرد: کدِ برنامه، CLAUDE.md و سندِ
///      ممیزیِ امنیتی روی yaqobipump.top خواندنی بودند.
///   ۳) هیچ robots.txtی نبود.
/// </summary>
public class CopyrightExposureTests
{
    private static readonly string Repo =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        SrcText.Read(Path.Combine(new[] { Repo }.Concat(parts).ToArray()));

    [Fact]
    public void Parvaneh_Ekhtesasi_Ast_NaMalekiyateOmoomi()
    {
        var lic = Read("LICENSE");
        Assert.DoesNotContain("public domain", lic);
        Assert.DoesNotContain("unencumbered", lic);
        Assert.Contains("All Rights Reserved", lic);
        Assert.Contains("reverse engineer", lic);
        Assert.Contains("artificial-intelligence", lic);
        Assert.Contains("VILL3N", Read("native", "PumpYaqobi.App", "PumpYaqobi.App.csproj"));
        Assert.Contains("AppCopyright=", Read("native", "installer", "PumpYaqobi.iss"));
    }

    [Fact]
    public void Sayt_FaghatFehresteSefid_RaMontasherMikonad()
    {
        var wf = Read(".github", "workflows", "deploy-pages.yml");
        Assert.DoesNotContain("path: '.'", wf);
        Assert.Contains("path: '_site'", wf);
        // هیچ پوشهٔ کد یا سندی در فهرستِ سفید نیست
        var line = wf.Split('\n').First(l => l.Contains("for d in ") && l.Contains("kar"));
        foreach (var no in new[] { "native", "tools", "android", "desktop", "server", "archive", "homelab-panel", "ai-support", "webvault" })
            Assert.DoesNotContain(no, line);
        foreach (var yes in new[] { "kar", "view", "downloads", "tablighat" })
            Assert.Contains(yes, line);
        Assert.Contains("'*.cs'", wf);
    }

    [Fact]
    public void KhazandeyeHooshMasnooi_Rah_Nadarad()
    {
        var robots = Read("robots.txt");
        foreach (var bot in new[] { "GPTBot", "ClaudeBot", "anthropic-ai", "Google-Extended", "CCBot", "PerplexityBot" })
            Assert.Contains("User-agent: " + bot, robots);
        Assert.Contains("Disallow: /kar/", robots);
        Assert.Contains("Disallow: /view/", robots);
        Assert.Contains("noai", Read("kar", "index.html"));
        Assert.Contains("noai", Read("view", "index.html"));
    }
}
