using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ وسط‌چینیِ نوشتهٔ خانه پس از چیدمانِ نیمه‌کاره گم نشود (۱۴۰۵/۰۷/۲۱) ══
///
/// ‎monthshift all‎ با ‎MS_SCALE=1.25‎ و ‎MS_HIST=safe,amanat,sarrafi‎ گرفتش: پستِ ‎Recenter‎
/// پس از عوض شدنِ متن پیش از چیدمانِ تازه می‌رسید، ‎CenterFix‎ صفر می‌داد، و خانهٔ بازیافتیِ
/// هم‌پهنا (‎Bounds‎ِ بی‌تغییر) دیگر هیچ‌وقت سنجیده نمی‌شد — «هارون بابت …» با تصحیحِ «کریم»ِ
/// پیشین ۶٫۹ پیکسل کج ماند (پیش از اصلاح ۲ ایراد، پس از آن ۰). رفتارش را همان سنجه با پنجرهٔ
/// واقعی می‌سنجد؛ این‌ها ممنوعه‌اند.
/// </summary>
public class RtlTrimPendingTests
{
    private static string Src()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        return File.ReadAllText(Path.Combine(d!.FullName, "PumpYaqobi.App", "Controls", "RtlTrim.cs"));
    }

    private static string Body(string src, string sig)
    {
        var i = src.IndexOf(sig, StringComparison.Ordinal);
        Assert.True(i >= 0, sig);
        var open = src.IndexOf('{', i);
        for (int k = open, depth = 0; k < src.Length; k++)
        {
            if (src[k] == '{') depth++;
            else if (src[k] == '}' && --depth == 0) return src[open..(k + 1)];
        }
        return src[open..];
    }

    /// <summary>⛔ چیدمانِ نیمه‌کاره ⇒ منتظرِ چیدمان بمان، نه «صفر» (که تصحیح را پاک یا کهنه می‌گذاشت).</summary>
    [Fact]
    public void ChidemaneNimekare_MontazerMimanad()
    {
        var b = Body(Src(), "private static void Recenter(TextBlock t)");
        var guard = b.IndexOf("if (!t.IsMeasureValid || !t.IsArrangeValid)", StringComparison.Ordinal);
        var fix = b.IndexOf("CenterFix(t)", StringComparison.Ordinal);
        Assert.True(guard > 0 && fix > guard, "نگهبان باید پیش از ‎CenterFix‎ باشد");
        Assert.Contains("AfterLayout(t);", b[guard..fix]);
    }

    /// <summary>
    /// ⛔ شنوندهٔ چیدمان یک‌باره است: با نخستین سنجشِ دیدنی برداشته می‌شود، از درخت که رفت هم؛
    /// تا پنهان است فقط ‎IsEffectivelyVisible‎ را می‌پرسد (قاعدهٔ «بخشِ پنهان صفر کار»).
    /// </summary>
    [Fact]
    public void ShenavandeyeChidman_YekBareAst()
    {
        var b = Body(Src(), "private static void AfterLayout(TextBlock t)");
        Assert.Contains("t.GetValue(PendingProperty)", b);
        Assert.Contains("!t.IsEffectivelyVisible", b);
        Assert.Equal(2, Regex.Matches(b, @"t\.LayoutUpdated -= h;").Count);
        Assert.Single(Regex.Matches(b, @"t\.LayoutUpdated \+= h;"));
        Assert.Contains("t.GetVisualRoot() is null", b);
        //  هیچ تکرارِ پستیِ بی‌سقف (که پیش از چیدمان می‌سوخت) برنگردد
        Assert.DoesNotContain("tries", Src());
    }
}
