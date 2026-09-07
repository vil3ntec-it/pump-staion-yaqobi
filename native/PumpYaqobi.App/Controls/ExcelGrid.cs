using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ جدولِ اکسل‌مانند ═══════════════════════════════════════════════════════
/// همان <c>DataGrid</c>ِ نیتیوِ Avalonia (که خودش ردیف‌ها را مجازی‌سازی می‌کند و
/// فقط ردیف‌های دیده‌شده را می‌سازد) به‌اضافهٔ رفتارهایی که کاربر از اکسل
/// انتظار دارد و صاحب ریپو صریح خواسته بود:
///
///   • تایپ کردن روی یک خانه، همان‌جا ویرایش را باز می‌کند — بدون دوبار کلیک.
///   • Enter خانه را می‌بندد و یک ردیف پایین می‌رود (Shift+Enter بالا).
///   • Tab خانهٔ بعدی، Shift+Tab خانهٔ پیشین — و در انتهای ردیف به ردیفِ بعد.
///   • Esc ویرایش را لغو می‌کند و مقدارِ پیشین برمی‌گردد.
///   • Delete خانه‌های انتخابی را خالی می‌کند (اگر ستون خواندنی نباشد).
///   • F2 مثل اکسل ویرایش را باز می‌کند.
///
/// ویرایش خانه‌به‌خانه است: هیچ‌جا جدول از نو ساخته نمی‌شود، فقط همان یک
/// خانه به حالت ویرایش می‌رود و مقدارش به مدل می‌نشیند (بندِ ۲۸).
/// </summary>
public class ExcelGrid : DataGrid
{
    protected override Type StyleKeyOverride => typeof(DataGrid);

    /// <summary>وقتی کاربر Enter بزند و ردیفِ بعدی وجود نداشته باشد، این صدا می‌زند
    /// تا بخش بتواند یک ردیفِ خالیِ تازه بسازد (مثل «ردیفِ خودکارِ آخر» در اکسل).</summary>
    public static readonly StyledProperty<bool> GrowsOnEnterProperty =
        AvaloniaProperty.Register<ExcelGrid, bool>(nameof(GrowsOnEnter));

    public bool GrowsOnEnter
    {
        get => GetValue(GrowsOnEnterProperty);
        set => SetValue(GrowsOnEnterProperty, value);
    }

    public event EventHandler? GrowRequested;

    public ExcelGrid()
    {
        // پیش‌فرض‌هایی که همهٔ جدول‌های برنامه یکسان می‌خواهند
        SelectionMode = DataGridSelectionMode.Extended;
        CanUserReorderColumns = true;
        CanUserResizeColumns = true;
        CanUserSortColumns = true;
        AutoGenerateColumns = false;
        IsReadOnly = false;
        HeadersVisibility = DataGridHeadersVisibility.Column;
        ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;

        // ⚠️ ستونِ «جاگیر» — بی آن، جدول جای اضافه را به ستونِ آخر می‌دهد و
        // دکمهٔ حذف وسطِ یک ستونِ خالیِ ۴۰۰ پیکسلی شناور می‌شود، در حالی که
        // ستون‌های عددی به هم فشرده‌اند. حالا جای اضافه در یک ستونِ خالیِ
        // انتهایی جمع می‌شود و بقیهٔ ستون‌ها پهنای طبیعیِ خودشان را دارند —
        // همان کاری که جدولِ نسخهٔ وب با ‎width:auto‎ می‌کرد.
        Loaded += (_, _) => AddFillerColumn();
    }

    private void AddFillerColumn()
    {
        if (Columns.Count == 0) return;
        if (Columns[^1] is FillerColumn) return;
        Columns.Add(new FillerColumn
        {
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            IsReadOnly = true,
            CanUserResize = false,
            CanUserReorder = false,
            CanUserSort = false,
            // ‎DataGridTemplateColumn‎ بدونِ قالب هنگامِ ساختِ خانه می‌ترکد،
            // پس یک قالبِ خالی می‌گیرد.
            CellTemplate = new FuncDataTemplate<object?>((_, _) => new Border(), true),
        });
    }

