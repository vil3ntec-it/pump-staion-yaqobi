using System.Globalization;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>جهتِ ورق. ‎Auto‎ یعنی «هرچه خودِ گزارش طبیعی‌اش است».</summary>
public enum PageOrientation { Auto = 0, Portrait = 1, Landscape = 2 }

/// <summary>کدام ورق‌ها چاپ شوند — همان کادرِ اولِ «تنظیمات»ِ صفحهٔ چاپ.</summary>
public enum PrintWhat { All = 0, Current = 1, Range = 2 }

/// <summary>
/// مقیاسِ چاپ — همان کادرِ «مقیاس».
///
/// ⚠️ چهار حالت است نه شش‌تای نسخهٔ وب، و دلیلش این است که موتورِ سندِ این
/// برنامه با مرورگر فرق دارد: آن‌جا جدولِ HTML از عرضِ ورق بیرون می‌زد و
/// «جا دادن ستون‌ها» کارِ واقعی می‌کرد؛ این‌جا ستون‌ها خودشان تا عرضِ ورق
/// چیده می‌شوند، پس آن حالت همان ‎None‎ است و ساختنِ یک گزینهٔ قلابی برایش
/// فقط کاربر را گمراه می‌کرد.
/// </summary>
public enum PrintScale
{
    /// <summary>در اندازهٔ واقعیِ خودش.</summary>
    None = 0,
    /// <summary>هر ورق تا جایی کوچک می‌شود که کاملاً در یک صفحه بنشیند.</summary>
    FitPage = 1,
    /// <summary>درصدِ دستیِ کاربر.</summary>
    Custom = 2,
}

/// <summary>
/// ══ تنظیمِ ورق — «کارگاه چاپ» ══════════════════════════════════════════════
/// همتای ‎S‎ / ‎DEF‎ / ‎PAPERS‎ / ‎MARGINS‎ی نسخهٔ وب (‎pumpPrintStudio_v4‎).
///
/// همان چیزهایی که کاربر سال‌ها در آن پنجره داشت: اندازهٔ کاغذ، ایستاده یا
/// خوابیده، حاشیه‌ها، سربرگ و پاورقیِ سه‌قسمتی با کدها، شمارهٔ ورقِ اول، و
/// کیفیتِ پیش‌نمایش.
///
/// ⚠️ پیش‌فرض‌ها **مو‌به‌مو** همان ‎DEF‎ نسخهٔ وب‌اند — حاشیهٔ «باریک»، پاورقیِ
/// وسط «ورق … از …» و پاورقیِ کناری سه تاریخ. اگر این‌ها را عوض کنید، ورقِ
/// کاربر با ورقی که سال‌ها چاپ کرده فرق می‌کند.
/// </summary>
public sealed record PageSetup
{
    /// <summary>اندازه‌های استانداردِ کاغذ (میلی‌متر، ورقِ ایستاده) — همان جدولِ ‎PAPERS‎.</summary>
    public static readonly IReadOnlyDictionary<string, (decimal W, decimal H)> Papers =
        new Dictionary<string, (decimal, decimal)>
        {
            ["A3"] = (297m, 420m), ["A4"] = (210m, 297m), ["A5"] = (148m, 210m),
            ["A6"] = (105m, 148m), ["A2"] = (420m, 594m),
            ["B4"] = (250m, 353m), ["B5"] = (176m, 250m),
            ["Letter"] = (215.9m, 279.4m), ["Legal"] = (215.9m, 355.6m),
            ["Tabloid"] = (279.4m, 431.8m), ["Executive"] = (184.1m, 266.7m),
            ["Statement"] = (139.7m, 215.9m), ["Folio"] = (215.9m, 330.2m),
        };

    /// <summary>ترتیبِ کشویِ کاغذ — همان ‎PAPER_ORDER‎.</summary>
    public static readonly string[] PaperOrder =
    {
        "A4", "A5", "A3", "A6", "A2", "B4", "B5",
        "Letter", "Legal", "Tabloid", "Executive", "Statement", "Folio", "Custom",
    };

