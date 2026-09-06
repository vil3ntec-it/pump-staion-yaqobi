using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PumpYaqobi.App.Views;

public partial class LockView : UserControl
{
    public LockView()
    {
        AvaloniaXamlLoader.Load(this);
        AttachedToVisualTree += (_, _) => this.FindControl<TextBox>("PwBox")?.Focus();
    }
}
