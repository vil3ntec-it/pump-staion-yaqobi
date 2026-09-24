using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ورق با عکسِ صاحب ریپو (۱۴۰۵/۰۷/۱۲) ════════════════════════════════════
///
/// ۱) «یادداشت و نوع و واحد از جدول بیرون نشوند — دیوارشان را بگذار.»
/// ۲) قرائتِ پمپ: بی ستونِ «تاریخ» و «هشدار»، تیلِ پارچه این‌جا عوض نشود،
///    و خطِ خودِ خانهٔ مشکل‌دار سرخ شود — نه یک کادرِ دیگر داخلش — و با ماوس
///    دلیل را **خلاصه** بگوید؛ هیچ کادرِ خودکاری جلوی ردیفِ بالا را نگیرد.
/// ۳) «داخلِ پی‌دی‌اف جملهٔ این شیفت نیست.»
/// ۴) «پرینتِ ورق‌ها بی‌کیفیت است و با زوم تارتر.»
///
/// رفتارِ کاملش را ‎waraqfit‎ با پنجرهٔ واقعی می‌سنجد؛ این‌جا قاعده‌ها قفل‌اند.
/// </summary>
public class WaraqSheetFixTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    private static string Bare(string x) => System.Text.RegularExpressions.Regex.Replace(
        x, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);

    private static string Page() => Bare(Read("PumpYaqobi.App", "Views", "Sections", "WaraqPageView.axaml"));

    [Fact]
    public void HarSeJadvaleVaraq_Divar_Darand()
    {
        var x = Page();
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(x, "KeepInside=\"True\"").Count);
    }

    [Fact]
    public void JayeKhaneha_SotooneShomare_RaHamMishomarad()
    {
        //  ⛔ ستونِ «#» هم جا می‌گیرد؛ پیش از این ‎SpreadColumns‎ ‎Bounds.Width‎
        //  را می‌گرفت و ستون‌ها به اندازهٔ همان ستون بیرون می‌زدند.
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        var spread = g[g.IndexOf("private void SpreadColumns()", StringComparison.Ordinal)..];
        spread = spread[..spread.IndexOf("_spread = true;", StringComparison.Ordinal)];
        Assert.Contains("var room = CellRoom();", spread);
        Assert.DoesNotContain("var room = Bounds.Width;", spread);
        Assert.Contains("return Bounds.Width - head - 4;", g);
    }

    [Fact]
    public void JadvalePayeha_TarikhVaHoshdar_Nadarad()
    {
        var x = Page();
        Assert.DoesNotContain("Header=\"تاریخ\"", x);
        Assert.DoesNotContain("Header=\"هشدار\"", x);
        Assert.Contains("<c:IssueTextColumn Header=\"شروع\" Binding=\"{Binding StartText}\" Width=\"Auto\" IssuePath=\"StartIssue\" />", x);
        Assert.Contains("<c:IssueTextColumn Header=\"ختم\" Binding=\"{Binding EndText}\" Width=\"Auto\" IssuePath=\"EndIssue\" />", x);
    }

    [Fact]
    public void TileParcha_InjaAvazNemishavad()
    {
        var x = Page();
        Assert.Contains("IsVisible=\"{Binding FuelEditable}\"", x);
        Assert.Contains("Classes=\"fuelchip locked\" IsVisible=\"{Binding FromParcha}\"", x);
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "WaraqSectionViewModel.cs");
        Assert.Contains("if (FromParcha) return;", vm);
    }

    [Fact]
    public void KhateKhodeKhane_Sorkh_Mishavad_NaKadreDovom()
    {
        var col = Read("PumpYaqobi.App", "Controls", "IssueTextColumn.cs");
        //  ⛔ هیچ ‎Border‎ی داخلِ خانه ساخته نمی‌شود — کلاس روی خودِ خانه است
        Assert.DoesNotContain("new Border", col);
        Assert.Contains("cell.Classes.Set(\"issue\"", col);
        var th = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        Assert.Contains("<Style Selector=\"DataGridCell.issue\">", th);
        Assert.Contains("<Style Selector=\"DataGridCell.issue /template/ Rectangle#PART_RightGridLine\">", th);
    }

    [Fact]
    public void Payam_FaghatBaMous_VaKootah()
    {
        //  ⛔ انتخابِ خانه هیچ کادری خودکار باز نمی‌کند (جلوی ردیفِ بالا را می‌گرفت)
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        Assert.DoesNotContain("ToolTip.SetIsOpen", g);
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "WaraqSectionViewModel.cs");
        var a = vm.IndexOf("public string StartIssue", StringComparison.Ordinal);
        var b = vm.IndexOf("bool IFlaggedRow.Flagged", StringComparison.Ordinal);
        Assert.True(a > 0 && b > a);
        //  ⛔ هر پیام **حداکثر دو خط** (۱۴۰۵/۰۷/۱۳: «روشن‌تر و خلاصه‌تر»): خطِ اول
        //  چه شده، خطِ دوم عددها — هیچ پیامی سه خط نمی‌شود.
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(vm[a..b], "\"([^\"]*)\""))
            Assert.True(m.Groups[1].Value.Split("\\n").Length <= 2, m.Value);
        Assert.DoesNotContain("\\n\" + nums + \"\\n", vm[a..b]);
    }

    [Fact]
    public void PdfeVaraq_JomleyeInShift_Darad()
    {
        var sd = new WaraqShift { Kind = ShiftKind.Day };
        sd.Pumps.Add(new WaraqPump { Num = 1, Fuel = FuelType.Petrol, Start = 100, End = 600, PricePerLiter = 100, Debt = 10 });
        sd.Pumps.Add(new WaraqPump { Num = 2, Fuel = FuelType.Petrol, Start = 50, End = 150, PricePerLiter = 100 });
        sd.Pumps.Add(new WaraqPump { Num = 1, Fuel = FuelType.Diesel, Start = 20, End = 70, PricePerLiter = 80, Debt = 5 });
        //  ختمِ کمتر از شروع ⇒ صفر لیتر، مثلِ خودِ جدول
        sd.Pumps.Add(new WaraqPump { Num = 3, Fuel = FuelType.Diesel, Start = 500, End = 400, PricePerLiter = 80 });

        var rows = WaraqReport.ShiftTotalRows(sd);
        Assert.Equal(3, rows.Count);
        Assert.Contains("جمله پطرول", rows[0].Label);
        Assert.Equal((600m, 60_000m, 10m), (rows[0].Liters, rows[0].Sales, rows[0].Debt));
        Assert.Contains("جمله دیزل", rows[1].Label);
        Assert.Equal((50m, 4_000m, 5m), (rows[1].Liters, rows[1].Sales, rows[1].Debt));
        Assert.Contains("جمله این شیفت", rows[2].Label);
        Assert.Equal((650m, 64_000m, 15m), (rows[2].Liters, rows[2].Sales, rows[2].Debt));
    }

    [Fact]
    public void PdfeVaraq_YekTil_FaghatYekRadif()
    {
        var sd = new WaraqShift { Kind = ShiftKind.Night };
        sd.Pumps.Add(new WaraqPump { Num = 1, Fuel = FuelType.Diesel, Start = 0, End = 10, PricePerLiter = 80 });
        var rows = WaraqReport.ShiftTotalRows(sd);
        Assert.Single(rows);
        Assert.Contains("جمله این شیفت — دیزل", rows[0].Label);
        Assert.Empty(WaraqReport.ShiftTotalRows(new WaraqShift()));
    }

    [Fact]
    public void Pishnamayesh_TizMishavad_VaChap_DastNakhorde()
    {
        var p = Read("PumpYaqobi.App", "Printing", "DocumentPreview.cs");
        Assert.Contains("public const int SharpMinDpi = 144, SharpMaxDpi = 300;", p);
        Assert.Contains("QueueSharpen();", p);
        //  ⛔ چاپ و ذخیره همان ‎Setup.Dpi‎ی کاربر را دارند، نه ورقِ پیش‌نمایش
        Assert.Contains("var dpi = Math.Clamp(Setup.Dpi, 72, 400);", p);
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");
        Assert.Contains("RenderOptions.BitmapInterpolationMode=\"HighQuality\"", w);
    }

    [Theory]
    [InlineData("یادداشتِ بلند", true)]
    [InlineData("hello", false)]
    [InlineData("1,234,567", false)]
    [InlineData("", false)]
    public void RtlTrim_NeveshteyeRastBeChap_RaMishenasad(string s, bool rtl) =>
        Assert.Equal(rtl, PumpYaqobi.App.Controls.RtlTrim.IsRtl(s));
}
