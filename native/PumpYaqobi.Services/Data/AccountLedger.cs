using System.Security.Cryptography;
using System.Text;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ هر حساب، دفترِ خودش ═══════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «حسابِ اول با حسابِ دوم عوض بشه،
/// اطلاعات دست نخوره، توی حساب‌ها بمونن، حساب‌ها عوض می‌شه و اطلاعاتِ همون
/// حساب نشون داده بشه — مثلِ برنامه‌های حرفه‌ای.»
///
/// <code>
/// %AppData%\PumpYaqobi\pump.db                      ← دفترِ ریشه
/// %AppData%\PumpYaqobi\accounts\&lt;نامِ امن&gt;\pump.db   ← دفترِ حساب‌های بعدی
/// </code>
///
/// ⛔ <b>این‌جا هیچ فایلی ساخته، جابه‌جا، کپی یا پاک نمی‌شود.</b> فقط یک
/// مسیر برمی‌گردد. پاک کردنِ دفترِ کسی برگشت‌ناپذیر است و هیچ‌وقت از
/// عوض کردنِ حساب درنمی‌آید — «اطلاعات دست نخوره» یعنی همین.
///
/// ⛔ <b>دفترِ ریشه جابه‌جا نمی‌شود و نخستین حساب صاحبش می‌شود.</b> راهِ
/// دیگر این بود که فایل را به پوشهٔ همان حساب ببریم؛ نبردیمش، چون بردنِ
/// دفترِ پنج‌سالهٔ یک مشتری سرِ نخستین ورود، یک کارِ یک‌طرفه است که هیچ
/// سودی ندارد — همان فایل سرِ جایش می‌ماند و فقط نامِ صاحبش در
/// <c>AppSettings.LedgerAccountId</c> نوشته می‌شود.
///
/// ⚠️ و همین است که قاعدهٔ ۱۴۰۵/۰۷/۰۷ را نگه می‌دارد («برای افتتاحِ حساب
/// نباید اطلاعات حذف یا از سر یا ریست بشن»): کسی که بی‌حساب کار کرده و
/// حالا حساب می‌سازد، <b>همان دفتر</b> را با خود می‌برد، چون دفترِ ریشه
/// به نامِ همان حساب می‌خورد و «بارِ اول»ِ همگام‌سازی همه‌اش را بالا
/// می‌فرستد. فقط حسابِ <b>دوم</b> است که دفترِ تازه می‌گیرد.
/// </summary>
public static class AccountLedger
{
    /// <summary>پوشهٔ همهٔ دفترهای غیرِ ریشه.</summary>
    public const string Folder = "accounts";

    /// <summary>نامِ فایلِ دفتر — همان نامِ دفترِ ریشه.</summary>
    public const string FileName = "pump.db";

    /// <summary>
    /// نامِ پوشهٔ یک حساب.
    ///
    /// ⛔ <b>شناسهٔ خام هیچ‌وقت مستقیم نامِ پوشه نمی‌شود.</b> شناسه از سرور
    /// می‌آید و اگر روزی شکلش عوض شود (یا کسی شکلِ دیگری بفرستد)، یک
    /// <c>..\</c> در آن یعنی نوشتن بیرونِ پوشهٔ برنامه. پس نویسه‌های ناامن
    /// می‌افتند و یک اثرِ انگشتِ کوتاه ته نام می‌نشیند تا دو شناسهٔ متفاوت
    /// هیچ‌وقت به یک پوشه نرسند.
    /// </summary>
    public static string SafeName(string accountId)
    {
        var raw = (accountId ?? "").Trim();
        if (raw.Length == 0) return "";

        var keep = new StringBuilder();
        foreach (var c in raw)
        {
            if (keep.Length >= 24) break;
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_') keep.Append(c);
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..8].ToLowerInvariant();
        return keep.Length > 0 ? keep + "-" + hash : hash;
    }

    /// <summary>
    /// دفترِ این حساب کجاست.
    /// </summary>
    /// <param name="rootDbPath">دفترِ ریشه — همان <c>PumpDbFactory.DefaultPath</c>.</param>
    /// <param name="accountId">شناسهٔ حسابِ واردشده؛ خالی یعنی هنوز حسابی نیست.</param>
    /// <param name="rootOwnerId">شناسهٔ حسابی که دفترِ ریشه را برداشته؛ خالی یعنی هنوز کسی.</param>
    public static string PathFor(string rootDbPath, string? accountId, string? rootOwnerId)
    {
        var id = (accountId ?? "").Trim();
        var owner = (rootOwnerId ?? "").Trim();

        //  بی حساب ⇒ همان دفترِ ریشه. برنامه باید بی‌اینترنت و بی‌حساب هم
        //  کار کند، پس این حالت هیچ‌وقت پوشهٔ تازه نمی‌سازد.
        if (id.Length == 0) return rootDbPath;

        //  ریشه هنوز صاحبی ندارد، یا صاحبش خودِ همین حساب است.
        if (owner.Length == 0 || string.Equals(owner, id, StringComparison.Ordinal)) return rootDbPath;

        var dir = Path.GetDirectoryName(rootDbPath) ?? "";
        return Path.Combine(dir, Folder, SafeName(id), FileName);
    }

    /// <summary>
    /// آیا دفترِ ریشه باید همین حالا به نامِ این حساب بخورد؟
    ///
    /// ⚠️ فقط یک بار در عمرِ یک نصب راست می‌شود — و همان یک بار است که
    /// دادهٔ «بی‌حساب» را به نخستین حسابِ کاربر می‌رساند.
    /// </summary>
    public static bool ShouldClaimRoot(string? accountId, string? rootOwnerId) =>
        (accountId ?? "").Trim().Length > 0 && (rootOwnerId ?? "").Trim().Length == 0;
}
