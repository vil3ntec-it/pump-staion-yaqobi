using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.Controls;

//  ⛔ شورا ج۴: بخشی از ‎ExcelGrid‎ — «در جا ثبت شود» و «از جدول بیرون بشوم». فقط جابه‌جاییِ همان عضوها از ‎ExcelGrid.cs‎، بی تغییرِ یک رفتار.
public partial class ExcelGrid
{
    // ══════════════════════════════════════════════════════════════════════
    //  ══ «در جا ثبت شود» — و «از جدول بیرون بشوم» ════════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶)، دو جمله با **یک** ریشه:
    //
    //    «هر یک کلمه از هر جای برنامه کم یا زیاد می‌کنم همان خودش اتومات ثبت
    //     بشود… و این خیلی مهم است که یارو اعتماد کند و آخر نبیند که هیچی ثبت
    //     نشده.»
    //    «روی کار یا جدولی هستم، روی صفحه می‌زنم، از آن جدول یا اینپوت بیرون
    //     نمی‌شود — این را ریشه‌ای درست کن.»
    //
    //  ⛔ ریشه: خانه‌ای که در حالِ ویرایش بود با کلیکِ بیرون فقط ‎CommitEdit‎
    //  می‌شد — یعنی مقدار در ویومدل می‌نشست ولی:
    //    ۱) نوشتنِ دیسک هنوز پشتِ تأخیرِ ۳۵۰ میلی‌ثانیه‌ای بود، و
    //    ۲) خودِ جدول فوکوسِ صفحه‌کلید را نگه می‌داشت، پس هم خانه «فعال» دیده
    //       می‌شد و هم کلیدهای بعدی به همان جدول می‌رفتند.
    //
    //  حالا هر سه با هم: ویرایش تمام ⇒ ردیف **همان لحظه** روی دیسک ⇒ فوکوس
    //  رها. و چون ‎SaveAsync‎ِ هر ردیف به ‎PumpDbContext.SaveChanges‎ می‌رسد،
    //  همان تراکنش یک ردیفِ ‎SyncOps‎ هم می‌سازد و موتورِ همگام‌سازی
    //  می‌بردش — پس «توی سرور هم برود» خودش انجام می‌شود.

    /// <summary>
    /// هر ردیفِ کثیفِ همین جدول را <b>همین حالا</b> روی دیسک می‌نشاند — بی
    /// تأخیرِ ۳۵۰ میلی‌ثانیه‌ای.
    ///
    /// ⚠️ ردیفِ دست‌نخورده هیچ دستورِ دیتابیسی نمی‌زند (‎RowViewModel.FlushAsync‎
    /// خودش با ‎IsDirty‎ رد می‌کند)، پس گشتنِ ساده همان صفرِ همیشگی می‌ماند —
    /// قاعدهٔ «ردیفِ دست‌نخورده ذخیره نمی‌شود» سرِ جایش است.
    ///
    /// ⚠️ و عمداً «همهٔ ردیف‌ها» است نه «ردیفی که ویرایش شد»: نشانِ ردیفِ
    /// ویرایش‌شده از آرگومانِ رویداد می‌آمد، و یک پیمایشِ مرجع‌به‌مرجع روی
    /// فهرستِ حافظه از یک وابستگیِ تازه به شکلِ آرگومانِ آوالونیا ارزان‌تر و
    /// مطمئن‌تر است. (فهرستِ پارک‌شده یعنی ‎ItemsSource‎ِ خالی، یعنی هیچ کار.)
    /// </summary>
    private void FlushDirtyRows()
    {
        if (ItemsSource is not System.Collections.IEnumerable src) return;
        foreach (var o in src)
            if (o is ViewModels.RowViewModel row && row.IsDirty) _ = row.FlushAsync();
    }

    /// <summary>
    /// «از این جدول بیرون آمدم»: ویرایش تمام، مقدار روی دیسک، فوکوس رها.
    ///
    /// ⚠️ انتخابِ ردیف (‎SelectedItem‎) عمداً دست نمی‌خورد — بخش‌هایی مثلِ
    /// «مقایسهٔ نرخ فاکتورها» سربرگشان را از همان می‌سازند و پاک کردنش یعنی
    /// خالی شدنِ کارت‌ها با هر کلیکِ بی‌ربط.
    /// </summary>
    public void LeaveNow()
    {
        if (_editing) CommitEdit(DataGridEditingUnit.Cell, true);
        FlushDirtyRows();
        if (IsKeyboardFocusWithin) TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }

