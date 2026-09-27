using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Views;

/// <summary>پنجرهٔ «🕘 تاریخ و ساعت» — شرحش بالای <see cref="ClockViewModel"/>.</summary>
public partial class ClockWindow : Window
{
    private static ClockWindow? _open;
    private readonly DispatcherTimer _tick;

    public ClockWindow()
    {
        AvaloniaXamlLoader.Load(this);
        var vm = new ClockViewModel();
        DataContext = vm;
        //  ساعتِ زنده فقط تا پنجره باز است — بسته شد، تیک هم می‌ایستد
        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => vm.Tick(AppClock.Now);
        _tick.Start();
        Closed += (_, _) => _tick.Stop();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    public static async Task ShowAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner }) return;
        if (_open is { } already) { already.Activate(); return; }
        var w = new ClockWindow();
        _open = w;
        try { await w.ShowDialog(owner); }
        finally { _open = null; }
    }
}
