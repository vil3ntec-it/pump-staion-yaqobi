using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ فهرستِ ۱۴۰۵/۰۷/۱۹ — قالبِ زندهٔ عدد و تاریخ، سربرگ، نمودارها، کپسولِ «نوع» ══
/// رفتارِ کلید و پنجره در ‎UiTests -- liveformat‎ است؛ این‌جا قاعده‌های خالص و سورس.
/// </summary>
public class LiveInputTests
{
    private static string R(params string[] p) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(p).ToArray()));

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    // ── عدد ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("5000", "5,000")]
    [InlineData("500000", "500,000")]
    [InlineData("1234567", "1,234,567")]
    [InlineData("12,34", "1,234")]
    [InlineData("-25000", "-25,000")]
    [InlineData("12500.75", "12,500.75")]
    [InlineData("۵۰۰۰", "۵,۰۰۰")]
    [InlineData("12٬500", "12,500")]
    [InlineData("999", "999")]
    public void Adad_HamanLahze_Kama_Migirad(string typed, string shown)
    {
        var (t, c) = LiveInput.Number(typed, typed.Length);
        Assert.Equal(shown, t);
        Assert.Equal(t.Length, c);
        //  ⛔ خواندنِ عدد عوض نشد
        Assert.Equal(Shamsi.Num(typed), Shamsi.Num(t));
    }

    [Theory]
    [InlineData("حواله 12")]
    [InlineData("12-3")]
    [InlineData("1.2.3")]
    [InlineData("abc")]
    [InlineData("0.730")]
    public void MatneGheyreAdadi_DastNemikhorad(string typed)
    {
        //  «0.730» عدد است ولی بخشِ صحیحش کوتاه است — همان می‌ماند
        Assert.Equal(typed, LiveInput.Number(typed, typed.Length).Text);
    }

    [Fact]
    public void Makannama_SareHamanRaqam_Mimanad()
    {
        //  «1|2345» (مکان‌نما پس از ۱) ⇐ «1|2,345»
        var (t, c) = LiveInput.Number("12345", 1);
        Assert.Equal("12,345", t);
        Assert.Equal(1, c);
        //  «1234|5» ⇐ «12,34|5»
        (t, c) = LiveInput.Number("12345", 4);
        Assert.Equal(5, c);
        Assert.Equal('5', t[c]);
    }

    [Fact]
    public void PakkonRooyeKama_RaqameQablRaMibarad()
    {
        //  «1,|234» و پاک‌کن ⇐ «1234»، که بی این قاعده دوباره «1,234» می‌شد
        var (t, c) = LiveInput.Number("1234", 1, previous: "1,234");
        Assert.Equal("234", t);
        Assert.Equal(0, c);
    }

    // ── تاریخ ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("14050719", "1405/07/19")]
    [InlineData("1405071", "1405/07/1")]
    [InlineData("140571", "1405/7/1")]
    [InlineData("14051", "1405/1")]
    [InlineData("140515", "1405/1/5")]
    [InlineData("140512", "1405/12")]
    [InlineData("1405123", "1405/12/3")]
    [InlineData("1405", "1405")]
    [InlineData("۱۴۰۵۰۷۱۹", "۱۴۰۵/۰۷/۱۹")]
    public void Tarikh_Eslash_KhodKar(string typed, string shown)
    {
        var (t, c) = LiveInput.Date(typed, typed.Length, previous: typed[..^1]);
        Assert.Equal(shown, t);
        Assert.Equal(t.Length, c);
    }

    [Theory]
    [InlineData("1405/5/12")]
    [InlineData("5/12")]
    [InlineData("1405/")]
    [InlineData("1405-07")]
    public void EslasheKhodeKarbar_DastNemikhorad(string typed) =>
        Assert.Equal(typed, LiveInput.Date(typed, typed.Length, typed[..^1]).Text);

    [Fact]
    public void PakKardaneEslash_BarNemigardad()
    {
        //  «1405/07» و پاک‌کن روی «/» ⇐ «140507» — همان، نه دوباره «1405/07»
        Assert.Equal("140507", LiveInput.Date("140507", 4, previous: "1405/07").Text);
    }

    // ── سورس: کجا وصل است ───────────────────────────────────────────────────

    [Fact]
    public void JadvalVaForm_HarDo_QalebeZende_Darand()
    {
        var grid = R("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        Assert.Contains("LiveFormat.Attach(fbox, p is \"DateShamsi\" ? \"date\"", grid);
        Assert.Contains("RowViewModel.IsNumberColumn(p) ? \"number\"", grid);
        var row = R("PumpYaqobi.App", "ViewModels", "RowViewModel.cs");
        Assert.Contains("path != \"DensityText\"", row);   // ⛔ «ثقلت» قاعدهٔ خودش را دارد

        //  کادرهای فرم: هر کدام نوعِ خودش
        foreach (var (file, bind, kind) in new[]
                 {
                     ("ParchaSectionView.axaml", "Start", "number"), ("ParchaSectionView.axaml", "PaDate", "date"),
                     ("StorageSectionView.axaml", "BuyKg", "number"), ("StorageSectionView.axaml", "BuyDate", "date"),
                     ("InvoiceSectionView.axaml", "FAmount", "number"), ("InvoiceSectionView.axaml", "FDate", "date"),
                     ("DebtReceiptSectionView.axaml", "AmountText", "number"),
                     ("DebtReceiptSectionView.axaml", "DateShamsi", "date"),
                     ("ProfitSectionView.axaml", "ManualIncome", "number"),
                 })
        {
            var x = R("PumpYaqobi.App", "Views", "Sections", file);
            var tag = System.Text.RegularExpressions.Regex.Match(x,
                "<TextBox\\b[^>]*Text=\"\\{Binding " + bind + "[,}][^>]*>").Value;
            Assert.Contains("c:LiveFormat.Kind=\"" + kind + "\"", tag);
        }
        //  ⛔ ثقلت و کادرهای غیرِعددی نه
        var st = R("PumpYaqobi.App", "Views", "Sections", "StorageSectionView.axaml");
        var dens = System.Text.RegularExpressions.Regex.Match(st, "<TextBox\\b[^>]*\\{Binding BuyDensity[^>]*>").Value;
        Assert.DoesNotContain("LiveFormat", dens);
    }

    [Fact]
    public void Sarbarg_KelideRoshanKhamushDarad()
    {
        var main = R("PumpYaqobi.App", "Views", "MainWindow.axaml");
        Assert.Contains("<Border Name=\"HeaderBlock\" IsVisible=\"{Binding IsHeaderVisible}\"", main);
        var vm = R("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("public bool IsHeaderVisible => IsChromeVisible && Services.HeaderPref.Show;", vm);
        var dash = R("PumpYaqobi.App", "Views", "Sections", "DashboardSectionView.axaml");
        Assert.Contains("IsChecked=\"{Binding ShowHeader}\"", dash);
        Assert.Contains("IsChecked=\"{Binding ShowBanner}\"", dash);
        //  مقدارِ راحتی ⇒ هر دو فهرست
        var s = R("PumpYaqobi.App", "Services", "AppSettings.cs");
        Assert.Contains("live.ShowHeader = ShowHeader;", s);
        Assert.Contains("ShowBanner, ShowHeader);", s);
        //  جای نوارِ بخش‌ها: سربرگِ پنهان صفر شمرده می‌شود
        Assert.Contains("header is { IsVisible: true } ? header.Bounds.Height : 0",
                        R("PumpYaqobi.App", "Views", "MainWindow.axaml.cs"));
    }

    [Fact]
    public void Nemoodar_ChapBeRast_Va_DoTabeJoda()
    {
        var x = R("PumpYaqobi.App", "Views", "Sections", "DashboardSectionView.axaml");
        Assert.Contains("ItemsSource=\"{Binding Bars}\" VerticalAlignment=\"Stretch\" FlowDirection=\"LeftToRight\"", x);
        //  روندِ مفاد و مصارف دیگر زیرِ نمودارِ فروش نیست — تبِ دومِ کارتِ روند است
        var barsCard = x[x.IndexOf("{Binding Bars}", StringComparison.Ordinal)..x.IndexOf("<!-- مخزن‌ها", StringComparison.Ordinal)];
        Assert.DoesNotContain("TrendProfitShown", barsCard);
        Assert.Contains("Command=\"{Binding SetTrendTabCommand}\" CommandParameter=\"sale\"", x);
        Assert.Contains("Command=\"{Binding SetTrendTabCommand}\" CommandParameter=\"profit\"", x);
        Assert.Contains("IsVisible=\"{Binding IsTrendSale}\"", x);
        Assert.Contains("IsVisible=\"{Binding IsTrendProfit}\"", x);
        var spark = R("PumpYaqobi.App", "Controls", "SparkChart.cs");
        Assert.Contains("CubicBezierTo", spark);           // نرم
        Assert.Contains("new DashStyle(", spark);          // خط‌کشِ نقطه‌چین
        Assert.DoesNotContain("DispatcherTimer", spark);   // ⚡ بی زمان‌سنج
    }

    [Fact]
    public void KapsuleNoe_BaKhodeSotoon_PeydaMishavad_NaBaShomare()
    {
        var g = R("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        var picker = g[g.IndexOf("private Control? CellPicker(", StringComparison.Ordinal)..];
        picker = picker[..picker.IndexOf("// ── جابه‌جاییِ خانه", StringComparison.Ordinal)];
        Assert.Contains("ReferenceEquals(ColumnOfCell(c), col)", picker);
        Assert.DoesNotContain("cells[idx]", picker);
    }
}
