using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.App;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services.AppHost.Start();
        ThemeManager.Apply(PumpTheme.ById(Services.AppSettings.Load().ThemeId), this);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
