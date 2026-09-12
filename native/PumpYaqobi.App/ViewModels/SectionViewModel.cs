using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// پایهٔ هر بخشِ برنامه. یک نمونه برای هر بخش ساخته می‌شود و تا پایانِ اجرا
/// زنده می‌ماند — پس رفت‌وبرگشت بین بخش‌ها هیچ چیزی را دوباره بار نمی‌کند
/// (بندِ ۲۹ خواسته: «تغییر Section نباید کل صفحه را reload کند»).
/// </summary>
public abstract partial class SectionViewModel : ObservableObject
{
    protected SectionViewModel(string id, string iconKey, string title)
    {
        Id = id; IconKey = iconKey; Title = title;
        FontScale = Scales.TryGetValue(id, out var s) ? Clamp(s) : 1;
    }

    public string Id { get; }

    /// <summary>
    /// ══ «📝 یادداشت این بخش» ═══════════════════════════════════════════════
    /// همتای ‎.sec-note-box‎ی نسخهٔ وب. بخشی که این را پر کند، خودبه‌خود کادرِ
    /// یادداشت را ته صفحه‌اش می‌گیرد — چون قالبِ مشترکِ ‎SectionPage‎ خودش
    /// آن را می‌کشد. بخشی که ندارد، هیچ چیزِ اضافه‌ای نمی‌بیند.
    ///
    /// ⚠️ کلیدش همان کلیدِ سایت است (‎safe‎، ‎expenses‎، ‎sarrafi‎، …) تا اگر
    /// روزی دادهٔ سایت وارد شد، نوت‌ها سرِ جای خودشان بنشینند.
    /// </summary>
    [ObservableProperty] private SectionNotesViewModel? _notes;

    /// <summary>
    /// ══ پهنای ستونِ محتوا ═══════════════════════════════════════════════════
    ///
    /// ⚠️ این قاعده را **صاحب ریپو** تعیین کرده، نه سایت. حرفِ آخرش:
    ///     «فقط و فقط بخشِ پارچه‌ها و بخشِ فاکتورها تمام صفحه نباشند؛
    ///      بقیهٔ همهٔ بخش‌ها تمام صفحه باشند.»
    ///
    /// سایت فهرستِ دیگری دارد (‎_WIDE‎ی دوازده‌تایی، با ستونِ ۱۰۰۰ پیکسلیِ
    /// ‎.main‎ برای بقیه) — عمداً دنبال نشد. دو تا فرم‌اند و در عرضِ زیاد بد
    /// دیده می‌شوند؛ بقیه جدول‌اند و هر چه پهن‌تر، بهتر.
    /// </summary>
    public double ContentMaxWidth =>
        IsPageOpen || !Narrow.Contains(Id) ? double.PositiveInfinity : 1000;

    /// <summary>تنها دو بخشی که ستونِ باریک می‌گیرند — پارچه‌ها و فاکتورها.</summary>
    private static readonly HashSet<string> Narrow = new(StringComparer.Ordinal)
    {
        "shifts", "invoices",
    };

    /// <summary>
    /// صفحهٔ درونیِ بخش (حسابِ شخص، شرکت، ورق، امانت) باز است.
    /// تا باز است، بخش تمام‌عرض می‌شود — چون در سایت هم آن صفحه‌ها
    /// ‎modal-overlay‎ی تمام‌پنجره‌اند، نه محتوای ستونِ ‎.main‎.
    /// </summary>
    [ObservableProperty] private bool _isPageOpen;

    partial void OnIsPageOpenChanged(bool v) => OnPropertyChanged(nameof(ContentMaxWidth));

    // ══ زیربخش‌ها ═══════════════════════════════════════════════════════════
    //
    // در سایت نوارِ بالا هجده دکمه دارد و بس. چیزهایی مثلِ «قرض‌های کهنه»،
    // «تخلیهٔ تانکر»، «گزارش ماهانه»، «تاریخچهٔ نرخ»، «چکنه» و «مدیریت
    // داده‌ها» دکمهٔ سرصفحه ندارند: هر کدام یک کارتِ ‎.tool-link-card‎ داخلِ
    // بخشِ خودشان‌اند و با زدنش صفحهٔ خودشان باز می‌شود.
    //
    // در نیتیو این هفت‌تا دکمهٔ نوار گرفته بودند و نوار بیست‌وپنج‌تایی شده
    // بود — همان چیزی که صاحب ریپو گفت «خیلی دارد اذیتم می‌کند». حالا هر
    // کدام «زیربخشِ» بخشِ خودش است: در نوار نیست، کارتش بالای همان بخش
    // می‌نشیند، و باز که شد نوارِ «‹ برگشت» بالایش می‌آید.
    //
    // ⚠️ نمونهٔ زیربخش هم مثلِ خودِ بخش‌ها زنده می‌ماند، پس رفت‌وبرگشت هیچ
    // چیزی را دوباره بار نمی‌کند.
    public ObservableCollection<SectionViewModel> SubSections { get; } = new();

