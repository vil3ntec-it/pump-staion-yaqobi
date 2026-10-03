using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.Update;

/// <summary>
/// ══ به‌روزرسانیِ خودکار: خودش می‌بیند، خودش می‌گیرد، یک بار پیشنهاد می‌دهد ══════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۶): «برنامه وقتی نت داشت خودش خودکار آپدیت را
/// ببیند و دانلود کند و پیشنهاد بدهد که بگیر نصبش کن — یک بار پیشنهاد شود نه
/// بیشتر، و اگر آپدیتِ دیگری آمد دوباره پیشنهاد شود. هر آپدیت پیشنهادِ خودش را
/// داشته باشد.»
///
/// ── رفتار ──────────────────────────────────────────────────────────────────
///   • پس از ورود، <see cref="FirstDelay"/> صبر (صفحهٔ اول بی رقیب بیاید)، بعد هر
///     <see cref="Every"/> یک بار <see cref="UpdateService.CheckAsync"/>.
///     نرسیدن به اینترنت ⇒ <see cref="RetryOffline"/> بعد دوباره.
///   • نسخهٔ تازه ⇒ <b>بی‌صدا</b> گرفته می‌شود (همان <see cref="UpdateService.DownloadAsync"/>
///     با چک‌سام و امضا؛ هیچ راهِ دومی ساخته نشد).
///   • گرفته شد ⇒ <b>یک</b> پرسش: «نصب شود؟». ⛔ همان نسخه دیگر هیچ‌وقت پرسیده
///     نمی‌شود (<see cref="AppSettings.UpdateOfferedVersion"/>)؛ نسخهٔ تازه‌تر
///     پرسشِ خودش را دارد. «بعداً» یعنی از «تنظیمات ← بک‌اپ و به‌روزرسانی‌ها»
///     هر وقت خواست نصب کند — فایل همان‌جا آماده است.
///   • ⛔ هیچ‌وقت بی پرسش نصب نمی‌کند: نصب برنامه را می‌بندد.
///   • ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد.
/// </summary>
public static class AutoUpdate
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(90);
    //  ⚠️ پانزده دقیقه، نه شش ساعت (۱۴۰۵/۰۷/۱۹): پرسش حالا از سرورِ خودِ پمپ
    //  است — یک پاسخِ کوچک، بی سقفِ نرخ — و «درجا که گذاشتم برسد».
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RetryOffline = TimeSpan.FromMinutes(30);

    /// <summary>در آزمون‌ها و سنجه‌ها خاموش — همان قاعدهٔ ‎SyncEngine.Disabled‎.</summary>
    public static bool Disabled { get; set; }

    /// <summary>نسخهٔ گرفته‌شده و آمادهٔ نصب (صفحهٔ به‌روزرسانی از همین می‌خواند).</summary>
    public static (UpdateInfo Info, string Path)? Ready { get; private set; }

    /// <summary>نسخه‌ای آماده شد — صفحهٔ به‌روزرسانی همان لحظه «نصب» را نشان دهد.</summary>
    public static event Action? ReadyChanged;

    /// <summary>
    /// پرسشِ «نصب شود؟» — تزریق‌پذیر برای سنجه‌ها. خروجی ‎true‎ یعنی کاربر گفت نصب کن.
    /// </summary>
    public static Func<UpdateInfo, Task<bool>> Ask { get; set; } = info =>
        Dialogs.ConfirmAsync(OfferTitle, OfferText(info), "⬆️ نصب کن", "بعداً");

    public const string OfferTitle = "⬆️ نسخهٔ تازهٔ برنامه آماده است";

    /// <summary>متنِ پرسش — یک جا (سنجه‌ها همین را نشان می‌دهند).</summary>
    public static string OfferText(UpdateInfo info) =>
        "نسخهٔ " + info.LatestVersion + " گرفته شد و آمادهٔ نصب است.\n"
        + "برنامه بسته می‌شود، نصب می‌شود و دوباره باز می‌شود — هیچ داده‌ای دست نمی‌خورد.\n"
        + "اگر «بعداً» بزنید، دیگر برای همین نسخه پرسیده نمی‌شود؛ از «تنظیمات ← بک‌اپ و به‌روزرسانی‌ها» هر وقت خواستید نصبش کنید.";

    /// <summary>پرسش روی نخِ رابط — تزریق‌پذیر برای آزمون‌ها (بی پنجره).</summary>
    public static Func<Func<Task<bool>>, Task<bool>> OnUi { get; set; } =
        f => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(f);

    /// <summary>نصب — تزریق‌پذیر برای سنجه‌ها.</summary>
    public static Func<UpdateInfo, string, Task> Install { get; set; } = InstallAndExitAsync;

    private static int _started;

    /// <summary>یک بار در هر اجرا، پس از ورود.</summary>
    public static void Start()
    {
        if (Disabled || Interlocked.Exchange(ref _started, 1) == 1) return;
        _ = Task.Run(LoopAsync);
    }

    private static async Task LoopAsync()
    {
        try { await Task.Delay(FirstDelay); } catch { return; }
        while (!Disabled)
        {
            var next = Every;
            try { if (!await StepAsync()) next = RetryOffline; }
            catch { next = RetryOffline; }
            try { await Task.Delay(next); } catch { return; }
        }
    }

    /// <summary>
    /// یک دور: بپرس، اگر تازه بود بگیر، و اگر این نسخه هنوز پیشنهاد نشده، بپرس.
    /// ‎false‎ یعنی به سرورِ به‌روزرسانی نرسیدیم (زودتر دوباره).
    /// </summary>
    public static async Task<bool> StepAsync(UpdateService? svc = null, CancellationToken ct = default)
    {
        svc ??= new UpdateService();
        var info = await svc.CheckAsync(ct);
        if (info.Failed) return false;
        if (!info.Available) return true;

        if (Ready is not { } have || have.Info.LatestVersion != info.LatestVersion || !File.Exists(have.Path))
        {
            var path = await svc.DownloadAsync(info, null, ct);
            if (path is null) return false;
            Ready = (info, path);
            try { ReadyChanged?.Invoke(); } catch { }
        }

        if (!ShouldOffer(info.LatestVersion)) return true;
        MarkOffered(info.LatestVersion);
        var yes = await OnUi(() => Ask(info));
        //  ⛔ نصب (و فلاشِ نوشته‌های در صف) روی نخِ رابط (۱۴۰۵/۰۷/۱۶) — این حلقه روی نخِ
        //  پس‌زمینه است و ‎SaveGuard.FlushAllAsync‎ هم‌زمان با تایپِ کاربر ردیف‌ها را می‌نوشت.
        if (yes && Ready is { } r) await OnUi(async () => { await Install(r.Info, r.Path); return true; });
        return true;
    }

    /// <summary>این نسخه هنوز پیشنهاد نشده؟ (هر نسخه یک بار — خالص و آزمون‌دار)</summary>
    public static bool ShouldOffer(string version, string? offered = null)
    {
        offered ??= SafeOffered();
        return version.Length > 0 && !string.Equals(offered, version, StringComparison.Ordinal);
    }

    private static string SafeOffered()
    {
        try { return AppSettings.Load().UpdateOfferedVersion; } catch { return ""; }
    }

    private static void MarkOffered(string version)
    {
        try
        {
            var s = AppSettings.Load();
            s.UpdateOfferedVersion = version;
            s.Save();
        }
        catch { }
    }

    /// <summary>
    /// نصب و بستن — همان کارِ دکمهٔ «نصب»ِ صفحهٔ به‌روزرسانی (یک جا، دو در).
    /// ⚠️ اول هر نوشتهٔ در صف روی دیسک می‌نشیند؛ نصاب برنامه را می‌بندد.
    /// </summary>
    public static async Task InstallAndExitAsync(UpdateInfo info, string path)
    {
        try { await SaveGuard.FlushAllAsync(); } catch { }
        if (!UpdateService.Launch(path, info.LatestVersion))
        {
            AppHost.Current.Toast("❌ نصبِ نسخهٔ تازه انجام نشد — از «تنظیمات ← بک‌اپ و به‌روزرسانی‌ها» دوباره بگیرید", ToastKind.Error);
            Ready = null;
            try { ReadyChanged?.Invoke(); } catch { }
            return;
        }
        AppHost.Current.Toast("برنامه بسته می‌شود و با نسخهٔ تازه باز می‌شود", ToastKind.Info);
        _ = Task.Run(async () =>
        {
            await Task.Delay(1200);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)
                    d.Shutdown();
                else Environment.Exit(0);
            });
        });
    }

    /// <summary>فقط برای آزمون‌ها.</summary>
    public static void TestReset() { Ready = null; Interlocked.Exchange(ref _started, 0); }
}
