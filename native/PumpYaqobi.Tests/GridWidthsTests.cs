namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پهنای ستون‌ها و جهتِ کلیدها ═════════════════════════════════════════════
///
/// سه گزارشِ جدا در ۱۴۰۵/۰۷/۱۲ و هر سه در همین یک کنترل:
///
///   «بخش قرض‌داران جدول‌هاشو جوری که خاستم اندازه کردم، ولی وقتی از حساب
///    بیرون می‌شم و میام دوباره همون مدلِ اول شده.»
///   «بالای ورق… توی بعضی کامپیوترها خیلی بزرگ است و از کادر زده بیرون.»
///   «خواستم ماه رو عوض کنم… تمام سربرگ‌ها باگ خوردن و رفتن کنجِ سربرگشون.»
///   «توی یک کادرِ جدول می‌نویسم و می‌خوام برم کادرِ بعدی: راست به چپ می‌ره،
///    بعد با کلیکِ دوم درست می‌شه.»
///
/// ⚠️ این بندها روی **سورس** می‌گردند و این عمدی است: هر چهارتا رفتارِ
/// چیدمان‌اند و بازسازیِ درستشان یک پنجرهٔ واقعی می‌خواهد (‎PumpYaqobi.UiTests‎).
/// آن‌چه این‌جا قفل می‌شود، همان چیزی است که با یک ویرایشِ بی‌خبر برمی‌گردد.
/// </summary>
public class GridWidthsTests
{
    private static readonly string App =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                                      "PumpYaqobi.App"));

    private static string Grid() => File.ReadAllText(Path.Combine(App, "Controls", "ExcelGrid.cs"));

    /// <summary>
    /// ⛔ تا ۳.۱.۱۵۷ فقط **سه** جدول ‎WidthKey‎ داشتند (هر سه در ورق) و
    /// ‎RememberWidths‎ نخستین خطش «کلید نداری؟ برگرد» بود — یعنی کشیدنِ ستون
    /// در هر جای دیگرِ برنامه هیچ‌وقت ذخیره نمی‌شد.
    /// </summary>
    [Fact]
    public void Har_Jadvali_Pahnayash_Ra_Zakhire_Mikonad()
    {
        var g = Grid();
        Assert.Contains("private string? EffectiveKey(int visible)", g);

        //  ⛔ «کلید نداری؟ برگرد» دیگر اولین خطِ ذخیره نیست.
        var at = g.IndexOf("private void RememberWidths()", StringComparison.Ordinal);
        Assert.True(at > 0, "RememberWidths پیدا نشد");
        var body = g[at..Math.Min(g.Length, at + 700)];
        Assert.DoesNotContain("if (string.IsNullOrWhiteSpace(WidthKey)) return;", body);
        Assert.Contains("EffectiveKey(cols.Count)", body);

        //  و کلیدِ خودکار چیدمانِ ستون‌ها را هم در خود دارد: جدولِ حسابِ
        //  قرض‌دار در دفترِ تیل و دفترِ پول ستون‌های متفاوتی نشان می‌دهد.
        Assert.Contains("+ \"#\" + visible", g);
    }

    /// <summary>
    /// ⛔ بارِ خودکار از قاب بیرون نمی‌زند — ولی کشیدنِ دستیِ ستون همچنان
    /// اسکرولِ افقی می‌دهد (خواستهٔ ۱۴۰۵/۰۶/۲۷، دست‌نخورده).
    /// </summary>
    [Fact]
    public void Bare_Khodkar_Az_Ghab_Birun_Nemizanad()
    {
        var g = Grid();
        Assert.Contains("private static double[] FitToRoom(", g);
        Assert.Contains("if (!spare) natural = FitToRoom(natural, room);", g);

        //  ⚠️ و ‎PinOnUserResize‎ — همان جایی که کشیدنِ کاربر را پیکسلی
        //  می‌کند — دست نخورده است.
        Assert.Contains("private void PinOnUserResize()", g);
    }

    /// <summary>
    /// ⛔ نخستین ‎SpreadColumns‎ پس از پر شدنِ دوبارهٔ فهرست یک پاس صبر
    /// می‌کند، وگرنه پهنای کهنه برای همیشه سفت می‌شود («رفتن کنجِ سربرگ»).
    /// </summary>
    [Fact]
    public void Pas_Az_Por_Shodane_Dobare_Yek_Pas_Sabr_Mishavad()
    {
        var g = Grid();
        Assert.Contains("private bool _freshCols;", g);
        Assert.Contains("_freshCols = true;", g);
        Assert.Contains("if (_freshCols) { _freshCols = false; InvalidateMeasure(); return; }", g);

        //  ترتیب: نگهبان باید **پیش از** خواندنِ ‎ActualWidth‎ باشد، وگرنه
        //  همان عددِ کهنه خوانده می‌شود و این بند بی‌معنا است.
        var at = g.IndexOf("private void SpreadColumns()", StringComparison.Ordinal);
        var guard = g.IndexOf("if (_freshCols)", at, StringComparison.Ordinal);
        var read = g.IndexOf("cols.Select(c => c.ActualWidth)", at, StringComparison.Ordinal);
        Assert.True(guard > at && read > guard, "نگهبان پس از خواندنِ پهنا است");
    }

    //  ⚠️ جهتِ چپ/راست این‌جا سنجیده **نمی‌شود** و نباید بشود:
    //  ‎KeyboardAndZeroTests.ArrowKeysFollowWhatTheEyeSees‎ از پیش قفلش
    //  کرده و نسخهٔ اولِ همین کار با یک «تمیزکاریِ» بی‌ضرر شکستش. دو
    //  نگهبان برای یک قاعده یعنی روزی یکی‌شان اجازهٔ چیزی را می‌دهد که
    //  آن یکی قدغن کرده.

    /// <summary>
    /// ⛔ «با کلیکِ دوم درست می‌شود»: در حالتِ نوشتن، ستونِ مبدأ باید
    /// <b>پیش از</b> <c>CommitEdit</c> برداشته شود — بستنِ ویرایش خودش
    /// می‌تواند خانهٔ جاری را جابه‌جا کند.
    /// </summary>
    [Fact]
    public void Dar_Halate_Neveshtan_Mabda_Pish_Az_Commit_Bardashte_Mishavad()
    {
        var g = Grid();
        var at = g.IndexOf("private void OnPreviewKey(", StringComparison.Ordinal);
        Assert.True(at > 0, "OnPreviewKey پیدا نشد");
        var body = g[at..Math.Min(g.Length, at + 1400)];

        var read = body.IndexOf("var from = CurIndex(cols);", StringComparison.Ordinal);
        var commit = body.IndexOf("CommitEdit(DataGridEditingUnit.Cell, true);", StringComparison.Ordinal);
        var move = body.IndexOf("MoveColumnFrom(cols, from,", StringComparison.Ordinal);

        Assert.True(read > 0 && commit > read, "ستونِ مبدأ پس از بستنِ ویرایش خوانده می‌شود");
        Assert.True(move > commit, "جابه‌جایی پیش از بستنِ ویرایش است");
    }
}
