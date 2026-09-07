using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PumpYaqobi.App.Views;
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
    public static async Task ShowAsync(Func<IDocument> build, string title)
    {
        var owner = Owner;
        if (owner is null) return;

        // ورق‌ها همین‌جا — بیرونِ نخِ رابط — تصویر می‌شوند
        var vm = await Task.Run(() =>
        {
            // ثبتِ فونت‌ها؛ بارِ دوم به بعد فقط یک قفلِ خالی است
            PumpYaqobi.Reporting.Pdf.PdfEngine.Initialize();
            return new DocumentPreviewViewModel(build(), title);
        });

        await Dispatcher.UIThread.InvokeAsync(async () =>
            await new DocumentPreviewWindow(vm).ShowDialog(owner));
    }
}
