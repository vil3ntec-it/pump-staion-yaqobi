using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ عکسِ یک حساب — همان چیزی که داخلِ کیو‌آر می‌رود ═════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «طرف که اسکن می‌کند سایت باز بشود و تمامِ اطلاعاتش
/// تو گوشی بیاید؛ یک سایتِ جدید برای دیدنِ اطلاعات، آن سایتِ قبلی را ول کن.»
///
/// ⚠️ تصمیمِ اصلی: داده **داخلِ خودِ کیو‌آر** است، نه پشتِ یک سرور.
///   • سرورِ صاحب ریپو فقط API است و صفحه‌ای سِرو نمی‌کند؛
///   • گوشیِ مشتری هیچ رمزی ندارد و نباید داشته باشد؛
///   • و این‌طور کدِ چاپ‌شده بی‌اینترنتِ سرور هم باز می‌شود.
///
/// پس عکسِ حساب به JSON می‌رود، فشرده می‌شود (‎deflate‎ی خام) و ‎base64url‎ در
/// تکهٔ ‎#d=…‎ی نشانی می‌نشیند. صفحهٔ ‎view/‎ همان را باز می‌کند و نشان می‌دهد.
///
/// ⚠️ نام‌ها عمداً یک‌حرفی‌اند: هر حرفِ اضافه در JSON یعنی کیو‌آرِ چگال‌تر و
/// سخت‌خوان‌تر.
/// </summary>
public sealed class AcctSnapshot
{
    /// <summary>نوعِ حساب — «قرض‌دار (واحد تیل)»، «شرکت تیل» …</summary>
    [JsonPropertyName("t")] public string Kind { get; set; } = "";

    /// <summary>نامِ صاحبِ حساب.</summary>
    [JsonPropertyName("n")] public string Name { get; set; } = "";

    /// <summary>عنوانِ همین حساب (حسابِ فرعی و مانندِ آن) — خالی یعنی حسابِ اصلی.</summary>
    [JsonPropertyName("a")] public string Account { get; set; } = "";

    /// <summary>واحدِ عددهای خلاصه: «لیتر» یا «افغانی».</summary>
    [JsonPropertyName("u")] public string Unit { get; set; } = "";

    /// <summary>تاریخِ ساختِ همین عکس.</summary>
    [JsonPropertyName("d")] public string Date { get; set; } = "";

    /// <summary>کادرهای خلاصه — هر کدام ‎[برچسب, مقدار]‎.</summary>
    [JsonPropertyName("s")] public List<string[]> Summary { get; set; } = new();

    /// <summary>سربرگِ ستون‌ها.</summary>
    [JsonPropertyName("h")] public List<string> Head { get; set; } = new();

    /// <summary>ردیف‌ها — همان چیزی که روی صفحه دیده می‌شود، آمادهٔ نمایش.</summary>
    [JsonPropertyName("r")] public List<string[]> Rows { get; set; } = new();

    /// <summary>پیامِ پایینِ جدول (مثلاً «فقط ۶۰ ردیفِ آخر»). خالی یعنی هیچ.</summary>
    [JsonPropertyName("m")] public string Note { get; set; } = "";
}

/// <summary>ساختنِ نشانیِ «صفحهٔ حسابِ من» از روی یک عکسِ حساب.</summary>
public static class AcctView
{
    /// <summary>
    /// بیشترین درازای نشانیِ داخلِ کیو‌آر.
    ///
    /// ⚠️ سقفِ خودِ استاندارد ۲٬۹۵۳ بایت است، ولی کدِ پر آن‌قدر ریز و چگال
    /// می‌شود که دوربینِ گوشی از روی کاغذ نمی‌خواندش. این عدد تجربی است و
    /// عمداً محافظه‌کار: ردیف‌های قدیمی کم می‌شوند تا نشانی زیرِ آن بماند.
    /// </summary>
    public const int MaxUrl = 1400;

    /// <summary>ردیف‌های تازه‌تر مهم‌ترند؛ کمترین شماری که همیشه می‌ماند.</summary>
    private const int MinRows = 5;

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// نشانیِ کاملِ باز‌شدنی. ‎baseUrl‎ خالی یعنی نشانیِ پیش‌فرضِ خودِ برنامه.
    ///
    /// ⚠️ اگر عکس بزرگ باشد، ردیف‌های **قدیمی** کم می‌شوند تا کیو‌آر خواندنی
    /// بماند و همان‌جا هم در صفحه نوشته می‌شود که چند ردیفِ آخر آمده. هرگز
    /// نشانیِ ناقص برنمی‌گردد.
    /// </summary>
    public static string Url(string? baseUrl, AcctSnapshot snap, string? marker = null)
    {
        var b = Clean(baseUrl);
        var all = snap.Rows;
        var keep = all.Count;

        while (true)
        {
            snap.Rows = keep >= all.Count ? all : all.Skip(all.Count - keep).ToList();
            snap.Note = keep >= all.Count
                ? ""
                : "فقط " + keep + " ردیفِ آخر در این کد جا شد (از " + all.Count + " ردیف).";

            var url = b + (string.IsNullOrEmpty(marker) ? "" : "?p=" + Uri.EscapeDataString(marker))
                        + "#d=" + Encode(snap);
            if (url.Length <= MaxUrl || keep <= MinRows) { snap.Rows = all; return url; }

            // هر بار یک‌چهارمِ ردیف‌های مانده کم می‌شود — چند تکرارِ کوتاه
            keep = Math.Max(MinRows, keep - Math.Max(1, keep / 4));
        }
    }

    /// <summary>JSON ← فشرده ← ‎base64url‎.</summary>
    public static string Encode(AcctSnapshot snap)
    {
        var raw = JsonSerializer.SerializeToUtf8Bytes(snap, Json);
        using var ms = new MemoryStream();
        using (var z = new DeflateStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            z.Write(raw, 0, raw.Length);
        return Convert.ToBase64String(ms.ToArray())
                      .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>برای آزمون — همان راهِ برگشت، تا مطمئن شویم صفحه همین را می‌بیند.</summary>
    public static AcctSnapshot? Decode(string b64)
    {
        try
        {
            var s = b64.Replace('-', '+').Replace('_', '/');
            s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
            using var ms = new MemoryStream(Convert.FromBase64String(s));
            using var z = new DeflateStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            z.CopyTo(outMs);
            return JsonSerializer.Deserialize<AcctSnapshot>(
                Encoding.UTF8.GetString(outMs.ToArray()), Json);
        }
        catch { return null; }
    }

    /// <summary>نشانیِ صفحه را تمیز کن: بی پرسش، بی هش، با ‎https‎ و یک ‎/‎ ته آن.</summary>
    private static string Clean(string? baseUrl)
    {
        var b = (baseUrl ?? "").Trim();
        if (b.Length == 0) b = PumpYaqobi.App.Update.UpdateService.ViewerBaseUrl;

        var cut = b.IndexOfAny(new[] { '#', '?' });
        if (cut >= 0) b = b[..cut];
        b = b.TrimEnd('/');
        if (!b.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !b.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            b = "https://" + b;
        return b + "/";
    }
}
