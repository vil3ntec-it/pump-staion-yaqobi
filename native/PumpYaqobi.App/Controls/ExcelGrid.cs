using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

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

        // جای اضافه بینِ ستون‌ها پخش می‌شود، نه در یک ستونِ خالیِ ته جدول
        LayoutUpdated += (_, _) => { SpreadColumns(); CapToOneScreen(); };
    }

    // ══════════════════════════════════════════════════════════════════════
    //  مجازی‌سازیِ ردیف‌ها — و این‌که چرا جدول سقفِ ارتفاع دارد
    // ══════════════════════════════════════════════════════════════════════
    //
    //  ‎DataGrid‎ی آوالونیا ردیف‌هایش را مجازی‌سازی می‌کند: فقط ردیف‌های داخلِ
    //  قاب (به‌اضافهٔ چند تا حاشیه) را می‌سازد و با اسکرول همان‌ها را بازیافت
    //  می‌کند. پس صد هزار ردیف هم صد هزار عنصرِ زنده نمی‌سازد.
    //
    //  ⚠️ ولی این فقط وقتی کار می‌کند که **ارتفاعش محدود باشد**. اگر جدول
    //  ارتفاعِ آزاد بگیرد (مثلاً مستقیم داخلِ یک ‎StackPanel‎ی اسکرول‌شونده)،
    //  خودش را هم‌قدِ همهٔ ردیف‌ها اندازه می‌گیرد و آن‌وقت هر ردیف ساخته
    //  می‌شود — همان چیزی که با یک میلیون ردیف برنامه را قفل می‌کند.
    //
    //  پس سقف لازم است. ولی سقف یعنی جدول اسکرولِ خودش را دارد، و صاحب ریپو
    //  صریح گفت که تا نوارِ بخش‌ها به سقفِ پنجره نچسبیده، محتوای بخش نباید
    //  تکان بخورد. این دو با هم جمع می‌شوند، به شرطِ «زنجیرهٔ اسکرول»:
    //
    //     پایین: اول صفحه می‌لغزد؛ وقتی صفحه ته کشید، ردیف‌ها می‌لغزند.
    //     بالا:  اول ردیف‌ها برمی‌گردند؛ وقتی جدول سرِ خط آمد، صفحه بالا می‌رود.
    //
    //  نتیجه برای کاربر همان است که خواسته بود — سربرگ و نوارِ آمار می‌روند
    //  بالا، نوار قفل می‌شود، بعد ردیف‌ها راه می‌افتند — و برای برنامه یعنی
    //  مجازی‌سازی سرِ جایش می‌ماند. (‎OnPointerWheelChanged‎ پایین‌ترِ همین فایل.)

    /// <summary>اسکرولِ صفحه در ‎MainWindow‎؛ یک‌بار پیدا می‌شود و نگه داشته می‌شود.</summary>
    private ScrollViewer? _page;

    private ScrollViewer? Page =>
        _page ??= this.GetVisualAncestors().OfType<ScrollViewer>()
                      .FirstOrDefault(v => v.Name == "PageScroll");

    /// <summary>
    /// سقفِ ارتفاعِ جدول = یک صفحه. بی این، جدول هم‌قدِ همهٔ ردیف‌هایش می‌شود و
    /// مجازی‌سازی می‌میرد.
    /// </summary>
    private void CapToOneScreen()
    {
        var screen = Page?.Viewport.Height ?? 0;
        if (screen <= 0) return;
        if (Math.Abs(MaxHeight - screen) > 1) MaxHeight = screen;
    }

    /// <summary>
    /// ══ زنجیرهٔ اسکرول ═══════════════════════════════════════════════════════
    /// چرخِ ماوس روی جدول، اول به صفحه می‌رسد نه به ردیف‌ها — مگر آن‌که صفحه
    /// در همان جهت جای رفتن نداشته باشد. برعکسش هم درست است: هنگامِ برگشتن
    /// اول ردیف‌ها بالا می‌آیند و بعد صفحه.
    /// </summary>
    /// <summary>ستونی که آخرین بار با چرخِ افقی به آن رسیدیم.</summary>
    private int _wheelCol;

    /// <summary>
    /// ══ چپ و راست هم، نه فقط بالا و پایین ══════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «برای لپ‌تاپ من با چپ و راست اسکرول می‌کنم و نمی‌شود؛
    /// بالا و پایین را فقط دارد. من می‌خواهم هر دو باشند.»
    ///
    /// جدول‌های این برنامه ده‌ها ستون دارند و از پهنای پنجره بیرون می‌زنند.
    /// نوارِ لغزشِ افقیِ خودِ ‎DataGrid‎ هست ولی فقط با کشیدنِ موشواره کار
    /// می‌کرد؛ حرکتِ افقیِ ترک‌پد (‎Delta.X‎) و ‎Shift+چرخ‎ — همان دو راهی که
    /// هر مرورگری می‌فهمد — به آن نمی‌رسید.
    ///
    /// جابه‌جایی ستون‌به‌ستون است، با ‎ScrollIntoView‎ی خودِ جدول: روشِ رسمیِ
    /// Avalonia است، و روی جدولی که ستون‌هایش پهنای متفاوت دارند طبیعی‌تر هم
    /// درمی‌آید — هر بار یک ستونِ کامل می‌آید تو، نه نصفِ یک ستون.
    ///
    /// ‎true‎ یعنی «حرکت افقی بود و انجامش دادم».
    /// </summary>
    private bool TryScrollSideways(PointerWheelEventArgs e)
    {
        var dx = e.Delta.X;
        if (dx == 0 && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) dx = e.Delta.Y;
        if (dx == 0) return false;

        var cols = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (cols.Count < 2) return false;

        var item = SelectedItem ?? (ItemsSource as System.Collections.IEnumerable)?
                                   .Cast<object>().FirstOrDefault();
        if (item is null) return false;

        var next = Math.Clamp(_wheelCol + (dx > 0 ? 1 : -1), 0, cols.Count - 1);
        if (next == _wheelCol) return false;   // به لبه رسیده‌ایم

        _wheelCol = next;
        ScrollIntoView(item, cols[next]);
        return true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // ⚠️ اول افقی: اگر کاربر واقعاً به چپ/راست کشیده (یا ‎Shift‎ گرفته)،
        // این حرکت هیچ ربطی به زنجیرهٔ اسکرولِ عمودیِ پایین ندارد و نباید
        // به‌جایش صفحه بالا و پایین برود.
        if (TryScrollSideways(e)) { e.Handled = true; return; }

        var page = Page;
        var dy = e.Delta.Y;

        if (page is not null && Math.Abs(dy) > 0)
        {
            var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);
            // «جدول سرِ خط است؟» را از نوارِ لغزشِ خودِ جدول می‌پرسیم؛
            // ‎DataGrid‎ آفستِ عمودی‌اش را بیرون نمی‌دهد.
            var bar = VerticalBar;
            var gridTop = bar is null || bar.Value <= 0.5;

            // پایین می‌رویم: تا وقتی صفحه جا دارد، صفحه می‌لغزد.
            // بالا می‌آییم: تا وقتی جدول سرِ خط نیامده، خودِ جدول می‌لغزد.
            var pageFirst = dy < 0 ? page.Offset.Y < max - 0.5
                                   : gridTop && page.Offset.Y > 0.5;

            if (pageFirst)
            {
                var step = dy * WheelStep;
                page.Offset = new Vector(page.Offset.X,
                                         Math.Clamp(page.Offset.Y - step, 0, max));
                e.Handled = true;
                return;
            }
        }

        base.OnPointerWheelChanged(e);
    }

    /// <summary>یک چرخِ ماوس چند پیکسل صفحه را می‌برد.</summary>
    private const double WheelStep = 58;

    private ScrollBar? _vbar;

    /// <summary>نوارِ لغزشِ عمودیِ خودِ جدول (‎PART_VerticalScrollbar‎).</summary>
    private ScrollBar? VerticalBar =>
        _vbar ??= this.GetVisualDescendants().OfType<ScrollBar>()
                      .FirstOrDefault(b => b.Orientation == Orientation.Vertical);

    /// <summary>هر جدول یک‌بار پهن می‌شود؛ بعدش ستون‌های ستاره‌ای خودشان
    /// با تغییرِ اندازهٔ پنجره تنظیم می‌شوند.</summary>
    private bool _spread;

    /// <summary>
    /// ══ جای اضافه را بینِ ستون‌ها پخش کن ═══════════════════════════════════
    ///
    /// ‎DataGrid‎ی آوالونیا ستون‌های ‎Auto‎ را هم‌قدِ محتوایشان می‌کند و باقیِ
    /// پهنا را دست‌نخورده رها می‌کند — یعنی یک نوارِ خالیِ بزرگ ته جدول.
    /// پیش از این یک «ستونِ جاگیر» آن را می‌بلعید، ولی نتیجه‌اش همان بود:
    /// یک ستونِ خالیِ چندصد پیکسلی در هر جدولِ هر بخش، که صاحب ریپو گفت
    /// بی‌دلیل است و باید برود.
    ///
    /// جدولِ نسخهٔ وب این مشکل را ندارد چون ‎&lt;table&gt;‎ی ‎width:100%‎ پهنای
    /// اضافه را بینِ ستون‌ها **به نسبتِ محتوایشان** پخش می‌کند. همین کار
    /// این‌جا هم می‌شود: پهنای طبیعیِ هر ستون خوانده می‌شود و بعد همان عدد
    /// وزنِ ستاره‌اش می‌گردد. پس نسبت‌ها همان می‌ماند و جدول تمامِ پهنا را
    /// می‌گیرد.
    ///
    /// ⚠️ ‎MinWidth‎ روی همان پهنای طبیعی می‌نشیند: ستونِ ستاره‌ای وگرنه در
    /// پنجرهٔ باریک زیرِ اندازهٔ محتوا فشرده می‌شود و نوشته‌ها بریده. با این
    /// کف، به‌جای بریدن، جدول افقی می‌لغزد — همان کاری که ‎overflow-x:auto‎ی
    /// سایت می‌کند.
    /// </summary>
    private void SpreadColumns()
    {
        if (_spread || Columns.Count == 0) return;

        var cols = Columns.Where(c => c.IsVisible).ToList();
        if (cols.Count == 0) return;

        var natural = cols.Select(c => c.ActualWidth).ToArray();
        if (natural.Any(w => double.IsNaN(w) || w <= 0)) return;   // هنوز چیده نشده

        var room = Bounds.Width;
        if (room <= 0) return;

        // جای اضافه‌ای نیست (یا آن‌قدر کم است که ارزشِ دست زدن ندارد)
        if (room - natural.Sum() < 8) return;

        for (var i = 0; i < cols.Count; i++)
        {
            if (cols[i].MinWidth < natural[i]) cols[i].MinWidth = natural[i];
            cols[i].Width = new DataGridLength(natural[i], DataGridLengthUnitType.Star);
        }

        _spread = true;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ناوبریِ صفحه‌کلید — مو‌به‌مو همان چیزی که سایت می‌کند
    // ══════════════════════════════════════════════════════════════════════
    //
    // گزارشِ صاحب ریپو، سه تا:
    //   «الان با تب نمی‌شود نوع تیل را عوض کرد، نوع و واحدِ تیل یا پول را
    //    تغییر داد.»
    //   «کلیدهای چپ و راست برعکس کار می‌کنند.»
    //   «موقعِ تایپ هم نمی‌روند سمتِ دیگر.»
    //
    // سایت (خطِ ۵۴۹۳۴ به بعدِ ‎index.html‎) این‌ها را چنین حل کرده:
    //
    //   • ‎Tab‎ روی ‎radio‎ یا ‎SELECT‎ ⇒ مقدارش عوض می‌شود، فوکوس جابه‌جا
    //     نمی‌شود (‎_toggleControl‎ + ‎e.preventDefault()‎). ‎Enter‎ هم همان.
    //   • چپ/راست ⇒ ‎_pickInDirection‎ که **هندسی** است: دنبالِ کنترلی
    //     می‌گردد که مرکزش واقعاً در همان سمت باشد (‎dx < 0‎ برای چپ). پس
    //     کلیدِ چپ همیشه چپ می‌برد، چه صفحه راست‌به‌چپ باشد چه نه.
    //   • در کادرِ متنی، اگر کُرسر **وسطِ** متن است (‎s === e2 && s > 0 &&
    //     s < len‎) چپ/راست ناوبری نمی‌کند و متن را ویرایش می‌کند.
    //
    // ‎DataGrid‎ی آوالونیا هیچ‌کدام را نمی‌کند: ‎Tab‎ فقط فوکوس می‌بَرد، و
    // چپ/راست را با **ایندکسِ منطقیِ ستون** حساب می‌کند — که در چیدمانِ
    // راست‌به‌چپ آینه می‌شود و دقیقاً همان «برعکس»ی است که گزارش شد.

    /// <summary>کنترلی که همین حالا فوکوس دارد.</summary>
    private Control? Focused =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;

    /// <summary>
    /// ‎_toggleControl‎ — مقدارِ کشویی یا رادیوییِ خانه را یک پله جلو می‌برد.
    /// ‎true‎ یعنی چیزی عوض شد و کلید نباید کارِ دیگری بکند.
    /// </summary>
    private bool ToggleCell()
    {
        if (IsReadOnly) return false;

        // خانه هنوز در حالتِ ویرایش نیست؟ اول بازش کن تا کشویی/رادیو ساخته شود.
        if (Focused is not (ComboBox or RadioButton)) BeginEdit();

        switch (Focused)
        {
            case ComboBox cb when cb.ItemCount > 0:
                cb.SelectedIndex = (cb.SelectedIndex + 1) % cb.ItemCount;
                return true;

            // گروهِ رادیویی: بعدی را تیک بزن (با دو تا، یعنی همان «آن‌یکی»)
            case RadioButton rb:
                var group = rb.FindAncestorOfType<Panel>()?
                              .GetVisualDescendants().OfType<RadioButton>().ToList();
                if (group is null || group.Count < 2) return false;
                var i = group.IndexOf(rb);
                group[(i + 1) % group.Count].IsChecked = true;
                return true;
        }
        return false;
    }

    /// <summary>
    /// کُرسر وسطِ متنِ یک کادرِ تایپ است؟ آن‌وقت چپ/راست مالِ خودِ متن است،
    /// نه ناوبریِ جدول — همان شرطِ ‎s === e2 && s > 0 && s < len‎ی سایت.
    /// </summary>
    private bool CaretInsideText()
    {
        if (Focused is not TextBox tb) return false;
        var len = (tb.Text ?? "").Length;
        return tb.SelectionStart == tb.SelectionEnd
            && tb.SelectionStart > 0 && tb.SelectionStart < len;
    }

    /// <summary>ستونِ جاری را ‎step‎ خانه جابه‌جا می‌کند (بر اساسِ ترتیبِ دیداری).</summary>
    private bool MoveColumn(int step)
    {
        var cols = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (cols.Count == 0) return false;
        var cur = CurrentColumn is null ? 0 : cols.IndexOf(CurrentColumn);
        if (cur < 0) cur = 0;
        var next = Math.Clamp(cur + step, 0, cols.Count - 1);
        if (next == cur) return false;

        CurrentColumn = cols[next];
        var item = SelectedItem ?? (ItemsSource as System.Collections.IEnumerable)?.Cast<object>().FirstOrDefault();
        if (item is not null) ScrollIntoView(item, cols[next]);
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // ── Tab: روی خانهٔ کشویی/رادیویی مقدار را عوض می‌کند، نه فوکوس را ──
        if (e.Key == Key.Tab && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && IsToggleColumn(CurrentColumn) && ToggleCell())
        {
            e.Handled = true;
            return;
        }

        // ── چپ/راست: جهتِ **دیداری**، نه ایندکسِ منطقیِ ستون ──
        if (e.Key is Key.Left or Key.Right)
        {
            if (CaretInsideText()) return;            // وسطِ متن ⇒ ویرایش، نه ناوبری

            // در چیدمانِ راست‌به‌چپ، ستونِ «بعدی» سمتِ چپ است. پس کلیدِ چپ
            // باید ایندکس را جلو ببرد و کلیدِ راست عقب — وارونهٔ حالتِ چپ‌به‌راست.
            var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
            var step = (e.Key == Key.Left) == rtl ? +1 : -1;
            if (MoveColumn(step)) { e.Handled = true; return; }
            e.Handled = true;                          // لبهٔ جدول: هیچ، ولی نپرد
            return;
        }

        switch (e.Key)
        {
            // Enter روی خانهٔ کشویی/رادیویی هم مقدار را عوض می‌کند — مثلِ سایت
            case Key.Enter when !IsReadOnly && IsToggleColumn(CurrentColumn) && ToggleCell():
                e.Handled = true;
                return;

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

    /// <summary>
    /// ستونی که ویرایشش کشویی یا رادیویی است — «نوع تیل»، «نوع» (قرض/مصرف)،
    /// «واحد» (تیل/پول) و مانندِ آن‌ها. فقط روی این‌هاست که ‎Tab‎ و ‎Enter‎
    /// مقدار را عوض می‌کنند؛ روی خانه‌های عددی و متنی رفتارشان عادی است.
    /// </summary>
    private static bool IsToggleColumn(DataGridColumn? col) =>
        col is DataGridTemplateColumn t && t.CellEditingTemplate is not null;

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
