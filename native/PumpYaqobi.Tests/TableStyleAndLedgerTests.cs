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

    // ══ دفترِ رسید — یک منبعِ داده ═══════════════════════════════════════════

    private static DebtCalculationService Calc() => new(new FixedRates());

    private sealed class FixedRates : IUnionRateProvider
    {
        public decimal UnionRate(FuelType fuel) => 60m;
    }

    /// <summary>رسیدِ تازه جای رسیدِ قبلی را نمی‌گیرد — هر کدام رکوردِ خودش.</summary>
    [Fact]
    public void EachHeaderReceiptKeepsItsOwnRecord()
    {
        var c = Calc();
        var a = new DebtAccount();

        c.PushRasid(a, LedgerMode.Fuel, FuelType.Petrol, 5000m);
        c.PushRasid(a, LedgerMode.Fuel, FuelType.Petrol, 2000m);

        Assert.Equal(2, a.RasidLog.Count);
        Assert.Equal(7000m, a.RasidFuelPetrol);          // کادرِ سربرگ = جمعِ دفتر
    }

    /// <summary>پطرول و دیزل، و تیل و پول، چهار دفترِ جدا هستند.</summary>
    [Fact]
    public void TheFourLedgersNeverMix()
    {
        var c = Calc();
        var a = new DebtAccount();

        c.PushRasid(a, LedgerMode.Fuel, FuelType.Petrol, 100m);
        c.PushRasid(a, LedgerMode.Fuel, FuelType.Diesel, 200m);
        c.PushRasid(a, LedgerMode.Money, FuelType.Petrol, 300m);
        c.PushRasid(a, LedgerMode.Money, FuelType.Diesel, 400m);

        Assert.Equal(100m, a.RasidFuelPetrol);
        Assert.Equal(200m, a.RasidFuelDiesel);
        Assert.Equal(300m, a.RasidMoneyPetrol);
        Assert.Equal(400m, a.RasidMoneyDiesel);
    }

    /// <summary>پاک کردنِ یک رسید فقط همان یکی را می‌بَرد و جمع را کم می‌کند.</summary>
    [Fact]
    public void DeletingOneReceiptLeavesTheOthers()
    {
        var c = Calc();
        var a = new DebtAccount();
        var first = c.PushRasid(a, LedgerMode.Fuel, FuelType.Petrol, 5000m);
        c.PushRasid(a, LedgerMode.Fuel, FuelType.Petrol, 2000m);

        Assert.True(c.RemoveRasid(a, first));
        Assert.Single(a.RasidLog);
        Assert.Equal(2000m, a.RasidFuelPetrol);
    }

    /// <summary>حسابِ قدیمی که فقط چهار عدد دارد، یک‌بار دفتر می‌گیرد.</summary>
    [Fact]
    public void OldAccountsGetALedgerFromTheirFourNumbers()
    {
        var c = Calc();
        var a = new DebtAccount { RasidFuelPetrol = 900m, RasidMoneyDiesel = 50m };

        Assert.True(c.RasidLogInit(a));
        Assert.Equal(2, a.RasidLog.Count);
        Assert.False(c.RasidLogInit(a));                 // دومین بار دیگر نه

        c.RasidLogSync(a);
        Assert.Equal(900m, a.RasidFuelPetrol);
        Assert.Equal(50m, a.RasidMoneyDiesel);
    }

    /// <summary>صفر رسید نیست — ثبت نمی‌شود.</summary>
    [Fact]
    public void ZeroIsNotAReceipt()
    {
        var c = Calc();
        var a = new DebtAccount();
        Assert.Null(c.PushRasid(a, LedgerMode.Fuel, FuelType.Petrol, 0m));
        Assert.Empty(a.RasidLog);
    }

    /// <summary>
    /// ردیفِ 📌ِ جدول از همان دفتر می‌آید، در هیچ جمعی شمرده نمی‌شود و در
    /// دیتابیس نوشته نمی‌شود — قاعدهٔ صریحِ پروژه.
    /// </summary>
    [Fact]
    public void TheAutoRowIsDisplayOnly()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "PersonViewModel.cs");
        Assert.Contains("public RasidEntry? Auto { get; private init; }", vm);
        Assert.Contains("if (IsAuto) return;", vm);
        Assert.Contains("IsAuto ? Task.CompletedTask : _owner.SaveRowAsync(_r)", vm);
        Assert.Contains("Calc.SplitTotals(Rows.Where(r => !r.IsAuto)", vm);
        Assert.Contains("AddRasidRows(want);", vm);
    }

    /// <summary>سربرگ که نوشته شود، رسیدِ تازه در همان دفتر ثبت می‌شود.</summary>
    [Fact]
    public void TheHeaderBoxWritesIntoTheLedger()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "PersonViewModel.cs");
        Assert.Contains("set => PushHeadRasid(FuelType.Petrol, value);", vm);
        Assert.Contains("set => PushHeadRasid(FuelType.Diesel, value);", vm);
        Assert.Contains("Calc.PushRasid(Entity, unit, fuel, v, Shamsi.Today())", vm);

        // کادر با دست خوردن خالی می‌شود و با بیرون رفتن ثبت — مثلِ سایت
        var v = Read("PumpYaqobi.App", "Views", "Sections", "PersonView.axaml");
        Assert.Contains("UpdateSourceTrigger=LostFocus", v);
        Assert.Contains("GotFocus=\"RasidBoxFocus\"", v);
    }
}
