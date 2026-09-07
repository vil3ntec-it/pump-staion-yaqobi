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
        Current?.ActivePage as IRowBatchHost ?? Current as IRowBatchHost;

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
            await s.OnActivatedAsync();
            await RefreshBannerAsync();
            return;
        }
        if (Current is not null) { Current.IsActive = false; Current.OnDeactivated(); }
        s.IsActive = true;
        Current = s;                       // نمونه‌ها زنده می‌مانند: هیچ ساختِ دوباره‌ای نیست
        _settings.LastSection = s.Id;
        _settings.Save();
        await s.EnsureLoadedAsync();
        await s.OnActivatedAsync();
        await RefreshBannerAsync();
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
        foreach (var s in Sections)
        {
            // بخشی که هرگز باز نشده، بارِ اولش را همان موقعِ ورودِ کاربر
            // می‌گیرد — این‌جا فقط باید «کهنه» علامت بخورد.
            if (!s.IsLoaded || ReferenceEquals(s, Current)) continue;
            s.IsLoaded = false;
        }

        if (Current is not null)
        {
            try { await Current.ReloadAsync(); } catch { }
            try { await Current.OnActivatedAsync(); } catch { }
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

        // ۲) قرضِ کلِ قرض‌داران — با همان خوددرمانیِ کارت‌ها
        var accounts = await host.Debtors.AccountsByDebtorAsync();
        decimal debt = 0;
        foreach (var list in accounts.Values)
        {
            foreach (var a in list) host.Debt.NormalizeAccount(a);
            debt += host.Debt.SumTotals(list).All.Albaqi;
        }

        // ۳) مفادِ امروز — جمعِ فایدهٔ هر دو شیفتِ پارچه‌های همین تاریخ
        var today = Shamsi.Today();
        var reports = (await host.StorageData.ReportsAsync(FuelType.Petrol))
            .Concat(await host.StorageData.ReportsAsync(FuelType.Diesel))
            .Where(r => r.DateShamsi == today);
        var profit = reports.Sum(r => (r.DayShift?.Profit ?? 0) + (r.NightShift?.Profit ?? 0));

        // ۴) مصارفِ امروز
        var expToday = calc.ExpQuick(await host.ExpenseLedger.ListAsync(null)).Day;

        string M(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        Banner[0].Value = M(compAlbaqi);
        Banner[1].Value = M(debt);
        Banner[2].Value = M(profit);
        Banner[3].Value = M(expToday);
    }

    /// <summary>ترتیبِ نوار، مو‌به‌مو مثلِ <c>&lt;div class="nav"&gt;</c> در نسخهٔ وب.</summary>
    /// <summary>ترتیبِ نوار، مو‌به‌مو مثلِ <c>&lt;div class="nav"&gt;</c> در نسخهٔ وب.</summary>
    private IEnumerable<SectionViewModel> BuildSections(AppHost host) => new SectionViewModel[]
    {
        new DashboardSectionViewModel(host, this),
        new ParchaSectionViewModel(host),
        new WaraqSectionViewModel(host),
        new DebtSectionViewModel(host),
        new InvoiceSectionViewModel(host),
        new RetailSectionViewModel(host),
        new ExchangeSectionViewModel(host),
        new ExpenseSectionViewModel(host),
        new ParchaReceiptSectionViewModel(host),
        new DebtReceiptSectionViewModel(host),
        new SafeSectionViewModel(host),
        new AmanatSectionViewModel(host),
        new CompanySectionViewModel(host),
        new StorageSectionViewModel(host),
        new TankerSectionViewModel(host),
        new CameraSectionViewModel(host),
        new AttendanceSectionViewModel(host),
        new StaffShortSectionViewModel(host),
        new OldLoansSectionViewModel(host),
        new MonthReportSectionViewModel(host),
        new RateHistorySectionViewModel(host),
        new ProfitSectionViewModel(host),
        new SettingsSectionViewModel(host),
        new DataSectionViewModel(host, this),
        new HistorySectionViewModel(host),
    };
}
