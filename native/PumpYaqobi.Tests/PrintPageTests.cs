using PumpYaqobi.App.Printing;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ صفحهٔ چاپ ══════════════════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو با عکسِ پشتِ‌صحنهٔ چاپِ اکسل: «بخشِ پرینت باید این
/// مدلی باشد؛ در سایت هم همین مدل بود — با تمامِ منطق‌هایی که داشت.»
///
/// این آزمون دو چیز را نگه می‌دارد:
///   ۱. **شکل** — ستونِ تنظیمات با همان ترتیب و همان کادرها.
///   ۲. **منطق** — «کدام ورق‌ها»، «تعدادِ نسخه» و «مرتب/نامرتب» واقعاً همان
///      کاری را بکنند که ‎doPrint()‎ی سایت می‌کند.
/// </summary>
public class PrintPageTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    /// <summary>
    /// کامنت‌های سی‌شارپ را برمی‌دارد — برای آزمون‌های «دیگر نباید باشد».
    ///
    /// بی این، آزمون توضیحِ خودمان را می‌خواند و قرمز می‌شود: کامنتی که
    /// می‌نویسد «فلان چیز نباید باشد»، خودش همان رشته را در فایل می‌گذارد.
    /// </summary>
    private static string NoComments(string code) =>
        System.Text.RegularExpressions.Regex.Replace(code, @"//[^\n]*", "");

    // ══ منطق ═════════════════════════════════════════════════════════════════

    /// <summary>«چاپِ همهٔ گزارش» یعنی ۱ تا آخر.</summary>
    [Fact]
    public void AllMeansEveryPage()
    {
        var s = PageSetup.Default;
        Assert.Equal(new[] { 1, 2, 3 }, PrintJob.Picked(s, 3, 1));
    }

    /// <summary>«چاپِ همین ورق» یعنی فقط ورقی که باز است.</summary>
    [Fact]
    public void CurrentMeansTheOpenPage()
    {
        var s = PageSetup.Default with { What = PrintWhat.Current };
        Assert.Equal(new[] { 2 }, PrintJob.Picked(s, 5, 2));
    }

    /// <summary>بازه — و اگر وارونه نوشته شده باشد، خودش صافش می‌کند.</summary>
    [Theory]
    [InlineData(2, 4, new[] { 2, 3, 4 })]
    [InlineData(4, 2, new[] { 2, 3, 4 })]     // وارونه
    [InlineData(0, 99, new[] { 1, 2, 3, 4, 5 })] // بیرون از دامنه
    public void RangeIsClampedAndOrdered(int from, int to, int[] want)
    {
        var s = PageSetup.Default with { What = PrintWhat.Range, From = from, To = to };
        Assert.Equal(want, PrintJob.Picked(s, 5, 1));
    }

    /// <summary>«مرتب» یعنی ۱،۲،۳ ۱،۲،۳ — همان خطِ زیرِ کادر.</summary>
    [Fact]
    public void CollatedRepeatsTheWholeSet()
    {
        var s = PageSetup.Default with { Copies = 3, Collate = true };
        Assert.Equal(new[] { 1, 2, 1, 2, 1, 2 }, PrintJob.Order(s, 2, 1));
    }

    /// <summary>«نامرتب» یعنی ۱،۱،۱ ۲،۲،۲.</summary>
    [Fact]
    public void UncollatedRepeatsEachPage()
    {
        var s = PageSetup.Default with { Copies = 3, Collate = false };
        Assert.Equal(new[] { 1, 1, 1, 2, 2, 2 }, PrintJob.Order(s, 2, 1));
    }

    /// <summary>یک نسخه یعنی همان فهرست، بی تکرار.</summary>
    [Fact]
    public void OneCopyChangesNothing()
    {
        var s = PageSetup.Default;
        Assert.Equal(new[] { 1, 2 }, PrintJob.Order(s, 2, 1));
        Assert.True(PrintJob.IsWholeDocument(PrintJob.Order(s, 2, 1), 2));
    }

    /// <summary>
    /// وقتی همهٔ ورق‌ها یک‌بار و به ترتیب خواسته شده‌اند، همان فایلِ اصلی کافی
    /// است — یعنی PDFِ برداری با متنِ قابلِ جست‌وجو، نه ورق‌های تصویری.
    /// </summary>
    [Fact]
    public void ASubsetIsNotTheWholeDocument()
    {
        var range = PageSetup.Default with { What = PrintWhat.Range, From = 2, To = 3 };
        Assert.False(PrintJob.IsWholeDocument(PrintJob.Order(range, 5, 1), 5));

        var twice = PageSetup.Default with { Copies = 2 };
        Assert.False(PrintJob.IsWholeDocument(PrintJob.Order(twice, 2, 1), 2));
    }

    /// <summary>سندِ بی‌ورق هیچ‌چیز نمی‌دهد — نه استثنا.</summary>
    [Fact]
    public void NoPagesMeansNothingToPrint()
        => Assert.Empty(PrintJob.Order(PageSetup.Default, 0, 1));

    /// <summary>
    /// «تعدادِ نسخه» و «کدام ورق‌ها» چیدمانِ ورق را عوض نمی‌کنند، پس نباید
    /// سند را از نو بسازند — وگرنه با هر بالا بردنِ عددِ نسخه، کلِ گزارش
    /// دوباره رسم می‌شد.
    /// </summary>
    [Fact]
    public void JobSettingsDoNotForceARebuild()
    {
        var a = PageSetup.Default;
        var b = a with { Copies = 7, Collate = false, What = PrintWhat.Range, From = 2, To = 3 };
        Assert.Equal(a.LayoutOnly(), b.LayoutOnly());

        // ولی کاغذ و مقیاس چرا
        Assert.NotEqual(a.LayoutOnly(), (a with { Paper = "A5" }).LayoutOnly());
        Assert.NotEqual(a.LayoutOnly(), (a with { Scale = PrintScale.FitPage }).LayoutOnly());
    }

    // ══ شکل ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// ستونِ تنظیمات، با همان ترتیبی که ‎sidebarHtml()‎ی سایت دارد و عکسِ
    /// صاحب ریپو نشان می‌دهد.
    /// </summary>
    [Fact]
    public void TheRailHasEverySectionOfTheExcelBackstage()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");

        foreach (var piece in new[]
        {
            "تعدادِ نسخه",                    // Copies
            "چاپگر", "آماده",                 // Printer ▸ Ready
            "انتخابِ چاپگر و تنظیماتش…",       // Printer Properties
            "{Binding Whats}",                // Print Active Sheets
            "ورق‌ها:",                        // Pages: … to …
            "{Binding Collates}",             // Collated
            "{Binding Orientations}",         // Orientation
            "{Binding Papers}",               // A4
            "{Binding MarginChoices}",        // Margins
            "{Binding Scales}",               // Scaling
            "تنظیمِ ورق…",                    // Page Setup
        })
            Assert.Contains(piece, w);
    }

    /// <summary>پیش‌نمایش و نوارِ پایینش — ‎◀ [۱] از ۲ ▶‎ و دو دکمهٔ گوشه.</summary>
    [Fact]
    public void ThePreviewHasThePageStrip()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");
        Assert.Contains("{Binding PageNumberText}", w);
        Assert.Contains("PrevCommand", w);
        Assert.Contains("NextCommand", w);
        Assert.Contains("ZoomPageCommand", w);
        Assert.Contains("{Binding ShowGuides}", w);
        Assert.Contains("{Binding GuideMargin}", w);
    }

    /// <summary>
    /// دو ستون است، نه یک پنجرهٔ تنظیماتِ جدا: ستونِ تنظیمات و پیش‌نمایش
    /// کنارِ هم دیده می‌شوند — همان چیزی که در عکس است.
    /// </summary>
    [Fact]
    public void ItIsTwoPanesSideBySide()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");
        Assert.Contains("ColumnDefinitions=\"Auto,*\"", w);
        Assert.Contains("WindowState=\"Maximized\"", w);
        // پیش‌نمایش آینه نمی‌شود؛ وگرنه خط‌چینِ حاشیه چپ و راستش برعکس می‌افتاد
        Assert.Contains("FlowDirection=\"LeftToRight\"", w);
    }

    /// <summary>کشوها کارتِ دوخطیِ اکسل‌اند: نشانه، عنوانِ پررنگ و یک خطِ توضیح.</summary>
    [Fact]
    public void TheDropdownsAreExcelStyleCards()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");
        // ⚠️ گزینشگر باید روی کلاس باشد: کلیدِ سبکِ SettingCard همان ComboBox
        // است و ‎c|SettingCard‎ هیچ‌وقت نمی‌خورد — کارت‌ها یک کشوی باریکِ ساده
        // می‌شدند.
        Assert.Contains("Selector=\"ComboBox.card\"", w);
        Assert.DoesNotContain("Selector=\"c|SettingCard\"", w);
        Assert.Contains("<c:SettingCard Classes=\"card\" HorizontalAlignment=\"Stretch\"", w);
        Assert.Contains("{Binding Icon}", w);
        Assert.Contains("{Binding Note}", w);

        var vm = Read("PumpYaqobi.App", "Printing", "PrintSetupViewModel.cs");
        Assert.Contains("string Icon = \"\"", vm);
    }

    /// <summary>
    /// ══ چرخِ ماوس مقدارِ کادر را عوض نمی‌کند ═══════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «روی هر کادر که می‌روم با اسکرول تغییر می‌کنند؛ باید
    /// با کلیک بتوانم تغییر بدهم.»
    ///
    /// ریشه‌اش خودِ ‎ComboBox‎ی آوالونیا بود که با چرخ ‎SelectNext/Previous‎
    /// می‌زند. ‎SettingCard‎ آن را برمی‌دارد — و مهم‌تر، رویداد را ‎Handled‎ هم
    /// نمی‌کند تا ستون همچنان بلغزد.
    /// </summary>
    [Fact]
    public void TheWheelScrollsTheRailInsteadOfChangingTheSetting()
    {
        var c = Read("PumpYaqobi.App", "Controls", "SettingCard.cs");
        Assert.Contains("class SettingCard : ComboBox", c);
        Assert.Contains("protected override void OnPointerWheelChanged(PointerWheelEventArgs e)", c);

        // نه ‎base‎ صدا زده می‌شود و نه رویداد مصرف — هر دو لازم است.
        //
        // ⚠️ کامنت‌ها برداشته می‌شوند، وگرنه آزمون توضیحِ خودِ آن فایل را
        // می‌خواند: همان‌جا نوشته «نه base، نه e.Handled» و آزمون قرمز می‌شد.
        var body = NoComments(c[c.IndexOf("protected override void OnPointerWheelChanged",
                                          StringComparison.Ordinal)..]);
        Assert.DoesNotContain("base.OnPointerWheelChanged", body);
        Assert.DoesNotContain("e.Handled", body);
    }

    /// <summary>
    /// ══ ستونِ تنظیمات سمتِ **چپ** ═══════════════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «آن کادرها باید سمت چپ باشند نه راست.»
    ///
    /// و خودِ سایت هم همین است — خطِ ۲۵۶۴۹ی ‎index.html‎:
    ///     ‎#xlpr{ … direction:ltr … }‎
    ///     ‎#xlpr .xs{flex:0 0 352px; … direction:rtl}‎
    /// یعنی کلِ پنجره چپ‌به‌راست و فقط ستون داخلش راست‌به‌چپ، پس ستون اولین
    /// چیزِ سمتِ چپ می‌شود.
    /// </summary>
    [Fact]
    public void TheRailSitsOnTheLeftLikeTheSite()
    {
        var w = Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");

        // کلِ پنجره چپ‌به‌راست
        Assert.Contains("FlowDirection=\"LeftToRight\" Background=\"#ffffff\"", w);

        // ستون، ستونِ **اول** است و خودش راست‌به‌چپ
        var at = w.IndexOf("Grid.Column=\"0\" Width=\"352\"", StringComparison.Ordinal);
        Assert.True(at > 0, "ستونِ تنظیمات پیدا نشد");
        Assert.Contains("FlowDirection=\"RightToLeft\"", w.Substring(at, 300));

        // و دیگر کلِ پنجره راست‌به‌چپ نیست — همان اشتباهی که ستون را آینه کرد
        Assert.DoesNotContain("FlowDirection=\"RightToLeft\" Background=", w);
    }

    /// <summary>اندازه‌ها و رنگ‌ها از خودِ سایت‌اند، نه حدسی.</summary>
    [Theory]
    [InlineData("#217346")]           // ‎.xt‎ و ‎.xh2‎ و ‎.xpdf‎ — سبزِ اکسل
    [InlineData("Height=\"44\"")]     // ‎.xt{height:44px}‎
    [InlineData("Width=\"352\"")]     // ‎.xs{flex:0 0 352px}‎
    [InlineData("#f3f2f1")]           // ‎.xp{background:#f3f2f1}‎
    [InlineData("#faf9f8")]           // ‎.xb{background:#faf9f8}‎
    [InlineData("#d2d0ce")]           // لبهٔ کارت‌ها
    [InlineData("#0f6cbd")]           // ‎.xlink‎
    [InlineData("22,14,22,30")]       // ‎.xs{padding:14px 22px 30px}‎
    public void TheMeasurementsComeFromTheSite(string piece)
        => Assert.Contains(piece, Read("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml"));

    /// <summary>
    /// «تنظیمِ ورق» نباید چیزهایی را که نشان نمی‌دهد پاک کند — پیش از این با
    /// هر تایید، تعدادِ نسخه و بازهٔ ورق‌ها به پیش‌فرض برمی‌گشتند.
    /// </summary>
    [Fact]
    public void ThePageSetupDialogKeepsTheJobSettings()
    {
        var job = PageSetup.Default with
        {
            Copies = 7, Collate = false, What = PrintWhat.Range, From = 2, To = 3,
        };
        var vm = new PumpYaqobi.App.Printing.PrintSetupViewModel(job);

        // تایید، بی هیچ تغییری
        var built = vm.Build();
        Assert.Equal(7, built.Copies);
        Assert.False(built.Collate);
        Assert.Equal(PrintWhat.Range, built.What);
        Assert.Equal((2, 3), (built.From, built.To));

        // حتی «برگرداندن به پیش‌فرض» هم کارِ چاپ را دست نمی‌زند — آن‌ها در
        // این پنجره نیستند و نباید بی‌صدا پاک شوند.
        vm.ResetCommand.Execute(null);
        var reset = vm.Build();
        Assert.Equal(7, reset.Copies);
        Assert.Equal(PrintWhat.Range, reset.What);
        Assert.Equal(PageSetup.Default.MarginTop, reset.MarginTop);   // ولی خودِ ورق برگشت
    }

    /// <summary>مقیاس یک جا اعمال می‌شود — پس روی همهٔ گزارش‌ها یکسان است.</summary>
    [Fact]
    public void ScalingIsAppliedInOnePlaceForEveryReport()
    {
        var d = Read("PumpYaqobi.Reporting", "Pdf", "DocStyle.cs");
        Assert.Contains("PrintScale.Custom => c.Scale(", d);
        Assert.Contains("Sized(page.Content())", d);

        // حالت‌های «جا دادن» پیش از ساختنِ سند حل می‌شوند — یک جا، برای همه
        var p = Read("PumpYaqobi.App", "Printing", "DocumentPreview.cs");
        Assert.Contains("ScaleSolver.Solve(s, _build)", p);
        Assert.DoesNotContain("_doc = _build(", NoComments(p));   // همه از BuildSolved می‌گذرند
    }

    // ══ کم‌بودی‌های چاپ که برطرف شد ══════════════════════════════════════════
    //
    // گزارشِ صاحب ریپو: «بخشِ پرینت خیلی کم‌بودی دارد، نه شبیهِ سایت است نه
    // شبیهِ اکسل.» این‌ها همان چیزهایی‌اند که کارگاهِ چاپِ سایت داشت و این‌جا
    // نبود — هر کدام یک سنجه، تا برنگردند.

    /// <summary>
    /// سرستونِ جدول فقط در ورقِ اول — خواستهٔ صریحِ صاحب ریپو («چرا آن سربرگِ
    /// جدول در صفحهٔ دیگر هم هست؟ نباید باشد»). تا پیش از این همهٔ گزارش‌ها
    /// مستقیم ‎t.Header(‎ می‌زدند و موتور روی هر ورق تکرارش می‌کرد.
    /// </summary>
    [Fact]
    public void NoReportRepeatsTheTableHeaderOnItsOwn()
    {
        var dir = Path.Combine(Root, "PumpYaqobi.Reporting", "Pdf");
        foreach (var f in Directory.GetFiles(dir, "*Report.cs"))
            Assert.DoesNotContain("t.Header(", NoComments(File.ReadAllText(f)));
    }

    /// <summary>
    /// و واقعاً روی ورق: با تکرار، سرستون جای می‌گیرد و ورقِ بیشتری می‌خورد.
    /// (هشتاد ردیف روی A5 چند ورق می‌شود؛ تکرارِ سرستون در هر ورق جا می‌گیرد.)
    /// </summary>
    [Fact]
    public void RepeatingTheHeaderReallyCostsRows()
    {
        PdfEngine.Initialize();
        var input = new ExpenseReportInput("سنبله 1405", Rows(120), "1405/06/09", "—");
        var off = new ExpenseReport(input) { Setup = PageSetup.Default with { Paper = "A5", Orientation = PageOrientation.Portrait, RepeatHead = false } };
        var on  = new ExpenseReport(input) { Setup = PageSetup.Default with { Paper = "A5", Orientation = PageOrientation.Portrait, RepeatHead = true } };

        var pOff = off.GenerateImages(Small()).Count();
        var pOn  = on.GenerateImages(Small()).Count();
        Assert.True(pOff >= 2, "دادهٔ آزمون باید چند ورق شود");
        Assert.True(pOn >= pOff, $"تکرارِ سرستون نباید ورق کم کند (off={pOff}, on={pOn})");
        Assert.False(PageSetup.Default.RepeatHead, "پیش‌فرض باید خاموش باشد — همان خواستهٔ صاحب ریپو");
    }

    /// <summary>«جا دادن کلِ گزارش در یک ورق» واقعاً یک ورق می‌دهد — با شمردنِ ورق، نه حدس.</summary>
    [Fact]
    public void FitAllReallyLandsOnOnePage()
    {
        PdfEngine.Initialize();
        var input = new ExpenseReportInput("سنبله 1405", Rows(60), "1405/06/09", "—");
        IDocument Build(PageSetup s) => new ExpenseReport(input) { Setup = s };

        var s = PageSetup.Default with { Scale = PrintScale.FitPage, Orientation = PageOrientation.Portrait };
        var pct = ScaleSolver.Solve(s, Build);
        Assert.InRange(pct, 10, 99);                        // باید کوچک شده باشد

        var pages = Build(s.WithResolvedScale(pct)).GenerateImages(Small()).Count();
        Assert.Equal(1, pages);
    }

    /// <summary>«جا دادن در ۱×۲ ورق» — دو ورق، نه یکی، نه سه‌تا.</summary>
    [Fact]
    public void FitPagesHonoursTheRequestedCount()
    {
        PdfEngine.Initialize();
        var input = new ExpenseReportInput("سنبله 1405", Rows(110), "1405/06/09", "—");
        IDocument Build(PageSetup s) => new ExpenseReport(input) { Setup = s };

        var s = PageSetup.Default with
        {
            Scale = PrintScale.FitPages, FitWidthPages = 1, FitHeightPages = 2,
            Orientation = PageOrientation.Portrait,
        };
        var pct = ScaleSolver.Solve(s, Build);
        var pages = Build(s.WithResolvedScale(pct)).GenerateImages(Small()).Count();
        Assert.True(pages <= 2, $"باید در ۲ ورق جا شود، شد {pages}");
    }

    /// <summary>حالت‌هایی که «جا دادن» نیستند، دست نمی‌خورند.</summary>
    [Theory]
    [InlineData(PrintScale.None, 100, 100)]
    [InlineData(PrintScale.FitColumns, 100, 100)]
    [InlineData(PrintScale.Custom, 73, 73)]
    public void NonFitModesKeepTheirPercent(PrintScale mode, int pct, int want)
    {
        var s = PageSetup.Default with { Scale = mode, ScalePercent = pct };
        Assert.Equal(want, ScaleSolver.Solve(s, _ => throw new Exception("نباید سند بسازد")));
    }

    /// <summary>خاکستری و سیاه‌وسفید — همان ‎grayscale(1)‎ و ‎contrast(3.2)‎ی سایت.</summary>
    [Fact]
    public void ColourModesMapEveryColourThroughOneFunction()
    {
        // بیرونِ Compose، تنظیمِ جاری پیش‌فرض است: رنگی
        Assert.Equal("#e53e3e", DocStyle.Paint("#e53e3e"));

        var d = NoComments(Read("PumpYaqobi.Reporting", "Pdf", "DocStyle.cs"));
        // هیچ رنگی مستقیم روی ورق نمی‌نشیند — همه از Paint می‌گذرند
        foreach (var call in new[] { ".FontColor(HeadFg)", ".FontColor(CellFg)", ".Background(HeadBg)", ".Background(RowAlt)" })
            Assert.DoesNotContain(call, d);
        // ⚠️ از ۱۴۰۵/۰۷/۱۲ نوشته از ‎Ink‎ و خط از ‎Edge‎ می‌گذرد — هر دو روی
        // همان ‎Paint‎ ساخته شده‌اند و فقط در سیاه‌وسفید قاعدهٔ خودشان را دارند
        // (خطِ روشن سفید نشود، برچسبِ کم‌رنگ ناپدید نشود). رفتارش در
        // ‎SheetsWidthsPrintTests.SiyahVaSefid_…‎ سنجیده می‌شود.
        Assert.Contains("FontColor(Ink(", d);
        Assert.Contains("Background(Paint(", d);
        Assert.DoesNotContain("FontColor(Paint(", d);
        Assert.DoesNotContain("BorderColor(Paint(", d);
        Assert.Contains("return Paint(hex);", d);          // رنگی و خاکستری همان Paint
        // و خطِ خانه با «خطوطِ جدول» خاموش، هیچ
        Assert.Contains("Current.Gridlines ? Edge(hex) : Colors.Transparent", d);
    }

    /// <summary>پنجرهٔ «تنظیمِ ورق» چهار زبانه دارد — همان چهارتای سایت و اکسل.</summary>
    [Fact]
    public void ThePageSetupDialogHasTheFourExcelTabs()
    {
        var w = Read("PumpYaqobi.App", "Views", "PrintSetupWindow.axaml");
        Assert.Contains("<TabControl", w);
        foreach (var tab in new[] { "Header=\"ورق\"", "Header=\"حاشیه‌ها\"", "Header=\"سربرگ/پاورقی\"", "Header=\"جدول\"" })
            Assert.Contains(tab, w);

        // و هر چیزی که سایت داشت و این‌جا نبود
        foreach (var piece in new[]
        {
            "{Binding HeaderGap}", "{Binding FooterGap}",       // فاصلهٔ سربرگ/پاورقی
            "{Binding CenterH}", "{Binding CenterV}",           // وسط‌چین
            "{Binding FitW}", "{Binding FitH}",                 // جا دادن در N×M
            "{Binding RepeatHead}", "{Binding Gridlines}",      // زبانهٔ جدول
            "{Binding Colors}",                                 // رنگ
            "{Binding Presets}",                                // سربرگِ آماده
            "Name=\"Tokens\"",                                  // دکمه‌های کد
            "{Binding MapBody}",                                // نقشهٔ حاشیه
            "ResetCommand",                                     // برگرداندن به پیش‌فرض
        })
            Assert.Contains(piece, w);
    }

    /// <summary>ستونِ کناری همان شش حالتِ مقیاسِ سایت را دارد.</summary>
    [Fact]
    public void TheRailOffersAllSixScaleModes()
    {
        var p = Read("PumpYaqobi.App", "Printing", "DocumentPreview.cs");
        foreach (var v in new[] { "\"none\"", "\"fitCols\"", "\"fitRows\"", "\"fitAll\"", "\"custom\"", "\"fitPages\"" })
            Assert.Contains("new SetupOption(" + v, p);
        Assert.Contains("مقیاسِ اعمال‌شده:", p);   // همان ‎xpr-scaleinfo‎
    }

    /// <summary>دورِ ورق دیگر کادری نیست — سایت هم ندارد.</summary>
    [Fact]
    public void TheSheetHasNoFrame()
    {
        var d = NoComments(Read("PumpYaqobi.Reporting", "Pdf", "DocStyle.cs"));
        Assert.DoesNotContain(".Border(1).BorderColor(FootLine).Padding(10)", d);
    }

    private static QuestPDF.Infrastructure.ImageGenerationSettings Small() => new()
    {
        ImageFormat = QuestPDF.Infrastructure.ImageFormat.Png,
        RasterDpi = 36,
    };

    private static List<PumpYaqobi.Domain.Entities.Expense> Rows(int n)
    {
        var rows = new List<PumpYaqobi.Domain.Entities.Expense>();
        for (var i = 1; i <= n; i++)
            rows.Add(new PumpYaqobi.Domain.Entities.Expense
            {
                DateShamsi = "1405/06/" + ((i % 30) + 1).ToString("00"),
                Title = "مصرف شمارهٔ " + i,
                Amount = 1_000m * i,
            });
        return rows;
    }
    // ══ ورق‌های دلخواه — «از سه ورق فقط دومی، یا یک و سه و دومی نه» ══════════

    [Theory]
    [InlineData("2", 3, new[] { 2 })]
    [InlineData("1,3", 3, new[] { 1, 3 })]
    [InlineData("۱،۳", 3, new[] { 1, 3 })]            // رقم و ویرگولِ فارسی
    [InlineData("2-4", 6, new[] { 2, 3, 4 })]
    [InlineData("۴-۲", 6, new[] { 2, 3, 4 })]         // وارونه هم می‌شود
    [InlineData("1 , 3 ; 5", 6, new[] { 1, 3, 5 })]
    [InlineData("3,1,3", 6, new[] { 1, 3 })]          // هر ورق یک‌بار، به ترتیب
    [InlineData("-2", 5, new[] { 1, 2 })]             // «تا ۲»
    [InlineData("4-", 5, new[] { 4, 5 })]             // «از ۴ تا آخر»
    [InlineData("2 تا 4", 5, new[] { 2, 3, 4 })]
    [InlineData("9", 3, new int[0])]                  // ورقی که نیست
    [InlineData("abc,2", 3, new[] { 2 })]             // تکهٔ ناخوانا نادیده
    [InlineData("", 3, new int[0])]
    public void PagesText_IsParsedLikeExcelsPagesBox(string text, int count, int[] want)
        => Assert.Equal(want, PrintJob.ParsePages(text, count));

    [Fact] // حالتِ «ورق‌های دلخواه» همان فهرست را چاپ می‌کند و فایلِ اصلی نیست
    public void PagesMode_PicksExactlyThoseSheets()
    {
        var s = PageSetup.Default with { What = PrintWhat.Pages, PagesText = "1,3" };
        Assert.Equal(new[] { 1, 3 }, PrintJob.Picked(s, 3, 2));
        Assert.False(PrintJob.IsWholeDocument(PrintJob.Order(s, 3, 1), 3));
        var all = s with { PagesText = "1-3" };
        Assert.True(PrintJob.IsWholeDocument(PrintJob.Order(all, 3, 1), 3));
    }

    [Theory]
    [InlineData(new[] { 2 }, "2")]
    [InlineData(new[] { 1, 3 }, "1،3")]
    [InlineData(new[] { 1, 2, 3, 5 }, "1-3،5")]
    [InlineData(new[] { 1, 2 }, "1،2")]
    [InlineData(new int[0], "")]
    public void PagesText_IsWrittenBackShort(int[] pages, string want)
        => Assert.Equal(want, PrintJob.FormatPages(pages));

    [Fact] // تیک‌ها و کادرِ متن یک چیزند — رفتارش در ‎printshot‎ی UiTests سنجیده می‌شود
    //        (ویومدلِ پیش‌نمایش برای ساختنِ تصویر پلتفرمِ آوالونیا می‌خواهد). این‌جا
    //        فقط قفل می‌کنیم که کادر و تیک‌ها هر دو در ستونِ تنظیمات هستند.
    public void TheSheetTicksAndThePagesBoxAreInTheRail()
    {
        var v = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml"));
        Assert.Contains("{Binding PagesText}", v);
        Assert.Contains("{Binding PageChecks}", v);
        Assert.Contains("IsChecked=\"{Binding IsOn}\"", v);
        Assert.Contains("IsVisible=\"{Binding IsPages}\"", v);
        var vm = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Printing", "DocumentPreview.cs"));
        Assert.Contains("\"pages\"", vm);
        Assert.Contains("PrintJob.FormatPages(PageChecks", vm);
    }

}
