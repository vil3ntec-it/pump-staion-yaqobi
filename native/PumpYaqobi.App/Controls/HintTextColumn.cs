using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ خانه‌ای که وقتی خالی است «نشانِ کم‌رنگ» دارد — ‎placeholder‎ی سایت ═══════
///
/// تیل امانت در سایت سه خانه دارد که خالی‌شان معنا دارد و عددِ جایگزین را
/// کم‌رنگ نشان می‌دهد: «مدت زمان» (خالی ⇐ «۱۲ روز» خودکار)، «درجه گرما» (خالی ⇐
/// گرمای پیش‌فرض) و «فیصدیِ من» (خالی ⇐ «↑۳» از حساب). این ستون همان را دارد:
///   • <see cref="HintPath"/> — نوشتهٔ کم‌رنگ؛ خالی یعنی هیچ.
///   • <see cref="ChipTextPath"/>/<see cref="ChipCommandPath"/> — دکمهٔ ریزِ کنارِ عدد
///     (چیپِ «↪ ۳٫۶٪»ِ زیرِ خانهٔ فیصدی). زدنش فقط عدد را در همان خانه می‌نویسد.
///
/// ⚠️ **هنوز یک ‎DataGridTextColumn‎ است** (مثلِ ‎IssueTextColumn‎): کپی/پیست/‎Delete‎ی
/// ‎ExcelGrid‎ مسیرش را از ‎Binding‎ِ همین ستون پیدا می‌کند.
/// </summary>
public sealed class HintTextColumn : DataGridTextColumn
{
    public string? HintPath { get; set; }
    public string? ChipTextPath { get; set; }
    public string? ChipCommandPath { get; set; }
    public string? ChipTipPath { get; set; }
    public string? ChipBrushPath { get; set; }

    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var inner = base.GenerateElement(cell, dataItem);
        if (string.IsNullOrWhiteSpace(HintPath) && string.IsNullOrWhiteSpace(ChipTextPath)) return inner;

        var host = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(inner, 0);
        host.Children.Add(inner);

        if (!string.IsNullOrWhiteSpace(HintPath))
        {
            var hint = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0),
                FontSize = 11.5,
                IsHitTestVisible = false,
            };
            hint.Bind(TextBlock.TextProperty, new Binding(HintPath));
            hint.Bind(Visual.IsVisibleProperty, new Binding(HintPath) { Converter = StringConverters.IsNotNullOrEmpty });
            hint.Bind(TextBlock.ForegroundProperty, hint.GetResourceObservable("Pump.Muted"));
            Grid.SetColumn(hint, 0);
            host.Children.Add(hint);
        }

        if (!string.IsNullOrWhiteSpace(ChipTextPath))
        {
            var chip = new Button
            {
                Padding = new Thickness(5, 0),
                Margin = new Thickness(2, 1),
                FontSize = 10.5,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Classes = { "ghost" },
            };
            chip.Bind(ContentControl.ContentProperty, new Binding(ChipTextPath));
            chip.Bind(Visual.IsVisibleProperty, new Binding(ChipTextPath) { Converter = StringConverters.IsNotNullOrEmpty });
            if (!string.IsNullOrWhiteSpace(ChipCommandPath))
                chip.Bind(Button.CommandProperty, new Binding(ChipCommandPath));
            if (!string.IsNullOrWhiteSpace(ChipTipPath))
                chip.Bind(ToolTip.TipProperty, new Binding(ChipTipPath));
            if (!string.IsNullOrWhiteSpace(ChipBrushPath))
                chip.Bind(Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, new Binding(ChipBrushPath)
                { Converter = Themes.ResourceKeyToBrushConverter.Instance, ConverterParameter = "ink" });
            Grid.SetColumn(chip, 1);
            host.Children.Add(chip);
        }
        return host;
    }
}
