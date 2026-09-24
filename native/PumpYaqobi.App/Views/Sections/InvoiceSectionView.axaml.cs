using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

public partial class InvoiceSectionView : UserControl
{
    public InvoiceSectionView() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// ══ کلیک روی هر جای کارت ⇒ همان فاکتور (۱۴۰۵/۰۷/۱۳) ═════════════════════
    /// خواستهٔ صاحب ریپو: «هر جای کادرِ فاکتور را زدم باز شود، نه فقط ‹دیدن›.»
    /// ⚠️ کلیک روی دکمه‌های خودِ کارت (تایید، برگشت، حذف) کارِ خودشان را می‌کند و
    /// فاکتور را باز نمی‌کند — وگرنه «🗑» اول صفحهٔ فاکتور را باز می‌کرد.
    /// </summary>
    private void OnCardReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (e.Source is Visual v && v.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
        if (sender is not Control { DataContext: InvoiceRowViewModel row }) return;
        if (DataContext is InvoiceSectionViewModel vm && vm.OpenDetailCommand.CanExecute(row))
        {
            vm.OpenDetailCommand.Execute(row);
            e.Handled = true;
        }
    }
}
