using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Views;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.App.Printing;

/// <summary>
/// ══ باز کردنِ پیش‌نمایشِ سند ═════════════════════════════════════════════════
/// همان کاری که ‎pumpOpenDoc(html)‎ در نسخهٔ وب می‌کرد، فقط با یک PDFِ واقعی:
/// سند ساخته می‌شود، ورق‌هایش تصویر می‌شوند و پنجرهٔ پیش‌نمایش باز می‌شود.
///
/// ⚠️ ساختنِ سند روی نخِ پس‌زمینه انجام می‌شود، نه روی نخِ رابط: یک گزارشِ
/// چندصد ردیفی چند صدم ثانیه نیست و برنامه نباید همان لحظه یخ بزند — همان
/// چیزی که نسخهٔ وب موقعِ ‎window.print()‎ می‌کرد و صاحب ریپو از آن شکایت داشت.
///
/// ⚠️ «تنظیمِ ورق» کنارِ خودِ برنامه ذخیره می‌شود (نه در دیتابیس)، مثلِ
/// ‎localStorage‎ی نسخهٔ وب: کاغذ و حاشیه‌ای که کاربر یک‌بار انتخاب کرده،
/// دفعهٔ بعد هم همان است.
///
/// در آزمون‌های بی‌پنجره (headless) پنجرهٔ اصلی وجود ندارد؛ آن‌جا بی‌سروصدا
/// هیچ نمی‌کند تا آزمون معلق نماند — درست مثلِ <c>Dialogs</c>.
/// </summary>
public static class Documents
{
    private static Window? Owner =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow : null;

    /// <summary>
    /// سند را بساز و نشان بده. <paramref name="build"/> روی نخِ پس‌زمینه صدا
    /// زده می‌شود، پس نباید به چیزی از رابط دست بزند.
    /// </summary>
    public static async Task ShowAsync(Func<IDocument> build, string title) =>
        await ShowAsync(setup =>
        {
            var doc = build();
            // سندی که «تنظیمِ ورق» می‌فهمد، همان‌جا آن را می‌گیرد — پس هیچ‌کدام
            // از جاهایی که سند را می‌سازند لازم نیست از تنظیمِ ورق خبر داشته باشند.
            if (doc is ISetupDocument d) d.Setup = setup;
            return doc;
        }, title);

    /// <summary>
    /// همان، ولی سند «تنظیمِ ورق» را می‌گیرد — پس با هر بار عوض شدنِ تنظیم
    /// می‌تواند از نو ساخته شود.
    /// </summary>
    public static async Task ShowAsync(Func<PageSetup, IDocument> build, string title)
    {
        var owner = Owner;
        if (owner is null) return;

        var setup = AppSettings.LoadPrintSetup();

        // ورق‌ها همین‌جا — بیرونِ نخِ رابط — تصویر می‌شوند
        var vm = await Task.Run(() =>
        {
            // ثبتِ فونت‌ها؛ بارِ دوم به بعد فقط یک قفلِ خالی است
            PdfEngine.Initialize();
            return new DocumentPreviewViewModel(build, title, setup);
        });

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var win = new DocumentPreviewWindow(vm)
            {
                SetupChanged = AppSettings.SavePrintSetup,
            };
            await win.ShowDialog(owner);
        });
    }
}
