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
        HookCodeBoxes();
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

    // ══ شش خانهٔ کد — فقط فوکوس ══════════════════════════════════════════
    //
    //  ⛔ **هیچ تصمیمی این‌جا نیست.** «این نویسه رقم است؟ چسباندن بود؟ چند
    //  رقم شد؟» همه در `CodeBoxesViewModel` است و آزمون دارد. این‌جا فقط
    //  همان چیزی انجام می‌شود که فقط صفحه می‌تواند: جابه‌جا کردنِ فوکوس.
    //
    //  ⚠️ `Text` عمداً `OneWay` است: اگر دوطرفه بود، متنِ خامِ کادر (که
    //  می‌تواند رقمِ فارسی یا شش رقمِ چسبانده باشد) مستقیم در ویومدل
    //  می‌نشست و نرمال‌سازی دور می‌خورد.

    private bool _codeBusy;
    private bool _codeHooked;

    private TextBox?[] CodeCells()
    {
        var boxes = new TextBox?[ViewModels.CodeBoxesViewModel.Size];
        for (var i = 0; i < boxes.Length; i++) boxes[i] = this.FindControl<TextBox>("Code" + i);
        return boxes;
    }

    private void HookCodeBoxes()
    {
        //  ⚠️ یک بار: صفحه با هر بار دیده شدن دوباره به درخت می‌چسبد و
        //  بی این، هر بار یک شنوندهٔ تازه روی هر کادر می‌نشست.
        if (_codeHooked) return;
        var boxes = CodeCells();
        _codeHooked = true;
        for (var i = 0; i < boxes.Length; i++)
        {
            if (boxes[i] is not { } box) continue;
            var index = i;
            box.TextChanged += (_, _) => OnCodeTyped(index, box);
        }
    }

    private void OnCodeTyped(int index, TextBox box)
    {
        //  ⚠️ نگهبانِ بازگشت: نوشتنِ ویومدل روی کادر دوباره همین را صدا می‌زند
        if (_codeBusy) return;
        if (DataContext is not ViewModels.Sections.AccountSectionViewModel vm) return;

        _codeBusy = true;
        try
        {
            var next = vm.CodeBoxes.Put(index, box.Text);
            var boxes = CodeCells();
            if (next >= 0 && next < boxes.Length) boxes[next]?.Focus();
        }
        finally { _codeBusy = false; }
    }
}
