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

//  ⛔ شورا ج۴: بخشی از ‎ExcelGrid‎ — کنترلرِ مرکزیِ صفحه‌کلید و جهتِ کلیدها. فقط جابه‌جاییِ همان عضوها از ‎ExcelGrid.cs‎، بی تغییرِ یک رفتار.
public partial class ExcelGrid
{
    // ══════════════════════════════════════════════════════════════════════
    //  کنترلرِ مرکزیِ صفحه‌کلیدِ جدول‌ها — سه حالتِ صریح
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ صاحب ریپو (بندِ ۱ و ۲ِ دستورِ تازه): «یک سیستم مرکزی برای همهٔ
    //  جدول‌ها؛ نه هر جدول یک رفتار. سه حالت داشته باشد و رفتارِ کلیدها در هر
    //  حالت روشن باشد.» پیش از این هر چیزی روی حدس بود — مثلاً «اگر کُرسر
    //  وسطِ متن است یعنی حتماً در حالِ ویرایشیم». آن حدس در لبهٔ متن می‌شکست و
    //  همان «موقعِ تایپ می‌پرد به خانهٔ دیگر»ی بود که گزارش شد.
    //
    //  حالا حالت از خودِ جدول پرسیده می‌شود، نه از جای کُرسر:
    //
    //  ┌ SELECTED ───────────────────── یک خانه انتخاب است، ویرایش باز نیست ┐
    //  │ ← ↑ ↓ →   خانه‌به‌خانه جابه‌جا می‌شود (جهتِ دیداری، نه ایندکسِ منطقی) │
    //  │ Tab       خانهٔ بعدی · Shift+Tab خانهٔ پیشین (ته ردیف ⇒ ردیفِ بعد)  │
    //  │ Enter     یک ردیف پایین · Shift+Enter یک ردیف بالا                │
    //  │ F2/تایپ   می‌رود به EDITING                                        │
    //  │ Delete    خانه‌های انتخابی را خالی می‌کند                          │
    //  │ Shift+←→↑↓ می‌رود به MULTI                                         │
    //  └────────────────────────────────────────────────────────────────────┘
    //  ┌ EDITING ─────────────────────────── خانه باز است و کادر تایپ دارد ┐
    //  │ ← →       فقط کُرسرِ داخلِ متن — هرگز ناوبری (بندِ صریحِ دستور)     │
    //  │ ↑ ↓       هیچ — تا عددِ نیمه‌تایپ‌شده با یک فلش نپرد               │
    //  │ Enter     ذخیره و یک ردیف پایین                                   │
    //  │ Tab       ذخیره و خانهٔ بعدی                                       │
    //  │ Esc       لغو؛ مقدارِ پیشین برمی‌گردد و به SELECTED برمی‌گردیم      │
    //  └────────────────────────────────────────────────────────────────────┘
    //  ┌ MULTI ──────────────────── چند خانه/چند ردیف با Shift انتخاب شده ┐
    //  │ Shift+↑↓  ردیف‌ها را می‌گستراند (انتخابِ خودِ DataGrid)             │
    //  │ Shift+←→  ستون‌ها را می‌گستراند (کادرِ رنگیِ ‎.rangesel‎)            │
    //  │ Delete    همهٔ خانه‌های داخلِ کادر را خالی می‌کند                   │
    //  │ Esc / کلیک / فلشِ تنها  ⇒ برمی‌گردد به SELECTED                    │
    //  └────────────────────────────────────────────────────────────────────┘
    //
    //  ⚠️ یک استثناء که عمدی است و باید بماند: روی ستونی که ویرایشش رادیویی
    //  یا کشویی است («نوع تیل»، «نوع»، «واحد»)، ‎Tab‎ و ‎Enter‎ مقدار را یک
    //  پله جلو می‌برند و فوکوس را جابه‌جا نمی‌کنند — هم سایت همین کار را
    //  می‌کند (‎_toggleControl‎، خطِ ۵۴۹۳۴) و هم صاحب ریپو صریح خواسته بود
    //  «با تب بشود نوع تیل را عوض کرد».

    /// <summary>سه حالتِ جدول — رفتارِ هر کلید از روی همین یکی تصمیم گرفته می‌شود.</summary>
    public enum GridMode
    {
        /// <summary>یک خانه انتخاب است و ویرایش باز نیست.</summary>
        Selected,
        /// <summary>خانه باز است و کادرِ تایپ دارد.</summary>
        Editing,
        /// <summary>بیش از یک خانه یا ردیف با ‎Shift‎ انتخاب شده.</summary>
        MultiSelect
    }

    /// <summary>ویرایش باز است؟ از خودِ رویدادهای جدول خوانده می‌شود، نه از حدس.</summary>
    private bool _editing;

    /// <summary>
    /// ══ دو حالتِ ویرایشِ اکسل ════════════════════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «توی اکسل موقعِ نوشتنِ حروف یا اعداد، وسط یا اول یا
    /// آخر فرقی نمی‌کند — بخواهی بروی کادرِ بعدی، می‌رود. ولی تو اپِ من این
    /// قابلیت وجود ندارد.»
    ///
    /// حق داشت، و ریشه‌اش این است که اکسل <b>دو</b> حالتِ ویرایش دارد و ما فقط
    /// یکی را داشتیم:
    ///
    ///   • <b>حالتِ نوشتن</b> (‎Enter mode‎) — با تایپ کردن روی خانه باز شده.
    ///     فلش‌ها مقدار را ذخیره می‌کنند و به خانهٔ بغلی می‌روند، هر جای متن
    ///     که کُرسر باشد. این همان چیزی است که گم بود.
    ///
    ///   • <b>حالتِ ویرایش</b> (‎Edit mode‎) — با ‎F2‎ یا دوبار کلیک باز شده.
    ///     فلش‌ها فقط کُرسر را داخلِ متن می‌برند. این را داشتیم و می‌ماند —
    ///     همان بندِ صریحی که قبلاً خواسته شده بود.
    ///
    /// ‎F2‎ وسطِ حالتِ نوشتن، مثلِ خودِ اکسل، به حالتِ ویرایش می‌بَرد.
    ///
    /// ‎true‎ یعنی «با تایپ آمدیم» ⇒ فلش‌ها ناوبری‌اند.
    /// </summary>
    private bool _typedIn;

    private bool _wired;

    /// <summary>سرِ کادرِ چندانتخابی (ستونی که ‎Shift‎ از آن شروع شد) و تهِ آن.</summary>
    private int _colAnchor = -1, _colHead = -1;

    /// <summary>کادرِ رنگی روی صفحه هست؟ تا وقتی نیست، هر چیدمان بی‌خود رنگ نزند.</summary>
    private bool _painted;

    /// <summary>حالتِ همین لحظهٔ جدول.</summary>
    public GridMode Mode =>
        _editing ? GridMode.Editing
        : (SelectedItems.Count > 1 || (_colAnchor >= 0 && _colHead != _colAnchor))
            ? GridMode.MultiSelect
            : GridMode.Selected;

