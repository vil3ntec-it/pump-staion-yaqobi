using Avalonia;
using Avalonia.Controls;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ شبکهٔ کارت‌ها — همتای ‎repeat(auto-fill, minmax(N, 1fr))‎ ═══════════════
///
/// نسخهٔ وب کارت‌ها را با همین یک خطِ CSS می‌چیند: «هر چند ستون که با پهنای
/// کمینهٔ N جا می‌شود بساز، بعد پهنای اضافه را بینشان پخش کن». نتیجه‌اش این
/// است که ردیف همیشه از لبه تا لبه پر است.
///
/// ‎WrapPanel‎ی که تا امروز به کار می‌رفت نیمهٔ اول را می‌کند و نیمهٔ دوم را نه:
/// کارت‌ها را می‌شکند ولی پهنای اضافه را پخش نمی‌کند، پس در پنجرهٔ ۱۴۴۰
/// پیکسلی با کارتِ ۲۵۲ پیکسلی، ۵ کارت جا می‌شد و ۱۴۰ پیکسل کنارِ ردیف خالی
/// می‌ماند — همان نوارِ خالی که در قرض‌داران، شرکت‌ها و حاضری دیده می‌شد.
///
/// این پنل همان کارِ CSS را می‌کند: شمارِ ستون از ‎MinItemWidth‎ در می‌آید و
/// پهنای هر ستون ‎(کلِ پهنا − فاصله‌ها) ÷ شمارِ ستون‎ است.
/// </summary>
public class AutoFillPanel : Panel
{
    /// <summary>پهنای کمینهٔ هر کارت — همان ‎minmax(N, …)‎.</summary>
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<AutoFillPanel, double>(nameof(MinItemWidth), 220d);

    /// <summary>فاصلهٔ بینِ کارت‌ها، افقی و عمودی — همان ‎gap‎.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AutoFillPanel, double>(nameof(Gap), 12d);

    /// <summary>
    /// ══ همهٔ کارت‌ها هم‌قد (۱۴۰۵/۰۷/۲۰) ═══════════════════════════════════
    /// صاحب ریپو: «دو کادرِ جمله پطرول و جمله دیزل بزرگ‌تر از آن چهار تا
    /// هستند، همه را برابر کن.» آن دو یک خطِ لیترِ اضافه دارند؛ با این گزینه
    /// قدِ همهٔ کارت‌ها قدِ بلندترینشان می‌شود — در هر ردیف و هر شکستنی.
    /// ⚠️ پیش‌فرض خاموش است تا شبکه‌های دیگر دست نخورند.
    /// </summary>
    public static readonly StyledProperty<bool> UniformHeightProperty =
        AvaloniaProperty.Register<AutoFillPanel, bool>(nameof(UniformHeight));

    public bool UniformHeight
    {
        get => GetValue(UniformHeightProperty);
        set => SetValue(UniformHeightProperty, value);
    }

