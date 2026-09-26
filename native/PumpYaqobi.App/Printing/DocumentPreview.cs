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
    /// <summary>
    /// تصویرِ **فشردهٔ** هر ورق — نه تصویرِ بازشده.
    ///
    /// ⚠️ فقط ورقِ جلوی چشم باز می‌شود (<see cref="Show"/>). ورقِ تیز (۲۰۰ تا
    /// ۳۰۰ نقطه) بازشده ده‌ها مگابایت است؛ بیست ورقِ بازشده یعنی نیم‌گیگ رم
    /// برای چیزی که یکی‌اش دیده می‌شود.
    /// </summary>
    private readonly List<byte[]> _pages = new();
    private Bitmap? _shown;
    private int _shownIdx = -1;

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

        _doc = BuildSolved(Setup);
        // ⚠️ در سازنده مستقیم نشانده می‌شود، نه از راهِ دیسپچر: این شیء هنوز به
        // هیچ کادری بسته نشده، و خودِ سازنده روی نخِ پس‌زمینه صدا زده می‌شود
        // (‎Documents.ShowAsync‎). رفتن به نخِ رابط این‌جا فقط یک انتظارِ بی‌دلیل
        // بود — و در جایی که حلقهٔ رابط نچرخد، یک قفلِ کامل.
        // ⚠️ **پنجره منتظرِ تصویرِ ورق نمی‌ماند.** سنجشِ ‎printperf‎ نشان داد
        // ‎GenerateImages‎ی QuestPDF تنبل نیست: پیش از دادنِ ورقِ اول، همهٔ
        // ورق‌ها را می‌سازد (۶۰۰ ردیف ⇒ ۱٫۷ ثانیه). پس ساختن به پس‌زمینه
        // می‌رود و پنجره همان لحظه باز می‌شود؛ ورق‌ها که آماده شدند خودشان
        // می‌نشینند.
        StartPages(marshal: false, background: true);
        _loading = false;
        LoadPrinters();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  چاپگرهای واقعیِ ویندوز — «مثلِ اکسل» (۱۴۰۵/۰۷/۱۴، شرح در ‎Printers‎)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>چاپگرهای نصب‌شده، پیش‌فرض اول.</summary>
    public ObservableCollection<PrinterItem> Printers { get; } = new();

    /// <summary>چاپگرِ برگزیده — بارِ اول همانی که بارِ پیش زده شد، وگرنه پیش‌فرضِ ویندوز.</summary>
    [ObservableProperty] private PrinterItem? _printer;

    /// <summary>فهرست آمد و خالی نبود — وگرنه همان کادرِ «چاپگرِ پیش‌فرضِ ویندوز».</summary>
    [ObservableProperty] private bool _hasPrinters;

    /// <summary>هنوز فهرست را می‌خوانیم.</summary>
    [ObservableProperty] private bool _printersLoading = true;

    private bool _pickingPrinter;

    /// <summary>
    /// فهرست روی نخِ دیگر خوانده می‌شود — ‎EnumPrinters‎ با چاپگرِ شبکه‌ایِ
    /// خاموش می‌تواند چند ثانیه بماند، و پنجرهٔ چاپ نباید منتظرش بماند.
    /// </summary>
    [RelayCommand]
    private void LoadPrinters()
    {
        PrintersLoading = true;
        Task.Run(() =>
        {
            var list = PumpYaqobi.App.Printing.Printers.List();
            string remembered;
            try { remembered = Services.AppSettings.Load().LastPrinter; } catch { remembered = ""; }
            Dispatcher.UIThread.Post(() =>
            {
                _pickingPrinter = true;
                Printers.Clear();
                foreach (var p in list) Printers.Add(p);
                Printer = PumpYaqobi.App.Printing.Printers.Pick(list, Printer?.Name ?? remembered);
                HasPrinters = Printers.Count > 0;
                PrintersLoading = false;
                _pickingPrinter = false;
            });
        });
    }

    /// <summary>انتخابِ کاربر یادش می‌ماند — مقدارِ راحتی، پس ‎SaveSoon‎.</summary>
    partial void OnPrinterChanged(PrinterItem? value)
    {
        if (_pickingPrinter || value is null) return;
        try
        {
            var s = Services.AppSettings.Load();
            if (s.LastPrinter == value.Name) return;
            s.LastPrinter = value.Name;
            s.SaveSoon();
        }
        catch { }
    }

    /// <summary>
    /// ورق‌های انتخاب‌شده (با تعدادِ نسخه و ترتیب) را با کیفیتِ خودِ کاربر
    /// (‎Setup.Dpi‎) مستقیم به چاپگر می‌فرستد. خالی ⇒ رفت؛ وگرنه جملهٔ خطا.
    /// </summary>
    public Task<string> PrintToAsync(PrinterItem printer)
    {
        var order = PrintOrder();
        var dpi = Math.Clamp(Setup.Dpi, 72, 400);
        var doc = _doc;
        return Task.Run(() =>
        {
            try
            {
                var full = doc.GenerateImages(new ImageGenerationSettings
                { ImageFormat = ImageFormat.Png, RasterDpi = dpi }).ToList();
                var picked = order.Where(i => i >= 1 && i <= full.Count).Select(i => full[i - 1]);
                return PumpYaqobi.App.Printing.Printers.Print(printer.Name, picked, dpi, Title);
            }
            catch (Exception ex) { return "ورق‌ها ساخته نشدند — " + ex.GetType().Name; }
        });
    }

    public string Title { get; }

    /// <summary>تنظیمِ جاری — همان ‎S‎ی نسخهٔ وب.</summary>
    public PageSetup Setup { get; private set; }

    /// <summary>هر بار که تنظیم عوض شود صدا زده می‌شود تا ذخیره‌اش کند.</summary>
    public Action<PageSetup>? SetupChanged { get; set; }

    /// <summary>ورق‌های همین لحظه با چه dpi تصویر شده‌اند — برای سنجه‌ها.</summary>
    public int PagesDpi => _pagesDpi;

    /// <summary>
    /// ضریبِ صفحهٔ نمایشِ پنجره (۱٫۲۵ یعنی ویندوزِ ۱۲۵٪). پنجره می‌نشاندش؛
    /// ورقِ تیز باید با پیکسلِ **واقعیِ** نمایشگر بسنجد، نه پیکسلِ منطقی.
    /// </summary>
    public double RenderScaling
    {
        get => _renderScaling;
        set { _renderScaling = value is > 0.5 and < 8 ? value : 1; QueueSharpen(); }
    }
    private double _renderScaling = 1;

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
    public ObservableCollection<SetupOption> Orientations { get; } = new();
    public ObservableCollection<SetupOption> Papers { get; } = new();
    public ObservableCollection<SetupOption> MarginChoices { get; } = new();
    public ObservableCollection<SetupOption> Scales { get; } = new();

    // ══ «رنگی باشد یا سیاه و سفید» ══════════════════════════════════════════
    //
    // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «تو بخش پرینت هم دکمه‌ای وجود نداره که
    // رنگه باشه یا سیاه و سفید.»
    //
    // ⛔ و حق داشت: خودِ تنظیم از روزِ اول بود (‎PrintColor‎ و ‎DocStyle.Paint‎)
    // ولی **فقط داخلِ پنجرهٔ «تنظیمِ ورق»** دیده می‌شد — یعنی دو کلیک آن‌طرف‌تر،
    // در پنجره‌ای که کاربر برای عوض کردنِ حاشیه بازش می‌کند. کاری که همیشه
    // لازم است باید همان‌جا باشد که چشم است، کنارِ کاغذ و جهت و مقیاس.
    public ObservableCollection<SetupOption> Inks { get; } = new();

    [ObservableProperty] private SetupOption? _what;
    [ObservableProperty] private SetupOption? _collate;
    [ObservableProperty] private SetupOption? _orientation;
    [ObservableProperty] private SetupOption? _paper;
    [ObservableProperty] private SetupOption? _margin;
    [ObservableProperty] private SetupOption? _scale;
    /// <summary>رنگی · خاکستری · سیاه و سفید — «مرکّب».</summary>
    [ObservableProperty] private SetupOption? _ink;

    [ObservableProperty] private string _copiesText = "۱";
    [ObservableProperty] private string _fromText = "۱";
    [ObservableProperty] private string _toText = "۱";
    /// <summary>«۱،۳» یا «2,4-6» — کادرِ ورق‌های دلخواه، مثلِ «Pages»ِ چاپِ اکسل.</summary>
    [ObservableProperty] private string _pagesText = "";

    /// <summary>«ورق‌ها: از … تا …» فقط در حالتِ بازه به کار می‌آید.</summary>
    public bool IsRange => Setup.What == PrintWhat.Range;
    /// <summary>کادرِ ورق‌های دلخواه و تیک‌های هر ورق — فقط در حالتِ «ورق‌های دلخواه».</summary>
    public bool IsPages => Setup.What == PrintWhat.Pages;

    /// <summary>
    /// یک تیک برای هر ورق — همان چیزی که صاحب ریپو خواست: «از سه ورق فقط
    /// دومی را بگیرم، یا یک و سه را و دومی را نه». تیک‌ها و کادرِ متن یک چیزند:
    /// زدنِ تیک متن را می‌نویسد و نوشتنِ متن تیک‌ها را می‌زند.
    /// </summary>
    public ObservableCollection<PageCheck> PageChecks { get; } = new();
    private bool _syncingChecks;

    private void RebuildPageChecks()
    {
        _syncingChecks = true;
        try
        {
            // ⚠️ از خودِ کادر، نه از ‎Setup‎: کادر پیش از ‎Push‎ عوض می‌شود و اگر از
            // تنظیمِ کهنه می‌خواندیم، تیک‌ها یک قدم عقب می‌ماندند (در عکس دیده شد).
            var on = PrintJob.ParsePages(PagesText, _pages.Count).ToHashSet();
            if (PageChecks.Count != _pages.Count)
            {
                PageChecks.Clear();
                for (var i = 1; i <= _pages.Count; i++) PageChecks.Add(new PageCheck(i, on.Contains(i), OnCheckToggled));
            }
            else
                foreach (var c in PageChecks) c.IsOn = on.Contains(c.Number);
        }
        finally { _syncingChecks = false; }
    }

    private void OnCheckToggled()
    {
        if (_syncingChecks) return;
        PagesText = PrintJob.FormatPages(PageChecks.Where(c => c.IsOn).Select(c => c.Number));
    }

    private void FillOptions()
    {
        // ── کدام ورق‌ها — همان ‎WHAT‎ی سایت ────────────────────────────────
        Whats.Add(new SetupOption("all", "چاپِ همهٔ گزارش", "همهٔ ورق‌ها چاپ می‌شوند", "🗒"));
        Whats.Add(new SetupOption("current", "چاپِ همین ورق", "فقط ورقی که می‌بینید", "📄"));
        Whats.Add(new SetupOption("range", "چاپِ بازهٔ ورق‌ها", "از شمارهٔ ورقی تا شمارهٔ ورقی", "🔢"));
        Whats.Add(new SetupOption("pages", "چاپِ ورق‌های دلخواه", "مثلاً ۱،۳ یا ۲-۴ — یا تیکِ هر ورق", "☑"));

        // ── مرتب/نامرتب — همان ‎COLL‎ ──────────────────────────────────────
        Collates.Add(new SetupOption("1", "مرتب", "۱،۲،۳   ۱،۲،۳   ۱،۲،۳", "🔃"));
        Collates.Add(new SetupOption("0", "نامرتب", "۱،۱،۱   ۲،۲،۲   ۳،۳،۳", "🔀"));

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

        // ── مرکّب — همان ‎PrintColor‎، حالا کنارِ بقیهٔ کادرها ──────────────
        Inks.Add(new SetupOption("color", "رنگی", "همان رنگ‌هایی که می‌بینید", "🎨"));
        Inks.Add(new SetupOption("gray", "خاکستری", "رنگ‌ها به طیفِ خاکستری", "🌫"));
        Inks.Add(new SetupOption("bw", "سیاه و سفید", "بی هیچ رنگی — کم‌خرج‌ترین", "🖨"));
        MarginChoices.Add(new SetupOption("custom", "حاشیهٔ دلخواه",
            "عددهایش را در «تنظیمِ ورق» بگذارید", "▭"));

        // ── مقیاس — همان شش حالتِ ‎SCAL‎ی سایت، با همان نوشته‌ها ──────────
        Scales.Add(new SetupOption("none", "بدون مقیاس",
            "در اندازهٔ واقعیِ خودش چاپ می‌شود", "🔍"));
        Scales.Add(new SetupOption("fitCols", "جا دادن همهٔ ستون‌ها در یک ورق",
            "پهنای جدول تا عرضِ ورق کوچک می‌شود", "↔"));
        Scales.Add(new SetupOption("fitRows", "جا دادن همهٔ سطرها در یک ورق",
            "بلندیِ گزارش تا یک ورق کوچک می‌شود", "↕"));
        Scales.Add(new SetupOption("fitAll", "جا دادن کلِ گزارش در یک ورق",
            "همه‌چیز در یک ورقِ واحد", "⤡"));
        Scales.Add(new SetupOption("custom", "مقیاسِ دلخواه",
            "درصدش را در «تنظیمِ ورق» بگذارید", "％"));
        Scales.Add(new SetupOption("fitPages", "جا دادن در چند ورقِ مشخص",
            "پهنا و بلندا را در «تنظیمِ ورق» بگذارید", "▦"));
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
            PrintWhat.Pages => "pages",
            _ => "all",
        });
        Collate = Pick(Collates, Setup.Collate ? "1" : "0");
        Orientation = Pick(Orientations, Setup.Orientation switch
        {
            PageOrientation.Portrait => "portrait",
            PageOrientation.Landscape => "landscape",
            _ => "auto",
        });
        Paper = Pick(Papers, Setup.Paper);
        Margin = Pick(MarginChoices, Setup.MarginPreset);
        Ink = Pick(Inks, Setup.Color switch
        {
            PrintColor.Gray => "gray",
            PrintColor.BlackWhite => "bw",
            _ => "color",
        });
        Scale = Pick(Scales, Setup.Scale switch
        {
            PrintScale.FitColumns => "fitCols",
            PrintScale.FitRows => "fitRows",
            PrintScale.FitPage => "fitAll",
            PrintScale.Custom => "custom",
            PrintScale.FitPages => "fitPages",
            _ => "none",
        });

        CopiesText = Shamsi.Money(Setup.Copies);
        SyncRangeBoxes();
        PagesText = Setup.PagesText;
        RebuildPageChecks();

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
                "pages" => PrintWhat.Pages,
                _ => PrintWhat.All,
            },
            Collate = Collate?.Value != "0",
            Orientation = Orientation?.Value switch
            {
                "portrait" => PageOrientation.Portrait,
                "landscape" => PageOrientation.Landscape,
                _ => PageOrientation.Auto,
            },
            Scale = Scale?.Value switch
            {
                "fitCols" => PrintScale.FitColumns,
                "fitRows" => PrintScale.FitRows,
                "fitAll" => PrintScale.FitPage,
                "custom" => PrintScale.Custom,
                "fitPages" => PrintScale.FitPages,
                _ => PrintScale.None,
            },
            Copies = (int)Math.Clamp(Shamsi.Num(CopiesText), 1m, 999m),
            From = (int)Math.Clamp(Shamsi.Num(FromText), 1m, 9999m),
            To = (int)Math.Clamp(Shamsi.Num(ToText), 1m, 9999m),
            PagesText = PagesText ?? "",
            Color = Ink?.Value switch
            {
                "gray" => PrintColor.Gray,
                "bw" => PrintColor.BlackWhite,
                _ => PrintColor.Color,
            },
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
    partial void OnOrientationChanged(SetupOption? v) => Push();
    partial void OnPaperChanged(SetupOption? v) => Push();
    partial void OnMarginChanged(SetupOption? v) => Push();
    partial void OnScaleChanged(SetupOption? v) => Push();
    partial void OnInkChanged(SetupOption? v) => Push();
    partial void OnCopiesTextChanged(string v) => Push();
    /* ⚠️ تایپ کردن در «ورق‌ها: از … تا …» خودش حالت را روی «بازهٔ ورق‌ها»
       می‌گذارد — همان کاری که سایت و اکسل می‌کنند. وگرنه عددها را می‌زدی ولی
       چون حالت روی «همهٔ گزارش» مانده بود، بی‌صدا نادیده گرفته می‌شدند. */
    partial void OnFromTextChanged(string v) { ToRange(); Push(); }
    partial void OnToTextChanged(string v) { ToRange(); Push(); }

    private void ToRange()
    {
        if (_loading || _syncingRange || Setup.What == PrintWhat.Range) return;
        What = Pick(Whats, "range");     // خودش ‎Push‎ می‌کند
    }

    private bool _syncingRange;

    /// <summary>
    /// تا وقتی کاربر «بازهٔ ورق‌ها» را انتخاب نکرده، این دو کادر همیشه کلِ
    /// گزارش را نشان می‌دهند (۱ تا آخر) — مثلِ اکسل. به‌محضِ انتخابِ بازه،
    /// عددهای خودِ کاربر دست‌نخورده می‌مانند (فقط به شمارِ ورق‌ها بریده می‌شوند).
    /// </summary>
    private void SyncRangeBoxes()
    {
        var was = _loading; _loading = true; _syncingRange = true;
        try
        {
            var n = Math.Max(1, _pages.Count);
            if (Setup.What != PrintWhat.Range) { FromText = Shamsi.Money(1); ToText = Shamsi.Money(n); }
            else
            {
                FromText = Shamsi.Money(Math.Clamp(Setup.From, 1, n));
                ToText = Shamsi.Money(Math.Clamp(Setup.To, 1, n));
            }
        }
        finally { _loading = was; _syncingRange = false; }
    }
    partial void OnPagesTextChanged(string v) { RebuildPageChecks(); Push(); }

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
                await Task.Run(() =>
                {
                    _doc = BuildSolved(next);
                    StartPages();
                });
                Status = "";
            }
            catch (Exception ex)
            {
                // تنظیمی که سند را نمی‌سازد (کاغذِ خیلی کوچک، حاشیهٔ خیلی بزرگ)
                // نباید پنجره را ببندد — ورقِ قبلی سرِ جایش می‌ماند.
                Setup = prev;
                Services.CrashGuard.Write("پیش‌نمایشِ چاپ", ex, report: false);
                Status = "این تنظیم روی ورق جا نمی‌شود — کاغذ را بزرگ‌تر یا حاشیه را کمتر کنید.";
                try
                {
                    await Task.Run(() => { _doc = BuildSolved(prev); StartPages(); });
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
            await Task.Run(() => { _doc = BuildSolved(next); StartPages(); });
            Status = "";
            SetupChanged?.Invoke(next);
        }
        catch (Exception ex)
        {
            Setup = prev;
            PullFromSetup();
            Services.CrashGuard.Write("پیش‌نمایشِ چاپ", ex, report: false);
            Status = "این تنظیم روی ورق جا نمی‌شود — کاغذ را بزرگ‌تر یا حاشیه را کمتر کنید.";
            try
            {
                await Task.Run(() => { _doc = BuildSolved(prev); StartPages(); });
            }
            catch { }
        }
        Busy = false;
        RefreshNotes();
    }

    /// <summary>
    /// مقیاسی که واقعاً روی ورق نشست — همان «مقیاسِ اعمال‌شده»ی زیرِ ستون.
    /// برای حالت‌های «جا دادن» با شمردنِ ورق پیدا می‌شود (‎ScaleSolver‎).
    /// </summary>
    public int AppliedPercent { get; private set; } = 100;

    /// <summary>
    /// سند را می‌سازد — و اگر مقیاس از نوعِ «جا دادن» است، اول با شمردنِ ورقِ
    /// واقعی درصدش را پیدا می‌کند و بعد با همان درصد می‌سازد. تنها راهِ
    /// ساختنِ سند در این کلاس همین است تا «آنچه می‌بینی همان چاپ می‌شود»
    /// روی هر مسیری بماند.
    /// </summary>
    private IDocument BuildSolved(PageSetup s)
    {
        AppliedPercent = ScaleSolver.Solve(s, _build);
        return _build(s.NeedsScaleSolve ? s.WithResolvedScale(AppliedPercent) : s);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  زیرنویسِ زندهٔ کادرها — همان ‎refreshCards()‎ی سایت
    // ══════════════════════════════════════════════════════════════════════
    //
    // زیرنویسِ کادرِ «کدام ورق‌ها» صریح می‌گوید دقیقاً چه چاپ می‌شود («هر ۳
    // ورق»، «فقط ورقِ ۲»، «ورقِ ۲ تا ۵») — نه یک جملهٔ کلی.

    public string WhatNote => Setup.What switch
    {
        PrintWhat.Current => "فقط ورقِ " + Shamsi.Money(PageIndex + 1) + " چاپ می‌شود",
        PrintWhat.Range => "ورقِ " + Shamsi.Money(Setup.From) + " تا "
                           + Shamsi.Money(Setup.To) + " چاپ می‌شود",
        PrintWhat.Pages => PickedPages().Count == 0
            ? "هیچ ورقی انتخاب نشده — شماره بنویسید یا تیک بزنید"
            : "فقط ورقِ " + PrintJob.FormatPages(PickedPages()) + " چاپ می‌شود ("
              + Shamsi.Money(PickedPages().Count) + " ورق)",
        _ => "هر " + Shamsi.Money(PageCount) + " ورق چاپ می‌شود",
    };

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
        PrintScale.FitColumns => "پهنای جدول تا عرضِ ورق کوچک می‌شود",
        PrintScale.FitRows => "بلندیِ گزارش تا یک ورق کوچک می‌شود",
        PrintScale.FitPage => "همه‌چیز در یک ورقِ واحد",
        PrintScale.FitPages => Shamsi.Money(Setup.FitWidthPages) + " ورق پهنا × "
                               + Shamsi.Money(Setup.FitHeightPages) + " ورق بلندا",
        _ => "در اندازهٔ واقعیِ خودش",
    };

    /// <summary>
    /// خطِ راهنمای زیرِ «تنظیمِ ورق…» — همان ‎xpr-scaleinfo‎ی سایت:
    /// «مقیاسِ اعمال‌شده: ۷۳٪ · تعدادِ ورق: ۲»، به اضافهٔ کاغذ و جهت.
    /// </summary>
    public string ScaleInfo =>
        "مقیاسِ اعمال‌شده: " + Shamsi.Money(AppliedPercent) + "٪ · تعدادِ ورق: " + Shamsi.Money(PageCount)
        + " · کاغذ " + (Setup.Paper == "Custom"
                    ? Shamsi.Money(Setup.CustomWidth) + "×" + Shamsi.Money(Setup.CustomHeight)
                    : Setup.Paper)
        + " " + (Setup.Orientation == PageOrientation.Landscape ? "خوابیده"
                   : Setup.Orientation == PageOrientation.Portrait ? "ایستاده" : "خودکار");

    private void RefreshNotes()
    {
        foreach (var n in new[]
        {
            nameof(WhatNote), nameof(PaperNoteText), nameof(MarginNoteText),
            nameof(ScaleNote), nameof(ScaleInfo), nameof(IsRange), nameof(IsPages), nameof(PageCount),
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
        QueueSharpen();
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

    partial void OnShowGuidesChanged(bool v) => OnPropertyChanged(nameof(GuideMargin));

    public Thickness GuideMargin
    {
        get
        {
            var bmp = CurrentPage;
            if (bmp is null || bmp.PixelSize.Width <= 0) return new Thickness(0);

            // ⚠️ dpiِ خودِ **تصویر**، نه dpiِ چاپ: پیش‌نمایش سبک‌تر تصویر می‌شود
            // (‎PreviewDpi‎) و با عددِ چاپ، خط‌چینِ حاشیه جابه‌جا می‌افتاد.
            var mmWide = bmp.PixelSize.Width / (double)Math.Clamp(_pagesDpi, 72, 400) * 25.4;
            if (mmWide <= 0) return new Thickness(0);
            var px = PageWidth / mmWide;                 // پیکسلِ صفحه در هر میلی‌متر

            var m = Setup.Margins();
            return new Thickness((double)m.Left * px, (double)m.Top * px,
                                 (double)m.Right * px, (double)m.Bottom * px);
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

    // ══════════════════════════════════════════════════════════════════════
    //  ══ ورقِ اول همان لحظه، بقیه در پس‌زمینه ═══════════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «برنامه خیلی سریع شده، ولی بخشِ پی‌دی‌اف
    //  هنوز دیر باز می‌شود.»
    //
    //  سنجشِ ‎printperf‎ حق را به او داد و ریشه را هم نشان داد: پیش از باز شدنِ
    //  پنجره، **همهٔ** ورق‌ها تصویر می‌شدند — هر ورق ~۱۳۰ میلی‌ثانیه:
    //
    //      ۴۰ ردیف  =  ۲ ورق  →   ۴۷۸ ms
    //     ۲۰۰ ردیف  =  ۸ ورق  → ۱٬۰۷۴ ms
    //     ۶۰۰ ردیف  = ۲۳ ورق  → ۲٬۸۹۸ ms
    //
    //  یعنی همان قاعدهٔ همیشگی: هزینه با اندازهٔ گزارش بالا می‌رفت. حالا فقط
    //  **ورقِ اول** پیش از باز شدن ساخته می‌شود و بقیه در پس‌زمینه یکی‌یکی
    //  می‌آیند — پس گزارشِ صد ورقی هم به‌اندازهٔ گزارشِ یک‌ورقی زود باز می‌شود.
    //
    //  ⚠️ **پیش‌نمایش با ‎PreviewDpi‎ تصویر می‌شود، نه با ‎Setup.Dpi‎.** آن‌چه
    //  روی صفحه دیده می‌شود یک تصویرِ چندصد پیکسلی است و ۱۱۰dpi برایش بس؛ ولی
    //  چاپ باید همان کیفیتی باشد که کاربر خواسته، پس مسیرِ «فقط این ورق‌ها»
    //  ورق‌های برگزیده را دوباره با ‎Setup.Dpi‎ می‌سازد (‎PagesDocument‎).
    //  هزینهٔ تصویر با **مربعِ** dpi بالا می‌رود، پس این خودش نصفِ کار است.
    //
    //  ⚠️ ‎_renderGen‎ نگهبانِ «تنظیم وسطِ کار عوض شد» است: هر بار که ساختِ
    //  تازه‌ای شروع شود شماره بالا می‌رود و جریانِ کهنه ورق‌هایش را دور
    //  می‌ریزد. بی این، ورق‌های تنظیمِ قبلی روی تنظیمِ تازه می‌نشستند.

    /// <summary>
    /// dpiِ تصویرِ پیش‌نمایش — فقط برای چشم، نه برای چاپ.
    ///
    /// ⚠️ ۹۶ عددِ خودِ صفحهٔ نمایش است: A4 در این dpi می‌شود ۷۹۴×۱۱۲۳ پیکسل،
    /// یعنی از قابِ پیش‌نمایش هم بزرگ‌تر. هزینهٔ تصویر با **مربعِ** dpi بالا
    /// می‌رود، پس ۱۴۴ ⇒ ۹۶ یعنی کمتر از نصفِ کار، بی این‌که چشم چیزی ببیند.
    /// چاپ و ذخیره همچنان با ‎Setup.Dpi‎ی خودِ کاربر است.
    /// </summary>
    public const int PreviewDpi = 96;

    private int _renderGen;

    /// <summary>ورق‌های همین لحظه با چه dpi ساخته شده‌اند (برای خط‌چینِ حاشیه).</summary>
    private int _pagesDpi = PreviewDpi;

    /// <summary>همهٔ ورق‌ها ساخته شده‌اند؟ — سنجش‌ها منتظرِ همین می‌مانند.</summary>
    [ObservableProperty] private bool _allRendered;

    private IEnumerable<byte[]> RenderStream(int dpi) =>
        _doc.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = Math.Clamp(dpi, 72, 400),
        });

    /// <summary>
    /// ورقِ اول را همین‌جا می‌سازد (پس خطای «این تنظیم جا نمی‌شود» همان‌جا که
    /// باید پیدا می‌شود) و بقیه را به پس‌زمینه می‌سپارد.
    /// </summary>
    private void StartPages(bool marshal = true, bool background = false)
    {
        var gen = ++_renderGen;
        _pagesDpi = PreviewDpi;
        _sharpDpi = 0;

        // ══ حالتِ باز شدنِ پنجره: هیچ انتظاری ═════════════════════════════
        if (background)
        {
            ResetPages(null, marshal);
            Busy = true;
            Status = "در حال ساختنِ ورق‌ها…";
            Task.Run(() =>
            {
                IEnumerator<byte[]>? it = null;
                try { it = RenderStream(PreviewDpi).GetEnumerator(); }
                catch
                {
                    Dispatcher.UIThread.Post(() => { if (gen == _renderGen) { Busy = false; Status = "این گزارش ساخته نشد."; } });
                    return;
                }
                Stream(gen, it);
            });
            return;
        }

        var sync = RenderStream(PreviewDpi).GetEnumerator();
        byte[]? first;
        try { first = sync.MoveNext() ? sync.Current : null; }
        catch { sync.Dispose(); throw; }

        ResetPages(first, marshal);
        if (first is null) { sync.Dispose(); Finish(gen, marshal); return; }
        Task.Run(() => Stream(gen, sync));
    }

    private void Stream(int gen, IEnumerator<byte[]> rest)
    {
        try
        {
            while (gen == _renderGen && rest.MoveNext())
            {
                var bytes = rest.Current;
                Dispatcher.UIThread.Post(() => { if (gen == _renderGen) AppendPage(bytes); });
            }
        }
        catch { /* ورقی که ساخته نشد، پیش‌نمایش را نمی‌شکند */ }
        finally
        {
            rest.Dispose();
            Dispatcher.UIThread.Post(() =>
            {
                if (gen != _renderGen) return;
                Busy = false;
                if (Status == "در حال ساختنِ ورق‌ها…") Status = "";
                Finish(gen, marshal: false);
            });
        }
    }

    /// <summary>ورق‌های کهنه می‌روند و ورقِ اولِ تازه می‌نشیند.</summary>
    private void ResetPages(byte[]? first, bool marshal)
    {
        void Set()
        {
            _pages.Clear();
            _shownIdx = -1;
            AllRendered = false;
            if (first is not null) AddPage(first);
            PageIndex = 0;
            Show();
            OnPropertyChanged(nameof(PageCount));
        }
        if (!marshal || Dispatcher.UIThread.CheckAccess()) Set();
        else Dispatcher.UIThread.Invoke(Set);
    }

    private void AppendPage(byte[] bytes)
    {
        AddPage(bytes);
        OnPropertyChanged(nameof(PageCount));
        Show();
    }

    private void AddPage(byte[] bytes) => _pages.Add(bytes);

    /// <summary>ساختِ ورق‌ها تمام شد — حالا کادرهایی که به شمارِ ورق بسته‌اند.</summary>
    private void Finish(int gen, bool marshal)
    {
        if (gen != _renderGen) return;
        void Set()
        {
            AllRendered = true;
            OnPropertyChanged(nameof(PageCount));
            RebuildPageChecks();
            SyncRangeBoxes();
            RefreshNotes();
            Show();
            QueueSharpen();
        }
        if (!marshal || Dispatcher.UIThread.CheckAccess()) Set();
        else Dispatcher.UIThread.Invoke(Set);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ ورقِ تیز — «کیفیتش باید عالی باشد، حتی با زوم» ═══════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «بخشِ پرینت ورق‌ها را خیلی بی‌کیفیت نشان
    //  می‌دهد و وقتی کمی زوم کنم حتی بدتر و تارتر دیده می‌شود.»
    //
    //  ریشه یک عدد بود: ورق با ۹۶ نقطه تصویر می‌شد (۷۹۴ پیکسلِ پهنا برای A4) و
    //  روی قابِ ۹۰۰ پیکسلی **بزرگ** نشان داده می‌شد — و روی ویندوزِ ۱۲۵٪ یا
    //  ۱۵۰٪ باز هم بزرگ‌تر. هر زوم همان تصویرِ کوچک را بیشتر کش می‌داد.
    //
    //  ⛔ **پاسِ اول همان ۹۶ می‌ماند**: پنجره باید همان لحظه باز شود و ورقِ اول
    //  زود بیاید (‎printperf‎). ولی همین که همه آمدند، پاسِ دوم با dpiی که
    //  **پیکسلِ واقعیِ نمایشگر** می‌خواهد (پهنای ورق × ضریبِ صفحه) در
    //  پس‌زمینه ساخته می‌شود و یک‌جا جای قبلی می‌نشیند. زوم که بالاتر از آن
    //  رفت، دوباره — با مکثِ کوتاه تا هر پلهٔ زوم یک رندر نشود.
    //
    //  ⚠️ **SVG آزموده شد و رد شد**: ‎GenerateSvg‎ی QuestPDF متنِ فارسی را با
    //  ‎<text>‎ و شمارهٔ گلیف می‌نویسد و روی صفحه خالی درمی‌آید.
    //  ⚠️ **JPEG با بهترین کیفیت، نه PNG**: سنجیده شد — در ۱۹۲ نقطه PNG هر ورق
    //  ۲۲۳ms و JPEG ۱۰۲ms؛ فشرده‌سازیِ PNG بیشترِ کار بود، نه رندر.
    //  ⚠️ چاپ و ذخیره از این مسیر نمی‌گذرند و همان ‎Setup.Dpi‎ را دارند.

    /// <summary>کمترین و بیشترین dpiِ ورقِ تیز. ۳۰۰ همان کیفیتِ چاپ است.</summary>
    public const int SharpMinDpi = 144, SharpMaxDpi = 300;

    /// <summary>dpiِ پاسِ تیزی که ساخته شده یا در راه است (۰ = هیچ).</summary>
    private int _sharpDpi;
    private DispatcherTimer? _sharpTimer;

    /// <summary>برای این زوم و این نمایشگر، ورق چند نقطه لازم دارد؟</summary>
    public int WantDpi()
    {
        if (_pages.Count == 0 || CurrentPage is not { } bmp || bmp.PixelSize.Width <= 0) return 0;
        var inches = bmp.PixelSize.Width / (double)Math.Clamp(_pagesDpi, 72, 400);
        if (inches <= 0) return 0;
        var need = PageWidth * RenderScaling / inches * 1.15;     // کمی جا برای زومِ بعدی
        var dpi = (int)Math.Ceiling(need / 24.0) * 24;
        return Math.Clamp(dpi, SharpMinDpi, SharpMaxDpi);
    }

    private void QueueSharpen()
    {
        if (!AllRendered || _pages.Count == 0) return;
        if (!Dispatcher.UIThread.CheckAccess()) return;
        var want = WantDpi();
        if (want <= Math.Max(_pagesDpi, _sharpDpi)) return;

        _sharpTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(220), DispatcherPriority.Background,
                                            (_, _) => { _sharpTimer!.Stop(); Sharpen(); });
        _sharpTimer.Stop();
        _sharpTimer.Start();
    }

    private void Sharpen()
    {
        var want = WantDpi();
        if (want <= Math.Max(_pagesDpi, _sharpDpi)) return;
        var gen = _renderGen;
        var doc = _doc;
        var count = _pages.Count;
        _sharpDpi = want;

        Task.Run(() =>
        {
            List<byte[]>? list = null;
            try
            {
                list = doc.GenerateImages(new ImageGenerationSettings
                {
                    ImageFormat = ImageFormat.Jpeg,
                    ImageCompressionQuality = ImageCompressionQuality.Best,
                    RasterDpi = want,
                }).ToList();
            }
            catch { /* ورقِ تیز نشد، همان ورقِ قبلی می‌ماند */ }

            Dispatcher.UIThread.Post(() =>
            {
                if (gen != _renderGen) return;
                if (list is null || list.Count != count || want <= _pagesDpi)
                {
                    if (_sharpDpi == want) _sharpDpi = _pagesDpi;
                    return;
                }
                _pages.Clear();
                _pages.AddRange(list);
                _pagesDpi = want;
                _shownIdx = -1;
                Show();
                QueueSharpen();      // اگر وسطِ کار باز هم زوم شده بود
            });
        });
    }

    /// <summary>سند را با تنظیمِ تازه از نو می‌سازد (برای آزمون‌ها و مسیرهای قدیمی).</summary>
    public void Rebuild(PageSetup setup)
    {
        Setup = setup;
        _doc = BuildSolved(setup);
        StartPages();
        PullFromSetup();
    }

    private void Show()
    {
        if (_pages.Count == 0)
        {
            CurrentPage = null;
            _shown?.Dispose(); _shown = null; _shownIdx = -1;
        }
        else
        {
            var i = Math.Clamp(PageIndex, 0, _pages.Count - 1);
            if (_shown is null || _shownIdx != i)
            {
                var old = _shown;
                try
                {
                    using var ms = new MemoryStream(_pages[i]);
                    _shown = new Bitmap(ms);
                    _shownIdx = i;
                }
                catch { _shown = null; _shownIdx = -1; }
                CurrentPage = _shown;
                old?.Dispose();
            }
        }
        PageLabel = _pages.Count == 0 ? "—" : $"{PageIndex + 1} از {_pages.Count}";
        OnPropertyChanged(nameof(PageNumberText));
        OnPropertyChanged(nameof(GuideMargin));
        OnPropertyChanged(nameof(WhatNote));
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

    /// <summary>همان‌ها، با تعدادِ نسخه و ترتیبِ مرتب/نامرتب.</summary>
    public IReadOnlyList<int> PrintOrder() =>
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

        var order = PrintOrder();
        if (wholeDocument || order.Count == 0 || PrintJob.IsWholeDocument(order, _pages.Count))
            _doc.GeneratePdf(path);
        else PagesDocument(order).GeneratePdf(path);
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
    /// ⚠️ ورق‌ها این‌جا **دوباره و با ‎Setup.Dpi‎** ساخته می‌شوند، نه از تصویرِ
    /// پیش‌نمایش: پیش‌نمایش عمداً سبک است (‎PreviewDpi‎) تا زود باز شود، ولی
    /// چاپ باید همان کیفیتی باشد که کاربر انتخاب کرده. این مسیر فقط با کلیکِ
    /// صریحِ «چاپ/ذخیره» می‌دود، نه هنگامِ باز شدن.
    /// </summary>
    private IDocument PagesDocument(IReadOnlyList<int> order)
    {
        var dpi = Math.Clamp(Setup.Dpi, 72, 400);
        var full = RenderStream(dpi).ToList();
        var picked = order.Where(i => i >= 1 && i <= full.Count).Select(i => full[i - 1]).ToList();
        return new PickedPagesDocument(picked, dpi);
    }

    /// <summary>
    /// ورق‌های برگزیده، هر کدام یک صفحه. اندازهٔ هر صفحه از پیکسلِ خودِ تصویر
    /// و ‎dpi‎ درمی‌آید، پس با هر کاغذ و هر جهتی مو‌به‌مو همان اندازهٔ اصلی است.
    /// </summary>
    private sealed class PickedPagesDocument : IDocument
    {
        private readonly IReadOnlyList<byte[]> _images;
        private readonly int _dpi;

        public PickedPagesDocument(IReadOnlyList<byte[]> images, int dpi)
        { _images = images; _dpi = dpi; }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            foreach (var png in _images)
            {
                var (w, h) = SizePt(png);
                container.Page(p =>
                {
                    p.Size(w, h, Unit.Point);
                    p.Margin(0);
                    p.PageColor(Colors.White);
                    p.Content().Image(png).FitArea();
                });
            }
        }

        /// <summary>اندازهٔ صفحه به «پوینت» از روی پیکسلِ تصویر: ‎px ÷ dpi × 72‎.</summary>
        private (float W, float H) SizePt(byte[] png)
        {
            try
            {
                using var ms = new MemoryStream(png);
                var bmp = new Bitmap(ms);
                return (bmp.PixelSize.Width / (float)_dpi * 72f,
                        bmp.PixelSize.Height / (float)_dpi * 72f);
            }
            catch { return (595f, 842f); }        // A4، اگر تصویر خوانده نشد
        }
    }
}

/// <summary>تیکِ یک ورق در ستونِ تنظیماتِ چاپ — همان «☑ ورقِ ۲».</summary>
public sealed partial class PageCheck : ObservableObject
{
    private readonly Action _changed;
    public PageCheck(int number, bool on, Action changed) { Number = number; _isOn = on; _changed = changed; }
    public int Number { get; }
    public string Label => "ورقِ " + Shamsi.Money(Number);
    [ObservableProperty] private bool _isOn;
    partial void OnIsOnChanged(bool v) => _changed();
}
