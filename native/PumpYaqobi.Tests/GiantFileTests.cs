using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ج۴ — فایلِ غول برنمی‌گردد ════════════════════════════════════════════
///
/// <c>ExcelGrid</c> (۳٬۸۱۰ خط) و <c>MainViewModel</c> (۲٬۲۰۴ خط) به شش تکهٔ
/// <c>partial</c>ِ هم‌موضوع شکستند. ⛔ هیچ تکه‌ای از <see cref="MaxLines"/> بلندتر
/// نمی‌شود — تکه‌ای که بزرگ شد، باز به تکهٔ هم‌موضوعِ تازه می‌شکند. و ⛔ هر تکه‌ای
/// که روی دیسک است در فهرستِ <c>SrcText</c> هست، وگرنه آزمون‌هایی که متنِ کلاس را
/// می‌خوانند تکهٔ تازه را نمی‌دیدند و بی‌صدا ضعیف می‌شدند.
/// </summary>
public class GiantFileTests
{
    private const int MaxLines = 1000;

    private static string Native([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, ".."));

    public static IEnumerable<object[]> Classes() => new[]
    {
        new object[] { "PumpYaqobi.App/Controls", "ExcelGrid" },
        new object[] { "PumpYaqobi.App/ViewModels", "MainViewModel" },
        //  شورا، د۵: ‎CloudLink.cs‎ (۲٬۹۵۷ خط) هم شکست.
        new object[] { "PumpYaqobi.Shell/Services", "CloudLink" },
    };

    [Theory, MemberData(nameof(Classes))]
    public void HichTekeyiAzSaghfBolandtarNist(string dir, string cls)
    {
        var parts = Directory.GetFiles(Path.Combine(Native(), dir), cls + "*.cs");
        Assert.True(parts.Length >= 6, $"{cls}: تکه‌ها پیدا نشدند ({parts.Length})");
        var big = parts.Select(p => (p, n: File.ReadAllLines(p).Length))
                       .Where(x => x.n > MaxLines)
                       .Select(x => $"{Path.GetFileName(x.p)}: {x.n} خط").ToList();
        Assert.True(big.Count == 0, "فایلِ غول برگشت:\n" + string.Join("\n", big));
    }

    [Theory, MemberData(nameof(Classes))]
    public void HarTekeDarSrcTextHast(string dir, string cls)
    {
        var full = Path.Combine(Native(), dir, cls + ".cs");
        var seen = SrcText.Read(full);
        foreach (var p in Directory.GetFiles(Path.Combine(Native(), dir), cls + ".*.cs"))
        {
            Assert.Equal(cls + ".cs", SrcText.ClassFile(Path.GetFileName(p)));
            // متنِ هر تکه داخلِ متنی است که آزمون‌ها از کلاس می‌بینند
            Assert.Contains(File.ReadAllText(p), seen);
        }
    }
}
