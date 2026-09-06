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
        Lock.SignedIn += () => IsLocked = false;

        Sections = new ObservableCollection<SectionViewModel>(BuildSections(AppHost.Current));
        Themes = new ObservableCollection<PumpTheme>(PumpTheme.All);
        _selectedTheme = PumpTheme.ById(_settings.ThemeId);

        var start = Sections.FirstOrDefault(s => s.Id == _settings.LastSection) ?? Sections[0];
        _ = GoAsync(start);
    }

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

    public LockViewModel Lock { get; }

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
        if (s is null || ReferenceEquals(s, Current)) return;
        if (Current is not null) Current.IsActive = false;
        s.IsActive = true;
        Current = s;                       // نمونه‌ها زنده می‌مانند: هیچ ساختِ دوباره‌ای نیست
        _settings.LastSection = s.Id;
        _settings.Save();
        await s.EnsureLoadedAsync();
        await s.OnActivatedAsync();
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
        new PlaceholderSectionViewModel("rasid",     "رسید پارچه"),
        new SafeSectionViewModel(host),
        new AmanatSectionViewModel(host),
        new CompanySectionViewModel(host),
        new StorageSectionViewModel(host),
        new PlaceholderSectionViewModel("cameras",   "دوربین‌ها"),
        new AttendanceSectionViewModel(host),
        new PlaceholderSectionViewModel("profit",    "مفاد / ضرر / اتحادیه"),
        new SettingsSectionViewModel(host),
        new PlaceholderSectionViewModel("history",   "تاریخچه‌ها"),
    };
}
