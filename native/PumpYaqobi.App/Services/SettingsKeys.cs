namespace PumpYaqobi.App.Services;

/// <summary>کلیدهای تنظیمات که رابط کاربری به آن‌ها دست می‌زند.</summary>
public static class SettingsKeys
{
    public const string StationName = PumpYaqobi.Services.Data.SettingsService.StationName;
    public const string StationAddress = PumpYaqobi.Services.Data.SettingsService.StationAddress;
    public const string StationPhone = PumpYaqobi.Services.Data.SettingsService.StationPhone;
    public const string UnionRatePetrol = PumpYaqobi.Services.Data.SettingsService.UnionRatePetrol;
    public const string UnionRateDiesel = PumpYaqobi.Services.Data.SettingsService.UnionRateDiesel;
    public const string ServerUrl = PumpYaqobi.Services.Data.SettingsService.ServerUrl;
    /// <summary>نشانیِ صفحهٔ حساب — ⚠️ با ‎ServerUrl‎ (سرورِ داده) یکی نیست.</summary>
    public const string ViewerUrl = PumpYaqobi.Services.Data.SettingsService.ViewerUrl;
    /// <summary>رمزِ سرورِ هم‌گام‌سازی — در لینکِ کیو‌آر هم می‌رود.</summary>
    public const string SyncCode = PumpYaqobi.Services.Data.SettingsService.SyncCode;
}
