using System.Text;
using System.Text.RegularExpressions;
using PumpYaqobi.Application.Localization;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ث۳ — یک نام برای هر کد، و یک مسیرِ اصلیِ ورود ══════════════════════
/// سه کد هست و هر کدام یک نام دارد (<see cref="CodeNames"/>). این آزمون هر
/// نوشتهٔ **دیدنیِ** برنامهٔ کامپیوتر (رشته‌های کد و ویژگی‌های XAML، بی توضیح)
/// و اپِ گوشی (`kar/`) را می‌گردد و هیچ نامِ کهنه‌ای نمی‌پذیرد. ⚠️ توضیحِ کد
/// عمداً گشته نمی‌شود — تاریخچهٔ تصمیم‌ها همان‌جاست و کاربر نمی‌بیندش.
/// </summary>
public class CodeNamesTests
{
    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void SeNam_SabetAnd_VaHichKodamKohneNist()
    {
        Assert.Equal("کدِ تأییدِ ایمیل", CodeNames.Email);
        Assert.Equal("کدِ اپِ گوشی", CodeNames.PhoneApp);
        Assert.Equal("کلیدِ اشتراکِ بی‌اینترنت", CodeNames.OfflineKey);
        foreach (var n in new[] { CodeNames.Email, CodeNames.PhoneApp, CodeNames.OfflineKey })
            Assert.Null(CodeNames.OldNameIn(n));
        Assert.Equal("کدِ پمپ", CodeNames.OldNameIn("این کدِ پمپ است"));
        Assert.Equal("کدِ ایمیل", CodeNames.OldNameIn("کدِ ایمیل را بزنید"));
        //  «کدِ کامپیوتر» چهارمی نیست و کهنه هم نیست
        Assert.Null(CodeNames.OldNameIn("کدِ کامپیوترِ همین‌جا"));
    }

