using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ رسیدِ سربرگِ قرض‌دار: ردیف ساخته شود و به حسابِ دیگر سرایت نکند ══════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «توی سربرگ رسید می‌زنم و جدولی اگه نباشه
/// رسید رو می‌گیره اما جدولی ساخته نمی‌شه… و همون مقدار که توی یک حساب زدم
/// توی همه حساب‌ها سرایت می‌کنه.»
///
/// ریشه‌ها و رفتارِ کامل در سنجهٔ رابطِ <c>headrasid</c>
/// (<c>PumpYaqobi.UiTests/HeadRasidProbe.cs</c>) سنجیده می‌شود؛ این‌جا فقط
/// قاعده‌هایی که بی پنجره سنجیدنی‌اند. ⛔ شورا ت۳: سه آزمونِ «متنِ سورس»ِ پیشین
/// (ردیفِ شبح، کشِ روی دیسک، کادرِ سربرگ) رفتند — همان سه را <c>headrasid</c>
/// با پنجره و دفترِ واقعی در CI می‌سنجد.
/// </summary>
public class HeadReceiptTests
{
    [Theory]
    [InlineData("‏7000", 7000)]     // RLM — صفحه‌کلیدِ فارسیِ ویندوز
    [InlineData("‎۲۰۰۰", 2000)]     // LRM + رقمِ فارسی
    [InlineData("7 000", 7000)]     // فاصلهٔ نشکن
    [InlineData("۱۲٬۵۰۰", 12500)]        // جداکنندهٔ هزارگانِ فارسی
    [InlineData("۱۲٫۵", 12.5)]           // ممیزِ فارسی
    [InlineData("1,250", 1250)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    public void Num_NevisehayeNamarei_RaNemikhorad(string text, double want) =>
        Assert.Equal((decimal)want, Shamsi.Num(text));
}
