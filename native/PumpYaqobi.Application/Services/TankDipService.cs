using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>مشخصاتِ مخزنِ یک سوخت — همان چیزی که ‎__tankInfo(f)‎ برمی‌گرداند.</summary>
public readonly record struct TankInfo(
    FuelType Fuel, string Label, string Icon,
    decimal Book, decimal Capacity, bool CapacityDefined, decimal Threshold);

/// <summary>نتیجهٔ یک میله‌زنی — همان بستهٔ ‎TankDip.calc(f, actual)‎.</summary>
/// <param name="Receivable">چقدر جا برای تانکر هست — در نسخهٔ وب همان «فضای خالی» است.</param>
/// <param name="Over">اختلاف از حدِ مجاز گذشته: احتمالِ نشتی، دزدی یا خطای ثبت.</param>
public readonly record struct DipNumbers(
    TankInfo Tank, decimal Measured, decimal Percent, decimal Empty,
    decimal Receivable, decimal Diff, decimal Allowed, bool Over);

/// <summary>
/// ══ میله‌زنیِ مخزن و تخلیهٔ تانکر ═══════════════════════════════════════════
/// رونوشتِ ماژولِ ‎TankDip‎ و ‎confirmTankerLog‎ی نسخهٔ وب.
///
///     پر بودن = واقعی ÷ ظرفیت × ۱۰۰   (سقفِ ۱۰۰، کفِ صفر)
///     فضای خالی = ظرفیت − واقعی        (منفی نمی‌شود)
///     اختلاف   = گردِ (واقعی − دفتری)
///     حد مجاز  = بیشترِ ۵۰ و گردِ (ظرفیت × ۰٫۵ ÷ ۱۰۰)
///
/// ⚠️ ظرفیتِ صفر (یعنی «هنوز در بخش مخزن‌ها ننوشته‌اند») همه‌جا صفر می‌دهد و
/// حدِ مجاز روی همان کفِ ۵۰ لیتر می‌ماند — نه تقسیم بر صفر، نه هشدارِ بی‌جا.
/// </summary>
public sealed class TankDipService
{
    /// <summary>حد مجاز اختلاف با دفتر: نیم درصدِ ظرفیت (کفِ ۵۰ لیتر).</summary>
    public const decimal AllowedDiffPercent = 0.5m;

    /// <summary>
    /// گردکردنِ ‎Math.round‎ِ جاوااسکریپت — نصفه‌ها به بالا، حتی برای عددِ منفی
    /// (‎Math.round(-2.5) === -2‎).
    ///
    /// ⚠️ با <c>MidpointRounding.AwayFromZero</c> عوض نکنید: آن رونوشتِ
    /// ‎toFixed‎ است (‎_round0‎)، نه ‎Math.round‎، و در اختلافِ منفیِ میله‌زنی
    /// یک لیتر فرق می‌کند.
    /// </summary>
    public static decimal JsRound(decimal v) => Math.Floor(v + 0.5m);

    /// <summary>
    /// ‎defaultCapacity(current, th)‎ — وقتی کاربر ظرفیت را ننوشته باشد،
    /// عددی که نوارِ پرشدگی را بی‌معنا نکند: به بالا تا هزارِ بعدی.
    /// </summary>
    public static decimal DefaultCapacity(decimal current, decimal threshold)
    {
        var b = Math.Max(Math.Max(current, threshold * 4m), 1000m);
        return Math.Ceiling(b / 1000m) * 1000m;
    }

    /// <summary>‎__tankInfo(f)‎ — ظرفیتِ نوشته‌شده، وگرنه ظرفیتِ پیش‌فرض.</summary>
    public TankInfo Info(FuelType fuel, decimal book, decimal capacitySetting, decimal threshold)
    {
        var defined = capacitySetting > 0m;
        var cap = defined ? capacitySetting : DefaultCapacity(book, threshold);
        return new TankInfo(fuel,
            fuel == FuelType.Diesel ? "دیزل" : "پطرول",
            fuel == FuelType.Diesel ? "🟤" : "⛽",
            book, cap, defined, threshold);
    }

    /// <summary>‎TankDip.calc(f, actual)‎.</summary>
    public DipNumbers Calc(TankInfo t, decimal measured)
    {
        var cap = t.Capacity;
        var pct = cap > 0m ? Math.Max(0m, Math.Min(100m, measured / cap * 100m)) : 0m;
        var empty = cap > 0m ? Math.Max(0m, cap - measured) : 0m;
        var diff = JsRound(measured - t.Book);
        var allowed = Math.Max(50m, JsRound(cap * AllowedDiffPercent / 100m));
        return new DipNumbers(t, measured, pct, empty, empty, diff, allowed,
                              Math.Abs(diff) > allowed);
    }

    /// <summary>
    /// کم‌آمدِ یک تخلیهٔ تانکر: تحویل منهای بارنامه. منفی یعنی کم آورده.
    /// همان ‎d = act − man‎ی ‎confirmTankerLog‎.
    /// </summary>
    public static decimal UnloadDifference(TankerUnload u) =>
        u is null ? 0m : u.Actual - u.Manifest;
}
