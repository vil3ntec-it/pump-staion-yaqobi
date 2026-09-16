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
    /// فایلِ نصبِ اندروید — <b>روی دامنهٔ خودِ صاحب ریپو</b>، نه روی نشانیِ
    /// مخزن. ورک‌فلوی صفحه (<c>deploy-pages.yml</c>) همان فایلِ انتشارِ
    /// <c>kar-latest</c> را کنارِ سایت می‌گذارد، پس این لینک همیشه تازه‌ترین
    /// فایلِ نصب است و می‌شود در واتساپ فرستادش.
    ///
    /// ⚠️ فقط <c>PumpYaqobiKar.apk</c> — فایلِ نصبِ سایتِ قدیم
    /// (<c>android-latest</c>) هیچ‌وقت به کاربر داده نمی‌شود.
    /// </summary>
    public static string ApkUrl(string viewerUrl = "") =>
        SiteOf(viewerUrl) + "/downloads/PumpYaqobiKar.apk";

    /// <summary>
    /// لینکِ آیفون (و هر مرورگرِ دیگری): همان صفحهٔ <c>kar/</c>. کاربر در
    /// سافاری بازش می‌کند و «افزودن به صفحهٔ اصلی» می‌زند — اپِ آیفون همین
    /// است و چیزِ دیگری ندارد.
    /// </summary>
    public static string IphoneUrl(string viewerUrl = "") => BaseOf(viewerUrl);

    /// <summary>
    /// پیامِ آمادهٔ فرستادن به کارمند یا کارفرما — همان چیزی که در واتساپ
    /// چسبانده می‌شود. کد اگر نباشد، جای خطش خالی می‌ماند (لینک‌ها بی کد هم
    /// کار می‌کنند؛ اپ خودش کد می‌پرسد).
    /// </summary>
    public static string ShareText(string code, string pumpName = "", string viewerUrl = "")
    {
        var name = string.IsNullOrWhiteSpace(pumpName) ? "پمپ" : pumpName.Trim();
        var clean = new string((code ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var lines = new List<string>
        {
            "اپِ «" + name + "» — دیدنِ حساب‌ها و مخزن روی گوشی:",
            "",
            "📱 اندروید (فایلِ نصب): " + ApkUrl(viewerUrl),
            "🍎 آیفون: " + IphoneUrl(viewerUrl) + "  (در سافاری باز کنید و «افزودن به صفحهٔ اصلی» را بزنید)",
        };
        if (clean.Length > 0)
        {
            lines.Add("");
            lines.Add("🔑 کدِ پمپ: " + FormatCode(clean));
            lines.Add("همین کد را در اپ بزنید — فقط حساب‌های همین پمپ را می‌بینید.");
        }
        return string.Join("\n", lines);
    }

    /// <summary>‎K7PM-3XQ2‎ — همان شکلی که در پروفایل دیده می‌شود.</summary>
    private static string FormatCode(string clean) =>
        clean.Length > 4 ? clean[..4] + "-" + clean[4..] : clean;

    /// <summary>
    /// ریشهٔ سایت (بی ‎/kar/‎ و بی ‎/view/‎) — برای فایل‌های کنارِ سایت.
    /// </summary>
    public static string SiteOf(string? viewerUrl)
    {
        var b = BaseOf(viewerUrl);                       // …/kar/
        return b[..^"/kar/".Length];
    }

    /// <summary>
    /// لینکِ «با کدِ پمپ»: اپِ گوشی با این باز می‌شود و همان لحظه کد را
    /// می‌زند (‎?code=‎). ⚠️ هیچ رمزِ سروری در این لینک نیست — فقط همان کدی
    /// که خودِ صاحبِ پمپ به کارمند می‌دهد؛ نشانی و رمزِ خواندن را اپ خودش از
    /// ابر می‌گیرد.
    /// </summary>
    public static string ForCode(string code, string viewerUrl = "")
    {
        var clean = new string((code ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return BaseOf(viewerUrl) + "?code=" + Uri.EscapeDataString(clean);
    }

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
