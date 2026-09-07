using PumpYaqobi.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record DebtReceiptReportInput(
    string MonthLabel,
    IReadOnlyList<DebtQuickReceipt> Rows,
    string Dates);

/// <summary>
/// ══ سندِ رسید قرض‌داران ══════════════════════════════════════════════════════
/// بازسازیِ ‎pdfDebtRasid(monthKey)‎: سربرگِ سبز، دو کادرِ «تعداد رسیدها» و
/// «جمله کل رسیدها»، و پنج ستون.
/// </summary>
public sealed class DebtReceiptReport : ISetupDocument
{
    private const string Green = "#38a169";

    private readonly DebtReceiptReportInput _in;

    public DebtReceiptReport(DebtReceiptReportInput input) => _in = input;

    /// <summary>تنظیمِ ورق — از «کارگاه چاپ». پیش‌فرض همان ورقی است که همیشه بود.</summary>
    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "🧾 پمپ یعقوبی — رسید قرض‌داران " + _in.MonthLabel,
                         null, _in.Dates, Body, titleColor: Green, setup: Setup);

    private void Body(IContainer c) => c.Column(col =>
    {
        var total = _in.Rows.Sum(r => r.Amount);

        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "تعداد رسیدها", PersianText.Num(_in.Rows.Count), DocStyle.FootFg));
            row.RelativeItem().Element(x =>
                DocStyle.SumBox(x, "جمله کل رسیدها", PersianText.Num(total) + " افغانی", Green));
        });

        col.Item().PaddingTop(8).Element(x => Table(x, total));
    });

    private void Table(IContainer c, decimal total) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);   // #
            cd.RelativeColumn(1.5f);   // تاریخ
            cd.RelativeColumn(2.6f);   // نام قرض‌دار
            cd.RelativeColumn(3.0f);   // توضیحات
            cd.RelativeColumn(1.8f);   // مبلغ رسید
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("#"); Th("تاریخ"); Th("نام قرض‌دار"); Th("توضیحات"); Th("مبلغ رسید");
        });

        var i = 0;
        foreach (var r in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(r.DateShamsi), DocStyle.Hawala);
            Td(DocStyle.Dash(r.Account));
            Td(DocStyle.Dash(r.Note), DocStyle.FootFg);
            Td(PersianText.Num(r.Amount) + " افغانی", Green);
        }

        t.Cell().ColumnSpan(4).Element(x => DocStyle.Tf(x, "جمله کل"));
        t.Cell().Element(x => DocStyle.Tf(x, PersianText.Num(total) + " افغانی"));
    });
}
