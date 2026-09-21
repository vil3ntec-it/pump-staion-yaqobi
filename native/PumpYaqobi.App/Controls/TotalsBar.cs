using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ یک خانهٔ ردیفِ «جمله» ═══════════════════════════════════════════════════
/// برچسبِ ستون و عددِ جمعِ همان ستون.
///
/// <para><see cref="Column"/> می‌گوید این جمع زیرِ کدام ستونِ جدول بنشیند — با
/// نامِ سربرگِ همان ستون. اگر داده نشود، خودِ <see cref="Label"/> نامِ ستون
/// فرض می‌شود؛ در بیشترِ جدول‌ها برچسب و سربرگ یکی است و چیزی لازم نیست.</para>
///
/// <para><see cref="BrushKey"/> اختیاری است و وقتی داده شود، عدد با همان رنگِ
/// تمِ برنامه کشیده می‌شود (مثل «الباقی» که در سایت سرخ/سبز می‌شود).</para>
/// </summary>
public sealed class TotalCell
{
    /// <summary>
    /// «این جمع زیرِ هیچ ستونی نیست» — مثلِ «شمارِ رسیدها» یا «امروز» که عددِ
    /// خلاصه‌اند، نه جمعِ یک ستون. این‌ها پشتِ‌سرِ بقیه می‌نشینند و همان درست
    /// است.
    ///
    /// ⚠️ باید **صریح** نوشته شود. پیش از این نبودنِ ستون از نداشتنِ آن قابلِ
    /// تشخیص نبود: ‎Column‎ پیش‌فرض خودِ ‎Label‎ می‌شد، پس جمعی که واقعاً باید
    /// زیرِ ستونی می‌نشست ولی ستونش ساخته نشده بود، بی سر و صدا به دُم می‌رفت.
    /// گزارشِ صاحب ریپو «بخشِ صرافی الباقی نداره» دقیقاً همین بود: خانهٔ
    /// «الباقی ($)» ساخته می‌شد و جدول ستونی به این نام نداشت.
    /// </summary>
    public const string NoColumn = "";

    public TotalCell(string label, string value, string? brushKey = null, string? column = null)
    { Label = label; Value = value; BrushKey = brushKey ?? "Pump.Text"; Column = column ?? label; }

    public string Label { get; }
    public string Value { get; }
    public string BrushKey { get; }

    /// <summary>
    /// نامِ سربرگِ ستونی که این جمع باید زیرش بنشیند، یا <see cref="NoColumn"/>.
    /// سنجشِ ‎chrome‎ نامی را که به هیچ ستونی نمی‌خورد ایراد می‌گیرد.
    /// </summary>
    public string Column { get; }
}

