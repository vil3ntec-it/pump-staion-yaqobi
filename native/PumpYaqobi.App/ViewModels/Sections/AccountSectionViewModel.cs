using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public AccountSectionViewModel(AppHost host) : base("account", "settings", "حسابِ من")
    {
        _host = host;
        ShowAccount();
        ShowSubscription();
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

        if (string.IsNullOrWhiteSpace(file.CloudDeviceToken))
        {
            SubStatus = "هنوز فعال نشده — کدِ شش‌رقمیِ اشتراک را بزنید.";
            return;
        }
        if (check.Valid)
        {
            var left = check.SubscriptionEndsAt > 0
                ? Math.Max(0, (int)((check.SubscriptionEndsAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                                    / 86_400_000L))
                : 0;
            var plan = string.IsNullOrWhiteSpace(check.PlanTitle) ? "" : $" ({check.PlanTitle})";
            SubStatus = $"✅ اشتراک فعال است{plan} — {left} روز مانده.";
        }
        else
        {
            SubStatus = "⚠️ " + check.Reason;
        }
    }

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
}
