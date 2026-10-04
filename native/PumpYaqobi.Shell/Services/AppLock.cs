namespace PumpYaqobi.App.Services;

/// <summary>حالِ قفلِ «فقط‌خواندنی»ِ کلِ برنامه.</summary>
public enum LockKind
{
    /// <summary>باز — هر کاری.</summary>
    None,
    /// <summary>اشتراکِ پولی تمام شده و هنوز در هفتهٔ هشدار است — هنوز می‌نویسد.</summary>
    PaidWarning,
    /// <summary>هنوز هیچ حسابی ساخته نشده.</summary>
    NoAccount,
    /// <summary>حساب هست، ولی هیچ اشتراک یا دورهٔ آزمایشی‌ای نرسیده.</summary>
    NoSubscription,
    /// <summary>دورهٔ آزمایشی تمام شد — بی هفتهٔ هشدار.</summary>
    TrialEnded,
    /// <summary>اشتراکِ پولی تمام شد و هفتهٔ هشدار هم گذشت.</summary>
    PaidEnded,
}

/// <param name="ReadOnly">هیچ نوشتنی در دفتر پذیرفته نمی‌شود — دیدن، چاپ، PDF و بکاپ باز.</param>
/// <param name="Kind">چرا.</param>
/// <param name="LockAt">در هفتهٔ هشدار: لحظهٔ بسته شدن (میلی‌ثانیه)؛ وگرنه صفر.</param>
/// <param name="DaysLeft">در هفتهٔ هشدار: روزهای ماندهٔ تا قفل (رو به بالا).</param>
public sealed record LockStatus(bool ReadOnly, LockKind Kind, long LockAt = 0, int DaysLeft = 0)
{
    public static readonly LockStatus Open = new(false, LockKind.None);
}

/// <summary>
/// ══ قفلِ «فقط‌خواندنی» — خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۲۰) ══════════════════════
///
/// «کسایی که اشتراک ندارن… همه برنامه و همه بخش‌ها قفل باشه… داخلش بشه رفت و
/// پی‌دی‌اف گرفت… فقط دیدنی باشه و هیچ کاری نتونه بکنه… برای کسایی که قبلن
/// اشتراک خریده بودن… یک هفته فرصت بزار… کسایی که آزمایشی داشتن از این هفته
/// محروم باشن.» و «تا حساب نسازد قفل».
///
/// ↩ این قاعدهٔ ۱۴۰۵/۰۷/۱۴ («اشتراک فقط شش بخش را می‌بندد، دفتر هرگز») را
/// به خواستهٔ تازهٔ خودِ صاحب ریپو <b>پس می‌گیرد</b>. آن روز ایرادِ اصلی این
/// بود که هر مجوزی که یک لحظه سنجیده نمی‌شد کلِ برنامه را می‌بست؛ پس این‌جا
/// فقط با <b>شاهدِ امضاشده</b> یا <b>نبودِ حساب</b> بسته می‌شود.
///
/// ⛔ <b>تنها جای این تصمیم همین تابعِ خالص است</b> (آزمون: <c>AppLockTests</c>).
/// ⛔ <b>فقط نوشتن را می‌بندد</b> — از درِ <c>PermissionService.ReadOnlyHook</c>.
///   دیدنِ همهٔ بخش‌ها، چاپ، PDF، بکاپ، ورود و ساختنِ حساب همیشه باز.
/// ⛔ <b>هیچ داده‌ای پاک یا پنهان نمی‌شود.</b>
/// </summary>
public static class AppLock
{
    /// <summary>هفتهٔ هشدار پس از پایانِ اشتراکِ <b>پولی</b>. آزمایشی ندارد.</summary>
    public static readonly TimeSpan PaidWarning = TimeSpan.FromDays(7);

    /// <param name="hasAccount">حسابی روی این کامپیوتر ساخته یا واردش شده.</param>
    /// <param name="open">اشتراک همین حالا باز است (مجوزِ زنده، پاسخِ سرور یا کلیدِ بی‌اینترنت).</param>
    /// <param name="check">مجوزِ امضاشدهٔ روی دیسک (ممکن است منقضی باشد)، یا <c>null</c>.</param>
    /// <param name="nowMs">حالا — از کفِ ساعت، نه ساعتِ خامِ ویندوز.</param>
    public static LockStatus Decide(bool hasAccount, bool open, LicenseCheck? check, long nowMs)
    {
        if (open) return LockStatus.Open;

        if (check is { SignatureOk: true })
        {
            //  پایانِ واقعیِ دوره: ‎sub_ends‎ — ولی مجوزی که مدتی آفلاین مانده و
            //  تازه نشده، بیش از ارفاقِ آفلاین (‎Entitlements.Grace‎) اعتبار ندارد.
            var offlineCap = check.ExpiresAt > 0 ? check.ExpiresAt + (long)Entitlements.Grace.TotalMilliseconds : long.MaxValue;
            var end = check.SubscriptionEndsAt > 0 ? Math.Min(check.SubscriptionEndsAt, offlineCap) : offlineCap;
            if (end == long.MaxValue) end = 0;

            if (check.IsTrial)
                return end > nowMs ? LockStatus.Open : new LockStatus(true, LockKind.TrialEnded);

            if (end > nowMs) return LockStatus.Open;
            var lockAt = end + (long)PaidWarning.TotalMilliseconds;
            return lockAt > nowMs
                ? new LockStatus(false, LockKind.PaidWarning, lockAt, Entitlements.DaysLeft(lockAt, nowMs))
                : new LockStatus(true, LockKind.PaidEnded);
        }

        return new LockStatus(true, hasAccount ? LockKind.NoSubscription : LockKind.NoAccount);
    }

    /// <summary>جملهٔ نوار برای هر حال — خالی یعنی چیزی برای گفتن نیست.</summary>
    public static string Sentence(LockStatus s) => s.Kind switch
    {
        LockKind.PaidWarning =>
            $"⚠️ اشتراکِ شما تمام شده — {s.DaysLeft} روز دیگر برنامه فقط‌خواندنی می‌شود. برای ادامهٔ کار اشتراک را تمدید کنید.",
        LockKind.NoAccount =>
            "🔒 برنامه فقط‌خواندنی است — برای شروع، از «پروفایل» حساب بسازید (با دورهٔ آزمایشیِ رایگان). دیدن، چاپ و PDF باز است.",
        LockKind.NoSubscription =>
            "🔒 برنامه فقط‌خواندنی است — اشتراکی برای این حساب نیست. دیدن، چاپ و PDF باز است؛ برای نوشتن اشتراک بخرید.",
        LockKind.TrialEnded =>
            "🔒 دورهٔ آزمایشی تمام شد — برنامه فقط‌خواندنی است. دیدن، چاپ و PDF باز است؛ برای ادامه اشتراک بخرید.",
        LockKind.PaidEnded =>
            "🔒 اشتراک تمام شد — برنامه فقط‌خواندنی است. هیچ داده‌ای پاک نشده؛ با تمدید همه‌چیز همان لحظه باز می‌شود.",
        _ => "",
    };

    /// <summary>پیامِ کوتاهِ «این کار نشد» وقتی کسی در حالِ قفل می‌خواهد بنویسد.</summary>
    public const string Denied = "🔒 برنامه فقط‌خواندنی است — برای نوشتن اشتراک لازم است (پروفایل ← اشتراک).";
}
