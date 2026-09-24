namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ چه چیزی در اشتراکِ این پمپ است ══════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «این‌ها را کاری کن که دیگر کار نکنند
/// و قابلِ دسترس نباشند مگر اشتراک داشته باشند: کیو‌آر، اپِ کارمندان و ربات،
/// بک‌اپِ ابری. پشتیبانی باشد، چون یکی از واجبات است.»
///
/// ── قاعدهٔ کلی ──────────────────────────────────────────────────────────────
///
///   دفترِ خودش  ⇒ همیشه باز. هیچ بخشی از قرض‌داران، ورق، پارچه، مخزن،
///                 گاوصندوق، فاکتور، مفاد و چاپ این‌جا سنجیده نمی‌شود.
///   پشتیبانی    ⇒ همیشه باز، حتی با اشتراکِ تمام‌شده.
///   سه چیزِ ابری ⇒ فقط با اشتراک: <see cref="Kar"/>، <see cref="QrLive"/>،
///                 <see cref="CloudBackup"/>.
///
/// ── «با یک باگ خراب نشود» ───────────────────────────────────────────────────
///
/// این مهم‌ترین قاعدهٔ این فایل است. کسی که پول داده نباید با نبودِ اینترنت،
/// سرورِ خاموش، ساعتِ عقب‌مانده یا یک اشتباهِ ما قفل شود. پس تصمیم
/// <b>سه‌لایه</b> است و هر لایه می‌تواند «باز» بگوید:
///
///   ۱) سرور همین حالا گفته اشتراک فعال است (<c>CloudLink.Subscription</c>)،
///   ۲) یا مجوزِ امضاشدهٔ ذخیره‌شده سالم است (<see cref="LicenseGuard"/>)،
///   ۳) یا مجوزِ امضاشدهٔ ذخیره‌شده تازه منقضی شده و از <c>exp</c>ش کمتر از
///      <see cref="Grace"/> گذشته — ⛔ و آن‌وقت فقط <b>همان پلنِ</b> همان
///      مجوز باز است. (تا ۱۴۰۵/۰۷/۱۲ این لایه از <c>AppSettings.EntitledUntil</c>ِ
///      بی‌امضا می‌آمد و هر شش قفل را باز می‌کرد — شرحش بالای
///      <see cref="State"/>.)
///
/// تنها حالتی که می‌بندد این است که <b>هر سه</b> بگویند نه — یعنی واقعاً
/// اشتراکی نیست. و حتی آن وقت هم دفتر و پشتیبانی بازند.
/// </summary>
public static class Entitlements
{
    /// <summary>اپِ کارمندان روی گوشی + رباتِ جست‌وجو (عکسِ زندهٔ پمپ).</summary>
    public const string Kar = "kar";

    /// <summary>کیو‌آرِ حسابِ مشتری و به‌روزرسانیِ زنده‌اش.</summary>
    public const string QrLive = "qrlive";

    /// <summary>بک‌اپِ خودکار روی سرور/ابر.</summary>
    public const string CloudBackup = "cloudbackup";

