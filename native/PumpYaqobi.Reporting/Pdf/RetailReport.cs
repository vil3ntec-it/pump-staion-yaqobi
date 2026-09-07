using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record RetailReportInput(
    string MonthLabel,
    IReadOnlyList<RetailRow> Rows,
    string Dates);

/// <summary>
/// ══ سندِ چکنه ══════════════════════════════════════════════════════════════
/// بازسازیِ ‎pdfChakana(monthKey)‎: سربرگِ بنفش و هشت ستون با ردیفِ «جمله».
///
/// ⚠️ ردیفِ «به پول» لیتر ندارد و بردگی‌اش همان عددِ نوشته‌شده است، نه لیتر×فی —
/// همان قاعده‌ای که ‎RetailService‎ نگه می‌دارد و ورق هم از خودِ آن می‌پرسد.
/// </summary>
public sealed class RetailReport : IDocument
{
    private const string Purple = "#805ad5";

    private readonly RetailReportInput _in;
    private readonly RetailService _calc;

    public RetailReport(RetailReportInput input, RetailService calc) { _in = input; _calc = calc; }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "🧾 پمپ یعقوبی — حساب‌های چکنه " + _in.MonthLabel,
                         null, _in.Dates, Body, titleColor: Purple);

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private void Body(IContainer c) => c.Column(col =>
    {
        var s = _calc.Summarize(_in.Rows);

        col.Item().Row(row =>
        {
            void Box(string l, string v, string cl, bool last = false)
            {
                var it = last ? row.RelativeItem() : row.RelativeItem().PaddingLeft(6);
                it.Element(x => DocStyle.SumBox(x, l, v, cl));
            }
            Box("جمله لیتر", R(s.Liters) + " لیتر", DocStyle.Fuel);
            Box("جمله بردگی", R(s.Bardagi), DocStyle.Hawala);
            Box("جمله رسید", R(s.Rasid), DocStyle.Money);
            Box("الباقی", R(s.Albaqi), s.Albaqi > 0m ? DocStyle.Danger : DocStyle.Money, last: true);
        });

        col.Item().PaddingTop(8).Element(x => Table(x, s));
    });

    private void Table(IContainer c, RetailSummary s) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.7f);   // شماره
            cd.RelativeColumn(1.5f);   // تاریخ
            cd.RelativeColumn(2.4f);   // نام
            cd.RelativeColumn(1.2f);   // نوع تیل
            cd.RelativeColumn(1.2f);   // مقدار تیل
            cd.RelativeColumn(1.0f);   // فی
            cd.RelativeColumn(1.4f);   // مقدار بردگی
            cd.RelativeColumn(1.3f);   // رسید
            cd.RelativeColumn(1.3f);   // الباقی
        });

        t.Header(h =>
        {
            void Th(string s2) => DocStyle.ThText(h.Cell(), s2);
            Th("شماره"); Th("تاریخ"); Th("نام"); Th("نوع تیل"); Th("مقدار تیل");
            Th("فی"); Th("مقدار بردگی"); Th("رسید"); Th("الباقی");
        });

        var i = 0;
        foreach (var e in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            var bard = _calc.Bardagi(e);
            var alb = _calc.Albaqi(e);
            void Td(string s2, string? cl = null) => DocStyle.TdText(t.Cell(), even, s2, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(e.DateShamsi), DocStyle.Hawala);
            Td(DocStyle.Dash(e.Name));
            Td(e.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول");
            // ردیفِ پولی لیتر و فی ندارد — «—» می‌ماند، نه صفر
            Td(e.ByMoney ? "—" : R(e.Liters), DocStyle.Fuel);
            Td(e.ByMoney ? "—" : R(e.PricePerLiter));
            Td(R(bard), DocStyle.Hawala);
            Td(R(e.Rasid), DocStyle.Money);
            Td(R(alb), alb > 0m ? DocStyle.Danger : DocStyle.Money);
        }

        t.Cell().ColumnSpan(6).Element(x => DocStyle.Tf(x, "جمله"));
        t.Cell().Element(x => DocStyle.Tf(x, R(s.Bardagi)));
        t.Cell().Element(x => DocStyle.Tf(x, R(s.Rasid)));
        t.Cell().Element(x => DocStyle.Tf(x, R(s.Albaqi)));
    });
}
