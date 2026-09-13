using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «سرورِ خانگی کجاست؟» — یک جواب، نه دو تا ═══════════════════════════════
///
/// نشانیِ سرور در برنامه دو جا نوشته می‌شود و این یک دامِ واقعی است:
///
///   • صفحهٔ «تنظیمات» در <b>دیتابیس</b> می‌نویسد (‎SettingsService.ServerUrl‎
///     و ‎SyncCode‎) — همان‌جایی که صاحب ریپو واقعاً پُرش می‌کند.
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
    /// <summary>کدِ پمپی که اگر هیچ‌جا چیزی نوشته نشده باشد به کار می‌رود.</summary>
    public const string DefaultStationCode = "pump1";

    /// <summary>نشانیِ سرورِ داده — خالی یعنی «فقط محلی».</summary>
    public static string Url(AppHost host) =>
        First(Db(host, SettingsService.ServerUrl), AppSettings.Load().ServerUrl);

    /// <summary>
    /// رمزِ همین پمپ — همانی که اجازهٔ <b>نوشتن</b> دارد.
    /// ⚠️ این را در کیو‌آرِ کارمند نگذارید؛ آن یکی <see cref="ReadKey"/> است.
    /// </summary>
    public static string Token(AppHost host) =>
        First(Db(host, SettingsService.SyncCode), AppSettings.Load().ServerToken);

    /// <summary>
    /// رمزِ فقط‌خواندنیِ همین پمپ — همانی که در کیو‌آرِ کارمند و اپِ گوشی
    /// می‌نشیند. خالی یعنی سرور هنوز رمزِ خواندن نداده (سرورِ قدیمی).
    /// </summary>
    public static string ReadKey(AppHost _) => AppSettings.Load().ServerReadKey.Trim();

    /// <summary>کدِ ایستگاه. خالی هرگز برنمی‌گردد.</summary>
    public static string StationCode(AppHost _)
    {
        var code = AppSettings.Load().StationCode.Trim();
        return code.Length == 0 ? DefaultStationCode : code;
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
