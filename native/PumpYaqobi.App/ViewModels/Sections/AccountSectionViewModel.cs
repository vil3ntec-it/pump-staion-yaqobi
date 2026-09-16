using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

        // ⚠️ **پیش از** هر بازگشتِ زودهنگام: تا دیروز روی پمپی که هنوز فعال
        // نشده بود (یعنی همان چیزی که صاحب ریپو می‌دید) این چهار خانه خالی
        // می‌ماندند و کارتِ اشتراک «خراب» به نظر می‌رسید.
        ShowSubDetails(check, file);

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
        if (!Entitlements.Gate(_host, Entitlements.Kar)) return;
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
        // ⚠️ خانهٔ خالی «خراب» به نظر می‌رسد — نداشتن با «—» گفته می‌شود، با
        // هیچ نه. (گزارشِ صاحب ریپو: «بخشِ پروفایل هنوز درست نشده برایم».)
        PumpPhone = Dash(s.GetString(SettingsService.StationPhone));
        PumpAddress = Dash(s.GetString(SettingsService.StationAddress));
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

    /// <summary>متنِ خالی را «—» می‌کند — هیچ خانه‌ای در پروفایل خالی نمی‌ماند.</summary>
    private static string Dash(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v.Trim();

    // ══ «چه چیزی در اشتراکِ من است» ═══════════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): کیو‌آر، اپِ کارمندان و ربات، و
    //  بک‌اپِ خودکار روی سرور با اشتراک باشند — و پشتیبانی همیشه باز، «چون
    //  یکی از واجبات است». پس همین‌جا، سیاه روی سفید، نوشته می‌شود کدام باز
    //  است و کدام نه. کاربری که نداند چه خریده، فردا شکایت می‌کند.

    [ObservableProperty] private string _accessKarText = "";
    [ObservableProperty] private string _accessQrText = "";
    [ObservableProperty] private string _accessBackupText = "";
    [ObservableProperty] private string _accessSupportText = "";

    /// <summary>«⏳ ۹ روز ارفاق» — خالی یعنی ارفاقی در کار نیست.</summary>
    [ObservableProperty] private string _graceText = "";

    private void ShowAccess(AppSettings file)
    {
        var st = Entitlements.State(file, _cloud?.Subscription);
        static string Mark(bool ok) => ok ? "✅ باز" : "🔒 با اشتراک";

        AccessKarText = Mark(st.Allows(Entitlements.Kar));
        AccessQrText = Mark(st.Allows(Entitlements.QrLive));
        AccessBackupText = Mark(st.Allows(Entitlements.CloudBackup));

        //  ⚠️ پشتیبانی هیچ‌وقت قفل نمی‌شود و این‌جا هم همین را می‌گوید.
        AccessSupportText = "✅ همیشه باز";

        GraceText = st.InGrace
            ? "⏳ اشتراک تمام شده — " + Shamsi.Money(st.GraceDaysLeft)
              + " روز ارفاق. در این مدت همه‌چیز کار می‌کند."
            : "";
    }

    private void ShowSubDetails(LicenseCheck check, AppSettings file)
    {
        ShowAccess(file);
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

    // ══ 🪪 کارتِ ورود — «ایمیل، کدِ شش‌رقمی، نام، نامِ پمپ» ══════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸) با دو عکس: «پروفایل یک بخش بگذار،
    //  بخشِ لاگین با ایمیل با اضافه کردنِ کدِ شش‌رقمی و زدنِ اسم و اسمِ پمپش…
    //  عکسِ آن اولی باشد، دیزاینِ این دومی.»
    //
    //  پس کارتی است دوستونه مثلِ همان طرح: یک طرف عکس، طرفِ دیگر فرم. و همان
    //  کاری را می‌کند که تا امروز پراکنده بود: نام و نامِ پمپ و ایمیل در
    //  تنظیمات می‌نشینند و کدِ شش‌رقمی همان فعال‌سازیِ اشتراک است.
    //
    //  ⚠️ **رمزی این‌جا تایپ نمی‌شود** — نه رمزِ گوگل، نه رمزِ سرور. ورود با
    //  گوگل همان مرورگرِ سیستم است (‎GoogleSignIn‎) و کدِ شش‌رقمی از خودِ
    //  فروشنده می‌آید.
    //
    //  ⚠️ **عکس یک درصدِ ثانیه هم به برنامه اضافه نمی‌کند** — خواستهٔ صریحِ
    //  صاحب ریپو. سه قاعده: (۱) فقط در ‎OnActivatedAsync‎ خوانده می‌شود، یعنی
    //  وقتی کاربر واقعاً روی این صفحه آمد — نه در سازنده و نه در
    //  ‎EnsureLoadedAsync‎ که پردهٔ لودینگ می‌زندشان؛ (۲) روی نخِ دیگر و با
    //  ‎DecodeToWidth‎ به پهنای نمایش باز می‌شود، نه با اندازهٔ اصلی؛ (۳) یک
    //  بار و برای همیشه (‎_art‎). سنجه‌اش ‎startup‎ و ‎idle‎ است.

    [ObservableProperty] private string _loginEmail = "";
    [ObservableProperty] private string _loginName = "";
    [ObservableProperty] private string _loginPump = "";
    [ObservableProperty] private string _loginCode = "";
    [ObservableProperty] private string _loginStatus = "";

    /// <summary>عکسِ کنارِ فرم — تا خوانده نشده ‎null‎ است و کادرش دیده نمی‌شود.</summary>
    [ObservableProperty] private IImage? _loginArt;

    /// <summary>
    /// پهنای بازکردنِ عکس. عکسِ اصلی ۷۳۵ پیکسل است و از آن فقط خودِ
    /// پمپ‌بنزین بریده می‌شود، پس بیشتر از اندازهٔ خودش بازش نمی‌کنیم.
    /// </summary>
    private const int ArtWidth = 736;

    /// <summary>
    /// ⚠️ **فقط خودِ پمپ‌بنزین** — خواستهٔ صریحِ صاحب ریپو: «آن نوشته‌های
    /// عکس هم نباشد.» عکسِ اصلی یک کارتِ تبلیغاتی است («TRANSPARENT GLASS /
    /// Gas Station / Learn More»)؛ این پنجره همان نوشته‌ها و حاشیه را
    /// می‌بُرد و فقط تصویرِ پمپ می‌ماند. بریدن روی خودِ عکسِ بازشده انجام
    /// می‌شود (<see cref="CroppedBitmap"/>) — یک پوشش است، نه یک عکسِ تازه،
    /// پس هیچ حافظه و وقتی اضافه نمی‌کند.
    /// </summary>
    private static readonly PixelRect ArtCrop = new(56, 42, 376, 412);

    private static IImage? _art;
    private static bool _artTried;

    private void ShowLogin()
    {
        var f = AppSettings.Load();
        if (LoginEmail.Length == 0) LoginEmail = f.CloudEmail;
        if (LoginName.Length == 0) LoginName = f.CloudName;
        if (LoginPump.Length == 0)
        {
            var name = _host.Settings.GetString(SettingsService.StationName);
            LoginPump = string.IsNullOrWhiteSpace(name) ? "" : name;
        }
    }

    /// <summary>
    /// «ثبت و فعال‌سازی»: نام و نامِ پمپ و ایمیل می‌نشینند و — اگر کدِ
    /// شش‌رقمی داده شده باشد — همان لحظه اشتراک هم فعال می‌شود.
    ///
    /// ⚠️ نامِ پمپ و نام بی کد هم ذخیره می‌شوند: کسی که هنوز کد نخریده باید
    /// بتواند نامش را بنویسد.
    /// </summary>
    [RelayCommand]
    private Task SubmitLoginAsync() => CrashGuard.RunAsync("ثبتِ حساب", async () =>
    {
        Busy = true;
        try
        {
            var f = AppSettings.Load();
            var email = (LoginEmail ?? "").Trim();
            var name = (LoginName ?? "").Trim();
            var pump = (LoginPump ?? "").Trim();

            if (email.Length > 0 && !email.Contains('@'))
            { LoginStatus = "❌ ایمیل درست نیست."; return; }

            if (email.Length > 0) f.CloudEmail = email;
            if (name.Length > 0) f.CloudName = name;
            f.Save();
            if (pump.Length > 0) _host.Settings.Set(SettingsService.StationName, pump);

            var code = new string((LoginCode ?? "").Where(char.IsDigit).ToArray());
            if (code.Length == 0)
            {
                LoginStatus = "✅ ذخیره شد. برای فعال شدنِ اشتراک، کدِ شش‌رقمی را هم بزنید.";
                RefreshAll();
                return;
            }
            if (code.Length != 6) { LoginStatus = "❌ کد باید شش رقم باشد."; return; }

            LoginStatus = "در حالِ فعال‌سازی روی سرور…";
            var res = await Cloud.RedeemAsync(code);
            LoginStatus = res.Ok ? "✅ اشتراک فعال شد." : "❌ " + res.Why;
            if (res.Ok) LoginCode = "";
            RefreshAll();
        }
        finally { Busy = false; }
    });

    /// <summary>
    /// عکسِ کارتِ ورود — روی نخِ دیگر، به پهنای نمایش، یک بار.
    /// نشدنش هیچ اهمیتی ندارد: صفحه بی عکس هم کامل است.
    /// </summary>
    private async Task LoadArtAsync()
    {
        if (LoginArt is not null) return;
        if (_art is not null) { LoginArt = _art; return; }
        if (_artTried) return;
        _artTried = true;

        try
        {
            //  کارِ سنگین (باز کردنِ JPEG) روی نخِ دیگر
            var full = await Task.Run(() =>
            {
                using var s = AssetLoader.Open(new Uri("avares://PumpYaqobi/Assets/login-art.jpg"));
                return Bitmap.DecodeToWidth(s, ArtWidth);
            });

            //  ⚠️ بریدن **باید** روی نخِ رابط باشد: ‎CroppedBitmap‎ یک
            //  ‎AvaloniaObject‎ است و سازنده‌اش نخ را می‌سنجد («Call from
            //  invalid thread»). خودش کاری نمی‌کند جز نگه داشتنِ یک مستطیل،
            //  پس هزینه‌اش صفر است — همان عکس، بی کپی.
            var box = ArtCrop.Intersect(new PixelRect(full.PixelSize));
            IImage art = box.Width > 0 && box.Height > 0 ? new CroppedBitmap(full, box) : full;

            _art = art;
            LoginArt = art;
        }
        catch { }
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
        ShowLogin();
        await LoadRowsAsync();
        //  ⚠️ عکس آخر از همه، و فقط همین‌جا — مسیرِ لودینگِ برنامه به آن
        //  دست نمی‌زند.
        await LoadArtAsync();
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
