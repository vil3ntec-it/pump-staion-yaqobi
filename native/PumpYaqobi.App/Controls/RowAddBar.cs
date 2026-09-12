using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ نوارِ «➕ ردیف» و «➕➕ چندتایی» — همتای ‎.row-add-bar‎ی نسخهٔ وب ══════════
///
/// در سایت زیرِ جدولِ مصارف، گاوصندوق، صرافی، رسید پارچه‌ها، چکنه و … یک نوار
/// هست: یک دکمهٔ «ردیف»، و کنارش کادرِ «تعداد:» با دکمهٔ «چندتایی» که همان‌قدر
/// ردیفِ خالی یک‌جا باز می‌کند. خودِ سایت هم نوشته چرا پایین است و نه در سربرگ:
///
///     «کادرهای ردیف جدید / چندتایی طبقِ خواستهٔ صاحب ریپو از سربرگ آمدند
///      پایینِ توضیحات، تا سربرگ شلوغ نباشد و دستِ آدم موقعِ کار به آن‌ها
///      نخورد.»
///
/// در برنامهٔ نیتیو «چندتایی» فقط با میان‌بُرِ صفحه‌کلید بود (‎Ctrl+عدد‎) و هیچ
/// دکمه‌ای نداشت — کسی که میان‌بُر را نمی‌دانست، راهی نداشت.
///
/// ⚠️ منطقِ تازه‌ای ساخته نشد: همان ‎IRowBatchHost.AddRowsAsync‎ی که میان‌بُر
/// صدا می‌زند، این‌جا هم صدا زده می‌شود. یک راه، دو در.
///
/// ⚠️ سقفِ ۵۰ ردیف هم از خودِ سایت است (‎n = Math.min(n, 50)‎).
/// </summary>
public class RowAddBar : TemplatedControl
{
    /// <summary>سقفِ یک‌بار افزودن — همان ‎Math.min(n, 50)‎ی سایت.</summary>
    public const int MaxRows = 50;

    /// <summary>پیش‌فرضِ کادرِ «تعداد» — سایت هم ‎value="5"‎ دارد.</summary>
    public const int DefaultCount = 5;

    public static readonly StyledProperty<object?> HostProperty =
        AvaloniaProperty.Register<RowAddBar, object?>(nameof(Host));

    public static readonly StyledProperty<ICommand?> AddRowCommandProperty =
        AvaloniaProperty.Register<RowAddBar, ICommand?>(nameof(AddRowCommand));

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<RowAddBar, int>(nameof(Count), DefaultCount);

    /// <summary>کسی که ردیف را می‌سازد — همان ویومدلِ بخش یا صفحهٔ باز.</summary>
    public object? Host { get => GetValue(HostProperty); set => SetValue(HostProperty, value); }

    /// <summary>«➕ ردیف» — یک ردیف، همان فرمانی که بخش از قبل داشت.</summary>
    public ICommand? AddRowCommand
    {
        get => GetValue(AddRowCommandProperty);
        set => SetValue(AddRowCommandProperty, value);
    }

    /// <summary>عددِ داخلِ کادرِ «تعداد».</summary>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    private Button? _many;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_many is not null) _many.Click -= OnMany;
        _many = e.NameScope.Find<Button>("PART_Many");
        if (_many is not null) _many.Click += OnMany;
    }

    private void OnMany(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // عددِ بی‌معنی هیچ کاری نمی‌کند — نه خطا، نه ردیفِ ناخواسته
        var n = Math.Clamp(Count, 0, MaxRows);
        if (n < 1) return;
        if (Host is IRowBatchHost h) _ = h.AddRowsAsync(n);
    }
}
