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

//  ⛔ شورا ج۴: بخشی از ‎ExcelGrid‎ — پهنای ستون‌ها: دوبار-کلیک، پخش، به‌یادسپاری، جای خالیِ ته ردیف. فقط جابه‌جاییِ همان عضوها از ‎ExcelGrid.cs‎، بی تغییرِ یک رفتار.
public partial class ExcelGrid
{
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

    /// <summary>شمارِ ردیف‌ها وقتی پهنا چیده شد — صفر یعنی روی جدولِ خالی.</summary>
    private int _spreadRows;

    private void SpreadColumns()
    {
        RememberDeclared();
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

        //  ⛔ جای **خانه‌ها** — قاب منهای ستونِ شمارهٔ ردیف (‎#‎) و دو خطِ لبه.
        //  تا ۱۴۰۵/۰۷/۱۲ این‌جا خودِ ‎Bounds.Width‎ بود، پس ستون‌ها درست به
        //  اندازهٔ ستونِ «#» (~۴۰ پیکسل) از کادر بیرون می‌زدند: «یادداشت» و
        //  کپسول‌های «نوع» و «واحد»ِ ورق نیمه‌بریده دیده می‌شدند (عکسِ صاحب
        //  ریپو). ‎StarFloors‎ همین را از روزِ اول کم می‌کرد و این یکی نه.
        var room = CellRoom();
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
        var wanted = (double[])natural.Clone();
        if (!spare) natural = FitToRoom(natural, room);

        // ══ پهنای ذخیره‌شده مقدم است ═════════════════════════════════════════
        // اگر کاربر یک بار این جدول را تنظیم کرده، همان می‌نشیند — نه پهنای
        // طبیعیِ محتوای امروز. پس ورقِ فردا هم همان‌قدر است.
        var saved = Saved(cols.Count);
        //  ⛔ «دیوار»: پهنای ذخیره‌شده‌ای که در این قاب جا نمی‌شود کوچک نشان
        //  داده می‌شود — روی دیسک همان می‌ماند (‎_autoWidths‎ همین عددِ کوچک
        //  است، پس ‎RememberWidths‎ چیزی نمی‌نویسد).
        if (saved is not null) wanted = (double[])saved.Clone();
        if (saved is not null && KeepInside && saved.Sum() > room + 0.5)
            saved = FitToRoom(saved, room, Numeric(cols));
        _fullWidths = saved is not null || !spare ? wanted : null;

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

        // ⛔ **خطِ پایهٔ «خودکار»** — همان چیزی که ‎RememberWidths‎ با آن
        // می‌فهمد پهنایی را کاربر ساخته یا خودِ برنامه. در حالتِ پیکسلی همین
        // عددهای طبیعی‌اند و در حالتِ ذخیره‌شده همان عددهای ذخیره‌شده. (در
        // حالتِ ستاره‌ای پهنا به قاب بند است و ‎PinOnUserResize‎ آن را پیش از
        // نخستین کشیدنِ کاربر می‌گیرد.)
        // بی این، جدولِ پیکسلی نخستین پهنای خودکارش را «خواستهٔ کاربر» ذخیره
        // می‌کرد و دفعهٔ بعد همان جای پهنای واقعیِ کاربر می‌نشست.
        _autoWidths = saved is not null ? (double[])saved.Clone()
                    : spare ? null : (double[])natural.Clone();
        _starBase = null;
        _starNatural = saved is null && spare ? (double[])natural.Clone() : null;
        if (_starNatural is not null) StarFloors(room);

        _spreadRows = RowCount();
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
        var scope = WidthScope;
        var tail = string.IsNullOrWhiteSpace(scope) ? "" : "@" + scope;
        var k = WidthKey;
        if (!string.IsNullOrWhiteSpace(k)) return k + tail;
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
        return _autoKey = host + "." + mine + "#" + visible + tail;
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
    /// ══ پنجره کوچک شد و ستون‌ها ستاره‌ای‌اند ═══════════════════════════════
    ///
    /// ستونِ ستاره‌ای با قاب **به نسبت** کوچک می‌شود — یعنی روی پنجرهٔ ۱۱۰۰
    /// پیکسلی شروع و ختمِ پایه‌ها «۱,۲۳۴,…» می‌شدند در حالی که ستونِ نام و
    /// یادداشت هنوز جای خالی داشتند (سنجهٔ ‎audit11‎). پس کفِ هر ستون همان
    /// سهمی است که ‎FitToRoom‎ برای این قاب می‌دهد: تا جای کافی هست کف همان
    /// پهنای طبیعی است (هیچ خانه‌ای «…» نمی‌شود)، و وقتی نیست فقط ستون‌های
    /// پهن کوتاه می‌شوند. ستاره‌ها بقیهٔ جا را مثلِ همیشه پخش می‌کنند.
    ///
    /// ⚠️ فقط پهنای **خودکار**: جدولی که کاربر ستونش را کشیده (پیکسلی) یا
    /// پهنای ذخیره‌شده دارد این‌جا نمی‌رسد (‎_starNatural‎ خالی است).
    /// </summary>
    private void StarFloors(double room)
    {
        if (_starNatural is not { } nat || room <= 0) return;
        var cols = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (cols.Count != nat.Length
            || cols.Any(c => c.Width.UnitType != DataGridLengthUnitType.Star)) { _starNatural = null; return; }
        //  جای خانه‌ها = قاب منهای ستونِ شماره (‎#‎) و دو خطِ لبه — وگرنه جمعِ
        //  کف‌ها به اندازهٔ همان ستون از قاب بیرون می‌زد و نوارِ لغزشِ افقی
        //  برمی‌گشت (با عکس دیده شد).
        var fit = FitToRoom(nat, room, Numeric(cols));
        for (var i = 0; i < cols.Count; i++)
            if (Math.Abs(cols[i].MinWidth - fit[i]) >= 0.5) cols[i].MinWidth = Math.Max(FloorWidth, fit[i]);
    }

    private double[]? _starNatural;

    /// <summary>
    /// جای خانه‌ها: قابِ جدول منهای ستونِ شمارهٔ ردیف (‎#‎) و دو خطِ لبه.
    /// ⛔ هر حسابِ «جا می‌شود؟» از همین است — ستونِ «#» هم جا می‌گیرد.
    /// </summary>
    // ══ خطِ عمودیِ آخرین ستون — همان لحظه که پهناها عوض شد (۱۴۰۵/۰۷/۱۹) ══
    //
    //  سنجهٔ ‎themeflip‎ در ورقِ روز «۱ پیکسل جابه‌جایی پس از تعویضِ تم» نشان
    //  می‌داد. ریشه تم نبود: ‎DataGrid‎ی آوالونیا خطِ راستِ <b>آخرین ستون</b> را
    //  فقط وقتی می‌کشد که ستون‌ها قاب را پر نکرده‌اند (ستونِ پُرکننده فعال
    //  است)، و این تصمیم را هنگامِ ساختنِ هر خانه می‌گیرد. ‎SpreadColumns‎ پس از
    //  آن ستون‌ها را به اندازهٔ قاب می‌کند ولی خانه‌های ساخته‌شده خبردار نمی‌شوند،
    //  پس آخرین ستون یک خطِ اضافه و ۱ پیکسل کمتر جا داشت — تا اولین
    //  بی‌اعتبارسازیِ بزرگ (همان تعویضِ تم). حالا هر بار که جمعِ پهناها عوض
    //  شد، خط‌ها از همان راهِ خودِ ‎DataGrid‎ دوباره سنجیده می‌شوند
    //  (‎GridLinesVisibility‎). ⚡ فقط با عوض شدنِ پهنا، و فقط ردیف‌های زنده.
    private double _gridLineSig = double.NaN;

    private void EnsureGridLinesForWidths()
    {
        if (!_spread) return;
        var v = GridLinesVisibility;
        if (!v.HasFlag(DataGridGridLinesVisibility.Vertical)) return;
        double sig = 0; var n = 0;
        foreach (var c in Columns)
            if (c.IsVisible) { var w = c.ActualWidth; if (double.IsNaN(w)) return; sig += w; n++; }
        sig += n * 100000 + Math.Round(CellRoom());
        if (Math.Abs(sig - _gridLineSig) < 0.5) return;
        var first = double.IsNaN(_gridLineSig);
        _gridLineSig = sig;
        if (first && RowCount() == 0) return;
        SetCurrentValue(GridLinesVisibilityProperty, DataGridGridLinesVisibility.Horizontal);
        SetCurrentValue(GridLinesVisibilityProperty, v);
    }

    private double CellRoom()
    {
        var head = HeadersVisibility.HasFlag(DataGridHeadersVisibility.Row) && !double.IsNaN(RowHeaderWidth)
            ? RowHeaderWidth : 0;
        return Bounds.Width - head - 4;
    }

    /// <summary>پهنای ستون‌ها لحظهٔ دست گذاشتن روی سرستون — تا بدانیم کدام کشیده شد.</summary>
    private double[]? _pressWidths;

    /// <summary>دستِ کاربر هنوز روی سرستون است (وسطِ کشیدن).</summary>
    private bool _headerDown;

    /// <summary>
    /// «دیوار» پس از کشیدنِ ستون: ستونی که کاربر پهن کرد همان می‌ماند و جا را
    /// از ستون‌های **دیگر** می‌گیرد (پهن‌ترها اول)؛ اگر آن‌ها هم به کفِ
    /// خوانایی رسیدند، خودِ ستونِ کشیده‌شده تا لبهٔ قاب کوتاه می‌شود.
    /// ⚠️ وسطِ کشیدن کاری نمی‌کند — ستون نباید زیرِ دستِ کاربر بپرد.
    /// </summary>
    /// <summary>
    /// پهنایی که جدول **می‌خواهد** (پهنای طبیعی یا ذخیره‌شدهٔ کاربر) پیش از
    /// آن‌که برای جا شدن کوچک شود — تا پنجره که بزرگ شد، ستون‌ها دوباره تا
    /// همان‌جا باز شوند، نه این‌که کوچک بمانند و کنارِ جدول خالی شود.
    /// </summary>
    private double[]? _fullWidths;

    /// <returns>پهنایی عوض شد؟ — آن‌وقت ‎RememberWidths‎ همین پاس نمی‌نویسد.</returns>
    // ══════════════════════════════════════════════════════════════════════
    //  ══ «جای خالیِ ته ردیف» — ستونِ پهن پرش می‌کند (۱۴۰۵/۰۷/۱۳) ══════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو با عکس: «آپدیتِ تازه در جدول‌ها یک جای خالی گذاشته —
    //  نه ستونِ پنهان باشد نه جای خالی.»
    //
    //  ⛔ ریشه: از ۳.۱.۱۵۸ هر جدولی پهنای کشیده‌شدهٔ کاربر را یادش می‌ماند، و
    //  پهنای ذخیره‌شده **پیکسلی** است. در قابِ پهن‌تر (پنجرهٔ بزرگ‌تر، یا
    //  ستونی که کاربر باریک کرد) جمعِ ستون‌ها از قاب کمتر است و تهِ هر ردیف
    //  یک نوارِ خالی می‌ماند. با پهنای خودکار این نبود، چون ستاره‌ای بود.
    //
    //  حالا همان فاصله به **ستونِ پهنِ خودِ جدول** داده می‌شود — همانی که در
    //  XAML ستاره‌ای تعریف شده (نام، شرح، یادداشت)، و اگر نبود آخرین ستون.
    //  ⚠️ فقط برای دیدن: اگر ستون‌ها همان پهنای خودکار/ذخیره‌شده بودند،
    //  ‎_autoWidths‎ هم با آن جلو می‌رود تا ‎RememberWidths‎ این پُرکردن را
    //  «خواستهٔ کاربر» روی دیسک ننویسد.
    //  ⚠️ وسطِ کشیدنِ سرستون کاری نمی‌کند (‎_headerDown‎)، وگرنه ستون زیرِ
    //  دستِ کاربر می‌پرید.

    /// <summary>ستون‌هایی که در XAML ستاره‌ای تعریف شده‌اند — یک بار، پیش از هر سفت کردن.</summary>
    private bool[]? _declaredStar;

    private void RememberDeclared()
    {
        if (_declaredStar is null && Columns.Count > 0)
            _declaredStar = Columns.Select(c => c.Width.IsStar).ToArray();
    }

    private int FillerIndex(List<DataGridColumn> cols)
    {
        if (_declaredStar is { } ds)
        {
            var best = -1;
            for (var i = 0; i < cols.Count; i++)
            {
                var at = Columns.IndexOf(cols[i]);
                if (at >= 0 && at < ds.Length && ds[at] && (best < 0 || cols[i].ActualWidth > cols[best].ActualWidth))
                    best = i;
            }
            if (best >= 0) return best;
        }
        return cols.Count - 1;
    }

    private bool FillGapStep()
    {
        if (!_spread || _headerDown || !IsEffectivelyVisible) return false;
        var cols = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (cols.Count == 0 || cols.Any(c => c.Width.UnitType != DataGridLengthUnitType.Pixel)) return false;
        var w = cols.Select(c => c.ActualWidth).ToArray();
        if (w.Any(x => double.IsNaN(x) || x <= 0)) return false;
        var room = CellRoom();
        var gap = room - w.Sum();
        if (room <= 0 || gap < 1.5) return false;

        var auto = _autoWidths is { } aw && aw.Length == w.Length
                   && aw.Zip(w, (x, y) => Math.Abs(x - y) < 0.5).All(z => z);
        var f = FillerIndex(cols);
        w[f] += Math.Floor(gap);
        cols[f].Width = new DataGridLength(w[f], DataGridLengthUnitType.Pixel);
        if (auto) _autoWidths = w;
        if (_fullWidths is { } full && full.Length == w.Length && full.Sum() < room) _fullWidths = (double[])w.Clone();
        return true;
    }

    /// <summary>
    /// ══ کشیدنِ خطِ ستون = جابه‌جا کردنِ مرزِ دو ستون (۱۴۰۵/۰۷/۱۷، دوم) ══════
    ///
    /// گزارشِ صاحب ریپو با عکس: «بزرگ یا کوچک کردن این مدل باشه که نه جای خالی
    /// به وجود بیاد نه اون طرف بره که دیده نشن… توی همون کادرشون همون اندازه که
    /// دیده میشه کوچیک و بزرگ بشن.» تا امروز «دیوار» فقط <b>پس از</b> رها کردن
    /// می‌سنجید؛ وسطِ کشیدن ستون‌های آن‌طرف از کادر بیرون می‌زدند (پهن کردن) یا
    /// کنارِ جدول خالی می‌ماند (باریک کردن).
    ///
    /// حالا وسطِ کشیدن هم: هر چه ستونِ کشیده‌شده پهن‌تر شود، <b>ستونِ همسایه‌اش</b>
    /// (همان‌که آن‌طرفِ خط است) به همان اندازه باریک‌تر می‌شود و برعکس — پس
    /// جمعِ پهناها همان است که لحظهٔ دست گذاشتن بود. همسایه که به کفِ خوانایی
    /// رسید، ستونِ کشیده‌شده دیگر پهن‌تر نمی‌شود. ستون‌های دیگر دست نمی‌خورند.
    /// ⚠️ فقط وقتی همهٔ ستون‌ها پیکسلی‌اند (پس از سنجاق شدن) — همان شرطِ دیوار.
    /// </summary>
    private void DragStep()
    {
        if (!KeepInside || !_headerDown || _pressWidths is not { } pw) return;
        var cols = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (cols.Count < 2 || pw.Length != cols.Count
            || cols.Any(c => c.Width.UnitType != DataGridLengthUnitType.Pixel)) return;
        var w = cols.Select(c => c.Width.Value).ToArray();
        if (w.Any(x => double.IsNaN(x) || x <= 0) || pw.Any(x => double.IsNaN(x) || x <= 0)) return;

        //  کدام ستون زیرِ دست است؟ — همانی که بیشترین فاصله را با لحظهٔ دست گذاشتن دارد
        //  و همسایه‌اش را خودمان عوض نکرده‌ایم.
        var grab = -1;
        var most = 0.5;
        for (var i = 0; i < w.Length; i++)
        {
            if (i == _dragNeighbor) continue;
            var d = Math.Abs(w[i] - pw[i]);
            if (d > most) { most = d; grab = i; }
        }
        if (grab < 0) return;
        var nb = _dragGrab == grab && _dragNeighbor >= 0 ? _dragNeighbor
               : grab + 1 < w.Length ? grab + 1 : grab - 1;
        _dragGrab = grab; _dragNeighbor = nb;

        var pair = pw[grab] + pw[nb];
        var g = Math.Clamp(w[grab], FloorWidth, pair - FloorWidth);
        var n = pair - g;
        var changed = false;
        void Set(int i, double v)
        {
            if (Math.Abs(cols[i].Width.Value - v) < 0.5) return;
            cols[i].Width = new DataGridLength(v, DataGridLengthUnitType.Pixel);
            changed = true;
        }
        Set(grab, g);
        Set(nb, n);
        for (var i = 0; i < w.Length; i++)
            if (i != grab && i != nb) Set(i, pw[i]);
        if (changed) InvalidateMeasure();
    }

    //  جفتِ ستونی که همین کشیدن جابه‌جا می‌کند — با هر دست گذاشتنِ تازه صفر می‌شود.
    private int _dragGrab = -1, _dragNeighbor = -1;

    private bool KeepInsideStep()
    {
        if (!KeepInside || !_spread || _headerDown) return false;
        var cols = Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        if (cols.Count == 0 || cols.Any(c => c.Width.UnitType != DataGridLengthUnitType.Pixel)) return false;
        var w = cols.Select(c => c.ActualWidth).ToArray();
        if (w.Any(x => double.IsNaN(x) || x <= 0)) return false;
        var room = CellRoom();
        if (room <= 0) return false;

        //  ⚡ همان ورودی‌هایی که بارِ پیش به «چیزی عوض نمی‌شود» رسیدند ⇒ هیچ
        //  کاری. این تابع با **هر** پاسِ چیدمان (هر گامِ چرخ) صدا می‌خورد و از
        //  ۱۴۰۵/۰۷/۱۷ روی همهٔ جدول‌هاست؛ ‎Numeric‎ خانه‌ها را می‌خواند — بی این
        //  ترمز، گامِ چرخ روی جدولِ ۵۰ ردیفی از ۱۲۰ms گذشت (‎bigtable‎ گرفتش).
        if (_pressWidths is null && _kiLast is { } kl && kl.Length == w.Length
            && Math.Abs(_kiRoom - room) < 0.5
            && ReferenceEquals(_kiFull, _fullWidths) && ReferenceEquals(_kiAuto, _autoWidths)
            && kl.Zip(w, (x, y) => Math.Abs(x - y) < 0.5).All(z => z))
            return false;

        //  کدام ستون کشیده شد؟ — همانی که از لحظهٔ دست گذاشتن بیشتر پهن شد
        var grab = -1;
        if (_pressWidths is { } pw && pw.Length == w.Length)
        {
            var best = 1.0;
            for (var i = 0; i < w.Length; i++)
                if (w[i] - pw[i] > best) { best = w[i] - pw[i]; grab = i; }
        }
        _pressWidths = null;

        //  کاربر خودش چیزی کشید ⇒ از این به بعد همین «خواسته» است
        var auto = _autoWidths is { } aw && aw.Length == w.Length
                   && aw.Zip(w, (x, y) => Math.Abs(x - y) < 0.5).All(z => z);
        if (!auto && grab < 0 && w.Sum() <= room + 1) { _fullWidths = null; Remember(room, w); return false; }

        double[] fit;
        if (grab >= 0)
        {
            var others = w.Where((_, i) => i != grab).ToArray();
            var left = Math.Max(0, room - w[grab]);
            var o = WaterFill(others, left, FloorWidth);
            fit = new double[w.Length];
            for (int i = 0, j = 0; i < w.Length; i++) fit[i] = i == grab ? w[i] : o[j++];
            var over = fit.Sum() - room;
            if (over > 0) fit[grab] = Math.Max(FloorWidth, fit[grab] - over);
            _fullWidths = (double[])fit.Clone();
        }
        else if (_fullWidths is { } full && full.Length == w.Length && auto)
            fit = FitToRoom(full, room, Numeric(cols));   // هم کوچک شدن، هم باز شدن تا خواسته
        else if (w.Sum() > room + 1)
            fit = FitToRoom(w, room, Numeric(cols));
        else { Remember(room, w); return false; }

        var changed = false;
        for (var i = 0; i < cols.Count; i++)
            if (Math.Abs(cols[i].ActualWidth - fit[i]) >= 0.5)
            {
                cols[i].Width = new DataGridLength(fit[i], DataGridLengthUnitType.Pixel);
                changed = true;
            }
        //  ⛔ پنجره عوض شد (کسی ستونی نکشید) ⇒ این پهنای **خودکار** است و نباید
        //  جای پهنای ذخیره‌شدهٔ کاربر روی دیسک بنشیند؛ روی نمایشگرِ بزرگ‌تر همان
        //  پهنای خودش برمی‌گردد. کشیدنِ واقعیِ کاربر (‎grab‎) مثلِ همیشه ذخیره می‌شود.
        if (changed && grab < 0) _autoWidths = (double[])fit.Clone();
        Remember(room, changed ? null : w);
        return changed;
    }

    //  ترمزِ ‎KeepInsideStep‎: آخرین ورودی‌ای که «هیچ تغییری» داد.
    private double _kiRoom = -1;
    private double[]? _kiLast, _kiFull, _kiAuto;

    private void Remember(double room, double[]? widths)
    {
        _kiRoom = room;
        _kiLast = widths;
        _kiFull = _fullWidths;
        _kiAuto = _autoWidths;
    }

    /// <summary>
    /// کدام ستون‌ها عدد نشان می‌دهند؟ — از نوشتهٔ خانه‌های ساخته‌شدهٔ همان
    /// ستون (چند ردیفِ اول). عددِ بریده در دفترِ حساب از نامِ بریده بدتر است،
    /// پس وقتی جا کم است اول نام و یادداشت کوتاه می‌شوند.
    /// </summary>
    private bool[] Numeric(List<DataGridColumn> cols)
    {
        var flags = new bool[cols.Count];
        if (ItemsSource is not System.Collections.IEnumerable src) return flags;
        var items = src.Cast<object?>().Take(6).Where(x => x is not null).ToList();
        for (var i = 0; i < cols.Count; i++)
        {
            int num = 0, seen = 0;
            foreach (var it in items)
            {
                var t = IssueTextColumn.TextOf(cols[i].GetCellContent(it!))?.Text;
                if (string.IsNullOrWhiteSpace(t)) continue;
                seen++;
                var digits = t.Count(char.IsDigit);
                if (digits > 0 && digits * 2 >= t.Count(ch => !char.IsWhiteSpace(ch))) num++;
            }
            flags[i] = seen > 0 && num * 2 >= seen;
        }
        return flags;
    }

    /// <summary>
    /// کاربر دستش را روی سرستون گذاشت (شاید برای کشیدنِ خطِ ستون) ⇒ کفِ
    /// «پهنای طبیعی» برداشته می‌شود، وگرنه هیچ ستونی را نمی‌شد باریک‌تر از
    /// محتوایش کشید. وزنِ ستاره‌ها همان پهنای همین لحظه می‌شود، پس هیچ ستونی
    /// زیرِ دستِ کاربر نمی‌پرد.
    /// </summary>
    private void ReleaseStarFloors()
    {
        if (_starNatural is null) return;
        _starNatural = null;
        var cols = Columns.Where(c => c.IsVisible).ToList();
        if (cols.Any(c => double.IsNaN(c.ActualWidth) || c.ActualWidth <= 0)) return;
        foreach (var c in cols)
        {
            if (c.Width.UnitType == DataGridLengthUnitType.Star)
                c.Width = new DataGridLength(c.ActualWidth, DataGridLengthUnitType.Star);
            c.MinWidth = FloorWidth;
        }
    }

    /// <summary>
    /// پهناها را به اندازهٔ قاب کوچک می‌کند — **از پهن‌ترین ستون‌ها**، نه به
    /// نسبت، و نه زیرِ کفِ خوانایی.
    ///
    /// ⛔ یک بار «به نسبت» بود و سنجهٔ ‎audit11‎ گرفتش: روی پنجرهٔ ۱۱۰۰
    /// پیکسلی، ورق جا می‌شد ولی **هر** خانه یک تکه کوتاه می‌شد و شروع و ختمِ
    /// پایه‌ها «۱,۲۳۴,…» می‌شدند — در یک دفترِ حساب، عددِ بریده از جدولی که
    /// اسکرول می‌خورد بدتر است. ستون‌های پهن (نام، یادداشت) همان‌هایی‌اند که
    /// جای اضافه دارند؛ پس یک سقفِ مشترک پیدا می‌شود که فقط **آن‌ها** را
    /// پایین می‌آورد و ستونِ باریکِ عددی دست‌نخورده می‌ماند.
    ///
    /// ⚠️ اگر حتی با کفِ همه باز هم جا نشد، همان بیرون‌زدگی می‌ماند و جدول
    /// افقی می‌لغزد — چاره‌ای نیست.
    /// </summary>
    private static double[] FitToRoom(double[] natural, double room, bool[]? keep = null)
    {
        var sum = natural.Sum();
        if (sum <= room || room <= 0) return natural;

        //  ستونِ عددی تا جایی دست نمی‌خورد که ستون‌های نوشته‌ای (نام، یادداشت)
        //  جا بدهند — هر کدام تا دو برابرِ کف، نه صفر.
        if (keep is { } k && k.Length == natural.Length && k.Any(x => x) && k.Any(x => !x))
        {
            var fixedSum = natural.Where((_, i) => k[i]).Sum();
            var text = natural.Where((_, i) => !k[i]).ToArray();
            var textRoom = room - fixedSum;
            var minText = text.Sum(x => Math.Min(x, FloorWidth * 2));
            if (textRoom >= minText)
            {
                var t = WaterFill(text, textRoom, FloorWidth * 2);
                var r = new double[natural.Length];
                for (int i = 0, j = 0; i < r.Length; i++) r[i] = k[i] ? natural[i] : t[j++];
                return r;
            }
        }
        return WaterFill(natural, room, FloorWidth);
    }

    private static double[] WaterFill(double[] natural, double room, double floor)
    {
        if (natural.Sum() <= room) return natural;

        //  سقفِ c را طوری پیدا کن که Σ min(wᵢ, max(c, کف)) = قاب (جست‌وجوی دودویی)
        double Fit(double c) => natural.Sum(x => Math.Min(x, Math.Max(c, floor)));
        if (Fit(floor) > room) return natural.Select(x => Math.Min(x, floor)).ToArray();
        double lo = floor, hi = natural.Max();
        for (var k = 0; k < 40; k++)
        {
            var mid = (lo + hi) / 2;
            if (Fit(mid) > room) hi = mid; else lo = mid;
        }
        return natural.Select(x => Math.Min(x, lo)).ToArray();
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
    // ══ «اندازه‌ام ثبت نمی‌شد» — ریشهٔ دوم (۱۴۰۵/۰۷/۱۲) ════════════════
    //
    // ⛔ تا جدول پهنایش را خودش نچیده (‎_spread‎)، هیچ چیزی ذخیره
    // نمی‌شود. سنجهٔ ‎persist widths‎ گرفتش: پس از پر شدنِ دوبارهٔ فهرست
    // (ماهِ دیگر، حسابِ دیگر، باز شدنِ دوبارهٔ برنامه) ‎SpreadColumns‎
    // عمداً یک پاس صبر می‌کند (‎_freshCols‎)، ولی همین تابع در **همان**
    // پاس پهنای ‎Auto‎ را می‌خواند و به‌جای پهنای کاربر روی دیسک
    // می‌نوشت — و ‎Saved()‎ی پاسِ بعد همان عددِ خودکار را برمی‌گرداند.
    // یعنی ستونی که کاربر پهن کرده بود، با نخستین باز شدنِ دوباره پاک
    // می‌شد.
    private void RememberWidths()
    {
        if (!_spread) return;

        var cols = Columns.Where(c => c.IsVisible).ToList();
        if (cols.Count == 0) return;

        //  همهٔ ستون‌ها هنوز ستاره‌ای‌اند ⇒ هیچ‌کدام را کاربر نکشیده.
        if (!cols.Any(c => c.Width.UnitType == DataGridLengthUnitType.Pixel)) return;
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
        //  ستونی پیکسلی شد (کشیدن، دوبار-کلیک، یا پهنای ذخیره‌شده) ⇒ کفِ
        //  «پهنای طبیعی» دیگر خواستهٔ خودکار نیست و باید برود، وگرنه ستون را
        //  نمی‌شد باریک‌تر از محتوایش کرد (سنجهٔ ‎cells‎ گرفتش: ۳۰ ⇒ ۱۱۶).
        if (_starNatural is not null
            && Columns.Any(c => c.IsVisible && c.Width.UnitType == DataGridLengthUnitType.Pixel))
            ReleaseStarFloors();
        if (!_spread || _pinned) return;

        var cols = Columns.Where(c => c.IsVisible).ToList();
        if (cols.Count == 0) return;

        // ⛔ همهٔ ستون‌ها هنوز ستاره‌ای‌اند ⇒ کاربر چیزی نکشیده. پهنای همین
        // لحظه **خطِ پایه** است: همان که کشیدنِ بعدی با آن سنجیده می‌شود.
        // تا ۱۴۰۵/۰۷/۱۲ خطِ پایه **پس از** کشیدن گرفته می‌شد، پس ستونِ
        // کشیده‌شده با خودش برابر درمی‌آمد و هیچ‌وقت ذخیره نمی‌شد.
        if (!cols.Any(c => c.Width.UnitType == DataGridLengthUnitType.Pixel))
        {
            if (cols.All(c => !double.IsNaN(c.ActualWidth) && c.ActualWidth > 0))
                _starBase = cols.Select(c => c.ActualWidth).ToArray();
            return;
        }
        if (cols.All(c => c.Width.UnitType == DataGridLengthUnitType.Pixel)) { _pinned = true; return; }

        foreach (var c in cols)
        {
            var w = c.ActualWidth;
            if (double.IsNaN(w) || w <= 0) return;   // هنوز چیده نشده
        }
        //  ⚠️ ستونی که کاربر کشیده (پیکسلی) همان پهنای خودش را نگه می‌دارد.
        foreach (var c in cols)
            if (c.Width.UnitType != DataGridLengthUnitType.Pixel)
                c.Width = new DataGridLength(c.ActualWidth, DataGridLengthUnitType.Pixel);

        _pinned = true;
        // ⚠️ خطِ پایهٔ **پیش از** کشیدن — شرحش بالا و بالای ‎RememberWidths‎.
        _autoWidths = _starBase is { } b && b.Length == cols.Count ? b : null;
    }

    /// <summary>پهنای ستون‌ها وقتی هنوز همه ستاره‌ای بودند — خطِ پایهٔ «خودکار».</summary>
    private double[]? _starBase;

    private bool _pinned;

    /// <summary>پهنای خودکارِ همین جدول در لحظهٔ سنجاق شدن.</summary>
    private double[]? _autoWidths;
}
