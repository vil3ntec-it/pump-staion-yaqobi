using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.Printing;

namespace PumpYaqobi.App.Views;

/// <summary>
/// پنجرهٔ پیش‌نمایشِ سند. چاپ و ذخیره هر دو از راهِ فایلِ PDFِ واقعی انجام
/// می‌شوند تا برنامه — برخلافِ نسخهٔ وب — لحظه‌ای هم یخ نزند.
/// </summary>
public partial class DocumentPreviewWindow : Window
{
    public DocumentPreviewWindow() => AvaloniaXamlLoader.Load(this);

    public DocumentPreviewWindow(DocumentPreviewViewModel vm) : this() => DataContext = vm;

    private DocumentPreviewViewModel? Vm => DataContext as DocumentPreviewViewModel;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var path = Vm.SaveTo(PrintService.DocsFolder);
        Vm.Status = "ذخیره شد: " + path;
        PrintService.Reveal(path);
    }

    private void OnPrint(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var path = Vm.SaveTo(PrintService.DocsFolder);
        Vm.Status = PrintService.Print(path) ? "به چاپگر فرستاده شد" : "چاپ انجام نشد";
    }
}
