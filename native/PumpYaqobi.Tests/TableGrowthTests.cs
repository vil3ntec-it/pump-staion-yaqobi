using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ جدول‌ها بلند می‌شوند، کوچک نمی‌مانند ═════════════════════════════════════
///
/// گزارشِ صاحب ریپو، روشن و بند‌به‌بند: «ساختارِ Excel-like فعلی مورد تأیید
/// است، فقط این مشکل‌ها را ریشه‌ای درست کن — قفل شدنِ ارتفاعِ جدول بعد از چند
/// ردیف، اسکرولِ ناخواسته فقط داخلِ جدول، رشد نکردنِ ارتفاعِ صفحه، شکستنِ خطوط،
/// جابه‌جاییِ عرضِ ستون‌ها، Center نبودن، و Inputِ کوچکِ سفیدِ بزرگ‌شونده.»
///
/// این آزمون متنی است چون چیزی که برگشتنی است همین است: کسی دوباره سقفِ
/// ارتفاع می‌گذارد یا سبکِ کادرِ تایپ را برمی‌دارد. رفتارِ واقعی‌اش را
/// ‎ParchaWaraqAudit‎ روی پنجرهٔ واقعی می‌سنجد (ردیف اضافه می‌کند و بلندی را
/// پیش و پس می‌سنجد).
/// </summary>
public class TableGrowthTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Grid() => Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
    private static string Theme() => Read("PumpYaqobi.App", "Themes", "Controls.axaml");

    /// <summary>همان متن، بی کامنت — برای «این چیز دیگر نباید باشد».</summary>
    private static string Bare(string s) =>
        Regex.Replace(Regex.Replace(s, "<!--.*?-->", "", RegexOptions.Singleline),
                      @"(?m)^\s*(///|//).*$", "");

    // ══ ۱) جدول هم‌قدِ ردیف‌هایش می‌شود — ولی نه بیشتر از یک صفحه ═══════════

    /// <summary>
    /// ══ چرا این آزمون وارونه شد ═══════════════════════════════════════════
    ///
    /// این‌جا تا امروز نوشته بود «هیچ سقفی نباید باشد» و جدول تا ۶۰۰ ردیف
    /// آزادانه بلند می‌شد. ‎ledgerperf‎ اندازه گرفت و معلوم شد همان «آزادی»
    /// ریشهٔ کندیِ گزارش‌شدهٔ صاحب ریپو بود:
    ///
    ///     گاوصندوق با ۲۰۰ ردیف → ۴٬۶۵۲ ms و ۲۰۰ ردیفِ زنده (جدول ۹٬۰۵۱px)
    ///     گاوصندوق با ۳٬۰۰۰ ردیف → ۹۱۶ ms و ۱۷ ردیفِ زنده (جدول ۸۰۰px)
    ///
    /// یعنی هرچه ردیف کمتر، کندتر — چون ‎DataGrid‎ مجازی‌سازی‌اش را از قابِ
    /// خودش می‌گیرد و قابِ ۹٬۰۵۱ پیکسلی یعنی ساختنِ هر ۲۰۰ ردیف.
    ///
    /// پس قاعده عوض شد، و این آزمون همان قاعدهٔ تازه را قفل می‌کند: جدول
    /// هم‌قدِ ردیف‌هایش می‌شود <b>تا یک صفحه</b>، و از آن‌جا به بعد می‌ایستد.
    /// خواستهٔ اصلیِ صاحب ریپو («ردیفِ ۴۰ و ۵۰ نباید در کادر گیر کند») سرِ
    /// جایش است — آن‌ها خیلی زیرِ یک صفحه‌اند.
    /// </summary>
    [Fact]
    public void TheGridStopsGrowingAtOneScreenSoVirtualisationSurvives()
    {
        var g = Bare(Grid());
        Assert.Contains("ScreenHeight()", g);
        // تنگنا باید در خودِ اندازه‌گیری باشد، نه پس از چیدمان
        Assert.Contains("return Capped(availableSize, screen)", g);
        // و «نمی‌دانم» هرگز نباید «بی‌کران» معنی شود
        Assert.Contains("h > 0 ? h : 900", g);
    }

    /// <summary>
    /// ══ پاسِ اولِ اندازه‌گیری همیشه تنگ است ═══════════════════════════════
    ///
    /// ستون‌های ‎Width="Auto"‎ پهنایشان را از محتوای ردیف‌ها می‌گیرند؛ تا
    /// پهنا سفت نشده، هر ردیفِ تازه همهٔ ستون‌ها را دوباره به اندازه‌گیری
    /// می‌اندازد و هزینه <b>مربعی</b> بالا می‌رود. برای همین «مصارف» که هیچ
    /// کشویی‌ای ندارد هم با ۲۰۰ ردیف ۱٫۴ ثانیه می‌گرفت.
    ///
    /// پس تا ‎SpreadColumns‎ پهناها را سفت نکرده، جدول یک صفحه بیشتر
    /// اندازه نمی‌گیرد.
    /// </summary>
    [Fact]
    public void ColumnsAreFrozenBeforeTheGridIsAllowedToGrow()
        => Assert.Contains("if (!_spread) return Capped(", Bare(Grid()));

    /// <summary>
    /// ══ «هم‌قدِ ردیف‌ها شدن» انتخابی است، و پیش‌فرضش خاموش ═══════════════
    ///
    /// چون ساختنِ ردیف گران است (‎ledgerperf‎): گاوصندوق با ۸۰ ردیفِ آزاد
    /// ۱٬۹۳۳ms، و همان وقتی مجازی‌سازی می‌کند ۱۲ تا ۳۸ms. پس دفترهای ماهانه
    /// سرِ یک صفحه می‌ایستند و فقط ورق و پارچه — که کوتاه‌اند و خواستهٔ صریحِ
    /// صاحب ریپو دربارهٔ همان‌ها بود — آزادند.
    /// </summary>
    [Fact]
    public void GrowingToContentIsOptInAndOnlyTheSheetsOptIn()
    {
        var g = Bare(Grid());
        Assert.Contains("GrowsToContentProperty", g);
        Assert.Contains("!GrowsToContent && want > screen", g);

        // ورق روشنش می‌کند…
        Assert.Contains("GrowsToContent=\"True\"",
                        Read("PumpYaqobi.App", "Views", "Sections", "WaraqPageView.axaml"));

        // …و دفترهای ماهانه نه
        foreach (var v in new[] { "SafeSectionView", "ExchangeSectionView", "ExpenseSectionView" })
            Assert.DoesNotContain("GrowsToContent",
                                  Read("PumpYaqobi.App", "Views", "Sections", v + ".axaml"));
    }

    /// <summary>
    /// و تا مرزِ رشد هیچ سقفی روی جدول نمی‌نشیند: نه ‎MaxHeight‎ی هست و نه
    /// چیزی که بلندیِ در دسترس را کم کند.
    ///
    /// ⚠️ تنگنا در ‎MeasureOverride‎ اعمال می‌شود، نه پس از چیدمان — وگرنه
    /// همان پاسِ اولِ اندازه‌گیری با بلندیِ بی‌کران انجام شده و جدولِ بزرگ
    /// همان لحظه همهٔ ردیف‌هایش را ساخته است.
    /// </summary>
    [Fact]
    public void BelowTheLimitTheGridHasNoCeilingAtAll()
    {
        var g = Bare(Grid());
        Assert.DoesNotContain("MaxHeight =", g);
        Assert.Contains("MeasureOverride", g);
        Assert.Contains("GrowRowLimit", g);
    }

    /// <summary>
    /// ══ مرزِ رشد باید بالای «ردیفِ ۴۰ و ۵۰ و ۵۱» باشد ══════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو همین بود: آن ردیف‌ها نباید داخلِ کادر گیر کنند.
    ///
    /// ⚠️ این‌جا یک‌بار نوشته بود «کمتر از ۵۰۰ نباشد» و همان باعث شد جدولِ
    /// ۲۰۰ ردیفی آزادانه بلند شود و ۴٫۶ ثانیه طول بکشد (‎ledgerperf‎). مرز
    /// باید از ۵۱ بالاتر باشد و از چند صد پایین‌تر — نه بیشتر.
    /// </summary>
    [Fact]
    public void TheGrowLimitClearsTheRowsTheOwnerNamedButNotHundreds()
    {
        var m = Regex.Match(Grid(), @"GrowRowLimit\s*=\s*(\d+)");
        Assert.True(m.Success, "مرزِ رشد پیدا نشد");
        var limit = int.Parse(m.Groups[1].Value);
        Assert.True(limit >= 60, "مرزِ رشد باید ردیفِ ۵۱ را با حاشیه در بر بگیرد");
        Assert.True(limit <= 150, "مرزِ رشدِ چندصدی یعنی برگشتن به همان ۴٫۶ ثانیه");
    }

    // ══ ۲) پهنای ستون با تایپ تکان نخورد ════════════════════════════════════

    /// <summary>
    /// در جدولِ پهن هم پهنای ستون سفت می‌شود؛ پیش از این آن‌جا ستون ‎Auto‎
    /// می‌ماند و با هر حرفی که تایپ می‌شد پهن‌تر می‌گشت.
    /// </summary>
    [Fact]
    public void ColumnWidthsAreFrozenEvenWhenThereIsNoSpareRoom()
    {
        var g = Bare(Grid());
        Assert.Contains("DataGridLengthUnitType.Pixel", g);
        Assert.Contains("DataGridLengthUnitType.Star", g);
    }

    // ══ ۳) کادرِ تایپ هم‌اندازهٔ خانه ════════════════════════════════════════

    [Theory]
    [InlineData("<Setter Property=\"MinHeight\" Value=\"0\" />")]
    [InlineData("<Setter Property=\"MinWidth\" Value=\"0\" />")]
    [InlineData("<Setter Property=\"Padding\" Value=\"0\" />")]
    [InlineData("<Setter Property=\"FontSize\" Value=\"17\" />")]
    [InlineData("<Setter Property=\"TextWrapping\" Value=\"NoWrap\" />")]
    [InlineData("<Setter Property=\"AcceptsReturn\" Value=\"False\" />")]
    public void TheCellEditorMatchesTheCell(string setter)
    {
        var block = Between(Theme(), "<Style Selector=\"DataGridCell TextBox\">", "</Style>");
        Assert.Contains(setter, block);
    }

    /// <summary>و همچنان بی‌رنگ و بی‌لبه می‌ماند — نه کادرِ سفیدِ تودرتو.</summary>
    [Fact]
    public void TheCellEditorStaysInvisible()
    {
        var block = Between(Theme(), "<Style Selector=\"DataGridCell TextBox\">", "</Style>");
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", block);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", block);
    }

    // ══ ۴) وسط‌چین، افقی و عمودی ════════════════════════════════════════════

    [Fact]
    public void CellsAndHeadersAreCenteredBothWays()
    {
        // ⚠️ ‎Stretch‎ است نه ‎Center‎ — و وسط‌چینی از ‎TextAlignment‎ی خودِ نوشته
        // می‌آید. با ‎Center‎، ‎ContentPresenter‎ی خانه به اندازهٔ محتوا جمع
        // می‌شد و کادرِ تایپ ۴ پیکسل پهنا می‌گرفت وسطِ خانهٔ ۴۴۲ پیکسلی —
        // همان «توی کادر یک کادرِ دیگر»ی که صاحب ریپو با عکس نشان داد.
        var cell = Between(Theme(), "<Style Selector=\"DataGridCell\">", "</Style>");
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\" />", cell);
        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\" />", cell);

        // و وسط‌چینی سرِ جایش است، فقط از راهِ نوشته
        var text = Between(Theme(), "<Style Selector=\"DataGridCell TextBlock\">", "</Style>");
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", text);
        var edit = Between(Theme(), "<Style Selector=\"DataGridCell TextBox\">", "</Style>");
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", edit);

        var head = Between(Theme(), "<Style Selector=\"DataGridColumnHeader\">", "</Style>");
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />", head);
        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\" />", head);
    }

    // ══ ۵) خطوطِ اکسلیِ جدول دست‌نخورده ═════════════════════════════════════

    /// <summary>
    /// ⚠️ قاعدهٔ صریحِ صاحب ریپو: «جدول‌ها را از حالت Excel-like خارج نکن.»
    /// خطِ افقی و عمودی و ضخامتشان باید همان بمانند.
    /// </summary>
    [Fact]
    public void TheExcelLookIsUntouched()
    {
        var t = Theme();
        Assert.Contains("<Setter Property=\"GridLinesVisibility\" Value=\"All\" />", t);
        Assert.Contains("PART_BottomGridLine", t);
        Assert.Contains("PART_RightGridLine", t);
        Assert.Contains("<Setter Property=\"RowHeight\" Value=\"44\" />", t);
    }

    private static string Between(string s, string from, string to)
    {
        var i = s.IndexOf(from, StringComparison.Ordinal);
        Assert.True(i >= 0, "پیدا نشد: " + from);
        var j = s.IndexOf(to, i, StringComparison.Ordinal);
        Assert.True(j > i, "پایانِ سبک پیدا نشد");
        return s[i..j];
    }
}
