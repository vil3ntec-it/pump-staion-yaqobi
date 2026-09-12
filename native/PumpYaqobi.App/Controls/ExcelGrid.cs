using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
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
        LayoutUpdated += (_, _) => { SpreadColumns(); Settle(); };
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
    //  ⚠️ ولی سقفِ «یک صفحه» غلط بود و صاحب ریپو درست گفت:
    //
    //      «جدول باید با زیاد شدنِ تراکنش‌ها خودش بزرگ شود. نباید یک ارتفاعِ
    //       ثابت باشد که بعد از چند ردیف فقط خودِ جدول داخلش اسکرول شود.
    //       شماره‌های ۴۰ و ۵۰ و ۵۱ نباید داخلِ یک کادرِ محدود گیر کنند.»
    //
    //  حق داشت: با سقفِ یک‌صفحه، از ردیفِ چهاردهم به بعد جدول دیگر بلند
    //  نمی‌شد و بقیهٔ ردیف‌ها داخلِ همان کادر پنهان می‌شدند.
    //
    //  حالا جدول تا <see cref="GrowRowLimit"/> ردیف **کاملاً باز** می‌شود:
    //  هیچ سقفی ندارد، هر ردیفِ تازه دقیقاً زیرِ ردیفِ قبلی ساخته می‌شود، و
    //  صفحه بلندتر می‌گردد — اسکرول یکی است و مالِ کلِ صفحه، مثلِ سایت.
    //
    //  سقف فقط در یک جا برمی‌گردد: جدولی که ردیف‌هایش از آن مرز بگذرد. آن‌جا
    //  دیگر بحثِ «چند ردیف بیشتر» نیست؛ بی مجازی‌سازی، صد هزار ردیف برنامه را
    //  قفل می‌کند (‎grid-perf‎ همین را با یک میلیون ردیف می‌سنجد). آن مرز
    //  آن‌قدر بالاست که هیچ جدولِ واقعیِ این برنامه به آن نمی‌رسد.

    /// <summary>اسکرولِ صفحه در ‎MainWindow‎؛ یک‌بار پیدا می‌شود و نگه داشته می‌شود.</summary>
    private ScrollViewer? _page;

    private ScrollViewer? Page =>
        _page ??= this.GetVisualAncestors().OfType<ScrollViewer>()
                      .FirstOrDefault(v => v.Name == "PageScroll");

    /// <summary>
    /// تا این شمارِ ردیف، جدول هیچ سقفی ندارد و هم‌قدِ ردیف‌هایش بلند می‌شود.
    ///
    /// ⚠️ عدد بزرگ است چون هیچ جدولِ واقعیِ این برنامه به آن نمی‌رسد؛ فقط
    /// جلوی «یک میلیون ردیف در یک صفحه» را می‌گیرد. اگر روزی کمش کردید،
    /// دوباره همان کادرِ محدودی می‌شود که صاحب ریپو از آن شکایت داشت.
    /// </summary>
    public const int GrowRowLimit = 600;

    /// <summary>چند پیکسلِ اصلاحیِ بلندی — پایین‌ترِ همین فایل، ‎Settle‎.</summary>
    private double _pad;

    /// <summary>شمارِ ردیف‌هایی که ‎_pad‎ برایشان حساب شده.</summary>
    private int _padRows = -1;

    /// <summary>
    /// جدول هم‌قدِ ردیف‌هایش می‌شود؛ تنگنا فقط وقتی می‌آید که شمارِ ردیف‌ها از
    /// <see cref="GrowRowLimit"/> بگذرد.
    ///
    /// ⚠️ چرا بلندی این‌جا **حساب** می‌شود و به بی‌کران سپرده نمی‌شود:
    /// ‎DataGrid‎ی آوالونیا با بلندیِ بی‌کران خودش را هم‌قدِ همهٔ ردیف‌ها
    /// نمی‌کند — به تخمینِ حدود شانزده ردیف بسنده می‌کند و بقیه را داخلِ
    /// خودش می‌لغزاند. همان «کادرِ محدودی» که صاحب ریپو دید. سنجشِ اسکرول
    /// هم همین را نشان داد: جدولِ گاوصندوق و مصارف و صرافی روی ۷۲۴ پیکسل
    /// می‌ایستادند و تا ۷۵۵ پیکسل لغزشِ درونی داشتند.
    ///
    /// پس بلندی از خودِ داده می‌آید: سربرگ + شمارِ ردیف × بلندیِ ردیف + لبه
    /// (+ نوارِ لغزشِ افقی، اگر دیده شود).
    ///
    /// ⚠️ و تنگنا باید **پیش از** اندازه‌گیری اعمال شود، نه پس از چیدمان:
    /// وگرنه همان پاسِ اول با بلندیِ بی‌کران انجام شده است.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var rows = RowCount();

        if (rows < 0 || rows > GrowRowLimit)
        {
            var screen = Page?.Viewport.Height ?? 0;
            if (screen <= 0) screen = 900;                 // «نمی‌دانم» ≠ «بی‌کران»
            if (availableSize.Height > screen)
                availableSize = availableSize.WithHeight(screen);
            return base.MeasureOverride(availableSize);
        }

        if (rows != _padRows) { _pad = 0; _padRows = rows; }

        var want = WantedHeight(rows);
        if (availableSize.Height > want) availableSize = availableSize.WithHeight(want);
        var size = base.MeasureOverride(availableSize);
        return size.WithHeight(want);
    }

    /// <summary>بلندیِ واقعیِ جدول برای این شمارِ ردیف.</summary>
    private double WantedHeight(int rows)
    {
        var rowH = double.IsNaN(RowHeight) || RowHeight <= 0 ? 44d : RowHeight;

        var head = 0d;
        if (HeadersVisibility != DataGridHeadersVisibility.None)
        {
            foreach (var h in this.GetVisualDescendants().OfType<DataGridColumnHeader>())
                head = Math.Max(head, h.Bounds.Height);
            if (head <= 0) head = rowH;                    // هنوز چیده نشده
        }

        var bar = 0d;
        var hbar = this.GetVisualDescendants().OfType<ScrollBar>()
                       .FirstOrDefault(b => b.Orientation == Orientation.Horizontal);
        if (hbar is { IsVisible: true }) bar = Math.Max(hbar.Bounds.Height, 12d);

        return head + rows * rowH + BorderThickness.Top + BorderThickness.Bottom + bar + _pad;
    }

    /// <summary>
    /// ══ ته‌نشین شدن ═══════════════════════════════════════════════════════
    /// اگر با همهٔ حساب‌وکتاب باز هم چند پیکسل کم آمده باشد و نوارِ لغزشِ
    /// عمودیِ جدول چیزی برای لغزاندن داشته باشد، همان‌قدر بلندتر می‌شویم.
    ///
    /// ⚠️ قاعدهٔ صاحب ریپو صریح است: «اسکرول شدنِ ناخواسته فقط داخلِ جدول»
    /// باید برود. پس به‌جای اعتماد به یک فرمول، خودِ نتیجه سنجیده می‌شود.
    /// سقفِ اصلاح هست تا اگر روزی چیزِ دیگری نوار را زنده نگه داشت، جدول
    /// بی‌پایان بلند نشود.
    /// </summary>
    private void Settle()
    {
        var rows = RowCount();
        if (rows < 0 || rows > GrowRowLimit) return;
        if (_pad > 400) return;

        var vbar = this.GetVisualDescendants().OfType<ScrollBar>()
                       .FirstOrDefault(b => b.Orientation == Orientation.Vertical);
        if (vbar is null || !vbar.IsVisible || vbar.Maximum <= 1) return;

        _pad += vbar.Maximum + 2;
        _padRows = rows;
        InvalidateMeasure();
    }

    /// <summary>شمارِ ردیف‌ها — نامعلوم یعنی «محتاط باش و تنگنا بگذار».</summary>
    private int RowCount() => ItemsSource switch
    {
        null => 0,
        System.Collections.ICollection c => c.Count,
        _ => -1,
    };

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

        // ══ چرا حتی وقتی جای اضافه نیست هم پهنا سفت می‌شود ═══════════════════
        //
        // گزارشِ صاحب ریپو: «نباید Input هنگام تایپ بزرگ شود، نباید باعث تغییر
        // عرض ستون شود، نباید باعث شکستن خطوط جدول شود.»
        //
        // ریشه‌اش همین‌جا بود: ستونِ ‎Auto‎ی ‎DataGrid‎ هم‌قدِ پهن‌ترین محتوایش
        // می‌ماند و **با هر حرفی که تایپ می‌شود دوباره اندازه می‌گیرد**. پس در
        // جدول‌های پهن (که جای اضافه ندارند و تا امروز از همین‌جا برمی‌گشتیم)
        // ستون وسطِ تایپ پهن می‌شد و خطوطِ عمودیِ همهٔ ردیف‌ها جابه‌جا.
        //
        // حالا پهنا در هر دو حال سفت می‌شود: جای اضافه هست ⇒ ستاره‌ای به نسبتِ
        // محتوا (تا جدول تمامِ پهنا را بگیرد)؛ نیست ⇒ همان پهنای طبیعی، ثابت.
        // ظاهر عوض نمی‌شود — فقط دیگر با تایپ تکان نمی‌خورد. کشیدنِ دستیِ
        // ستون‌ها هم مثلِ قبل کار می‌کند.
        var spare = room - natural.Sum() >= 8;

        for (var i = 0; i < cols.Count; i++)
        {
            if (cols[i].MinWidth < natural[i]) cols[i].MinWidth = natural[i];
            cols[i].Width = spare
                ? new DataGridLength(natural[i], DataGridLengthUnitType.Star)
                : new DataGridLength(natural[i], DataGridLengthUnitType.Pixel);
        }

        _spread = true;
    }


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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_wired) return;
        _wired = true;
        // تنها منبعِ درستِ «الان در حال ویرایشیم» — خودِ جدول می‌گوید.
        PreparingCellForEdit += (_, _) => _editing = true;
        CellEditEnded += (_, _) => _editing = false;

        // ══ کلیک روی خانه، صفحه را نلغزاند ══════════════════════════════════
        //
        // گزارشِ صاحب ریپو: «وقتی در کادرِ جدول‌ها کلیک می‌کنم، آن سربرگ‌ها گم
        // می‌شوند … این مدل نباشد، هر وقت خواستم خودم اسکرول می‌کنم.»
        //
        // ریشه‌اش ‎RequestBringIntoView‎ است: با فوکوس گرفتنِ خانه، آوالونیا
        // درخواستِ «مرا در دید بیاور» را بالا می‌فرستد و اسکرولِ صفحه — همان
        // اسکرولِ واحدِ کلِ پنجره — صفحه را می‌کشد تا خانه وسط بیفتد. نتیجه‌اش
        // این است که سربرگ و نوارِ آمار از بالا می‌روند.
        //
        // ⚠️ درخواست فقط وقتی متوقف می‌شود که از **کلیک** آمده باشد. ناوبری با
        // کلید (‎ScrollIntoView‎ی خودِ جدول) باید کار کند، وگرنه با پایین رفتن
        // در یک جدولِ بلند، ردیفِ جاری از دید بیرون می‌ماند.
        AddHandler(RequestBringIntoViewEvent, (_, ev) =>
        {
            if (_pointerDriven) ev.Handled = true;
        }, RoutingStrategies.Bubble);
    }

    /// <summary>آخرین حرکت از ماوس بود، نه از صفحه‌کلید.</summary>
    private bool _pointerDriven;

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
    /// ستونی که ویرایشش کشویی یا رادیویی است — «نوع تیل»، «نوع» (قرض/مصرف)،
    /// «واحد» (تیل/پول) و مانندِ آن‌ها.
    /// </summary>
    private static bool IsToggleColumn(DataGridColumn? col) =>
        col is DataGridTemplateColumn t && t.CellEditingTemplate is not null;

    // ── جابه‌جاییِ خانه ───────────────────────────────────────────────────

    /// <summary>
    /// ستونِ جاری را ‎step‎ خانه جابه‌جا می‌کند (ترتیبِ دیداری).
    /// ‎extend‎ یعنی ‎Shift‎ گرفته شده: سرِ کادر سرِ جایش می‌ماند و فقط ته آن می‌رود.
    /// </summary>
    private bool MoveColumn(int step, bool extend = false)
    {
        var cols = VisibleCols();
        if (cols.Count == 0) return false;
        var cur = CurIndex(cols);
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

        OpenComboUnderPointer(e);

        // این کلیک نباید صفحه را بلغزاند — تا پایانِ همین رویداد علامت می‌ماند.
        _pointerDriven = true;
        try { base.OnPointerPressed(e); }
        finally { Dispatcher.UIThread.Post(() => _pointerDriven = false, DispatcherPriority.Input); }
        return;
    }

    /// <summary>
    /// ══ کشویی با یک کلیک باز شود ═══════════════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «نه این‌که یک بار بزنی تا آن کادر کشویی بشود، بعد بارِ
    /// بعد بزنی باز بشود، باز بعد بروی انتخاب کنی — همین که بزنم بیایند و با
    /// انتخاب زود عوض شوند.»
    ///
    /// ریشه‌اش خودِ ‎DataGrid‎ است: کلیکِ اول را برای «انتخابِ خانه» می‌خورد و
    /// تازه کلیکِ دوم به کشوییِ داخلِ خانه می‌رسد. پس همان کلیکِ اول را
    /// می‌گیریم و کشویی را باز می‌کنیم.
    ///
    /// ⚠️ ‎e.Handled‎ نمی‌شود: خانه باید مثلِ همیشه انتخاب هم بشود. فقط
    /// کشویی زودتر باز می‌شود.
    /// </summary>
    private static void OpenComboUnderPointer(PointerPressedEventArgs e)
    {
        if (e.Source is not Visual v) return;

        for (Visual? x = v; x is not null; x = x.GetVisualParent())
        {
            if (x is ComboBox cb)
            {
                if (!cb.IsDropDownOpen && cb.IsEffectivelyEnabled)
                {
                    // بعد از این‌که ‎DataGrid‎ کارِ خودش را کرد، وگرنه همان
                    // انتخابِ خانه دوباره کشویی را می‌بندد.
                    Dispatcher.UIThread.Post(() =>
                    {
                        cb.Focus();
                        cb.IsDropDownOpen = true;
                    }, DispatcherPriority.Input);
                }
                return;
            }
            if (x is DataGridRow) return;      // از خانه بیرون زدیم
        }
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
        var cols = VisibleCols();
        if (cols.Count == 0) return false;

        var cur = CurIndex(cols);
        var lo = _colAnchor < 0 ? cur : Math.Min(_colAnchor, _colHead);
        var hi = _colAnchor < 0 ? cur : Math.Max(_colAnchor, _colHead);
        if (lo < 0 || hi < 0) return false;
        lo = Math.Max(0, lo); hi = Math.Min(cols.Count - 1, hi);

        var rows = SelectedItems.Cast<object>().Where(o => o is not null).ToList();
        if (rows.Count == 0 && SelectedItem is not null) rows.Add(SelectedItem);
        if (rows.Count == 0) return false;

        var touched = false;
        foreach (var item in rows)
            for (var i = lo; i <= hi; i++)
            {
                if (cols[i].IsReadOnly) continue;
                var path = PathOf(cols[i]);
                if (string.IsNullOrEmpty(path)) continue;
                var p = item.GetType().GetProperty(path);
                if (p is null || !p.CanWrite || p.PropertyType != typeof(string)) continue;
                try { p.SetValue(item, ""); touched = true; } catch { }
            }
        return touched;
    }

    // ── خودِ کلیدها ───────────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

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
        if (_editing && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
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

            // ── چپ/راست: جهتِ دیداری، نه ایندکسِ منطقیِ ستون ───────────────
            // در چیدمانِ راست‌به‌چپ ستونِ «بعدی» سمتِ چپ است، پس کلیدِ چپ
            // ایندکس را جلو می‌برد. با ‎Shift‎ کادرِ چندانتخابی گسترده می‌شود.
            case Key.Left:
            case Key.Right:
            {
                var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
                var step = (e.Key == Key.Left) == rtl ? +1 : -1;
                MoveColumn(step, shift);
                e.Handled = true;
                return;
            }

            // ── بالا/پایین: خودِ جدول می‌بَرد (با ‎Shift‎ چندردیفی) ─────────
            // فقط کادرِ ستونی جمع می‌شود اگر ‎Shift‎ گرفته نشده باشد.
            case Key.Up:
            case Key.Down:
                if (!shift && _colAnchor >= 0) { ResetRange(CurIndex(VisibleCols())); PaintRange(); }
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

            case Key.F2 when !IsReadOnly:
                BeginEdit();
                e.Handled = true;
                return;

            case Key.Delete when !IsReadOnly && ClearSelectedCells():
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
