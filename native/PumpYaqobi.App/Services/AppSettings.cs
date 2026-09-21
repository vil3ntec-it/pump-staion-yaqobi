using System.Text.Json;
using System.Text.Json.Serialization;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.Services;

/// <summary>
/// تنظیماتِ سبکِ خودِ برنامه (تم، آخرین بخش، اندازهٔ پنجره).
/// کنارِ دیتابیس در پوشهٔ کاربر می‌نشیند تا بکاپِ دیتابیس آن را با خود نبرد.
/// </summary>
public sealed class AppSettings
{
    public string ThemeId { get; set; } = "blue";
    public string LastSection { get; set; } = "dashboard";

    /// <summary>اندازهٔ ماشین‌حسابِ شناور — «اندازه‌اش ثبت بشه».</summary>
    public double CalcWidth { get; set; } = 286;
    public double CalcHeight { get; set; } = 430;
    public bool CalcLarge { get; set; }

    /// <summary>
    /// ══ «برسیِ زنجیرهٔ پایه‌ها» در پارچه‌ها ═══════════════════════════════════
    ///
    /// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «یک مدلِ اختیاریهٔ جدید هم بیاد که ختمِ
    /// یک عدد با شروع متفاوت باشه… و بررسی باید بشه.» ⇒ <b>اختیاری</b>، پس
    /// یک کلید دارد. روشن است تا خودش دیده باشدش و بعد اگر خواست خاموشش کند.
    ///
    /// ⚠️ یک مقدارِ راحتی است، نه رازی که گم شدنش کسی را از حسابش بیرون
    /// بیندازد — پس با <c>SaveSoon()</c> می‌نشیند و در فهرستِ
    /// <c>SaveComfortOnly</c> هم هست (قاعدهٔ همان‌جا: این دو فهرست باید یکی
    /// بمانند).
    /// </summary>
    public bool ParchaChainCheck { get; set; } = true;

    /// <summary>
    /// نشانیِ سرورِ خانگیِ خودِ صاحب ریپو — پیام‌رسان از همین می‌خواند.
    /// ⚠️ هیچ سرویسِ بیرونی‌ای این‌جا نمی‌آید؛ خالی یعنی پیام‌رسان خاموش.
    /// </summary>
    public string ServerUrl { get; set; } = "";

    /// <summary>
    /// رمزِ همین پمپ روی سرور — همانی که اجازهٔ <b>نوشتن</b> دارد.
    /// ⚠️ فقط در همین برنامه می‌ماند. در کیو‌آرِ کارمند نمی‌رود.
    /// </summary>
    [JsonIgnore] public string ServerToken { get; set; } = "";

    /// <summary>
    /// رمزِ <b>فقط‌خواندنیِ</b> همین پمپ — همانی که در کیو‌آرِ کارمند و اپِ
    /// اندروید/آیفون می‌نشیند. کیو‌آر روی کاغذ چاپ می‌شود و دستِ چند نفر
    /// می‌گردد؛ با رمزِ نوشتن، همان کاغذ اجازهٔ پاک کردنِ دفترِ پمپ را هم داشت.
    /// خالی یعنی سرور هنوز به‌روز نشده و رمزِ خواندن ندارد.
    /// </summary>
    public string ServerReadKey { get; set; } = "";

    /// <summary>شناسهٔ سروری که برنامه خودش در شبکه پیدا کرد — فقط برای نمایش.</summary>
    public string ServerId { get; set; } = "";

    /// <summary>
    /// کدِ ایستگاه — همان کلیدی که نسخهٔ وب زیرِ ‎stations/&lt;کد&gt;‎ می‌نویسد.
    /// ⚠️ باید دقیقاً همان کدی باشد که در سایت گذاشته‌اید، وگرنه سایت دادهٔ
    /// این برنامه را نمی‌بیند و دو دفترِ جدا می‌شوند.
    /// </summary>
    public string StationCode { get; set; } = "pump1";

    /// <summary>
    /// آخرین باری که پشتیبان واقعاً روی سرورِ خانگی نشست (‎ISO‎).
    /// خالی یعنی هنوز هیچ‌وقت نرفته — <see cref="BackupPusher"/> از همین
    /// می‌فهمد کِی باید به مدیر بگوید.
    /// </summary>
    public string LastBackupSentAt { get; set; } = "";

    /// <summary>
    /// ثبتِ خودکار در سرورِ خانگی روشن باشد؟
    ///
    /// روشن یعنی: اگر نشانی نداشتیم، برنامه خودش سرور را در شبکهٔ خانگی
    /// پیدا می‌کند، پوشه و رمزِ همین پمپ را می‌گیرد و می‌نویسدشان. کاربر هیچ
    /// آدرسی تایپ نمی‌کند. کسی که عمداً «فقط محلی» می‌خواهد، همین را خاموش
    /// می‌کند و برنامه دیگر دنبالِ هیچ سروری نمی‌گردد.
    /// </summary>
    public bool AutoEnroll { get; set; } = true;

    // ══ ابر — حساب و اشتراک ══════════════════════════════════════════════
    //
    // ⚠️ نشانیِ ابر این‌جا **نیست** و نباید بیاید: در `CloudConfig.BaseUrl`
    // قفل است. اگر از تنظیمات خوانده می‌شد، هر کسی می‌توانست نشانیِ سرورِ
    // خودش را بنویسد و برنامه را با مجوزِ ساختگیِ خودش باز کند — یعنی قفل
    // با یک کادرِ متنی دور می‌خورد.
    //
    // این‌ها فقط چیزهایی‌اند که خودِ ابر به ما داده و باید نگه داریم.

