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
///   ۱) حساب           — ایمیل و رمزِ خودمان، بی هیچ سرویسِ بیرونی
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

    /// <summary>
    /// ⚠️ **همان شیئی ذخیره می‌شود که <see cref="CloudLink"/> عوضش می‌کند.**
    ///
    /// پیش از این، پاسخِ ذخیره یک `AppSettings.Load()`ِ **تازه** بود:
    /// `CloudLink` توکنِ حساب و توکنِ دستگاه و مجوز را روی شیءِ خودش
    /// می‌نشاند و بعد یک شیءِ تازه از دیسک خوانده و **همان کهنه** دوباره
    /// نوشته می‌شد — یعنی ثبت‌نام و فعال‌سازی روی دیسک هیچ‌وقت نمی‌نشست و
    /// با هر بار باز شدنِ برنامه کاربر باید دوباره کدِ شش‌رقمی می‌زد.
    /// (سنجهٔ `cloudlogin` گرفتش؛ بقیهٔ جاهای برنامه —
    /// `StationPublisher` و چت — از اول درست بودند.)
    /// </summary>
    private CloudLink Cloud
    {
        get
        {
            if (_cloud is not null) return _cloud;
            var file = AppSettings.Load();
            return _cloud = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
        }
    }

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

        AccountStatus = SignedIn ? "" : "برای گرفتنِ اشتراک و وصل شدنِ خودکار، حساب بسازید یا وارد شوید.";
        UpdatePill();
    }

    //  ⛔ **دکمهٔ «ورود با گوگل» از این صفحه برداشته شد** — خواستهٔ صریحِ
    //  صاحب ریپو (۱۴۰۵/۰۶/۲۸): «هیچ پکنه‌ای نباشد، نه از گوگل و نه غیره؛
    //  هیچ‌کدامشان را نمی‌خواهم.» پس تنها راهِ حساب همان ایمیل و رمزِ خودمان
    //  است (`AccountStepAsync`) و «بعداً» برای بی‌اینترنت.
    //  ⚠️ `Services/GoogleSignIn.cs` و `CloudLink.SignInAsync` پاک نشدند:
    //  اپِ کارمندان (`kar/cloud.js`) همان راه را دارد و سرور همان مسیر را
    //  می‌شناسد. فقط این صفحه دیگر آن را نشان نمی‌دهد.

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

    // ══ 🪪 ثبت‌نام و ورود — دو گام، و بس ════════════════════════════════════
    //
    //  گزارشِ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «نه می‌گوید حساب داری یا نه، نه
    //  ثبت یا ساختنِ حساب دارد و نه رمز دارد و می‌خواهد — خیلی اشتباه درست
    //  شده. اول اسم، ایمیل، رمز، تکرارِ رمز؛ بعد برود بخشِ بعدی: کدِ شش‌رقمی
    //  و تاییدِ آن از سرور و اسمِ پمپ و لوکیشنِ پمپ. همین و بعد هم تمام.»
    //
    //      گامِ ۱  ساختنِ حساب یا ورود   نام · ایمیل · رمز · تکرارِ رمز
    //      گامِ ۲  پمپ                   کدِ شش‌رقمی (تاییدِ سرور) · نامِ پمپ · لوکیشن
    //      گامِ ۳  تمام
    //
    //  ⚠️ **رمز هیچ‌جا روی این کامپیوتر نمی‌نشیند** — نه خام، نه هش. فقط
    //  همان یک بار به ابر می‌رود و توکنِ نشست برمی‌گردد
    //  (<see cref="CloudLink.RegisterAsync"/>). پس از هر گام پاک می‌شود.
    //
    //  ⚠️ **«حساب دارم» و «حساب می‌سازم» دو راهِ جدا هستند** و کاربر باید
    //  ببیند روی کدام است — همان چیزی که نبود.
    //
    //  ⚠️ گام‌ها از حالِ واقعیِ برنامه حساب می‌شوند، نه از حافظهٔ صفحه: وارد
    //  شده؟ فعال شده؟ نامِ پمپ دارد؟ (<see cref="ShowLogin"/>) پس با بسته و
    //  باز شدنِ برنامه همان‌جایی است که باید باشد.
    //
    //  ⚠️ **بی‌اینترنت هم راه بسته نمی‌شود**: «بعداً» گامِ حساب را رد می‌کند و
    //  نام و ایمیل را همان‌جا ذخیره می‌کند. دفترِ کاربر هیچ‌وقت گروگان نیست.
    //
    //  ⚠️ **عکسِ مرجع خودش در صفحه است** — «همین مدل باشد و این آدمک‌ها هم
    //  باشند؛ اصلاً همین عکس باید باشد.» فقط آدمک‌ها بریده می‌شوند
    //  (<see cref="ArtCrop"/>)، چون نوشته‌ها و دو نشانِ فروشگاهِ عکس نباید
    //  دیده شوند.
    //
    //  ⛔ **هیچ دکمهٔ گوگل یا هر سرویسِ دیگری نیست**: «هیچ‌کدامشان را
    //  نمی‌خواهم.» تنها راهِ حساب همان ایمیل و رمزِ خودمان است، و «بعداً» که
    //  بی‌اینترنت هم کار کند.

    /// <summary>
    /// گامِ جاری: ۱ حساب · ۲ کدِ ایمیل · ۳ پمپ · ۴ تمام.
    ///
    /// ⚠️ گامِ «کدِ ایمیل» از ۱۴۰۵/۰۶/۲۹ اضافه شد: سرور حسابِ بی تأییدِ
    /// ایمیل نمی‌سازد (`verification_required`) و راهِ درستش سه‌پله است.
    /// «حساب دارم» این گام را ندارد و از ۱ به ۳ می‌رود.
    /// </summary>
    [ObservableProperty] private int _loginStep = 1;

    /// <summary>روی «حساب می‌سازم» هستیم یا «حساب دارم».</summary>
    [ObservableProperty] private bool _isSignUp = true;

    [ObservableProperty] private string _loginName = "";
    [ObservableProperty] private string _loginEmail = "";
    [ObservableProperty] private string _loginPassword = "";
    [ObservableProperty] private string _loginPassword2 = "";
    [ObservableProperty] private string _loginCode = "";

    /// <summary>کدِ شش‌رقمی که به **ایمیل** آمد — با کدِ اشتراک یکی نیست.</summary>
    [ObservableProperty] private string _emailCode = "";

    /// <summary>شرایط و ضوابط را پذیرفته‌ام — خودِ سرور اجباری‌اش کرده.</summary>
    [ObservableProperty] private bool _acceptTerms;

    /// <summary>متنِ شرایط، وقتی کاربر خواست ببیندش.</summary>
    [ObservableProperty] private string _termsText = "";

    [ObservableProperty] private bool _showTerms;
    [ObservableProperty] private string _loginPump = "";
    [ObservableProperty] private string _loginLocation = "";
    [ObservableProperty] private string _loginStatus = "";

    public bool IsSignIn => !IsSignUp;

    /// <summary>
    /// ⚠️ **کسی که حساب ندارد، تمامِ صفحهٔ پروفایل برایش همین صفحهٔ ورود
    /// است** — خواستهٔ صریحِ صاحب ریپو: «وقتی پروفایل را کلیک می‌کنم این
    /// صفحهٔ لاگین اولویت باشد و تمامِ صفحه همین را نشان بدهد برای کسانی که
    /// حساب ندارند؛ و برای کسانی که دارند، پروفایل همان مشخصات را نشان
    /// بدهد.» گامِ سه یعنی «تمام» ⇒ از آن پس خودِ پروفایل دیده می‌شود.
    /// </summary>
    public bool ShowLoginPage => LoginStep < 4;

    /// <summary>وارونهٔ بالا — خودِ پروفایل.</summary>
    public bool ShowProfilePage => !ShowLoginPage;

    public bool StepAccount => LoginStep == 1;
    public bool StepEmailCode => LoginStep == 2;
    public bool StepPump => LoginStep == 3;
    public bool StepDone => LoginStep == 4;

    /// <summary>نوشتهٔ دکمهٔ گامِ اول — با راهِ انتخاب‌شده عوض می‌شود.</summary>
    public string AccountButtonText => IsSignUp ? "ساختنِ حساب و ادامه" : "ورود و ادامه";

    partial void OnIsSignUpChanged(bool v)
    {
        OnPropertyChanged(nameof(IsSignIn));
        OnPropertyChanged(nameof(AccountButtonText));
        LoginStatus = "";
    }

    partial void OnLoginStepChanged(int v)
    {
        OnPropertyChanged(nameof(StepAccount));
        OnPropertyChanged(nameof(StepEmailCode));
        OnPropertyChanged(nameof(StepPump));
        OnPropertyChanged(nameof(StepDone));
        OnPropertyChanged(nameof(ShowLoginPage));
        OnPropertyChanged(nameof(ShowProfilePage));

        // ⚠️ «فقط همین را نشان بده، نه بخش‌ها باشند نه غیره» — سربرگ، نوارِ
        // جمله‌ها و نوارِ بخش‌های خودِ پنجره با همین یک نشان پنهان می‌شوند
        // (`MainViewModel.IsChromeVisible`). همان راهی که صفحهٔ حسابِ قرض‌دار
        // و صفحهٔ شرکت از پارسال می‌روند — قاعدهٔ تازه‌ای ساخته نشد.
        IsPageOpen = ShowLoginPage;
    }

    [RelayCommand]
    private void SetSignUp(string? yes) => IsSignUp = yes != "no";

    /// <summary>
    /// گام‌ها را از حالِ واقعیِ برنامه می‌چیند و کادرها را از تنظیمات پر
    /// می‌کند. رمز هیچ‌وقت پر نمی‌شود، چون هیچ‌جا ذخیره نشده.
    /// </summary>
    private void ShowLogin()
    {
        var f = AppSettings.Load();
        if (LoginEmail.Length == 0) LoginEmail = f.CloudEmail;
        if (LoginName.Length == 0) LoginName = f.CloudName;
        if (LoginPump.Length == 0) LoginPump = _host.Settings.GetString(SettingsService.StationName);
        if (LoginLocation.Length == 0) LoginLocation = _host.Settings.GetString(SettingsService.StationAddress);

        var activated = !string.IsNullOrWhiteSpace(f.CloudDeviceToken);
        var hasPump = !string.IsNullOrWhiteSpace(_host.Settings.GetString(SettingsService.StationName));

        //  ⚠️ «تمام» یعنی یا واقعاً همه‌چیز هست، یا کاربر خودش گفته «بعداً»
        //  (`AppSettings.LoginSkipped`) — وگرنه صفحهٔ ورود می‌شد یک دیوار
        //  جلوی دفترِ خودش، و آن خلافِ قاعدهٔ «دفتر گروگان نیست» بود.
        LoginStep = SignedIn && activated && hasPump ? 4
                  : f.LoginSkipped ? 4
                  : !SignedIn ? 1 : 3;
        //  کسی که حساب دارد، پیش‌فرضش «ورود» است نه «ثبت‌نام»
        if (SignedIn || f.CloudEmail.Length > 0) IsSignUp = !SignedIn && f.CloudEmail.Length == 0;

        //  ⚠️ صریح، نه فقط از راهِ `OnLoginStepChanged`: گامِ یک مقدارِ
        //  پیش‌فرضِ خودِ فیلد است، پس وقتی صفحهٔ ورود همان گامِ یک بماند هیچ
        //  خبری نمی‌آید و پوستهٔ پنجره پنهان نمی‌شد.
        IsPageOpen = ShowLoginPage;
    }

    // ── گامِ ۱: حساب ────────────────────────────────────────────────────

    /// <summary>
    /// «ساختنِ حساب» یا «ورود» — و بعد می‌رود گامِ دو.
    ///
    /// ⚠️ رمز پس از رفتن پاک می‌شود و هیچ‌وقت ذخیره نمی‌شود.
    /// </summary>
    [RelayCommand]
    private Task AccountStepAsync() => CrashGuard.RunAsync("حسابِ ابر", async () =>
    {
        var name = (LoginName ?? "").Trim();
        var email = (LoginEmail ?? "").Trim();
        var pass = LoginPassword ?? "";
        var pass2 = LoginPassword2 ?? "";

        if (email.Length == 0 || !email.Contains('@') || email.EndsWith("@"))
        { LoginStatus = "❌ ایمیل درست نیست."; return; }
        if (IsSignUp && name.Length < 2) { LoginStatus = "❌ نامتان را بنویسید."; return; }
        //  ⚠️ هشت نویسه، همان قاعدهٔ خودِ سرور (`password.checkStrength`) —
        //  وگرنه کاربر رمزِ شش‌نویسه‌ای می‌زد و سرور ردش می‌کرد.
        if (pass.Length < 8) { LoginStatus = "❌ رمز دستِ‌کم هشت نویسه باشد."; return; }
        if (IsSignUp && pass != pass2) { LoginStatus = "❌ دو رمز یکی نیستند."; return; }
        if (IsSignUp && !AcceptTerms) { LoginStatus = "❌ شرایط و ضوابط را بپذیرید."; return; }

        Busy = true;
        LoginStatus = IsSignUp ? "در حالِ فرستادنِ کد به ایمیل…" : "در حالِ ورود…";
        try
        {
            if (IsSignUp)
            {
                //  پلهٔ یک: کد به ایمیل می‌رود و حسابی ساخته **نمی‌شود**.
                var start = await Cloud.RegisterStartAsync(name, email, pass);
                if (!start.Ok) { LoginStatus = "❌ " + start.Why; return; }

                //  ⚠️ رمز این‌جا پاک نمی‌شود: پلهٔ سوم خودش رمز را می‌خواهد.
                //  فقط روی دیسک نمی‌نشیند — همان قاعدهٔ همیشه.
                EmailCode = "";
                LoginStatus = "✅ کدِ شش‌رقمی به " + email + " فرستاده شد.";
                RefreshAll();
                LoginStep = 2;
                return;
            }

            var res = await Cloud.SignInWithPasswordAsync(email, pass);
            if (!res.Ok) { LoginStatus = "❌ " + res.Why; return; }

            //  ⚠️ رمز از حافظهٔ صفحه هم می‌رود
            LoginPassword = ""; LoginPassword2 = "";
            ClearSkipped();
            LoginStatus = "";
            RefreshAll();
            LoginStep = 3;
        }
        finally { Busy = false; }
    });

    // ── گامِ ۲: کدِ ایمیل ────────────────────────────────────────────────

    /// <summary>
    /// کدی که به ایمیل آمد ⇒ بلیتِ ثبت‌نام ⇒ حساب و نشست.
    ///
    /// ⚠️ دو پلهٔ سرور (`verify` و `complete`) این‌جا یکی دیده می‌شوند، چون
    /// از دیدِ کاربر یک کار است: «کد را زدم، حسابم ساخته شد».
    /// </summary>
    [RelayCommand]
    private Task VerifyEmailAsync() => CrashGuard.RunAsync("تاییدِ کدِ ایمیل", async () =>
    {
        var code = new string((EmailCode ?? "").Where(char.IsDigit).ToArray());
        var pass = LoginPassword ?? "";
        if (code.Length != 6) { LoginStatus = "❌ کدِ ایمیل باید شش رقم باشد."; return; }
        if (pass.Length < 8) { LoginStatus = "❌ رمز گم شد — از گامِ حساب دوباره شروع کنید."; return; }

        Busy = true;
        LoginStatus = "در حالِ تاییدِ کد…";
        try
        {
            var ver = await Cloud.RegisterVerifyAsync((LoginEmail ?? "").Trim(), code);
            if (!ver.Ok) { LoginStatus = "❌ " + ver.Why; return; }

            var done = await Cloud.RegisterCompleteAsync((LoginName ?? "").Trim(), pass, AcceptTerms);
            if (!done.Ok) { LoginStatus = "❌ " + done.Why; return; }

            //  ⚠️ حساب ساخته شد؛ از این‌جا به بعد رمز هیچ‌جا لازم نیست
            LoginPassword = ""; LoginPassword2 = ""; EmailCode = "";
            ClearSkipped();
            LoginStatus = "";
            RefreshAll();
            LoginStep = 3;
        }
        finally { Busy = false; }
    });

    /// <summary>کد نرسید — دوباره بفرست (همان پلهٔ یک).</summary>
    [RelayCommand]
    private Task ResendEmailCodeAsync() => CrashGuard.RunAsync("فرستادنِ دوبارهٔ کد", async () =>
    {
        var email = (LoginEmail ?? "").Trim();
        var pass = LoginPassword ?? "";
        if (pass.Length < 8) { LoginStatus = "❌ از گامِ حساب دوباره شروع کنید."; return; }

        Busy = true;
        try
        {
            var again = await Cloud.RegisterStartAsync((LoginName ?? "").Trim(), email, pass);
            LoginStatus = again.Ok ? "✅ کد دوباره فرستاده شد." : "❌ " + again.Why;
        }
        finally { Busy = false; }
    });

    /// <summary>متنِ شرایط و ضوابط را از سرور بگیر و نشان بده.</summary>
    [RelayCommand]
    private Task LoadTermsAsync() => CrashGuard.RunAsync("شرایط و ضوابط", async () =>
    {
        ShowTerms = !ShowTerms;
        if (!ShowTerms || TermsText.Length > 0) return;
        var (ok, text, why) = await Cloud.TermsAsync();
        TermsText = ok ? text : "متنِ شرایط از سرور نیامد — " + why;
    });

    /// <summary>نشانِ «بعداً» را برمی‌دارد، چون حالا حسابِ واقعی هست.</summary>
    private static void ClearSkipped()
    {
        var f = AppSettings.Load();
        if (!f.LoginSkipped) return;
        f.LoginSkipped = false;
        f.Save();
    }

    /// <summary>
    /// «بعداً» — بی‌اینترنت یا بی حساب هم باید بتوان ادامه داد. نام و ایمیل
    /// همان‌جا ذخیره می‌شوند؛ رمز نه (چون حسابی ساخته نشده).
    /// </summary>
    [RelayCommand]
    private void SkipAccount()
    {
        var f = AppSettings.Load();
        var email = (LoginEmail ?? "").Trim();
        var name = (LoginName ?? "").Trim();
        if (email.Length > 0 && email.Contains('@')) f.CloudEmail = email;
        if (name.Length > 0) f.CloudName = name;
        f.Save();

        LoginPassword = ""; LoginPassword2 = "";
        LoginStatus = "نام و ایمیل ذخیره شد — حساب روی سرور بعداً ساخته می‌شود.";
        RefreshAll();
        LoginStep = 3;
    }

    // ── گامِ ۲: پمپ ─────────────────────────────────────────────────────

    /// <summary>
    /// کدِ شش‌رقمی را **از سرور** تایید می‌کند و نامِ پمپ و لوکیشن را
    /// می‌نشاند؛ بعد «تمام».
    ///
    /// ⚠️ نامِ پمپ و لوکیشن **پیش از** فرستادنِ کد ذخیره می‌شوند و همراهِ
    /// همان درخواست به ابر هم می‌روند، تا ابر همان پمپ را با همان نام و
    /// لوکیشن بشناسد.
    /// </summary>
    [RelayCommand]
    private Task VerifyCodeAsync() => CrashGuard.RunAsync("تاییدِ کدِ پمپ", async () =>
    {
        var pump = (LoginPump ?? "").Trim();
        var where = (LoginLocation ?? "").Trim();
        var code = new string((LoginCode ?? "").Where(char.IsDigit).ToArray());

        if (pump.Length < 2) { LoginStatus = "❌ نامِ پمپ را بنویسید."; return; }
        if (code.Length != 6) { LoginStatus = "❌ کد باید شش رقم باشد."; return; }

        Busy = true;
        LoginStatus = "در حالِ تایید از سرور…";
        try
        {
            _host.Settings.Set(SettingsService.StationName, pump);
            if (where.Length > 0) _host.Settings.Set(SettingsService.StationAddress, where);
            //  ⚠️ **همین‌جا** تازه می‌شود، نه فقط سرِ موفقیت: نام و لوکیشن از
            //  همین لحظه ذخیره شده‌اند، پس اگر سرور جواب نداد هم باید همان
            //  نامِ تازه در پروفایل و سربرگ دیده شود. (سنجهٔ ۱۴ گرفتش: نام
            //  ذخیره شده بود ولی صفحه نامِ قبلی را نشان می‌داد.)
            RefreshAll();

            var res = await Cloud.RedeemAsync(code, pump, where);
            if (!res.Ok) { LoginStatus = "❌ " + res.Why; return; }

            LoginCode = "";
            LoginStatus = "";
            RefreshAll();
            LoginStep = 4;
        }
        finally { Busy = false; }
    });

    /// <summary>
    /// «بعداً» روی گامِ پمپ: نام و لوکیشن ذخیره می‌شوند و صفحه رد می‌شود.
    /// بی این، پمپی که اینترنت ندارد تا ابد روی همین صفحه می‌ماند.
    /// </summary>
    [RelayCommand]
    private void SkipPump()
    {
        var pump = (LoginPump ?? "").Trim();
        var where = (LoginLocation ?? "").Trim();
        if (pump.Length > 0) _host.Settings.Set(SettingsService.StationName, pump);
        if (where.Length > 0) _host.Settings.Set(SettingsService.StationAddress, where);

        var f = AppSettings.Load();
        f.LoginSkipped = true;
        f.Save();

        LoginStatus = "";
        RefreshAll();
        LoginStep = 4;
    }

    /// <summary>از خودِ پروفایل برگرد به صفحهٔ ورود (عوض کردنِ حساب یا پمپ).</summary>
    [RelayCommand]
    private void OpenAccountPage() { LoginStatus = ""; LoginStep = 1; }

    /// <summary>
    /// «برگشت» — کسی که نمی‌خواهد حساب بسازد برمی‌گردد سرِ دفترِ خودش.
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «یک برگشت هم داشته باشد که پس
    /// برود اگر کسی نخواست ثبت‌نام کند.»
    ///
    /// ⚠️ نشانِ «بعداً» هم گذاشته می‌شود، وگرنه دفعهٔ بعد همین صفحه دوباره
    /// جلویش سبز می‌شد و «برگشت» معنی نداشت.
    /// </summary>
    [RelayCommand]
    private Task CloseLoginAsync() => CrashGuard.RunAsync("برگشت از صفحهٔ ورود", async () =>
    {
        var f = AppSettings.Load();
        if (!f.LoginSkipped) { f.LoginSkipped = true; f.Save(); }
        LoginStatus = "";
        RefreshAll();
        LoginStep = 4;
        if (_host.GoHome is { } home) await home();
    });

    /// <summary>برگشت به گامِ حساب — برای عوض کردنِ حساب یا رمز.</summary>
    [RelayCommand]
    private void BackToAccount() { LoginStatus = ""; LoginStep = 1; }

    /// <summary>از گامِ «تمام» به گامِ پمپ — برای عوض کردنِ نام یا لوکیشن.</summary>
    [RelayCommand]
    private void BackToPump() { LoginStatus = ""; LoginStep = 3; }

    // ── عکسِ کنارِ فرم — همان آدمک‌های عکسِ مرجع ─────────────────────────
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «همین مدل باشد و این آدمک‌ها هم
    //  باشند؛ اصلاً همین عکس باید باشد.» پس عکسِ خودش کنارِ فرم می‌نشیند.
    //
    //  ⚠️ و همان سه قاعدهٔ «یک درصدِ ثانیه هم اضافه نکند» سرِ جایش است:
    //  فقط در فعال‌سازیِ همین صفحه، روی نخِ دیگر، به پهنای نمایش، و یک
    //  بار برای همیشه (`_art`). سنجه‌اش `startup` و `idle` است.

    /// <summary>عکسِ کنارِ فرم — تا خوانده نشده ‎null‎ است و کادرش دیده نمی‌شود.</summary>
    [ObservableProperty] private IImage? _loginArt;

    /// <summary>پهنای بازکردنِ عکس — خودِ فایل ۷۳۶ پیکسل است.</summary>
    private const int ArtWidth = 736;

    /// <summary>
    /// ⚠️ **فقط خودِ آدمک‌ها** — نه نوشته‌های انگلیسیِ عکس و نه آن دو نشانِ
    /// «App Store / Google Play»: خواستهٔ صریحِ صاحب ریپو «هیچ پکنه‌ای نباشد،
    /// نه از گوگل و نه غیره» با این پنجره هم برقرار می‌ماند. فرمِ واقعی خودِ
    /// برنامه است، نه فرمِ داخلِ عکس.
    /// بریدن با <see cref="CroppedBitmap"/> است — یک پوشش روی همان عکس، بی
    /// کپی و بی هزینه.
    /// </summary>
    private static readonly PixelRect ArtCrop = new(52, 86, 368, 396);

    private static IImage? _art;
    private static bool _artTried;

    /// <summary>
    /// عکسِ صفحهٔ ورود — روی نخِ دیگر، به پهنای نمایش، یک بار.
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
            //  invalid thread»). یک بار همین باگ عکس را کاملاً ناپدید کرد و
            //  ‎catch { }‎ هم صدایش را خورد.
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

        //  ⚠️ عکس **پیش از** ردیف‌های تب‌ها خوانده می‌شود — خواستهٔ صاحب ریپو
        //  «آن عکسِ آدم‌ها را فوری کن». سنجیده شد (`loginart`): خودِ عکس ۴
        //  میلی‌ثانیه است و هیچ دستورِ دیتابیسی نمی‌زند، ولی `LoadRowsAsync`
        //  سه پرس‌وجو دارد (کارمندان · تاریخچه · پشتیبان‌ها) و عکس پشتِ آن‌ها
        //  ~۱۱۰ میلی‌ثانیه دیر می‌آمد. با همین جابه‌جایی ۱۱۰ ⇒ ۱۰ شد.
        //  ⚠️ و عکس فقط همین‌جا خوانده می‌شود — پردهٔ لودینگ فقط
        //  `EnsureLoadedAsync` را می‌زند، پس در مسیرِ باز شدنِ برنامه نیست.
        await LoadArtAsync();
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
