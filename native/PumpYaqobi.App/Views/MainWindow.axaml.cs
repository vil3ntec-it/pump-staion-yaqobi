using Avalonia.Controls;
using Avalonia.Threading;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer? _clock;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.Clock = PumpYaqobi.App.Localization.Clock.Now();
        };
        _clock.Start();
        if (DataContext is MainViewModel v) v.Clock = PumpYaqobi.App.Localization.Clock.Now();
    }
}
