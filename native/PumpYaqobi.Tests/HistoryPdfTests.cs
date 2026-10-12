using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Data;
using QuestPDF.Fluent;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «هیچ‌کدام از تاریخچه‌ها PDF ندارند» (۱۴۰۵/۰۷/۲۰، نسخه 3.1.272) ═══════════
/// هر سیزده تاریخچه و تاریخچهٔ نرخ یک سندِ واقعی و ناخالی می‌سازند — با همان
/// ستون‌ها و ردیف‌های صفحه — و هر دو صفحه دکمهٔ «🖨 PDF» دارند.
/// </summary>
public class HistoryPdfTests
{
    private const string Dates = "1405/07/20  ·  1448/04/28  ·  2026/10/12";

    private static void Check(QuestPDF.Infrastructure.IDocument doc, string name)
    {
        PdfEngine.Initialize();
        var bytes = doc.GeneratePdf();
        Assert.True(bytes.Length > 3000, $"{name}: سند خیلی کوچک است ({bytes.Length} بایت)");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Contains("Vazirmatn", System.Text.Encoding.Latin1.GetString(bytes));
        Assert.NotEmpty(doc.GenerateImages().Take(1));
    }

    private static List<HistoryRowViewModel> Sample(string kind)
    {
        var cols = HistoryService.ColumnsOf(kind);
        var rows = new List<HistoryRowViewModel>();
        for (var i = 1; i <= 40; i++)
        {
            var cells = cols?.Select((c, k) => c.Header + " " + i + "/" + k).ToList();
            var shift = kind == "shift"
                ? new HistoryShiftRef(i, ShiftKind.Day, "کارمند " + i, 1 + i % 3, 1000m * i, 1000m * i + 350m, 62m, 500m)
                : null;
            var r = new HistoryRow(kind, "1405/07/" + (i % 28 + 1).ToString("00"), 14050700 + i % 28 + 1,
                "ثبتِ " + i, "توضیحِ " + i, 1250m * i, " افغانی", i % 2 == 0 ? "in" : "out",
                cells, FuelType.Petrol, 1 + i % 3, shift);
            rows.Add(new HistoryRowViewModel(r, i));
        }
        return rows;
    }

    public static IEnumerable<object[]> AllKinds() =>
        HistoryService.Kinds.Select(k => new object[] { k.Key });

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryHistory_BuildsANonEmptyPdf_WithTheSameColumns(string kind)
    {
        var rows = Sample(kind);
        var input = HistorySectionViewModel.PdfInput(kind, HistoryService.ColumnsOf(kind), rows, "1405/07", Dates);

        Assert.Equal(rows.Count, input.Rows.Count);
        var cols = HistoryService.ColumnsOf(kind);
        Assert.Equal(cols?.Count ?? 4, input.Columns.Count);
        if (cols is not null)
            Assert.Equal(cols.Select(c => c.Header), input.Columns.Select(c => c.Header));
        Assert.All(input.Rows, r => Assert.Equal(input.Columns.Count, r.Cells.Count));

        Check(new HistoryReport(input), "history-" + kind);
    }

    /// <summary>پارچه: خانه‌های ویرایش‌شدنی همان عددهای جدول‌اند، نه ‎Cells‎ِ کهنه.</summary>
    [Fact]
    public void ShiftPdf_UsesTheSameCellsAsTheGrid()
    {
        var rows = Sample("shift");
        var input = HistorySectionViewModel.PdfInput("shift", HistoryService.ColumnsOf("shift"), rows, "", Dates);
        var heads = input.Columns.Select(c => c.Header).ToList();
        Assert.Equal(rows[0].EditName, input.Rows[0].Cells[heads.IndexOf("کارمند")]);
        Assert.Equal(rows[0].EditStart, input.Rows[0].Cells[heads.IndexOf("شروعِ پایه")]);
        Assert.Equal(rows[0].MoneyCell, input.Rows[0].Cells[heads.IndexOf("پول")]);
    }

    [Fact]
    public void EmptyHistory_StillBuilds()
        => Check(new HistoryReport(HistorySectionViewModel.PdfInput(
               "safe", null, new List<HistoryRowViewModel>(), "همهٔ ماه‌ها", Dates)), "history-empty");

    [Fact]
    public void RateHistory_BuildsANonEmptyPdf()
    {
        var rows = Enumerable.Range(1, 12).Select(i => new RateHistoryRowViewModel(
            new RateHistoryEntry { Fuel = i % 2 == 0 ? FuelType.Diesel : FuelType.Petrol, Rate = 60m + i,
                                   DateShamsi = "1405/0" + (i % 9 + 1) + "/01" }, i)).ToList();
        var input = RateHistorySectionViewModel.PdfInput(rows, Dates);
        Assert.Equal(12, input.Rows.Count);
        Check(new HistoryReport(input), "history-rate");
    }

    [Fact]
    public void LargeHistory_BuildsWithoutTheUiThread()
    {
        // ده‌هزار ردیف — ساخته شدنش روی نخِ دیگر (همان ‎Task.Run‎ِ ‎Documents.ShowAsync‎)
        var rows = Enumerable.Range(0, 25).SelectMany(_ => Sample("waraq")).ToList();
        var input = HistorySectionViewModel.PdfInput("waraq", HistoryService.ColumnsOf("waraq"), rows, "", Dates);
        var bytes = Task.Run(() => { PdfEngine.Initialize(); return new HistoryReport(input).GeneratePdf(); }).Result;
        Assert.True(bytes.Length > 10_000);
    }

    // ── دکمه در هر دو صفحه ───────────────────────────────────────────────
    private static string Src(string rel) =>
        SrcText.Read(Path.Combine(RepoRoot(), "native", "PumpYaqobi.App", rel));

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "CLAUDE.md"))) d = d.Parent;
        return d!.FullName;
    }

    [Theory]
    [InlineData("Views/Sections/HistorySectionView.axaml", "ViewModels/Sections/HistorySectionViewModel.cs")]
    [InlineData("Views/Sections/RateHistorySectionView.axaml", "ViewModels/Sections/RateHistorySectionViewModel.cs")]
    public void EveryHistoryView_HasAPdfButton(string view, string vm)
    {
        var v = Src(view);
        Assert.Contains("Content=\"🖨 PDF\"", v);
        Assert.Contains("{Binding PdfCommand}", v);
        var m = Src(vm);
        Assert.Contains("private Task PdfAsync()", m);
        Assert.Contains("Documents.ShowAsync(", m);
        Assert.Contains("new HistoryReport(", m);
    }
}
