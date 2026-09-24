using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="Fuel">‎null‎ یعنی هر دو تیل، پطرول اول.</param>
public sealed record CompanyPurchasesReportInput(
    string Title, FuelType? Fuel,
    IReadOnlyList<FuelPurchase> Petrol, IReadOnlyList<FuelPurchase> Diesel,
    IReadOnlyList<CompanyRow> ManualPetrol, IReadOnlyList<CompanyRow> ManualDiesel,
    string Dates);

/// <summary>
/// ══ 📦 خریدهای یک شرکت ═══════════════════════════════════════════════════
/// رونوشتِ ‎pdfCompanyPurchases()‎: خریدهای مخزنِ همان بازه (زنده یا جدولِ آرشیو)
/// و زیرش «ردیف‌های دستی جدول». مقدار فقط به تن — خواستهٔ صاحب ریپو: کیلو اضافه است.
/// </summary>
public sealed class CompanyPurchasesReport : ISetupDocument
{
    private const string Head = "#c07700";
    private const string DieselColor = "#b7791f";

    private readonly CompanyPurchasesReportInput _in;
    private readonly CompanyService _calc;

    public CompanyPurchasesReport(CompanyPurchasesReportInput input, CompanyService calc)
    { _in = input; _calc = calc; }

    public PageSetup Setup { get; set; } = PageSetup.Default;
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    private static string R0(decimal v) => PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));
    private static string Ton(decimal v) => PersianText.Num(Math.Round(v, 3), 3);

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "📦 پمپ یعقوبی — " + _in.Title, null, _in.Dates, Body,
                         landscape: true, titleColor: Head, setup: Setup);

    private void Body(IContainer c) => c.Column(col =>
    {
        var any = false;
        if (_in.Fuel != FuelType.Diesel) any |= Section(col, FuelType.Petrol, _in.Petrol, _in.ManualPetrol);
        if (_in.Fuel != FuelType.Petrol) any |= Section(col, FuelType.Diesel, _in.Diesel, _in.ManualDiesel);
        if (!any)
            col.Item().PaddingTop(8).Border(1).BorderColor(DocStyle.Edge(DocStyle.BoxLine)).Padding(16)
               .AlignCenter().Text("هیچ خریدی ثبت نشده").FontSize(DocStyle.CellSize).FontColor(DocStyle.Ink(DocStyle.FootFg));
    });

    private bool Section(ColumnDescriptor col, FuelType fuel, IReadOnlyList<FuelPurchase> entries, IReadOnlyList<CompanyRow> manual)
    {
        var shown = manual.Where(r => _calc.Ton(r) != 0m || _calc.TotalUsd(r) != 0m).ToList();
        if (entries.Count == 0 && shown.Count == 0) return false;
        var color = fuel == FuelType.Diesel ? DieselColor : Head;
        col.Item().PaddingTop(10).PaddingBottom(4)
           .Text(fuel == FuelType.Diesel ? "🟤 خریدهای دیزل" : "⛽ خریدهای پطرول")
           .FontSize(DocStyle.BoxValue).Bold().FontColor(DocStyle.Ink(color));

        if (entries.Count > 0)
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(0.5f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.6f);
                    cd.RelativeColumn(1.0f); cd.RelativeColumn(1.0f); cd.RelativeColumn(1.0f);
                    cd.RelativeColumn(0.9f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.3f); cd.RelativeColumn(1.1f);
                });
                DocStyle.Head(t, cell =>
                {
                    void Th(string s) => DocStyle.ThText(cell(), s);
                    Th("#"); Th("تاریخ"); Th("فروشنده"); Th("تن"); Th("لیتر"); Th("فی تن ($)");
                    Th("نرخ دالر"); Th("کل ($)"); Th("کل (افغانی)"); Th("فی لیتر");
                });
                decimal sL = 0, sU = 0, sA = 0, sT = 0;
                var i = 0;
                foreach (var e in entries)
                {
                    var even = i % 2 == 1; var n = ++i;
                    var ton = e.Ton != 0m ? e.Ton : e.Kg / 1000m;
                    sL += e.Liters; sU += e.TotalUsd; sA += e.TotalAfn; sT += ton;
                    void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);
                    Td(PersianText.Num(n), DocStyle.Index);
                    Td(DocStyle.Dash(e.DateShamsi));
                    Td(DocStyle.Dash(e.Seller));
                    Td(Ton(ton));
                    Td(R0(e.Liters));
                    Td(PersianText.Num(e.PriceTon));
                    Td(PersianText.Num(e.UsdRate));
                    Td(PersianText.Num(e.TotalUsd, 1) + " $", DocStyle.Money);
                    Td(R0(e.TotalAfn), color);
                    Td(PersianText.Num(e.PerLiter, 1));
                }
                t.Cell().ColumnSpan(3).Element(x => DocStyle.Tf(x, "جمله — " + PersianText.Num(entries.Count) + " خرید"));
                t.Cell().Element(x => DocStyle.Tf(x, Ton(sT)));
                t.Cell().Element(x => DocStyle.Tf(x, R0(sL)));
                t.Cell().ColumnSpan(2).Element(x => DocStyle.Tf(x, ""));
                t.Cell().Element(x => DocStyle.Tf(x, PersianText.Num(sU, 1) + " $"));
                t.Cell().Element(x => DocStyle.Tf(x, R0(sA)));
                t.Cell().Element(x => DocStyle.Tf(x, ""));
            });

        if (shown.Count > 0)
        {
            col.Item().PaddingTop(8).PaddingBottom(3).Text("ردیف‌های دستی جدول")
               .FontSize(DocStyle.BoxLabel).Bold().FontColor(DocStyle.Ink(DocStyle.Sub));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(0.5f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.8f);
                    cd.RelativeColumn(1.0f); cd.RelativeColumn(1.0f); cd.RelativeColumn(0.9f);
                    cd.RelativeColumn(1.2f); cd.RelativeColumn(1.3f);
                });
                DocStyle.Head(t, cell =>
                {
                    void Th(string s) => DocStyle.ThText(cell(), s);
                    Th("#"); Th("تاریخ"); Th("نام"); Th("مقدار (تن)"); Th("فی تن ($)"); Th("نرخ"); Th("کل ($)"); Th("کل (افغانی)");
                });
                decimal sT = 0, sU = 0, sA = 0;
                var i = 0;
                foreach (var r in shown)
                {
                    var even = i % 2 == 1; var n = ++i;
                    var ton = _calc.Ton(r); var tu = _calc.TotalUsd(r); var ta = _calc.TotalAfn(r);
                    sT += ton; sU += tu; sA += ta;
                    void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);
                    Td(PersianText.Num(n), DocStyle.Index);
                    Td(DocStyle.Dash(r.DateShamsi));
                    Td(DocStyle.Dash(r.Name));
                    Td(Ton(ton));
                    Td(PersianText.Num(r.Usd));
                    Td(PersianText.Num(r.Rate));
                    Td(PersianText.Num(tu, 1) + " $", DocStyle.Money);
                    Td(R0(ta), color);
                }
                t.Cell().ColumnSpan(3).Element(x => DocStyle.Tf(x, "جمله"));
                t.Cell().Element(x => DocStyle.Tf(x, Ton(sT)));
                t.Cell().ColumnSpan(2).Element(x => DocStyle.Tf(x, ""));
                t.Cell().Element(x => DocStyle.Tf(x, PersianText.Num(sU, 1) + " $"));
                t.Cell().Element(x => DocStyle.Tf(x, R0(sA)));
            });
        }
        return true;
    }
}
