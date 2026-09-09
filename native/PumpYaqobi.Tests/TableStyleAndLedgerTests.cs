using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ دو خواستهٔ بزرگِ همین دور ═══════════════════════════════════════════════
///
///   ۲) «خطوطِ جدول نباید Hard-coded باشند — در تنظیمات بخشِ ظاهرِ جدول باشد
///      با رنگ و ضخامت، و روی همهٔ جدول‌ها اعمال شود.»
///   ۴) «سربرگ، جدول و Summary باید از یک Data Source بخوانند؛ رسیدی که در
///      سربرگ وارد می‌شود همان لحظه ردیفِ خودش را در جدول داشته باشد.»
/// </summary>
public class TableStyleAndLedgerTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    // ══ ظاهرِ جدول‌ها ════════════════════════════════════════════════════════

    /// <summary>رنگ و ضخامت جای یکتا دارند و هیچ‌کدام داخلِ سبک ثابت نیستند.</summary>
    [Fact]
    public void TableLinesComeFromSettingsNotFromTheStyleSheet()
    {
        var t = Read("PumpYaqobi.App", "Themes", "Controls.axaml");

        // رنگ
        Assert.Contains("HorizontalGridLinesBrush\" Value=\"{DynamicResource Pump.Table.Border}\"", t);
        Assert.Contains("VerticalGridLinesBrush\" Value=\"{DynamicResource Pump.Table.Border}\"", t);

        // ضخامت — هر سه خطِ جدول
        Assert.Contains("Rectangle#PART_BottomGridLine", t);
        Assert.Contains("Rectangle#PART_RightGridLine", t);
        Assert.Contains("Rectangle#VerticalSeparator", t);
        Assert.Contains("Value=\"{DynamicResource Pump.Table.Line}\"", t);
        Assert.Contains("Value=\"{DynamicResource Pump.Table.HeadLine}\"", t);

        // مرزِ سربرگ و مرزِ ردیفِ جمله
        Assert.Contains("BorderThickness\" Value=\"{DynamicResource Pump.Table.HeadUnderline}\"", t);
        Assert.Contains("BorderThickness=\"{DynamicResource Pump.Table.SumBorder}\"", t);
    }

    /// <summary>چهار منبعِ پویا واقعاً پُر می‌شوند و با تعویضِ تم هم از نو.</summary>
    [Fact]
    public void TheTableStyleIsAppliedAtStartupAndOnThemeChange()
    {
        var s = Read("PumpYaqobi.App", "Themes", "TableStyle.cs");
        foreach (var key in new[] { "Pump.Table.Border", "Pump.Table.Line",
                                    "Pump.Table.HeadLine", "Pump.Table.HeadUnderline",
                                    "Pump.Table.SumBorder" })
            Assert.Contains("r[\"" + key + "\"]", s);

        Assert.Contains("ThemeManager.Changed += _ => Apply();", s);

        var app = Read("PumpYaqobi.App", "App.axaml.cs");
        Assert.Contains("TableStyle.Hook();", app);
        Assert.Contains("TableStyle.Apply(app: this);", app);
    }

    /// <summary>بخشِ «ظاهرِ جدول‌ها» در صفحهٔ تنظیمات هست و هر سه ضخامت را دارد.</summary>
    [Fact]
    public void SettingsHasATableAppearanceCard()
    {
        var v = Read("PumpYaqobi.App", "Views", "Sections", "SettingsSectionView.axaml");
        Assert.Contains("ظاهرِ جدول‌ها", v);
        Assert.Contains("{Binding TableBorderColor}", v);
        Assert.Contains("{Binding TableLine}", v);
        Assert.Contains("{Binding TableHeadLine}", v);
        Assert.Contains("{Binding TableSumLine}", v);
    }

    // ══ ردیفِ «جمله» زیرِ ستونِ خودش ═══════════════════════════════════════

    /// <summary>
    /// گزارشِ صاحب ریپو: «جمله زیرِ جدول‌ها … همه‌شان یک‌جا، یک کنج دیده
    /// می‌شوند و هر کدام زیرِ بخشِ خودش نیست.» پس هر جمع مختصاتِ ستونِ خودش
    /// را می‌گیرد، نه یک ‎WrapPanel‎ی پشتِ‌سرِ‌هم.
    /// </summary>
    [Fact]
    public void EachTotalSitsUnderItsOwnColumn()
    {
        var src = Read("PumpYaqobi.App", "Controls", "TotalsBar.cs");
        Assert.Contains("public string Column { get; }", src);
        Assert.Contains("class TotalsStrip : Panel", src);
        Assert.Contains("cols.FindIndex(c => Head(c) == want)", src);
        Assert.Contains("child.Arrange(new Rect(left, 0, cols[i].ActualWidth, h));", src);

        var t = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        Assert.Contains("<c:TotalsStrip />", t);
    }

    // ══ رسید — یک رکوردِ واقعی ═══════════════════════════════════════════════
    //
    // ⚠️ آزمون‌های خودِ منطق به ‎ReceiptSourceOfTruthTests‎ رفتند. این‌جا فقط
    // همان چیزی می‌ماند که به **کد** مربوط است: اینکه رسید دیگر انبارِ جدا
    // ندارد و سربرگ عددِ مستقلی نگه نمی‌دارد.

    /// <summary>رسید فقط در ستونِ خودِ ردیف زندگی می‌کند.</summary>
    [Fact]
    public void ThereIsOnlyOneReceiptStore()
    {
        var calc = Read("PumpYaqobi.Application", "Services", "DebtCalculationService.cs");
        Assert.Contains("public bool SyncReceiptTotals(DebtAccount a)", calc);
        Assert.Contains("public decimal ReceiptTotal(DebtAccount a, LedgerMode unit, FuelType fuel)", calc);
        Assert.Contains("public static DebtRow NewReceiptRow(", calc);

        // دفترِ جدا و توابعش دیگر نیستند
        Assert.DoesNotContain("PushRasid(", calc);
        Assert.DoesNotContain("RasidLogSum(", calc);
    }

    /// <summary>سربرگ عددِ خودش را نگه نمی‌دارد — از ردیف‌ها می‌خواند.</summary>
    [Fact]
    public void TheHeaderReadsFromTheRows()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "PersonViewModel.cs");
        Assert.Contains("private decimal HeadRasid(FuelType fuel)", vm);
        Assert.Contains("return IsMoney ? t.Rasid : t.RasidFuel;", vm);
        Assert.Contains("get => Shamsi.MoneyOrBlank(HeadRasid(FuelType.Petrol));", vm);
        Assert.Contains("set => AddHeadReceipt(FuelType.Petrol, value);", vm);

        // و ردیفِ نمایشیِ نسخهٔ پیشین دیگر نیست
        Assert.DoesNotContain("IsAuto", vm);
    }

    /// <summary>
    /// «الباقی»ِ سربرگ فرمولِ خودِ سایت است — برد + فیصدی − رسید — نه جمعِ
    /// سادهٔ ستونِ الباقی.
    /// </summary>
    [Fact]
    public void TheHeaderRemainderUsesTheSiteFormula()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "PersonViewModel.cs");
        Assert.Contains("var comm = rasid * pct / 100m;", vm);
        Assert.Contains("Round0(bord + comm - rasid)", vm);
    }
}
