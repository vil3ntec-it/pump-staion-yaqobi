using Avalonia.Controls;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.ViewModels;

//  ⛔ شورا ج۴: بخشی از ‎MainViewModel‎ — بنرِ بالای صفحه، اعلانِ مدیر، هشدارها و پرسیدن از سرورها. فقط جابه‌جاییِ همان عضوها از ‎MainViewModel.cs‎، بی تغییرِ یک رفتار.
public sealed partial class MainViewModel
{
    // ══ بنرِ بالای صفحه — اعلانِ مدیر و قفلِ نرم ═════════════════════════

    /// <summary>جملهٔ بنر — خالی یعنی بنری نیست.</summary>
    [ObservableProperty] private string _noticeText = "";

    public bool HasNotice => NoticeText.Length > 0;

    partial void OnNoticeTextChanged(string v) => OnPropertyChanged(nameof(HasNotice));

    /// <summary>بنر را می‌بندد — تا اعلانِ بعدی.</summary>
    /// ⛔ بستن یعنی بستن (۱۴۰۵/۰۷/۱۵ — «بسته نمی‌شه»): پیش از این همان نوارِ
    /// «اشتراک فعال نیست» دوباره می‌نشست. حالا نوارِ اشتراک تا عوض شدنِ حالش
    /// برنمی‌گردد (‎SeenNotices.DismissedBanner‎)، و پیامِ مدیر که بسته شد جایش
    /// فقط نوارِ اشتراکی می‌آید که کاربر هنوز نبسته.
    [RelayCommand]
    private void CloseNotice()
    {
        if (NoticeText.Length > 0 && NoticeText == SoftLock.Banner())
        {
            SeenNotices.DismissedBanner = SoftLock.BannerKind();
            NoticeText = "";
            return;
        }
        NoticeText = SoftLock.VisibleBanner();
    }

    /// <summary>
    /// اعلانِ تازه: هم بنرِ داخلِ برنامه، هم اعلانِ خودِ ویندوز.
    ///
    /// ⚠️ هر دو از <b>یک</b> جا می‌آیند. قاعدهٔ جدا ننویسید، وگرنه روزی
    /// یکی می‌آید و آن یکی نه.
    /// </summary>
    /// <summary>
    /// هشدارهای تازه ⇒ توستِ میرزا، و زنگِ داشبورد از همان فهرست.
    /// ⚠️ «تمام شد» توستِ سرخ است (هشدار)، «کم مانده» زرد.
    /// </summary>
    private void OnAlertsChanged(IReadOnlyList<AlertItem> opened, bool initial)
    {
        try
        {
            foreach (var d in AllPages.OfType<DashboardSectionViewModel>()) d.ApplyAlerts();
            var text = AlertWatch.ToastText(opened, initial);
            if (text.Length > 0)
                AppHost.Current.Toast(text, opened.Any(a => a.IsOut) ? ToastKind.Error : ToastKind.Warn);
        }
        catch { /* خبر رفاه است */ }
    }

    /// <summary>بخش‌های اشتراکیِ باز، در آخرین باری که پرسیدیم.</summary>
    private HashSet<string> _paidOpen = new();

