using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.App.Printing;

/// <summary>
/// ══ صفحهٔ چاپ ══════════════════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (با عکسِ صفحهٔ چاپِ اکسل): «بخشِ پرینت باید این مدلی
/// باشد؛ در سایت هم همین مدل بود — با تمامِ منطق‌هایی که داشت و همان شکل و
/// شمایل.»
///
/// سایت این را دارد (‎pumpPrintStudio‎، خطِ ۲۵۴۶۰ به بعدِ ‎index.html‎) و شکلش
/// دقیقاً پشتِ‌صحنهٔ چاپِ اکسل است: یک ستونِ تنظیمات در یک طرف و پیش‌نمایشِ
/// زندهٔ ورق در طرفِ دیگر. ترتیبِ ستون هم همان است:
///
///     چاپ  ▸ دکمهٔ چاپ + تعدادِ نسخه
///     چاپگر ▸ نامِ چاپگر و «آماده» + انتخابِ چاپگر
///     تنظیمات ▸ کدام ورق‌ها · ورق‌ها از…تا · مرتب/نامرتب · جهت · کاغذ ·
///               حاشیه · مقیاس  ▸ «تنظیمِ ورق…»
///
/// و پایینِ پیش‌نمایش: ‎◀ [۱] از ۲ ▶‎ و دو دکمهٔ «حاشیه‌ها» و «هم‌اندازهٔ ورق».
///
/// ⚠️ سه چیز عمداً با نسخهٔ وب فرق دارد، و هر سه به‌سودِ درستیِ کار است:
///   ۱. پیش‌نمایش تصویرِ خودِ PDF است، نه یک تقلیدِ HTML — پس «هرچه می‌بینی
///      همان چاپ می‌شود» این‌جا واقعاً تضمین است.
///   ۲. مقیاس چهار حالت ندارد بلکه سه حالت دارد؛ دلیلش در <see cref="PrintScale"/>.
///   ۳. چاپ و ذخیره روی یک فایلِ PDFِ واقعی انجام می‌شوند، پس برنامه — برخلافِ
///      نسخهٔ وب که با ‎window.print()‎ یخ می‌زد — یک لحظه هم نمی‌ایستد.
/// </summary>
public sealed partial class DocumentPreviewViewModel : ObservableObject
{
    private readonly Func<PageSetup, IDocument> _build;
    private IDocument _doc;
    private readonly List<Bitmap> _pages = new();
    private readonly List<byte[]> _png = new();

    /// <summary>تا وقتی کادرها از روی تنظیمِ ذخیره‌شده پر می‌شوند، چیزی ذخیره نشود.</summary>
    private bool _loading = true;

    public DocumentPreviewViewModel(Func<PageSetup, IDocument> build, string title,
                                    PageSetup? setup = null)
    {
        _build = build;
        Title = title;
        Setup = setup ?? PageSetup.Default;

        FillOptions();
        PullFromSetup();

        _doc = _build(Setup);
        // ⚠️ در سازنده مستقیم نشانده می‌شود، نه از راهِ دیسپچر: این شیء هنوز به
        // هیچ کادری بسته نشده، و خودِ سازنده روی نخِ پس‌زمینه صدا زده می‌شود
        // (‎Documents.ShowAsync‎). رفتن به نخِ رابط این‌جا فقط یک انتظارِ بی‌دلیل
        // بود — و در جایی که حلقهٔ رابط نچرخد، یک قفلِ کامل.
        Apply(RenderPages(), marshal: false);
        _loading = false;
    }

    public string Title { get; }

    /// <summary>تنظیمِ جاری — همان ‎S‎ی نسخهٔ وب.</summary>
    public PageSetup Setup { get; private set; }

    /// <summary>هر بار که تنظیم عوض شود صدا زده می‌شود تا ذخیره‌اش کند.</summary>
    public Action<PageSetup>? SetupChanged { get; set; }

    public IReadOnlyList<Bitmap> Pages => _pages;

    [ObservableProperty] private int _pageIndex;
    [ObservableProperty] private Bitmap? _currentPage;
    [ObservableProperty] private string _pageLabel = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;

    /// <summary>ستونِ تنظیمات باز است؟ — دکمهٔ «⚙ تنظیمات»ِ نوارِ بالا.</summary>
    [ObservableProperty] private bool _railOpen = true;

    // ══════════════════════════════════════════════════════════════════════
    //  کادرهای ستونِ تنظیمات
    // ══════════════════════════════════════════════════════════════════════

    public ObservableCollection<SetupOption> Whats { get; } = new();
    public ObservableCollection<SetupOption> Collates { get; } = new();
    public ObservableCollection<SetupOption> Orders { get; } = new();
    public ObservableCollection<SetupOption> Orientations { get; } = new();
    public ObservableCollection<SetupOption> Papers { get; } = new();
    public ObservableCollection<SetupOption> MarginChoices { get; } = new();
    public ObservableCollection<SetupOption> Scales { get; } = new();

