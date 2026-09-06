using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>
/// ══ سبکِ سندهای چاپ و PDF ══════════════════════════════════════════════════
/// رنگ‌ها و اندازه‌ها مو‌به‌مو از خودِ CSSِ سندهای نسخهٔ وب برداشته شده‌اند، تا
/// ورقی که از این برنامه بیرون می‌آید با ورقی که کاربر سال‌ها چاپ کرده یکی باشد.
///
/// تبدیلِ اندازه: در سندهای وب فونتِ ریشه ۱۸ پیکسل است و اندازه‌ها rem‌اند؛
/// پس ‎0.75rem = 13.5px‎ و در واحدِ چاپ ‎13.5 × 0.75 = 10.1pt‎.
/// </summary>
public static class DocStyle
{
    // ── رنگ‌ها (همان کدهای CSS) ──────────────────────────────────────────────
    public const string Title = "#b45309";      // عنوانِ سند و خطِ زیرش
    public const string Sub = "#5b6472";        // زیرنویس و تاریخ‌های سربرگ
    public const string HeadBg = "#2d3748";     // سرستونِ جدول و ردیفِ «جمله»
    public const string HeadFg = "#e2e8f0";
    public const string HeadLine = "#4a5568";
    public const string CellLine = "#e3e6eb";
    public const string CellFg = "#1f2430";
    public const string RowAlt = "#fafbfc";     // ردیف‌های زوج
    public const string FootFg = "#7b8494";
    public const string FootLine = "#d6dae1";
    public const string BoxLine = "#d6dae1";

    // رنگِ خانه‌ها — همان رنگ‌هایی که در جدولِ برنامه هم هستند
    public const string Index = "#718096";
    public const string Hawala = "#c07700";     // نمبر حواله و «برد»
    public const string Fuel = "#2b6cb0";       // مقدار تیل و الباقی
    public const string Money = "#276749";      // رسیدِ پول
    public const string Danger = "#e53e3e";     // فیصدیِ ما
    public const string Blue = "#2b6cb0";
    public const string Petrol = "#b45309";
    public const string Diesel = "#8a5a2b";

    // ── اندازه‌ها (pt) ───────────────────────────────────────────────────────
    public const float TitleSize = 14.2f;
    public const float SubSize = 9.7f;
    public const float HeadSize = 9.5f;
    public const float CellSize = 10.1f;
    public const float FootSize = 8.9f;
    public const float BoxLabel = 7.8f;
    public const float BoxValue = 10.5f;

