using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PumpYaqobi.App.Printing;

/// <summary>
/// ══ چاپ ════════════════════════════════════════════════════════════════════
/// سند اول به‌صورت یک فایلِ PDFِ واقعی نوشته می‌شود، بعد به خودِ ویندوز سپرده
/// می‌شود.
///
/// ⚠️ چرا این راه: در نسخهٔ وب، ‎window.print()‎ از داخلِ خودِ صفحه رشتهٔ اجرا را
/// می‌بست و تا بسته شدنِ پنجرهٔ چاپِ ویندوز کلِ برنامه یخ می‌زد (گزارشِ صاحب
/// ریپو). این‌جا چاپ یک پروسهٔ جداست و برنامه یک لحظه هم نمی‌ایستد.
/// </summary>
public static class PrintService
{
    /// <summary>پوشهٔ سندهای ساخته‌شده — کنارِ دادهٔ برنامه، نه در Temp.</summary>
    public static string DocsFolder
    {
        get
        {
            var d = Path.Combine(Services.AppSettings.Dir, "docs");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    /// <summary>فرستادنِ فایل به چاپگرِ پیش‌فرضِ ویندوز.</summary>
    public static bool Print(string pdfPath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(pdfPath) { Verb = "print", UseShellExecute = true });
                return true;
            }
            // روی لینوکس (فقط برای آزمون) با lp
            Process.Start(new ProcessStartInfo("lp", $"\"{pdfPath}\"") { UseShellExecute = false });
            return true;
        }
        catch { return false; }
    }

    /// <summary>باز کردنِ فایل با نمایشگرِ پیش‌فرض — «ذخیره و باز کن».</summary>
    public static bool Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    /// <summary>نشان دادنِ فایل در پوشه‌اش.</summary>
    public static void Reveal(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch { }
    }
}
