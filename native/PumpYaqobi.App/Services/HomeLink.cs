using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «سرورِ خانگی کجاست؟» — یک جواب، نه دو تا ═══════════════════════════════
///
/// نشانیِ سرور در برنامه دو جا نوشته می‌شود و این یک دامِ واقعی است:
///
///   • صفحهٔ «تنظیمات» در <b>دیتابیس</b> می‌نویسد (‎SettingsService.ServerUrl‎)
///     — همان‌جایی که صاحب ریپو واقعاً پُرش می‌کند. ⛔ رمزِ نوشتن
///     (‎SyncCode‎) دیگر هرگز آن‌جا نمی‌نشیند (<see cref="Token"/>).
///   • بخشِ پیام‌رسان از <b>فایلِ کنارِ برنامه</b> می‌خواند
///     (‎AppSettings.ServerUrl‎) — و ثبتِ خودکار هم همان‌جا می‌نویسد، چون
///     پیش از ورودِ کاربر اجازهٔ نوشتن در دیتابیس نیست.
///
/// پس هر چیزِ تازه‌ای که به سرور وصل می‌شود باید <b>هر دو</b> را ببیند، وگرنه
/// کاربر نشانی را در تنظیمات می‌نویسد و هیچ اتفاقی نمی‌افتد و دلیلش را هم
/// نمی‌فهمد. این کلاس همان «هر دو» است: اول دیتابیس، بعد فایل.
/// </summary>
public static class HomeLink
{
    /// <summary>نشانیِ سرورِ داده — خالی یعنی «فقط محلی».</summary>
    public static string Url(AppHost host) =>
        First(Db(host, SettingsService.ServerUrl), AppSettings.Load().ServerUrl);

    /// <summary>
    /// نشانی‌ای که به <b>گوشیِ کارمند</b> داده می‌شود (ابر و کیو‌آر).
    /// ⛔ هیچ‌وقت ‎127.0.0.1‎ نیست وقتی نشانیِ شبکه را می‌دانیم — آن نشانی روی
    /// گوشی یعنی «خودِ همین گوشی» و درِ شبکهٔ پمپ هیچ‌وقت باز نمی‌شد.
    /// </summary>
    public static string ShareUrl(AppHost host)
    {
        var url = Url(host);
        var lan = AppSettings.Load().ServerLanUrl.Trim();
        return ServerFinder.IsLoopbackUrl(url) && lan.Length > 0 ? lan : url;
    }

    /// <summary>
    /// رمزِ همین پمپ — همانی که اجازهٔ <b>نوشتن</b> دارد.
    /// ⚠️ این را در کیو‌آرِ کارمند نگذارید؛ آن یکی <see cref="ReadKey"/> است.
    ///
    /// ⛔ <b>فقط از <see cref="AppSettings.ServerToken"/></b> — که روی دیسک رمز
    /// می‌شود (<see cref="SecretStore"/>). تا ۱۴۰۵/۰۷/۱۲ این‌جا اول دیتابیس
    /// (<c>SettingsService.SyncCode</c>) خوانده می‌شد و ثبتِ خودکار همان رمز
    /// را <b>خام</b> در <c>pump.db</c> هم می‌نوشت — و <c>pump.db</c> همان
    /// چیزی است که هر شش ساعت به سرورِ خانگی و ابر پشتیبان می‌شود. یعنی
    /// رمزِ نوشتنِ پمپ داخلِ هر فایلِ پشتیبان بود.
    /// </summary>
    public static string Token(AppHost host)
    {
        MigrateDbToken(host);
        return AppSettings.Load().ServerToken.Trim();
    }

