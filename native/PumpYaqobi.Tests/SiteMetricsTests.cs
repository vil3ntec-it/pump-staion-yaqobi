using System.Text.RegularExpressions;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ اندازه‌ها همان اندازه‌های خودِ سایت بمانند ═══════════════════════════════
///
/// این عددها حدس نیستند. با یک کرومیومِ واقعی، ‎index.html‎ در پنجرهٔ ۱۴۴۰
/// پیکسلی باز شد و مقدارِ <b>محاسبه‌شدهٔ</b> خودِ مرورگر برای هر عنصر خوانده
/// شد (‎getComputedStyle‎ + ‎getBoundingClientRect‎):
///
///   .header        ۷۵ پیکسل بلند · padding 12/20 · عنوان ۲۱٫۱ وزنِ ۹۰۰
///   .top-banner    ۷۴ پیکسل بلند · padding 8/20 · برچسب ۱۶/۸۰۰ · عدد ۱۶٫۸/۸۰۰
///   .nav           ۶۰ پیکسل بلند · padding 8/12
///   .nav-btn       padding 8/16 · شعاعِ ۱۱ · فونتِ ۱۶ · وزنِ ۸۰۰
///   .xls-tbl       فونتِ ۱۷٫۲۸ · th با padding 9/8 و قدِ ۴۴ · قدِ ردیف ۴۴
///   .stat-value    ۲۴٫۸ · .stat-label ۱۶ وزنِ ۸۰۰
///
/// چرا آزمون: خواندنِ CSS جواب نمی‌دهد — فایل ۴ مگابایتی است و همان قاعده‌ها
/// چند بار بازنویسی می‌شوند. مثلاً در CSS نوشته ‎.card{border-radius:14px}‎
/// ولی مقدارِ واقعی ۱۸ است، و ‎.nav-btn{border-radius:7px}‎ در عمل ۱۱ می‌شود.
/// اگر روزی کسی این عددها را در ‎Controls.axaml‎ عوض کند، این‌جا قرمز می‌شود.
/// </summary>
public class SiteMetricsTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Controls() =>
        File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Themes", "Controls.axaml"));

    private static string MainWindow() =>
        File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Views", "MainWindow.axaml"));

    /// <summary>مقدارِ یک ‎Setter‎ داخلِ سبکی که با ‎selector‎ شروع می‌شود.</summary>
    private static string? Setter(string xaml, string selector, string property)
    {
        var i = xaml.IndexOf($"Selector=\"{selector}\"", StringComparison.Ordinal);
        if (i < 0) return null;
        var end = xaml.IndexOf("</Style>", i, StringComparison.Ordinal);
        if (end < 0) return null;
        var m = Regex.Match(xaml[i..end], $"Property=\"{property}\"\\s+Value=\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    // ══ جدول‌ها ══
    [Fact]
    public void Table_row_height_matches_the_site()
        => Assert.Equal("44", Setter(Controls(), "DataGrid", "RowHeight"));

    [Fact]
    public void Table_cell_font_matches_the_site()
        => Assert.Equal("17", Setter(Controls(), "DataGridCell", "FontSize"));

    [Fact]
    public void Table_header_font_and_padding_match_the_site()
    {
        var c = Controls();
        Assert.Equal("17", Setter(c, "DataGridColumnHeader", "FontSize"));
        Assert.Equal("8,9", Setter(c, "DataGridColumnHeader", "Padding"));
        Assert.Equal("44", Setter(c, "DataGridColumnHeader", "MinHeight"));
    }

    // ══ نوارِ ناوبری ══
    [Fact]
    public void Nav_button_matches_the_site()
    {
        var c = Controls();
        Assert.Equal("16,8", Setter(c, "Button.nav", "Padding"));
        Assert.Equal("11", Setter(c, "Button.nav", "CornerRadius"));
        Assert.Equal("ExtraBold", Setter(c, "Button.nav", "FontWeight"));
    }

    [Fact]
    public void Nav_button_font_is_sixteen()
        => Assert.Contains("FontSize=\"16\"", MainWindow());

    // ══ کادرِ عددِ خلاصه ══
    [Fact]
    public void Stat_box_matches_the_site()
    {
        var c = Controls();
        Assert.Equal("10", Setter(c, "Border.stat", "CornerRadius"));
        Assert.Equal("14", Setter(c, "Border.stat", "Padding"));
        Assert.Equal("86", Setter(c, "Border.stat", "MinHeight"));
    }

    [Fact]
    public void Stat_value_font_matches_the_site()
        => Assert.Equal("24", Setter(Controls(), "TextBlock.num", "FontSize"));

    // ══ سربرگ و نوارِ خبر ══
    [Fact]
    public void Header_padding_and_title_match_the_site()
    {
        var w = MainWindow();
        Assert.Contains("Padding=\"20,12\"", w);      // ‎.header‎ → ۷۵ پیکسل بلند
        Assert.Contains("FontSize=\"21\"", w);        // عنوان
        Assert.Contains("FontWeight=\"Black\"", w);   // وزنِ ۹۰۰
    }

    [Fact]
    public void Banner_is_two_lines_at_the_sites_sizes()
    {
        var w = MainWindow();
        Assert.Contains("Padding=\"20,8\"", w);       // ‎.top-banner‎
        Assert.Contains("FontSize=\"16.8\"", w);      // ‎.tb-value‎
        Assert.Contains("MinWidth=\"140\"", w);       // ‎.tb-item{min-width:140px}‎
    }

    /// <summary>
    /// سربرگ باید دکمهٔ قفل و نشانِ نقش داشته باشد — هر دو در نسخهٔ وب هستند و
    /// در نیتیو نبودند. ‎SignOut‎ ساخته شده بود ولی هیچ دکمه‌ای صدایش نمی‌زد.
    /// </summary>
    [Fact]
    public void Header_has_the_lock_button_and_role_badge()
    {
        var w = MainWindow();
        Assert.Contains("SignOutCommand", w);
        Assert.Contains("RoleText", w);
        Assert.Contains("Classes=\"pill\"", w);
    }

    /// <summary>
    /// ستون‌ها باید به اندازهٔ محتوا باشند، نه پهنای دستی. جدولِ HTML چیدمانِ
    /// خودکار دارد؛ وقتی نیتیو پهنای ثابت داشت، با رسیدن به فونتِ واقعیِ سایت
    /// سرِ ستون‌ها بریده شد («رسید به صرافی» شد «مید به صرافی»).
    /// </summary>
    [Fact]
    public void Grid_columns_size_to_their_content()
    {
        var dir = Path.Combine(Root, "PumpYaqobi.App", "Views", "Sections");
        var offenders = new List<string>();
        foreach (var f in Directory.GetFiles(dir, "*.axaml"))
        {
            var s = File.ReadAllText(f);
            var i = s.IndexOf("<DataGrid.Columns>", StringComparison.Ordinal);
            if (i < 0) continue;
            var end = s.IndexOf("</DataGrid.Columns>", i, StringComparison.Ordinal);
            var body = s[i..(end < 0 ? s.Length : end)];
            var fixedW = Regex.Matches(body, "Width=\"\\d+\"").Count;
            if (fixedW > 0) offenders.Add($"{Path.GetFileName(f)}: {fixedW}");
        }
        Assert.True(offenders.Count == 0,
            "ستونِ با پهنای ثابت مانده: " + string.Join(" · ", offenders));
    }
}
