using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

public partial class CompanyPurchasesView : UserControl
{
    private CompanyPurchasesPageViewModel? _vm;

    public CompanyPurchasesView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as CompanyPurchasesPageViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
            ShowHit();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_vm is null && DataContext is CompanyPurchasesPageViewModel vm)
        {
            _vm = vm;
            vm.PropertyChanged += OnVmChanged;
        }
        ShowHit();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnVmChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompanyPurchasesPageViewModel.HighlightedId)) ShowHit();
    }

    /// <summary>
    /// ⛔ «جستجوی خرید» تا ۳.۱.۲۱۳ فقط <c>HighlightedId</c> را می‌نوشت و هیچ نمایی آن را
    /// نمی‌خواند — خرید نه پررنگ می‌شد نه جلوی چشم می‌آمد. پس از چیده شدنِ کارت‌ها
    /// (اولویتِ ‎Loaded‎) کارتِ نشان‌دار جلوی چشم آورده می‌شود.
    /// </summary>
    private void ShowHit()
    {
        if (DataContext is not CompanyPurchasesPageViewModel { HighlightedId: > 0 }) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsEffectivelyVisible) return;
            var hit = this.GetVisualDescendants().OfType<Border>()
                          .FirstOrDefault(b => b.DataContext is PurchaseItemViewModel { IsHighlighted: true }
                                               && b.Classes.Contains("hit"));
            hit?.BringIntoView();
        }, DispatcherPriority.Loaded);
    }
}
