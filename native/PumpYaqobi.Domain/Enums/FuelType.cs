namespace PumpYaqobi.Domain.Enums;

/// <summary>
/// نوعِ سوخت — بندِ ۱۰ خواستهٔ صاحب ریپو.
///
/// ⚠️ در نسخهٔ HTML این با رشته‌های پراکنده («petrol» / «diesel» و جاهایی
/// «پطرول» / «دیزل») نگه داشته می‌شد و همان ریشهٔ باگِ «پطرول و دیزل جابه‌جا
/// می‌شوند» بود: هر جا یک رشته غلط تایپ می‌شد یا مقایسه‌اش فرق می‌کرد، ردیف
/// خاموش به دفترِ دیگری می‌رفت.
///
/// این‌جا دیگر ممکن نیست: کامپایلر جلوی مقدارِ نامعتبر را می‌گیرد.
/// برچسبِ فارسی فقط در لایهٔ نمایش ساخته می‌شود، نه در منطق.
/// </summary>
public enum FuelType
{
    Petrol = 1,
    Diesel = 2
}

public static class FuelTypeExtensions
{
    /// <summary>برچسبِ فارسی — فقط برای نمایش، هرگز برای منطق یا ذخیره.</summary>
    public static string ToPersian(this FuelType f) => f switch
    {
        FuelType.Petrol => "پطرول",
        FuelType.Diesel => "دیزل",
        _ => string.Empty
    };

    /// <summary>
    /// خواندنِ دادهٔ نسخهٔ قدیمی. قاعده مو‌به‌مو همان چیزی است که HTML داشت:
    /// «فقط اگر صریحاً diesel بود دیزل است، وگرنه پطرول» — یعنی مقدارِ خالی،
    /// null یا ناشناخته پطرول می‌شود، دقیقاً مثل ‎(r.ftype === 'diesel')‎ .
    /// این عمداً سخت‌گیرانه نیست تا ردیف‌های قدیمی همان‌جا بمانند که بودند.
    /// </summary>
    public static FuelType FromLegacy(string? raw) =>
        (raw is "diesel" or "D" or "دیزل") ? FuelType.Diesel : FuelType.Petrol;

    public static string ToLegacy(this FuelType f) => f == FuelType.Diesel ? "diesel" : "petrol";
}
