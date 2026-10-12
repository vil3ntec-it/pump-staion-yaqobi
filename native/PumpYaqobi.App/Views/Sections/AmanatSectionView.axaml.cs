using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

public partial class AmanatSectionView : UserControl
{
    private AmanatSectionViewModel? _vm;

    public AmanatSectionView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => Wire();
    }

    /// <summary>
    /// «📏 اندازه‌گیری» — ستون‌های «موجودی واقعی» و «اختلاف» پیش‌فرض پنهان‌اند
    /// (‎_amShowMeasure‎ی سایت). ستونِ ‎DataGrid‎ در درختِ بصری نیست و اتصالِ
    /// ‎IsVisible‎ رویش ‎DataContext‎ نمی‌یابد؛ پس همین‌جا از روی ویومدل.
    /// </summary>
    private void Wire()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVm;
        _vm = DataContext as AmanatSectionViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVm;
        ApplyMeasure();
    }

    private void OnVm(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AmanatSectionViewModel.ShowMeasure) or nameof(AmanatSectionViewModel.Page))
            ApplyMeasure();
    }

    private void ApplyMeasure()
    {
        if (this.FindControl<DataGrid>("AccGrid") is not { } g) return;
        var on = _vm?.ShowMeasure == true;
        foreach (var c in g.Columns)
            if (c.Header is string h && (h == AmanatAccountViewModel.HActual || h == AmanatAccountViewModel.HDiff))
                c.IsVisible = on;
    }
}
