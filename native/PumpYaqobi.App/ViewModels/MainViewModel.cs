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
    /// <summary>⛔ شورا ب۵: آزمونِ بازیابیِ ماهانه در سنجه‌ها خاموش است (هر سنجه دفترِ تازه دارد و
    /// عکس و رونوشتِ اضافه فقط شمارِ دستورهای دیتابیس را در ‎idle‎ به‌هم می‌زد). خودِ آزمون را
    /// ‎RestoreDrillTests‎ می‌سنجد.</summary>
    public static bool RestoreDrillDisabled { get; set; }

    private readonly AppSettings _settings;

    public MainViewModel(AppSettings? settings = null)
    {
        _settings = settings ?? AppSettings.Load();
        _simpleMode = _settings.SimpleMode;
        //  نامِ پمپ که عوض شد (ساختنِ حساب، پروفایل، بازگردانی) ⇒ سربرگ و عنوانِ پنجره همان لحظه
        PumpBrand.Changed += () =>
        {
            if (Dispatcher.UIThread.CheckAccess()) OnPropertyChanged(nameof(BrandName));
            else Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(BrandName)));
        };

        //  رمزِ «مفاد/ضرر» زده یا دوباره قفل شد ⇒ «مفاد امروز» همان لحظه
        ProfitVeil.Changed += () => Dispatcher.UIThread.Post(() => Banner[2].Value = ProfitVeil.Show(_bannerProfit));

        //  نوارِ چهار عدد روشن/خاموش (کلیدِ کوچکِ داشبورد) — روشن شد ⇒ همان لحظه بخوان
        Services.BannerPref.Changed += () => Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(IsBannerVisible));
            if (Services.BannerPref.Show && Phase == AppPhase.Ready) QueueBannerRefresh();
        });

        //  سربرگِ بالا روشن/خاموش (همان کلیدِ داشبورد، ۱۴۰۵/۰۷/۱۹ — ‎HeaderPref‎)
        Services.HeaderPref.Changed += () => Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(IsHeaderVisible)));

        Lock = new LockViewModel(AppHost.Current);
        OpenRequest.Arrived += () => Dispatcher.UIThread.Post(() => _ = HandleOpenRequestsAsync());

        // ‎Ctrl+Z‎/‎Ctrl+Y‎ی کلِ برنامه — پس از برگرداندنِ یک حذف، صفحهٔ جلوی
        // چشم (و صفحهٔ بازِ درونش) از نو خوانده می‌شود. شرح: ‎Services/UndoHub.cs‎
        UndoHub.Wire();
        UndoHub.AfterDataChange = async () =>
        {
            if (ActiveSection is { } s) await s.AfterUndoAsync();
            if (!ReferenceEquals(ActiveSection, Current) && Current is { } c) await c.AfterUndoAsync();
        };
        // ⚠️ بخشِ آغازین بعد از ورود بار می‌شود، نه در سازنده. دو دلیل:
        //   ۱) پیش از ورود هیچ اجازه‌ای نداریم و لایهٔ سرویس درست هم رد می‌کند.
        //   ۲) وقتی در سازنده بار می‌شد، عددهای نوارِ بالا و داشبورد روی همان
        //      لحظهٔ صفرِ پیش از ورود می‌ماندند و کاربر «۰ افغانی» می‌دید.
        Lock.SignedIn += () =>
        {
            // ⚠️ ترتیب مهم است: اول بخشِ آغازین بار می‌شود، بعد پرده کنار
            // می‌رود. وگرنه کاربر یک لحظه پوستهٔ خالی را می‌بیند.
            Phase = AppPhase.Ready;
            ExitBackup.Mark();         // «پیش از بستن بکاپ؟» از همین لحظه می‌شمارد
            _ = OpenStartSectionAsync();

            //  ⛔ **از این‌جا به پایین فقط یک بار در عمرِ برنامه.**
            //
            //  با آمدنِ دفترِ هر حساب (۱۴۰۵/۰۷/۱۱) این رویداد می‌تواند
            //  **دوباره** شلیک شود: عوض شدنِ حساب، دفترِ تازه‌ای را باز
            //  می‌کند و اگر آن دفتر رمز نداشته باشد برنامه دوباره «وارد»
            //  می‌شود. آن‌چه پایین است ولی یک‌بارمصرف است — سه شنوندهٔ
            //  موتورِ همگام‌سازی و نصبِ قفلِ نرم — بارِ دوم **دو برابر**
            //  می‌شد: هر توستِ اعلان دو بار، هر تیکِ چراغ دو بار.
            //
            //  ⚠️ حلقه‌ها (`Publisher` · `Sync` · `BackupToServer`) خودشان
            //  دو بار صدا زدن را یکی می‌کنند؛ مشکل شنونده‌ها بودند.
            //  ⛔ «فروش ورق»های قدیمیِ گاوصندوق منهای قرض (یک بار برای هر دفتر، ۱۴۰۵/۰۷/۱۸) —
            //  پیش از نگهبانِ یک‌بارمصرف، چون دفترِ تازه (حسابِ دیگر) مهرِ خودش را می‌خواهد.
            _ = AppHost.Current.ShiftWaraqSync.StartFixOldSales();
            //  ⛔ شورا، الف۱: عددِ ناخوانای روی دیسک (از نسخه‌های پیشین یا opِ خراب) گفته شود —
            //  فقط گزارش، روی نخِ دیگر، یک بار برای هر دفتر
            _ = ReportLedgerHealthAsync();
            //  ⛔ شورا، الف۳: عددهای مشتقِ ذخیره‌شده با ردیف‌ها — روزی یک بار برای هر دفتر
            _ = AppHost.Current.Parity.StartDaily();

            if (_afterSignIn) return;
            _afterSignIn = true;
            Services.Hints.Show = t => AppHost.Current.Toasts.Show(t);

            // ══ عکسِ روزانه (بندِ ۲۳) ══════════════════════════════════════════
            // همان ‎_autoDailyBackup‎ی نسخهٔ وب، ولی از فایلِ دیتابیس. روی نخِ
            // دیگر می‌رود تا باز شدنِ برنامه معطلِ آن نماند، و خودش هیچ استثنایی
            // بیرون نمی‌دهد — بکاپِ خودکار نباید ورودِ کاربر را بشکند.
            //  ⛔ شورا ب۵: و ماهی یک بار همان عکس در پوشهٔ موقت باز و با دفتر سنجیده می‌شود
            //  (پشتِ سرِ عکس، نه هم‌زمان با آن). سرخ شد ⇒ یک توست، و نتیجه در پروفایل.
            _ = Task.Run(() =>
            {
                AppHost.Current.Backup.SnapshotToday();
                if (RestoreDrillDisabled) return;
                var d = AppHost.Current.Drill.MonthlyOnce();
                if (d is { Ok: false })
                    Dispatcher.UIThread.Post(() => AppHost.Current.Toast(d.Text, ToastKind.Error));
            });

            // ══ خوراکِ اپِ کارمندان و ربات ═══════════════════════════════════
            // خواستهٔ صاحب ریپو: «هر تغییری که در اپ انجام می‌شود توی ربات هم
            // باشد.» پس از همین‌جا یک حلقهٔ آرامِ پس‌زمینه شروع می‌شود که عکسِ
            // برنامه را روی سرورِ خانگی تازه نگه می‌دارد.
            //
            // ⚠️ بعد از ورود، نه در سازنده: پیش از ورود هیچ اجازه‌ای نداریم و
            // لایهٔ سرویس درست هم رد می‌کند. اگر سروری تنظیم نشده باشد، این
            // حلقه بی‌صدا هیچ کاری نمی‌کند.
            //  ⛔ شورا ج۷: هر حاشیه از درِ ماژولِ خودش — شکستنِ یکی بقیه و دفتر را نمی‌برد
            Modules.Start("publisher", AppHost.Current.Publisher.Start);

            // ══ به‌روزرسانیِ خودکار (۱۴۰۵/۰۷/۱۶) — خودش می‌بیند و می‌گیرد، و هر
            // نسخه را یک بار پیشنهاد می‌کند. شرح: ‎Update/AutoUpdate.cs‎
            Modules.Start("autoupdate", Update.AutoUpdate.Start);

            // ══ حسابِ واردشده ⇒ آماده، بی باز کردنِ پروفایل (۱۴۰۵/۰۷/۱۳) ═════
            // گزارشِ صاحب ریپو پس از ۳.۱.۱۷۹: «حسابی که قبلاً آزمایشی نداشت،
            // باز هم نگرفت.» سنجهٔ `oldacct` روی پشتهٔ واقعی دید: حسابی که با
            // نسخهٔ پیشین ساخته شده و پمپ ندارد، تا کاربر «پروفایل» را باز نکند
            // هیچ پمپی نمی‌گیرد — و آزمایشی داخلِ مجوزِ همان پمپ است. پس یک بار
            // در هر اجرا، پس از همان مکثِ آرامِ ناشر، **همان** گامِ «حساب آماده»ِ
            // پروفایل (`EnsureReadyAsync`) می‌دود. ⛔ حلقهٔ پس‌زمینه همچنان هیچ
            // پمپی نمی‌سازد؛ این باز کردنِ برنامه به دستِ خودِ کاربر است، و
            // «هر حساب یک پمپ» سرِ جایش است (پیش از ساختن از سرور می‌پرسد).
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(Services.StationPublisher.FirstDelay); } catch { return; }
                Dispatcher.UIThread.Post(() => _ = Account.EnsureReadyOnOpenAsync());
            });

            // ══ هشدارِ قرض‌دار و مخزن ⇒ میرزا ═══════════════════════════════
            // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «برای قرض‌داری که حسابش تموم بشه
            // یا قرض‌دار بشه، چرا پیامِ هشدار به میرزا نمیاد؟» — حالا همان
            // لحظه‌ای که فهرست عوض شد: یک توست، و زنگِ داشبورد تازه می‌شود.
            // ⚠️ فهرست همان است که به سرور و بات می‌رود (`AlertWatch`).
            AppHost.Current.LiveAlerts.Changed += (opened, initial) =>
                Dispatcher.UIThread.Post(() => OnAlertsChanged(opened, initial));

            // ══ همگام‌سازی با سرورِ حساب (VILL3N Sync v1) ════════════════════
            // بندِ ۳ی پرامپتِ ۲۲. صف در خودِ SQLite است، پس بسته شدنِ برنامه
            // چیزی را نمی‌برد و این حلقه فقط «از همان‌جا ادامه می‌دهد».
            // ⚠️ بعد از ورود، نه در سازنده — همان دلیلِ ناشر: پیش از ورود
            // هیچ اجازه‌ای نداریم.
            SoftLock.Install();
            var sync = AppHost.Current.Sync;
            sync.Changed += () => Dispatcher.UIThread.Post(() => { TickSyncDot(); TickSyncPrime(); });
            sync.NoticeArrived += n => Dispatcher.UIThread.Post(() => ShowNotice(n));
            sync.ConflictsFound += n => Dispatcher.UIThread.Post(() => AppHost.Current.Toast(
                $"⚠️ {n} خانه هم این‌جا و هم روی کامپیوترِ دیگر عوض شده بود — هیچ‌کدام گم نشد؛ "
                + "در «تنظیمات ← بک‌اپ ← تعارض‌ها» ببینید و انتخاب کنید", ToastKind.Warn));
            sync.PrimeFinished += (okPrime, why, got) =>
                Dispatcher.UIThread.Post(() => _ = OnPrimeFinishedAsync(okPrime, why, got));
            sync.Start();
            TickSyncDot();
            TickSyncPrime();
            //  ⚠️ قفلِ نرم بی‌صدا نباشد: اگر اشتراک تمام شده (یا هفت روز
            //  مانده) کاربر باید بداند چرا نوشتن نمی‌شود، نه این‌که فکر کند
            //  برنامه خراب است. امروز این جمله خالی است، چون قفل‌ها بازند.
            NoticeText = SoftLock.VisibleBanner();

            // ══ پشتیبانِ هر شش ساعت روی سرورِ خانگی ═════════════════════════
            // خواستهٔ صاحب ریپو: «هر ۶ ساعت بک‌آپ برود به سرور و سه روز بماند؛
            // نرفت، به مدیر بگو.» شرحِ کامل در ‎BackupPusher‎.
            Modules.Start("backup-push", AppHost.Current.BackupToServer.Start);

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
        AppHost.Current.GoSection = GoSectionAsync;
        AppHost.Current.FindSection = id => AllPages.FirstOrDefault(p => p.Id == id);
        //  دفترِ حساب عوض شد ⇒ همه‌چیز از نو خوانده شود و قفلِ همان دفترِ
        //  تازه پرسیده شود. شرحِ کامل در ‎OnLedgerSwitchedAsync‎.
        AppHost.Current.LedgerSwitched += () =>
            Dispatcher.UIThread.Post(() => _ = OnLedgerSwitchedAsync());
        //  مجوزِ تازه‌ای که حلقهٔ پس‌زمینه گرفت (مدیر اشتراک داد، تمدید کرد
        //  یا برداشت) ⇒ سربرگ و پروفایل همان لحظه، بی باز کردنِ دوبارهٔ
        //  پروفایل. شرحش بالای ‎CloudLink.LicenseChanged‎.
        //  ⛔ و نوارِ «فقط‌خواندنی» همان لحظه — تا ۱۴۰۵/۰۷/۱۴ فقط سرِ بالا آمدنِ
        //  برنامه خوانده می‌شد، پس مجوزی که درست شده بود تا بستن و باز کردنِ
        //  برنامه هنوز «اشتراک تمام شده» می‌گفت.
        CloudLink.LicenseChanged += () => Dispatcher.UIThread.Post(OnLicenseMoved);
        //  🤖 مرزِ خودِ مجوز رد شد (آفلاین هم) — همان کار. شرحش بالای
        //  ‎SubscriptionWatch.LocalTick‎.
        SubscriptionWatch.BoundaryCrossed += () => Dispatcher.UIThread.Post(OnLicenseMoved);
        SubscriptionWatch.Changed += () => Dispatcher.UIThread.Post(() => Account.WatchLine = SubscriptionWatch.Line());
        _paidOpen = Entitlements.Paid.Where(Entitlements.Allows).ToHashSet();
        Account.PumpCreatedHere += () => { _boundAt = DateTime.MinValue; TickLinkDot(); };
        //  ⛔ ورود و خروج همان لحظه در چراغ دیده می‌شود، نه ده ثانیه بعد
        //  (سنجهٔ `signuptrial`: تازه وارد شده بود و چراغ «هنوز وارد حساب
        //  نشده‌اید» می‌گفت — کَشِ ده‌ثانیه‌ایِ `DeviceBound`).
        Account.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AccountSectionViewModel.SignedIn)
                or nameof(AccountSectionViewModel.NeedsPump) or nameof(AccountSectionViewModel.NeedsBind))
            { _boundAt = DateTime.MinValue; TickCloudDot(); TickLinkDot(); }
        };
        // فهرست‌های نام‌دارِ پیشنهادِ خودکار — همان ‎datalist‎های سایت
        var host0 = AppHost.Current;
        Controls.Suggest.Provide("staff", async () => (await host0.Attendance.StaffAsync()).Select(x => x.Name ?? ""));
        Controls.Suggest.Provide("debtor", async () => (await host0.Debtors.ListAsync()).Select(x => x.Name));
        //  «/هارون» در نامِ ورق: نامِ هر حساب و هر فرعی («هارون دکان») — فقط نام‌ها (۱۴۰۵/۰۷/۱۷)
        Controls.Suggest.Provide("debtor-acct", async () => (await host0.Debtors.AccountUnitsAsync())
            .Select(a => a.IsMain ? a.PersonName : (a.PersonName + " " + a.AccountName).Trim())
            .Append(PumpYaqobi.Application.Services.PostingService.RetailWord));   // «/چکنه» ⇒ دفترِ چکنه
        //  نام‌های حساب‌های چکنه — کادرِ نامِ «رسید قرض‌داران / چکنه» وقتی «چکنه» برگزیده شده
        Controls.Suggest.Provide("chakana", async () => await host0.DebtReceipts.RetailNamesAsync());
        // ⚠️ و همین حالا یک بار خوانده شوند: کَشِ سرد یعنی نخستین تایپِ
        // کاربر هیچ پیشنهادی نمی‌گیرد. شرحش بالای ‎Suggest.Warm‎.
        Controls.Suggest.Warm("staff", "debtor", "debtor-acct");
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
        if (Warm.Done) { await LockOrOpenAsync(); return; }

        var all = AllPages;
        if (Avalonia.Application.Current?.DataTemplates.OfType<ViewLocator>().FirstOrDefault()
            is not { } locator || all.Count == 0)
        { await LockOrOpenAsync(); return; }

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
            await LockOrOpenAsync();
        }

        // ══ بقیه پشتِ صفحهٔ قفل ═══════════════════════════════════════════
        // پوسته زیرِ قفل هم «دیده‌شونده» است (‎IsShellVisible‎) ولی صفحهٔ رمز
        // مات و رویش است. با اولین ‎Ready‎ می‌ایستد.
        _ = WarmRestAsync(locator, layout);
    }

    /// <summary>
    /// ══ نصبِ بی‌رمز هیچ صفحهٔ قفلی نمی‌بیند ════════════════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «برنامه بدون رمز باشه، چون کسایی
    /// که تازه به برنامه می‌رسن نباید رمز داشته باشه و خود طرف برای خودش رمز
    /// خودشو می‌زنه.»
    ///
    /// ⛔ <b>این تنها جای تصمیم است.</b> هر سه راهِ خروجِ
    /// <see cref="WarmUpAsync"/> از همین رد می‌شوند، وگرنه یکی‌شان جا
    /// می‌ماند و برنامه گاهی صفحهٔ قفلِ بی‌رمز نشان می‌داد.
    ///
    /// ⚠️ تصمیم از <see cref="Services.Security.AuthService.HasPassword"/>
    /// می‌آید، نه از رشتهٔ خالی: هشِ خالی «رمزی نیست» است و
    /// <c>PasswordHasher.Verify</c> هیچ‌وقت رویش درست نمی‌گوید.
    /// ⚠️ و پرده تا نشستنِ <c>Phase</c> نمی‌رود: <c>SignedIn</c> خودش
    /// <c>Ready</c> می‌گذارد، پس صفحهٔ قفل حتی یک فریم هم دیده نمی‌شود.
    /// </summary>
    private async Task LockOrOpenAsync()
    {
        try
        {
            if (await Lock.OpenIfNoPasswordAsync()) return;
        }
        catch { /* نشد ⇒ همان صفحهٔ قفل، نه پردهٔ جاویدان */ }
        Phase = AppPhase.Locked;
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
            SectionGate.IsHidden(x.Id) || SectionGate.IsBuilding(x.Id)
            || AppHost.Current.Locks.GatesSection(x.Id)
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
    public IReadOnlyList<SectionViewModel> NavSections
    {
        get
        {
            var defaults = NavDefaults();
            var byId = defaults.ToDictionary(s => s.Id);
            var all = NavOrder.Arrange(defaults.Select(s => s.Id).ToList(), _settings.NavOrder)
                              .Select(id => byId[id]).ToList();
            //  شورا، ث۶ — حالتِ ساده: همان ترتیبِ دیدنی، فقط پنج بخشِ روزانه
            return SimpleMode && !ShowAllNav ? all.Where(x => SimpleIds.Contains(x.Id)).ToList() : all;
        }
    }

    // ══ شورا، ث۶ — «حالتِ ساده» ════════════════════════════════════════════
    //  «۲۰ بخش و ۳۳ دکمه برای کسی که فقط پارچه و ورق می‌زند زیاد است.»
    //  ⛔ هیچ بخشی حذف نمی‌شود: فقط نوار کوتاه می‌شود، «☰ همه» بقیه را نشان
    //  می‌دهد، و رفتن از جای دیگر (تاریخچه، هشدار، چت…) همان ‎GoAsync‎ است.
    //  ⛔ ‎Alt+عدد‎ همان ترتیبِ دیدنی را می‌رود (از ‎NavSections‎ می‌خواند).

    /// <summary>پنج بخشِ روزانهٔ میرزا.</summary>
    public static readonly string[] SimpleIds = { "shifts", "waraq", "debt", "safe", "debtrasid" };

    [ObservableProperty] private bool _simpleMode;
    [ObservableProperty] private bool _showAllNav;

    partial void OnSimpleModeChanged(bool value)
    {
        if (_settings.SimpleMode != value)
        {
            _settings.SimpleMode = value;
            _settings.SaveSoon();         // مقدارِ راحتی — نه ‎fsync‎ روی نخِ رابط
        }
        ShowAllNav = false;
        OnPropertyChanged(nameof(NavSections));
        OnPropertyChanged(nameof(AllNavText));
    }

    partial void OnShowAllNavChanged(bool value)
    {
        OnPropertyChanged(nameof(NavSections));
        OnPropertyChanged(nameof(AllNavText));
    }

    public string AllNavText => ShowAllNav ? "‹ فقط بخش‌های روزانه" : $"☰ همه ({NavDefaults().Count})";

    [RelayCommand]
    private void ToggleAllNav() => ShowAllNav = !ShowAllNav;

    private List<SectionViewModel> NavDefaults() =>
        Sections.Where(s => s.Id is not ("chat" or "account") && !SectionGate.IsHidden(s.Id)).ToList();

    // ══ جابه‌جا کردنِ بخش در نوار — راست‌کلیک روی هر بخش (۱۴۰۵/۰۷/۱۵) ══
    //  شرح و قاعده‌ها بالای ‎ViewModels.NavOrder‎. فقط ترتیبِ دیدن عوض می‌شود.
    [RelayCommand]
    private void NavReset()
    {
        if (_settings.NavOrder.Length == 0) return;
        _settings.NavOrder = "";
        _settings.SaveSoon();
        OnPropertyChanged(nameof(NavSections));
    }

    public void MoveNav(SectionViewModel? s, NavOrder.Where where)
    {
        if (s is null) return;
        var shown = NavSections.Select(x => x.Id).ToList();
        StoreNav(shown, NavOrder.Move(shown, s.Id, where));
    }

    /// <summary>کشیدن و رها کردن در نوار — پیش از خانهٔ ‎index‎ (شرح: ‎NavOrder.MoveTo‎).</summary>
    public void MoveNavTo(SectionViewModel? s, int index)
    {
        if (s is null) return;
        var shown = NavSections.Select(x => x.Id).ToList();
        StoreNav(shown, NavOrder.MoveTo(shown, s.Id, index));
    }

    private void StoreNav(List<string> shown, List<string> moved)
    {
        if (moved.SequenceEqual(shown)) return;
        _settings.NavOrder = NavOrder.Save(NavDefaults().Select(x => x.Id).ToList(), moved);
        _settings.SaveSoon();
        OnPropertyChanged(nameof(NavSections));
    }

    /// <summary>
    /// چهار عددِ نوارِ بالا — همان ‎#topBanner‎: الباقیِ شرکت‌ها، قرضِ کل،
    /// مفادِ امروز و مصارفِ امروز. با هر بار عوض کردنِ بخش تازه می‌شوند.
    /// </summary>
    private string _bannerProfit = "0";

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

    /// <summary>
    /// نوارِ چهار عدد — با پوسته، و فقط اگر کاربر خاموشش نکرده باشد
    /// (‎BannerPref‎، کلیدِ کوچکِ داشبورد).
    /// </summary>
    public bool IsBannerVisible => IsChromeVisible && Services.BannerPref.Show;

    /// <summary>
    /// سربرگِ بالا (نام، چراغ، تاریخ، تم، پروفایل، پشتیبانی…) — با پوسته، و فقط
    /// اگر کاربر خاموشش نکرده باشد (‎HeaderPref‎، کلیدِ کوچکِ داشبورد).
    /// </summary>
    public bool IsHeaderVisible => IsChromeVisible && Services.HeaderPref.Show;

    /// <summary>نوشتهٔ دکمهٔ برگشت — «‹ برگشت به قرض‌داران».</summary>
    public string BackText => "‹ برگشت به " + (Current?.Title ?? "");
    [ObservableProperty] private PumpTheme _selectedTheme;
    [ObservableProperty] private string _clock = "";

    /// <summary>
    /// راهنمای ساعتِ سربرگ. ⛔ هیچ هشداری دربارهٔ «ساعتِ ویندوز جلو/عقب است» در
    /// کار نیست (۱۴۰۵/۰۷/۱۶ — «هر دقیقه می‌گه چند دقیقه عقب است، به من چه»).
    /// تاریخِ سربرگ نمایشی است (‎DisplayClock‎) و هیچ حسابی را عوض نمی‌کند.
    /// </summary>
    public string ClockTip => "تاریخ و ساعت — برای عوض کردنِ آن‌چه این‌جا نشان داده می‌شود کلیک کنید";

    /// <summary>تاریخِ سربرگ با تاریخِ نمایشی جلو رفت (نیمه‌شب یا عوض کردنش).</summary>
    public void DisplayDayChanged() => OnPropertyChanged(nameof(TodayText));
}
