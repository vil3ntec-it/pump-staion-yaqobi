using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// یک عددِ نوارِ خبرِ بالای صفحه (‎.tb-item‎) — برچسب، عدد و رنگِ همان عدد.
/// </summary>
public sealed partial class BannerItemViewModel : ObservableObject
{
    public BannerItemViewModel(string label, string colorKey)
    { Label = label; ColorKey = colorKey; }

    public string Label { get; }
    public string ColorKey { get; }

    [ObservableProperty] private string _value = "0";
}

/// <summary>
/// پوستهٔ برنامه: سربرگ، نوارِ خبر، نوارِ افقیِ بخش‌ها و ناحیهٔ محتوا.
/// همان چیدمانِ نسخهٔ وب — نوار بالا می‌ماند و فقط محتوا اسکرول می‌شود.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    public MainViewModel(AppSettings? settings = null)
    {
        _settings = settings ?? AppSettings.Load();

        Lock = new LockViewModel(AppHost.Current);
        // ⚠️ بخشِ آغازین بعد از ورود بار می‌شود، نه در سازنده. دو دلیل:
        //   ۱) پیش از ورود هیچ اجازه‌ای نداریم و لایهٔ سرویس درست هم رد می‌کند.
        //   ۲) وقتی در سازنده بار می‌شد، عددهای نوارِ بالا و داشبورد روی همان
        //      لحظهٔ صفرِ پیش از ورود می‌ماندند و کاربر «۰ افغانی» می‌دید.
        Lock.SignedIn += () =>
        {
            IsLocked = false;
            _ = OpenStartSectionAsync();

            // ══ عکسِ روزانه (بندِ ۲۳) ══════════════════════════════════════════
            // همان ‎_autoDailyBackup‎ی نسخهٔ وب، ولی از فایلِ دیتابیس. روی نخِ
            // دیگر می‌رود تا باز شدنِ برنامه معطلِ آن نماند، و خودش هیچ استثنایی
            // بیرون نمی‌دهد — بکاپِ خودکار نباید ورودِ کاربر را بشکند.
            _ = Task.Run(() => AppHost.Current.Backup.SnapshotToday());
        };

        Sections = new ObservableCollection<SectionViewModel>(BuildSections(AppHost.Current));
        AttachSubSections(AppHost.Current);
        Themes = new ObservableCollection<PumpTheme>(PumpTheme.All);
        _selectedTheme = PumpTheme.ById(_settings.ThemeId);

    }

    /// <summary>همان بخشی که کاربر دفعهٔ پیش داخلش بود.</summary>
    public Task OpenStartSectionAsync() =>
        GoAsync(Sections.FirstOrDefault(s => s.Id == _settings.LastSection) ?? Sections[0]);

    public ObservableCollection<SectionViewModel> Sections { get; }

    /// <summary>
    /// چهار عددِ نوارِ بالا — همان ‎#topBanner‎: الباقیِ شرکت‌ها، قرضِ کل،
    /// مفادِ امروز و مصارفِ امروز. با هر بار عوض کردنِ بخش تازه می‌شوند.
    /// </summary>
    public ObservableCollection<BannerItemViewModel> Banner { get; } = new()
    {
        new BannerItemViewModel("شرکت ها تیل (الباقی)", "Pump.Accent"),
        new BannerItemViewModel("قرض کل", "Pump.Danger"),
        new BannerItemViewModel("مفاد امروز", "Pump.Ok"),
        new BannerItemViewModel("مصارف امروز", "Pump.Warn"),
    };
    public ObservableCollection<PumpTheme> Themes { get; }

    [ObservableProperty] private SectionViewModel? _current;

    /// <summary>
    /// چیزی که ناحیهٔ محتوا نشان می‌دهد — خودِ بخش، یا زیربخشِ بازش.
    /// جدا از ‎Current‎ است تا با بسته شدنِ زیربخش، بخش دوباره ساخته نشود.
    /// </summary>
    [ObservableProperty] private SectionViewModel? _content;

    /// <summary>زیربخشی باز است ⇒ نوارِ «‹ برگشت» بالای صفحه بیاید.</summary>
    public bool IsSubOpen => Current?.OpenSub is not null;

    /// <summary>
    /// ══ سربرگ و نوارها، وقتی حسابی باز است ═══════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «تو بخش‌هایی مثل قرض‌داران، تیل امانت، شرکت‌ها و ورق
    /// آن سربرگ و بخش‌های بالا را نشان می‌دهد، ولی وقتی وارد یک حسابِ قرض‌دار
    /// یا امانت یا شرکت می‌شوی نباید دیده شوند، چون جا می‌گیرند.»
    ///
    /// در سایت هم دقیقاً همین است: ‎#personModal .modal‎ صریحاً
    /// ‎width:100%;height:100%‎ می‌گیرد و روی سربرگ و نوارِ آمار و نوارِ بخش‌ها
    /// می‌افتد. پس این‌جا هم با باز شدنِ حسابِ درونِ بخش، هر سه می‌روند و کلِ
    /// پنجره مالِ خودِ حساب می‌شود.
    /// </summary>
    public bool IsChromeVisible => Content?.IsPageOpen != true;

    /// <summary>نوشتهٔ دکمهٔ برگشت — «‹ برگشت به قرض‌داران».</summary>
    public string BackText => "‹ برگشت به " + (Current?.Title ?? "");
    [ObservableProperty] private PumpTheme _selectedTheme;
    [ObservableProperty] private string _clock = "";
    [ObservableProperty] private bool _isLocked = true;

    /// <summary>
    /// نامِ نقشِ کاربر برای نشانِ سربرگ — همان ‎.role-badge‎ نسخهٔ وب.
    /// با هر بار باز و بسته شدنِ قفل تازه می‌شود.
    /// </summary>
    public string RoleText => AppHost.Current.Session.Role switch
    {
        UserRole.Admin  => "🛡️ مدیر",
        UserRole.Staff  => "👤 کارمند",
        _               => "👁️ نظاره‌گر",
    };

    /// <summary>رنگِ نشانِ نقش — مدیر سبز، کارمند آبی، نظاره‌گر خاکستری.</summary>
    public string RoleBrushKey => AppHost.Current.Session.Role switch
    {
        UserRole.Admin => "Pump.Ok",
        UserRole.Staff => "Pump.Info",
        _              => "Pump.Muted",
    };

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(RoleText));
        OnPropertyChanged(nameof(RoleBrushKey));
    }

    /// <summary>تاریخِ شمسیِ امروز — خطِ اولِ بلوکِ تاریخِ سربرگ.</summary>
    public string TodayText => Shamsi.DayName(DateTime.Now) + "، " + Shamsi.Today();

    public LockViewModel Lock { get; }

    /// <summary>
    /// «جدولی که همین حالا جلوی کاربر است» — همان ‎_kbCtx()‎ِ نسخهٔ وب.
    /// اولویت با صفحهٔ بازِ داخلِ بخش است (حسابِ شخص، شرکت، ورق، امانت)؛
    /// اگر باز نباشد، خودِ بخش. هیچ جدولِ دیگری دست نمی‌خورد.
    /// </summary>
    public IRowBatchHost? RowHost =>
        ActiveSection?.ActivePage as IRowBatchHost ?? ActiveSection as IRowBatchHost;

    /// <summary>
    /// بخشی که واقعاً جلوی چشمِ کاربر است: اگر زیربخشی باز باشد همان، وگرنه
    /// خودِ بخش. میانبرها و «جدولِ فعال» باید همین را ببینند، نه بخشِ پشتِ آن.
    /// </summary>
    public SectionViewModel? ActiveSection => Current?.OpenSub ?? Current;

    /// <summary>پیام‌های کوتاهِ پایینِ صفحه.</summary>
    public Services.ToastService Toasts => AppHost.Current.Toasts;

    /// <summary>خروج و برگشت به صفحهٔ قفل — بی آن‌که برنامه بسته شود.</summary>
    [RelayCommand]
    private void SignOut()
    {
        AppHost.Current.Auth.SignOut();
        IsLocked = true;
    }

    partial void OnSelectedThemeChanged(PumpTheme value)
    {
        ThemeManager.Apply(value);
        _settings.ThemeId = value.Id;
        _settings.Save();
    }

    [RelayCommand]
    public async Task GoAsync(SectionViewModel? s)
    {
        if (s is null) return;

        // زدنِ دکمهٔ همان بخشی که باز است یعنی «تازه‌اش کن» — نه «هیچ کاری نکن».
        // پیش از این این‌جا برمی‌گشتیم و نتیجه‌اش این بود که عددهای نوارِ بالا
        // روی همان لحظهٔ بارِ اول می‌ماندند.
        if (ReferenceEquals(s, Current))
        {
            // زدنِ دکمهٔ بخشِ باز یعنی «از اول» — پس اگر زیربخشی باز مانده،
            // بسته می‌شود. وگرنه کاربر روی «قرض‌داران» می‌زد و باز هم صفحهٔ
            // «قرض‌های کهنه» جلویش می‌ماند.
            s.OpenSub = null;
            await s.OnActivatedAsync();
            await RefreshBannerAsync();
            return;
        }
        if (Current is not null)
        {
            Current.PropertyChanged -= OnSectionPropertyChanged;
            Current.IsActive = false;
            Current.OnDeactivated();
        }
        s.IsActive = true;
        Current = s;                       // نمونه‌ها زنده می‌مانند: هیچ ساختِ دوباره‌ای نیست
        s.PropertyChanged += OnSectionPropertyChanged;
        SyncContent();
        _settings.LastSection = s.Id;
        _settings.Save();
        await s.EnsureLoadedAsync();
        await s.OnActivatedAsync();
        await RefreshBannerAsync();
    }

    /// <summary>
    /// ══ رفتن به بخشی با شناسه ══════════════════════════════════════════════
    /// همان ‎showSection('x')‎ی نسخهٔ وب — و درست مثلِ آن، شناسه می‌تواند
    /// زیربخش هم باشد («oldloans»، «monthreport»، «datamgmt»…). آن‌وقت اول
    /// بخشِ میزبان باز می‌شود و بعد زیربخش رویش.
    ///
    /// ⚠️ بی این، لینک‌های داشبورد که به زیربخش‌ها می‌روند بی‌صدا هیچ کاری
    /// نمی‌کردند: فهرستِ ‎Sections‎ دیگر آن‌ها را ندارد.
    /// </summary>
    public async Task GoByIdAsync(string id)
    {
        if (Sections.FirstOrDefault(x => x.Id == id) is { } top) { await GoAsync(top); return; }

        foreach (var parent in Sections)
            if (parent.SubSections.FirstOrDefault(x => x.Id == id) is { } sub)
            {
                await GoAsync(parent);
                parent.OpenSub = sub;
                return;
            }
    }

    /// <summary>
    /// باز و بسته شدنِ زیربخش. ناحیهٔ محتوا عوض می‌شود و زیربخشِ تازه — مثلِ
    /// خودِ بخش‌ها — بارِ اولش را همان لحظه می‌گیرد، نه در سازنده.
    /// </summary>
    private void OnSectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SectionViewModel.OpenSub)) return;
        SyncContent();
        if (Current?.OpenSub is { } sub) _ = OpenSubAsync(sub);
    }

    /// <summary>بخشی که همین حالا محتوا است — تا باز و بسته شدنِ حسابش را بشنویم.</summary>
    private SectionViewModel? _watchedContent;

    private void OnContentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SectionViewModel.IsPageOpen))
            OnPropertyChanged(nameof(IsChromeVisible));
    }

    private async Task OpenSubAsync(SectionViewModel sub)
    {
        try
        {
            await sub.EnsureLoadedAsync();
            await sub.OnActivatedAsync();
        }
        catch (Exception ex) { AppHost.Current.Toast("باز نشد: " + ex.Message, ToastKind.Error); }
    }

    private void SyncContent()
    {
        Content = Current?.OpenSub ?? Current;

        if (!ReferenceEquals(_watchedContent, Content))
        {
            if (_watchedContent is not null)
                _watchedContent.PropertyChanged -= OnContentPropertyChanged;
            _watchedContent = Content;
            if (_watchedContent is not null)
                _watchedContent.PropertyChanged += OnContentPropertyChanged;
        }

        OnPropertyChanged(nameof(IsChromeVisible));
        OnPropertyChanged(nameof(IsSubOpen));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(ActiveSection));
        OnPropertyChanged(nameof(RowHost));
    }

    /// <summary>
    /// ══ خواندنِ دوبارهٔ همهٔ بخش‌ها ═══════════════════════════════════════════
    /// بعد از کاری که کلِ دیتابیس را عوض می‌کند — بازگردانیِ بکاپ، آوردنِ دادهٔ
    /// نسخهٔ وب، یا برگرداندنِ چیزی از سطلِ زباله.
    ///
    /// ⚠️ نمونهٔ بخش‌ها زنده می‌مانند (تا چیدمان و جای اسکرول از دست نرود)، پس
    /// بی این، صفحه‌ها عددِ دیتابیسِ **قبلی** را نشان می‌دهند و کاربر خیال
    /// می‌کند بازگردانی نگرفته است.
    ///
    /// بخشی که همین حالا باز است، آخر و همان‌جا تازه می‌شود.
    /// </summary>
    public async Task ReloadAllAsync()
    {
        // زیربخش‌ها هم بخش‌اند و دادهٔ خودشان را دارند — اگر این‌جا از قلم
        // بیفتند، «قرض‌های کهنه» بعد از بازگردانیِ بکاپ عددِ دیتابیسِ قبلی را
        // نشان می‌دهد.
        foreach (var s in Sections.Concat(Sections.SelectMany(x => x.SubSections)))
        {
            // بخشی که هرگز باز نشده، بارِ اولش را همان موقعِ ورودِ کاربر
            // می‌گیرد — این‌جا فقط باید «کهنه» علامت بخورد.
            if (!s.IsLoaded || ReferenceEquals(s, Content)) continue;
            s.IsLoaded = false;
        }

        if (Content is not null)
        {
            try { await Content.ReloadAsync(); } catch { }
            try { await Content.OnActivatedAsync(); } catch { }
        }

        await RefreshBannerAsync();
    }

    /// <summary>
    /// چهار عددِ نوارِ بالا — همان ‎updateBanner‎ِ نسخهٔ وب. فقط خواندنی است و
    /// هر بار که کاربر بخشی را باز می‌کند تازه می‌شود.
    /// </summary>
    public async Task RefreshBannerAsync()
    {
        var host = AppHost.Current;
        var calc = new DashboardService();

        // ۱) الباقیِ شرکت‌های تیل
        var companies = await host.Companies.ListAsync();
        var compAlbaqi = companies.Sum(c => host.Company.Summarize(c, c.Rows).AlbaqiAfn);

        // ۲) قرضِ کلِ قرض‌داران
        //
        // ⚠️ این‌جا پیش از این **همهٔ ردیف‌های همهٔ حساب‌ها** خوانده می‌شد — و
        // چون نوارِ بالا با هر بار عوض کردنِ بخش تازه می‌شود، همان هزینه به
        // ازای هر جابه‌جایی تکرار می‌گشت. سنجشِ کارایی همین را نشان داد:
        // باز کردنِ هر بخش، حتی سبک‌ترینشان، شش ثانیهٔ ثابت.
        //
        // ‎CardAccountsAsync‎ همان جمع‌ها را از خودِ دیتابیس می‌گیرد (با همان
        // خوددرمانیِ ردیف، داخلِ کوئری) و هیچ ردیفی نمی‌خواند.
        var accounts = await host.Debtors.CardAccountsAsync();
        decimal debt = 0;
        foreach (var list in accounts.Values) debt += host.Debt.SumTotals(list).All.Albaqi;

        // ۳) مفادِ امروز — جمعِ فایدهٔ هر دو شیفتِ پارچه‌های همین تاریخ
        var today = Shamsi.Today();
        var reports = (await host.StorageData.ReportsAsync(FuelType.Petrol))
            .Concat(await host.StorageData.ReportsAsync(FuelType.Diesel))
            .Where(r => r.DateShamsi == today);
        var profit = reports.Sum(r => (r.DayShift?.Profit ?? 0) + (r.NightShift?.Profit ?? 0));

        // ۴) مصارفِ امروز — فقط ماهِ جاری، نه همهٔ مصارفِ تاریخ. «امروز» همیشه
        //    داخلِ همین ماه است، پس عدد همان است و خواندن هزار برابر کمتر.
        var expToday = calc.ExpQuick(await host.ExpenseLedger.ListAsync(Shamsi.ThisMonth())).Day;

        string M(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        Banner[0].Value = M(compAlbaqi);
        Banner[1].Value = M(debt);
        Banner[2].Value = M(profit);
        Banner[3].Value = M(expToday);
    }

    /// <summary>
    /// ══ ترتیبِ نوار — مو‌به‌مو همان هجده دکمهٔ ‎&lt;div class="nav"&gt;‎ ═══════════
    ///
    /// ⚠️ این ترتیب فقط ظاهری نیست: میانبرِ ‎Ctrl+Shift+عدد‎ بخش را **با شمارهٔ
    /// جایش در همین فهرست** باز می‌کند. پیش از این ترتیب از ردیفِ دهم به بعد
    /// با سایت فرق داشت، پس ‎Ctrl+Shift+۱۰‎ که در سایت «گاوصندوق» بود این‌جا
    /// «رسید قرض‌داران» را باز می‌کرد — و همین‌طور تا هجده. کاربری که سال‌ها با
    /// این میانبرها کار کرده، هر بار بخشِ اشتباه را می‌گرفت.
    ///
    ///   ۱ داشبورد · ۲ پارچه‌ها · ۳ ورق‌های روزانه · ۴ قرض‌داران ·
    ///   ۵ ثبت فاکتورها · ۶ رسید قرض‌داران / چکنه · ۷ صرافی · ۸ مصارف ·
    ///   ۹ رسید پارچه · ۱۰ گاوصندوق · ۱۱ تیل امانت · ۱۲ شرکت‌ها تیل ·
    ///   ۱۳ مخزن · ۱۴ دوربین‌ها · ۱۵ حاضری و معاش · ۱۶ مفاد/ضرر ·
    ///   ۱۷ تنظیمات · ۱۸ تاریخچه‌ها
    ///
    /// ⚠️ <b>هجده‌تا و بس.</b> تا دیروز هفت بخشِ دیگر هم ته این فهرست بودند و
    /// نوار بیست‌وپنج دکمه‌ای شده بود. آن هفت‌تا در سایت هم دکمهٔ نوار ندارند:
    /// هر کدام کارتی داخلِ بخشِ دیگری‌اند. حالا این‌جا هم همان‌اند و در
    /// <see cref="AttachSubSections"/> به بخشِ خودشان بسته می‌شوند. اگر
    /// دوباره یکی‌شان را به این فهرست اضافه کنید، هم نوار شلوغ می‌شود هم
    /// آزمونِ ترتیبِ نوار قرمز.
    /// </summary>
    private IEnumerable<SectionViewModel> BuildSections(AppHost host) => new SectionViewModel[]
    {
        new DashboardSectionViewModel(host, this),   //  ۱
        new ParchaSectionViewModel(host),            //  ۲
        new WaraqSectionViewModel(host),             //  ۳
        new DebtSectionViewModel(host),              //  ۴
        new InvoiceSectionViewModel(host),           //  ۵
        new DebtReceiptSectionViewModel(host),       //  ۶
        new ExchangeSectionViewModel(host),          //  ۷
        new ExpenseSectionViewModel(host),           //  ۸
        new ParchaReceiptSectionViewModel(host),     //  ۹
        new SafeSectionViewModel(host),              // ۱۰
        new AmanatSectionViewModel(host),            // ۱۱
        new CompanySectionViewModel(host),           // ۱۲
        new StorageSectionViewModel(host),           // ۱۳
        new CameraSectionViewModel(host),            // ۱۴
        new AttendanceSectionViewModel(host),        // ۱۵
        new ProfitSectionViewModel(host),            // ۱۶
        new SettingsSectionViewModel(host),          // ۱۷
        new HistorySectionViewModel(host),           // ۱۸
    };

    /// <summary>
    /// ══ هفت زیربخش، هر کدام زیرِ بخشِ خودش ══════════════════════════════════
    ///
    /// جای هر کدام از خودِ سایت آمده — همان‌جایی که ‎.tool-link-card‎ش نشسته:
    ///
    ///   مقایسهٔ نرخ      → ثبت فاکتورها            (‎sec-invoices‎)
    ///   چکنه            → رسید قرض‌داران / چکنه   (‎sec-debtrasid‎)
    ///   تخلیهٔ تانکر     → مخزن                    (‎sec-storage‎)
    ///   کمبودی کارمندان → حاضری و معاش            (‎sec-attendance‎)
    ///   قرض‌های کهنه     → قرض‌داران                (‎sec-debt‎)
    ///   گزارش ماهانه    → مفاد / ضرر              (‎sec-profit‎)
    ///   تاریخچهٔ نرخ     → مفاد / ضرر              (‎sec-profit‎)
    ///   مدیریت داده‌ها   → تنظیمات                 (‎sec-settings‎)
    ///
    /// ⚠️ نمونه‌ها این‌جا ساخته می‌شوند، نه در ‎BuildSections‎ — وگرنه آزمونِ
    /// ترتیبِ نوار (که تنِ ‎BuildSections‎ را می‌خواند) آن‌ها را هم دکمهٔ نوار
    /// می‌شمارد.
    /// </summary>
    private void AttachSubSections(AppHost host)
    {
        SectionViewModel? By(string id) => Sections.FirstOrDefault(s => s.Id == id);

        By("invoices")?.AddSub(new InvRateSectionViewModel(host),      "📉 مقایسهٔ نرخ فاکتورها");
        By("debtrasid")?.AddSub(new RetailSectionViewModel(host),      "🧾 حساب‌های چکنه");
        By("storage")?.AddSub(new TankerSectionViewModel(host),        "🚚 تخلیهٔ تانکر");
        By("attendance")?.AddSub(new StaffShortSectionViewModel(host), "👷 کمبودی کارمندان");
        By("debt")?.AddSub(new OldLoansSectionViewModel(host),         "⏰ قرض‌های کهنه");
        By("profit")?.AddSub(new MonthReportSectionViewModel(host),    "📅 گزارش پایان ماه");
        By("profit")?.AddSub(new RateHistorySectionViewModel(host),    "📈 تاریخچهٔ نرخ اتحادیه");
        By("settings")?.AddSub(new DataSectionViewModel(host, this),   "🗂️ مدیریت داده‌ها");
    }
}
