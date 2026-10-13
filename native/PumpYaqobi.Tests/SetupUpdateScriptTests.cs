using System.Diagnostics;
using PumpYaqobi.App.Update;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «از داخلِ برنامه به‌روز می‌کنم؛ بسته می‌شود و دیگر باز نمی‌شود» (۳.۱.۲۷۲) ══
/// مسیرِ نصاب (‎.exe‎) باز کردنِ دوباره را به نصاب می‌سپرد و نصابِ ناموفق برنامه را بسته
/// می‌گذاشت. حالا اسکریپتِ بیرونی (<see cref="UpdateService.SetupScript"/>) صبر می‌کند،
/// نصاب را تا پایان اجرا می‌کند، کدِ شکست را می‌نویسد و <b>همیشه</b> دوباره باز می‌کند.
/// </summary>
public class SetupUpdateScriptTests
{
    [Fact]
    public void Arguman_BiSeda_BiPayam_BiRelaunchDoJa()
    {
        var a = UpdateService.SetupArgs("/ARCH=x86", @"C:\Program Files\Pump (Ali)");
        Assert.Contains("/SILENT", a);
        //  ⛔ هیچ پیامی نصاب را در ‎/SILENT‎ نگه نمی‌دارد
        Assert.Contains("/SUPPRESSMSGBOXES", a);
        Assert.Contains("/NORESTART", a);
        Assert.Contains("/ARCH=x86", a);
        Assert.Contains("/DIR=\"C:\\Program Files\\Pump (Ali)\"", a);
        //  ⛔ باز کردن کارِ اسکریپت است — نصاب نباید دوباره بازش کند (دو نمونه)
        Assert.DoesNotContain("/RELAUNCH", a);
        Assert.DoesNotContain("/RESTARTAPPLICATIONS", a);
    }

    [Fact]
    public void Tartib_Entezar_Nasab_Natije_VaHamishe_DobareBaz()
    {
        var s = UpdateService.SetupScript(4321, @"C:\u\PumpYaqobi-9.9.9.exe", "/SILENT /DIR=\"D\"", @"C:\r.txt", "echo relaunch");
        int i(string x) => s.IndexOf(x, StringComparison.Ordinal);
        Assert.StartsWith("@echo off", s);
        Assert.Contains("taskkill /F /PID 4321", s);
        Assert.Contains("start \"\" /wait \"C:\\u\\PumpYaqobi-9.9.9.exe\" /SILENT /DIR=\"D\"", s);
        Assert.Contains("echo setup:%RC%", s);
        Assert.True(i(":wait") < i("taskkill") && i("taskkill") < i("start \"\" /wait") && i("start \"\" /wait") < i("set RC=")
                    && i("set RC=") < i("echo relaunch"));
        //  ⛔ خطِ باز کردن هیچ شرطی ندارد: نصاب گرفت یا نه، برنامه برمی‌گردد
        var line = s.Split('\n').First(l => l.Contains("echo relaunch"));
        Assert.Equal("echo relaunch", line.Trim());
    }

    [Fact]
    public void PayameShekast_Farsi_VaNasabAzZipJodast()
    {
        Assert.Equal("", UpdateService.FailureDetail(""));
        Assert.Equal("", UpdateService.FailureDetail("0"));
        Assert.Contains("فایل‌ها جای‌گزین نشدند", UpdateService.FailureDetail("8"));
        var d = UpdateService.FailureDetail("setup:5\r\n");
        Assert.Contains("نصب‌کنندهٔ نسخهٔ تازه کامل نشد", d);
        Assert.Contains("کدِ 5", d);
    }

