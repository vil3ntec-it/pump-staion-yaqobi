using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ردیفِ «جمله»ی ته جدول‌ها ═══════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «آخرِ هر جدول جمله ندارد — در سایت بگرد و همان مدل این‌جا
/// هم پیاده شود.» در سایت هر جدولِ اکسلی یک ‎&lt;tfoot class="xls-foot"&gt;‎ دارد.
///
/// این آزمون متنی است چون چیزی که برگشتنی است، همین است: کسی جدولی را جابه‌جا
/// می‌کند و نوارِ «جمله» جا می‌ماند. آزمونِ رفتاری این را نمی‌گیرد، چون نبودنِ
/// یک نوارِ نمایشی هیچ محاسبه‌ای را نمی‌شکند — فقط کاربر عددش را نمی‌بیند.
/// </summary>
public class TotalsRowTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string View(string name) =>
        File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Views", "Sections", name + ".axaml"));

    /// <summary>هر جدولی که عددِ جمع‌شدنی دارد، باید نوارِ «جمله» هم داشته باشد.</summary>
    [Theory]
    [InlineData("PersonView")]            // حسابِ قرض‌دار
    [InlineData("AmanatSectionView")]     // تیل امانت
    [InlineData("CompanyPageView")]       // دفترِ شرکت
    [InlineData("WaraqPageView")]         // ورقِ روزانه
    [InlineData("RetailSectionView")]     // چکنه
    [InlineData("SafeSectionView")]       // گاوصندوق
    [InlineData("ExchangeSectionView")]   // صرافی
    [InlineData("ExpenseSectionView")]    // مصارف
    [InlineData("ParchaReceiptSectionView")]
    [InlineData("DebtReceiptSectionView")]
    [InlineData("OldLoansSectionView")]
    [InlineData("StaffShortSectionView")]
    [InlineData("StorageSectionView")]
    [InlineData("TankerSectionView")]
    [InlineData("AttendanceSectionView")]
    [InlineData("InvRateSectionView")]
    public void EveryNumericTableHasATotalsRow(string view)
        => Assert.Contains("c:TotalsBar", View(view));

    /// <summary>
    /// نوارِ «جمله» بیرونِ جدول می‌ماند و هرگز ردیفِ ساختگی داخلِ آن نمی‌شود —
    /// قاعدهٔ صریحِ پروژه: ردیفِ نمایشی در هیچ جمعی شمرده نمی‌شود.
    /// </summary>
    [Fact]
    public void TheTotalsBarIsNeverAGridRow()
    {
        var src = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Controls", "TotalsBar.cs"));
        Assert.DoesNotContain(": DataGrid", src);
        Assert.Contains("TemplatedControl", src);
    }

    // ══ حسابِ قرض‌دار — سه خواستهٔ دیگرِ همان گزارش ═══════════════════════════

    /// <summary>«حساب فرعی یک کادرِ کشویی است»، نه چیپ‌های افقی.</summary>
    [Fact]
    public void SubAccountsAreADropdown()
    {
        var v = View("PersonView");
        Assert.Contains("<ComboBox ItemsSource=\"{Binding Accounts}\"", v);
        Assert.DoesNotContain("Classes=\"chips\"", v);
    }

    /// <summary>
    /// «نوعِ تیل کادرهایی مثلِ رادیو دارد که یکی‌شان انتخاب می‌شود».
    /// ⚠️ گروهِ رادیوها دیگر رشتهٔ ثابتِ «rowFuel» نیست: با نامِ ثابت، آوالونیا
    /// همهٔ ردیف‌های جدول را یک گروه می‌دید و زدنِ «دیزل» در یک ردیف انتخابِ
    /// همهٔ ردیف‌های دیگر را برمی‌داشت. حالا هر ردیف ‎FuelGroup‎ی یکتا دارد
    /// (‎"rowFuel-" + Id‎)، پس این آزمون همان اتصال را می‌خواهد، نه رشتهٔ ثابت را.
    /// </summary>
    [Fact]
    public void FuelTypeIsRadioButtons()
    {
        var v = View("PersonView");
        Assert.Contains("<RadioButton GroupName=\"{Binding FuelGroup}\" Content=\"پطرول\"", v);
        Assert.Contains("<RadioButton GroupName=\"{Binding FuelGroup}\" Content=\"دیزل\"", v);
        Assert.DoesNotContain("GroupName=\"rowFuel\"", v);

        var vm = File.ReadAllText(Path.Combine(
            Root, "PumpYaqobi.App", "ViewModels", "Sections", "PersonViewModel.cs"));
        Assert.Contains("public string FuelGroup", vm);
    }

    /// <summary>«کادرِ جدول‌های آرشیو» و دکمهٔ «جدول جدید» هر دو سرِ جایشان.</summary>
    [Fact]
    public void ArchiveBoxAndNewTableButtonExist()
    {
        var v = View("PersonView");
        Assert.Contains("ToggleArchivesCommand", v);
        Assert.Contains("NewTableCommand", v);
    }

    /// <summary>«آموزشِ صدا» از همه‌جا رفته — خواستهٔ صریحِ صاحب ریپو.</summary>
    [Fact]
    public void VoiceTrainingIsGoneEverywhere()
    {
        var app = Path.Combine(Root, "PumpYaqobi.App");
        var hits = Directory.EnumerateFiles(app, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".axaml"))
            .Where(f => File.ReadAllText(f).Contains("VoiceTeach"))
            .ToList();
        Assert.Empty(hits);
    }
}