    [ObservableProperty] private SetupOption? _what;
    [ObservableProperty] private SetupOption? _collate;
    [ObservableProperty] private SetupOption? _order;
    [ObservableProperty] private SetupOption? _orientation;
    [ObservableProperty] private SetupOption? _paper;
    [ObservableProperty] private SetupOption? _margin;
    [ObservableProperty] private SetupOption? _scale;

    [ObservableProperty] private string _copiesText = "۱";

    /// <summary>
    /// ══ «از ورق … تا ورق …» — کشو، نه کادرِ آزاد ═════════════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «اگر گزارش ۳ ورق دارد، گزینه‌ها باید ۱ و ۲ و ۳
    /// باشند… نباید عدد صفحات Hard-code شود.»
    ///
    /// پس فهرست از خودِ سند می‌آید (<see cref="PageNumbers"/>) و با هر بار
    /// ساخته شدنِ دوبارهٔ ورق‌ها از نو پر می‌شود. یعنی نوشتنِ عددی که وجود
    /// ندارد از اساس ممکن نیست.
    /// </summary>
    public ObservableCollection<int> PageNumbers { get; } = new();

    [ObservableProperty] private int _fromPage = 1;
    [ObservableProperty] private int _toPage = 1;

    /// <summary>«صفحاتِ انتخابی» — «۱،۳،۵» یا «۱-۳، ۷».</summary>
    [ObservableProperty] private string _customPagesText = "";

    /// <summary>«ورق‌ها: از … تا …» فقط در حالتِ بازه به کار می‌آید.</summary>
    public bool IsRange => Setup.What == PrintWhat.Range;

    /// <summary>کادرِ «صفحاتِ انتخابی» فقط در همان حالت.</summary>
    public bool IsCustomPages => Setup.What == PrintWhat.Custom;

    private void FillOptions()
    {
        // ── کدام ورق‌ها — همان ‎WHAT‎ی سایت ────────────────────────────────
        Whats.Add(new SetupOption("all", "چاپِ همهٔ گزارش", "همهٔ ورق‌ها چاپ می‌شوند", "🗒"));
        Whats.Add(new SetupOption("current", "چاپِ همین ورق", "فقط ورقی که می‌بینید", "📄"));
        Whats.Add(new SetupOption("range", "چاپِ بازهٔ ورق‌ها", "از شمارهٔ ورقی تا شمارهٔ ورقی", "🔢"));
        Whats.Add(new SetupOption("custom", "صفحاتِ انتخابی", "مثلِ ۱،۳،۵ یا ۱-۳، ۷", "✳"));

        // ── مرتب/نامرتب — همان ‎COLL‎ ──────────────────────────────────────
        Collates.Add(new SetupOption("1", "مرتب", "۱،۲،۳   ۱،۲،۳   ۱،۲،۳", "🔃"));
        Collates.Add(new SetupOption("0", "نامرتب", "۱،۱،۱   ۲،۲،۲   ۳،۳،۳", "🔀"));

        // ── ترتیبِ چاپ ────────────────────────────────────────────────────
        // ⚠️ نمایشی نیست: ‎PrintJob.Order‎ واقعاً فهرست را وارونه می‌کند و هم
        // PDF و هم چاپِ مستقیم از همان فهرست ساخته می‌شوند.
        Orders.Add(new SetupOption("normal", "ترتیبِ عادی", "۱ ← ۲ ← ۳", "⬇"));
        Orders.Add(new SetupOption("reverse", "ترتیبِ وارونه", "۳ ← ۲ ← ۱", "⬆"));

        // ── جهت — همان ‎ORI‎ ───────────────────────────────────────────────
        Orientations.Add(new SetupOption("auto", "به‌انتخابِ خودِ گزارش",
            "جدول‌های پهن خوابیده و فرم‌ها ایستاده", "🅰"));
        Orientations.Add(new SetupOption("portrait", "ورقِ ایستاده", "Portrait", "📄"));
        Orientations.Add(new SetupOption("landscape", "ورقِ خوابیده", "Landscape", "📃"));

        // ── کاغذ — همان ‎paperOpts()‎، با اینچ کنارِ میلی‌متر مثلِ اکسل ─────
        foreach (var k in PageSetup.PaperOrder)
            Papers.Add(new SetupOption(k, k == "Custom" ? "اندازهٔ دلخواه" : k, PaperNote(k), "📐"));

        // ── حاشیه — همان ‎marginOpts()‎ ───────────────────────────────────
        MarginChoices.Add(new SetupOption("normal", "حاشیهٔ عادی", MarginNote("normal"), "▭"));
        MarginChoices.Add(new SetupOption("narrow", "حاشیهٔ باریک", MarginNote("narrow"), "▭"));
        MarginChoices.Add(new SetupOption("wide", "حاشیهٔ پهن", MarginNote("wide"), "▭"));
        MarginChoices.Add(new SetupOption("custom", "حاشیهٔ دلخواه",
            "عددهایش را در «تنظیمِ ورق» بگذارید", "▭"));

        // ── مقیاس — سه حالت؛ چرایی‌اش در ‎PrintScale‎ ──────────────────────
        Scales.Add(new SetupOption("none", "بدون مقیاس",
            "در اندازهٔ واقعیِ خودش — ستون‌ها خودشان تا عرضِ ورق جا می‌شوند", "🔍"));
        Scales.Add(new SetupOption("fit", "جا دادنِ ورق در یک صفحه",
            "هر ورق تا جایی کوچک می‌شود که کامل بنشیند", "⤡"));
        Scales.Add(new SetupOption("fitcols", "جا دادنِ همهٔ ستون‌ها در عرضِ ورق",
            "اگر جدول پهن‌تر از ورق باشد، یکنواخت کوچک می‌شود", "↔"));
        Scales.Add(new SetupOption("custom", "مقیاسِ دلخواه",
            "درصدش را در «تنظیمِ ورق» بگذارید", "％"));
    }

