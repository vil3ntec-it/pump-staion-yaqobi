using PumpYaqobi.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="Filter">
/// کدام واحد. در حالتِ <see cref="AgingFilter.All"/> واحدها قاطی‌اند، پس ستونِ
/// «واحد» اضافه می‌شود و جمعِ کل عمداً «—» می‌ماند — لیتر و افغانی با هم جمع
/// نمی‌شوند.
/// </param>
public sealed record OldLoansReportInput(
    AgingFilter Filter,
    IReadOnlyList<AgingRow> Rows,
    string Dates);

/// <summary>
/// ══ سندِ قرض‌های کهنه ═══════════════════════════════════════════════════════
/// بازسازیِ ‎pdfOldLoans(filter)‎: چهار کادرِ خلاصه و جدولی که بی‌حرکت‌ترین
/// قرض‌داران را اول می‌آورد.
/// </summary>
public sealed class OldLoansReport : IDocument
{
    private readonly OldLoansReportInput _in;

    public OldLoansReport(OldLoansReportInput input) => _in = input;

    private bool IsMoney => _in.Filter == AgingFilter.Money;
    private bool IsMixed => _in.Filter == AgingFilter.All;
    private string Unit => IsMoney ? "افغانی" : "لیتر";
    private string Color => IsMoney ? "#38a169" : DocStyle.Danger;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container,
            (IsMixed ? "📋" : IsMoney ? "💵" : "⏰") + " پمپ یعقوبی — قرض‌های کهنهٔ "
            + (IsMixed ? "همه" : IsMoney ? "پول (واحد پول)" : "تیل (واحد تیل)"),
            "به ترتیبِ «چند روز است هیچ ردیف تازه‌ای ندارند» — بی‌حرکت‌ها اول",
            _in.Dates, Body, titleColor: IsMixed ? DocStyle.Title : Color);

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    /// <summary>جمعِ کل فقط وقتی معنی دارد که همهٔ ردیف‌ها یک واحد داشته باشند.</summary>
    private string TotalText =>
        IsMixed ? "—" : R(_in.Rows.Sum(r => r.Figures.Albaqi)) + " " + Unit;

    private void Body(IContainer c) => c.Column(col =>
    {
        var stale30 = _in.Rows.Count(r => r.DaysIdle >= 30);
        var stale60 = _in.Rows.Count(r => r.DaysIdle >= 60);

        col.Item().Row(row =>
        {
            void Box(string l, string v, string cl, bool last = false)
            {
                var it = last ? row.RelativeItem() : row.RelativeItem().PaddingLeft(6);
                it.Element(x => DocStyle.SumBox(x, l, v, cl));
            }
            Box("تعداد بدهکاران", PersianText.Num(_in.Rows.Count), DocStyle.FootFg);
            Box(IsMixed ? "جمله الباقی" : "جمله الباقی (" + Unit + ")", TotalText, DocStyle.Danger);
            Box("بیشتر از 30 روز بی‌حرکت", PersianText.Num(stale30), DocStyle.Hawala);
            Box("بیشتر از 60 روز بی‌حرکت", PersianText.Num(stale60), DocStyle.Danger, last: true);
        });

        col.Item().PaddingTop(8).Element(Table);
    });

    private void Table(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);                    // #
            cd.RelativeColumn(3.0f);                    // نام قرض‌دار
            cd.RelativeColumn(1.8f);                    // الباقی
            if (IsMixed) cd.RelativeColumn(1.0f);       // واحد
            cd.RelativeColumn(1.6f);                    // بی‌حرکت
            cd.RelativeColumn(2.0f);                    // شماره تماس
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("#"); Th("نام قرض‌دار");
            Th(IsMixed ? "الباقی" : "الباقی (" + Unit + ")");
            if (IsMixed) Th("واحد");
            Th("بی‌حرکت"); Th("شماره تماس");
        });

        var i = 0;
        foreach (var r in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            var days = r.DaysIdle;
            var dc = days >= 60 ? DocStyle.Danger : days >= 30 ? DocStyle.Hawala : DocStyle.FootFg;
            var rowUnit = r.Figures.IsMoney ? "افغانی" : "لیتر";
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(r.Person.Name));
            Td(R(r.Figures.Albaqi) + (IsMoney || IsMixed ? "" : " لیتر"), DocStyle.Danger);
            if (IsMixed) Td(rowUnit, DocStyle.Sub);
            Td(days < 0 ? "بی‌تاریخ" : PersianText.Num(days) + " روز", dc);
            Td(DocStyle.Dash(r.Person.Phone));
        }

        t.Cell().ColumnSpan(2).Element(x => DocStyle.Tf(x, "جمله کل"));
        t.Cell().Element(x => DocStyle.Tf(x, TotalText));
        t.Cell().ColumnSpan((uint)(IsMixed ? 3 : 2)).Element(x => DocStyle.Tf(x, ""));
    });
}
