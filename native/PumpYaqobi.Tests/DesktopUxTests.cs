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
        var at = g.IndexOf("private static void OnPreviewPressed", StringComparison.Ordinal);
        Assert.True(at > 0);
        var body = g[at..(at + 700)];
        Assert.DoesNotContain("Dispatcher", body);
        Assert.DoesNotContain("Task.Delay", body);
    }

    // ── فلش‌ها: ایندکسِ ستون، مستقل از جهت ──────────────────────────────────

    /// <summary>
    /// خواستهٔ نوشتهٔ صاحب ریپو: «منطقِ Cell Index باید مستقل از Direction
    /// باشد — ArrowRight → column + 1، ArrowLeft → column − 1.»
    /// </summary>
    [Fact]
    public void ArrowKeysUseColumnIndexNotScreenDirection()
    {
        var g = App("Controls", "ExcelGrid.cs");
        Assert.Contains("MoveColumn(e.Key == Key.Right ? +1 : -1, shift)", g);

        // دیگر به FlowDirection بند نیست
        var at = g.IndexOf("case Key.Left:", StringComparison.Ordinal);
        Assert.True(at > 0);
        Assert.DoesNotContain("FlowDirection", g[at..(at + 400)]);
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

    /// <summary>هر دوازده ماه، حتی ماهی که هنوز ردیفی ندارد.</summary>
    [Fact]
    public void EveryOneOfTheTwelveMonthsCanBePicked()
    {
        var p = new YearMonthPicker(_ => { });
        p.Load(new[] { "1405/06" }, "1405/06");

        var months = p.Months.Select(m => YearMonthPicker.MonthOf(m.Key)).ToList();
        Assert.Equal(12, months.Count);
        for (var m = 1; m <= 12; m++)
            Assert.Contains(m.ToString("00"), months);
    }

    /// <summary>و بیش از یک سال — دستِ‌کم امسال و سالِ بعد.</summary>
    [Fact]
    public void MoreThanOneYearIsOffered()
    {
        var p = new YearMonthPicker(_ => { });
        p.Load(new[] { "1403/01", "1405/06" }, "1405/06");

        var years = p.Years.Select(y => y.Key).Where(k => k.Length > 0).ToList();
        Assert.True(years.Count >= 3, "سال‌های ارائه‌شده: " + string.Join(",", years));
        Assert.Contains("1403", years);
        Assert.Contains("1405", years);
    }

    /// <summary>دادهٔ خیلی قدیمی که بیرونِ بازه افتاده هم از دست نرود.</summary>
    [Fact]
    public void AnOldMonthOutsideTheRangeIsStillReachable()
    {
        var p = new YearMonthPicker(_ => { });
        p.Load(new[] { "1390/04", "1405/06" }, "1390/04");
        Assert.Equal("1390", p.Year!.Key);
        Assert.Contains(p.Months, m => m.Key == "1390/04");
    }

    // ── «ماه جدید» ──────────────────────────────────────────────────────────

    /// <summary>
    /// «کلیک → دیالوگِ انتخابِ سال و ماه → تأیید → ایجاد → فعال شدن».
    /// و ماهِ تکراری ساخته نشود.
    /// </summary>
    [Fact]
    public void NewMonthAsksBeforeCreatingAndRefusesDuplicates()
    {
        var led = App("ViewModels", "LedgerSectionViewModel.cs");
        Assert.Contains("Dialogs.PickMonthAsync", led);
        Assert.Contains("if (Months.Contains(pick))", led);

        var dlg = App("Views", "MonthPickWindow.axaml.cs");
        Assert.Contains("OkBtn.IsEnabled = k is not null && !dupe", dlg);
        for (var m = 1; m <= 12; m++) { }                       // هر دوازده ماه:
        Assert.Contains("for (var m = 1; m <= 12; m++)", dlg);
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
        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto,Auto,*,Auto\"", v);

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
