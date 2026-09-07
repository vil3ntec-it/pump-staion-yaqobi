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
        try
        {
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
