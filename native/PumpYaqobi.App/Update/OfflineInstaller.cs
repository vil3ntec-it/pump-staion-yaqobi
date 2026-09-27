namespace PumpYaqobi.App.Update;

/// <summary>
/// ══ «به‌روزرسانی از فایل» — برای کامپیوتری که اینترنت ندارد ═══════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «برای کسایی که نت ندارن هم آپدیت برسانم…
/// مدل‌های قدیمی را قبول نکند و جدیدها را قبول کند.»
///
/// کاربر فایلِ نصبِ تازه (<c>PumpYaqobi-Setup.exe</c>) را از فلش برمی‌گزیند.
/// پیش از اجرا دو چیز سنجیده می‌شود، هر دو از <b>خودِ فایل</b>:
///   ۱) نامِ محصول همان «پمپ یعقوبی» است — هر ‎exe‎ی دیگری اجرا نمی‌شود.
///   ۲) نسخه‌اش از همین برنامه تازه‌تر است — کهنه‌تر رد می‌شود، همین نسخه
///      فقط با پرسش (تعمیر).
///
/// ⚠️ این تورِ اول است، نه تنها تور: خودِ نصاب هم (‎InitializeSetup‎ در
/// ‎PumpYaqobi.iss‎) نسخهٔ نصب‌شده را از رجیستری می‌خواند و نسخهٔ کهنه‌تر را
/// رد می‌کند — پس کسی که فایل را مستقیم از فلش دوبار-کلیک کند هم نمی‌تواند
/// برنامه را به عقب ببرد.
/// </summary>
public static class OfflineInstaller
{
    /// <summary>همان ‎AppName‎ِ نصاب — ‎VersionInfoProductName‎ پیش‌فرضش همین است.</summary>
    public const string ProductName = "پمپ بنزین";

    /// <summary>نامِ نصاب‌های پیش از ۱۴۰۵/۰۷/۱۵ — آن‌ها هم نصابِ همین برنامه‌اند (کهنه‌تر، پس رد می‌شوند).</summary>
    public const string OldProductName = "پمپ یعقوبی";

    public enum Verdict { Newer, Same, Older, NotOurs, Unreadable }

    public sealed record Decision(Verdict Kind, string Version, string Message)
    {
        /// <summary>اجرا شود؟ (همین نسخه فقط پس از پرسش)</summary>
        public bool CanRun => Kind is Verdict.Newer or Verdict.Same;
    }

    /// <summary>
    /// تصمیم — خالص، از نامِ محصول و نسخهٔ خودِ فایل (<see cref="System.Diagnostics.FileVersionInfo"/>).
    /// </summary>
    public static Decision Decide(string? productName, string? fileVersion, string current)
    {
        var pn = productName?.Trim();
        if (!string.Equals(pn, ProductName, StringComparison.Ordinal) && !string.Equals(pn, OldProductName, StringComparison.Ordinal))
            return new(Verdict.NotOurs, "",
                "این فایل، فایلِ نصبِ همین برنامه نیست — اجرا نشد");

        var ver = Normalize(fileVersion);
        if (ver is null)
            return new(Verdict.Unreadable, "", "نسخهٔ این فایلِ نصب خوانده نشد — اجرا نشد");

        var cmp = UpdateService.Compare(ver, current);
        return cmp switch
        {
            > 0 => new(Verdict.Newer, ver, $"نسخهٔ {ver} — از این برنامه ({current}) تازه‌تر است"),
            0 => new(Verdict.Same, ver, $"همین نسخه ({ver}) — دوباره روی همان نصب می‌شود (تعمیر)"),
            _ => new(Verdict.Older, ver,
                $"این فایل نسخهٔ {ver} است و برنامه {current} — فایلِ نصبِ کهنه‌تر پذیرفته نمی‌شود"),
        };
    }

    /// <summary>«3.1.200.0» ⇒ «3.1.200»؛ ناخوانا ⇒ ‎null‎.</summary>
    public static string? Normalize(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        if (!Version.TryParse(v.Trim(), out var p)) return null;
        return p.Build >= 0 ? $"{p.Major}.{p.Minor}.{p.Build}" : $"{p.Major}.{p.Minor}.0";
    }

    /// <summary>از خودِ فایل می‌خواند و تصمیم می‌گیرد. هیچ‌وقت استثنا بیرون نمی‌دهد.</summary>
    public static Decision Inspect(string path, string current)
    {
        try
        {
            if (!File.Exists(path)) return new(Verdict.Unreadable, "", "فایل پیدا نشد");
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            return Decide(info.ProductName, info.FileVersion ?? info.ProductVersion, current);
        }
        catch { return new(Verdict.Unreadable, "", "این فایل خوانده نشد — اجرا نشد"); }
    }
}
