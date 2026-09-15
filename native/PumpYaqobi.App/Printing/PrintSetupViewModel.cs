using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.Printing;

/// <summary>
/// یک گزینهٔ کشو — نشانه، عنوان و زیرنویسِ توضیحی، مثلِ کشوهای اکسل.
///
/// کشوی معمولی فقط یک خطِ ساده نشان می‌دهد؛ کشوی اکسل هر گزینه را با یک
/// نشانه، یک خطِ عنوان و یک خطِ توضیح می‌آورد — همان چیزی که صاحب ریپو با
/// عکس خواست. <see cref="Icon"/> اختیاری است تا کشوهای سادهٔ «تنظیمِ ورق»
/// دست‌نخورده بمانند.
/// </summary>
public sealed record SetupOption(string Value, string Title, string Note, string Icon = "")
{
    public override string ToString() => Title;

    public bool HasIcon => Icon.Length > 0;
}

/// <summary>
/// ══ «تنظیمِ ورق» — چهار زبانه، مو‌به‌مو مثلِ Page Setup اکسل و کارگاهِ سایت ══
///
///     ورق          ▸ جهت · مقیاس (٪ یا جا دادن در N×M ورق) · کاغذ · کیفیت · شمارهٔ ورقِ اول
///     حاشیه‌ها     ▸ شش عدد (بالا/پایین/چپ/راست/سربرگ/پاورقی) · نقشهٔ کوچک · وسط‌چین
///     سربرگ/پاورقی ▸ سربرگِ آماده · شش کادر · دکمه‌های کد · «ورقِ اول نداشته باشد»
///     جدول         ▸ تکرارِ سطرِ عنوان · خطوطِ جدول · رنگ
///
/// گزارشِ صاحب ریپو: «بخشِ پرینت خیلی کم‌بودی دارد، نه شبیهِ سایت است نه
/// شبیهِ اکسل.» تا پیش از این، این پنجره یک ستونِ تختِ بی‌زبانه بود و
/// هیچ‌کدام از فاصلهٔ سربرگ، وسط‌چین، جا دادن در چند ورق، خطوطِ جدول، رنگ و
/// تکرارِ سطرِ عنوان را نداشت.
///
/// ⚠️ این پنجره خودش هیچ چیزی را ذخیره نمی‌کند و هیچ سندی نمی‌سازد؛ فقط یک
/// <see cref="PageSetup"/> می‌سازد. تصمیمِ «ذخیره کن» و «سند را از نو بساز»
/// با صداکننده است.
/// </summary>
public sealed partial class PrintSetupViewModel : ObservableObject
{
    /// <summary>
    /// ⚠️ تنظیمِ ورودی نگه داشته می‌شود و خروجی از روی همین ساخته می‌شود
    /// (‎_orig with { … }‎)، نه یک ‎new PageSetup‎ی خالی.
    ///
    /// وگرنه هر بار که کاربر این پنجره را باز و تایید می‌کرد، چیزهایی که این
    /// پنجره اصلاً نشان نمی‌دهد — «تعدادِ نسخه»، «کدام ورق‌ها»، «مرتب/نامرتب» —
    /// بی‌صدا به پیش‌فرض برمی‌گشتند.
    /// </summary>
    private PageSetup _orig;

