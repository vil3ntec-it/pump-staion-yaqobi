using Avalonia.Threading;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ شورا، ج۵ — وصل کردنِ درِ نخِ رابطِ پوسته به آوالونیا ═══════════════════════
///
/// ⛔ <c>ModuleInitializer</c>: همان لحظه‌ای که اسمبلیِ برنامه بار می‌شود — پیش از
/// هر توست و هر دامِ خطا، در برنامهٔ واقعی، در سنجه‌های رابط و در آزمون‌ها. پس
/// <see cref="ToastService"/> و <see cref="CrashGuard"/> مو‌به‌مو همان رفتارِ پیش از
/// جدایی را دارند.
/// </summary>
internal static class UiThreadAvalonia
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Wire()
    {
        UiThread.CheckAccess = () => Dispatcher.UIThread.CheckAccess();
        UiThread.Post = a => Dispatcher.UIThread.Post(a);
        UiThread.HookUnhandled = handle => Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            if (handle(e.Exception)) e.Handled = true;
        };
        //  و میزبان‌های ساعتِ گیت‌هاب — نشانیِ منبع فقط در ‎UpdateService.cs‎ نوشته می‌شود
        TimeSync.UpdateTimeHosts = () => Update.UpdateService.TimeHosts;
        TimeSync.UpdateProbeUrl = () => Update.UpdateService.TimeProbeUrl;
    }
}