    /// <summary>
    /// ستونِ «#» (شمارهٔ ردیف) — مثلِ ستونِ اولِ جدول‌های سایت. پیش‌فرض روشن؛
    /// جدولی که واقعاً نباید شماره داشته باشد خودش خاموشش می‌کند.
    /// </summary>
    public static readonly StyledProperty<bool> RowNumbersProperty =
        AvaloniaProperty.Register<ExcelGrid, bool>(nameof(RowNumbers), true);

    public bool RowNumbers
    {
        get => GetValue(RowNumbersProperty);
        set => SetValue(RowNumbersProperty, value);
    }

    /// <summary>
    /// ══ حذفِ یک ردیف، بی ستونِ «حذف» ════════════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «اون دکمهٔ حذف بشه، دیده نشه … اون دیده بشه خیلی
    /// جدول رو هم بزرگ می‌کنه.» حق داشت — یک ستونِ کاملِ ۵۰ پیکسلی در **سیزده**
    /// جدول، فقط برای دکمه‌ای که به‌ندرت زده می‌شود، و در جدول‌های سایت هم اصلاً
    /// وجود ندارد.
    ///
    /// ولی «دیده نشود» نباید یعنی «نشود». پس کار همان‌جا ماند و فقط از دید
    /// رفت: راست‌کلیک روی ردیف ⇒ «حذفِ این ردیف». جدولی که این را ببندد،
    /// منویی هم نمی‌گیرد.
    ///
    /// ⚠️ ‎CommandParameter‎ خودِ ویومدلِ همان ردیف است، همان چیزی که ستونِ حذف
    /// هم می‌فرستاد — پس هیچ ویومدلی عوض نشد.
    /// </summary>
    public static readonly StyledProperty<System.Windows.Input.ICommand?> RowDeleteCommandProperty =
        AvaloniaProperty.Register<ExcelGrid, System.Windows.Input.ICommand?>(nameof(RowDeleteCommand));

    public System.Windows.Input.ICommand? RowDeleteCommand
    {
        get => GetValue(RowDeleteCommandProperty);
        set => SetValue(RowDeleteCommandProperty, value);
    }

    /// <summary>
    /// ══ منوی راست‌کلیکِ ردیف — ساخته‌شده در لحظهٔ راست‌کلیک ══════════════════
    ///
    /// ⚠️ پیش از این برای **هر ردیفِ ساخته‌شده** یک ‎MenuFlyout‎ و یک
    /// ‎MenuItem‎ و یک اتصال ساخته می‌شد. اندازه‌گیری نشان داد هزینهٔ باز شدنِ
    /// صفحه تقریباً خطیِ شمارِ ردیف‌های ساخته‌شده است و هر ردیف گران تمام
    /// می‌شود — و فلای‌اوت از سنگین‌ترین چیزهایی است که می‌شود به یک ردیف
    /// آویزان کرد، در حالی که کاربر شاید هیچ‌وقت راست‌کلیک نکند.
    ///
    /// حالا ردیف فقط یک قلابِ سبک می‌گیرد و منو همان لحظه‌ای ساخته می‌شود که
    /// واقعاً راست‌کلیک شد.
    /// </summary>
    /// <summary>دستِ‌کم یک ردیف واقعاً چیده شده؟ — شرحش در ‎SpreadColumns‎.</summary>
    private bool _anyRowLoaded;

    private void OnRowMenu(object? sender, DataGridRowEventArgs e)
    {
        _anyRowLoaded = true;
        e.Row.ContextFlyout = null;
        e.Row.ContextRequested -= OnRowContext;
        if (RowDeleteCommand is null && e.Row.DataContext is not IFlaggedRow) return;
        e.Row.ContextRequested += OnRowContext;
    }

    private void OnRowContext(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        var items = new List<MenuItem>();
        //  «✔ نشانِ سرخ را بردار» — همان دکمهٔ «عادی شد»ِ ستونِ «هشدار» که
        //  برداشته شد (خواستهٔ ۱۴۰۵/۰۷/۰۶: «با یک دکمه بشود عادی‌اش کرد»).
        if (row.DataContext is IFlaggedRow { Flagged: true } f)
            items.Add(new MenuItem { Header = "✔ نشانِ سرخ را بردار — عدد عوض نمی‌شود", Command = f.ClearFlagCommand });
        if (RowDeleteCommand is not null)
            items.Add(new MenuItem
            {
                Header = "🗑 حذفِ این ردیف",
                Command = RowDeleteCommand,
                CommandParameter = row.DataContext,
            });
        if (items.Count == 0) return;
        new MenuFlyout { ItemsSource = items }.ShowAt(row, showAtPointer: true);
        e.Handled = true;
    }



    /// <summary>ردیفی که یکی از خانه‌هایش همین حالا باز است.</summary>
    private RowViewModel? _editRow;

    /// <summary>کادرِ خانهٔ عددی: نوشتهٔ ناخوانا ⇐ کلاسِ ‎badnum‎ (لبهٔ سرخ).</summary>
    private static void WatchBadNumber(TextBox box)
    {
        static void Check(TextBox b) => b.Classes.Set("badnum", !PumpYaqobi.Application.Localization.Shamsi.IsReadable(b.Text));
        Check(box);
        if (box.Tag as string == "badnum-hooked") return;
        box.Tag = "badnum-hooked";
        box.TextChanged += (_, _) => Check(box);
    }

