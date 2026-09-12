using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ همان راه‌هایی که سایت دارد ═════════════════════════════════════════════
///
/// چهار گزارشِ صاحب ریپو، همه با عکسِ خودِ سایت:
///   • «تا قرض‌دار اسمش را نوشت» — کادرِ نام، نه کادرهای بالای صفحه؛
///   • «تا ورق ساخته بشه و بتونم روز رو خودم انتخاب کنم»؛
///   • «ورقی که برای فردا ساختم، فردا پارچه که پر شد باید تو همون بره»؛
///   • «اسم‌های کادر سرِ کادرهایشان سمتِ راست‌اند».
/// </summary>
public class SiteFlowParityTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] p) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(p).ToArray()));

    // ── ورق با تاریخ ─────────────────────────────────────────────────────────

    /// <summary>دکمهٔ ورق تاریخ می‌پرسد — نه این‌که همیشه ورقِ امروز را باز کند.</summary>
    [Fact]
    public void TheNewSheetButtonAsksForTheDate()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "WaraqSectionViewModel.cs");
        Assert.Contains("Dialogs.PickWaraqDateAsync", vm);
        Assert.DoesNotContain("OpenOrCreateAsync(Shamsi.Today()", vm);
    }

    /// <summary>پنجره‌اش همان چیزی است که سایت دارد.</summary>
    [Fact]
    public void TheDateWindowHasTheSameControlsAsTheSite()
    {
        var x = Read("PumpYaqobi.App", "Views", "WaraqDateWindow.axaml");
        foreach (var t in new[] { "▲ روز بعد", "▼ روز قبل", "‹ ماه", "امروز", "ماه ›", "⌨️ نوشتن", "✔ باز کردن" })
            Assert.Contains(t, x);

        var cs = Read("PumpYaqobi.App", "Views", "WaraqDateWindow.axaml.cs");
        // و پیش از تایید می‌گوید تازه است یا از قبل هست — همان دو جملهٔ سایت
        Assert.Contains("📂 ورقِ این تاریخ از قبل هست", cs);
        Assert.Contains("🆕 ورقِ تازه برای این تاریخ ساخته می‌شود", cs);
    }

    /// <summary>
    /// ⚠️ مهم‌ترین بند: ورقِ فردا همان ورقی است که فردا پارچه در آن می‌نشیند.
    ///
    /// هر دو مسیر — ساختنِ دستیِ ورق و همگام‌سازیِ پارچه — با **کلیدِ تاریخ**
    /// می‌گردند، پس به یک ورق می‌رسند و ورقِ تکراری ساخته نمی‌شود.
    /// </summary>
    [Fact]
    public void OneSheetPerDayNoMatterWhoCreatesIt()
    {
        var data = Read("PumpYaqobi.Services", "Data", "WaraqDataService.cs");
        Assert.Contains("var key = Shamsi.Key(dateShamsi);", data);
        Assert.Contains("FirstOrDefaultAsync(w => w.DateKey == key", data);

        var sync = Read("PumpYaqobi.Services", "Data", "ShiftWaraqSyncService.cs");
        Assert.Contains("var key = Shamsi.Key(date);", sync);
        Assert.Contains("FirstOrDefaultAsync(x => x.DateKey == key", sync);
    }

    // ── کادرِ نامِ قرض‌دار و شرکت ─────────────────────────────────────────────

    /// <summary>«➕ افزودن شخص» کادرِ نام باز می‌کند، مثلِ ‎addPersonModal‎ی سایت.</summary>
    [Fact]
    public void AddingADebtorAsksForTheNameInADialog()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "DebtSectionViewModel.cs");
        Assert.Contains("Dialogs.PromptAsync(\"افزودن قرض‌دار\"", vm);
        // و حسابِ تکراری ساخته نمی‌شود — همان حسابِ قبلی باز می‌شود
        Assert.Contains("این حساب از قبل وجود دارد — همان حساب باز شد", vm);
    }

    /// <summary>و کادرهای «حسابِ تازه» از بالای صفحه رفتند — سایت ندارد.</summary>
    [Fact]
    public void TheDebtorPageHasNoInlineNewAccountBoxes()
    {
        var v = Read("PumpYaqobi.App", "Views", "Sections", "DebtSectionView.axaml");
        Assert.DoesNotContain("Text=\"{Binding NewName}\"", v);
        Assert.DoesNotContain("Text=\"{Binding NewPhone}\"", v);
    }

    /// <summary>شرکت‌ها هم همان کادر را دارد.</summary>
    [Fact]
    public void AddingACompanyAsksForTheNameInADialog()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "CompanySectionViewModel.cs");
        Assert.Contains("Dialogs.PromptAsync(\"افزودن شرکت تیل\"", vm);
    }

    // ── برچسبِ کادرهای فرم ───────────────────────────────────────────────────

    /// <summary>
    /// «اسم‌های کادر سرِ کادرهایشان سمتِ راست‌اند.» در سایت ‎.field-label‎ هیچ
    /// ‎text-align‎ی ندارد، پس در چیدمانِ راست‌به‌چپ سرِ خط می‌نشیند.
    ///
    /// ⚠️ قاعدهٔ «هر نوشته وسط» — که برای **جدول‌ها** خواسته شده بود — دست
    /// نخورد؛ فقط برچسبِ فرم‌ها از آن بیرون آمد.
    /// </summary>
    [Fact]
    public void FormLabelsSitAtTheStartNotCentred()
    {
        var t = Read("PumpYaqobi.App", "Themes", "Controls.axaml");

        var fld = Slice(t, "<Style Selector=\"TextBlock.fld\">", "</Style>");
        Assert.Contains("Value=\"Start\"", fld);

        // ⚠️ ‎.label‎ عمداً وسط ماند: همان کلاس در کادرهای خلاصه و سربرگ‌ها هم
        // هست و سرِ خط کردنش گزارشِ «سربرگ‌ها خراب شدند» را ساخت.
        var lbl = Slice(t, "<Style Selector=\"TextBlock.label\">", "</Style>");
        Assert.DoesNotContain("Value=\"Start\"", lbl);

        // و خودِ جدول‌ها هنوز وسط‌چین‌اند
        var cell = Slice(t, "<Style Selector=\"DataGridCell\">", "</Style>");
        Assert.Contains("HorizontalContentAlignment\" Value=\"Center\"", cell);
    }

    // ── کشوییِ داخلِ جدول ─────────────────────────────────────────────────────

    /// <summary>
    /// یک کلیک باز کند و همان کلیک را ‎DataGrid‎ دوباره مصرف نکند — وگرنه
    /// کشویی همان لحظه بسته می‌شود و کاربر می‌گوید «راحت کار نمی‌کند».
    /// </summary>
    [Fact]
    public void ACellComboOpensOnOneClickAndTheGridDoesNotEatIt()
    {
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        Assert.Contains("cb.IsDropDownOpen = true;", g);
        Assert.Contains("e.Handled = true;", g);
        Assert.Contains("RoutingStrategies.Tunnel", g);
        // بی هیچ تاخیری
        var at = g.IndexOf("private void OnPreviewPressed", StringComparison.Ordinal);
        Assert.True(at > 0);
        var body = g[at..(at + 1200)];
        Assert.DoesNotContain("Dispatcher", body);
        Assert.DoesNotContain("Task.Delay", body);
    }

    /// <summary>
    /// ‎Tab‎/‎Enter‎ روی همان خانه هم مقدار را عوض می‌کند — حتی حالا که کشویی
    /// همیشه‌پیداست و ‎CellEditingTemplate‎ ندارد.
    /// </summary>
    [Fact]
    public void TabStillTogglesAnAlwaysVisibleCombo()
    {
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        Assert.Contains("private Control? CellPicker(DataGridColumn? col)", g);
        Assert.Contains("var target = CellPicker(CurrentColumn) ?? Focused;", g);
        Assert.Contains("t.CellEditingTemplate is not null || CellPicker(col) is not null", g);
    }

    // ── پنجره‌های گفت‌وگو ────────────────────────────────────────────────────

    /// <summary>
    /// ══ چرا «Object reference not set» می‌آمد ═══════════════════════════════
    ///
    /// فیلدهای ‎x:Name‎ را کدِ تولیدشدهٔ ‎InitializeComponent‎ پر می‌کند. این سه
    /// پنجره به‌جای آن مستقیم ‎AvaloniaXamlLoader.Load(this)‎ را صدا می‌زدند:
    /// XAML بار می‌شد ولی فیلدها ‎null‎ می‌ماندند، و اولین دست زدن به آن‌ها
    /// همان پیام را می‌داد — یعنی افزودنِ شخص، ورق، حسابِ فرعی، جدولِ جدید،
    /// آرشیو و کیو‌آر، همه.
    ///
    /// ⚠️ هر پنجره‌ای که فیلدِ ‎x:Name‎ را مستقیم صدا می‌زند باید
    /// ‎InitializeComponent()‎ داشته باشد. (خواندن با ‎FindControl‎ فرق دارد و
    /// با ‎Load‎ هم کار می‌کند — همان کاری که ‎PersonView‎ می‌کند.)
    /// </summary>
    [Theory]
    [InlineData("DialogWindow")]
    [InlineData("QrWindow")]
    [InlineData("WaraqDateWindow")]
    public void WindowsWithNamedFieldsCallInitializeComponent(string name)
    {
        var cs = Read("PumpYaqobi.App", "Views", name + ".axaml.cs");
        Assert.Contains("InitializeComponent()", cs);
        Assert.DoesNotContain("AvaloniaXamlLoader.Load(this)", cs);
    }

    // ── ستونِ «#» ────────────────────────────────────────────────────────────

    /// <summary>
    /// «چرا هیچ جدولی شماره ندارد؟» — در سایت ستونِ اولِ هر جدول ‎#‎ است.
    /// حالا سرستونِ ردیفِ خودِ جدول همان را نشان می‌دهد، برای **همهٔ** جدول‌ها
    /// با هم (نه چهل ویومدل که هر کدام خاصیتِ شماره بگیرند).
    /// </summary>
    [Fact]
    public void EveryTableShowsRowNumbers()
    {
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        Assert.Contains("HeadersVisibility = DataGridHeadersVisibility.All;", g);
        Assert.Contains("e.Row.Header = e.Row.GetIndex() + 1;", g);

        // و نوارِ «جمله» پهنای همین ستون را حساب می‌کند تا جا‌به‌جا نشود
        var t = Read("PumpYaqobi.App", "Controls", "TotalsBar.cs");
        Assert.Contains("var acc = RowHeaderWidth(grid);", t);
    }

    /// <summary>سرستونِ ستون‌ها واقعاً وسط بنشیند — نوشته‌اش داخلِ قالب ساخته می‌شود.</summary>
    [Fact]
    public void ColumnHeadersAreCentredInsideTheirTemplate()
    {
        var t = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        Assert.Contains("<Style Selector=\"DataGridColumnHeader /template/ ContentPresenter\">", t);
        Assert.Contains("<Style Selector=\"DataGridColumnHeader /template/ TextBlock\">", t);
    }

    private static string Slice(string s, string from, string to)
    {
        var i = s.IndexOf(from, StringComparison.Ordinal);
        Assert.True(i >= 0, "یافت نشد: " + from);
        var j = s.IndexOf(to, i, StringComparison.Ordinal);
        Assert.True(j > i);
        return s[i..j];
    }
}