    private static string Inch(decimal mm) => (mm / 25.4m).ToString("0.00");

    private static string PaperNote(string k)
    {
        if (k == "Custom") return "عرض و بلندا را خودتان بنویسید";
        var p = PageSetup.Papers[k];
        return $"{p.W} × {p.H} mm  ·  {Inch(p.W)}\" × {Inch(p.H)}\"";
    }

    private static string MarginNote(string k)
    {
        var m = PageSetup.MarginPresets[k];
        return "چپ " + Shamsi.Money(m.Left) + " · راست " + Shamsi.Money(m.Right)
             + " · بالا " + Shamsi.Money(m.Top) + " · پایین " + Shamsi.Money(m.Bottom) + " میلی‌متر";
    }

    private static SetupOption Pick(ObservableCollection<SetupOption> list, string value) =>
        list.FirstOrDefault(o => o.Value == value) ?? list[0];

    /// <summary>کادرها را از روی تنظیمِ جاری پر می‌کند (بی راه انداختنِ ذخیره).</summary>
    private void PullFromSetup()
    {
        var was = _loading;
        _loading = true;

        What = Pick(Whats, Setup.What switch
        {
            PrintWhat.Current => "current",
            PrintWhat.Range => "range",
            PrintWhat.Custom => "custom",
            _ => "all",
        });
        Collate = Pick(Collates, Setup.Collate ? "1" : "0");
        Order = Pick(Orders, Setup.Order == PrintOrder.Reverse ? "reverse" : "normal");
        Orientation = Pick(Orientations, Setup.Orientation switch
        {
            PageOrientation.Portrait => "portrait",
            PageOrientation.Landscape => "landscape",
            _ => "auto",
        });
        Paper = Pick(Papers, Setup.Paper);
        Margin = Pick(MarginChoices, Setup.MarginPreset);
        Scale = Pick(Scales, Setup.Scale switch
        {
            PrintScale.FitPage => "fit",
            PrintScale.FitColumns => "fitcols",
            PrintScale.Custom => "custom",
            _ => "none",
        });

        CopiesText = Shamsi.Money(Setup.Copies);
        FromPage = ClampPage(Setup.From);
        ToPage = ClampPage(Setup.To);
        CustomPagesText = Setup.CustomPages;

        _loading = was;
        RefreshNotes();
    }

    /// <summary>تنظیمِ تازه از روی کادرها — بقیهٔ فیلدها از «تنظیمِ ورق» می‌آیند.</summary>
    private PageSetup Compose()
    {
        var next = Setup with
        {
            What = What?.Value switch
            {
                "current" => PrintWhat.Current,
                "range" => PrintWhat.Range,
                "custom" => PrintWhat.Custom,
                _ => PrintWhat.All,
            },
            Collate = Collate?.Value != "0",
            Order = Order?.Value == "reverse" ? PrintOrder.Reverse : PrintOrder.Normal,
            Orientation = Orientation?.Value switch
            {
                "portrait" => PageOrientation.Portrait,
                "landscape" => PageOrientation.Landscape,
                _ => PageOrientation.Auto,
            },
            Scale = Scale?.Value switch
            {
                "fit" => PrintScale.FitPage,
                "fitcols" => PrintScale.FitColumns,
                "custom" => PrintScale.Custom,
                _ => PrintScale.None,
            },
            Copies = (int)Math.Clamp(Shamsi.Num(CopiesText), 1m, 999m),
            From = FromPage,
            To = ToPage,
            CustomPages = CustomPagesText,
        };

        // کاغذ و حاشیه عددهای همراهشان را هم با خود می‌آورند — مثلِ سایت
        if (Paper is not null && Paper.Value != next.Paper) next = next.WithPaper(Paper.Value);
        if (Margin is not null && Margin.Value != next.MarginPreset)
            next = next.WithMarginPreset(Margin.Value);

        return next;
    }