    public bool HasSubSections => SubSections.Count > 0;

    /// <summary>
    /// ردیفِ خودکارِ کارت‌های زیربخش بالای بخش نشان داده شود؟
    ///
    /// پیش‌فرض بله. بخشی که خودش کارتِ لینکش را جایی گذاشته — مثلِ فاکتورها
    /// که «مقایسهٔ نرخ» را کارتِ سومِ ردیفِ آماری‌اش کرده، عینِ سایت — این را
    /// خاموش می‌کند تا یک لینک دو بار پیدا نشود.
    /// </summary>
    protected virtual bool ShowSubLinks => true;

    public bool ShowSubLinkCards => HasSubSections && ShowSubLinks;

    /// <summary>بخشی که این یکی زیرِ آن نشسته — برای نوشتهٔ دکمهٔ برگشت.</summary>
    public SectionViewModel? ParentSection { get; private set; }

    /// <summary>زیربخشِ بازِ همین بخش. ‎null‎ یعنی خودِ بخش جلوی چشم است.</summary>
    [ObservableProperty] private SectionViewModel? _openSub;

    /// <summary>
    /// نوشتهٔ روی کارتِ لینک. جدا از ‎Title‎ است چون سایت روی کارت چیزِ
    /// گویاتری می‌نویسد («⏰ قرض‌های کهنه — تیل») و در سربرگِ خودِ صفحه
    /// عنوانِ کوتاه را.
    /// </summary>
    public string LinkTitle { get; private set; } = "";

    public void AddSub(SectionViewModel sub, string? linkTitle = null)
    {
        sub.ParentSection = this;
        sub.LinkTitle = linkTitle ?? sub.Title;
        SubSections.Add(sub);
        OnPropertyChanged(nameof(HasSubSections));
        OnPropertyChanged(nameof(ShowSubLinkCards));
    }

    /// <summary>
    /// باز کردنِ یک زیربخش — همان ‎showSection('oldloans')‎ی سایت.
    /// نامش ‎ShowSub‎ است نه ‎OpenSub‎، چون ‎OpenSub‎ خودِ خاصیتِ بالاست.
    /// </summary>
    [RelayCommand]
    public void ShowSub(SectionViewModel? sub)
    {
        if (sub is null || !SubSections.Contains(sub)) return;
        OpenSub = sub;
    }

    /// <summary>«‹ برگشت» — از زیربخش به خودِ بخش.</summary>
    [RelayCommand]
    public void CloseSub() => OpenSub = null;

    /// <summary>
    /// ══ «➕ ردیف» و «➕➕ چندتایی» ═══════════════════════════════════════════
    /// بخشی که ردیفِ دستی دارد این را می‌دهد و قالبِ مشترک، نوارِ افزودن را
    /// پایینِ جدولش می‌کشد — همان‌جایی که سایت گذاشته. ‎null‎ یعنی این بخش
    /// ردیفِ دستی ندارد و هیچ نواری نمی‌بیند.
    /// </summary>
    public virtual ICommand? RowAddCommand => null;

    // ══ اندازهٔ نوشتهٔ بخش — ‎A−‎ / ‎A+‎ / ‎↺‎ ═══════════════════════════════════
    //
    // در سایت کنارِ عنوانِ **هر** بخش این سه دکمه هست (‎adjSecFont‎ /
    // ‎resetSecFont‎) و خودِ سایت هم نوشته چرا: «بخش‌هایی که این دکمه‌ها را
    // نداشتند خودکار می‌گیرند، پس دیگر هیچ بخشی بدونِ کنترلِ اندازه نمی‌ماند.»
    // در برنامهٔ نیتیو هیچ بخشی نداشت.
    //
    // این‌جا روی خودِ ‎SectionViewModel‎ نشسته، نه در تک‌تکِ بخش‌ها: هر بخشی که
    // ساخته شود خودبه‌خود دارد و قالبِ مشترکِ ‎SectionPage‎ دکمه‌هایش را می‌کشد.
    //
    // ⚠️ گام و کف و سقف مو‌به‌مو همان سایت است: ‎0.08‎ هر بار، بینِ ‎0.6‎ و ‎2.2‎.

