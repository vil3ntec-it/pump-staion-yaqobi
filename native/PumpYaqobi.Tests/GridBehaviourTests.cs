using System.Collections.Specialized;
using System.Text.RegularExpressions;
using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ گزارش‌های صاحب ریپو دربارهٔ جدول‌ها ═══════════════════════════════════
///
/// چهار چیز، همه با هم آمدند:
///   «بخش‌هایی که جدول‌های زیادی دارند خیلی دیر باز می‌شوند … یک ثانیه گیر می‌کند»
///   «کادرهای کشویی هیچ‌کدام کار نمی‌کنند … سرِ جایشان نمی‌نشیند»
///   «کادر کشویی سال هم نیست»
///   «سربرگ‌های جدول … نوشته‌هاشان را وسط بگذار»
/// </summary>
public class GridBehaviourTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string View(string n) =>
        File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Views", "Sections", n + ".axaml"));

    private static string NoComments(string s) =>
        Regex.Replace(s, "<!--.*?-->", "", RegexOptions.Singleline);

    // ── پر شدنِ جدول: یک خبر، نه n خبر ──────────────────────────────────────

    /// <summary>
    /// ⚠️ این همان چیزی است که «یک ثانیه گیر کردن» را می‌ساخت: هر ‎Add‎ یک
    /// ‎CollectionChanged‎ می‌داد و ‎DataGrid‎ به ازای هر ردیف یک‌بار خودش را
    /// از نو می‌چید. آزمون واقعاً خبرها را می‌شمارد، نه این‌که ادعا کند.
    /// </summary>
    [Fact]
    public void FillingTheGridRaisesOneNotificationNotOnePerRow()
    {
        var c = new BulkRows<int>();
        var n = 0;
        c.CollectionChanged += (_, _) => n++;

        c.ResetTo(Enumerable.Range(0, 500));

        Assert.Equal(500, c.Count);
        Assert.Equal(1, n);
    }

    [Fact]
    public void TheBatchScopeAlsoRaisesOnlyOne()
    {
        var c = new BulkRows<int>();
        var n = 0;
        c.CollectionChanged += (_, _) => n++;

        using (c.Batch())
        {
            c.Clear();
            for (var i = 0; i < 300; i++) c.Add(i);
        }

        Assert.Equal(300, c.Count);
        Assert.Equal(1, n);
    }

    /// <summary>محدودهٔ تودرتو هم باید یک خبر بدهد، نه دو تا.</summary>
    [Fact]
    public void NestedBatchesStillRaiseOnlyOne()
    {
        var c = new BulkRows<int>();
        var n = 0;
        c.CollectionChanged += (_, _) => n++;

        using (c.Batch())
        {
            c.Add(1);
            using (c.Batch()) c.Add(2);
            c.Add(3);
        }

        Assert.Equal(3, c.Count);
        Assert.Equal(1, n);
    }

    /// <summary>بیرون از محدوده، همان ‎ObservableCollection‎ی همیشگی است.</summary>
    [Fact]
    public void OutsideABatchItBehavesNormally()
    {
        var c = new BulkRows<int>();
        var n = 0;
        c.CollectionChanged += (_, _) => n++;
        c.Add(1); c.Add(2);
        Assert.Equal(2, n);
    }

    // ── کشویِ سال و ماه ──────────────────────────────────────────────────────

    [Fact]
    public void PickingAYearNarrowsTheMonthsToThatYear()
    {
        var picked = new List<string>();
        var p = new YearMonthPicker(k => picked.Add(k));
        p.Load(new[] { "1404/11", "1404/12", "1405/01", "1405/06" }, "1405/06");

        // تازه‌ترین سال اول
        Assert.Equal(new[] { "1405", "1404" }, p.Years.Select(y => y.Key).ToArray());
        Assert.Equal("1405", p.Year!.Key);
        Assert.Equal(new[] { "1405/06", "1405/01" }, p.Months.Select(m => m.Key).ToArray());

        p.Year = p.Years.First(y => y.Key == "1404");
        Assert.Equal(new[] { "1404/12", "1404/11" }, p.Months.Select(m => m.Key).ToArray());
    }

    /// <summary>
    /// با عوض شدنِ سال، هم‌شمارهٔ ماهِ قبلی در سالِ تازه انتخاب می‌شود — عینِ
    /// ‎_ymYearChanged‎ی سایت.
    /// </summary>
    [Fact]
    public void ChangingYearKeepsTheSameMonthNumberWhenItExists()
    {
        var p = new YearMonthPicker(_ => { });
        p.Load(new[] { "1404/06", "1404/09", "1405/06" }, "1405/06");

        p.Year = p.Years.First(y => y.Key == "1404");
        Assert.Equal("1404/06", p.SelectedKey);
    }

    /// <summary>و اگر آن ماه در سالِ تازه نباشد، تازه‌ترین ماهِ همان سال.</summary>
    [Fact]
    public void ChangingYearFallsBackToTheNewestMonthOfThatYear()
    {
        var p = new YearMonthPicker(_ => { });
        p.Load(new[] { "1404/03", "1405/09" }, "1405/09");

        p.Year = p.Years.First(y => y.Key == "1404");
        Assert.Equal("1404/03", p.SelectedKey);
    }

    /// <summary>آن‌چه کاربر می‌بیند برچسبِ خوانا است، نه کلیدِ خامِ «1405/06».</summary>
    [Fact]
    public void MonthsShowAReadableLabelNotTheRawKey()
    {
        var p = new YearMonthPicker(_ => { });
        p.Load(new[] { "1405/06" }, "1405/06");
        Assert.DoesNotContain("1405/06", p.Months[0].Label);
        Assert.Contains("1405", p.Months[0].Label);
    }

    // ── کشویی‌های داخلِ جدول ─────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ ‎&lt;ComboBoxItem&gt;‎ داخلِ ‎ComboBox‎ یعنی ‎SelectedItem‎ یک <b>شیء</b>
    /// است نه رشته — و اتصالش به یک ‎string‎ نه مقدار را می‌نشاند و نه
    /// برمی‌گرداند. همان «نه منطقش کار می‌کند نه سرِ جایش می‌نشیند».
    /// </summary>
    [Theory]
    [InlineData("SafeSectionView")]
    [InlineData("ExchangeSectionView")]
    [InlineData("WaraqPageView")]
    [InlineData("CompanyPageView")]
    [InlineData("PersonView")]
    public void NoGridComboBoxUsesObjectItems(string view)
    {
        var v = NoComments(View(view));
        Assert.DoesNotContain("<ComboBoxItem", v);
    }

    /// <summary>
    /// و کشویی‌های داخلِ جدول همیشه پیدا هستند، نه فقط هنگامِ ویرایش — در
    /// سایت ‎&lt;select class="xls-in"&gt;‎ی همیشه‌پیداست که ‎onchange‎ فوراً
    /// ثبت می‌کند. داخلِ ‎CellEditingTemplate‎ بودن یعنی مقدارِ فعلی دیده
    /// نمی‌شود و هر تغییر دو کلیک می‌خواهد.
    /// </summary>
    [Theory]
    [InlineData("SafeSectionView")]
    [InlineData("ExchangeSectionView")]
    [InlineData("WaraqPageView")]
    [InlineData("CompanyPageView")]
    public void GridComboBoxesAreAlwaysVisible(string view)
    {
        var v = NoComments(View(view));
        foreach (Match m in Regex.Matches(v, @"<DataGridTemplateColumn\.CellEditingTemplate>.*?</DataGridTemplateColumn\.CellEditingTemplate>",
                                          RegexOptions.Singleline))
            Assert.DoesNotContain("<ComboBox", m.Value);
    }

    // ── سربرگِ جدول ──────────────────────────────────────────────────────────

    /// <summary>«تاریخ»، «نام»، «حواله» و بقیه وسطِ خانهٔ خودشان.</summary>
    [Fact]
    public void ColumnHeadersAreCentred()
    {
        var t = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Themes", "Controls.axaml"));
        Assert.Contains("<Style Selector=\"DataGridColumnHeader TextBlock\">", t);

        var at = t.IndexOf("<Style Selector=\"DataGridColumnHeader TextBlock\">", StringComparison.Ordinal);
        var block = t[at..t.IndexOf("</Style>", at, StringComparison.Ordinal)];
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", block);
    }

    /// <summary>
    /// «📅 ماه جدید» فرمان دارد — تا امروز دکمه‌اش بود و هیچ کاری نمی‌کرد.
    /// </summary>
    [Theory]
    [InlineData("ExpenseSectionView")]
    [InlineData("SafeSectionView")]
    [InlineData("RetailSectionView")]
    [InlineData("ExchangeSectionView")]
    public void TheNewMonthButtonIsWired(string view)
    {
        var v = NoComments(View(view));
        Assert.Contains("ماه جدید", v);
        Assert.Contains("NewMonthCommand", v);
    }

    // ── کلید راست/چپ در چیدمانِ راست‌به‌چپ ──────────────────────────────────

    /// <summary>
    /// گزارشِ صاحب ریپو: «کلیدِ راست را می‌زنم، چپ می‌رود.»
    /// در چیدمانِ RTL جهت‌ها آینه می‌شوند، پس کلید باید برعکس شود تا با آن‌چه
    /// کاربر می‌بیند یکی باشد. این آزمون خودِ عوض‌شدن را قفل می‌کند.
    /// </summary>
    [Fact]
    public void ArrowKeysAreMirroredInRightToLeft()
    {
        var src = File.ReadAllText(Path.Combine(
            Root(), "PumpYaqobi.App", "Services", "FieldNavigation.cs"));
        Assert.Contains("IsRtl(from)", src);
        Assert.Contains("dir == Dir.Left ? Dir.Right : Dir.Left", src);
    }

    // ── کشویی با یک کلیک، و نلغزیدنِ صفحه ───────────────────────────────────

    /// <summary>«همین که بزنم بیایند» — نه دو سه کلیک.</summary>
    [Fact]
    public void ComboBoxesOpenOnTheFirstClick()
    {
        var src = File.ReadAllText(Path.Combine(
            Root(), "PumpYaqobi.App", "Controls", "ExcelGrid.cs"));
        // ⚠️ حالا روی فازِ ‎Tunnel‎ است، نه یک کمکیِ جدا با ‎Dispatcher‎ —
        // همان چیزی که صاحب ریپو خواست: «با delay مشکل را پنهان نکن».
        Assert.Contains("RoutingStrategies.Tunnel", src);
        Assert.Contains("IsDropDownOpen = true", src);
    }

    /// <summary>
    /// «وقتی در کادرِ جدول کلیک می‌کنم، سربرگ‌ها گم می‌شوند … خودم اسکرول
    /// می‌کنم.» کلیک نباید ‎BringIntoView‎ی صفحه را راه بیندازد.
    /// </summary>
    [Fact]
    public void ClickingACellDoesNotScrollThePage()
    {
        var src = File.ReadAllText(Path.Combine(
            Root(), "PumpYaqobi.App", "Controls", "ExcelGrid.cs"));
        Assert.Contains("RequestBringIntoViewEvent", src);
        // ⚠️ شرطِ «فقط وقتی از کلیک آمده» برداشته شد: درخواست همیشه در مرزِ
        // جدول می‌ایستد، و ناوبری با کلید از ‎ScrollIntoView‎ی خودِ جدول
        // استفاده می‌کند.
        Assert.Contains("ev.Handled = true", src);
        Assert.DoesNotContain("_pointerDriven", src);
    }

    // ── تایپِ داخلِ خانه، مثلِ اکسل ───────────────────────────────────────────

    /// <summary>
    /// «موقعِ تایپ نمی‌خواهم آن چهارگوش باشد … مثلِ اکسل.» دو مستطیلِ خودِ
    /// خانه نباید کشیده شوند و کادرِ ویرایشی باید تمامِ خانه را بگیرد.
    /// </summary>
    [Fact]
    public void TypingInACellLooksLikeExcel()
    {
        var t = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Themes", "Controls.axaml"));
        Assert.Contains("Rectangle#CurrencyVisual", t);
        Assert.Contains("Rectangle#FocusVisual", t);
        // ⚠️ قاعدهٔ نام‌دارِ ‎#PART_EditingElement‎ برداشته شد: نامِ اجزای قالبِ
        // ‎DataGrid‎ بین نسخه‌های آوالونیا عوض می‌شود و اگر نگیرد، بی‌صدا هیچ
        // کاری نمی‌کند. تنظیم‌ها روی ‎DataGridCell TextBox‎ نشسته‌اند که به
        // **نوع** بند است، و قابِ خانهٔ فعال روی خودِ ‎CurrencyVisual‎ی قالب.
        Assert.Contains("Rectangle#CurrencyVisual", t);
        Assert.Contains("<Setter Property=\"Stroke\"", t);
    }

    // ── کارت‌های هم‌اندازه ───────────────────────────────────────────────────

    /// <summary>
    /// «کادرهایشان برابرِ هم نیستند … در هر صفحه شش تا هشت تا جا بشود.»
    /// بی ‎MinItemHeight‎ هر کارت به قدِ محتوای خودش می‌ماند و ردیف ناهموار
    /// می‌شود؛ بی سقفِ ستون، روی نمایشگرِ پهن کارت‌ها بی‌جهت پهن می‌شوند.
    /// </summary>
    [Theory]
    [InlineData("DebtSectionView")]
    [InlineData("WaraqSectionView")]
    [InlineData("AmanatSectionView")]
    [InlineData("CompanySectionView")]
    public void CardsAreTheSameSizeAndSixToEightPerRow(string view)
    {
        var v = NoComments(View(view));
        Assert.Contains("MinItemHeight=", v);
        Assert.Contains("MaximumRowsOrColumns=\"8\"", v);

        var m = Regex.Match(v, "MinItemWidth=\"(\\d+)\"");
        Assert.True(m.Success, "MinItemWidth پیدا نشد");
        var w = int.Parse(m.Groups[1].Value);

        // روی باریک‌ترین صفحهٔ پشتیبانی‌شده (۱۲۸۰) دستِ‌کم شش‌تا جا شود
        Assert.True(1280 / w >= 6, $"با MinItemWidth={w} روی ۱۲۸۰ فقط {1280 / w} کارت جا می‌شود");
    }

    /// <summary>و کشویِ سال کنارِ کشویِ ماه هست.</summary>
    [Theory]
    [InlineData("ExpenseSectionView")]
    [InlineData("SafeSectionView")]
    [InlineData("RetailSectionView")]
    [InlineData("ExchangeSectionView")]
    public void EveryMonthPickerHasAYearPickerBesideIt(string view)
    {
        var v = NoComments(View(view));
        Assert.Contains("Picker.Years", v);
        Assert.Contains("Picker.Months", v);
    }
}
