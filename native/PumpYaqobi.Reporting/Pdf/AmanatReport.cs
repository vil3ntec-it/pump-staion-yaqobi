using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>یک ردیفِ امانت، با حسابِ آماده‌اش.</summary>
public sealed record AmanatReportRow(AmanatRow Row, AmanatRowCalc Calc);

/// <summary>یک حسابِ امانت — سربرگ، جمع‌ها و ردیف‌هایش.</summary>
public sealed record AmanatReportAccount(
    string Name, string FuelLabel, AmanatAccountCalc Totals,
    IReadOnlyList<AmanatReportRow> Rows);

/// <param name="ShowAccountHeads">
/// ‎printAmanat()‎ سربرگِ هر حساب را می‌آورد؛ ‎printAmanatAccount(i)‎ — که فقط
/// یک حساب است — نمی‌آورد، چون عنوانِ خودِ سند همان نام است.
/// </param>
public sealed record AmanatReportInput(
    string Title,
    AmanatSettings Settings,
    IReadOnlyList<AmanatReportAccount> Accounts,
    string Dates,
    bool ShowAccountHeads = true);

/// <summary>
/// ══ سندِ تیل امانت ══════════════════════════════════════════════════════════
/// بازسازیِ ‎printAmanat()‎ · ‎printAmanatAccount(i)‎ · ‎_amAccPdfBlock‎.
///
/// ⚠️ حساب‌ها **هیچ‌وقت با هم جمع نمی‌شوند**: هر محموله کمبودیِ خودش را دارد و
/// جمع زدنِ حساب‌های جدا عددِ بی‌معنی می‌داد. هر حساب جدولِ خودش را دارد، با
/// ردیفِ «جمله»ی خودش — همان چیدمانی که صاحب ریپو خواست.
///
/// ⚠️ نوارِ بالای سند عمداً هست: «درصد کمبودی برآوردِ محاسباتی است و جایگزینِ
/// اندازه‌گیریِ واقعی نمی‌شود». این جمله در نسخهٔ وب هم روی ورق چاپ می‌شد.
/// </summary>
public sealed class AmanatReport : IDocument
{
    private const string Head = "#0f766e";
    private const string Amber = "#b7791f";

    private readonly AmanatReportInput _in;

    public AmanatReport(AmanatReportInput input) => _in = input;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    /// <summary>‎_amFmt2‎ — همیشه دقیقاً دو رقمِ اعشار.</summary>
    private static string F2(decimal v) => PersianText.Num(Math.Round(v, 2), 2);