    public const double FontStep = 0.08;
    public const double FontMin = 0.6;
    public const double FontMax = 2.2;

    /// <summary>
    /// اندازهٔ ذخیره‌شدهٔ همهٔ بخش‌ها — یک‌بار از فایلِ تنظیمات خوانده می‌شود،
    /// نه یک‌بار برای هر کدام از چهل‌ودو بخش.
    /// </summary>
    private static Dictionary<string, double>? _scales;

    private static Dictionary<string, double> Scales =>
        _scales ??= AppSettings.Load().SecFontScales;

    /// <summary>آزمون‌ها که پوشهٔ تنظیمات را عوض می‌کنند، حافظهٔ بالا را دور بریزند.</summary>
    public static void ForgetFontScales() => _scales = null;

    private static double Clamp(double v) => Math.Round(Math.Clamp(v, FontMin, FontMax), 2);

    [ObservableProperty] private double _fontScale = 1;

    [RelayCommand] private void FontBigger() => SetFontScale(FontScale + FontStep);

    [RelayCommand] private void FontSmaller() => SetFontScale(FontScale - FontStep);

    /// <summary>‎↺‎ — برگشت به اندازهٔ عادی، بی چند بار زدنِ ‎A−‎.</summary>
    [RelayCommand] private void FontReset() => SetFontScale(1);

    private void SetFontScale(double v)
    {
        v = Clamp(v);
        if (Math.Abs(v - FontScale) < 0.0005) return;      // به کف/سقف رسیده
        FontScale = v;
        if (Math.Abs(v - 1) < 0.005) Scales.Remove(Id); else Scales[Id] = v;
        AppSettings.SaveSecFontScale(Id, v);
    }

    /// <summary>کلیدِ آیکون در <c>Icons.axaml</c> — معمولاً همان شناسهٔ بخش.</summary>
    public string IconKey { get; }
    public string Title { get; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;

    /// <summary>بارِ اولِ داده — فقط یک‌بار، همان لحظه‌ای که کاربر واقعاً وارد بخش شد.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded) return;
        IsBusy = true;
        try
        {
            await LoadAsync();
            // پیش‌نویس و شمارِ نوت‌های همین بخش هم همین‌جا خوانده می‌شوند —
            // وگرنه کادرِ یادداشت هر بار خالی و بی‌شمار باز می‌شد.
            if (Notes is not null) await Notes.LoadAsync();
            IsLoaded = true;
        }
        finally { IsBusy = false; }
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>
    /// هر بار که کاربر واردِ بخش می‌شود — نه فقط بارِ اول. بخش‌هایی که فقط
    /// «خلاصه»ی دادهٔ بخش‌های دیگرند (مثلِ داشبورد) باید این‌جا خودشان را تازه
    /// کنند، وگرنه عددهایشان روی عکسِ لحظهٔ ورودِ اولِ برنامه می‌ماند.
    /// </summary>
    public virtual Task OnActivatedAsync() => Task.CompletedTask;

    /// <summary>
    /// وقتی کاربر از بخش بیرون می‌رود.
    ///
    /// بیشترِ بخش‌ها لازمش ندارند، ولی بخشی که چیزی را «باز» نگه می‌دارد —
    /// مثلِ اتصالِ زندهٔ دوربین‌ها — باید همان‌جا ببنددش، وگرنه تا بسته شدنِ
    /// برنامه باز می‌ماند و شبکه و باتری را می‌خورد.
    /// </summary>
    public virtual void OnDeactivated() { }

    /// <summary>
    /// صفحهٔ بازِ درونِ بخش — حسابِ شخص، صفحهٔ شرکت، ورق، حسابِ امانت…
    /// اگر کاربر در فهرستِ کارت‌ها باشد ‎null‎ است.
    ///
    /// میانبرهای صفحه‌کلید «جدولِ جلوی چشمِ کاربر» را از همین می‌شناسند: تا
    /// وقتی حسابی باز است، ‎Ctrl+عدد‎ به همان حساب ردیف می‌افزاید، نه به جدولِ
    /// پشتِ آن. همان اولویتی که ‎_kbCtx()‎ در نسخهٔ وب داشت.
    /// </summary>
    public virtual object? ActivePage => null;

    /// <summary>بارگیریِ دوباره (پس از واردکردن بکاپ یا هم‌گام‌سازی).</summary>
    public virtual async Task ReloadAsync()
    {
        IsLoaded = false;
        await EnsureLoadedAsync();
    }
}
