using Avalonia.Threading;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ تورِ ایمنی ═════════════════════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «بخشِ شرکت‌های تیل، می‌خواهم شرکتی اضافه کنم، از برنامه
/// می‌اندازد بیرون.» ریشه‌اش یک باگِ آن دکمه نبود — برنامه <b>هیچ</b> توری
/// نداشت: هر استثنایی که از یک فرمان یا رویداد بیرون می‌زد، مستقیم پنجره را
/// می‌بست و کاربر هیچ نمی‌فهمید چه شد. یک ردیفِ خرابِ دیتابیس یا یک اجازهٔ
/// نداشته، کلِ کار را می‌بست.
///
/// حالا:
///   ۱) خطای نخِ رابط (که فرمان‌های ‎AsyncRelayCommand‎ هم از همان‌جا بیرون
///      می‌زنند) گرفته می‌شود، برنامه باز می‌ماند و پیامِ کوتاهی پایین صفحه
///      نشان داده می‌شود.
///   ۲) هر خطایی — گرفته‌شده یا نه — در ‎crash.log‎ کنارِ دیتابیس نوشته
///      می‌شود، تا وقتی صاحب ریپو می‌گوید «بیرونم انداخت» چیزی برای خواندن
///      باشد.
///
/// ⚠️ این تور جای درست‌کردنِ باگ را نمی‌گیرد. کارش این است که یک باگِ کوچک،
/// دفترِ بازِ کاربر را نبندد.
/// </summary>
public static class CrashGuard
{
    private static readonly object Lock = new();

    /// <summary>پیشِ ساختنِ پنجره صدا زده می‌شود — در ‎Program.Main‎.</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("AppDomain", e.ExceptionObject as Exception);

        // ‎Task‎ی که کسی نتیجه‌اش را نخوانده و خطا داده — مثلاً ‎_ = DoAsync()‎
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("Task", e.Exception);
            e.SetObserved();
        };

        // خطای نخِ رابط: برنامه نباید بسته شود.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Write("UI", e.Exception);
            e.Handled = true;
            try { AppHost.Current.Toast(Friendly(e.Exception), ToastKind.Error); } catch { }
        };
    }

    /// <summary>
    /// اجرای یک کارِ کاربر با تور. اگر شکست، برنامه باز می‌ماند و کاربر
    /// می‌فهمد چه نشد — نه اینکه پنجره بی‌حرف بسته شود.
    /// </summary>
    public static async Task RunAsync(string what, Func<Task> work)
    {
        try { await work(); }
        catch (Exception ex)
        {
            Write(what, ex);
            try { AppHost.Current.Toast("⚠️ " + what + " نشد — " + Friendly(ex), ToastKind.Error); }
            catch { }
        }
    }

    /// <summary>کوتاه‌ترین جمله‌ای که به دردِ کاربر می‌خورد.</summary>
    private static string Friendly(Exception? ex)
    {
        var e = ex;
        while (e is AggregateException a && a.InnerException is not null) e = a.InnerException;
        return e?.Message is { Length: > 0 } m ? m : "خطای ناشناخته";
    }

    /// <summary>
    /// نوشتنِ خطا در ‎crash.log‎. خودش هیچ استثنایی بیرون نمی‌دهد — تورِ ایمنی
    /// که خودش بیفتد، هیچ‌کاره است.
    /// </summary>
    public static void Write(string where, Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var path = Path.Combine(AppSettings.Dir, "crash.log");
            Directory.CreateDirectory(AppSettings.Dir);
            var line = $"── {DateTime.Now:yyyy-MM-dd HH:mm:ss} · {where} ─────────────{Environment.NewLine}"
                     + ex + Environment.NewLine + Environment.NewLine;
            lock (Lock) File.AppendAllText(path, line);
        }
        catch { }
    }
}