    public PrintSetupViewModel(PageSetup s)
    {
        _orig = s;
        foreach (var k in PageSetup.PaperOrder)
            Papers.Add(new SetupOption(k, k == "Custom" ? "اندازهٔ دلخواه" : k, PaperNote(k)));

        // کیفیتِ فایلِ PDF — همان کشوی سایت
        Dpis.Add(new SetupOption("100", "۱۰۰ dpi — سبک", ""));
        Dpis.Add(new SetupOption("150", "۱۵۰ dpi", ""));
        Dpis.Add(new SetupOption("200", "۲۰۰ dpi — پیشنهادی", ""));
        Dpis.Add(new SetupOption("300", "۳۰۰ dpi — پرکیفیت", ""));
        Dpis.Add(new SetupOption("400", "۴۰۰ dpi — خیلی سنگین", ""));

        // سربرگ‌های آماده — مثل کشوی Header اکسل
        Presets.Add(new SetupOption("keep", "(دست‌نویسِ خودم)", ""));
        Presets.Add(new SetupOption("none", "(هیچ)", ""));
        Presets.Add(new SetupOption("title", "نامِ گزارش", ""));
        Presets.Add(new SetupOption("titledate", "نامِ گزارش + تاریخ", ""));
        Presets.Add(new SetupOption("page", "ورق ۱ از ؟", ""));

        Colors.Add(new SetupOption("color", "رنگی", ""));
        Colors.Add(new SetupOption("gray", "خاکستری", ""));
        Colors.Add(new SetupOption("bw", "سیاه و سفید", ""));

        _preset = Presets[0];
        Pull(s);
    }

    private static string Num(decimal v) => Shamsi.Money(v);

    private static string PaperNote(string k)
    {
        if (k == "Custom") return "عرض و بلندا را خودتان بنویسید";
        var p = PageSetup.Papers[k];
        return Shamsi.Money(p.W) + " × " + Shamsi.Money(p.H) + " میلی‌متر";
    }

    /// <summary>کادرها را از روی یک تنظیم پر می‌کند — هم در آغاز، هم با «برگرداندن به پیش‌فرض».</summary>
    private void Pull(PageSetup s)
    {
        _loading = true;

        Paper = Papers.FirstOrDefault(p => p.Value == s.Paper) ?? Papers[0];
        IsPortrait = s.Orientation != PageOrientation.Landscape;
        IsAuto = s.Orientation == PageOrientation.Auto;

        CustomWidth = Num(s.CustomWidth);
        CustomHeight = Num(s.CustomHeight);
        Top = Num(s.MarginTop); Bottom = Num(s.MarginBottom);
        Left = Num(s.MarginLeft); Right = Num(s.MarginRight);
        HeaderGap = Num(s.MarginHeader); FooterGap = Num(s.MarginFooter);
        CenterH = s.CenterH; CenterV = s.CenterV;

        HeaderRight = s.HeaderRight; HeaderCenter = s.HeaderCenter; HeaderLeft = s.HeaderLeft;
        FooterRight = s.FooterRight; FooterCenter = s.FooterCenter; FooterLeft = s.FooterLeft;
        SkipFirst = s.SkipFirstHeaderFooter;

        FirstPage = s.FirstPage.ToString();
        Dpi = Dpis.FirstOrDefault(d => d.Value == s.Dpi.ToString()) ?? Dpis[2];

        // مقیاس: دکمهٔ رادیویی می‌گوید «درصد» یا «جا دادن در N×M ورق»
        IsFitPages = s.Scale == PrintScale.FitPages;
        ScalePercent = s.ScalePercent.ToString();
        FitW = s.FitWidthPages.ToString();
        FitH = s.FitHeightPages.ToString();

        RepeatHead = s.RepeatHead;
        Gridlines = s.Gridlines;
        Color = Colors.FirstOrDefault(c => c.Value == s.Color switch
        {
            PrintColor.Gray => "gray",
            PrintColor.BlackWhite => "bw",
            _ => "color",
        }) ?? Colors[0];

        _loading = false;
        RefreshMap();
    }

    private bool _loading;

    public ObservableCollection<SetupOption> Papers { get; } = new();
    public ObservableCollection<SetupOption> Dpis { get; } = new();
    public ObservableCollection<SetupOption> Presets { get; } = new();
    public ObservableCollection<SetupOption> Colors { get; } = new();

