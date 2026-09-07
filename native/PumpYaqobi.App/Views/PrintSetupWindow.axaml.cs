using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.Printing;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.Views;

/// <summary>
/// پنجرهٔ «تنظیمِ ورق». ‎null‎ برمی‌گرداند اگر کاربر انصراف بدهد — همان قرارِ
/// <see cref="DialogWindow"/>.
/// </summary>
public partial class PrintSetupWindow : Window
{
    public PrintSetupWindow() => AvaloniaXamlLoader.Load(this);

    public PrintSetupWindow(PrintSetupViewModel vm) : this() => DataContext = vm;

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Close((DataContext as PrintSetupViewModel)?.Build());

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    /// <summary>پنجره را باز کن و تنظیمِ تازه را بگیر. ‎null‎ = انصراف.</summary>
    public static async Task<PageSetup?> ShowAsync(Window owner, PageSetup current) =>
        await new PrintSetupWindow(new PrintSetupViewModel(current))
            .ShowDialog<PageSetup?>(owner);
}
