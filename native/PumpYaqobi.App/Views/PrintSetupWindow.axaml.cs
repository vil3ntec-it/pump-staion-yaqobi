using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.Printing;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.Views;

/// <summary>
/// پنجرهٔ «تنظیمِ ورق» — چهار زبانه. ‎null‎ برمی‌گرداند اگر کاربر انصراف
/// بدهد — همان قرارِ <see cref="DialogWindow"/>.
///
/// دکمه‌های کدِ سربرگ/پاورقی این‌جا ساخته می‌شوند (نه در XAML) چون فهرستشان
/// در خودِ ویومدل است و هر کدام یک فرمان با پارامتر می‌زند.
/// </summary>
public partial class PrintSetupWindow : Window
{
    public PrintSetupWindow() => AvaloniaXamlLoader.Load(this);

    public PrintSetupWindow(PrintSetupViewModel vm) : this()
    {
        DataContext = vm;
        var host = this.FindControl<WrapPanel>("Tokens");
        if (host is null) return;
        foreach (var (label, token) in PrintSetupViewModel.Tokens)
        {
            var b = new Button { Content = label, Classes = { "tok" } };
            b.Click += (_, _) => vm.InsertTokenCommand.Execute(token);
            host.Children.Add(b);
        }
    }

    /// <summary>کدام کادر فوکوس گرفت — دکمه‌های کد همان‌جا می‌نویسند.</summary>
    private void OnBoxFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox tb && DataContext is PrintSetupViewModel vm && tb.Name is { } n)
            vm.LastBox = n;
    }

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Close((DataContext as PrintSetupViewModel)?.Build());

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    /// <summary>پنجره را باز کن و تنظیمِ تازه را بگیر. ‎null‎ = انصراف.</summary>
    public static async Task<PageSetup?> ShowAsync(Window owner, PageSetup current) =>
        await new PrintSetupWindow(new PrintSetupViewModel(current))
            .ShowDialog<PageSetup?>(owner);
}
