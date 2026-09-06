using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// ورودِ سریعِ رسید — بی هیچ دکمهٔ «ثبت».
///
/// نسخهٔ وب با ‎onchange‎ کار می‌کند: به‌محضِ اینکه کاربر از کادرِ مبلغ بیرون
/// برود (یا Enter بزند) و نام و مبلغ هر دو پر باشند، رسید ثبت می‌شود. این‌جا
/// هم همان دو رویداد به همان یک فرمان وصل شده‌اند.
/// </summary>
public partial class DebtReceiptSectionView : UserControl
{
    public DebtReceiptSectionView()
    {
        AvaloniaXamlLoader.Load(this);

        var name = this.FindControl<TextBox>("NameBox");
        var amount = this.FindControl<TextBox>("AmountBox");

        if (amount is not null)
        {
            amount.LostFocus += (_, _) => Submit();
            amount.KeyDown += OnEnter;
        }
        if (name is not null) name.KeyDown += OnEnter;

        DataContextChanged += (_, _) =>
        {
            if (DataContext is DebtReceiptSectionViewModel vm)
                vm.FocusNameRequested += () => name?.Focus();
        };
    }

    private void OnEnter(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return)) return;
        e.Handled = true;
        Submit();
    }

    private void Submit()
    {
        if (DataContext is DebtReceiptSectionViewModel vm && vm.SubmitCommand.CanExecute(null))
            vm.SubmitCommand.Execute(null);
    }
}
