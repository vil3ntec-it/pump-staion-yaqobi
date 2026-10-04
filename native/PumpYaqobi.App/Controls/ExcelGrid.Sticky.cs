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

//  ⛔ شورا ج۴: بخشی از ‎ExcelGrid‎ — پنجرهٔ چسبان، چرخِ ماوس و دنبالِ خانه. فقط جابه‌جاییِ همان عضوها از ‎ExcelGrid.cs‎، بی تغییرِ یک رفتار.
public partial class ExcelGrid
{
    // ══════════════════════════════════════════════════════════════════════
    //  ══ پنجرهٔ چسبان — مجازی‌سازیِ واقعی، با اسکرولِ صفحه ═══════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «برنامه با زیاد شدنِ جدول خیلی کند
    //  می‌شود… اسکرول نرم نیست، گیر گیر دارد… برنامه بی‌نهایت هم باشد نباید
    //  افتی داشته باشد، مثلِ برنامه‌های حرفه‌ای.»
    //
    //  سنجشِ ‎bigtable‎ حق را به او داد، و ریشه را هم نشان داد — نه باز شدن
    //  کند بود و نه خودِ اسکرول:
    //
    //      ردیف   باز شدن   رشدِ کامل   ردیفِ زنده   حافظه
    //         50     69 ms      435 ms         50    157 MB
    //        200    118 ms    3,805 ms        200    294 MB
    //        600     16 ms    7,714 ms        600    650 MB
    //
    //  یعنی هر ردیفِ ساخته‌شده ~۱۲ میلی‌ثانیه CPU و نزدیک به یک مگابایت
    //  حافظه می‌برد، و رشدِ تدریجی **همهٔ** ردیف‌ها را می‌ساخت. با این قاعده
    //  هیچ ترفندی «ده سال داده» را نجات نمی‌دهد: هزینه با شمارِ ردیف بالا
    //  می‌رود، همین.
    //
    //  ⚠️ پس تنها راهِ درست این است که **شمارِ ردیفِ زنده از شمارِ ردیف‌های
    //  داده جدا شود**. راهِ رایجش (پنجره کردنِ خودِ ‎ItemsSource‎) شمارهٔ
    //  ردیف، انتخاب، ویرایش و کلیدها را می‌شکند، چون آن‌وقت ‎DataGrid‎
    //  فهرستِ کامل را نمی‌بیند. پس راهِ دیگری رفته‌ایم:
    //
    //      • ‎DataGrid‎ همان فهرستِ کامل را دارد (شماره، انتخاب، ویرایش،
    //        Enter و Tab همه دست‌نخورده) ولی **قابش یک صفحه است** — یعنی
    //        مجازی‌سازیِ خودش زنده است و فقط یک صفحه ردیف می‌سازد.
    //      • ولی جدول به اندازهٔ **همهٔ** ردیف‌هایش جا می‌گیرد، پس صفحه
    //        هم‌قدِ جدول بلند است و هیچ «کادرِ محدود»ی دیده نمی‌شود — همان
    //        چیزی که صاحب ریپو بارها خواسته.
    //      • و با لغزشِ صفحه، آن قابِ یک‌صفحه‌ای داخلِ جای خودش پایین
    //        می‌آید (‎_stickyY‎) و به همان اندازه ردیف‌های داخلش هم
    //        می‌لغزند (‎ScrollSlotsByHeight‎). نتیجه برای چشم دقیقاً یک
    //        جدولِ بلند است که با صفحه می‌لغزد؛ سربرگش هم بالای دید
    //        می‌ماند، مثلِ اکسل.
    //
    //  ⚠️ جابه‌جاییِ قاب با ‎RenderTransform‎ است نه با ‎Arrange‎: جابه‌جاییِ
    //  رندری هیچ پاسِ چیدمانی نمی‌خواهد، پس هر گامِ اسکرول فقط یک ترجمه است
    //  به‌علاوهٔ بازچرخانیِ چند ردیف.
    //
    //  ⚠️ ‎ScrollSlotsByHeight‎ درونیِ ‎DataGrid‎ است و با بازتاب صدا زده
    //  می‌شود. راهِ عمومی‌اش ‎ScrollIntoView‎ است که «ردیف را به دید بیاور»
    //  می‌گوید، نه «دقیقاً این‌قدر پیکسل بلغز» — و برای چسبیدنِ قاب به صفحه
    //  عددِ دقیق لازم است. اگر روزی این متد نبود، ‎_slots‎ خالی می‌ماند و
    //  جدول خودبه‌خود به رفتارِ پیشین (سقفِ یک صفحه با نوارِ خودش) برمی‌گردد
    //  — نه استثنا، نه صفحهٔ خراب.

