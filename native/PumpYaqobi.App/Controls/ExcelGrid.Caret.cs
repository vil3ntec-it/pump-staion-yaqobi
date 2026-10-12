using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ مکان‌نما هنگامِ باز شدنِ ویرایشِ خانه (۱۴۰۵/۰۷/۲۰) ═════════════════════
/// ‎DataGridTextColumn.PrepareCellForEdit‎ی آوالونیا هر ویرایشی را که با ‎F2‎ باز نشده
/// با «انتخابِ همهٔ متن» باز می‌کند. شنوندهٔ ‎PreparingCellForEdit‎ (‎ExcelGrid.Keys.cs‎)
/// پس از همان انتخاب این‌ها را صدا می‌زند: دوبار-کلیک ⇒ زیرِ نقطهٔ کلیک، پس از چسباندن ⇒ ته متن.
/// </summary>
public partial class ExcelGrid
{
    /// <summary>پس از ویرایشی که با ‎BeginEdit()‎ِ کد باز شد (مثلاً پس از چسباندن): مکان‌نما ته متن، بی انتخاب.</summary>
    private bool _caretToEnd;

    private static void CaretAtEnd(TextBox box)
    {
        var end = (box.Text ?? "").Length;
        box.CaretIndex = end;
        box.SelectionStart = box.SelectionEnd = end;
        box.Focus();
    }

    /// <summary>
    /// مکان‌نما درست زیرِ نقطهٔ دوبار-کلیک — همان کاری که خودِ ‎TextBox‎ با کلیک می‌کند
    /// (‎TextPresenter.MoveCaretToPoint‎). کادر یک پاسِ چیدمان بعد اندازه می‌گیرد، پس
    /// همان‌جا (‎Loaded‎) نشانده می‌شود؛ اگر هنوز ساخته نشده بود، ته متن.
    /// </summary>
    private static void CaretAtPointer(TextBox box, PointerPressedEventArgs press)
    {
        CaretAtEnd(box);
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().FirstOrDefault() is not { } tp)
                    return;
                tp.MoveCaretToPoint(press.GetPosition(tp));
                var i = Math.Clamp(tp.CaretIndex, 0, (box.Text ?? "").Length);
                box.CaretIndex = i;
                box.SelectionStart = box.SelectionEnd = i;
                box.Focus();
            }
            catch { /* مکان‌نما ته متن می‌ماند — بدتر از پیش نیست */ }
        }, DispatcherPriority.Loaded);
    }
}
