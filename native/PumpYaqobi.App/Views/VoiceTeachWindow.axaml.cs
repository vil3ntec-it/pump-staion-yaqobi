using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Views;

/// <summary>
/// پنجرهٔ «🎓 آموزش صدا» — همان ‎#vxTeach‎ی نسخهٔ وب، ولی پنجرهٔ واقعیِ ویندوز.
/// </summary>
public partial class VoiceTeachWindow : Window
{
    public VoiceTeachWindow() => AvaloniaXamlLoader.Load(this);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// باز کردنش روی پنجرهٔ اصلی. در آزمونِ بی‌پنجره صاحبی در کار نیست، پس
    /// بی‌صدا برمی‌گردد به‌جای اینکه آزمون را معلق کند.
    /// </summary>
    public static async Task ShowAsync(Window? owner, VoiceTeachViewModel vm)
    {
        if (owner is null) return;
        await vm.LoadAsync();
        var w = new VoiceTeachWindow { DataContext = vm };
        await w.ShowDialog(owner);
    }
}
