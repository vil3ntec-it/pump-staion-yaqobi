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
    public DocumentPreviewWindow()
    {
        AvaloniaXamlLoader.Load(this);

        // ورق باید هم‌قدِ پنجره باز شود، نه ۹۰۰ پیکسلِ ثابت. پنجره تازه پس از
        // چیده شدن پهنای واقعی‌اش را می‌داند، پس همان‌جا به ویومدل می‌رسد و
        // یک‌بار «اندازهٔ پنجره» زده می‌شود.
        var fitted = false;
        LayoutUpdated += (_, _) =>
        {
            if (fitted || Vm is null || Bounds.Width < 200) return;
            fitted = true;
            Vm.FitWidth = Bounds.Width - 80;      // جای حاشیه و نوارِ اسکرول
            Vm.ZoomFitCommand.Execute(null);
        };
    }

    public DocumentPreviewWindow(DocumentPreviewViewModel vm) : this() => DataContext = vm;

    private DocumentPreviewViewModel? Vm => DataContext as DocumentPreviewViewModel;

    /// <summary>هر بار که «تنظیمِ ورق» عوض شود، این صدا زده می‌شود تا ذخیره‌اش کند.</summary>
    public Action<Reporting.Pdf.PageSetup>? SetupChanged { get; set; }

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

    /// <summary>
    /// «⚙ تنظیمِ ورق» — همان کارگاهِ چاپِ نسخهٔ وب.
    ///
    /// ⚠️ سند از نو ساخته می‌شود، نه بزرگ‌نمایی: اندازهٔ کاغذ و حاشیه چیدمانِ
    /// جدول را عوض می‌کنند و ورقی که می‌بینید باید همانی باشد که چاپ می‌شود.
    /// ساختنش روی نخِ پس‌زمینه است تا پنجره یخ نزند.
    /// </summary>
    private async void OnSetup(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var next = await PrintSetupWindow.ShowAsync(this, Vm.Setup);
        if (next is null) return;

        Vm.Status = "در حال ساختنِ دوبارهٔ ورق…";
        try
        {
            await Task.Run(() => Vm.Rebuild(next));
            Vm.Status = "";
            SetupChanged?.Invoke(next);
        }
        catch (Exception ex)
        {
            // تنظیمی که سند را نمی‌سازد (کاغذِ خیلی کوچک، حاشیهٔ خیلی بزرگ)
            // نباید پنجره را ببندد — پیام می‌دهد و ورقِ قبلی سرِ جایش می‌ماند.
            Vm.Status = "این تنظیم روی ورق جا نمی‌شود: " + ex.Message;
            try { await Task.Run(() => Vm.Rebuild(Reporting.Pdf.PageSetup.Default)); } catch { }
        }
    }
}
