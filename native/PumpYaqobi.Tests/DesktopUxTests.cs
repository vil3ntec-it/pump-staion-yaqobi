using System.Text.RegularExpressions;
using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ گزارشِ بزرگِ صاحب ریپو دربارهٔ رفتارِ دسکتاپ ═══════════════════════════
///
/// تب با یک کلیک، کشویی با یک کلیک، سال و ماهِ واقعی، «ماه جدید»ِ واقعی،
/// ویرایشِ اکسلی، جهتِ فلش‌ها، فوکوس، اسکرول و کیو‌آرِ کارت‌ها.
///
/// این آزمون‌ها متنی‌اند چون چیزی که برمی‌گردد همین است: کسی یک ‎Setter‎ را
/// برمی‌دارد یا یک ‎[RelayCommand]‎ را ساده می‌کند و باگ بی‌سروصدا برمی‌گردد.
/// </summary>
public class DesktopUxTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string App(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root(), "PumpYaqobi.App" }.Concat(parts).ToArray()));

    private static string View(string n) => App("Views", "Sections", n + ".axaml");
    private static string NoComments(string s) => Regex.Replace(s, "<!--.*?-->", "", RegexOptions.Singleline);

    // ── تب با یک کلیک ───────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ ریشهٔ «کلیکِ اولِ تب هیچ کاری نمی‌کند»: ‎AsyncRelayCommand‎ی پیش‌فرض تا
    /// پایانِ یک اجرا ‎CanExecute‎ را ‎false‎ می‌کند، پس کلیک در فاصلهٔ بار شدنِ
    /// بخشِ قبلی بلعیده می‌شود.
    /// </summary>
    [Fact]
    public void SwitchingSectionNeverSwallowsAClick()
    {
        var vm = App("ViewModels", "MainViewModel.cs");
        var at = vm.IndexOf("public async Task GoAsync", StringComparison.Ordinal);
        Assert.True(at > 0, "GoAsync پیدا نشد");

        var attr = vm.LastIndexOf("[RelayCommand", at, StringComparison.Ordinal);
        Assert.True(attr > 0);
        Assert.Contains("AllowConcurrentExecutions = true", vm[attr..at]);
    }

    // ── کشویی با یک کلیک، بی تاخیر ──────────────────────────────────────────

    /// <summary>
    /// کشویی روی فازِ ‎Tunnel‎ باز می‌شود — یعنی پیش از آن‌که ‎DataGrid‎ کلیک را
    /// برای «انتخابِ خانه» مصرف کند.
    /// ⚠️ و بی هیچ ‎Dispatcher‎/تاخیری: خواستهٔ صریحِ صاحب ریپو «به هیچ عنوان با
    /// setTimeout یا delay مشکل را پنهان نکن».
    /// </summary>
    [Fact]
    public void ComboBoxesOpenOnTheFirstPressWithoutAnyDelay()
    {
        var g = App("Controls", "ExcelGrid.cs");
        Assert.Contains("AddHandler(PointerPressedEvent, OnPreviewPressed, RoutingStrategies.Tunnel)", g);
        Assert.Contains("cb.IsDropDownOpen = true", g);

        // هیچ تاخیری برای باز کردنِ کشویی نمانده باشد
        var at = g.IndexOf("private void OnPreviewPressed", StringComparison.Ordinal);
        Assert.True(at > 0);
        var body = g[at..(at + 1200)];
        Assert.DoesNotContain("Dispatcher", body);
        Assert.DoesNotContain("Task.Delay", body);
    }

    // ── فلش‌ها: همان‌جایی که چشم می‌بیند ────────────────────────────────────

    /// <summary>
    /// ⚠️ این آزمون برعکسِ نسخهٔ پیشینِ خودش است، چون خودِ قاعده عوض شد.
    ///
    /// ایندکسِ خام («‎ArrowRight → column + 1‎») در جدولِ راست‌به‌چپ همان
    /// شکایتی را ساخت که قرار بود درست کند: ستونِ «بعدی» سمتِ چپ است، پس
    /// کلیدِ راست، چپ می‌رفت. صاحب ریپو دوباره همان را گزارش کرد.
    ///
    /// قاعدهٔ تازه: ‎→‎ خانهٔ سمتِ راست، ‎←‎ خانهٔ سمتِ چپ — و جهتِ چیدمان از
    /// **جای واقعیِ سربرگ‌ها** خوانده می‌شود، نه از خاصیتی که ممکن است به
    /// کنترل نرسد.
    /// </summary>
    [Fact]
    public void ArrowKeysFollowTheScreenNotTheRawIndex()
    {
        var g = App("Controls", "ExcelGrid.cs");
        Assert.Contains("MoveColumn(e.Key == Key.Right ? -1 : +1, shift)", g);
        Assert.DoesNotContain("MoveColumn(e.Key == Key.Right ? +1 : -1", g);
        Assert.DoesNotContain("ColumnsRunRightToLeft", g);
    }

    // ── اسکرول و سربرگ ──────────────────────────────────────────────────────

    /// <summary>
    /// «فقط Grid باید Scroll شود، نه کلِ Layout» و «Header با فوکوس جابه‌جا
    /// نشود». درخواستِ ‎BringIntoView‎ باید در مرزِ جدول بایستد — همیشه، نه
    /// فقط وقتی از کلیک آمده.
    /// </summary>
    [Fact]
    public void BringIntoViewNeverEscapesTheGrid()
    {
        var g = App("Controls", "ExcelGrid.cs");
        Assert.Contains("RequestBringIntoViewEvent", g);
        Assert.Contains("ev.Handled = true", g);
        Assert.DoesNotContain("_pointerDriven", g);   // شرطِ قبلی برداشته شد
    }

    /// <summary>کلیکِ بیرون، ویرایش را تمام کند — ولی کشویی/پاپ‌آپ «بیرون» نیست.</summary>
    [Fact]
    public void ClickingOutsideEndsEditingButPopupsDoNotCount()
    {
        var g = App("Controls", "ExcelGrid.cs");
        Assert.Contains("OnOutsidePressed", g);
        Assert.Contains("is Popup or FlyoutPresenter", g);
        Assert.Contains("CommitEdit(DataGridEditingUnit.Cell, true)", g);
    }

    // ── سال و ماه ───────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ این آزمون هم عوض شد، و برعکسِ نسخهٔ پیشینِ خودش است.
    ///
    /// یک‌بار «هر دوازده ماه، حتی ماهی که ردیفی ندارد» قفل شده بود. عکسِ خودِ
    /// سایت خلافش را نشان داد: کشوی ماه فقط ماه‌هایی را دارد که داده دارند،
    /// به‌علاوهٔ یک گزینهٔ «همهٔ ماه‌های ‎<سال>‎». ماهِ تازه با دکمهٔ «ماه جدید»
    /// باز می‌شود، نه با پر کردنِ کشویی.
    /// </summary>
    [Fact]
    public void TheMonthListHoldsOnlyMonthsThatExist()
    {
        var p = new YearMonthPicker(_ => { }, "همهٔ ماه‌ها");
        p.Load(new[] { "1405/06", "1405/07" }, "1405/07");

        var keys = p.Months.Select(m => m.Key).ToList();
        Assert.Equal(new[] { "1405/*", "1405/07", "1405/06" }, keys);

        // برچسب مثلِ سایت: «میزان — 1405/07»
        Assert.Equal("میزان — 1405/07", p.Months[1].Label);
        Assert.Equal("همهٔ ماه‌های 1405", p.Months[0].Label);
    }

    /// <summary>«📆 همهٔ سال‌ها» هم هست، بالای سال‌هایی که داده دارند.</summary>
    [Fact]
    public void TheYearListHasAnAllOption()
    {
        var p = new YearMonthPicker(_ => { }, "همهٔ ماه‌ها");
        p.Load(new[] { "1403/01", "1405/06" }, "1405/06");

        var years = p.Years.Select(y => y.Key).ToList();
        Assert.Equal(new[] { "", "1405", "1403" }, years);
        Assert.Equal("📆 همهٔ سال‌ها", p.Years[0].Label);
    }

    /// <summary>دادهٔ خیلی قدیمی هم سرِ جایش است.</summary>
    [Fact]
    public void AnOldMonthIsStillReachable()
    {
        var p = new YearMonthPicker(_ => { });
        p.Load(new[] { "1390/04", "1405/06" }, "1390/04");
        Assert.Equal("1390", p.Year!.Key);
        Assert.Contains(p.Months, m => m.Key == "1390/04");
    }

    // ── «ماه جدید» ──────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ این آزمون عوض شد، چون خودِ قاعده عوض شد.
    ///
    /// یک‌بار این‌جا دیالوگِ «سال و ماه را انتخاب کن» قفل شده بود. صاحب ریپو با
    /// عکسِ خودِ سایت نشان داد که چنین دیالوگی وجود ندارد: دکمه بی هیچ پرسشی
    /// ماهِ **بعدیِ آخرین ماه** را باز می‌کند و پیام می‌دهد. حالا همان است
    /// (‎addExpenseMonth()‎، خطِ ۳۷۷۷۳ی index.html).
    /// </summary>
    [Fact]
    public void NewMonthOpensTheNextMonthLikeTheSite()
    {
        var led = App("ViewModels", "LedgerSectionViewModel.cs");
        Assert.Contains("mo++;", led);
        Assert.Contains("if (mo > 12) { mo = 1; yr++; }", led);
        Assert.Contains("e.DateShamsi = next + \"/01\";", led);
        Assert.Contains("✅ جدول ماه ", led);

        // و ماهِ تکراری دوباره ساخته نمی‌شود
        Assert.Contains("if (!Months.Contains(next))", led);

        // دیالوگِ ساختگی رفت
        Assert.DoesNotContain("Dialogs.PickMonthAsync", led);
    }

    // ── کیو‌آرِ کارت‌ها ───────────────────────────────────────────────────────

    /// <summary>
    /// «QR نصفه دیده می‌شود.» ریشه: خانهٔ هم‌اندازه از محتوا کوتاه‌تر بود و
    /// ردیفِ آخرِ کارت — همان کیو‌آر — بریده می‌شد. حالا کششِ اضافه بالای آن
    /// جذب می‌شود و ردیفِ کیو‌آر ته کارت، کامل، سرِ جایش است.
    /// </summary>
    [Fact]
    public void TheQrRowIsNeverClipped()
    {
        var v = NoComments(View("DebtSectionView"));
        // ⚠️ ردیفِ ‎*‎ باید **خالی** باشد، نه ردیفِ محتوا: ردیفِ ستاره‌دارِ
        // محتوادار وقتی جا کم بیاید تا صفر جمع می‌شود و محتوایش ناپدید —
        // همان «دیزل و پطرول زیرِ کادر گم شدند».
        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto,Auto,Auto,*,Auto\"", v);

        var m = Regex.Match(v, "MinItemHeight=\"(\\d+)\"");
        Assert.True(m.Success);
        Assert.True(int.Parse(m.Groups[1].Value) >= 224,
            "خانهٔ کارت از محتوای کارت کوتاه‌تر است و ردیفِ کیو‌آر بریده می‌شود");
    }

    /// <summary>و دکمهٔ کیو‌آرِ کارت واقعاً کاری بکند — تا امروز هیچ فرمانی نداشت.</summary>
    [Fact]
    public void TheCardQrButtonIsWired()
    {
        var v = NoComments(View("DebtSectionView"));
        Assert.Contains("ShowCardQrCommand", v);
    }
}
