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
    public TotalCell(string label, string value, string? brushKey = null, string? column = null)
    { Label = label; Value = value; BrushKey = brushKey ?? "Pump.Text"; Column = column ?? label; }

    public string Label { get; }
    public string Value { get; }
    public string BrushKey { get; }

    /// <summary>نامِ سربرگِ ستونی که این جمع باید زیرش بنشیند.</summary>
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
    private string _sig = "";

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
        g.LayoutUpdated += (_, _) =>
        {
            var sig = Signature(g);
            if (sig == _sig) return;
            _sig = sig;
            InvalidateArrange();
        };
    }

    private static string Signature(DataGrid g) =>
        string.Join(',', g.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex)
                          .Select(c => Math.Round(c.ActualWidth))) + "|" + Math.Round(HScroll(g));

    /// <summary>جدول چقدر افقی لغزیده — نوار باید همان‌قدر بلغزد.</summary>
    private static double HScroll(DataGrid g) =>
        g.GetVisualDescendants().OfType<ScrollBar>()
         .FirstOrDefault(b => b.Orientation == Orientation.Horizontal)?.Value ?? 0;

    private static string Head(DataGridColumn c) => c.Header?.ToString()?.Trim() ?? "";

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
            var x = new double[cols.Count];
            // ⚠️ ستونِ «#» (سرستونِ ردیف) پهنا می‌گیرد و جدول را جابه‌جا می‌کند؛
            // نوارِ جمله هم باید همان‌قدر عقب بیفتد، وگرنه هر جمع یک ستون
            // آن‌طرف‌تر می‌نشیند.
            var acc = RowHeaderWidth(grid);
            for (var i = 0; i < cols.Count; i++) { x[i] = acc; acc += cols[i].ActualWidth; }
            var scroll = HScroll(grid);

            var loose = new List<Control>();
            foreach (var child in Children)
            {
                var want = (child.DataContext as TotalCell)?.Column;
                var i = string.IsNullOrEmpty(want) ? -1 : cols.FindIndex(c => Head(c) == want);
                if (i < 0) { loose.Add(child); continue; }
                var left = x[i] - scroll;
                child.Arrange(new Rect(left, 0, cols[i].ActualWidth, h));
                tail = Math.Max(tail, left + cols[i].ActualWidth);
            }
            foreach (var child in loose)
            {
                child.Arrange(new Rect(tail, 0, child.DesiredSize.Width, h));
                tail += child.DesiredSize.Width;
            }
            return finalSize;
        }

        // جدولی پیدا نشد — همان چیدنِ پشتِ‌سرِ‌هم، تا دستِ‌کم چیزی گم نشود
        foreach (var child in Children)
        {
            child.Arrange(new Rect(tail, 0, child.DesiredSize.Width, h));
            tail += child.DesiredSize.Width;
        }
        return finalSize;
    }
}
