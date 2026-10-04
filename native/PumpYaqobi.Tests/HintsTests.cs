using PumpYaqobi.App.Services;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.Tests;

/// <summary>
/// شورا، ث۲ — «قاعده‌های پنهان دیده شوند». هیچ رفتاری عوض نمی‌شود؛ این‌جا سنجیده
/// می‌شود که هر راهنما واقعاً جایی نشسته، با F1 جور است، یک بار گفته می‌شود و
/// بستنش روی همین کامپیوتر می‌ماند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class HintsTests : IDisposable
{
    private static readonly string Native =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "hints-" + Guid.NewGuid().ToString("N"));
    private readonly string? _old;

    public HintsTests()
    {
        Directory.CreateDirectory(_dir);
        _old = AppSettings.DirOverride;
        AppSettings.DirOverride = _dir;
        Hints.Forget();
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _old;
        Hints.Forget();
        Hints.Show = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static string Read(string rel) => File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", rel));

    [Fact]
    public void HarRahnama_JaiNeshasteAst()
    {
        var waraq = Read("Views/Sections/WaraqPageView.axaml");
        var parcha = Read("Views/Sections/ParchaSectionView.axaml");
        var theme = Read("Themes/Controls.axaml");
        //  نوارِ هر بخش زیرِ جدولِ همان بخش
        Assert.Contains("HintKey=\"waraq\"", waraq);
        Assert.Contains("HintKey=\"parcha\"", parcha);
        Assert.Contains("HintKey=\"rows\"", theme);
        //  هر دو ستونِ «نام»ِ ورق راهنمای کادر دارند
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(waraq, "c:Suggest.Hint=\"waraq-name\"").Count);
        foreach (var k in Hints.Strip.Keys) Assert.Contains($"HintKey=\"{k}\"", waraq + parcha + theme);
        foreach (var k in Hints.Watermark.Keys) Assert.Contains($"c:Suggest.Hint=\"{k}\"", waraq);
        //  هر «می‌دانستید؟» صداکننده‌ای دارد
        var code = Read("Controls/RowAddBar.cs") + Read("ViewModels/Sections/ParchaSectionViewModel.cs");
        Assert.Contains("DidYouKnow(\"rows\")", code);
        Assert.Contains("DidYouKnow(\"parcha\")", code);
        Assert.Contains("Services.Hints.DidYouKnow(hk)", Read("Controls/ExcelGrid.cs"));
    }

    [Fact]
    public void RahnamaBaF1_Jor_Ast()
    {
        var keys = string.Join("\n", ShortcutsWindow.All.Select(r => r.Key));
        foreach (var k in new[] { "Ctrl + عدد", "Shift + عدد", "Ctrl + Tab", "/نام" }) Assert.Contains(k, keys);
        var ctrlTab = ShortcutsWindow.All.First(r => r.Key == "Ctrl + Tab").What;
        //  ⛔ پارچه با Ctrl+Tab تیل را عوض می‌کند (۱۴۰۵/۰۷/۱۸)، نه کارتِ روز/شب
        Assert.True(ctrlTab.Contains("پطرول ⇄ دیزل", StringComparison.Ordinal), ctrlTab);
        Assert.DoesNotContain("کارتِ روز/شب", ctrlTab);
        Assert.Contains("Ctrl+Tab", Hints.Strip["waraq"]);
        Assert.Contains("Ctrl+عدد", Hints.Strip["rows"]);
        Assert.Contains("Shift+عدد", Hints.Strip["rows"]);
    }

    [Fact]
    public void MiDanestid_YekBar_VaBastanMimanad()
    {
        var shown = new List<string>();
        Hints.Show = shown.Add;
        var quiet = Hints.Quiet;
        Hints.Quiet = false;
        try
        {
            Assert.True(Hints.DidYouKnow("parcha"));
            Assert.False(Hints.DidYouKnow("parcha"));
            Assert.False(Hints.DidYouKnow("nope"));
            Assert.Single(shown);
            Assert.StartsWith("💡", shown[0]);

            Assert.True(Hints.StripVisible("rows"));
            Hints.DismissStrip("rows");
            Assert.False(Hints.StripVisible("rows"));
            Assert.True(Hints.StripVisible("waraq"));

            //  اجرای دیگرِ برنامه (حافظه فراموش، فایل سرِ جایش)
            Hints.Forget();
            Assert.False(Hints.StripVisible("rows"));
            Assert.False(Hints.DidYouKnow("parcha"));
            Assert.True(File.Exists(Path.Combine(_dir, "hints.json")));
        }
        finally { Hints.Quiet = quiet; }
    }

    [Fact]
    public void FileKharab_HichChiziNemishkanad()
    {
        File.WriteAllText(Path.Combine(_dir, "hints.json"), "{not json");
        Hints.Forget();
        Assert.True(Hints.StripVisible("parcha"));
        Assert.True(Hints.Mark("x"));
    }
}