    // ── خودِ کلیدها ───────────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        // ══ کلیپ‌بورد، انتخابِ همه، برگشت و دوباره ═══════════════════════════
        //
        // ⛔ **فقط بیرونِ حالتِ ویرایش.** داخلِ کادرِ تایپِ یک خانه، ‎Ctrl+C‎ و
        // ‎Ctrl+V‎ و ‎Ctrl+A‎ و ‎Ctrl+Z‎ مالِ خودِ کادرند — همان قاعدهٔ همیشگیِ
        // این برنامه: «هیچ میانبری کارِ کادرِ تایپ را نخورد.» کسی که وسطِ
        // نوشتنِ یک نام ‎Ctrl+Z‎ می‌زند، حرفِ قبلی‌اش را می‌خواهد، نه خانهٔ
        // قبلی‌اش.
        //
        // ⚠️ ‎Alt‎ کنار گذاشته می‌شود چون ‎AltGr‎ (‎Ctrl+Alt‎) روی صفحه‌کلیدهای
        // اروپایی نویسه می‌سازد.
        if (ctrl && !alt && !_editing)
        {
            if (e.Key == Key.C) { _ = CopySelectionAsync(); e.Handled = true; return; }
            if (e.Key == Key.X && !IsReadOnly) { _ = CutSelectionAsync(); e.Handled = true; return; }
            if (e.Key == Key.V && !IsReadOnly) { _ = PasteAsync(); e.Handled = true; return; }
            if (e.Key == Key.A) { SelectAllCells(); e.Handled = true; return; }
            //  ‎Ctrl+Z‎/‎Ctrl+Y‎ دیگر این‌جا گرفته نمی‌شوند: ‎ShortcutService‎ آن‌ها را
            //  برای کلِ برنامه می‌گیرد (پشتهٔ ‎UndoHub‎)، هر جا که فوکوس باشد.
        }

        // ── Esc: در هر حالتی «برگرد سرِ جای اول» ──────────────────────────
        if (e.Key == Key.Escape)
        {
            if (_editing) CancelEdit(DataGridEditingUnit.Cell);
            _colAnchor = _colHead = -1;
            PaintRange();
            e.Handled = true;
            return;
        }

