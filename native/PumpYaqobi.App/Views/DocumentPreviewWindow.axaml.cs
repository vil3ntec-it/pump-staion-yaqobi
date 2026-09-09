using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.Printing;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Views;

/// <summary>
/// ══ صفحهٔ چاپ ══════════════════════════════════════════════════════════════
/// همان پشتِ‌صحنهٔ چاپِ اکسل که صاحب ریپو با عکس خواست و سایت هم دارد: ستونِ
/// تنظیمات یک طرف، پیش‌نمایشِ زندهٔ ورق طرفِ دیگر.
///
/// ⚠️ چاپ و ذخیره هر دو از راهِ یک فایلِ PDFِ واقعی انجام می‌شوند تا برنامه —
/// برخلافِ نسخهٔ وب که با ‎window.print()‎ یخ می‌زد — لحظه‌ای هم نایستد.
/// </summary>
public partial class DocumentPreviewWindow : Window
{
    public DocumentPreviewWindow()
    {
        AvaloniaXamlLoader.Load(this);

        // ورق باید هم‌قدِ پنجره باز شود، نه یک عددِ ثابت. پنجره تازه پس از
        // چیده شدن اندازهٔ واقعی‌اش را می‌داند، پس همان‌جا به ویومدل می‌رسد و
        // یک‌بار «هم‌اندازهٔ ورق» زده می‌شود — همان کاری که اکسل می‌کند.
        var fitted = false;
        LayoutUpdated += (_, _) =>
        {
            if (Vm is null) return;

            // ⚠️ اندازه از خودِ فهرستِ برگه‌ها گرفته می‌شود. پیش از این یک
            // ‎ScrollViewer‎ بود؛ حالا فهرستِ ورق‌هاست و ‎FindControl<ScrollViewer>‎
            // همیشه ‎null‎ می‌داد — یعنی ورق دیگر هم‌قدِ پنجره باز نمی‌شد.
            var list = this.FindControl<ListBox>("PreviewScroll");
            var w = list?.Bounds.Width ?? 0;
            var h = list?.Bounds.Height ?? 0;
            if (w < 120 || h < 120) return;

            Vm.FitWidth = w - 72;          // جای حاشیه و نوارِ اسکرول
            Vm.FitHeight = h - 72;

            if (fitted) return;
            fitted = true;
            Vm.ZoomPageCommand.Execute(null);
        };

        // ══ صفحه‌کلید ══ برنامه بومی است؛ ‎Esc‎ باید ببندد.
        KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Escape) return;
            e.Handled = true;
            Close();
        };
    }

    public DocumentPreviewWindow(DocumentPreviewViewModel vm) : this()
    {
        DataContext = vm;
        vm.SetupChanged = s => SetupChanged?.Invoke(s);
    }

    private DocumentPreviewViewModel? Vm => DataContext as DocumentPreviewViewModel;

    /// <summary>هر بار که «تنظیمِ ورق» عوض شود، این صدا زده می‌شود تا ذخیره‌اش کند.</summary>
    public Action<Reporting.Pdf.PageSetup>? SetupChanged { get; set; }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    // ── تعدادِ نسخه ────────────────────────────────────────────────────────
    private void OnCopiesPlus(object? sender, RoutedEventArgs e) => Bump(+1);
    private void OnCopiesMinus(object? sender, RoutedEventArgs e) => Bump(-1);

    private void Bump(int step)
    {
        if (Vm is null) return;
        var n = (int)Math.Clamp(Shamsi.Num(Vm.CopiesText) + step, 1m, 999m);
        Vm.CopiesText = Shamsi.Money(n);
    }

    // ── چاپ و ذخیره ───────────────────────────────────────────────────────
    //
    // ⚠️ هر دو از ‎SaveTo‎ می‌گذرند، و ‎SaveTo‎ خودش «کدام ورق‌ها» و «تعدادِ
    // نسخه» را اعمال می‌کند — پس چیزی که چاپ می‌شود دقیقاً همان است که در
    // ستونِ تنظیمات انتخاب شده.

    private void OnPrint(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || !Vm.CanPrint) return;
        if (Vm.PickedPages().Count == 0)
        { Vm.Status = "هیچ ورقی در این بازه نیست"; return; }

        var path = Vm.SaveTo(PrintService.DocsFolder);
        Vm.Status = PrintService.Print(path) ? "به چاپگر فرستاده شد" : "چاپ انجام نشد";
    }

    /// <summary>«⬇️ ساختنِ فایلِ PDF» — می‌سازد و پوشه‌اش را باز می‌کند.</summary>
    private void OnPdf(object? sender, RoutedEventArgs e) => Save(reveal: true);

    /// <summary>«⬇️ ذخیرهٔ فایلِ گزارش» — همان، بی باز کردنِ پوشه.</summary>
    private void OnSave(object? sender, RoutedEventArgs e) => Save(reveal: false);

    private void Save(bool reveal)
    {
        if (Vm is null || !Vm.CanPrint) return;
        var path = Vm.SaveTo(PrintService.DocsFolder);
        Vm.Status = "ذخیره شد: " + path;
        if (reveal) PrintService.Reveal(path);
    }

    /// <summary>«انتخابِ چاپگر و تنظیماتش…» — چاپگرهای خودِ ویندوز.</summary>
    private void OnPrinters(object? sender, RoutedEventArgs e)
    {
        if (!PrintService.OpenPrinters() && Vm is not null)
            Vm.Status = "پنجرهٔ چاپگرها باز نشد — از تنظیماتِ خودِ ویندوز بازش کنید";
    }

    /// <summary>
    /// «تنظیمِ ورق…» — همان چهار زبانهٔ ‎Page Setup‎: کاغذ و جهت، حاشیه‌ها،
    /// سربرگ/پاورقی و مقیاس.
    ///
    /// ⚠️ سند از نو ساخته می‌شود، نه بزرگ‌نمایی: اندازهٔ کاغذ و حاشیه چیدمانِ
    /// جدول را عوض می‌کنند و ورقی که می‌بینید باید همانی باشد که چاپ می‌شود.
    /// </summary>
    private async void OnSetup(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var next = await PrintSetupWindow.ShowAsync(this, Vm.Setup);
        if (next is null) return;
        await Vm.ApplyFromDialogAsync(next);
    }
}
