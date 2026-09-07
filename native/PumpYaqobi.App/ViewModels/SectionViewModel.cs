using CommunityToolkit.Mvvm.ComponentModel;

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
    }

    public string Id { get; }

    /// <summary>
    /// ══ پهنای ستونِ محتوا ═══════════════════════════════════════════════════
    /// در نسخهٔ وب ‎.main‎ عرضش ۱۰۰۰ پیکسل است و وسط می‌ایستد؛ فقط چند بخشِ
    /// جدولی با کلاسِ ‎sec-wide‎ تمامِ عرض را می‌گیرند. همان فهرست، مو‌به‌مو:
    ///
    ///   ‎const _WIDE = ['dashboard','rasid','debtrasid','chakana','oldloans',
    ///                   'oldloansmoney','debtsum','debtsummoney','priceloss',
    ///                   'plsource','plperson','invrate'];‎
    ///
    /// ⚠️ عمداً «همه‌چیز تمام‌عرض» نیست: صاحب ریپو گفت همین ستونِ وسط‌چینِ
    /// نسخهٔ وب «خیلی بهتر است». فرمِ دوستونهٔ پارچه یا مخزن، کشیده روی یک
    /// نمایشگرِ ۲۷ اینچی، خوانا نیست — چشم باید سر تا سرِ میز را بگردد.
    /// </summary>
    private static readonly HashSet<string> WideSections = new()
    {
        "dashboard", "rasid", "debtrasid", "chakana", "oldloans", "oldloansmoney",
        "debtsum", "debtsummoney", "priceloss", "plsource", "plperson", "invrate",
    };

    public bool IsWide => WideSections.Contains(Id);

    /// <summary>
    /// سقفِ پهنا. داشبورد تمام‌عرض است ولی خودش تا ۱۴۴۰ بند می‌شود تا روی
    /// نمایشگرِ خیلی پهن کارت‌هایش بی‌جهت کش نیایند — همان قاعدهٔ نسخهٔ وب.
    /// </summary>
    /// <summary>
    /// صفحهٔ درونیِ بخش (حسابِ شخص، شرکت، ورق، امانت) باز است.
    ///
    /// در نسخهٔ وب این‌ها مودالِ تمام‌صفحه‌اند — ‎#personModal .modal‎ صریحاً
    /// ‎width:100%;height:100%;max-width:100%‎ می‌گیرد. جدولِ حسابِ شخص ده‌ها
    /// ستون دارد و در ستونِ ۱۰۰۰ پیکسلی نصفش بیرون می‌ماند.
    /// </summary>
    [ObservableProperty] private bool _isPageOpen;

    partial void OnIsPageOpenChanged(bool v) => OnPropertyChanged(nameof(ContentMaxWidth));

    public double ContentMaxWidth =>
        IsPageOpen ? double.PositiveInfinity
        : Id == "dashboard" ? 1440
        : IsWide ? double.PositiveInfinity
        : 1000;
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
        try { await LoadAsync(); IsLoaded = true; }
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
