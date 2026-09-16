using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Views;

public partial class MainWindow : Window
{
    /// <summary>کشیدنِ گوشهٔ بالا-چپِ ماشین‌حساب: به چپ/بالا = بزرگ‌تر.</summary>
    private void OnCalcResize(object? sender, Avalonia.Input.VectorEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm) vm.Calculator.Resize(-e.Vector.X, -e.Vector.Y);
    }

    private readonly DispatcherTimer? _clock;
    private readonly ShortcutService? _keys;
    private readonly FieldNavigationService? _fieldNav;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;

        // میانبرهای سراسری — همان‌هایی که کاربر در نسخهٔ وب داشت
        _keys = new ShortcutService(this, vm);

        // ناوبریِ اکسل‌مانندِ بینِ کادرهای فرم‌ها (جدول‌ها خودشان دارند)
        _fieldNav = new FieldNavigationService(this);

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var day = DateTime.Now.Date;
        _clock.Tick += (_, _) =>
        {
            vm.Clock = PumpYaqobi.App.Localization.Clock.Now();
            vm.TickServerDot();
            // ══ نیمه‌شب: تاریخِ سربرگ و نوار هم عوض شوند ══════════════════
            // چک‌لیستِ تحویل (بندِ ۶۲–۶۴): پیش از این «امروز» فقط یک بار در
            // ساخت خوانده می‌شد و برنامه‌ای که شب باز مانده بود، صبح هنوز
            // تاریخِ دیروز را نشان می‌داد.
            if (DateTime.Now.Date != day) { day = DateTime.Now.Date; vm.DayChanged(); }
        };
        _clock.Start();
        vm.Clock = PumpYaqobi.App.Localization.Clock.Now();

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

            var chrome = (header?.Bounds.Height ?? 0) + (banner?.Bounds.Height ?? 0);
            var y = Math.Max(0, chrome - scroll.Offset.Y);
            if (Math.Abs(slide.Y - y) > 0.5) slide.Y = y;
        }

        scroll.ScrollChanged += (_, _) => Place();
        scroll.LayoutUpdated += (_, _) => Place();
        Place();
    }
}
