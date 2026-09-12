using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// «با کلیک روی هر خط، حساب کامل همان شخص باز می‌شود» — جملهٔ خودِ سایت.
/// در جدولِ نیتیو کلیکِ ساده خانه را انتخاب می‌کند، پس باز کردن با دوبار کلیک
/// است تا انتخابِ خانه از دست نرود.
/// </summary>
public partial class DebtSummarySectionView : UserControl
{
    public DebtSummarySectionView() => AvaloniaXamlLoader.Load(this);

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DebtSummarySectionViewModel vm &&
            sender is DataGrid { SelectedItem: DebtSummaryRowViewModel row })
            vm.OpenCommand.Execute(row);
    }
}