    // ── زبانهٔ «ورق» ─────────────────────────────────────────────────────
    [ObservableProperty] private SetupOption _paper = null!;
    [ObservableProperty] private bool _isPortrait = true;
    /// <summary>«به‌انتخابِ خودِ گزارش» — ویژگیِ خودِ این برنامه؛ سایت ندارد.</summary>
    [ObservableProperty] private bool _isAuto = true;
    [ObservableProperty] private string _customWidth = "210";
    [ObservableProperty] private string _customHeight = "297";
    [ObservableProperty] private SetupOption _dpi = null!;
    [ObservableProperty] private string _firstPage = "1";

    [ObservableProperty] private bool _isFitPages;
    [ObservableProperty] private string _scalePercent = "100";
    [ObservableProperty] private string _fitW = "1";
    [ObservableProperty] private string _fitH = "1";

    public bool IsCustomPaper => Paper?.Value == "Custom";

    partial void OnPaperChanged(SetupOption value)
    {
        OnPropertyChanged(nameof(IsCustomPaper));
        if (_loading) return;
        if (value.Value != "Custom" && PageSetup.Papers.TryGetValue(value.Value, out var p))
        { CustomWidth = Num(p.W); CustomHeight = Num(p.H); }
        RefreshMap();
    }
    partial void OnIsPortraitChanged(bool v) { if (!_loading) IsAuto = false; RefreshMap(); }
    partial void OnCustomWidthChanged(string v) => RefreshMap();
    partial void OnCustomHeightChanged(string v) => RefreshMap();

    // تایپ در کادرِ درصد یعنی «درصد»، تایپ در N×M یعنی «جا دادن» — مثل سایت
    partial void OnScalePercentChanged(string v) { if (!_loading) IsFitPages = false; }
    partial void OnFitWChanged(string v) { if (!_loading) IsFitPages = true; }
    partial void OnFitHChanged(string v) { if (!_loading) IsFitPages = true; }

    // ── زبانهٔ «حاشیه‌ها» ────────────────────────────────────────────────
    [ObservableProperty] private string _top = "";
    [ObservableProperty] private string _bottom = "";
    [ObservableProperty] private string _left = "";
    [ObservableProperty] private string _right = "";
    [ObservableProperty] private string _headerGap = "";
    [ObservableProperty] private string _footerGap = "";
    [ObservableProperty] private bool _centerH;
    [ObservableProperty] private bool _centerV;

    partial void OnTopChanged(string v) => RefreshMap();
    partial void OnBottomChanged(string v) => RefreshMap();
    partial void OnLeftChanged(string v) => RefreshMap();
    partial void OnRightChanged(string v) => RefreshMap();
    partial void OnHeaderGapChanged(string v) => RefreshMap();
    partial void OnFooterGapChanged(string v) => RefreshMap();

    /// <summary>
    /// ══ نقشهٔ کوچکِ حاشیه‌ها ═══════════════════════════════════════════
    /// همان ‎paintMarginPrev()‎ی سایت و نقشهٔ Page Setup اکسل: ورقی ۱۷۲ پیکسل
    /// بلند با همان نسبتِ کاغذ، که سربرگ، بدنه و پاورقی‌اش با هر تغییرِ عدد
    /// جلوی چشم جابه‌جا می‌شوند. اندازه‌ها از همان عددهای واقعی می‌آیند.
    /// </summary>
    [ObservableProperty] private double _mapW = 122;
    [ObservableProperty] private double _mapH = 172;
    /// <summary>فاصلهٔ بدنه از چهار لبهٔ نقشه — همان چهار حاشیه، به مقیاس.</summary>
    [ObservableProperty] private Avalonia.Thickness _mapBody;
    /// <summary>خطِ سربرگ: ‎mh‎ از بالا (و هیچ‌وقت پایین‌تر از ‎mt‎).</summary>
    [ObservableProperty] private Avalonia.Thickness _mapHeaderLine;
    /// <summary>خطِ پاورقی: ‎mf‎ از پایین.</summary>
    [ObservableProperty] private Avalonia.Thickness _mapFooterLine;

