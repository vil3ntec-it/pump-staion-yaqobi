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
using PumpYaqobi.App.Services;

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

    /// <summary>
    /// ══ این جدول هم‌قدِ ردیف‌هایش بلند شود؟ ═══════════════════════════════
    ///
    /// پیش‌فرض <b>نه</b>، و این پیش‌فرض را با عدد انتخاب کرده‌ایم نه با سلیقه.
    /// جدولی که هم‌قدِ ردیف‌هایش بلند می‌شود، ‎DataGrid‎ هر ردیفش را واقعاً
    /// می‌سازد؛ و ساختنِ ردیف گران است (‎ledgerperf‎):
    ///
    ///     گاوصندوق با ۸۰ ردیفِ آزاد  → ۱٬۹۳۳ ms   (هر ردیف دو کشویی دارد)
    ///     صرافی    با ۸۰ ردیفِ آزاد  → ۱٬۱۴۲ ms
    ///     مصارف    با ۸۰ ردیفِ آزاد  →   ۳۸۴ ms   (بی هیچ کشویی)
    ///     همان‌ها وقتی مجازی‌سازی می‌کنند → ۱۲ تا ۳۸ ms
    ///
    /// پس دفترهای ماهانه (گاوصندوق، صرافی، مصارف، چکنه…) سرِ یک صفحه
    /// می‌ایستند و می‌لغزند — چرخِ ماوس زنجیره‌ای است، پس کاربر یک اسکرولِ
    /// پیوسته حس می‌کند.
    ///
    /// ⚠️ و کجا <b>روشن</b> می‌شود: ورق و پارچه. خواستهٔ صریحِ صاحب ریپو
    /// دربارهٔ همان‌ها بود («شماره‌های ۴۰ و ۵۰ و ۵۱ نباید داخلِ یک کادرِ
    /// محدود گیر کنند») و آن‌ها ذاتاً کوتاه‌اند، پس رشدشان ارزان است.
    /// ‎GrowRowLimit‎ آن‌جا هم سقفِ ایمنی است.
    /// </summary>
    public static readonly StyledProperty<bool> GrowsToContentProperty =
        AvaloniaProperty.Register<ExcelGrid, bool>(nameof(GrowsToContent));

    public bool GrowsToContent
    {
        get => GetValue(GrowsToContentProperty);
        set => SetValue(GrowsToContentProperty, value);
    }

    /// <summary>
    /// ══ پهنای ستون‌ها یک بار تنظیم شود، همه‌جا بماند ════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «وقتی جدولِ یک ورق را تنظیم می‌کنم، تمامِ جدول‌های
    /// همهٔ ورق‌ها برابر بشوند… نمی‌شود که هر روز من اندازه‌ها را درست کنم.»
    ///
    /// جدولی که این کلید را داشته باشد، پهنای دستیِ کاربر را در تنظیماتِ
    /// برنامه می‌گذارد و هر جدولِ دیگری با همان کلید — ورقِ فردا هم — همان را
    /// برمی‌دارد. خالی یعنی «یادت نماند»، پیش‌فرضِ همهٔ جدول‌های دیگر.
    ///
    /// ⚠️ کلید باید به **ستون‌ها** بسته باشد نه به دادهٔ ردیف‌ها: دو جدولِ
    /// تراکنشِ ورق ستون‌های یکسان دارند و عمداً یک کلید می‌گیرند، تا چپ و
    /// راست هم‌اندازه بمانند.
    /// </summary>
    public static readonly StyledProperty<string?> WidthKeyProperty =
        AvaloniaProperty.Register<ExcelGrid, string?>(nameof(WidthKey));

    public string? WidthKey
    {
        get => GetValue(WidthKeyProperty);
        set => SetValue(WidthKeyProperty, value);
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

        // ══ اگر ستون‌ها از قاب پهن‌تر شدند، افقی بلغزد ═══════════════════════
        // خواستهٔ صاحب ریپو: «اگه زیاد بزرگ شد به چپ و راست هم اسکرول بشه.»
        // ⚠️ عمودی همچنان ‎Disabled‎ می‌ماند: قاعدهٔ برنامه این است که فقط
        // **صفحه** عمودی می‌لغزد، نه داخلِ جدول (سنجشِ ‎scroll‎ همین را قفل
        // کرده). افقی ربطی به آن ندارد.
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;

        // جای اضافه بینِ ستون‌ها پخش می‌شود، نه در یک ستونِ خالیِ ته جدول
        // ⚠️ ‎LayoutUpdated‎ برای هر چیدمانِ هر جای پنجره شلیک می‌شود، و همهٔ
        // بخش‌ها با هم در درخت می‌مانند؛ جدولِ بخشِ پنهان با هر چرخِ ماوس در
        // بخشِ دیگر نباید کاری کند.
        LayoutUpdated += (_, _) =>
        {
            if (!IsEffectivelyVisible) return;
            SpreadColumns(); PinOnUserResize(); RememberWidths(); Settle(); SyncSticky();
        };

        // ══ دوبار-کلیک روی خطِ ستون = هم‌قدِ محتوا، مثلِ اکسل ═════════════════
        // خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «روی اندازه کردنِ جدول‌ها یک سیستم
        // بگذار مثلِ اکسل که دوبار روی خطِ همان جدول بزنی، خودکار اندازه‌اش
        // ⚠️ ‎Bubble‎، نه ‎Tunnel‎: ‎DoubleTapped‎ی آوالونیا رویدادِ حبابی است و
        // شنوندهٔ تونلی هرگز شلیک نمی‌شود.
        AddHandler(Gestures.DoubleTappedEvent, OnHeaderLineDoubleTap, RoutingStrategies.Bubble);

        // ردیفِ قفل‌شده (‎ILockedRow‎ — مثلِ ردیفِ 📦 خریدِ مخزن در حسابِ شرکت)
        // ویرایشگر باز نمی‌کند؛ همان ‎readonly‎ی سایت
        BeginningEdit += (_, e) =>
        {
            if (e.Row?.DataContext is ViewModels.ILockedRow { IsLocked: true }) { e.Cancel = true; return; }
            SnapEdit(e.Column, e.Row);
        };
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
    /// ══ تا این شمارِ ردیف، جدول هم‌قدِ ردیف‌هایش بلند می‌شود ═════════════════
    ///
    /// بالاتر از این، سرِ یک صفحه می‌ایستد و مجازی‌سازیِ ‎DataGrid‎ برمی‌گردد.
    ///
    /// ⚠️ چرا شمارِ **ردیف** و نه پیکسل: هزینه به شمارِ ردیف‌هایی است که
    /// واقعاً ساخته می‌شوند، نه به بلندی. یک‌بار تنگنا را «یک صفحه» گذاشتیم و
    /// جدولِ ورق با ۲۵ ردیف هم به آن خورد (۳۶۶ ⇒ ۶۴۰ پیکسل به‌جای ۱٬۱۰۰) —
    /// یعنی همان کادری که صاحب ریپو از آن شکایت داشت، فقط بزرگ‌تر.
    ///
    /// ⚠️ چرا از ۸۰ به ۲۵۰ رفت: خواستهٔ تازهٔ صاحب ریپو صریح بود — «ورق جوری
    /// باشد که سقفِ پایینی نداشته باشد و تا ۱۰۰ تا کادر را به‌راحتی باز کند».
    /// با ۸۰، ورقِ صدردیفی دقیقاً همان کادرِ محدودی می‌شد که از آن شکایت
    /// داشت (‎rowcost‎ همین را گرفت: با ۱۰۰ ردیف فقط ۱۹ ردیف زنده می‌ماند).
    ///
    /// و چرا حالا از پسش برمی‌آید: ریشهٔ کندی شمارِ ردیف نبود، **ساختنِ
    /// دوبارهٔ کلِ صفحه** بود. با زنده ماندنِ ‎WaraqPageViewModel‎ باز کردنِ
    /// ورقِ ۸۰ ردیفی از ۱٬۵۷۱ به ۸۴ میلی‌ثانیه رسید — پس جا برای ردیفِ
    /// بیشتر باز شد.
    ///
    /// ⚠️ ولی سقف حذف نشد و نباید بشود: بی آن، یک دفترِ صدهزارردیفی همهٔ
    /// ردیف‌هایش را واقعاً می‌سازد و برنامه قفل می‌شود (‎gridperf‎ همین را با
    /// یک میلیون ردیف می‌سنجد). این عدد فقط آن‌قدر بالا رفت که هیچ ورقِ
    /// واقعی به آن نرسد.
    /// </summary>
    public const int GrowRowLimit = 600;

    /// <summary>
    /// ══ دفترهای ماهانه: سقف به **ردیف** است، نه به «یک صفحه» ════════════════
    ///
    /// گزارشِ صاحب ریپو، دو بار: «اون سقفِ زیرینِ جدول‌ها هم هستند، گفتم اون‌ها
    /// هم نباشند» و «سقفِ زیری نباشد که جمله سرِ جا بماند و جدول از زیرشان
    /// کم‌کم بیاید».
    ///
    /// حق داشت. قاعدهٔ قبلی «تا یک صفحه بلند شو» بود، و یک صفحه یعنی حدودِ
    /// **هفده** ردیف — یعنی دفترِ بیست‌ردیفی هم داخلِ کادر گیر می‌کرد و
    /// بقیه‌اش از زیرِ جمله‌ها رد می‌شد. همان چیزی که از آن شکایت داشت.
    ///
    /// ⚠️ ولی سقف کلاً هم نمی‌تواند برود، و این با عدد ثابت شد
    /// (‎ledgerperf‎، پس از سبک شدنِ ردیف‌ها):
    ///
    ///     ۸۰ ردیف  →   ۱۱۲ ms      ← بی سقف، و روان
    ///     ۲۰۰ ردیف → ۱٬۴۷۴ ms      ← دیگر نه
    ///
    /// پس سقف ماند ولی جایش عوض شد: دیگر «یک صفحه» نیست، **صد ردیف** است.
    /// هیچ ماهِ واقعیِ این پمپ به صد ردیف نمی‌رسد، پس عملاً سقفی دیده
    /// نمی‌شود؛ و اگر روزی رسید، برنامه به‌جای قفل شدن همان‌جا می‌ایستد.
    /// </summary>
    /// <summary>دیگر سقفِ «یک صفحه» نیست — رشدِ تدریجی جایش را گرفت (پایین). عدد برای سنجش‌های قدیمی مانده.</summary>
    public const int PageRowLimit = 100;

    // ══════════════════════════════════════════════════════════════════════
    //  ⚠️ چرا تنگنا هست — با عدد، نه با حدس
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو: «بخشِ صرافی و مصارف و گاوصندوق خیلی دیر باز می‌شوند.»
    //
    //  ‎LedgerPerf‎ اندازه گرفت و نتیجه وارونهٔ انتظار بود — هرچه ردیف
    //  **کمتر**، کندتر:
    //
    //      بخش        ردیف   خواندنِ SQL   باز شدن    ردیفِ زنده   بلندیِ جدول
    //      گاوصندوق     200         3 ms   4,652 ms         200      9,051px
    //      صرافی        200         1 ms   2,728 ms         200      9,051px
    //      مصارف        200         1 ms   1,376 ms         200      9,051px
    //      گاوصندوق   3,000        18 ms     916 ms          17        800px
    //
    //  دیتابیس بی‌گناه بود (یک تا پنجاه‌ونه میلی‌ثانیه). ریشه این بود که با
    //  مرزِ ۶۰۰ ردیفِ قبلی، جدولِ ۲۰۰ ردیفی تا ۹٬۰۵۱ پیکسل بلند می‌شد، و
    //  ‎DataGrid‎ی آوالونیا مجازی‌سازی‌اش را از **قابِ خودش** می‌گیرد: قابِ
    //  ۹٬۰۵۱ پیکسلی یعنی هر ۲۰۰ ردیف و همهٔ کشویی‌هایشان واقعاً ساخته
    //  می‌شوند. بالای آن مرز، جدول به یک صفحه تنگ می‌شد و ناگهان ۱۷ ردیف
    //  می‌ساخت و صد برابر سریع‌تر بود.
    //
    //  ══ و یک هزینهٔ دوم که از چشم افتاده بود ═══════════════════════════════
    //
    //  ستون‌های ‎Width="Auto"‎ی همهٔ این جدول‌ها پهنایشان را از **محتوای
    //  ردیف‌ها** می‌گیرند. تا وقتی پهنا سفت نشده، هر ردیفِ تازه‌ای که ساخته
    //  می‌شود همهٔ ستون‌ها را دوباره به اندازه‌گیری می‌اندازد — یعنی هزینه با
    //  شمارِ ردیف‌ها **مربعی** بالا می‌رود، نه خطی. برای همین مصارف هم که
    //  هیچ کشویی‌ای ندارد، با ۲۰۰ ردیف ۱٫۴ ثانیه می‌گرفت.
    //
    //  پس پاسِ **اولِ** اندازه‌گیری همیشه تنگ است (یک صفحه): ستون‌های ‎Auto‎
    //  فقط یک صفحه ردیف را می‌بینند، ‎SpreadColumns‎ پهناها را سفت می‌کند، و
    //  از پاسِ بعد جدول آزاد می‌شود و هم‌قدِ ردیف‌هایش بلند می‌گردد — این بار
    //  بی هیچ اندازه‌گیریِ دوبارهٔ ستون.

    /// <summary>
    /// بلندیِ یک «صفحه» — قابِ اسکرولِ صفحه، و اگر هنوز معلوم نیست، یک عددِ
    /// محافظه‌کار. ⚠️ «نمی‌دانم» هرگز یعنی «بی‌کران» نیست.
    /// </summary>
    private double ScreenHeight()
    {
        var h = Page?.Viewport.Height ?? 0;
        return h > 0 ? h : 900;
    }

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
    /// <summary>
    /// شمارنده‌های سنجش — ‎waraqperf‎ رویشان حساب می‌کند. بی این‌ها یک بار
    /// دو ساعت دنبالِ «چرا دوازده پاسِ چیدمان» گشتیم، در حالی که خودِ جدول
    /// فقط دو بار اندازه گرفته می‌شد.
    /// </summary>
    public static int DiagMeasure, DiagSettle;
    /// <summary>گران‌ترین پاسِ اندازه‌گیری‌ای که ردیفِ تازه ساخت، و چند ردیف — برای ‎scrollperf‎.</summary>
    public static long DiagChunkMaxMs;
    public static int DiagChunkMaxRows;
    private int _measuredShown;

    /// <summary>اصلاحیهٔ بلندی — فقط برای سنجش.</summary>
    public double DiagPad => _pad;
    public int DiagShown => _shown;
    public bool DiagQueued => _growQueued;

    // ══════════════════════════════════════════════════════════════════════
    //  رشدِ تدریجی — «کلِ صفحه به اندازهٔ جدول بزرگ شود، ولی لگ نزند»
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ چندبارهٔ صاحب ریپو: «سقفِ زیرِ جدول نباشد که جمله سرِ جا بماند و
    //  جدول‌های دیگر از زیرش رد شوند… تمام صفحه به اندازهٔ همان جدول بزرگ
    //  شود… مثلِ اکسل موقعِ اسکرول جدول‌های زیر هم رندر شوند و لگ نزند و
    //  کامپیوتر را داغ نکند.»
    //
    //  پس دیگر هیچ جدولی سرِ «یک صفحه» نمی‌ایستد (‎PageRowLimit‎ رفت). ولی
    //  ساختنِ همهٔ ردیف‌ها در یک پاسِ چیدمان همان چیزی است که باز شدنِ بخش را
    //  کند می‌کرد (۲۰۰ ردیف ⇒ ۱٫۴ ثانیه). راهِ میانه: **رشدِ تدریجی**.
    //
    //      پاسِ اول:  فقط یک صفحه ردیف (ستون‌ها سفت می‌شوند)
    //      هر فریم:  ‎GrowChunk‎ ردیفِ دیگر، با اولویتِ پس‌زمینه
    //      …تا همهٔ ردیف‌ها ساخته شوند
    //
    //  کاربر همان لحظه یک صفحهٔ کامل می‌بیند و می‌تواند کار کند؛ بقیهٔ جدول
    //  در چند فریمِ بعد زیرِ آن ساخته می‌شود — پیش از آن‌که با اسکرول به آن
    //  برسد. هیچ فریمی بیش از یک تکه کار نمی‌کند، پس رابط نمی‌پرد.
    //
    //  ⚠️ سقفِ ایمنی (‎GrowRowLimit‎) می‌ماند: بی مجازی‌سازی، یک دفترِ
    //  صدهزارردیفی حافظه را می‌بلعد. آن‌قدر بالاست که هیچ دفترِ ماهانه به آن
    //  نمی‌رسد؛ اگر رسید، جدول همان‌جا با نوارِ خودش می‌لغزد.

    /// <summary>
    /// چند ردیف در هر فریمِ رشد ساخته می‌شود.
    ///
    /// ⚠️ کوچک و ثابت — سنجشِ ‎scrollperf‎ (۱۴۰۵/۰۶/۲۶): ساختنِ هر ردیف ۱۰ تا ۲۵
    /// میلی‌ثانیه است (قالب، سبک‌ها، درختِ ترکیب‌گر)، پس تکهٔ ۱۲۰ ردیفیِ پیشین
    /// یک مکثِ ۱٫۳ ثانیه‌ای وسطِ اسکرول بود — همان «کادرها دیر می‌آیند و لگ
    /// می‌زند». «تکهٔ بزرگ‌تر ارزان‌تر است» که پیش‌تر اندازه گرفته شده بود، مالِ
    /// وقتی بود که هر پاسِ چیدمان با ‎TotalsStrip‎ کلِ درختِ جدول را می‌گشت؛
    /// با کَش شدنِ آن، پاسِ بی‌ردیفِ تازه ارزان است و تکهٔ کوچک دیگر مربعی نمی‌شود
    /// (‎ledgerperf‎: رشدِ کاملِ ۲۰۰ ردیف ۳۶۰ تا ۶۰۰ میلی‌ثانیه).
    /// </summary>
    private const int GrowChunk = 8;

    /// <summary>بیشترین ردیفی که یک فریمِ رشد می‌سازد — سقفِ مکثِ یک فریم.</summary>
    private const int GrowMax = 8;

    /// <summary>
    /// همان سقف برای جدولِ <see cref="GrowsToContent"/> — ورق و پارچه.
    /// آن‌ها یک صفحه‌اند و با هم باز و بسته می‌شوند، پس یک مکثِ کوتاه بهتر از
    /// ده پاسِ پس‌زمینه است.
    /// </summary>
    private const int GrowTall = 40;

    /// <summary>تا این‌جا بلند شده‌ایم (شمارِ ردیف). صفر یعنی هنوز شروع نشده.</summary>
    private int _shown;

    private bool _growQueued;

    /// <summary>ردیف‌هایی که یک صفحه جا می‌گیرند — پاسِ اولِ رشد.</summary>
    private int FirstChunk(double screen)
    {
        var rowH = MeasuredRowHeight();
        return Math.Max(GrowChunk / 2, (int)Math.Ceiling(screen / rowH) + 2);
    }

    // ══ شمارِ ردیف‌ها عوض شد ⇒ دوباره اندازه بگیر ═══════════════════════════
    // ⚠️ ‎DataGrid‎ با اضافه/کم شدنِ ردیف خودش را دوباره اندازه **نمی‌گیرد**؛
    // فقط گسترهٔ لغزشِ درونی‌اش را به‌روز می‌کند. پیش از این رشدِ جدول پس از
    // «➕ ردیف» فقط از راهِ ‎Settle‎ رخ می‌داد (نوارِ لغزش پیدا می‌شد و بلندی
    // پُر می‌شد) — همان راهی که صفحهٔ خالی می‌ساخت. حالا خودِ فهرست خبر می‌دهد.
    private System.Collections.Specialized.INotifyCollectionChanged? _watched;

    // ══════════════════════════════════════════════════════════════════════
    //  بخشی که دیده نمی‌شود، ردیفِ زنده هم ندارد
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «اگر توی بخش نیستم، آن بخش فعال
    //  نباشد و هیچ مصرفی نداشته باشد — حتی یک درصد… توی یک حساب یا حسابِ شرکت
    //  یا ورقی هستم، نباید ورق‌های دیگر و حساب‌های دیگر داده‌ای مصرف کنند.»
    //
    //  سنجشِ ‎idle‎ نشان داد پرس‌وجویی در کار نیست (بی‌کاریِ هر بخش صفر دستور)،
    //  ولی **ردیف‌های ساخته‌شده** می‌مانند: هر بخشی که یک بار باز شده بود،
    //  ردیف‌هایش تا همیشه در درختِ بصری زنده بودند — پس از یک گشتِ ساده در
    //  برنامه ۲۴۵ ردیفِ نامرئی روی حافظه و روی هر بی‌اعتبارسازیِ بزرگ (تعویضِ
    //  تم) کار می‌ماند.
    //
    //  حالا جدولی که نامرئی می‌شود فهرستش را «پارک» می‌کند: ‎ItemsSource‎ برداشته
    //  می‌شود، ‎DataGrid‎ همهٔ ردیف‌هایش را دور می‌ریزد، و با دیده شدنِ دوباره
    //  همان فهرست برمی‌گردد و از نو — تدریجی — ساخته می‌شود.
    //
    //  ⚠️ با ‎SetCurrentValue‎، نه با ‎SetValue‎: اتصالِ ‎{Binding Rows}‎ سرِ جایش
    //  می‌ماند، پس اگر ویومدل وسطِ پنهانی فهرستِ تازه‌ای بدهد (ماهِ دیگر، حسابِ
    //  دیگر) همان می‌نشیند و پارکِ کهنه دور ریخته می‌شود.
    //
    //  ⚠️ وسطِ ویرایش پارک نمی‌کنیم: برداشتنِ فهرست زیرِ پای ویرایشگر یعنی
    //  نوشتهٔ نیمه‌تمام. جدولِ نامرئی در حالِ ویرایش نیست، ولی قاعده صریح بماند.

    private object? _parked;
    private bool _parkedAway;

    /// <summary>
    /// ⚠️ ‎IsEffectivelyVisible‎ در آوالونیا ۱۱ خبر نمی‌دهد (یک ویژگیِ ساده است،
    /// نه ‎AvaloniaProperty‎). پس پوسته خودش بعد از هر عوض شدنِ صفحه یک بار
    /// <see cref="NotifyPagesChanged"/> را می‌زند و هر جدولِ زنده خودش را
    /// می‌سنجد. جدول‌ها چهل‌تا هم نمی‌شوند، پس این حلقه هیچ است.
    /// </summary>
    private static event Action? PagesChanged;

    /// <summary>
    /// ══ «برگرد» — همین حالا، نه یک پاسِ دیگر بعد ═══════════════════════════
    ///
    /// ⛔ <b>باگی که هر باز کردنِ یک بخش را «یک ثانیه بعد» می‌کرد.</b> گزارشِ
    /// صاحب ریپو (۱۴۰۵/۰۷/۰۵): «هر بخش رو باز می‌کنم جدول‌ها یک ثانیه بعد
    /// میان و این خیلی روی مخ من است.»
    ///
    /// ریشه، ترتیبِ کارها بود:
    /// <code>
    ///   SyncContent()  ⇒ IsShown نشست، صفحه عوض شد
    ///                  ⇒ NotifyPagesChanged()  ← فقط یک Post، با اولویتِ Loaded
    ///   چیدمانِ ۱      ⇒ جدول دیده می‌شود ولی ItemsSourceش هنوز **پارک** است
    ///                  ⇒ یک جدولِ **خالی** کشیده و به کاربر نشان داده می‌شود
    ///   نوبتِ Loaded   ⇒ تازه حالا فهرست برمی‌گردد
    ///   چیدمانِ ۲      ⇒ و تازه حالا ردیف‌ها ساخته می‌شوند
    /// </code>
    ///
    /// پس «جدول یک ثانیه بعد می‌آید» توصیفِ دقیقِ همان بود: یک فریمِ خالی که
    /// پشتِ صفِ ‎Dispatcher‎ به چند فریم کش می‌آمد.
    ///
    /// <para>
    /// ⚠️ <b>و چرا اصلاً ‎Post‎ بود:</b> ‎IsEffectivelyVisible‎ همان لحظه هنوز
    /// مقدارِ قبلی را می‌دهد. ولی آن فقط برای جهتِ <b>پنهان کردن</b> لازم
    /// است. برای جهتِ <b>برگرداندن</b> اصلاً به آن نیازی نیست:
    /// <see cref="SectionShown"/> از خودِ ویومدل می‌پرسد
    /// (<c>SectionViewModel.IsShown</c>) و <c>SyncContent</c> آن را
    /// <b>پیش از</b> صدا زدنِ این متد نوشته است. پس همین حالا درست است.
    /// </para>
    ///
    /// <para>
    /// ⛔ پس: برگرداندن <b>هم‌زمان</b>، پنهان کردن همچنان با ‎Post‎. هیچ
    /// قاعده‌ای پس نرفت — «بخشِ پنهان صفر ردیفِ زنده دارد» همان است و
    /// سنجشِ ‎idle‎ همان را می‌سنجد، چون این جهت فقط جدولی را برمی‌گرداند که
    /// بخشش <b>همین حالا</b> دیده می‌شود.
    /// </para>
    /// </summary>
    private static event Action? PagesShown;

    private static bool _notifyQueued;

    public static void NotifyPagesChanged()
    {
        //  ۱) برگرداندن: همین حالا، پیش از چیدمانِ همین پاس
        PagesShown?.Invoke();

        //  ۲) پارک کردن: پس از چیدمان، چون ‎IsEffectivelyVisible‎ تازه رسیده
        //     و هنوز مقدارِ قبلی را می‌دهد.
        if (_notifyQueued || PagesChanged is null) return;
        _notifyQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _notifyQueued = false;
            PagesChanged?.Invoke();
        }, DispatcherPriority.Loaded);
    }

    private void OnPagesChanged() => OnShownChanged(IsEffectivelyVisible);

    /// <summary>
    /// جهتِ «برگرد»، بی هیچ صبری.
    ///
    /// ⚠️ عمداً فقط <see cref="SectionShown"/> را می‌پرسد و نه
    /// ‎IsEffectivelyVisible‎ را: آن یکی هنوز به‌روز نشده. و عمداً فقط
    /// <b>برمی‌گرداند</b> — اگر این‌جا پارک هم می‌کرد، جدولی که همین حالا
    /// دارد نشان داده می‌شود با مقدارِ کهنهٔ ‎IsEffectivelyVisible‎ پارک
    /// می‌شد.
    /// </summary>
    private void OnPagesShown()
    {
        if (!_parkedAway || GrowsToContent) return;
        if (!SectionShown()) return;
        _parkedAway = false;
        var back = _parked;
        _parked = null;
        if (back is not null) SetCurrentValue(ItemsSourceProperty, back);
    }

    private void OnShownChanged(bool shown)
    {
        // ⚠️ جدولِ **هم‌قدِ ردیف‌هایش** (ورق و پارچه) پارک نمی‌شود. آن‌ها هیچ
        // ردیفی را مجازی‌سازی نمی‌کنند، پس بازسازی یعنی ساختنِ دوبارهٔ هر سه
        // جدولِ صفحه از صفر — سنجشِ ‎waraqperf‎ عددش را داد: باز کردنِ دوبارهٔ
        // یک ورق از ~۳۰ میلی‌ثانیه به ۱٫۲ ثانیه می‌رفت. در عوض سقفِ خودشان
        // (‎GrowRowLimit‎ = ۸۰ ردیف) یعنی زنده ماندنشان ارزان است. پارک برای
        // دفترها و حساب‌های بلند است، که صدها ردیف زنده می‌گذاشتند.
        if (GrowsToContent) return;

        // ⚠️ و جدولی که فقط **صفحه‌اش** بسته شده، نه بخشش. کاربر که از حسابِ
        // یک قرض‌دار بیرون می‌آید تا حسابِ بعدی را باز کند، هنوز در همان بخش
        // است — و پارک کردن یعنی ‎DataGrid‎ همهٔ خانه‌های همهٔ ردیف‌ها را برای
        // حسابِ بعدی از صفر بسازد. ‎enterperf‎ عددش را داد: باز کردنِ حساب با
        // پارک ۶۳۰ میلی‌ثانیه، بی پارک ۶۷.
        //
        // قاعده دست‌نخورده می‌ماند: **بخشِ پنهان** همچنان صفر ردیفِ زنده دارد
        // (سنجشِ ‎idle‎)، چون میزانِ سنجش خودِ صفحهٔ بخش است، نه این جدول.
        if (!shown && SectionShown()) return;

        if (!shown)
        {
            if (_parkedAway || _editing || ItemsSource is null) return;
            _parked = ItemsSource;
            _parkedAway = true;
            SetCurrentValue(ItemsSourceProperty, null);
        }
        else if (_parkedAway)
        {
            _parkedAway = false;
            var back = _parked;
            _parked = null;
            if (back is not null) SetCurrentValue(ItemsSourceProperty, back);
        }
    }

    /// <summary>
    /// بخشی که این جدول در آن نشسته هنوز جلوی چشم است؟
    ///
    /// ⚠️ از روی **ویومدل** پرسیده می‌شود، نه از روی ‎IsEffectivelyVisible‎ی
    /// جدِ دیداری. یک بار از راهِ دیداری رفتم و سنجشِ ‎idle‎ همان‌جا گرفتش:
    /// ‎ContentPresenter‎ی که ‎ItemsControl‎ دورِ هر بخش می‌سازد هیچ‌وقت پنهان
    /// نمی‌شود (پنهانی روی ‎ContentControl‎ِ داخلش است) ولی ‎DataContext‎ش
    /// همان ویومدلِ بخش است — پس «بیرونی‌ترین جد» همیشه دیده‌شده درمی‌آمد و
    /// هیچ جدولی پارک نمی‌شد (۱۶۶ ردیفِ زندهٔ بخشِ پنهان).
    ///
    /// <see cref="ViewModels.SectionViewModel.IsShown"/> را خودِ پوسته
    /// می‌نویسد (‎MainViewModel.SyncContent‎) و دقیقاً یعنی «این صفحه همانی
    /// است که کاربر می‌بیند».
    /// </summary>
    private bool SectionShown()
    {
        foreach (var v in this.GetVisualAncestors())
            if (v is Control { DataContext: ViewModels.SectionViewModel sec }) return sec.IsShown;
        return false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ItemsSourceProperty) return;
        // فهرستِ تازه‌ای از خودِ اتصال رسید ⇒ پارکِ کهنه دیگر معتبر نیست
        if (_parkedAway && ItemsSource is not null) { _parkedAway = false; _parked = null; }
        if (_watched is not null) _watched.CollectionChanged -= OnRowsChanged;
        _watched = ItemsSource as System.Collections.Specialized.INotifyCollectionChanged;
        if (_watched is not null) _watched.CollectionChanged += OnRowsChanged;
        _shown = 0; _measuredShown = 0;
        ForgetHistory();          // فهرستِ تازه ⇒ برگشتِ کهنه به ردیفی می‌خورد که دیگر نیست
        LeaveSticky();
        FixRowHeaderWidth();
        InvalidateMeasure();
    }

    /// <summary>
    /// ══ پهنای ستونِ «#» صریح است، نه خودکار ═══════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «تعویضِ تم خط‌ها را کج می‌کرد — از وسطِ کادر به چپ یا
    /// راست می‌آمدند.» سنجشِ ‎themeflip‎ ریشه‌اش را نشان داد: با ‎RowHeaderWidth‎ی
    /// خودکار، ‎DataGrid‎ پهنای سرستونِ ردیف را از خودِ سرستون‌ها می‌گیرد و در
    /// چیدمانِ اول آن را صفر می‌شمارد (۴۲ پیکسلِ خالی به ستونِ پُرکننده می‌رفت)؛
    /// اولین بی‌اعتبارسازیِ بزرگ — تعویضِ تم — دوباره می‌شمرد، ۴۲ می‌شد و همهٔ
    /// خط‌های عمودی ۴۲ پیکسل جابه‌جا می‌شدند. پهنای صریح در هر دو حال یکی است.
    /// عدد از شمارِ ردیف‌ها درمی‌آید تا ۶۴٬۰۰۰ هم جا شود.
    /// </summary>
    private void FixRowHeaderWidth()
    {
        if (!RowNumbers) return;
        var digits = Math.Max(2, RowCount().ToString().Length);
        var w = 26 + 8 * digits;                       // دو رقم ⇒ همان ۴۲ی همیشگی
        if (double.IsNaN(RowHeaderWidth) || Math.Abs(RowHeaderWidth - w) > 0.5) RowHeaderWidth = w;
    }

    private void OnRowsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // فهرست از نو پر شد (ماهِ دیگر، حسابِ دیگر) ⇒ رشد از اول، تدریجی.
        // وگرنه ‎_shown‎ی ماهِ قبل می‌ماند و کلِ ماهِ تازه در یک پاس ساخته می‌شد.
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset
            || RowCount() < _shown)
        {
            _shown = 0; _measuredShown = 0;

            // ⚠️ فقط ‎Reset‎ تاریخچه را پاک می‌کند، نه هر کم شدنِ ردیف: حذفِ یک
            // ردیفِ بی‌ربط نباید برگشتِ پنج خانه‌ای را که کاربر تایپ کرده ببرد.
            // ردیفِ رفته را خودِ ‎Live()‎ هنگامِ برگشت رد می‌کند.
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                ForgetHistory();

            // ══ محتوای تازه ⇒ پهنای تازه ═════════════════════════════════════
            //
            // گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۶/۲۷): بالای ورق، «شروع» و «ختم»ِ
            // پایه‌ها «…» می‌شدند. ریشه: جدولِ ورق با ردیف‌های **خالی** باز
            // می‌شود، پس پهنای طبیعیِ ستون همان پهنای سرستون است و همان‌جا
            // سفت می‌شود؛ بعد که عددهای واقعی نشستند، دیگر جا نبود.
            //
            // پس با پر شدنِ دوبارهٔ فهرست (ورقِ دیگر، ماهِ دیگر، حسابِ دیگر)
            // یک بار دیگر پهنا از روی محتوای واقعی حساب می‌شود.
            //
            // ⚠️ نه وقتی کاربر خودش پهنا ذخیره کرده (‎_saved‎) — آن‌جا حرفِ
            // کاربر آخر است، مثلِ اکسل.
            // ⚠️ چیدمانِ ستون‌ها عوض شد (حسابِ تیل ⇄ حسابِ پول، که ستون‌های
            // متفاوتی نشان می‌دهند) ⇒ کلیدِ پهنای دیگری و تنظیمِ دیگری.
            // بی این، عددهای چیده‌شده برای آن یکی چیدمان این‌جا می‌نشستند.
            var visible = Columns.Count(c => c.IsVisible);
            if (_autoKeyFor >= 0 && _autoKeyFor != visible)
            {
                _autoKey = null; _autoKeyFor = -1;
                _savedRead = false; _saved = null;
            }

            if (_saved is null)
            {
                _spread = false; _pinned = false; _anyRowLoaded = false; _autoWidths = null;
                foreach (var c in Columns)
                    c.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);

                //  ⛔ **یک پاس صبر**، وگرنه پهنای کهنه سفت می‌شود.
                //  شرحش بالای ‎_freshCols‎.
                _freshCols = true;
            }
        }
        FixRowHeaderWidth();
        InvalidateMeasure();
    }

    /// <summary>
    /// ══ رشد فقط تا جایی که دیده می‌شود ═══════════════════════════════════════
    ///
    /// سنجشِ «پنج سال داده» (‎years‎): فهرست‌های گزارشی با ۶۰۰ ردیف (قرض‌های
    /// کهنه، مدتِ عضویت، جمعِ رسیدها) شش تا هشت ثانیه CPU می‌خوردند فقط برای
    /// ساختنِ ردیف‌هایی که کاربر شاید هیچ‌وقت تا آن‌جا نلغزد — و همان CPU،
    /// تعویضِ تم را هم چهارده ثانیه می‌کرد (هر ردیفِ زنده ده‌ها منبعِ پویا دارد).
    ///
    /// حالا جدول تا <see cref="Lookahead"/> صفحه پایین‌ترِ دیدِ کاربر بلند
    /// می‌شود و بس؛ همین که صفحه بلغزد، تکهٔ بعدی می‌آید — پیش از آن‌که کاربر
    /// به آن برسد. «سقفِ زیرِ جدول» همچنان نیست: بلندیِ جدول با هر تکه بیشتر
    /// می‌شود و اسکرول مالِ کلِ صفحه است.
    /// </summary>
    private const double Lookahead = 1.5;

    private bool _pageHooked;

    /// <summary>آیا پایینِ ‎show‎ ردیفِ ساخته‌شده نزدیکِ دیدِ کاربر است.</summary>
    private bool NearViewport(int show)
    {
        var page = Page;
        if (page is null || page.Viewport.Height <= 0) return true;
        var top = this.TranslatePoint(new Point(0, 0), page)?.Y;
        if (top is null) return true;
        return top.Value + WantedHeight(show) <= page.Viewport.Height * (1 + Lookahead);
    }

    private void HookPage()
    {
        if (_pageHooked || Page is not { } page) return;
        _pageHooked = true;
        page.ScrollChanged += OnPageScroll;
    }

    private void OnPageScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (_sticky) { SyncSticky(); return; }
        var rows = RowCount();
        if (!_spread || _growQueued || rows < 0 || rows > GrowRowLimit || _shown >= rows) return;
        if (NearViewport(Math.Max(_shown, 1))) QueueGrow(rows, Math.Max(_shown, 1));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PagesChanged -= OnPagesChanged;
        PagesShown -= OnPagesShown;
        if (_pageHooked && _page is { } page) page.ScrollChanged -= OnPageScroll;
        _pageHooked = false;
        _page = null;
    }

    private void QueueGrow(int rows, int show)
    {
        if (_growQueued) return;
        _growQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _growQueued = false;
            // تکهٔ کوچک و ثابت — چرایی‌اش بالای ‎GrowChunk‎. هیچ فریمی بیش از
            // ‎GrowMax‎ ردیفِ تازه نمی‌سازد.
            //
            // ⚠️ مگر جدولی که **هم‌قدِ ردیف‌هایش** است (ورق و پارچه): آن‌ها ذاتاً
            // کوتاه‌اند (سقفِ ‎GrowRowLimit‎ی خودشان ۸۰ ردیف) و کاربر پشتِ سرِ هم
            // بازشان می‌کند. با تکهٔ هشت‌تایی، باز کردنِ یک ورق ده پاسِ پس‌زمینه
            // می‌شد و سنجشِ ‎waraqperf‎ آن را گرفت (۶۳۱ms در برابرِ سقفِ ۴۰۰).
            // لگِ اسکرول هم مالِ همان‌ها نبود: دفترهای بلندِ لغزنده بودند.
            var chunk = Math.Clamp(show, GrowChunk, GrowsToContent ? GrowTall : GrowMax);
            _shown = Math.Min(rows, show + chunk);
            InvalidateMeasure();
        }, DispatcherPriority.Background);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        DiagMeasure++;
        var rows = RowCount();
        var screen = ScreenHeight();

        // ⚠️ پاسِ اول همیشه تنگ است، حتی برای جدولِ کوچک: تا ستون‌های ‎Auto‎
        // پهنایشان را از یک صفحه ردیف بگیرند، نه از همهٔ ردیف‌ها. تا پهنا سفت
        // نشده، هر ردیفِ تازه همهٔ ستون‌ها را دوباره اندازه می‌گیرد و هزینه
        // مربعی بالا می‌رود. ‎SpreadColumns‎ همین که پهناها را سفت کرد،
        // ‎_spread‎ می‌شود و از پاسِ بعد جدول آزاد است.
        if (!_spread) return Capped(availableSize, screen);
        if (rows < 0) return Capped(availableSize, screen);

        // ══ جدولِ بلند: پنجرهٔ چسبان، نه ساختنِ همهٔ ردیف‌ها ═══════════════
        if (WantsSticky(rows, screen)) return MeasureSticky(availableSize, rows, screen);
        LeaveSticky();
        if (rows > GrowRowLimit) return Capped(availableSize, screen);

        if (rows != _padRows) { _pad = 0; _padRows = rows; }

        // رشدِ تدریجی: این پاس فقط تا ‎show‎ ردیف بلند می‌شود؛ بقیه فریمِ بعد.
        var show = Math.Min(rows, Math.Max(_shown, FirstChunk(screen)));
        // ⚠️ و فقط اگر آن «بعد» به چشم بیاید (شرحِ ‎NearViewport‎): ردیفی که
        // دو صفحه پایین‌تر از دیدِ کاربر است تا او به آن نزدیک نشده ساخته
        // نمی‌شود — همان چیزی که صفحهٔ سیصدردیفی را از چند ثانیه CPU به چند
        // ده میلی‌ثانیه رساند. با لغزشِ صفحه، ‎OnPageScroll‎ ادامه می‌دهد.
        if (show < rows) { if (NearViewport(show)) QueueGrow(rows, show); else HookPage(); }
        else _shown = rows;

        var want = WantedHeight(show);
        if (availableSize.Height > want) availableSize = availableSize.WithHeight(want);
        // سنجش: گران‌ترین تکهٔ رشد (ردیف‌های تازه در یک پاس) — ‎scrollperf‎ می‌خواند
        var grew = show - _measuredShown;
        var sw = grew > 0 ? System.Diagnostics.Stopwatch.StartNew() : null;
        var size = base.MeasureOverride(availableSize);
        if (sw is not null)
        {
            sw.Stop();
            _measuredShown = show;
            if (sw.ElapsedMilliseconds > DiagChunkMaxMs) { DiagChunkMaxMs = sw.ElapsedMilliseconds; DiagChunkMaxRows = grew; }
        }
        return size.WithHeight(want);
    }

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
    /// ⚠️ چرا نه از ردیفِ اول: جدولِ کوتاه (دفترِ یک ماهِ معمولی، ورق، پارچه)
    /// ارزان است و رفتارِ امروزش سال‌ها سنجیده شده. پنجرهٔ چسبان برای جایی
    /// است که هزینه واقعاً بالا می‌رود — و ۶۰ ردیف حدودِ سه صفحه است.
    /// </summary>
    public const int StickyRowLimit = 60;

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
        if (_navBar is { } cached && cached.GetVisualRoot() is not null) return cached.Bounds.Height;
        if (Page?.GetVisualRoot() is not Visual root) return 0;
        _navBar = root.GetVisualDescendants().OfType<Border>()
                      .FirstOrDefault(b => b.Name == "NavBar");
        return _navBar?.Bounds.Height ?? 0;
    }

    private double _blind;

    private Size MeasureSticky(Size availableSize, int rows, double screen)
    {
        var total = WantedHeight(rows);
        _windowH = Math.Min(screen, total);
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
        var want = Math.Clamp(_blind - at.Y, 0, span);
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

    // ══════════════════════════════════════════════════════════════════════
    //  ══ دوبار-کلیک روی خطِ ستون: هم‌قدِ محتوا ═══════════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  همان کارِ اکسل. قاعده‌اش ساده و قابلِ حدس است: **همان ستونی هم‌قد
    //  می‌شود که کشیدنِ همان دسته پهنش می‌کرد** — دستهٔ راستِ یک سربرگ مالِ
    //  خودش است و دستهٔ چپش مالِ ستونِ پیشین (به ترتیبِ نمایش). این‌طور کاربر
    //  لازم نیست قاعدهٔ تازه‌ای یاد بگیرد؛ همان چیزی که با کشیدن می‌دید.
    //
    //  ⚠️ پهنا از **ردیف‌های ساخته‌شده** درمی‌آید، نه از همهٔ ردیف‌های داده:
    //  جدولِ چسبان فقط یک صفحه ردیف زنده دارد و همین هم درست است — اکسل هم
    //  ستون را به بلندترین چیزی که در دید هست می‌رساند، نه به کلِ فایل.
    //
    //  ⚠️ پس از هم‌قد شدن، همهٔ ستون‌ها پیکسلی می‌شوند (همان قاعدهٔ
    //  ‎PinOnUserResize‎): ستونِ ستاره‌ای «سهم از قاب» است، پس اگر یکی پهن شود
    //  بقیه خودشان جمع می‌شوند و کاربر می‌بیند ستون‌هایی که دست نزده هم تکان
    //  خوردند. و پهنای تازه با ‎WidthKey‎ همان‌جا ذخیره می‌شود.

    /// <summary>
    /// ⚠️ ‎DataGrid‎ی آوالونیا برای خطِ ستون **دستهٔ جداگانه‌ای ندارد**: خودِ
    /// سربرگ، نزدیکیِ لبه‌اش را «خطِ تغییرِ اندازه» می‌شمارد
    /// (‎DATAGRIDCOLUMNHEADER_resizeRegionWidth‎). یک بار دنبالِ ‎Thumb‎ گشتیم و
    /// سنجه گرفتش: در درختِ سربرگ اصلاً ‎Thumb‎ی نیست. پس همان قاعدهٔ خودِ
    /// جدول: لبهٔ راست ⇒ همین ستون، لبهٔ چپ ⇒ ستونِ پیشین.
    /// </summary>
    private const double LineGrab = 8;

    private void OnHeaderLineDoubleTap(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual v) return;
        var header = v as DataGridColumnHeader ?? v.GetVisualAncestors().OfType<DataGridColumnHeader>().FirstOrDefault();
        if (header is null) return;
        double x;
        try { x = e.GetPosition(header).X; } catch { return; }
        if (AutoFitAt(header, x)) e.Handled = true;
    }

    /// <summary>
    /// دوبار-کلیک در نقطهٔ ‎x‎ از سربرگ: اگر روی خطِ ستون بود، همان ستونی را
    /// هم‌قدِ محتوا می‌کند که کشیدنِ همان خط پهنش می‌کرد. برمی‌گرداند «کاری کردم».
    /// </summary>
    public bool AutoFitAt(DataGridColumnHeader header, double x)
    {
        if (ColumnOfHeader(header) is not { } own) return false;
        var w = header.Bounds.Width;
        var cols = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        var i = cols.IndexOf(own);

        DataGridColumn? target = null;
        if (w - x <= LineGrab) target = own;
        else if (x <= LineGrab && i > 0) target = cols[i - 1];
        if (target is null) return false;

        AutoFit(target);
        return true;
    }

    /// <summary>ستون را هم‌قدِ محتوایش می‌کند و همان پهنا را سفت نگه می‌دارد.</summary>
    public void AutoFit(DataGridColumn col)
    {
        col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
        InvalidateMeasure();
        // یک پاس بعد، وقتی ‎Auto‎ پهنای طبیعی را حساب کرد، همان عدد سفت می‌شود
        Dispatcher.UIThread.Post(() =>
        {
            var w = col.ActualWidth;
            if (double.IsNaN(w) || w <= 0) return;
            col.Width = new DataGridLength(Math.Max(FloorWidth, w), DataGridLengthUnitType.Pixel);
            _pinned = false;
            PinOnUserResize();
            RememberWidths();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>ستونِ یک سربرگ — مثلِ ‎ColumnOfCell‎، از خودِ سربرگ.</summary>
    private static DataGridColumn? ColumnOfHeader(DataGridColumnHeader h) =>
        h.GetType().GetProperty("OwningColumn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
              | System.Reflection.BindingFlags.Public)?.GetValue(h) as DataGridColumn;

    /// <summary>ستونی که این خانه مالِ اوست — از خودِ خانه.</summary>
    private static DataGridColumn? ColumnOfCell(DataGridCell cell) =>
        cell.GetType().GetProperty("OwningColumn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
              | System.Reflection.BindingFlags.Public)?.GetValue(cell) as DataGridColumn;

    private ScrollBar? _vbar;

    /// <summary>نوارِ لغزشِ عمودیِ خودِ جدول (‎PART_VerticalScrollbar‎).</summary>
    private ScrollBar? VerticalBar =>
        _vbar ??= this.GetVisualDescendants().OfType<ScrollBar>()
                      .FirstOrDefault(b => b.Orientation == Orientation.Vertical);

    /// <summary>هر جدول یک‌بار پهن می‌شود؛ بعدش ستون‌های ستاره‌ای خودشان
    /// با تغییرِ اندازهٔ پنجره تنظیم می‌شوند.</summary>
    private bool _spread;

    /// <summary>
    /// کم‌ترین پهنایی که یک ستون می‌تواند بگیرد — فقط آن‌قدر که ستون از دید
    /// گم نشود و دستهٔ کشیدنش زیرِ ماوس بماند. سقفی در کار نیست.
    /// </summary>
    private const double FloorWidth = 24;

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
    /// ⚠️ و این‌جا یک کفِ **کوچک** می‌نشیند، نه پهنای طبیعیِ ستون.
    ///
    /// تا امروز ‎MinWidth‎ روی همان پهنای طبیعی گذاشته می‌شد تا ستون زیرِ
    /// اندازهٔ محتوا فشرده نشود. ولی گزارشِ صاحب ریپو همین را باگ دانست:
    /// «اندازه‌های جدول رو نمی‌تونم هر چقد که می‌خوام بزرگ یا کوچیک کنم و این
    /// خیلی اذیت می‌کنه … محدودیت هم نباشه.» حق داشت — آن کف یعنی ستون از
    /// یک جایی به بعد کوچک نمی‌شد و دسته زیرِ دست گیر می‌کرد.
    ///
    /// حالا کف فقط <see cref="FloorWidth"/> است (به اندازه‌ای که ستون گم
    /// نشود و دسته‌اش پیدا بماند) و سقفی هم در کار نیست. اگر جمعِ ستون‌ها از
    /// قاب بیشتر شد، جدول افقی می‌لغزد — همان ‎overflow-x:auto‎ی سایت.
    /// </summary>
    // ══════════════════════════════════════════════════════════════════════
    //  ══ «ماه را عوض کردم، همهٔ سربرگ‌ها رفتند کنج» ══════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «خواستم ماه را عوض کنم به ماه قبلی
    //  بروم، تمام سربرگ‌ها باگ خوردند و رفتند کنجِ سربرگشان.»
    //
    //  ⛔ ریشه یک مسابقهٔ یک‌پاسی بود: با عوض شدنِ ماه فهرست ‎Reset‎ می‌شود،
    //  همان‌جا همهٔ ستون‌ها به ‎Auto‎ برمی‌گردند و ‎_spread‎ باز می‌شود. ولی
    //  ‎ActualWidth‎ در همان لحظه هنوز **پهنای کهنه** است — آوالونیا هنوز
    //  ‎Auto‎ را اندازه نگرفته. اگر ‎LayoutUpdated‎ی همان پاس برسد،
    //  ‎SpreadColumns‎ همان عددهای کهنه را «طبیعی» می‌خواند و برای همیشه
    //  سفتشان می‌کند: ستون‌ها جمع می‌شوند در یک گوشه و بقیهٔ قاب خالی
    //  می‌ماند — دقیقاً همان «رفتند کنج».
    //
    //  ⚠️ و چرا خودش درست نمی‌شد: ‎_spread‎ از همان پاس ‎true‎ شده و
    //  ‎SpreadColumns‎ دیگر هیچ‌وقت دوباره نمی‌دود.
    //
    //  چاره یک پاس صبر است: نخستین ‎SpreadColumns‎ پس از پر شدنِ دوبارهٔ
    //  فهرست فقط چیدمان را بی‌اعتبار می‌کند و برمی‌گردد. پاسِ بعدی
    //  ‎ActualWidth‎ِ واقعیِ ‎Auto‎ را دارد. (هزینه‌اش یک پاسِ چیدمان است،
    //  همان چیزی که ‎OnRowsChanged‎ هم به‌هرحال می‌خواهد.)

    /// <summary>فهرست تازه پر شده و پهناها هنوز کهنه‌اند.</summary>
    private bool _freshCols;

    private void SpreadColumns()
    {
        if (_spread || Columns.Count == 0) return;
        if (_freshCols) { _freshCols = false; InvalidateMeasure(); return; }

        var cols = Columns.Where(c => c.IsVisible).ToList();
        if (cols.Count == 0) return;

        var natural = cols.Select(c => c.ActualWidth).ToArray();
        if (natural.Any(w => double.IsNaN(w) || w <= 0)) return;   // هنوز چیده نشده

        // ══ تا ردیفی چیده نشده، پهنا سفت نشود ═══════════════════════════════
        //
        // «پهنای طبیعی»ِ یک ستونِ ‎Auto‎ پیش از آمدنِ ردیف‌ها فقط پهنای
        // **سربرگ** است. اگر همان‌جا سفت شود، ستونی که سرستونِ کوتاه و عددِ
        // بلند دارد تا ابد تنگ می‌ماند و متنش «…» می‌شود — در ورق‌ها ستونِ
        // «ختم» دقیقاً همین بود («۱۰,۹۳۰» ⇒ «…,۹۳۰») در حالی که «شروع»ِ
        // بغلش جا داشت. همان تلهٔ ‎Auto‎ که در جدول‌های «زیانِ افزایش قیمت»
        // با پهنای صریح دور زده شده بود.
        //
        // پس یک پاس صبر می‌کنیم: جدولی که ردیف دارد، تا نخستین ردیفش چیده
        // نشده پهنایش را قفل نمی‌کند. (پاسِ اول به‌هرحال تنگ است — بالای
        // ‎MeasureOverride‎ — پس این یک پاس هزینه‌ای ندارد.)
        if (!_anyRowLoaded && RowCount() > 0) return;

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

        // ══ بارِ **خودکار** هیچ‌وقت از قاب بیرون نمی‌زند ═════════════════════
        //
        // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «بالای ورق، همان‌جایی که شروع و ختمِ
        // پایه‌ها نوشته می‌شود، توی بعضی کامپیوترها خیلی بزرگ است و از کادر
        // زده بیرون و باید با اسکرولِ چپ و راست بروی تا پیدایش کنی.»
        //
        // ⛔ ریشه: پهنای طبیعیِ ستونِ ‎Auto‎ به قلم و DPIِ همان کامپیوتر بند
        // است. روی نمایشگرِ بزرگ‌تر یا با قلمِ درشت‌ترِ ویندوز، جمعِ همان
        // ستون‌ها از قاب می‌زند بیرون — و چون همان لحظه پیکسلی سفت می‌شدند،
        // برای همیشه بیرون می‌ماندند.
        //
        // ⚠️ و این خواستهٔ «اگه زیاد بزرگ شد به چپ و راست هم اسکرول بشه» را
        // پس نمی‌گیرد: آن دربارهٔ ستونی است که **کاربر** کشیده
        // (‎PinOnUserResize‎، دست‌نخورده). این‌جا فقط بارِ خودکار است، که
        // کاربر انتخابش نکرده و از آن انتظارِ اسکرولِ افقی هم ندارد.
        if (!spare) natural = FitToRoom(natural, room);

        // ══ پهنای ذخیره‌شده مقدم است ═════════════════════════════════════════
        // اگر کاربر یک بار این جدول را تنظیم کرده، همان می‌نشیند — نه پهنای
        // طبیعیِ محتوای امروز. پس ورقِ فردا هم همان‌قدر است.
        var saved = Saved(cols.Count);

        for (var i = 0; i < cols.Count; i++)
        {
            cols[i].MinWidth = FloorWidth;
            cols[i].MaxWidth = double.PositiveInfinity;
            cols[i].Width = saved is not null
                ? new DataGridLength(saved[i], DataGridLengthUnitType.Pixel)
                : spare
                    ? new DataGridLength(natural[i], DataGridLengthUnitType.Star)
                    : new DataGridLength(natural[i], DataGridLengthUnitType.Pixel);
        }

        // پهنای ذخیره‌شده خودش پیکسلی است، پس جدول از همین حالا «سنجاق‌شده»
        // است و ‎PinOnUserResize‎ نباید دوباره رویش حساب کند.
        if (saved is not null) _pinned = true;

        _spread = true;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ هر جدولی پهنایش را یادش می‌ماند — نه فقط سه جدولِ ورق ══════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «بخش قرض‌داران جدول‌هاشو جوری که خاستم
    //  اندازه کردم، ولی وقتی از حساب بیرون می‌شم و میام دوباره همون مدلِ اول
    //  شده و اندازهٔ من خراب شده یا ثبت نشده.»
    //
    //  ⛔ ریشه سنجیده شد، نه حدس: در کلِ برنامه **سه** جدول ‎WidthKey‎ داشتند
    //  (هر سه در ورق). ‎RememberWidths‎ نخستین خطش این است: «کلید نداری؟
    //  برگرد». پس کشیدنِ ستون در حسابِ قرض‌دار، گاوصندوق، مصارف، صرافی،
    //  شرکت‌ها و هر جای دیگر **هیچ‌وقت** ذخیره نمی‌شد.
    //
    //  ⛔ و راهِ درست «به سی‌وچند فایلِ XAML یک صفت اضافه کن» نبود: فردا
    //  جدولِ سی‌وپنجم اضافه می‌شود و کسی یادش می‌رود، و همین باگ بی‌صدا
    //  برمی‌گردد. پس کلید **خودش ساخته می‌شود** و ‎WidthKey‎ فقط وقتی لازم
    //  است که دو جدول عمداً یک تنظیم را شریک باشند (دو جدولِ تراکنشِ ورق).
    //
    //  ⚠️ شمارِ ستونِ **دیده‌شده** داخلِ کلید است: جدولِ حسابِ قرض‌دار در
    //  دفترِ تیل و دفترِ پول ستون‌های متفاوتی نشان می‌دهد، و یک کلیدِ مشترک
    //  یعنی عددهایی که برای آن یکی چیده شده‌اند. (‎Saved‎ هم همین را جدا
    //  می‌سنجد، ولی آن‌جا فقط «رد کن» است؛ این‌جا هر کدام تنظیمِ خودش را
    //  نگه می‌دارد.)

    /// <summary>کلیدِ ذخیرهٔ پهنا: دستی اگر داده شده، وگرنه خودکار.</summary>
    private string? EffectiveKey(int visible)
    {
        var k = WidthKey;
        if (!string.IsNullOrWhiteSpace(k)) return k;
        if (visible <= 0) return null;
        if (_autoKey is not null && _autoKeyFor == visible) return _autoKey;

        //  ⛔ تا جدول به صفحه‌اش نچسبیده، کلید ساخته **نمی‌شود**. نامِ صفحه
        //  نیمی از کلید است؛ اگر همان یک پاس «پیدا نشد» بدهد و کلیدِ ناقص
        //  کَش شود، پهنای کاربر زیرِ نامِ دیگری می‌نشیند و اجرای بعدی پیدایش
        //  نمی‌کند — یعنی دقیقاً همان «اندازه‌ام ثبت نشد» که این کار برای
        //  بستنش نوشته شد، از درِ دیگر. یک پاس صبر ارزان‌تر است.
        if (this.FindAncestorOfType<UserControl>() is not { } page) return null;
        var host = page.GetType().Name;
        var mine = Name;
        if (string.IsNullOrWhiteSpace(mine))
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in Columns)
                if (c.IsVisible) sb.Append(c.Header as string ?? "?").Append('|');
            mine = Fingerprint(sb.ToString());
        }

        //  کلید عوض شد ⇒ عددهای کلیدِ قبلی به کارِ این چیدمان نمی‌آیند.
        //  ⛔ این‌جا کَشِ «پهنای ذخیره‌شده» **باطل نمی‌شود**، هر چند وسوسه‌اش
        //  هست. این متد از مسیرِ چیدمان صدا زده می‌شود (هم ‎Saved‎ و هم
        //  ‎RememberWidths‎)، پس هر باطل کردنی یعنی خواندنِ دوبارهٔ
        //  ‎settings.json‎ از **دیسک** روی نخِ رابط — برای هر جدول، در هر
        //  پاس. نسخهٔ اولِ همین کار همین را داشت و صفِ ‎Dispatcher‎ را پر
        //  کرد: پارکِ جدولِ بخشِ پیشین که با ‎DispatcherPriority.Loaded‎
        //  پست شده بود دیر می‌رسید، و سنجهٔ ‎idle‎ یک «ردیفِ زندهٔ بخشِ
        //  پنهان» می‌دید. باطل کردن جای خودش را دارد: بلوکِ ‎Reset‎، همان‌جا
        //  که شمارِ ستون‌ها واقعاً عوض می‌شود.
        _autoKeyFor = visible;
        return _autoKey = host + "." + mine + "#" + visible;
    }

    private string? _autoKey;
    private int _autoKeyFor = -1;

    /// <summary>هشتِ نویسهٔ پایدار از نامِ سرستون‌ها — برای جدولِ بی‌نام.</summary>
    private static string Fingerprint(string text)
    {
        unchecked
        {
            ulong h = 1469598103934665603;
            foreach (var ch in text) { h ^= ch; h *= 1099511628211; }
            return h.ToString("x16")[..8];
        }
    }

    /// <summary>
    /// پهناها را به اندازهٔ قاب کوچک می‌کند — به نسبت، و نه زیرِ کفِ خوانایی.
    ///
    /// ⚠️ ستونی که از قبل روی کف است بیشتر از این کوچک نمی‌شود، و مابقیِ
    /// کمبود بینِ بقیه پخش می‌شود. اگر حتی با کفِ همه باز هم جا نشد، همان
    /// بیرون‌زدگی می‌ماند و جدول افقی می‌لغزد — چاره‌ای نیست، ولی دستِ‌کم
    /// دلیلش «ستونِ بی‌جهت پهن» نیست.
    /// </summary>
    private static double[] FitToRoom(double[] natural, double room)
    {
        var sum = natural.Sum();
        if (sum <= room || room <= 0) return natural;

        var w = (double[])natural.Clone();
        for (var pass = 0; pass < 4 && w.Sum() > room + 0.5; pass++)
        {
            var movable = w.Where(x => x > FloorWidth).Sum() - FloorWidth * w.Count(x => x > FloorWidth);
            if (movable <= 0.5) break;
            var need = w.Sum() - room;
            var take = Math.Min(need, movable);
            for (var i = 0; i < w.Length; i++)
            {
                var slack = w[i] - FloorWidth;
                if (slack <= 0) continue;
                w[i] -= take * (slack / movable);
                if (w[i] < FloorWidth) w[i] = FloorWidth;
            }
        }
        return w;
    }

    /// <summary>پهنای ذخیره‌شدهٔ همین جدول — اگر بود و شمارِ ستون‌ها هم خورد.</summary>
    private double[]? Saved(int count)
    {
        var key = EffectiveKey(count);
        if (string.IsNullOrWhiteSpace(key)) return null;

        // ⚠️ یک بار خوانده می‌شود و همان می‌ماند: این تابع در مسیرِ چیدمان
        // است و خواندنِ فایل در هر پاس یعنی همان کندی‌ای که تازه درستش
        // کرده‌ایم.
        if (!_savedRead)
        {
            _savedRead = true;
            _saved = Services.AppSettings.LoadColumnWidths(key);
        }

        // شمارِ ستون‌ها عوض شده (ستونی اضافه یا کم شده) ⇒ عددهای کهنه
        // به‌درد نمی‌خورند و به پهنای طبیعی برمی‌گردیم.
        return _saved is { } w && w.Length == count && w.All(x => x >= FloorWidth) ? w : null;
    }

    private bool _savedRead;
    private double[]? _saved;

    /// <summary>
    /// کاربر ستونی را کشید ⇒ پهنای همهٔ ستون‌ها برای همیشه نوشته می‌شود.
    ///
    /// ⚠️ با تأخیر، نه همان لحظه: کشیدنِ ستون ده‌ها رویدادِ پشتِ سرِ هم
    /// می‌دهد و نوشتنِ فایل در هر کدام، کشیدن را لق می‌کند.
    /// </summary>
    private void RememberWidths()
    {
        var cols = Columns.Where(c => c.IsVisible).ToList();
        if (cols.Count == 0) return;
        var key = EffectiveKey(cols.Count);
        if (string.IsNullOrWhiteSpace(key)) return;
        var w = cols.Select(c => c.ActualWidth).ToArray();
        if (w.Any(x => double.IsNaN(x) || x <= 0)) return;

        // ══ فقط پهنایی که **کاربر** ساخته ذخیره می‌شود ══════════════════════
        //
        // پیش از این هر جدولی در نخستین چیدمانش پهنای خودکارش را هم ذخیره
        // می‌کرد — و چون جدولِ ورق با ردیف‌های **خالی** باز می‌شود، همان
        // پهنای «سرستونِ خالی» برای همیشه در تنظیمات می‌نشست و از فردا هر
        // عددی «…» می‌شد. گزارشِ صاحب ریپو با عکس: «شروع و ختمِ پایه‌ها از
        // کادرشان بیرون زده.»
        //
        // حالا تا وقتی پهناها همان‌اند که خودِ برنامه سنجاق کرده، چیزی
        // نوشته نمی‌شود؛ اولین کشیدنِ دستیِ ستون (یا دوبار-کلیکِ هم‌قدسازی)
        // همه را ذخیره می‌کند، همان‌طور که همیشه بود.
        if (_autoWidths is { } auto && auto.Length == w.Length
            && auto.Zip(w, (a, b) => Math.Abs(a - b) < 0.5).All(x => x)) return;
        if (_saved is { } old && old.Length == w.Length
            && old.Zip(w, (a, b) => Math.Abs(a - b) < 0.5).All(x => x)) return;

        _saved = w;
        _savedRead = true;
        var name = key!;
        Dispatcher.UIThread.Post(() => Services.AppSettings.SaveColumnWidths(name, w),
                                 DispatcherPriority.Background);
    }

    /// <summary>
    /// ══ کاربر که ستونی را کشید، همه پیکسلی می‌شوند ══════════════════════════
    ///
    /// خواستهٔ صاحب ریپو: «اگه زیاد بزرگ شد به چپ و راست هم اسکرول بشه.»
    ///
    /// ⚠️ با پهنای ستاره‌ای این هرگز رخ نمی‌داد و سنجش هم همین را گرفت: ستون
    /// را ۹۰۰ کردم، بقیه خودشان جمع شدند تا جمع در قاب بماند، و نوارِ لغزش
    /// نیامد. ستاره یعنی «سهم از قاب»، پس جدولِ ستاره‌ای **هیچ‌وقت** از قابش
    /// پهن‌تر نمی‌شود.
    ///
    /// پس همان لحظه که یکی از ستون‌ها پیکسلی شد (یعنی کاربر کشیدش)، بقیه هم
    /// روی پهنای همان لحظه‌شان قفل می‌شوند. از آن به بعد جمعِ ستون‌ها آزاد
    /// است از قاب بزند بیرون و جدول افقی بلغزد — مثلِ اکسل.
    /// </summary>
    private void PinOnUserResize()
    {
        if (!_spread || _pinned) return;

        var cols = Columns.Where(c => c.IsVisible).ToList();
        if (cols.Count == 0) return;
        if (!cols.Any(c => c.Width.UnitType == DataGridLengthUnitType.Pixel)) return;
        if (cols.All(c => c.Width.UnitType == DataGridLengthUnitType.Pixel)) { _pinned = true; return; }

        foreach (var c in cols)
        {
            var w = c.ActualWidth;
            if (double.IsNaN(w) || w <= 0) return;   // هنوز چیده نشده
        }
        foreach (var c in cols)
            c.Width = new DataGridLength(c.ActualWidth, DataGridLengthUnitType.Pixel);

        _pinned = true;
        // ⚠️ این پهناها را **ذخیره نمی‌کنیم**: خودکارند، نه خواستهٔ کاربر.
        // همین‌جا نگهشان می‌داریم تا بعداً معلوم شود کاربر ستونی را کشیده
        // یا نه — شرحش بالای ‎RememberWidths‎.
        _autoWidths = cols.Select(c => c.ActualWidth).ToArray();
    }

    private bool _pinned;

    /// <summary>پهنای خودکارِ همین جدول در لحظهٔ سنجاق شدن.</summary>
    private double[]? _autoWidths;


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
        if (RowDeleteCommand is null) return;
        e.Row.ContextRequested += OnRowContext;
    }

    private void OnRowContext(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not DataGridRow row || RowDeleteCommand is null) return;

        var item = new MenuItem
        {
            Header = "🗑 حذفِ این ردیف",
            Command = RowDeleteCommand,
            CommandParameter = row.DataContext,
        };
        new MenuFlyout { ItemsSource = new[] { item } }.ShowAt(row, showAtPointer: true);
        e.Handled = true;
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
        if (_wired) return;
        _wired = true;
        // تنها منبعِ درستِ «الان در حال ویرایشیم» — خودِ جدول می‌گوید.
        PreparingCellForEdit += (_, e) =>
        {
            _editing = true; AutoDirection(e.EditingElement);
            // پیشنهادِ خودکار: فهرستِ نام‌دارِ ستون (اگر داشت) + مقدارهای همان ستون
            if (e.EditingElement is TextBox tb || (e.EditingElement?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } tb2 && (tb = tb2) is not null))
            {
                var named = Suggest.Of(Suggest.GetKey(e.Column));
                var learned = Suggest.ColumnValues(ItemsSource, e.Column);
                if (named.Count + learned.Count > 0) Suggest.Attach(tb, named, learned);
            }
        };
        // ⚠️ پیش از نشستنِ مقدار در ردیف: تکملهٔ پذیرفته‌نشدهٔ پیشنهادِ خودکار
        // برداشته شود، وگرنه «س»ی کاربر «سلام من هارون هستم» ذخیره می‌شد.
        CellEditEnding += (_, _) => Suggest.Settle();
        CellEditEnded += (_, _) =>
        {
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
        if (TopLevel.GetTopLevel(this) is { } top)
            top.AddHandler(PointerPressedEvent, OnOutsidePressed, RoutingStrategies.Tunnel);

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
    private static void OnNumberRow(object? sender, DataGridRowEventArgs e) =>
        e.Row.Header = RowNumber(e.Row);

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
        else MoveColumnFrom(cols, from, e.Key == Key.Right ? -1 : +1);

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
            if (x is DataGridColumnHeader) return;
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

        var cells = row.GetVisualDescendants().OfType<DataGridCell>().ToList();
        var idx = VisibleCols().IndexOf(col);
        if (idx < 0 || idx >= cells.Count) return null;

        // ⚠️ ‎Button.celltoggle‎ هم شمرده می‌شود، نه فقط کشویی و رادیویی.
        //
        // گزارشِ صاحب ریپو: «نوعِ تیل هم تو بخشِ قرض‌داران با تب یا اینتر عوض
        // نمی‌شه.» حق داشت و کارِ خودم بود: ستونِ «نوع تیل» تا دیروز دو دکمهٔ
        // رادیویی داشت و این‌جا شناخته می‌شد؛ وقتی به یک کپسول تبدیلش کردم،
        // از این فهرست افتاد و ‎Tab‎/‎Enter‎ دیگر کاری نمی‌کرد.
        return cells[idx].GetVisualDescendants()
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

    // ══════════════════════════════════════════════════════════════════════
    //  ══ کپی · برش · پیست · انتخابِ همه · برگشت و دوباره ═══════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ صاحب ریپو: «Ctrl+C / V / X و Ctrl+Z / Y و Ctrl+A هم باشند…
    //  همه‌شان با دقت و ظرافت کار کنند و هیچ بخشی منطقش خراب یا دست‌کاری
    //  نشود.»
    //
    //  ⛔ و دقیقاً به همین دلیل، **هیچ‌کدامشان درِ تازه‌ای به داده باز نمی‌کند.**
    //  هر نوشتنی از همان یک در رد می‌شود: <see cref="Write"/> رشته را در همان
    //  خاصیتِ ‎…Text‎ی ستون می‌نشاند — مو‌به‌مو همان کاری که تایپِ خودِ کاربر
    //  می‌کند. پس همان ‎Touch()‎، همان ‎Apply()‎، همان ذخیرهٔ تأخیری و همان
    //  جمع‌های بالای صفحه. هیچ محاسبه‌ای دور زده نمی‌شود و هیچ سرویسی از کنار
    //  صدا زده نمی‌شود.
    //
    //  ⛔ **ردیفِ قفل‌شده (‎ILockedRow‎) و ستونِ خواندنی نوشته نمی‌شوند** — همان
    //  قاعده‌ای که ‎BeginningEdit‎ برای تایپ دارد. پیش از این ‎Delete‎ این قید
    //  را نداشت و می‌شد ردیفِ 📦 خریدِ مخزن را خالی کرد؛ حالا هر چهار راه یک
    //  قاعده دارند.
    //
    //  ⚠️ **پیست ردیف نمی‌سازد.** آن‌چه از قابِ جدول بیرون بزند دور ریخته
    //  می‌شود. ساختنِ خودکارِ ردیف یعنی یک ‎Ctrl+V‎ی ناخواسته صدها ردیفِ واقعی
    //  در دفتر بسازد؛ ردیف با ‎Ctrl+عدد‎ یا دکمهٔ «➕ ردیف» ساخته می‌شود، آگاهانه.
    //
    //  ⚠️ **برگشت (‎Ctrl+Z‎) فقط «ویرایشِ خانه» را برمی‌گرداند** — تایپ، خالی
    //  کردن، برش و پیست. ساختن و حذفِ ردیف، حذفِ حساب و هر چیزی که در دیتابیس
    //  ردیف می‌سازد یا می‌برد **در آن نیست**: برگرداندنشان یک «تاریخچهٔ
    //  عکس‌فوریِ کلِ دیتابیس» می‌خواهد که ساخته نشده، و وانمود کردن به داشتنش
    //  از نداشتنش بدتر است. برای حذف، سطلِ زباله سرِ جایش هست.
    //
    //  ⚠️ و تاریخچه مالِ **همین جدول** است، نه سراسری: با عوض شدنِ فهرست (ماهِ
    //  دیگر، حسابِ دیگر) پاک می‌شود، و پیش از هر برگشت سنجیده می‌شود که آن
    //  ردیف هنوز در همین جدول هست (<see cref="Live"/>) — وگرنه نوشتن روی
    //  ردیفی که دیگر نیست، ذخیره‌ای می‌ساخت که هیچ‌کس نخواسته بود.

    /// <summary>یک خانهٔ عوض‌شده: چه بود و چه شد.</summary>
    private readonly record struct Edit(object Item, System.Reflection.PropertyInfo Prop,
                                        string? Before, string? After);

    private const int UndoDepth = 120;

    private readonly List<List<Edit>> _undo = new();
    private readonly List<List<Edit>> _redo = new();

    /// <summary>عکسِ خانه‌ای که همین حالا ویرایشش باز شد (برای ‎Ctrl+Z‎).</summary>
    private (object Item, System.Reflection.PropertyInfo Prop, string? Before)? _snap;

    /// <summary>فهرستِ زندهٔ همین جدول، به ترتیبِ خودش.</summary>
    private List<object> AllRows() =>
        ItemsSource?.Cast<object>().Where(o => o is not null).ToList() ?? new List<object>();

    /// <summary>این ردیف هنوز در همین جدول هست؟</summary>
    private bool Live(object item) =>
        ItemsSource is System.Collections.IEnumerable src
        && src.Cast<object>().Any(o => ReferenceEquals(o, item));

    /// <summary>فهرستِ تازه ⇒ تاریخچهٔ کهنه دیگر معنا ندارد.</summary>
    private void ForgetHistory() { _undo.Clear(); _redo.Clear(); _snap = null; }

    /// <summary>
    /// کادرِ انتخاب‌شده: ردیف‌ها (به ترتیبِ خودِ جدول، نه ترتیبِ انتخاب) و
    /// ستون‌های بینِ لنگر و سر. همان مستطیلی که ‎Delete‎ از روزِ اول خالی
    /// می‌کرد — حالا کپی و برش هم از همین می‌خوانند تا هر چهار کلید یک چیز
    /// ببینند.
    /// </summary>
    private (List<object> Rows, List<DataGridColumn> Cols)? Selection()
    {
        var cols = VisibleCols();
        if (cols.Count == 0) return null;

        var cur = CurIndex(cols);
        var lo = _colAnchor < 0 ? cur : Math.Min(_colAnchor, _colHead);
        var hi = _colAnchor < 0 ? cur : Math.Max(_colAnchor, _colHead);
        if (lo < 0 || hi < 0) return null;
        lo = Math.Max(0, lo); hi = Math.Min(cols.Count - 1, hi);

        var rows = SelectedItems.Cast<object>().Where(o => o is not null).ToList();
        if (rows.Count == 0 && SelectedItem is not null) rows.Add(SelectedItem);
        if (rows.Count == 0) return null;

        // ⚠️ ‎SelectedItems‎ ترتیبِ **انتخاب** را می‌دهد، نه ترتیبِ جدول. برای
        // کپی این فرق می‌کند: انتخاب از پایین به بالا، متنِ وارونه می‌داد.
        var all = AllRows();
        if (all.Count > 0)
            rows = rows.OrderBy(o => { var i = all.FindIndex(x => ReferenceEquals(x, o)); return i < 0 ? int.MaxValue : i; })
                       .ToList();

        return (rows, cols.GetRange(lo, hi - lo + 1));
    }

    /// <summary>خواندنِ نوشتهٔ یک خانه — برای کپی. ستونِ خواندنی هم خوانده می‌شود.</summary>
    private static string ReadCell(object item, DataGridColumn col)
    {
        var path = PathOf(col);
        if (string.IsNullOrEmpty(path)) return "";
        var p = item.GetType().GetProperty(path);
        if (p is null || !p.CanRead) return "";
        try { return p.GetValue(item)?.ToString() ?? ""; } catch { return ""; }
    }

    /// <summary>
    /// نوشتنِ یک خانه — <b>تنها</b> جایی که کپی/برش/پیست/‎Delete‎ به داده دست
    /// می‌زنند. برمی‌گرداند که چیزی عوض شد یا نه، و تغییر را در ‎log‎ می‌نویسد.
    /// </summary>
    private static bool Write(object item, DataGridColumn col, string value, List<Edit> log)
    {
        if (col.IsReadOnly) return false;
        if (item is ViewModels.ILockedRow { IsLocked: true }) return false;
        var path = PathOf(col);
        if (string.IsNullOrEmpty(path)) return false;
        var p = item.GetType().GetProperty(path);
        if (p is null || !p.CanWrite || p.PropertyType != typeof(string)) return false;

        var before = p.GetValue(item) as string;
        if (string.Equals(before, value, StringComparison.Ordinal)) return false;
        try { p.SetValue(item, value); } catch { return false; }
        log.Add(new Edit(item, p, before, value));
        return true;
    }

    private void PushUndo(List<Edit> group)
    {
        if (group.Count == 0) return;
        _undo.Add(group);
        if (_undo.Count > UndoDepth) _undo.RemoveAt(0);
        _redo.Clear();            // شاخهٔ تازه ⇒ «دوباره»ی کهنه بی‌معنا شد
    }

    // ── عکسِ پیش و پسِ تایپِ خودِ کاربر ────────────────────────────────────
    //
    // ⚠️ عکس در ‎BeginningEdit‎ گرفته می‌شود، نه در ‎CellEditEnding‎: آن‌جا
    // مطمئنیم مقدار هنوز کهنه است. و مقایسه یک تیک **بعد** از پایانِ ویرایش
    // انجام می‌شود، چون نشستنِ مقدار در ردیف کارِ خودِ اتصال است و ممکن است
    // همان لحظه نباشد.
    private void SnapEdit(DataGridColumn? col, DataGridRow? row)
    {
        _snap = null;
        var path = PathOf(col);
        if (string.IsNullOrEmpty(path)) return;
        if (row?.DataContext is not { } item) return;
        var p = item.GetType().GetProperty(path);
        if (p is null || !p.CanWrite || p.PropertyType != typeof(string)) return;
        try { _snap = (item, p, p.GetValue(item) as string); } catch { _snap = null; }
    }

    private void CaptureEdit()
    {
        if (_snap is not { } s) return;
        _snap = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (!Live(s.Item)) return;
            string? after;
            try { after = s.Prop.GetValue(s.Item) as string; } catch { return; }
            if (string.Equals(s.Before, after, StringComparison.Ordinal)) return;
            PushUndo(new List<Edit> { new(s.Item, s.Prop, s.Before, after) });
        }, DispatcherPriority.Background);
    }

    private bool Replay(List<Edit> group, bool forward)
    {
        var any = false;
        // وارونه، تا اگر دو خانه روی هم نوشته شده بودند ترتیب برعکس باز شود
        for (var i = group.Count - 1; i >= 0; i--)
        {
            var ed = group[i];
            if (!Live(ed.Item)) continue;
            try { ed.Prop.SetValue(ed.Item, forward ? ed.After : ed.Before); any = true; } catch { }
        }
        return any;
    }

    /// <summary>‎Ctrl+Z‎ — آخرین ویرایشِ خانه را برمی‌گرداند.</summary>
    private bool UndoEdit()
    {
        while (_undo.Count > 0)
        {
            var g = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            if (!Replay(g, forward: false)) continue;   // ردیفش دیگر نیست ⇒ رد شو
            _redo.Add(g);
            return true;
        }
        return false;
    }

    /// <summary>‎Ctrl+Y‎ (یا ‎Ctrl+Shift+Z‎) — همان را دوباره انجام می‌دهد.</summary>
    private bool RedoEdit()
    {
        while (_redo.Count > 0)
        {
            var g = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            if (!Replay(g, forward: true)) continue;
            _undo.Add(g);
            return true;
        }
        return false;
    }

    // ── کلیپ‌بورد ─────────────────────────────────────────────────────────

    // ⚠️ نامش ‎Clip‎ نیست: ‎Visual.Clip‎ از پیش هست و هم‌نامی آن را پنهان می‌کرد.
    private IClipboard? Board => TopLevel.GetTopLevel(this)?.Clipboard;

    /// <summary>کادرِ انتخابی به‌شکلِ جدولِ Tab-دار — همان چیزی که اکسل می‌فهمد.</summary>
    private string? SelectionText()
    {
        if (Selection() is not { } r) return null;
        var sb = new System.Text.StringBuilder();
        for (var y = 0; y < r.Rows.Count; y++)
        {
            if (y > 0) sb.Append('\n');
            for (var x = 0; x < r.Cols.Count; x++)
            {
                if (x > 0) sb.Append('\t');
                sb.Append(ReadCell(r.Rows[y], r.Cols[x]));
            }
        }
        return sb.ToString();
    }

    private async Task CopySelectionAsync()
    {
        if (SelectionText() is not { } text) return;
        if (Board is not { } clip) return;
        try { await clip.SetTextAsync(text); } catch { }
    }

    private async Task CutSelectionAsync()
    {
        if (IsReadOnly) return;
        await CopySelectionAsync();
        ClearSelectedCells();
    }

    private async Task PasteAsync()
    {
        if (IsReadOnly) return;
        if (Board is not { } clip) return;

        string? text = null;
        try { text = await clip.GetTextAsync(); } catch { }
        if (string.IsNullOrEmpty(text)) return;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var cells = lines.Select(l => l.Split('\t')).ToList();
        // خطِ خالیِ آخر (هر کپی از اکسل یکی دارد) شمرده نشود
        while (cells.Count > 1 && cells[^1].Length == 1 && cells[^1][0].Length == 0)
            cells.RemoveAt(cells.Count - 1);
        if (cells.Count == 0) return;

        var cols = VisibleCols();
        var all = AllRows();
        if (cols.Count == 0 || all.Count == 0) return;

        var log = new List<Edit>();

        // ⚠️ یک خانه روی یک کادرِ چندتایی ⇒ همان یک مقدار در همهٔ خانه‌ها —
        // همان کاری که اکسل می‌کند و کاربر انتظارش را دارد.
        if (cells.Count == 1 && cells[0].Length == 1 && Selection() is { } r
            && (r.Rows.Count > 1 || r.Cols.Count > 1))
        {
            foreach (var item in r.Rows)
                foreach (var col in r.Cols)
                    Write(item, col, cells[0][0], log);
        }
        else
        {
            var startCol = Math.Max(0, CurIndex(cols));
            var startRow = SelectedItem is { } cur
                ? Math.Max(0, all.FindIndex(x => ReferenceEquals(x, cur)))
                : 0;

            for (var y = 0; y < cells.Count; y++)
            {
                var ri = startRow + y;
                if (ri >= all.Count) break;               // بیرونِ جدول ⇒ دور ریخته می‌شود
                for (var x = 0; x < cells[y].Length; x++)
                {
                    var ci = startCol + x;
                    if (ci >= cols.Count) break;
                    Write(all[ri], cols[ci], cells[y][x], log);
                }
            }
        }

        if (log.Count == 0) return;
        PushUndo(log);
        AppHost.Current.Toasts.Show($"📋 {log.Count} خانه چسبانده شد", ToastKind.Ok);
    }

    /// <summary>‎Ctrl+A‎ — همهٔ ردیف‌ها و همهٔ ستون‌ها.</summary>
    private void SelectAllCells()
    {
        var cols = VisibleCols();
        if (cols.Count == 0) return;
        SelectAll();
        _colAnchor = 0;
        _colHead = cols.Count - 1;
        PaintRange();
    }

    /// <summary>
    /// ویرایشِ بازِ همین جدول را همین حالا می‌نشاند — پیش از ‎Ctrl+S‎، وگرنه
    /// خانه‌ای که کاربر وسطش بود ذخیره نمی‌شد.
    /// </summary>
    public void CommitNow()
    {
        if (_editing) CommitEdit(DataGridEditingUnit.Cell, true);
    }

    /// <summary>
    /// خانهٔ بازِ جدولی که همین حالا فوکوس دارد را می‌نشاند — و بعد ردیفش را
    /// روی دیسک.
    ///
    /// ⚠️ از روی <b>فوکوس</b> پیدا می‌شود، نه از ویومدل: یک صفحه می‌تواند چند
    /// جدول داشته باشد (ورق سه تا دارد) و فقط آن یکی که کاربر داخلش است
    /// ویرایشِ باز دارد.
    ///
    /// ⛔ تنها جای این کار است: <c>Ctrl+S</c> و بسته شدنِ برنامه هر دو از
    /// همین‌جا می‌آیند. دو نسخه یعنی روزی یکی‌شان از قلم می‌افتد — و آن یکی
    /// دقیقاً همان است که کاربر وسطِ تایپ ✕ را می‌زند.
    /// </summary>
    public static void CommitFocused(TopLevel? top)
    {
        if (top?.FocusManager?.GetFocusedElement() is not Visual v) return;
        var grid = v.FindAncestorOfType<ExcelGrid>();
        if (grid is null) return;
        grid.CommitNow();
        grid.FlushDirtyRows();
    }

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
            if (e.Key == Key.Z && !shift) { if (UndoEdit()) e.Handled = true; return; }
            if (e.Key == Key.Y || (e.Key == Key.Z && shift)) { if (RedoEdit()) e.Handled = true; return; }
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
                MoveColumn(e.Key == Key.Right ? -1 : +1, shift);
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
        if (IsReadOnly || _editing || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
        {
            base.OnTextInput(e);
            return;
        }

        BeginEdit();
        _typedIn = true;   // با تایپ آمدیم ⇒ فلش‌ها ناوبری‌اند، نه کُرسر

        var box = CurrentEditor();
        if (box is null) { base.OnTextInput(e); return; }

        box.Text = e.Text;
        box.CaretIndex = e.Text.Length;
        box.Focus();
        e.Handled = true;
    }

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
}
