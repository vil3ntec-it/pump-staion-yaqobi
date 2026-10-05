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

//  ⛔ شورا ج۴: بخشی از ‎MainViewModel‎ — خروج، رفتن به بخش‌ها، قفل‌ها و زیربخش‌ها. فقط جابه‌جاییِ همان عضوها از ‎MainViewModel.cs‎، بی تغییرِ یک رفتار.
public sealed partial class MainViewModel
{
    /// <summary>
    /// خروج و برگشت به صفحهٔ قفل — بی آن‌که برنامه بسته شود.
    ///
    /// ⛔ <b>بی رمز، خروج یک بن‌بست است و انجام نمی‌شود.</b> صفحهٔ قفلی که
    /// رمزی برای زدن ندارد فقط کاربر را از دفترِ خودش بیرون می‌گذاشت و تنها
    /// راهِ برگشت بستن و باز کردنِ برنامه بود. پس می‌گوییم چرا، نه این‌که
    /// بی‌صدا رد شویم — دکمه‌ای که زده شود و هیچ اتفاقی نیفتد باگ است.
    /// </summary>
    [RelayCommand]
    private async Task SignOut()
    {
        if (!AppHost.Current.Auth.HasPassword())
        {
            AppHost.Current.Toasts.Show("رمزی گذاشته نشده — برای خروج، اول در «تنظیمات ← رمزها و کد» رمز بگذارید.");
            return;
        }
        // ⛔ اول هر نوشتهٔ در صف: پس از ‎Auth.SignOut()‎ نقش «بیننده» است و ذخیرهٔ
        // تأخیریِ همان خانه‌ای که همین حالا تایپ شد با «اجازه ندارید» گم می‌شد.
        await FlushEverythingAsync();
        AppHost.Current.Auth.SignOut();
        // ⚠️ به ‎Locked‎ برمی‌گردیم، نه به ‎Starting‎: لودینگ یک بار در عمرِ
        // اجرای برنامه است و خروج نباید دوباره راهش بیندازد.
        Phase = AppPhase.Locked;
    }

    partial void OnSelectedThemeChanged(PumpTheme value)
    {
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(DarkSwitch));
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

        //  ⛔ **پیش از** رفتن، جایی که از آن آمدیم را به خاطر بسپار — وگرنه
        //  کاربر در تاریخچه‌ها گیر می‌کند و باید از نوار دنبالِ بخشِ خودش
        //  بگردد (گزارشِ ۱۴۰۵/۰۷/۰۶). «همان بخش»، نه «صفحهٔ اول».
        //  ⚠️ و اگر خودِ تاریخچه‌ها باز بود، مبدأ دست نمی‌خورد: زدنِ یک کارتِ
        //  دیگر نباید راهِ برگشت را به «تاریخچه‌ها» عوض کند.
        if (Current is not null && Current.Id != "history")
            h.SetOrigin(Current.Id, Current.Title);