        // ── حالتِ EDITING: فلش‌ها فقط مالِ متن‌اند ─────────────────────────
        // بندِ صریحِ دستور: «در حالت EDITING، کلیدهای جهت‌دار فقط داخل همان
        // خانه حرکت کنند و هرگز به خانهٔ دیگر نپرند.» پس این‌جا بسته می‌شوند
        // و کادرِ تایپ خودش هر کاری با کُرسر دارد می‌کند.
        // در «حالتِ ویرایش» (‎F2‎/دوبار کلیک) فلش فقط کُرسر را می‌برد؛ در
        // «حالتِ نوشتن» (با تایپ آمده‌ایم) ذخیره می‌کند و به خانهٔ بعدی می‌رود —
        // دقیقاً مثلِ اکسل. پس این‌جا فقط حالتِ ویرایش برمی‌گردد.
        if (_editing && !_typedIn && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            return;   // ‎Handled‎ نمی‌شود تا خودِ ‎TextBox‎ کُرسر را ببرد



        // ── Tab روی خانهٔ کشویی/رادیویی: مقدار عوض می‌شود، نه فوکوس ────────
        if (e.Key == Key.Tab && !shift && IsToggleColumn(CurrentColumn) && ToggleCell())
        {
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            // ── Tab / Shift+Tab: خانهٔ بعدی و پیشین ───────────────────────
            case Key.Tab:
                MoveCell(shift ? -1 : +1);
                e.Handled = true;
                return;

            // ══ چپ/راست: همان‌جایی که چشم می‌بیند ════════════════════════════
            //
            // ⚠️ این خانه **سه بار** عوض شده و هر سه بار گزارشِ صاحب ریپو یکی
            // بود: «کلیدِ راست را می‌زنم، چپ می‌رود.» تاریخچه‌اش را بخوان و
            // دوباره عوضش نکن:
            //
            //   ۱) از ‎FlowDirection‎ خوانده می‌شد — به این کنترل نمی‌رسید و
            //      بی‌صدا برعکس می‌شد.
            //   ۲) ایندکسِ خام شد («‎Right → column+1‎») — در جدولِ راست‌به‌چپ
            //      ستونِ بعدی سمتِ **چپ** است، پس همان شکایت.
            //   ۳) از جای سربرگ‌ها اندازه گرفته شد — و باز هم برعکس ماند، چون
            //      آوالونیا چیدمانِ راست‌به‌چپ را با آینه‌کردنِ **رسم** انجام
            //      می‌دهد و مختصاتی که به ما می‌دهد هنوز چپ‌به‌راست است.
            //
            // پس دیگر «تشخیص» در کار نیست. کلِ این برنامه راست‌به‌چپ است
            // (‎Window‎ در ‎Controls.axaml‎ صریح ‎RightToLeft‎ است) و ستونِ
            // شمارهٔ ۰ سمتِ **راست** می‌نشیند. قاعده همین است و ثابت می‌ماند:
            //
            //     ‎→‎ خانهٔ سمتِ راست  ⇒ ایندکسِ کمتر
            //     ‎←‎ خانهٔ سمتِ چپ    ⇒ ایندکسِ بیشتر
            case Key.Left:
            case Key.Right:
                if (!MoveColumn(e.Key == Key.Right ? -1 : +1, shift) && !shift)
                    CrossToNeighbour(e.Key);
                e.Handled = true;
                return;

            // ── بالا/پایین: خودِ جدول می‌بَرد (با ‎Shift‎ چندردیفی) ─────────
            // فقط کادرِ ستونی جمع می‌شود اگر ‎Shift‎ گرفته نشده باشد.
            case Key.Up:
            case Key.Down:
                if (!shift && _colAnchor >= 0) { ResetRange(CurIndex(VisibleCols())); PaintRange(); }
                break;

            case Key.PageUp:
            case Key.PageDown:
                break;

            // ── Home/End: سرِ ردیف و ته ردیف (با ‎Ctrl‎: سرِ جدول و ته جدول) ─
            case Key.Home when !ctrl:
                MoveColumn(-VisibleCols().Count, shift);
                e.Handled = true;
                return;
            case Key.End when !ctrl:
                MoveColumn(+VisibleCols().Count, shift);
                e.Handled = true;
                return;

            // Enter روی خانهٔ کشویی/رادیویی هم مقدار را عوض می‌کند — مثلِ سایت
            case Key.Enter when !IsReadOnly && IsToggleColumn(CurrentColumn) && ToggleCell():
                e.Handled = true;
                return;

            case Key.Enter when !IsReadOnly:
                CommitEdit(DataGridEditingUnit.Cell, true);
                MoveRow(shift ? -1 : +1);
                e.Handled = true;
                return;

            // ‎F2‎ وسطِ حالتِ نوشتن، مثلِ اکسل، به حالتِ ویرایش می‌بَرد: از آن
            // به بعد فلش‌ها کُرسر را داخلِ متن می‌برند، نه به خانهٔ بعدی.
            case Key.F2 when _editing:
                _typedIn = false;
                e.Handled = true;
                return;

            case Key.F2 when !IsReadOnly:
                BeginEdit();
                e.Handled = true;
                return;

            // ══ Delete و Backspace: هر دو پاک می‌کنند ═════════════════════
            //
            // گزارشِ صاحب ریپو با عکسِ اکسل: «اگر بک‌اسپیس را بزنم پاک می‌شود …
            // الان برنامهٔ من این را پاک نمی‌کند و باید سه بار بزنم رویش.»
            //
            // حق داشت و سنجشِ ‎cells‎ هم نشان داد: ‎Delete‎ کار می‌کرد و
            // ‎Backspace‎ اصلاً دیده نمی‌شد. در اکسل هر دو خانه را خالی
            // می‌کنند (‎Backspace‎ علاوه بر آن ویرایش را هم باز می‌کند، ولی
            // این‌جا تفاوتش برای کاربر صفر است چون بعدش بی‌درنگ تایپ می‌کند).
            // ══ Ctrl+Delete: حذفِ ردیفِ جاری ══════════════════════════════════
            // گزارشِ صاحب ریپو: «حذفِ یک ردیف گم شده… جوری باشه که جا نگیره.»
            // ستونِ حذف جا می‌گرفت و برداشته شد؛ راست‌کلیک هست ولی کسی نمی‌داند.
            // پس یک کلید هم: ‎Ctrl+Delete‎ همان فرمانِ حذفِ همان ردیف را می‌زند
            // ⚠️ پیش از ‎Delete‎ی خالی‌کننده، وگرنه آن یکی می‌بلعدش (سنجشِ ‎verify‎).
            case Key.Delete when !_editing && e.KeyModifiers.HasFlag(KeyModifiers.Control)
                                 && RowDeleteCommand is { } del && SelectedItem is { } cur:
                if (del.CanExecute(cur)) del.Execute(cur);
                e.Handled = true;
                return;

            case Key.Delete or Key.Back when !_editing && !IsReadOnly && ClearSelectedCells():
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);

        // ══ و بعدِ هر ناوبری، صفحه دنبالِ خانه بیاید ═════════════════════════
        //
        // ⚠️ این‌جا لازم است، نه فقط داخلِ ‎MoveRow‎/‎MoveColumn‎: فلشِ بالا و
        // پایینِ تنها را **خودِ ‎DataGrid‎** انجام می‌دهد (بالا فقط ‎break‎
        // می‌شود و کار به ‎base‎ می‌رسد)، پس آن دو تابع اصلاً صدا زده نمی‌شوند.
        // سنجش همین را گرفت: بعدِ ۴۰ فلشِ پایین، خانهٔ جاری در ۱۲۷۷ افتاده بود
        // و قابِ دید تا ۹۰۰ — یعنی ۳۷۷ پیکسل زیرِ صفحه.
        if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right
                  or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Tab or Key.Enter)
            Dispatcher.UIThread.Post(FollowCell, DispatcherPriority.Background);
    }

    /// <summary>
    /// ══ تایپ روی خانهٔ انتخاب‌شده = جایگزینی، مثلِ اکسل ══════════════════════
    ///
    /// گزارشِ صاحب ریپو: «اگر بزنم روی یک عدد یا حرف، آن تغییر می‌کند … الان
    /// برنامهٔ من تغییر نمی‌دهد و باید سه بار بزنم رویش.»
    ///
    /// ⚠️ ‎BeginEdit()‎ تنها کافی نبود و همین سه‌کلیکه‌اش می‌کرد: کادرِ ویرایش
    /// همان لحظه ساخته می‌شود ولی هنوز فوکوس ندارد، پس **همان حرفی که ویرایش
    /// را باز کرد گم می‌شد** — کاربر می‌دید هیچ اتفاقی نیفتاد و دوباره
    /// می‌زد. سنجشِ ‎cells‎ همین را گرفت: «برق دکان» ⇐ «برق دکان».
    ///
    /// پس حالا خودمان حرف را می‌نشانیم: محتوای قبلی می‌رود (اکسل هم همین
    /// می‌کند) و کُرسر ته متن می‌ایستد تا ادامهٔ تایپ پشتِ همان بنشیند.
    /// </summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        //  حرفِ دوم و سوم پیش از آماده شدنِ کادر — پشتِ همان حرفِ اول، به ترتیب
        if (_typeBuf is not null && _editing && !string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
        {
            _typeBuf += e.Text;
            if (CurrentEditor() is { } b)
            {
                LiveFormat.MarkUser(b);
                b.Text = _typeBuf;
                var end = (b.Text ?? "").Length;   // قالبِ زنده ممکن است کاما گذاشته باشد
                b.CaretIndex = end;
                b.SelectionStart = b.SelectionEnd = end;
                b.Focus();
            }
            e.Handled = true;
            return;
        }
        if (IsReadOnly || _editing || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
        {
            base.OnTextInput(e);
            return;
        }

        _typeBuf = e.Text;
        BeginEdit();
        _typedIn = true;   // با تایپ آمدیم ⇒ فلش‌ها ناوبری‌اند، نه کُرسر

        var box = CurrentEditor();
        if (box is null) { if (!_editing) _typeBuf = null; e.Handled = _editing; if (!_editing) base.OnTextInput(e); return; }

        var t = _typeBuf ?? e.Text;
        LiveFormat.MarkUser(box);
        box.Text = t;
        var tEnd = (box.Text ?? "").Length;
        box.CaretIndex = tEnd;
        box.SelectionStart = box.SelectionEnd = tEnd;
        box.Focus();
        e.Handled = true;
    }

    /// <summary>
    /// ══ «12960» نه «2960»، نه «12906» (۱۴۰۵/۰۷/۱۹) ══════════════════════════════
    ///
    /// تایپ روی خانهٔ انتخاب‌شده ویرایش را باز می‌کند، ولی کادرِ ویرایش <b>یک پاسِ
    /// چیدمان بعد</b> آماده می‌شود و همان لحظه ستونِ ‎DataGridTextColumn‎ کلِ متنِ
    /// کادر را <b>انتخاب</b> می‌کند (‎PrepareCellForEdit‎، چون ویرایش با ‎F2‎ باز
    /// نشده). پس اگر کاربر تند بزند، حرف‌هایی که تا آن لحظه نشسته‌اند برجسته
    /// می‌شوند و حرفِ بعدی <b>جایشان را می‌گیرد</b> — ‎typeall‎ با تایپِ تند همین را
    /// گرفت: «12960» ⇒ «2960». و اگر مکان‌نما جا می‌ماند، حرف وسطِ عدد می‌نشست.
    ///
    /// ⛔ پس هر چه از حرفِ اول تا آماده شدنِ کادر زده شد این‌جا جمع می‌شود و همان
    /// لحظهٔ آماده شدن، <b>بی انتخاب</b> و با مکان‌نما ته متن در کادر می‌نشیند.
    /// </summary>
    private string? _typeBuf;

    /// <summary>کادرِ تایپِ خانه‌ای که همین حالا در حالِ ویرایش است.</summary>
    private TextBox? CurrentEditor() =>
        this.GetVisualDescendants().OfType<DataGridCell>()
            .Where(c => c.IsVisible)
            .SelectMany(c => c.GetVisualDescendants().OfType<TextBox>())
            .FirstOrDefault(t => t.IsVisible);

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
        // ⚠️ یک پاسِ چیدمان بعد: ردیفِ تازه هنوز ساخته نشده و مختصاتش صفر است.
        Dispatcher.UIThread.Post(FollowCell, DispatcherPriority.Background);
    }

    /// <summary>
    /// مقدارهای همین ستون برای تکمیلِ خودکار — از این جدول و جدول‌های هم‌جنسِ همان صفحه.
    /// ⛔ (۱۴۰۵/۰۷/۱۸، دوم) دو جدولِ تراکنشِ ورق دو نیمهٔ یک فهرست‌اند و ردیفِ مرز با «➕ ردیف»
    /// از یکی به دیگری می‌رود؛ بی این، نامی که در نیمهٔ دیگر نوشته شده بود پیشنهاد نمی‌شد
    /// (‎keys17‎ گرفتش). فقط جدولی که ردیف‌هایش همان نوع‌اند و ستونش همان مسیر را دارد.
    /// </summary>
    private IReadOnlyList<string> PeerColumnValues(DataGridColumn column)
    {
        var own = Suggest.ColumnValues(ItemsSource, column);
        if (column is not DataGridBoundColumn { Binding: Avalonia.Data.Binding { Path: { Length: > 0 } path } }) return own;
        var type = ItemsSource?.Cast<object>().FirstOrDefault(x => x is not null)?.GetType();
        var scope = this.FindAncestorOfType<SectionPage>() as Control ?? this.FindAncestorOfType<UserControl>();
        if (type is null || scope is null) return own;
        List<string>? all = null;
        foreach (var g in scope.GetVisualDescendants().OfType<ExcelGrid>())
        {
            if (ReferenceEquals(g, this) || g.ItemsSource is null) continue;
            if (g.ItemsSource.Cast<object>().FirstOrDefault(x => x is not null)?.GetType() != type) continue;
            var col = g.Columns.FirstOrDefault(c => c is DataGridBoundColumn { Binding: Avalonia.Data.Binding b } && b.Path == path);
            if (col is null) continue;
            all ??= new List<string>(own);
            foreach (var v in Suggest.ColumnValues(g.ItemsSource, col))
                if (all.Count < 400 && !all.Contains(v)) all.Add(v);
        }
        return all ?? own;
    }
}
