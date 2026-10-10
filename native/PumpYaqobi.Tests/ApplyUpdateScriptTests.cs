using System.Diagnostics;
using PumpYaqobi.App.Update;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «می‌گوید بسته و دوباره باز می‌شود؛ هرگز باز نمی‌شود و آپدیتی نشده» (۱۴۰۵/۰۷/۲۲) ══
/// اسکریپتِ جای‌گزینیِ بستهٔ زیپ (<see cref="UpdateService.ApplyScript"/>). روی ویندوزِ
/// واقعی (‎test-windows‎) **اجرا** می‌شود، همان‌طور که برنامه اجرایش می‌کند: ‎cmd.exe‎ِ
/// بی‌پنجره (‎CreateNoWindow‎)، با پروسه‌ای که هنوز زنده است.
/// </summary>
public class ApplyUpdateScriptTests
{
    [Fact]
    public void Entezar_BaPing_NaTimeout_VaBadAzMohlat_Kill()
    {
        var s = UpdateService.ApplyScript(1234, "S", "A", "R", "echo relaunch", "T");
        //  ⛔ ‎timeout‎ در پروسهٔ بی‌پنجره همان لحظه بیرون می‌رود («Input redirection is not supported»)
        Assert.DoesNotContain("timeout ", s);
        Assert.Contains("ping -n 2 127.0.0.1 >nul", s);
        Assert.Contains("taskkill /F /PID 1234", s);
        //  ترتیب: انتظار ⇐ کشتن ⇐ کپی ⇐ دوباره باز کردن
        int i(string x) => s.IndexOf(x, StringComparison.Ordinal);
        Assert.True(i(":wait") < i("taskkill") && i("taskkill") < i("robocopy") && i("robocopy") < i("echo relaunch"));
    }

    /// <summary>
    /// ⛔ روی ویندوز: پروسه‌ای که «بسته» نشده ولی زنده مانده کشته می‌شود، فایل‌ها جای‌گزین
    /// می‌شوند و خطِ دوباره‌باز‌کردن می‌دود — همه در پروسهٔ بی‌پنجره.
    /// </summary>
    [Fact]
    public void RuyeWindows_PorosesheZende_Koshte_FileJaygozin_VaDobareBaz()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "pyq-apply-" + Guid.NewGuid().ToString("N")[..8]);
        var src = Path.Combine(root, "staging");
        var app = Path.Combine(root, "app");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(app, "PumpYaqobi.dll"), "old");
        File.WriteAllText(Path.Combine(src, "PumpYaqobi.dll"), "new");
        var marker = Path.Combine(root, "relaunched.txt");
        var result = Path.Combine(root, "result.txt");

        //  «برنامهٔ کهنه» که هرگز خودش نمی‌رود
        using var stuck = Process.Start(new ProcessStartInfo("ping", "-n 600 127.0.0.1")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        var script = Path.Combine(root, "apply-update.cmd");
        File.WriteAllText(script,
            UpdateService.ApplyScript(stuck.Id, src, app, result, $"(echo ok)>\"{marker}\"", src),
            new System.Text.UTF8Encoding(false));

        var sw = Stopwatch.StartNew();
        using var cmd = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/s /c \"\"" + script + "\"\"",
            WorkingDirectory = app,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        Assert.True(cmd.WaitForExit(180_000), "اسکریپت تمام نشد");

        //  انتظارِ واقعی (~۶۰ ثانیه)، نه ~۲۰ ثانیهٔ ‎timeout‎ِ شکسته
        Assert.True(sw.Elapsed > TimeSpan.FromSeconds(45), "انتظار " + sw.Elapsed.TotalSeconds + " ثانیه بود");
        Assert.True(stuck.HasExited, "پروسهٔ کهنه زنده ماند");
        Assert.Equal("new", File.ReadAllText(Path.Combine(app, "PumpYaqobi.dll")));
        Assert.True(File.Exists(marker), "خطِ دوباره‌باز‌کردن ندوید");
        Assert.False(File.Exists(result), "robocopy شکست خورد");
        try { Directory.Delete(root, true); } catch { }
    }
}
