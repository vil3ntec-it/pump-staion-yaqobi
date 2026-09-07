using System.Text.Json;
using PumpYaqobi.Application.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بندِ ۱۵: شناختِ لینکِ دوربین ═══════════════════════════════════════════
/// خودِ ‎index.html‎ در یک کرومیومِ واقعی برای ۲۹ لینک گفته که هر کدام چه‌جور
/// تصویری است؛ این آزمون همان‌ها را از C# می‌پرسد.
///
/// چرا این‌قدر مهم است: اشتباه در همین یک تصمیم یعنی «دوربینِ کاربر تصویر
/// نمی‌آید» — و کاربر هیچ راهی ندارد بفهمد چرا.
/// </summary>
public class CameraParityTests
{
    private sealed record KindCase(string url, string kind);
    private sealed record BustCase(string url, string @out);
    private sealed record Golden(List<KindCase> kinds, List<BustCase> bust);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-camera.json");
        if (!File.Exists(p)) p = "golden-camera.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    private static CameraKind Parse(string s) => s switch
    {
        "none" => CameraKind.None,
        "rtsp" => CameraKind.Rtsp,
        "hls" => CameraKind.Hls,
        "video" => CameraKind.Video,
        "image" => CameraKind.Image,
        _ => CameraKind.WebPage,
    };

    [Fact]
    public void LinkKind_MatchesTheHtmlExactly()
    {
        foreach (var c in Load().kinds)
            Assert.Equal(Parse(c.kind), CameraService.KindOf(c.url));
    }

    /// <summary>
    /// ‎…/cgi-bin/hls/index.m3u8‎ هم به HLS می‌خورد هم به «عکس». نسخهٔ وب HLS
    /// را جلوتر می‌سنجد، پس HLS برنده است. اگر روزی کسی ترتیب را عوض کند،
    /// همین آزمون قرمز می‌شود.
    /// </summary>
    [Fact]
    public void HlsWins_WhenTheLinkAlsoLooksLikeASnapshot()
    {
        Assert.Equal(CameraKind.Hls,
            CameraService.KindOf("http://192.168.1.9/cgi-bin/hls/index.m3u8"));
    }

    [Fact]
    public void CacheBuster_MatchesTheHtmlExactly()
    {
        foreach (var c in Load().bust)
            Assert.Equal(c.@out, CameraService.CacheBusted(c.url, 1700000000000L));
    }

    /// <summary>
    /// پسوند باید واقعاً تهِ لینک (یا پیش از ‎?‎ و ‎#‎) باشد — نه هر جای متن.
    /// فاصله‌های دو سر هم مثلِ نسخهٔ وب بریده می‌شوند.
    /// </summary>
    [Fact]
    public void ExtensionMustEndTheLink_AndEdgesAreTrimmed()
    {
        Assert.Equal(CameraKind.Video, CameraService.KindOf("  http://cam.local/a.mp4\n"));
        Assert.Equal(CameraKind.WebPage, CameraService.KindOf("http://cam.local/a.mp4.txt"));
        Assert.Equal(CameraKind.Hls, CameraService.KindOf("http://cam.local/a.m3u8#x"));
        Assert.Equal(CameraKind.None, CameraService.KindOf("   "));
    }

    [Fact]
    public void OnlySnapshotStreams_ArePlayedInsideTheApp()
    {
        Assert.True(CameraService.ShowsInApp(CameraKind.Image));
        foreach (var k in new[] { CameraKind.None, CameraKind.Rtsp, CameraKind.Hls,
                                  CameraKind.Video, CameraKind.WebPage })
            Assert.False(CameraService.ShowsInApp(k));
    }

    [Fact]
    public void DefaultName_CountsFromOne()
    {
        Assert.Equal("دوربین 1", CameraService.DefaultName(0));
        Assert.Equal("دوربین 4", CameraService.DefaultName(3));
    }
}