    /// <summary>
    /// ⛔ روی ویندوز، واقعاً: «نصاب»ی که شکست می‌خورد (کدِ ۵) ⇒ کد نوشته می‌شود و خطِ
    /// دوباره‌باز‌کردن باز هم می‌دود — در همان ‎cmd.exe‎ِ بی‌پنجره‌ای که برنامه اجرا می‌کند.
    /// </summary>
    [Fact]
    public void RuyeWindows_NasabeNakam_Kod_Neveshte_VaBarname_DobareBaz()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "pyq-setup-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        //  «نصاب»ِ ساختگی: خودِ ‎cmd.exe‎ با کدِ خروجیِ ۵ (همان «کاربر لغو کرد»ِ Inno)
        var fake = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var marker = Path.Combine(root, "relaunched.txt");
        var result = Path.Combine(root, "result.txt");
        var script = Path.Combine(root, "run-setup.cmd");

        //  پروسه‌ای که همین حالا رفته ⇒ انتظار بی‌درنگ تمام می‌شود
        using var gone = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { UseShellExecute = false, CreateNoWindow = true })!;
        gone.WaitForExit();
        File.WriteAllText(script,
            UpdateService.SetupScript(gone.Id, fake, "/c exit 5", result, $"(echo ok)>\"{marker}\""),
            new System.Text.UTF8Encoding(false));
        using var cmd = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/s /c \"\"" + script + "\"\"",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        Assert.True(cmd.WaitForExit(120_000), "اسکریپت تمام نشد");
        Assert.True(File.Exists(marker), "برنامه دوباره باز نشد");
        Assert.Equal("setup:5", File.ReadAllText(result).Trim());
        try { Directory.Delete(root, true); } catch { }
    }

    /// <summary>
    /// ⛔ (۱۴۰۵/۰۷/۲۱) روی ویندوز، واقعاً، با پوشهٔ فارسی و پرانتز — مسیرها فقط آرگومانِ ‎cmd‎اند
    /// (همان ‎ScriptArgs‎ و همان شکلِ ‎RunScript‎) و متنِ اسکریپت ASCII است، بی ‎chcp‎.
    /// </summary>
    [Fact]
    public void RuyeWindows_PusheyeFarsi_MasirhaArguman_BiChcp()
    {
        var text = UpdateService.SetupScript(1, "%~1", UpdateService.SetupArgs("/ARCH=x86", "%~2"), "%~3",
                                             UpdateService.RelaunchLine("%~4", "%~2", elevated: false));
        Assert.True(text.All(c => c < 128), "متنِ اسکریپت باید فقط ASCII باشد");
        Assert.DoesNotContain("chcp", text);
        if (!OperatingSystem.IsWindows()) return;

        var root = Path.Combine(Path.GetTempPath(), "pyq-fa-" + Guid.NewGuid().ToString("N")[..6], "برنامهٔ پمپ (علی)");
        Directory.CreateDirectory(root);
        var marker = Path.Combine(root, "relaunched.txt");
        var result = Path.Combine(root, "نتیجه.txt");
        var script = Path.Combine(root, "run-setup.cmd");
        using var gone = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { UseShellExecute = false, CreateNoWindow = true })!;
        gone.WaitForExit();
        //  «نصاب» = ‎cmd /c exit 7‎ ؛ «باز کردن» = نوشتنِ نشانه در همان پوشهٔ فارسی (‎%~2‎)
        File.WriteAllText(script,
            UpdateService.SetupScript(gone.Id, "%~1", "/c exit 7", "%~3", "(echo ok)>\"%~2\\relaunched.txt\""),
            new System.Text.UTF8Encoding(false));
        var args = UpdateService.ScriptArgs(Path.Combine(Environment.SystemDirectory, "cmd.exe"), root, result, "x");
        using var cmd = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/s /c \"\"" + script + "\" " + args + "\"",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        Assert.True(cmd.WaitForExit(120_000), "اسکریپت تمام نشد");
        Assert.True(File.Exists(marker), "خطِ بازکردن در پوشهٔ فارسی نرسید");
        Assert.Equal("setup:7", File.ReadAllText(result).Trim());
        try { Directory.Delete(Path.GetDirectoryName(root)!, true); } catch { }
    }
}