    /// <summary>
    /// مجوز عوض شد یا مرزش رد شد ⇒ سربرگ، پروفایل و نوار همین حالا — و
    /// ⛔ <b>اگر قفل‌ها واقعاً جابه‌جا شدند، به کاربر گفته می‌شود</b>. تا
    /// ۱۴۰۵/۰۷/۱۴ اشتراکِ تازه بی‌صدا می‌نشست و صاحبِ پمپ نمی‌دانست باز شد.
    /// و بخشِ اشتراکی‌ای که همین حالا جلوی چشم است و بسته شد، به صفحهٔ اول
    /// برمی‌گردد — نه این‌که قفل تا کلیکِ بعدی دیده نشود.
    /// </summary>
    private void OnLicenseMoved()
    {
        _boundAt = DateTime.MinValue; Account.RefreshAll(); TickLinkDot();
        //  ⚠️ پیامِ مدیر که هنوز جلوی چشم است جایش را به نوارِ اشتراک نمی‌دهد
        if (!NoticeText.StartsWith("📣", StringComparison.Ordinal)) NoticeText = SoftLock.VisibleBanner();
        SubscriptionWatch.Forget();

        var now = Entitlements.Paid.Where(Entitlements.Allows).ToHashSet();
        var opened = now.Except(_paidOpen).ToList();
        var closed = _paidOpen.Except(now).ToList();
        _paidOpen = now;
        if (opened.Count > 0)
            AppHost.Current.Toast("🔓 اشتراک رسید — " + string.Join("، ", opened.Select(Entitlements.TitleOf))
                + " باز شد" + (Account.PillText is { Length: > 0 } p ? " · " + p : ""), ToastKind.Ok);
        if (closed.Count > 0)
        {
            AppHost.Current.Toast("🔒 " + string.Join("، ", closed.Select(Entitlements.TitleOf))
                + " بسته شد — دفتر و بقیهٔ برنامه کامل کار می‌کند.", ToastKind.Warn);
            if (PlanFeatureOf(Current?.Id) is { } f && !Entitlements.Allows(f)) _ = OpenStartSectionAsync();
        }
    }

    private void ShowNotice(CloudNotice n)
    {
        //  🤖 پیامِ سرور (مثلاً «اشتراک تمدید شد») ⇒ پیگیرِ اشتراک همین حالا
        //  می‌پرسد، نه در دورِ بعد — پیام و قفل با هم برسند.
        StationPublisher.CloudSoon();
        NoticeText = "📣 " + n.Title + (n.Body.Length > 0 ? " — " + n.Body : "");
        AppHost.Current.Toast(NoticeText, ToastKind.Info);
        NativeNotice.Show(n.Title.Length > 0 ? n.Title : PumpBrand.Name, n.Body, WindowHandle);
    }

    /// <summary>
    /// دستگیرهٔ پنجره — فقط برای اعلانِ خودِ ویندوز.
    /// ⚠️ صفر یعنی «نمی‌شود»، و همان‌جا بی‌صدا رد می‌شود.
    /// </summary>
    public IntPtr WindowHandle { get; set; }

    /// <summary>کلیکِ چراغِ ابر — همین حالا یک درخواستِ واقعی می‌زند.</summary>
    [RelayCommand]
    private async Task CheckCloudAsync()
    {
        AppHost.Current.Toast("در حالِ تماس با سرورِ حساب…", ToastKind.Info);
        //  ⛔ کلیک یعنی «همین حالا وصل شو»، نه فقط «بالاست؟» (۱۴۰۵/۰۷/۱۳،
        //  سنجهٔ `linkstates`): تا امروز این‌جا فقط `/api/health` پرسیده
        //  می‌شد، پس کلیکِ چراغِ زرد هیچ‌وقت این کامپیوتر را ثبت نمی‌کرد. حالا
        //  همان دورِ کاملِ پس‌زمینه (نشست، ثبتِ دستگاه، مجوز) همین حالا می‌دود.
        await Services.StationPublisher.CloudKeepNowAsync();
        var (up, ver) = await Services.CloudLink.CloudHealthAsync();
        _boundAt = DateTime.MinValue;
        var bound = DeviceBound();
        TickCloudDot();
        if (!up)
        {
            AppHost.Current.Toast("❌ به سرورِ حساب نرسیدیم — اینترنت و بالا بودنِ سرور را ببینید", ToastKind.Error);
            return;
        }
        if (bound)
        {
            Account.RefreshAll();
            var v = ver.Length > 0 ? $" (نسخهٔ {ver})" : "";
            AppHost.Current.Toast("✅ سرورِ حساب جواب داد و این کامپیوتر به پمپِ شما ثبت است" + v, ToastKind.Ok);
            return;
        }
        //  هنوز ثبت نیست ⇒ جملهٔ راستِ همان حال، و اگر کارِ کاربر است
        //  (ورود یا نامِ پمپ)، همان صفحه همین حالا باز می‌شود — بن‌بست نه.
        AppHost.Current.Toast("⚠️ " + UnboundWhy(_signedInCache).Split(" — ")[0], ToastKind.Warn);
        if (!_signedInCache || Services.CloudLink.AccountHasStation == false)
            await GoAsync(Account);
    }