    public double MinItemWidth
    {
        get => GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    static AutoFillPanel()
    {
        AffectsMeasure<AutoFillPanel>(MinItemWidthProperty, GapProperty, UniformHeightProperty);
    }

    /// <summary>
    /// شمارِ ستون‌هایی که در این پهنا جا می‌شوند — دستِ‌کم یکی و حداکثر به
    /// شمارِ خودِ کارت‌ها.
    ///
    /// سقفِ «شمارِ کارت‌ها» همان فرقِ ‎auto-fit‎ با ‎auto-fill‎ است: بی آن، در
    /// پنجرهٔ پهن پنج ستون ساخته می‌شد ولی چهار کارت داشتیم و ستونِ پنجم خالی
    /// می‌ماند — همان نوارِ خالیِ کنارِ کادرهای «قرض‌های کهنه».
    /// </summary>
    private int Columns(double available)
    {
        var min = Math.Max(1, MinItemWidth);
        var gap = Math.Max(0, Gap);
        if (double.IsInfinity(available) || available <= 0) return 1;
        // n ستون یعنی n×min + (n−1)×gap ≤ available
        var n = (int)Math.Floor((available + gap) / (min + gap));
        return Math.Clamp(n, 1, Math.Max(1, Children.Count));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var kids = Children;
        if (kids.Count == 0) return default;

        var gap = Math.Max(0, Gap);
        var cols = Columns(availableSize.Width);
        var colW = double.IsInfinity(availableSize.Width)
            ? MinItemWidth
            : Column(0, cols, availableSize.Width, gap).width;

        for (var i = 0; i < kids.Count; i++)
            kids[i].Measure(new Size(colW, double.PositiveInfinity));
        var uniform = UniformHeight ? TallestKid() : 0d;

        double total = 0, rowH = 0;
        for (var i = 0; i < kids.Count; i++)
        {
            rowH = Math.Max(rowH, Math.Max(uniform, kids[i].DesiredSize.Height));
            // پایانِ ردیف (یا پایانِ کار) → قدِ ردیف را به جمع اضافه کن
            if ((i + 1) % cols == 0 || i == kids.Count - 1)
            {
                total += rowH + (i == kids.Count - 1 ? 0 : gap);
                rowH = 0;
            }
        }

        var w = double.IsInfinity(availableSize.Width) ? colW * cols + gap * (cols - 1) : availableSize.Width;
        return new Size(w, total);
    }

    /// <summary>
    /// لبهٔ چپِ ستونِ ‎k‎ و لبهٔ راستش، گِرد‌شده به پیکسل.
    ///
    /// چرا گِرد کردن این‌جا و نه با ضربِ ساده: Avalonia چیدمان را به پیکسلِ
    /// کامل می‌چسباند. اگر پهنای ستون را یک عددِ اعشاری بدهیم و جای هر ستون
    /// را جدا حساب کنیم، گِرد شدن‌ها روی هم جمع می‌شوند و کارتِ آخرِ ردیف یک
    /// پیکسل از لبهٔ پنل بیرون می‌زند (و بریده می‌شود). با حساب کردنِ خودِ
    /// «لبه‌ها» — نه پهناها — لبهٔ آخر دقیقاً روی عرضِ پنل می‌افتد و باقی‌ماندهٔ
    /// تقسیم بینِ ستون‌ها پخش می‌شود.
    /// </summary>
    private static (double left, double width) Column(int k, int cols, double width, double gap)
    {
        var track = width - gap * (cols - 1);        // مجموعِ پهنای خودِ ستون‌ها
        var l = Math.Round(k * track / cols) + k * gap;
        var r = Math.Round((k + 1) * track / cols) + k * gap;
        return (l, Math.Max(1, r - l));
    }

    private double TallestKid()
    {
        double h = 0;
        foreach (var k in Children)
            if (k.IsVisible) h = Math.Max(h, k.DesiredSize.Height);
        return h;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var kids = Children;
        if (kids.Count == 0) return finalSize;

        var gap = Math.Max(0, Gap);
        var cols = Columns(finalSize.Width);

        var uniform = UniformHeight ? TallestKid() : 0d;
        double y = 0, rowH = 0;
        for (var i = 0; i < kids.Count; i++)
        {
            var (x, w) = Column(i % cols, cols, finalSize.Width, gap);
            var h = Math.Max(uniform, kids[i].DesiredSize.Height);
            // چیدمان همیشه در مختصاتِ چپ‌به‌راست است؛ خودِ Avalonia برای
            // ‎FlowDirection="RightToLeft"‎ آینه‌اش می‌کند، پس این‌جا کاری
            // با راست‌به‌چپ نداریم.
            kids[i].Arrange(new Rect(x, y, w, h));
            rowH = Math.Max(rowH, h);

            if ((i + 1) % cols == 0)
            {
                y += rowH + gap;
                rowH = 0;
            }
        }
        return finalSize;
    }
}
