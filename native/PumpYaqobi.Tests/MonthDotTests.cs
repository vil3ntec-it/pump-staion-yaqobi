using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ نقطهٔ سرخِ «ماهِ تازه» — جای نوارِ «ماهِ فلان شروع شد» (۱۴۰۵/۰۷/۱۳) ══════
///
/// خواستهٔ صریحِ صاحب ریپو: «روی همان کادرِ کشوییِ ماه‌ها یا سال‌ها یک نقطهٔ سرخ
/// بیاید و رویش که زد همان ماهی که قبلاً بود را با یک نقطهٔ سرخ نشان بدهد… وقتی
/// رفت رویشان آن نقطه‌ها بروند و دیگر دیده نشوند، مگر این‌که ماهِ دیگر عوض شود.»
///
///   ۱) کدام ماه نقطه می‌گیرد (خالص)
///   ۲) کشوی ماه/سال: نقطه روی کشویی و کنارِ همان گزینه
///   ۳) «دیدم» فقط با رفتنِ خودِ کاربر، نه با انتخابِ خودِ برنامه
///   ۴) تا ماهِ بعد برنمی‌گردد — نه با بستن و باز کردنِ برنامه
/// </summary>
[Collection(AppHostCollection.Name)]
public class MonthDotTests : IDisposable
{
    private readonly string? _oldDir = AppSettings.DirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "monthdot-" + Guid.NewGuid().ToString("N"));

    public MonthDotTests()
    {
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _oldDir;
        try { Directory.Delete(_dir, true); } catch { /* موقت است */ }
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static string Src(string rel) =>
        File.ReadAllText(Path.Combine(Root(), rel.Replace('/', Path.DirectorySeparatorChar)));

    // ── ۱) کدام ماه ──────────────────────────────────────────────────────────

    [Fact]
    public void TazeTarinMaheDadedar_PishAzMaheJari()
    {
        var data = new[] { "1405/07", "1405/05", "1405/06", "1404/12" };
        Assert.Equal("1405/06", MonthDot.MarkOf(data, "1405/07", ""));
        //  ماهِ پیش داده نداشت ⇒ تازه‌ترین ماهی که دارد
        Assert.Equal("1405/05", MonthDot.MarkOf(new[] { "1405/05", "1404/12" }, "1405/07", ""));
        //  سرِ سال: حوتِ پارسال
        Assert.Equal("1404/12", MonthDot.MarkOf(new[] { "1404/12", "1404/11" }, "1405/01", ""));
    }

    [Fact]
    public void DidehShode_YaHichDadeiNist_NoghteiNist()
    {
        Assert.Equal("", MonthDot.MarkOf(new[] { "1405/06" }, "1405/07", "1405/07"));   // دیده شد
        Assert.Equal("", MonthDot.MarkOf(new[] { "1405/07" }, "1405/07", ""));          // فقط همین ماه
        Assert.Equal("", MonthDot.MarkOf(Array.Empty<string>(), "1405/07", ""));        // نصبِ تازه
        //  ماهِ بعد دوباره (دیدنِ ماهِ پیش، ماهِ بعد را نمی‌پوشاند)
        Assert.Equal("1405/07", MonthDot.MarkOf(new[] { "1405/07", "1405/06" }, "1405/08", "1405/07"));
        //  «همه»، خالی و کلیدِ خراب هیچ‌وقت نقطه نمی‌گیرند
        Assert.Equal("", MonthDot.MarkOf(new[] { "", "1405/*", "x", "1405/6" }, "1405/07", ""));
        //  رقمِ فارسی همان رقم است
        Assert.Equal("1405/06", MonthDot.MarkOf(new[] { "۱۴۰۵/۰۶" }, "۱۴۰۵/۰۷", ""));
    }

    // ── ۲) کشوی ماه/سال ─────────────────────────────────────────────────────

    [Fact]
    public void KeshuyeMah_Noghte_RooyeKeshuyi_VaKenareHamanMah()
    {
        var p = new YearMonthPicker(_ => { }, "همهٔ ماه‌ها");
        p.Load(new[] { "1405/07", "1405/06", "1405/05" }, "1405/07");
        p.SetDot("1405/06");

        Assert.True(p.HasMonthDot);
        Assert.False(p.HasYearDot);
        Assert.True(p.Months.Single(m => m.Key == "1405/06").Dot);
        Assert.All(p.Months.Where(m => m.Key != "1405/06"), m => Assert.False(m.Dot));
    }

    [Fact]
    public void SareSal_Noghte_RooyeKeshuyeSal()
    {
        var p = new YearMonthPicker(_ => { }, "همهٔ ماه‌ها");
        p.Load(new[] { "1405/01", "1404/12", "1404/11", "1404/01" }, "1405/01");
        p.SetDot("1404/12");

        //  ماهِ نقطه‌دار در سالِ دیگری است ⇒ نقطه روی کشوی سال و کنارِ همان سال
        Assert.True(p.HasYearDot);
        Assert.False(p.HasMonthDot);
        Assert.True(p.Years.Single(y => y.Key == "1404").Dot);
        Assert.False(p.Years.Single(y => y.Key == "1405").Dot);

        //  رفتن به همان سال (حملِ پارسال می‌آید) ⇒ حالا نقطه کنارِ خودِ حوت است
        p.Year = p.Years.Single(y => y.Key == "1404");
        Assert.Equal("1404/01", p.SelectedKey);
        Assert.False(p.HasYearDot);
        Assert.True(p.Months.Single(m => m.Key == "1404/12").Dot);
        Assert.True(p.HasMonthDot);
    }

    [Fact]
    public void SaleDigar_KeHamanMahRaMiavarad_DidanAst()
    {
        //  سالِ پارسال فقط حوت را دارد: رفتن به همان سال یعنی دیدنِ همان ماه
        var p = new YearMonthPicker(_ => { }, "");
        p.Load(new[] { "1405/01", "1404/12" }, "1405/01");
        p.SetDot("1404/12");
        var seen = 0;
        p.DotSeen += () => { seen++; p.SetDot(""); };
        p.Year = p.Years.Single(y => y.Key == "1404");
        Assert.Equal("1404/12", p.SelectedKey);
        Assert.Equal(1, seen);
        Assert.False(p.HasYearDot || p.HasMonthDot);
    }

    // ── ۳) «دیدم» ───────────────────────────────────────────────────────────

    [Fact]
    public void RaftanBeHamanMah_DotSeen_Mizanad()
    {
        var picked = new List<string>();
        var p = new YearMonthPicker(k => picked.Add(k), "همهٔ ماه‌ها");
        p.Load(new[] { "1405/07", "1405/06" }, "1405/07");
        p.SetDot("1405/06");
        var seen = 0;
        p.DotSeen += () => { seen++; p.SetDot(""); };

        //  رفتن به ماهِ دیگری که نقطه ندارد «دیدم» نیست
        p.Selected = p.Months.Single(m => m.Key == "1405/*");
        Assert.Equal(0, seen);
        Assert.True(p.HasMonthDot);

        p.Selected = p.Months.Single(m => m.Key == "1405/06");
        Assert.Equal(1, seen);
        Assert.False(p.HasMonthDot);
        Assert.All(p.Months, m => Assert.False(m.Dot));
        Assert.Contains("1405/06", picked);
    }

    [Fact]
    public void EntekhabeKhodeBarname_DidanNist()
    {
        var p = new YearMonthPicker(_ => { }, "همهٔ ماه‌ها");
        var seen = 0;
        p.DotSeen += () => seen++;
        p.SetDot("1405/06");
        //  بار شدن روی همان ماه (مثلاً ورق‌ها که خودش ماهِ دارای داده را می‌آورد)
        p.Load(new[] { "1405/07", "1405/06" }, "1405/06");
        p.Adopt("1405/07");
        Assert.Equal(0, seen);
        //  و ماهی که همین حالا جلوی چشم است نقطه نمی‌گیرد
        p.Load(new[] { "1405/07", "1405/06" }, "1405/06");
        Assert.False(p.HasMonthDot);
        Assert.False(p.Months.Single(m => m.Key == "1405/06").Dot);
    }

    // ── ۴) تا ماهِ بعد برنمی‌گردد ──────────────────────────────────────────────

    [Fact]
    public void DidanSabtMishavad_VaBaBastanoBaz_Bar_Nemigardad()
    {
        Assert.Equal("", MonthDotStore.SeenOf("safe"));
        MonthDotStore.Ack("safe", "1405/07");
        Assert.Equal("1405/07", MonthDotStore.SeenOf("safe"));
        Assert.True(File.Exists(Path.Combine(_dir, "month-dots.json")));

        //  «بستن و باز کردن»: پوشهٔ دیگر و برگشت ⇒ از روی دیسک دوباره خوانده می‌شود
        AppSettings.DirOverride = _dir + "-x";
        Assert.Equal("", MonthDotStore.SeenOf("safe"));
        AppSettings.DirOverride = _dir;
        Assert.Equal("1405/07", MonthDotStore.SeenOf("safe"));

        //  هر بخش جدا
        Assert.Equal("", MonthDotStore.SeenOf("expenses"));
        Assert.Equal("", MonthDot.MarkOf(new[] { "1405/06" }, "1405/07", MonthDotStore.SeenOf("safe")));
        Assert.Equal("1405/06", MonthDot.MarkOf(new[] { "1405/06" }, "1405/07", MonthDotStore.SeenOf("expenses")));
        try { Directory.Delete(_dir + "-x", true); } catch { }
    }

    // ── پنج نما، یک قالب ────────────────────────────────────────────────────

    [Theory]
    [InlineData("SafeSectionView", "Picker.HasMonthDot", "Picker.HasYearDot")]
    [InlineData("ExpenseSectionView", "Picker.HasMonthDot", "Picker.HasYearDot")]
    [InlineData("RetailSectionView", "Picker.HasMonthDot", "Picker.HasYearDot")]
    [InlineData("ExchangeSectionView", "Picker.HasMonthDot", "Picker.HasYearDot")]
    [InlineData("WaraqSectionView", "Binding HasMonthDot", "Binding HasYearDot")]
    public void HarKeshuyeMah_NoghteDarad(string view, string monthDot, string yearDot)
    {
        var v = Src($"PumpYaqobi.App/Views/Sections/{view}.axaml");
        Assert.Contains(monthDot, v);
        Assert.Contains(yearDot, v);
        Assert.Equal(2, CountOf(v, "ItemTemplate=\"{StaticResource Pump.MonthDotItem}\""));
        Assert.Equal(2, CountOf(v, "Classes=\"monthdot badge\""));
    }

    [Fact]
    public void GhalebeNoghte_YekJaAst_VaAnimationNadarad()
    {
        var c = Src("PumpYaqobi.App/Themes/Controls.axaml");
        Assert.Contains("x:Key=\"Pump.MonthDotItem\"", c);
        Assert.Contains("IsVisible=\"{Binding Dot}\"", c);
        var at = c.IndexOf("Selector=\"Ellipse.monthdot\"", StringComparison.Ordinal);
        Assert.True(at > 0);
        Assert.DoesNotContain("Animation", c[at..Math.Min(c.Length, at + 1200)]);
    }

    [Fact]
    public void DidanRa_KhodeKarbar_Mizanad_VaNaHichDasturiDatabase()
    {
        var led = Src("PumpYaqobi.App/ViewModels/LedgerSectionViewModel.cs");
        Assert.Contains("Picker.DotSeen +=", led);
        Assert.Contains("MonthDotStore.Ack(Id, Shamsi.ThisMonth());", led);
        var wq = Src("PumpYaqobi.App/ViewModels/Sections/WaraqSectionViewModel.cs");
        Assert.Contains("MonthDotStore.Ack(Id, Shamsi.ThisMonth());", wq);
        //  ⛔ نه جدولِ Settings (اجازه می‌خواهد و Version را بالا می‌برد) و نه settings.json
        var dot = Src("PumpYaqobi.App/ViewModels/MonthDot.cs");
        Assert.DoesNotContain("Settings.Set(", dot);
        Assert.DoesNotContain(".Save()", dot);
    }

    /// <summary>نیمه‌شبِ آخرِ ماه، برنامهٔ باز: همان نقطه — نه نوار و نه توست.</summary>
    [Fact]
    public void NimeShab_NoghteMigozarad()
    {
        var led = Src("PumpYaqobi.App/ViewModels/LedgerSectionViewModel.cs");
        var at = led.IndexOf("public override void OnDayChanged()", StringComparison.Ordinal);
        var body = led[at..led.IndexOf("\n    }", at, StringComparison.Ordinal)];
        Assert.Contains("RefreshDot();", body);
        //  منطقِ جابه‌جایی همان است که بود
        Assert.Contains("MonthRoll.Decide(Month, _autoMonth, now)", body);

        var wq = Src("PumpYaqobi.App/ViewModels/Sections/WaraqSectionViewModel.cs");
        at = wq.IndexOf("public override void OnDayChanged()", StringComparison.Ordinal);
        body = wq[at..wq.IndexOf("\n    }", at, StringComparison.Ordinal)];
        Assert.Contains("LoadAsync", body);
        Assert.Contains("RefreshDot();", body);
    }

    private static int CountOf(string s, string what)
    {
        int n = 0, at = 0;
        while ((at = s.IndexOf(what, at, StringComparison.Ordinal)) >= 0) { n++; at += what.Length; }
        return n;
    }
}
