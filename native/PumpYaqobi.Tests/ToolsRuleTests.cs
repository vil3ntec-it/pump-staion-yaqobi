using System.Diagnostics;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، د۷ — ابزارهای «سنجهٔ سنجه» خودشان هم سنجیده می‌شوند ═══════════════════
///
/// <c>tools/fix-needs-test.sh</c> با «درست» در هر جای عنوان («درستیِ دفتر») هر PRِ
/// بی‌ربطی را رفعِ باگ می‌خواند. حالا واژهٔ کامل است. و <c>tools/mut.sh</c> (جهش برای
/// دیدنِ دندانِ آزمون) در مخزن است، نه فقط روی ماشینِ یک سیزن.
/// </summary>
public class ToolsRuleTests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    private static string Title(string t)
    {
        var psi = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        psi.ArgumentList.Add(Path.Combine(Root(), "tools", "fix-needs-test.sh"));
        psi.ArgumentList.Add("a"); psi.ArgumentList.Add("b"); psi.ArgumentList.Add(t);
        psi.Environment["FIX_TITLE_ONLY"] = "1";
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEnd().Trim();
        p.WaitForExit();
        return o;
    }

    [Theory]
    [InlineData("اصلاحِ ورق (نسخه 3.1.1)", "fix")]
    [InlineData("باگ‌ها رفع شد", "fix")]
    [InlineData("درست کردنِ چاپ", "fix")]
    [InlineData("درست‌شدنِ زنجیره", "fix")]
    [InlineData("اصلاحاتِ همگام‌سازی", "fix")]
    [InlineData("درستیِ دفتر سنجیده شد", "nofix")]
    [InlineData("نادرست‌خوانی را نشان بده", "nofix")]
    [InlineData("نمودارِ تازه", "nofix")]
    public void RafeBag_BaVazheyeKamel(string title, string want)
    {
        if (!OperatingSystem.IsLinux()) return;   // ‎bash‎ و ‎grep -P‎ِ رانرِ لینوکس — همان جایی که ورک‌فلو می‌دود
        Assert.Equal(want, Title(title));
    }

    [Fact]
    public void Mut_DarMakhzan_VaHamishe_Barmigardanad()
    {
        var mut = File.ReadAllText(Path.Combine(Root(), "tools", "mut.sh"));
        Assert.Contains("trap 'cp \"$BAK\" \"$F\"", mut);           // فایل همیشه برمی‌گردد
        Assert.Contains("بی‌دندان", mut);                            // سبزِ همیشگی گفته می‌شود
        Assert.Contains("exit 2", mut);                               // خطای کامپایل دندان نیست
    }
}