/// <summary>
/// ══ ردیفِ «جمله»ی ته جدول — ‎&lt;tfoot class="xls-foot"&gt;‎ی سایت ══════════════
///
/// گزارشِ اولِ صاحب ریپو: «آخرِ هر جدول جمله ندارد.» ⇒ این نوار ساخته شد.
///
/// گزارشِ دومش (که این‌بار پیاده شد): «جمله زیرِ جدول‌ها جوری نیست که آخرِ
/// همان کادرِ موردِ نظر دیده بشود؛ الان همه‌شان یک‌جا، یک کنج دیده می‌شوند و
/// هر کدام زیرِ بخشِ خودش نیست.» حق داشت: نوار یک ‎WrapPanel‎ بود و جمع‌ها
/// پشتِ‌سرِ‌هم از یک گوشه شروع می‌شدند، بی هیچ ربطی به ستون‌ها.
///
/// حالا <see cref="TotalsStrip"/> هر جمع را دقیقاً روی مختصاتِ ستونِ خودش
/// می‌نشاند: پهنای زندهٔ ستون‌های همان جدول خوانده می‌شود (کاربر می‌تواند
/// ستون را بکشد یا جابه‌جا کند) و لغزشِ افقیِ جدول هم کم می‌شود، پس نوار با
/// جدول هم‌قدم می‌ماند.
///
/// چرا هنوز بیرونِ جدول است و ردیفِ داخلِ آن نشد: قاعدهٔ پروژه است که ردیفِ
/// نمایشی هرگز نباید در ‎ItemsSource‎ بنشیند و در جمع‌ها شمرده شود
/// (‎tr.pm-auto-row‎ی سایت هم همین‌طور است). این‌جا نوار بیرون است، پس نه در
/// داده‌ها می‌آید نه در هیچ محاسبه‌ای.
/// </summary>
public class TotalsBar : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<TotalsBar, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TotalsBar, string>(nameof(Title), "🧮 جمله");

    /// <summary>
    /// جدولی که این جمع‌ها مالِ اوست. اگر داده نشود خودش پیدا می‌شود — نزدیک‌ترین
    /// جدولِ بالادست. فقط جایی لازم است که در یک قاب **دو** جدول باشد
    /// (مثلِ دو جدولِ ورق) و باید گفت کدام.
    /// </summary>
    public static readonly StyledProperty<DataGrid?> GridProperty =
        AvaloniaProperty.Register<TotalsBar, DataGrid?>(nameof(Grid));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public DataGrid? Grid
    {
        get => GetValue(GridProperty);
        set => SetValue(GridProperty, value);
    }

    private DataGrid? _found;

    /// <summary>جدولِ این نوار — داده‌شده، وگرنه نزدیک‌ترینِ بالادست.</summary>
    internal DataGrid? ResolveGrid()
    {
        if (Grid is not null) return Grid;
        if (_found is not null && _found.GetVisualRoot() is not null) return _found;

        for (Visual? p = this.GetVisualParent(); p is not null; p = p.GetVisualParent())
        {
            var g = p.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
            if (g is not null) return _found = g;
        }
        return null;
    }
}

/// <summary>
/// ══ چیدنِ جمع‌ها روی ستون‌های خودشان ═══════════════════════════════════════
///
/// ‎ItemsPanel‎ی نوارِ «جمله». برای هر خانه، ستونِ هم‌نامش در جدول پیدا می‌شود
/// و خانه دقیقاً روی همان مختصات و با همان پهنا چیده می‌شود. خانه‌ای که ستونی
/// به نامش نیست، پشتِ‌سرِ آخرین خانهٔ چیده‌شده می‌نشیند تا هیچ عددی گم نشود.
///
/// ⚠️ چیدمانِ راست‌به‌چپ در آوالونیا یک «آینهٔ رسم» است، نه چیدمانِ وارونه؛ پس
/// همان مختصاتِ منطقیِ ستون‌ها این‌جا هم درست است و نباید برعکس شود.
/// </summary>
public class TotalsStrip : Panel
{
    private TotalsBar? _bar;
    private DataGrid? _watched;

    private TotalsBar? Bar => _bar ??= this.FindAncestorOfType<TotalsBar>();

