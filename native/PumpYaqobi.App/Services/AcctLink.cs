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
    /// نشانیِ <b>کاملِ قابلِ باز شدن</b> — همان چیزی که باید داخلِ کیو‌آر برود.
    ///
    /// ══ چرا سه تکه لازم است ══════════════════════════════════════════════
    ///
    /// کیو‌آرِ حساب برای <b>خودِ قرض‌دار</b> است: صاحب ریپو آن را می‌فرستد،
    /// مشتری با گوشیِ خودش اسکن می‌کند و حسابش را <b>زنده</b> می‌بیند.
    ///
    /// گوشیِ مشتری یک <b>صفحه</b> باز می‌کند (‎index.html‎)، و آن صفحه است که
    /// داده را از سرورِ خانگی می‌گیرد. پس:
    ///
    ///   <paramref name="pageUrl"/>  جایی که ‎index.html‎ سِرو می‌شود.
    ///   <paramref name="serverUrl"/> سرورِ داده — صفحه از این‌جا می‌خواند.
    ///   <paramref name="token"/>     رمزِ همان سرور، اگر داشته باشد.
    ///
    /// ⚠️ ‎server‎ و ‎token‎ حتماً باید در نشانی باشند. گوشیِ مشتری تا امروز
    /// این صفحه را باز نکرده، پس چیزی در حافظه‌اش نیست و نمی‌داند به کجا وصل
    /// شود — صفحه‌ای خالی می‌بیند. خودِ سایت هم دقیقاً همین کار را می‌کند
    /// (‎copyShareLink‎):
    ///
    ///     link = origin + pathname + '?server=' + u + '&amp;token=' + t
    ///
    /// و صفحه سرِ بارگیری برشان می‌دارد، در ‎localStorage‎ می‌گذارد و نشانی را
    /// تمیز می‌کند تا رمز در نوارِ نشانی نماند.
    ///
    /// ‎null‎ یعنی نشانیِ صفحه خالی است و کیو‌آری نمی‌شود ساخت.
    /// </summary>
    public static string? FullUrl(string? pageUrl, long personId, string? subId = null,
                                  string type = "debt",
                                  string? serverUrl = null, string? token = null)
    {
        var b = (pageUrl ?? "").Trim();
        if (b.Length == 0) return null;

        // هرچه بعد از «#» یا «?» باشد جای پرسش و هشِ خودمان را می‌گیرد.
        var cut = b.IndexOfAny(new[] { '#', '?' });
        if (cut >= 0) b = b[..cut];
        b = b.TrimEnd('/');
        if (b.Length == 0) return null;

        // بی «http» گوشی نشانی را باز نمی‌کند و متن می‌بیند.
        if (!b.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !b.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            b = "https://" + b;

        var q = "";
        var srv = (serverUrl ?? "").Trim();
        if (srv.Length > 0)
        {
            q = "/?server=" + Uri.EscapeDataString(srv);
            var t = (token ?? "").Trim();
            if (t.Length > 0) q += "&token=" + Uri.EscapeDataString(t);
        }
        else q = "/";

        return b + q + Build(personId, subId, type);
    }

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
