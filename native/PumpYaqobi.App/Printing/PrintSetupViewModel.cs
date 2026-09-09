using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
/// ══ «تنظیمِ ورق» ═══════════════════════════════════════════════════════════
/// همتای پنجرهٔ «کارگاه چاپ»ِ نسخهٔ وب: اندازهٔ کاغذ، ایستاده/خوابیده، حاشیه،
/// سربرگ و پاورقیِ سه‌قسمتی، شمارهٔ ورقِ اول و کیفیتِ پیش‌نمایش.
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
    private readonly PageSetup _orig;

    public PrintSetupViewModel(PageSetup s)
    {
        _orig = s;
        foreach (var k in PageSetup.PaperOrder)
            Papers.Add(new SetupOption(k, k == "Custom" ? "اندازهٔ دلخواه" : k, PaperNote(k)));

        Margins.Add(new SetupOption("normal", "حاشیهٔ عادی", MarginNote("normal")));
        Margins.Add(new SetupOption("narrow", "حاشیهٔ باریک", MarginNote("narrow")));
        Margins.Add(new SetupOption("wide", "حاشیهٔ پهن", MarginNote("wide")));
        Margins.Add(new SetupOption("custom", "حاشیهٔ دلخواه", "عددهای زیر به کار می‌روند"));

        Orientations.Add(new SetupOption("auto", "به‌انتخابِ خودِ گزارش",
            "جدول‌های پهن خوابیده و فرم‌ها ایستاده چاپ می‌شوند"));
        Orientations.Add(new SetupOption("portrait", "ورقِ ایستاده", "Portrait"));
        Orientations.Add(new SetupOption("landscape", "ورقِ خوابیده", "Landscape"));

        _paper = Papers.FirstOrDefault(p => p.Value == s.Paper) ?? Papers[0];
        _margin = Margins.FirstOrDefault(m => m.Value == s.MarginPreset) ?? Margins[1];
        _orientation = Orientations[s.Orientation switch
        {
            PageOrientation.Portrait => 1,
            PageOrientation.Landscape => 2,
            _ => 0,
        }];

        _customWidth = Num(s.CustomWidth);
        _customHeight = Num(s.CustomHeight);
        _top = Num(s.MarginTop); _bottom = Num(s.MarginBottom);
        _left = Num(s.MarginLeft); _right = Num(s.MarginRight);

        _headerRight = s.HeaderRight; _headerCenter = s.HeaderCenter; _headerLeft = s.HeaderLeft;
        _footerRight = s.FooterRight; _footerCenter = s.FooterCenter; _footerLeft = s.FooterLeft;

        _firstPage = s.FirstPage.ToString();
        _dpi = s.Dpi.ToString();
        _skipFirst = s.SkipFirstHeaderFooter;
        _scalePercent = s.ScalePercent.ToString();
    }

    private static string Num(decimal v) => Shamsi.Money(v);

    private static string PaperNote(string k)
    {
        if (k == "Custom") return "عرض و بلندا را خودتان بنویسید";
        var p = PageSetup.Papers[k];
        return Shamsi.Money(p.W) + " × " + Shamsi.Money(p.H) + " میلی‌متر";
    }

    private static string MarginNote(string k)
    {
        var m = PageSetup.MarginPresets[k];
        return "بالا " + Shamsi.Money(m.Top) + " · پایین " + Shamsi.Money(m.Bottom)
             + " · چپ " + Shamsi.Money(m.Left) + " · راست " + Shamsi.Money(m.Right) + " میلی‌متر";
    }

    public ObservableCollection<SetupOption> Papers { get; } = new();
    public ObservableCollection<SetupOption> Margins { get; } = new();
    public ObservableCollection<SetupOption> Orientations { get; } = new();

    [ObservableProperty] private SetupOption _paper;
    [ObservableProperty] private SetupOption _margin;
    [ObservableProperty] private SetupOption _orientation;

    [ObservableProperty] private string _customWidth = "210";
    [ObservableProperty] private string _customHeight = "297";
    [ObservableProperty] private string _top = "";
    [ObservableProperty] private string _bottom = "";
    [ObservableProperty] private string _left = "";
    [ObservableProperty] private string _right = "";

    [ObservableProperty] private string _headerRight = "";
    [ObservableProperty] private string _headerCenter = "";
    [ObservableProperty] private string _headerLeft = "";
    [ObservableProperty] private string _footerRight = "";
    [ObservableProperty] private string _footerCenter = "";
    [ObservableProperty] private string _footerLeft = "";

    [ObservableProperty] private string _firstPage = "1";
    [ObservableProperty] private string _dpi = "144";
    [ObservableProperty] private bool _skipFirst;

    /// <summary>درصدِ «مقیاسِ دلخواه» — همان کادرِ ‎٪ از اندازهٔ عادی‎ی اکسل.</summary>
    [ObservableProperty] private string _scalePercent = "100";

    public bool IsCustomPaper => Paper.Value == "Custom";
    public bool IsCustomMargin => Margin.Value == "custom";

    /// <summary>برداشتنِ یک کاغذِ آماده، عددهای «دلخواه» را هم با آن پر می‌کند.</summary>
    partial void OnPaperChanged(SetupOption value)
    {
        OnPropertyChanged(nameof(IsCustomPaper));
        if (value.Value != "Custom" && PageSetup.Papers.TryGetValue(value.Value, out var p))
        { CustomWidth = Num(p.W); CustomHeight = Num(p.H); }
    }

    /// <summary>برداشتنِ یک حاشیهٔ آماده، چهار عدد را با آن پر می‌کند.</summary>
    partial void OnMarginChanged(SetupOption value)
    {
        OnPropertyChanged(nameof(IsCustomMargin));
        if (value.Value != "custom" && PageSetup.MarginPresets.TryGetValue(value.Value, out var m))
        { Top = Num(m.Top); Bottom = Num(m.Bottom); Left = Num(m.Left); Right = Num(m.Right); }
    }

    /// <summary>کدهایی که در سربرگ/پاورقی کار می‌کنند — برای راهنمای زیرِ کادرها.</summary>
    public static string TokenHelp =>
        "کدها: &[ورق] · &[کل] · &[شمسی] · &[قمری] · &[میلادی] · &[ساعت] · &[نام] · &[Dates]";

    /// <summary>خروجی — همان چیزی که به سند داده می‌شود.</summary>
    public PageSetup Build() => _orig with
    {
        Paper = Paper.Value,
        CustomWidth = Pos(CustomWidth, 210m),
        CustomHeight = Pos(CustomHeight, 297m),
        Orientation = Orientation.Value switch
        {
            "portrait" => PageOrientation.Portrait,
            "landscape" => PageOrientation.Landscape,
            _ => PageOrientation.Auto,
        },
        MarginPreset = Margin.Value,
        MarginTop = NonNeg(Top), MarginBottom = NonNeg(Bottom),
        MarginLeft = NonNeg(Left), MarginRight = NonNeg(Right),
        HeaderLeft = HeaderLeft, HeaderCenter = HeaderCenter, HeaderRight = HeaderRight,
        FooterLeft = FooterLeft, FooterCenter = FooterCenter, FooterRight = FooterRight,
        FirstPage = (int)Math.Clamp(Shamsi.Num(FirstPage), 1m, 9999m),
        Dpi = (int)Math.Clamp(Shamsi.Num(Dpi), 72m, 400m),
        SkipFirstHeaderFooter = SkipFirst,
        ScalePercent = (int)Math.Clamp(Shamsi.Num(ScalePercent), 10m, 400m),
    };

    /// <summary>عددِ مثبت؛ خالی یا صفر به پیش‌فرض برمی‌گردد — ورقِ صفرعرض نداریم.</summary>
    private static decimal Pos(string s, decimal fallback)
    {
        var v = Shamsi.Num(s);
        return v > 0m ? v : fallback;
    }

    private static decimal NonNeg(string s) => Math.Max(0m, Shamsi.Num(s));
}
