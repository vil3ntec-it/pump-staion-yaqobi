using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

/// <summary>
/// ستونِ «رسید» فقط در دفترِ پول و «رسید تیل» فقط در دفترِ تیل دیده می‌شود، و
/// «نوع تیل» فقط وقتی فیلتر روی «همه» است — همان قاعدهٔ حسابِ زنده (سایت:
/// «ستونِ واحدِ مقابل از جدول برداشته شود»). ستونِ ‎DataGrid‎ ‎DataContext‎
/// ندارد، پس مثلِ ‎PersonView‎ از روی سرستون پیدا و از کد نوشته می‌شود.
/// </summary>
public partial class DebtArchiveView : UserControl
{
    public DebtArchiveView() => AvaloniaXamlLoader.Load(this);

    // ⛔ هیچ شنوندهٔ ‎LayoutUpdated‎ی نیست (۱۴۰۵/۰۷/۱۶): تا امروز این صفحه با هر
    // چیدمانِ هر جای پنجره — هر فریمِ اسکرول، هر کلید — کلِ درختِ خودش را با
    // همهٔ ردیف‌ها و خانه‌ها می‌گشت تا جدول‌های تازه را پیدا کند، و هر جدولِ
    // دیده‌شده (با شنونده‌ای روی ویومدلش) برای همیشه در یک ‎HashSet‎ می‌ماند.
    // حالا هر جدول خودش خبر می‌دهد (‎DataContextChanged‎ در همان ‎axaml‎) و
    // شنوندهٔ ویومدلِ قبلی همان‌جا برداشته می‌شود.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ExcelGrid, Hook> _hooks = new();

    private sealed class Hook
    {
        public INotifyPropertyChanged? Vm;
        public PropertyChangedEventHandler? Handler;
    }

    private void OnArchiveGridContext(object? sender, EventArgs e)
    {
        if (sender is not ExcelGrid g) return;
        var hook = _hooks.GetValue(g, _ => new Hook());
        if (hook.Vm is { } old && hook.Handler is { } h) old.PropertyChanged -= h;
        hook.Vm = null; hook.Handler = null;

        Apply(g);
        if (g.DataContext is INotifyPropertyChanged vm)
        {
            PropertyChangedEventHandler handler = (_, pe) =>
            {
                if (pe.PropertyName is nameof(DebtArchiveViewModel.ShowFuelTypeColumn) or nameof(DebtArchiveViewModel.RowFilter))
                    Apply(g);
            };
            vm.PropertyChanged += handler;
            hook.Vm = vm; hook.Handler = handler;
        }
    }

    private static void Apply(ExcelGrid g)
    {
        if (g.DataContext is not DebtArchiveViewModel vm) return;
        foreach (var col in g.Columns)
        {
            var h = col.Header?.ToString() ?? "";
            col.IsVisible = h switch
            {
                "رسید" => vm.ShowRasidColumn,
                "رسید تیل" => vm.ShowRasidFuelColumn,
                "نوع تیل" => vm.ShowFuelTypeColumn,
                _ => col.IsVisible,
            };
        }
    }
}