    private void RefreshMap()
    {
        var (w, h) = Sheet();
        if (w <= 0 || h <= 0) return;
        const double H = 172;
        var W = Math.Max(60, Math.Round(H * (double)(w / h)));
        var k = H / (double)h;
        MapW = W; MapH = H;

        double top = (double)NonNeg(Top) * k, bottom = (double)NonNeg(Bottom) * k;
        double left = (double)NonNeg(Left) * k, right = (double)NonNeg(Right) * k;
        MapBody = new Avalonia.Thickness(left, top, right, bottom);
        MapHeaderLine = new Avalonia.Thickness(left, Math.Min((double)NonNeg(HeaderGap) * k, top), right, 0);
        MapFooterLine = new Avalonia.Thickness(left, 0, right, Math.Min((double)NonNeg(FooterGap) * k, bottom));
    }

    private (decimal W, decimal H) Sheet()
    {
        var (w, h) = Paper?.Value == "Custom" || Paper is null || !PageSetup.Papers.TryGetValue(Paper.Value, out var p)
            ? (Pos(CustomWidth, 210m), Pos(CustomHeight, 297m))
            : (p.W, p.H);
        return IsPortrait ? (w, h) : (h, w);
    }

    // ── زبانهٔ «سربرگ/پاورقی» ────────────────────────────────────────────
    [ObservableProperty] private SetupOption _preset;
    [ObservableProperty] private string _headerRight = "";
    [ObservableProperty] private string _headerCenter = "";
    [ObservableProperty] private string _headerLeft = "";
    [ObservableProperty] private string _footerRight = "";
    [ObservableProperty] private string _footerCenter = "";
    [ObservableProperty] private string _footerLeft = "";
    [ObservableProperty] private bool _skipFirst;

    /// <summary>کدام کادر آخرین بار فوکوس داشت — دکمه‌های کد همان‌جا می‌نویسند.</summary>
    public string LastBox { get; set; } = "FooterCenter";

    /// <summary>سربرگِ آماده — همان ‎xpr-hfpreset‎ی سایت.</summary>
    partial void OnPresetChanged(SetupOption value)
    {
        if (_loading) return;
        switch (value.Value)
        {
            case "none": HeaderLeft = ""; HeaderCenter = ""; HeaderRight = ""; break;
            case "title": HeaderLeft = ""; HeaderCenter = "&[File]"; HeaderRight = ""; break;
            case "titledate": HeaderLeft = "&[Date]"; HeaderCenter = "&[File]"; HeaderRight = ""; break;
            case "page": HeaderLeft = ""; HeaderCenter = "ورق &[Page] از &[Pages]"; HeaderRight = ""; break;
        }
    }

    /// <summary>کدها، همان هفت‌تای سایت — دکمه‌های زیرِ کادرها.</summary>
    public static IReadOnlyList<(string Label, string Token)> Tokens { get; } = new[]
    {
        ("شمارهٔ ورق", "&[Page]"), ("تعداد ورق", "&[Pages]"),
        ("تاریخ شمسی", "&[Date]"), ("تاریخ قمری", "&[قمری]"), ("تاریخ میلادی", "&[میلادی]"),
        ("ساعت", "&[Time]"), ("نامِ گزارش", "&[File]"),
    };

    /// <summary>یک کد را به آخرین کادرِ فوکوس‌شده می‌چسباند.</summary>
    [RelayCommand]
    private void InsertToken(string token)
    {
        switch (LastBox)
        {
            case "HeaderRight": HeaderRight += token; break;
            case "HeaderCenter": HeaderCenter += token; break;
            case "HeaderLeft": HeaderLeft += token; break;
            case "FooterRight": FooterRight += token; break;
            case "FooterLeft": FooterLeft += token; break;
            default: FooterCenter += token; break;
        }
    }

    // ── زبانهٔ «جدول» ────────────────────────────────────────────────────
    [ObservableProperty] private bool _repeatHead;
    [ObservableProperty] private bool _gridlines = true;
    [ObservableProperty] private SetupOption _color = null!;

