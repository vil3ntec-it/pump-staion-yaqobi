using System.Globalization;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>جهتِ ورق. ‎Auto‎ یعنی «هرچه خودِ گزارش طبیعی‌اش است».</summary>
public enum PageOrientation { Auto = 0, Portrait = 1, Landscape = 2 }

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

    public static readonly PageSetup Default = new();

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
