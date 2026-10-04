using PumpYaqobi.Application.Localization;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>شورا، ث۵ — واژه‌نامه: واژه‌به‌واژه، و هر واژه یک معنی.</summary>
public class GlossaryTests
{
    [Fact]
    public void Vazheh_BeVazheh_NaZirreshte()
    {
        Assert.StartsWith("فیصدی:", Glossary.MeaningIn("فیصدی پطرول"));
        Assert.StartsWith("فی:", Glossary.MeaningIn("فی (افغانی)"));
        Assert.StartsWith("ختم:", Glossary.MeaningIn("ختمِ پایه"));
        Assert.Null(Glossary.MeaningIn("فیلم"));
        Assert.Null(Glossary.MeaningIn("تاریخ"));
        Assert.Null(Glossary.MeaningIn(""));
    }

    [Fact]
    public void HarVazheh_YekBar_Va_MaaniDarad()
    {
        Assert.Equal(Glossary.All.Length, Glossary.All.Select(t => t.Word).Distinct().Count());
        Assert.All(Glossary.All, t => Assert.True(t.Meaning.Length > 10, t.Word));
        //  «رسید» سه معنی دارد و هر سه گفته می‌شوند
        Assert.Contains("سه معنی", Glossary.All.Single(t => t.Word == "رسید").Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public void DarF1_Va_Sarsotoonha_Khande_Mishavad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var sw = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "ShortcutsWindow.axaml.cs"));
        Assert.Contains("Glossary.All", sw, StringComparison.Ordinal);
        var g = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Controls", "ExcelGrid.cs"));
        Assert.Contains("Glossary", g, StringComparison.Ordinal);
    }
}
