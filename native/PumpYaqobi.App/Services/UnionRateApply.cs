using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>
/// نشاندنِ نرخِ اتحادیه‌ای که از <b>بیرون</b> آمده (تلگرام) — همان دو تنظیمی که
/// کادرِ «نرخ اتحادیه»ی مفاد/ضرر می‌نویسد، و همان «📈 تاریخچهٔ نرخ».
/// ⛔ تیلی که ‎null‎ است دست نمی‌خورد («نرخ جدید پطرول ۷۹» دیزل را صفر نمی‌کند).
/// ⚠️ اجازه همان اجازهٔ کادرِ مفاد/ضرر است (‎ManageSettings‎)؛ نبودش استثنا است
/// و صدا‌زننده «ننشست» می‌گوید.
/// </summary>
public static class UnionRateApply
{
    public static async Task ApplyAsync(AppHost host, decimal? petrol, decimal? diesel)
    {
        if (petrol is { } p && p > 0)
        {
            host.Settings.Set(SettingsService.UnionRatePetrol, p);
            await host.Tools.RecordRateAsync(FuelType.Petrol, p);
        }
        if (diesel is { } d && d > 0)
        {
            host.Settings.Set(SettingsService.UnionRateDiesel, d);
            await host.Tools.RecordRateAsync(FuelType.Diesel, d);
        }
    }
}
