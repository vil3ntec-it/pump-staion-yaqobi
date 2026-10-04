using PumpYaqobi.App.Update;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ نامِ برنامه: «پمپ بنزین»، و پس از ساختنِ حساب نامِ خودِ پمپ (۱۴۰۵/۰۷/۱۵) ══
/// خواستهٔ صاحب ریپو: «برنامه اسمش پمپ یعقوبی نباشد، پمپ بنزین خالی نوشته
/// باشد، و یارو وقتی حساب می‌زند و اسمِ پمپ را می‌نویسد، اسمِ پمپ همان باشد.»
/// </summary>
public class PumpBrandTests
{
    private static readonly string Native =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] p) => SrcText.Read(Path.Combine(new[] { Native }.Concat(p).ToArray()));

    [Theory]
    [InlineData(null, "پمپ بنزین")]
    [InlineData("", "پمپ بنزین")]
    [InlineData("   ", "پمپ بنزین")]
    [InlineData("پمپ یعقوبی", "پمپ بنزین")]          // پیش‌فرضِ کهنه، نه نامِ واقعی
    [InlineData("  پمپ بنزینِ کریمی ", "پمپ بنزینِ کریمی")]
    [InlineData("Karimi Station", "Karimi Station")]
    public void Nam_AzNamePomp_VagarnaPishfarz(string? stored, string want)
        => Assert.Equal(want, PumpBrand.Of(stored));

    /// <summary>⛔ «پمپ یعقوبی» در هیچ نوشتهٔ دیدنیِ برنامه و گزارش‌ها نیست.</summary>
    [Fact]
    public void HichJa_PompYaqobi_NeveshteNashode()
    {
        var bad = new List<string>();
        foreach (var proj in new[] { "PumpYaqobi.App", "PumpYaqobi.Reporting", "PumpYaqobi.Services" })
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Native, proj), "*.*", SearchOption.AllDirectories))
        {
            if (!(f.EndsWith(".cs") || f.EndsWith(".axaml"))) continue;
            if (f.Contains("/obj/") || f.Contains("\\obj\\") || f.Contains("/bin/") || f.Contains("\\bin\\")) continue;
            var n = 0;
            foreach (var line in File.ReadLines(f))
            {
                n++;
                var t = line.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("<!--") || t.StartsWith("/*")) continue;
                if (!line.Contains("پمپ یعقوبی")) continue;
                //  فقط برای **شناختنِ** نامِ کهنه
                if (line.Contains("LegacyDefault") || line.Contains("OldProductName")) continue;
                bad.Add(Path.GetFileName(f) + ":" + n);
            }
        }
        Assert.Empty(bad);
    }

    /// <summary>
    /// ⛔ شورا، ث۷ — همین قاعده برای صفحه‌های بیرونِ برنامه: صفحهٔ دانلود، اپِ
    /// گوشی و صفحهٔ کیو‌آر. فقط نوشتهٔ دیدنی؛ توضیحِ کد (نقلِ گفتهٔ صاحب ریپو) نه.
    /// </summary>
    [Fact]
    public void SafheyeDanlod_AppeGooshi_VaQr_HamPompBenzin()
    {
        var root = Path.GetFullPath(Path.Combine(Native, ".."));
        var files = new List<string> { Path.Combine(root, "index.html"), Path.Combine(root, "sw.js") };
        //  شورا، د۷: «پیام‌رسان» (‎payam/‎) هم — تا ۱۴۰۵/۰۷/۲۰ هنوز «یعقوبی» نشان می‌داد
        foreach (var dir in new[] { "kar", "view", "payam" })
            files.AddRange(Directory.EnumerateFiles(Path.Combine(root, dir), "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".html") || f.EndsWith(".js") || f.EndsWith(".json")));
        var bad = new List<string>();
        foreach (var f in files)
        {
            var inBlock = false; var n = 0;
            foreach (var line in File.ReadLines(f))
            {
                n++;
                var t = line.TrimStart();
                if (inBlock) { if (t.Contains("*/") || t.Contains("-->")) inBlock = false; continue; }
                if (t.StartsWith("/*") || t.StartsWith("<!--"))
                {
                    if (!(t.Contains("*/") || t.Contains("-->"))) inBlock = true;
                    continue;
                }
                if (t.StartsWith("//") || t.StartsWith("*")) continue;
                if (line.Contains("یعقوبی")) bad.Add(Path.GetFileName(f) + ":" + n);
            }
        }
        Assert.True(files.Count > 5, "فایل‌های وب پیدا نشدند");
        Assert.Empty(bad);
    }

    [Fact]
    public void SarbargVaPanjare_AzHamanNam_MiKhanand()
    {
        var w = Read("PumpYaqobi.App", "Views", "MainWindow.axaml");
        Assert.Contains("Title=\"{Binding BrandName}\"", w);
        Assert.Contains("Text=\"{Binding BrandName}\" FontSize=\"21\"", w);
        var host = Read("PumpYaqobi.Shell", "Services", "AppHost.cs");
        //  هر نوشتنِ نامِ پمپ، هر جا — و دفترِ دیگر / بازگردانی ⇒ نامِ همان دفتر
        Assert.Contains("SettingsService.Written += (k, v) => { if (k == SettingsService.StationName) PumpBrand.Set(v); };", host);
        var sw = host[host.IndexOf("public bool UseLedgerOf", StringComparison.Ordinal)..];
        Assert.Contains("RefreshBrand();", sw[..sw.IndexOf("LedgerSwitched?.Invoke();", StringComparison.Ordinal)]);
        Assert.Contains("Settings.Invalidate();", host[host.IndexOf("public void RefreshBrand()", StringComparison.Ordinal)..]);
        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        var reload = vm[vm.IndexOf("public async Task ReloadAllAsync()", StringComparison.Ordinal)..];
        Assert.Contains("RefreshBrand()", reload[..400]);
    }

    [Fact]
    public void Nasab_BaNameTaze_VaMianbarhayeKohneRaBarmidarad()
    {
        var iss = Read("installer", "PumpYaqobi.iss");
        Assert.Contains("#define AppName \"پمپ بنزین\"", iss);
        Assert.Contains("Type: files; Name: \"{autodesktop}\\{#OldName}.lnk\"", iss);
        Assert.Contains("Type: filesandordirs; Name: \"{autoprograms}\\{#OldName}\"", iss);
        Assert.Contains("UsePreviousGroup=no", iss);
        //  شناسه‌ها دست نخوردند — به‌روزرسانی همان نصب را پیدا می‌کند
        Assert.Contains("#define AppGuid \"{{8E86F349-343C-4FFB-983E-BBDDC5390081}\"", iss);
        Assert.Contains("#define AppExe  \"PumpYaqobi.exe\"", iss);
        //  «نصب از فایل» نصابِ نامِ تازه و کهنه هر دو را می‌شناسد
        Assert.True(OfflineInstaller.Decide("پمپ بنزین", "3.1.201.0", "3.1.200").CanRun);
        Assert.Equal(OfflineInstaller.Verdict.Older, OfflineInstaller.Decide("پمپ یعقوبی", "3.1.199.0", "3.1.200").Kind);
    }
}
