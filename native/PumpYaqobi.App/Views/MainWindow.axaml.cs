using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Views;

public partial class MainWindow : Window
{
    /// <summary>کشیدنِ گوشهٔ بالا-چپِ ماشین‌حساب: به چپ/بالا = بزرگ‌تر.</summary>
    /// <summary>
    /// راست‌کلیک روی بخشِ نوار ⇐ جابه‌جا کردنش (۱۴۰۵/۰۷/۱۵). از کدِ پشت، نه
    /// اتصالِ ‎$parent[Window]‎: منوی راست‌کلیک در پنجرهٔ بازشوی جدا می‌نشیند و
    /// آن‌جا ‎Window‎ی بالادستی نیست. ‎DataContext‎ی آیتمِ منو همان بخش است.
    /// </summary>
    /// <summary>راست‌کلیکِ نوار — فقط «ترتیبِ پیش‌فرض». جابه‌جایی با کشیدن است (‎NavDrag‎).</summary>
    private void OnNavMove(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: "reset" } && DataContext is MainViewModel vm) vm.NavResetCommand.Execute(null);
    }

    /// <summary>کشیدن و رها کردنِ بخش‌های نوار (سنجه‌ها از این می‌خوانند).</summary>
    public Controls.NavDrag? NavDragger { get; private set; }

    private void OnCalcResize(object? sender, Avalonia.Input.VectorEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm) vm.Calculator.Resize(-e.Vector.X, -e.Vector.Y);
    }

    /// <summary>بستن یک بار لغو شد و حالا واقعاً می‌بندیم.</summary>
    private bool _closing;
    private bool _flushed;

    private readonly DispatcherTimer? _clock;
    private readonly ShortcutService? _keys;
    private readonly FieldNavigationService? _fieldNav;
    private readonly FocusOutService? _focusOut;

    public MainWindow()
    {
        InitializeComponent();
        PumpYaqobi.App.Controls.Spot.Root = this;   // «نشانم بده»ِ شروعِ سریع در همین پنجره می‌گردد
        var vm = new MainViewModel();
        DataContext = vm;

        // میانبرهای سراسری — همان‌هایی که کاربر در نسخهٔ وب داشت
        _keys = new ShortcutService(this, vm);

        // ناوبریِ اکسل‌مانندِ بینِ کادرهای فرم‌ها (جدول‌ها خودشان دارند)
        _fieldNav = new FieldNavigationService(this);

        // «کلیک روی جای خالی = بیرون آمدن از کادر» — شرحش بالای خودِ سرویس
        _focusOut = new FocusOutService(this);

        //  جابه‌جا کردنِ بخش‌های نوار با کشیدن (۱۴۰۵/۰۷/۱۶)
        if (this.FindControl<ItemsControl>("NavItems") is { } navItems)
            NavDragger = Controls.NavDrag.Attach(navItems, (item, index) => vm.MoveNavTo(item as SectionViewModel, index));

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var day = AppClock.Now.Date;
        var shownDay = Services.DisplayClock.Now.Date;
        //  تاریخِ نمایشی (‎DisplayClock‎) همان لحظه — نه یک ثانیه بعد
        Action onDisplay = () => Dispatcher.UIThread.Post(() =>
        {
            vm.Clock = PumpYaqobi.App.Localization.Clock.Now();
            shownDay = Services.DisplayClock.Now.Date;
            vm.DisplayDayChanged();
        });
        Services.DisplayClock.Changed += onDisplay;
        //  ⚠️ رویدادِ ایستا و زمان‌سنج با بسته شدنِ پنجره رها می‌شوند — وگرنه
        //  پنجرهٔ بسته (و ویومدلش) زنده می‌ماند و هر ثانیه کار می‌کرد.
        Closed += (_, _) => { Services.DisplayClock.Changed -= onDisplay; _clock?.Stop(); };
        _clock.Tick += (_, _) =>
        {
            vm.Clock = PumpYaqobi.App.Localization.Clock.Now();
            //  تاریخِ سربرگ نمایشی است و نیمه‌شبِ خودش را دارد؛ ماهِ بخش‌ها نه
            if (Services.DisplayClock.Now.Date != shownDay) { shownDay = Services.DisplayClock.Now.Date; vm.DisplayDayChanged(); }
            //  یک چراغ در سربرگ — و خودش هر دو تیک را می‌زند
            vm.TickLinkDot();
            //  نوارِ قفل هر ۳۰ ثانیه از همان درِ یگانه (‎RefreshLockBanner‎)
            vm.RefreshLockBanner();
            // ══ نیمه‌شب: تاریخِ سربرگ و نوار هم عوض شوند ══════════════════
            // چک‌لیستِ تحویل (بندِ ۶۲–۶۴): پیش از این «امروز» فقط یک بار در
            // ساخت خوانده می‌شد و برنامه‌ای که شب باز مانده بود، صبح هنوز
            // تاریخِ دیروز را نشان می‌داد.
            if (AppClock.Now.Date != day) { day = AppClock.Now.Date; vm.DayChanged(); }
        };
        _clock.Start();
        vm.Clock = PumpYaqobi.App.Localization.Clock.Now();

        // ══════════════════════════════════════════════════════════════════
        //  ══ بسته شدنِ برنامه، بی گم شدنِ یک نویسه ═════════════════════════
        // ══════════════════════════════════════════════════════════════════
        //
        //  گزارشِ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «چندین ورق رو پر کردم اما
        //  همه‌شون پاک شدن و هیچ چیزی ثبت نشده بودن… یارو اگه داشت جمله‌ای
        //  می‌نوشت از برنامه در جا بره بیرون، همون‌ها باید ثبت شده باشن.»
        //
        //  ⛔ ریشه: در کلِ برنامه **یک** شنوندهٔ ‎Closing‎ نبود (گشته شد:
        //  صفر). پس زدنِ ✕ یعنی:
        //    ۱) خانه‌ای که در حالِ ویرایش بود هیچ‌وقت ‎Commit‎ نمی‌شد —
        //       نوشته‌اش حتی به ویومدل هم نمی‌رسید، چه برسد به دیسک؛
        //    ۲) و هر ردیفی که پشتِ تأخیرِ ۳۵۰ میلی‌ثانیه‌ای بود می‌رفت.
        //
        //  ⚠️ بستن یک بار **لغو** می‌شود تا نوشتن تمام شود، و بعد خودمان
        //  دوباره می‌بندیم. ‎_closing‎ نگهبانِ حلقه است.
        //  ⚠️ و سقفِ وقت دارد (‎SaveGuard.FlushAllAsync‎): برنامه‌ای که بسته
        //  نمی‌شود از برنامه‌ای که یک ردیف گم می‌کند بدتر است.
        //  ⛔ ✕ِ دوم وسطِ نوشتن هم بستن را لغو می‌کند (۱۴۰۵/۰۷/۱۶) — پیش از این ‎_closing‎
        //  آن را بی ‎Cancel‎ رها می‌کرد، پنجره همان لحظه بسته می‌شد و ردیفی که با دیسکِ
        //  قفل دوباره تلاش می‌کرد گم می‌شد. فقط ‎Close()‎ِ خودمان پس از نوشتن رد می‌شود.
        Closing += async (_, e) =>
        {
            if (_flushed) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true;
            try { Controls.ExcelGrid.CommitFocused(this); } catch { }
            try { await vm.FlushEverythingAsync(); } catch { }
            try { await vm.OfferBackupBeforeExitAsync(); } catch { }
            try { await vm.SendToServerBeforeExitAsync(); } catch { }
            _flushed = true;
            Close();
        };

        //  ⚠️ دستگیرهٔ پنجره — تنها چیزی که اعلانِ خودِ ویندوز لازم دارد.
        //  پیش از باز شدنِ پنجره هنوز وجود ندارد، پس همین‌جا سرِ ‎Opened‎
        //  گرفته می‌شود؛ صفر یعنی «نمی‌شود» و بی‌صدا رد می‌شود.
        Opened += (_, _) =>
        {
            try { vm.WindowHandle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero; }
            catch { /* اعلانِ سیستمی رفاه است */ }
        };

        // با هر عوض شدنِ بخش، دکمهٔ همان بخش داخلِ قابِ نوار بیاید — چه با
        // ماوس زده شده باشد چه با ‎Ctrl+Shift+عدد‎.
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Current))
                this.FindControl<Controls.NavStrip>("Nav")?.BringActiveIntoView();
        };

        // ══ پردهٔ لودینگ، همان لحظهٔ ورود ═══════════════════════════════════
        //
        // خواستهٔ صاحب ریپو: «موقعِ تازه باز کردنِ اپ باید یک لودینگ داشته باشه
        // تا همهٔ بخش‌ها و برنامه رو رندر کنه؛ بعدش بازگشت به صفحهٔ اصلی نباید
        // تأخیری داشته باشه.»
        //
        // ⚠️ از این‌جا صدا زده می‌شود نه از خودِ ویومدل: قابِ گرم‌کن یک کنترلِ
        // واقعیِ همین پنجره است و ویومدل نباید به درختِ بصری دست بزند.
        // ══ پرده **پیش از** رمز، نه بعدش ═══════════════════════════════════
        //
        // گزارشِ صاحب ریپو: «یک صفحهٔ جدا موقعِ باز شدنِ اپ، نه این‌که رمز را
        // بزنم بعد بیاید… من اول فکر کردم برنامه خراب شده.»
        //
        // پس همین‌جا، در سازندهٔ پنجره، شروع می‌شود — پیش از آن‌که کاربر چیزی
        // ببیند. پرده که رفت، صفحهٔ رمز می‌آید.
        //
        // ⚠️ یک نوبت دیرتر، تا پنجره یک بار چیده شده باشد؛ گرم کردن روی
        // پنجره‌ای که هنوز اندازه ندارد هیچ کاری نمی‌کند.
        Dispatcher.UIThread.Post(() =>
        {
            _ = vm.WarmUpAsync(UpdateLayout);
        }, DispatcherPriority.Background);

        StickNavToTop();
    }

    /// <summary>
    /// ══ نوارِ بخش‌ها را چسبانِ بالا نگه می‌دارد ═══════════════════════════════
    ///
    /// در نسخهٔ وب این یک خط ‎CSS‎ است: ‎.nav{position:sticky;top:0}‎ — نوار با
    /// صفحه بالا می‌رود تا به سقف برسد، بعد همان‌جا می‌ماند و محتوا زیرش
    /// می‌لغزد؛ و با برگشتنِ اسکرول به بالا، دوباره سرِ جای خودش پایین
    /// می‌نشیند.
    ///
    /// آوالونیا ‎position:sticky‎ ندارد. نزدیک‌ترین معادلش همین است: نوار
    /// بیرونِ اسکرول‌ویور و روی آن شناور است، و جابه‌جاییِ عمودی‌اش برابرِ
    /// «چقدر از سربرگ و نوارِ آمار هنوز دیده می‌شود» گذاشته می‌شود.
    ///
    /// ⚠️ ‎LayoutUpdated‎ هم لازم است، نه فقط ‎ScrollChanged‎: بلندیِ سربرگ با
    /// عوض شدنِ بخش یا اندازهٔ پنجره فرق می‌کند و بی آن، نوار سرِ جای قدیمی
    /// می‌ماند. یک ‎TranslateTransform‎ ساخته می‌شود و بعد فقط ‎Y‎ی همان
    /// عوض می‌شود — نه یک شیء تازه در هر رویداد.
    /// </summary>
    private void StickNavToTop()
    {
        var scroll = this.FindControl<ScrollViewer>("PageScroll");
        var nav = this.FindControl<Border>("NavBar");
        var header = this.FindControl<Border>("HeaderBlock");
        var banner = this.FindControl<Border>("BannerBlock");
        var gap = this.FindControl<Panel>("NavGap");
        if (scroll is null || nav is null) return;

        var slide = new TranslateTransform();
        nav.RenderTransform = slide;

        void Place()
        {
            // جای خالیِ نوار دقیقاً هم‌بلندیِ خودِ نوار بماند — وگرنه اولین
            // چیزِ هر بخش زیرِ نوار پنهان می‌شود.
            if (gap is not null && Math.Abs(gap.Height - nav.Bounds.Height) > 0.5
                && nav.Bounds.Height > 0)
                gap.Height = nav.Bounds.Height;

            //  ⚠️ کنترلِ پنهان ‎Bounds‎ِ آخرش را نگه می‌دارد (همان تلهٔ ‎BlindTop‎)؛
            //  نوارِ خاموش (‎BannerPref‎) صفر است، وگرنه نوارِ بخش‌ها پایین می‌ماند
            var chrome = (header is { IsVisible: true } ? header.Bounds.Height : 0)
                       + (banner is { IsVisible: true } ? banner.Bounds.Height : 0);
            var y = Math.Max(0, chrome - scroll.Offset.Y);
            if (Math.Abs(slide.Y - y) > 0.5) slide.Y = y;
        }

        scroll.ScrollChanged += (_, _) => Place();
        scroll.LayoutUpdated += (_, _) => Place();
        Place();
    }
}