    /// <summary>
    /// ‎_amFmt(n, dec)‎ — تا ‎dec‎ رقمِ اعشار، ولی صفرهای انتهایی نمی‌آیند
    /// (‎minimumFractionDigits: 0‎). «0.02» است، نه «0.020».
    /// </summary>
    private static string F(decimal v, int d)
    {
        var r = Math.Round(v, d);
        var fmt = d <= 0 ? "#,##0" : "#,##0." + new string('#', d);
        return r.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    }
    private static string Opt2(decimal? v) => v is null ? "—" : F2(v.Value);
    private static string OptPct(decimal? v, int d) => v is null ? "—" : F(v.Value, d) + "٪";

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "🛢️ پمپ یعقوبی — " + _in.Title, null, _in.Dates, Body,
                         landscape: true, titleColor: Head);

    private void Body(IContainer c) => c.Column(col =>
    {
        var s = _in.Settings;

        col.Item().Border(1).BorderColor(DocStyle.BoxLine).Padding(6).Text(
            "درصد کمبودی — برآورد محاسباتی است و جایگزین اندازه‌گیری واقعی موجودی نمی‌شود. "
            + "پایه: " + F(s.BasePct, 3) + "٪ ماهانه · ضریب پطرول: " + F(s.FPetrol, 3)
            + " · ضریب دیزل: " + F(s.FDiesel, 3)
            + " · حاشیهٔ اطمینانِ فیصدی: " + F(s.SafetyPct, 2) + "٪")
           .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);

        if (_in.Accounts.Count == 0)
        {
            col.Item().PaddingTop(8).Border(1).BorderColor(DocStyle.BoxLine).Padding(16)
               .AlignCenter().Text("هیچ حسابی ثبت نشده")
               .FontSize(DocStyle.CellSize).FontColor(DocStyle.FootFg);
            return;
        }

        foreach (var a in _in.Accounts)
            col.Item().PaddingTop(9).Element(x => Account(x, a));
    });

    private void Account(IContainer c, AmanatReportAccount a) => c.Column(col =>
    {
        var t = a.Totals;

        if (_in.ShowAccountHeads)
        {
            col.Item().Text((string.IsNullOrWhiteSpace(a.Name) ? "حسابِ بی‌نام" : a.Name)
                            + " — " + a.FuelLabel)
               .FontSize(DocStyle.BoxValue).Bold().FontColor(Head);

            col.Item().PaddingTop(2).Text(
                "رسید کل " + F2(t.Liters) + " لیتر · برده شده " + F2(t.Taken) + " لیتر · کمبودی "
                + F2(t.Loss) + " لیتر"
                + (t.LossMoney is null ? "" : " (" + F(t.LossMoney.Value, 0) + " پول)")
                + " · باقی تیل " + F2(t.Rest) + " لیتر"
                + (t.HasActual
                    ? " · اندازه‌گیری واقعی " + F2(t.Actual) + " لیتر · اختلاف " + F2(t.RealDiff) + " لیتر"
                    : "")
                + " · " + (t.MyPct is null
                    ? "فیصدی ثبت نشده"
                    : "فیصدیِ شما (هدف) " + F(t.MyPct.Value, 2) + "٪ = " + Opt2(t.TargetL)
                      + " لیتر · فیصدیِ لازم " + OptPct(t.NeedPct, 3)
                      + " → گِردشده " + OptPct(t.AskPct, 1)
                      + " · با آن بعد از بخار " + Opt2(t.NetIfAsk) + " لیتر برایتان می‌ماند"))
               .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
        }

        col.Item().PaddingTop(4).Element(x => Table(x, a));
    });

    private void Table(IContainer c, AmanatReportAccount a) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.RelativeColumn(0.6f);   // #
            cd.RelativeColumn(1.4f);   // تاریخ
            cd.RelativeColumn(1.3f);   // نام
            cd.RelativeColumn(1.3f);   // به حسابهٔ
            cd.RelativeColumn(1.5f);   // رسید تیل
            cd.RelativeColumn(1.3f);   // برده شده
            cd.RelativeColumn(1.1f);   // مدت زمان
            cd.RelativeColumn(1.1f);   // درجه گرما
            cd.RelativeColumn(1.4f);   // تیلِ بخار
            cd.RelativeColumn(1.2f);   // فیصدیِ من
            cd.RelativeColumn(1.2f);   // فیصدیِ بخار
            cd.RelativeColumn(1.2f);   // فیصدیِ لازم
            cd.RelativeColumn(1.3f);   // سهمیهٔ من
            cd.RelativeColumn(1.3f);   // به من می‌رسد
            cd.RelativeColumn(1.4f);   // الباقیِ طرف
            cd.RelativeColumn(1.3f);   // موجودی واقعی
            cd.RelativeColumn(1.2f);   // اختلاف
        });

        // سربرگِ دوطبقه — همان گروه‌بندیِ ورقِ نسخهٔ وب
        t.Header(h =>
        {
            void Group(string s, uint span, uint rows = 1) =>
                DocStyle.ThText(h.Cell().ColumnSpan(span).RowSpan(rows), s);

            Group("#", 1, 2);
            Group("شناسه", 3);
            Group("🛢️ تیل (لیتر)", 2);
            Group("🌡️ شرایطِ نگهداری", 2);
            Group("تیلِ بخار (لیتر)", 1, 2);
            Group("٪ فیصدی‌ها", 3);
            Group("سهمِ من (لیتر)", 2);
            Group("الباقیِ طرف (لیتر)", 1, 2);
            Group("📏 اندازه‌گیری", 2);

            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("تاریخ"); Th("نام"); Th("به حسابهٔ");
            Th("رسید تیل"); Th("برده شده");
            Th("مدت زمان"); Th("درجه گرما");
            Th("فیصدیِ من"); Th("فیصدیِ بخار"); Th("فیصدیِ لازم");
            Th("سهمیهٔ من"); Th("به من می‌رسد");
            Th("موجودی واقعی"); Th("اختلاف");
        });

        var i = 0;
        foreach (var (row, x) in a.Rows.Select(r => (r.Row, r.Calc)))
        {
            var even = i % 2 == 1;
            var n = ++i;
            void Td(string s, string? cl = null) => DocStyle.TdText(t.Cell(), even, s, cl);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(row.DateShamsi));
            Td(DocStyle.Dash(row.Name));
            Td(DocStyle.Dash(row.ToAccount));
            Td(F2(x.Liters));
            Td(F2(x.Taken));
            Td(PersianText.Num(x.Days));
            Td(F(x.Temp, 2));
            Td(F2(x.Loss), Amber);
            Td(OptPct(x.MyPct, 2));
            Td(F(x.LossPct, 3) + "٪", Amber);
            Td(OptPct(x.NeedPct, 3), DocStyle.Blue);
            Td(Opt2(x.TargetL));
            Td(Opt2(x.NetIfMy));
            Td(F2(x.Rest), DocStyle.Money);
            Td(x.HasActual ? Opt2(x.Actual) : "—");
            Td(x.HasActual ? Opt2(x.Diff) : "—");
        }

        var tt = a.Totals;
        t.Cell().ColumnSpan(4).Element(x => DocStyle.Tf(x, "جمله"));
        t.Cell().Element(x => DocStyle.Tf(x, F2(tt.Liters)));
        t.Cell().Element(x => DocStyle.Tf(x, F2(tt.Taken)));
        t.Cell().ColumnSpan(2).Element(x => DocStyle.Tf(x, ""));
        t.Cell().Element(x => DocStyle.Tf(x, F2(tt.Loss)));
        t.Cell().Element(x => DocStyle.Tf(x, ""));
        t.Cell().Element(x => DocStyle.Tf(x, F(tt.LossPct, 3) + "٪"));
        t.Cell().Element(x => DocStyle.Tf(x, OptPct(tt.NeedPct, 3)));
        t.Cell().Element(x => DocStyle.Tf(x, tt.Share != 0m ? F2(tt.Share) : "—"));
        t.Cell().Element(x => DocStyle.Tf(x, Opt2(tt.NetIfMy)));
        t.Cell().Element(x => DocStyle.Tf(x, F2(tt.Rest)));
        t.Cell().Element(x => DocStyle.Tf(x, tt.HasActual ? F2(tt.Actual) : "—"));
        t.Cell().Element(x => DocStyle.Tf(x, tt.HasActual ? F2(tt.RealDiff) : "—"));
    });
}
