using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ قفلِ نرم — «اشتراک تمام شد، دفتر نه» ═══════════════════════════════
///
/// بندِ ۸ی پرامپتِ ۲۲: «قفلِ نرم هنگامِ انقضا + خروجی Excel/PDF همیشه
/// فعال.»
///
/// ── دقیقاً چه می‌شود و چه نمی‌شود ────────────────────────────────────
/// <code>
///   دیدنِ همهٔ بخش‌ها      ✅ همیشه
///   چاپ و PDF و بکاپ      ✅ همیشه
///   چتِ پشتیبانی          ✅ همیشه — «یکی از واجبات است»
///   همگام‌سازی            ✅ همیشه — چیزی که نوشته شده باید برسد
///   نوشتنِ تازه           ✅ همیشه — از ۱۴۰۵/۰۷/۱۴ (پایین)
///   شش بخشِ اشتراکی        🔒 تا تمدید — فقط از راهِ <see cref="Entitlements.Allows"/>
/// </code>
///
/// ⛔ <b>هیچ داده‌ای پاک یا پنهان نمی‌شود.</b> گروگان گرفتنِ دفترِ مشتری
/// سریع‌ترین راهِ از دست دادنِ اعتماد است — همان جمله‌ای که
/// <see cref="Entitlements"/> از روزِ اول با آن نوشته شده.
///
/// ⚠️ <b>از ۱۴۰۵/۰۷/۰۸ زنده است.</b> بینِ ۱۴۰۵/۰۷/۰۲ و آن روز هیچ‌وقت
/// روشن نمی‌شد، چون <see cref="Entitlements.Unlocked"/> به خواستهٔ صریحِ
/// صاحب ریپو <c>true</c> بود؛ حالا <c>false</c> است. این‌جا هیچ خطی از آن
/// تصمیم را دور نمی‌زند و هیچ‌وقت نزده — همان یک مقدار تنها کلیدِ کار است.
///
/// ⚠️ و حتی حالا هم <b>نصبِ بند‌نشده و مشتریِ درونِ ارفاق قفل نمی‌شوند</b>
/// (پیش از ۱۴۰۵/۰۷/۱۴): کسی که هیچ اشتراکی نخریده
/// چیزی ندارد که تمام شده باشد، و کسی که پول داده نباید با یک قطعیِ
/// اینترنت قفل شود.
///
/// ⚠️ و <see cref="Entitlements.TestDeny"/> از آن هم جلوتر است، پس
/// صاحبِ پمپ می‌تواند همین حالا قفلِ نرم را <b>ببیند</b> بی این‌که کسی
/// واقعاً قفل شود.
/// </summary>
public static class SoftLock
{
    /// <summary>
    /// مهلتِ نرم پس از پایانِ اشتراک — هفت روز، همان بندِ ۲۰٫۷.
    ///
    /// ⚠️ این با <see cref="Entitlements.Grace"/>ِ چهارده‌روزه یکی نیست و
    /// نباید بشود: آن یکی ارفاقِ «نرسیدن به سرور» است (شاید اشتراک اصلاً
    /// تمام نشده و ما خبر نداریم)، این یکی مهلتِ «تمام شده، ولی هنوز
    /// می‌نویسی». آن یکی همیشه جلوتر است، پس این هیچ‌وقت زودتر نمی‌بندد.
    /// </summary>
    public static readonly TimeSpan Warn = TimeSpan.FromDays(7);

    /// <summary>
    /// برنامه فقط‌خواندنی است؟ — از ۱۴۰۵/۰۷/۲۰ <b>بله، وقتی اشتراک نیست</b>.
    ///
    /// ↩ قاعدهٔ ۱۴۰۵/۰۷/۱۴ («هرگز؛ فقط شش بخش») به خواستهٔ تازهٔ صاحب ریپو پس
    /// گرفته شد. تصمیم فقط در <see cref="AppLock.Decide"/> است؛ این‌جا فقط
    /// ورودی‌هایش از روی دیسک جمع می‌شود.
    /// ⛔ ولی آن درسِ ۰۷/۱۴ سرِ جایش است: مجوزی که یک لحظه سنجیده نمی‌شود کلِ
    /// برنامه را نمی‌بندد — فقط <b>نبودِ حساب</b> یا <b>مجوزِ امضاشدهٔ تمام‌شده</b>
    /// یا <b>حسابِ بی هیچ مجوز</b>.
    /// </summary>
    public static bool ReadOnly => Status().ReadOnly;

    /// <summary>فقط برای آزمون‌های واحد (پوشهٔ موقتِ بی‌حساب) — برنامهٔ واقعی هرگز.</summary>
    public static bool Disabled { get; set; }

    private static LockStatus? _cached;
    private static DateTime _cachedAt = DateTime.MinValue;

    /// <summary>حالِ قفل — سه ثانیه در حافظه، چون هر نوشتن می‌پرسد و سنجشِ امضا ارزان نیست.</summary>
    public static LockStatus Status()
    {
        if (Disabled) return LockStatus.Open;
        var c = _cached;
        if (c is not null && AppClock.Mono - _cachedAt < TimeSpan.FromSeconds(3)) return c;
        c = Compute();
        _cached = c; _cachedAt = AppClock.Mono;
        return c;
    }

