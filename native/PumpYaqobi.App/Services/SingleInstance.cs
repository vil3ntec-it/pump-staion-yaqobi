namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ قفلِ «یک نمونه» ═════════════════════════════════════════════════════
///
/// نام از هشِ <see cref="AppSettings.Dir"/> ساخته می‌شود، پس دو نصب با دو
/// پوشهٔ تنظیماتِ جدا (کاربرانِ دیگرِ ویندوز) هم را نمی‌بندند.
/// <c>Local\</c> یعنی فقط همین نشستِ ویندوز.
///
/// ⚠️ نمونهٔ دوم با یک رویدادِ نام‌دار به اولی می‌گوید «پنجره‌ات را نشان
/// بده». روی سیستمی که رویدادِ نام‌دار ندارد (لینوکس) فقط بیرون می‌رود —
/// قفل خودش سرِ جایش است.
/// </summary>
public static class SingleInstance
{
    private static System.Threading.Mutex? _mutex;
    private static System.Threading.EventWaitHandle? _wake;
    private static string? _key;

    private static string Key()
    {
        var dir = Path.GetFullPath(AppSettings.Dir).TrimEnd('\\', '/').ToLowerInvariant();
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(dir));
        return "PumpYaqobi-" + Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// قفل را بگیر. <c>false</c> ⇒ نمونهٔ دیگری روی همین پوشه باز است؛ به
    /// آن خبر داده شد و این نمونه باید بیرون برود. هر خطایی ⇒ <c>true</c>:
    /// قفلی که خودش جلوی بالا آمدن را بگیرد، از نبودنش بدتر است.
    /// </summary>
    public static bool Acquire()
    {
        string key;
        try { key = Key(); }
        catch { return true; /* قفلی نشد ساخت — برنامه باید بالا بیاید */ }

        try
        {
            _mutex = new System.Threading.Mutex(true, "Local\\" + key, out var mine);
            if (!mine)
            {
                bool got;
                try { got = _mutex.WaitOne(0); }
                //  نمونهٔ قبلی بی بستنِ درست مرده بود — حالا مالِ ماست
                catch (System.Threading.AbandonedMutexException) { got = true; }
                if (!got) { Wake(key); return false; }
            }
        }
        catch { return true; /* سیستمی که قفلِ نام‌دار ندارد — برنامه باید بالا بیاید */ }

        _key = key;
        return true;
    }

    /// <summary>به نمونهٔ اول بگو پنجره‌اش را جلو بیاورد.</summary>
    private static void Wake(string key)
    {
        try
        {
            using var ev = System.Threading.EventWaitHandle.OpenExisting("Local\\" + key + "-wake");
            ev.Set();
        }
        catch { /* نمونهٔ اول هنوز گوش نمی‌دهد یا سیستم پشتیبانی نمی‌کند */ }
    }

    /// <summary>
    /// نخِ پس‌زمینه‌ای که منتظرِ نمونهٔ دوم می‌ماند.
    ///
    /// ⚠️ <b>فقط بعد از بالا آمدنِ آوالونیا</b> (<c>App.OnFrameworkInitializationCompleted</c>):
    /// این نخ به <c>Dispatcher</c> پست می‌کند، و دست زدن به دیسپچر پیش از
    /// <c>UsePlatformDetect()</c> همان «پنجرهٔ سفیدِ مرده»ای است که
    /// <c>StartupOrderTests</c> قدغن کرده.
    /// </summary>
    public static void Listen()
    {
        var key = _key;
        if (key is null || _wake is not null) return;
        try
        {
            _wake = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset,
                                                         "Local\\" + key + "-wake");
        }
        catch { return; }

        var t = new System.Threading.Thread(() =>
        {
            while (true)
            {
                try { _wake.WaitOne(); }
                catch { return; }
                Avalonia.Threading.Dispatcher.UIThread.Post(ShowMain);
            }
        })
        { IsBackground = true, Name = "single-instance" };
        t.Start();
    }

    private static void ShowMain()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
                    { MainWindow: { } w })
            {
                if (w.WindowState == Avalonia.Controls.WindowState.Minimized)
                    w.WindowState = Avalonia.Controls.WindowState.Normal;
                w.Show();
                w.Activate();
            }
        }
        catch { /* جلو آوردنِ پنجره رفاه است */ }
    }
}
