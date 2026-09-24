using Avalonia.Controls;
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
        var overlay = this.FindControl<Panel>("BuyOverlay");
        if (overlay is null) return;
        overlay.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty || !Equals(e.NewValue, true)) return;
            //  یک تیک بعد: پیش از چیدمان، جای پنجره هنوز معنا ندارد.
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    this.FindControl<Border>("BuyCard")?.BringIntoView();
                    var first = overlay.GetVisualDescendants().OfType<TextBox>()
                                       .FirstOrDefault(t => t.IsEffectivelyVisible && t.IsEffectivelyEnabled);
                    first?.Focus();
                    first?.SelectAll();
                }
                catch { /* رفاه است، نه شرطِ باز شدنِ پنجره */ }
            }, DispatcherPriority.Background);
        };
    }
}