    //  ── سه دروازهٔ تازه، از مرزِ «استاندارد ⇄ وی‌آی‌پی» ────────────────
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۳۰) دربارهٔ پلنِ استاندارد: «مفاد و
    //  ضرر براش نشون داده نشه یا هم تار باشه که هیچی دیده نشه، نه فایده نه
    //  ضررشو… تاریخچه‌ها هم بسته بشه و برای هیچ بخشی تاریخچه‌ای نباشه… و
    //  داشبورد هم قفل باشه.»
    //
    //  ⛔ و در همان جمله، قاعده‌ای که این سه را از «گروگان گرفتنِ دفتر» جدا
    //  می‌کند: «این‌ها هم براشون باشن، اطلاعاتشون باشن ولی دیده نتونن، و
    //  اگه یارو بار دیگه وی‌آی‌پی یا دائمی رو خرید قفلِ اون‌ها باز بشه.»
    //  یعنی هیچ محاسبه‌ای خاموش نمی‌شود و هیچ ردیفی پاک نمی‌شود — فقط درِ
    //  صفحه بسته است.

    /// <summary>بخشِ «مفاد / ضرر / اتحادیه» و همان عددش در صفحهٔ اصلی.</summary>
    /// <remarks>
    /// ⚠️ <b>نرخِ اتحادیه آسیب نمی‌بیند.</b> جملهٔ خودِ صاحب ریپو: «ببین که
    /// نرخِ اتحادیه این وسط آسیب نبینه.» نرخ این‌جا فقط <b>دیده</b> می‌شود؛
    /// خواندنش برای زیانِ افزایشِ قیمت و فاکتورها از تاریخچهٔ اتحادیه است و
    /// این قفل به آن دست نمی‌زند.
    /// </remarks>
    public const string Profit = "profit";

    /// <summary>بخشِ «تاریخچه‌ها» و دکمهٔ تاریخچهٔ هر بخش.</summary>
    public const string History = "history";

    /// <summary>صفحهٔ داشبورد.</summary>
    public const string Dashboard = "dashboard";

    /// <summary>چتِ پشتیبانی — «یکی از واجبات است»، پس هرگز قفل نمی‌شود.</summary>
    public const string Support = "support";

    /// <summary>همهٔ چیزهایی که اشتراک می‌خواهند — بی ترتیبِ خاص.</summary>
    public static readonly string[] Paid =
        { Kar, QrLive, CloudBackup, Profit, History, Dashboard };

    /// <summary>نامِ فارسیِ هر کدام، برای پیام و صفحهٔ پروفایل.</summary>
    public static string TitleOf(string feature) => feature switch
    {
        Kar => "اپِ کارمندان و ربات",
        QrLive => "کیو‌آرِ حسابِ مشتری",
        CloudBackup => "بک‌اپِ خودکار روی سرور",
        Profit => "مفاد / ضرر / اتحادیه",
        History => "تاریخچه‌ها",
        Dashboard => "داشبورد",
        Support => "چتِ پشتیبانی",
        _ => feature,
    };

    /// <summary>
    /// ارفاقِ پس از پایانِ اشتراک — دو هفته.
    ///
    /// ⚠️ این عدد را کم نکنید. مجوز کوتاه‌عمر است و برنامه‌ای که دو هفته
    /// آفلاین بماند هنوز مشتریِ پول‌داده است، نه دور‌زننده.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromDays(14);

    /// <summary>
    /// «آزمایشِ حالتِ بی‌اشتراک» — خواستهٔ صاحب ریپو: «بخشِ وی‌آی‌پی را اعمال
    /// کن که من ببینم و تست کنم.» با روشن بودنش هر سه کارِ ابری بسته دیده
    /// می‌شوند، پس صاحبِ پمپ می‌تواند خودش قفل‌ها را ببیند.
    ///
    /// ⚠️ <b>فقط می‌بندد، هیچ‌وقت باز نمی‌کند</b> — پس راهِ دور زدنِ اشتراک
    /// نیست. و فقط در حافظه است: با بسته شدنِ برنامه خودش می‌رود
    /// (روی دیسک نمی‌نشیند).
    /// </summary>
    public static bool TestDeny { get; set; }

    /// <summary>
    /// ⏳ <b>قفل‌ها دوباره بازند — خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۲):</b>
    /// «الان قفلی نزار روی برنامه برای فعلاً؛ هر وقت خواستم بهت می‌گم که
    /// دوباره بذاریش.»
    ///
    /// ── تاریخچه، تا فردا کسی دوباره نپرسد ──────────────────────────────
    /// ۱۴۰۵/۰۷/۰۲ باز شد · ۱۴۰۵/۰۷/۰۸ به خواستهٔ خودش بسته شد · و امروز
    /// دوباره باز. هر بار <b>فقط همین یک مقدار</b> عوض شده و هیچ چیزِ
    /// دیگری — نه مجوز، نه ارفاق، نه فهرستِ پلن، نه هیچ پیامی.
    ///
    /// با <c>true</c> بودنش هر شش دروازهٔ <see cref="Paid"/> (اپِ کارمندان ·
    /// کیو‌آر · بک‌اپِ ابری · مفاد/ضرر · تاریخچه‌ها · داشبورد) برای همه باز
    /// است — با اشتراک، بی اشتراک، فعال‌نشده.
    ///
    /// ⛔ <b>برای قفل کردنِ دوباره فقط همین یک مقدار را <c>false</c> کنید.</b>
    /// <see cref="SoftLock"/> هم از همین می‌خواند، پس تا این <c>true</c>
    /// است هیچ اشتراکِ تمام‌شده‌ای برنامه را فقط‌خواندنی نمی‌کند.
    ///
    /// ⚠️ مرزِ واقعیِ پلن‌ها همچنان زیرِ آزمون است: <c>EntitlementsTests</c>
    /// با <see cref="Unlocked"/> = <c>false</c> می‌دود، پس روزِ قفل کردن
    /// غافلگیر نمی‌شوید.
    ///
    /// ⚠️ <see cref="TestDeny"/> از این هم جلوتر است و همیشه بوده: کلیدِ
    /// «آزمایشِ حالتِ بی‌اشتراک» می‌بندد، هیچ‌وقت باز نمی‌کند.
    /// </summary>
    public const bool UnlockedForNow = true;

    /// <summary>
    /// همان <see cref="UnlockedForNow"/>، ولی تزریق‌پذیر — فقط برای آزمون‌ها
    /// که مرزِ واقعیِ پلن‌ها را با قفلِ روشن بسنجند. در خودِ برنامه هیچ‌جا
    /// نوشته نمی‌شود.
    /// </summary>
    public static bool Unlocked { get; set; } = UnlockedForNow;

    /// <summary>حالا — تزریق‌پذیر تا آزمون بتواند زمان را جلو ببرد.</summary>
    public static Func<long> Now { get; set; } =
        () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// این کار باز است؟
    ///
    /// <paramref name="feature"/> یکی از چهار رشتهٔ بالا. هر چیزِ ناشناسِ
    /// دیگری **باز** شمرده می‌شود: قفلِ ناخواسته بدتر از بازِ ناخواسته است.
    /// </summary>
    public static bool Allows(string feature)
    {
        if (Array.IndexOf(Paid, feature) < 0) return true;   // پشتیبانی و هر چیزِ دیگر
        return State().Allows(feature);
    }

    /// <summary>
    /// «باز است؟ اگر نه، خودت به کاربر بگو.» — تنها راهِ قفل کردنِ یک دکمه.
    ///
    /// ⚠️ بی‌صدا رد نشوید: کاربری که دکمه را می‌زند و هیچ اتفاقی نمی‌افتد
    /// فکر می‌کند برنامه خراب است، نه این‌که اشتراک لازم دارد.
    /// </summary>
    public static bool Gate(AppHost host, string feature)
    {
        if (Allows(feature)) return true;
        host.Toast("🔒 " + Why(feature), ToastKind.Warn);
        return false;
    }

    /// <summary>چرا بسته است — یک جملهٔ آمادهٔ نمایش. خالی یعنی باز است.</summary>
    public static string Why(string feature) =>
        Allows(feature) ? "" : State().Why(TitleOf(feature));

    /// <summary>
    /// حالِ اشتراک، از روی تنظیماتِ روی دیسک و — اگر داشته باشیم — پاسخِ
    /// همین لحظهٔ سرور.
    ///
    /// ⛔ <b>ارفاق فقط از مجوزِ امضاشده می‌آید</b> (۱۴۰۵/۰۷/۱۲). تا دیروز
    /// <c>AppSettings.EntitledUntil</c> — یک عددِ ساده در فایلِ تنظیمات — ارفاق
    /// را می‌ساخت، و <c>InGrace</c> پیش از فهرستِ پلن جواب می‌داد. یعنی:
    ///   • نوشتنِ «۲۱۰۰» در همان یک خط ⇒ ارفاقِ همیشگی و همهٔ قفل‌ها باز؛
    ///   • پلنِ <b>استاندارد</b> در ارفاق ⇒ هر شش قفلِ وی‌آی‌پی باز؛
    ///   • برداشتنِ اشتراک از پنل ⇒ مُهری که پاسخِ بی‌امضای <c>/me</c> جلو
    ///     برده بود، ماه‌ها باز نگهش می‌داشت.
    /// حالا ارفاق = مجوزی که امضا و هویتش سالم است و فقط تازه منقضی شده، تا
    /// <c>exp + Grace</c> — و در ارفاق همان <b>فهرستِ همان مجوز</b> اعمال
    /// می‌شود. <c>EntitledUntil</c> دیگر هیچ دری را باز نمی‌کند.
    ///
    /// ⚠️ <paramref name="live"/> (پاسخِ بی‌امضای همین لحظه) هنوز می‌تواند
    /// همین نشست را «باز» نشان دهد — همان «باگِ صدورِ مجوز نباید مشتری را
    /// ببندد» — ولی ⛔ <b>هیچ چیزی از آن روی دیسک نمی‌نشیند</b>
    /// (<see cref="Remember"/>).
    /// </summary>
    public static EntitlementState State(AppSettings? file = null, PumpSubscription? live = null)
    {
        var f = file ?? AppSettings.Load();

        //  ۱) هنوز با کدِ اشتراک فعال نشده — هیچ‌کدام از این سه کار بی ابر
        //     معنا هم ندارد.
        if (string.IsNullOrWhiteSpace(f.CloudDeviceToken))
            return new EntitlementState(false, Array.Empty<string>(), "", 0, Now(), true);

        //  ۲) مجوزِ امضاشده — همان چیزی که آفلاین هم کار می‌کند. «حالا» از
        //     کفِ ساعت می‌آید، نه ساعتِ خامِ ویندوز (`LicenseClock`).
        var now = LicenseClock.Now(f);
        var check = LicenseGuard.CheckStored(f, now);

        var until = 0L;
        var plan = f.EntitledPlan;
        var feats = Array.Empty<string>() as IReadOnlyList<string>;
        var graceEnds = 0L;

        //  ⚠️ «فهرست نیامده» ≠ «فهرست خالی». نیامده یعنی مجوزِ نسلِ اول و
        //  پلنِ کامل؛ خالی یعنی پلنِ پایه و هیچ‌کدام. شرحش در
        //  ‎LicenseCheck.HasFeatureList‎.
        var listed = false;

        //  ⛔ هم مجوزِ زنده و هم مجوزِ امضاشدهٔ تازه‌منقضی فهرستِ **خودشان** را
        //  می‌دهند — ارفاق هیچ‌وقت پلن را گشادتر نمی‌کند.
        if (check.SignatureOk)
        {
            feats = check.Features;
            listed = check.HasFeatureList;
            plan = string.IsNullOrWhiteSpace(check.PlanTitle) ? plan : check.PlanTitle;
            until = Math.Max(check.SubscriptionEndsAt, check.ExpiresAt);
            if (check.Expired && check.ExpiresAt > 0)
                graceEnds = check.ExpiresAt + (long)Grace.TotalMilliseconds;
        }

        //  ۳) و اگر سرور همین حالا «فعال» گفته باشد، همان حرفِ آخر است —
        //     حتی اگر مجوزِ تازه هنوز نرسیده باشد (باگِ صدورِ مجوز نباید
        //     مشتریِ پول‌داده را ببندد). ⚠️ فقط برای همین نشست، در حافظه.
        if (live is { Active: true })
        {
            if (live.Features.Count > 0) { feats = live.Features; listed = true; }
            if (!string.IsNullOrWhiteSpace(live.PlanTitle)) plan = live.PlanTitle;
            until = Math.Max(until, live.EndsAt > 0 ? live.EndsAt : now);
        }

        var open = check.Valid || live is { Active: true };
        return new EntitlementState(open, feats, plan, until, now, false, listed, graceEnds);
    }

    /// <summary>
    /// مُهرِ «دیدیم که باز است» را روی دیسک به‌روز می‌کند — از
    /// <see cref="CloudLink.RefreshAsync"/> و فعال‌سازی صدا می‌خورد.
    ///
    /// ⛔ <b>فقط از مجوزی که امضایش سنجیده شده.</b> پاسخِ بی‌امضای
    /// <c>/me</c> (<paramref name="live"/>) دیگر هیچ عددی را جلو نمی‌برد: هر
    /// چه از آن روی دیسک بنشیند، یک ویرایشگرِ متن هم می‌تواند بنشاند.
    ///
    /// ⚠️ <c>EntitledUntil</c> از امروز فقط برای <b>نمایش</b> است (آخرین
    /// <c>exp</c>/<c>sub_ends</c>ِ امضاشده) و هیچ قفلی را باز نمی‌کند.
    /// فقط جلو می‌رود، هرگز عقب نمی‌آید.
    /// </summary>
    public static void Remember(AppSettings f, PumpSubscription? live, LicenseCheck? check)
    {
        _ = live;   // ⛔ عمداً خوانده نمی‌شود — بالا نوشته چرا
        if (check is not { SignatureOk: true }) return;

        var until = Math.Max(f.EntitledUntil, Math.Max(check.SubscriptionEndsAt, check.ExpiresAt));
        if (until > f.EntitledUntil) f.EntitledUntil = until;
        if (!string.IsNullOrWhiteSpace(check.PlanTitle) && check.PlanTitle != f.EntitledPlan)
            f.EntitledPlan = check.PlanTitle;
    }
}

