using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// «روی هر ردیف (یا دکمهٔ جزئیات) بزنید تا صفحهٔ کاملِ همان قرض‌دار باز شود» —
/// جملهٔ خودِ سایت. در جدولِ نیتیو کلیکِ ساده خانه را انتخاب می‌کند، پس باز
/// کردن با دوبار کلیک است تا انتخابِ خانه از دست نرود؛ دکمهٔ «جزئیات» هم هست.
/// </summary>
public partial class PriceLossSectionView : UserControl
{
    public PriceLossSectionView() => AvaloniaXamlLoader.Load(this);

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PriceLossSectionViewModel vm &&
            sender is DataGrid { SelectedItem: PriceLossRowViewModel row })
            vm.OpenRowCommand.Execute(row);
    }
}
