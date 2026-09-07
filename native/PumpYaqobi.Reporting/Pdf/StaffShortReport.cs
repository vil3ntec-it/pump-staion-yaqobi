using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record StaffShortReportInput(
    IReadOnlyList<StaffShortRow> Rows,
    IReadOnlyList<StaffShortSettle> Settles,
    string Dates);

/// <summary>
/// ══ سندِ کمبودی و اضافیِ کارمندان ════════════════════════════════════════════
/// همان جدولِ ‎_renderStaffShortPanel‎ روی کاغذ: کارمند · شیفت · 🔴 کمبودیِ
/// مانده · 🟢 اضافیِ مانده، با ردیفِ «جمله کل» — و زیرش فهرستِ تسویه‌ها.
///
/// ⚠️ ستونِ «عمل» (دکمهٔ تسویه) روی ورق نمی‌آید: کاغذ دکمه ندارد.
///
/// ⚠️ کمبودی و اضافی دو ستونِ جدا هستند و هرگز با هم جمع نمی‌شوند — یکی
/// بدهیِ کارمند به پمپ است و دیگری بدهیِ پمپ به کارمند.
/// </summary>
public sealed class StaffShortReport : ISetupDocument
{
    private const string Head = "#b45309";

    private readonly StaffShortReportInput _in;

    public StaffShortReport(StaffShortReportInput input) => _in = input;

    /// <summary>تنظیمِ ورق — از «کارگاه چاپ». پیش‌فرض همان ورقی است که همیشه بود.</summary>
    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private static string Pos(decimal v) => v > 0m ? R(v) : "—";

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "👷 پمپ یعقوبی — کمبودی و اضافیِ کارمندان",
                         "از ورق‌های روزانه حساب می‌شود؛ تسویه‌ها فقط از باقی‌مانده کم می‌کنند",
                         _in.Dates, Body, titleColor: Head, setup: Setup);

    private void Body(IContainer c) => c.Column(col =>
    {
        var totShort = _in.Rows.Sum(r => r.RemainShort);
        var totExcess = _in.Rows.Sum(r => r.RemainExcess);

        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "تعداد کارمندان", PersianText.Num(_in.Rows.Count), DocStyle.FootFg));
            row.RelativeItem().PaddingLeft(6).Element(x =>
                DocStyle.SumBox(x, "🔴 جمله کمبودیِ مانده", Pos(totShort), DocStyle.Danger));
            row.RelativeItem().Element(x =>
                DocStyle.SumBox(x, "🟢 جمله اضافیِ مانده", Pos(totExcess), DocStyle.Money));
        });

        col.Item().PaddingTop(8).Element(x => Table(x, totShort, totExcess));

        if (_in.Settles.Count > 0)
        {
            col.Item().PaddingTop(10).Text("🧾 رسیدها و پرداخت‌ها")
               .FontSize(DocStyle.HeadSize + 1).Bold().FontColor(Head);
            col.Item().PaddingTop(4).Element(Settles);
        }
    });

    private void Table(IContainer c, decimal totShort, decimal totExcess) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);   // #
            cd.RelativeColumn(3.0f);   // کارمند
            cd.RelativeColumn(1.2f);   // شیفت
            cd.RelativeColumn(2.0f);   // کمبودیِ مانده
            cd.RelativeColumn(2.0f);   // اضافیِ مانده
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("#"); Th("کارمند"); Th("شیفت"); Th("🔴 کمبودیِ مانده"); Th("🟢 اضافیِ مانده");
        });

        var i = 0;
        foreach (var r in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(r.Name));
            Td(PersianText.Num(r.Shifts));
            Td(Pos(r.RemainShort), r.RemainShort > 0m ? DocStyle.Danger : DocStyle.FootFg);
            Td(Pos(r.RemainExcess), r.RemainExcess > 0m ? DocStyle.Money : DocStyle.FootFg);
        }

        t.Cell().ColumnSpan(3).Element(x => DocStyle.Tf(x, "جمله کل"));
        t.Cell().Element(x => DocStyle.Tf(x, Pos(totShort)));
        t.Cell().Element(x => DocStyle.Tf(x, Pos(totExcess)));
    });

    private void Settles(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);   // #
            cd.RelativeColumn(1.5f);   // تاریخ
            cd.RelativeColumn(2.6f);   // کارمند
            cd.RelativeColumn(2.0f);   // نوع
            cd.RelativeColumn(1.6f);   // مبلغ
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("#"); Th("تاریخ"); Th("کارمند"); Th("نوع"); Th("مبلغ");
        });

        var i = 0;
        foreach (var s in _in.Settles)
        {
            var even = i % 2 == 1;
            var n = ++i;
            var excess = s.Kind == StaffSettleKind.Excess;
            void Td(string v, string? cl = null) => DocStyle.TdText(t.Cell(), even, v, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(s.DateShamsi), DocStyle.Hawala);
            Td(DocStyle.Dash(s.Name));
            Td(excess ? "💸 پرداخت اضافی" : "💵 رسید کمبودی",
               excess ? DocStyle.Money : DocStyle.Danger);
            Td(R(s.Amount), excess ? DocStyle.Money : DocStyle.Danger);
        }

        t.Cell().ColumnSpan(4).Element(x => DocStyle.Tf(x, "جمله"));
        t.Cell().Element(x => DocStyle.Tf(x, R(_in.Settles.Sum(s => s.Amount))));
    });
}
