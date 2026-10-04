using System.Linq;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ صفحهٔ اصلیِ سایت = دریافتِ برنامه‌های تازه؛ سایتِ قدیم در انباری ══════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴) با عکسِ «ورود به سیستم · نسخهٔ ۲.۹.۴۶۶»: «این
/// چیه که اومد؟ این مالِ صدها نسخه قبل بود — اینو فراموش کن و بزار تو انباری…
/// برنامهٔ جدید با سایتِ جدید کو، اونو بده.»
///
/// این سنجه‌ها روی خودِ فایل‌ها می‌گردند: سایتِ قدیم در archive/old-site هست
/// ولی منتشر نمی‌شود، صفحهٔ اصلی فقط برنامه‌های تازه را می‌دهد، سرویس‌ورکرِ
/// قدیم از گوشی‌ها برداشته می‌شود، و ورک‌فلوی قدیمیِ ویندوز کانالِ
/// به‌روزرسانیِ برنامهٔ تازه را خودکار بازنویسی نمی‌کند.
/// </summary>
public class SiteHomeTests
{
    private static readonly string Repo =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        SrcText.Read(Path.Combine(new[] { Repo }.Concat(parts).ToArray()));

    [Fact]
    public void SayteGhadim_DarAnbari_Ast()
    {
        var old = Read("archive", "old-site", "index.html");
        Assert.Contains("APP_VERSION", old);
        Assert.True(File.Exists(Path.Combine(Repo, "archive", "old-site", "sw.js")));
    }

    [Fact]
    public void SafheyeAsli_FaghatBarnamehayeTaze_RaMidahad()
    {
        var home = Read("index.html");
        Assert.Contains("./downloads/PumpYaqobi-Setup.exe", home);
        Assert.Contains("./downloads/PumpYaqobiKar.apk", home);
        Assert.Contains("./kar/", home);

        //  ⛔ هیچ ردی از سایتِ قدیم
        Assert.DoesNotContain("APP_VERSION", home);
        Assert.DoesNotContain("PumpYaqobi.apk\"", home);
        Assert.DoesNotContain("serviceWorker.register", home);
        //  ⛔ و هیچ نشانیِ مخزن
        Assert.DoesNotContain("github", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/releases/", home);
    }

    [Fact]
    public void SwJs_FaghatKasheSayteGhadim_RaPakMikonad()
    {
        var sw = Read("sw.js");
        Assert.Contains("indexOf('pump-yaqobi-') === 0", sw);
        Assert.Contains("registration.unregister()", sw);
        //  ⛔ هیچ درخواستی گرفته نمی‌شود، و کشِ اپ‌های دیگر دست نمی‌خورد
        Assert.DoesNotContain("addEventListener('fetch'", sw);
        Assert.Contains("keys.filter(", sw);
    }

    [Fact]
    public void Deploy_SayteGhadim_Va_ApkGhadim_RaMontasherNemikonad()
    {
        var yml = Read(".github", "workflows", "deploy-pages.yml");
        Assert.Contains("rm -rf archive", yml);
        Assert.DoesNotContain("grab('android-latest'", yml);
        Assert.DoesNotContain("open('index.html'", yml);
    }

    [Fact]
    public void BarnameyeGhadimeWindows_KanaleBehRoozresani_RaKhodkar_NemiGirad()
    {
        //  build-desktop.yml برنامهٔ قدیم را روی desktop-latest منتشر می‌کند —
        //  همان برچسبی که برنامهٔ تازه از آن به‌روزرسانی می‌گیرد.
        var yml = Read(".github", "workflows", "build-desktop.yml");
        var on = yml[yml.IndexOf("\non:", StringComparison.Ordinal)..yml.IndexOf("\npermissions:", StringComparison.Ordinal)];
        Assert.Contains("workflow_dispatch", on);
        Assert.DoesNotContain("push:", on);
    }
}