/// <summary>
/// عکسِ حالِ اشتراک در یک لحظه — همان چیزی که <see cref="Entitlements.Allows"/>
/// روی آن تصمیم می‌گیرد و صفحهٔ پروفایل نشانش می‌دهد.
/// </summary>
/// <param name="Open">مجوز یا پاسخِ سرور همین حالا «باز» می‌گوید.</param>
/// <param name="Features">فهرستِ کارهای بازِ پلن. خالی = «همه» (پلنِ کامل یا مجوزِ نسلِ اول).</param>
/// <param name="PlanTitle">نامِ پلن، برای نمایش.</param>
/// <param name="EntitledUntil">آخرین لحظه‌ای که «باز» دیده شده.</param>
/// <param name="NowMs">همین حالا.</param>
/// <param name="NotActivated">هنوز با کدِ اشتراک فعال نشده.</param>
/// <param name="Listed">
/// پلن فهرستِ کارهایش را صریح گفته. دروغ یعنی مجوزِ نسلِ اول ⇒ پلنِ کامل.
/// </param>
/// <param name="GraceEndsAt">
/// پایانِ ارفاق = <c>exp</c>ِ مجوزِ امضاشدهٔ منقضی + <see cref="Entitlements.Grace"/>.
/// صفر یعنی ارفاقی در کار نیست (مجوزی نیست، امضایش سالم نیست، یا هنوز
/// منقضی نشده). ⛔ هیچ عددِ بی‌امضایی این را نمی‌سازد.
/// </param>
public sealed record EntitlementState(
    bool Open,
    IReadOnlyList<string> Features,
    string PlanTitle,
    long EntitledUntil,
    long NowMs,
    bool NotActivated,
    bool Listed = false,
    long GraceEndsAt = 0)
{
    /// <summary>ارفاق تا این لحظه ادامه دارد.</summary>
    public long GraceUntil => GraceEndsAt;

    /// <summary>اشتراک تمام شده ولی هنوز در ارفاقیم.</summary>
    public bool InGrace => !Open && GraceEndsAt > 0 && NowMs < GraceEndsAt;

    /// <summary>روزهای ماندهٔ ارفاق — برای نوشتنِ «۹ روز ارفاق».</summary>
    public int GraceDaysLeft => !InGrace ? 0
        : (int)Math.Ceiling((GraceUntil - NowMs) / 86_400_000d);

    /// <summary>این کار باز است؟</summary>
    public bool Allows(string feature)
    {
        if (Array.IndexOf(Entitlements.Paid, feature) < 0) return true;
        //  ⚠️ فقط می‌بندد — آزمایشِ دستیِ خودِ صاحبِ پمپ، بی اثر روی دیسک.
        if (Entitlements.TestDeny) return false;
        //  ⏳ بازِ موقت — خواستهٔ ۱۴۰۵/۰۷/۰۲؛ شرحش بالای ‎UnlockedForNow‎.
        if (Entitlements.Unlocked) return true;
        if (NotActivated) return false;
        //  ⚠️ ارفاق: باگ و آفلاین نباید ببندد — ⛔ ولی فقط **همان پلن**. تا
        //  ۱۴۰۵/۰۷/۱۲ این خط `return true` بود و پلنِ استاندارد در ارفاق
        //  هر شش قفلِ وی‌آی‌پی را باز می‌دید.
        if (!Open && !InGrace) return false;
        //  فهرستی نیامده یعنی پلنِ کامل — مجوزهای نسلِ اول ‎feat‎ نداشتند و
        //  نباید یک‌شبه همه‌چیزشان بسته شود. فهرستِ **خالی** ولی یعنی پلنِ
        //  پایه: هیچ‌کدام.
        return !Listed || Has(feature);
    }

    /// <summary>
    /// ⛔ <b>نامِ قابلیت در برنامه و روی سرور یکی نیست — و این یک بار
    /// نزدیک بود همهٔ مشتری‌های پولی را قفل کند.</b>
    ///
    /// کاتالوگِ پمپ روی سرور (‎lib/features.js‎) این نام‌ها را دارد:
    /// <c>kar_app</c> · <c>bot</c> · <c>cloud</c> · <c>multi_device</c> و…
    /// ولی سه دروازهٔ این برنامه <c>kar</c> · <c>qrlive</c> ·
    /// <c>cloudbackup</c> است. یعنی روزی که سرور فهرستِ واقعیِ پلن را
    /// بفرستد، یک مشتریِ <b>وی‌آی‌پی</b> هر سه را <b>قفل</b> می‌دید — چون
    /// هیچ‌کدام از آن سه رشته در فهرستش نبود.
    ///
    /// پس نامِ سرور هم پذیرفته می‌شود. این نگاشت <b>ساختگی نیست</b>، از خودِ
    /// سرور درآمده: مسیرِ فایلِ ابری — همان جایی که هم عکسِ زندهٔ حساب و هم
    /// پشتیبان از آن رد می‌شوند — روی کلیدِ <c>cloud</c> قفل است
    /// (‎routes/pump-device.js‎). و اپِ کارمندان همان <c>kar_app</c> است.
    ///
    /// ⚠️ قاعده همان قاعدهٔ همیشگیِ این پرونده است: <b>قفلِ ناخواسته بدتر
    /// از بازِ ناخواسته است.</b> پس هر دو نام باز می‌کنند، و هیچ‌کدام
    /// چیزی را نمی‌بندد.
    /// </summary>
    private bool Has(string feature)
    {
        if (Features.Contains(feature)) return true;

        return feature switch
        {
            Entitlements.Kar         => Features.Contains("kar_app") || Features.Contains("bot"),
            Entitlements.QrLive      => Features.Contains("cloud"),
            Entitlements.CloudBackup => Features.Contains("cloud"),
            //  ⚠️ این سه هنوز روی سرور نیستند (‎docs/PLANS-fa.md‎، «کارِ
            //  سرور»). تا آن روز فقط نامِ خودِ برنامه کار می‌کند.
            _ => false,
        };
    }

    /// <summary>جملهٔ «چرا بسته است» برای همان کار.</summary>
    public string Why(string title) =>
        NotActivated
            ? title + " با اشتراک کار می‌کند — کدِ اشتراک را در «پروفایل» بزنید."
            : !Open && GraceEndsAt > 0 && NowMs >= GraceEndsAt
                ? "اشتراکِ این پمپ تمام شده، پس " + title + " خاموش است. دفترِ خودتان و "
                  + "پشتیبانی باز است."
                : title + " در پلنِ فعلیِ شما نیست.";
}