    /// <summary>
    /// از این شمارِ ردیف به بالا، جدول پنجرهٔ چسبان می‌گیرد.
    ///
    /// ⚠️ چرا نه از ردیفِ اول: جدولِ کوتاه (کمتر از یک‌و‌نیم صفحه) ارزان است.
    ///
    /// ⛔ ۶۰ نه، ۳۰ (۱۴۰۵/۰۷/۱۶، گزارشِ «صرافی دیر باز می‌شود و لگ دارد»): ‎bigtable‎
    /// نشان داد جدولِ **۵۰ ردیفی کندترین** بود — ۴۴۰ms رشد و ۵۰ ردیفِ زنده، کندتر
    /// از جدولِ هزار ردیفی (۱۰۰ms) — چون زیرِ سقف همهٔ ردیف‌ها ساخته می‌شدند و دفترِ
    /// یک ماهِ معمولی درست همین‌قدر است. با ۳۰: همان جدول ۱۵۰ تا ۲۰۰ms و ۲۹ ردیف.
    /// ⚠️ ورق و پارچه (‎GrowsToContent‎) دست نخوردند.
    /// </summary>
    public const int StickyRowLimit = 30;

    /// <summary>الان با پنجرهٔ چسبان کار می‌کنیم؟ (سنجش‌ها می‌خوانند)</summary>
    public bool DiagSticky => _sticky;

    private bool _sticky;
    private double _windowH;
    private double _stickyY;
    private TranslateTransform? _slide;

    private bool WantsSticky(int rows, double screen) =>
        !GrowsToContent && rows > StickyRowLimit && Page is not null
        && _proc is not null && _barField is not null && MeasuredRowHeight() > 0;

    /// <summary>
    /// نوارِ بخش‌ها روی صفحه شناور است، پس بالای دید به اندازهٔ او کور است و
    /// قابِ چسبان باید از زیرِ او شروع شود — وگرنه سربرگِ جدول پشتِ نوار
    /// پنهان می‌ماند (با عکس دیده شد).
    /// </summary>
    /// ⚠️ خودِ نوار یک‌بار پیدا می‌شود و همان می‌ماند — مثلِ بقیهٔ کَش‌های
    /// بالا. این تابع از ‎MeasureSticky‎ صدا می‌خورد، یعنی در هر پاسِ چیدمانِ
    /// هر جدولِ چسبان؛ و گشتنِ **کلِ درختِ پنجره** در آن مسیر گران است:
    /// نمونه‌بردار (‎dotnet-trace‎ روی ‎enterperf‎) ۹۱۲ میلی‌ثانیه از وقتِ
    /// چیدمان را همین‌جا نشان داد. بلندیِ نوار هر بار از خودِ نوار خوانده
    /// می‌شود، پس عوض شدنِ اندازه‌اش هم دیده می‌شود.
    private Border? _navBar;

    private double BlindTop()
    {
        if (_navBar is { } cached && cached.GetVisualRoot() is not null) return Blind(cached);
        if (Page?.GetVisualRoot() is not Visual root) return 0;
        _navBar = root.GetVisualDescendants().OfType<Border>()
                      .FirstOrDefault(b => b.Name == "NavBar");
        return _navBar is { } nb ? Blind(nb) : 0;
    }

    /// <summary>
    /// ⛔ نوارِ **پنهان** جایی را کور نمی‌کند. صفحهٔ باز (تاریخچهٔ یک بخش، حسابِ
    /// قرض‌دار، …) نوارِ بخش‌ها را پنهان می‌کند، ولی ‎Bounds‎ِ کنترلِ پنهان همان
    /// عددِ آخرش می‌ماند — پس قابِ چسبان ۵۳ پیکسل پایین‌تر از بالای دید
    /// می‌نشست و بالای سرستون یک **نوارِ سفید** می‌ماند (عکسِ صاحب ریپو از
    /// تاریخچهٔ مصارف، ۱۴۰۵/۰۷/۱۲).
    /// </summary>
    private static double Blind(Control bar) => bar.IsVisible ? bar.Bounds.Height : 0;

