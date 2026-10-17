using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Controls;

//  ⛔ بخشی از ‎ExcelGrid‎ — «از ویرایش با فلش بیرون آمدن» و «ستونِ عددی پیشنهاد ندارد» (۱۴۰۵/۰۷/۲۵).
public partial class ExcelGrid
{
    /// <summary>
    /// ══ «حروفِ زیاد می‌نویسم، چپ/راست می‌زنم، وسطِ نوشته می‌رود» (۱۴۰۵/۰۷/۲۵) ══
    /// خانه‌ای که با دوبار-کلیک باز شد و کاربر <b>در آن تایپ کرد</b> دیگر «ویرایشِ
    /// یک حرف» نیست، «نوشتن» است: فلش ذخیره می‌کند و به خانهٔ بغلی می‌رود.
    /// فقط ‎F2‎ (‎_caretMode‎) فلش را صرفاً مالِ کُرسر نگه می‌دارد.
    /// </summary>
    private bool _typedSinceOpen;

    /// <summary>‎F2‎ زده شد ⇒ فلش‌ها کُرسر را می‌برند تا لبهٔ متن.</summary>
    private bool _caretMode;

    /// <summary>
    /// ویرایش ذخیره می‌شود و خانه با همان کلید جابه‌جا می‌شود. ‎false‎ یعنی
    /// خانه بسته نشد (عددِ ناخوانا) و کلید باید به کادر برسد.
    /// </summary>
    private bool LeaveEditBy(Key key)
    {
        //  ⛔ **پیش از** بستنِ ویرایش: خودِ ‎CommitEdit‎ می‌تواند خانهٔ جاری را
        //  جابه‌جا کند و آن‌وقت گام از جای اشتباه حساب می‌شد — همان «کلیکِ
        //  دوم درست می‌شود».
        var cols = VisibleCols();
        var from = CurIndex(cols);

        CommitEdit(DataGridEditingUnit.Cell, true);
        if (_editing) return false;

        if (key is Key.Up or Key.Down) MoveRow(key == Key.Down ? +1 : -1);
        else if (!MoveColumnFrom(cols, from, key == Key.Right ? -1 : +1)) CrossToNeighbour(key);
        //  ⛔ فوکوس به خودِ جدول — کادرِ بسته‌شده آن را با خود نبرد (کلیدِ بعدی گم نشود)
        if (!IsKeyboardFocusWithin || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
            Focus(NavigationMethod.Directional);
        return true;
    }

    /// <summary>هر نویسه یا پاک‌کنی که کاربر در کادرِ باز زد ⇒ «نوشت».</summary>
    private void WatchTyping(TextBox box)
    {
        box.RemoveHandler(TextInputEvent, OnEditorTyped);
        box.AddHandler(TextInputEvent, OnEditorTyped, RoutingStrategies.Tunnel, handledEventsToo: true);
        box.RemoveHandler(KeyDownEvent, OnEditorErase);
        box.AddHandler(KeyDownEvent, OnEditorErase, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnEditorTyped(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0])) _typedSinceOpen = true;
    }

    private void OnEditorErase(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Back or Key.Delete) _typedSinceOpen = true;
    }

    /// <summary>
    /// ستونِ عدد یا تاریخ — پیشنهادِ تکمیل ندارد. ‎«5» ⇒ «50»‎ کاربر را به اشتباه
    /// می‌انداخت (۱۴۰۵/۰۷/۲۵). ستونی که مسیرش شناخته نیست، از مقدارهایش: اگر همه
    /// عدد بودند، باز هم عددی است.
    /// </summary>
    private bool NumericColumn(DataGridColumn? column)
    {
        var p = PathOf(column);
        if (p is "DateShamsi" or "DensityText" || RowViewModel.IsNumberColumn(p)) return true;
        if (column is null) return false;
        var vals = Suggest.ColumnValues(ItemsSource, column);
        return vals.Count > 0 && vals.All(LooksNumeric);
    }

    internal static bool LooksNumeric(string s)
    {
        var any = false;
        foreach (var c in s)
        {
            if (char.IsDigit(c)) { any = true; continue; }
            if (c is ',' or '.' or '٬' or '٫' or '/' or '-' or ' ' or '+' or '%' or '٪') continue;
            return false;
        }
        return any;
    }
}