    // هر کادری که عوض شود، همین یکی صدا زده می‌شود
    partial void OnWhatChanged(SetupOption? v) => Push();
    partial void OnCollateChanged(SetupOption? v) => Push();
    partial void OnOrderChanged(SetupOption? v) => Push();
    partial void OnOrientationChanged(SetupOption? v) => Push();
    partial void OnPaperChanged(SetupOption? v) => Push();
    partial void OnMarginChanged(SetupOption? v) => Push();
    partial void OnScaleChanged(SetupOption? v) => Push();
    partial void OnCopiesTextChanged(string v) => Push();
    partial void OnCustomPagesTextChanged(string v) => Push();

    // ⚠️ کشوها با عددِ خالی (۰) هم خبر می‌دهند — لحظه‌ای که فهرست از نو پر
    // می‌شود. آن لحظه نباید تنظیم عوض شود، وگرنه بازهٔ کاربر پاک می‌شد.
    partial void OnFromPageChanged(int v) { if (v >= 1) Push(); }
    partial void OnToPageChanged(int v) { if (v >= 1) Push(); }

    private void Push()
    {
        if (_loading) return;
        _ = ApplyAsync();
    }

    /// <summary>
    /// تنظیمِ تازه را می‌نشاند و — فقط اگر چیدمانِ ورق عوض شده باشد — سند را
    /// از نو می‌سازد.
    ///
    /// ⚠️ «تعدادِ نسخه» و «کدام ورق‌ها» چیدمان را عوض نمی‌کنند، پس با آن‌ها
    /// سند از نو ساخته نمی‌شود؛ وگرنه هر بار که کاربر عددِ نسخه را بالا
    /// می‌برد، کلِ گزارش دوباره رسم می‌شد.
    /// </summary>
    private async Task ApplyAsync()
    {
        var next = Compose();
        var relayout = next.LayoutOnly() != Setup.LayoutOnly();
        var prev = Setup;
        Setup = next;

        if (relayout)
        {
            Busy = true;
            Status = "در حال ساختنِ دوبارهٔ ورق…";
            try
            {
                var pages = await Task.Run(() =>
                {
                    _doc = _build(next);
                    return RenderPages();
                });
                Apply(pages);
                Status = "";
            }
            catch (Exception ex)
            {
                // تنظیمی که سند را نمی‌سازد (کاغذِ خیلی کوچک، حاشیهٔ خیلی بزرگ)
                // نباید پنجره را ببندد — ورقِ قبلی سرِ جایش می‌ماند.
                Setup = prev;
                Status = "این تنظیم روی ورق جا نمی‌شود: " + ex.Message;
                try
                {
                    var back = await Task.Run(() => { _doc = _build(prev); return RenderPages(); });
                    Apply(back);
                }
                catch { }
                PullFromSetup();
                Busy = false;
                return;
            }
            Busy = false;
        }

        SetupChanged?.Invoke(Setup);
        RefreshNotes();
    }

    /// <summary>
    /// از بیرون (پنجرهٔ «تنظیمِ ورق») تنظیمِ کامل می‌آید — کادرها هم با آن
    /// هم‌گام می‌شوند.
    /// </summary>
    public async Task ApplyFromDialogAsync(PageSetup next)
    {
        var prev = Setup;
        Setup = next;
        PullFromSetup();
        if (next.LayoutOnly() == prev.LayoutOnly()) { SetupChanged?.Invoke(next); return; }

        Busy = true;
        Status = "در حال ساختنِ دوبارهٔ ورق…";
        try
        {
            var pages = await Task.Run(() => { _doc = _build(next); return RenderPages(); });
            Apply(pages);
            Status = "";
            SetupChanged?.Invoke(next);
        }
        catch (Exception ex)
        {
            Setup = prev;
            PullFromSetup();
            Status = "این تنظیم روی ورق جا نمی‌شود: " + ex.Message;
            try
            {
                var back = await Task.Run(() => { _doc = _build(prev); return RenderPages(); });
                Apply(back);
            }
            catch { }
        }
        Busy = false;
        RefreshNotes();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  زیرنویسِ زندهٔ کادرها — همان ‎refreshCards()‎ی سایت
    // ══════════════════════════════════════════════════════════════════════
    //
    // زیرنویسِ کادرِ «کدام ورق‌ها» صریح می‌گوید دقیقاً چه چاپ می‌شود («هر ۳
    // ورق»، «فقط ورقِ ۲»، «ورقِ ۲ تا ۵») — نه یک جملهٔ کلی.

    public string WhatNote
    {
        get
        {
            if (Error is { Length: > 0 }) return Error;
            var n = PickedPages().Count;
            return Setup.What switch
            {
                PrintWhat.Current => "فقط ورقِ " + Shamsi.Money(PageIndex + 1) + " چاپ می‌شود",
                PrintWhat.Range => "ورقِ " + Shamsi.Money(Setup.From) + " تا "
                                   + Shamsi.Money(Setup.To) + " چاپ می‌شود",
                PrintWhat.Custom => Shamsi.Money(n) + " ورق چاپ می‌شود: "
                                    + string.Join("، ", PickedPages().Select(x => Shamsi.Money(x))),
                _ => "هر " + Shamsi.Money(PageCount) + " ورق چاپ می‌شود",
            };
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  وارسی — پیش از چاپ، نه بعدش
    // ══════════════════════════════════════════════════════════════════════
    //
    // ⚠️ خودِ قاعده‌ها در ‎PrintJob.Validate‎ هستند، بیرونِ هر رابطی، تا بشود
    // آزمودشان. این‌جا فقط نتیجه نشان داده می‌شود و دکمهٔ چاپ بسته می‌شود.

    /// <summary>جملهٔ خطای جاری — خالی یعنی همه‌چیز درست است.</summary>
    [ObservableProperty] private string _error = "";

    partial void OnErrorChanged(string v)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanPrint));
        OnPropertyChanged(nameof(WhatNote));
    }