    [Fact]
    public void BarnameyeKampiyuter_HichNameKohneNemiNevisad()
    {
        var bad = new List<string>();
        foreach (var dir in new[] { "PumpYaqobi.App", "PumpYaqobi.Application", "PumpYaqobi.Reporting", "PumpYaqobi.Services" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(Root, dir), "*.*", SearchOption.AllDirectories))
            {
                if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
                IEnumerable<string> visible =
                    f.EndsWith(".cs") ? CsLiterals(File.ReadAllText(f))
                    : f.EndsWith(".axaml") ? new[] { Regex.Replace(File.ReadAllText(f), "<!--.*?-->", "", RegexOptions.Singleline) }
                    : Array.Empty<string>();
                //  خودِ فهرستِ نام‌های کهنه تنها جایی است که باید آن‌ها را بنویسد
                if (f.EndsWith("CodeNames.cs")) continue;
                foreach (var v in visible)
                    if (CodeNames.OldNameIn(v) is { } old)
                        bad.Add($"{Path.GetFileName(f)}: «{old}» در «{Short(v)}»");
            }
        Assert.True(bad.Count == 0, "نامِ کهنه در نوشتهٔ دیدنی:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void AppeGooshi_HichNameKohneNemiNevisad()
    {
        var bad = new List<string>();
        var kar = Path.GetFullPath(Path.Combine(Root, "..", "kar"));
        foreach (var name in new[] { "index.html", "app.js", "cloud.js", "update.js" })
        {
            var p = Path.Combine(kar, name);
            if (!File.Exists(p)) continue;
            foreach (var (line, i) in StripWebComments(File.ReadAllText(p)).Split('\n').Select((l, i) => (l, i + 1)))
                if (CodeNames.OldNameIn(line) is { } old) bad.Add($"kar/{name}:{i}: «{old}»");
        }
        Assert.True(bad.Count == 0, "نامِ کهنه در اپِ گوشی:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void SafheyeVorud_YekMasireAsli_VaBaghiyeZireRahhayeDigar()
    {
        var x = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml"));
        var vm = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs"));

        //  پیش‌فرض: ایمیل ⇐ کد ⇐ تمام
        Assert.Contains("private bool _isCodeLogin = true;", vm);
        //  کلیدِ دوتکهٔ رمزدار فقط در راه‌های رمزدار
        var sw = x.IndexOf("Content=\"حساب می‌سازم\"", StringComparison.Ordinal);
        var border = x.LastIndexOf("<Border CornerRadius=\"999\"", sw, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding !IsCodeLogin}\"", x[border..sw]);
        //  و هیچ دکمهٔ سومی برای کد در آن کلید نیست — کد مسیرِ اصلی است
        var switchEnd = x.IndexOf("</Border>", sw, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandParameter=\"code\"", x[border..switchEnd]);
        //  «راه‌های دیگر» فقط دیدن را باز می‌کند و دو راهِ رمزدار را دارد
        Assert.Contains("Name=\"OtherWays\"", x);
        Assert.Contains("IsVisible=\"{Binding #OtherWays.IsChecked}\"", x);
        var ways = x[x.IndexOf("Name=\"OtherWays\"", StringComparison.Ordinal)..];
        ways = ways[..ways.IndexOf("</StackPanel>\n                    </StackPanel>", StringComparison.Ordinal)];
        Assert.Contains("CommandParameter=\"no\"", ways);
        Assert.Contains("CommandParameter=\"yes\"", ways);
        //  و برگشت به مسیرِ اصلی از راه‌های رمزدار
        Assert.Contains("CommandParameter=\"code\"", x);
        //  ⛔ نامِ کاربر، تکرارِ رمز و شرایط در مسیرِ کد دیده نمی‌شوند (قابِ بیرونی)
        foreach (var inner in new[] { "Text=\"نامِ شما\"", "Text=\"تکرارِ رمز\"", "Content=\"شرایط و ضوابط\"" })
        {
            var at = x.IndexOf(inner, StringComparison.Ordinal);
            var wrap = x.LastIndexOf("<Panel IsVisible=\"{Binding !IsCodeLogin}\">", at, StringComparison.Ordinal);
            Assert.True(wrap > 0 && x.IndexOf("</Panel>", wrap, StringComparison.Ordinal) > at, inner);
        }
    }

    [Fact]
    public void VajehNameh_SeKodRaMiguyad()
    {
        foreach (var n in new[] { CodeNames.Email, CodeNames.PhoneApp, CodeNames.OfflineKey })
            Assert.Contains(Glossary.All, t => t.Word == n);
    }

    // ── کمکی‌ها ────────────────────────────────────────────────────────────

    private static string Short(string s) => s.Length > 70 ? s[..70] + "…" : s;

    /// <summary>رشته‌های C#، بی توضیح و بی نویسهٔ تک.</summary>
    internal static IEnumerable<string> CsLiterals(string s)
    {
        var i = 0; var n = s.Length;
        while (i < n)
        {
            if (string.CompareOrdinal(s, i, "//", 0, 2) == 0) { var j = s.IndexOf('\n', i); i = j < 0 ? n : j; continue; }
            if (string.CompareOrdinal(s, i, "/*", 0, 2) == 0) { var j = s.IndexOf("*/", i, StringComparison.Ordinal); i = j < 0 ? n : j + 2; continue; }
            var c = s[i];
            if (c == '\'')
            {
                var j = i + 1;
                while (j < n && s[j] != '\'') j += s[j] == '\\' ? 2 : 1;
                i = j + 1; continue;
            }
            if (c == '"' || ((c == '$' || c == '@') && i + 1 < n && (s[i + 1] == '"' || s[i + 1] == '$' || s[i + 1] == '@')))
            {
                var k = i; var verb = false;
                while (s[k] == '$' || s[k] == '@') { verb |= s[k] == '@'; k++; }
                var q = 0; while (k + q < n && s[k + q] == '"') q++;
                if (q >= 3)
                {
                    var end = s.IndexOf(new string('"', q), k + q, StringComparison.Ordinal);
                    yield return s[(k + q)..end];
                    i = end + q; continue;
                }
                var sb = new StringBuilder(); var p = k + 1;
                while (p < n)
                {
                    if (!verb && s[p] == '\\') { sb.Append(s, p, Math.Min(2, n - p)); p += 2; continue; }
                    if (s[p] == '"') { if (verb && p + 1 < n && s[p + 1] == '"') { p += 2; continue; } break; }
                    sb.Append(s[p]); p++;
                }
                yield return sb.ToString();
                i = p + 1; continue;
            }
            i++;
        }
    }

    /// <summary>توضیحِ HTML و JS بیرون — فقط آن‌چه کاربر می‌بیند می‌ماند.</summary>
    private static string StripWebComments(string s)
    {
        s = Regex.Replace(s, "<!--.*?-->", m => new string('\n', m.Value.Count(ch => ch == '\n')), RegexOptions.Singleline);
        s = Regex.Replace(s, @"/\*.*?\*/", m => new string('\n', m.Value.Count(ch => ch == '\n')), RegexOptions.Singleline);
        return string.Join('\n', s.Split('\n').Select(l =>
        {
            var t = l.TrimStart();
            if (t.StartsWith("//")) return "";
            var c = Regex.Match(l, @"(^|\s)//\s");
            return c.Success ? l[..c.Index] : l;
        }));
    }
}
