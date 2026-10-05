using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «چپ/راست رفتنِ خط‌ها و نوشته‌ها پس از عوض کردنِ ماه — همهٔ بخش‌ها» (۱۴۰۵/۰۷/۲۱) ══
///
/// رفتارش را ‎monthshift‎ (‎PumpYaqobi.UiTests‎) با پنجرهٔ واقعی و کشوی واقعیِ ماه
/// می‌سنجد؛ این‌ها فقط ممنوعه‌اند — دو ریشه‌ای که سنجه پیدا کرد برنگردند:
///   ۱) ستونِ «#» با شمارِ ردیف‌های ماه بزرگ و کوچک می‌شد ⇒ همهٔ خط‌ها ۸px می‌پریدند
///   ۲) جدولِ خالیِ ماهِ تازه با سرستون‌ها چیده می‌شد و نخستین ماهِ پر همه را از نو می‌چید
/// </summary>
public class MonthShiftWidthTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Grid() =>
        SrcText.Read(Path.Combine(Root(), "PumpYaqobi.App", "Controls", "ExcelGrid.cs"))
        + SrcText.Read(Path.Combine(Root(), "PumpYaqobi.App", "Controls", "ExcelGrid.Widths.cs"));

    private static string Body(string src, string sig)
    {
        var i = src.IndexOf(sig, StringComparison.Ordinal);
        Assert.True(i >= 0, sig);
        var open = src.IndexOf('{', i);
        var depth = 0;
        for (var k = open; k < src.Length; k++)
        {
            if (src[k] == '{') depth++;
            else if (src[k] == '}' && --depth == 0) return src[open..(k + 1)];
        }
        return src[open..];
    }

    /// <summary>⛔ ستونِ «#» کفِ سه رقم دارد و هرگز کوچک نمی‌شود.</summary>
    [Fact]
    public void SotooneShomare_BaMaah_KuchakNemishavad()
    {
        var b = Body(Grid(), "private void FixRowHeaderWidth()");
        Assert.Contains("Math.Max(3,", b);
        Assert.DoesNotContain("Math.Max(2,", b);
        //  کوچک‌تر نمی‌شود: پهنای بزرگ‌ترِ موجود همان‌جا برمی‌گرداند
        Assert.Matches(new Regex(@"RowHeaderWidth\s*>=\s*w\s*-\s*0\.5\)\s*return;"), b);
    }

    /// <summary>
    /// ⛔ «کهنه» فقط پهنای <b>حدسی</b> است؛ جدولِ خالی‌ای که پهنای طبیعیِ محتوای
    /// پیشین را گرفت با آمدنِ ردیف‌ها از نو چیده نمی‌شود.
    /// </summary>
    [Fact]
    public void JadvaleKhali_AzHafezeMigirad_VaDobareNemichinad()
    {
        var g = Grid();
        Assert.Contains("var stale = !_spread || (_spreadRows == 0 && _spreadGuess);", g);
        Assert.DoesNotContain("var stale = !_spread || _spreadRows == 0;", g);
        var spread = Body(g, "private void SpreadColumns()");
        Assert.Contains("AutoMemory(mk)", spread);
        Assert.Contains("_spreadGuess = empty && remembered is null;", spread);
        //  حافظه فقط از چیدن روی ردیفِ واقعی و بی پهنای کاربر نوشته می‌شود
        Assert.Contains("if (!empty && _saved is null && AutoMemoryKey(cols.Count) is { } wk)", spread);
    }

    /// <summary>
    /// ⛔ حافظهٔ «پهنای طبیعی» هرگز جای پهنای <b>کاربر</b> نمی‌نشیند: کلیدش پیشوندِ
    /// خودش را دارد و ‎Saved‎ (پهنای کاربر) هیچ‌وقت آن را نمی‌خواند.
    /// </summary>
    [Fact]
    public void HafezeyePahna_JaayePahnayeKarbarNemineshinad()
    {
        var g = Grid();
        Assert.Contains("internal const string AutoPrefix = \"auto|\";", g);
        Assert.Contains("return AutoPrefix + k;", Body(g, "private string? AutoMemoryKey(int visible)"));
        Assert.DoesNotContain("AutoPrefix", Body(g, "private double[]? Saved(int count)"));
        Assert.DoesNotContain("AutoPrefix", Body(g, "private void RememberWidths()"));
    }
}
