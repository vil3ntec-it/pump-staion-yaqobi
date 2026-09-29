using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace PumpYaqobi.App.Views.Sections;

public partial class CompanySearchView : UserControl
{
    public CompanySearchView()
    {
        AvaloniaXamlLoader.Load(this);
        // کادرِ مقدار همان لحظه آمادهٔ تایپ است — سربرگ گفت چه بنویسد
        AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("QtyBox")?.Focus(), DispatcherPriority.Input);
    }
}
