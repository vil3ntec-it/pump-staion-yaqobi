using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// ══ جدولِ تاریخچهٔ یک بخش با ستون‌های خودش (۱۴۰۵/۰۷/۱۲) ════════════════════
///
/// هر بخشی که ستون‌های خودش را دارد (‎HistoryService.ColumnsOf‎) یک جدولِ
/// **تازه** می‌گیرد — نه عوض کردنِ ستون‌های جدولِ قبلی: ‎ExcelGrid‎ پهنای
/// ستون‌ها را یک بار می‌سنجد و سفت می‌کند، و ستون‌های بخشِ دیگر روی همان
/// حالت یعنی پهنای غلط. نمونهٔ تازه حالتِ تازه دارد، و کلیدِ پهنای ذخیره‌شده‌اش
/// از نامِ سرستون‌ها ساخته می‌شود، پس هر بخش پهنای خودش را یادش می‌ماند.
/// </summary>
public partial class HistorySectionView : UserControl
{
    private HistorySectionViewModel? _vm;
    /// <summary>ستون‌هایی که جدولِ همین لحظه با آن‌ها ساخته شده — همان شیء.</summary>
    private object? _builtFor;

    public HistorySectionView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVm;
            _vm = DataContext as HistorySectionViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVm;
            Build();
        };
    }

    private void OnVm(object? s, PropertyChangedEventArgs e)
    {
        //  ⚠️ فقط با خودِ ستون‌ها، نه با ‎OpenKind‎: آن یکی پیش از ستون‌ها
        //  می‌نشیند و جدول با ستون‌های بخشِ **قبلی** ساخته می‌شد.
        if (e.PropertyName is nameof(HistorySectionViewModel.Columns)) Build();
    }

    private void Build()
    {
        if (this.FindControl<Border>("KindHost") is not { } host) return;
        var cols = _vm?.Columns;
        if (cols is null) { host.Child = null; _builtFor = null; return; }
        if (ReferenceEquals(_builtFor, cols) && host.Child is not null) return;
        _builtFor = cols;

        var g = new ExcelGrid
        {
            IsReadOnly = true,
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(0),
            //  ⛔ سرستون با اسکرول همراه نمی‌آید — همان جدولِ کلی (‎HeaderFollows‎)
            HeaderFollows = false,
        };
        for (var i = 0; i < cols.Count; i++)
            g.Columns.Add(Column(cols[i], i));
        g.Bind(DataGrid.ItemsSourceProperty, new Binding(nameof(HistorySectionViewModel.Rows)));
        host.Child = g;
    }

    private static DataGridColumn Column(HistoryCol c, int i)
    {
        var width = c.Wide ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto;
        var path = "Cells[" + i + "]";
        if (string.IsNullOrEmpty(c.Brush))
            return new DataGridTextColumn { Header = c.Header, Binding = new Binding(path), Width = width };

        //  ستونِ رنگی — همان کاری که ستونِ «مبلغ»ِ جدولِ کلی می‌کند
        return new DataGridTemplateColumn
        {
            Header = c.Header,
            Width = width,
            CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<HistoryRowViewModel>((_, _) =>
            {
                var t = new TextBlock
                {
                    FontWeight = FontWeight.Bold,
                    Margin = new Avalonia.Thickness(8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                t.Bind(TextBlock.TextProperty, new Binding(path));
                //  رنگِ ثابت از منبعِ تم (با تعویضِ تم عوض می‌شود)، «tone» از خودِ ردیف
                if (c.Brush == "tone")
                    t.Bind(TextBlock.ForegroundProperty, new Binding(nameof(HistoryRowViewModel.AmountBrushKey))
                        { Converter = ResourceKeyToBrushConverter.Instance });
                else
                    t.Bind(TextBlock.ForegroundProperty, t.GetResourceObservable(c.Brush));
                return t;
            }),
        };
    }
}
