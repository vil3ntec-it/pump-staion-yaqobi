namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ نشانیِ یک حساب داخلِ کیو‌آر ═════════════════════════════════════════════
///
/// عینِ ‎_acctHash‎ و ‎_splitAcctId‎ی نسخهٔ وب:
///
///     #roview-debt-&lt;شناسهٔ شخص&gt;[~&lt;شناسهٔ حساب فرعی&gt;]-pdf
///
/// ⚠️ عمداً <b>همان</b> شکل است، حرف‌به‌حرف. هر کیو‌آری که تا امروز از سایت
/// چاپ یا ذخیره شده، با همین برنامه هم خوانده می‌شود؛ و برعکس، کیو‌آری که
/// این‌جا ساخته می‌شود روی سایت هم باز می‌شود. اگر شکلش را عوض کنید،
/// کاغذهای چاپ‌شدهٔ صاحب ریپو از کار می‌افتند.
/// </summary>
public static class AcctLink
{
    /// <summary>نشانیِ حساب — ‎subId‎ی خالی یعنی حسابِ اصلی.</summary>
    public static string Build(long personId, string? subId = null, string type = "debt") =>
        "#roview-" + type + "-" + personId
        + (string.IsNullOrWhiteSpace(subId) ? "" : "~" + subId) + "-pdf";

    /// <summary>
    /// خواندنِ متنی که از کیو‌آر بیرون آمد.
    ///
    /// متن می‌تواند نشانیِ کاملِ سایت باشد (‎https://…/#roview-debt-7-pdf‎) یا
    /// فقط همان تکهٔ ‎#roview…‎ — هر دو پذیرفته می‌شوند، چون کیو‌آرهای سایت
    /// نشانیِ کامل دارند.
    ///
    /// ‎null‎ یعنی این کیو‌آر نشانیِ حساب نبود.
    /// </summary>
    public static (long PersonId, string? SubId, string Type)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var at = text.IndexOf("#roview-", StringComparison.Ordinal);
        if (at < 0) return null;

        var s = text[(at + "#roview-".Length)..];

        // دُمِ «-pdf» اختیاری است؛ بعضی نشانی‌های قدیمی ندارند.
        if (s.EndsWith("-pdf", StringComparison.Ordinal)) s = s[..^4];

        var dash = s.IndexOf('-');
        if (dash <= 0) return null;

        var type = s[..dash];
        var rest = s[(dash + 1)..];

        // ⚠️ «~» شناسهٔ حساب فرعی را جدا می‌کند — همان ‎_splitAcctId‎.
        string? sub = null;
        var tilde = rest.IndexOf('~');
        if (tilde >= 0)
        {
            sub = rest[(tilde + 1)..];
            rest = rest[..tilde];
            if (sub.Length == 0) sub = null;
        }

        return long.TryParse(rest, out var id) && id > 0 ? (id, sub, type) : null;
    }
}
