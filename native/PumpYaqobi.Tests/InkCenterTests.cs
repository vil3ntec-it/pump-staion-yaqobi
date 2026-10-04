using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ وسط‌چینی با جوهر، نه با حدس (۱۴۰۵/۰۷/۱۹) ══════════════════════════════
/// رفتار را ‎centerlab‎ و ‎oldmonths‎ با پیکسلِ واقعی می‌سنجند؛ این‌جا قاعده‌های
/// سورس قفل می‌شوند تا حدسِ پیشین برنگردد.
/// </summary>
public class InkCenterTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        return d!.FullName;
    }

    private static string Fix()
    {
        var s = SrcText.Read(Path.Combine(Root(), "PumpYaqobi.App", "Controls", "RtlTrim.cs"));
        var i = s.IndexOf("public static double CenterFix(TextBlock t)", StringComparison.Ordinal);
        var j = s.IndexOf("private static void Recenter(TextBlock t)", i, StringComparison.Ordinal);
        Assert.True(i > 0 && j > i);
        return s[i..j];
    }

    [Fact]
    public void DadehyeGhadimiBaFaseleh_HamVasatMiravad()
    {
        var f = Fix();
        //  ⛔ نوشتهٔ با فاصلهٔ پایانی دیگر رها نمی‌شود
        Assert.DoesNotContain("char.IsWhiteSpace(t.Text[^1])) return 0", f);
        Assert.Contains("Blank(text[a])", f);
        Assert.Contains("Blank(text[b])", f);
        //  نویسه‌های بی‌جوهر: فاصله، نیم‌فاصله، جهت‌نماها
        Assert.Contains("'\\u200c'", f);
        Assert.Contains("'\\u200f'", f);
    }

    [Fact]
    public void JoharAzKhodeTextLayout_NevisehBeNeviseh()
    {
        var f = Fix();
        //  ⛔ یک‌جا فقط تکهٔ اولِ خطِ چندتکه را می‌دهد — نویسه‌به‌نویسه
        Assert.Contains("for (var i = a; i <= b; i++)", f);
        Assert.Contains("tl.HitTestTextRange(i, n)", f);
        Assert.DoesNotContain("HitTestTextRange(a, b - a + 1)", f);
        //  ⛔ حدسِ پیشین برنگشت
        Assert.DoesNotContain("WidthIncludingTrailingWhitespace - line.Width", f);
    }

    [Fact]
    public void MatneKarbar_DastNemikhorad()
    {
        var f = Fix();
        Assert.DoesNotContain(".Text =", f);
        Assert.DoesNotContain("Trim()", f);
    }
}