    /// <summary>
    /// آوردنِ بکاپ مجاز است؟ ‎null‎ ⇒ بله؛ وگرنه جملهٔ کاربر. تصمیم در
    /// <see cref="AppLock.RestoreBlocked"/>؛ این‌جا فقط ورودی‌ها از روی دیسک.
    /// ⚠️ برخلافِ قفلِ نوشتن، خطای خواندنِ مجوز این‌جا «باز» نیست: آوردنِ بکاپِ
    /// بیرونی کارِ روزمره نیست و درِ سوءاستفاده است — با مجوزِ ناخوانا بسته.
    /// </summary>
    public static string? RestoreBlocked()
    {
        if (Disabled) return null;
        if (Entitlements.Unlocked && !Entitlements.TestDeny) return null;
        try
        {
            var f = AppSettings.Load();
            var now = LicenseClock.Now(f);
            if (Entitlements.TestDeny) return AppLock.RestoreBlocked(false, null, false);
            var offline = OfflineKey.Stored(f, now).Valid;
            var check = string.IsNullOrWhiteSpace(f.CloudDeviceToken) ? null : LicenseGuard.CheckStored(f, now);
            return AppLock.RestoreBlocked(Entitlements.State(f).Open, check, offline);
        }
        catch { return AppLock.RestoreNoPlan; }
    }

    /// <summary>مجوز یا حساب عوض شد — حالِ قفل همان لحظه دوباره سنجیده شود.</summary>
    public static void Invalidate() => _cached = null;

    private static LockStatus Compute()
    {
        try
        {
            if (Disabled) return LockStatus.Open;
            if (Entitlements.Unlocked && !Entitlements.TestDeny) return LockStatus.Open;
            var f = AppSettings.Load();
            var hasAccount = !string.IsNullOrWhiteSpace(f.CloudAccountToken)
                          || !string.IsNullOrWhiteSpace(f.CloudUserId)
                          || !string.IsNullOrWhiteSpace(f.CloudDeviceToken);
            if (Entitlements.TestDeny) return new LockStatus(true, hasAccount ? LockKind.NoSubscription : LockKind.NoAccount);
            var now = LicenseClock.Now(f);
            var state = Entitlements.State(f);
            var check = string.IsNullOrWhiteSpace(f.CloudDeviceToken) ? null : LicenseGuard.CheckStored(f, now);
            return AppLock.Decide(hasAccount, state.Open, check, now);
        }
        catch
        {
            //  ⛔ خواندنِ تنظیمات یا مجوز شکست ⇒ باز. قفلِ ناخواسته بدتر از بازِ ناخواسته است.
            return LockStatus.Open;
        }
    }

    /// <summary>
    /// جملهٔ نوارِ بالای صفحه — خالی یعنی چیزی برای گفتن نیست.
    ///
    /// ⚠️ هفت روزِ آخر <b>پیش از</b> بسته شدن هشدار می‌دهد. کاربری که یک
    /// روز صبح برنامه‌اش را قفل ببیند حق دارد فکر کند خراب شده.
    /// </summary>
    /// <summary>
    /// نوعِ نوار: «grace» · «closed» · «soon» — خالی یعنی نواری نیست. «بستن»ِ
    /// کاربر همین را به یاد می‌سپارد (‎SeenNotices.DismissedBanner‎) تا همان نوع
    /// پیام تا عوض شدنِ حالِ اشتراک برنگردد.
    /// </summary>
    public static string BannerKind()
    {
        var t = Banner();
        return t.Length == 0 ? "" : t.StartsWith("🔒") ? "closed" : t.Contains("ارفاق") ? "grace" : "soon";
    }

    /// <summary>
    /// همان نوار، مگر کاربر همین نوعش را بسته باشد (۱۴۰۵/۰۷/۱۵: «بسته نمی‌شه و
    /// با هر بار باز شدنِ برنامه میاد»). حالِ اشتراک خوب شد ⇒ یادِ بستن هم پاک.
    /// </summary>
    public static string VisibleBanner()
    {
        var kind = BannerKind();
        if (kind.Length == 0) { if (SeenNotices.DismissedBanner.Length > 0) SeenNotices.DismissedBanner = ""; return ""; }
        return kind == SeenNotices.DismissedBanner ? "" : Banner();
    }

    public static string Banner()
    {
        if (Entitlements.Unlocked && !Entitlements.TestDeny) return "";
        var lockState = Status();
        if (lockState.Kind != LockKind.None) return AppLock.Sentence(lockState);

        var state = Entitlements.State();
        if (state.NotActivated) return "";

        if (state.InGrace)
            return $"⏳ {state.GraceDaysLeft} روز ارفاق — مجوز تازه نشده؛ یک بار به اینترنت وصل شوید.";

        if (!state.Open)
            return "🔒 اشتراک فعال نیست — بخش‌های اشتراکی (اپِ کارمندان، کیو‌آرِ زنده، بک‌اپِ ابری، "
                 + "مفاد/ضرر، تاریخچه‌ها، داشبورد) بسته‌اند.";

        var left = state.EntitledUntil - Entitlements.Now();
        if (left <= 0 || left > (long)Warn.TotalMilliseconds) return "";
        var days = (int)Math.Ceiling(left / 86_400_000d);
        return $"⏳ {days} روز تا پایانِ اشتراک — پس از آن برنامه فقط‌خواندنی می‌شود (اشتراکِ پولی یک هفته فرصت دارد).";
    }

    /// <summary>
    /// یک بار، سرِ بالا آمدنِ برنامه.
    /// </summary>
    /// ⛔ از ۱۴۰۵/۰۷/۲۰ قلابِ «فقط‌خواندنی» را می‌نشاند — تنها درِ اعمالِ
    /// <see cref="AppLock"/>؛ همهٔ نوشتن‌های دفتر از <c>PermissionService</c> می‌گذرند.
    public static void Install()
    {
        PermissionService.ReadOnlyHook = () => ReadOnly;
        PermissionService.RestoreGateHook = RestoreBlocked;
        CloudLink.LicenseChanged -= Invalidate;
        CloudLink.LicenseChanged += Invalidate;
    }
}
