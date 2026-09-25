using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
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
        //  ⚠️ «ارسالِ خودکار بعد از رقمِ ششم» — بندِ ۴ی پرامپت. خودِ خانه‌ها
        //  خبر می‌دهند که پر شدند؛ صفحه هیچ تصمیمی نمی‌گیرد.
        CodeBoxes.Completed += () =>
        {
            if (IsCodeLogin && !Busy && LoginStep == 2) VerifyEmailCommand.Execute(null);
        };
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
        //  ══ دفترِ همان حسابی که همین حالا وارد است ═══════════════════════
        //
        //  ⛔ این تنها قلابِ «حساب عوض شد» در پوستهٔ برنامه است، و عمداً
        //  همین‌جاست: هر مسیرِ ورود، ثبت‌نام، کدِ ایمیلی و خروج سرِ آخر به
        //  `RefreshAll` می‌رسد. قلابِ دوم یعنی روزی یکی عوض می‌شود و
        //  دیگری نه.
        //
        //  ⚠️ تا حساب عوض نشده باشد **صفر هزینه** دارد: فقط دو رشته
        //  مقایسه می‌شوند و برمی‌گردد — نه فایلی خوانده می‌شود، نه دستوری
        //  به دیتابیس می‌رود. تصمیم و خودِ جابه‌جایی مالِ
        //  `AppHost.UseLedgerOf` است، نه این‌جا.
        try { AppHost.Current.UseLedgerOf(AppSettings.Load().CloudUserId); }
        catch { /* دفترِ فعلی سرِ جایش است؛ صفحهٔ پروفایل نباید بشکند */ }

        ShowAccount();
        ShowSubscription();
        ShowAccessCode();
        ShowPump();
    }

    /// <summary>
    /// حالِ واقعیِ نشست — از <see cref="CloudLink.Reach"/> که فقط با جوابِ
    /// خودِ سرور پر می‌شود.
    ///
    /// ⚠️ «نرسیدیم» کاربر را از حسابش بیرون نمی‌اندازد و توکن را هم پاک
    /// نمی‌کند — فقط راستش را می‌گوید. بیرون انداختنِ کاربر با یک قطعیِ
    /// اینترنت، خودش باگِ بزرگ‌تری است.
    /// </summary>
    private static string CloudNote() => CloudLink.Reach switch
    {
        CloudReach.Online => CloudLink.CloudOkAt is { } at
            ? $"✅ سرورِ حساب تایید کرد · آخرین تماس: {at:HH:mm}"
            : "✅ سرورِ حساب تایید کرد",
        CloudReach.Offline => "⚠️ نشستِ شما روی این کامپیوتر هست، ولی به سرورِ حساب نمی‌رسیم"
            + (CloudLink.CloudOkAt is { } ok ? $" — آخرین تاییدِ واقعی: {ok:HH:mm}" : " و هنوز هیچ تاییدی نگرفته‌ایم"),
        _ => "⏳ هنوز با سرورِ حساب تماس نگرفته‌ایم — خودش تا یک دقیقهٔ دیگر می‌پرسد",
    };

    private void UpdatePill()
    {
        VipActive = SubActive;
        //  ⛔ دکمهٔ سربرگ **نامِ واقعیِ** اشتراک را می‌گوید: دورهٔ آزمایشیِ
        //  حسابِ تازه «VIP» نیست و پلنِ استاندارد هم نه — تا ۱۴۰۵/۰۷/۱۳ هر
        //  اشتراکِ بازی «VIP · N روز» خوانده می‌شد و صاحبِ پمپ نمی‌فهمید چه
        //  دارد (سنجهٔ `livestack` با سرورِ واقعی دیدش).
        PillText = !SubActive ? "پروفایل"
            : SubPermanent ? $"{SubKind} · دائمی"
            : $"{SubKind} · {VipDays} روز";
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

        //  ⛔ **«وارد شده‌اید» از روی فایلِ روی دیسک گفته نمی‌شود.**
        //  `SignedIn` فقط یعنی «توکنی روی این کامپیوتر هست» — نه این‌که ابر
        //  آن را پذیرفته باشد. تا دیروز همین‌جا یک رشتهٔ خالی می‌نشست و
        //  صفحه با اطمینان می‌گفت وارد شده‌اید، حتی اگر برنامه **هیچ‌وقت**
        //  با ابر حرف نزده بود. (خواستهٔ ۱۴۰۵/۰۷/۰۱: «هیچ کلکِ دروغی نباشد
        //  که بگوید وصل است.»)
        AccountStatus = SignedIn
            ? CloudNote()
            : "برای گرفتنِ اشتراک و وصل شدنِ خودکار، حساب بسازید یا وارد شوید.";
        UpdatePill();
    }

    //  ⛔ **دکمهٔ «ورود با گوگل» از این صفحه برداشته شد** — خواستهٔ صریحِ
    //  صاحب ریپو (۱۴۰۵/۰۶/۲۸): «هیچ پکنه‌ای نباشد، نه از گوگل و نه غیره؛
    //  هیچ‌کدامشان را نمی‌خواهم.» پس تنها راهِ حساب همان ایمیل و رمزِ خودمان
    //  است (`AccountStepAsync`) و «بعداً» برای بی‌اینترنت.
    //  ⚠️ `Services/GoogleSignIn.cs` و `CloudLink.SignInAsync` پاک نشدند:
    //  اپِ کارمندان (`kar/cloud.js`) همان راه را دارد و سرور همان مسیر را
    //  می‌شناسد. فقط این صفحه دیگر آن را نشان نمی‌دهد.

    /// <summary>
    /// خروج — دفترِ روی کامپیوتر دست نمی‌خورد.
    ///
    /// ⚠️ <see cref="CloudLink.SignOutAsync"/> از ۱۴۰۵/۰۶/۳۰ نشست را روی
    /// <b>سرور</b> هم باطل می‌کند، نه فقط این‌جا.
    ///
    /// ⚠️ و صفحه دوباره از حالِ واقعی چیده می‌شود (<c>ShowLogin</c>): پیش
    /// از این گام روی «تمام» می‌ماند، پس کاربری که تازه خارج شده بود هنوز
    /// صفحهٔ پروفایل را می‌دید — با نامِ پاک‌شده و کارتِ خالی.
    /// </summary>
    [RelayCommand]
    private Task SignOutAsync() => CrashGuard.RunAsync("خروج از حساب", async () =>
    {
        Busy = true;
        try { await Cloud.SignOutAsync(); }
        finally { Busy = false; }

        StationLine = "";
        LoginPassword = ""; LoginPassword2 = "";
        RefreshAll();
        ShowLogin();
    });

    // ── ۱ب) «این دستگاه دیگر مالِ این پمپ نیست» ─────────────────────────

    /// <summary>
    /// این نصب به پمپی روی ابر بند است؟ (توکنِ دستگاه، شناسهٔ پمپ یا کدِ
    /// اپِ کارمندان — هر کدام باشد یعنی بند هست.)
    /// </summary>
    [ObservableProperty] private bool _pumpBound;

    /// <summary>
    /// ══ جدا کردنِ این دستگاه از این پمپ ═════════════════════════════════
    ///
    /// ⛔ <b>تا امروز این کار هیچ دکمه‌ای نداشت.</b>
    /// <see cref="CloudLink.ForgetStationAsync"/> نوشته شده بود و از هیچ‌جا
    /// صدا زده نمی‌شد، در حالی که خودِ برنامه سرِ ورودِ حسابِ پمپِ دیگر
    /// می‌گفت «برای جابه‌جایی، این دستگاه را از پمپِ فعلی جدا کنید» —
    /// یعنی کاربر را به کاری راهنمایی می‌کرد که راهش وجود نداشت. حالا
    /// <see cref="SeatAsync"/>ِ ابر خودش با عوض شدنِ حساب بندها را باز
    /// می‌کند، و این دکمه همان کار را <b>دستی</b> هم ممکن می‌کند (برای
    /// نصب‌های کهنه که شناسهٔ حساب ندارند، و برای حسابی که خودش چند پمپ
    /// دارد).
    ///
    /// ⚠️ <b>دفتر دست نمی‌خورد و پرسیده می‌شود.</b> فقط بندهای «این نصب به
    /// کدام پمپ وصل است» باز می‌شوند؛ یک ردیف از حساب‌ها هم پاک نمی‌شود.
    /// </summary>
    [RelayCommand]
    private Task ForgetPumpAsync() => CrashGuard.RunAsync("جدا کردنِ دستگاه از پمپ", async () =>
    {
        var yes = await Dialogs.ConfirmAsync(
            "جدا کردنِ این دستگاه از این پمپ",
            "اشتراک، کدِ اپِ کارمندان و نشانیِ سرورِ خانگیِ این پمپ از این کامپیوتر "
            + "برداشته می‌شوند. برای وصل شدنِ دوباره از «حساب و ورود» وارد شوید و نامِ پمپ را بزنید.\n\n"
            + "⛔ دفتر و حساب‌های روی این کامپیوتر دست نمی‌خورند.",
            "جدا کن", "بی‌خیال");
        if (!yes) return;

        Busy = true;
        try { await Cloud.ForgetStationAsync(); }
        finally { Busy = false; }

        StationLine = "";
        RefreshAll();
        _host.Toast("این دستگاه از پمپ جدا شد. برای وصل شدنِ دوباره از «حساب و ورود» وارد شوید و نامِ پمپ را بزنید.");
    });

    /// <summary>
    /// اگر ورودِ همین حالا حسابِ <b>دیگری</b> بود و بندهای پمپِ قبلی باز
    /// شدند، همان را می‌گوید.
    ///
    /// ⚠️ قاعدهٔ «قفل بی‌صدا نباشد» این‌جا هم هست: کاربری که بی توضیح
    /// می‌بیند اشتراک و کدِ اپش رفته‌اند، فکر می‌کند برنامه خراب شده.
    /// </summary>
    private string SwitchNote() => Cloud.AccountSwitched
        ? "⚠️ این کامپیوتر پیش از این به حسابِ دیگری وصل بود، پس بندهای پمپِ قبلی "
          + "(اشتراک، کدِ اپِ کارمندان و نشانیِ سرور) برداشته شدند. دفتر دست نخورده است — "
          + "با زدنِ نامِ پمپ، این کامپیوتر به پمپِ همین حساب وصل می‌شود."
        : "";

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
            //  ⛔ رمزِ **خواندن** جای خودش را دارد (`ServerReadKey`). تا
            //  ۱۴۰۵/۰۷/۱۲ این‌جا در `SyncCode`ِ دیتابیس — همان جای رمزِ
            //  **نوشتن** — می‌نشست: هم رمزِ نوشتن را می‌پوشاند و هم خام داخلِ
            //  `pump.db` (و هر پشتیبانش) می‌ماند.
            if (readKey.Length > 0)
            {
                var file = AppSettings.Load();
                if (file.ServerReadKey != readKey) { file.ServerReadKey = readKey; file.Save(); }
            }

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
        //  ⚠️ از همان درِ یگانه (`CheckStored`): کفِ ساعت و اثرِ انگشتِ
        //  کامپیوتر هم سنجیده می‌شوند — وگرنه پروفایل «فعال» می‌گفت در حالی
        //  که قفل‌ها بسته بودند.
        var now = LicenseClock.Now(file);
        var check = LicenseGuard.CheckStored(file, now);

        SubActive = check.Valid;
        //  ⚠️ **رو به بالا، مثلِ خودِ سرور** (`daysLeft = Math.ceil(...)`).
        //  تا ۱۴۰۵/۰۷/۱۳ رو به پایین بود: دورهٔ آزمایشیِ سی‌روزه همان لحظهٔ
        //  ساختنِ پمپ «۲۹ روز» خوانده می‌شد در حالی که پنلِ مدیر «۳۰ روز
        //  مانده» می‌گفت — دو حرف از یک اشتراک. همان قاعدهٔ `VipSection`،
        //  `SoftLock` و `Entitlements.GraceDaysLeft`.
        VipDays = check.Valid && check.SubscriptionEndsAt > now
            ? (int)Math.Ceiling((check.SubscriptionEndsAt - now) / 86_400_000d)
            : 0;
        SubKind = check.Valid ? KindOf(check.PlanTitle) : "";
        SubPermanent = check.Valid && VipDays > 3650;

        // ⚠️ **پیش از** هر بازگشتِ زودهنگام: تا دیروز روی پمپی که هنوز فعال
        // نشده بود (یعنی همان چیزی که صاحب ریپو می‌دید) این چهار خانه خالی
        // می‌ماندند و کارتِ اشتراک «خراب» به نظر می‌رسید.
        ShowSubDetails(check, file);

        if (string.IsNullOrWhiteSpace(file.CloudDeviceToken))
        {
            //  ⛔ «کدِ شش‌رقمیِ اشتراک را بزنید» دیگر راهِ درست نیست و کاربر را
            //  دنبالِ کدی می‌فرستاد که ندارد: این کامپیوتر با **ورود به حساب و
            //  نامِ پمپ** خودش بند می‌شود و دورهٔ آزمایشی هم همان‌جا می‌آید.
            SubStatus = "هنوز به پمپ وصل نشده — از «حساب و ورود» وارد شوید و نامِ پمپ را بزنید؛ دورهٔ آزمایشی خودش می‌آید.";
            //  ⛔ «جدا شده» با «هنوز فعال نشده» یکی نیست: قفلِ بی‌توضیح باگ است.
            if (CloudLink.DeviceDetachedWhy.Length > 0) SubStatus = "⚠️ " + CloudLink.DeviceDetachedWhy;
            UpdatePill();
            return;
        }
        if (check.Valid)
        {
            SubStatus = IsTrial(check.PlanTitle)
                ? $"✅ دورهٔ آزمایشیِ رایگان فعال است — {VipDays} روز مانده."
                : SubPermanent
                    ? $"✅ اشتراکِ «{SubKind}» فعال است — دائمی."
                    : $"✅ اشتراکِ «{SubKind}» فعال است — {VipDays} روز مانده.";
        }
        else
        {
            SubStatus = "⚠️ " + check.Reason;
        }
        //  ⏰ ساعتِ ویندوز از آخرین زمانی که برنامه دیده عقب‌تر است — مجوز
        //  با همان کفِ ساعت سنجیده می‌شود، پس به کاربر گفته می‌شود چرا.
        if (LicenseClock.Behind(file))
            SubStatus += "\n⏰ ساعتِ این کامپیوتر عقب است — تاریخ و ساعتِ ویندوز را درست کنید.";
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
        PumpCodeLine = string.IsNullOrWhiteSpace(f.CloudStationId) ? "هنوز روی سرورِ حساب ثبت نشده" : f.CloudStationId;
        HomeLine = string.IsNullOrWhiteSpace(s.GetString(SettingsService.ServerUrl))
            ? "هنوز پیدا نشده — با روشن شدنِ سرورِ خانگی خودش پیدا می‌شود"
            : "وصل و ثبت‌شده";
        //  ⛔ **«فعال نشده» به‌تنهایی هیچ کاری دستِ کاربر نمی‌دهد.**
        //
        //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): حساب ساخته، پمپ ساخته، و باز
        //  هم «فعال نشده» — بی هیچ سرنخی. دلیلش **بود** ولی هیچ‌جا نمی‌رفت:
        //  حلقهٔ شصت‌ثانیه‌ای نتیجهٔ `BindAsync` را دور می‌ریخت.
        //  ⚠️ نامِ هیچ میزبانی در این پیام نمی‌آید — همان قاعدهٔ همیشگی.
        CloudLine = !string.IsNullOrWhiteSpace(f.CloudDeviceToken)
            ? "فعال — وصل به پمپِ شما"
            : CloudLink.LastBindWhy is { Length: > 0 } why
                ? "فعال نشده — " + why
                : "فعال نشده";
        AppVersionLine = PumpYaqobi.App.Update.AppVersion.Current;

        //  دکمهٔ «جدا کردن» فقط وقتی دیده می‌شود که بندی برای باز کردن باشد
        PumpBound = !string.IsNullOrWhiteSpace(f.CloudDeviceToken)
                 || !string.IsNullOrWhiteSpace(f.CloudStationId)
                 || !string.IsNullOrWhiteSpace(f.CloudAccessCode);

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
              + " روز ارفاق. در این مدت همان پلنِ شما کار می‌کند."
            : "";
    }

    /// <summary>نامِ کوتاهِ اشتراک برای دکمهٔ سربرگ — «آزمایشی» · «استاندارد» · «VIP» · «دائمی».</summary>
    [ObservableProperty] private string _subKind = "";

    /// <summary>اشتراکِ بی تاریخِ پایان (دائمی) — روز شمرده نمی‌شود.</summary>
    [ObservableProperty] private bool _subPermanent;

    /// <summary>
    /// سرور برای دورهٔ آزمایشی «دوره‌ی آزمایشی» می‌فرستد و برای اشتراک
    /// **کدِ** پلن (`std` · `vip` · `perm`)، نه نامش. پس نام همین‌جا ساخته
    /// می‌شود؛ هر نامِ دیگری (مثلاً «VIP»ِ مجوزهای کهنه) همان‌طور می‌ماند.
    /// </summary>
    public static bool IsTrial(string planTitle) => (planTitle ?? "").Contains("آزمایشی");

    public static string KindOf(string planTitle) => (planTitle ?? "").Trim() switch
    {
        "" => "VIP",
        var t when IsTrial(t) => "آزمایشی",
        "std" or "standard" => "استاندارد",
        "vip" => "VIP",
        "perm" or "permanent" => "دائمی",
        var t => t,
    };

    private void ShowSubDetails(LicenseCheck check, AppSettings file)
    {
        ShowAccess(file);
        var activated = !string.IsNullOrWhiteSpace(file.CloudDeviceToken);
        SubPlanText = check.Valid
            ? (IsTrial(check.PlanTitle) ? "دورهٔ آزمایشیِ رایگان" : KindOf(check.PlanTitle))
            : activated ? "بدونِ اشتراکِ فعال" : "فعال نشده";
        SubEndsText = check.Valid && check.SubscriptionEndsAt > 0
            ? Shamsi.Of(DateTimeOffset.FromUnixTimeMilliseconds(check.SubscriptionEndsAt).LocalDateTime)
            : "—";
        SubDaysText = !check.Valid ? "—" : SubPermanent ? "دائمی" : $"{Shamsi.Money(VipDays)} روز";
        SubSourceText = check.Valid ? "مجوزِ امضاشدهٔ سرور" : activated ? check.Reason : "وارد حساب شوید و نامِ پمپ را بزنید";
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
    //  ⚠️ **آدمک‌های عکسِ مرجع در صفحه هستند** — «همین مدل باشد و این
    //  آدمک‌ها هم باشند.» ولی از ۱۴۰۵/۰۶/۲۹ **برداری** کشیده شده‌اند، با
    //  رنگ و تمِ خودِ برنامه: `Controls/LoginArt.axaml`.
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

    // ══ درِ سوم: ورود با کدِ ایمیلی (بندِ ۲۰٫۷) ═══════════════════════════
    //
    //  ⚠️ **جانشینِ راهِ رمز نیست، کنارِ آن است.** مشتری‌های امروز با ایمیل و
    //  رمز وارد می‌شوند و آن مسیر دست‌نخورده ماند؛ این یکی قراردادِ ثابتِ
    //  پرامپت است (`POST /api/auth/{app}/request-code` و `/verify`) که هر
    //  سه برنامه با همان نوشته می‌شوند.
    //
    //  ⚠️ و رمزی در کار نیست: ثبت‌نام و ورود یکی‌اند — حسابِ نبوده سرِ
    //  همان `verify`ِ موفق ساخته می‌شود. پس کاربر نه رمزی می‌سازد و نه
    //  رمزی گم می‌کند.

    /// <summary>روی «ورود با کدِ ایمیلی» هستیم — از دو حالتِ بالا جلوتر است.</summary>
    [ObservableProperty] private bool _isCodeLogin;

    /// <summary>شش خانهٔ کد — پرشِ خودکار، Paste، ارقامِ فارسی، ارسالِ خودکار.</summary>
    public CodeBoxesViewModel CodeBoxes { get; } = new();

    /// <summary>ثانیه‌های ماندهٔ «دوباره بفرست». صفر یعنی می‌شود.</summary>
    [ObservableProperty] private int _codeSeconds;

    /// <summary>ایمیلِ پوشانده، همان‌طور که سرور برگرداند.</summary>
    [ObservableProperty] private string _codeMasked = "";

    /// <summary>ایمیل رفت یا نه — از <c>request-status</c>ِ خودِ سرور.</summary>
    [ObservableProperty] private string _codeDelivery = "";

    public bool CanResendCode => CodeSeconds <= 0;

    partial void OnCodeSecondsChanged(int v) => OnPropertyChanged(nameof(CanResendCode));

    partial void OnIsCodeLoginChanged(bool v)
    {
        OnPropertyChanged(nameof(AccountButtonText));
        LoginStatus = "";
    }

    private CancellationTokenSource? _codeTick;

    /// <summary>
    /// شمارشِ معکوسِ شصت ثانیه.
    ///
    /// ⚠️ با بسته شدنِ صفحه خاموش می‌شود — تایمری که پشتِ صفحهٔ بسته بچرخد
    /// همان چیزی است که قاعدهٔ «بخشی که تویش نیستی هیچ مصرفی ندارد» قدغن
    /// کرده. و هیچ دستورِ دیتابیسی هم نمی‌زند.
    /// </summary>
    private void StartCountdown(int seconds)
    {
        StopCountdown();
        CodeSeconds = Math.Max(0, seconds);
        if (CodeSeconds == 0) return;
        var cts = new CancellationTokenSource();
        _codeTick = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested && CodeSeconds > 0)
                {
                    await Task.Delay(1000, cts.Token);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (!cts.IsCancellationRequested && CodeSeconds > 0) CodeSeconds--;
                    });
                }
            }
            catch { /* لغو شد */ }
        }, cts.Token);
    }

    private void StopCountdown()
    {
        var cts = _codeTick;
        _codeTick = null;
        try { cts?.Cancel(); cts?.Dispose(); } catch { }
    }

    [ObservableProperty] private string _loginName = "";
    [ObservableProperty] private string _loginEmail = "";
    [ObservableProperty] private string _loginPassword = "";
    [ObservableProperty] private string _loginPassword2 = "";
    //  ⛔ `_loginCode` (کدِ اشتراک در گامِ پمپ) در ۱۴۰۵/۰۷/۰۴ برداشته شد —
    //  خواستهٔ صریحِ صاحب ریپو: «بعد از کد شش رقمی یک کد شش رقمی دیگه
    //  می‌خواد، اون چیه؟ اون رو حذف کن، لازم نیست.» راهِ خرج کردنِ کد
    //  دست‌نخورده ماند و همان‌جایی است که باید باشد: کارتِ اشتراکِ پروفایل
    //  (`SubCode` + `RedeemSubCommand`).

    /// <summary>کدِ شش‌رقمی که به **ایمیل** آمد — با کدِ اشتراک یکی نیست.</summary>
    [ObservableProperty] private string _emailCode = "";

    /// <summary>شرایط و ضوابط را پذیرفته‌ام — خودِ سرور اجباری‌اش کرده.</summary>
    [ObservableProperty] private bool _acceptTerms;

    /// <summary>متنِ شرایط، وقتی کاربر خواست ببیندش.</summary>
    [ObservableProperty] private string _termsText = "";

    [ObservableProperty] private bool _showTerms;
    [ObservableProperty] private string _loginPump = "";
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
    public string AccountButtonText =>
        IsCodeLogin ? "فرستادنِ کد به ایمیل"
        : IsSignUp ? "ساختنِ حساب و ادامه"
        : "ورود و ادامه";

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
        OnPropertyChanged(nameof(StepForgot));
        OnPropertyChanged(nameof(ShowLoginPage));
        OnPropertyChanged(nameof(ShowProfilePage));

        //  شمارشِ معکوس فقط روی همان گام می‌چرخد — تایمرِ پشتِ صفحهٔ بسته
        //  همان چیزی است که قاعدهٔ «بخشِ پنهان صفر مصرف» قدغن کرده.
        if (v != 2) StopCountdown();

        // ⚠️ «فقط همین را نشان بده، نه بخش‌ها باشند نه غیره» — سربرگ، نوارِ
        // جمله‌ها و نوارِ بخش‌های خودِ پنجره با همین یک نشان پنهان می‌شوند
        // (`MainViewModel.IsChromeVisible`). همان راهی که صفحهٔ حسابِ قرض‌دار
        // و صفحهٔ شرکت از پارسال می‌روند — قاعدهٔ تازه‌ای ساخته نشد.
        IsPageOpen = ShowLoginPage;

        //  ══ «ورود تمام شد ⇒ دفترِ حسابش را همین حالا بیاور» ════════════
        //
        //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «یارو اینترنت داره و می‌ره
        //  تو حساب است و لودینگ روی صفحه نمیاد تا اطلاعاتی که توی حساب و
        //  سرور است بیاد.»
        //
        //  ⚠️ <c>SyncEngine.FirstDelay</c> پانزده ثانیه است تا صفحهٔ اولِ
        //  برنامه بی رقیب بالا بیاید — ولی کسی که همین حالا وارد شده به یک
        //  صفحهٔ **خالی** نگاه می‌کند و گمان می‌کند دفترش رفته. این یک خط
        //  همان مکث را — و فقط همان را — رد می‌کند.
        //  ⛔ هیچ تصمیمِ تازه‌ای این‌جا نیست: «پرده بیاید یا نه» فقط در
        //  <c>SyncEngine.PrimeWanted</c> است، و نصبِ بند‌نشده همان‌جا
        //  بی هیچ هزینه‌ای برمی‌گردد.
        if (v == 4) AppHost.Current.SyncIfStarted?.PrimeNow();
    }

    [RelayCommand]
    private void SetSignUp(string? yes)
    {
        //  «کد» درِ سوم است؛ دو دکمهٔ قبلی همان کارِ قبلی را می‌کنند
        IsCodeLogin = yes == "code";
        if (!IsCodeLogin) IsSignUp = yes != "no";
    }

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

        var activated = !string.IsNullOrWhiteSpace(f.CloudDeviceToken);
        var hasPump = !string.IsNullOrWhiteSpace(_host.Settings.GetString(SettingsService.StationName));

        //  ⚠️ «تمام» یعنی یا واقعاً همه‌چیز هست، یا کاربر خودش گفته «بعداً»
        //  (`AppSettings.LoginSkipped`) — وگرنه صفحهٔ ورود می‌شد یک دیوار
        //  جلوی دفترِ خودش، و آن خلافِ قاعدهٔ «دفتر گروگان نیست» بود.
        //  ⛔ **«تمام» به بند شدنِ دستگاه بند نیست — و نباید باشد.**
        //
        //  گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۱): «همه‌شو تموم کردم، ولی
        //  هر بار روی پروفایل می‌زنم می‌گه اسمِ پمپ رو انتخاب کن.»
        //
        //  بند شدنِ دستگاه کاری نیست که کاربر در این صفحه بتواند انجام
        //  دهد (کادرِ کد از ۱۴۰۵/۰۷/۰۴ برداشته شده و تنها دکمه‌اش
        //  «ساختنِ پمپ» است). پس شرط کردنش یعنی گامی که **هیچ راهِ
        //  خروجی ندارد** — یک دیوارِ نامرئی، همان چیزی که بندِ
        //  ۱۴۰۵/۰۷/۱۰ برای برداشتنش نوشته شد و از درِ دیگر برگشت.
        //
        //  ⚠️ و چیزی پنهان نمی‌شود: تا دستگاه بند نشده، پروفایل همچنان
        //  «سرورِ حساب: فعال نشده» می‌گوید و حلقهٔ شصت‌ثانیه‌ای خودش
        //  دوباره می‌زندش.
        LoginStep = SignedIn && (activated || f.PumpStepDone) && hasPump ? 4
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
    private Task AccountStepAsync() => CrashGuard.RunAsync("سرورِ حساب", async () =>
    {
        var name = (LoginName ?? "").Trim();
        var email = (LoginEmail ?? "").Trim();
        var pass = LoginPassword ?? "";
        var pass2 = LoginPassword2 ?? "";

        if (LoginRules.BadEmail(email) is { } mailWhy) { LoginStatus = "❌ " + mailWhy; return; }
        //  ⚠️ راهِ کد رمز ندارد و نباید بخواهد — ثبت‌نام و ورود در آن یکی‌اند
        if (!IsCodeLogin)
        {
            if (IsSignUp && name.Length < 2) { LoginStatus = "❌ نامتان را بنویسید."; return; }
            if (LoginRules.WeakPassword(pass) is { } passWhy) { LoginStatus = "❌ " + passWhy; return; }
            if (IsSignUp && pass != pass2) { LoginStatus = "❌ دو رمز یکی نیستند."; return; }
            if (IsSignUp && !AcceptTerms) { LoginStatus = "❌ شرایط و ضوابط را بپذیرید."; return; }
        }

        Busy = true;
        LoginStatus = IsSignUp ? "در حالِ فرستادنِ کد به ایمیل…" : "در حالِ ورود…";
        try
        {
            if (IsCodeLogin)
            {
                //  ⛔ **پاسخ همیشه ۲۰۰ است مگر سقفِ نرخ** — پس این‌جا هم
                //  هیچ‌وقت نمی‌گوییم «چنین حسابی نیست». همان قاعدهٔ سرور:
                //  با این مسیر نباید بشود فهمید کدام ایمیل حساب دارد.
                var sent = await Cloud.RequestCodeAsync(email);
                if (!sent.Ok) { LoginStatus = "❌ " + sent.Why; return; }

                CodeBoxes.Clear();
                CodeMasked = Cloud.CodeMaskedEmail;
                CodeDelivery = "";
                StartCountdown(Cloud.CodeResendAfter);
                LoginStatus = "✅ کدِ شش‌رقمی به "
                            + (CodeMasked.Length > 0 ? CodeMasked : email) + " فرستاده شد.";
                RefreshAll();
                LoginStep = 2;
                _ = CheckDeliveryAsync();
                return;
            }

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
            LoginStatus = SwitchNote();
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
        if (IsCodeLogin) { await VerifyCodeLoginAsync(); return; }

        var code = LoginRules.Digits(EmailCode);
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
            LoginStatus = SwitchNote();
            RefreshAll();
            LoginStep = 3;
        }
        finally { Busy = false; }
    });

    /// <summary>
    /// ══ کدِ ایمیلی ⇒ نشست ═══════════════════════════════════════════════
    ///
    /// یک پله، نه دو: حسابِ نبوده همان‌جا ساخته می‌شود.
    ///
    /// ⚠️ کدِ اشتباه خانه‌ها را <b>پاک می‌کند</b> و فوکوس را برمی‌گرداند
    /// سرِ خانهٔ اول — وگرنه کاربر باید شش بار Backspace بزند، و سرور هم
    /// پنج تلاش بیشتر نمی‌دهد.
    /// </summary>
    private async Task VerifyCodeLoginAsync()
    {
        var code = CodeBoxes.Code;
        if (code.Length != CodeBoxesViewModel.Size)
        {
            LoginStatus = "❌ کد باید شش رقم باشد.";
            return;
        }

        Busy = true;
        LoginStatus = "در حالِ تاییدِ کد…";
        try
        {
            var res = await Cloud.VerifyCodeAsync(code);
            if (!res.Ok)
            {
                CodeBoxes.Clear();
                LoginStatus = "❌ " + res.Why;
                return;
            }

            StopCountdown();
            CodeBoxes.Clear();
            ClearSkipped();
            LoginStatus = SwitchNote();
            RefreshAll();
            LoginStep = 3;
        }
        finally { Busy = false; }
    }

    /// <summary>
    /// «ایمیل رفت یا نه؟» — جوابش از خودِ سرور می‌آید
    /// (<c>request-status</c>)، نه از حدسِ ما.
    ///
    /// ⚠️ همین است که کاربر را از انتظارِ کور بیرون می‌آورد: اگر سرویسِ
    /// ایمیلِ سرور خراب باشد، به‌جای شصت ثانیه نگاه کردن به صندوقِ خالی،
    /// همان لحظه می‌فهمد.
    /// </summary>
    private async Task CheckDeliveryAsync()
    {
        try
        {
            //  یک مکثِ کوتاه تا Worker فرصتِ فرستادن داشته باشد
            await Task.Delay(3000);
            var (ok, state, reason, _) = await Cloud.RequestStatusAsync();
            if (!ok) return;
            CodeDelivery = state switch
            {
                "sent" => "✅ ایمیل فرستاده شد — صندوقِ ورودی و پوشهٔ هرزنامه را ببینید.",
                "failed" => "❌ فرستادنِ ایمیل نشد" + (reason.Length > 0 ? " — " + reason : "") + ".",
                "sending" => "در حالِ فرستادن…",
                _ => "در صفِ فرستادن…",
            };
        }
        catch { /* خبرِ تحویل رفاه است */ }
    }

    /// <summary>کد نرسید — دوباره بفرست (همان پلهٔ یک).</summary>
    [RelayCommand]
    private Task ResendEmailCodeAsync() => CrashGuard.RunAsync("فرستادنِ دوبارهٔ کد", async () =>
    {
        var email = (LoginEmail ?? "").Trim();

        if (IsCodeLogin)
        {
            if (!CanResendCode)
            {
                LoginStatus = $"❌ {CodeSeconds} ثانیه صبر کنید.";
                return;
            }
            Busy = true;
            try
            {
                var again = await Cloud.RequestCodeAsync(email);
                if (again.Ok)
                {
                    CodeBoxes.Clear();
                    CodeMasked = Cloud.CodeMaskedEmail;
                    CodeDelivery = "";
                    StartCountdown(Cloud.CodeResendAfter);
                    _ = CheckDeliveryAsync();
                }
                LoginStatus = again.Ok ? "✅ کد دوباره فرستاده شد." : "❌ " + again.Why;
            }
            finally { Busy = false; }
            return;
        }

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
        //  ⚠️ **کدِ خطای فنی به کاربر نشان داده نمی‌شود.** «سرور جواب نداد
        //  (۴۰۴)» زیرِ تیکِ شرایط فقط کاربر را می‌ترساند؛ متنِ شرایط یک
        //  نوشتهٔ خواندنی است، نه بخشی از کارِ ثبت‌نام (پذیرش را خودِ سرور
        //  سرِ `register/complete` می‌سنجد). پس نبودنش یک جملهٔ آرام است.
        TermsText = ok ? text : "متنِ شرایط فعلاً در دسترس نیست.";
        _ = why;
    });

    /// <summary>نشانِ «بعداً» را برمی‌دارد، چون حالا حسابِ واقعی هست.</summary>
    private static void ClearSkipped()
    {
        var f = AppSettings.Load();
        if (!f.LoginSkipped) return;
        f.LoginSkipped = false;
        f.Save();
    }

    //  ⛔ **«بعداً — فعلاً بی حساب ادامه می‌دهم» برداشته شد** (۱۴۰۵/۰۶/۳۰).
    //
    //  گزارشِ صاحب ریپو با عکس: «این نباشه و کار نمی‌کنه.» و درست می‌گفت —
    //  آن دکمه **دروغ می‌گفت**: نوشته‌اش «بی حساب ادامه می‌دهم» بود ولی
    //  `LoginSkipped` را نمی‌گذاشت و فقط `LoginStep = 3` می‌کرد، یعنی کاربر
    //  همچنان **داخلِ همان دیوارِ ورود** می‌ماند، این بار روی گامِ کدِ پمپ.
    //
    //  ⚠️ راهِ واقعیِ «بی حساب ادامه بده» همان **«‹ برگشت به برنامه»**ی بالای
    //  صفحه است (`CloseLoginAsync`) که واقعاً `LoginSkipped` می‌گذارد و به
    //  دفتر برمی‌گردد — و حالا آن‌چه کاربر تایپ کرده را هم نگه می‌دارد. پس
    //  قاعدهٔ «صفحهٔ ورود نباید دیوار شود» سرِ جایش است؛ فقط دکمهٔ دروغ‌گو رفت.

    // ── گامِ ۲: پمپ ─────────────────────────────────────────────────────

    /// <summary>
    /// گامِ پمپ را تمام می‌کند — <b>بی هیچ کدی</b>.
    ///
    /// <para>
    /// ⛔ <b>تا دیروز کدِ شش‌رقمی اجباری بود و همین‌جا همه گیر می‌کردند.</b>
    /// حسابِ تازه هیچ پمپی ندارد، و تنها راهِ ساختنش کد بود — کدی که صاحب
    /// ریپو صریح گفت در کار نیست («اشتراک رو من به حسابِ یارو از سرور
    /// می‌دم»). سنجشِ واقعی روی سرورِ واقعی نشانش داد: ثبت‌نام و ورود سبز،
    /// و بعد <c>bind</c> ۴۰۴ِ <c>no_station</c> — یعنی نه توکنِ دستگاه، نه
    /// مجوز، نه کدِ اپِ کارمندان.
    /// </para>
    ///
    /// <para>
    /// ⛔ و از ۱۴۰۵/۰۷/۰۴ کد <b>کاملاً از این گام رفت</b>. گزارشِ صاحب
    /// ریپو: «بعد از کد شش رقمی یک کد شش رقمی دیگه می‌خواد، اون چیه؟ اون
    /// رو حذف کن، لازم نیست.» و حق داشت: درست بعد از کدِ <b>ایمیل</b>، یک
    /// کادرِ شش‌رقمیِ دیگر با همان شکل و همان اندازه می‌آمد که هیچ ربطی به
    /// آن یکی نداشت — «اختیاری» بودنش هم دردی دوا نمی‌کرد، چون کاربر
    /// نمی‌دانست کدام است و دنبالِ کدی می‌گشت که اصلاً ندارد.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>هیچ قابلیتی از بین نرفت</b>: خرج کردنِ کدِ اشتراک همان‌جایی
    /// ماند که باید باشد — کارتِ «اشتراک»ِ خودِ پروفایل
    /// (<c>SubCode</c> + <c>RedeemSubCommand</c> ⇒
    /// <see cref="CloudLink.RedeemAsync"/>). کسی که کد دارد، بعد از ورود
    /// آن‌جا می‌زندش؛ کسی که ندارد اصلاً کادری نمی‌بیند.
    /// </para>
    ///
    /// ⚠️ نامِ پمپ و لوکیشن **پیش از** رفتن به سرور ذخیره می‌شوند، تا اگر
    /// سرور جواب نداد هم همان نامِ تازه در پروفایل و سربرگ دیده شود.
    /// </summary>
    [RelayCommand]
    private Task FinishPumpAsync() => CrashGuard.RunAsync("گامِ پمپ", async () =>
    {
        var pump = (LoginPump ?? "").Trim();

        if (pump.Length < 2) { LoginStatus = "❌ نامِ پمپ را بنویسید."; return; }

        Busy = true;
        LoginStatus = "در حالِ ساختنِ پمپ روی سرور…";
        try
        {
            _host.Settings.Set(SettingsService.StationName, pump);
            RefreshAll();

            var res = await Cloud.EnsureStationAsync(pump);
            if (!res.Ok)
            {
                /*
                 *  ⛔ **این گام نباید دیوار شود.**
                 *
                 *  گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۰): «این بخش مانعِ
                 *  ساختِ حساب می‌شود… نمی‌خوام این مشکل پیش بیاد، منو
                 *  دیوانه نکنی.» و روی صفحه: «❌ خطای داخلی سرور».
                 *
                 *  آن جمله **پیامِ خودِ سرور** است (۵۰۰). خرابی آن‌طرف بود،
                 *  ولی گیر کردنِ کاربر این‌طرف: تنها دیوارِ بینِ او و
                 *  برنامه همین گام است.
                 *
                 *  ⛔ پس پیش از «نشد»، از سرور می‌پرسیم **پمپ ساخته شد یا
                 *  نه**. کارِ این گام یک چیز است و اگر انجام شده، انجام
                 *  شده — هر چه آن یک درخواست گفته باشد. و این پنهان کردنِ
                 *  خطا نیست: بند شدنِ دستگاه گم نمی‌شود، حلقهٔ
                 *  شصت‌ثانیه‌ایِ پس‌زمینه خودش دوباره می‌زندش.
                 */
                if (await Cloud.HasStationAsync())
                {
                    //  ⛔ **مهرِ ماندگار، نه فقط یک `LoginStep = 4`.**
                    //  تا دیروز همین‌جا فقط گام عوض می‌شد؛ بازدیدِ بعدیِ
                    //  پروفایل دوباره از حالِ واقعی حساب می‌کرد و چون
                    //  دستگاه بند نشده بود، به همین گام برمی‌گشت — حلقهٔ
                    //  «هر بار اسمِ پمپ را می‌خواهد».
                    var f2 = AppSettings.Load();
                    f2.PumpStepDone = true;
                    try { f2.Save(); } catch { /* دورِ بعد دوباره */ }

                    LoginStatus = "";
                    RefreshAll();
                    LoginStep = 4;
                    _host.Toast("✅ پمپِ شما روی حسابتان هست — بقیه‌اش خودکار انجام می‌شود", ToastKind.Ok);
                    return;
                }

                LoginStatus = "❌ " + PumpStepWhy(res);
                return;
            }

            //  ⛔ **همان مهرِ ماندگار، در مسیرِ موفق هم.** بی این، اگر
            //  فردا بند شدنِ دستگاه از کار می‌افتاد (کلیدِ عوض‌شده، سقفِ
            //  نرخ، سرورِ خاموش)، همین کاربرِ تمام‌شده دوباره در گامِ پمپ
            //  گیر می‌کرد — حلقه از درِ دیگر برمی‌گشت.
            var fin = AppSettings.Load();
            fin.PumpStepDone = true;
            try { fin.Save(); } catch { /* دورِ بعد دوباره */ }

            //  نشانیِ سرورِ خانگی و اشتراک را هم همین‌جا برمی‌داریم، وگرنه
            //  کاربر تا تیکِ بعدیِ پس‌زمینه «هنوز وصل نیست» می‌بیند.
            try { await Cloud.HomeFromAccountAsync(); } catch { /* رفاه است */ }

            LoginStatus = "";
            RefreshAll();
            LoginStep = 4;
        }
        finally { Busy = false; }
    });

    /// <summary>
    /// ⛔ «خطای داخلی سرور» به‌تنهایی هیچ کاری دستِ کاربر نمی‌دهد.
    ///
    /// سرورِ حساب از ۲.۸.۵ روی هر ۵۰۰ یک <b>کدِ پیگیری</b> می‌گذارد
    /// (<c>error.ref</c>) که همان رشته کنارِ خودِ استثنا در لاگِ سرور چاپ
    /// می‌شود. پس اگر کد آمده بود، همان را نشان می‌دهیم و می‌گوییم کجا
    /// دنبالش بگردد؛ و اگر <b>نیامده بود</b>، خودِ همین یعنی سرورِ حساب از
    /// آن نسخه قدیمی‌تر است — و این را هم می‌گوییم، نه این‌که کاربر را با
    /// یک جملهٔ بی‌سرنخ رها کنیم.
    ///
    /// ⚠️ نامِ هیچ میزبانی در این پیام نمی‌آید — همان قاعدهٔ همیشگی.
    /// </summary>
    private static string PumpStepWhy(CloudResult res)
    {
        var why = res.Why ?? "";
        if (!why.Contains("خطای داخلی سرور")) return why;
        var hasRef = why.Contains("پیگیری");
        return hasRef
            ? why + " — این کد را در «سرورِ حساب ← لاگ» بگردید"
            : why + " — و کدِ پیگیری نداد، یعنی سرورِ حساب کهنه است؛ "
                      + "فایلِ نصبِ تازه را بگیرید. «بعداً» هم شما را رد می‌کند.";
    }

    /// <summary>
    /// «بعداً» روی گامِ پمپ: نام و لوکیشن ذخیره می‌شوند و صفحه رد می‌شود.
    /// بی این، پمپی که اینترنت ندارد تا ابد روی همین صفحه می‌ماند.
    /// </summary>
    [RelayCommand]
    private void SkipPump()
    {
        var pump = (LoginPump ?? "").Trim();
        if (pump.Length > 0) _host.Settings.Set(SettingsService.StationName, pump);

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
        //  ⚠️ آن‌چه کاربر تایپ کرده گم نمی‌شود — همان کاری که «بعداً»ی
        //  برداشته‌شده می‌کرد، ولی این بار روی دکمه‌ای که واقعاً کارش را
        //  می‌کند. رمز عمداً نه: حسابی ساخته نشده که رمزی داشته باشد.
        var f = AppSettings.Load();
        var email = (LoginEmail ?? "").Trim();
        var name = (LoginName ?? "").Trim();
        if (email.Length > 0 && LoginRules.BadEmail(email) is null) f.CloudEmail = email;
        if (name.Length > 0) f.CloudName = name;
        f.LoginSkipped = true;
        f.Save();

        var pump = (LoginPump ?? "").Trim();
        if (pump.Length > 0) _host.Settings.Set(SettingsService.StationName, pump);

        LoginPassword = ""; LoginPassword2 = "";
        LoginStatus = "";
        RefreshAll();
        LoginStep = 4;
        if (_host.GoHome is { } home) await home();
    });

    /// <summary>برگشت به گامِ حساب — برای عوض کردنِ حساب یا رمز.</summary>
    [RelayCommand]
    private void BackToAccount() { LoginStatus = ""; LoginStep = 1; }

    // ── گامِ ۰: رمزم را فراموش کرده‌ام ───────────────────────────────────
    //
    //  ⚠️ **گامِ صفر، نه گامِ پنج.** `ShowLoginPage => LoginStep < 4` قفل
    //  است (`AppLinksTests`) و باید همان بماند؛ صفر هم زیرِ چهار است، پس
    //  صفحهٔ ورود تمامِ پنجره را می‌گیرد، درست مثلِ بقیهٔ گام‌ها.
    //
    //  ⚠️ **رمزِ تازه نامِ فیلدِ جدا دارد** (`ResetPass`)، نه `LoginPassword`:
    //  آن یکی سه جای مشخص پاک می‌شود و سنجه شمارشش را قفل کرده.

    /// <summary>کدِ شش‌رقمی که برای بازیابی به ایمیل آمده.</summary>
    [ObservableProperty] private string _resetCode = "";

    /// <summary>رمزِ تازه — مثلِ هر رمزِ دیگری، هیچ‌جا ذخیره نمی‌شود.</summary>
    [ObservableProperty] private string _resetPass = "";
    [ObservableProperty] private string _resetPass2 = "";

    /// <summary>کد فرستاده شد؟ تا نرفته، نیمهٔ دومِ فرم دیده نمی‌شود.</summary>
    [ObservableProperty] private bool _resetSent;

    public bool StepForgot => LoginStep == 0;

    /// <summary>«رمزم را فراموش کرده‌ام» — از گامِ یک.</summary>
    [RelayCommand]
    private void OpenForgot()
    {
        LoginStatus = "";
        ResetSent = false;
        ResetCode = ""; ResetPass = ""; ResetPass2 = "";
        LoginStep = 0;
    }

    /// <summary>
    /// پلهٔ یک — کد به ایمیل.
    ///
    /// ⚠️ پیامِ موفقیت عمداً «اگر این ایمیل حساب داشته باشد…» است، نه «کد
    /// فرستاده شد»: خودِ سرور هم برای ایمیلِ موجود و ناموجود <b>یک جواب</b>
    /// می‌دهد تا فهرستِ ایمیل‌های مشتری‌ها لو نرود. برنامه نباید آن کار را
    /// خراب کند.
    /// </summary>
    [RelayCommand]
    private Task SendResetCodeAsync() => CrashGuard.RunAsync("کدِ بازیابی", async () =>
    {
        var email = (LoginEmail ?? "").Trim();
        if (LoginRules.BadEmail(email) is { } why) { LoginStatus = "❌ " + why; return; }

        Busy = true;
        LoginStatus = "در حالِ فرستادنِ کد…";
        try
        {
            var res = await Cloud.ForgotPasswordAsync(email);
            if (!res.Ok) { LoginStatus = "❌ " + res.Why; return; }
            ResetSent = true;
            LoginStatus = "✅ اگر این ایمیل حسابی داشته باشد، کدِ شش‌رقمی برایش رفت.";
        }
        finally { Busy = false; }
    });

    /// <summary>پلهٔ دو — کد و رمزِ تازه. سرور همان‌جا وارد هم می‌کند.</summary>
    [RelayCommand]
    private Task ResetPasswordAsync() => CrashGuard.RunAsync("رمزِ تازه", async () =>
    {
        var email = (LoginEmail ?? "").Trim();
        var code = new string((ResetCode ?? "").Where(char.IsDigit).ToArray());
        var pass = ResetPass ?? "";

        if (code.Length != 6) { LoginStatus = "❌ کدِ ایمیل باید شش رقم باشد."; return; }
        if (LoginRules.WeakPassword(pass) is { } why) { LoginStatus = "❌ " + why; return; }
        if (pass != (ResetPass2 ?? "")) { LoginStatus = "❌ دو رمز یکی نیستند."; return; }

        Busy = true;
        LoginStatus = "در حالِ گذاشتنِ رمزِ تازه…";
        try
        {
            var res = await Cloud.ResetPasswordAsync(email, code, pass);
            if (!res.Ok) { LoginStatus = "❌ " + res.Why; return; }

            //  رمزِ تازه از حافظهٔ صفحه هم می‌رود — همان قاعدهٔ همیشه
            ResetPass = ""; ResetPass2 = ""; ResetCode = ""; ResetSent = false;
            ClearSkipped();
            LoginStatus = "";
            RefreshAll();
            LoginStep = 3;
        }
        finally { Busy = false; }
    });

    // ── نشان دادنِ رمز ───────────────────────────────────────────────────

    /// <summary>
    /// «چشم» — خواستهٔ بندِ ۲ی صاحب ریپو: «نمایش/مخفی کردنِ رمز عبور».
    ///
    /// ⚠️ روی <b>هر سه</b> کادرِ رمز (ورود، تکرار، رمزِ تازه) یک‌جا اثر
    /// می‌کند: کاربری که رمز را می‌بیند، تکرارش را هم می‌خواهد ببیند.
    /// </summary>
    [ObservableProperty] private bool _revealPass;

    /// <summary>
    /// نویسهٔ پوشاننده. <c>'\0'</c> یعنی «نپوشان» — همان چیزی که
    /// <c>TextBox.PasswordChar</c> برای متنِ آشکار می‌خواهد.
    /// </summary>
    public char PassChar => RevealPass ? '\0' : '•';

    /// <summary>نوشتهٔ خودِ دکمه، تا کاربر بداند زدنش چه می‌کند.</summary>
    public string RevealPassText => RevealPass ? "🙈 پنهان کردنِ رمز" : "👁 نشان دادنِ رمز";

    partial void OnRevealPassChanged(bool v)
    {
        OnPropertyChanged(nameof(PassChar));
        OnPropertyChanged(nameof(RevealPassText));
    }

    [RelayCommand]
    private void ToggleRevealPass() => RevealPass = !RevealPass;

    /// <summary>از گامِ «تمام» به گامِ پمپ — برای عوض کردنِ نام یا لوکیشن.</summary>
    [RelayCommand]
    private void BackToPump() { LoginStatus = ""; LoginStep = 3; }

    // ── آدمک‌های کنارِ فرم — دیگر عکس نیستند، نقشهٔ برداری‌اند ───────────
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۹، با عکس و خطِ زردِ دورِ همان
    //  آدمک‌ها): «اون آدمک‌ها رو باسازی کن و بک‌گراندشو درست کن و با رنگ و
    //  تمِ خودِ برنامه باشه و همه‌چی با کیفیتِ خیلی بالا درست کن.»
    //
    //  ⛔ پس **هیچ چیزی از عکس در ویومدل نمانده**: نه `LoginArt`، نه
    //  `DecodeToWidth`، نه `CroppedBitmap`، نه فایلِ JPEG. خودِ صحنه در
    //  `Controls/LoginArt.axaml` برداری کشیده شده و رنگ‌هایش از تمِ برنامه
    //  می‌آید، پس در هر اندازه تیز است و هیچ بایتی خوانده نمی‌شود.
    //  سنجه: `dotnet run --project PumpYaqobi.UiTests -c Release -- loginart`.

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

        //  ⚠️ **آدمک‌ها هیچ‌جا خوانده نمی‌شوند** — نقشهٔ برداری با خودِ
        //  چیدمانِ صفحه کشیده می‌شود، پس نه گامی برایش لازم است و نه بایتی.
        //  (پیش از این یک JPEG بود و باید پیش از `LoadRowsAsync` خوانده
        //  می‌شد تا پشتِ سه پرس‌وجوی تب‌ها نماند.)
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