        _historyFromSection = true;
        try { await GoAsync(h); }
        finally { _historyFromSection = false; }
        await h.OpenAsync(kind);
    }

    /// <summary>
    /// رفتن به یک بخش با شناسه‌اش — درِ <see cref="AppHost.GoSection"/>.
    /// ⚠️ به همان <c>GoAsync</c> می‌رسد، پس قفلِ پلن و رمزِ بخش سرِ جایشان‌اند.
    /// </summary>
    private async Task GoSectionAsync(string id)
    {
        if (Sections.FirstOrDefault(x => x.Id == id) is { } s) await GoAsync(s);
    }

    /// <summary>
    /// «همین حالا از راهِ <see cref="OpenHistoryAsync"/> می‌رویم» — یک نشانِ
    /// یک‌بارمصرف. ⚠️ لازم است چون رفتنِ <b>مستقیم</b> به «تاریخچه‌ها» (از
    /// نوار یا ‎Alt+عدد‎) باید راهِ برگشتِ کهنه را پاک کند؛ وگرنه دکمهٔ
    /// «برگشت به گاوصندوق» یک هفتهٔ بعد هم آن‌جا می‌مانْد و کاربر را به بخشی
    /// می‌بُرد که یادش نبود.
    /// </summary>
    private bool _historyFromSection;

    // ⚠️ ‎[RelayCommand]‎ مالِ ‎GoAsync‎ است و باید **بی‌فاصله** بالایش بماند:
    // یک بار چیزی بینشان افتاد و کامپایلر ‎CS0592‎ داد («این ویژگی فقط روی
    // متد معتبر است»). هیچ فیلد یا سندی این‌جا نگذارید.
    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task GoAsync(SectionViewModel? s)
    {
        if (s is null) return;

        // ⛔ بخشِ پنهان (دوربین‌ها) و در حالِ ساخت (تیل امانت) — ‎SectionGate‎
        if (SectionGate.IsHidden(s.Id)) return;
        if (SectionGate.IsBuilding(s.Id) && !await BuildingTapAsync(s)) return;

        // ⛔ بخشِ قفل‌دار (مفاد/ضرر) بی رمز باز نمی‌شود — شرحش در ‎UnlockAsync‎.
        if (!await UnlockAsync(s)) return;

        if (s is Sections.HistorySectionViewModel hv && !_historyFromSection)
            hv.SetOrigin("", "");

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
        //  ⛔ صفحهٔ تمام‌صفحه (پیام‌رسان، پروفایل) «آخرین بخش» نمی‌شود: «‹ برگشت»ِ
        //  آن‌ها همان «آخرین بخش» را باز می‌کند و برنامهٔ فردا هم از همان‌جا بالا
        //  می‌آید — پس هر دو دوباره روی همان صفحه می‌ماندند.
        if (s.Id is not ("chat" or "account")) _settings.LastSection = s.Id;
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
        //  ⛔ «storage-diesel» / «shifts-diesel» (کارتِ مخزنِ دیزل، هشدارِ مخزنِ
        //  دیزل، ردیفِ دیزلِ «آخرین ثبت‌ها») هیچ بخشی نیستند و تا امروز کلیک
        //  رویشان بی‌صدا هیچ کاری نمی‌کرد. پسوند تیل را می‌گوید، نه بخش را.
        //  و لینکِ بی‌پسوندِ همین دو بخش یعنی پطرول.
        var diesel = id.EndsWith("-diesel", StringComparison.Ordinal);
        if (diesel) id = id[..^"-diesel".Length];
        if (Sections.FirstOrDefault(x => x.Id == id) is { } top)
        {
            await GoAsync(top);
            if (ActiveSection != top) return;
            if (top is StorageSectionViewModel st) st.IsDiesel = diesel;
            else if (top is ParchaSectionViewModel pa) pa.IsDiesel = diesel;
            return;
        }

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
        if (Current?.OpenSub is { } locked && AppHost.Current.Locks.GatesSection(locked.Id))
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
        {
            OnPropertyChanged(nameof(IsChromeVisible));
            OnPropertyChanged(nameof(IsBannerVisible));
            OnPropertyChanged(nameof(IsHeaderVisible));
        }

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
        //  ⛔ «مفاد/ضرر» از ۱۴۰۵/۰۷/۲۰ بسته نمی‌شود، **تار** می‌شود — پرده‌اش در خودِ
        //  بخش است (‎ProfitSectionViewModel.PlanVeiled‎ و ‎ProfitVeil‎).
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

        //  ⛔ «مفاد/ضرر» بخش را نمی‌بندد، فقط عددهایش تار است (‎VeilsInstead‎)
        if (!AppHost.Current.Locks.GatesSection(s.Id)) return true;
        return await AskSectionPasswordAsync(s.Id, s.Title);
    }

    /// <summary>
    /// پرسیدنِ رمزِ یک بخش — با ترمزِ حدس زدن. تنها جای این پرسش است: هم درِ
    /// بخشِ قفل‌دار و هم پردهٔ «مفاد/ضرر» از همین می‌گذرند.
    /// </summary>
    public static async Task<bool> AskSectionPasswordAsync(string id, string title)
    {
        var locks = AppHost.Current.Locks;
        if (!locks.NeedsUnlock(id)) return true;

        //  ⛔ ترمزِ حدس زدن — پیش از پرسیدن هم، تا پنجرهٔ رمز بی‌فایده باز نشود
        if (locks.WaitSeconds(id) is > 0 and var wait)
        {
            AppHost.Current.Toast($"⏳ چند بار رمزِ نادرست زده شد — {wait} ثانیهٔ دیگر دوباره امتحان کنید",
                                  ToastKind.Warn);
            return false;
        }

        var pw = await Dialogs.PromptAsync("🔒 " + title,
                                           "این بخش رمز دارد. رمزش را بزنید.");
        if (pw is null) return false;

        if (!locks.Unlock(id, pw))
        {
            var left = locks.WaitSeconds(id);
            AppHost.Current.Toast(left > 0
                ? $"❌ رمزِ این بخش درست نیست — {left} ثانیه صبر کنید"
                : "❌ رمزِ این بخش درست نیست", ToastKind.Error);
            return false;
        }
        return true;
    }

    /// <summary>
    /// بخشِ در حالِ ساخت: هر زدن «در حالِ ساخت» می‌گوید؛ سومین زدنِ پشتِ سرِ هم
    /// رمزِ توسعه را می‌پرسد. درست ⇒ برای همین اجرا باز. شرح در ‎SectionGate‎.
    /// </summary>
    private async Task<bool> BuildingTapAsync(SectionViewModel s)
    {
        var now = AppClock.Mono;
        if (SectionGate.Tap(now) < SectionGate.TapsForPin)
        {
            AppHost.Current.Toast($"«{s.Title}» — {SectionGate.BuildingText}", ToastKind.Info);
            return false;
        }
        SectionGate.ResetTaps();

        if (SectionGate.WaitSeconds(now) is > 0 and var wait)
        {
            AppHost.Current.Toast($"⏳ چند بار رمزِ نادرست زده شد — {wait} ثانیهٔ دیگر", ToastKind.Warn);
            return false;
        }
        var pw = await Dialogs.PromptAsync("🚧 " + s.Title, "رمزِ توسعه را بزنید.");
        if (pw is null) return false;
        if (!SectionGate.TryDevUnlock(pw, AppClock.Mono))
        {
            AppHost.Current.Toast("❌ رمز درست نیست", ToastKind.Error);
            return false;
        }
        AppHost.Current.Toast($"🔓 «{s.Title}» برای همین اجرا باز شد", ToastKind.Ok);
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
        catch (Exception ex)
        {
            CrashGuard.Write("باز کردنِ زیربخش", ex);
            AppHost.Current.Toast("باز نشد: " + ErrorText.Friendly(ex), ToastKind.Error);
        }
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
        OnPropertyChanged(nameof(IsBannerVisible));
        OnPropertyChanged(nameof(IsHeaderVisible));
        OnPropertyChanged(nameof(IsSubOpen));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(ActiveSection));
        OnPropertyChanged(nameof(RowHost));
    }
}
