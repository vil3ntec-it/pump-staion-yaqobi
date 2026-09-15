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

    /// <summary>
    /// دفترهای این حساب — از امروز <b>همهٔ</b> دفترها، نه فقط دفترِ فعال.
    ///
    /// ⚠️ ‎Head‎/‎Rows‎/‎Summary‎ی بالا برداشته نشده‌اند و نباید برداشته شوند:
    /// کیو‌آرهایی که تا امروز <b>چاپ شده و دستِ مشتری است</b> همان شکل را
    /// دارند و صفحهٔ ‎view/‎ باید تا ابد بازشان کند. پس عکسِ تازه هر دو را با
    /// هم می‌برد — دفترِ اول در جای قدیمی هم تکرار می‌شود — و صفحه اگر
    /// ‎b‎ را دید همان را نشان می‌دهد.
    /// </summary>
    [JsonPropertyName("b")] public List<AcctBook> Books { get; set; } = new();
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

    /// <summary>
    /// ══ نشانیِ پیش‌فرضِ «صفحهٔ حسابِ من» ═════════════════════════════════════
    ///
    /// دامنهٔ خودِ صاحب ریپو است (همان ‎CNAME‎ی مخزن)، نه نشانیِ منبع — قاعدهٔ
    /// «توش نوشته نباشه از مخزن فلان فلان» این‌جا هم رعایت شده: مشتری روی
    /// گوشی‌اش فقط دامنهٔ خودِ پمپ را می‌بیند.
    ///
    /// اگر روزی صفحه جای دیگری رفت، «تنظیمات › نشانیِ صفحهٔ حساب» جایش را
    /// می‌گیرد و این پیش‌فرض کنار می‌رود.
    /// </summary>
    public const string DefaultBase = "https://yaqobipump.top/view/";

    /// <summary>ردیف‌های تازه‌تر مهم‌ترند؛ کمترین شماری که همیشه می‌ماند.</summary>
    private const int MinRows = 5;

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>همان تنظیمِ JSON برای هر کسی که عکس را جای دیگری می‌فرستد (کیو‌آرِ زنده).</summary>
    public static JsonSerializerOptions JsonOptions => Json;

    /// <summary>
    /// نشانیِ کاملِ باز‌شدنی. ‎baseUrl‎ خالی یعنی نشانیِ پیش‌فرضِ خودِ برنامه.
    ///
    /// ⚠️ اگر عکس بزرگ باشد، ردیف‌های **قدیمی** کم می‌شوند تا کیو‌آر خواندنی
    /// بماند و همان‌جا هم در صفحه نوشته می‌شود که چند ردیفِ آخر آمده. هرگز
    /// نشانیِ ناقص برنمی‌گردد.
    /// </summary>
    /// <param name="live">
    /// تکهٔ کیو‌آرِ زنده (‎s=…&amp;a=…&amp;k=…&amp;t=…‎ از <see cref="AcctLive.Fragment"/>)
    /// که پیش از ‎d=‎ در هش می‌نشیند. خالی یعنی کیو‌آرِ ایستا — همان قبلی.
    /// ⚠️ پیش از ‎d=‎، نه بعدش: صفحه ‎d‎ را با ‎(?:^|&amp;)d=([^&amp;]+)‎ می‌خواند و
    /// دادهٔ فشرده هیچ ‎&amp;‎ ندارد، پس هر دو ترتیب کار می‌کند؛ ولی این‌طور
    /// پارامترهای کوتاه اولِ نشانی‌اند و در کیو‌آرِ بریده هم خوانا می‌مانند.
    /// </param>
    public static string Url(string? baseUrl, AcctSnapshot snap, string? marker = null, string? live = null)
    {
        var b = Clean(baseUrl);
        var tail = string.IsNullOrEmpty(marker) ? "" : "?p=" + Uri.EscapeDataString(marker);
        var head = string.IsNullOrEmpty(live) ? "#d=" : "#" + live + "&d=";

        // ⚠️ ردیف‌های اصلِ همهٔ دفترها کنار گذاشته می‌شوند و در پایان — چه کد
        // جا شده باشد چه نه — سرِ جایشان برمی‌گردند. عکسی که به این تابع
        // داده شده مالِ صداکننده است و نباید ناقص تحویلش داده شود.
        var all = snap.Rows;
        var bookAll = snap.Books.Select(x => x.Rows).ToList();

        // بیشترین شمارِ ردیفِ یک دفتر — سقفی که از آن پایین می‌آییم. ‎keep‎
        // برای همهٔ دفترها یکی است تا دفترِ پُر، دفترِ خلوت را خالی نکند.
        var keep = Math.Max(all.Count, bookAll.Count == 0 ? 0 : bookAll.Max(r => r.Count));

        while (true)
        {
            Trim(snap, all, keep, n => snap.Note = n);
            for (var i = 0; i < snap.Books.Count; i++)
            {
                var book = snap.Books[i];
                Trim(book, bookAll[i], keep, n => book.Note = n);
            }

            var url = b + tail + head + Encode(snap);
            if (url.Length <= MaxUrl || keep <= MinRows) { Restore(); return url; }

            // هر بار یک‌چهارمِ ردیف‌های مانده کم می‌شود — چند تکرارِ کوتاه
            keep = Math.Max(MinRows, keep - Math.Max(1, keep / 4));
        }

        void Restore()
        {
            snap.Rows = all;
            for (var i = 0; i < snap.Books.Count; i++) snap.Books[i].Rows = bookAll[i];
        }

        // ردیف‌های **قدیمی** کم می‌شوند، نه تازه‌ها: مشتری آخرین معامله‌هایش
        // را می‌خواهد. و آرشیوِ ماه‌به‌ماه دست نمی‌خورد، پس ماهی گم نمی‌شود.
        static void Trim(object owner, List<string[]> source, int keep, Action<string> note)
        {
            var cut = keep >= source.Count ? source : source.Skip(source.Count - keep).ToList();
            if (owner is AcctSnapshot s) s.Rows = cut; else ((AcctBook)owner).Rows = cut;
            note(keep >= source.Count
                ? ""
                : "فقط " + keep + " ردیفِ آخر در این کد جا شد (از " + source.Count
                  + " ردیف) — جمع‌ها و آرشیوِ ماه‌ها کاملِ کامل‌اند.");
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
        if (b.Length == 0) b = DefaultBase;

        var cut = b.IndexOfAny(new[] { '#', '?' });
        if (cut >= 0) b = b[..cut];
        b = b.TrimEnd('/');
        if (!b.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !b.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            b = "https://" + b;
        return b + "/";
    }
}

/// <summary>
/// ══ یک «دفتر» داخلِ کیو‌آر ═══════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «یارو نمی‌تواند ببیند که واحدِ پول چقدر قرض‌دار است یا
/// تیل چقدر… شرکت‌ها هم نمی‌دانند دیزل چقدر از من می‌خواهند یا پطرول چقدر.»
///
/// ریشه‌اش این بود که کیو‌آر فقط **یک** دفتر می‌برد: هر حسابِ قرض‌دار دو دفترِ
/// کاملاً جدا دارد (‎FuelRows‎ و ‎MoneyRows‎) و تا امروز فقط دفترِ «فعال»
/// (‎ActiveRows‎) داخلِ کد می‌رفت. پس مشتری‌ای که هم تیل برده بود هم پول،
/// نیمِ حسابش را می‌دید و خبر نداشت نیمهٔ دیگری هم هست.
///
/// از امروز هر عکس فهرستی از دفترهاست و هر دفتر خودش کامل است: خلاصه،
/// تفکیکِ پطرول/دیزل، جدول، و آرشیوِ ماه‌به‌ماه.
///
/// ⚠️ نام‌ها یک‌حرفی‌اند چون هر بایت در کیو‌آر جا می‌گیرد.
/// </summary>
public sealed class AcctBook
{
    /// <summary>نامِ دفتر — «واحد تیل»، «واحد پول»، «خرید تیل».</summary>
    [JsonPropertyName("k")] public string Title { get; set; } = "";

    /// <summary>واحدِ عددهای این دفتر — «لیتر» یا «افغانی».</summary>
    [JsonPropertyName("u")] public string Unit { get; set; } = "";

    /// <summary>کادرهای خلاصه — ‎[برچسب, مقدار]‎.</summary>
    [JsonPropertyName("s")] public List<string[]> Summary { get; set; } = new();

    /// <summary>
    /// تفکیکِ تیل — ‎[نامِ تیل, بردگی, رسید, فیصدی, الباقی]‎.
    /// همان چیزی که خواسته شد: «دیزل چقدر از من می‌خواهند یا پطرول چقدر».
    /// </summary>
    [JsonPropertyName("f")] public List<string[]> Fuels { get; set; } = new();

    /// <summary>سربرگِ جدولِ تفکیکِ تیل — چون دفترِ قرض‌دار و دفترِ شرکت
    /// ستون‌های متفاوتی دارند (بردگی/رسید در برابرِ تن/پرداختی).</summary>
    [JsonPropertyName("fh")] public List<string> FuelHead { get; set; } = new();

    [JsonPropertyName("h")] public List<string> Head { get; set; } = new();
    [JsonPropertyName("r")] public List<string[]> Rows { get; set; } = new();

    /// <summary>
    /// آرشیوِ ماه‌به‌ماه — ‎[ماه, بردگی, رسید, الباقیِ همان ماه]‎.
    ///
    /// ⚠️ این‌جا حساب می‌شود، نه در مرورگر: ردیف‌های قدیمی ممکن است برای جا
    /// شدن در کیو‌آر کم شوند، ولی آرشیو **همیشه کاملِ همهٔ ماه‌هاست** — وگرنه
    /// مشتری ماه‌هایی را که ردیفشان کم شده اصلاً نمی‌دید.
    /// </summary>
    [JsonPropertyName("g")] public List<string[]> Archive { get; set; } = new();

    /// <summary>
    /// شمارهٔ ستونِ تاریخ در <see cref="Head"/> — صفحه با همین «ماه» را
    /// درمی‌آورد. ‎-1‎ یعنی این دفتر ستونِ تاریخ ندارد.
    ///
    /// ⚠️ چرا شمارهٔ ستون و نه فهرستِ ماهِ هر ردیف: فهرست یعنی یک رشتهٔ هفت
    /// حرفی به ازای هر ردیف داخلِ کیو‌آر. شمارهٔ ستون یک عدد است.
    /// </summary>
    [JsonPropertyName("dc")] public int DateCol { get; set; } = -1;

    /// <summary>شمارهٔ ستونِ «تیل» — برای فیلترِ پطرول/دیزل. ‎-1‎ یعنی ندارد.</summary>
    [JsonPropertyName("fc")] public int FuelCol { get; set; } = -1;

    /// <summary>پیامِ پایینِ جدول (مثلاً «فقط ردیف‌های آخر»).</summary>
    [JsonPropertyName("n")] public string Note { get; set; } = "";
}
