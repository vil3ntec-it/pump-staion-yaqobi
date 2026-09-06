using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.Update;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ تنظیمات ════════════════════════════════════════════════════════════════
/// مشخصاتِ پمپ، نرخِ اتحادیه، آستانهٔ کمبودِ مخزن، تم، و به‌روزرسانیِ برنامه.
///
/// ⚠️ کادرِ به‌روزرسانی عمداً هیچ نشانی یا نامِ منبعی نشان نمی‌دهد — فقط
/// «نسخهٔ فعلی»، «نسخهٔ تازه» و دکمه. (خواستهٔ صریحِ صاحب ریپو.)
/// </summary>
public sealed partial class SettingsSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private readonly UpdateService _update = new();
    private UpdateInfo? _info;
    private string? _downloaded;

    public SettingsSectionViewModel(AppHost host) : base("settings", "settings", "تنظیمات")
    {
        _host = host;
        _currentVersion = AppVersion.Current;
        Themes = new System.Collections.ObjectModel.ObservableCollection<PumpTheme>(PumpTheme.All);
        _selectedTheme = ThemeManager.Current;
    }

    public System.Collections.ObjectModel.ObservableCollection<PumpTheme> Themes { get; }

    // ── مشخصاتِ پمپ ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _stationName = "";
    [ObservableProperty] private string _stationAddress = "";
    [ObservableProperty] private string _stationPhone = "";

    // ── نرخ‌ها و آستانه‌ها ──────────────────────────────────────────────────
    [ObservableProperty] private string _unionRatePetrol = "";
    [ObservableProperty] private string _unionRateDiesel = "";
    [ObservableProperty] private string _lowStockThreshold = "";

    [ObservableProperty] private PumpTheme _selectedTheme;

    // ── به‌روزرسانی ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _currentVersion;
    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private bool _checking;
    [ObservableProperty] private bool _downloading;
    [ObservableProperty] private double _downloadPercent;
    [ObservableProperty] private bool _readyToInstall;

    partial void OnSelectedThemeChanged(PumpTheme value) => ThemeManager.Apply(value);

    protected override Task LoadAsync()
    {
        var s = _host.Settings;
        StationName = s.GetString(SettingsService.StationName);
        StationAddress = s.GetString(SettingsService.StationAddress);
        StationPhone = s.GetString(SettingsService.StationPhone);
        UnionRatePetrol = Shamsi.Money(s.GetDecimal(SettingsService.UnionRatePetrol));
        UnionRateDiesel = Shamsi.Money(s.GetDecimal(SettingsService.UnionRateDiesel));
        LowStockThreshold = Shamsi.Money(s.GetDecimal(SettingsService.LowStockThreshold, 1000m));
        SelectedTheme = ThemeManager.Current;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Save()
    {
        var s = _host.Settings;
        s.Set(SettingsService.StationName, StationName.Trim());
        s.Set(SettingsService.StationAddress, StationAddress.Trim());
        s.Set(SettingsService.StationPhone, StationPhone.Trim());
        s.Set(SettingsService.UnionRatePetrol, Shamsi.Num(UnionRatePetrol));
        s.Set(SettingsService.UnionRateDiesel, Shamsi.Num(UnionRateDiesel));
        s.Set(SettingsService.LowStockThreshold, Shamsi.Num(LowStockThreshold));
        _host.Toast("✅ تنظیمات ذخیره شد", ToastKind.Ok);
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        Checking = true;
        UpdateStatus = "در حال بررسی…";
        try
        {
            _info = await _update.CheckAsync();
            UpdateAvailable = _info.Available;
            UpdateStatus = _info.Available
                ? $"نسخهٔ تازه آماده است: {_info.LatestVersion}"
                : "برنامه به‌روز است";
        }
        finally { Checking = false; }
    }

    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        if (_info is null || !_info.Available) return;
        Downloading = true;
        DownloadPercent = 0;
        UpdateStatus = "در حال گرفتنِ نسخهٔ تازه…";
        try
        {
            var progress = new Progress<double>(p => DownloadPercent = p);
            _downloaded = await _update.DownloadAsync(_info, progress);
            if (_downloaded is null)
            {
                UpdateStatus = "گرفتنِ نسخهٔ تازه انجام نشد";
                return;
            }
            ReadyToInstall = true;
            UpdateStatus = "نسخهٔ تازه گرفته شد — آمادهٔ نصب";
        }
        finally { Downloading = false; }
    }

    [RelayCommand]
    private void InstallUpdate()
    {
        if (_downloaded is null) return;
        if (UpdateService.Launch(_downloaded))
        {
            UpdateStatus = "نصاب باز شد — برنامه بسته می‌شود";
            _host.Toast("برنامه برای نصبِ نسخهٔ تازه بسته می‌شود", ToastKind.Info);
        }
        else UpdateStatus = "باز کردنِ نصاب انجام نشد";
    }
}