    /// <summary>
    /// چارچوبِ مشترکِ همهٔ سندها: کادرِ نقطه‌چینِ دورِ ورق، سربرگِ دوطرفه
    /// (عنوان و زیرنویس یک طرف، تاریخ‌ها طرفِ دیگر) و پانویسِ شمارهٔ ورق.
    /// </summary>
    public static void Compose(IDocumentContainer doc, string title, string? subtitle,
                               string dates, Action<IContainer> body,
                               bool landscape = false, string titleColor = Title)
    {
        doc.Page(page =>
        {
            page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(14);
            page.PageColor(Colors.White);
            // ⚠️ Fallback لازم است: وزیرمتن ایموجی ندارد و بدونِ آن ستونِ
            // «نوع تیل» و نشانه‌های سربرگ در ورق خالی می‌مانند.
            page.DefaultTextStyle(t => t.FontFamily(PdfEngine.Font).FontSize(CellSize)
                                        .SemiBold().FontColor(CellFg)
                                        .Fallback(f => f.FontFamily(PdfEngine.EmojiFont)));
            page.ContentFromRightToLeft();

            // کادرِ نقطه‌چینِ دورِ ورق — همان چیزی که در چاپِ نسخهٔ وب دیده می‌شود
            page.Content().Border(1).BorderColor(FootLine).Padding(10).Column(col =>
            {
                col.Item().Element(c => Header(c, title, subtitle, titleColor));
                col.Item().PaddingTop(8).Element(body);
            });

            page.Footer().PaddingTop(6).BorderTop(1).BorderColor(FootLine)
                .Row(row =>
                {
                    row.RelativeItem().Text(dates)
                       .FontSize(FootSize).FontColor(FootFg);
                    row.RelativeItem().AlignLeft().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(FootSize).FontColor(FootFg));
                        t.Span("ورق ");
                        t.CurrentPageNumber();
                        t.Span(" از ");
                        t.TotalPages();
                    });
                });
        });
    }

    /// <summary>
    /// سربرگ: عنوان و زیرنویس. تاریخ‌ها عمداً این‌جا نیستند — در ورقِ نسخهٔ وب
    /// هم فقط در پانویس می‌آیند.
    /// </summary>
    private static void Header(IContainer c, string title, string? subtitle, string titleColor)
    {
        c.BorderBottom(2).BorderColor(titleColor).PaddingBottom(6).Column(col =>
        {
            col.Item().Text(title).FontSize(TitleSize).Bold().FontColor(titleColor);
            if (!string.IsNullOrWhiteSpace(subtitle))
                col.Item().PaddingTop(2).Text(subtitle!).FontSize(SubSize).FontColor(Sub);
        });
    }

    /// <summary>یک «کادرِ خلاصه» — برچسبِ کوچک بالا، عددِ پررنگ پایین.</summary>
    public static void SumBox(IContainer c, string label, string value, string color)
    {
        c.Border(1).BorderColor(CellLine).Padding(5).Column(col =>
        {
            col.Item().AlignCenter().Text(label).FontSize(BoxLabel).FontColor(Sub);
            col.Item().PaddingTop(2).AlignCenter().Text(value)
               .FontSize(BoxValue).Bold().FontColor(color);
        });
    }

    /// <summary>سرستونِ جدول — نوارِ تیره با نوشتهٔ روشن.</summary>
    public static IContainer Th(IContainer c) =>
        c.Background(HeadBg).Border(1).BorderColor(HeadLine).PaddingVertical(6).PaddingHorizontal(4)
         .AlignCenter().AlignMiddle();

    /// <summary>سرستون — کادر و نوشته با هم. یک‌جا انجام می‌شود تا کادر دوبار
    /// کشیده نشود (یک‌بار همین شد و دورِ هر خانه یک کادرِ اضافه افتاد).</summary>
    public static void ThText(IContainer c, string text) =>
        Th(c).Text(text).FontSize(HeadSize).Bold().FontColor(HeadFg);

    /// <summary>خانهٔ جدول — ردیف‌های زوج ته‌رنگِ ملایم دارند، مثلِ خودِ سند.</summary>
    public static IContainer Td(IContainer c, bool even) =>
        (even ? c.Background(RowAlt) : c)
            .Border(1).BorderColor(CellLine).PaddingVertical(5).PaddingHorizontal(4)
            .AlignCenter().AlignMiddle();

    /// <summary>
    /// خانهٔ جدول — کادر و نوشته با هم.
    /// ⚠️ ورودی باید خودِ خانهٔ خام باشد (‎table.Cell()‎)، نه خانه‌ای که از قبل
    /// از <see cref="Td"/> رد شده: یک‌بار هر دو با هم اجرا شد و دورِ نوشتهٔ هر
    /// خانه یک کادرِ کوچکِ اضافه افتاد که در ورق کاملاً دیده می‌شد.
    /// </summary>
    public static void TdText(IContainer c, bool even, string text, string? color = null)
    {
        var cell = Td(c, even);
        if (string.IsNullOrEmpty(text)) { cell.Text(string.Empty); return; }
        cell.Text(text).FontSize(CellSize).SemiBold().FontColor(color ?? CellFg);
    }

    /// <summary>ردیفِ «جمله» — همان نوارِ تیرهٔ پایینِ جدول.</summary>
    public static void Tf(IContainer c, string text) =>
        c.Background(HeadBg).Border(1).BorderColor(HeadLine).PaddingVertical(6).PaddingHorizontal(4)
         .AlignCenter().AlignMiddle()
         .Text(text).FontSize(HeadSize).Bold().FontColor(HeadFg);

    /// <summary>«—» برای خانهٔ خالی — مثلِ خودِ سند، نه صفر.</summary>
    public static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!;

    public static string DashNum(decimal v) => v == 0m ? "—" : PersianText.Num(v);
}