    /// <summary>
    /// سرستون با اسکرول همراهِ دید بماند (مثلِ اکسل)؟ ‎false‎ ⇒ سرستون مثلِ یک
    /// جدولِ کاغذی بالای جدول می‌ماند و با صفحه بالا می‌رود.
    ///
    /// خواستهٔ صاحب ریپو برای تاریخچه‌ها (۱۴۰۵/۰۷/۱۲): «سرِ جدول‌ها توی اسکرول
    /// تا ته صفحه با من می‌آید.» ⚠️ مجازی‌سازی سرِ جایش است: قاب فقط به اندازهٔ
    /// بلندیِ سرستون بالاتر می‌نشیند (سرستون بیرونِ دید) و یک سرستون بلندتر است
    /// تا پایینِ دید خالی نماند. جای هر ردیف همان است که بود، چون ردیف‌ها به
    /// همان اندازه‌ای می‌لغزند که قاب.
    /// </summary>
    public static readonly StyledProperty<bool> HeaderFollowsProperty =
        AvaloniaProperty.Register<ExcelGrid, bool>(nameof(HeaderFollows), true);

    public bool HeaderFollows
    {
        get => GetValue(HeaderFollowsProperty);
        set => SetValue(HeaderFollowsProperty, value);
    }

    /// <summary>چقدر قاب بالاتر از بالای دید بنشیند — فقط وقتی سرستون همراه نیست.</summary>
    private double Lift() => HeaderFollows ? 0 : Math.Max(0, HeaderHeight());

    private double _blind;

    private Size MeasureSticky(Size availableSize, int rows, double screen)
    {
        var total = WantedHeight(rows);
        //  ⛔ یک‌و‌نیم صفحه، نه یک صفحه (۱۴۰۵/۰۷/۱۶): با سقفِ ۳۰، قابِ یک‌صفحه‌ای فقط
        //  ~۲۰ ردیف داشت و گامِ چرخ روی جدولِ هزار ردیفی هر از گاه ۳۰۰ تا ۴۵۰ms می‌شد
        //  (ردیفِ تازه ساخته می‌شد). نیم صفحهٔ اضافه زیرِ دید همان ذخیرهٔ بازچرخانی
        //  است — ‎bigtable‎ با آن دو بار سبز: بیشینهٔ گام ≤۱۱۷ms، درستیِ ترتیب ✅.
        _windowH = Math.Min(screen * 1.5 + Lift(), total);
        _blind = BlindTop();
        if (!_sticky)
        {
            _sticky = true;
            _stickyY = 0;
            Classes.Set("sticky", true);
        }
        HookPage();
        var size = base.MeasureOverride(availableSize.WithHeight(_windowH));
        return size.WithHeight(total).WithWidth(size.Width);
    }

    private void LeaveSticky()
    {
        if (!_sticky) return;
        _sticky = false;
        _stickyY = 0;
        Classes.Set("sticky", false);
        if (_slide is not null) _slide.Y = 0;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // ⚠️ قاب به اندازهٔ **یک صفحه** چیده می‌شود، هرچند جدول به اندازهٔ
        // همهٔ ردیف‌هایش جا گرفته باشد: همین است که مجازی‌سازیِ ‎DataGrid‎ را
        // زنده نگه می‌دارد.
        if (!_sticky) return base.ArrangeOverride(finalSize);
        base.ArrangeOverride(new Size(finalSize.Width, _windowH));
        ApplySlide();
        return finalSize;
    }

    private void ApplySlide()
    {
        if (this.GetVisualChildren().OfType<Control>().FirstOrDefault() is not { } child) return;
        if (_slide is null || !ReferenceEquals(child.RenderTransform, _slide))
        {
            _slide = new TranslateTransform();
            child.RenderTransformOrigin = RelativePoint.TopLeft;
            child.RenderTransform = _slide;
        }
        if (Math.Abs(_slide.Y - _stickyY) > 0.01) _slide.Y = _stickyY;
    }

