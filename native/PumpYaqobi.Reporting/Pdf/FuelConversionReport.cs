using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record FuelConversionReportInput(IReadOnlyList<FuelConversion> Rows, string Filter, string Dates);

/// <summary>
/// ══ سندِ تاریخچهٔ تبدیلِ تیل (۱۴۰۵/۰۷/۱۸) ════════════════════════════════════
/// هر تبدیل یک سطر با همهٔ جزئیاتِ مالی‌اش: تاریخ و ساعت، مبدأ ⇐ مقصد، لیتر و قیمتِ خریدِ
/// مبدأ، ارزشِ کل، قیمتِ مقصد و منبعش، لیترِ مجاز و تحویل‌شده، اختلاف، سود/ضرر و وضعیت.
/// ⛔ هیچ عددی این‌جا حساب نمی‌شود — همان عددهایی که لحظهٔ ثبت نوشته شدند.
/// </summary>
public sealed class FuelConversionReport : ISetupDocument
{
    private const string Head = "#7c3aed";
    private readonly FuelConversionReportInput _in;

    public FuelConversionReport(FuelConversionReportInput input) => _in = input;

    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    private static string R(decimal v) => PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));
    private static string L(decimal v) => PersianText.Num(Math.Round(v, 2));
    private static string F(FuelType f) => f == FuelType.Diesel ? "دیزل" : "پطرول";

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "🔁 " + PumpBrand.Name + " — تاریخچهٔ تبدیلِ تیل",
                         _in.Filter, _in.Dates, Body, titleColor: Head, setup: Setup);

    private void Body(IContainer c) => c.Column(col =>
    {
        var loss = _in.Rows.Where(r => r.ProfitLoss < 0).Sum(r => -r.ProfitLoss);
        var gain = _in.Rows.Where(r => r.ProfitLoss > 0).Sum(r => r.ProfitLoss);
        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "تعدادِ تبدیل", PersianText.Num(_in.Rows.Count), DocStyle.FootFg));
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "ارزشِ کلِ مبدأ", R(_in.Rows.Sum(r => r.FromValue)), DocStyle.Money));
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "🔴 ضررِ لیترِ اضافه", R(loss), DocStyle.Danger));
            row.RelativeItem().Element(x =>
                DocStyle.SumBox(x, "🟢 سودِ کمتر داده‌شده", R(gain), DocStyle.Money));
        });
        col.Item().PaddingTop(8).Element(Table);
    });

    private void Table(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.5f);  // #
            cd.RelativeColumn(1.6f);  // تاریخ و ساعت
            cd.RelativeColumn(1.5f);  // تبدیل
            cd.RelativeColumn(1.1f);  // لیترِ مبدأ
            cd.RelativeColumn(1.1f);  // قیمتِ مبدأ
            cd.RelativeColumn(1.4f);  // ارزش
            cd.RelativeColumn(1.6f);  // قیمتِ مقصد (منبع)
            cd.RelativeColumn(1.1f);  // مجاز
            cd.RelativeColumn(1.1f);  // تحویل
            cd.RelativeColumn(1.0f);  // اختلاف
            cd.RelativeColumn(1.2f);  // سود/ضرر
            cd.RelativeColumn(1.6f);  // وضعیت
        });
        DocStyle.Head(t, cell =>
        {
            void Th(string s) => DocStyle.ThText(cell(), s);
            Th("#"); Th("تاریخ و ساعت"); Th("تبدیل"); Th("لیترِ مبدأ"); Th("خریدِ هر لیترِ مبدأ");
            Th("ارزشِ مبدأ"); Th("خریدِ هر لیترِ مقصد"); Th("مجاز"); Th("تحویل‌شده"); Th("اختلاف");
            Th("سود / ضرر"); Th("وضعیت");
        });
        var i = 0;
        foreach (var r in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);
            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(r.DateShamsi) + " " + (r.TimeText ?? ""), DocStyle.Hawala);
            Td(F(r.FromFuel) + " ⇐ " + F(r.ToFuel));
            Td(L(r.Qty));
            Td(L(r.FromPrice) + (string.IsNullOrEmpty(r.FromPriceSource) ? "" : " (" + r.FromPriceSource + ")"));
            Td(R(r.FromValue), DocStyle.Money);
            Td(L(r.ToPrice) + (string.IsNullOrEmpty(r.ToPriceSource) ? "" : " (" + r.ToPriceSource + ")"));
            Td(L(r.AllowedLiters));
            Td(L(r.DeliveredLiters));
            Td(r.DiffLiters == 0 ? "—" : (r.DiffLiters > 0 ? "+" : "−") + L(Math.Abs(r.DiffLiters)),
               r.DiffLiters > 0 ? DocStyle.Danger : DocStyle.FootFg);
            Td(r.ProfitLoss == 0 ? "—" : (r.ProfitLoss > 0 ? "+" : "−") + R(Math.Abs(r.ProfitLoss)),
               r.ProfitLoss < 0 ? DocStyle.Danger : DocStyle.Money);
            Td(DocStyle.Dash(r.Status), r.DiffLiters > 0 ? DocStyle.Danger : null);
        }
        t.Cell().ColumnSpan(5).Element(x => DocStyle.Tf(x, "جمله"));
        t.Cell().Element(x => DocStyle.Tf(x, R(_in.Rows.Sum(r => r.FromValue))));
        t.Cell().ColumnSpan(4).Element(x => DocStyle.Tf(x, ""));
        var pl = _in.Rows.Sum(r => r.ProfitLoss);
        t.Cell().Element(x => DocStyle.Tf(x, pl == 0 ? "—" : (pl > 0 ? "+" : "−") + R(Math.Abs(pl))));
        t.Cell().Element(x => DocStyle.Tf(x, ""));
    });
}