    protected override Size MeasureOverride(Size available)
    {
        var h = 0d;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, available.Height));
            h = Math.Max(h, child.DesiredSize.Height);
        }
        Watch();
        var w = double.IsInfinity(available.Width)
            ? Children.Sum(c => c.DesiredSize.Width)
            : available.Width;
        return new Size(w, h);
    }

    /// <summary>
    /// جدول که پهنای ستون‌هایش را عوض کند یا افقی بلغزد، نوار باید دوباره
    /// چیده شود. برای این‌که هر چیدمانی یک چیدمانِ دیگر نسازد، فقط وقتی
    /// دوباره خوانده می‌شود که «امضای» پهناها واقعاً عوض شده باشد.
    /// </summary>
    private void Watch()
    {
        var g = Bar?.ResolveGrid();
        if (g is null || ReferenceEquals(g, _watched)) return;
        _watched = g;
        _hbar = null;
        g.LayoutUpdated += (_, _) =>
        {
            // ⚠️ ‎LayoutUpdated‎ برای **هر** چیدمانِ **هر جای** پنجره شلیک می‌شود —
            // یعنی نوارِ جملهٔ بخشِ پنهان هم با هر چرخِ ماوس در بخشِ دیگر بیدار
            // می‌شد. بخشِ پنهان چیزی برای چیدن ندارد.
            if (!g.IsEffectivelyVisible) return;
            var sig = Signature(g);
            if (sig == _sig) return;
            _sig = sig;
            InvalidateArrange();
        };
    }

    /// <summary>
    /// امضای پهنای ستون‌ها و لغزشِ افقی — یک عدد، بی رشته‌سازی.
    ///
    /// ⚠️ این در هر پاسِ چیدمان صدا می‌خورد؛ سنجشِ ‎scrollperf‎ (نمونه‌بردارِ
    /// ‎dotnet-trace‎) نشان داد نسخهٔ پیشین — که هر بار برای پیدا کردنِ نوارِ
    /// لغزشِ افقی **کلِ درختِ جدول** را می‌گشت — به‌تنهایی ۱۳٪ کلِ وقتِ نخِ رابط
    /// هنگامِ اسکرول بود، و با بزرگ شدنِ جدول بیشتر می‌شد.
    /// </summary>
    private long Signature(DataGrid g)
    {
        long h = 17;
        foreach (var c in g.Columns)
        {
            if (!c.IsVisible) continue;
            h = unchecked(h * 31 + (long)Math.Round(c.ActualWidth) * 8 + c.DisplayIndex);
        }
        return unchecked(h * 31 + (long)Math.Round(HScroll(g)));
    }

    private long _sig;
    private ScrollBar? _hbar;
    private Visual? _hbarRoot;
    private int _hbarMiss;

    /// <summary>
    /// جدول چقدر افقی لغزیده — نوار باید همان‌قدر بلغزد.
    /// نوارِ لغزش یک بار پیدا می‌شود و تا وقتی در همان درختِ زنده است، همان می‌ماند.
    /// </summary>
    private double HScroll(DataGrid g)
    {
        var root = g.GetVisualRoot() as Visual;
        if (root is null) return 0;
        if (!ReferenceEquals(_hbarRoot, root)) { _hbarRoot = root; _hbar = null; _hbarMiss = 0; }
        if (_hbar is null || _hbar.GetVisualRoot() is null)
        {
            // قالبِ جدول شاید هنوز پیاده نشده باشد؛ چند بار می‌گردیم و بس —
            // جدولی که نوارِ افقی ندارد نباید در هر چیدمان دوباره گشته شود.
            if (_hbarMiss >= 3) return 0;
            _hbar = g.GetVisualDescendants().OfType<ScrollBar>()
                     .FirstOrDefault(b => b.Orientation == Orientation.Horizontal);
            if (_hbar is null) { _hbarMiss++; return 0; }
        }
        return _hbar.Value;
    }

    private TextBlock? _title;

    /// <summary>
    /// جایی که نوشتهٔ «🧮 جمله» گرفته — خانه‌های بی‌ستون از این‌جا به بعد
    /// می‌نشینند.
    ///
    /// ⛔ <b>باگی که این را لازم کرد</b>: خانه‌های بی‌ستون از ‎x = 0‎ چیده
    /// می‌شدند، یعنی <b>دقیقاً روی خودِ «جمله»</b>. در گاوصندوق هر شش عدد
    /// بی‌ستون‌اند (یک ستونِ «مبلغ» و شش جمع)، پس همه‌شان روی هم و روی
    /// نوشته می‌افتادند — همان «خیلی داغون» بودنِ نوار.
    /// </summary>
    private double TitleGutter()
    {
        if (_title is null || _title.GetVisualRoot() is null)
            _title = Bar?.GetVisualDescendants().OfType<TextBlock>()
                        .FirstOrDefault(t => t.Name == "PART_Title");
        var w = _title?.Bounds.Width ?? 0;

        // ⚠️ در نخستین پاسِ چیدمان، نوشته هنوز اندازه نگرفته و پهنایش صفر
        // است. صفر برگرداندن یعنی همان یک فریم خانه‌ها روی «جمله» می‌افتند
        // و چون چیزی نوار را دوباره بی‌اعتبار نمی‌کند، همان‌جا می‌مانند.
        // پس کفِ ثابتی هست که از پهنای واقعیِ «🧮 جمله» کمتر نیست.
        return w > 0 ? w + 20 : 76;         // ۱۰ حاشیه از هر طرف
    }

    private static string Head(DataGridColumn c) => c.Header?.ToString()?.Trim() ?? "";

    private Dictionary<string, DataGridColumnHeader>? _heads;

    /// <summary>سرستون‌های همین جدول — یک بار پیدا می‌شوند و کَش می‌مانند.</summary>
    private Dictionary<string, DataGridColumnHeader> Heads(DataGrid grid)
    {
        // کَش تا وقتی معتبر است که همان سرستون‌ها هنوز در درختِ زنده باشند.
        if (_heads is { Count: > 0 } c
            && c.Values.All(h => h.GetVisualRoot() is not null && h.Bounds.Width > 0))
            return c;

        return _heads = grid.GetVisualDescendants().OfType<DataGridColumnHeader>()
                            .Where(hd => hd.Bounds.Width > 0)
                            .GroupBy(hd => hd.Content?.ToString()?.Trim() ?? "")
                            .ToDictionary(g2 => g2.Key, g2 => g2.First());
    }

    /// <summary>
    /// پهنای ستونِ «#»ِ همین جدول — از روی خودِ سرستونِ ساخته‌شده، نه از روی
    /// عددی که شاید ‎NaN‎ باشد.
    /// </summary>
    private static double RowHeaderWidth(DataGrid g)
    {
        if (g.HeadersVisibility is not (DataGridHeadersVisibility.All or DataGridHeadersVisibility.Row))
            return 0;
        var w = g.GetVisualDescendants().OfType<DataGridRowHeader>()
                 .Select(h => h.Bounds.Width).FirstOrDefault(v => v > 0);
        if (w > 0) return w;
        return double.IsNaN(g.RowHeaderWidth) ? 0 : g.RowHeaderWidth;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var grid = Bar?.ResolveGrid();
        var cols = grid?.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        var h = finalSize.Height;
        var tail = 0d;

        if (grid is not null && cols is { Count: > 0 })
        {
            // ══ جای هر ستون را از خودِ سرستونِ آن می‌پرسیم ═══════════════════
            //
            // ⚠️ پیش از این پهناها از ستونِ «#» به بعد جمع زده می‌شد. آن حساب
            // یک چیز را نمی‌دید: **نوارِ لغزشِ عمودیِ خودِ جدول**. جدولی که
            // نوار داشته باشد، ستون‌هایش ۱۷ پیکسل کم‌عرض‌تر جا می‌گیرند و
            // نوارِ «جمله» — که نوارِ لغزش ندارد — همان‌قدر جابه‌جا می‌شد.
            // سنجشِ ‎chrome‎ همین را گرفت: در «رسید قرض‌داران» جملهٔ «مبلغ رسید»
            // ۱۷ پیکسل از ستونش دور بود و در بقیهٔ بخش‌ها نه.
            //
            // پرسیدن از خودِ سرستون همهٔ این‌ها را یک‌جا حل می‌کند: نوارِ لغزش،
            // ستونِ «#»، لغزشِ افقی، و آینهٔ راست‌به‌چپ. هر دو کنترل در یک
            // درختِ آینه‌شده‌اند، پس مختصاتِ محلی‌شان هم‌جنس است.
            // ⚠️ از کَش، نه از گشتنِ درخت در **هر** چیدمان.
            //
            // این‌جا پیش از این کلِ درختِ جدول گشته می‌شد تا سرستون‌ها پیدا
            // شوند — و آن درخت شاملِ همهٔ ردیف‌ها و خانه‌های ساخته‌شده است.
            // یعنی هزینهٔ هر چیدمان با شمارِ ردیف‌ها بالا می‌رفت، دقیقاً همان
            // چیزی که سنجش نشان داد: با ۸۰ ردیفِ ساخته‌شده هر پاسِ چیدمانِ
            // صفحهٔ ورق ~۴۰۰ میلی‌ثانیه بود.
            //
            // سرستون‌ها یک بار ساخته می‌شوند و تا وقتی در درختِ زنده‌اند همان
            // می‌مانند، پس یک بار پیدا کردنشان بس است.
            var heads = Heads(grid);

            // اگر هنوز سرستونی ساخته نشده، همان حسابِ قدیمی پشتیبان است
            var x = new double[cols.Count];
            var acc = RowHeaderWidth(grid);
            for (var i = 0; i < cols.Count; i++) { x[i] = acc; acc += cols[i].ActualWidth; }
            var scroll = HScroll(grid);

            var loose = new List<Control>();
            var anyPlaced = false;
            foreach (var child in Children)
            {
                var want = (child.DataContext as TotalCell)?.Column;
                var i = string.IsNullOrEmpty(want) ? -1 : cols.FindIndex(c => Head(c) == want);
                if (i < 0) { loose.Add(child); continue; }
                anyPlaced = true;

                double left, width;
                if (!string.IsNullOrEmpty(want) && heads.TryGetValue(want, out var head)
                    && head.TranslatePoint(new Point(0, 0), this) is { } at)
                {
                    left = at.X;
                    width = head.Bounds.Width;
                }
                else
                {
                    left = x[i] - scroll;
                    width = cols[i].ActualWidth;
                }

                child.Arrange(new Rect(left, 0, width, h));
                tail = Math.Max(tail, left + width);
            }
            ArrangeLoose(loose, Math.Max(tail, TitleGutter()), finalSize.Width, h, !anyPlaced);
            return finalSize;
        }

        // جدولی پیدا نشد — همان چیدنِ پشتِ‌سرِ‌هم، تا دستِ‌کم چیزی گم نشود
        ArrangeLoose(Children.ToList(), TitleGutter(), finalSize.Width, h, true);
        return finalSize;
    }

    /// <summary>
    /// خانه‌هایی که زیرِ هیچ ستونی نیستند.
    ///
    /// <para>اگر <b>هیچ</b> خانه‌ای ستون نداشته باشد (مثلِ گاوصندوق، که یک
    /// ستونِ «مبلغ» دارد و شش جمع) نوار یک ردیفِ کاملِ خودش است: جا به
    /// تساوی بینشان پخش می‌شود، از بعدِ «جمله» تا لبهٔ راست. این‌طور هر عدد
    /// کادرِ خودش را دارد و نوار هم‌قدِ جدول دیده می‌شود.</para>
    ///
    /// <para>اگر بعضی ستون دارند و بعضی نه، بی‌ستون‌ها پشتِ آخرین
    /// خانهٔ چیده‌شده می‌نشینند — همان رفتارِ همیشگی — ولی دیگر از لبهٔ نوار
    /// بیرون نمی‌زنند.</para>
    /// </summary>
    private static void ArrangeLoose(List<Control> loose, double start, double width,
                                     double h, bool spread)
    {
        if (loose.Count == 0) return;
        var room = Math.Max(0, width - start);

        if (spread && room > 0)
        {
            var each = room / loose.Count;
            for (var i = 0; i < loose.Count; i++)
                loose[i].Arrange(new Rect(start + i * each, 0, each, h));
            return;
        }

        var x = start;
        foreach (var child in loose)
        {
            // از لبه بیرون نزند: آخرین خانه هرچه مانده را می‌گیرد
            var w = Math.Min(child.DesiredSize.Width, Math.Max(0, width - x));
            child.Arrange(new Rect(x, 0, w, h));
            x += w;
        }
    }
}
