using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ ورود در سنجه‌ها — و منتظر ماندنش ═══════════════════════════════════════
///
/// ⚠️ از ۱۴۰۵/۰۶/۳۰ <c>LockViewModel.Submit</c> دیگر هم‌زمان نیست: هشِ رمز
/// (۲۱۰٬۰۰۰ دورِ PBKDF2 — سنجیده شد، ۱۴۰ میلی‌ثانیه) روی <b>نخِ دیگر</b>
/// می‌رود تا پنجره در هر ورود نخشکد.
///
/// یعنی <c>SubmitCommand.Execute(null)</c> حالا <b>همان لحظه برمی‌گردد</b> و
/// ورود هنوز تمام نشده. هر سنجه‌ای که بعدش کارش را ادامه بدهد، برنامه را
/// هنوز روی صفحهٔ قفل می‌بیند و <b>سبزِ دروغ</b> یا خطای بی‌ربط می‌دهد.
///
/// پس همهٔ سنجه‌ها از این‌جا وارد می‌شوند: دیسپچر را می‌پماند تا ادامهٔ کار
/// روی نخِ رابط بنشیند و <c>Phase</c> واقعاً <c>Ready</c> شود.
/// </summary>
internal static class LockIn
{
    /// <summary>
    /// دکمهٔ «ورود» را می‌زند و تا واقعاً تمام شدنش می‌ماند.
    /// رمز را خودِ سنجه از قبل گذاشته است.
    ///
    /// ⛔ <b>و نصبِ بی‌رمز اصلاً صفحهٔ قفل ندارد</b> (۱۴۰۵/۰۷/۰۷): برنامه خودش
    /// باز می‌شود (<c>AuthService.OpenWithoutPassword</c>). پس اگر رمزی
    /// گذاشته نشده، این‌جا هیچ دکمه‌ای زده نمی‌شود و فقط تا باز شدنِ واقعی
    /// منتظر می‌مانیم. زدنِ «ورود» روی حسابِ بی‌رمز فقط یک «رمز درست نیست»ِ
    /// بی‌معنا می‌ساخت و سنجه را دنبالِ باگی می‌فرستاد که وجود ندارد.
    /// </summary>
    public static void Wait(LockViewModel lockVm)
    {
        if (!lockVm.HasPassword()) { Settle(); return; }

        var t = lockVm.SubmitCommand.ExecuteAsync(null);
        //  همان حلقهٔ ‎Wait‎ی خودِ سنجه‌ها: پمپ کن و کمی بخواب، تا ادامهٔ
        //  پس از ‎await‎ روی نخِ رابط اجرا شود.
        for (var i = 0; i < 4000 && !t.IsCompleted; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(2);
        }
        Dispatcher.UIThread.RunJobs();
        if (t.IsFaulted) throw t.Exception!;
    }

    /// <summary>
    /// نصبِ بی‌رمز: فقط می‌پماند تا ورودِ خودکار واقعاً بنشیند.
    /// ⚠️ ملاک <c>Session.IsSignedIn</c> است، نه یک <c>Sleep</c>ِ ثابت —
    /// ورودِ بی‌رمز یک نوشتنِ دیتابیس روی نخِ دیگر دارد.
    /// </summary>
    private static void Settle()
    {
        for (var i = 0; i < 4000 && !AppHost.Current.Session.IsSignedIn; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(2);
        }
        Dispatcher.UIThread.RunJobs();
    }
}