    /// <summary>شناسهٔ این کامپیوتر. یک بار ساخته می‌شود و می‌ماند.</summary>
    public string CloudDeviceUid { get; set; } = "";

    /// <summary>توکنی که ابر پس از فعال‌سازی داده. انقضا ندارد؛ مجوز دارد.</summary>
    [JsonIgnore] public string CloudDeviceToken { get; set; } = "";

    /// <summary>شناسهٔ پمپِ این برنامه روی ابر.</summary>
    public string CloudStationId { get; set; } = "";

    /// <summary>
    /// کلیدِ عمومیِ سرور — **قفل‌شده در اولین فعال‌سازی**.
    ///
    /// ⚠️ مهم‌ترین تکهٔ ضدِ کرک. بی این، کسی می‌توانست سرورِ خودش را بالا
    /// بیاورد، کلیدِ خودش را بدهد و مجوزِ خودش را امضا کند. از اولین بار
    /// به بعد فقط همین پذیرفته می‌شود.
    /// </summary>
    public string CloudPublicKey { get; set; } = "";

    /// <summary>آخرین مجوزِ امضاشده. آفلاین هم از روی همین تصمیم گرفته می‌شود.</summary>
    public string CloudLicense { get; set; } = "";

    /// <summary>آخرین باری که با ابر حرف زدیم (میلی‌ثانیهٔ یونیکس).</summary>
    public long CloudSyncedAt { get; set; }

    /// <summary>
    /// تا این لحظه (میلی‌ثانیهٔ یونیکس) **یک بار به چشمِ خودمان دیده‌ایم** که
    /// اشتراک باز است. با هر مجوزِ سالم یا هر «اشتراکِ فعال»ِ سرور جلو می‌رود.
    ///
    /// ⚠️ این عدد برای **ارفاق** است، نه برای قفل: خواستهٔ صریحِ صاحب ریپو
    /// «نمی‌خواهم کسی که اشتراک خریده با یک باگ خراب شود». پس تا
    /// <see cref="Entitlements.Grace"/> پس از این لحظه، نبودِ اینترنت یا
    /// خرابیِ سرور چیزی را نمی‌بندد. بالا بردنش به دستِ کاربر هم خطری ندارد:
    /// بی مجوزِ امضاشده، همان ارفاق روزی تمام می‌شود و دیگر تازه نمی‌شود.
    /// </summary>
    public long EntitledUntil { get; set; }

    /// <summary>نامِ پلنی که آخرین بار دیده شد — فقط برای نمایش.</summary>
    public string EntitledPlan { get; set; } = "";

    // ── حسابِ گوگل ─────────────────────────────────────────────────────
    //
    //  خواستهٔ صاحب ریپو: «مثلِ برنامهٔ شاپ باشد که بی اینکه من رمز یا چیزی
    //  بزنم، اطلاعات از حسابش به سرور بیاید.» پس نشانیِ سرورِ خانگی و رمزِ
    //  خواندن دیگر تایپ نمی‌شوند — از همین حساب می‌آیند.

    /// <summary>توکنِ نشستِ حساب (ورود با گوگل).</summary>
    [JsonIgnore] public string CloudAccountToken { get; set; } = "";

    /// <summary>توکنِ تازه‌سازی — تا کاربر هر بار وارد نشود.</summary>
    [JsonIgnore] public string CloudRefreshToken { get; set; } = "";

    /// <summary>ایمیلِ حساب — فقط برای نشان دادن.</summary>
    /// <summary>
    /// کاربر روی صفحهٔ ورود «بعداً» را زده است.
    ///
    /// ⚠️ این «حساب دارم» نیست — فقط یعنی صفحهٔ ورود دیگر جلوی دفترش را
    /// نمی‌گیرد. با ساختنِ حسابِ واقعی خودش برداشته می‌شود، و در خودِ
    /// پروفایل دکمهٔ برگشت به همان صفحه هست.
    /// </summary>
    public bool LoginSkipped { get; set; }

    /// <summary>
    /// گزارشِ خطا به سرور خاموش است — بندِ ۲۰٫۸: «با اجازهٔ کاربر».
    ///
    /// ⚠️ پیش‌فرض <b>روشن</b> است (یعنی این دروغ)، چون «من قبل از تماسِ
    /// مشتری خبر داشته باشم» خواستهٔ خودِ صاحب سامانه است؛ ولی کلیدِ
    /// خاموشی در «تنظیمات ← همگام‌سازی» هست و همان‌جا نوشته شده چه می‌رود.
    /// ⛔ هیچ دادهٔ حساب، نام، شماره یا مبلغی گزارش نمی‌شود.
    ///
    /// ⚠️ این‌جا می‌نشیند و نه در جدولِ تنظیماتِ دیتابیس: <c>CrashGuard</c>
    /// باید وقتی هم که خودِ دیتابیس خراب است بتواند بخواندش.
    /// </summary>
    public bool ReportErrorsOff { get; set; }

    public string CloudEmail { get; set; } = "";

    /// <summary>نامِ حساب — فقط برای نشان دادن.</summary>
    public string CloudName { get; set; } = "";

