using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «توی دارک مود نوشته‌های رنگی خوانده نمی‌شوند — همه سفید» (۱۴۰۵/۰۷/۱۴) ══
///
/// سه ریشه، هر کدام یک قفل:
/// ۱) نوشتهٔ رنگی مستقیم ‎Pump.Ok/Danger/…‎ را می‌گرفت. حالا ‎Pump.Ink.*‎ است:
///    در تمِ روشن همان رنگ، در تمِ تیره سفید (‎Text‎).
/// ۲) رنگی که از ویومدل با کلید می‌آمد (‎ResourceKeyToBrushConverter‎) یک بار
///    گرفته می‌شد و با عوض شدنِ تم عوض نمی‌شد — آبیِ تیره روی بومِ مشکی.
/// ۳) سبکِ سراسریِ ‎TextBlock‎ نوشتهٔ تبِ فعالِ نوار را سفید نگه می‌داشت،
///    روی زمینهٔ زرد.
/// </summary>
public class DarkInkTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(Root(), Path.Combine(parts)));

    private static readonly string[] Colored = { "Ok", "Warn", "Danger", "Info", "Orange", "Accent", "Purple", "Diesel" };

    /// <summary>⛔ هیچ نوشته‌ای رنگِ معنایی را مستقیم نمی‌گیرد — فقط ‎Pump.Ink.*‎.</summary>
    [Fact]
    public void NeveshtehRangi_AzInkMiayad()
    {
        var app = Path.Combine(Root(), "PumpYaqobi.App");
        var bad = new List<string>();
        var rx = new Regex(@"(Foreground=""|Property=""(TextBlock\.)?Foreground"" Value="")\{DynamicResource Pump\.(" +
                           string.Join("|", Colored) + @")\}");
        foreach (var f in Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories))
            foreach (Match m in rx.Matches(File.ReadAllText(f)))
                bad.Add(Path.GetFileName(f) + ": " + m.Value);
        Assert.True(bad.Count == 0, "نوشتهٔ رنگیِ بی ‎Ink‎:\n" + string.Join("\n", bad));
    }

    /// <summary>⛔ در تمِ تیره هر ‎Ink‎ همان ‎Text‎ است و در روشن همان رنگِ خودش.</summary>
    [Fact]
    public void Ink_DarTire_Sefid_DarRoshan_HamanRang()
    {
        var tm = Read("PumpYaqobi.App", "Themes", "ThemeManager.cs");
        Assert.Contains("Color Ink(Color c) => t.IsDark ? t.Text : c;", tm);
        foreach (var k in Colored)
            Assert.Contains($"Br(\"Ink.{k}\"", tm);
    }

    /// <summary>⛔ رنگِ کلیدی با تم عوض می‌شود — و نوشته‌ها ‎ink‎ می‌گیرند.</summary>
    [Fact]
    public void RangeKelidi_BaTam_Avaz_Mishavad()
    {
        var conv = Read("PumpYaqobi.App", "Themes", "ResourceKeyToBrushConverter.cs");
        Assert.Contains("public static void Refresh()", conv);
        Assert.Contains("\"Ink.\"", conv);
        var tm = Read("PumpYaqobi.App", "Themes", "ThemeManager.cs");
        var apply = tm.Split("public static void Apply(")[1].Split("private static")[0];
        Assert.Contains("ResourceKeyToBrushConverter.Refresh();", apply);
        //  ⚠️ ‎Refresh‎ پیش از ‎Changed‎ — شنونده‌ها باید رنگِ تازه را ببینند
        Assert.True(apply.IndexOf("Refresh()", StringComparison.Ordinal)
                    < apply.IndexOf("Changed?.Invoke", StringComparison.Ordinal));

        //  هر نوشته‌ای که رنگش از کلید می‌آید پارامتر دارد (‎ink‎ یا ‎band‎)
        var app = Path.Combine(Root(), "PumpYaqobi.App");
        var rx = new Regex(@"<TextBlock[^>]*Foreground=""\{Binding [A-Za-z.]+, Converter=\{x:Static th:ResourceKeyToBrushConverter.Instance\}\}""");
        var bad = new List<string>();
        foreach (var f in Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories))
            foreach (Match m in rx.Matches(File.ReadAllText(f)))
                bad.Add(Path.GetFileName(f));
        Assert.True(bad.Count == 0, "نوشتهٔ کلیدیِ بی ‎ink‎: " + string.Join(", ", bad));
    }

    /// <summary>⛔ نوشتهٔ تبِ فعالِ نوار از ‎NavActiveFg‎ است (مشکی روی زردِ تمِ تیره).</summary>
    [Fact]
    public void TabeFaal_NeveshtehAz_NavActiveFg()
    {
        var c = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        var style = c.Split("<Style Selector=\"Button.nav.active TextBlock\">")[1].Split("</Style>")[0];
        Assert.Contains("Pump.NavActiveFg", style);
    }

    /// <summary>⛔ سه کارتِ صرافی از بالا تراز — عنوان و عدد روی یک خط.</summary>
    [Fact]
    public void KarthayeSarrafi_AzBala_Taraz()
    {
        var v = Read("PumpYaqobi.App", "Views", "Sections", "ExchangeSectionView.axaml");
        var sum = v.Split("<c:SectionPage.Summary>")[1].Split("</c:SectionPage.Summary>")[0];
        Assert.Equal(3, Regex.Matches(sum, @"<StackPanel Spacing=""3"" VerticalAlignment=""Top"">").Count);
    }
}
