using System.Globalization;

namespace PumpYaqobi.App.Localization;

/// <summary>
/// ساعتِ سربرگ. فقط ساعت — تاریخ خطِ بالای همین بلوک است (مثلِ نسخهٔ وب که
/// ‎#headerDate‎ و خطِ دومش جدا هستند). پیش از این تاریخ در هر دو خط تکرار
/// می‌شد.
///
/// ⛔ <b>دوازده‌ساعته با AM/PM</b> — خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۵):
/// «ساعت قالبِ ۲۴ ساعته نباشد و pm am داشته باشد». هر جا ساعتِ روز نشان
/// داده می‌شود (سربرگ، پنجرهٔ «🕘 تاریخ و ساعت») از همین‌جا می‌گذرد.
/// ⚠️ پیشوندِ «نشانهٔ چپ‌به‌راست» (‎U+200E‎): بی آن، در پاراگرافِ راست‌به‌چپ
/// عدد و «AM» دو تکه می‌شوند و «AM 08:30:44» کشیده می‌شد (عکسِ ‎round15‎
/// گرفتش). ⛔ «جزیره» (‎U+2066‎) این‌جا کار نمی‌کند — آوالونیا آن را نادیده
/// می‌گیرد؛ نشانه با قاعدهٔ W7ِ خودِ الگوریتمِ دوسویه عدد را چپ‌به‌راست
/// می‌کند و هر دو یک تکه می‌مانند.
/// </summary>
public static class Clock
{
    public static string Now() => Of(DateTime.Now, seconds: true);

    /// <summary>«08:25:42 AM» یا «09:05 PM».</summary>
    public static string Of(DateTime t, bool seconds = false) =>
        "\u200E" + t.ToString(seconds ? "hh:mm:ss tt" : "hh:mm tt", CultureInfo.InvariantCulture);

    /// <summary>برچسبِ ساعتِ ۰ تا ۲۳ در قالبِ دوازده‌ساعته: «12 AM»، «01 PM»، …</summary>
    public static string HourLabel(int h) =>
        (h % 12 == 0 ? 12 : h % 12).ToString("00", CultureInfo.InvariantCulture) + (h < 12 ? " AM" : " PM");
}
