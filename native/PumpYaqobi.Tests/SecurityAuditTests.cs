using PumpYaqobi.App.Update;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>ممیزیِ امنیتِ ۱۴۰۵/۰۷/۲۷ — بخش‌هایی که بیرون از ویندوز هم سنجیدنی‌اند.</summary>
public class SecurityAuditTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    [Theory]
    [InlineData("https://github.com/someone-else/pump/releases/download/v9/PumpYaqobi-Setup.exe")]
    [InlineData("https://api.github.com/repos/someone-else/pump/releases/latest")]
    public void DownloadOnlyFromOurRepositories(string url) => Assert.False(UpdateService.AllowedUrl(url));

    [Fact]
    public void OurOwnReleaseStillAllowed() =>
        Assert.True(UpdateService.AllowedUrl("https://github.com/" + UpdateService.Owner + "/x/releases/download/v9/a.exe"));

    [Fact]
    public void StationTokenNeverGoesToADiscoveredHost()
    {
        var src = SrcText.Read(Path.Combine(Root(), "PumpYaqobi.Shell", "Services", "StationLink.cs"));
        Assert.Contains("file.AutoEnroll && token.Length == 0 && pin.Trim().Length == 0", src);
    }
}
