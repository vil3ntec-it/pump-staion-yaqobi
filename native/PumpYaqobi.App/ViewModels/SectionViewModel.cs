using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    /// هیچ سقفی. هر بخش تمامِ عرضِ پنجره را می‌گیرد.
    ///
    /// ⚠️ این عمداً برگشتِ تصمیمِ قبلی است. تا دیروز اینجا ستونِ ۱۰۰۰ پیکسلیِ
    /// وسط‌چینِ ‎.main‎ِ نسخهٔ وب تقلید می‌شد و فقط چند بخشِ جدولی تمام‌عرض
    /// بودند. صاحب ریپو با عکس نشان داد که نتیجه‌اش روی لپ‌تاپ چه شد:
    /// «چپ و راستِ هر بخش را ببینی تمام صفحه نیستن» — دو نوارِ خالی کنارِ هر
    /// بخش. حالا همه تمام‌عرض‌اند و کارِ جا دادنِ محتوا با خودِ بخش است.
    ///
    /// خودِ خاصیت مانده (نه حذف) چون پوستهٔ برنامه به آن بند است و آزمونش
    /// همین قاعده را قفل می‌کند.
    /// </summary>
    public double ContentMaxWidth => double.PositiveInfinity;

    /// <summary>
    /// صفحهٔ درونیِ بخش (حسابِ شخص، شرکت، ورق، امانت) باز است.
    /// دیگر روی پهنا اثری ندارد — همه‌چیز تمام‌عرض است — ولی بخش‌ها با آن
    /// می‌فهمند فهرستشان پشتِ یک صفحهٔ باز رفته.
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