    public bool HasError => Error.Length > 0;

    /// <summary>دکمه‌های «چاپ» و «PDF» فقط وقتی کار می‌کنند که تنظیم درست باشد.</summary>
    public bool CanPrint => !HasError && PageCount > 0;

    private void Revalidate() => Error = PrintJob.Validate(Setup, PageCount) ?? "";

    /// <summary>عددِ ورق، بریده‌شده به دامنهٔ واقعیِ همین سند.</summary>
    private int ClampPage(int n) => PageCount <= 0 ? Math.Max(1, n) : Math.Clamp(n, 1, PageCount);

    /// <summary>
    /// فهرستِ کشوهای «از/تا» را با شمارِ واقعیِ ورق‌ها از نو می‌سازد.
    ///
    /// ⚠️ انتخابِ کاربر پیش از پاک کردنِ فهرست برداشته و بعد برگردانده می‌شود:
    /// خالی کردنِ ‎ItemsSource‎ خودبه‌خود ‎SelectedItem‎ را صفر می‌کند و بی این،
    /// هر بار که کاغذ عوض می‌شد بازهٔ کاربر هم می‌پرید.
    /// </summary>
    private void RebuildPageNumbers()
    {
        var wantFrom = FromPage;
        var wantTo = ToPage;

        var was = _loading;
        _loading = true;
        PageNumbers.Clear();
        for (var i = 1; i <= Math.Max(1, PageCount); i++) PageNumbers.Add(i);
        FromPage = ClampPage(wantFrom);
        ToPage = ClampPage(wantTo);
        _loading = was;
    }

    public string PaperNoteText => Paper is null ? "" : PaperNote(Paper.Value);

    public string MarginNoteText =>
        Setup.MarginPreset == "custom"
            ? "چپ " + Shamsi.Money(Setup.MarginLeft) + " · راست " + Shamsi.Money(Setup.MarginRight)
              + " · بالا " + Shamsi.Money(Setup.MarginTop) + " · پایین "
              + Shamsi.Money(Setup.MarginBottom) + " میلی‌متر"
            : Margin?.Note ?? "";

    public string ScaleNote => Setup.Scale switch
    {
        PrintScale.Custom => Shamsi.Money(Setup.ScalePercent) + "٪ از اندازهٔ واقعی",
        PrintScale.FitPage => "هر ورق تا جایی کوچک می‌شود که کامل بنشیند",
        PrintScale.FitColumns => "اگر جدول پهن‌تر از ورق باشد، یکنواخت کوچک می‌شود",
        _ => "در اندازهٔ واقعیِ خودش",
    };

    /// <summary>خطِ راهنمای زیرِ «تنظیمِ ورق…» — همان ‎xpr-scaleinfo‎ی سایت.</summary>
    public string ScaleInfo =>
        "کاغذ " + (Setup.Paper == "Custom"
                    ? Shamsi.Money(Setup.CustomWidth) + "×" + Shamsi.Money(Setup.CustomHeight)
                    : Setup.Paper)
        + " · " + (Setup.Orientation == PageOrientation.Landscape ? "خوابیده"
                   : Setup.Orientation == PageOrientation.Portrait ? "ایستاده" : "خودکار")
        + " · " + Shamsi.Money(PageCount) + " ورق";