    /// <summary>
    /// شناسهٔ خودِ حساب روی ابر (<c>user.id</c>ی پاسخِ ورود).
    ///
    /// ⚠️ <b>راز نیست و رمز نیست</b> — یک عدد است، پس رمز هم نمی‌شود. تنها
    /// کارش پاسخ دادن به یک پرسش است: «حسابی که همین حالا وارد شد، همان
    /// حسابِ قبلی است یا حسابِ دیگری؟» ایمیل برای این کار کافی نیست، چون
    /// خودِ کاربر می‌تواند ایمیلِ حسابش را عوض کند و آن‌وقت برنامه همان
    /// حساب را «حسابِ تازه» می‌دید و بندهای پمپ را بی‌دلیل باز می‌کرد.
    /// <see cref="CloudLink.SeatAsync"/> تنها جای خواندن و نوشتنش است.
    /// </summary>
    public string CloudUserId { get; set; } = "";

    /// <summary>
    /// کدِ دسترسیِ پمپ — همان کدِ هشت‌حرفی که کارمند در اپِ گوشی می‌زند.
    /// سرور می‌دهدش؛ این‌جا فقط نسخهٔ آخر می‌ماند تا بی‌اینترنت هم روی
    /// صفحهٔ پروفایل دیده شود.
    /// </summary>
    public string CloudAccessCode { get; set; } = "";

    // ══ توکن‌ها روی دیسک رمز می‌شوند ═════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو: «توکن را داخلِ فایلِ متنیِ ساده یا تنظیماتِ قابلِ
    //  مشاهده ذخیره نکن.» تا امروز هر چهارتا — توکنِ حساب، توکنِ تازه‌سازی
    //  (که روی سرور **نود روز** عمر دارد)، توکنِ دستگاه و رمزِ نوشتنِ سرورِ
    //  خانگی — **خام** در `settings.json` می‌نشستند.
    //
    //  پس خودِ خاصیت‌ها بالا `[JsonIgnore]` شدند و این چهار «دوقلوی رمزی»
    //  جایشان در فایل می‌نشینند (`SecretStore` ⇒ DPAPI روی ویندوز).
    //
    //  ⚠️ **هیچ‌کدام هیچ‌وقت خالی نمی‌کنند، فقط پر می‌کنند.** برای همین
    //  ترتیبِ کلیدها در فایل مهم نیست و نصبِ امروزی — که هنوز کلیدِ کهنهٔ
    //  خام را دارد — با به‌روزرسانی از حساب بیرون نمی‌افتد و کدِ شش‌رقمی را
    //  دوباره نمی‌پرسد. با اولین `Save` خودشان رمز می‌شوند.
    //  (`SettingsSecretTests`)

    //  ⛔ **یک رمزگشاییِ ناموفق حق ندارد راز را پاک کند.**
    //
    //  باگی که ساختِ ویندوزِ CI گرفت (اجرای #128): اگر `Unprotect` یک بار
    //  خالی برمی‌گرداند — یک خطای گذرای DPAPI، فایلی که وسطِ جایگزینی
    //  خوانده شد، هر چه — آن `AppSettings` با توکنِ **خالی** بالا می‌آمد و
    //  ذخیرهٔ بعدی همان خالی را روی بلوکِ سالمِ دیسک می‌نوشت. یعنی **یک**
    //  لغزش، توکنِ دستگاه را برای همیشه می‌برد و کاربر باید دوباره کدِ
    //  شش‌رقمی می‌زد. در آن اجرا از ۴۰ خواندنِ پشتِ سرِ هم، ۳۸ تا پس از
    //  همان یک لغزش خراب بودند — شکلِ «مسمومیت»، نه شکلِ «مسابقه».
    //
    //  پس حالا بلوکِ باز-نشده **دست‌نخورده** نگه داشته می‌شود و همان
    //  دوباره نوشته می‌شود: در حافظه «توکن ندارم» (پس برنامه ورود
    //  می‌خواهد، و با زبالهٔ رمزنشده به سرور نمی‌زند) ولی روی دیسک چیزی
    //  از بین نمی‌رود و لغزشِ گذرا خودش خوب می‌شود.
    //
    //  ⚠️ با پر شدنِ توکنِ واقعی، بلوکِ کهنه دور انداخته می‌شود — وگرنه
    //  ورودِ تازه هم بلوکِ حسابِ قبلی را با خودش می‌کشید.

    private string _deviceBlob = "";
    private string _accountBlob = "";
    private string _refreshBlob = "";
    private string _serverBlob = "";

    private static string Keep(string live, string blob) =>
        live.Length > 0 ? SecretStore.Protect(live) : blob;

    [JsonPropertyName("CloudDeviceTokenEnc")]
    public string CloudDeviceTokenEnc
    {
        get => Keep(CloudDeviceToken, _deviceBlob);
        set
        {
            var v = SecretStore.Unprotect(value);
            if (v.Length > 0) { CloudDeviceToken = v; _deviceBlob = ""; }
            else _deviceBlob = value ?? "";
        }
    }

    [JsonPropertyName("CloudAccountTokenEnc")]
    public string CloudAccountTokenEnc
    {
        get => Keep(CloudAccountToken, _accountBlob);
        set
        {
            var v = SecretStore.Unprotect(value);
            if (v.Length > 0) { CloudAccountToken = v; _accountBlob = ""; }
            else _accountBlob = value ?? "";
        }
    }

