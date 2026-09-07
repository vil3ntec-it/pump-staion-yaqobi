using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ قالبِ مشترکِ بخش‌ها ═════════════════════════════════════════════════════
/// هر بخشِ برنامه همین چیدمان را دارد: عنوان، نوارِ ابزار، نوارِ فیلتر،
/// بدنه (معمولاً جدول) و نوارِ جمع‌ها. یک‌جا تعریف می‌شود تا چهل‌ودو بخش
/// مو‌به‌مو یک‌شکل بمانند و تغییرِ ظاهری در همه‌شان با هم اعمال شود.
/// </summary>
public class SectionPage : TemplatedControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SectionPage, string?>(nameof(Header));

    public static readonly StyledProperty<string?> SubHeaderProperty =
        AvaloniaProperty.Register<SectionPage, string?>(nameof(SubHeader));

    public static readonly StyledProperty<object?> ToolbarProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Toolbar));

    public static readonly StyledProperty<object?> FiltersProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Filters));

    public static readonly StyledProperty<object?> SummaryProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Summary));

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Body));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SectionPage, object?>(nameof(Footer));

    /// <summary>
    /// ══ بدنه اسکرول شود؟ ══════════════════════════════════════════════════
    /// پیش‌فرض **بله** — مثلِ نسخهٔ وب که کلِ صفحه اسکرول می‌شود.
    ///
    /// ⚠️ بخشی که بدنه‌اش **جدول** است باید ‎False‎ بگذارد. دلیلش کارایی است،
    /// نه ظاهر: جدول داخلِ اسکرول‌ویور ارتفاعِ بی‌نهایت می‌گیرد و همهٔ ردیف‌ها
    /// را یک‌جا می‌سازد — مجازی‌سازی از بین می‌رود و با هزاران قرض‌دار برنامه
    /// یخ می‌زند. جدول اسکرولِ خودش را دارد و باید جای باقی‌مانده را پر کند.
    /// </summary>
    public static readonly StyledProperty<bool> ScrollBodyProperty =
        AvaloniaProperty.Register<SectionPage, bool>(nameof(ScrollBody), defaultValue: true);

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string? SubHeader { get => GetValue(SubHeaderProperty); set => SetValue(SubHeaderProperty, value); }
    public object? Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }
    public object? Filters { get => GetValue(FiltersProperty); set => SetValue(FiltersProperty, value); }
    public object? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    public bool ScrollBody { get => GetValue(ScrollBodyProperty); set => SetValue(ScrollBodyProperty, value); }

    [Content]
    public object? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
}
