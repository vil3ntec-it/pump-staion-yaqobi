using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Update;

/// <summary>
/// ══ «برنامه برای نصب بسته می‌شود» — پرسشِ بکاپ سرِ راه نمی‌ایستد (۱۴۰۵/۰۷/۲۰) ══
/// گزارشِ صاحب ریپو: «وقتی برنامه را از داخل نصب و راه‌اندازیِ مجدد می‌کنم برنامه
/// بسته می‌شود و دوباره باز نمی‌شود — روی بعضی کامپیوترها.»
///
/// ریشه: بستنِ برنامه پرسشِ «پیش از بستن بکاپ گرفته شود؟» را باز می‌کرد (فقط
/// وقتی در همان اجرا چیزی نوشته شده — یعنی «بعضی کامپیوترها»). برنامه پشتِ آن
/// پنجره باز می‌ماند، نصاب نمی‌توانست ببندش، و نمونهٔ تازه‌ای که نصاب باز
/// می‌کرد قفلِ تک‌نمونه را باز می‌دید و بیرون می‌رفت. کاربر که پاسخ می‌داد،
/// برنامه بسته می‌شد و هیچ چیزی دوباره باز نمی‌شد.
///
/// ⛔ فقط آن پرسش رد می‌شود — هر نوشتهٔ در صف همچنان پیش از بستن روی دیسک
/// می‌نشیند، و نصاب خودش پیش از نصب بکاپ می‌گیرد.
/// ⚠️ مهلت دارد: «نصب از فایل» ویزاردی است که کاربر می‌تواند لغو کند؛ پس از
/// آن، بستنِ عادی دوباره می‌پرسد.
/// </summary>
public static class UpdateExit
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private static DateTime? _armedAt;

    public static void Arm() => _armedAt = AppClock.Mono;

    public static bool Active => Armed(_armedAt, AppClock.Mono);

    /// <summary>خالص: پرسشِ بکاپ رد شود؟</summary>
    public static bool Armed(DateTime? armedAt, DateTime now) =>
        armedAt is { } at && now >= at && now - at <= Window;

    /// <summary>فقط برای آزمون‌ها.</summary>
    public static void TestReset() => _armedAt = null;

    /// <summary>
    /// ══ بستن برای نصب — و پروسه واقعاً می‌رود (۱۴۰۵/۰۷/۲۲، بارِ سوم) ══════════════
    /// گزارشِ صاحب ریپو: «می‌گوید بسته و دوباره باز می‌شود؛ هرگز باز نمی‌شود و آپدیتی
    /// نشده.» بستنِ پنجره با رفتنِ پروسه یکی نیست: هر کاری که پس از ‎Shutdown‎ هنوز
    /// بدود، فایل‌های برنامه و قفلِ تک‌نمونه را نگه می‌دارد — نصاب یا اسکریپتِ زیپ
    /// نمی‌تواند جایشان بنویسد و نمونهٔ تازه پشتِ قفل می‌ماند و بیرون می‌رود.
    ///
    /// ⛔ پس پس از <see cref="ForceAfter"/> پروسه با ‎Environment.Exit‎ می‌رود. نوشته‌ها
    /// پیش از این روی دیسک نشسته‌اند (‎SaveGuard.FlushAllAsync‎ پیش از ‎Launch‎ و
    /// ‎ShutdownRequested‎)، و تنها درِ «بستن برای نصب» همین است — دو صفحه، یک کد.
    /// </summary>
    public static readonly TimeSpan ForceAfter = TimeSpan.FromSeconds(6);

    public static void CloseForInstall()
    {
        Arm();
        _ = Task.Run(async () =>
        {
            await Task.Delay(1200);
            //  نگهبان: هر چه پس از ‎Shutdown‎ ماند، پروسه را نگه ندارد
            _ = Task.Run(async () =>
            {
                await Task.Delay(ForceAfter);
                Environment.Exit(0);
            });
            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Avalonia.Application.Current?.ApplicationLifetime
                        is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)
                        d.Shutdown();
                    else Environment.Exit(0);
                });
            }
            catch { Environment.Exit(0); }
        });
    }
}
