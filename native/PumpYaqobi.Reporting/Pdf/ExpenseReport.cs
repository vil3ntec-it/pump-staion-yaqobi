using PumpYaqobi.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="TodayShamsi">تاریخِ امروز — کادرِ «مصارف امروز» از همین می‌آید.</param>
public sealed record ExpenseReportInput(
    string MonthLabel,
    IReadOnlyList<Expense> Rows,
    string TodayShamsi,
    string Dates);

/// <summary>
/// ══ سندِ مصارف ═════════════════════════════════════════════════════════════
/// بازسازیِ ‎pdfExpenses(monthKey)‎: سربرگِ قرمز، سه کادرِ «تعداد مصارف»،
/// «مصارف امروز» و «جمله کل مصارف»، و پنج ستون.
/// </summary>
public sealed class ExpenseReport : IDocument
{
    private readonly ExpenseReportInput _in;

    public ExpenseReport(ExpenseReportInput input) => _in = input;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container,
            "💸 پمپ یعقوبی — مصارف ماه " + _in.MonthLabel, null, _in.Dates, Body,
            titleColor: DocStyle.Danger);

    private void Body(IContainer c) => c.Column(col =>
    {
        var total = _in.Rows.Sum(e => e.Amount);
        var today = _in.Rows.Where(e => e.DateShamsi == _in.TodayShamsi).Sum(e => e.Amount);

        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "تعداد مصارف", PersianText.Num(_in.Rows.Count), DocStyle.FootFg));
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "مصارف امروز", PersianText.Num(today) + " افغانی", DocStyle.Danger));
            row.RelativeItem().Element(x =>
                DocStyle.SumBox(x, "جمله کل مصارف", PersianText.Num(total) + " افغانی", DocStyle.Danger));
        });

        col.Item().PaddingTop(8).Element(x => Table(x, total));
    });

    private void Table(IContainer c, decimal total) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);   // #
            cd.RelativeColumn(1.5f);   // تاریخ
            cd.RelativeColumn(3.0f);   // عنوان
            cd.RelativeColumn(1.8f);   // مبلغ
            cd.RelativeColumn(2.6f);   // یادداشت
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("#"); Th("تاریخ"); Th("عنوان"); Th("مبلغ"); Th("یادداشت");
        });

        var i = 0;
        foreach (var e in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(e.DateShamsi), DocStyle.Hawala);
            Td(DocStyle.Dash(e.Title));
            Td(PersianText.Num(e.Amount) + " افغانی", DocStyle.Danger);
            Td(DocStyle.Dash(e.Note), DocStyle.FootFg);
        }

        t.Cell().ColumnSpan(3).Element(x => DocStyle.Tf(x, "جمله کل"));
        t.Cell().Element(x => DocStyle.Tf(x, PersianText.Num(total) + " افغانی"));
        t.Cell().Element(x => DocStyle.Tf(x, ""));
    });
}
