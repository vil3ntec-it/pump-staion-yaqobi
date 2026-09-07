using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="Fuel">
/// ‎null‎ یعنی «هر دو سوخت با هم» — آن‌وقت ستونِ «نوع تیل» هم می‌آید، دقیقاً
/// مثلِ ‎pdfCompany('all')‎.
/// </param>
public sealed record CompanyReportInput(
    TilCompany Company,
    FuelType? Fuel,
    IReadOnlyList<CompanyRow> Rows,
    string Dates);

/// <summary>
/// ══ سندِ حسابِ شرکتِ تیل ══════════════════════════════════════════════════════
/// بازسازیِ ‎pdfCompany(fuelMode)‎: شش کادرِ خلاصه و جدولِ خریدها با ردیفِ «جمله».
///
/// ⚠️ در هر ردیف «الباقیِ دالری × نرخِ همان ردیف = الباقیِ افغانی» برقرار است،
/// چون هر دو سمت از یک نرخ حساب می‌شوند. این همان چیزی است که یک‌بار خراب شد و
/// صاحب ریپو گزارشش کرد: «چرا الباقیِ دالر با افغانی برابر نیست؟» — روی جمعِ کل
/// این تساوی لازم نیست برقرار باشد، چون ردیف‌ها نرخ‌های متفاوت دارند و نرخِ
/// سربرگ یک نرخِ میانگین (یا نرخِ دستیِ شرکت) است.
/// </summary>
public sealed class CompanyReport : IDocument
{
    private const string Purple = "#805ad5";
    private const string BoxBg = "#faf5ff";

    private readonly CompanyReportInput _in;
    private readonly CompanyService _calc;

    public CompanyReport(CompanyReportInput input, CompanyService calc)
    { _in = input; _calc = calc; }

    private bool BothFuels => _in.Fuel is null;