    private void EndRowEdit()
    {
        var r = _editRow;
        _editRow = null;
        r?.EndCellEdit();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // «صفحهٔ دیده‌شونده عوض شد» — شرحش بالای ‎NotifyPagesChanged‎
        PagesChanged -= OnPagesChanged;
        PagesChanged += OnPagesChanged;
        //  و جهتِ «برگرد»، که هم‌زمان است و نه با یک پاس تأخیر
        PagesShown -= OnPagesShown;
        PagesShown += OnPagesShown;
        HookOutside();
        //  شورا، ث۵ — سرستونی که واژهٔ برنامه است معنایش را می‌گوید؛ تنبل، زیرِ ماوس
        Themes.GlossaryTips.Install();
        if (_wired) return;
        _wired = true;
        // تنها منبعِ درستِ «الان در حال ویرایشیم» — خودِ جدول می‌گوید.
        PreparingCellForEdit += (_, e) =>
        {
            _editing = true;
            //  ⛔ حرف‌هایی که پیش از آماده شدنِ کادر زده شدند — شرحش بالای ‎_typeBuf‎.
            //  ⚠️ در **تهِ** همین شنونده می‌نشینند، پس از وصل شدنِ تکمیلِ خودکار
            //  (‎Suggest.Attach‎) — وگرنه تکمیل تغییرِ متن را نمی‌دید و «/ها» هیچ
            //  تکمله‌ای نمی‌گرفت (‎keys17‎ گرفتش).
            var pendingTyped = _typeBuf;
            _typeBuf = null;
            AutoDirection(e.EditingElement);
            //  ⛔ قالبِ ردیف به کادرِ در حالِ نوشتن پس فرستاده نشود — شرحش بالای ‎RowViewModel.BeginCellEdit‎
            if (e.Row?.DataContext is RowViewModel rv && PathOf(e.Column) is { Length: > 0 } path
                && !path.Contains('.') && !path.Contains('['))
            {
                rv.BeginCellEdit(path);
                _editRow = rv;
                if (e.EditingElement is { } ed)
                    ed.DetachedFromVisualTree += (_, _) => { if (ReferenceEquals(_editRow, rv)) EndRowEdit(); };
            }
            // پیشنهادِ خودکار: فهرستِ نام‌دارِ ستون (اگر داشت) + مقدارهای همان ستون
            if (e.EditingElement is TextBox tb || (e.EditingElement?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } tb2 && (tb = tb2) is not null))
            {
                var named = Suggest.Of(Suggest.GetKey(e.Column));
                var learned = PeerColumnValues(e.Column);
                //  «/هارون» — نامِ حساب پس از خط‌کج (ستونِ نامِ ورق)
                var slash = Suggest.Of(Suggest.GetSlashKey(e.Column));
                if (named.Count + learned.Count + slash.Count > 0) Suggest.Attach(tb, named, learned, slash);
                //  شورا، ث۲: راهنمای کم‌رنگ و «می‌دانستید؟»ِ نخستین بار
                if (Suggest.GetHint(e.Column) is { Length: > 0 } hk)
                {
                    if (Services.Hints.Watermark.TryGetValue(hk, out var wm)) tb.Watermark = wm;
                    Services.Hints.DidYouKnow(hk);
                }
            }
            //  ⛔ قالبِ زندهٔ عدد («5,000») و تاریخ («1405/07/19») — ۱۴۰۵/۰۷/۱۹. شرح: ‎LiveFormat‎
            if ((e.EditingElement as TextBox ?? e.EditingElement?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()) is { } fbox)
            {
                var p = PathOf(e.Column);
                LiveFormat.Attach(fbox, p is "DateShamsi" ? "date"
                                      : RowViewModel.IsNumberColumn(p) ? "number" : null);
                //  ⛔ عددِ ناخوانا (شورا، بندِ ۱): خانه همان لحظه سرخ — ردیف مقدارش را عوض نمی‌کند
                if (p is "DensityText" || RowViewModel.IsNumberColumn(p)) WatchBadNumber(fbox);
            }
            if (pendingTyped is { } typed
                && (e.EditingElement as TextBox ?? e.EditingElement?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()) is { } ready)
            {
                LiveFormat.MarkUser(ready);
                ready.Text = typed;
                //  ⚠️ پس از قالبِ زنده متن ممکن است بلندتر از نوشتهٔ خام باشد («5,000»)
                var end = (ready.Text ?? "").Length;
                ready.CaretIndex = end;
                ready.SelectionStart = ready.SelectionEnd = end;
                ready.Focus();
            }
        };
        // ⚠️ پیش از نشستنِ مقدار در ردیف: تکملهٔ پذیرفته‌نشدهٔ پیشنهادِ خودکار
        // برداشته شود، وگرنه «س»ی کاربر «سلام من هارون هستم» ذخیره می‌شد.
        CellEditEnding += (_, e) =>
        {
            Suggest.Settle();
            //  ⛔ خانهٔ عددیِ ناخوانا بسته نمی‌شود (شورا، بندِ ۱): می‌ماند و سرخ است تا
            //  درست شود، یا Esc همان عددِ قبلی را برگرداند. هیچ صفری ذخیره نمی‌شود.
            if (e.EditAction == DataGridEditAction.Commit
                && (e.EditingElement as TextBox ?? e.EditingElement?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()) is { } eb
                && eb.Classes.Contains("badnum"))
            {
                e.Cancel = true;
                AppHost.Current?.Toast("«" + (eb.Text ?? "").Trim() + "» عدد نیست — ذخیره نشد. درستش کنید یا Esc بزنید.", ToastKind.Warn);
            }
        };
        CellEditEnded += (_, _) =>
        {
            EndRowEdit();
            _typeBuf = null;
            _editing = false; _typedIn = false; CaptureEdit();
            // ⛔ خانه‌ای که بسته شد، همان لحظه روی دیسک می‌نشیند — شرحش بالای ‎FlushDirtyRows‎
            FlushDirtyRows();
        };

        // ══ جدول خودش می‌لغزد، نه کلِ صفحه ══════════════════════════════════
        //
        // گزارشِ صاحب ریپو، دو تا با یک ریشه:
        //   «وقتی در کادرِ جدول کلیک می‌کنم، سربرگ‌ها گم می‌شوند — خودم اسکرول
        //    می‌کنم.»
        //   «فقط Grid باید Scroll شود، نه کلِ Layout.»
        //
        // ریشه: ‎RequestBringIntoView‎. با فوکوس گرفتنِ خانه (چه با کلیک، چه با
        // کلید) آوالونیا درخواستِ «مرا در دید بیاور» را به بالا می‌فرستد. این
        // درخواست از جدول بیرون می‌زند و به اسکرولِ واحدِ کلِ پنجره می‌رسد، و
        // آن صفحه را می‌کشد تا خانه وسط بیفتد — سربرگ و نوارِ آمار از بالا
        // می‌روند.
        //
        // ⚠️ درخواست <b>همیشه</b> همین‌جا می‌ایستد، نه فقط وقتی از کلیک آمده.
        // خودِ ‎DataGrid‎ با ‎ScrollIntoView‎ اسکرولِ <b>درونیِ</b> خودش را
        // انجام می‌دهد، پس ناوبری با کلید هم بی این درخواست کار می‌کند و ردیفِ
        // جاری از دید بیرون نمی‌ماند. چیزی که برداشته می‌شود فقط لغزاندنِ
        // ناخواستهٔ کلِ صفحه است.
        AddHandler(RequestBringIntoViewEvent,
                   (_, ev) => ev.Handled = true,
                   RoutingStrategies.Bubble);

        // ══ کشویی با یک کلیک باز شود ════════════════════════════════════════
        //
        // گزارشِ صاحب ریپو: «نه این‌که یک بار بزنی تا کادر کشویی بشود، بعد بارِ
        // بعد بزنی باز بشود، باز بعد بروی انتخاب کنی.»
        //
        // ریشه: ‎DataGrid‎ کلیکِ اول را برای «انتخابِ خانه» مصرف می‌کند و کشویی
        // هرگز آن فشار را نمی‌بیند.
        //
        // ⚠️ روی فازِ ‎Tunnel‎ نشسته، یعنی <b>پیش از</b> آن‌که ‎DataGrid‎ کلیک را
        // ببیند. پس نه تاخیری لازم است و نه ‎Dispatcher‎ی — همان فشارِ اول
        // کشویی را باز می‌کند. ‎Handled‎ هم نمی‌شود تا خانه مثلِ همیشه انتخاب
        // شود.
        AddHandler(PointerPressedEvent, OnPreviewPressed, RoutingStrategies.Tunnel);
        //  پایانِ کشیدنِ ستون — «دیوار» فقط پس از رها کردن می‌سنجد
        AddHandler(PointerReleasedEvent, (_, _) =>
        {
            if (!_headerDown) return;
            _headerDown = false;
            if (KeepInside) InvalidateMeasure();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, _) => _headerDown = false,
                   RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);

