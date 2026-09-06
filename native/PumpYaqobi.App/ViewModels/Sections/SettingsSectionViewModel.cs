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

        // پوشهٔ نصب را خودِ کاربر موقعِ نصب انتخاب می‌کند، پس این‌جا نشان
        // داده می‌شود — و اگر جایی باشد که نوشتن در آن اجازهٔ مدیر می‌خواهد،
        // همان‌جا گفته می‌شود، نه وسطِ به‌روزرسانی.
        _installDir = UpdateService.InstallDir;
        _installNote = UpdateService.InstallDirWritable
            ? ""
            : "این پوشه اجازهٔ مدیر می‌خواهد؛ هنگامِ به‌روزرسانی ویندوز اجازه می‌پرسد.";

        // اگر به‌روزرسانیِ گذشته نیمه‌کاره مانده، همین حالا گفته شود
        _lastFailure = UpdateService.ConsumeLastFailure() ?? "";
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

    /// <summary>پوشه‌ای که کاربر موقعِ نصب انتخاب کرده.</summary>
    [ObservableProperty] private string _installDir = "";

    /// <summary>خالی یعنی همه‌چیز رو‌به‌راه است.</summary>
    [ObservableProperty] private string _installNote = "";

    /// <summary>«به‌روزرسانیِ کوچک — ۲٫۱ مگابایت» یا «بستهٔ کامل — ۵۷ مگابایت».</summary>
    [ObservableProperty] private string _packageText = "";

    /// <summary>پیامِ به‌روزرسانیِ ناتمامِ دفعهٔ پیش.</summary>
    [ObservableProperty] private string _lastFailure = "";

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
            PackageText = _info.PackageText;
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

    /// <summary>
    /// نصب و راه‌اندازیِ دوباره.
    ///
    /// ⚠️ برنامه باید همین‌جا بسته شود: تا وقتی باز است، فایل‌های خودش قفل‌اند
    /// و جای‌گزینی شکست می‌خورد. کارِ جابه‌جایی و باز کردنِ دوبارهٔ برنامه به
    /// دستورِ بیرونی سپرده شده که خودش منتظرِ بسته شدنِ ما می‌ماند.
    /// </summary>
    [RelayCommand]
    private void InstallUpdate()
    {
        if (_downloaded is null) return;
        LastFailure = "";
        if (!UpdateService.Launch(_downloaded)) { UpdateStatus = "نصبِ نسخهٔ تازه انجام نشد"; return; }

        UpdateStatus = "برنامه بسته می‌شود و با نسخهٔ تازه باز می‌شود";
        _host.Toast("برنامه بسته می‌شود و با نسخهٔ تازه باز می‌شود", ToastKind.Info);

        // یک لحظه فرصت بده پیام دیده شود، بعد ببند
        _ = Task.Run(async () =>
        {
            await Task.Delay(1200);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)
                    d.Shutdown();
                else Environment.Exit(0);
            });
        });
    }

    /// <summary>
    /// بررسیِ خودکار در پس‌زمینه — یک‌بار، هنگامِ بازکردنِ همین صفحه. بی‌صدا
    /// است: اگر اینترنت نباشد یا نسخهٔ تازه‌ای نباشد، هیچ پیامی نمی‌آید.
    /// </summary>
    public override async Task OnActivatedAsync()
    {
        if (_autoChecked) return;
        _autoChecked = true;
        try { await CheckUpdateAsync(); } catch { }
    }

    private bool _autoChecked;
}
