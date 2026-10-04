using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ممیزیِ عرضه (۱۴۰۵/۰۷/۲۰) — مجوزِ کتابخانه‌ها همراهِ نصاب ══════════════════
///
/// برنامه LibVLC (‎LGPL-2.1‎) و ده‌ها کتابخانهٔ ‎MIT/Apache‎ را با خودش می‌برد و تا
/// امروز متنِ هیچ‌کدام را همراه نداشت. ⛔ هر بسته‌ای که برنامه به آن ارجاع می‌دهد
/// باید در ‎THIRD-PARTY-NOTICES.txt‎ باشد — کتابخانهٔ تازه بی مجوزش این‌جا سرخ
/// می‌شود — و نصاب همان فایل را در پوشهٔ برنامه بگذارد.
/// </summary>
public class ThirdPartyNoticesTests
{
    private static string Native([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, ".."));

    [Fact]
    public void HarBasteyeBarname_DarNoticesHast()
    {
        var notices = File.ReadAllText(Path.Combine(Native(), "installer", "THIRD-PARTY-NOTICES.txt"));
        var missing = new List<string>();
        foreach (var proj in new[] { "PumpYaqobi.App", "PumpYaqobi.Services", "PumpYaqobi.Persistence",
                                     "PumpYaqobi.Reporting", "PumpYaqobi.Shell", "PumpYaqobi.Application",
                                     "PumpYaqobi.Domain" })
        {
            var f = Path.Combine(Native(), proj, proj + ".csproj");
            if (!File.Exists(f)) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "<PackageReference\\s[^>]*>"))
            {
                if (Regex.IsMatch(m.Value, "Condition=\"[^\"]*Debug")) continue;   // فقط در Debug — همراهِ نصاب نیست
                var name = Regex.Match(m.Value, "Include=\"([^\"]+)\"").Groups[1].Value;
                if (!Regex.IsMatch(notices, "^" + Regex.Escape(name) + @"\s", RegexOptions.Multiline))
                    missing.Add(proj + ": " + name);
            }
        }
        Assert.True(missing.Count == 0, "بی مجوز در THIRD-PARTY-NOTICES.txt — tools/third-party-notices.py را بدوانید:\n" + string.Join("\n", missing));
        //  و متنِ کاملِ مجوزِ کپی‌لفت، نه فقط نامش
        Assert.Contains("GNU LESSER GENERAL PUBLIC LICENSE", notices);
        Assert.Contains("Apache License", notices);
    }

    [Fact]
    public void Nasab_Notices_RaMigozarad()
    {
        var iss = File.ReadAllText(Path.Combine(Native(), "installer", "PumpYaqobi.iss"));
        Assert.Contains("Source: \"THIRD-PARTY-NOTICES.txt\"; DestDir: \"{app}\"", iss);
    }
}
