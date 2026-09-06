using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record StatementHeader(string StationName, string? Address, string? Phone, string PrintedOn);

/// <summary>
/// ══ صورتِ حسابِ قرض‌دار — PDF بومی ═════════════════════════════════════════
/// همان چیزی که نسخهٔ HTML با پنجرهٔ چاپ می‌ساخت، این‌بار مستقیم و بدونِ مرورگر.
/// A4 · راست‌به‌چپ · فارسی · شمارهٔ صفحه · جمع‌ها در پاورقیِ جدول.
/// </summary>
public sealed class DebtorStatementReport : IDocument
{
    private readonly Debtor _p;
    private readonly DebtAccount _acc;
    private readonly StatementHeader _h;
    private readonly DebtCalculationService _calc;

    public DebtorStatementReport(Debtor p, DebtAccount acc, StatementHeader h, DebtCalculationService calc)
    { _p = p; _acc = acc; _h = h; _calc = calc; }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"صورت حساب — {_p.Name}",
        Author = _h.StationName,
    };

    public void Compose(IDocumentContainer doc)
    {
        var rows = _acc.ActiveRows();
        var money = _acc.Mode.IsMoney();
        var t = _calc.SplitTotals(rows);

        doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily(PdfEngine.Font).FontSize(9));
            page.ContentFromRightToLeft();               // کلِ سند راست‌به‌چپ

            page.Header().Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(_h.StationName).Bold().FontSize(15);
                        if (!string.IsNullOrWhiteSpace(_h.Address)) c.Item().Text(_h.Address!).FontSize(8).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(_h.Phone)) c.Item().Text("تماس: " + PersianText.ToFa(_h.Phone!)).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    r.ConstantItem(180).AlignLeft().Column(c =>
                    {
                        c.Item().AlignLeft().Text("صورتِ حساب").Bold().FontSize(13);
                        c.Item().AlignLeft().Text(PersianText.ToFa(_h.PrintedOn)).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
                col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                col.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Text(txt =>
                    {
                        txt.Span("حساب: ").SemiBold();
                        txt.Span(_acc.IsMain ? _p.Name : (_acc.Name ?? "حساب فرعی"));
                    });
                    r.ConstantItem(150).Text(txt =>
                    {
                        txt.Span("واحد: ").SemiBold();
                        txt.Span(money ? "پول (افغانی)" : "تیل (لیتر)");
                    });
                });
            });

            page.Content().PaddingVertical(8).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(26);   // #
                    c.ConstantColumn(58);   // تاریخ
                    c.RelativeColumn(2);    // نام
                    c.ConstantColumn(50);   // نوع تیل
                    c.ConstantColumn(48);   // لیتر
                    c.ConstantColumn(44);   // فی
                    c.ConstantColumn(60);   // بردگی
                    c.ConstantColumn(58);   // رسید
                    c.ConstantColumn(58);   // الباقی
                });

                table.Header(h =>
                {
                    void Th(string s) => h.Cell().Background(Colors.Grey.Lighten2).Padding(4)
                                          .Text(s).Bold().FontSize(8.5f);
                    Th("#"); Th("تاریخ"); Th("نام"); Th("نوع تیل");
                    Th(money ? "—" : "لیتر"); Th("فی"); Th("بردگی"); Th("رسید"); Th("الباقی");
                });

                int i = 1;
                foreach (var r in rows)
                {
                    var bg = i % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                    void Td(string s) => table.Cell().Background(bg).Padding(3).Text(s).FontSize(8.5f);
                    Td(PersianText.Num(i));
                    Td(PersianText.ToFa(r.DateShamsi ?? "—"));
                    Td(r.Name ?? "—");
                    Td(r.Fuel.ToPersian());
                    Td(money ? "—" : PersianText.Num(r.Liters, 2));
                    Td(r.PricePerLiter is > 0 ? PersianText.Num(r.PricePerLiter.Value) : "—");
                    Td(PersianText.Num(_calc.RowBardagi(r)));
                    Td(PersianText.Num(r.Rasid));
                    Td(PersianText.Num(r.Albaqi));
                    i++;
                }

                table.Footer(f =>
                {
                    void Tf(string s) =>
                        f.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(s).Bold().FontSize(8.5f);
                    Tf("جمع"); Tf(""); Tf(""); Tf("");
                    Tf(money ? "—" : PersianText.Num(t.All.Liters, 2));
                    Tf("");
                    Tf(PersianText.Num(t.All.Bardagi));
                    Tf(PersianText.Num(t.All.Rasid));
                    Tf(PersianText.Num(t.All.Albaqi));
                });
            });

            page.Footer().Row(r =>
            {
                r.RelativeItem().Text(t2 =>
                {
                    t2.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                    t2.Span("صفحهٔ ");
                    t2.CurrentPageNumber();
                    t2.Span(" از ");
                    t2.TotalPages();
                });
                r.ConstantItem(200).AlignLeft()
                 .Text(_h.StationName).FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }
}
