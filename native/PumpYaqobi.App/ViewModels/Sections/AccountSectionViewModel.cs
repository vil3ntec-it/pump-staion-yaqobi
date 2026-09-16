using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ 👤 حسابِ من ═════════════════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «صفحهٔ لاگین با جیمیل هم داخلِ اپ نیست و صفحهٔ
/// پروفایل هم داخلِ برنامهٔ پمپ بنزین نیست… چرا اشتراک تو تنظیمات است؟…
/// مثلِ برنامهٔ شاپ باشد که بی اینکه من رمز یا چیزی بزنم، اطلاعات از حسابش
/// به سرور بیاید و این‌جا نشانی در تنظیمات دیده نشود.»
///
/// پس این سه چیز از تنظیمات درآمدند و یک‌جا شدند — همان‌جایی که کاربرِ یک
/// برنامهٔ اشتراکی دنبالشان می‌گردد:
///
///   ۱) ورود با گوگل   — بی رمز، بی نشانی، بی کدِ دستی
///   ۲) پروفایل        — کیستم، و کدام پمپ به این حساب وصل است
///   ۳) اشتراک         — چند روز مانده، و کدِ شش‌رقمی
///
/// ⚠️ <b>نشانیِ سرورِ خانگی این‌جا هم تایپ نمی‌شود.</b> بعد از ورود، خودِ
/// برنامه از <c>/api/pump/me</c> می‌پرسد و نشانی و رمزِ خواندن را می‌گیرد و
/// می‌نشاند. کادرِ نشانی نه این‌جاست نه در تنظیمات — و نباید بیاید.
/// </summary>
public sealed partial class AccountSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public AccountSectionViewModel(AppHost host) : base("account", "settings", "پروفایل")
    {
        _host = host;
        ShowAccount();
        ShowSubscription();
        ShowAccessCode();
        ShowPump();
    }

    /// <summary>
    /// نوشتهٔ دکمهٔ «پروفایل»ِ سربرگ — کنارِ تم. خواستهٔ صریحِ صاحب ریپو:
    /// «یک بخشِ جدید بالای صفحه بغلِ تم بگذار به اسمِ پروفایل که آن‌جا هم
    /// بتواند لاگین با جیمیل را انجام بدهد و هم VIP و مدتش را ببیند.»
    /// همان‌جا، بی کلیک، دیده می‌شود: «VIP · ۴۲ روز» یا «بدون اشتراک».
    /// </summary>
    [ObservableProperty] private string _pillText = "پروفایل";

    /// <summary>خطِ دومِ دکمه — نامِ حساب یا «وارد نشده».</summary>
    [ObservableProperty] private string _pillSub = "";

    /// <summary>اشتراکِ فعال ⇒ رنگِ طلایی؛ وگرنه خنثی.</summary>
    [ObservableProperty] private bool _vipActive;

    /// <summary>روزهای مانده — برای دکمه و کارت.</summary>
    [ObservableProperty] private int _vipDays;

    /// <summary>پس از ورود یا هر تغییرِ تنظیمات، همه‌چیز از نو خوانده می‌شود.</summary>
    public void RefreshAll()
    {
        ShowAccount();
        ShowSubscription();
        ShowAccessCode();
        ShowPump();
    }

    private void UpdatePill()
    {
        VipActive = SubActive;
        PillText = SubActive ? $"VIP · {VipDays} روز" : "پروفایل";
        PillSub = SignedIn
            ? (AccountName.Trim().Length > 0 ? AccountName.Trim() : AccountEmail.Trim())
            : (SubActive ? "بدون ورود" : "وارد نشده");
    }

    private CloudLink Cloud => _cloud ??= new CloudLink(
        AppSettings.Load(), () => { AppSettings.Load().Save(); return Task.CompletedTask; });
    private CloudLink? _cloud;

    // ── ۱) ورود ─────────────────────────────────────────────────────────

    /// <summary>وارد شده‌ایم یا نه — نیمی از صفحه به همین بسته است.</summary>
    [ObservableProperty] private bool _signedIn;

    /// <summary>وارونهٔ بالا، برای نشان دادنِ دکمهٔ ورود.</summary>
    public bool SignedOut => !SignedIn;

    partial void OnSignedInChanged(bool v) => OnPropertyChanged(nameof(SignedOut));

    [ObservableProperty] private string _accountName = "";
    [ObservableProperty] private string _accountEmail = "";

    /// <summary>حرفِ اولِ نام — به‌جای عکسِ پروفایل.</summary>
    [ObservableProperty] private string _initial = "؟";

    /// <summary>یک خط دربارهٔ حالِ ورود.</summary>
    [ObservableProperty] private string _accountStatus = "";

    /// <summary>پمپی که به این حساب وصل است.</summary>
    [ObservableProperty] private string _stationLine = "";

    [ObservableProperty] private bool _busy;

    private void ShowAccount()
    {
        var f = AppSettings.Load();
        SignedIn = !string.IsNullOrWhiteSpace(f.CloudAccountToken);
        AccountName = f.CloudName;
        AccountEmail = f.CloudEmail;

        var source = AccountName.Trim().Length > 0 ? AccountName.Trim() : AccountEmail.Trim();
        Initial = source.Length > 0 ? source[..1].ToUpperInvariant() : "؟";

        AccountStatus = SignedIn ? "" : "برای گرفتن اشتراک و وصل شدنِ خودکار، با گوگل وارد شوید.";
        UpdatePill();
    }

    /// <summary>
    /// ورود با گوگل — مرورگرِ سیستم باز می‌شود و کاربر همان‌جا وارد می‌شود.
    /// رمزِ گوگل هیچ‌وقت داخلِ این برنامه تایپ نمی‌شود.
    /// </summary>
    [RelayCommand]
    private Task SignInAsync() => CrashGuard.RunAsync("ورود با گوگل", async () =>
    {
        Busy = true;
        AccountStatus = "در حالِ باز کردنِ مرورگر…";
        try
        {
            var clientId = await Cloud.GoogleClientIdAsync();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                AccountStatus = "❌ ورود با گوگل روی سرور روشن نیست.";
                return;
            }

            var google = await GoogleSignIn.RunAsync(clientId);
            if (!google.Ok) { AccountStatus = "❌ " + google.Why; return; }

            AccountStatus = "در حالِ ساختنِ حساب روی سرور…";
            var res = await Cloud.SignInAsync(google.IdToken);
            if (!res.Ok) { AccountStatus = "❌ " + res.Why; return; }

            ShowAccount();
            await PullHomeAsync();
            ShowSubscription();
        }
        finally { Busy = false; }
    });

    /// <summary>خروج — دفترِ روی کامپیوتر دست نمی‌خورد.</summary>
    [RelayCommand]
    private Task SignOutAsync() => CrashGuard.RunAsync("خروج از حساب", async () =>
    {
        await Cloud.SignOutAsync();
        ShowAccount();
        StationLine = "";
    });

    // ── ۲) پروفایل: نشانیِ سرور از حساب می‌آید ──────────────────────────

    /// <summary>
    /// نشانیِ سرورِ خانگی و رمزِ خواندن را از حساب می‌گیرد و می‌نشاند.
    ///
    /// ⚠️ همین جای آن دو کادری را گرفت که از تنظیمات برداشته شدند.
    /// </summary>
    [RelayCommand]
    private Task PullHomeAsync() => CrashGuard.RunAsync("گرفتنِ نشانی از حساب", async () =>
    {
        if (!SignedIn) { StationLine = ""; return; }

        Busy = true;
        try
        {
            var (ok, url, readKey, station, why) = await Cloud.HomeFromAccountAsync();
            if (!ok) { StationLine = "⚠️ " + why; return; }

            var s = _host.Settings;
            if (url.Length > 0) s.Set(SettingsService.ServerUrl, url);
            if (readKey.Length > 0) s.Set(SettingsService.SyncCode, readKey);

            StationLine = station.Length > 0
                ? $"⛽ پمپِ وصل‌شده: {station}"
                : "⚠️ هنوز پمپی به این حساب وصل نشده است.";
            AccountStatus = "";
        }
        finally { Busy = false; }
    });

    // ── ۳) اشتراک ───────────────────────────────────────────────────────

    [ObservableProperty] private string _subCode = "";
    [ObservableProperty] private string _subStatus = "";
    [ObservableProperty] private string _subMessage = "";
    [ObservableProperty] private bool _subActive;
    [ObservableProperty] private string _joinCode = "";

    private void ShowSubscription()
    {
        var file = AppSettings.Load();
        var check = LicenseGuard.Check(
            file.CloudLicense, file.CloudPublicKey,
            CloudConfig.DeviceUid(file), file.CloudStationId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        SubActive = check.Valid;
        VipDays = check.Valid && check.SubscriptionEndsAt > 0
            ? Math.Max(0, (int)((check.SubscriptionEndsAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                                / 86_400_000L))
            : 0;

        if (string.IsNullOrWhiteSpace(file.CloudDeviceToken))
        {
            SubStatus = "هنوز فعال نشده — کدِ شش‌رقمیِ اشتراک را بزنید.";
            UpdatePill();
            return;
        }
        if (check.Valid)
        {
            var plan = string.IsNullOrWhiteSpace(check.PlanTitle) ? "" : $" ({check.PlanTitle})";
            SubStatus = $"✅ اشتراکِ VIP فعال است{plan} — {VipDays} روز مانده.";
        }
        else
        {
            SubStatus = "⚠️ " + check.Reason;
        }
        ShowSubDetails(check, file);
        UpdatePill();
    }

    // ── ۴) کدِ پمپ — درِ اپِ گوشیِ کارمندان ─────────────────────────────
    //
    //  خواستهٔ صریحِ صاحب ریپو: «برای هر پمپ یک کد باشد که در اندروید و
    //  آیفون بزند، حساب‌های همان پمپ را نشان بدهد و با پمپ‌های دیگر قاطی
    //  نشود… هر کسی که برنامه را نصب می‌کند باید آن کد را بزند.»
    //
    //  کد را سرور می‌سازد و برای این پمپ ثابت است. این‌جا فقط نشان داده،
    //  کپی، کیو‌آر و — اگر لازم شد — عوض می‌شود.

    /// <summary>کدِ خام، هشت حرف — همان که در تنظیمات می‌ماند.</summary>
    [ObservableProperty] private string _accessCode = "";

    /// <summary>برای نمایش: ‎K7PM-3XQ2‎.</summary>
    [ObservableProperty] private string _accessCodeDisplay = "";

    /// <summary>یک خط دربارهٔ حالِ کد.</summary>
    [ObservableProperty] private string _accessStatus = "";

    public bool HasAccessCode => AccessCode.Length > 0;
    partial void OnAccessCodeChanged(string v)
    {
        AccessCodeDisplay = CloudLink.FormatAccessCode(v);
        OnPropertyChanged(nameof(HasAccessCode));
    }

    private void ShowAccessCode()
    {
        var file = AppSettings.Load();
        AccessCode = file.CloudAccessCode ?? "";
        AccessStatus = string.IsNullOrWhiteSpace(file.CloudDeviceToken)
            ? "کدِ پمپ بعد از فعال شدنِ اشتراک از سرور می‌آید."
            : HasAccessCode ? "" : "هنوز از سرور گرفته نشده — «گرفتنِ کد» را بزنید.";
    }

    [RelayCommand]
    private Task LoadAccessCodeAsync() => CrashGuard.RunAsync("کدِ پمپ", async () =>
    {
        Busy = true;
        AccessStatus = "در حالِ گرفتن از سرور…";
        try
        {
            var (ok, code, why) = await Cloud.AccessCodeAsync();
            if (ok) { AccessCode = code; AccessStatus = ""; }
            else AccessStatus = "❌ " + why;
        }
        finally { Busy = false; }
    });

    /// <summary>
    /// کدِ تازه — کدِ قبلی همان لحظه از کار می‌افتد. گوشی‌هایی که از قبل
    /// وصل‌اند سرِ کارند تا رمزِ خواندنِ سرورِ خانگی عوض نشود.
    /// </summary>
    [RelayCommand]
    private Task RotateAccessCodeAsync() => CrashGuard.RunAsync("عوض کردنِ کدِ پمپ", async () =>
    {
        if (!await Dialogs.ConfirmAsync("عوض کردنِ کدِ پمپ",
                "کدِ قبلی همان لحظه از کار می‌افتد و باید کدِ تازه را به کارمندان بدهید. مطمئنید؟",
                "عوض کن")) return;
        Busy = true;
        AccessStatus = "در حالِ ساختنِ کدِ تازه…";
        try
        {
            var (ok, code, why) = await Cloud.AccessCodeAsync(rotate: true);
            if (ok) { AccessCode = code; AccessStatus = "✅ کدِ تازه ساخته شد؛ کدِ قبلی دیگر کار نمی‌کند."; }
            else AccessStatus = "❌ " + why;
        }
        finally { Busy = false; }
    });

    [RelayCommand]
    private Task CopyAccessCodeAsync() => CrashGuard.RunAsync("کپیِ کدِ پمپ", async () =>
    {
        if (!HasAccessCode) return;
        AccessStatus = await Dialogs.CopyAsync(AccessCodeDisplay) ? "📋 کپی شد." : "";
    });

    /// <summary>کیو‌آرِ «با کدِ پمپ» — اسکنش اپ را باز می‌کند و کد را خودش می‌زند.</summary>
    [RelayCommand]
    private Task ShowAccessQrAsync() => CrashGuard.RunAsync("کیو‌آرِ کدِ پمپ", async () =>
    {
        if (!HasAccessCode) return;
        var link = KarLink.ForCode(AccessCode, _host.Settings.GetString(SettingsService.ViewerUrl));
        var png = await Task.Run(() => PumpYaqobi.Services.Vision.QrWriter.EncodePng(link));
        await Dialogs.ShowQrAsync("📲 کدِ پمپ — " + AccessCodeDisplay, link, png,
            "کارمند این را اسکن کند یا همین کد را در اپ بزند. فقط حساب‌های همین پمپ را می‌بیند "
            + "و بعدش رمزِ برنامه را هم می‌خواهد. هیچ رمزِ سروری در این کد نیست.");
    });

    [RelayCommand]
    private Task RedeemSubAsync() => CrashGuard.RunAsync("فعال‌سازیِ اشتراک", async () =>
    {
        var code = new string((SubCode ?? "").Where(char.IsDigit).ToArray());
        if (code.Length != 6) { SubMessage = "❌ کد باید شش رقم باشد."; return; }

        Busy = true;
        SubMessage = "در حالِ گرفتن از سرور…";
        try
        {
            var res = await Cloud.RedeemAsync(code);
            SubMessage = res.Ok ? "✅ اشتراک روی سرور ثبت شد." : "❌ " + res.Why;
            if (res.Ok) SubCode = "";
            ShowSubscription();
        }
        finally { Busy = false; }
    });

    [RelayCommand]
    private Task RefreshSubAsync() => CrashGuard.RunAsync("تازه‌سازیِ اشتراک", async () =>
    {
        Busy = true;
        SubMessage = "در حالِ پرسیدن از سرور…";
        try
        {
            var res = await Cloud.RefreshAsync();
            SubMessage = res.Ok ? "" : "❌ " + res.Why;
            ShowSubscription();
        }
        finally { Busy = false; }
    });

    [RelayCommand]
    private Task MakeJoinCodeAsync() => CrashGuard.RunAsync("کدِ پیوستن", async () =>
    {
        Busy = true;
        try
        {
            var (ok, code, why) = await Cloud.JoinCodeAsync();
            JoinCode = ok ? code : "";
            SubMessage = ok
                ? "کارمند این شش رقم را در اپِ گوشی بزند — تا ۲۴ ساعت کار می‌کند."
                : "❌ " + why;
        }
        finally { Busy = false; }
    });

    // ══ صفحهٔ پروفایل — به شکلِ «پروفایلِ بیمار»ِ مرجع ═══════════════════════
    //
    //  خواستهٔ صاحب ریپو با عکس: «پروفایلِ پمپ بنزین باید شبیهِ این عکس باشد، نه
    //  این که توی تنظیمات برود.» سه ستون: کارتِ آواتار (چپ)، مشخصاتِ پمپ (وسط)،
    //  اشتراک (راست)؛ زیرش تب‌ها با ردیف‌های رنگی، و ستونِ فایل‌ها (پشتیبان‌ها).
    //  ⚠️ هیچ کادرِ نشانی و رمزی این‌جا نیست — همان قاعدهٔ ‎CloudAddressLockTests‎.

    /// <summary>نام و نقشِ کسی که وارد شده — کارتِ آواتار.</summary>
    [ObservableProperty] private string _userLine = "";
    [ObservableProperty] private string _roleText = "";
    [ObservableProperty] private string _roleBrushKey = "Pump.Info";

    /// <summary>مشخصاتِ پمپ — از تنظیماتِ خودِ برنامه، فقط‌خواندنی این‌جا.</summary>
    [ObservableProperty] private string _pumpName = "";
    [ObservableProperty] private string _pumpPhone = "";
    [ObservableProperty] private string _pumpAddress = "";
    [ObservableProperty] private string _pumpCodeLine = "";
    [ObservableProperty] private string _homeLine = "";
    [ObservableProperty] private string _cloudLine = "";
    [ObservableProperty] private string _appVersionLine = "";

    /// <summary>اشتراک — به شکلِ کارتِ مرجع: چند سطرِ «برچسب: مقدار».</summary>
    [ObservableProperty] private string _subPlanText = "";
    [ObservableProperty] private string _subEndsText = "";
    [ObservableProperty] private string _subDaysText = "";
    [ObservableProperty] private string _subSourceText = "";

    /// <summary>ردیف‌های تبِ فعال (کارمندان / تاریخچه / پشتیبان‌ها).</summary>
    public ObservableCollection<ProfileRow> Rows { get; } = new();
    public ObservableCollection<ProfileRow> Backups { get; } = new();

    [ObservableProperty] private string _tab = "staff";
    public bool TabStaff => Tab == "staff";
    public bool TabHistory => Tab == "history";
    public bool TabBackups => Tab == "backups";
    partial void OnTabChanged(string v)
    {
        OnPropertyChanged(nameof(TabStaff)); OnPropertyChanged(nameof(TabHistory)); OnPropertyChanged(nameof(TabBackups));
        FillRows();
    }

    [RelayCommand] private void SetTab(string t) => Tab = t;

    private List<ProfileRow> _staffRows = new(), _historyRows = new(), _backupRows = new();

    private static readonly string[] Palette = { "Pump.Info", "Pump.Accent", "Pump.Ok", "Pump.Warn" };

    private void FillRows()
    {
        Rows.Clear();
        var src = Tab == "history" ? _historyRows : Tab == "backups" ? _backupRows : _staffRows;
        foreach (var r in src) Rows.Add(r);
    }

    private void ShowPump()
    {
        var s = _host.Settings;
        var f = AppSettings.Load();
        PumpName = s.GetString(SettingsService.StationName);
        if (string.IsNullOrWhiteSpace(PumpName)) PumpName = "پمپ یعقوبی";
        //  آواتار: تا وارد نشده، حرفِ اولِ نامِ پمپ — نه علامتِ سوال
        if (Initial == "؟") Initial = PumpName.Trim()[..1];
        PumpPhone = s.GetString(SettingsService.StationPhone);
        PumpAddress = s.GetString(SettingsService.StationAddress);
        PumpCodeLine = string.IsNullOrWhiteSpace(f.CloudStationId) ? "هنوز روی ابر ثبت نشده" : f.CloudStationId;
        HomeLine = string.IsNullOrWhiteSpace(s.GetString(SettingsService.ServerUrl))
            ? "هنوز پیدا نشده — با روشن شدنِ سرورِ خانگی خودش پیدا می‌شود"
            : "وصل و ثبت‌شده";
        CloudLine = string.IsNullOrWhiteSpace(f.CloudDeviceToken) ? "فعال نشده" : "فعال — با کدِ شش‌رقمی";
        AppVersionLine = PumpYaqobi.App.Update.AppVersion.Current;

        var session = _host.Session;
        UserLine = string.IsNullOrWhiteSpace(session.UserName) ? "کاربرِ برنامه" : session.UserName!;
        RoleText = session.Role switch
        {
            UserRole.Admin => "🛡️ مدیر",
            UserRole.Staff => "👤 کارمند",
            _ => "👁️ نظاره‌گر",
        };
        RoleBrushKey = session.Role switch
        {
            UserRole.Admin => "Pump.Ok",
            UserRole.Staff => "Pump.Info",
            _ => "Pump.Muted",
        };
    }

    private void ShowSubDetails(LicenseCheck check, AppSettings file)
    {
        var activated = !string.IsNullOrWhiteSpace(file.CloudDeviceToken);
        SubPlanText = check.Valid
            ? (string.IsNullOrWhiteSpace(check.PlanTitle) ? "اشتراکِ VIP" : check.PlanTitle)
            : activated ? "بدونِ اشتراکِ فعال" : "فعال نشده";
        SubEndsText = check.Valid && check.SubscriptionEndsAt > 0
            ? Shamsi.Of(DateTimeOffset.FromUnixTimeMilliseconds(check.SubscriptionEndsAt).LocalDateTime)
            : "—";
        SubDaysText = check.Valid ? $"{Shamsi.Money(VipDays)} روز" : "—";
        SubSourceText = check.Valid ? "مجوزِ امضاشدهٔ سرور" : activated ? check.Reason : "کدِ شش‌رقمی را بزنید";
    }

    /// <summary>ردیف‌های تب‌ها — کارمندان، تاریخچهٔ بخش‌ها، پشتیبان‌ها.</summary>
    private async Task LoadRowsAsync()
    {
        try
        {
            var staff = await _host.Attendance.StaffAsync();
            _staffRows = staff.Select((m, i) => new ProfileRow(
                m.Name ?? "", "شیفت", (m.ShiftIn ?? "—") + " تا " + (m.ShiftOut ?? "—"),
                "معاش", Shamsi.Money(m.Salary), string.IsNullOrWhiteSpace(m.Note) ? "" : m.Note!,
                Palette[i % Palette.Length])).ToList();
        }
        catch { _staffRows = new(); }

        try
        {
            var kinds = await _host.History.CardsAsync();
            _historyRows = kinds.Where(k => k.Count > 0).Select((k, i) => new ProfileRow(
                k.Label, "ردیف", Shamsi.Money(k.Count), "آخرین", string.IsNullOrWhiteSpace(k.LatestDate) ? "—" : k.LatestDate, "",
                Palette[i % Palette.Length])).ToList();
        }
        catch { _historyRows = new(); }

        try
        {
            var files = _host.Backup.List();
            _backupRows = files.Take(30).Select((b, i) => new ProfileRow(
                "پشتیبانِ " + b.Day, "حجم", b.SizeText, "ساعت", b.TakenAt.ToString("HH:mm"), b.Path,
                Palette[i % Palette.Length])).ToList();
            Backups.Clear();
            foreach (var r in _backupRows.Take(6)) Backups.Add(r);
        }
        catch { _backupRows = new(); }

        FillRows();
    }

    public override async Task OnActivatedAsync()
    {
        RefreshAll();
        await LoadRowsAsync();
    }

    /// <summary>پوشهٔ پشتیبان‌ها را با فایل‌منیجرِ سیستم باز می‌کند.</summary>
    [RelayCommand]
    private void OpenBackups()
    {
        try
        {
            var dir = _host.Backup.SnapshotDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch { }
    }
}

/// <summary>یک ردیفِ رنگیِ تب‌های پروفایل — همان «ویزیت»های مرجع: عنوان و سه ستونِ برچسب/مقدار.</summary>
public sealed record ProfileRow(string Title, string L1, string V1, string L2, string V2, string Note, string ColorKey)
{
    public bool HasNote => Note.Length > 0;
}
