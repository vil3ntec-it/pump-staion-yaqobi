namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ لینک و کیو‌آرِ اپِ کارمندان ═════════════════════════════════════════════
///
/// صاحب ریپو کیو‌آر را به کارمند می‌دهد؛ کارمند اسکن می‌کند، گوشی‌اش صفحهٔ
/// ‎kar/‎ را باز می‌کند و آن صفحه می‌داند به کدام سرور وصل شود.
///
/// ⚠️ چرا ‎server‎ و ‎token‎ و ‎station‎ حتماً باید در نشانی باشند: گوشیِ کارمند
/// این صفحه را تا امروز باز نکرده، پس چیزی در حافظه‌اش نیست و نمی‌داند کجا
/// وصل شود — صفحه‌ای خالی می‌بیند. خودِ صفحه سرِ بارگیری برشان می‌دارد، در
/// ‎localStorage‎ می‌گذارد و نشانی را تمیز می‌کند تا رمز در نوارِ نشانی نماند.
///
/// ⚠️ این با کیو‌آرِ حسابِ مشتری (<see cref="AcctView"/>) یکی نیست و نباید
/// قاطی شود:
///   • کیو‌آرِ مشتری: دادهٔ <b>یک حساب</b> داخلِ خودِ کد، بی سرور و بی رمز.
///   • کیو‌آرِ کارمند: <b>هیچ داده‌ای</b> ندارد، فقط نشانیِ سرور — و خودِ صفحه
///     پشتِ رمزِ همین برنامه قفل است.
///
/// ⚠️⚠️ رمزی که این‌جا می‌رود <b>رمزِ فقط‌خواندنیِ</b> پمپ است، نه رمزِ
/// برنامه. این کد روی کاغذ چاپ می‌شود و دستِ چند نفر می‌گردد؛ با رمزِ
/// نوشتن، همان کاغذ اجازهٔ پاک کردنِ دفترِ پمپ را هم داشت. سرور خودش هم
/// جلویش را می‌گیرد، ولی اول از همه این‌جا نباید فرستاده شود.
/// </summary>
public static class KarLink
{
    /// <summary>
    /// نشانیِ پیش‌فرضِ اپِ کارمندان — دامنهٔ خودِ صاحب ریپو (همان ‎CNAME‎).
    /// قاعدهٔ «نامِ مخزن هیچ‌جا نیاید» این‌جا هم رعایت شده.
    /// </summary>
    public const string DefaultBase = "https://yaqobipump.top/kar/";

    /// <summary>
    /// نشانیِ کاملِ باز‌شدنی. ‎null‎ یعنی سروری تنظیم نشده و کیو‌آری نمی‌شود
    /// ساخت — اپِ کارمندان بی سرور هیچ کاری نمی‌تواند بکند.
    /// </summary>
    /// <param name="viewerUrl">
    /// «نشانیِ صفحهٔ حساب»ِ تنظیمات. اگر پُر باشد، اپِ کارمندان کنارِ همان
    /// می‌نشیند (‎…/view/‎ ⇒ ‎…/kar/‎) — تا اگر روزی سایت جای دیگری رفت، این هم
    /// خودش دنبالش برود.
    /// </param>
    public static string? Build(string? viewerUrl, string? serverUrl, string? token, string? stationCode)
    {
        var srv = (serverUrl ?? "").Trim();
        if (srv.Length == 0) return null;

        var b = BaseOf(viewerUrl);
        var q = "?server=" + Uri.EscapeDataString(srv);

        var t = (token ?? "").Trim();
        if (t.Length > 0) q += "&token=" + Uri.EscapeDataString(t);

        var code = (stationCode ?? "").Trim();
        if (code.Length > 0) q += "&station=" + Uri.EscapeDataString(code);

        return b + q;
    }

    /// <summary>
    /// همان کار، با خواندنِ خودِ تنظیمات.
    ///
    /// رمزِ فقط‌خواندنی را ترجیح می‌دهد. اگر سرور هنوز به‌روز نشده و چنین
    /// رمزی نداده، به رمزِ برنامه برمی‌گردد — وگرنه کیو‌آرِ کارمند یک‌شبه از
    /// کار می‌افتاد و کسی نمی‌فهمید چرا.
    /// </summary>
    public static string? Build(AppHost host)
    {
        var readKey = HomeLink.ReadKey(host);
        return Build(
            host.Settings.GetString(SettingsKeys.ViewerUrl),
            HomeLink.Url(host),
            readKey.Length > 0 ? readKey : HomeLink.Token(host),
            HomeLink.StationCode(host));
    }

    /// <summary>
    /// نشانیِ صفحهٔ اپِ کارمندان — همیشه با ‎https‎ و یک ‎/‎ ته آن.
    /// </summary>
    public static string BaseOf(string? viewerUrl)
    {
        var v = (viewerUrl ?? "").Trim();
        if (v.Length == 0) return DefaultBase;

        // هرچه بعد از «#» یا «?» باشد جای پرسشِ خودمان را می‌گیرد
        var cut = v.IndexOfAny(new[] { '#', '?' });
        if (cut >= 0) v = v[..cut];
        v = v.TrimEnd('/');
        if (v.Length == 0) return DefaultBase;

        if (!v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            v = "https://" + v;

        // ‎…/view‎ همسایهٔ ‎…/kar‎ است؛ هر چیز دیگری، ‎kar/‎ زیرِ خودش
        var last = v.LastIndexOf('/');
        if (last > "https://".Length && v[(last + 1)..].Equals("view", StringComparison.OrdinalIgnoreCase))
            v = v[..last];

        return v + "/kar/";
    }
}
