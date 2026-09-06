using Avalonia.Controls;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer? _clock;
    private readonly ShortcutService? _keys;
    private readonly FieldNavigationService? _fieldNav;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;

        // میانبرهای سراسری — همان‌هایی که کاربر در نسخهٔ وب داشت
        _keys = new ShortcutService(this, vm);

        // ناوبریِ اکسل‌مانندِ بینِ کادرهای فرم‌ها (جدول‌ها خودشان دارند)
        _fieldNav = new FieldNavigationService(this);

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) =>
        {
            vm.Clock = PumpYaqobi.App.Localization.Clock.Now();
        };
        _clock.Start();
        vm.Clock = PumpYaqobi.App.Localization.Clock.Now();
    }
}