    private void RefreshNotes()
    {
        Revalidate();
        RefreshPicked();

        foreach (var n in new[]
        {
            nameof(WhatNote), nameof(PaperNoteText), nameof(MarginNoteText),
            nameof(ScaleNote), nameof(ScaleInfo), nameof(IsRange), nameof(IsCustomPages),
            nameof(PageCount), nameof(CanPrint), nameof(HasError),
        })
            OnPropertyChanged(n);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  بزرگ‌نمایی
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>پهنای ورق روی صفحه (پیکسل).</summary>
    [ObservableProperty] private double _pageWidth = 900;

    public string ZoomLabel => PersianText.Num((int)Math.Round(PageWidth / 9.0)) + "٪";

    partial void OnPageWidthChanged(double v)
    {
        OnPropertyChanged(nameof(ZoomLabel));
        OnPropertyChanged(nameof(GuideMargin));
        foreach (var p in PreviewPages) { p.Width = v; p.Guide = GuideFor(p.Image); }
    }

    private const double MinW = 240, MaxW = 3200, Step = 1.25;

    [RelayCommand] private void ZoomIn() => PageWidth = Math.Min(MaxW, PageWidth * Step);
    [RelayCommand] private void ZoomOut() => PageWidth = Math.Max(MinW, PageWidth / Step);

    /// <summary>«هم‌اندازهٔ پهنا».</summary>
    [RelayCommand] private void ZoomFit() => PageWidth = Math.Clamp(FitWidth, MinW, MaxW);

    /// <summary>
    /// «هم‌اندازهٔ ورق» — دکمهٔ دومِ گوشهٔ نوارِ پایین در سایت: کلِ ورق در قاب
    /// دیده شود، نه فقط پهنایش.
    /// </summary>
    [RelayCommand]
    private void ZoomPage()
    {
        var bmp = CurrentPage;
        if (bmp is null || bmp.PixelSize.Height <= 0) { ZoomFit(); return; }
        var byHeight = FitHeight * bmp.PixelSize.Width / bmp.PixelSize.Height;
        PageWidth = Math.Clamp(Math.Min(FitWidth, byHeight), MinW, MaxW);
    }

    public double FitWidth { get; set; } = 900;
    public double FitHeight { get; set; } = 700;

    // ══════════════════════════════════════════════════════════════════════
    //  خط‌چینِ حاشیه‌ها — دکمهٔ «نمایشِ حاشیه‌ها»ی نوارِ پایین
    // ══════════════════════════════════════════════════════════════════════
    //
    // کادرِ خط‌چین دقیقاً روی مرزِ حاشیه کشیده می‌شود تا کاربر ببیند محتوا کجا
    // تمام می‌شود. اندازه‌اش از خودِ تصویرِ ورق درمی‌آید (پیکسل ÷ dpi = اینچ)،
    // نه از حدس — پس با هر کاغذ و هر جهتی درست می‌ماند.

    [ObservableProperty] private bool _showGuides = true;

    partial void OnShowGuidesChanged(bool v)
    {
        OnPropertyChanged(nameof(GuideMargin));
        foreach (var p in PreviewPages) p.ShowGuides = v;
    }

    public Thickness GuideMargin => GuideFor(CurrentPage);

    /// <summary>
    /// خط‌چینِ حاشیه برای یک ورقِ مشخص.
    ///
    /// اندازه از خودِ تصویر درمی‌آید (پیکسل ÷ dpi = اینچ)، نه از حدس — پس با
    /// هر کاغذ و هر جهتی درست می‌ماند.
    /// </summary>
    private Thickness GuideFor(Bitmap? bmp)
    {
        if (bmp is null || bmp.PixelSize.Width <= 0) return new Thickness(0);

        var mmWide = bmp.PixelSize.Width / (double)Math.Clamp(Setup.Dpi, 72, 400) * 25.4;
        if (mmWide <= 0) return new Thickness(0);
        var px = PageWidth / mmWide;                 // پیکسلِ صفحه در هر میلی‌متر

        var m = Setup.Margins();
        return new Thickness((double)m.Left * px, (double)m.Top * px,
                             (double)m.Right * px, (double)m.Bottom * px);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  پیش‌نمایشِ همهٔ ورق‌ها، پشتِ‌سرِ هم
    // ══════════════════════════════════════════════════════════════════════
    //
    // خواستهٔ صریحِ صاحب ریپو: «اگر گزارش ۳ صفحه دارد: [صفحه ۱] [صفحه ۲]
    // [صفحه ۳] — هر صفحه مثلِ یک برگهٔ واقعی، نه یک کادرِ سفیدِ ساده.»
    //
    // پیش از این فقط **یک** ورق دیده می‌شد و باید با ◀ ▶ ورق می‌زدی. حالا
    // همه‌شان در یک فهرستِ لغزنده‌اند و ◀ ▶ همان فهرست را جابه‌جا می‌کند.
    //
    // ⚠️ فهرست ‎ListBox‎ است نه ‎ItemsControl‎: مجازی‌سازی و «برو به ورقِ فلان»
    // را خودش دارد، و گزارشِ صد‌ورقی نباید صد تصویر را با هم بچیند.

    public ObservableCollection<PreviewPage> PreviewPages { get; } = new();

    /// <summary>
    /// ورق‌هایی که چاپ **نمی‌شوند** اصلاً دیده نشوند؟
    ///
    /// ⚠️ در حالتِ «چاپِ همین ورق» عمداً اثری ندارد: آن‌جا تنها راهِ عوض کردنِ
    /// «همین ورق»، رفتن روی ورقِ دیگر است — اگر بقیه پنهان می‌شدند، کاربر در
    /// همان یک ورق حبس می‌شد و دیگر نمی‌توانست ورقِ دیگری را انتخاب کند.
    /// </summary>
    [ObservableProperty] private bool _onlyPrinted = true;

    partial void OnOnlyPrintedChanged(bool v) => RefreshPicked();

    /// <summary>کدام ورق چاپ می‌شود و کدام نه — روی خودِ فهرستِ پیش‌نمایش.</summary>
    private void RefreshPicked()
    {
        if (PreviewPages.Count == 0) return;

        var picked = new HashSet<int>(PrintJob.Picked(Setup, _pages.Count, PageIndex + 1));
        var hide = OnlyPrinted && Setup.What != PrintWhat.Current && !HasError;

        foreach (var p in PreviewPages)
        {
            p.IsPicked = picked.Contains(p.Number);
            p.IsShown = p.IsPicked || !hide;
        }
    }


    // ══════════════════════════════════════════════════════════════════════
    //  ورق‌ها
    // ══════════════════════════════════════════════════════════════════════

    public int PageCount => _pages.Count;

    /// <summary>کادرِ عددیِ نوارِ پایین — «۱» از «۲».</summary>
    public string PageNumberText
    {
        get => Shamsi.Money(PageIndex + 1);
        set
        {
            var n = (int)Shamsi.Num(value);
            if (n >= 1 && n <= _pages.Count) PageIndex = n - 1;
            else OnPropertyChanged();
        }
    }

    /// <summary>ورق‌ها را می‌سازد — روی نخِ پس‌زمینه، بی دست زدن به رابط.</summary>
    private List<byte[]> RenderPages() =>
        _doc.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = Math.Clamp(Setup.Dpi, 72, 400),
        }).ToList();

