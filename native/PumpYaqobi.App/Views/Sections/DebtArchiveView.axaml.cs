using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
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
    public DebtArchiveView()
    {
        AvaloniaXamlLoader.Load(this);
        Loaded += (_, _) => WireGrids();
        //  ⚠️ ‎LayoutUpdated‎ برای **هر** چیدمانِ **هر جای** پنجره شلیک می‌شود
        //  و این صفحه — مثلِ همهٔ بخش‌ها — همیشه در درخت می‌ماند. بی این
        //  نگهبان، هر فریمِ اسکرولِ هر بخشِ دیگری یک ‎GetVisualDescendants‎ی
        //  کاملِ این صفحه را می‌دواند. همان قاعده‌ای که برای ‎ExcelGrid‎ و
        //  ‎TotalsBar‎ از ۱۴۰۵/۰۶/۲۶ نوشته شده:
        //  «هیچ شنوندهٔ ‎LayoutUpdated‎ی بی ‎IsEffectivelyVisible‎».
        LayoutUpdated += (_, _) =>
        {
            if (!IsEffectivelyVisible) return;
            WireGrids();
        };
    }

    private readonly HashSet<ExcelGrid> _wired = new();

    private void WireGrids()
    {
        foreach (var g in this.GetVisualDescendants().OfType<ExcelGrid>())
        {
            if (!_wired.Add(g)) continue;
            Apply(g);
            if (g.DataContext is INotifyPropertyChanged vm)
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(DebtArchiveViewModel.ShowFuelTypeColumn) or nameof(DebtArchiveViewModel.RowFilter))
                        Apply(g);
                };
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