    /// <summary>
    /// ستونِ جاگیر — نوعِ خودش را دارد تا با ستونِ «حذف» اشتباه نشود.
    ///
    /// ⚠️ نه رشتهٔ خالی به‌عنوان نشانه (ستونِ حذفِ خیلی از جدول‌ها هم
    /// ‎Header=""‎ دارد، پس جاگیر هرگز اضافه نمی‌شد) و نه یک ‎object‎ِ
    /// نشانه‌گذار (خودِ جدول ‎ToString()‎ اش را در سرستون چاپ می‌کرد و
    /// «System.Object» بالای جدول می‌نشست).
    /// </summary>
    private sealed class FillerColumn : DataGridTemplateColumn { }

    /// <summary>ستونی که آخرین بار با چرخِ افقی به آن رسیدیم.</summary>
    private int _wheelCol;

    /// <summary>
    /// ══ اسکرولِ چپ و راست با چرخ و ترک‌پد ══════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «برای لپ‌تاپ من با چپ و راست اسکرول می‌کنم و نمی‌شود؛
    /// بالا و پایین را فقط دارد. من می‌خواهم هر دو باشند.»
    ///
    /// جدول‌های این برنامه ده‌ها ستون دارند و از پهنای پنجره بیرون می‌زنند.
    /// نوارِ لغزشِ افقیِ خودِ ‎DataGrid‎ هست ولی فقط با کشیدنِ موشواره کار
    /// می‌کرد؛ حرکتِ افقیِ ترک‌پد (‎Delta.X‎) و ‎Shift+چرخ‎ — همان دو راهی که
    /// هر مرورگری می‌فهمد — به آن نمی‌رسید.
    ///
    /// جابه‌جایی ستون‌به‌ستون است، با ‎ScrollIntoView‎ی خودِ جدول: هم روشِ
    /// رسمیِ Avalonia است، هم روی جدولی که ستون‌هایش پهنای متفاوت دارند
    /// طبیعی‌تر از لغزشِ پیکسلی درمی‌آید — هر بار یک ستونِ کامل می‌آید تو،
    /// نه نصفِ یک ستون.
    ///
    /// ⚠️ اول ‎base‎ صدا زده می‌شود: اگر خودِ ‎DataGrid‎ این حرکت را فهمید و
    /// مصرف کرد، این‌جا دیگر کاری نمی‌کنیم و دوبار اسکرول نمی‌شود.
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled) return;

        // حرکتِ افقیِ ترک‌پد، یا ‎Shift+چرخ‎ (قاعدهٔ همیشگیِ مرورگرها)
        var dx = e.Delta.X;
        if (dx == 0 && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) dx = e.Delta.Y;
        if (dx == 0) return;

        var cols = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (cols.Count < 2) return;

        var item = SelectedItem ?? (ItemsSource as System.Collections.IEnumerable)?
                                   .Cast<object>().FirstOrDefault();
        if (item is null) return;

        var next = Math.Clamp(_wheelCol + (dx > 0 ? 1 : -1), 0, cols.Count - 1);
        if (next == _wheelCol) return;      // به لبه رسیده‌ایم — بگذار صفحه اسکرول کند

        _wheelCol = next;
        ScrollIntoView(item, cols[next]);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when !IsReadOnly:
                // ویرایشِ باز را ببند، بعد یک ردیف بالا/پایین برو
                CommitEdit(DataGridEditingUnit.Cell, true);
                MoveRow(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : +1);
                e.Handled = true;
                return;

            case Key.F2 when !IsReadOnly:
                BeginEdit();
                e.Handled = true;
                return;

            case Key.Escape:
                CancelEdit(DataGridEditingUnit.Cell);
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        // تایپِ ساده روی خانه = شروعِ ویرایش (رفتارِ اکسل)
        if (!IsReadOnly && !string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
            BeginEdit();
        base.OnTextInput(e);
    }

    private void MoveRow(int delta)
    {
        if (ItemsSource is not System.Collections.IList list || list.Count == 0) return;
        var i = SelectedIndex;
        var next = i + delta;

        if (next >= list.Count)
        {
            if (!GrowsOnEnter) return;
            GrowRequested?.Invoke(this, EventArgs.Empty);
            if (next >= list.Count) return;   // بخش ردیفِ تازه نساخت
        }
        if (next < 0) return;

        SelectedIndex = next;
        ScrollIntoView(list[next], CurrentColumn);
    }
}
