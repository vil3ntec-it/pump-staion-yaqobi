using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

public partial class StaffShortSectionView : UserControl
{
    private StaffShortSectionViewModel? _vm;

    public StaffShortSectionView() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// «رسید کمبودی»ِ کنارِ نام ⇒ فوکوس روی کادرِ مبلغ (۱۴۰۵/۰۷/۱۸) — پیش از این زدنش فقط
    /// ردیف را برمی‌گزید و کادرِ ثبتِ پایینِ صفحه از چشم دور بود: «دکمه کار نمی‌کند».
    /// </summary>
    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.AmountFocusRequested -= FocusAmount;
        _vm = DataContext as StaffShortSectionViewModel;
        if (_vm is not null) _vm.AmountFocusRequested += FocusAmount;
    }

    private void FocusAmount() => Dispatcher.UIThread.Post(() =>
    {
        if (this.FindControl<TextBox>("AmountBox") is not { } box) return;
        box.BringIntoView();
        box.Focus();
        box.SelectAll();
    }, DispatcherPriority.Background);
}
