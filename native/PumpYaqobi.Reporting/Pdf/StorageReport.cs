using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record StorageReportInput(
    FuelType Fuel,
    IReadOnlyList<FuelPurchase> Purchases,
    TankState Tank,
    string Dates);

/// <summary>
/// ══ سندِ مخزن ══════════════════════════════════════════════════════════════
/// بازسازیِ ‎pdfStorage(fuelType)‎: کارتِ بزرگِ «موجودی فعلی مخزن»، پنج کادرِ
/// خلاصه، و جدولِ دوازده‌ستونهٔ تاریخچهٔ خریدها با ردیفِ «جمله».
///
/// ⚠️ ورق عمداً کم‌جوهر است: فقط سرستون و ردیفِ جمله تیره‌اند و بقیهٔ ورق سفیدِ
/// ساده می‌ماند — همان چیزی که صاحب ریپو خواست («ورقِ تمام‌سیاه رنگِ زیادی
/// مصرف می‌کرد»).
/// </summary>
public sealed class StorageReport : IDocument
{
    private readonly StorageReportInput _in;
    private readonly StorageService _calc;

    public StorageReport(StorageReportInput input, StorageService calc)
    { _in = input; _calc = calc; }

    private bool IsPetrol => _in.Fuel != FuelType.Diesel;
    private string Color => IsPetrol ? DocStyle.Petrol : "#92400e";
    private string Label => IsPetrol ? "⛽ پطرول" : "🟤 دیزل";

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container,
            "🛢️ پمپ یعقوبی — مخزن " + Label, "گزارش موجودی و تاریخچهٔ خریدها",
            _in.Dates, Body, landscape: true, titleColor: Color);

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private void Body(IContainer c) => c.Column(col =>
    {
        var t = _in.Tank;
        var liters = _in.Purchases.Sum(p => _calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).Liters);
        var afn = _in.Purchases.Sum(p => _calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).TotalAfn);
        var usd = _in.Purchases.Sum(p => _calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate).TotalUsd);
        var avg = liters > 0m ? afn / liters : 0m;

        // ── کارتِ موجودی ──────────────────────────────────────────────────
        col.Item().Border(1.5f).BorderColor(Color).Padding(12).Row(row =>
        {
            row.RelativeItem().Column(m =>
            {
                m.Item().AlignCenter().Text("موجودی فعلی مخزن")
                 .FontSize(DocStyle.SubSize).FontColor(DocStyle.Sub);
                m.Item().AlignCenter().Text(R(t.Display))
                 .FontSize(26).Bold().FontColor(Color);
                m.Item().AlignCenter().Text("لیتر")
                 .FontSize(DocStyle.SubSize).FontColor(DocStyle.Sub);
            });
            row.ConstantItem(150).AlignMiddle().Column(m =>
            {
                m.Item().AlignCenter().Text("جمله ورودی")
                 .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
                m.Item().AlignCenter().Text(R(t.In) + " لیتر")
                 .FontSize(DocStyle.BoxValue).Bold().FontColor("#0f7a4d");
            });
            row.ConstantItem(150).AlignMiddle().Column(m =>
            {
                m.Item().AlignCenter().Text("جمله فروش")
                 .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
                m.Item().AlignCenter().Text(R(t.Out) + " لیتر")
                 .FontSize(DocStyle.BoxValue).Bold().FontColor("#b3261e");
            });
        });

        // ── پنج کادرِ خلاصه ───────────────────────────────────────────────
        col.Item().PaddingTop(8).Row(row =>
        {
            void Box(string l, string v, string cl, bool last = false)
            {
                var it = last ? row.RelativeItem() : row.RelativeItem().PaddingLeft(6);
                it.Element(x => DocStyle.SumBox(x, l, v, cl));
            }
            Box("تعداد خریدها", PersianText.Num(_in.Purchases.Count), DocStyle.FootFg);
            Box("جمله لیتر خریداری", R(liters) + " لیتر", "#1a4f9c");
            Box("فی متوسط / لیتر", R(avg) + " افغ", Color);
            Box("جمله هزینه (افغانی)", R(afn), "#0f7a4d");
            Box("جمله هزینه (دالر)", PersianText.Num(usd, 1) + " $", "#5b3fb5", last: true);
        });

        col.Item().PaddingTop(8).Text("📋 تاریخچه خریدها")
           .FontSize(DocStyle.HeadSize + 1).Bold().FontColor(Color);

        if (_in.Purchases.Count == 0)
        {
            col.Item().PaddingTop(6).Border(1).BorderColor(DocStyle.BoxLine).Padding(16)
               .AlignCenter().Text("هیچ خریدی ثبت نشده")
               .FontSize(DocStyle.CellSize).FontColor(DocStyle.FootFg);
            return;
        }

        col.Item().PaddingTop(6).Element(x => Table(x, liters, afn, usd, avg));
    });

    private void Table(IContainer c, decimal liters, decimal afn, decimal usd, decimal avg) =>
        c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.5f);   // #
            cd.RelativeColumn(1.3f);   // تاریخ
            cd.RelativeColumn(2.0f);   // فروشنده
            cd.RelativeColumn(1.1f);   // وزن (کگ)
            cd.RelativeColumn(1.0f);   // تن
            cd.RelativeColumn(0.9f);   // تقلت
            cd.RelativeColumn(1.1f);   // لیتر
            cd.RelativeColumn(1.1f);   // فی تن ($)
            cd.RelativeColumn(1.1f);   // نرخ دالر
            cd.RelativeColumn(1.1f);   // کل ($)
            cd.RelativeColumn(1.3f);   // کل (افغانی)
            cd.RelativeColumn(1.2f);   // فی لیتر (افغ)
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("#"); Th("تاریخ"); Th("فروشنده"); Th("وزن (کگ)"); Th("تن"); Th("تقلت");
            Th("لیتر"); Th("فی تن ($)"); Th("نرخ دالر"); Th("کل ($)");
            Th("کل (افغانی)"); Th("فی لیتر (افغ)");
        });

        var i = 0;
        foreach (var p in _in.Purchases)
        {
            var even = i % 2 == 1;
            var n = ++i;
            var num = _calc.Compute(p.Kg, p.Density, p.PriceTon, p.UsdRate);
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(p.DateShamsi), DocStyle.Hawala);
            Td(DocStyle.Dash(p.Seller));
            Td(PersianText.Num(p.Kg));
            Td(PersianText.Ton(num.Ton));
            Td(PersianText.Num(p.Density));
            Td(R(num.Liters), DocStyle.Fuel);
            Td(PersianText.Num(p.PriceTon));
            Td(PersianText.Num(p.UsdRate));
            Td(PersianText.Num(num.TotalUsd, 1));
            Td(R(num.TotalAfn), DocStyle.Money);
            Td(PersianText.Num(num.PerLiter, 1), Color);

            // یادداشتِ خرید، اگر باشد، یک ردیفِ تمام‌عرضِ زیرِ همان خرید است
            if (!string.IsNullOrWhiteSpace(p.Note))
            {
                t.Cell().Element(x => DocStyle.TdText(x, true, ""));
                t.Cell().ColumnSpan(11)
                 .Element(x => DocStyle.TdText(x, true, "📝 " + p.Note, DocStyle.Sub));
            }
        }

        t.Cell().ColumnSpan(3).Element(x =>
            DocStyle.Tf(x, "جمله (" + PersianText.Num(_in.Purchases.Count) + " خرید)"));
        t.Cell().Element(x => DocStyle.Tf(x, "—"));
        t.Cell().Element(x => DocStyle.Tf(x, "—"));
        t.Cell().Element(x => DocStyle.Tf(x, "—"));
        t.Cell().Element(x => DocStyle.Tf(x, R(liters)));
        t.Cell().Element(x => DocStyle.Tf(x, "—"));
        t.Cell().Element(x => DocStyle.Tf(x, "—"));
        t.Cell().Element(x => DocStyle.Tf(x, PersianText.Num(usd, 1)));
        t.Cell().Element(x => DocStyle.Tf(x, R(afn)));
        t.Cell().Element(x => DocStyle.Tf(x, R(avg)));
    });
}
