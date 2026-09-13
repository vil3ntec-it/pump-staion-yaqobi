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
///     (‎AppSettings.ServerUrl‎).
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

    /// <summary>رمزِ سرور — خالی یعنی سرور رمز نمی‌خواهد.</summary>
    public static string Token(AppHost host) =>
        First(Db(host, SettingsService.SyncCode), AppSettings.Load().ServerToken);

    /// <summary>کدِ ایستگاه. خالی هرگز برنمی‌گردد.</summary>
    public static string StationCode(AppHost _)
    {
        var code = AppSettings.Load().StationCode.Trim();
        return code.Length == 0 ? "pump1" : code;
    }

    /// <summary>هر دو با هم — همان چیزی که <see cref="HomeSync"/> می‌خواهد.</summary>
    public static (string Url, string Token) Config(AppHost host) => (Url(host), Token(host));

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