        // ══ فلش در «حالتِ نوشتن» ⇒ ذخیره و خانهٔ بعدی ════════════════════════
        //
        // ⚠️ روی فازِ ‎Tunnel‎، وگرنه هرگز صدا زده نمی‌شود: کادرِ تایپ فوکوس
        // دارد، ‎Left/Right‎ را برای بردنِ کُرسر مصرف می‌کند و ‎Handled‎ش
        // می‌کند — پس کلید هیچ‌وقت به ‎OnKeyDown‎ی جدول نمی‌رسد. یک بار همین
        // را در ‎OnKeyDown‎ نوشتم و سنجش نشان داد ستون تکان نمی‌خورد.
        AddHandler(KeyDownEvent, OnPreviewKey, RoutingStrategies.Tunnel);

        // ══ کلیک بیرونِ جدول، ویرایش را تمام کند ═══════════════════════════
        //
        // گزارشِ صاحب ریپو: «وقتی یک خانه در حالِ ویرایش است، هر جای دیگری از
        // برنامه کلیک می‌کنم، هنوز همان خانه ویرایش را نگه می‌دارد.»
        //
        // ریشه: ‎DataGrid‎ی آوالونیا ویرایش را با «فوکوس از دست رفت» تمام
        // نمی‌کند؛ منتظرِ ‎Enter‎ یا ‎Tab‎ می‌ماند. پس تا وقتی خودِ جدول کلیک
        // نمی‌گرفت، خانه در حالتِ تایپ می‌ماند.
        //
        // ⚠️ کلیک روی چیزی که <b>مالِ خودِ جدول</b> است (کشوییِ باز، منوی
        // شناور، پنجرهٔ گفت‌وگو) نباید «بیرون» شمرده شود، وگرنه انتخابِ یک
        // گزینه از کشویی همان لحظه ویرایش را می‌بندد و انتخاب از دست می‌رود.
        // پس فقط وقتی تمام می‌شود که فشار در درختِ بصریِ همین پنجره باشد و
        // هیچ جدّی از آن، این جدول یا یک ‎Popup‎ نباشد.
        //  (بستنش به پنجره در ‎HookOutside‎ است، بیرون از ‎_wired‎ — پایین.)

        // ══ ستونِ «#» — شمارهٔ ردیف ══════════════════════════════════════════
        //
        // گزارشِ صاحب ریپو: «چرا هیچ جدولی شماره ندارد؟» حق داشت: در سایت
        // ستونِ اولِ **هر** جدول ‎#‎ است و شمارهٔ ردیف را نشان می‌دهد
        // (‎&lt;td class="xls-num"&gt;‎). در برنامهٔ نیتیو هیچ جدولی نداشت.
        //
        // ⚠️ چرا سرستونِ ردیف و نه یک ستونِ داده‌ای: ستونِ داده‌ای یعنی هر
        // ویومدلِ ردیف باید خاصیتِ «شماره» داشته باشد و با هر افزودن/حذف
        // همهٔ ردیف‌ها دوباره شماره بخورند — هم کارِ تکراری در چهل ویومدل، هم
        // n برابر کار در هر تغییر. سرستونِ ردیف را خودِ جدول می‌سازد و با
        // مجازی‌سازی فقط برای ردیف‌های دیده‌شده پر می‌شود.
        if (RowNumbers)
        {
            HeadersVisibility = DataGridHeadersVisibility.All;
            FixRowHeaderWidth();
            LoadingRow -= OnNumberRow;
            LoadingRow += OnNumberRow;
        }

