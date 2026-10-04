using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// نوارِ کوچکِ میانبرهای یک بخش، زیرِ جدول (شورا، ث۲). متن از
/// <see cref="Hints.Strip"/>؛ «✕» آن را روی همین کامپیوتر برای همیشه می‌بندد.
/// ⛔ هیچ کاری انجام نمی‌دهد جز گفتن.
/// </summary>
public class HintStrip : Border
{
    public static readonly StyledProperty<string?> HintKeyProperty =
        AvaloniaProperty.Register<HintStrip, string?>(nameof(HintKey));

    public string? HintKey { get => GetValue(HintKeyProperty); set => SetValue(HintKeyProperty, value); }

    private readonly TextBlock _text = new() { FontSize = 11.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

    public HintStrip()
    {
        Classes.Add("hintstrip");
        Padding = new Thickness(10, 4);
        CornerRadius = new CornerRadius(8);
        Margin = new Thickness(0, 6, 0, 0);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        var close = new Button { Content = "✕", Classes = { "ghost" }, Padding = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(close, "دیگر نشان نده (فقط روی همین کامپیوتر)");
        close.Click += (_, _) => { if (HintKey is { } k) Hints.DismissStrip(k); IsVisible = false; };
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        g.Children.Add(_text);
        Grid.SetColumn(close, 1);
        g.Children.Add(close);
        Child = g;
        IsVisible = false;
    }

    //  ⚠️ شنونده فقط تا وقتی در درخت است — رویدادِ ایستا نباید نوارِ دورریخته را زنده نگه دارد
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Hints.StripDismissed += OnDismissed;
        if (HintKey is { } k) IsVisible = Hints.StripVisible(k);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Hints.StripDismissed -= OnDismissed;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDismissed(string key)
    {
        if (key == HintKey) Avalonia.Threading.Dispatcher.UIThread.Post(() => IsVisible = false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HintKeyProperty)
        {
            var k = HintKey ?? "";
            _text.Text = Hints.Strip.TryGetValue(k, out var t) ? t : "";
            IsVisible = Hints.StripVisible(k);
        }
    }
}
