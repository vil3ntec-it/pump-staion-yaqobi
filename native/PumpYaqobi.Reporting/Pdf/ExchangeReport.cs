using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record ExchangeReportInput(
    string MonthLabel,
    IReadOnlyList<ExchangeRow> Rows,
    string Dates);

/// <summary>
/// ══ سندِ صرافی ═════════════════════════════════════════════════════════════
/// بازسازیِ ‎printSarrafi()‎: سربرگِ آبی، دو کادرِ «جمله دالر این ماه» و
/// «جمله بردگی این ماه»، نوارِ سبزِ «الباقی صرافی نزد پمپ» و همان ده ستون.
/// </summary>
public sealed class ExchangeReport : ISetupDocument
{
    private const string Blue = "#1e4f8a";
    private const string BoxBg = "#eef5fd";
    private const string GreenLine = "#38a169";

    private readonly ExchangeReportInput _in;
    private readonly ExchangeService _calc;

    public ExchangeReport(ExchangeReportInput input, ExchangeService calc)
    { _in = input; _calc = calc; }

    /// <summary>تنظیمِ ورق — از «کارگاه چاپ». پیش‌فرض همان ورقی است که همیشه بود.</summary>
    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "💱 صرافی — " + _in.MonthLabel, null, _in.Dates, Body,
                         titleColor: Blue, setup: Setup);

    private void Body(IContainer c) => c.Column(col =>
    {
        var s = _calc.Summarize(_in.Rows);

        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Background(BoxBg).Border(1).BorderColor(DocStyle.CellLine)
               .Padding(8).Column(b =>
            {
                b.Item().AlignCenter().Text("$ جمله دالر این ماه")
                 .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
                b.Item().PaddingTop(3).AlignCenter()
                 .Text("$ " + PersianText.Num(Math.Round(s.TotalUsd, 2)))
                 .FontSize(DocStyle.BoxValue + 2).Bold().FontColor(Blue);
            });
            row.RelativeItem().Background(BoxBg).Border(1).BorderColor(DocStyle.CellLine)
               .Padding(8).Column(b =>
            {
                b.Item().AlignCenter().Text("💵 جمله بردگی این ماه")
                 .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
                b.Item().PaddingTop(3).AlignCenter()
                 .Text("$ " + PersianText.Num(Math.Round(s.TotalBardagi, 2)))
                 .FontSize(DocStyle.BoxValue + 2).Bold().FontColor(DocStyle.Money);
            });
        });

        col.Item().PaddingTop(6).Border(1).BorderColor(GreenLine).Padding(9).AlignCenter()
           .Text("🟢 الباقی صرافی نزد پمپ: $ " + PersianText.Num(Math.Round(s.Baqi, 2)))
           .FontSize(DocStyle.BoxValue + 1).Bold().FontColor(DocStyle.Money);

        col.Item().PaddingTop(8).Element(Table);
    });

    private void Table(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            // ⚠️ ستون‌ها نسبی‌اند، نه ثابت: با عرضِ ثابت مجموعشان از عرضِ ورق
            // بیشتر می‌شد و ستونِ «توضیحات» تا یک نویسه له می‌شد.
            cd.RelativeColumn(0.6f);   // #
            cd.RelativeColumn(1.4f);   // تاریخ
            cd.RelativeColumn(2.4f);   // توضیحات
            cd.RelativeColumn(1.2f);   // مبلغ
            cd.RelativeColumn(1.0f);   // واحد
            cd.RelativeColumn(0.8f);   // فی
            cd.RelativeColumn(1.0f);   // دالر
            cd.RelativeColumn(1.6f);   // رسید به صرافی
            cd.RelativeColumn(1.8f);   // بردگی پمپ بنزین ($)
            cd.RelativeColumn(1.2f);   // الباقی ($)
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("#"); Th("تاریخ"); Th("توضیحات"); Th("مبلغ"); Th("واحد");
            Th("فی"); Th("دالر"); Th("رسید به صرافی"); Th("بردگی پمپ بنزین ($)"); Th("الباقی ($)");
        });

        var i = 0;
        foreach (var r in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            void Td(string s, string? color = null) => DocStyle.TdText(t.Cell(), even, s, color);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(r.DateShamsi), DocStyle.Hawala);
            Td(DocStyle.Dash(r.Description));
            Td(PersianText.Num(r.Amount));
            Td(r.Currency switch
            {
                ExchangeCurrency.Toman => "تومان",
                ExchangeCurrency.Kaldar => "کلدار",
                _ => "افغانی",
            });
            Td(PersianText.Num(r.Rate));
            Td(DocStyle.DashNum(Math.Round(_calc.ToUsd(r), 2)), DocStyle.Blue);
            Td("—", DocStyle.Money);
            Td(DocStyle.DashNum(r.Bardagi), DocStyle.Hawala);
            Td(DocStyle.DashNum(Math.Round(_calc.RowBaqi(r), 2)));
        }
    });
}
