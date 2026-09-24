using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «این نشانی را باز کن» — تنها درِ سپردنِ یک رشته به ویندوز ══════════════
///
/// ⛔ <b>درزی که این می‌بندد</b>: <c>Process.Start(new ProcessStartInfo(x)
/// { UseShellExecute = true })</c> هر چیزی را اجرا می‌کند — نشانیِ وب، ولی
/// همان‌قدر هم <c>C:\…\setup.exe</c>، <c>\\سرور\share\x.lnk</c>،
/// <c>file:///…</c> یا یک پروتکلِ ثبت‌شدهٔ دلخواه. و رشته‌هایی که به این‌جا
/// می‌رسیدند از بیرون می‌آمدند: لینکِ دوربینی که از کیو‌آر خوانده شده،
/// پاسخِ سرور، متنِ یک پیام. یعنی یک کیو‌آرِ دست‌ساز می‌توانست یک فایلِ
/// اجرایی را روی کامپیوترِ پمپ باز کند.
///
/// حالا فقط نشانیِ <b>مطلق</b> با طرح‌های شناخته‌شده باز می‌شود — وب
/// (<c>http</c>/<c>https</c>)، جریانِ دوربین (<c>rtsp</c>/<c>rtmp</c>) و
/// <c>mailto</c> — و هر مسیرِ فایل، UNC یا <c>file:</c>ی رد می‌شود.
///
/// ⚠️ برای <b>فایلِ محلیِ خودِ برنامه</b> (رسانهٔ چت، پوشهٔ پشتیبان، PDF) راهِ
/// جدای خودش را دارد و این‌جا نمی‌آید: آن مسیرها را خودِ برنامه ساخته، نه
/// کسی از بیرون.
/// </summary>
public static class SafeOpen
{
    private static readonly string[] Schemes = { "http", "https", "rtsp", "rtmp", "mailto" };

    /// <summary>⚠️ فقط برای آزمون — به‌جای اجرای واقعی.</summary>
    public static Func<ProcessStartInfo, bool>? TestStart { get; set; }

    /// <summary>این رشته نشانی‌ای است که بشود به ویندوز سپرد؟</summary>
    public static bool IsAllowed(string? url)
    {
        var t = (url ?? "").Trim();
        if (t.Length == 0 || t.Length > 4096) return false;
        //  ⛔ مسیرِ ویندوز و UNC — پیش از `Uri.TryCreate`، چون «C:\x» را هم
        //  یک نشانیِ مطلق با طرحِ «c» می‌خواند.
        if (t.StartsWith(@"\\", StringComparison.Ordinal) || t.StartsWith("//", StringComparison.Ordinal)) return false;
        if (Regex.IsMatch(t, @"^[A-Za-z]:[\\/]")) return false;
        //  نویسه‌های کنترلی (خطِ تازه، NUL) هیچ‌وقت در نشانیِ درست نیستند
        if (t.Any(char.IsControl)) return false;

        if (!Uri.TryCreate(t, UriKind.Absolute, out var u)) return false;
        if (u.IsFile || u.IsUnc) return false;
        var scheme = u.Scheme.ToLowerInvariant();
        if (Array.IndexOf(Schemes, scheme) < 0) return false;
        //  mailto میزبان ندارد؛ بقیه باید داشته باشند
        return scheme == "mailto" || u.Host.Length > 0;
    }

    /// <summary>
    /// نشانی را به مرورگر یا پخش‌کنندهٔ پیش‌فرضِ سیستم می‌دهد.
    /// <c>false</c> ⇒ یا نشانیِ پذیرفتنی نبود، یا سیستم بازش نکرد.
    /// ⚠️ هیچ‌وقت استثنا بیرون نمی‌دهد.
    /// </summary>
    public static bool Url(string? url)
    {
        if (!IsAllowed(url)) return false;
        try
        {
            var psi = new ProcessStartInfo(url!.Trim()) { UseShellExecute = true };
            if (TestStart is { } hook) return hook(psi);
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }
}
