using System.Reflection;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «➕ ردیف» و «➕➕ چندتایی» ═══════════════════════════════════════════════
///
/// در سایت زیرِ جدولِ مصارف، گاوصندوق، صرافی، رسید پارچه‌ها، حسابِ شخص، شرکت و
/// ورق یک نوار هست: دکمهٔ ردیف، و کنارش کادرِ «تعداد» با دکمهٔ «چندتایی».
/// در برنامهٔ نیتیو «چندتایی» فقط با میان‌بُرِ ‎Ctrl+عدد‎ بود و هیچ دکمه‌ای
/// نداشت — کسی که میان‌بُر را نمی‌دانست راهی نداشت.
///
/// ⚠️ منطقِ تازه‌ای ساخته نشد: همان ‎IRowBatchHost.AddRowsAsync‎ی که میان‌بُر
/// صدا می‌زند. یک راه، دو در.
/// </summary>
public class RowAddBarTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string View(string name) =>
        Read("PumpYaqobi.App", "Views", "Sections", name);

    /// <summary>سقف و پیش‌فرض همان سایت است (‎Math.min(n, 50)‎ و ‎value="5"‎).</summary>
    [Fact]
    public void TheCapAndDefaultMatchTheSite()
    {
        Assert.Equal(50, RowAddBar.MaxRows);
        Assert.Equal(5, RowAddBar.DefaultCount);
    }

    /// <summary>
    /// نوار در قالبِ **مشترک** نشسته، نه در تک‌تکِ ویوها — و جایش زیرِ جدول
    /// است، نه در سربرگ. خودِ سایت هم نوشته چرا: «تا سربرگ شلوغ نباشد و دستِ
    /// آدم موقعِ کار به آن‌ها نخورد.»
    /// </summary>
    [Fact]
    public void TheBarLivesInTheSharedTemplateBelowTheTable()
    {
        var t = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        Assert.Contains("<c:RowAddBar Grid.Row=\"6\"", t);
        Assert.Contains("AddRowCommand=\"{Binding RowAddCommand}\"", t);

        // و خودِ نوار: یک دکمهٔ ردیف، کادرِ تعداد، دکمهٔ چندتایی
        Assert.Contains("Content=\"➕ ردیف\"", t);
        Assert.Contains("Content=\"➕➕ چندتایی\"", t);
        Assert.Contains("Name=\"PART_Many\"", t);
    }

    /// <summary>
    /// دکمهٔ «ردیف» از سربرگِ این بخش‌ها رفت — همان جابه‌جاییِ خواسته‌شدهٔ سایت.
    /// </summary>
    [Theory]
    [InlineData("ExpenseSectionView.axaml")]
    [InlineData("SafeSectionView.axaml")]
    [InlineData("ExchangeSectionView.axaml")]
    [InlineData("RetailSectionView.axaml")]
    [InlineData("ParchaReceiptSectionView.axaml")]
    public void TheToolbarNoLongerCarriesAnAddRowButton(string view)
    {
        var v = View(view);
        var head = v.IndexOf("</c:SectionPage.Toolbar>", StringComparison.Ordinal);
        Assert.True(head > 0);
        var toolbar = v[..head];
        Assert.DoesNotContain("Command=\"{Binding AddRowCommand}\"", toolbar);
    }

    /// <summary>
    /// هر بخشی که ردیفِ دستی دارد، نوارش را هم دارد — ‎RowAddCommand‎ همان
    /// فرمانِ قبلی است، نه یک فرمانِ تازه.
    /// </summary>
    [Fact]
    public void EveryHandTypedLedgerExposesTheCommand()
    {
        var led = Read("PumpYaqobi.App", "ViewModels", "LedgerSectionViewModel.cs");
        Assert.Contains("public override System.Windows.Input.ICommand? RowAddCommand => AddRowCommand;", led);

        var rasid = Read("PumpYaqobi.App", "ViewModels", "Sections", "ParchaReceiptSectionViewModel.cs");
        Assert.Contains("RowAddCommand => AddRowCommand", rasid);

        var comp = Read("PumpYaqobi.App", "ViewModels", "Sections", "CompanySectionViewModel.cs");
        Assert.Contains("RowAddCommand => AddRowCommand", comp);

        var waraq = Read("PumpYaqobi.App", "ViewModels", "Sections", "WaraqSectionViewModel.cs");
        Assert.Contains("RowAddCommand => AddTxnCommand", waraq);
    }

    /// <summary>صفحهٔ شخص و حسابِ امانت هم همان نوارِ مشترک را گرفتند.</summary>
    [Theory]
    [InlineData("PersonView.axaml")]
    [InlineData("AmanatSectionView.axaml")]
    public void ThePagesUseTheSameSharedBar(string view)
        => Assert.Contains("<c:RowAddBar", View(view));

    /// <summary>
    /// بخشی که ردیفِ دستی ندارد (داشبورد، تاریخچه‌ها، …) هیچ نواری نمی‌بیند —
    /// ‎null‎ می‌ماند و قالب پنهانش می‌کند.
    /// </summary>
    [Fact]
    public void SectionsWithoutHandTypedRowsGetNoBar()
    {
        var prop = typeof(SectionViewModel).GetProperty("RowAddCommand",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.True(prop!.GetGetMethod()!.IsVirtual);
    }

    /// <summary>عددِ بی‌معنی هیچ ردیفی نمی‌سازد — نه خطا، نه ردیفِ ناخواسته.</summary>
    [Fact]
    public void ANonsenseCountAddsNothing()
    {
        var src = Read("PumpYaqobi.App", "Controls", "RowAddBar.cs");
        Assert.Contains("Math.Clamp(Count, 0, MaxRows)", src);
        Assert.Contains("if (n < 1) return;", src);
        Assert.Contains("h.AddRowsAsync(n)", src);
    }
}
