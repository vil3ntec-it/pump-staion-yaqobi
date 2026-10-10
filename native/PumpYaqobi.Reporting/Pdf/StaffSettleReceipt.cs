using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="Settle">خودِ رسیدِ ثبت‌شده.</param>
/// <param name="Period">دوره‌ای که رسید برای آن است («میزان 1405» · «همهٔ ماه‌ها»).</param>
/// <param name="Lines">ورق‌هایی که کمبودی/اضافیِ همین کارمند در همان دوره از آن‌ها آمده.</param>
public sealed record StaffSettleReceiptInput(StaffShortSettle Settle, string Period,
                                             IReadOnlyList<StaffShortLine> Lines, string Dates);

/// <summary>
/// ══ رسیدِ یک تسویهٔ کمبودی/اضافی (۱۴۰۵/۰۷/۱۸) ══════════════════════════════════
/// گزارشِ صاحب ریپو: «دکمه‌های رسیدِ کمبودی را اصلاح کن تا رسیدها با اطلاعاتِ دقیق و
/// مرتبط تولید و چاپ شوند.» تا امروز هیچ رسیدِ چاپی‌ای نبود. حالا: شمارهٔ رسید، تاریخ،
/// کارمند، دوره، مبلغ، ماندهٔ پیش و پس، و هر ورقی که کمبودی از آن آمده با دلیلش.
/// ⛔ هیچ عددی این‌جا ساخته نمی‌شود — همان ردیفِ ذخیره‌شده و همان سطرهای ‎StaffShortService‎.
/// </summary>
public sealed class StaffSettleReceipt : ISetupDocument
{
    private const string Head = "#b45309";
    private readonly StaffSettleReceiptInput _in;

    public StaffSettleReceipt(StaffSettleReceiptInput input) => _in = input;

    public PageSetup Setup { get; set; } = PageSetup.Default;
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    private static string R(decimal v) => PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private bool Excess => _in.Settle.Kind == StaffSettleKind.Excess;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container,
            (Excess ? "💸 رسیدِ پرداختِ اضافی — " : "🧾 رسیدِ کمبودی — ") + PumpBrand.Name,
            "رسید شمارهٔ " + PersianText.Num(_in.Settle.Id) + " · دوره: " + _in.Period,
            _in.Dates, Body, titleColor: Head, setup: Setup);

    private void Body(IContainer c) => c.Column(col =>
    {
        var s = _in.Settle;
        var after = Math.Max(0m, s.RemainBefore - s.Amount);
        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Element(x => DocStyle.SumBox(x, "کارمند", DocStyle.Dash(s.Name), DocStyle.FootFg));
            row.RelativeItem().PaddingLeft(6).Element(x => DocStyle.SumBox(x, "تاریخ", DocStyle.Dash(s.DateShamsi), DocStyle.Hawala));
            row.RelativeItem().Element(x => DocStyle.SumBox(x, Excess ? "💸 پرداخت شد" : "💵 دریافت شد",
                                                             R(s.Amount) + " افغانی", Excess ? DocStyle.Money : DocStyle.Danger));
        });
        col.Item().PaddingTop(6).Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Element(x => DocStyle.SumBox(x,
                Excess ? "اضافیِ مانده پیش از این رسید" : "کمبودیِ مانده پیش از این رسید", R(s.RemainBefore), DocStyle.FootFg));
            row.RelativeItem().Element(x => DocStyle.SumBox(x,
                Excess ? "اضافیِ مانده پس از این رسید" : "کمبودیِ مانده پس از این رسید", R(after),
                after > 0 ? DocStyle.Danger : DocStyle.Money));
        });

        var mine = _in.Lines.Where(l => l.Key == s.NameKey && (Excess ? l.Excess > 0 : l.Shortage > 0)).ToList();
        col.Item().PaddingTop(10).Text(Excess ? "از کجا آمد — ورق‌هایی که اضافی داشتند" : "از کجا آمد — ورق‌هایی که کمبودی داشتند")
           .FontSize(DocStyle.HeadSize + 1).Bold().FontColor(DocStyle.Ink(Head));
        col.Item().PaddingTop(4).Element(x => Lines(x, mine));

        col.Item().PaddingTop(24).Row(row =>
        {
            row.RelativeItem().AlignCenter().Text("امضای کارمند: ....................").FontSize(DocStyle.CellSize);
            row.RelativeItem().AlignCenter().Text("امضای مسئول: ....................").FontSize(DocStyle.CellSize);
        });
    });

    private void Lines(IContainer c, List<StaffShortLine> lines) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.5f);   // #
            cd.RelativeColumn(1.5f);   // تاریخ
            cd.RelativeColumn(0.9f);   // شیفت
            cd.RelativeColumn(1.6f);   // قرضِ اعلام‌شده
            cd.RelativeColumn(1.6f);   // ثبت‌شده
            cd.RelativeColumn(1.6f);   // مبلغ
        });
        DocStyle.Head(t, cell =>
        {
            void Th(string x) => DocStyle.ThText(cell(), x);
            Th("#"); Th("تاریخِ ورق"); Th("شیفت"); Th("قرضِ اعلام‌شده (پارچه)");
            Th("قرض + مصرفِ ثبت‌شده در ورق"); Th(Excess ? "🟢 اضافی" : "🔴 کمبودی");
        });
        var i = 0;
        foreach (var l in lines)
        {
            var even = i % 2 == 1;
            var n = ++i;
            void Td(string x, string? cl = null) => DocStyle.TdText(t.Cell(), even, x, cl);
            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(l.DateShamsi), DocStyle.Hawala);
            Td(l.Kind == ShiftKind.Night ? "شب" : "روز");
            Td(R(l.Declared));
            Td(R(l.Debt + l.Expenses));
            Td(R(Excess ? l.Excess : l.Shortage), Excess ? DocStyle.Money : DocStyle.Danger);
        }
        if (lines.Count == 0)
        {
            t.Cell().ColumnSpan(6).Element(x => DocStyle.TdText(x, false, "در این دوره ورقی نیست"));
            return;
        }
        t.Cell().ColumnSpan(5).Element(x => DocStyle.Tf(x, "جمله"));
        t.Cell().Element(x => DocStyle.Tf(x, R(lines.Sum(l => Excess ? l.Excess : l.Shortage))));
    });
}