    /// <summary>
    /// قاب را با صفحه هم‌گام می‌کند: هرقدر بالای جدول از دید بیرون رفته،
    /// همان‌قدر قاب پایین می‌آید و همان‌قدر ردیف‌ها می‌لغزند.
    /// </summary>
    private void SyncSticky()
    {
        if (!_sticky || Page is not { } page) return;
        if (this.TranslatePoint(new Point(0, 0), page) is not { } at) return;

        var span = Math.Max(0, Bounds.Height - _windowH);
        var bar = InnerBar();
        var want = Math.Clamp(_blind - at.Y - Lift(), 0, span);
        // ⚠️ سقفِ واقعی را خودِ جدول می‌گوید: اگر بلندیِ حساب‌شدهٔ ما یکی-دو
        // پیکسل با شمارشِ خودِ جدول فرق داشته باشد، بی این، آخرین گام همیشه
        // «هنوز نرسیده» می‌ماند.
        if (bar is not null) want = Math.Min(want, bar.Maximum);

        if (Math.Abs(want - _stickyY) > 0.5)
        {
            _stickyY = want;
            ApplySlide();
        }
        ScrollTo(want);
    }

    // ══ دو درِ درونیِ ‎DataGrid‎ ═══════════════════════════════════════════════
    //
    //  ⚠️ **راهِ خودِ جدول** را می‌رویم، نه یک میان‌بُر: نوارِ لغزشِ درونی را
    //  می‌گذاریم و ‎ProcessVerticalScroll‎ را صدا می‌زنیم — دقیقاً همان دو
    //  خطی که ‎VerticalScrollBar_Scroll‎ی خودِ ‎DataGrid‎ انجام می‌دهد. جدول
    //  آن‌وقت در پاسِ چیدمانِ بعدیِ **خودش** می‌لغزد.
    //
    //  یک بار میان‌بُر زدیم (‎ScrollSlotsByHeight‎ی مستقیم) و عکس‌ها ریشه را
    //  نشان دادند: با پرشِ بزرگ، ردیف‌ها وارونه چیده می‌شدند (۳۰۷، ۳۰۶، ۳۰۵…)
    //  و یک ردیفِ کهنه ته جدول می‌ماند، چون لغزاندن بیرون از پاسِ چیدمانِ
    //  جدول انجام می‌شد. قاعده: با حالتِ درونیِ یک کنترل، از درِ خودش وارد شو.
    private static readonly System.Reflection.FieldInfo? _barField =
        typeof(DataGrid).GetField("_vScrollBar",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    private static readonly System.Reflection.MethodInfo? _proc =
        typeof(DataGrid).GetMethod("ProcessVerticalScroll",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(ScrollEventType) }, null);

    private ScrollBar? InnerBar() => _barField?.GetValue(this) as ScrollBar;

    private void ScrollTo(double want)
    {
        if (_proc is null || InnerBar() is not { } bar) return;
        var v = Math.Clamp(want, bar.Minimum, bar.Maximum);
        if (Math.Abs(bar.Value - v) < 0.5) return;
        bar.Value = v;
        try { _proc.Invoke(this, new object[] { ScrollEventType.ThumbTrack }); }
        catch { /* جدول هنوز آمادهٔ لغزیدن نیست — فریمِ بعد */ }
    }

    /// <summary>اندازه‌گیری با تنگنای «یک صفحه» — همان‌جا که مجازی‌سازی زنده است.</summary>
    private Size Capped(Size availableSize, double screen)
    {
        if (availableSize.Height > screen) availableSize = availableSize.WithHeight(screen);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>بلندیِ واقعیِ جدول برای این شمارِ ردیف.</summary>
    /// <summary>
    /// بلندیِ واقعیِ یک ردیف — از خودِ ردیفِ ساخته‌شده، نه از ‎RowHeight‎.
    /// ⚠️ ‎RowHeight‎ ۴۴ است ولی ردیفِ چیده‌شده ۴۵ پیکسل است (خطِ زیرش). با ۴۴
    /// هر ردیف یک پیکسل کم می‌آمد، جدولِ ۶۱ ردیفی ۶۱ پیکسل کوتاه می‌شد، نوارِ
    /// لغزشِ خودش پیدا می‌شد و ‎Settle‎ به جبرانش می‌افتاد — همان چیزی که یک
    /// بار یک صفحهٔ خالی زیرِ جدول گذاشت.
    /// </summary>
    private double MeasuredRowHeight()
    {
        if (_rowH > 0) return _rowH;
        var row = this.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.Bounds.Height > 0);
        if (row is not null) _rowH = row.Bounds.Height;
        return _rowH > 0 ? _rowH : (double.IsNaN(RowHeight) || RowHeight <= 0 ? 45d : RowHeight + 1);
    }