    /// <summary>ورق‌های تازه را می‌نشاند — روی نخِ رابط.</summary>
    private void Apply(List<byte[]> pages, bool marshal = true)
    {
        void Set()
        {
            // ⚠️ فهرستِ برگه‌ها **پیش از** دور انداختنِ تصویرها خالی می‌شود:
            // اگر ‎ListBox‎ هنوز به تصویری بسته باشد که ‎Dispose‎ شده، همان
            // لحظهٔ چیدنِ بعدی برنامه می‌افتد.
            PreviewPages.Clear();

            foreach (var b in _pages) b.Dispose();
            _pages.Clear();
            _png.Clear();

            foreach (var bytes in pages)
            {
                using var ms = new MemoryStream(bytes);
                _pages.Add(new Bitmap(ms));
                _png.Add(bytes);
            }

            for (var i = 0; i < _pages.Count; i++)
                PreviewPages.Add(new PreviewPage(i + 1, _pages[i], PageWidth,
                                                 GuideFor(_pages[i]), ShowGuides));

            // فهرستِ کشوهای «از/تا» از همین‌جا می‌آید — پس هیچ‌وقت عددی که
            // ورقی پشتش نیست در کشو نمی‌افتد.
            RebuildPageNumbers();

            PageIndex = 0;
            Show();
            OnPropertyChanged(nameof(PageCount));
            RefreshNotes();
        }

        if (!marshal || Dispatcher.UIThread.CheckAccess()) Set();
        else Dispatcher.UIThread.Invoke(Set);
    }

    /// <summary>سند را با تنظیمِ تازه از نو می‌سازد (برای آزمون‌ها و مسیرهای قدیمی).</summary>
    public void Rebuild(PageSetup setup)
    {
        Setup = setup;
        _doc = _build(setup);
        Apply(RenderPages());
        PullFromSetup();
    }

    private void Show()
    {
        CurrentPage = _pages.Count == 0 ? null : _pages[Math.Clamp(PageIndex, 0, _pages.Count - 1)];
        PageLabel = _pages.Count == 0 ? "—" : $"{PageIndex + 1} از {_pages.Count}";
        OnPropertyChanged(nameof(PageNumberText));
        OnPropertyChanged(nameof(GuideMargin));
        OnPropertyChanged(nameof(WhatNote));

        // «چاپِ همین ورق» به ورقِ باز بند است — با ورق زدن، نشانِ برگه‌ها هم
        // باید همان لحظه عوض شود.
        if (Setup.What == PrintWhat.Current) RefreshPicked();
    }

    partial void OnPageIndexChanged(int value) => Show();

    [RelayCommand] private void Next() { if (PageIndex + 1 < _pages.Count) PageIndex++; }
    [RelayCommand] private void Prev() { if (PageIndex > 0) PageIndex--; }

