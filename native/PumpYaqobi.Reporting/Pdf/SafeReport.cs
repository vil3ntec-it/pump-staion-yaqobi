using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

public sealed record SafeReportInput(
    string MonthLabel,
    IReadOnlyList<SafeEntry> Rows,
    string Dates);

/// <summary>
/// ══ سندِ گاوصندوق ══════════════════════════════════════════════════════════
/// بازسازیِ ‎printSafe(monthKey)‎: سربرگِ آبی، سه کادرِ «جمله بردگی»،
/// «جمله ماندگی» و «موجودی خالص»، و همان شش ستون.
///
/// ⚠️ دو ارز هرگز با هم جمع نمی‌شوند — هر ارز خطِ خودش را دارد، مثلِ
/// ‎safeAmtHtml‎ در نسخهٔ وب. کادری که فقط یک ارز دارد فقط یک خط نشان می‌دهد،
/// نه «۰ دالر»ی که کاربر را به اشتباه بیندازد.
/// </summary>
public sealed class SafeReport : ISetupDocument
{
    private const string Blue = "#2b6cb0";

    private readonly SafeReportInput _in;
    private readonly SafeService _calc;

    public SafeReport(SafeReportInput input, SafeService calc) { _in = input; _calc = calc; }

    /// <summary>تنظیمِ ورق — از «کارگاه چاپ». پیش‌فرض همان ورقی است که همیشه بود.</summary>
    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container,
            "🏦 پمپ یعقوبی — گاوصندوق " + _in.MonthLabel, null, _in.Dates, Body,
            titleColor: Blue, setup: Setup);

    /// <summary>«12,000 افغانی» و «$ 300 دالر» — هر ارز یک خط، هیچ‌کدام صفرِ الکی.</summary>
    private static string Pair(MoneyPair p)
    {
        var parts = new List<string>(2);
        if (p.Afn != 0m) parts.Add(PersianText.Num(p.Afn) + " افغانی");
        if (p.Usd != 0m) parts.Add("$ " + PersianText.Num(p.Usd, 2) + " دالر");
        return parts.Count == 0 ? "—" : string.Join("  ·  ", parts);
    }

    private void Body(IContainer c) => c.Column(col =>
    {
        var s = _calc.Summarize(_in.Rows);
        var ok = s.Net.Afn >= 0m && s.Net.Usd >= 0m;

        col.Item().Row(row =>
        {
            row.RelativeItem().PaddingLeft(6)
               .Element(x => DocStyle.SumBox(x, "💵 جمله بردگی", Pair(s.Bardagi), DocStyle.Money));
            row.RelativeItem().PaddingLeft(6)
               .Element(x => DocStyle.SumBox(x, "🏛️ جمله ماندگی", Pair(s.Mandagi), Blue));
            row.RelativeItem()
               .Element(x => DocStyle.SumBox(x, "🏦 موجودی خالص", Pair(s.Net),
                                             ok ? DocStyle.Money : DocStyle.Danger));
        });

        col.Item().PaddingTop(8).Element(x => Table(x, s));
    });

    private void Table(IContainer c, SafeSummary s) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);   // #
            cd.RelativeColumn(1.5f);   // تاریخ شمسی
            cd.RelativeColumn(1.3f);   // نوع
            cd.RelativeColumn(2.4f);   // نام
            cd.RelativeColumn(2.0f);   // مبلغ
            cd.RelativeColumn(2.2f);   // یادداشت
        });

        t.Header(h =>
        {
            void Th(string s2) => DocStyle.ThText(h.Cell(), s2);
            Th("#"); Th("تاریخ شمسی"); Th("نوع"); Th("نام"); Th("مبلغ"); Th("یادداشت");
        });

        var i = 0;
        foreach (var e in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            var bardagi = e.Kind == SafeEntryKind.Bardagi;
            var color = bardagi ? DocStyle.Money : Blue;
            var amount = e.Currency == Currency.Usd
                ? "$" + PersianText.Num(e.Amount, 2) + " دالر"
                : PersianText.Num(e.Amount) + " افغانی";

            void Td(string s2, string? cl = null) => DocStyle.TdText(t.Cell(), even, s2, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(e.DateShamsi), DocStyle.Hawala);
            Td(bardagi ? "💵 بردگی" : "🏛️ ماندگی", color);
            Td(DocStyle.Dash(e.Title));
            Td(amount, color);
            Td(DocStyle.Dash(e.Note), DocStyle.FootFg);
        }

        // ردیفِ «جمله» — همان نوارِ تیرهٔ پایینِ سند
        var okNet = s.Net.Afn >= 0m && s.Net.Usd >= 0m;
        t.Cell().ColumnSpan(4).Element(x => DocStyle.Tf(x, (okNet ? "💵" : "📉") + " موجودی خالص"));
        t.Cell().Element(x => DocStyle.Tf(x, Pair(s.Net)));
        t.Cell().Element(x => DocStyle.Tf(x, ""));
    });
}
