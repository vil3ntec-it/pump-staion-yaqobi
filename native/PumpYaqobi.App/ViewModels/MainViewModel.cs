using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// پوستهٔ برنامه: سربرگ، نوارِ بخش‌ها و ناحیهٔ محتوا.
/// همان هجده دکمهٔ نوارِ نسخهٔ وب، با همان ترتیب و همان نام‌ها.
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
    public ObservableCollection<PumpTheme> Themes { get; }

    [ObservableProperty] private SectionViewModel? _current;
    [ObservableProperty] private PumpTheme _selectedTheme;
    [ObservableProperty] private string _clock = "";
    [ObservableProperty] private bool _isLocked = true;

    public LockViewModel Lock { get; }

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
    }

    /// <summary>ترتیبِ نوار، مو‌به‌مو مثلِ <c>&lt;div class="nav"&gt;</c> در نسخهٔ وب.</summary>
    /// <summary>ترتیبِ نوار، مو‌به‌مو مثلِ <c>&lt;div class="nav"&gt;</c> در نسخهٔ وب.</summary>
    private static IEnumerable<SectionViewModel> BuildSections(AppHost host) => new SectionViewModel[]
    {
        new PlaceholderSectionViewModel("dashboard", "داشبورد"),
        new ParchaSectionViewModel(host),
        new WaraqSectionViewModel(host),
        new DebtSectionViewModel(host),
        new PlaceholderSectionViewModel("invoices",  "ثبت فاکتورها"),
        new RetailSectionViewModel(host),
        new ExchangeSectionViewModel(host),
        new ExpenseSectionViewModel(host),
        new PlaceholderSectionViewModel("rasid",     "رسید پارچه"),
        new SafeSectionViewModel(host),
        new PlaceholderSectionViewModel("amanat",    "تیل امانت"),
        new CompanySectionViewModel(host),
        new PlaceholderSectionViewModel("storage",   "مخزن"),
        new PlaceholderSectionViewModel("cameras",   "دوربین‌ها"),
        new PlaceholderSectionViewModel("attendance","حاضری و معاش"),
        new PlaceholderSectionViewModel("profit",    "مفاد / ضرر / اتحادیه"),
        new PlaceholderSectionViewModel("settings",  "تنظیمات"),
        new PlaceholderSectionViewModel("history",   "تاریخچه‌ها"),
    };
}
