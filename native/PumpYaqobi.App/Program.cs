using Avalonia;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // تورِ ایمنی پیش از هر چیزِ دیگر — وگرنه خطای همان لحظهٔ آغاز هم
        // بی‌ردّ‌ونشان برنامه را می‌بندد.
        CrashGuard.Install();

        //  ══ فقط یک نمونه روی هر پوشهٔ تنظیمات ═════════════════════════════
        //  ⛔ دو نمونهٔ هم‌زمان روی یک دفتر یعنی دو حلقهٔ همگام‌سازی، دو
        //  چرخشِ توکن و دو نوشتنِ `settings.json` روی هم — هر کدام عکسِ کهنهٔ
        //  دیگری را برمی‌گرداند. پس نمونهٔ دوم فقط پنجرهٔ اولی را جلو می‌آورد
        //  و بیرون می‌رود. ⚠️ این‌جاست و نه در `AppHost.Start`: سنجه‌ها و
        //  آزمون‌ها میزبان را مستقیم می‌سازند و نباید قفل شوند.
        if (!SingleInstance.Acquire()) return;

        try
        {
            // ══ میزبان روی نخِ دیگر، هم‌زمان با بالا آمدنِ آوالونیا ═══════════
            // سنجشِ ‎startup‎: میزبان (دیتابیس + EF + سرویس‌ها) ۲٫۶ ثانیه و
            // راه‌اندازیِ آوالونیا ۱ ثانیه بود — پشتِ سرِ هم. هیچ‌کدام به دیگری
            // نیاز ندارد، پس با هم می‌روند و ‎App‎ فقط منتظرِ همان یکی می‌ماند
            // (قفلِ ‎AppHost.Start‎ خودش انتظار را می‌سازد). خطایش هم گم نمی‌شود:
            // ‎Start‎ی دوم همان استثنا را دوباره می‌سازد و ‎CrashGuard‎ می‌گیرد.
            _ = System.Threading.Tasks.Task.Run(() => { try { AppHost.Start(); } catch { } });
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            CrashGuard.Write("Startup", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