    public readonly record struct MarginSet(decimal Top, decimal Bottom, decimal Left, decimal Right);

    /// <summary>سه حاشیهٔ آماده — همان ‎MARGINS‎.</summary>
    public static readonly IReadOnlyDictionary<string, MarginSet> MarginPresets =
        new Dictionary<string, MarginSet>
        {
            ["normal"] = new(19.1m, 19.1m, 17.8m, 17.8m),
            ["narrow"] = new(19.1m, 19.1m, 6.4m, 6.4m),
            ["wide"] = new(25.4m, 25.4m, 25.4m, 25.4m),
        };

    // ── کاغذ ─────────────────────────────────────────────────────────────
    public string Paper { get; init; } = "A4";
    public decimal CustomWidth { get; init; } = 210m;
    public decimal CustomHeight { get; init; } = 297m;
    public PageOrientation Orientation { get; init; } = PageOrientation.Auto;

    // ── حاشیه (میلی‌متر) ─────────────────────────────────────────────────
    public string MarginPreset { get; init; } = "narrow";
    public decimal MarginTop { get; init; } = 19.1m;
    public decimal MarginBottom { get; init; } = 19.1m;
    public decimal MarginLeft { get; init; } = 6.4m;
    public decimal MarginRight { get; init; } = 6.4m;

    // ── سربرگ و پاورقیِ سه‌قسمتی ──────────────────────────────────────────
    public string HeaderLeft { get; init; } = "";
    public string HeaderCenter { get; init; } = "";
    public string HeaderRight { get; init; } = "";
    public string FooterLeft { get; init; } = "";
    public string FooterCenter { get; init; } = "ورق &[Page] از &[Pages]";
    public string FooterRight { get; init; } = "&[Dates]";

    /// <summary>«شمارهٔ ورقِ اول» — همان ‎First page number‎ی اکسل.</summary>
    public int FirstPage { get; init; } = 1;

    /// <summary>ورقِ اول سربرگ/پاورقی نداشته باشد.</summary>
    public bool SkipFirstHeaderFooter { get; init; }

    /// <summary>کیفیتِ تصویرِ پیش‌نمایش (نقطه بر اینچ).</summary>
    public int Dpi { get; init; } = 144;

    // ── مقیاس ─────────────────────────────────────────────────────────────
    public PrintScale Scale { get; init; } = PrintScale.None;

    /// <summary>درصدِ «مقیاسِ دلخواه» — ۱۰ تا ۴۰۰.</summary>
    public int ScalePercent { get; init; } = 100;

    // ── کارِ چاپ ───────────────────────────────────────────────────────────
    //
    // این پنج‌تا روی **ساختِ** سند هیچ اثری ندارند؛ فقط می‌گویند از سندِ
    // ساخته‌شده کدام ورق‌ها و چند نسخه چاپ/ذخیره شود. کنارِ بقیه نگه داشته
    // می‌شوند چون در نسخهٔ وب هم همه در یک ‎S‎ی واحد ذخیره می‌شدند و کاربر
    // انتظار دارد دفعهٔ بعد همان‌طور باز شود.

    public int Copies { get; init; } = 1;

    /// <summary>مرتب (۱۲۳ ۱۲۳) یا نامرتب (۱۱۱ ۲۲۲).</summary>
    public bool Collate { get; init; } = true;

    public PrintWhat What { get; init; } = PrintWhat.All;
    public int From { get; init; } = 1;
    public int To { get; init; } = 1;

    public static readonly PageSetup Default = new();