    private string FuelLabel => _in.Fuel switch
    {
        FuelType.Petrol => "⛽ پطرول",
        FuelType.Diesel => "🟤 دیزل",
        _ => "⛽ پطرول + 🟤 دیزل",
    };

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container,
            "🛢️ " + DocStyle.Dash(_in.Company.Name) + " — حسابِ شرکتِ تیل (" + FuelLabel + ")",
            null, _in.Dates, Body, landscape: BothFuels, titleColor: Purple);

    private void Body(IContainer c) => c.Column(col =>
    {
        var s = _calc.Summarize(_in.Company, _in.Rows);
        var ton = _in.Rows.Sum(r => _calc.Ton(r));
        var owes = s.AlbaqiAfn > 0m;

        void Box(RowDescriptor row, string l, string v, string cl, bool last = false)
        {
            var it = last ? row.RelativeItem() : row.RelativeItem().PaddingLeft(6);
            it.Background(BoxBg).Element(x => DocStyle.SumBox(x, l, v, cl));
        }

        col.Item().Row(row =>
        {
            Box(row, "جمله مقدار",
                R(ton * 1000m) + " کیلو  (" + PersianText.Num(Math.Round(ton, 2), 2) + " تن)",
                DocStyle.Fuel);
            Box(row, "جمله کل دالر", PersianText.Num(s.TotalUsd, 1) + " $", DocStyle.Money);
            Box(row, "جمله کل (افغانی)", R(s.TotalAfn), Purple, last: true);
        });

        col.Item().PaddingTop(6).Row(row =>
        {
            Box(row, "رسید (افغانی)", R(s.PaidAfn), DocStyle.Money);
            Box(row, "رسید دالر", PersianText.Num(s.PaidUsd, 1) + " $", DocStyle.Money);
            Box(row, "جمله الباقی",
                R(s.AlbaqiAfn) + " AFN  ·  " + PersianText.Num(s.AlbaqiUsd, 1) + " $",
                owes ? DocStyle.Danger : DocStyle.Money, last: true);
        });

        col.Item().PaddingTop(8).Element(x => Table(x, s));
    });

    private void Table(IContainer c, CompanySummary s) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);                     // #
            cd.RelativeColumn(1.4f);                     // تاریخ
            cd.RelativeColumn(2.0f);                     // نام
            if (BothFuels) cd.RelativeColumn(1.2f);      // نوع تیل
            cd.RelativeColumn(1.3f);                     // خرید (کیلو)
            cd.RelativeColumn(1.3f);                     // قیمت تن ($)
            cd.RelativeColumn(1.3f);                     // کل ($)
            cd.RelativeColumn(0.9f);                     // نرخ
            cd.RelativeColumn(1.5f);                     // کل (افغانی)
            cd.RelativeColumn(1.5f);                     // رسید (افغانی)
            cd.RelativeColumn(1.7f);                     // الباقی (افغانی)
        });

        t.Header(h =>
        {
            void Th(string s2) => DocStyle.ThText(h.Cell(), s2);
            Th("#"); Th("تاریخ"); Th("نام");
            if (BothFuels) Th("نوع تیل");
            Th("خرید (کیلو)"); Th("قیمت تن ($)"); Th("کل ($)"); Th("نرخ");
            Th("کل (افغانی)"); Th("رسید (افغانی)"); Th("الباقی (افغانی)");
        });

        var i = 0;
        foreach (var r in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            var ton = _calc.Ton(r);
            var usd = _calc.TotalUsd(r);
            var afn = _calc.TotalAfn(r);
            var alb = _calc.AlbaqiAfn(r, s.ConvRate);
            var albUsd = _calc.AlbaqiUsd(r, s.ConvRate);
            // عددِ کیلو همان چیزی است که کاربر نوشته؛ ردیف‌های کهنه فقط «تن» دارند
            var kg = r.Kg != 0m ? r.Kg : ton * 1000m;
            var diesel = r.Fuel == FuelType.Diesel;

            void Td(string s2, string? cl = null) => DocStyle.TdText(t.Cell(), even, s2, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(r.DateShamsi), DocStyle.Hawala);
            Td(DocStyle.Dash(r.Name));
            if (BothFuels)
                Td(diesel ? "🟤 دیزل" : "⛽ پطرول", diesel ? "#8B4513" : DocStyle.Hawala);
            Td(kg == 0m ? "—" : R(kg), DocStyle.Fuel);
            Td(PersianText.Num(r.Usd), DocStyle.Money);
            Td(PersianText.Num(usd, 1), DocStyle.Fuel);
            Td(PersianText.Num(r.Rate));
            Td(R(afn), Purple);
            Td(r.PoulCurrency == Currency.Usd
                   ? R(r.Poul) + " $"
                   : R(r.Poul), DocStyle.Money);
            Td(R(alb) + (usd != 0m ? "  (" + PersianText.Num(albUsd, 1) + " $)" : ""),
               alb > 0m ? DocStyle.Danger : DocStyle.Money);
        }

        var span = (uint)(BothFuels ? 4 : 3);
        t.Cell().ColumnSpan(span).Element(x => DocStyle.Tf(x, "جمله"));
        t.Cell().Element(x => DocStyle.Tf(x, R(_in.Rows.Sum(r => _calc.Ton(r)) * 1000m)));
        t.Cell().Element(x => DocStyle.Tf(x, "—"));
        t.Cell().Element(x => DocStyle.Tf(x, PersianText.Num(s.TotalUsd, 1)));
        t.Cell().Element(x => DocStyle.Tf(x, "—"));
        t.Cell().Element(x => DocStyle.Tf(x, R(s.TotalAfn)));
        t.Cell().Element(x => DocStyle.Tf(x, R(s.PaidAfn)));
        t.Cell().Element(x => DocStyle.Tf(
            x, R(s.AlbaqiAfn) + "  (" + PersianText.Num(s.AlbaqiUsd, 1) + " $)"));
    });
}
