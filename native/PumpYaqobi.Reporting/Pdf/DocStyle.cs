using QuestPDF.Elements;
using QuestPDF.Elements.Table;
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
    /// <summary>
    /// تنظیمِ ورقی که همین لحظه دارد چیده می‌شود.
    ///
    /// ⚠️ چرا ایستا و نخ‌محور: خانه‌های جدول (‎Td‎/‎Th‎) از ده‌ها جای ۱۶ گزارش
    /// صدا زده می‌شوند و هیچ‌کدام تنظیم را دستشان ندارند. به‌جای این‌که به هر
    /// یک از آن‌ها یک پارامتر اضافه شود، <see cref="Compose"/> تنظیم را
    /// این‌جا می‌نشاند و همان‌جا برمی‌دارد. موتورِ سند همه‌چیز را روی همان نخ
    /// و همان لحظه می‌چیند، پس دو سندِ هم‌زمان روی دو نخ به هم نمی‌ریزند.
    /// </summary>
    [ThreadStatic] private static PageSetup? _current;

    /// <summary>تنظیمِ جاری — بیرونِ ‎Compose‎ همان پیش‌فرض است.</summary>
    public static PageSetup Current => _current ?? PageSetup.Default;

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
        var (mh, mf) = s.BandOffsets();

        var hasHeader = HasText(s.HeaderLeft, s.HeaderCenter, s.HeaderRight);
        var hasFooter = HasText(s.FooterLeft, s.FooterCenter, s.FooterRight);

        _current = s;
        try
        {
            doc.Page(page =>
            {
                // اندازه از خودِ جدولِ کاغذ می‌آید (میلی‌متر)، نه یک ثابتِ A4 —
                // هر کاغذی که کاربر بردارد، ورق دقیقاً به همان اندازه می‌شود.
                page.Size((float)w, (float)h, Unit.Millimetre);

                // ── سربرگ و پاورقی جای خودشان را دارند ─────────────────────
                // مثلِ سایت (و اکسل)، سربرگ ‎mh‎ میلی‌متر از لبهٔ ورق می‌نشیند و
                // محتوا از ‎mt‎. پس حاشیهٔ ورق تا سربرگ است و فاصلهٔ سربرگ تا
                // محتوا با خودِ نوار پر می‌شود. بی سربرگ، حاشیه همان ‎mt‎ است.
                page.MarginTop((float)(hasHeader ? mh : m.Top), Unit.Millimetre);
                page.MarginBottom((float)(hasFooter ? mf : m.Bottom), Unit.Millimetre);
                page.MarginLeft((float)m.Left, Unit.Millimetre);
                page.MarginRight((float)m.Right, Unit.Millimetre);
                page.PageColor(Colors.White);
                // ⚠️ Fallback لازم است: وزیرمتن ایموجی ندارد و بدونِ آن ستونِ
                // «نوع تیل» و نشانه‌های سربرگ در ورق خالی می‌مانند.
                page.DefaultTextStyle(t => t.FontFamily(PdfEngine.Font).FontSize(CellSize)
                                            .SemiBold().FontColor(Ink(CellFg))
                                            .Fallback(f => f.FontFamily(PdfEngine.EmojiFont)));
                page.ContentFromRightToLeft();

                // «ورقِ اول سربرگ/پاورقی نداشته باشد» — همان ‎hfSkipFirst‎ی نسخهٔ وب
                IContainer Slot(IContainer x) => s.SkipFirstHeaderFooter ? x.SkipOnce() : x;

                if (hasHeader)
                    Slot(page.Header())
                        .Height((float)Math.Max(0m, m.Top - mh), Unit.Millimetre)
                        .AlignTop()
                        .Element(c => Band(c, s.HeaderLeft, s.HeaderCenter, s.HeaderRight,
                                           s, title, dates, top: true));

                // ── مقیاسِ چاپ ───────────────────────────────────────────────
                // تنها جایی است که مقیاس اعمال می‌شود، پس روی **همهٔ** گزارش‌ها
                // یکسان کار می‌کند. حالت‌های «جا دادن» پیش از رسیدن به این‌جا
                // با شمردنِ ورق به یک درصد تبدیل شده‌اند (‎ScaleSolver‎)؛ این‌جا
                // فقط «بی‌مقیاس» و «درصد» معنا دارد.
                IContainer Sized(IContainer c) => s.Scale switch
                {
                    PrintScale.Custom => c.Scale(Math.Clamp(s.ScalePercent, 10, 400) / 100f),
                    // اگر حل نشده به این‌جا رسید (سندی که بیرونِ پیش‌نمایش ساخته
                    // شده)، همان کارِ همیشگیِ موتور: هر ورق در خودش جا شود.
                    PrintScale.FitPage or PrintScale.FitRows or PrintScale.FitPages => c.ScaleToFit(),
                    _ => c,
                };

                // ── وسط‌چین روی ورق — زبانهٔ «حاشیه‌ها» ─────────────────────
                IContainer Centered(IContainer c)
                {
                    if (s.CenterH) c = c.AlignCenter();
                    if (s.CenterV) c = c.AlignMiddle();
                    return c;
                }

                // ⚠️ دورِ ورق هیچ کادری نیست — سایت هم ندارد. کادرِ نقطه‌چینی که
                // این‌جا بود با حاشیه‌اش یک‌دهمِ ورق را می‌خورد و روی ورق‌های بعدی
                // یک نوارِ خالیِ بی‌دلیل می‌گذاشت.
                Centered(Sized(page.Content())).Column(col =>
                {
                    col.Item().Element(c => Header(c, title, subtitle, titleColor));
                    col.Item().PaddingTop(8).Element(body);
                });

                if (hasFooter)
                    Slot(page.Footer())
                        .Height((float)Math.Max(0m, m.Bottom - mf), Unit.Millimetre)
                        .AlignBottom()
                        .Element(c => Band(c, s.FooterLeft, s.FooterCenter, s.FooterRight,
                                           s, title, dates, top: false));
            });
        }
        finally { _current = null; }
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
            t.DefaultTextStyle(x => x.FontSize(FootSize).FontColor(Ink(FootFg)));
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
        c.BorderBottom(2).BorderColor(Edge(titleColor)).PaddingBottom(6).Column(col =>
        {
            col.Item().Text(title).FontSize(TitleSize).Bold().FontColor(Ink(titleColor));
            if (!string.IsNullOrWhiteSpace(subtitle))
                col.Item().PaddingTop(2).Text(subtitle!).FontSize(SubSize).FontColor(Ink(Sub));
        });
    }

    /// <summary>یک «کادرِ خلاصه» — برچسبِ کوچک بالا، عددِ پررنگ پایین.</summary>
    public static void SumBox(IContainer c, string label, string value, string color)
    {
        Probe(c, "box", label + " | " + value).Border(1).BorderColor(Edge(CellLine)).Padding(5).Column(col =>
        {
            col.Item().AlignCenter().Element(x => Line(x, label, BoxLabel, Ink(Sub), Weight.Normal));
            col.Item().PaddingTop(2).AlignCenter()
               .Element(x => Line(x, value, BoxValue, Ink(color), Weight.Bold));
        });
    }

    // ── رنگِ چاپ: رنگی / خاکستری / سیاه‌وسفید ─────────────────────────────
    //
    // همان کشوی «رنگ»ِ زبانهٔ «جدول». هر رنگی که روی ورق می‌نشیند از همین
    // یک تابع رد می‌شود، پس یک حالت برای همهٔ گزارش‌ها یکسان کار می‌کند.
    // خاکستری = روشناییِ همان رنگ (‎grayscale(1)‎ی سایت)؛ سیاه‌وسفید = روشن‌ها
    // سفید و تیره‌ها سیاه (‎grayscale(1) contrast(3.2)‎).

    /// <summary>رنگِ نهایی روی ورق، با توجه به حالتِ رنگِ چاپ.</summary>
    public static string Paint(string hex)
    {
        var mode = Current.Color;
        if (mode == PrintColor.Color || hex.Length != 7 || hex[0] != '#') return hex;

        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        var y = (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b);

        if (mode == PrintColor.BlackWhite) y = y < 140 ? 0 : 255;
        return $"#{y:x2}{y:x2}{y:x2}";
    }

    // ══ سیاه‌وسفید: نوشته و خط، هر کدام قاعدهٔ خودش ═══════════════════════
    //
    // ⛔ سنجهٔ ‎printpages‎ (۱۴۰۵/۰۷/۱۲) نشان داد ‎Paint‎ی تنها برای «سیاه‌وسفید»
    // کافی نیست: آستانهٔ روشنایی‌اش ۱۴۰ است، پس
    //   • هر **خطِ** خاکستریِ روشنِ جدول سفید می‌شد — ورقِ سیاه‌وسفید اصلاً
    //     خطِ خانه نداشت؛
    //   • و هر **نوشتهٔ** کم‌رنگ (خاکستریِ برچسب، آبیِ روشن) سفید روی سفید
    //     می‌شد، یعنی ناپدید.
    // پس در سیاه‌وسفید: نوشته سیاه است مگر خودش سفید باشد (نوشتهٔ روی نوارِ
    // تیرهٔ سرستون)، و خط همیشه سیاه. رنگی و خاکستری همان ‎Paint‎اند.

    /// <summary>رنگِ **نوشته** — در سیاه‌وسفید هرگز سفید روی سفید نمی‌شود.</summary>
    public static string Ink(string hex)
    {
        if (Current.Color != PrintColor.BlackWhite || hex.Length != 7 || hex[0] != '#') return Paint(hex);
        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        var y = (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b);
        return y >= 235 ? "#ffffff" : "#000000";
    }

    /// <summary>
    /// نوشته روی یک زمینهٔ مشخص — در سیاه‌وسفید، روی زمینهٔ تیره سفید و روی
    /// زمینهٔ روشن سیاه. سرستون و ردیفِ «جمله» از این می‌خوانند.
    /// </summary>
    public static string InkOn(string fg, string bg)
    {
        if (Current.Color != PrintColor.BlackWhite) return Paint(fg);
        return Paint(bg) == "#000000" ? "#ffffff" : "#000000";
    }

    /// <summary>رنگِ **خط و کادر** — در سیاه‌وسفید همیشه سیاه، تا دیده شود.</summary>
    public static string Edge(string hex)
    {
        if (Current.Color != PrintColor.BlackWhite || hex.Length != 7 || hex[0] != '#') return Paint(hex);
        return "#000000";
    }

    /// <summary>خطِ خانه — با «خطوطِ جدول» خاموش، هیچ.</summary>
    private static string Line(string hex) => Current.Gridlines ? Edge(hex) : Colors.Transparent;

    // ══ سنجه: بلندیِ واقعیِ هر خانه روی ورق ═══════════════════════════════
    //
    // ⚠️ فقط برای آزمون (‎PrintOneLineTests‎). وقتی تهی است هیچ چیزی به ورق
    // اضافه نمی‌شود. وقتی هست، پشتِ هر خانه یک لایهٔ خالی می‌نشیند که موتور
    // با **اندازهٔ واقعیِ کشیده‌شدهٔ** همان خانه صدایش می‌زند — یعنی بلندیِ
    // همان ردیفی که روی کاغذ می‌آید، نه حدس.

    /// <summary>‎(نوع، نوشته، بلندی)‎ — فقط آزمون پرش می‌کند.</summary>
    [ThreadStatic] public static Action<string, string, float>? CellProbe;

    private const string EmptySvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"/>";

    private static IContainer Probe(IContainer c, string kind, string text)
    {
        var p = CellProbe;
        if (p is null) return c;
        var primary = c;
        c.Layers(l =>
        {
            l.Layer().Svg(size => { p(kind, text, size.Height); return EmptySvg; });
            primary = l.PrimaryLayer();
        });
        return primary;
    }

    /// <summary>سرستونِ جدول — نوارِ تیره با نوشتهٔ روشن.</summary>
    public static IContainer Th(IContainer c) =>
        c.Background(Paint(HeadBg)).Border(1).BorderColor(Line(HeadLine))
         .PaddingVertical(6).PaddingHorizontal(4)
         .AlignCenter().AlignMiddle();

    /// <summary>سرستون — کادر و نوشته با هم. یک‌جا انجام می‌شود تا کادر دوبار
    /// کشیده نشود (یک‌بار همین شد و دورِ هر خانه یک کادرِ اضافه افتاد).</summary>
    public static void ThText(IContainer c, string text) =>
        Line(Th(Probe(c, "th", text)), text, HeadSize, InkOn(HeadFg, HeadBg), Weight.Bold);

    /// <summary>
    /// سرستونِ جدول — فقط در ورقِ اول، مگر «تکرارِ سطرِ عنوان» روشن باشد.
    ///
    /// ⚠️ تا پیش از این همهٔ گزارش‌ها مستقیم ‎t.Header(...)‎ می‌زدند و موتور
    /// آن را روی **هر** ورق تکرار می‌کرد — درست خلافِ خواستهٔ صریحِ صاحب
    /// ریپو («چرا آن سربرگِ جدول در صفحهٔ دیگر هم هست؟ نباید باشد»). حالا
    /// همان یک خط از این‌جا می‌گذرد و تنظیمِ ورق تصمیم می‌گیرد.
    /// </summary>
    /// <param name="fill">خانه‌های سرستون را می‌سازد؛ هر بار ‎cell()‎ یک خانهٔ تازه می‌دهد.</param>
    public static void Head(TableDescriptor t, Action<Func<ITableCellContainer>> fill)
    {
        if (Current.RepeatHead) t.Header(h => fill(h.Cell));
        else fill(t.Cell);
    }

    /// <summary>خانهٔ جدول — ردیف‌های زوج ته‌رنگِ ملایم دارند، مثلِ خودِ سند.</summary>
    public static IContainer Td(IContainer c, bool even) =>
        (even ? c.Background(Paint(RowAlt)) : c)
            .Border(1).BorderColor(Line(CellLine)).PaddingVertical(5).PaddingHorizontal(4)
            .AlignCenter().AlignMiddle();

    /// <summary>
    /// خانهٔ جدول — کادر و نوشته با هم.
    /// ⚠️ ورودی باید خودِ خانهٔ خام باشد (‎table.Cell()‎)، نه خانه‌ای که از قبل
    /// از <see cref="Td"/> رد شده: یک‌بار هر دو با هم اجرا شد و دورِ نوشتهٔ هر
    /// خانه یک کادرِ کوچکِ اضافه افتاد که در ورق کاملاً دیده می‌شد.
    /// </summary>
    public static void TdText(IContainer c, bool even, string text, string? color = null)
    {
        var cell = Td(Probe(c, "td", text), even);
        if (string.IsNullOrEmpty(text)) { cell.Text(string.Empty); return; }
        // ⛔ **هیچ خانه‌ای دو خط نمی‌شود** (۱۴۰۵/۰۷/۲۰) — نه فقط تاریخ و عدد.
        // پیش از این قاعدهٔ یک‌خطی فقط مالِ «خانهٔ کوتاهِ رقم‌دار» بود و نام و
        // یادداشت می‌پیچیدند؛ یک یادداشتِ بلند یعنی ردیفی سه-چهار برابر و همان
        // گزارشِ صاحب ریپو: «توی تمام پی‌دی‌اف‌ها طولِ کادرها بزرگ می‌شن».
        Line(cell, Tight(text), CellSize, Ink(color ?? CellFg), Weight.SemiBold);
    }

    /// <summary>
    /// خانه‌ای که <b>همیشه یک خط</b> است — نامِ ردیفِ ورق (۱۴۰۵/۰۷/۱۸). از
    /// ۱۴۰۵/۰۷/۲۰ همان <see cref="TdText"/> است، چون هر خانه‌ای یک خط است.
    /// </summary>
    public static void TdOneLine(IContainer c, bool even, string text, string? color = null) =>
        TdText(c, even, text, color);

    // ══════════════════════════════════════════════════════════════════════
    //  ══ یک خط، همیشه — کوچک تا یک کف، و بعد بریده با «…» ═══════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۲۰): «توی تمام پی‌دی‌اف‌ها همه‌شون طولِ
    //  کادرهاشون بزرگ می‌شن… طول نباید بزرگ بشه از کادرهای توی پرینت.»
    //
    //  ⛔ <see cref="Line"/> تنها راهِ نوشتن در خانهٔ جدول، سرستون، ردیفِ
    //  «جمله» و کادرِ خلاصه است، و نوشته‌اش هرگز به خطِ دوم نمی‌رود:
    //    ۱) در یک خط جا شد ⇒ همان اندازه، دست‌نخورده؛
    //    ۲) نشد ولی تا <see cref="MinScale"/> کوچک شدن جا می‌دهد ⇒ همان‌قدر
    //       کوچک که جا شود (‎ScaleToFit‎ زیرِ سقفِ بلندیِ همان یک خط)؛
    //    ۳) باز هم نشد (یادداشتِ خیلی بلند) ⇒ در کوچک‌ترین اندازه، یک خط و
    //       ته‌اش «…». ⚠️ ‎ScaleToFit‎ِ تنها کف ندارد و یادداشتِ بلند را تا
    //       ریزِ ناخوانا کوچک می‌کرد.
    //
    //  ⚠️ چرا جزءِ پویا (‎IDynamicComponent‎): «جا می‌شود یا نه» به پهنای
    //  **واقعیِ** ستون بسته است و آن فقط هنگامِ چیدن معلوم است. اندازه‌گیری با
    //  خودِ موتور و همان قلم است، نه شمردنِ نویسه.
    //  ⚠️ «یک خط» هم از خودِ موتور است (همان نوشته با ‎ClampLines(1)‎)، نه یک
    //  ضریبِ ثابتِ قلم — ایموجیِ قلمِ جایگزین خطِ بلندتری دارد.

    /// <summary>کوچک‌ترین اندازه نسبت به قلمِ خودِ خانه — زیرِ این بریده می‌شود.</summary>
    public const float MinScale = 0.6f;

    /// <summary>وزنِ قلمِ یک خط.</summary>
    public enum Weight { Normal, SemiBold, Bold }

    /// <summary>
    /// یک خطِ نوشته که هرگز دو خط نمی‌شود — برای هر خانه و کادری که بلندی‌اش
    /// نباید با نوشته بالا برود. <paramref name="color"/> رنگِ نهایی است (از
    /// ‎Ink‎/‎InkOn‎ رد شده).
    /// </summary>
    public static void Line(IContainer c, string text, float size, string color, Weight weight = Weight.Normal)
    {
        if (string.IsNullOrEmpty(text)) { c.Text(string.Empty).FontSize(size); return; }
        var box = Probe(c, "line:" + size.ToString(System.Globalization.CultureInfo.InvariantCulture), text)
            .AlignMiddle();

        // ⚡ نوشتهٔ کوتاه (عدد، تاریخ، نامِ کوتاه — بیشترِ خانه‌های هر ورق) راهِ
        // ایستا دارد: سقفِ یک خط + ‎ScaleToFit‎، بی هیچ اندازه‌گیریِ پویا. کوتاه
        // است، پس کوچک شدنِ بی‌کفش هم ناخوانا نمی‌شود. جزءِ پویا گران است
        // (سنجهٔ ‎printperf‎: ۶۰۰ ردیف از ۱٫۶ به ۵٫۴ ثانیه رفت وقتی همه پویا
        // بودند)، پس فقط مالِ نوشتهٔ بلند است که کف و «…» لازم دارد.
        if (text.Length <= ShortLine)
        {
            Style(box.MaxHeight(size * OneLineFactor).ScaleToFit().Text(text), size, color, weight);
            return;
        }
        box.ShowEntire().Dynamic(new OneLineText(text, size, color, weight));
    }

    /// <summary>تا این‌قدر نویسه راهِ ایستا (بی کف) کافی است.</summary>
    private const int ShortLine = 24;

    /// <summary>
    /// سقفِ بلندیِ یک خط به نسبتِ قلم — بیشتر از یک خطِ وزیرمتن (~۱٫۵۶) و
    /// ایموجیِ جایگزین، و کمتر از دو خط (~۳٫۱). همان عددِ پیشینِ خانهٔ عددی.
    /// </summary>
    private const float OneLineFactor = 1.75f;

    private static void Style(TextBlockDescriptor t, float size, string color, Weight weight)
    {
        t.FontSize(size).FontColor(color);
        if (weight == Weight.Bold) t.Bold();
        else if (weight == Weight.SemiBold) t.SemiBold();
        else t.NormalWeight();
    }

    private sealed class OneLineText : IDynamicComponent
    {
        private readonly string _text;
        private readonly float _size;
        private readonly string _color;
        private readonly Weight _weight;

        // تصمیم برای همان پهنا یک بار گرفته می‌شود؛ موتور یک خانه را چند بار
        // می‌چیند (اندازه‌گیری و کشیدن) و پهنا عوض نمی‌شود.
        private float _forWidth = float.NaN;
        private int _mode;          // ۱ تا جا شود کوچک (مقیاسِ ۱ اگر جا بود) · ۲ کوچک‌ترین + «…»
        private float _lineHeight;

        public OneLineText(string text, float size, string color, Weight weight)
        { _text = text; _size = size; _color = color; _weight = weight; }

        private void Write(IContainer c, bool clamp)
        {
            var t = c.Text(_text);
            Style(t, _size, _color, _weight);
            if (clamp) t.ClampLines(1, "…");
        }

        // ⚠️ ‎CreateElement‎ با فضای **بی‌سقف** اندازه می‌گیرد (سنجیده شد: یادداشتِ
        // بلند در ستونِ ۱۰۰pt هم «یک خط» درمی‌آمد)، پس پهنا را خودمان می‌بندیم.
        // بلندی آزاد می‌ماند — همان چیزی که برای «چند خط می‌شود؟» لازم است.
        private float Height(DynamicContext ctx, Action<IContainer> write) =>
            ctx.CreateElement(x => write(x.Width(_forWidth))).Size.Height;

        private void Decide(DynamicContext ctx)
        {
            // دو اندازه‌گیری، نه بیشتر — هر کدام یک چیدنِ کاملِ متنِ بلند است
            // (سنجهٔ ‎printperf‎ و ۶۰۰ ردیفِ یادداشت‌دار):
            //   • بلندیِ یک خطِ همین نوشته (با ‎ClampLines(1)‎ — ایموجیِ قلمِ
            //     جایگزین خطِ بلندتری دارد، پس ضریبِ ثابت نه)؛
            //   • همین نوشته در کوچک‌ترین اندازه. ‎Scale‎ خطی است، پس «یک خط در
            //     کوچک‌ترین اندازه» همان ‎one × MinScale‎ است.
            // نوشته‌ای که در اندازهٔ کامل جا می‌شود هم از حالتِ ۱ می‌گذرد:
            // ‎ScaleToFit‎ در مقیاسِ ۱ همان بارِ اول جا می‌شود.
            var one = Height(ctx, x => Write(x, true));
            var small = Height(ctx, x => Write(x.Scale(MinScale), false));
            _lineHeight = one;
            _mode = small <= one * MinScale + 0.5f ? 1 : 2;
            // ⚠️ کفِ بلندیِ آن‌چه کشیده می‌شود، نه «یک خطِ اندازهٔ کامل»: نوشتهٔ
            // کوچک‌شده کوتاه‌تر است و ستون همان‌قدر جا به آن می‌دهد؛ سنجیدن با یک
            // خطِ کامل یعنی «جا نیست» برای همیشه. (در حالتِ ۱ اگر جا از این بیشتر
            // و از اندازهٔ انتخابی کمتر بود، ‎ScaleToFit‎ خودش کمی بیشتر کوچک می‌کند.)
            _need = one * MinScale;
        }

        private float _need;

        private void Content(IContainer x)
        {
            switch (_mode)
            {
                case 1: Write(x.MaxHeight(_lineHeight + 0.01f).ScaleToFit(), false); break;
                default: Write(x.Scale(MinScale), true); break;
            }
        }

        public DynamicComponentComposeResult Compose(DynamicContext ctx)
        {
            var w = ctx.AvailableSize.Width;
            // پهنای صفر (اندازه‌گیریِ کمینهٔ خودِ موتور) هیچ تصمیمی نمی‌سازد
            if (w <= 0f)
                return new DynamicComponentComposeResult { Content = ctx.CreateElement(_ => { }), HasMoreContent = true };
            if (w != _forWidth) { _forWidth = w; Decide(ctx); _content = null; }

            // ته ورق جای همین یک خط نیست ⇒ «بقیه در ورقِ بعد»؛ ‎ShowEntire‎ِ
            // بیرونی آن را «کلِ ردیف به ورقِ بعد» می‌کند، نه نیم‌خانه.
            if (ctx.AvailableSize.Height + 0.01f < _need)
                return new DynamicComponentComposeResult { Content = ctx.CreateElement(_ => { }), HasMoreContent = true };

            // ⚡ همان عنصر برای همان پهنا دوباره به کار می‌رود: موتور یک خانه را
            // چند بار اندازه می‌گیرد و هر ساختنِ تازه یعنی چیدنِ دوبارهٔ متنِ بلند.
            // ⚠️ فقط برای همان فضای در دسترس: عنصری که یک بار کشیده شده حالِ
            // «کشیده شد» دارد و دوباره به کار بردنش در اندازه‌گیریِ ورقِ دیگر چیدمان
            // را می‌شکست (سنجهٔ ‎shifts‎ همین را گرفت).
            if (_content is null || ctx.AvailableSize.Width != _contentFor.Width || ctx.AvailableSize.Height != _contentFor.Height)
            {
                _content = ctx.CreateElement(Content);
                _contentFor = ctx.AvailableSize;
            }
            return new DynamicComponentComposeResult { Content = _content, HasMoreContent = false };
        }

        private IDynamicElement? _content;
        private Size _contentFor;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ عدد و واحدش یک چیزند و دو خط نمی‌شوند ═══════════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «پرینت‌ها و پی‌دی‌اف‌ها خیلی داغون داده
    //  می‌شوند و اصلاً هیچ‌کدام تراز نیست — می‌بینی دو خط شده تاریخ، یا طولِ
    //  کادر یکی کوچک یکی بزرگ.»
    //
    //  ⛔ یک ریشه‌اش این‌جاست و ربطی به پهنای ستون ندارد: خانه‌ای مثلِ
    //  ‎«۱۲۳٬۴۵۶ افغانی»‎ یا ‎«$۱۲٫۳۴ دالر»‎ یک فاصلهٔ معمولی دارد، پس موتورِ
    //  متن حق دارد همان‌جا بشکند. با یک شکست، آن **یک** خانه دو خطی می‌شود و
    //  چون بلندیِ ردیفِ جدول از بلندترین خانه‌اش می‌آید، **کلِ ردیف** دو برابر
    //  می‌شود — همان «یکی کوچیک یکی بزرگ».
    //
    //  ⚠️ و این فقط زشت نیست: «۱۲۳٬۴۵۶» در یک خط و «افغانی» در خطِ بعد، در
    //  یک سندِ حساب‌داری یعنی عددی که واحدش معلوم نیست.
    //
    //  ⛔ ولی متنِ آزاد (یادداشت، نامِ مشتری، عنوانِ مصرف) باید مثلِ همیشه
    //  بپیچد، وگرنه از کادر می‌زند بیرون. پس قاعده تنگ و سنجیدنی است:
    //  **فقط خانه‌ای که رقم دارد و کوتاه است** — یعنی همان «عدد + واحد» و
    //  تاریخ. نوشتهٔ بی‌رقم و نوشتهٔ بلند دست نمی‌خورند.

    /// <summary>بلندترین خانه‌ای که «یک مقدار» شمرده می‌شود، نه جمله.</summary>
    private const int TightMax = 24;

    /// <summary>فاصلهٔ نشکن — دیده نمی‌شود، فقط جلوی شکستنِ خط را می‌گیرد.</summary>
    private const char Nbsp = '\u00a0';

    /// <summary>
    /// «عدد + واحد» را یک تکه می‌کند. متنِ بی‌رقم یا بلندتر از
    /// <see cref="TightMax"/> دست‌نخورده برمی‌گردد.
    /// </summary>
    public static string Tight(string text)
    {
        if (text.Length > TightMax || text.IndexOf(' ') < 0) return text;

        var hasDigit = false;
        foreach (var ch in text)
            if (char.IsDigit(ch)) { hasDigit = true; break; }

        return hasDigit ? text.Replace(' ', Nbsp) : text;
    }

    /// <summary>ردیفِ «جمله» — همان نوارِ تیرهٔ پایینِ جدول.</summary>
    public static void Tf(IContainer c, string text) =>
        Probe(c, "tf", text).Background(Paint(HeadBg)).Border(1).BorderColor(Line(HeadLine))
         .PaddingVertical(6).PaddingHorizontal(4)
         .AlignCenter().AlignMiddle()
         .Element(x => Line(x, Tight(text), HeadSize, InkOn(HeadFg, HeadBg), Weight.Bold));

    /// <summary>«—» برای خانهٔ خالی — مثلِ خودِ سند، نه صفر.</summary>
    public static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!;

    public static string DashNum(decimal v) => v == 0m ? "—" : PersianText.Num(v);
}
