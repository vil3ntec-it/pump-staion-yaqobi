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

//  ⛔ شورا ج۴: بخشی از ‎MainViewModel‎ — حالت‌های برنامه، باز کردنِ فایل، روزِ تازه و پیش از بستن. فقط جابه‌جاییِ همان عضوها از ‎MainViewModel.cs‎، بی تغییرِ یک رفتار.
public sealed partial class MainViewModel
{
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
        AppHost.Current.Unlocked = v == AppPhase.Ready;
        foreach (var n in new[] { nameof(IsStarting), nameof(IsLockVisible),
                                  nameof(IsShellVisible), nameof(IsLocked),
                                  nameof(RoleText), nameof(RoleBrushKey) })
            OnPropertyChanged(n);
        //  دکمهٔ «پروفایل»ِ سربرگ همان لحظهٔ ورود درست بگوید VIP هست یا نه
        if (v == AppPhase.Ready) { Account.RefreshAll(); RefreshLockBanner(force: true); }
        //  فایلی که با دوبار-کلیک آمده، پس از ورود (‎OpenRequest‎)
        if (v == AppPhase.Ready) Dispatcher.UIThread.Post(() => _ = HandleOpenRequestsAsync());
        //  اطلاعات از درایوِ C به پوشهٔ برنامه آمد (‎DataHome‎) — یک بار گفته شود
        if (v == AppPhase.Ready && PumpYaqobi.Services.Data.DataHome.TakeMoved() is { Length: > 0 })
            AppHost.Current.Toast("📁 همهٔ اطلاعات به پوشهٔ برنامه منتقل شد: " + AppSettings.Dir, ToastKind.Ok);
    }

    // ══ دوبار-کلیک روی ‎.pumpyaqobi‎ یا ‎.pumpkey‎ (۱۴۰۵/۰۷/۱۵) ═══════════════
    //  ⛔ هیچ کاری بی پرسش نیست: فایلِ کامل از همان درِ «آوردنِ فایلِ کامل»
    //  می‌گذرد (سنجش ⇒ خلاصه ⇒ «بله، بیاور»)، و کدِ اشتراک از همان درِ
    //  «انتخابِ فایل». این‌جا فقط راهِ رسیدن به همان صفحه است.
    private bool _opening;

    private async Task HandleOpenRequestsAsync()
    {
        if (Phase != AppPhase.Ready || _opening) return;
        _opening = true;
        try
        {
            while (OpenRequest.Take() is { } path)
            {
                try { await OpenFileAsync(path); }
                catch (Exception ex) { CrashGuard.Write("باز کردنِ فایل", ex); }
            }
        }
        finally { _opening = false; }
    }

    private async Task OpenFileAsync(string path)
    {
        var isKey = Path.GetExtension(path).Equals(OpenRequest.KeyExt, StringComparison.OrdinalIgnoreCase);
        var subId = isKey ? "vip" : "backups";
        var parent = Sections.FirstOrDefault(s => s.SubSections.Any(x => x.Id == subId));
        var sub = parent?.SubSections.FirstOrDefault(x => x.Id == subId);
        if (parent is null || sub is null) return;

        await GoAsync(parent);
        parent.ShowSub(sub);
        if (LastSubOpen is { } opening) { try { await opening; } catch { } }

        if (sub is BackupSectionViewModel b) await b.ImportFullFromAsync(path);
        else if (sub is VipSectionViewModel v) await v.ApplyKeyFileAsync(path);
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

    /// <summary>
    /// تاریخِ شمسیِ امروز — «یک‌شنبه سنبله 1405/6/5».
    /// ⛔ شکلش خواستهٔ صریحِ صاحب ریپو است: روزِ هفته، نامِ ماه، و سال/ماه/روز
    /// بی صفرِ پیشرو — ⛔ با <b>اسلش</b>، نه نقطه (۱۴۰۵/۰۷/۱۵ دوم: «تاریخ رو /
    /// بده نه . — باید اسلش باشه وسطشون تا فهمیده بشه»).
    /// ⛔ این تاریخ <b>نمایشی</b> است (‎DisplayClock‎، ۱۴۰۵/۰۷/۱۶): کاربر هر
    /// تاریخی بخواهد از پنجرهٔ «🕘» می‌گذارد و هیچ حسابی عوض نمی‌شود — هر
    /// تصمیمی همچنان از <see cref="AppClock"/> است.
    /// </summary>
    public string TodayText => HeaderDate(Services.DisplayClock.Now);

    /// <summary>
    /// نامِ برنامه در سربرگ، عنوانِ پنجره و پرده‌ها — نامِ پمپی که کاربر نوشته،
    /// وگرنه «پمپ بنزین» (‎PumpBrand‎، ۱۴۰۵/۰۷/۱۵).
    /// </summary>
    public string BrandName => PumpBrand.Name;

    public static string HeaderDate(DateTime now)
    {
        var p = Shamsi.Of(now).Split('/');
        var text = p.Length == 3 && int.TryParse(p[1], out var m) && int.TryParse(p[2], out var d)
            ? Shamsi.DayName(now) + " " + Shamsi.MonthName(m) + " " + p[0] + "/" + m + "/" + d
            : Shamsi.DayName(now) + "، " + Shamsi.Of(now);
        //  ⛔ داخلِ یک «جزیرهٔ راست‌به‌چپ» (‎U+2067 … U+2069‎) — گزارشِ صاحب ریپو
        //  (۱۴۰۵/۰۷/۱۵): «تو زدی ۱۴۰۵.سنبله.۱». عددها و واژه‌ها در یک پاراگرافِ
        //  چپ‌به‌راست به ترتیبِ دیداری وارونه می‌نشینند (سال اول، روزِ هفته آخر)؛
        //  جزیره ترتیبِ خودِ این جمله را همیشه «روزِ هفته، ماه، سال.ماه.روز» نگه
        //  می‌دارد، هر جا که نوشته شود.
        return "\u2067" + text + "\u2069";
    }

    /// <summary>
    /// کلیک روی تاریخ و ساعتِ سربرگ ⇐ پنجرهٔ «🕘 تاریخ و ساعت»ِ خودِ برنامه
    /// (۱۴۰۵/۰۷/۱۵ — «برنامهٔ من خودش داشته باشد، نرود از ویندوز باز شود»).
    /// ⛔ «ثبت» فقط تاریخ و ساعتِ <b>نمایشی</b> را عوض می‌کند (‎DisplayClock‎) —
    /// نه ساعتِ ویندوز و نه هیچ حسابی (۱۴۰۵/۰۷/۱۶).
    /// </summary>
    [RelayCommand]
    private Task OpenClock() => Views.ClockWindow.ShowAsync();

    /// <summary>
    /// دو دکمهٔ رادیوییِ تم در سربرگ (۱۴۰۵/۰۷/۱۵ — «کشویی است، به رادیو باتون
    /// عوض کن که خیلی جا نگیرد»). همان ‎SelectedTheme‎، فقط دو درِ دیگر.
    /// </summary>
    public bool IsLightTheme
    {
        get => !SelectedTheme.IsDark;
        set { if (value && SelectedTheme.IsDark) SelectedTheme = PumpTheme.Blue; }
    }

    public bool IsDarkTheme
    {
        get => SelectedTheme.IsDark;
        set { if (value && !SelectedTheme.IsDark) SelectedTheme = PumpTheme.Gold; }
    }

    /// <summary>
    /// ══ کلیدِ یگانهٔ تم در سربرگ (۱۴۰۵/۰۷/۱۵، دومین خواستهٔ همان روز) ══
    /// «دارک مود و لایت مود هر دو توی یک کادر باشند» — یک کلید که هر دو سو
    /// را می‌رود: خاموش = روشن (کپسولِ نارنجی)، روشن = تیره (کپسولِ بنفش).
    /// همان ‎SelectedTheme‎؛ تنها فرقش با دو درِ بالا این است که «نادرست» هم
    /// معنا دارد (برگشت به روشن).
    /// </summary>
    public bool DarkSwitch
    {
        get => SelectedTheme.IsDark;
        set { if (value != SelectedTheme.IsDark) SelectedTheme = value ? PumpTheme.Gold : PumpTheme.Blue; }
    }

    /// <summary>
    /// روز عوض شد (نیمه‌شب): تاریخِ سربرگ و عددهای نوار از نو — و بخش‌های
    /// دفتری هم خبر می‌شوند، چون شاید **ماه** هم عوض شده باشد.
    ///
    /// ⛔ تا امروز فقط سربرگ و نوار از نو ساخته می‌شدند، پس برنامه‌ای که شبِ
    /// آخرِ ماه باز مانده بود فردا هنوز جدولِ ماهِ گذشته را نشان می‌داد
    /// (شرحِ کامل بالای <c>LedgerSectionViewModel.OnDayChanged</c>).
    /// ⚠️ بخشی که ماه ندارد پیش‌فرضِ خالی می‌گیرد، پس این حلقه برای نوزده
    /// بخش از بیست‌ودو بخش **هیچ** کاری نمی‌کند.
    /// </summary>
    public void DayChanged()
    {
        OnPropertyChanged(nameof(TodayText));
        //  ⛔ توستِ «📅 ماهِ فلان شروع شد» (۱۴۰۵/۰۷/۱۲) برداشته شد — خواستهٔ
        //  صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «نمی‌خواهم آن مدل باشد که بگوید این
        //  ماه فلان‌فلان شده.» جایش نقطهٔ سرخ روی کشوی ماه/سالِ هر بخش است
        //  (‎MonthDot‎) که همین ‎OnDayChanged‎ی بخش‌ها می‌نشاندش.
        QueueBannerRefresh();
        foreach (var p in AllPages) p.OnDayChanged();
        //  «هر شب یک بار» (شورا، الف۳) — برنامه‌ای که شب باز مانده
        if (_afterSignIn) _ = AppHost.Current.Parity.StartDaily();
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

    /// <summary>
    /// ══ پیش از بستن: «بکاپ بگیرم؟» — فقط وقتی لازم است (۱۴۰۵/۰۷/۱۹) ══
    /// قاعده در ‎ExitBackup‎. «بله» ⇒ اگر فایلِ بکاپِ قبلی هنوز همان‌جاست، همان
    /// به‌روز می‌شود؛ وگرنه عکسِ امروزِ همین کامپیوتر. ⛔ «نه» همان بستنِ همیشگی است.
    /// </summary>
    public async Task OfferBackupBeforeExitAsync()
    {
        //  ⛔ برای نصب بسته می‌شود ⇒ نپرس (‎Update.UpdateExit‎): پنجرهٔ پرسش برنامه را
        //  باز نگه می‌داشت و نسخهٔ تازه دوباره باز نمی‌شد.
        if (Update.UpdateExit.Active) return;
        if (Phase != AppPhase.Ready || !ExitBackup.Needed(AppSettings.Load().AskBackupOnExit)) return;
        var last = BackupSectionViewModel.LastFullPath();
        var where = last is null ? "روی همین کامپیوتر (عکسِ امروز)" : $"در فایلِ قبلی «{Path.GetFileName(last)}»";
        if (!await Dialogs.ConfirmAsync("پیش از بستن",
                "در این اجرا چیزهایی نوشته شده و هنوز بکاپ نگرفته‌اید.\n"
                + $"بکاپ {where} گرفته شود؟", "بله، بکاپ بگیر", "بستن بی بکاپ"))
            return;
        var backup = Sections.SelectMany(s => s.SubSections).OfType<BackupSectionViewModel>().FirstOrDefault();
        if (last is not null && backup is not null && await backup.WriteFullAsync(last)) return;
        try { if (await Task.Run(() => AppHost.Current.Backup.SnapshotToday()) is not null) ExitBackup.Mark(); } catch { }
    }

    /// <summary>
    /// ══ «همه‌اش را همین حالا بنویس» ════════════════════════════════════════
    /// پیش از بسته شدنِ برنامه و با <c>Ctrl+S</c>.
    ///
    /// دو تکه، و هر دو لازم‌اند:
    ///   • <see cref="Services.SaveGuard"/> — هر ردیفِ کثیفِ کلِ برنامه، حتی
    ///     در بخشی که کاربر ساعت‌ها پیش تویش بوده.
    ///   • <c>FlushAsync</c>ِ خودِ بخش و صفحهٔ باز — نوشته‌هایی که ردیفِ جدول
    ///     نیستند (سربرگ‌ها).
    ///
    /// ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد: این روی مسیرِ بسته شدنِ پنجره است و
    /// یک استثنا یعنی برنامه‌ای که بسته نمی‌شود.
    /// </summary>
    public async Task<int> FlushEverythingAsync()
    {
        foreach (var target in new object?[] { ActiveSection?.ActivePage, ActiveSection })
        {
            var task = target?.GetType()
                             .GetMethod(Services.ShortcutService.FlushMethodName, Type.EmptyTypes)?
                             .Invoke(target, null) as Task;
            if (task is null) continue;
            try { await task; } catch { /* نگهبانِ پایین دوباره امتحانش می‌کند */ }
        }
        //  ⛔ و مقدارهای راحتیِ در صف (پهنای ستون‌ها، تم، آخرین بخش): بی
        //  این، ستونی که همین حالا پهن شده و برنامه بسته شود گم می‌شود.
        try { Services.AppSettings.FlushNow(); } catch { }

        try { return await Services.SaveGuard.FlushAllAsync(); }
        catch { return 0; }
    }

    /// <summary>پیام‌های کوتاهِ پایینِ صفحه.</summary>
    public Services.ToastService Toasts => AppHost.Current.Toasts;
}