    /// <summary>
    /// همین تنظیم، ولی بی هر چیزی که فقط به «کارِ چاپ» مربوط است.
    ///
    /// دو تنظیم که این‌ها یکی باشند، سندِ یکسانی می‌سازند — پس با عوض شدنِ
    /// «تعدادِ نسخه» یا «کدام ورق‌ها» نباید سند از نو ساخته شود. مقایسه‌اش
    /// رایگان است چون ‎record‎ خودش برابریِ مقداری دارد.
    /// </summary>
    public PageSetup LayoutOnly() => this with
    {
        Copies = 1, Collate = true, What = PrintWhat.All, From = 1, To = 1,
    };

    /// <summary>اندازهٔ نهاییِ ورق، با درنظر گرفتنِ ایستاده/خوابیده.</summary>
    public (decimal W, decimal H) SizeMm(bool naturalLandscape)
    {
        var (w, h) = Paper == "Custom" || !Papers.TryGetValue(Paper, out var p)
            ? (CustomWidth, CustomHeight)
            : (p.W, p.H);

        return IsLandscape(naturalLandscape) ? (h, w) : (w, h);
    }

    /// <summary>
    /// ورق خوابیده است؟ ‎Auto‎ یعنی «هرچه خودِ گزارش می‌خواهد» — جدولِ
    /// هفده‌ستونهٔ امانت طبیعتاً خوابیده است و فرمِ گاوصندوق ایستاده.
    /// </summary>
    public bool IsLandscape(bool naturalLandscape) => Orientation switch
    {
        PageOrientation.Portrait => false,
        PageOrientation.Landscape => true,
        _ => naturalLandscape,
    };

    /// <summary>حاشیه‌های نهایی — «دلخواه» عددهای خودش را می‌دهد.</summary>
    public MarginSet Margins() =>
        MarginPreset != "custom" && MarginPresets.TryGetValue(MarginPreset, out var m)
            ? m
            : new MarginSet(MarginTop, MarginBottom, MarginLeft, MarginRight);

    /// <summary>پیش‌فرضِ عددهای حاشیه وقتی کاربر یک آمادهٔ دیگر را برمی‌دارد.</summary>
    public PageSetup WithMarginPreset(string preset)
    {
        if (preset == "custom" || !MarginPresets.TryGetValue(preset, out var m))
            return this with { MarginPreset = preset };
        return this with
        {
            MarginPreset = preset,
            MarginTop = m.Top, MarginBottom = m.Bottom,
            MarginLeft = m.Left, MarginRight = m.Right,
        };
    }

    /// <summary>پیش‌فرضِ عددهای کاغذِ دلخواه وقتی کاربر کاغذی را برمی‌دارد.</summary>
    public PageSetup WithPaper(string paper)
    {
        if (paper == "Custom" || !Papers.TryGetValue(paper, out var p))
            return this with { Paper = paper };
        return this with { Paper = paper, CustomWidth = p.W, CustomHeight = p.H };
    }
}

/// <summary>
/// ══ کارِ چاپ — کدام ورق‌ها و به چه ترتیبی ═══════════════════════════════════
///
/// رونوشتِ ‎doPrint()‎ی نسخهٔ وب، ولی جدا از هر رابطی تا بشود واقعاً آزمودش:
///
///   • «همهٔ گزارش» ⇒ ۱ تا آخر · «همین ورق» ⇒ فقط ورقِ باز ·
///     «بازه» ⇒ از…تا، و اگر وارونه نوشته شده باشد خودش صافش می‌کند.
///   • «مرتب» یعنی ۱،۲،۳ ۱،۲،۳ و «نامرتب» یعنی ۱،۱،۱ ۲،۲،۲ — همان دو خطِ
///     زیرِ کادرِ «مرتب/نامرتب».
/// </summary>
public static class PrintJob
{
    /// <summary>ورق‌های انتخاب‌شده (شماره از ۱).</summary>
    public static IReadOnlyList<int> Picked(PageSetup s, int pageCount, int currentPage)
    {
        if (pageCount <= 0) return Array.Empty<int>();

        int from = 1, to = pageCount;
        if (s.What == PrintWhat.Current)
            from = to = Math.Clamp(currentPage, 1, pageCount);
        else if (s.What == PrintWhat.Range)
        {
            from = Math.Clamp(s.From, 1, pageCount);
            to = Math.Clamp(s.To, 1, pageCount);
            if (to < from) (from, to) = (to, from);
        }

        var list = new List<int>(to - from + 1);
        for (var i = from; i <= to; i++) list.Add(i);
        return list;
    }

