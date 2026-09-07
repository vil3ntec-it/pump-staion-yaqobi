using System.Collections;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ یک خانهٔ ردیفِ «جمله» ═══════════════════════════════════════════════════
/// برچسبِ ستون و عددِ جمعِ همان ستون. <see cref="BrushKey"/> اختیاری است و
/// وقتی داده شود، عدد با همان رنگِ تمِ برنامه کشیده می‌شود (مثل «الباقی» که
/// در سایت سرخ/سبز می‌شود).
/// </summary>
public sealed class TotalCell
{
    public TotalCell(string label, string value, string? brushKey = null)
    { Label = label; Value = value; BrushKey = brushKey ?? "Pump.Text"; }

    public string Label { get; }
    public string Value { get; }
    public string BrushKey { get; }
}

/// <summary>
/// ══ ردیفِ «جمله»ی ته جدول — ‎&lt;tfoot class="xls-foot"&gt;‎ی سایت ══════════════
///
/// گزارشِ صاحب ریپو: «آخرِ هر جدول جمله ندارد؛ در سایت بگرد و همان مدل این‌جا
/// هم پیاده شود.» در سایت هر جدولِ اکسلی یک ‎tfoot‎ دارد که خانهٔ اولش «جمله»
/// است و بقیهٔ خانه‌ها جمعِ همان ستون.
///
/// چرا نوار است و نه ردیفِ داخلِ خودِ جدول: ستون‌های ‎DataGrid‎ عرضِ زنده دارند
/// (کاربر می‌تواند بکشد و جابه‌جا کند) و ردیفِ ساختگیِ داخلِ جدول باید هم‌نوعِ
/// ردیف‌های واقعی می‌بود — یعنی همان چیزی که قاعدهٔ پروژه ممنوع کرده: ردیفِ
/// نمایشی هرگز نباید در جمع‌ها شمرده شود. این‌جا نوار بیرونِ جدول است، پس نه
/// در ‎ItemsSource‎ می‌نشیند، نه در هیچ محاسبه‌ای دیده می‌شود.
///
/// هر بخش فقط یک فهرستِ <see cref="TotalCell"/> می‌دهد؛ ظاهر و رنگ یک‌جا
/// این‌جا تعریف شده تا همهٔ جدول‌های برنامه یک شکل بمانند.
/// </summary>
public class TotalsBar : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<TotalsBar, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TotalsBar, string>(nameof(Title), "🧮 جمله");

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
}
