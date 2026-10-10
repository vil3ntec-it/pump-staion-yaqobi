using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// ══ تبدیلِ تیل — پطرول ⇄ دیزل (۱۴۰۵/۰۷/۱۸) ══════════════════════════════════
/// خواستهٔ صاحب ریپو: «ارزشِ تیلِ مبدأ با قیمتِ خریدِ واقعیِ ثبت‌شده در مخزن محاسبه شود،
/// نه قیمتِ اتحادیه. سپس ارزشِ کلِ تیلِ مبدأ بر قیمتِ خریدِ هر لیترِ تیلِ مقصد تقسیم شود
/// تا مقدارِ قابلِ تبدیل به دست آید.» مثال: ۱۰۰ لیتر پطرول × ۷۰ = ۷٬۰۰۰؛ دیزلِ ۸۰ ⇒ ۸۷٫۵
/// لیتر، نه ۱۰۰.
///
///   ارزشِ مبدأ = لیترِ مبدأ × قیمتِ خریدِ هر لیترِ مبدأ
///   لیترِ مجاز = ارزشِ مبدأ ÷ قیمتِ خریدِ هر لیترِ مقصد
///   اختلاف    = لیترِ تحویل‌شده − لیترِ مجاز   (مثبت ⇒ «لیتر اضافه داده شده»)
///   سود/ضرر   = (لیترِ مجاز − لیترِ تحویل‌شده) × قیمتِ مقصد
///
/// ⛔ خالص و یکسان برای هر دو جهت. تحویلِ نانوشته (صفر) یعنی «همان مجاز».
/// ⛔ لیتر تا دو رقمِ اعشار گرد می‌شود و پول تا افغانی — همان قاعدهٔ ورق.
/// </summary>
public static class FuelConversionService
{
    /// <summary>دو لیتر «یکی‌اند» اگر اختلافشان از این کمتر باشد (یک صدم لیتر).</summary>
    public const decimal LiterEps = 0.005m;

    public const string StatusOk = "درست";
    public const string StatusOver = "لیتر اضافه داده شده";
    public const string StatusUnder = "کمتر از مجاز داده شد";

    public static FuelType Other(FuelType f) => f == FuelType.Diesel ? FuelType.Petrol : FuelType.Diesel;

    public static ConversionResult Compute(decimal qty, decimal fromPrice, decimal toPrice, decimal delivered)
    {
        if (qty <= 0 || fromPrice <= 0 || toPrice <= 0)
            return new ConversionResult(false, 0, 0, 0, 0, 0, "");
        var value = qty * fromPrice;
        var allowed = Math.Round(value / toPrice, 2, MidpointRounding.AwayFromZero);
        var given = delivered > 0 ? delivered : allowed;
        var diff = given - allowed;
        if (Math.Abs(diff) < LiterEps) diff = 0;
        var pl = Math.Round(-diff * toPrice, 0, MidpointRounding.AwayFromZero);
        var status = diff > 0 ? StatusOver : diff < 0 ? StatusUnder : StatusOk;
        return new ConversionResult(true, Math.Round(value, 0, MidpointRounding.AwayFromZero), allowed, given, diff, pl, status);
    }
}

/// <summary>
/// نتیجهٔ یک تبدیل. ‎Ok=false‎ یعنی یکی از سه عدد (لیتر، قیمتِ مبدأ، قیمتِ مقصد) نیست.
/// ‎ProfitLoss‎ مثبت سود است و منفی ضرر.
/// </summary>
public readonly record struct ConversionResult(
    bool Ok, decimal FromValue, decimal Allowed, decimal Delivered, decimal Diff, decimal ProfitLoss, string Status)
{
    public bool Over => Diff > 0;
    public decimal ExtraLiters => Diff > 0 ? Diff : 0;
    public decimal Loss => ProfitLoss < 0 ? -ProfitLoss : 0;
}
