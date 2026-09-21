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

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// یک عددِ نوارِ خبرِ بالای صفحه (‎.tb-item‎) — برچسب، عدد و رنگِ همان عدد.
/// </summary>
public sealed partial class BannerItemViewModel : ObservableObject
{
    public BannerItemViewModel(string label, string colorKey)
    { Label = label; ColorKey = colorKey; }

    public string Label { get; }
    public string ColorKey { get; }

    [ObservableProperty] private string _value = "0";
}

/// <summary>
/// پوستهٔ برنامه: سربرگ، نوارِ خبر، نوارِ افقیِ بخش‌ها و ناحیهٔ محتوا.
/// همان چیدمانِ نسخهٔ وب — نوار بالا می‌ماند و فقط محتوا اسکرول می‌شود.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    public MainViewModel(AppSettings? settings = null)
    {
        _settings = settings ?? AppSettings.Load();

        Lock = new LockViewModel(AppHost.Current);
        // ⚠️ بخشِ آغازین بعد از ورود بار می‌شود، نه در سازنده. دو دلیل:
        //   ۱) پیش از ورود هیچ اجازه‌ای نداریم و لایهٔ سرویس درست هم رد می‌کند.
        //   ۲) وقتی در سازنده بار می‌شد، عددهای نوارِ بالا و داشبورد روی همان
        //      لحظهٔ صفرِ پیش از ورود می‌ماندند و کاربر «۰ افغانی» می‌دید.
        Lock.SignedIn += () =>
        {
            // ⚠️ ترتیب مهم است: اول بخشِ آغازین بار می‌شود، بعد پرده کنار
            // می‌رود. وگرنه کاربر یک لحظه پوستهٔ خالی را می‌بیند.
            Phase = AppPhase.Ready;
            _ = OpenStartSectionAsync();

            // ══ عکسِ روزانه (بندِ ۲۳) ══════════════════════════════════════════
            // همان ‎_autoDailyBackup‎ی نسخهٔ وب، ولی از فایلِ دیتابیس. روی نخِ
            // دیگر می‌رود تا باز شدنِ برنامه معطلِ آن نماند، و خودش هیچ استثنایی
            // بیرون نمی‌دهد — بکاپِ خودکار نباید ورودِ کاربر را بشکند.
            _ = Task.Run(() => AppHost.Current.Backup.SnapshotToday());

            // ══ خوراکِ اپِ کارمندان و ربات ═══════════════════════════════════
            // خواستهٔ صاحب ریپو: «هر تغییری که در اپ انجام می‌شود توی ربات هم
            // باشد.» پس از همین‌جا یک حلقهٔ آرامِ پس‌زمینه شروع می‌شود که عکسِ
            // برنامه را روی سرورِ خانگی تازه نگه می‌دارد.
            //
            // ⚠️ بعد از ورود، نه در سازنده: پیش از ورود هیچ اجازه‌ای نداریم و
            // لایهٔ سرویس درست هم رد می‌کند. اگر سروری تنظیم نشده باشد، این
            // حلقه بی‌صدا هیچ کاری نمی‌کند.
            AppHost.Current.Publisher.Start();

            // ══ همگام‌سازی با سرورِ حساب (VILL3N Sync v1) ════════════════════
            // بندِ ۳ی پرامپتِ ۲۲. صف در خودِ SQLite است، پس بسته شدنِ برنامه
            // چیزی را نمی‌برد و این حلقه فقط «از همان‌جا ادامه می‌دهد».
            // ⚠️ بعد از ورود، نه در سازنده — همان دلیلِ ناشر: پیش از ورود
            // هیچ اجازه‌ای نداریم.
            SoftLock.Install();
            var sync = AppHost.Current.Sync;
            sync.Changed += () => Dispatcher.UIThread.Post(TickSyncDot);
            sync.NoticeArrived += n => Dispatcher.UIThread.Post(() => ShowNotice(n));
            sync.Start();
            TickSyncDot();
            //  ⚠️ قفلِ نرم بی‌صدا نباشد: اگر اشتراک تمام شده (یا هفت روز
            //  مانده) کاربر باید بداند چرا نوشتن نمی‌شود، نه این‌که فکر کند
            //  برنامه خراب است. امروز این جمله خالی است، چون قفل‌ها بازند.
            NoticeText = SoftLock.Banner();

            // ══ پشتیبانِ هر شش ساعت روی سرورِ خانگی ═════════════════════════
            // خواستهٔ صاحب ریپو: «هر ۶ ساعت بک‌آپ برود به سرور و سه روز بماند؛
            // نرفت، به مدیر بگو.» شرحِ کامل در ‎BackupPusher‎.
            AppHost.Current.BackupToServer.Start();

            // ══ گذرِ دومِ گرم کردن ═══════════════════════════════════════════
            // پردهٔ لودینگ **پیش از** این اتفاق افتاده و صفحه‌ها را ساخته و
            // چیده است (‎WarmUpAsync‎، از سازندهٔ پنجره). آن‌جا اجازه‌ای نبود،
            // پس مسیرِ داده همین حالا و بی‌صدا یک بار گرم می‌شود.
            // ⚠️ گذرِ دومِ داده (‎WarmDataAsync‎) دیگر این‌جا صدا زده نمی‌شود:
            // خواندنِ دادهٔ سی‌ودو بخش پشتِ سرِ هم، درست بعد از رمز، همان «بعد
            // از رمز هنوز کند است» بود — با پنج سال داده ده ثانیه CPU روی نخِ
            // رابط. هر بخش سرِ اولین دیدارِ خودش خوانده می‌شود (ده‌ها تا صد
            // میلی‌ثانیه). خودِ تابع برای سنجش‌ها می‌ماند.
        };

        Sections = new ObservableCollection<SectionViewModel>(BuildSections(AppHost.Current));
        AttachSubSections(AppHost.Current);
        AllPages = Sections.Concat(Sections.SelectMany(s => s.SubSections)).ToList();
        // «🕘 تاریخچه»ی هر بخش — همان ‎openSectionHistory(kind)‎ی سایت: به بخشِ
        // تاریخچه‌ها می‌رود و همان‌جا تاریخچهٔ همان بخش را باز می‌کند.
        AppHost.Current.OpenHistory = OpenHistoryAsync;
        AppHost.Current.GoHome = OpenStartSectionAsync;
        // فهرست‌های نام‌دارِ پیشنهادِ خودکار — همان ‎datalist‎های سایت
        var host0 = AppHost.Current;
        Controls.Suggest.Provide("staff", async () => (await host0.Attendance.StaffAsync()).Select(x => x.Name ?? ""));
        Controls.Suggest.Provide("debtor", async () => (await host0.Debtors.ListAsync()).Select(x => x.Name));
        Themes = new ObservableCollection<PumpTheme>(PumpTheme.All);
        _selectedTheme = PumpTheme.ById(_settings.ThemeId);

        // اندازهٔ ماشین‌حساب از تنظیمات، و هر تغییرش با تأخیر به تنظیمات
        //  ⚠️ همان تأخیرِ ۶۰۰ میلی‌ثانیه‌ای، ولی حالا از راهِ یگانهٔ
        //  `AppSettings.SaveSoon` — این‌جا از ۱۴۰۵/۰۶/۲۵ دستیِ خودش را
        //  داشت (`CancellationTokenSource` + `Task.Delay`)، و وقتی همان
        //  الگو برای «آخرین بخش» هم لازم شد، یک‌جا شد. دو سازوکارِ هم‌کار
        //  یعنی روزی یکی‌شان اصلاح می‌شود و آن یکی نه.
        Calculator.Width = Math.Clamp(_settings.CalcWidth, CalculatorViewModel.MinW, CalculatorViewModel.MaxW);
        Calculator.Height = Math.Clamp(_settings.CalcHeight, CalculatorViewModel.MinH, CalculatorViewModel.MaxH);
        Calculator.IsLarge = _settings.CalcLarge;
        Calculator.SizeChanged += () =>
        {
            _settings.CalcWidth = Calculator.Width; _settings.CalcHeight = Calculator.Height; _settings.CalcLarge = Calculator.IsLarge;
            _settings.SaveSoon();
        };
    }

    // ══ پردهٔ لودینگِ آغاز ═══════════════════════════════════════════════════
    //
    // خواستهٔ صاحب ریپو: «موقعِ تازه باز کردنِ اپ باید یک لودینگ داشته باشه تا
    // همهٔ بخش‌ها و برنامه رو رندر کنه؛ بعدش بازگشت به صفحهٔ اصلی نباید تأخیری
    // داشته باشه.»
    //
    // ⚠️ چرا بعد از ورود و نه پیش از آن: پیش از ورود هیچ اجازه‌ای نداریم و
    // لایهٔ سرویس درست هم رد می‌کند — همان دلیلی که بارِ بخشِ آغازین هم به
    // بعد از ورود موکول شده.

    /// <summary>صفر تا یک — میلهٔ پیشرفتِ زیرِ انیمیشن.</summary>
    [ObservableProperty] private double _warmProgress;

    /// <summary>«۴۰٪» — تنها چیزی که روی پردهٔ لودینگ نوشته می‌شود.</summary>
    public string WarmPercentText => Shamsi.Money((int)Math.Round(WarmProgress * 100)) + "٪";

    partial void OnWarmProgressChanged(double v) => OnPropertyChanged(nameof(WarmPercentText));

    public Services.WarmUp Warm { get; } = new();

    /// <summary>
    /// ══ گرم کردنِ پشتِ پرده — بی هیچ ناوبری ═══════════════════════════════
    ///
    /// صفحه‌ها در یک قابِ نادیدنیِ پشتِ پرده فقط **چیده** می‌شوند.
    /// ‎Current‎ و ‎LastSection‎ دست نمی‌خورند و هیچ صفحهٔ محافظت‌شده‌ای پیش از
    /// احراز هویت جلوی چشم نمی‌آید — چراییِ کامل در <see cref="Services.WarmUp"/>.
    ///
    /// ⚠️ پاسِ چیدمان از پنجره می‌آید: ویومدل نباید به درختِ بصری دست بزند،
    /// ولی بی یک پاسِ واقعی، «گرم شدن» اتفاق نمی‌افتد.
    /// </summary>
    public async Task WarmUpAsync(Action layout)
    {
        if (Warm.Done) { Phase = AppPhase.Locked; return; }

        var all = AllPages;
        if (Avalonia.Application.Current?.DataTemplates.OfType<ViewLocator>().FirstOrDefault()
            is not { } locator || all.Count == 0)
        { Phase = AppPhase.Locked; return; }

        // ══ پرده فقط بخشِ آغازین را گرم می‌کند ═════════════════════════════
        // گزارشِ صاحب ریپو: «برنامه خیلی کند باز می‌شود؛ برنامه‌های دیگر زود
        // باز می‌شوند.» سنجشِ ‎startup‎: پرده با دیتابیسِ **خالی** چهار ثانیه
        // بود — بیست‌ونه صفحه پشتِ سرِ هم، پیش از آن‌که کاربر حتی صفحهٔ رمز را
        // ببیند. حالا زیرِ پرده فقط همان بخشی چیده می‌شود که پس از رمز جلوی
        // چشم می‌آید؛ بقیه پشتِ صفحهٔ قفل (‎WarmRestAsync‎)، همان چند ثانیه‌ای
        // که کاربر رمز می‌زند. اگر زودتر وارد شد، هر صفحه سرِ اولین دیدار
        // خودش ساخته می‌شود — مثلِ هر برنامهٔ دیگری.
        var first = Sections.FirstOrDefault(x => x.Id == _settings.LastSection) ?? Sections[0];
        try
        {
            await Warm.RunAsync(new[] { first }, locator, layout, p => WarmProgress = p);
        }
        finally
        {
            // ⚠️ چه گرم شده باشد چه نه، پرده باید برود و نوبتِ رمز برسد.
            // وگرنه یک خطای گرم کردن، برنامه را روی صفحهٔ لودینگ قفل می‌کرد.
            Phase = AppPhase.Locked;
        }

        // ══ بقیه پشتِ صفحهٔ قفل ═══════════════════════════════════════════
        // پوسته زیرِ قفل هم «دیده‌شونده» است (‎IsShellVisible‎) ولی صفحهٔ رمز
        // مات و رویش است. با اولین ‎Ready‎ می‌ایستد.
        _ = WarmRestAsync(locator, layout);
    }

    private async Task WarmRestAsync(ViewLocator locator, Action layout)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Warm.RunAsync(AllPages, locator, layout, _ => { }, () => Phase == AppPhase.Locked);
        }
        catch { /* گرم کردن یک تجمل است */ }
    }

    /// <summary>
    /// ══ گذرِ دومِ داده — بی‌صدا، پس از رمز ═══════════════════════════════════
    ///
    /// پرده پیش از رمز است، و پیش از ورود هیچ اجازه‌ای نداریم. پس مسیرِ داده
    /// همان یک بارِ گرانش را این‌جا می‌دهد — بی پرده، چون کاربر همان لحظه
    /// بخشِ خودش را می‌بیند و این پشتِ سرش می‌گذرد.
    ///
    /// ⚠️ و «خوانده‌شده» پس گرفته می‌شود، وگرنه هر بخش تا آخرِ عمرِ برنامه
    /// روی عکسِ همین لحظه می‌ماند.
    /// </summary>
    public async Task WarmDataAsync()
    {
        if (Warm.DataDone) return;
        Warm.MarkDataDone();

        foreach (var sec in AllPages)
        {
            if (sec.IsLoaded) continue;                  // بخشِ جلوی چشم، دست نخورد
            try
            {
                await sec.EnsureLoadedAsync();
                sec.IsLoaded = false;
            }
            catch { /* گرم کردن یک تجمل است */ }

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// همان بخشی که کاربر دفعهٔ پیش داخلش بود.
    ///
    /// ⚠️ اگر آن بخش قفل‌دار باشد، به جایش صفحهٔ اول باز می‌شود: سرِ بالا آمدنِ
    /// برنامه رمز پرسیدن یعنی کاربری که انصراف بدهد با صفحهٔ خالی روبه‌رو
    /// شود. قفل سرِ جایش هست؛ همان لحظه‌ای می‌پرسد که کاربر خودش روی آن بخش
    /// بزند.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>و همین قاعده برای قفلِ پلن هم هست — با یک دلیلِ سنگین‌تر.</b>
    /// صفحهٔ اولِ برنامه داشبورد است و داشبورد از امروز مالِ وی‌آی‌پی است.
    /// بی این خط، نصبی که هنوز اشتراک ندارد (یا اینترنتش قطع است) بالا
    /// می‌آمد و <b>صفحهٔ خالی</b> می‌دید — یعنی همان «برنامه خراب است»ی که
    /// اعتبار را می‌برد. پس اولین بخشی که پلن اجازه‌اش را می‌دهد باز
    /// می‌شود، بی هیچ پیامی.
    ///
    /// ⚠️ توست هم نمی‌دهیم: کاربر این‌جا روی چیزی نزده که بخواهد جوابی
    /// بگیرد. قفل همان لحظه‌ای حرف می‌زند که خودش روی آن بخش بزند.
    /// </remarks>
    public Task OpenStartSectionAsync()
    {
        var last = Sections.FirstOrDefault(s => s.Id == _settings.LastSection);
        if (last is null || Blocked(last)) last = Sections.FirstOrDefault(x => !Blocked(x)) ?? Sections[0];
        return GoAsync(last);

        static bool Blocked(SectionViewModel x) =>
            AppHost.Current.Locks.NeedsUnlock(x.Id)
            || (PlanFeatureOf(x.Id) is { } f && !Entitlements.Allows(f));
    }

    public ObservableCollection<SectionViewModel> Sections { get; }

    /// <summary>
    /// ══ دکمه‌های نوار — بی «پیام‌رسان» و «پروفایل» ═══════════════════════════
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «این دو بخش دو جا هستند؛ آن بالا
    /// هستند، این‌جا هم نمی‌خواهد باشند.» هر دو دکمهٔ خودشان را در سربرگ دارند
    /// (💬 پشتیبانی و 👤 پروفایل)، پس از نوار برداشته شدند. خودِ بخش‌ها سرِ
    /// جایشان در <see cref="Sections"/> می‌مانند (نوزدهم و بیستم — ‎NavOrderTests‎)
    /// و با همان دکمه‌های سربرگ باز می‌شوند.
    /// </summary>
    public IReadOnlyList<SectionViewModel> NavSections =>
        Sections.Where(s => s.Id is not ("chat" or "account")).ToList();

    /// <summary>
    /// چهار عددِ نوارِ بالا — همان ‎#topBanner‎: الباقیِ شرکت‌ها، قرضِ کل،
    /// مفادِ امروز و مصارفِ امروز. با هر بار عوض کردنِ بخش تازه می‌شوند.
    /// </summary>
    public ObservableCollection<BannerItemViewModel> Banner { get; } = new()
    {
        new BannerItemViewModel("شرکت ها تیل (الباقی)", "Pump.Accent"),
        new BannerItemViewModel("قرض کل", "Pump.Danger"),
        new BannerItemViewModel("مفاد امروز", "Pump.Ok"),
        new BannerItemViewModel("مصارف امروز", "Pump.Warn"),
    };
    public ObservableCollection<PumpTheme> Themes { get; }

    /// <summary>پیام‌رسان — برای دکمهٔ «💬 پشتیبانی»ی سربرگ و شمارهٔ نخوانده‌هایش.</summary>
    public ChatSectionViewModel Chat => Sections.OfType<ChatSectionViewModel>().First();

    /// <summary>
    /// «پروفایل» — برای دکمهٔ کنارِ تم در سربرگ: ورود با گوگل، VIP و روزهای
    /// مانده، و کدِ پمپ برای اپِ کارمندان. بخشِ بیستم است و بیستم می‌ماند.
    /// </summary>
    public AccountSectionViewModel Account => Sections.OfType<AccountSectionViewModel>().First();

    [ObservableProperty] private SectionViewModel? _current;

    /// <summary>
    /// چیزی که ناحیهٔ محتوا نشان می‌دهد — خودِ بخش، یا زیربخشِ بازش.
    /// جدا از ‎Current‎ است تا با بسته شدنِ زیربخش، بخش دوباره ساخته نشود.
    /// </summary>
    [ObservableProperty] private SectionViewModel? _content;

    /// <summary>زیربخشی باز است ⇒ نوارِ «‹ برگشت» بالای صفحه بیاید.</summary>
    public bool IsSubOpen => Current?.OpenSub is not null;

    /// <summary>
    /// ══ سربرگ و نوارها، وقتی حسابی باز است ═══════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «تو بخش‌هایی مثل قرض‌داران، تیل امانت، شرکت‌ها و ورق
    /// آن سربرگ و بخش‌های بالا را نشان می‌دهد، ولی وقتی وارد یک حسابِ قرض‌دار
    /// یا امانت یا شرکت می‌شوی نباید دیده شوند، چون جا می‌گیرند.»
    ///
    /// در سایت هم دقیقاً همین است: ‎#personModal .modal‎ صریحاً
    /// ‎width:100%;height:100%‎ می‌گیرد و روی سربرگ و نوارِ آمار و نوارِ بخش‌ها
    /// می‌افتد. پس این‌جا هم با باز شدنِ حسابِ درونِ بخش، هر سه می‌روند و کلِ
    /// پنجره مالِ خودِ حساب می‌شود.
    /// </summary>
    /// <summary>
    /// ماشین‌حسابِ شناور — جزوِ بخش‌ها نیست و هر جای برنامه با ‎Ctrl+K‎ یا
    /// دکمهٔ سربرگ باز می‌شود. خواستهٔ صریحِ صاحب ریپو: «ماشین‌حساب داینامیک».
    /// </summary>
    public CalculatorViewModel Calculator { get; } = new();

    public bool IsChromeVisible => Content?.IsPageOpen != true;

    /// <summary>نوشتهٔ دکمهٔ برگشت — «‹ برگشت به قرض‌داران».</summary>
    public string BackText => "‹ برگشت به " + (Current?.Title ?? "");
    [ObservableProperty] private PumpTheme _selectedTheme;
    [ObservableProperty] private string _clock = "";

    /// <summary>
    /// ══ چراغِ سرور در سربرگ ══════════════════════════════════════════════════
    /// سبز = به سرورِ خانگی وصل‌ایم؛ سرخ = سرور تنظیم شده ولی جواب نمی‌دهد؛
    /// خاکستری = سروری تنظیم نشده. با تیکِ ساعتِ پنجره تازه می‌شود
    /// (<see cref="TickServerDot"/>) — فقط خواندنِ دو ویژگی، هیچ درخواستی.
    /// </summary>
    [ObservableProperty] private string _serverDotBrushKey = "Pump.Muted";
    [ObservableProperty] private string _serverDotReason = "سرور تنظیم نشده";

    public void TickServerDot()
    {
        var sync = AppHost.Current.PublisherIfStarted;
        string key, why;
        if (sync is null || !sync.Configured)
        {
            key = "Pump.Muted";
            why = "سرورِ خانگی هنوز تنظیم نشده — از پروفایل وارد شوید";
        }
        else if (sync.Connected)
        {
            key = "Pump.Ok";
            why = sync.Mode == Services.HomeSyncMode.Station
                ? "به سرورِ خانگی وصل است"
                : "به سرورِ خانگی وصل است (درِ قدیمی)";
        }
        else
        {
            key = "Pump.Danger";
            //  ⚠️ «آخرین وصل» را می‌گوید تا کاربر بداند همین حالا قطع شده یا
            //  از اول وصل نشده — و این‌که هر پنج ثانیه خودش دوباره می‌گردد.
            var last = sync.LastLinkedAt is { } t ? $" · آخرین وصل: {t:HH:mm}" : "";
            why = "سرورِ خانگی جواب نمی‌دهد — خاموش است یا شبکه قطع است" + last
                + " · هر پنج ثانیه خودش دوباره می‌گردد؛ برای بررسیِ همین حالا کلیک کنید";
        }
        if (key != ServerDotBrushKey) ServerDotBrushKey = key;
        if (why != ServerDotReason) ServerDotReason = why;
    }

    // ══ ● چراغِ دوم: ابر ═══════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۱): «ببین برنامه‌ها چرا به سرور وصل
    //  نمی‌شوند… و هیچ کلکِ دروغی نباشد که بگوید وصل است.»
    //
    //  ⚠️ **دو سرور داریم و یکی نیستند.** چراغِ اول مالِ سرورِ **خانگی** است
    //  (دفتر و دادهٔ زنده، در شبکهٔ خودِ پمپ). این یکی مالِ **ابر** است
    //  (حساب و اشتراک). تا امروز فقط اولی چراغ داشت، پس کاربر یک سبز
    //  می‌دید و گمان می‌کرد همه‌چیز وصل است — در حالی که ممکن بود برنامه
    //  هیچ‌وقت به ابر نرسیده باشد.
    //
    //  ⛔ **سبز فقط با جوابِ واقعی.** این‌جا هیچ تصمیمی گرفته نمی‌شود؛ فقط
    //  `CloudLink.Reach` خوانده می‌شود که خودش از `SendFull` پر می‌شود.
    //  «هنوز نپرسیده‌ایم» خاکستری است، نه سبز.
    //  ⛔ و هیچ نام و نشانیِ سروری نوشته نمی‌شود — همان قاعدهٔ چراغِ اول.

    [ObservableProperty] private string _cloudDotBrushKey = "Pump.Muted";
    [ObservableProperty] private string _cloudDotReason = "هنوز با سرورِ حساب تماس نگرفته‌ایم";

    public void TickCloudDot()
    {
        string key, why;
        switch (Services.CloudLink.Reach)
        {
            case Services.CloudReach.Online:
                key = "Pump.Ok";
                why = "به سرورِ حساب وصل است"
                    + (Services.CloudLink.CloudOkAt is { } at ? $" · آخرین جواب: {at:HH:mm}" : "");
                break;

            case Services.CloudReach.Offline:
                key = "Pump.Danger";
                var seen = Services.CloudLink.CloudOkAt is { } ok ? $" · آخرین جوابِ درست: {ok:HH:mm}" : "";
                var reason = Services.CloudLink.CloudWhy.Length > 0
                    ? " — " + Services.CloudLink.CloudWhy : "";
                why = "به سرورِ حساب نمی‌رسیم" + reason + seen
                    + " · هر دقیقه خودش دوباره می‌گردد؛ برای بررسیِ همین حالا کلیک کنید";
                break;

            default:
                key = "Pump.Muted";
                why = "هنوز با سرورِ حساب تماس نگرفته‌ایم — برای بررسیِ همین حالا کلیک کنید";
                break;
        }
        if (key != CloudDotBrushKey) CloudDotBrushKey = key;
        if (why != CloudDotReason) CloudDotReason = why;
    }

    // ══ ● چراغِ سوم: همگام‌سازی — در نوارِ **پایینِ** پنجره ═══════════════
    //
    //  بندِ ۱۳ی پرامپتِ ۲۲: «نمایشِ وضعیتِ Sync در نوارِ وضعیتِ پایینِ پنجره
    //  (سبز/زرد/خاکستری/قرمز).»
    //
    //  ⛔ **هیچ نام و نشانیِ سروری نوشته نمی‌شود** — همان قاعدهٔ دو چراغِ
    //  سربرگ. دلیل فقط در ‎ToolTip‎ است.
    //  ⚠️ و هیچ تصمیمی این‌جا گرفته نمی‌شود: فقط ‎SyncEngine‎ خوانده می‌شود،
    //  که خودش از جوابِ واقعیِ سرور پر می‌شود.

    [ObservableProperty] private string _syncDotBrushKey = "Pump.Muted";
    [ObservableProperty] private string _syncDotReason = "همگام‌سازی هنوز شروع نشده";
    [ObservableProperty] private string _syncDotText = "همگام‌سازی";

    public void TickSyncDot()
    {
        var sync = AppHost.Current.SyncIfStarted;
        if (sync is null)
        {
            SyncDotBrushKey = "Pump.Muted";
            SyncDotReason = "همگام‌سازی هنوز شروع نشده";
            SyncDotText = "همگام‌سازی";
            return;
        }

        var key = sync.Light switch
        {
            SyncLight.Synced => "Pump.Ok",
            SyncLight.Queued => "Pump.Warn",
            SyncLight.Failed => "Pump.Danger",
            _ => "Pump.Muted",
        };
        var text = sync.Light switch
        {
            SyncLight.Synced => "همگام",
            SyncLight.Queued => sync.Queued > 0 ? $"{sync.Queued} در صف" : "در صف",
            SyncLight.Failed => "همگام نشد",
            _ => "همگام‌سازی",
        };

        if (key != SyncDotBrushKey) SyncDotBrushKey = key;
        if (text != SyncDotText) SyncDotText = text;
        if (sync.Reason != SyncDotReason) SyncDotReason = sync.Reason;
    }

    /// <summary>کلیکِ چراغِ همگام‌سازی — «الان همگام کن».</summary>
    [RelayCommand]
    private async Task SyncNowAsync()
    {
        AppHost.Current.Toast("در حالِ همگام‌سازی…", ToastKind.Info);
        var ok = await AppHost.Current.Sync.SyncNowAsync();
        TickSyncDot();
        AppHost.Current.Toast(
            ok ? "✅ همگام شد" : "⏳ " + AppHost.Current.Sync.Reason,
            ok ? ToastKind.Ok : ToastKind.Warn);
    }

    // ══ بنرِ بالای صفحه — اعلانِ مدیر و قفلِ نرم ═════════════════════════

    /// <summary>جملهٔ بنر — خالی یعنی بنری نیست.</summary>
    [ObservableProperty] private string _noticeText = "";

    public bool HasNotice => NoticeText.Length > 0;

    partial void OnNoticeTextChanged(string v) => OnPropertyChanged(nameof(HasNotice));

    /// <summary>بنر را می‌بندد — تا اعلانِ بعدی.</summary>
    [RelayCommand]
    private void CloseNotice() => NoticeText = SoftLock.Banner();

    /// <summary>
    /// اعلانِ تازه: هم بنرِ داخلِ برنامه، هم اعلانِ خودِ ویندوز.
    ///
    /// ⚠️ هر دو از <b>یک</b> جا می‌آیند. قاعدهٔ جدا ننویسید، وگرنه روزی
    /// یکی می‌آید و آن یکی نه.
    /// </summary>
    private void ShowNotice(CloudNotice n)
    {
        NoticeText = "📣 " + n.Title + (n.Body.Length > 0 ? " — " + n.Body : "");
        AppHost.Current.Toast(NoticeText, ToastKind.Info);
        NativeNotice.Show(n.Title.Length > 0 ? n.Title : "پمپ یعقوبی", n.Body, WindowHandle);
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
        var (up, ver) = await Services.CloudLink.CloudHealthAsync();
        TickCloudDot();
        var v = ver.Length > 0 ? $" (نسخهٔ {ver})" : "";
        AppHost.Current.Toast(
            up ? "✅ سرورِ حساب جواب داد" + v
               : "❌ به سرورِ حساب نرسیدیم — اینترنت و بالا بودنِ سرور را ببینید",
            up ? ToastKind.Ok : ToastKind.Error);
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
        if (sync is null || !sync.Configured)
        {
            AppHost.Current.Toast("سرورِ خانگی تنظیم نشده — از «پروفایل» وارد شوید", ToastKind.Info);
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



    // ══ سه حالتِ مستقل، و فقط یکی در هر لحظه ══════════════════════════════
    //
    // گزارشِ صاحب ریپو: «هنگام اجرای لودینگ، کاربر به بخش‌های مختلف برنامه
    // منتقل می‌شود، صفحات مختلف نمایش داده می‌شوند و در نهایت دوباره به صفحه
    // قفل برمی‌گردد. این رفتار کاملاً اشتباه است.»
    //
    // ریشه دو تا بود:
    //
    //   ۱) پردهٔ لودینگ ‎Background="{DynamicResource Pump.Bg}"‎ داشت و
    //      **چنین کلیدی در تم وجود ندارد** (نامِ درست ‎Pump.AppBg‎ است).
    //      ‎DynamicResource‎ی که پیدا نشود بی‌صدا ‎null‎ می‌شود، پس پرده
    //      کاملاً شفاف بود و همه‌چیز از پشتش دیده می‌شد. ⚠️ آوالونیا برای
    //      منبعِ نبوده نه خطا می‌دهد نه هشدار.
    //
    //   ۲) و بدتر از آن، خودِ گرم کردن با ‎GoAsync‎ در بخش‌ها **می‌گشت** —
    //      یعنی حتی با پردهٔ درست هم، صفحه‌های محافظت‌شده پیش از احراز هویت
    //      ساخته و نشان داده می‌شدند.
    //
    // حالا سه حالت هست، صریح و مستقل، و هیچ‌کدام از دلِ آن یکی حساب نمی‌شود:
    //
    //     Starting ⇒ فقط پردهٔ لودینگ
    //     Locked   ⇒ فقط صفحهٔ رمز
    //     Ready    ⇒ فقط خودِ برنامه
    //
    // ⚠️ و گرم کردن دیگر «رفتن به بخش» نیست: صفحه‌ها در یک قابِ نادیدنیِ
    // پشتِ پرده فقط **چیده** می‌شوند. ‎Current‎ دست نمی‌خورد، هیچ مسیری عوض
    // نمی‌شود، و چون هنوز وارد نشده‌ایم هیچ دادهٔ محافظت‌شده‌ای هم در آن‌ها
    // نیست.

    /// <summary>حالتِ برنامه — هر لحظه دقیقاً یکی.</summary>
    public enum AppPhase { Starting, Locked, Ready }

    [ObservableProperty] private AppPhase _phase = AppPhase.Starting;

    partial void OnPhaseChanged(AppPhase v)
    {
        foreach (var n in new[] { nameof(IsStarting), nameof(IsLockVisible),
                                  nameof(IsShellVisible), nameof(IsLocked),
                                  nameof(RoleText), nameof(RoleBrushKey) })
            OnPropertyChanged(n);
        //  دکمهٔ «پروفایل»ِ سربرگ همان لحظهٔ ورود درست بگوید VIP هست یا نه
        if (v == AppPhase.Ready) Account.RefreshAll();
    }

    /// <summary>پردهٔ لودینگِ آغاز — فقط در ‎Starting‎.</summary>
    public bool IsStarting => Phase == AppPhase.Starting;

    /// <summary>صفحهٔ رمز — فقط در ‎Locked‎.</summary>
    public bool IsLockVisible => Phase == AppPhase.Locked;

    /// <summary>
    /// پوستهٔ برنامه.
    ///
    /// ⚠️ حینِ ‎Starting‎ هم «دیده‌شونده» است — ولی زیرِ پردهٔ **مات**ِ لودینگ،
    /// پس کاربر هیچ‌وقت نمی‌بیندش. دلیلش در <see cref="Services.WarmUp"/>
    /// نوشته: نمایی که در درختِ بصری نباشد اصلاً چیده نمی‌شود و گرم کردن فقط
    /// ادایش را درمی‌آورد.
    ///
    /// ⚠️ زیرِ صفحهٔ قفل هم هست — تا بقیهٔ صفحه‌ها همان چند ثانیه‌ای که کاربر
    /// رمز می‌زند گرم شوند (‎WarmRestAsync‎). صفحهٔ رمز مات است و روی همه؛
    /// ‎WarmAudit‎ همین را می‌سنجد. داده‌ای هم در کار نیست: تا رمز نخورده هیچ
    /// بخشی خوانده نمی‌شود.
    /// </summary>
    public bool IsShellVisible => true;

    /// <summary>تاریخِ شمسیِ امروز — خطِ اولِ بلوکِ تاریخِ سربرگ.</summary>
    public string TodayText => Shamsi.DayName(DateTime.Now) + "، " + Shamsi.Today();

    /// <summary>روز عوض شد (نیمه‌شب): تاریخِ سربرگ و عددهای نوار از نو.</summary>
    public void DayChanged()
    {
        OnPropertyChanged(nameof(TodayText));
        QueueBannerRefresh();
    }

    public LockViewModel Lock { get; }

    /// <summary>
    /// «جدولی که همین حالا جلوی کاربر است» — همان ‎_kbCtx()‎ِ نسخهٔ وب.
    /// اولویت با صفحهٔ بازِ داخلِ بخش است (حسابِ شخص، شرکت، ورق، امانت)؛
    /// اگر باز نباشد، خودِ بخش. هیچ جدولِ دیگری دست نمی‌خورد.
    /// </summary>
    public IRowBatchHost? RowHost =>
        ActiveSection?.ActivePage as IRowBatchHost ?? ActiveSection as IRowBatchHost;

    /// <summary>
    /// بخشی که واقعاً جلوی چشمِ کاربر است: اگر زیربخشی باز باشد همان، وگرنه
    /// خودِ بخش. میانبرها و «جدولِ فعال» باید همین را ببینند، نه بخشِ پشتِ آن.
    /// </summary>
    public SectionViewModel? ActiveSection => Current?.OpenSub ?? Current;

    /// <summary>پیام‌های کوتاهِ پایینِ صفحه.</summary>
    public Services.ToastService Toasts => AppHost.Current.Toasts;

    /// <summary>خروج و برگشت به صفحهٔ قفل — بی آن‌که برنامه بسته شود.</summary>
    [RelayCommand]
    private void SignOut()
    {
        AppHost.Current.Auth.SignOut();
        // ⚠️ به ‎Locked‎ برمی‌گردیم، نه به ‎Starting‎: لودینگ یک بار در عمرِ
        // اجرای برنامه است و خروج نباید دوباره راهش بیندازد.
        Phase = AppPhase.Locked;
    }

    partial void OnSelectedThemeChanged(PumpTheme value)
    {
        ThemeManager.Apply(value);
        _settings.ThemeId = value.Id;
        //  تعویضِ تم خودش یک خبرِ بزرگ به کلِ درخت است (~۴۰۰ms با پنج سال
        //  داده)؛ یک `fsync` هم رویش گذاشتن فقط همان را درازتر می‌کرد.
        _settings.SaveSoon();
    }

    /// <summary>
    /// ══ چرا ‎AllowConcurrentExecutions‎ ═══════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «گاهی کلیکِ اولِ تب هیچ تغییری نمی‌دهد و باید دوباره
    /// کلیک کرد.»
    ///
    /// ریشه‌اش این‌جاست و «گاهی» هم دقیقاً به همین خاطر است:
    /// ‎[RelayCommand]‎ روی یک متدِ ‎async‎ یک ‎AsyncRelayCommand‎ می‌سازد که
    /// <b>پیش‌فرضش اجرای هم‌زمان را نمی‌پذیرد</b> — یعنی تا وقتی یک اجرا تمام
    /// نشده، ‎CanExecute‎ی همهٔ دکمه‌های نوار ‎false‎ است و کلیک <b>بلعیده</b>
    /// می‌شود.
    ///
    /// و این متد ‎await‎ دارد: ‎EnsureLoadedAsync‎ی یک بخشِ پرداده می‌تواند
    /// نزدیکِ یک ثانیه طول بکشد. هر کلیکی در آن فاصله می‌پرید — پس کاربر باید
    /// دوباره می‌زد.
    ///
    /// ⚠️ مسابقه‌ای هم درست نمی‌شود: ‎Current‎ و ‎SyncContent()‎ <b>پیش از</b>
    /// اولین ‎await‎ اجرا می‌شوند، پس آخرین کلیک همان چیزی است که روی صفحه
    /// می‌نشیند، و ‎EnsureLoadedAsync‎ خودش با ‎IsLoaded‎ دوبار بار نمی‌کند.
    /// </summary>
    private async Task OpenHistoryAsync(string kind)
    {
        //  ⛔ همان قفلِ پلن — «برای هیچ بخشی تاریخچه‌ای نباشه». دکمهٔ تاریخچهٔ
        //  گاوصندوق/صرافی/امانت هم از همین در رد می‌شود، نه فقط خودِ بخش.
        if (!Entitlements.Gate(AppHost.Current, Entitlements.History)) return;
        if (Sections.FirstOrDefault(x => x.Id == "history") is not Sections.HistorySectionViewModel h) return;
        await GoAsync(h);
        await h.OpenAsync(kind);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task GoAsync(SectionViewModel? s)
    {
        if (s is null) return;

        // ⛔ بخشِ قفل‌دار (مفاد/ضرر) بی رمز باز نمی‌شود — شرحش در ‎UnlockAsync‎.
        if (!await UnlockAsync(s)) return;

        // زدنِ دکمهٔ همان بخشی که باز است یعنی «تازه‌اش کن» — نه «هیچ کاری نکن».
        // پیش از این این‌جا برمی‌گشتیم و نتیجه‌اش این بود که عددهای نوارِ بالا
        // روی همان لحظهٔ بارِ اول می‌ماندند.
        if (ReferenceEquals(s, Current))
        {
            // زدنِ دکمهٔ بخشِ باز یعنی «از اول» — پس اگر زیربخشی باز مانده،
            // بسته می‌شود. وگرنه کاربر روی «قرض‌داران» می‌زد و باز هم صفحهٔ
            // «قرض‌های کهنه» جلویش می‌ماند.
            s.OpenSub = null;
            await s.OnActivatedAsync();
            QueueBannerRefresh();
            return;
        }
        if (Current is not null)
        {
            Current.PropertyChanged -= OnSectionPropertyChanged;
            Current.IsActive = false;
            Current.OnDeactivated();
        }
        s.IsActive = true;
        Current = s;                       // نمونه‌ها زنده می‌مانند: هیچ ساختِ دوباره‌ای نیست
        s.PropertyChanged += OnSectionPropertyChanged;
        SyncContent();
        _settings.LastSection = s.Id;
        //  ⛔ **نوشتنِ «آخرین بخش» روی نخِ رابط نمی‌ماند.** گزارشِ صاحب ریپو
        //  (۱۴۰۵/۰۷/۰۵): «هر بخش رو باز می‌کنم جدول‌ها یک ثانیه بعد میان.»
        //  ریشه دقیقاً همین خط بود: `Save()` یک نوشتنِ بادوام است
        //  (`Flush(true)` + `File.Replace`)، و این‌جا **بینِ** نشان دادنِ صفحه
        //  و خواندنِ داده‌اش می‌نشست — یعنی ردیف‌ها پشتِ یک `fsync` معطل
        //  می‌ماندند. شرحِ کامل بالای `AppSettings.SaveSoon`.
        _settings.SaveSoon();
        // ⚠️ اگر همین حالا خوانده شد و فعال‌سازی همان خواندن است، دوباره نه
        var fresh = await s.EnsureLoadedAsync();
        //  ⛔ و اگر فعال‌سازی فقط دفتر را می‌خواند و از آخرین بار **هیچ
        //  نوشتنی** نشده، اصلاً صدا زده نمی‌شود — شرحش بالای
        //  `SectionViewModel.ActivationOnlyReadsDb`.
        if (!(fresh && s.ActivationRepeatsLoad) && !s.ActivationCanBeSkipped)
            await s.OnActivatedAsync();
        s.MarkActivationSeen();
        QueueBannerRefresh();
    }

    /// <summary>
    /// ══ رفتن به بخشی با شناسه ══════════════════════════════════════════════
    /// همان ‎showSection('x')‎ی نسخهٔ وب — و درست مثلِ آن، شناسه می‌تواند
    /// زیربخش هم باشد («oldloans»، «monthreport»، «datamgmt»…). آن‌وقت اول
    /// بخشِ میزبان باز می‌شود و بعد زیربخش رویش.
    ///
    /// ⚠️ بی این، لینک‌های داشبورد که به زیربخش‌ها می‌روند بی‌صدا هیچ کاری
    /// نمی‌کردند: فهرستِ ‎Sections‎ دیگر آن‌ها را ندارد.
    /// </summary>
    public async Task GoByIdAsync(string id)
    {
        if (Sections.FirstOrDefault(x => x.Id == id) is { } top) { await GoAsync(top); return; }

        foreach (var parent in Sections)
            if (parent.SubSections.FirstOrDefault(x => x.Id == id) is { } sub)
            {
                await GoAsync(parent);
                parent.OpenSub = sub;
                return;
            }
    }

    /// <summary>
    /// باز و بسته شدنِ زیربخش. ناحیهٔ محتوا عوض می‌شود و زیربخشِ تازه — مثلِ
    /// خودِ بخش‌ها — بارِ اولش را همان لحظه می‌گیرد، نه در سازنده.
    /// </summary>
    private void OnSectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SectionViewModel.OpenSub)) return;

        // ⛔ زیربخشِ قفل‌دار («زیان ناشی از افزایش قیمت») اول بسته می‌ماند و
        // بعد از رمزِ درست خودش باز می‌شود. اگر همین‌جا نشان داده می‌شد،
        // رمز پرسیدن پس از دیده شدنِ داده بی‌معنا بود.
        if (Current?.OpenSub is { } locked && AppHost.Current.Locks.NeedsUnlock(locked.Id))
        {
            Current.OpenSub = null;
            LastSubOpen = UnlockThenShowAsync(Current, locked);
            return;
        }

        SyncContent();
        if (Current?.OpenSub is { } sub) LastSubOpen = OpenSubAsync(sub);
    }

    /// <summary>آخرین باز شدنِ زیربخش — تا سنجش‌ها بتوانند منتظرِ همان بمانند، نه دوباره خودشان بار کنند.</summary>
    public Task? LastSubOpen { get; private set; }

    /// <summary>بخشی که همین حالا محتوا است — تا باز و بسته شدنِ حسابش را بشنویم.</summary>
    private SectionViewModel? _watchedContent;

    private void OnContentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SectionViewModel.IsPageOpen))
            OnPropertyChanged(nameof(IsChromeVisible));

        // حساب/شرکت/ورقِ باز عوض شد (‎Person‎، ‎Overlay‎، ‎Page‎…) ⇒ همان قاعده:
        // صفحه‌ای که رفت، ردیف‌هایش هم می‌روند.
        Controls.ExcelGrid.NotifyPagesChanged();
    }

    /// <summary>
    /// ══ قفلِ بخش ══════════════════════════════════════════════════════════
    /// بخشی که در «تنظیمات › رمزها و کد» رمز گرفته باشد، بارِ اولِ هر اجرا
    /// رمزش را می‌پرسد. ‎false‎ یعنی «باز نشد» — و آن‌وقت هیچ چیزی از آن بخش
    /// روی صفحه نمی‌آید.
    ///
    /// ⚠️ رمز **پیش از** نشان دادنِ صفحه پرسیده می‌شود، نه بعدش: رمزی که پس
    /// از دیده شدنِ داده پرسیده شود هیچ چیزی را نگه نداشته است.
    /// </summary>
    /// <summary>
    /// ══ قفلِ پلن — «استاندارد این بخش را ندارد» ═══════════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۳۰) دربارهٔ پلنِ استاندارد: «مفاد و
    /// ضرر براش نشون داده نشه… تاریخچه‌ها هم بسته بشه و برای هیچ بخشی
    /// تاریخچه‌ای نباشه… و داشبورد هم قفل باشه.»
    ///
    /// ⛔ <b>این با قفلِ رمزِ بخش یکی نیست</b> و جایش را نمی‌گیرد: آن یکی
    /// خواستهٔ خودِ صاحبِ پمپ است (رمز روی «مفاد» و «زیانِ افزایشِ قیمت»)،
    /// این یکی مرزِ پلن است. هر دو می‌دوند و ترتیبشان مهم نیست.
    ///
    /// ⚠️ <b>داده دست نمی‌خورد.</b> فقط درِ صفحه بسته است — «اطلاعاتشون باشن
    /// ولی دیده نتونن، و اگه بار دیگه وی‌آی‌پی یا دائمی رو خرید قفلِ اون‌ها
    /// باز بشه». پس هیچ محاسبه‌ای خاموش نمی‌شود و نرخِ اتحادیه هم — که جای
    /// دیگری هم به کار می‌رود — آسیب نمی‌بیند.
    /// </summary>
    private static string? PlanFeatureOf(string? id) => id switch
    {
        "profit"    => Entitlements.Profit,
        "priceloss" => Entitlements.Profit,   // زیربخشِ همان «ضرر»
        "history"   => Entitlements.History,
        "dashboard" => Entitlements.Dashboard,
        _ => null,
    };

    /// <summary>پلن این بخش را دارد؟ نداشت، خودش به کاربر می‌گوید چرا.</summary>
    private static bool PlanAllows(SectionViewModel s)
    {
        var feature = PlanFeatureOf(s.Id);
        if (feature is null) return true;
        //  ⚠️ `Gate` خودش توست می‌دهد — دکمه‌ای که زده شود و هیچ اتفاقی
        //  نیفتد، در چشمِ کاربر باگ است نه قفل.
        return Entitlements.Gate(AppHost.Current, feature);
    }

    private async Task<bool> UnlockAsync(SectionViewModel s)
    {
        if (!PlanAllows(s)) return false;

        var locks = AppHost.Current.Locks;
        if (!locks.NeedsUnlock(s.Id)) return true;

        var pw = await Dialogs.PromptAsync("🔒 " + s.Title,
                                           "این بخش رمز دارد. رمزش را بزنید.");
        if (pw is null) return false;

        if (!locks.Unlock(s.Id, pw))
        {
            AppHost.Current.Toast("❌ رمزِ این بخش درست نیست", ToastKind.Error);
            return false;
        }
        return true;
    }

    /// <summary>رمز درست بود ⇒ همان زیربخش دوباره باز می‌شود (این‌بار بی قفل).</summary>
    private async Task UnlockThenShowAsync(SectionViewModel parent, SectionViewModel sub)
    {
        if (!await UnlockAsync(sub)) return;
        parent.ShowSub(sub);
    }

    private async Task OpenSubAsync(SectionViewModel sub)
    {
        try
        {
            var fresh = await sub.EnsureLoadedAsync();
            if (!(fresh && sub.ActivationRepeatsLoad) && !sub.ActivationCanBeSkipped)
                await sub.OnActivatedAsync();
            sub.MarkActivationSeen();
        }
        catch (Exception ex) { AppHost.Current.Toast("باز نشد: " + ex.Message, ToastKind.Error); }
    }

    /// <summary>
    /// همهٔ بخش‌ها و زیربخش‌ها — ناحیهٔ محتوا همهٔ اینها را با هم نگه می‌دارد و
    /// فقط یکی‌شان را نشان می‌دهد. چراییِ «نگه می‌دارد» در
    /// <see cref="SectionViewModel.IsShown"/> نوشته شده.
    /// </summary>
    public IReadOnlyList<SectionViewModel> AllPages { get; private set; } =
        Array.Empty<SectionViewModel>();

    private void SyncContent()
    {
        Content = Current?.OpenSub ?? Current;

        // فقط یکی دیده می‌شود؛ بقیه سرِ جایشان می‌مانند
        foreach (var p in AllPages) p.IsShown = ReferenceEquals(p, Content);

        if (!ReferenceEquals(_watchedContent, Content))
        {
            if (_watchedContent is not null)
                _watchedContent.PropertyChanged -= OnContentPropertyChanged;
            _watchedContent = Content;
            if (_watchedContent is not null)
                _watchedContent.PropertyChanged += OnContentPropertyChanged;
        }

        // صفحهٔ دیده‌شونده عوض شد ⇒ جدول‌های پنهان ردیف‌هایشان را رها کنند
        // (شرحش بالای ‎ExcelGrid.NotifyPagesChanged‎).
        Controls.ExcelGrid.NotifyPagesChanged();

        OnPropertyChanged(nameof(IsChromeVisible));
        OnPropertyChanged(nameof(IsSubOpen));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(ActiveSection));
        OnPropertyChanged(nameof(RowHost));
    }

    /// <summary>
    /// ══ خواندنِ دوبارهٔ همهٔ بخش‌ها ═══════════════════════════════════════════
    /// بعد از کاری که کلِ دیتابیس را عوض می‌کند — بازگردانیِ بکاپ، آوردنِ دادهٔ
    /// نسخهٔ وب، یا برگرداندنِ چیزی از سطلِ زباله.
    ///
    /// ⚠️ نمونهٔ بخش‌ها زنده می‌مانند (تا چیدمان و جای اسکرول از دست نرود)، پس
    /// بی این، صفحه‌ها عددِ دیتابیسِ **قبلی** را نشان می‌دهند و کاربر خیال
    /// می‌کند بازگردانی نگرفته است.
    ///
    /// بخشی که همین حالا باز است، آخر و همان‌جا تازه می‌شود.
    /// </summary>
    public async Task ReloadAllAsync()
    {
        // زیربخش‌ها هم بخش‌اند و دادهٔ خودشان را دارند — اگر این‌جا از قلم
        // بیفتند، «قرض‌های کهنه» بعد از بازگردانیِ بکاپ عددِ دیتابیسِ قبلی را
        // نشان می‌دهد.
        foreach (var s in Sections.Concat(Sections.SelectMany(x => x.SubSections)))
        {
            // بخشی که هرگز باز نشده، بارِ اولش را همان موقعِ ورودِ کاربر
            // می‌گیرد — این‌جا فقط باید «کهنه» علامت بخورد.
            if (!s.IsLoaded || ReferenceEquals(s, Content)) continue;
            s.IsLoaded = false;
        }

        if (Content is not null)
        {
            try { await Content.ReloadAsync(); } catch { }
            try { await Content.OnActivatedAsync(); } catch { }
        }

        await RefreshBannerAsync();
    }

    private bool _bannerBusy, _bannerAgain;

    /// <summary>
    /// ══ نوارِ بالا جلوی باز شدنِ بخش را نگیرد ═══════════════════════════════
    ///
    /// چهار عددِ نوار به هیچ بخشی ربط ندارند؛ ولی چون جابه‌جایی منتظرشان
    /// می‌ماند، هزینه‌شان به **هر** باز کردنِ بخش اضافه می‌شد — در سنجش حدودِ
    /// ۱۶۰ ms روی هر جابه‌جایی، یعنی همان مکثی که خواستهٔ صاحب ریپو نبود.
    ///
    /// حالا بخش فوراً باز می‌شود و نوار یک لحظه بعد خودش تازه می‌شود.
    ///
    /// ⚠️ اگر کاربر تند تند بخش عوض کند، تازه‌سازی‌ها روی هم نمی‌ریزند: تا یکی
    /// در جریان است بقیه فقط «یک‌بارِ دیگر» را علامت می‌زنند و در پایان همان
    /// یک‌بار اجرا می‌شود — پس عددِ آخر همیشه عددِ درست است.
    /// </summary>
    public void QueueBannerRefresh()
    {
        if (_bannerBusy) { _bannerAgain = true; return; }
        _bannerBusy = true;
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                do
                {
                    _bannerAgain = false;
                    // ⚠️ بی این ‎try‎، یک خطای گذرا در خواندن، استثنای
                    // «مشاهده‌نشده» می‌شد و برنامه را می‌بست.
                    try { await RefreshBannerAsync(); } catch { }
                } while (_bannerAgain);
            }
            finally { _bannerBusy = false; }
        }
    }

    /// <summary>
    /// چهار عددِ نوارِ بالا — همان ‎updateBanner‎ِ نسخهٔ وب. فقط خواندنی است و
    /// هر بار که کاربر بخشی را باز می‌کند تازه می‌شود.
    /// </summary>
    /// <summary>نسخهٔ داده‌ای که نوار با آن حساب شده — تا با هر جابه‌جایی از نو نخواند.</summary>
    private long _bannerVersion = -1;

    public async Task RefreshBannerAsync()
    {
        var host = AppHost.Current;
        var calc = new DashboardService();

        // ⚠️ سنجشِ «پنج سال داده»: این تابع با هر بار عوض کردنِ بخش همهٔ
        // ردیف‌های شرکت‌ها و همهٔ گزارش‌های پارچه را می‌خواند — یک ثانیه روی
        // **هر** جابه‌جایی، حتی به بخشی خالی مثلِ دوربین‌ها. تا چیزی ذخیره
        // نشده، عددها همان‌اند.
        // ⚠️ روز هم بخشی از کلید است: «مفادِ امروز» و «مصارفِ امروز» با نیمه‌شب
        // عوض می‌شوند بی آن‌که چیزی ذخیره شده باشد (چک‌لیستِ تحویل، بندِ ۶۲).
        var version = PumpYaqobi.Persistence.PumpDbContext.Version * 100000L + DateTime.Now.DayOfYear;
        if (version == _bannerVersion) return;
        _bannerVersion = version;

        // ۱) الباقیِ شرکت‌های تیل
        var companies = await host.Companies.ListAsync();
        var compAlbaqi = companies.Sum(c => host.Company.Summarize(c, c.Rows).AlbaqiAfn);

        // ۲) قرضِ کلِ قرض‌داران
        //
        // ⚠️ این‌جا پیش از این **همهٔ ردیف‌های همهٔ حساب‌ها** خوانده می‌شد — و
        // چون نوارِ بالا با هر بار عوض کردنِ بخش تازه می‌شود، همان هزینه به
        // ازای هر جابه‌جایی تکرار می‌گشت. سنجشِ کارایی همین را نشان داد:
        // باز کردنِ هر بخش، حتی سبک‌ترینشان، شش ثانیهٔ ثابت.
        //
        // ‎CardAccountsAsync‎ همان جمع‌ها را از خودِ دیتابیس می‌گیرد (با همان
        // خوددرمانیِ ردیف، داخلِ کوئری) و هیچ ردیفی نمی‌خواند.
        var accounts = await host.Debtors.CardAccountsAsync();
        decimal debt = 0;
        foreach (var list in accounts.Values) debt += host.Debt.SumTotals(list).All.Albaqi;

        // ۳) مفادِ امروز — جمعِ فایدهٔ هر دو شیفتِ پارچه‌های همین تاریخ
        var today = Shamsi.Today();
        var reports = (await host.StorageData.ReportsAsync(FuelType.Petrol))
            .Concat(await host.StorageData.ReportsAsync(FuelType.Diesel))
            .Where(r => r.DateShamsi == today);
        var profit = reports.Sum(r => (r.DayShift?.Profit ?? 0) + (r.NightShift?.Profit ?? 0));

        // ۴) مصارفِ امروز — فقط ماهِ جاری، نه همهٔ مصارفِ تاریخ. «امروز» همیشه
        //    داخلِ همین ماه است، پس عدد همان است و خواندن هزار برابر کمتر.
        var expToday = calc.ExpQuick(await host.ExpenseLedger.ListAsync(Shamsi.ThisMonth())).Day;

        string M(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        Banner[0].Value = M(compAlbaqi);
        Banner[1].Value = M(debt);
        //  ⛔ «مفاد و ضرر هم یک نوع اس تو صفحهٔ اصلی است و دیده نمیشه» —
        //  جملهٔ خودِ صاحب ریپو دربارهٔ پلنِ استاندارد. عدد **حساب می‌شود**
        //  (چون بقیهٔ برنامه به آن نیاز دارد) ولی روی نوار «•••» می‌نشیند.
        Banner[2].Value = Entitlements.Allows(Entitlements.Profit) ? M(profit) : "•••";
        Banner[3].Value = M(expToday);
    }

    /// <summary>
    /// ══ ترتیبِ نوار — مو‌به‌مو همان هجده دکمهٔ ‎&lt;div class="nav"&gt;‎ ═══════════
    ///
    /// ⚠️ این ترتیب فقط ظاهری نیست: میانبرِ ‎Ctrl+Shift+عدد‎ بخش را **با شمارهٔ
    /// جایش در همین فهرست** باز می‌کند. پیش از این ترتیب از ردیفِ دهم به بعد
    /// با سایت فرق داشت، پس ‎Ctrl+Shift+۱۰‎ که در سایت «گاوصندوق» بود این‌جا
    /// «رسید قرض‌داران» را باز می‌کرد — و همین‌طور تا هجده. کاربری که سال‌ها با
    /// این میانبرها کار کرده، هر بار بخشِ اشتباه را می‌گرفت.
    ///
    ///   ۱ داشبورد · ۲ پارچه‌ها · ۳ ورق‌های روزانه · ۴ قرض‌داران ·
    ///   ۵ ثبت فاکتورها · ۶ رسید قرض‌داران / چکنه · ۷ صرافی · ۸ مصارف ·
    ///   ۹ رسید پارچه · ۱۰ گاوصندوق · ۱۱ تیل امانت · ۱۲ شرکت‌ها تیل ·
    ///   ۱۳ مخزن · ۱۴ دوربین‌ها · ۱۵ حاضری و معاش · ۱۶ مفاد/ضرر ·
    ///   ۱۷ تنظیمات · ۱۸ تاریخچه‌ها
    ///
    /// ⚠️ <b>هجده‌تا و بس.</b> تا دیروز هفت بخشِ دیگر هم ته این فهرست بودند و
    /// نوار بیست‌وپنج دکمه‌ای شده بود. آن هفت‌تا در سایت هم دکمهٔ نوار ندارند:
    /// هر کدام کارتی داخلِ بخشِ دیگری‌اند. حالا این‌جا هم همان‌اند و در
    /// <see cref="AttachSubSections"/> به بخشِ خودشان بسته می‌شوند. اگر
    /// دوباره یکی‌شان را به این فهرست اضافه کنید، هم نوار شلوغ می‌شود هم
    /// آزمونِ ترتیبِ نوار قرمز.
    /// </summary>
    private IEnumerable<SectionViewModel> BuildSections(AppHost host) => new SectionViewModel[]
    {
        new DashboardSectionViewModel(host, this),   //  ۱
        new ParchaSectionViewModel(host),            //  ۲
        new WaraqSectionViewModel(host),             //  ۳
        new DebtSectionViewModel(host),              //  ۴
        new InvoiceSectionViewModel(host),           //  ۵
        new DebtReceiptSectionViewModel(host),       //  ۶
        new ExchangeSectionViewModel(host),          //  ۷
        new ExpenseSectionViewModel(host),           //  ۸
        new ParchaReceiptSectionViewModel(host),     //  ۹
        new SafeSectionViewModel(host),              // ۱۰
        new AmanatSectionViewModel(host),            // ۱۱
        new CompanySectionViewModel(host),           // ۱۲
        new StorageSectionViewModel(host),           // ۱۳
        new CameraSectionViewModel(host),            // ۱۴
        new AttendanceSectionViewModel(host),        // ۱۵
        new ProfitSectionViewModel(host),            // ۱۶
        new SettingsSectionViewModel(host),          // ۱۷
        new HistorySectionViewModel(host),           // ۱۸
        // ⚠️ بعد از هجدهمی، نه وسط: هجده بخشِ اولِ نوار باید به همان ترتیبِ
        // سایت بمانند، وگرنه ‎Ctrl+Shift+عدد‎ بخشِ دیگری را باز می‌کند.
        // (‎NavOrderTests‎ همین را گرفت، و درست هم گرفت.)
        new ChatSectionViewModel(host),              // ۱۹ — پیام‌رسان، مالِ خودِ نیتیو
        //  ۲۰ — حساب و اشتراک. باید بعد از هجدهمی بماند، وگرنه
        //  ‎Ctrl+Shift+عدد‎ جابه‌جا می‌شود (‎NavOrderTests‎).
        new AccountSectionViewModel(host),           // ۲۰
    };

    /// <summary>
    /// ══ هفت زیربخش، هر کدام زیرِ بخشِ خودش ══════════════════════════════════
    ///
    /// جای هر کدام از خودِ سایت آمده — همان‌جایی که ‎.tool-link-card‎ش نشسته:
    ///
    ///   مقایسهٔ نرخ      → ثبت فاکتورها            (‎sec-invoices‎)
    ///   چکنه            → رسید قرض‌داران / چکنه   (‎sec-debtrasid‎)
    ///   تخلیهٔ تانکر     → مخزن                    (‎sec-storage‎)
    ///   کمبودی کارمندان → حاضری و معاش            (‎sec-attendance‎)
    ///   قرض‌های کهنه     → قرض‌داران                (‎sec-debt‎)
    ///   گزارش ماهانه    → مفاد / ضرر              (‎sec-profit‎)
    ///   تاریخچهٔ نرخ     → مفاد / ضرر              (‎sec-profit‎)
    ///   مدیریت داده‌ها   → تنظیمات                 (‎sec-settings‎)
    ///
    /// ⚠️ نمونه‌ها این‌جا ساخته می‌شوند، نه در ‎BuildSections‎ — وگرنه آزمونِ
    /// ترتیبِ نوار (که تنِ ‎BuildSections‎ را می‌خواند) آن‌ها را هم دکمهٔ نوار
    /// می‌شمارد.
    /// </summary>
    private void AttachSubSections(AppHost host)
    {
        SectionViewModel? By(string id) => Sections.FirstOrDefault(s => s.Id == id);

        By("invoices")?.AddSub(new InvRateSectionViewModel(host),      "📉 مقایسهٔ نرخ فاکتورها");
        By("debtrasid")?.AddSub(new RetailSectionViewModel(host),      "🧾 حساب‌های چکنه");
        By("storage")?.AddSub(new TankerSectionViewModel(host),        "🚚 تخلیهٔ تانکر");
        By("attendance")?.AddSub(new StaffShortSectionViewModel(host), "👷 کمبودی کارمندان");
        By("debt")?.AddSub(new OldLoansSectionViewModel(host),         "⏰ قرض‌های کهنه");
        // «⏳ مدت عضویت همه»ی سایت — تا امروز دکمه‌اش هیچ کاری نمی‌کرد
        By("debt")?.AddSub(new MembershipSectionViewModel(host),       "⏳ مدت عضویت قرض‌داران");
        // «قرض‌های دسته‌جمعی» — دو صفحهٔ کاملاً جدا، مثلِ سایت. کلیک روی هر خط
        // حسابِ همان شخص را در بخشِ قرض‌داران باز می‌کند.
        if (By("debt") is DebtSectionViewModel debt)
        {
            Task Open(long id) => GoAsync(debt).ContinueWith(_ => debt.OpenPersonAsync(id)).Unwrap();
            By("debtrasid")?.AddSub(new DebtSummarySectionViewModel(host, false, Open),
                                    "⛽ قرض‌های دسته‌جمعی — واحد تیل");
            By("debtrasid")?.AddSub(new DebtSummarySectionViewModel(host, true, Open),
                                    "💵 قرض‌های دسته‌جمعی — واحد پول");
            // «📉 زیان ناشی از افزایش قیمت» — کارتِ بخشِ قرض‌دارانِ سایت
            // (‎sec-priceloss‎ و ‎sec-plperson‎). تا امروز در برنامه نبود.
            debt.AddSub(new PriceLossSectionViewModel(host, Open), "📉 زیان ناشی از افزایش قیمت");
        }
        By("profit")?.AddSub(new MonthReportSectionViewModel(host),    "📅 گزارش پایان ماه");
        By("profit")?.AddSub(new RateHistorySectionViewModel(host),    "📈 تاریخچهٔ نرخ اتحادیه");
        // ══ تنظیمات: سه صفحه، و بس ═══════════════════════════════════════
        // خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸). ترتیبشان همان ترتیبی است که
        // گفت و کارت‌های صفحهٔ تنظیمات هم همین است.
        By("settings")?.AddSub(new KeysSectionViewModel(host),         "🔑 رمزها و کد");
        By("settings")?.AddSub(new BackupSectionViewModel(host, this), "💾 بک‌اپ و به‌روزرسانی‌ها");
        By("settings")?.AddSub(new TrashSectionViewModel(host, this),  "🗑️ سطل زباله");
        //  خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «لینکِ دانلودِ اپِ اندروید و لینکِ
        //  برنامهٔ آیفون را توی یک بخشِ جدید توی تنظیمات بگذار… و کدِ پمپ هم
        //  همان‌جا دیده شود.»
        By("settings")?.AddSub(new AppsSectionViewModel(host),         "📲 اپِ گوشی — لینک و کد");
        //  بندِ ۱۱ی پرامپتِ ۲۲: «وضعیتِ Sync با جزئیات… و دکمهٔ الان همگام کن.»
        //  ⛔ کادرِ نشانیِ سرور آن‌جا **نیست** — نشانی قفل است.
        By("settings")?.AddSub(new SyncSectionViewModel(host),         "🔄 همگام‌سازی");
        //  «بخشِ وی‌آی‌پی را هم اعمال کن که من ببینم و تست کنم» — زیرِ خودِ
        //  پروفایل، چون همان‌جا حالِ اشتراک دیده می‌شود.
        By("account")?.AddSub(new VipSectionViewModel(host),           "💎 اشتراک و پلن‌ها");
    }
}