    private double _rowH;

    private double WantedHeight(int rows)
    {
        var rowH = MeasuredRowHeight();

        // ⚠️ از کَش، نه از گشتنِ درخت. این تابع در هر پاسِ چیدمان صدا زده
        // می‌شود (هم از ‎MeasureOverride‎ هم از ‎Settle‎)، و گشتنِ کلِ درخت در
        // هر پاس یعنی هزاران بازدیدِ بی‌فایده. اندازه‌گیری: باز کردنِ یک ورق
        // ۴٬۳۲۴ میلی‌ثانیه بود در حالی که خواندنش از دیتابیس فقط ۳ میلی‌ثانیه.
        var head = 0d;
        if (HeadersVisibility != DataGridHeadersVisibility.None)
        {
            head = HeaderHeight();
            if (head <= 0) head = rowH;                    // هنوز چیده نشده
        }

        var bar = 0d;
        if (HScrollBar is { IsVisible: true } hbar) bar = Math.Max(hbar.Bounds.Height, 12d);

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
        // ⚠️ در حالتِ چسبان نوارِ لغزشِ جدول همیشه چیزی برای لغزاندن دارد
        // (قاب عمداً یک صفحه است)، پس این اصلاحیه بی‌معنی و بی‌پایان می‌شد.
        if (_sticky) return;
        if (rows < 0 || rows > GrowRowLimit) return;

        // ⚠️ فقط وقتی جدول واقعاً هم‌قدِ همهٔ ردیف‌هایش شده. پیش از آن (پاسِ
        // تنگِ اول، یا وسطِ رشدِ تدریجی) نوارِ لغزش به‌عمد پیداست و «جای
        // لغزش»ش هزار پیکسل است — یک بار همان هزار پیکسل به بلندی اضافه شد و
        // زیرِ جدول یک صفحهٔ خالی ماند (گزارشِ صاحب ریپو با عکس).
        if (!_spread || _shown < rows) return;

        if (VerticalBar is not { IsVisible: true } vbar || vbar.Maximum <= 1) return;

        // سقفِ اصلاح: چند پیکسلِ گردکردن، نه بیشتر. اگر بیش از این کم آمده،
        // حسابِ بلندی غلط است و باید همان درست شود، نه با پُر کردن پنهان.
        var add = Math.Min(vbar.Maximum + 2, MaxPad - _pad);
        if (add <= 0) return;

        DiagSettle++;
        _pad += add;
        _padRows = rows;
        InvalidateMeasure();
    }

    /// <summary>بیشترین اصلاحیهٔ بلندی که ‎Settle‎ حق دارد بدهد.</summary>
    private const int MaxPad = 24;

    // ══ کَشِ اجزای قالب ═══════════════════════════════════════════════════════
    //
    // ⚠️ اینها در هر پاسِ چیدمان لازم‌اند، پس **نباید** هر بار با گشتنِ درخت
    // پیدا شوند. با ۸۰ ردیف و ۱۰ کنترل در هر ردیف، هر گشت چند هزار بازدید
    // است — و ‎Settle‎ هم ‎InvalidateMeasure‎ می‌زند، یعنی پاسِ بعدی و گشتِ
    // بعدی. همان حلقه‌ای که باز کردنِ ورق را ۴٫۳ ثانیه کرده بود.
    //
    // با عوض شدنِ قالب یا ردیف‌ها، کَش خودش باطل می‌شود (ریشهٔ بصری عوض شده).

    private ScrollBar? _hbar;
    private double _headH;

    private ScrollBar? HScrollBar
    {
        get
        {
            if (_hbar is { } b && b.GetVisualRoot() is not null) return b;
            return _hbar = this.GetVisualDescendants().OfType<ScrollBar>()
                               .FirstOrDefault(x => x.Orientation == Orientation.Horizontal);
        }
    }

