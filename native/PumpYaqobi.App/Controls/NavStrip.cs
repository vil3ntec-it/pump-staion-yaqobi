using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ نوارِ افقیِ بخش‌ها ═══════════════════════════════════════════════════════
/// همان ‎&lt;div class="nav"&gt;‎ نسخهٔ وب: یک نوارِ افقی که اگر جا کم بیاورد
/// می‌لغزد.
///
/// سه چیز که ‎ScrollViewer‎ِ خالی نداشت و نبودشان توی چشم می‌زد:
///
///  ۱) <b>نوارِ لغزشِ افقی، دکمه‌ها را می‌بُرید.</b> ارتفاعِ نوار به اندازهٔ
///     دکمه‌ها بود و نوارِ لغزش از پایین همان را می‌خورد، پس زیرِ نوشتهٔ هر
///     دکمه بریده می‌شد. حالا نوارِ لغزش پنهان است و ارتفاع ثابت.
///  ۲) <b>چرخِ ماوس هیچ کاری نمی‌کرد.</b> در نواری که فقط افقی می‌لغزد،
///     ‎ScrollViewer‎ چرخ را نادیده می‌گیرد. این‌جا چرخ به لغزشِ افقی ترجمه
///     می‌شود — همان کاری که مرورگر روی ‎overflow-x:auto‎ می‌کند.
///  ۳) <b>معلوم نبود بخشِ دیگری هم هست.</b> دو دکمهٔ «‹» و «›» فقط وقتی
///     پیدا می‌شوند که واقعاً چیزی بیرونِ قاب مانده باشد.
///
/// ⚠️ بخشِ فعال باید همیشه دیده شود: با عوض شدنِ بخش (چه با ماوس، چه با
/// ‎Ctrl+Shift+عدد‎) نوار خودش تا همان دکمه می‌لغزد.
/// </summary>
public class NavStrip : ContentControl
{
    private ScrollViewer? _sv;
    private Button? _prev;
    private Button? _next;

    /// <summary>یک «صفحه» لغزش برای هر بار زدنِ دکمه یا چرخاندنِ چرخ.</summary>
    private const double Step = 220;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _sv = e.NameScope.Find<ScrollViewer>("PART_Scroll");
        _prev = e.NameScope.Find<Button>("PART_Prev");
        _next = e.NameScope.Find<Button>("PART_Next");

        if (_sv is not null)
        {
            _sv.ScrollChanged += (_, _) => UpdateArrows();
            // ⚠️ فقط ‎ScrollChanged‎ کافی نیست: با عوض شدنِ بخش، نوشتهٔ دکمهٔ
            // فعال پررنگ می‌شود و پهنای کلِ نوار تغییر می‌کند بی‌آن‌که لغزشی
            // رخ دهد. آن‌وقت فلش‌ها روی حسابِ پهنای کهنه می‌ماندند و روی
            // آخرین بخش هم «باز هم هست» نشان می‌دادند.
            _sv.LayoutUpdated += (_, _) => UpdateArrows();
            // چرخِ ماوس → لغزشِ افقی
            _sv.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        }

        // در راست‌به‌چپ «قبلی» سمتِ راست است، ولی جهتِ لغزش را خودِ Avalonia
        // برعکس می‌کند؛ پس هر دکمه همان کاری را می‌کند که نامش می‌گوید.
        if (_prev is not null) _prev.Click += (_, _) => By(-Step);
        if (_next is not null) _next.Click += (_, _) => By(Step);

        Dispatcher.UIThread.Post(UpdateArrows, DispatcherPriority.Loaded);
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_sv is null) return;
        By(-e.Delta.Y * Step * 0.5);
        e.Handled = true;
    }

    private void By(double delta)
    {
        if (_sv is null) return;
        var max = Math.Max(0, _sv.Extent.Width - _sv.Viewport.Width);
        var x = Math.Clamp(_sv.Offset.X + delta, 0, max);
        _sv.Offset = _sv.Offset.WithX(x);
    }

    private void UpdateArrows()
    {
        if (_sv is null) return;
        var max = Math.Max(0, _sv.Extent.Width - _sv.Viewport.Width);
        if (_prev is not null) _prev.IsVisible = _sv.Offset.X > 1;
        if (_next is not null) _next.IsVisible = _sv.Offset.X < max - 1;
    }

    /// <summary>
    /// دکمهٔ بخشِ فعال را داخلِ قاب می‌آورد. صداکننده‌اش پوستهٔ برنامه است، هر
    /// بار که بخش عوض شود — از هر راهی که عوض شده باشد.
    /// </summary>
    public void BringActiveIntoView()
    {
        if (_sv is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            var active = this.GetVisualDescendants().OfType<Button>()
                             .FirstOrDefault(b => b.Classes.Contains("active"));
            active?.BringIntoView();

            // حاشیهٔ ده‌پیکسلیِ دو سرِ نوار باعث می‌شد ‎BringIntoView‎ همان ده
            // پیکسل به‌اضافهٔ حاشیهٔ خودِ دکمه کم بلغزد (اندازه‌گیری: ۱۳ پیکسل
            // روی آخرین بخش)؛ نتیجه‌اش این بود که روی بخشِ اولی یا آخری، فلشِ
            // «باز هم هست» بی‌خود روشن می‌ماند در حالی که چیزی جز فاصله نمانده
            // بود. اگر تا لبه فقط همان فاصله‌ها مانده، تا ته می‌لغزیم.
            const double edge = 24;
            var max = Math.Max(0, _sv.Extent.Width - _sv.Viewport.Width);
            if (_sv.Offset.X > max - edge) _sv.Offset = _sv.Offset.WithX(max);
            else if (_sv.Offset.X < edge) _sv.Offset = _sv.Offset.WithX(0);

            UpdateArrows();
        }, DispatcherPriority.Background);
    }
}
