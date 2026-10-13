namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «بعدِ هر تغییر چک بشه» — چک‌های پیش از مرج سرِ جایشان‌اند (۱۴۰۵/۰۷/۱۵) ══
/// اگر کسی روزی ورک‌فلوی سنجه را بردارد یا کوچکش کند، همین‌جا سرخ می‌شود.
/// </summary>
public class CiGateTests
{
    private static string Wf(string name) => SrcText.Read(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".github", "workflows", name));

    [Fact]
    public void HamanBarname_PishAzMerj_RuyeWindows_Nasb_Va_Baz_Mishavad()
    {
        var w = Wf("app-smoke.yml");
        Assert.Contains("pull_request:", w);
        Assert.Contains("'native/**'", w);
        //  همان ساختِ انتشار، نه بارِ ساختگی
        Assert.Contains("dotnet publish PumpYaqobi.App", w);
        Assert.Contains("native/installer/PumpYaqobi.iss", w);
        Assert.Contains("seal.ps1", w);
        //  روی هر ویندوزی که گیت‌هاب دارد
        foreach (var os in new[] { "windows-2022", "windows-2025", "windows-11-arm" })
            Assert.Contains(os, w);
        //  نصب با ویزارد، باز شدن، اطلاعات، دوبار-کلیک، ۳۲بیتی، حذف
        Assert.Contains("drive-wizard.ps1", w);
        Assert.Contains("PYSEAL state=1", w);
        Assert.Contains("RunApp 'بارِ اول'", w);
        Assert.Contains("RunApp '۳۲بیتی'", w);
        Assert.Contains("smoke-test.pumpyaqobi", w);
        Assert.Contains("/ARCH=x86 /DIR=`\"$dir`\"", w);   //  مسیر با گیومه، مثلِ خودِ برنامه
        Assert.Contains("UninstallString", w);
        Assert.Contains("(AppDomain|UI|Startup)", w);
        Assert.Contains("exit 1", w);
        //  ۱۴۰۵/۰۷/۲۱: «به‌روزرسانی از داخلِ برنامه» سر تا ته — بسته شد، نسخهٔ تازه، خودش باز شد
        Assert.Contains("/DAppVersion=9.9.10", w);
        Assert.Contains("PUMP_SELFTEST_UPDATE", w);
        Assert.Contains("\"برنامه خودش دوباره باز شد\"", w);
        var auto = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "PumpYaqobi.App", "Update", "AutoUpdate.cs")));
        Assert.Contains("await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => InstallAndExitAsync(info, path));", auto);
        Assert.Contains("Environment.SetEnvironmentVariable(SelfTestEnv, null);", auto);
    }

    [Fact]
    public void AzmunHa_RuyeHarDoSistem_PishAzMerj()
    {
        var t = Wf("test-native.yml");
        Assert.Contains("pull_request:", t);
        Assert.Contains("test-windows", t);
    }
}
