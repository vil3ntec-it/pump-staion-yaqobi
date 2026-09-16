using System.Text.Json;
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
    /// نشانیِ سرورِ خانگیِ خودِ صاحب ریپو — پیام‌رسان از همین می‌خواند.
    /// ⚠️ هیچ سرویسِ بیرونی‌ای این‌جا نمی‌آید؛ خالی یعنی پیام‌رسان خاموش.
    /// </summary>
    public string ServerUrl { get; set; } = "";

    /// <summary>
    /// رمزِ همین پمپ روی سرور — همانی که اجازهٔ <b>نوشتن</b> دارد.
    /// ⚠️ فقط در همین برنامه می‌ماند. در کیو‌آرِ کارمند نمی‌رود.
    /// </summary>
    public string ServerToken { get; set; } = "";

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
    public string CloudDeviceToken { get; set; } = "";

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

    // ── حسابِ گوگل ─────────────────────────────────────────────────────
    //
    //  خواستهٔ صاحب ریپو: «مثلِ برنامهٔ شاپ باشد که بی اینکه من رمز یا چیزی
    //  بزنم، اطلاعات از حسابش به سرور بیاید.» پس نشانیِ سرورِ خانگی و رمزِ
    //  خواندن دیگر تایپ نمی‌شوند — از همین حساب می‌آیند.

    /// <summary>توکنِ نشستِ حساب (ورود با گوگل).</summary>
    public string CloudAccountToken { get; set; } = "";

    /// <summary>توکنِ تازه‌سازی — تا کاربر هر بار وارد نشود.</summary>
    public string CloudRefreshToken { get; set; } = "";

    /// <summary>ایمیلِ حساب — فقط برای نشان دادن.</summary>
    public string CloudEmail { get; set; } = "";

    /// <summary>نامِ حساب — فقط برای نشان دادن.</summary>
    public string CloudName { get; set; } = "";

    /// <summary>
    /// کدِ دسترسیِ پمپ — همان کدِ هشت‌حرفی که کارمند در اپِ گوشی می‌زند.
    /// سرور می‌دهدش؛ این‌جا فقط نسخهٔ آخر می‌ماند تا بی‌اینترنت هم روی
    /// صفحهٔ پروفایل دیده شود.
    /// </summary>
    public string CloudAccessCode { get; set; } = "";

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
    public static string? DirOverride { get; set; }

    public static string Dir => DirOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PumpYaqobi");

    private static string File_ => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(File_))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(File_)) ?? new AppSettings();
        }
        catch { /* تنظیماتِ خراب هرگز نباید جلوی باز شدنِ برنامه را بگیرد */ }
        return new AppSettings();
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

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(File_, JsonSerializer.Serialize(this, Json));
        }
        catch { }
    }
}
