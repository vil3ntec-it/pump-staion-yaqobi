using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ‎A−‎ / ‎A+‎ / ‎↺‎ِ هر بخش، و ‎A−/A+‎ِ جداگانهٔ یادداشت‌ها ══════════════════
///
/// در سایت کنارِ عنوانِ **هر** بخش این سه دکمه هست و خودِ سایت هم نوشته چرا:
/// «بخش‌هایی که این دکمه‌ها را نداشتند خودکار می‌گیرند، پس دیگر هیچ بخشی بدونِ
/// کنترلِ اندازه نمی‌ماند.» در برنامهٔ نیتیو هیچ بخشی نداشت.
///
/// ⚠️ دو کنترلِ جدا، به خواستهٔ صریحِ سایت: اندازهٔ یادداشت‌ها با بزرگ شدنِ
/// جدول‌ها عوض نمی‌شود و برعکس.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SectionFontTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Theme() =>
        File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Themes", "Controls.axaml"));

    private sealed class Fake : SectionViewModel
    {
        public Fake(string id) : base(id, id, id) { }
    }

    /// <summary>هر تغییرِ اندازه در پوشهٔ موقت بنشیند، نه در تنظیماتِ واقعیِ کاربر.</summary>
    private static void InTempSettings(Action body)
    {
        var old = AppSettings.DirOverride;
        var dir = Path.Combine(Path.GetTempPath(), "pump-font-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppSettings.DirOverride = dir;
        SectionViewModel.ForgetFontScales();
        try { body(); }
        finally
        {
            AppSettings.DirOverride = old;
            SectionViewModel.ForgetFontScales();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // ── ویومدل ───────────────────────────────────────────────────────────────

    /// <summary>گام، کف و سقف مو‌به‌مو همان سایت است.</summary>
    [Fact]
    public void TheStepAndLimitsMatchTheSite()
    {
        Assert.Equal(0.08, SectionViewModel.FontStep);
        Assert.Equal(0.6, SectionViewModel.FontMin);
        Assert.Equal(2.2, SectionViewModel.FontMax);
    }

    [Fact]
    public void BiggerAndSmallerMoveOneStepAndResetReturnsToOne() => InTempSettings(() =>
    {
        var s = new Fake("safe");
        Assert.Equal(1, s.FontScale);

        s.FontBiggerCommand.Execute(null);
        Assert.Equal(1.08, s.FontScale, 3);

        s.FontSmallerCommand.Execute(null);
        Assert.Equal(1, s.FontScale, 3);

        s.FontBiggerCommand.Execute(null);
        s.FontBiggerCommand.Execute(null);
        s.FontResetCommand.Execute(null);
        Assert.Equal(1, s.FontScale, 3);
    });

    /// <summary>به کف و سقف که رسید، همان‌جا می‌ایستد — نه بی‌نهایت کوچک، نه بی‌نهایت بزرگ.</summary>
    [Fact]
    public void ItStopsAtTheFloorAndTheCeiling() => InTempSettings(() =>
    {
        var s = new Fake("waraq");
        for (var i = 0; i < 60; i++) s.FontBiggerCommand.Execute(null);
        Assert.Equal(SectionViewModel.FontMax, s.FontScale, 3);

        for (var i = 0; i < 90; i++) s.FontSmallerCommand.Execute(null);
        Assert.Equal(SectionViewModel.FontMin, s.FontScale, 3);
    });

    /// <summary>
    /// اندازه می‌ماند — و فقط برای همان بخش. (در سایت هم کلیدِ جدا دارد:
    /// ‎secFont_&lt;id&gt;‎.)
    /// </summary>
    [Fact]
    public void EachSectionKeepsItsOwnSizeAcrossRestarts() => InTempSettings(() =>
    {
        var safe = new Fake("safe");
        safe.FontBiggerCommand.Execute(null);
        safe.FontBiggerCommand.Execute(null);

        // «راه‌اندازیِ دوباره»: حافظه دور ریخته و از فایل خوانده می‌شود
        SectionViewModel.ForgetFontScales();
        Assert.Equal(1.16, new Fake("safe").FontScale, 3);
        Assert.Equal(1, new Fake("sarrafi").FontScale, 3);
    });

    /// <summary>برگشت به عادی، کلید را از فایل هم برمی‌دارد — نه این‌که ۱ بنویسد.</summary>
    [Fact]
    public void ResetRemovesTheStoredKey() => InTempSettings(() =>
    {
        var s = new Fake("expenses");
        s.FontBiggerCommand.Execute(null);
        Assert.True(AppSettings.Load().SecFontScales.ContainsKey("expenses"));

        s.FontResetCommand.Execute(null);
        Assert.False(AppSettings.Load().SecFontScales.ContainsKey("expenses"));
    });

    // ── یادداشت‌ها: کنترلِ جدا ────────────────────────────────────────────────

    /// <summary>کف و سقفِ یادداشت‌ها عمداً با مالِ بخش یکی نیست — همان سایت.</summary>
    [Fact]
    public void TheNoteFontHasItsOwnLimits()
    {
        Assert.Equal(0.5, NoteFontViewModel.Min);
        Assert.Equal(2.5, NoteFontViewModel.Max);
        Assert.Equal(SectionViewModel.FontStep, NoteFontViewModel.Step);
        Assert.NotEqual(SectionViewModel.FontMin, NoteFontViewModel.Min);
    }

    /// <summary>یکی است برای همهٔ بخش‌ها — مثلِ متغیرِ ریشه‌ایِ سایت.</summary>
    [Fact]
    public void AllSectionsShareOneNoteFont()
        => Assert.Same(NoteFontViewModel.Instance, NoteFontViewModel.Instance);

    // ── قالبِ مشترک ───────────────────────────────────────────────────────────

    /// <summary>
    /// سه دکمه در قالبِ **مشترک** نشسته‌اند، نه در تک‌تکِ ویوها — پس هر بخشی
    /// که ساخته شود خودبه‌خود دارد.
    /// </summary>
    [Fact]
    public void TheThreeButtonsLiveInTheSharedSectionTemplate()
    {
        var t = Theme();
        Assert.Contains("Command=\"{Binding FontSmallerCommand}\"", t);
        Assert.Contains("Command=\"{Binding FontBiggerCommand}\"", t);
        Assert.Contains("Command=\"{Binding FontResetCommand}\"", t);
    }

    /// <summary>
    /// ⚠️ فقط **بدنه** بزرگ می‌شود. سایت صریح نوشته: «‎A−/A+‎ برای نوشته‌های
    /// خودِ بخش است، نه برای نوارِ ابزارش.» پس سربرگ و نوارِ ابزار بیرونِ
    /// قابِ بزرگ‌شونده مانده‌اند.
    /// </summary>
    [Fact]
    public void OnlyTheBodyScalesNotTheToolbar()
    {
        var t = Theme();
        var open = t.IndexOf("<LayoutTransformControl", StringComparison.Ordinal);
        var close = t.IndexOf("</LayoutTransformControl>", StringComparison.Ordinal);
        Assert.True(open > 0 && close > open);

        var toolbar = t.IndexOf("Content=\"{TemplateBinding Toolbar}\"", StringComparison.Ordinal);
        var header = t.IndexOf("Text=\"{TemplateBinding Header}\"", StringComparison.Ordinal);
        Assert.True(toolbar > 0 && toolbar < open, "نوارِ ابزار نباید داخلِ قابِ بزرگ‌شونده باشد");
        Assert.True(header > 0 && header < open, "سربرگ نباید داخلِ قابِ بزرگ‌شونده باشد");

        Assert.True(t.IndexOf("Content=\"{TemplateBinding Body}\"", StringComparison.Ordinal) is var b
                    && b > open && b < close, "بدنه باید داخلِ قابِ بزرگ‌شونده باشد");
    }

    /// <summary>
    /// اندازه از خودِ ویومدل می‌آید و با مبدلِ مشترک به دگرگونیِ چیدمان تبدیل
    /// می‌شود — نه با ‎zoom‎ِ سرِ دست و نه با بایندینگ روی تک‌تکِ خانه‌های جدول
    /// (که سنجشِ کاراییِ جدول‌ها را خراب می‌کرد).
    /// </summary>
    [Fact]
    public void TheBodyScaleComesFromTheViewModel()
    {
        var t = Theme();
        Assert.Contains("LayoutTransform=\"{Binding FontScale,", t);
        Assert.Contains("th:FontScaleConverter.Instance", t);
    }

    /// <summary>کادرِ یادداشت اندازهٔ خودش را دارد، نه اندازهٔ بخش.</summary>
    [Fact]
    public void TheNoteBoxUsesItsOwnSize()
    {
        var t = Theme();
        Assert.Contains("FontSize=\"{Binding Font.Size}\"", t);
        Assert.Contains("Command=\"{Binding Font.SmallerCommand}\"", t);
        Assert.Contains("Command=\"{Binding Font.BiggerCommand}\"", t);
    }
}