    /// <summary>بلندیِ سرستون — یک بار اندازه گرفته می‌شود و همان می‌ماند.</summary>
    private double HeaderHeight()
    {
        if (_headH > 0) return _headH;
        foreach (var h in this.GetVisualDescendants().OfType<DataGridColumnHeader>())
            _headH = Math.Max(_headH, h.Bounds.Height);
        return _headH;
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
            // ⚠️ در حالتِ چسبان جدول خودش نمی‌لغزد؛ صفحه می‌لغزد و قابِ جدول
            // با آن می‌آید. پس چرخ همیشه مالِ صفحه است.
            var gridTop = _sticky || bar is null || bar.Value <= 0.5;

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

            // ⚠️ در حالتِ چسبان، چرخ **هرگز** به خودِ جدول نمی‌رسد — حتی وقتی
            // صفحه دیگر جا ندارد. جای ردیف‌های قابِ چسبان را فقط ‎SyncSticky‎
            // می‌نویسد (از روی لغزشِ صفحه)؛ اگر ‎DataGrid‎ خودش هم بلغزاند،
            // یک فریم ردیف‌ها می‌پرند و فریمِ بعد ‎SyncSticky‎ برشان می‌گرداند
            // — همان «به آخر که می‌رسد پرپر می‌شود و بعد درست می‌شود»ی
            // گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷). سنجه: ‎scrollend‎.
            if (_sticky) { e.Handled = true; return; }
        }

        base.OnPointerWheelChanged(e);
    }

    /// <summary>یک چرخِ ماوس چند پیکسل صفحه را می‌برد.</summary>
    private const double WheelStep = 58;

    /// <summary>
    /// ══ صفحه دنبالِ خانهٔ جاری بیاید — ولی فقط با کلید ═══════════════════════
    ///
    /// گزارشِ صاحب ریپو: «وقتی با کلیدها به سقف یا کفِ صفحه می‌رسم، اسکرول
    /// نمی‌شود. این را هم درست کن، مثلِ اکسل.»
    ///
    /// حق داشت، و ریشه‌اش یک قاعدهٔ خودمان بود: ‎RequestBringIntoView‎ بالاتر
    /// **همیشه** بسته می‌شود. آن برای کلیک درست است (کلیک نباید کلِ صفحه را
    /// بکشد و سربرگ را از بالا ببرد)، ولی همان قاعده جلوی دنبال کردنِ کلید را
    /// هم می‌گرفت.
    ///
    /// پس به‌جای تکیه بر آن رویداد، خودمان کم‌ترین لغزشِ لازم را می‌دهیم:
    ///   • خانه بالای دید افتاده ⇒ فقط تا زیرِ نوارِ چسبانِ بخش‌ها بیا بالا
    ///   • خانه پایینِ دید افتاده ⇒ فقط تا لبهٔ پایین بیا پایین
    ///   • داخلِ دید است ⇒ هیچ. (وگرنه هر فلش صفحه را می‌پراند.)
    /// </summary>
    private void FollowCell()
    {
        if (Page is not { } page) return;

        var cell = this.GetVisualDescendants().OfType<DataGridCell>()
                       .FirstOrDefault(c => c.IsVisible && c.Bounds.Height > 0
                                         && ReferenceEquals(ColumnOfCell(c), CurrentColumn)
                                         && c.DataContext is not null
                                         && ReferenceEquals(c.DataContext, SelectedItem));
        if (cell is null) return;
        if (cell.TranslatePoint(new Point(0, 0), page) is not { } at) return;

        // نوارِ بخش‌ها روی صفحه شناور است، پس بالای دید به اندازهٔ او کور است.
        var blind = page.GetVisualRoot() is Visual root
            ? root.GetVisualDescendants().OfType<Border>()
                  .FirstOrDefault(b => b.Name == "NavBar")?.Bounds.Height ?? 0
            : 0;

        var top = at.Y;
        var bottom = at.Y + cell.Bounds.Height;
        var max = Math.Max(0, page.Extent.Height - page.Viewport.Height);

        double delta;
        if (top < blind) delta = top - blind;                       // برو بالا
        else if (bottom > page.Viewport.Height) delta = bottom - page.Viewport.Height;
        else return;                                                // همین‌جا پیداست

        page.Offset = new Vector(page.Offset.X,
                                 Math.Clamp(page.Offset.Y + delta, 0, max));
    }
}
