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
    /// ══ «هم‌قدِ ردیف‌ها شدن» انتخابی است، ولی سقفش دیگر «یک صفحه» نیست ═══
    ///
    /// گزارشِ صاحب ریپو، دو بار: «اون سقفِ زیرینِ جدول‌ها هم هستند، گفتم
    /// اون‌ها هم نباشند.» حق داشت — «یک صفحه» یعنی حدودِ **هفده** ردیف، پس
    /// دفترِ بیست‌ردیفی هم داخلِ کادر گیر می‌کرد.
    ///
    /// حالا سقف به **ردیف** است (‎PageRowLimit‎)، و با عدد انتخاب شده
    /// (‎ledgerperf‎، پس از سبک شدنِ ردیف‌ها): ۸۰ ردیف آزاد و روان (۸۲ تا
    /// ۲۹۳ms در حالتِ پایدار)، ۲۰۰ ردیف نه. هیچ ماهِ واقعی به صد ردیف
    /// نمی‌رسد، پس عملاً سقفی دیده نمی‌شود.
    /// </summary>
    [Fact]
    public void EveryGridGrowsToItsRowsProgressively()
    {
        var g = Bare(Grid());

        // ⚠️ سقفِ «یک صفحه» رفت (خواستهٔ چندبارهٔ صاحب ریپو: «سقفِ زیرِ جدول
        // نباشد که جمله سرِ جا بماند و جدول‌های دیگر از زیرش رد شوند»). هیچ
        // جدولی دیگر با ‎PageRowLimit‎ تنگ نمی‌شود.
        Assert.DoesNotContain("rows > PageRowLimit", g);
        Assert.DoesNotContain("!GrowsToContent && want > screen", g);

        // …ولی همه‌شان **تدریجی** بلند می‌شوند: یک صفحه در پاسِ اول، بقیه در
        // فریم‌های بعد با اولویتِ پس‌زمینه — وگرنه باز شدنِ بخش می‌ایستاد.
        Assert.Contains("QueueGrow(", g);
        Assert.Contains("DispatcherPriority.Background", g);
        Assert.Contains("FirstChunk(", g);

        // و رشد با شمارِ ردیف‌های تازه دوباره راه می‌افتد (‎DataGrid‎ خودش
        // با اضافه شدنِ ردیف دوباره اندازه نمی‌گیرد)
        Assert.Contains("CollectionChanged += OnRowsChanged", g);
    }

    /// <summary>
    /// اصلاحیهٔ بلندی (‎Settle‎) یک بار یک صفحهٔ خالی زیرِ جدول گذاشت: نوارِ
    /// لغزشِ گذرا هزار پیکسل «جای لغزش» داشت و همه‌اش به بلندی اضافه شد.
    /// حالا فقط پس از رشدِ کامل و فقط چند پیکسل.
    /// </summary>
    [Fact]
    public void TheSettleFixIsCappedAndWaitsForFullGrowth()
    {
        var g = Bare(Grid());
        Assert.Contains("if (!_spread || _shown < rows) return;", g);
        var m = Regex.Match(g, @"MaxPad\s*=\s*(\d+)");
        Assert.True(m.Success && int.Parse(m.Groups[1].Value) <= 48, "اصلاحیهٔ بلندی باید چند پیکسل باشد، نه یک صفحه");
        // و بلندیِ ردیف از خودِ ردیفِ چیده‌شده می‌آید، نه از ‎RowHeight‎ی ۴۴
        Assert.Contains("MeasuredRowHeight()", g);
    }

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
    /// ۲۰۰ ردیفی آزادانه بلند شود و ۴٫۶ ثانیه طول بکشد (‎ledgerperf‎).
    ///
    /// ⚠️ و چرا حالا ۲۵۰ بی‌خطر است، در حالی که آن‌وقت نبود: آن ترس مالِ
    /// دفترهای ماهانه بود، و آن‌ها ‎GrowsToContent‎ ندارند — پس همان‌جا با
    /// تنگنای «یک صفحه» گرفته می‌شوند، هر مرزی که این عدد باشد
    /// (‎ledgerperf‎: با ۳٬۰۰۰ ردیف هم ۱۷ ردیفِ زنده و ۸۰۰ پیکسل). این مرز
    /// فقط ورق و پارچه را آزاد می‌کند، و خواستهٔ تازهٔ صاحب ریپو هم دربارهٔ
    /// همان بود: «تا ۱۰۰ تا کادر را به‌راحتی باز کند.»
    /// </summary>
    [Fact]
    public void TheGrowLimitClearsTheRowsTheOwnerNamedButNotHundreds()
    {
        var m = Regex.Match(Grid(), @"GrowRowLimit\s*=\s*(\d+)");
        Assert.True(m.Success, "مرزِ رشد پیدا نشد");
        var limit = int.Parse(m.Groups[1].Value);
        Assert.True(limit >= 120, "ورقِ صدردیفی نباید داخلِ کادر گیر کند");
        // با رشدِ تدریجی، مرز بالاتر رفت تا دفترِ ماهانهٔ بزرگ هم بی سقف باز شود؛
        // ولی همچنان هست: بی هیچ مرزی، دفترِ صدهزارردیفی حافظه را می‌بلعد.
        Assert.True(limit <= 1500, "بی هیچ مرزی، دفترِ صدهزارردیفی برنامه را قفل می‌کند");
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
