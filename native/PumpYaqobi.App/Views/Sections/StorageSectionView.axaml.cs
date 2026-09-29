using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Views.Sections;

public partial class StorageSectionView : UserControl
{
    public StorageSectionView()
    {
        AvaloniaXamlLoader.Load(this);

        // ══════════════════════════════════════════════════════════════════
        //  ══ «ثبت خرید» همان‌جا که چشمِ کاربر است باز شود ══════════════════
        // ══════════════════════════════════════════════════════════════════
        //
        //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «وقتی می‌خواهم خرید را ثبت کنم،
        //  روی برگهٔ ثبت در جا قفل بشود و همان لحظه نشانش بدهد — نه این‌که
        //  اسکرول کنم بروم بعد بنویسم؛ در جا سرش برود که زودتر برسانم.»
        //
        //  ⛔ ریشه: این پنجره یک روپوشِ ‎VerticalAlignment="Center"‎ است، ولی
        //  «وسط» را نسبت به **کلِ محتوای بخش** می‌گیرد، نه نسبت به آن‌چه
        //  کاربر می‌بیند. بخشِ مخزن با فهرستِ خریدها چند صفحه بلند است، پس
        //  پنجره وسطِ همان چند صفحه می‌نشست — یعنی جایی که کاربر باید
        //  دنبالش می‌گشت.
        //
        //  ⚠️ چارهٔ درست «روپوش را شناور کن» نبود: این بخش داخلِ اسکرولِ
        //  صفحه است و شناور کردنش یعنی یک لایهٔ تازهٔ چیدمان برای یک پنجره.
        //  کاری که واقعاً لازم است یک چیز است: **همان لحظه ببرش جلوی چشم**،
        //  و فوکوس را بگذار روی نخستین کادر تا تایپ همان‌جا شروع شود.
        //  ⛔ «0730» ⇐ «0.730» همان لحظهٔ تایپ (۱۴۰۵/۰۷/۱۷). قاعده فقط در
        //  ‎DensityInput.Typed‎ است؛ این‌جا فقط کادر را همان‌طور نشان می‌دهد و
        //  مکان‌نما را ته متن می‌گذارد. حسابِ ویومدل با ‎DensityInput.Parse‎ همین
        //  را جدا هم می‌خواند، پس متنِ چسبانده هم درست است.
        if (this.FindControl<TextBox>("BuyDensityBox") is { } dens)
            dens.TextChanged += (_, _) =>
            {
                var cur = dens.Text ?? "";
                var want = PumpYaqobi.Application.Services.DensityInput.Typed(cur);
                if (want == cur) return;
                dens.Text = want;
                dens.CaretIndex = want.Length;
            };

        var overlay = this.FindControl<Panel>("BuyOverlay");
        if (overlay is null) return;

        //  «روی برگهٔ ثبت درجا قفل بشه» — آوردنش جلوی چشم نیمی از کار بود:
        //  سنجهٔ ‎audit11‎ نشان داد یک چرخِ ماوس (روی خودِ فرم یا روی سایهٔ
        //  دورش) صفحه را می‌لغزاند و فرم ۶۰۰ پیکسل بالاتر از قاب می‌رفت. تا
        //  فرم باز است، چرخ مالِ خودِ فرم است: کادرِ لغزانِ داخلش اول
        //  (حبابی — خودش زودتر می‌گیرد) و هر چه ماند این‌جا بلعیده می‌شود.
        overlay.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (overlay.IsVisible) e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: false);
        overlay.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty || !Equals(e.NewValue, true)) return;
            //  یک تیک بعد: پیش از چیدمان، جای پنجره هنوز معنا ندارد.
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    FitCard();
                    var first = overlay.GetVisualDescendants().OfType<TextBox>()
                                       .FirstOrDefault(t => t.IsEffectivelyVisible && t.IsEffectivelyEnabled);
                    first?.Focus();
                    first?.SelectAll();
                }
                catch { /* رفاه است، نه شرطِ باز شدنِ پنجره */ }
            }, DispatcherPriority.Background);
        };
    }

    /// <summary>
    /// ⛔ پنجرهٔ خرید <b>کامل</b> جلوی چشم — نه «یادداشت نیمه دیده می‌شود»
    /// (عکسِ صاحب ریپو، ۱۴۰۵/۰۷/۱۶، روی کامپیوتری با پنجرهٔ کوتاه‌تر).
    ///
    /// سقفِ ۶۴۰ی قاب از بلندیِ دیدِ صفحه بزرگ‌تر بود، و نوارِ بخش‌ها روی بالای
    /// همان دید شناور است؛ پس ‎BringIntoView‎ی ساده بالای قاب را زیرِ نوار یا
    /// پایینش را بیرونِ پنجره می‌گذاشت. حالا قاب هم‌اندازهٔ دیدِ واقعی (منهای
    /// نوار) می‌شود و درست زیرِ نوار آورده می‌شود؛ باقی را کادرِ لغزانِ خودِ
    /// قاب نشان می‌دهد.
    /// </summary>
    private void FitCard()
    {
        var card = this.FindControl<Border>("BuyCard");
        if (card is null) return;
        var sv = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault(s => s.Name == "PageScroll")
                 ?? this.GetVisualAncestors().OfType<ScrollViewer>().LastOrDefault();
        var nav = TopLevel.GetTopLevel(this)?.GetVisualDescendants().OfType<Border>()
                          .FirstOrDefault(b => b.Name == "NavBar");
        var navH = nav is { IsVisible: true } ? nav.Bounds.Height : 0;
        const double gap = 12;
        if (sv is not null && sv.Viewport.Height > 0)
            card.MaxHeight = Math.Max(320, Math.Min(760, sv.Viewport.Height - navH - gap * 2));
        card.UpdateLayout();
        //  بالا تا زیرِ نوار هم باید دیده شود — پس مستطیلِ «به دید بیاور» از بالا بلندتر است
        card.BringIntoView(new Avalonia.Rect(0, -(navH + gap), card.Bounds.Width,
                                             card.Bounds.Height + navH + gap * 2));
    }
}
