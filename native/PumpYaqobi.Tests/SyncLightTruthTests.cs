using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ چراغِ همگام‌سازی دربارهٔ **حالا** حرف می‌زند، نه دربارهٔ گذشته ═════════
///
/// گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۱): سرورِ حساب سبز و وصل بود
/// («✅ سرورِ حساب جواب داد (نسخهٔ ۲.۷.۰)») ولی چراغِ نوارِ پایین
/// <b>سرخ</b> می‌گفت «همگام نشد».
///
/// ریشه یک زنجیرهٔ چهارخطی بود:
///
/// <code>
/// ۱) یک نرسیدنِ گذرا        ⇒ LastError پر می‌شود
/// ۲) دورِ بعد صف خالی است   ⇒ بلوکِ فرستادن — و تنها `LastError = ""` — رد می‌شود
/// ۳) گرفتن موفق است         ⇒ ولی چیزی را پاک نمی‌کرد
/// ۴) ته حلقه: صف ۰ و خطا پر ⇒ «همگام نشد»ِ سرخ، برای همیشه
/// </code>
///
/// ⛔ یعنی چراغ دربارهٔ <b>گذشته</b> حرف می‌زد. همان «کلکِ دروغ»ِ قدغنِ این
/// ریپو، وارونه: می‌گفت خراب است در حالی که نبود.
///
/// ⚠️ این‌جا روی <b>خودِ سورس</b> سنجیده می‌شود و نه با اجرای حلقه، چون آن
/// حلقه اینترنت و توکنِ واقعی می‌خواهد و در همهٔ آزمون‌ها
/// <c>SyncEngine.Disabled</c> روشن است.
/// </summary>
public class SyncLightTruthTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Engine() =>
        File.ReadAllText(Path.Combine(Root,
            "PumpYaqobi.App/Services/SyncEngine.cs".Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>
    /// ⛔ <b>گرفتنِ موفق، خطای دورِ قبل را پاک می‌کند.</b>
    ///
    /// این بند دندان دارد: با برگرداندنِ خطِ قدیمی
    /// (<c>if (applied.Failed > 0) LastError = …</c> و بی هیچ پاک کردنی)
    /// همان لحظه سرخ می‌شود.
    /// </summary>
    [Fact]
    public void Gereftane_Movaffagh_Khataye_Dore_Ghabl_Ra_Pak_Mikonad()
    {
        var src = Engine();

        //  جای پاک کردن باید **پس از** گرفتنِ موفق باشد و **پیش از** جایی
        //  که چراغ تصمیم می‌گیرد.
        var pulled = src.IndexOf("var applyWhy = \"\";", StringComparison.Ordinal);
        var clear = src.IndexOf("LastError = applyWhy;", StringComparison.Ordinal);
        var light = src.IndexOf("else if (LastError.Length > 0) Set(SyncLight.Failed", StringComparison.Ordinal);

        Assert.True(pulled > 0, "مسیرِ گرفتن پیدا نشد");
        Assert.True(clear > pulled, "خطا پس از گرفتنِ موفق پاک نمی‌شود");
        Assert.True(light > clear, "چراغ پیش از پاک شدنِ خطا تصمیم می‌گیرد");

        //  ⛔ و خطای **واقعیِ همین دور** نباید پاک شود
        Assert.Contains("applyWhy = \"چند تغییرِ رسیده ننشست: \"", src);

        //  ⛔ و حالِ روی دیسک هم همان را می‌گوید، نه خطای کهنه را
        var stored = src.IndexOf("x.LastError = applyWhy;", StringComparison.Ordinal);
        Assert.True(stored > pulled, "حالِ ذخیره‌شده خطای کهنه را نگه می‌دارد");
    }

    /// <summary>
    /// ⛔ <b>تنها جای پاک کردن، بلوکِ فرستادن نیست.</b> آن بلوک فقط وقتی
    /// می‌دود که صف <b>پر</b> باشد (<c>pending &gt; 0</c>) — و دقیقاً
    /// همان بود که چراغ را برای همیشه سرخ نگه می‌داشت: پمپی که هنوز چیزی
    /// ننوشته صفش خالی است، پس هیچ‌وقت به آن خط نمی‌رسید.
    /// </summary>
    [Fact]
    public void Pak_Kardan_Faghat_Dar_Boloke_Ferestadan_Nist()
    {
        var src = Engine();
        var n = Count(src, "LastError = \"\";");
        var any = Count(src, "LastError = applyWhy;");

        Assert.True(any >= 1, "مسیرِ گرفتن خطا را پاک نمی‌کند");
        //  بلوکِ فرستادن همچنان پاک می‌کند — ولی دیگر تنها جا نیست
        Assert.True(n + any >= 2, "پاک کردن فقط یک جا مانده است");
    }

    private static int Count(string src, string needle)
    {
        int n = 0, i = 0;
        while ((i = src.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