    /// <summary>
    /// کلیکِ روی چراغ — «همین حالا بررسی کن».
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «سرور روشن است اما پمپ بنزین می‌گوید
    /// خاموش است… آن‌جوری هست که هر ثانیه چک کند؟» خودِ حلقه هر پنج ثانیه
    /// می‌گردد، ولی کاربر باید بتواند **همین حالا** هم بپرسد و جواب ببیند.
    /// </summary>
    [RelayCommand]
    private async Task CheckServerAsync()
    {
        var sync = AppHost.Current.PublisherIfStarted;
        //  ⛔ «تنظیم نشده» دیگر بن‌بست نیست (۱۴۰۵/۰۷/۱۳): تا امروز کلیکِ چراغِ
        //  نصبی که هنوز نشانی نداشت فقط «از پروفایل وارد شوید» می‌گفت و هیچ
        //  نمی‌گشت — در حالی که یافتنِ سرورِ خانگی هیچ حسابی نمی‌خواهد. حالا
        //  همان کشفِ خودکار همین حالا می‌دود.
        if (sync is null)
        {
            AppHost.Current.Toast("برنامه هنوز بالا نیامده — چند ثانیهٔ دیگر دوباره بزنید", ToastKind.Info);
            return;
        }

        AppHost.Current.Toast("در حالِ گشتن برای سرورِ خانگی…", ToastKind.Info);
        //  ⚠️ `force` یعنی ترمزِ «پنج دقیقه دوباره نگرد» را هم رد کن: کاربر
        //  خودش گفته همین حالا.
        var ok = await sync.KeepLinkAsync(force: true);
        TickServerDot();
        AppHost.Current.Toast(
            ok ? "✅ به سرورِ خانگی وصل شد" : "❌ سرورِ خانگی پیدا نشد — روشن بودنش و شبکه را ببینید",
            ok ? ToastKind.Ok : ToastKind.Error);
    }
    /// <summary>
    /// «قفل است؟» — حالا فقط نمایی از <see cref="Phase"/> است، نه یک حالتِ
    /// جدا.
    ///
    /// ⚠️ دو منبعِ حقیقت برای یک چیز همان جایی است که باگ می‌نشیند: پیش از
    /// این ‎IsLocked‎ و ‎IsWarming‎ جدا بودند و «کدام صفحه دیده شود» از
    /// ترکیبشان حساب می‌شد — و همان ترکیب بود که کاربر را وسطِ لودینگ به
    /// صفحه‌های داخلی می‌بُرد و بعد به قفل برمی‌گرداند.
    /// </summary>
    public bool IsLocked => Phase != AppPhase.Ready;

    /// <summary>
    /// نامِ نقشِ کاربر برای نشانِ سربرگ — همان ‎.role-badge‎ نسخهٔ وب.
    /// با هر بار باز و بسته شدنِ قفل تازه می‌شود.
    /// </summary>
    public string RoleText => AppHost.Current.Session.Role switch
    {
        UserRole.Admin  => "🛡️ مدیر",
        UserRole.Staff  => "👤 کارمند",
        _               => "👁️ نظاره‌گر",
    };

    /// <summary>رنگِ نشانِ نقش — مدیر سبز، کارمند آبی، نظاره‌گر خاکستری.</summary>
    public string RoleBrushKey => AppHost.Current.Session.Role switch
    {
        UserRole.Admin => "Pump.Ok",
        UserRole.Staff => "Pump.Info",
        _              => "Pump.Muted",
    };
}
