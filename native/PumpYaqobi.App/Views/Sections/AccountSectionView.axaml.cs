using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// ══ پروفایل — و صفحهٔ ورودی که کلِ پنجره را می‌گیرد ═════════════════════════
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «این لاگین تمام صفحه باشد و کلِ صفحه
/// را بگیرد… فقط همین را نشان بده در صفحه، نه بخش‌ها باشند نه غیره.»
///
/// ⚠️ «تمامِ صفحه» با XAML تنها نمی‌شود: خودِ صفحه‌ها داخلِ یک
/// <see cref="ScrollViewer"/> و یک <c>StackPanel</c> می‌نشینند، پس بلندیشان
/// **خودکار** است و کادرِ ورود فقط به اندازهٔ محتوایش بلند می‌شد. بلندیِ قابِ
/// پنجره را از خودِ <see cref="TopLevel"/> می‌گیریم و روی همان می‌نشانیم.
/// ⚠️ و این کار **بی هیچ شنوندهٔ چیدمانی** است (قاعدهٔ سرعت):
/// فقط با عوض شدنِ اندازهٔ پنجره خبر می‌رسد.
/// </summary>
public partial class AccountSectionView : UserControl
{
    private TopLevel? _top;

    public AccountSectionView() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top is null) return;
        _top.PropertyChanged += OnTopChanged;
        FillHeight();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_top is not null) _top.PropertyChanged -= OnTopChanged;
        _top = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTopChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty) FillHeight();
    }

    private void FillHeight()
    {
        if (this.FindControl<Border>("LoginPage") is not { } page || _top is null) return;
        var h = _top.ClientSize.Height;
        if (h > 0) page.MinHeight = Math.Max(h, 480);
    }
}
