using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// ورودِ سریعِ رسید — بی هیچ دکمهٔ «ثبت».
///
/// نسخهٔ وب با ‎onchange‎ کار می‌کند: به‌محضِ اینکه کاربر از کادرِ مبلغ بیرون
/// برود (یا Enter بزند) و نام و مبلغ هر دو پر باشند، رسید ثبت می‌شود.
///
/// ⚠️ از ۱۴۰۵/۰۷/۱۴ «رسید» بغلِ نام است و واحد، نوعِ تیل و «حساب» (چکنه/
/// قرض‌دار) **بعد از** آن می‌آیند. پس بیرون رفتن از کادرِ مبلغ دیگر نمی‌تواند
/// ثبت کند — کاربر هنوز واحد و حساب را برنگزیده. ثبت وقتی است که فوکوس از
/// **کلِ فرم** بیرون برود (یا Enter). رفتن به کشوییِ خودِ فرم و بازشوی آن
/// (پنجرهٔ جدا) بیرون رفتن نیست.
/// </summary>
public partial class DebtReceiptSectionView : UserControl
{
    public DebtReceiptSectionView()
    {
        AvaloniaXamlLoader.Load(this);

        var name = this.FindControl<TextBox>("NameBox");
        var amount = this.FindControl<TextBox>("AmountBox");
        var form = this.FindControl<WrapPanel>("QuickForm");

        if (amount is not null) amount.KeyDown += OnEnter;
        if (name is not null) name.KeyDown += OnEnter;
        if (form is not null)
        {
            form.AddHandler(KeyDownEvent, (_, e) =>
            {
                //  Enter روی کشوییِ باز مالِ خودِ کشویی است
                if (e.Source is ComboBox { IsDropDownOpen: true }) return;
                OnEnter(null, e);
            }, Avalonia.Interactivity.RoutingStrategies.Bubble);
            form.AddHandler(LostFocusEvent, (_, _) =>
                Dispatcher.UIThread.Post(() => { if (!FocusInside(form)) Submit(); }, DispatcherPriority.Input),
                Avalonia.Interactivity.RoutingStrategies.Bubble);
        }

        DataContextChanged += (_, _) =>
        {
            if (DataContext is DebtReceiptSectionViewModel vm)
                vm.FocusNameRequested += () => name?.Focus();
        };
    }

    /// <summary>فوکوس هنوز در همین فرم است — یا در بازشوی یکی از کشویی‌هایش.</summary>
    private bool FocusInside(Visual form)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused is null) return false;
        if (focused == form || form.IsVisualAncestorOf(focused)) return true;
        return focused.GetVisualRoot() is PopupRoot;
    }

    private void OnEnter(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || e.Handled) return;
        e.Handled = true;
        Submit();
    }

    private void Submit()
    {
        if (DataContext is DebtReceiptSectionViewModel vm && vm.SubmitCommand.CanExecute(null))
            vm.SubmitCommand.Execute(null);
    }
}
