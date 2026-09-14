using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ منبعی که وجود ندارد، بی‌صدا هیچ می‌شود ══════════════════════════════════
///
/// گزارشِ صاحب ریپو: «هنگام اجرای لودینگ، کاربر به بخش‌های مختلف برنامه منتقل
/// می‌شود، صفحات مختلف نمایش داده می‌شوند و در نهایت دوباره به صفحه قفل
/// برمی‌گردد.»
///
/// یکی از دو ریشه‌اش یک خط بود:
///
///     Background="{DynamicResource Pump.Bg}"
///
/// و <b>چنین کلیدی در تم وجود ندارد</b> — نامِ درست ‎Pump.AppBg‎ است.
/// ⚠️ آوالونیا برای ‎DynamicResource‎ی که پیدا نشود نه خطا می‌دهد نه هشدار؛
/// فقط مقدار را ‎null‎ می‌گذارد. یعنی پردهٔ لودینگ **کاملاً شفاف** بود و
/// همه‌چیزِ پشتش دیده می‌شد. نه کامپایلر می‌گرفتش، نه آزمون، نه چشم — مگر
/// این‌که کسی برنامه را باز کند.
///
/// این آزمون همان شکاف را می‌بندد: هر کلیدِ ‎Pump.*‎ که در XAML خوانده می‌شود
/// باید جایی تعریف شده باشد.
/// </summary>
public class ThemeKeyTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string App = Path.Combine(Root, "PumpYaqobi.App");

    /// <summary>هر کلیدی که تم می‌سازد — چه با ‎Br(...)‎ چه با ‎Set(...)‎.</summary>
    private static HashSet<string> Defined()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        var mgr = File.ReadAllText(Path.Combine(App, "Themes", "ThemeManager.cs"));

        // ‎Br("Accent", …)‎ ⇒ ‎Pump.Accent‎ و ‎Pump.AccentColor‎
        foreach (Match m in Regex.Matches(mgr, @"Br\(""([A-Za-z.]+)"""))
        {
            keys.Add("Pump." + m.Groups[1].Value);
            keys.Add("Pump." + m.Groups[1].Value + "Color");
        }

        // ‎Set("Pump.AppBg", …)‎
        foreach (Match m in Regex.Matches(mgr, @"Set\(""(Pump\.[A-Za-z.]+)"""))
            keys.Add(m.Groups[1].Value);

        // ⚠️ و هر جای دیگری که کلیدی **نشانده** می‌شود، نه فقط ‎ThemeManager‎:
        // ‎TableStyle‎ رنگِ خطوطِ جدول را می‌گذارد و ‎App.axaml.cs‎ نشانگرِ خانه
        // را. بی این، آزمون خودش کلیدهای درست را «نبوده» می‌خواند.
        foreach (var f in Directory.GetFiles(App, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(f)) continue;
            var code = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(code, @"\[""(Pump\.[A-Za-z.]+)""\]\s*="))
                keys.Add(m.Groups[1].Value);
        }

        // و کلیدهای ایستا در خودِ فایل‌های XAML
        foreach (var f in Directory.GetFiles(App, "*.axaml", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"x:Key=""(Pump\.[A-Za-z.]+)"""))
                keys.Add(m.Groups[1].Value);

        return keys;
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    [Fact]
    public void EveryThemeKeyTheMarkupAsksForActuallyExists()
    {
        var defined = Defined();
        var missing = new List<string>();

        foreach (var f in Directory.GetFiles(App, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(text, @"DynamicResource (Pump\.[A-Za-z.]+)"))
            {
                var key = m.Groups[1].Value;
                if (!defined.Contains(key))
                    missing.Add($"{Path.GetFileName(f)}: {key}");
            }
        }

        Assert.True(missing.Count == 0,
            "کلیدِ تمِ نبوده (آوالونیا بی‌صدا ‎null‎ می‌گذارد):\n  " + string.Join("\n  ", missing));
    }

    /// <summary>
    /// ⚠️ و همین را برای کلیدهایی که از ویومدل می‌آیند هم می‌سنجیم: کپسول‌ها
    /// و کارت‌ها رنگشان را با یک **رشته** می‌دهند
    /// (‎ResourceKeyToBrushConverter‎)، و آن‌جا هم غلطِ املایی بی‌صدا می‌ماند.
    /// </summary>
    [Fact]
    public void EveryBrushKeyAViewModelHandsOutActuallyExists()
    {
        var defined = Defined();
        var missing = new List<string>();

        foreach (var f in Directory.GetFiles(App, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(f)) continue;

            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"""(Pump\.[A-Za-z.]+)"""))
            {
                var key = m.Groups[1].Value;
                if (!defined.Contains(key)) missing.Add($"{Path.GetFileName(f)}: {key}");
            }
        }

        Assert.True(missing.Count == 0,
            "کلیدِ رنگِ نبوده در کد:\n  " + string.Join("\n  ", missing));
    }
}
