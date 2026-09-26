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

            var scroll = this.FindControl<ScrollViewer>("PreviewScroll");
            var w = scroll?.Viewport.Width ?? 0;
            var h = scroll?.Viewport.Height ?? 0;
            if (w < 120 || h < 120) return;

            Vm.FitWidth = w - 56;          // جای حاشیه و نوارِ اسکرول
            Vm.FitHeight = h - 56;

            if (fitted) return;
            fitted = true;
            Vm.RenderScaling = RenderScaling;
            Vm.ZoomPageCommand.Execute(null);
        };

        // ورقِ تیز با پیکسلِ واقعیِ نمایشگر سنجیده می‌شود؛ بردنِ پنجره به
        // نمایشگرِ دیگر (۱۰۰٪ ⇄ ۱۵۰٪) همان را عوض می‌کند.
        ScalingChanged += (_, _) => { if (Vm is not null) Vm.RenderScaling = RenderScaling; };
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

    // ▲▼ِ «ورق‌ها: از … تا …» — همان ‎numBox‎ی سایت؛ به شمارِ ورق‌ها بریده می‌شود
    private void OnFromPlus(object? sender, RoutedEventArgs e) => BumpPage(true, +1);
    private void OnFromMinus(object? sender, RoutedEventArgs e) => BumpPage(true, -1);
    private void OnToPlus(object? sender, RoutedEventArgs e) => BumpPage(false, +1);
    private void OnToMinus(object? sender, RoutedEventArgs e) => BumpPage(false, -1);

    private void BumpPage(bool from, int step)
    {
        if (Vm is null) return;
        var max = Math.Max(1, Vm.PageCount);
        var cur = Shamsi.Num(from ? Vm.FromText : Vm.ToText);
        var n = (int)Math.Clamp(cur + step, 1m, max);
        if (from) Vm.FromText = Shamsi.Money(n); else Vm.ToText = Shamsi.Money(n);
    }

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

    private bool _printing;

    /// <summary>
    /// «🖨️ چاپ» — مستقیم به همان چاپگری که در فهرست برگزیده شده (مثلِ اکسل).
    ///
    /// ⚠️ راهِ قدیم («PDF را با فعلِ ‎print‎ به ویندوز بده») فقط وقتی می‌ماند
    /// که ویندوز هیچ چاپگری به ما نشان نداد: آن راه به برنامهٔ PDFخوانِ
    /// کامپیوتر بند است و روی کامپیوتری که PDF را با اج باز می‌کند بی‌صدا
    /// هیچ کاری نمی‌کرد — همان «اصلاً نمی‌آید» که صاحب ریپو دید.
    /// </summary>
    private async void OnPrint(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || _printing) return;
        if (Vm.PickedPages().Count == 0)
        { Vm.Status = "هیچ ورقی در این بازه نیست"; return; }

        if (Vm.Printer is { } printer)
        {
            _printing = true;
            try
            {
                Vm.Status = "در حالِ فرستادن به «" + printer.Name + "»…";
                var err = await Vm.PrintToAsync(printer);
                Vm.Status = err.Length == 0
                    ? "✅ به «" + printer.Name + "» فرستاده شد"
                    : "❌ " + err;
            }
            finally { _printing = false; }
            return;
        }

        if (Vm.PrintersLoading) { Vm.Status = "فهرستِ چاپگرها هنوز می‌آید — یک لحظه صبر کنید"; return; }

        var path = Vm.SaveTo(PrintService.DocsFolder);
        Vm.Status = PrintService.Print(path)
            ? "به چاپگرِ پیش‌فرضِ ویندوز فرستاده شد"
            : "ویندوز هیچ چاپگری نشان نداد — «افزودنِ چاپگر…» را بزنید، یا PDF بسازید";
    }

    /// <summary>«⬇️ ساختنِ فایلِ PDF» — می‌سازد و پوشه‌اش را باز می‌کند.</summary>
    private void OnPdf(object? sender, RoutedEventArgs e) => Save(reveal: true);

    /// <summary>«⬇️ ذخیرهٔ فایلِ گزارش» — همان، بی باز کردنِ پوشه.</summary>
    private void OnSave(object? sender, RoutedEventArgs e) => Save(reveal: false);

    private void Save(bool reveal)
    {
        if (Vm is null) return;
        var path = Vm.SaveTo(PrintService.DocsFolder);
        Vm.Status = "ذخیره شد: " + path;
        if (reveal) PrintService.Reveal(path);
    }

    /// <summary>«افزودنِ چاپگر / تنظیماتِ ویندوز…» — چاپگرهای خودِ ویندوز.</summary>
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
