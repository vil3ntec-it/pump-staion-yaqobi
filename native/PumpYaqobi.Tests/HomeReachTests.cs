using System.Net;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «برنامه به سرورِ خانگی نمی‌رسد و خانه‌اش سبز نمی‌شود» (۱۴۰۵/۰۷/۱۳) ══════
///
/// سنجهٔ زنده‌اش ‎lanreach‎ است (سرورِ خانگیِ واقعی). این‌جا قاعده‌های خالص و
/// سورسی که آن سه باگ را بسته‌اند:
///   ۱) اولین جوابِ کشف برداشته می‌شد، نه نشانیِ رسیدنی؛
///   ۲) نشانیِ مُرده هیچ‌وقت دوباره گشته نمی‌شد (چراغ برای همیشه سرخ)؛
///   ۳) ‎127.0.0.1‎ به گوشیِ کارمند داده می‌شد.
/// </summary>
public class HomeReachTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    [Theory]
    [InlineData("http://127.0.0.1:4700", true)]
    [InlineData("http://localhost:4700", true)]
    [InlineData("http://[::1]:4700", true)]
    [InlineData("http://192.168.1.5:4700", false)]
    [InlineData("ws://10.0.0.2:4700", false)]
    [InlineData("", false)]
    public void Loopback_DorostShenakhteMishavad(string url, bool loop) =>
        Assert.Equal(loop, ServerFinder.IsLoopbackUrl(url));

    [Fact]
    public void NeshaniyeShabake_Az127_Jolotar_Ast()
    {
        var found = new[]
        {
            new FoundServer("a", "http://127.0.0.1:4700", "s1"),
            new FoundServer("a", "http://192.168.1.5:4700", "s1"),
        };
        Assert.Equal("http://192.168.1.5:4700", ServerFinder.Order(found).First().Url);
    }

    [Fact]
    public void JavabAz127_NeshaniyeShabakeRa_BarayeGooshi_Migirad()
    {
        const string card = "{\"reply\":\"PUMP-SERVER-HERE\",\"id\":\"s1\",\"name\":\"خانه\",\"port\":4700,\"url\":\"http://192.168.1.5:4700\"}";
        var viaLoop = ServerFinder.Parse(card, IPAddress.Loopback)!;
        Assert.Equal("http://127.0.0.1:4700", viaLoop.Url);
        Assert.Equal("http://192.168.1.5:4700", viaLoop.LanUrl);   // ⛔ نه 127.0.0.1

        var viaLan = ServerFinder.Parse(card, IPAddress.Parse("192.168.1.5"))!;
        Assert.Equal("http://192.168.1.5:4700", viaLan.Url);
        Assert.Equal(viaLan.Url, viaLan.LanUrl);
    }

    [Fact]
    public void SabteKhodkar_NeshaniyeResidani_RaMigirad_NaAvvalinJavab()
    {
        var src = Read("PumpYaqobi.App", "Services", "StationLink.cs");
        Assert.DoesNotContain("ServerFinder.FindFirstAsync(", src);
        Assert.Contains("ServerFinder.FindReachableAsync(ct: ct)", src);
    }

    [Fact]
    public void NeshaniyeMorde_DobareGashte_Mishavad()
    {
        var src = Read("PumpYaqobi.App", "Services", "StationPublisher.cs");
        var at = src.IndexOf("if (!await _sync.ConnectAsync(ct))", StringComparison.Ordinal);
        Assert.True(at > 0);
        var body = src[at..(at + 1800)];
        //  ⛔ وصل نشد ⇒ با `force` دوباره ثبت (که کشفِ خودکار را هم می‌زند)، با ترمزِ خودش
        Assert.Contains("StationLink.EnsureAsync(_host, force: true, ct: ct)", body);
        Assert.Contains("_lastRepairTry", body);
        Assert.Contains("await _sync.DropAsync();", body);
    }

    [Fact]
    public void BeGooshi_Hargez127_DadeNemishavad()
    {
        var home = Read("PumpYaqobi.App", "Services", "HomeLink.cs");
        Assert.Contains("public static string ShareUrl(AppHost host)", home);
        Assert.Contains("HomeLink.ShareUrl(host),", Read("PumpYaqobi.App", "Services", "KarLink.cs"));
        Assert.Contains("var url = HomeLink.ShareUrl(_host);", Read("PumpYaqobi.App", "Services", "StationPublisher.cs"));
    }

    [Fact]
    public void CheragheTanzimNashode_BanBast_Nist_VaDoroghNemigooyad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        //  یافتنِ سرورِ خانگی هیچ حسابی نمی‌خواهد
        Assert.DoesNotContain("\"سرورِ خانگی هنوز تنظیم نشده — از پروفایل وارد شوید\"", vm);
        Assert.DoesNotContain("if (sync is null || !sync.Configured)\n        {\n            AppHost.Current.Toast(\"سرورِ خانگی تنظیم نشده", vm);
        var check = vm[vm.IndexOf("private async Task CheckServerAsync()", StringComparison.Ordinal)..];
        check = check[..check.IndexOf("\n    }", StringComparison.Ordinal)];
        Assert.DoesNotContain("!sync.Configured", check);
    }
}