        LoadingRow -= OnRowMenu;
        LoadingRow += OnRowMenu;
    }

    /// <summary>
    /// شمارهٔ ردیف — ‎GetIndex()‎ همان جای واقعیِ ردیف در فهرستِ **دیده‌شده**
    /// است، پس با مرتب‌سازی و صافی هم درست می‌ماند و با بازچرخانیِ ردیف‌ها
    /// (مجازی‌سازی) دوباره نوشته می‌شود.
    /// </summary>
    private static void OnNumberRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Header = RowNumber(e.Row);
        WatchNumber(e.Row);
    }

    /// <summary>
    /// ══ شمارهٔ ردیف همان لحظه تازه می‌شود (۱۴۰۵/۰۷/۱۸) ══════════════════════════
    /// شماره فقط سرِ ‎LoadingRow‎ نوشته می‌شد؛ پس وقتی ورق ‎Index‎ِ ردیف‌ها را با «➕ ردیف»
    /// عوض می‌کرد (‎Renumber‎)، ردیف‌های ساخته‌شده شمارهٔ کهنه را نگه می‌داشتند (عکسِ
    /// ‎waraqadd‎: «۱ تا ۱۰» و بعد «۲۱» در جدولی که باید از ۱۱ شروع می‌شد) تا «خروج و ورود».
    /// حالا ردیف به ‎Index‎/‎IndexText‎ِ ویومدلِ خودش گوش می‌دهد — و با بازیافتِ ردیف شنونده
    /// جابه‌جا می‌شود، نه انباشته.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridRow, NumberWatch> _numWatch = new();

    private sealed class NumberWatch
    {
        public System.ComponentModel.INotifyPropertyChanged? Source;
        public System.ComponentModel.PropertyChangedEventHandler? Handler;
    }

    private static void WatchNumber(DataGridRow row)
    {
        var w = _numWatch.GetValue(row, _ => new NumberWatch());
        if (w.Source is not null && w.Handler is not null) w.Source.PropertyChanged -= w.Handler;
        w.Source = null; w.Handler = null;
        if (row.DataContext is not System.ComponentModel.INotifyPropertyChanged npc) return;
        var t = npc.GetType();
        if (t.GetProperty("IndexText") is null && t.GetProperty("Index") is null) return;
        w.Source = npc;
        w.Handler = (s, a) =>
        {
            if (a.PropertyName is not ("Index" or "IndexText")) return;
            if (!ReferenceEquals(row.DataContext, s)) return;
            row.Header = RowNumber(row);
        };
        npc.PropertyChanged += w.Handler;
    }

    /// <summary>
    /// ══ شمارهٔ ردیف: اول از خودِ ردیف بپرس ══════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو با عکس: «۱۲۰ است، اما تو جای تیره ۴۵ — این باید ۱۲۰
    /// بشود.»
    ///
    /// حق داشت. ‎GetIndex()‎ جای ردیف در **همین جدول** است، و بخشی مثلِ ورق
    /// یک فهرستِ واحد را بینِ دو جدول نصف می‌کند (‎TxnsFirst‎/‎TxnsSecond‎).
    /// پس ردیفی که در کلِ ورق شمارهٔ ۱۲۰ است، در جدولِ دوم ردیفِ ۴۵ می‌افتد و
    /// نوار همان ۴۵ را می‌نوشت. ستونِ دادهٔ «#» عددِ درست را داشت — و همین
    /// دوتایی شدن هم ایرادِ دیگرِ همان گزارش بود.
    ///
    /// حالا نوار اول از خودِ ویومدلِ ردیف می‌پرسد (‎IndexText‎ یا ‎Index‎) و
    /// فقط اگر نداشت به جای ردیف برمی‌گردد. پس ستونِ «#» می‌تواند برود.
    /// </summary>
    private static object RowNumber(DataGridRow row)
    {
        if (row.DataContext is { } dc)
        {
            var t = dc.GetType();
            if (!_numProp.TryGetValue(t, out var p))
                _numProp[t] = p = t.GetProperty("IndexText") ?? t.GetProperty("Index");

            switch (p?.GetValue(dc))
            {
                case string s when s.Length > 0: return s;
                case int i when i > 0: return i;
            }
        }
        return row.GetIndex() + 1;
    }

    /// <summary>خاصیتِ شمارهٔ هر نوعِ ردیف — تا بازتاب هر بار تکرار نشود.</summary>
    private static readonly Dictionary<Type, System.Reflection.PropertyInfo?> _numProp = new();

    /// <summary>
    /// همان قاعدهٔ اکسل: در «حالتِ نوشتن» فلش مقدار را ذخیره می‌کند و به خانهٔ
    /// بغلی می‌رود — هر جای متن که کُرسر باشد. در «حالتِ ویرایش» (‎F2‎) دست
    /// نمی‌زنیم و کادرِ تایپ خودش کُرسر را می‌برد.
    /// </summary>
    private void OnPreviewKey(object? sender, KeyEventArgs e)
    {
        if (!_editing || !_typedIn) return;
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;

        //  ⛔ **پیش از** بستنِ ویرایش: خودِ ‎CommitEdit‎ می‌تواند خانهٔ جاری را
        //  جابه‌جا کند و آن‌وقت گام از جای اشتباه حساب می‌شد — همان «کلیکِ
        //  دوم درست می‌شود».
        var cols = VisibleCols();
        var from = CurIndex(cols);

        CommitEdit(DataGridEditingUnit.Cell, true);

        if (e.Key is Key.Up or Key.Down) MoveRow(e.Key == Key.Down ? +1 : -1);
        else if (!MoveColumnFrom(cols, from, e.Key == Key.Right ? -1 : +1)) CrossToNeighbour(e.Key);

        e.Handled = true;
    }

    private void OnOutsidePressed(object? sender, PointerPressedEventArgs e)
    {
        // ⛔ گاردِ قدیمی ‎_editing‎ بود و نیمِ مشکل را باقی می‌گذاشت: خانه‌ای
        // که فقط انتخاب شده (نه در حالِ تایپ) همچنان فوکوس را نگه می‌داشت و
        // کلیدها به همان جدول می‌رفتند — همان «از آن جدول بیرون نمی‌شوم».
        if (!_editing && !IsKeyboardFocusWithin) return;
        if (e.Source is not Visual v) return;

        for (Visual? x = v; x is not null; x = x.GetVisualParent())
        {
            if (ReferenceEquals(x, this)) return;        // داخلِ خودِ جدول
            if (x is Popup or FlyoutPresenter) return;    // کشویی/منوی همین جدول
        }

        // واقعاً بیرون بود: مقدارِ نیمه‌تمام ثبت شود، روی دیسک بنشیند، و جدول فوکوس را رها کند.
        LeaveNow();
    }

    /// <summary>
    /// ══ کشوییِ داخلِ خانه: یک کلیک، مثلِ سایت ═══════════════════════════════
    ///
    /// در سایت این کادر یک ‎&lt;select class="xls-in"&gt;‎ی همیشه‌پیداست: یک
    /// کلیک بازش می‌کند، یکی هم انتخاب. در برنامهٔ نیتیو ‎DataGrid‎ کلیک را
    /// برای «انتخابِ خانه» مصرف می‌کند و کشویی یا باز نمی‌شد یا همان لحظه
    /// بسته می‌شد — همان «راحت کار نمی‌کند»ی گزارش‌شده.
    ///
    /// پس روی فازِ ‎Tunnel‎ — یعنی **پیش از** آن‌که ‎DataGrid‎ کلیک را ببیند —
    /// خودمان کار را تمام می‌کنیم:
    ///   ۱) ردیفِ زیرِ انگشت انتخاب می‌شود (وگرنه حالِ جدول عقب می‌ماند)،
    ///   ۲) کشویی باز می‌شود،
    ///   ۳) ‎Handled‎ می‌شود تا ‎DataGrid‎ همان کلیک را دوباره مصرف نکند و
    ///      کشویی را نبندد.
    ///
    /// ⚠️ هیچ ‎Dispatcher‎ی و هیچ تاخیری در کار نیست — خواستهٔ صریح.
    /// ⚠️ کلیکِ بازِ دوباره (برای بستن) دست‌نخورده می‌ماند: اگر کشویی باز است
    /// کاری نمی‌کنیم و خودِ کنترل می‌بنددش.
    /// </summary>
    private void OnPreviewPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual v) return;

        DataGridRow? row = null;
        for (Visual? x = v; x is not null; x = x.GetVisualParent())
        {
            if (x is DataGridColumnHeader)
            {
                ReleaseStarFloors();
                _headerDown = true;
                _dragGrab = -1; _dragNeighbor = -1;
                _pressWidths = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex)
                                      .Select(c => c.ActualWidth).ToArray();
                return;
            }
            if (x is DataGridRow r) { row = r; continue; }
            if (x is not ComboBox cb) continue;
            if (!cb.IsEffectivelyEnabled || cb.IsDropDownOpen) return;

            // ردیف را از بالای همین زنجیره پیدا کن (کشویی داخلِ خانه است)
            for (Visual? y = cb; y is not null && row is null; y = y.GetVisualParent())
                if (y is DataGridRow rr) row = rr;
            if (row?.DataContext is { } item) SelectedItem = item;

            cb.IsDropDownOpen = true;
            e.Handled = true;
            return;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ⚠️ کلیدِ چپ و راست هنگامِ تایپ — «می‌زنم چپ، می‌رود راست»
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو: «موقعِ تایپ، اگر بخواهی چپ بروی اتومات می‌رود راست،
    //  بعدش می‌رود چپ — باگِ خیلی مزخرفی است.»
    //
    //  بازتولید شد و اندازه گرفته شد (حالتِ ‎keys‎ی ‎PumpYaqobi.UiTests‎):
    //
    //      متنِ خانه     کلید   کُرسر
    //      12,345         ←     ۳ ⇐ ۴    ✖ (باید ۲ می‌شد)
    //      12,345         →     ۳ ⇐ ۲    ✖ (باید ۴ می‌شد)
    //      برق دکان       ←     ۴ ⇐ ۵    ✔
    //      برق دکان       →     ۴ ⇐ ۳    ✔
    //
    //  یعنی در خانهٔ **عددی** کُرسر وارونه می‌رفت و در خانهٔ فارسی درست.
    //
    //  ریشه: کلِ پنجره راست‌به‌چپ است، پس کادرِ تایپ هم ‎FlowDirection‎ی
    //  راست‌به‌چپ به ارث می‌برد و آوالونیا کُرسر را بر اساسِ همان جهتِ **پایه**
    //  می‌بَرد، نه بر اساسِ جهتِ خودِ نوشته. عددِ لاتین در یک کادرِ راست‌به‌چپ
    //  چپ‌به‌راست دیده می‌شود، پس هر کلید وارونه حس می‌شود.
    //
    //  چارهٔ همین کار در وب ‎dir="auto"‎ است و سایت هم دقیقاً همان را دارد:
    //  جهتِ کادر از **نخستین حرفِ قویِ** خودِ نوشته می‌آید. همان قاعده این‌جا
    //  پیاده شده، پس:
    //    • خانهٔ عدد/تاریخ ⇒ چپ‌به‌راست ⇒ ←/→ همان‌جا که چشم می‌بیند
    //    • خانهٔ نامِ فارسی ⇒ راست‌به‌چپ ⇒ باز هم همان‌جا که چشم می‌بیند
    //
    //  ⚠️ وسط‌چینیِ خانه‌ها از ‎TextAlignment="Center"‎ می‌آید، نه از جهت، پس
    //  ظاهرِ جدول عوض نمی‌شود — فقط کُرسر درست راه می‌رود.

    /// <summary>
    /// جهتِ کادرِ تایپ را از خودِ نوشته‌اش می‌گیرد، و با هر تایپ دوباره
    /// می‌سنجد (خانهٔ خالی که فارسی تایپ شود، همان‌جا راست‌به‌چپ می‌گردد).
    /// </summary>
    private static void AutoDirection(Control? editor)
    {
        var box = editor as TextBox
               ?? editor?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        if (box is null) return;

        Sync();
        box.PropertyChanged -= OnEditorText;
        box.PropertyChanged += OnEditorText;

        void OnEditorText(object? _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextBox.TextProperty) Sync();
        }

        // ⚠️ فقط وقتی واقعاً عوض شده باشد: نوشتنِ دوبارهٔ همان مقدار، کُرسر را
        // سرِ جای اول برمی‌گرداند و تایپ می‌پرد.
        void Sync()
        {
            var want = DirectionOf(box.Text);
            if (box.FlowDirection != want) box.FlowDirection = want;
        }
    }

    /// <summary>
    /// ‎dir="auto"‎ی وب: نخستین حرفِ قوی جهت را تعیین می‌کند؛ نوشته‌ای که هیچ
    /// حرفِ قوی ندارد (عدد، تاریخ، کاما) چپ‌به‌راست است — همان کاری که مرورگر
    /// می‌کند.
    /// </summary>
    public static Avalonia.Media.FlowDirection DirectionOf(string? text)
    {
        foreach (var ch in text ?? "")
        {
            // ══ رقم هرگز «حرفِ قوی» نیست ══════════════════════════════════════
            //
            // ⚠️ این‌جا جای همان باگی بود که دو بار گزارش شد: «می‌خواهم یک جهت
            // بروم — چپ یا راست — باگ می‌خورد و برعکس می‌رود.»
            //
            // رقمِ فارسی ‎۰..۹‎ یعنی ‎U+06F0..U+06F9‎، و جداکنندهٔ ‎٬‎ یعنی
            // ‎U+066C‎ — هر دو **داخلِ** بازهٔ ‎0590..08FF‎ی زیر. پس عددی مثلِ
            // ‎۱۲٬۳۴۵‎ «فارسی» تشخیص داده می‌شد، کادر راست‌به‌چپ می‌گشت، و
            // کُرسر وارونه راه می‌رفت — در حالی که خودِ عدد چپ‌به‌راست دیده
            // می‌شود. با رقمِ لاتین (‎12,345‎) درست بود و همین پنهانش کرد.
            //
            // اندازه‌گیری (‎keys‎)، پیش از این تغییر:
            //
            //     ۱۲٬۳۴۵   ←   ۳ ⇐ ۴    ✖ (باید ۲ می‌شد)
            //     ۱۲٬۳۴۵   →   ۳ ⇐ ۲    ✖ (باید ۴ می‌شد)
            //     12,345   ←   ۳ ⇐ ۲    ✔
            //
            // و الگوریتمِ دوسویهٔ یونیکد هم همین را می‌گوید: رقم‌ها ردهٔ
            // ‎AN‎/‎EN‎ دارند، نه ‎R‎ — یعنی هیچ‌وقت جهتِ پاراگراف را تعیین
            // نمی‌کنند. ‎dir="auto"‎ی مرورگر هم از رویشان رد می‌شود.
            if (ch is >= (char)0x0660 and <= (char)0x0669     // رقمِ عربی
                   or >= (char)0x06F0 and <= (char)0x06F9     // رقمِ فارسی
                   or (char)0x066A or (char)0x066B or (char)0x066C)  // ٪ ٫ ٬
                continue;

            // عبری، عربی، فارسی و همسایه‌هایشان
            if (ch is >= (char)0x0590 and <= (char)0x08FF
                   or >= (char)0xFB1D and <= (char)0xFDFF
                   or >= (char)0xFE70 and <= (char)0xFEFF)
                return Avalonia.Media.FlowDirection.RightToLeft;
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                return Avalonia.Media.FlowDirection.LeftToRight;
        }
        return Avalonia.Media.FlowDirection.LeftToRight;
    }

    /// <summary>کنترلی که همین حالا فوکوس دارد.</summary>
    private Control? Focused =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;

    /// <summary>ستون‌های دیده‌شونده به ترتیبِ دیداری.</summary>
    private List<DataGridColumn> VisibleCols() =>
        Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();

    /// <summary>شمارهٔ ستونِ جاری در همان ترتیب (‎-1‎ یعنی هیچ).</summary>
    private int CurIndex(List<DataGridColumn> cols) =>
        CurrentColumn is null ? (cols.Count > 0 ? 0 : -1) : cols.IndexOf(CurrentColumn);

    /// <summary>
    /// ‎_toggleControl‎ — مقدارِ کشویی یا رادیوییِ خانه را یک پله جلو می‌برد.
    /// ‎true‎ یعنی چیزی عوض شد و کلید نباید کارِ دیگری بکند.
    /// </summary>
    /// <summary>این کنترل همانی است که ‎Tab‎/‎Enter‎ می‌تواند مقدارش را جلو ببرد؟</summary>
    private static bool CanToggle(object? c) =>
        c is ComboBox or RadioButton || (c is Button b && b.Classes.Contains("celltoggle"));

    private bool ToggleCell()
    {
        if (IsReadOnly) return false;

        // اول خودِ خانه: کشویی‌های همیشه‌پیدا فوکوس ندارند ولی همان‌جا هستند.
        var target = CellPicker(CurrentColumn) ?? Focused;
        if (!CanToggle(target))
        {
            // قالبِ ویرایشی دارد؟ بازش کن تا ساخته شود.
            BeginEdit();
            target = Focused;
        }

        switch (target)
        {
            case ComboBox cb when cb.ItemCount > 0:
                cb.SelectedIndex = (cb.SelectedIndex + 1) % cb.ItemCount;
                return true;

            // کپسولِ خانه (مثلِ «نوع تیل»): فرمانِ خودش را می‌زنیم — همان کاری
            // که کلیکِ کاربر می‌کند، پس منطق یک جا می‌ماند.
            case Button btn when btn.Classes.Contains("celltoggle"):
                if (btn.Command?.CanExecute(btn.CommandParameter) != true) return false;
                btn.Command.Execute(btn.CommandParameter);
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
    /// ستونی که ویرایشش کشویی یا رادیویی است — «نوع تیل»، «نوع» (قرض/مصرف)،
    /// «واحد» (تیل/پول) و مانندِ آن‌ها.
    /// </summary>
    /// <summary>
    /// ⚠️ «کشویی داخلِ قالبِ ویرایش» دیگر تنها نشانه نیست: از وقتی کشویی‌ها —
    /// مثلِ ‎&lt;select class="xls-in"&gt;‎ی سایت — همیشه پیدا شدند و به
    /// ‎CellTemplate‎ رفتند، ‎CellEditingTemplate‎شان خالی است و ‎Tab‎/‎Enter‎
    /// بی‌صدا از کار افتاده بود. حالا خودِ خانه نگاه می‌شود.
    /// </summary>
    private bool IsToggleColumn(DataGridColumn? col) =>
        col is DataGridTemplateColumn t &&
        (t.CellEditingTemplate is not null || CellPicker(col) is not null);

    /// <summary>
    /// کشویی/رادیوییِ داخلِ خانهٔ جاری — همان چیزی که ‎Tab‎ و ‎Enter‎ باید
    /// مقدارش را یک پله جلو ببرند. ‎null‎ یعنی این خانه چنین چیزی ندارد.
    /// </summary>
    private Control? CellPicker(DataGridColumn? col)
    {
        if (col is null || SelectedItem is null) return null;
        var row = this.GetVisualDescendants().OfType<DataGridRow>()
                      .FirstOrDefault(r => ReferenceEquals(r.DataContext, SelectedItem));
        if (row is null) return null;

        //  ⛔ خانه را با <b>خودِ ستونش</b> پیدا کن، نه با شمارهٔ ترتیبی (۱۴۰۵/۰۷/۱۹). ترتیبِ
        //  خانه‌ها در درختِ دیداری ترتیبِ ساختِ ستون‌هاست و ‎VisibleCols‎ ترتیبِ دیدنِ
        //  آن‌ها (‎DisplayIndex‎)؛ با یک جابه‌جاییِ ستون (کشیدنِ سربرگ — گاوصندوق دارد)
        //  یا ستونِ پنهان، این دو از هم جدا می‌شدند و ‎Enter‎/‎Tab‎ کپسولِ «نوع» را
        //  پیدا نمی‌کردند: به‌جای عوض کردنِ نوع، ردیف عوض می‌شد.
        var cell = row.GetVisualDescendants().OfType<DataGridCell>()
                      .FirstOrDefault(c => ReferenceEquals(ColumnOfCell(c), col));
        if (cell is null) return null;

        // ⚠️ ‎Button.celltoggle‎ هم شمرده می‌شود، نه فقط کشویی و رادیویی.
        //
        // گزارشِ صاحب ریپو: «نوعِ تیل هم تو بخشِ قرض‌داران با تب یا اینتر عوض
        // نمی‌شه.» حق داشت و کارِ خودم بود: ستونِ «نوع تیل» تا دیروز دو دکمهٔ
        // رادیویی داشت و این‌جا شناخته می‌شد؛ وقتی به یک کپسول تبدیلش کردم،
        // از این فهرست افتاد و ‎Tab‎/‎Enter‎ دیگر کاری نمی‌کرد.
        return cell.GetVisualDescendants()
                         .FirstOrDefault(x => x is ComboBox or RadioButton
                                           || (x is Button b && b.Classes.Contains("celltoggle")))
                         as Control;
    }

    // ── جابه‌جاییِ خانه ───────────────────────────────────────────────────

    /// <summary>
    /// ستونِ جاری را ‎step‎ خانه جابه‌جا می‌کند (ترتیبِ دیداری).
    /// ‎extend‎ یعنی ‎Shift‎ گرفته شده: سرِ کادر سرِ جایش می‌ماند و فقط ته آن می‌رود.
    /// </summary>
    private bool MoveColumn(int step, bool extend = false)
    {
        var cols = VisibleCols();
        if (cols.Count == 0) return false;
        return MoveColumnFrom(cols, CurIndex(cols), step, extend);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ «کدام طرف» یک جا تصمیم گرفته می‌شود، و از روی چیدمانِ واقعی ════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «وقتی روی یک جدول استم اوکی است؛ وقتی
    //  توی یک کادرِ جدول می‌نویسم و می‌خواهم بروم کادرِ بعدی، راست به چپ
    //  می‌رود و با کلیکِ دوم درست می‌شود.»
    //
    //  دو چیزِ جدا زیرش بود:
    //
    //  ⛔ **جهت یک نویسه هم عوض نشد.** نسخهٔ اولِ همین اصلاح آن را از
    //     ‎FlowDirection‎ خواند و یک **پس‌رفت** بود: پایین‌تر، بالای
    //     ‎case Key.Left‎، به‌صراحت نوشته شده که همان راه بارِ اول امتحان
    //     شد و بی‌صدا برعکس می‌شد — و ‎ArrowKeysFollowWhatTheEyeSees‎ همان
    //     را قفل کرده بود و همین پس‌رفت را در CI گرفت. حتی تابعِ میانیِ
    //     «تمیزتر» هم برداشته شد: این خانه سه بار عوض شده و هر بار گران
    //     تمام شده، پس متنش دست‌نخورده می‌ماند.
    //
    //  ⛔ آن‌چه واقعاً عوض شد، همان «کلیکِ دوم درست می‌شود» است: در حالتِ **نوشتن** اول
    //     ‎CommitEdit‎ زده می‌شد و **بعد** ستونِ جاری خوانده می‌شد. بستنِ
    //     ویرایش خودش می‌تواند خانهٔ جاری را جابه‌جا کند، پس گام از ستونِ
    //     **تازه** حساب می‌شد و یک خانه پرت می‌افتاد؛ فشارِ بعدی از جای
    //     درست شروع می‌کرد و «درست» به نظر می‌رسید. حالا ستونِ مبدأ **پیش
    //     از** بستنِ ویرایش برداشته می‌شود.

    /// <summary>
    /// ══ لبهٔ جدول ⇒ جدولِ کناری (۱۴۰۵/۰۷/۱۸) ═════════════════════════════════
    /// گزارشِ صاحب ریپو: «در ورق‌ها حرکت بین تراکنش‌های راست و چپ متوقف می‌شود.»
    /// چپ/راست در لبهٔ جدول کلید را می‌خورد و هیچ کدی فوکوس را به جدولِ بغلی
    /// نمی‌داد. حالا اگر در همان جهت جدولِ دیدنیِ دیگری (هم‌ردیف، در همان صفحهٔ
    /// بخش) باشد، فوکوس به همان شمارهٔ ردیف در آن جدول و به ستونِ لبهٔ نزدیک می‌رود.
    /// ⛔ جهت از جای واقعیِ روی صفحه است (<see cref="Services.FieldNavigationService.ScreenX"/>)،
    /// نه از ترتیبِ ستون‌ها؛ جدولِ بی‌ردیف مقصد نیست.
    /// </summary>
    internal bool CrossToNeighbour(Key key)
    {
        var scope = this.FindAncestorOfType<SectionPage>() as Control
                    ?? this.FindAncestorOfType<UserControl>() as Control;
        if (scope is null || Services.FieldNavigationService.ScreenRect(this) is not { } me) return false;

        var wantLeft = key == Key.Left;
        ExcelGrid? best = null;
        var bestGap = double.PositiveInfinity;
        foreach (var g in scope.GetVisualDescendants().OfType<ExcelGrid>())
        {
            if (ReferenceEquals(g, this) || !g.IsEffectivelyVisible || !g.IsEffectivelyEnabled) continue;
            if (g.ItemsSource is not System.Collections.IList { Count: > 0 }) continue;
            if (Services.FieldNavigationService.ScreenRect(g) is not { } r) continue;
            // هم‌ردیف: بازهٔ عمودی‌شان هم‌پوشانی دارد
            if (r.Bottom <= me.Top || r.Top >= me.Bottom) continue;
            var gap = wantLeft ? me.Left - r.Right : r.Left - me.Right;
            if (gap < -4) continue;
            if (gap < bestGap) { bestGap = gap; best = g; }
        }
        if (best is null) return false;

        if (_editing) CommitEdit(DataGridEditingUnit.Cell, true);
        var list = (System.Collections.IList)best.ItemsSource!;
        var row = Math.Clamp(SelectedIndex < 0 ? 0 : SelectedIndex, 0, list.Count - 1);
        var cols = best.VisibleCols();
        if (cols.Count == 0) return false;
        // ستون ۰ سمتِ راست است (کلِ برنامه راست‌به‌چپ): رفتن به چپ ⇒ لبهٔ راستِ جدولِ چپ
        var col = wantLeft ? cols[0] : cols[^1];
        best.SelectedIndex = row;
        best.CurrentColumn = col;
        best.ResetRange(cols.IndexOf(col));
        best.ScrollIntoView(list[row], col);
        best.Focus(NavigationMethod.Directional);
        best.PaintRange();
        Dispatcher.UIThread.Post(best.FollowCell, DispatcherPriority.Background);
        return true;
    }

    /// <summary>همان جابه‌جایی، ولی با ستونِ مبدأی که خودِ صدازننده می‌دهد.</summary>
    private bool MoveColumnFrom(List<DataGridColumn> cols, int cur, int step, bool extend = false)
    {
        if (cols.Count == 0) return false;
        if (cur < 0) cur = 0;
        var next = Math.Clamp(cur + step, 0, cols.Count - 1);
        if (next == cur && !extend) return false;

        CurrentColumn = cols[next];
        if (extend)
        {
            if (_colAnchor < 0) _colAnchor = cur;
            _colHead = next;
        }
        else ResetRange(next);

        var item = SelectedItem ?? (ItemsSource as System.Collections.IEnumerable)?.Cast<object>().FirstOrDefault();
        if (item is not null) ScrollIntoView(item, cols[next]);
        PaintRange();
        Dispatcher.UIThread.Post(FollowCell, DispatcherPriority.Background);
        return true;
    }

    /// <summary>کادرِ چندانتخابی جمع می‌شود و روی همان یک ستون می‌نشیند.</summary>
    private void ResetRange(int col)
    {
        _colAnchor = _colHead = col;
    }

    /// <summary>
    /// ══ Tab: خانهٔ بعدی، و ته ردیف ⇒ سرِ ردیفِ بعد ═══════════════════════
    /// همان کاری که اکسل می‌کند. اگر ویرایش باز باشد اول ذخیره می‌شود.
    /// </summary>
    private void MoveCell(int step)
    {
        if (_editing) CommitEdit(DataGridEditingUnit.Cell, true);
        var cols = VisibleCols();
        if (cols.Count == 0) return;
        var cur = CurIndex(cols);
        if (cur < 0) cur = 0;
        var next = cur + step;

        if (next >= cols.Count) { MoveRow(+1); next = 0; }
        else if (next < 0) { MoveRow(-1); next = cols.Count - 1; }

        CurrentColumn = cols[next];
        ResetRange(next);
        var item = SelectedItem;
        if (item is not null) ScrollIntoView(item, cols[next]);
        PaintRange();
    }

    // ── کادرِ رنگیِ چندانتخابی ────────────────────────────────────────────
    //
    // ‎DataGrid‎ی آوالونیا فقط «ردیف» را انتخاب می‌کند و خبری از انتخابِ
    // خانه‌به‌خانه ندارد. پس ستون‌های داخلِ کادر خودمان رنگ می‌شوند: به هر
    // خانهٔ داخلِ کادر کلاسِ ‎rangesel‎ داده می‌شود و رنگش در ‎Controls.axaml‎
    // تعریف شده. شمارهٔ ستونِ هر خانه از جای دیداری‌اش خوانده می‌شود
    // (‎Bounds.X‎ داخلِ ردیف) چون خودِ ‎DataGridCell‎ ستونش را بیرون نمی‌دهد.
    // ⚠️ چیدمانِ راست‌به‌چپ در آوالونیا یک «آینهٔ رسم» است، نه چیدمانِ وارونه؛
    // پس ‎Bounds.X‎ در هر دو جهت همان ترتیبِ ستون‌هاست و نباید برعکس شود.
    private void PaintRange()
    {
        var lo = Math.Min(_colAnchor, _colHead);
        var hi = Math.Max(_colAnchor, _colHead);
        var many = _colAnchor >= 0 && hi > lo;
        if (!many && !_painted) return;      // چیزی رنگی نیست و نبوده — کاری نکن
        _painted = many;

        try
        {
            foreach (var row in this.GetVisualDescendants().OfType<DataGridRow>())
            {
                var cells = row.GetVisualDescendants().OfType<DataGridCell>()
                               .OrderBy(c => c.Bounds.X).ToList();
                for (var i = 0; i < cells.Count; i++)
                    cells[i].Classes.Set("rangesel", many && row.IsSelected && i >= lo && i <= hi);
            }
        }
        catch { /* رنگ فقط تزیین است — هرگز نباید جلوی کار را بگیرد */ }
    }

    /// <summary>کلیک یعنی «از نو» — کادرِ چندانتخابی جمع می‌شود.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _colAnchor = _colHead = -1;
            PaintRange();
        }

        base.OnPointerPressed(e);
    }


    // ── خالی کردنِ خانه‌های انتخابی ───────────────────────────────────────
    //
    // ستون‌های این برنامه همه به یک خاصیتِ رشته‌ایِ ‎…Text‎ بسته‌اند (همان‌ها که
    // ‎MoneyOrBlank‎ را می‌سازند)، پس «خالی کردن» یعنی نوشتنِ رشتهٔ خالی در
    // همان خاصیت — دقیقاً همان چیزی که اگر کاربر خودش خانه را پاک می‌کرد
    // اتفاق می‌افتاد. ستونِ خواندنی و ستونِ بی‌اتصال دست نمی‌خورند.
    private static string? PathOf(DataGridColumn? col) =>
        (col as DataGridBoundColumn)?.Binding is Avalonia.Data.Binding b ? b.Path : null;

    private bool ClearSelectedCells()
    {
        if (IsReadOnly) return false;
        if (Selection() is not { } r) return false;

        var log = new List<Edit>();
        foreach (var item in r.Rows)
            foreach (var col in r.Cols)
                Write(item, col, "", log);

        if (log.Count == 0) return false;
        PushUndo(log);
        return true;
    }
}
