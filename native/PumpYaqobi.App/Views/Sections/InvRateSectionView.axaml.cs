using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Views.Sections;

public partial class InvRateSectionView : UserControl
{
    public InvRateSectionView()
    {
        AvaloniaXamlLoader.Load(this);

        // ══ «جای دیگر که کلیک کردم، از همه را نشان بده» ═════════════════════
        // خواستهٔ صریحِ صاحب ریپو. انتخابِ ردیف سربرگ را روی همان یک فاکتور
        // می‌برد؛ پس برداشتنِ انتخاب هم باید به همان سادگی باشد.
        //
        // ⚠️ تونلی، نه حبابی: کلیکِ داخلِ جدول را خودِ ‎DataGrid‎ مصرف می‌کند و
        // شنوندهٔ حبابی برای «بیرون» هیچ‌وقت خبر نمی‌شود.
        AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (this.FindControl<Controls.ExcelGrid>("RateGrid") is not { } grid) return;
        if (e.Source is Visual v && (ReferenceEquals(v, grid) || v.GetVisualAncestors().Contains(grid)))
            return;
        grid.SelectedItem = null;
    }
}