    /// <summary>
    /// ردیفِ جامانده در دیتابیس ⇒ اگر تنظیمات رمزی ندارند، همان به تنظیماتِ
    /// رمزشده می‌رود؛ و در هر حال ردیف از دیتابیس <b>پاک</b> می‌شود.
    ///
    /// ⚠️ ارزان است و هر بار سنجیده می‌شود: <c>SettingsService</c> جدولش را
    /// در حافظه نگه می‌دارد، پس «ردیفی نیست» یک خواندنِ دیکشنری است — و
    /// دفترِ حسابِ دیگری که بعداً باز شود هم پاک‌سازی می‌شود. پیش از ورود
    /// خواندنِ دیتابیس اجازه ندارد و آن‌جا بارِ بعد امتحان می‌شود.
    /// </summary>
    internal static void MigrateDbToken(AppHost host)
    {
        try
        {
            var legacy = host.Settings.Get(SettingsService.SyncCode);
            if (legacy is not null)
            {
                var file = AppSettings.Load();
                if (file.ServerToken.Trim().Length == 0 && legacy.Trim().Length > 0)
                {
                    file.ServerToken = legacy.Trim();
                    file.Save();
                    //  ⛔ اگر نوشتن در تنظیمات نشست (فایلِ قفل)، ردیف پاک نمی‌شود —
                    //  پاک کردنِ تنها نسخهٔ رمز یعنی پمپی که دیگر به سرورش نمی‌رسد.
                    if (AppSettings.Load().ServerToken.Trim() != legacy.Trim()) return;
                }
                host.Settings.Remove(SettingsService.SyncCode);
            }
        }
        catch { /* پیش از ورود — بارِ بعد */ }
    }

    /// <summary>
    /// رمزِ فقط‌خواندنیِ همین پمپ — همانی که در کیو‌آرِ کارمند و اپِ گوشی
    /// می‌نشیند. خالی یعنی سرور هنوز رمزِ خواندن نداده (سرورِ قدیمی).
    /// </summary>
    public static string ReadKey(AppHost _) => AppSettings.Load().ServerReadKey.Trim();

    /// <summary>
    /// کدِ پوشهٔ همین پمپ. خالی هرگز برنمی‌گردد.
    ///
    /// ⛔ **هیچ کدِ پیش‌فرضِ مشترکی نیست** — نه ‎pump1‎ و نه هیچ چیزِ دیگر.
    /// ⚠️ تا وقتی رمزِ یک پوشه را داریم، همان کد برمی‌گردد (کد و رمز باید با هم
    /// بخوانند)، حتی اگر مالِ این حساب نباشد: جابه‌جایی کارِ
    /// <see cref="StationLink.EnsureAsync"/> است و فقط وقتی ثبتِ تازه نشست.
    /// </summary>
    public static string StationCode(AppHost _)
    {
        var file = AppSettings.Load();
        var saved = file.StationCode.Trim();
        if (saved.Length > 0 && file.ServerToken.Trim().Length > 0) return saved;
        return StationLink.CodeFor(file);
    }

    /// <summary>
    /// کدی که مشتری و اپِ کارمندان روی <b>سرورِ حساب</b> با آن می‌پرسند (‎s‎ی
    /// کیو‌آرِ زنده). ⛔ مالِ خودِ همین حساب است، نه پوشهٔ سرورِ خانگی؛ بی
    /// حساب همان کدِ پوشه (که روی سرورِ حساب هم هیچ پمپِ دیگری نیست).
    /// </summary>
    public static string CloudCode(AppHost host)
    {
        var mine = AcctLive.CloudCode(AppSettings.Load().CloudStationCode);
        return mine.Length > 0 ? mine : StationCode(host);
    }

    /// <summary>نامِ خواندنیِ پمپ — برای وقتی که خودش را در سرور ثبت می‌کند.</summary>
    public static string StationName(AppHost host)
    {
        var name = Db(host, SettingsService.StationName).Trim();
        return name.Length > 0 ? name : StationCode(host);
    }

    /// <summary>هر سه با هم — همان چیزی که <see cref="HomeSync"/> می‌خواهد.</summary>
    public static HomeTarget Config(AppHost host) =>
        new(Url(host), Token(host), StationCode(host));

    private static string Db(AppHost host, string key)
    {
        // ⚠️ خواندنِ تنظیمات پیش از ورود اجازه ندارد و استثنا می‌دهد؛ آن‌جا
        // فقط یعنی «هنوز نمی‌دانیم»، نه خطا.
        try { return host.Settings.GetString(key); }
        catch { return ""; }
    }

    private static string First(string a, string b)
    {
        var t = a.Trim();
        return t.Length > 0 ? t : b.Trim();
    }
}
