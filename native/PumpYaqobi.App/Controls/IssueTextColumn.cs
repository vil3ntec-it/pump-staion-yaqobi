using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ خانه‌ای که خودش می‌گوید مشکل دارد ════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۲): «هشدار را بردار؛ همان کادر که مشکل
/// دارد، کناره‌های همان کادر را سرخ کن و یک علامتِ کوچک که مزاحمِ نوشته یا
/// کادرِ دیگر نشود… و وقتی روی همان کادر رفتی، درجا یک پیام بیاید و بگوید چه
/// شده و چرا سرخ شده — نوشته‌ها هم بالای کادر باشد.»
///
/// پس ستونِ جداگانهٔ «هشدار» رفت و خودِ ستونِ متنی (شروع، ختم) این را دارد:
///   • دورِ خانه یک خطِ سرخ و ته‌رنگِ ملایمِ سرخ،
///   • یک مثلثِ کوچک در گوشهٔ بالا — دور از عددِ وسط‌چین،
///   • پیامِ همان خانه بالای آن (‎ToolTip‎ با تأخیرِ صفر؛ با صفحه‌کلید هم
///     ‎ExcelGrid‎ همان را باز می‌کند).
///
/// ⚠️ **هنوز یک ‎DataGridTextColumn‎ است**، نه ستونِ قالبی: کپی/پیست/‎Delete‎ی
/// ‎ExcelGrid.Write‎ مسیرش را از ‎Binding‎ِ همین ستون پیدا می‌کند و ستونِ قالبی
/// آن را می‌بُرید — همان دلیلی که ستونِ «هشدار» اول جدا ساخته شده بود.
/// ⚠️ هیچ محاسبه‌ای از این نمی‌گذرد؛ فقط رنگ و پیام است.
/// </summary>
public sealed class IssueTextColumn : DataGridTextColumn
{
    /// <summary>نامِ خاصیتِ متنیِ ردیف که پیامِ مشکل را می‌دهد؛ خالی یعنی سالم.</summary>
    public string? IssuePath { get; set; }

    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var inner = base.GenerateElement(cell, dataItem);
        if (string.IsNullOrWhiteSpace(IssuePath)) return inner;

        var flagged = new Binding(IssuePath) { Converter = StringConverters.IsNotNullOrEmpty };

        //  ⛔ **هیچ کادرِ دومی داخلِ خانه نیست** (خواستهٔ صاحب ریپو با عکس،
        //  ۱۴۰۵/۰۷/۱۲: «اینو بردار که یک کادرِ دیگه توش گذاشتی؛ خودِ اون خطِ
        //  جدول، چهارگوشِ کادرش قرمز بشه»). خودِ خانه کلاسِ ‎issue‎ می‌گیرد و
        //  سبکش (‎Controls.axaml‎) خطِ خودِ خانه — ‎CellBorder‎ی قالب و خطِ
        //  عمودیِ کنارش — را سرخ و ضخیم می‌کند. خانه‌ها بازیافت می‌شوند، پس
        //  کلاس از روی پیامِ همین لحظهٔ ردیف گذاشته و برداشته می‌شود.
        //  علامتِ کوچک: مثلثِ گوشهٔ بالا — همیشه همان گوشه، هر جهتی که صفحه داشته باشد
        var mark = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M0,0 L9,0 L0,9 Z"),
            Width = 9, Height = 9,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -3, 0, 0),
            FlowDirection = FlowDirection.LeftToRight,
            IsHitTestVisible = false,
        };
        mark.Bind(Shape.FillProperty, mark.GetResourceObservable("Pump.Danger"));
        mark.Bind(Visual.IsVisibleProperty, new Binding(IssuePath) { Converter = StringConverters.IsNotNullOrEmpty });

        var host = new Grid { Classes = { "issuecell" } };
        host.Children.Add(inner);
        host.Children.Add(mark);

        //  پیامِ کوتاه با ماوس، بی تأخیر — ⛔ خودکار با انتخابِ خانه باز نمی‌شود (جلوی
        //  نوشته‌های ردیفِ بالا را می‌گرفت)
        //  ⚠️ پیام یک ‎TextBlock‎ِ صریح است، نه رشتهٔ خام: رشته در ‎ToolTip‎ با
        //  همان ‎TextBlock‎ی کشیده می‌شود که سبکِ ‎DataGridCell TextBlock‎ رویش
        //  می‌نشیند (یک خط، وسط‌چین، «…») — و پیام فقط «…» دیده می‌شد (عکسِ
        //  ‎waraqfit‎ گرفتش). مقدارهای محلیِ این‌جا بر آن سبک برنده‌اند.
        host.Bind(ToolTip.TipProperty, new Binding(IssuePath) { Converter = TipOf.Instance });
        host.GetObservable(ToolTip.TipProperty)
            .Subscribe(new Watch(v => cell.Classes.Set("issue", v is not null)));
        ToolTip.SetShowDelay(host, 0);
        ToolTip.SetPlacement(host, PlacementMode.Top);
        ToolTip.SetVerticalOffset(host, -4);
        return host;
    }

    /// <summary>پیام ⇐ کادرِ پیامِ خوانا؛ پیامِ خالی ⇐ هیچ (یعنی هیچ ‎ToolTip‎ی).</summary>
    public sealed class TipOf : IValueConverter
    {
        public static readonly TipOf Instance = new();
        public object? Convert(object? value, Type t, object? p, System.Globalization.CultureInfo c) =>
            value is string { Length: > 0 } text
                ? new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.None,
                    TextAlignment = TextAlignment.Right,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(0),
                    FlowDirection = FlowDirection.RightToLeft,
                }
                : null;
        public object? ConvertBack(object? v, Type t, object? p, System.Globalization.CultureInfo c) => null;
    }

    /// <summary>متنِ پیامِ یک خانه — برای سنجه‌ها.</summary>
    public static string MessageOf(Control? host) => (ToolTip.GetTip(host!) as TextBlock)?.Text ?? "";

    private sealed class Watch(Action<object?> on) : IObserver<object?>
    {
        public void OnNext(object? v) => on(v);
        public void OnError(Exception e) { }
        public void OnCompleted() { }
    }

    /// <summary>متنِ نمایشیِ خانه — برای جایی که ‎TextBlock‎ِ خودِ ستون را می‌خواهد.</summary>
    public static TextBlock? TextOf(Control? content) => content switch
    {
        TextBlock t => t,
        Panel p => p.Children.OfType<TextBlock>().FirstOrDefault(),
        _ => null,
    };
}

/// <summary>
/// ردیفی که نشانِ «مشکل دارد» می‌گیرد و می‌شود دستی برداشتش — منوی
/// راست‌کلیکِ ‎ExcelGrid‎ برایش «✔ نشانِ سرخ را بردار» می‌گذارد.
/// </summary>
public interface IFlaggedRow
{
    bool Flagged { get; }
    System.Windows.Input.ICommand ClearFlagCommand { get; }
}