    // ══════════════════════════════════════════════════════════════════════
    //  کدام ورق‌ها، و به چه ترتیبی — همان ‎doPrint()‎ی سایت
    // ══════════════════════════════════════════════════════════════════════

    // ⚠️ خودِ حساب در ‎PrintJob‎ است، بیرونِ هر رابطی — تا بشود واقعاً آزمودش.
    // این‌جا فقط «چند ورق داریم» و «کدام ورق باز است» به آن داده می‌شود.

    /// <summary>شمارهٔ ورق‌های انتخاب‌شده (از ۱).</summary>
    public IReadOnlyList<int> PickedPages() =>
        PrintJob.Picked(Setup, _pages.Count, PageIndex + 1);

    /// <summary>
    /// همان‌ها، با تعدادِ نسخه و ترتیبِ مرتب/نامرتب و عادی/وارونه.
    ///
    /// ⚠️ نامش عمداً ‎PrintOrder‎ نیست: از وقتی ترتیبِ چاپ خودش یک نوع شد
    /// (<see cref="PumpYaqobi.Reporting.Pdf.PrintOrder"/>)، متدی به همان نام
    /// داخلِ این کلاس جلوی دیده شدنِ آن نوع را می‌گرفت و ‎PrintOrder.Reverse‎
    /// اصلاً کامپایل نمی‌شد.
    /// </summary>
    public IReadOnlyList<int> PrintOrderPages() =>
        PrintJob.Order(Setup, _pages.Count, PageIndex + 1);

    // ══════════════════════════════════════════════════════════════════════
    //  ذخیره و چاپ
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// فایلِ PDF را می‌نویسد و نشانی‌اش را برمی‌گرداند.
    ///
    /// ⚠️ اگر همهٔ ورق‌ها یک‌بار خواسته شده باشند، همان سندِ اصلی نوشته می‌شود
    /// — یعنی PDFِ برداری با متنِ قابلِ جست‌وجو. فقط وقتی بازه یا نسخهٔ چندتایی
    /// خواسته شده، ورق‌های انتخابی از روی تصویرِ خودشان چیده می‌شوند (موتورِ
    /// سند برشِ ورق ندارد). آن‌وقت هم چیزی که چاپ می‌شود دقیقاً همان است که
    /// در پیش‌نمایش دیده‌اید.
    /// </summary>
    public string SaveTo(string folder, bool wholeDocument = false)
    {
        Directory.CreateDirectory(folder);
        var safe = string.Join("_", Title.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(folder, safe + ".pdf");

        var order = PrintOrderPages();
        if (wholeDocument || order.Count == 0 || PrintJob.IsWholeDocument(order, _pages.Count))
            _doc.GeneratePdf(path);
        else PickedDocument(order).GeneratePdf(path);
        return path;
    }

    public byte[] ToPdfBytes()
    {
        using var ms = new MemoryStream();
        _doc.GeneratePdf(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// سندی از ورق‌های انتخاب‌شده، به همان ترتیب.
    ///
    /// ⚠️ خودِ چیدن در <see cref="PumpYaqobi.Reporting.Pdf.PagesDocument"/> است،
    /// کنارِ موتورِ چاپ — تا هم پیش‌نمایش و هم PDF و هم چاپِ مستقیم از یک
    /// جا بیایند و آزمون هم بتواند بی هیچ پنجره‌ای بسنجدش.
    /// </summary>
    private IDocument PickedDocument(IReadOnlyList<int> order) =>
        new PumpYaqobi.Reporting.Pdf.PagesDocument(
            order.Select(i => _png[i - 1]).ToList(), Setup.Dpi);
}

/// <summary>
/// یک برگه در فهرستِ پیش‌نمایش — تصویرِ خودِ ورق، شماره‌اش، و اینکه چاپ
/// می‌شود یا نه.
/// </summary>
public sealed partial class PreviewPage : ObservableObject
{
    public PreviewPage(int number, Bitmap image, double width,
                       Thickness guide, bool showGuides)
    {
        Number = number;
        Image = image;
        _width = width;
        _guide = guide;
        _showGuides = showGuides;
    }

    public int Number { get; }
    public Bitmap Image { get; }

    public string Label => "ورق " + Shamsi.Money(Number);

    [ObservableProperty] private double _width;
    [ObservableProperty] private Thickness _guide;
    [ObservableProperty] private bool _showGuides;

    /// <summary>این ورق در کارِ چاپِ جاری هست؟</summary>
    [ObservableProperty] private bool _isPicked = true;

    /// <summary>در فهرست دیده می‌شود؟ (وقتی «فقط ورق‌های چاپی» روشن است)</summary>
    [ObservableProperty] private bool _isShown = true;

    partial void OnIsPickedChanged(bool v) => OnPropertyChanged(nameof(SkipNote));

    /// <summary>نشانِ روی برگه‌ای که چاپ نمی‌شود.</summary>
    public string SkipNote => IsPicked ? "" : "چاپ نمی‌شود";
}