    /// <summary>همان‌ها، با تعدادِ نسخه و ترتیبِ مرتب/نامرتب.</summary>
    public static IReadOnlyList<int> Order(PageSetup s, int pageCount, int currentPage)
    {
        var picked = Picked(s, pageCount, currentPage);
        var copies = Math.Clamp(s.Copies, 1, 999);
        if (picked.Count == 0 || copies <= 1) return picked;

        var order = new List<int>(picked.Count * copies);
        if (s.Collate)
            for (var c = 0; c < copies; c++) order.AddRange(picked);
        else
            foreach (var p in picked) for (var c = 0; c < copies; c++) order.Add(p);
        return order;
    }

    /// <summary>همهٔ ورق‌ها، یک‌بار، به ترتیب؟ آن‌وقت فایلِ اصلی خودش کافی است.</summary>
    public static bool IsWholeDocument(IReadOnlyList<int> order, int pageCount)
    {
        if (order.Count != pageCount) return false;
        for (var i = 0; i < order.Count; i++) if (order[i] != i + 1) return false;
        return true;
    }
}

/// <summary>
/// ══ کدهای سربرگ و پاورقی ═══════════════════════════════════════════════════
/// رونوشتِ ‎hfText(tpl, pageNo, total)‎ — کدها هم به شکلِ اکسل (‎&amp;[Page]‎) و
/// هم فارسی (‎&amp;[ورق]‎) پذیرفته می‌شوند.
///
/// ⚠️ کدی که شناخته نشود **همان‌طور که هست** می‌ماند، نه اینکه پاک شود؛ همان
/// رفتارِ نسخهٔ وب. کاربر باید ببیند چه نوشته که کار نکرده.
/// </summary>
public static class HeaderFooter
{
    private static readonly System.Text.RegularExpressions.Regex Token =
        new(@"&\[([^\]]+)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static string Render(string? template, int pageNo, int total,
                                PageSetup setup, string docTitle, string dates,
                                DateTime? when = null)
    {
        if (string.IsNullOrEmpty(template)) return "";

        var off = Math.Clamp(setup.FirstPage, 1, 9999) - 1;
        var page = pageNo + off;
        var last = total + off;

        var now = when ?? DateTime.Now;
        var time = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        var sh = DocDates.Shamsi(now);
        var mi = DocDates.Miladi(now);
        var qa = DocDates.Qamari(now);

        return Token.Replace(template!, m => m.Groups[1].Value switch
        {
            "Page" or "ورق" or "صفحه" => PersianText.Num(page),
            "Pages" or "کل" => PersianText.Num(last),
            "Date" or "تاریخ" or "شمسی" => sh,
            "DateM" or "میلادی" => mi,
            "DateQ" or "قمری" => qa,
            "Dates" or "تاریخ‌ها" => dates,
            "Time" or "ساعت" => time,
            "File" or "نام" or "Tab" => docTitle,
            _ => m.Value,
        });
    }
}

/// <summary>
/// سندی که «تنظیمِ ورق» می‌پذیرد.
///
/// ⚠️ عمداً ‎set‎ است نه ‎init‎: پنجرهٔ پیش‌نمایش سند را می‌سازد و بعد تنظیم را
/// رویش می‌نشاند، پس هیچ‌کدام از جاهایی که سند را می‌سازند لازم نیست از
/// تنظیمِ ورق خبر داشته باشند.
/// </summary>
public interface ISetupDocument : QuestPDF.Infrastructure.IDocument
{
    PageSetup Setup { get; set; }
}