    [JsonPropertyName("CloudRefreshTokenEnc")]
    public string CloudRefreshTokenEnc
    {
        get => Keep(CloudRefreshToken, _refreshBlob);
        set
        {
            var v = SecretStore.Unprotect(value);
            if (v.Length > 0) { CloudRefreshToken = v; _refreshBlob = ""; }
            else _refreshBlob = value ?? "";
        }
    }

    [JsonPropertyName("ServerTokenEnc")]
    public string ServerTokenEnc
    {
        get => Keep(ServerToken, _serverBlob);
        set
        {
            var v = SecretStore.Unprotect(value);
            if (v.Length > 0) { ServerToken = v; _serverBlob = ""; }
            else _serverBlob = value ?? "";
        }
    }

    //  ── و خواندنِ فایلِ کهنه ───────────────────────────────────────────
    //  ⚠️ این چهار تا فقط **خوانده** می‌شوند: `get` همیشه `null` است، پس
    //  `WhenWritingNull` آن‌ها را در فایلِ تازه نمی‌نویسد و کلیدِ خام با
    //  اولین ذخیره از فایل می‌رود.

    [JsonPropertyName("CloudDeviceToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyCloudDeviceToken
    {
        get => null;
        set { if (!string.IsNullOrEmpty(value) && CloudDeviceToken.Length == 0) CloudDeviceToken = value; }
    }

    [JsonPropertyName("CloudAccountToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyCloudAccountToken
    {
        get => null;
        set { if (!string.IsNullOrEmpty(value) && CloudAccountToken.Length == 0) CloudAccountToken = value; }
    }

    [JsonPropertyName("CloudRefreshToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyCloudRefreshToken
    {
        get => null;
        set { if (!string.IsNullOrEmpty(value) && CloudRefreshToken.Length == 0) CloudRefreshToken = value; }
    }

    [JsonPropertyName("ServerToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyServerToken
    {
        get => null;
        set { if (!string.IsNullOrEmpty(value) && ServerToken.Length == 0) ServerToken = value; }
    }

    /// <summary>
    /// لحظه‌ای که توکنِ دسترسیِ حساب منقضی می‌شود (میلی‌ثانیهٔ یونیکس).
    ///
    /// ⚠️ سرور به این توکن **یک ساعت** عمر می‌دهد
    /// (<c>ACCESS_TOKEN_TTL_MIN</c>)، پس بی این عدد هر درخواستِ حساب پس از
    /// یک ساعت یک ۴۰۱ِ حتمی بود. <see cref="CloudLink"/> کمی زودتر خودش
    /// تازه‌اش می‌کند. صفر یعنی «نمی‌دانیم» — آن‌وقت همان راهِ ۴۰۱ می‌رود.
    /// </summary>
    public long CloudAccessExpiresAt { get; set; }

    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 900;
    public bool WindowMaximized { get; set; } = true;

    /// <summary>
    /// «تنظیمِ ورق»ِ چاپ — همتای ‎pumpPrintStudio_v4‎ در ‎localStorage‎ی نسخهٔ وب.
    /// کنارِ خودِ برنامه می‌نشیند، نه در دیتابیس: مالِ همین دستگاه است و
    /// بکاپِ حساب‌ها نباید آن را با خود ببرد.
    /// </summary>
    public PageSetup? PrintSetup { get; set; }

    /// <summary>
    /// ══ ظاهرِ جدول‌ها ═══════════════════════════════════════════════════════
    /// خواستهٔ صریحِ صاحب ریپو: «رنگ و ضخامتِ خطِ جدول نباید سفت و سخت داخلِ کد
    /// نوشته شده باشد؛ در تنظیمات باشد و روی **همهٔ** جدول‌های برنامه بنشیند.»
    ///
    /// پس این چهار عدد تنها جای تعریفِ خطِ جدول‌اند و از این‌جا به شکلِ منبعِ
    /// پویا (<c>Pump.Table.…</c>) پخش می‌شوند — ‎Themes/TableStyle.cs‎.
    /// کنارِ خودِ برنامه می‌نشینند نه در دیتابیس: مالِ همین دستگاه‌اند.
    /// </summary>
    /// <remarks>خالی یعنی «رنگِ خودِ تم» — همان چیزی که تا امروز بوده.</remarks>
    public string TableBorderColor { get; set; } = "";

    /// <summary>ضخامتِ خطِ بینِ خانه‌ها (۱ تا ۴ پیکسل).</summary>
    public double TableLine { get; set; } = 1;

    /// <summary>ضخامتِ خطِ زیرِ سربرگِ جدول.</summary>
    public double TableHeadLine { get; set; } = 2;

    /// <summary>ضخامتِ خطِ بالای ردیفِ «جمله».</summary>
    public double TableSumLine { get; set; } = 2;

    /// <summary>
    /// ══ اندازهٔ نوشتهٔ هر بخش — همتای ‎secFont_&lt;id&gt;‎ی ‎localStorage‎ی سایت ══
    ///
    /// در سایت هر بخش کنارِ عنوانش ‎A−‎ / ‎A+‎ / ‎↺‎ دارد و اندازه‌اش جدا از
    /// بقیه ذخیره می‌شود («هر بخشی که جدولش ریز است را خودم بزرگ می‌کنم»).
    /// کلید همان شناسهٔ بخش است (‎safe‎، ‎sarrafi‎، ‎waraq‎، …) تا اگر روزی
    /// دادهٔ سایت وارد شد، همان‌جا بنشیند.
    ///
    /// ⚠️ کنارِ خودِ برنامه می‌ماند، نه در دیتابیس: مالِ همین دستگاه است و
    /// بکاپِ حساب‌ها نباید اندازهٔ نوشتهٔ یک کامپیوترِ دیگر را با خود ببرد.
    /// </summary>
    public Dictionary<string, double> SecFontScales { get; set; } = new();

    /// <summary>
    /// ══ پهنای ستون‌ها، به کلیدِ جدول ═══════════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «وقتی جدولِ یک ورق را تنظیم می‌کنم، تمامِ ورق‌ها
    /// برابر بشوند… نمی‌شود که هر روز من اندازه‌ها را درست کنم.»
    ///
    /// پس پهنا به **جدول** بسته است، نه به ورق: هر جدولی که ‎WidthKey‎ داشته
    /// باشد پهنای دستیِ کاربر را همین‌جا می‌گذارد و همهٔ ورق‌های دیگر — و
    /// فردا، بعدِ بسته شدنِ برنامه — همان را برمی‌دارند.
    /// </summary>
    public Dictionary<string, double[]> ColumnWidths { get; set; } = new();

    /// <summary>
    /// اندازهٔ نوشتهٔ «کادرهای یادداشت» — همتای ‎noteFontScale‎ی سایت. یکی است
    /// برای همهٔ بخش‌ها (سایت هم یک متغیرِ ریشه‌ای دارد، نه یکی برای هر بخش).
    /// </summary>
    public double NoteFontScale { get; set; } = 1;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>
    /// جای فایلِ تنظیمات. ابزارِ عکس‌گیری و آزمون‌ها آن را به یک پوشهٔ موقت
    /// می‌برند تا هرگز تنظیماتِ واقعیِ کاربر را نخوانند و ننویسند.
    /// </summary>
    /// <remarks>
    /// ⚠️ عوض شدنِ پوشه، کلیدِ رمزِ کَش‌شدهٔ <see cref="SecretStore"/> را هم
    /// باطل می‌کند — وگرنه سنجهٔ بعدی فایلِ پوشهٔ تازه را با کلیدِ پوشهٔ قبلی
    /// می‌خواند و توکن‌ها خالی درمی‌آمدند.
    /// </remarks>
    public static string? DirOverride
    {
        get => _dirOverride;
        //  ⚠️ دیگر کلید را فراموش نمی‌کنیم: `SecretStore` کلیدِ **هر پوشه**
        //  را جدا نگه می‌دارد، پس عوض شدنِ این، کلیدِ پوشهٔ دیگری را باطل
        //  نمی‌کند. (پاک کردنِ سراسری همان چیزی بود که سنجه‌های موازی را
        //  گاهی سرخ می‌کرد.)
        //  ⛔ و نوبتِ در صف را هم دور می‌ریزد: `SaveSoon` ششصد میلی‌ثانیه بعد
        //  می‌نویسد و تا آن لحظه پوشه عوض شده، پس آن نوشتن در پوشهٔ **کسِ
        //  دیگری** می‌نشست. یک بار همین شد و آزمونِ `FayleGhoflShode_…`
        //  توکنِ کلاسِ دیگری را در دفترِ خودش دید.
        //  ⚠️ هر دو کار زیرِ یک قفل‌اند: وگرنه `FlushSoon` می‌توانست پوشه را
        //  پیش از عوض شدن بخواند و درست بعدش بنویسد.
        set { lock (SoonGate) { _soonWho = null; _soonDir = null; _dirOverride = value; } }
    }

    private static string? _dirOverride;

    public static string Dir => DirOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PumpYaqobi");

    private static string File_ => Path.Combine(Dir, "settings.json");

    /// <summary>نسخهٔ سالمِ قبلی — تورِ ایمنیِ «فایل نصفه ماند».</summary>
    private static string Backup_ => File_ + ".bak";

    /// <summary>
    /// ══ نوشتن و خواندن، هر دو زیرِ یک قفل ══════════════════════════════════
    ///
    /// ⚠️ <b>سه نخ هم‌زمان این فایل را می‌نویسند</b>: خودِ رابط (تم، ورود،
    /// پهنای ستون)، حلقهٔ بیست‌ثانیه‌ایِ <see cref="StationPublisher"/>، و
    /// <see cref="BackupPusher"/>. بی این قفل، دو نوشتنِ هم‌زمان یکدیگر را
    /// قطع می‌کردند. سنجیده شد: از ۴۰ ذخیرهٔ هم‌زمان، <b>۱۸ تا</b> خراب یا
    /// خالی خوانده می‌شدند.
    /// </summary>
    private static readonly object FileGate = new();

    public static AppSettings Load()
    {
        lock (FileGate)
        {
            //  اول فایلِ اصلی، بعد نسخهٔ سالمِ قبلی
            var locked = false;
            var a = Read(File_, ref locked) ?? Read(Backup_, ref locked);
            if (a is not null) return a;

            //  ⛔ **فایل بود ولی قفل بود ⇒ پیش‌فرض ندهیم که ذخیره‌اش کنیم.**
            //  روی ویندوز ضدِ ویروس و نمایه‌سازِ سیستم گاهی یک لحظه دستهٔ
            //  فایل را نگه می‌دارند. تا امروز همان یک لحظه یعنی `Load`ی که
            //  تنظیماتِ **خالی** برمی‌گرداند و `Save`ِ بعدی آن را روی دفترِ
            //  واقعی می‌نوشت: کدِ پمپ به `pump1` برمی‌گشت (نوشتن روی پوشهٔ
            //  اشتباهِ سرور)، قفلِ ضدِ کرک می‌رفت و توکنِ دستگاه هم.
            //  حالا چنین نمونه‌ای **کور** است و هیچ‌وقت نمی‌نویسد.
            var blind = new AppSettings();
            if (locked) blind._blind = true;
            return blind;
        }
    }

    /// <summary>
    /// این نمونه از فایلی آمد که **قفل** بود، نه از فایلِ سالم. پس هر چه
    /// دارد پیش‌فرض است و نوشتنش یعنی پاک کردنِ دادهٔ واقعی.
    /// </summary>
    [JsonIgnore]
    private bool _blind;

    /// <param name="locked">
    /// اگر فایل **هست** ولی خوانده نشد (قفلِ گذرا)، راست می‌شود. خرابیِ
    /// خودِ JSON این را راست نمی‌کند — آن فایل واقعاً خراب است و باید
    /// روی‌نویسی شود.
    /// </param>
    private static AppSettings? Read(string path, ref bool locked)
    {
        if (!File.Exists(path)) return null;

        //  ⚠️ قفلِ گذرا را با چند تلاشِ کوتاه رد می‌کنیم — همان کاری که
        //  هر برنامهٔ ویندوزی باید بکند.
        for (var i = 0; i < 4; i++)
        {
            try
            {
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) return null;
                return JsonSerializer.Deserialize<AppSettings>(text);
            }
            catch (IOException) { Thread.Sleep(15); }
            catch (UnauthorizedAccessException) { Thread.Sleep(15); }
            catch { return null; /* خرابیِ خودِ JSON — روی‌نویسی آزاد است */ }
        }

        locked = true;
        return null;
    }

    /// <summary>تنظیمِ ورقِ ذخیره‌شده — نبود، همان پیش‌فرضِ همیشگی.</summary>
    public static PageSetup LoadPrintSetup() => Load().PrintSetup ?? PageSetup.Default;

    /// <summary>
    /// فقط تنظیمِ ورق را عوض می‌کند و بقیهٔ فایل را دست‌نخورده نگه می‌دارد —
    /// وگرنه تم و «آخرین بخش» پاک می‌شدند.
    /// </summary>
    public static void SavePrintSetup(PageSetup setup)
    {
        var a = Load();
        a.PrintSetup = setup;
        a.Save();
    }

    /// <summary>
    /// اندازهٔ نوشتهٔ یک بخش را ذخیره کن و بقیهٔ فایل را دست‌نخورده بگذار —
    /// مثلِ ‎SavePrintSetup‎، وگرنه تم و «آخرین بخش» پاک می‌شدند.
    /// </summary>
    /// <remarks>مقدارِ ۱ یعنی «عادی» و کلید اصلاً نوشته نمی‌شود.</remarks>
    public static void SaveSecFontScale(string sectionId, double scale)
    {
        if (string.IsNullOrWhiteSpace(sectionId)) return;
        var a = Load();
        if (Math.Abs(scale - 1) < 0.005) a.SecFontScales.Remove(sectionId);
        else a.SecFontScales[sectionId] = scale;
        a.Save();
    }

    /// <summary>پهنای دستیِ ستون‌های یک جدول — نبود، ‎null‎.</summary>
    public static double[]? LoadColumnWidths(string key) =>
        string.IsNullOrWhiteSpace(key) ? null
        : Load().ColumnWidths.TryGetValue(key, out var w) ? w : null;

    /// <summary>
    /// پهنای ستون‌های یک جدول را نگه دار و بقیهٔ فایل را دست‌نخورده بگذار —
    /// مثلِ ‎SavePrintSetup‎، وگرنه تم و «آخرین بخش» پاک می‌شدند.
    /// </summary>
    public static void SaveColumnWidths(string key, double[] widths)
    {
        if (string.IsNullOrWhiteSpace(key) || widths.Length == 0) return;
        var a = Load();
        a.ColumnWidths[key] = widths;
        a.Save();
    }

    /// <summary>اندازهٔ نوشتهٔ کادرهای یادداشت — یکی برای همهٔ بخش‌ها.</summary>
    public static void SaveNoteFontScale(double scale)
    {
        var a = Load();
        a.NoteFontScale = scale;
        a.Save();
    }

    /// <summary>
    /// ══ ذخیره — یا کاملِ تازه، یا کاملِ کهنه؛ هیچ‌وقت نیمه ═════════════════
    ///
    /// ⛔ <b>باگی که یک بستنِ ناگهانی، اشتراکِ کاربر را می‌برد.</b> پیش از
    /// این <c>File.WriteAllText</c> بود: فایل را اول <b>خالی</b> می‌کند و بعد
    /// می‌نویسد. اگر برنامه (یا برق) وسطِ همان لحظه می‌رفت، <c>settings.json</c>
    /// نصفه می‌ماند، <see cref="Load"/> خطا می‌گرفت و یک تنظیماتِ
    /// <b>خالی</b> برمی‌گرداند. سنجیده شد، حدس نیست — این‌ها می‌رفتند:
    ///
    ///   • <c>CloudDeviceToken</c> ⇒ کاربر باید دوباره کدِ شش‌رقمی می‌زد
    ///   • <c>CloudPublicKey</c>   ⇒ <b>قفلِ ضدِ کرک (TOFU) باز می‌شد</b> و
    ///     کلیدِ هر سروری که بعد جواب می‌داد جایش می‌نشست
    ///   • <c>StationCode</c>      ⇒ به <c>pump1</c>ِ پیش‌فرض برمی‌گشت، یعنی
    ///     پمپ روی <b>پوشهٔ اشتباهِ</b> سرورِ خانگی می‌نوشت
    ///
    /// حالا: در فایلِ موقت می‌نویسیم، روی دیسک ته‌نشینش می‌کنیم، و بعد
    /// <b>جایگزینِ اتمی</b> می‌کنیم. پس هر لحظه که برنامه بمیرد، روی دیسک یا
    /// نسخهٔ کاملِ تازه است یا نسخهٔ کاملِ کهنه.
    ///
    /// ⚠️ <c>File.Replace</c> نسخهٔ قبلی را خودش در <c>.bak</c> نگه می‌دارد،
    /// و <see cref="Load"/> اگر اصلی خراب بود از همان می‌خواند.
    /// </summary>
    /// <summary>
    /// ══ ذخیرهٔ «بعداً» — برای چیزهای راحتی، بیرونِ نخِ رابط ═══════════════
    ///
    /// ⛔ <b>باگی که هر باز کردنِ یک بخش را کُند می‌کرد.</b> گزارشِ صاحب ریپو
    /// (۱۴۰۵/۰۷/۰۵): «هر بخش رو باز می‌کنم جدول‌ها یک ثانیه بعد میان.»
    ///
    /// ریشه‌اش این‌جا بود: <see cref="Save"/> عمداً یک نوشتنِ <b>بادوام</b>
    /// است — فایلِ موقت، <c>Flush(true)</c> (یعنی «تا روی خودِ بشقابِ دیسک
    /// ننشست برنگرد») و بعد <c>File.Replace</c>ِ سه‌فایلی. آن دوام برای
    /// توکن و کلیدِ عمومی <b>لازم</b> است و برنمی‌گردد.
    ///
    /// ولی <c>MainViewModel.GoAsync</c> همان را برای نوشتنِ «آخرین بخش»
    /// صدا می‌زد — <b>روی نخِ رابط</b>، درست بینِ نشان دادنِ صفحه و خواندنِ
    /// داده‌اش. یعنی کاربر صفحه را می‌دید، بعد نخِ رابط پشتِ یک
    /// <c>FlushFileBuffers</c> و یک جایگزینیِ اتمی می‌ایستاد (و روی ویندوز،
    /// پشتِ ضدِ ویروسی که همان لحظه فایل را باز می‌کند)، و <b>تازه بعدش</b>
    /// ردیف‌ها خوانده و ساخته می‌شدند.
    ///
    /// <para>
    /// ⚠️ الگو تازه نیست: اندازهٔ ماشین‌حساب از ۱۴۰۵/۰۶/۲۵ دقیقاً همین کار
    /// را می‌کرد (۶۰۰ میلی‌ثانیه تأخیر، روی <c>TaskScheduler.Default</c>).
    /// این فقط همان را یک‌جا کرد تا هر جای دیگری هم بتواند از آن استفاده کند.
    /// </para>
    ///
    /// <para>
    /// ⛔ <b>فقط برای مقدارهای راحتی</b> — تم، آخرین بخش، اندازهٔ پنجره.
    /// هر چیزی که از دست رفتنش کاربر را از حسابش بیرون می‌اندازد
    /// (<c>CloudDeviceToken</c>، <c>CloudPublicKey</c>، <c>StationCode</c>)
    /// همچنان <see cref="Save"/>ِ بادوام را صدا می‌زند.
    /// </para>
    ///
    /// <para>
    /// ⚠️ و هر <see cref="Save"/>ِ واقعی، نوبتِ در صفِ این را هم <b>می‌خورد</b>:
    /// آن یکی کلِ شیء را می‌نویسد، پس نوشتنِ دوباره فقط یک <c>fsync</c>ِ
    /// بیهوده است.
    /// </para>
    /// </summary>
    public void SaveSoon()
    {
        if (_blind) return;
        lock (SoonGate)
        {
            _soonWho = this;
            _soonDir = Dir;
            _soonTimer ??= new System.Threading.Timer(
                _ => FlushSoon(), null,
                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            _soonTimer.Change(SoonMs, System.Threading.Timeout.Infinite);
        }
    }

    /// <summary>مهلتِ جمع شدنِ چند تغییرِ پشتِ سرِ هم در یک نوشتن.</summary>
    private const int SoonMs = 600;

    private static readonly object SoonGate = new();
    private static AppSettings? _soonWho;

    /// <summary>
    /// پوشه‌ای که این نوشتنِ در صف برای آن ثبت شد. ⛔ اگر تا لحظهٔ نوشتن عوض
    /// شده باشد، نوشته <b>نمی‌شود</b>: مقدارِ راحتیِ یک پوشه هیچ‌وقت نباید
    /// در دفترِ پوشهٔ دیگری بنشیند.
    /// </summary>
    private static string? _soonDir;
    private static System.Threading.Timer? _soonTimer;

    private static void FlushSoon()
    {
        AppSettings? who;
        lock (SoonGate)
        {
            //  ⛔ پوشه عوض شده ⇒ این نوشتن دیگر مالِ این‌جا نیست. سنجش زیرِ
            //  همان قفلی است که `DirOverride` با آن می‌نویسد.
            who = _soonDir == Dir ? _soonWho : null;
            _soonWho = null;
            _soonDir = null;
        }
        //  ⛔ نشدنش هیچ‌وقت چیزی را نمی‌شکند — این فقط «آخرین بخش» است.
        try { who?.SaveComfortOnly(); } catch { }
    }

    /// <summary>
    /// ══ نوشتنِ در صف فقط مقدارهای راحتی را می‌برد، نه کلِ شیء ═══════════════
    ///
    /// ⛔ <b>باگی که این را لازم کرد — و از دست رفتنِ واقعیِ داده بود.</b>
    ///
    /// ‎SaveSoon()‎ یک <b>عکسِ کهنه</b> از تنظیمات را ششصد میلی‌ثانیه نگه
    /// می‌دارد و بعد می‌نویسد. اگر در همان فاصله کسِ دیگری چیزی روی دیسک
    /// نوشته باشد، این نوشتن آن را <b>پاک می‌کرد</b> — چون کلِ شیء را
    /// می‌برد.
    ///
    /// و این فرضی نبود: «‹ برگشت به برنامه» ایمیل و نامِ تایپ‌شده و نشانِ
    /// ‎LoginSkipped‎ را با ‎Save()‎ی بادوام می‌نوشت و بلافاصله ‎GoHome()‎ را
    /// صدا می‌زد؛ آن هم «آخرین بخش» را در صف می‌گذاشت. ششصد میلی‌ثانیه بعد،
    /// همان عکسِ کهنه روی فایل می‌نشست و هر سه از بین می‌رفتند — یعنی
    /// <b>دیوارِ ورود دوباره برمی‌گشت</b> و کاربر نمی‌فهمید چرا.
    /// (‎verify‎ بندِ ۱۴ همین را گرفت.)
    ///
    /// ⚠️ ‎Save()‎ی خطِ بالا هم نجاتش نمی‌داد: آن فقط نوبتی را لغو می‌کند که
    /// <b>همین شیء</b> گذاشته باشد، و این‌جا نوبت را شیءِ دیگری — و
    /// <b>بعد</b> از آن ‎Save()‎ — گذاشته بود.
    ///
    /// پس نوشتنِ در صف از امروز <b>ادغام</b> است نه جایگزینی: تازه‌ترین
    /// نسخهٔ دیسک خوانده می‌شود و فقط همان پنج مقدارِ راحتی رویش می‌نشیند.
    /// ⛔ فهرستِ این پنج‌تا باید با جاهایی که ‎SaveSoon()‎ را صدا می‌زنند یکی
    /// بماند (تم · آخرین بخش · اندازهٔ ماشین‌حساب · برسیِ زنجیرهٔ پایه)؛ هر
    /// چیزِ دیگری که از دست
    /// رفتنش کاربر را از حسابش بیرون می‌اندازد، ‎Save()‎ی بادوام می‌خواهد.
    /// </summary>
    private void SaveComfortOnly()
    {
        if (_blind) return;

        var live = Load();
        if (live._blind) return;          // فایل قفل بود — دفعهٔ بعد

        live.ThemeId = ThemeId;
        live.LastSection = LastSection;
        live.CalcWidth = CalcWidth;
        live.CalcHeight = CalcHeight;
        live.CalcLarge = CalcLarge;
        live.ParchaChainCheck = ParchaChainCheck;
        live.Save();
    }

    public void Save()
    {
        //  ⛔ نمونهٔ «کور» هیچ‌وقت نمی‌نویسد — بالا نوشته چرا.
        if (_blind) return;

        //  نوبتِ در صف دیگر لازم نیست: همین نوشتن کلِ شیء را می‌برد.
        lock (SoonGate) { if (ReferenceEquals(_soonWho, this)) { _soonWho = null; _soonDir = null; } }

        lock (FileGate)
        {
            var tmp = File_ + ".tmp";
            try
            {
                Directory.CreateDirectory(Dir);
                var text = JsonSerializer.Serialize(this, Json);

                //  ⚠️ ‎Flush(true)‎ یعنی «تا روی خودِ دیسک ننشست برنگرد» —
                //  وگرنه جایگزینیِ اتمی هم فایلی را جابه‌جا می‌کرد که هنوز
                //  در حافظهٔ سیستم‌عامل است و با قطعِ برق از دست می‌رفت.
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var w = new StreamWriter(fs, new System.Text.UTF8Encoding(false)))
                {
                    w.Write(text);
                    w.Flush();
                    fs.Flush(true);
                }

                if (File.Exists(File_)) File.Replace(tmp, File_, Backup_, ignoreMetadataErrors: true);
                else File.Move(tmp, File_);
            }
            catch
            {
                //  ⛔ نشدنِ ذخیره هیچ‌وقت نباید کار را بشکند — ولی حالا فایلِ
                //  سالمِ قبلی هم دست‌نخورده می‌ماند، نه این‌که نصفه شود.
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }
}
