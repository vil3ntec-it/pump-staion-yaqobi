using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.Update;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Services.Data;
using PumpYaqobi.Services.Vision;

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

        // نتیجهٔ به‌روزرسانیِ گذشته — چه گرفت چه نگرفت، همین حالا گفته شود.
        // «هیچ نگفتن» بدترین حالت است: کاربر خیال می‌کند به‌روز شده.
        var last = UpdateService.ConsumeLastResult();
        _lastFailure = last is null || last.Ok ? "" : last.Message;
        _lastSuccess = last is not null && last.Ok ? last.Message : "";
    }

    public System.Collections.ObjectModel.ObservableCollection<PumpTheme> Themes { get; }

    // ── مشخصاتِ پمپ ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _stationName = "";
    [ObservableProperty] private string _stationAddress = "";
    [ObservableProperty] private string _stationPhone = "";

    /// <summary>
    /// ══ دو نشانیِ جدا ═══════════════════════════════════════════════════════
    ///
    /// ⚠️ این دو یکی نیستند و نباید یکی گرفته شوند — در نسخهٔ وب هم جدا بودند:
    ///
    ///   • ‎ServerUrl‎  سرورِ **داده** (هم‌گام‌سازی) — همتای ‎SELF_HOST_URL‎.
    ///                 برنامه از این‌جا داده می‌گیرد و می‌فرستد.
    ///   • ‎ViewerUrl‎  نشانیِ **صفحهٔ حساب** — جایی که ‎index.html‎ سِرو می‌شود.
    ///                 کیو‌آرِ هر قرض‌دار از این ساخته می‌شود؛ مشتری اسکن
    ///                 می‌کند، گوشی‌اش این صفحه را باز می‌کند و صفحه خودش
    ///                 داده را از سرورِ بالایی می‌گیرد.
    ///
    /// در سایت این دومی لازم نبود چون خودِ صفحه نشانیِ خودش را می‌دانست
    /// (‎window.location.href‎). برنامهٔ نیتیو صفحه‌ای ندارد، پس نوشته می‌شود.
    /// </summary>
    [ObservableProperty] private string _serverUrl = "";
    [ObservableProperty] private string _viewerUrl = "";

    /// <summary>رمزِ سرورِ هم‌گام‌سازی — اگر سرور رمز دارد.</summary>
    [ObservableProperty] private string _syncCode = "";

    // ── وصل شدن به سرور ─────────────────────────────────────────────────────
    //
    // خواستهٔ صریحِ صاحب ریپو: «اگه ادرس نداشت با ادرس براش بساز که کار کنه».
    // پس کاربر معمولاً هیچ‌کدام از کادرهای بالا را دست نمی‌زند: دکمهٔ «پیدا کن
    // و وصل شو» سرور را در شبکهٔ خانگی می‌یابد، پوشه و رمزِ همین پمپ را
    // می‌گیرد و هر دو جای تنظیمات را خودش می‌نویسد.

    /// <summary>
    /// کدِ همین پمپ بنزین. روی سرور یک پوشهٔ کاملاً جدا با همین نام ساخته
    /// می‌شود، پس دو پمپ نباید کدِ یکسان داشته باشند.
    /// </summary>
    [ObservableProperty] private string _stationCode = HomeLink.DefaultStationCode;

    /// <summary>کدِ شش‌رقمیِ پنلِ سرور — فقط وقتی برنامه در شبکهٔ خانگی نیست.</summary>
    [ObservableProperty] private string _pairPin = "";

    /// <summary>آخرین خبر از وصل شدن — به فارسی، برای خودِ کاربر.</summary>
    [ObservableProperty] private string _serverStatus = "";

    /// <summary>نشانیِ ‎GET‎ی عکسِ زنده — همان چیزی که در شورت‌کاتِ آیفون می‌گذارید.</summary>
    [ObservableProperty] private string _shortcutUrl = "";

    // ── نرخ‌ها و آستانه‌ها ──────────────────────────────────────────────────
    [ObservableProperty] private string _unionRatePetrol = "";
    [ObservableProperty] private string _unionRateDiesel = "";
    [ObservableProperty] private string _lowStockThreshold = "";

    [ObservableProperty] private PumpTheme _selectedTheme;

    // ── ظاهرِ جدول‌ها ────────────────────────────────────────────────────────
    //
    // خواستهٔ صریحِ صاحب ریپو: «خطوطِ جدول نباید Hard-coded باشند — یک بخشِ
    // «ظاهرِ جدول» در تنظیمات باشد با رنگِ خط و ضخامتِ خط (۱ تا ۴ پیکسل)، و
    // همان روی **همهٔ** جدول‌های برنامه اعمال شود.»
    //
    // این چهار کادر تنها جای تعریفِ خطِ جدول‌اند. هر تغییری همان لحظه هم
    // ذخیره می‌شود و هم روی همهٔ جدول‌ها می‌نشیند — دکمهٔ «ذخیره» لازم ندارد،
    // چون کاربر باید نتیجه را همان‌جا ببیند و انتخاب کند.

    /// <summary>ضخامت‌های مجاز: ۱ تا ۴ پیکسل.</summary>
    public IReadOnlyList<double> LineSizes { get; } = TableStyle.Sizes;

    /// <summary>خالی یعنی «رنگِ خودِ تم».</summary>
    [ObservableProperty] private string _tableBorderColor = "";
    [ObservableProperty] private double _tableLine = 1;
    [ObservableProperty] private double _tableHeadLine = 2;
    [ObservableProperty] private double _tableSumLine = 2;

    /// <summary>تا وقتی تنظیماتِ ذخیره‌شده خوانده نشده، هیچ‌چیز نوشته نشود.</summary>
    private bool _tableReady;

    partial void OnTableBorderColorChanged(string value) => ApplyTableStyle();
    partial void OnTableLineChanged(double value) => ApplyTableStyle();
    partial void OnTableHeadLineChanged(double value) => ApplyTableStyle();
    partial void OnTableSumLineChanged(double value) => ApplyTableStyle();

    /// <summary>رنگِ نوشته‌شده خوانا نیست؟ کادرِ راهنما همین را می‌گوید.</summary>
    public string TableColorNote =>
        string.IsNullOrWhiteSpace(TableBorderColor) ? "رنگِ خطِ جدول از خودِ تم گرفته می‌شود."
        : TableStyle.Parse(TableBorderColor) is null ? "این رنگ خوانده نشد — مثلِ ‎#3b4252‎ بنویسید."
        : "رنگِ دستی روی همهٔ جدول‌ها نشست.";

    [RelayCommand]
    private void UseThemeTableColor() => TableBorderColor = "";

    private void ApplyTableStyle()
    {
        OnPropertyChanged(nameof(TableColorNote));
        if (!_tableReady) return;

        var a = AppSettings.Load();
        a.TableBorderColor = TableStyle.Parse(TableBorderColor) is null ? "" : TableBorderColor.Trim();
        a.TableLine = TableLine;
        a.TableHeadLine = TableHeadLine;
        a.TableSumLine = TableSumLine;
        a.Save();
        TableStyle.Apply(a);
    }

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

    /// <summary>«✅ برنامه به نسخهٔ … به‌روز شد» — تاییدِ دفعهٔ پیش.</summary>
    [ObservableProperty] private string _lastSuccess = "";

    partial void OnSelectedThemeChanged(PumpTheme value) => ThemeManager.Apply(value);

    protected override Task LoadAsync()
    {
        var s = _host.Settings;
        StationName = s.GetString(SettingsService.StationName);
        StationAddress = s.GetString(SettingsService.StationAddress);
        StationPhone = s.GetString(SettingsService.StationPhone);
        ServerUrl = s.GetString(SettingsService.ServerUrl);
        ViewerUrl = s.GetString(SettingsService.ViewerUrl);
        SyncCode = s.GetString(SettingsService.SyncCode);
        UnionRatePetrol = Shamsi.Money(s.GetDecimal(SettingsService.UnionRatePetrol));
        UnionRateDiesel = Shamsi.Money(s.GetDecimal(SettingsService.UnionRateDiesel));
        LowStockThreshold = Shamsi.Money(s.GetDecimal(SettingsService.LowStockThreshold, 1000m));
        SelectedTheme = ThemeManager.Current;

        // کدِ پمپ و رمزِ خواندن کنارِ خودِ برنامه می‌نشینند، نه در دیتابیس:
        // مالِ همین دستگاه‌اند و بکاپِ حساب‌ها نباید ببردشان.
        var file = AppSettings.Load();
        StationCode = file.StationCode.Trim().Length > 0 ? file.StationCode.Trim() : HomeLink.DefaultStationCode;
        RefreshServerLinks();

        // ظاهرِ جدول‌ها از فایلِ کنارِ برنامه می‌آید، نه از دیتابیس
        var look = AppSettings.Load();
        _tableReady = false;
        TableBorderColor = look.TableBorderColor;
        TableLine = look.TableLine;
        TableHeadLine = look.TableHeadLine;
        TableSumLine = look.TableSumLine;
        _tableReady = true;
        OnPropertyChanged(nameof(TableColorNote));

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _host.Settings;
        s.Set(SettingsService.StationName, StationName.Trim());
        s.Set(SettingsService.StationAddress, StationAddress.Trim());
        s.Set(SettingsService.StationPhone, StationPhone.Trim());
        s.Set(SettingsService.ServerUrl, ServerUrl.Trim());
        s.Set(SettingsService.ViewerUrl, ViewerUrl.Trim());
        s.Set(SettingsService.SyncCode, SyncCode.Trim());

        // کدِ پمپ در فایل می‌نشیند — همان‌جایی که ثبتِ خودکار هم می‌خواندش
        var code = StationCode.Trim();
        if (code.Length > 0)
        {
            var file = AppSettings.Load();
            if (file.StationCode != code)
            {
                file.StationCode = code;
                file.Save();
            }
        }

        s.Set(SettingsService.UnionRatePetrol, Shamsi.Num(UnionRatePetrol));
        s.Set(SettingsService.UnionRateDiesel, Shamsi.Num(UnionRateDiesel));
        s.Set(SettingsService.LowStockThreshold, Shamsi.Num(LowStockThreshold));

        // ── تاریخچهٔ نرخ ──────────────────────────────────────────────────
        // همان کارِ ‎setUnionRate‎: نرخِ تازه اگر واقعاً عوض شده باشد ثبت
        // می‌شود. روی هیچ محاسبه‌ای اثر ندارد؛ فقط دفترچهٔ «کِی چند بود».
        var changed = await _host.Tools.RecordRateAsync(
                          PumpYaqobi.Domain.Enums.FuelType.Petrol, Shamsi.Num(UnionRatePetrol))
                    | await _host.Tools.RecordRateAsync(
                          PumpYaqobi.Domain.Enums.FuelType.Diesel, Shamsi.Num(UnionRateDiesel));

        _host.Toast(changed ? "✅ تنظیمات ذخیره شد — نرخِ تازه در تاریخچه ثبت شد"
                            : "✅ تنظیمات ذخیره شد", ToastKind.Ok);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  اپِ کارمندان — کیو‌آر و انتشارِ دستی
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو: «اپ برای اندروید برای کارمندان تا قرض‌داران را
    //  چک کنند که موجودی دارند یا نه — که اضافه ندهند — و موجودیِ تیل در مخزن
    //  را ببینند… اول رمزِ برنامهٔ نیتیوِ کامپیوتر را بخواهد.»
    //
    //  همین برنامه منبعِ آن اپ است: هر بیست ثانیه یک عکسِ کامل روی سرورِ خانگی
    //  می‌گذارد و اپ به همان گوش می‌دهد. این دو دکمه فقط دو کارِ کاربر را
    //  ممکن می‌کنند: «کیو‌آر را بگیر و بفرست» و «همین حالا بفرست».

    /// <summary>پیامِ زیرِ دکمه‌ها — چه رفت، چه نرفت و چرا.</summary>
    [ObservableProperty] private string _staffStatus = "";

    /// <summary>
    /// «📲 کیو‌آرِ اپِ کارمندان» — نشانیِ اپ با سرور و رمز و کدِ ایستگاه.
    ///
    /// ⚠️ داخلِ این کیو‌آر هیچ حسابی نیست؛ فقط نشانیِ سرور است. خودِ اپ پشتِ
    /// رمزِ همین برنامه قفل است، پس کسی که کد را ببیند هم چیزی نمی‌بیند.
    /// </summary>
    [RelayCommand]
    private Task ShowStaffQrAsync() => CrashGuard.RunAsync("کیو‌آرِ اپِ کارمندان", async () =>
    {
        var link = KarLink.Build(_host);
        if (link is null)
        {
            StaffStatus = "اول «نشانیِ سرور» را بنویسید و ذخیره کنید — "
                        + "بی سرور، اپِ کارمندان هیچ داده‌ای ندارد.";
            return;
        }

        var png = await Task.Run(() => QrWriter.EncodePng(link));
        await Dialogs.ShowQrAsync("📲 اپِ کارمندان", link, png,
            "این کد را به کارمندان بدهید. با اسکنش اپ روی گوشیشان باز می‌شود و "
            + "رمزِ همین برنامه را می‌پرسد. اپ فقط می‌خوانَد — هیچ حسابی را عوض نمی‌کند.");
        StaffStatus = "";
    });

    /// <summary>
    /// «🔎 سرور را پیدا کن و وصل شو».
    ///
    /// همان سه گامِ <see cref="StationLink"/>: پیدا کردنِ سرور در شبکهٔ خانگی،
    /// گرفتنِ پوشه و رمزِ همین پمپ، و نوشتنِ هر دو جای تنظیمات. اگر برنامه در
    /// شبکهٔ خانگی نیست، کدِ شش‌رقمیِ پنلِ سرور را در کادرِ کنارش بزنید.
    /// </summary>
    [RelayCommand]
    private Task ConnectServerAsync() => CrashGuard.RunAsync("وصل شدن به سرور", async () =>
    {
        ServerStatus = "در حالِ گشتن دنبالِ سرور…";

        // کدِ پمپ باید پیش از ثبت روی دیسک باشد، وگرنه با کدِ قبلی ثبت می‌شود
        var code = StationCode.Trim();
        if (code.Length > 0)
        {
            var file = AppSettings.Load();
            if (file.StationCode != code) { file.StationCode = code; file.Save(); }
        }

        var res = await StationLink.EnsureAsync(_host, PairPin, force: true);
        if (!res.Ok)
        {
            ServerStatus = "❌ " + res.Why;
            return;
        }

        PairPin = "";
        _host.Settings.Invalidate();
        ServerUrl = _host.Settings.GetString(SettingsService.ServerUrl);
        SyncCode = _host.Settings.GetString(SettingsService.SyncCode);
        StationCode = res.Code;
        RefreshServerLinks();

        var sent = await _host.Publisher.PublishOnceAsync(force: true);
        ServerStatus = (res.Created ? "✅ پوشهٔ این پمپ روی سرور ساخته شد" : "✅ به پوشهٔ همین پمپ وصل شدیم")
            + $" — {res.Name} ({res.Code}) روی {res.Url}"
            + (sent ? "، و عکسِ تازه همین حالا رفت." : ". هنوز چیزی نرفت؛ چند لحظه دیگر خودش می‌فرستد.");
    });

    /// <summary>نشانی‌هایی که به گوشی‌ها داده می‌شود، از روی تنظیماتِ همین لحظه.</summary>
    private void RefreshServerLinks()
    {
        ShortcutUrl = StationLink.LiveUrl(HomeLink.Url(_host), HomeLink.StationCode(_host), HomeLink.ReadKey(_host))
                      ?? "";
    }

    /// <summary>
    /// «🚀 همین حالا بفرست» — بی معطلیِ حلقهٔ بیست‌ثانیه‌ای.
    /// </summary>
    [RelayCommand]
    private Task PublishNowAsync() => CrashGuard.RunAsync("انتشار", async () =>
    {
        StaffStatus = "در حالِ فرستادن…";
        var ok = await _host.Publisher.PublishOnceAsync(force: true);
        StaffStatus = ok
            ? "✅ عکسِ تازهٔ برنامه روی سرور نشست — گوشی‌ها همین حالا می‌بینند."
            : "❌ نرفت. نشانیِ سرور یا رمزش را بررسی کنید و مطمئن شوید سرورِ "
              + "خانگی روشن است.";
    });

    // ── آوردنِ دادهٔ نسخهٔ وب ────────────────────────────────────────────────
    [ObservableProperty] private bool _importBusy;
    [ObservableProperty] private string _importStatus = "";

    /// <summary>
    /// فایلِ بکاپِ نسخهٔ وب (‎pump-backup-….json‎) را می‌آورد.
    ///
    /// اگر دیتابیس خالی نباشد، سرویس خودش جلویش را می‌گیرد و این‌جا صریح
    /// پرسیده می‌شود — «جایگزین شود؟». بکاپِ خودکارِ پیش از مهاجرت همیشه
    /// گرفته می‌شود و مسیرش همین‌جا نوشته می‌شود، تا اگر چیزی بد شد کاربر
    /// بداند نسخهٔ سالمش کجاست.
    /// </summary>
    [RelayCommand]
    private async Task ImportLegacyAsync()
    {
        var path = await Dialogs.PickJsonAsync();
        if (path is null) return;

        ImportBusy = true;
        ImportStatus = "در حال خواندنِ فایل…";
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var res = await _host.LegacyImport.ImportAsync(json);

            if (!res.Ok && res.Message.Contains("خالی نیست"))
            {
                var yes = await Dialogs.ConfirmAsync(
                    "جایگزینیِ همهٔ حساب‌ها",
                    $"این فایل {res.SourceRecords} رکورد دارد و جای همهٔ حساب‌های فعلی را می‌گیرد. "
                    + "پیش از جایگزینی، یک بکاپِ کامل از دادهٔ فعلی گرفته می‌شود. ادامه؟",
                    "بله، جایگزین کن");
                if (!yes) { ImportStatus = "انصراف داده شد — چیزی عوض نشد"; return; }
                res = await _host.LegacyImport.ImportAsync(json, replaceExisting: true);
            }

            ImportStatus = res.Message
                + (res.BackupPath is not null
                    ? Environment.NewLine + "📦 بکاپِ پیش از مهاجرت: " + res.BackupPath : "")
                + (res.Warnings.Count > 0
                    ? Environment.NewLine + "⚠️ " + string.Join(" · ", res.Warnings.Take(5)) : "");
            _host.Toast(res.Ok ? res.Message : "❌ " + res.Message,
                        res.Ok ? ToastKind.Ok : ToastKind.Error);

            if (res.Ok) _host.Toast("برای دیدنِ حساب‌ها، برنامه را ببندید و باز کنید", ToastKind.Info);
        }
        catch (Exception ex)
        {
            ImportStatus = "❌ " + ex.Message;
        }
        finally { ImportBusy = false; }
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
        // نسخهٔ هدف همراهش می‌رود: دفعهٔ بعد که برنامه باز شود، خودش می‌سنجد
        // که واقعاً به همان نسخه رسیده یا نه.
        if (!UpdateService.Launch(_downloaded, _info?.LatestVersion))
        {
            UpdateStatus = "نصبِ نسخهٔ تازه انجام نشد";
            return;
        }

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
    public override Task OnActivatedAsync()
    {
        if (_autoChecked) return Task.CompletedTask;
        _autoChecked = true;

        // ══ منتظرِ اینترنت نمی‌مانیم ══════════════════════════════════════════
        //
        // ⚠️ پیش از این ‎await‎ می‌شد، و سنجشِ ‎warm‎ همین را گرفت: باز کردنِ
        // «تنظیمات» ۱٬۲۶۱ میلی‌ثانیه بود و بارِ دومش ۱۵۹ — یعنی صفحه معطلِ یک
        // درخواستِ شبکه می‌ماند. جایی که اینترنت نباشد، همان معطلی به اندازهٔ
        // مهلتِ اتصال طول می‌کشد.
        //
        // بررسیِ نسخه هیچ چیزِ دیده‌شدنی‌ای را نگه نمی‌دارد: نتیجه‌اش در
        // ‎UpdateStatus‎ می‌نشیند و هر وقت رسید، خودش دیده می‌شود.
        _ = Task.Run(async () => { try { await CheckUpdateAsync(); } catch { } });
        return Task.CompletedTask;
    }

    private bool _autoChecked;
}