    /// <summary>«برگرداندن به پیش‌فرض» — همان دکمهٔ پایینِ پنجرهٔ سایت.</summary>
    [RelayCommand]
    private void Reset()
    {
        // چیزهایی که این پنجره نشان نمی‌دهد (نسخه، کدام ورق‌ها) دست نمی‌خورند
        _orig = PageSetup.Default with
        {
            Copies = _orig.Copies, Collate = _orig.Collate,
            What = _orig.What, From = _orig.From, To = _orig.To,
        };
        Pull(_orig);
    }

    /// <summary>خروجی — همان چیزی که به سند داده می‌شود.</summary>
    public PageSetup Build()
    {
        var top = NonNeg(Top); var bottom = NonNeg(Bottom);
        var left = NonNeg(Left); var right = NonNeg(Right);

        // دست زدن به عددها یعنی «حاشیهٔ دلخواه» — مگر با یکی از آماده‌ها بخواند
        var preset = "custom";
        foreach (var (k, m) in PageSetup.MarginPresets)
            if (m.Top == top && m.Bottom == bottom && m.Left == left && m.Right == right) { preset = k; break; }

        // مقیاس: زبانهٔ «ورق» فقط «درصد» و «جا دادن در N×M» را می‌شناسد؛
        // حالت‌های آمادهٔ کشوی کناری (جا دادن ستون‌ها/سطرها/همه) دست‌نخورده
        // می‌مانند مگر کاربر این‌جا چیزی زده باشد.
        var scale = _orig.Scale;
        if (IsFitPages) scale = PrintScale.FitPages;
        else if (scale is PrintScale.FitPages) scale = PrintScale.Custom;
        else if (scale is PrintScale.Custom or PrintScale.None or PrintScale.FitColumns
                 && (int)Math.Clamp(Shamsi.Num(ScalePercent), 10m, 400m) != 100)
            scale = PrintScale.Custom;

        return _orig with
        {
            Paper = Paper.Value,
            CustomWidth = Pos(CustomWidth, 210m),
            CustomHeight = Pos(CustomHeight, 297m),
            Orientation = IsAuto ? PageOrientation.Auto
                        : IsPortrait ? PageOrientation.Portrait : PageOrientation.Landscape,
            MarginPreset = preset,
            MarginTop = top, MarginBottom = bottom, MarginLeft = left, MarginRight = right,
            MarginHeader = NonNeg(HeaderGap), MarginFooter = NonNeg(FooterGap),
            CenterH = CenterH, CenterV = CenterV,
            HeaderLeft = HeaderLeft, HeaderCenter = HeaderCenter, HeaderRight = HeaderRight,
            FooterLeft = FooterLeft, FooterCenter = FooterCenter, FooterRight = FooterRight,
            SkipFirstHeaderFooter = SkipFirst,
            FirstPage = (int)Math.Clamp(Shamsi.Num(FirstPage), 1m, 9999m),
            Dpi = (int)Math.Clamp(Shamsi.Num(Dpi.Value), 72m, 400m),
            Scale = scale,
            ScalePercent = (int)Math.Clamp(Shamsi.Num(ScalePercent), 10m, 400m),
            FitWidthPages = (int)Math.Clamp(Shamsi.Num(FitW), 1m, 20m),
            FitHeightPages = (int)Math.Clamp(Shamsi.Num(FitH), 1m, 99m),
            RepeatHead = RepeatHead,
            Gridlines = Gridlines,
            Color = Color.Value switch
            {
                "gray" => PrintColor.Gray,
                "bw" => PrintColor.BlackWhite,
                _ => PrintColor.Color,
            },
        };
    }

    /// <summary>عددِ مثبت؛ خالی یا صفر به پیش‌فرض برمی‌گردد — ورقِ صفرعرض نداریم.</summary>
    private static decimal Pos(string s, decimal fallback)
    {
        var v = Shamsi.Num(s);
        return v > 0m ? v : fallback;
    }

    private static decimal NonNeg(string s) => Math.Max(0m, Shamsi.Num(s));
}
