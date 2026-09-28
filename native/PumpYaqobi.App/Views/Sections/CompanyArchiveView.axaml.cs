using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

public partial class CompanyArchiveView : UserControl
{
    private CompanyArchivePageViewModel? _vm;

    public CompanyArchiveView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => Hook();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Hook();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void Hook()
    {
        var now = DataContext as CompanyArchivePageViewModel;
        if (ReferenceEquals(now, _vm)) { ShowHit(); return; }
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = now;
        if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        ShowHit();
    }

    private void OnVmChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompanyArchivePageViewModel.HighlightedArchive)) ShowHit();
    }

    /// <summary>
    /// ⛔ «جستجوی خرید» تا ۳.۱.۲۱۳ فقط شمارهٔ ردیف را می‌نوشت و هیچ نمایی آن را
    /// نمی‌خواند. پس از چیده شدنِ آرشیوِ باز، جدولش جلوی چشم می‌آید و ردیف در خودِ
    /// جدول به دید. ⚠️ خودِ ‎ExcelGrid‎ درخواستِ «به دید بیاور» را می‌بلعد (عمداً)،
    /// پس کادرِ دورِ جدول درخواست را می‌فرستد.
    /// </summary>
    private void ShowHit()
    {
        if (_vm?.HighlightedArchive is not { } arc) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsEffectivelyVisible) return;
            var grid = this.GetVisualDescendants().OfType<ExcelGrid>()
                           .FirstOrDefault(g => ReferenceEquals(g.DataContext, arc));
            if (grid is null) return;
            if (arc.SelectedRow is { } row) grid.ScrollIntoView(row, null);
            (grid.GetVisualParent() as Control ?? grid).BringIntoView();
        }, DispatcherPriority.Loaded);
    }
}
