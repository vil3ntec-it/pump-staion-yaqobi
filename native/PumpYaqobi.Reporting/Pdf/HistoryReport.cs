using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>یک ستونِ سندِ تاریخچه — همان ستونِ جدولِ صفحه.</summary>
/// <param name="Wide">ستونِ پهن (نام، توضیح، شرح).</param>
/// <param name="Brush">
/// رنگِ نوشته، همان کلیدِ تمِ صفحه (‎Pump.Ok‎، ‎Pump.Danger‎…)، یا ‎"tone"‎ یعنی
/// رنگِ «آمد/رفت»ِ همان ردیف، یا خالی یعنی رنگِ معمولی.
/// </param>
public sealed record HistoryReportColumn(string Header, bool Wide = false, string Brush = "");

/// <summary>یک ردیفِ سند — خانه‌ها همان نوشته‌هایی‌اند که صفحه نشان می‌دهد.</summary>
/// <param name="Tone">‎in‎ سبز · ‎out‎ نارنجی · خالی بی‌رنگ.</param>
public sealed record HistoryReportRow(IReadOnlyList<string> Cells, string Tone = "");

/// <param name="Label">نامِ تاریخچه («🏦 گاوصندوق»، «تاریخچهٔ نرخ»…).</param>
/// <param name="Filter">صافیِ جاری: ماه/سال/همه، تیل، پایه — همان که روی صفحه است.</param>
public sealed record HistoryReportInput(
    string Label, string Filter,
    IReadOnlyList<HistoryReportColumn> Columns,
    IReadOnlyList<HistoryReportRow> Rows,
    string Dates);

/// <summary>
/// ══ سندِ تاریخچه‌ها (۱۴۰۵/۰۷/۲۰) ═══════════════════════════════════════════
/// گزارشِ صاحب ریپو: «هیچ‌کدام از تاریخچه‌ها PDF ندارند.»
///
/// یک سند برای همهٔ تاریخچه‌ها: دقیقاً همان ستون‌ها و همان ردیف‌هایی که صفحه با
/// صافیِ جاری نشان می‌دهد (‎HistoryService.ColumnsOf‎ یا جدولِ کلی)، با همان
/// سربرگ و پانویسِ تاریخِ سندهای دیگر.
/// ⛔ هیچ عددی این‌جا حساب نمی‌شود — خانه‌ها همان نوشته‌های صفحه‌اند.
/// </summary>
public sealed class HistoryReport : ISetupDocument
{
    private const string Purple = "#7c3aed";
    private readonly HistoryReportInput _in;

    public HistoryReport(HistoryReportInput input) => _in = input;

    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    /// <summary>جدولِ پهن (هفت ستون و بیشتر) افقی چیده می‌شود.</summary>
    public bool Landscape => _in.Columns.Count >= 7;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "🕘 " + PumpBrand.Name + " — تاریخچهٔ " + _in.Label,
                         _in.Filter, _in.Dates, Body, landscape: Landscape, setup: Setup);

    /// <summary>کلیدِ رنگِ صفحه ⇐ رنگِ سند.</summary>
    public static string? Color(string brush, string tone) => brush switch
    {
        "tone" => tone switch { "in" => DocStyle.Money, "out" => DocStyle.Hawala, _ => null },
        "Pump.Ok" => DocStyle.Money,
        "Pump.Danger" => DocStyle.Danger,
        "Pump.Accent" => DocStyle.Hawala,
        "Pump.Purple" => Purple,
        _ => null,
    };

    private void Body(IContainer c) => c.Column(col =>
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "شمارِ ردیف‌ها", PersianText.Num(_in.Rows.Count), DocStyle.FootFg));
            row.RelativeItem(2).Element(x =>
                DocStyle.SumBox(x, "صافی", DocStyle.Dash(_in.Filter), DocStyle.Sub));
        });
        if (_in.Rows.Count == 0)
            col.Item().PaddingTop(14).AlignCenter().Element(x =>
                DocStyle.Line(x, "در این صافی چیزی ثبت نشده", DocStyle.CellSize, DocStyle.FootFg));
        else
            col.Item().PaddingTop(8).Element(Table);
    });

    private void Table(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.5f);   // #
            foreach (var col in _in.Columns) cd.RelativeColumn(col.Wide ? 2.6f : 1.2f);
        });
        DocStyle.Head(t, cell =>
        {
            DocStyle.ThText(cell(), "#");
            foreach (var col in _in.Columns) DocStyle.ThText(cell(), col.Header);
        });
        var i = 0;
        foreach (var r in _in.Rows)
        {
            var even = i % 2 == 1;
            DocStyle.TdText(t.Cell(), even, PersianText.Num(++i), DocStyle.Index);
            for (var k = 0; k < _in.Columns.Count; k++)
            {
                var text = k < r.Cells.Count ? r.Cells[k] : "";
                DocStyle.TdText(t.Cell(), even, DocStyle.Dash(text), Color(_in.Columns[k].Brush, r.Tone));
            }
        }
    });
}
