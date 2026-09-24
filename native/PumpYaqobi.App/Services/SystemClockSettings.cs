using System.Diagnostics;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «تاریخ و ساعت را خودم تنظیم کنم» (۱۴۰۵/۰۷/۱۳) ═══════════════════════════
///
/// خواستهٔ صاحب ریپو: «تاریخ یا ساعت را خودم نمی‌توانم تنظیم کنم.»
///
/// ⛔ برنامه <b>تاریخِ ساختگیِ خودش را نمی‌سازد</b>: تاریخِ هر ردیف، ماهِ هر
/// دفتر، مهرِ مجوز و ترمزِ ارفاق همه از ساعتِ کامپیوتر می‌آیند، و ساعتِ دوم
/// یعنی روزی ردیف در ماهِ اشتباه بنشیند. پس کلیک روی تاریخِ سربرگ همان
/// صفحهٔ «تاریخ و ساعتِ» خودِ ویندوز را باز می‌کند — تنها جای درستِ این کار.
///
/// ⚠️ رشته ثابت است و از هیچ‌جای بیرون نمی‌آید — همان الگوی
/// ‎PrintService.OpenPrinterSettings‎ (‎ms-settings:printers‎)، نه ‎SafeOpen‎.
/// </summary>
public static class SystemClockSettings
{
    /// <summary>⚠️ فقط برای آزمون.</summary>
    public static Func<ProcessStartInfo, bool>? TestStart { get; set; }

    public static bool Open()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (Start(new ProcessStartInfo("ms-settings:dateandtime") { UseShellExecute = true })) return true;
                return Start(new ProcessStartInfo("control.exe", "timedate.cpl") { UseShellExecute = true });
            }
            return false;
        }
        catch { return false; }
    }

    private static bool Start(ProcessStartInfo psi)
    {
        if (TestStart is { } t) return t(psi);
        try { return Process.Start(psi) is not null; }
        catch { return false; }
    }
}
