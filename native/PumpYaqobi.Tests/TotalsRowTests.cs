using System.Text.RegularExpressions;
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

    /// <summary>
    /// همان ویو، ولی بی کامنت‌های XML.
    ///
    /// ⚠️ برای آزمون‌های «این چیز دیگر نباید باشد» حتماً از این استفاده کنید،
    /// نه از ‎View()‎. کامنت‌های این ریپو توضیح می‌دهند چه چیزی برداشته شد و
    /// چرا — یعنی نامِ همان چیزِ برداشته‌شده داخلشان نوشته است. با ‎View()‎
    /// خامه، آزمون خودِ توضیح را «هنوز هست» می‌خواند و بی‌جهت قرمز می‌شود.
    /// </summary>
    private static string ViewNoComments(string name) =>
        Regex.Replace(View(name), "<!--.*?-->", "", RegexOptions.Singleline);

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
    /// «نوعِ تیل کادرهایی مثلِ رادیو دارد که یکی‌شان انتخاب می‌شود» — و در
    /// سایت این دو **روی هم**‌اند، نه بغلِ هم:
    ///
    ///     .ft-pick{ display:flex; flex-direction:column; ... }
    ///
    /// گزارشِ صاحب ریپو با عکسِ سایت: «نوع تیل رو هم ببین پایین و بالا هم استن
    /// نه بغل به بغل هم … خیلی بزرگ می‌شه و خیلی خرابه.»
    ///
    /// ⚠️ گروهِ رادیوها رشتهٔ ثابت نیست: با نامِ ثابت، آوالونیا همهٔ ردیف‌های
    /// جدول را یک گروه می‌دید و زدنِ «دیزل» در یک ردیف انتخابِ همهٔ ردیف‌های
    /// دیگر را برمی‌داشت.
    /// </summary>
    [Fact]
    public void FuelTypeIsRadioButtons()
    {
        var v = View("PersonView");
        Assert.Contains("<RadioButton GroupName=\"{Binding FuelGroup}\" Content=\"⛽ پطرول\"", v);
        Assert.Contains("<RadioButton GroupName=\"{Binding FuelGroup}\" Content=\"🟤 دیزل\"", v);
        Assert.DoesNotContain("GroupName=\"rowFuel\"", v);

        var vm = File.ReadAllText(Path.Combine(
            Root, "PumpYaqobi.App", "ViewModels", "Sections", "PersonViewModel.cs"));
        Assert.Contains("public string FuelGroup", vm);
    }

    /// <summary>
    /// ⚠️ ستونِ «تیل» بغل‌به‌بغل نشود. ‎StackPanel‎ی که این دو رادیو را نگه
    /// می‌دارد نباید ‎Orientation="Horizontal"‎ بگیرد — پیش‌فرضش عمودی است و
    /// همان چیزی است که سایت دارد. اگر روزی افقی شود، ستون دوباره پهن می‌شود
    /// و همان شکایتِ «خیلی بزرگ می‌شه» برمی‌گردد.
    /// </summary>
    [Fact]
    public void FuelRadiosAreStackedNotSideBySide()
    {
        var v = View("PersonView");
        var at = v.IndexOf("GroupName=\"{Binding FuelGroup}\"", StringComparison.Ordinal);
        Assert.True(at > 0, "ستونِ نوع تیل پیدا نشد");

        // ظرفِ نزدیکِ بالادستِ همین دو رادیو
        var open = v.LastIndexOf("<StackPanel", at, StringComparison.Ordinal);
        Assert.True(open > 0);
        var head = v.Substring(open, at - open);
        Assert.DoesNotContain("Orientation=\"Horizontal\"", head);
    }

    /// <summary>
    /// سربرگِ حساب: نشانِ «⛽ حساب پطرول» **کنارِ** ردیف می‌نشیند، نه بالای آن —
    /// عینِ ‎#personModal .fs-group{display:flex;align-items:center}‎ی سایت.
    /// گزارشِ صاحب ریپو: «حساب پطرول و دیزل بغلِ فیصدی استن و بالا نوشته نشده
    /// آن‌جوری که تو کشیدی.»
    ///
    /// و دو کارت روی هم می‌مانند (پطرول بالا، دیزل پایین).
    /// </summary>
    [Fact]
    public void AccountBadgesSitBesideTheStatsRow()
    {
        var v = View("PersonView");
        Assert.Contains("RowDefinitions=\"Auto,8,Auto\"", v);          // روی هم
        Assert.Contains("Classes=\"fshead\"", v);                       // نشانِ کنارِ ردیف
        Assert.Contains("<Grid ColumnDefinitions=\"Auto,10,*\">", v);   // نشان │ فاصله │ چهار خانه
        Assert.DoesNotContain("ColumnDefinitions=\"*,12,*\"", v);       // نه بغلِ هم
    }

    /// <summary>
    /// ⛔ نوارِ پهنِ فیصدی/رسید/سپرده که زیرِ سربرگ بود، رفته.
    /// خواستهٔ صریحِ صاحب ریپو با عکس و کادرِ قرمز: «آن بخش که در عکس سوم زدم
    /// را برمی‌داری و دیگر نبینمش.»
    ///
    /// رسیدها جای خودشان در سربرگ‌اند و «واحد پول» دکمهٔ سربرگ است، نه
    /// تیک‌مارک: «واحد پول و تیل چرا شبیهِ کادرِ آن بالا بغلِ پی‌دی‌اف نیست؟»
    /// </summary>
    [Fact]
    public void TheWideFieldStripIsGone()
    {
        var v = ViewNoComments("PersonView");
        Assert.DoesNotContain("رسیدِ تیلِ پطرول", v);
        Assert.DoesNotContain("رسیدِ پولِ پطرول", v);
        Assert.DoesNotContain("<CheckBox Content=\"واحدِ پول\"", v);

        // به‌جایش: دکمهٔ دفتر در سربرگ، و فیصدیِ بسته‌شونده
        Assert.Contains("ModeToggleText", v);
        Assert.Contains("IsVisible=\"{Binding IsPercentOpen}\"", v);
        Assert.Contains("TogglePercentCommand", v);
    }

    /// <summary>
    /// چیزهایی که سایت داشت و نیتیو اصلاً نداشت: «→ قبلی / بعدی ←»،
    /// «✏️ تغییر اسم» و کادرِ «📌 نوت».
    /// </summary>
    [Fact]
    public void PersonHeaderHasTheSiteButtons()
    {
        var v = View("PersonView");
        Assert.Contains("PrevPersonCommand", v);
        Assert.Contains("NextPersonCommand", v);
        Assert.Contains("RenamePersonCommand", v);
        Assert.Contains("AccountNote", v);
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
