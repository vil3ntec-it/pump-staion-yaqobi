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
    ///
    /// <paramref name="landscape"/> جهتِ **طبیعیِ** خودِ گزارش است؛ اگر کاربر
    /// در «تنظیمِ ورق» جهتی انتخاب کرده باشد، آن مقدم است.
    /// </summary>
    public static void Compose(IDocumentContainer doc, string title, string? subtitle,
                               string dates, Action<IContainer> body,
                               bool landscape = false, string titleColor = Title,
                               PageSetup? setup = null)
    {
        var s = setup ?? PageSetup.Default;
        var (w, h) = s.SizeMm(landscape);
        var m = s.Margins();

        doc.Page(page =>
        {
            // اندازه از خودِ جدولِ کاغذ می‌آید (میلی‌متر)، نه یک ثابتِ A4 —
            // هر کاغذی که کاربر بردارد، ورق دقیقاً به همان اندازه می‌شود.
            page.Size((float)w, (float)h, Unit.Millimetre);
            page.MarginTop((float)m.Top, Unit.Millimetre);
            page.MarginBottom((float)m.Bottom, Unit.Millimetre);
            page.MarginLeft((float)m.Left, Unit.Millimetre);
            page.MarginRight((float)m.Right, Unit.Millimetre);
            page.PageColor(Colors.White);
            // ⚠️ Fallback لازم است: وزیرمتن ایموجی ندارد و بدونِ آن ستونِ
            // «نوع تیل» و نشانه‌های سربرگ در ورق خالی می‌مانند.
            page.DefaultTextStyle(t => t.FontFamily(PdfEngine.Font).FontSize(CellSize)
                                        .SemiBold().FontColor(CellFg)
                                        .Fallback(f => f.FontFamily(PdfEngine.EmojiFont)));
            page.ContentFromRightToLeft();

            // «ورقِ اول سربرگ/پاورقی نداشته باشد» — همان ‎hfSkipFirst‎ی نسخهٔ وب
            IContainer Slot(IContainer x) => s.SkipFirstHeaderFooter ? x.SkipOnce() : x;

            if (HasText(s.HeaderLeft, s.HeaderCenter, s.HeaderRight))
                Slot(page.Header()).PaddingBottom(5)
                    .Element(c => Band(c, s.HeaderLeft, s.HeaderCenter, s.HeaderRight,
                                       s, title, dates, top: true));

            // ── مقیاسِ چاپ ───────────────────────────────────────────────
            // همان کادرِ «مقیاس»ِ صفحهٔ چاپ. تنها جایی است که مقیاس اعمال
            // می‌شود، پس روی **همهٔ** گزارش‌ها یکسان کار می‌کند.
            //
            // ⚠️ «هم‌اندازهٔ یک ورق» با ‎ScaleToFit‎ی خودِ موتور است، نه یک
            // ضریبِ حدسی: موتور خودش محتوا را اندازه می‌گیرد و تا جایی کوچک
            // می‌کند که واقعاً جا شود — پس آنچه می‌بینید همان است که چاپ
            // می‌شود.
            IContainer Sized(IContainer c) => s.Scale switch
            {
                PrintScale.FitPage => c.ScaleToFit(),
                PrintScale.Custom => c.Scale(Math.Clamp(s.ScalePercent, 10, 400) / 100f),
                _ => c,
            };

            // کادرِ نقطه‌چینِ دورِ ورق — همان چیزی که در چاپِ نسخهٔ وب دیده می‌شود
            Sized(page.Content()).Border(1).BorderColor(FootLine).Padding(10).Column(col =>
            {
                col.Item().Element(c => Header(c, title, subtitle, titleColor));
                col.Item().PaddingTop(8).Element(body);
            });

            if (HasText(s.FooterLeft, s.FooterCenter, s.FooterRight))
                Slot(page.Footer()).PaddingTop(6)
                    .Element(c => Band(c, s.FooterLeft, s.FooterCenter, s.FooterRight,
                                       s, title, dates, top: false));
        });
    }

    private static bool HasText(params string?[] parts) =>
        parts.Any(p => !string.IsNullOrWhiteSpace(p));

    /// <summary>
    /// نوارِ سربرگ یا پاورقی — سه قسمت: کناره‌ها و وسط.
    ///
    /// ⚠️ شمارهٔ ورق با ‎CurrentPageNumber‎/‎TotalPages‎ی خودِ موتور نوشته
    /// می‌شود، نه با یک عددِ ازپیش‌ساخته: تعدادِ ورق‌ها تا پایانِ چیدمان معلوم
    /// نیست. کدهای دیگر (تاریخ، ساعت، نامِ سند) همان‌جا جای‌گذاری می‌شوند.
    /// </summary>
    private static void Band(IContainer c, string? left, string? center, string? right,
                             PageSetup s, string title, string dates, bool top)
    {
        var band = top ? c.BorderBottom(1).BorderColor(FootLine).PaddingBottom(4)
                       : c.BorderTop(1).BorderColor(FootLine).PaddingTop(4);

        band.Row(row =>
        {
            // در ورقِ راست‌به‌چپ، «راست» اولین جای ردیف است
            row.RelativeItem().Element(x => Part(x, right, s, title, dates, Align.Start));
            row.RelativeItem().Element(x => Part(x, center, s, title, dates, Align.Center));
            row.RelativeItem().Element(x => Part(x, left, s, title, dates, Align.End));
        });
    }

    private enum Align { Start, Center, End }

    private static void Part(IContainer c, string? tpl, PageSetup s,
                             string title, string dates, Align align)
    {
        var box = align switch
        {
            Align.Center => c.AlignCenter(),
            Align.End => c.AlignLeft(),
            _ => c.AlignRight(),
        };

        if (string.IsNullOrWhiteSpace(tpl)) { box.Text(string.Empty); return; }

        // «شمارهٔ ورقِ اول» روی هر دو عدد اثر دارد — همان ‎off‎ی ‎hfText‎
        var off = Math.Clamp(s.FirstPage, 1, 9999) - 1;
        string Shift(int? n) => PersianText.Num((n ?? 0) + off);

        box.Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(FootSize).FontColor(FootFg));
            foreach (var piece in Split(tpl!))
            {
                if (piece == "&[Page]") t.CurrentPageNumber().Format(Shift);
                else if (piece == "&[Pages]") t.TotalPages().Format(Shift);
                else t.Span(HeaderFooter.Render(piece, 1, 1, s, title, dates));
            }
        });
    }

    /// <summary>
    /// متنِ الگو را دورِ کدهای «شمارهٔ ورق» می‌شکند — آن دو تا باید به خودِ
    /// موتور سپرده شوند، بقیه همان‌جا جای‌گذاری می‌شوند.
    /// </summary>
    private static IEnumerable<string> Split(string tpl)
    {
        var normalized = tpl
            .Replace("&[ورق]", "&[Page]").Replace("&[صفحه]", "&[Page]")
            .Replace("&[کل]", "&[Pages]");

        var i = 0;
        while (i < normalized.Length)
        {
            var next = normalized.IndexOf("&[", i, StringComparison.Ordinal);
            if (next < 0) { yield return normalized[i..]; break; }
            var end = normalized.IndexOf(']', next);
            if (end < 0) { yield return normalized[i..]; break; }

            var tok = normalized[next..(end + 1)];
            if (tok is "&[Page]" or "&[Pages]")
            {
                if (next > i) yield return normalized[i..next];
                yield return tok;
            }
            else
            {
                yield return normalized[i..(end + 1)];
            }
            i = end + 1;
        }
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
