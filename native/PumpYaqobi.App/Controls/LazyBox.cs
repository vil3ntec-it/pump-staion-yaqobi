using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ تا به آن نرسیده‌ای، ساخته نمی‌شود ═══════════════════════════════════════
///
/// خواستهٔ صاحب ریپو:
///
///   «کاری کن زود باز بشه ولی جدول‌ها داخلِ همون بخش لود بشه — نه این‌که تو
///    همون بخش یا حساب برم همه رو یک‌جا بخاد لود کنه. تو همون بخش که رفت، با
///    اسکرول جدول‌های پایین زود لود بشن.»
///
/// و اندازه‌گیری هم دقیقاً همین را گفت (سنجشِ ‎waraqperf‎): خواندنِ ورق از
/// دیتابیس ۲ میلی‌ثانیه است و ساختنِ ویومدلش ۴، ولی **هر پاسِ چیدمانِ صفحه**
/// حدودِ ۴۰۰ میلی‌ثانیه — چون صفحه هر سه جدولش را یک‌جا می‌سازد و هر کدام
/// هم‌قدِ ردیف‌هایش بلند می‌شود. پس هر پاس روی هر هشتاد ردیف کار می‌کند.
///
/// این کادر همان را می‌شکند: محتوایش را نگه می‌دارد ولی **نمی‌سازد** تا وقتی
/// که نزدیکِ قابِ دید برسد. تا آن موقع فقط یک جای خالی به بلندیِ
/// <see cref="PlaceholderHeight"/> است، پس نوارِ لغزش هم نمی‌پرد.
///
/// ⚠️ یک بار که ساخته شد، دیگر خراب نمی‌شود. برداشتنِ محتوا در اسکرولِ برگشت
/// هم «صرفه‌جویی» است هم چشمک زدن و از دست رفتنِ جای اسکرول و متنِ نیمه‌تایپ —
/// و خواستهٔ صاحب ریپو صریح بود که برگشتن نباید تأخیر داشته باشد.
/// </summary>
public class LazyBox : ContentControl
{
    /// <summary>
    /// قالبی که تا نزدیک نشده‌ایم پیاده نمی‌شود. عمداً جدا از
    /// <see cref="ContentControl.ContentTemplate"/> است: آن یکی را خودمان
    /// سرِ وقتش پر می‌کنیم.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> DeferredProperty =
        AvaloniaProperty.Register<LazyBox, IDataTemplate?>(nameof(Deferred));

    public IDataTemplate? Deferred
    {
        get => GetValue(DeferredProperty);
        set => SetValue(DeferredProperty, value);
    }

    /// <summary>
    /// بلندیِ جای خالی پیش از ساخته شدن — تقریبی، فقط برای این‌که نوارِ لغزش
    /// جهش نکند و کاربر بفهمد پایین‌تر چیزی هست.
    /// </summary>
    public static readonly StyledProperty<double> PlaceholderHeightProperty =
        AvaloniaProperty.Register<LazyBox, double>(nameof(PlaceholderHeight), 240d);

    public double PlaceholderHeight
    {
        get => GetValue(PlaceholderHeightProperty);
        set => SetValue(PlaceholderHeightProperty, value);
    }

    /// <summary>
    /// چقدر زودتر از رسیدن به قابِ دید ساخته شود. یک صفحهٔ کامل جلوتر، تا
    /// کاربر هیچ‌وقت جای خالی نبیند.
    /// </summary>
    private const double Lookahead = 900;

    private bool _open;
    private ScrollViewer? _page;
    private bool _wired;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_open && MinHeight <= 0) MinHeight = PlaceholderHeight;
        Wire();
        // ⚠️ یک پاس دیرتر: همین حالا هنوز چیده نشده‌ایم و مختصاتمان صفر است،
        // پس هر جدولی «داخلِ دید» به نظر می‌رسید و همه با هم ساخته می‌شدند.
        Dispatcher.UIThread.Post(Check, DispatcherPriority.Background);
    }

    private void Wire()
    {
        if (_wired) return;
        _page = this.GetVisualAncestors().OfType<ScrollViewer>()
                    .FirstOrDefault(v => v.Name == "PageScroll");
        if (_page is null) return;
        _wired = true;
        _page.ScrollChanged += (_, _) => Check();
        _page.PropertyChanged += (_, a) =>
        {
            if (a.Property == BoundsProperty) Check();
        };
    }

    /// <summary>نزدیک شده‌ایم؟ اگر آری، همین حالا و برای همیشه ساخته شود.</summary>
    private void Check()
    {
        if (_open) return;
        Wire();

        // قابِ صفحه پیدا نشد ⇒ این کادر داخلِ اسکرولِ صفحه نیست. آن وقت
        // «تنبلی» معنا ندارد و بی‌درنگ ساخته می‌شود — وگرنه چیزی هرگز
        // دیده نمی‌شود.
        if (_page is null) { Open(); return; }
        if (this.GetVisualRoot() is null) return;

        if (this.TranslatePoint(new Point(0, 0), _page) is not { } at) return;
        var top = at.Y;
        var bottom = top + Math.Max(Bounds.Height, PlaceholderHeight);

        if (bottom >= -Lookahead && top <= _page.Viewport.Height + Lookahead) Open();
    }

    private void Open()
    {
        if (_open) return;
        _open = true;
        MinHeight = 0;
        ContentTemplate = Deferred;
    }
}
