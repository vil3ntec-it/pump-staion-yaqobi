using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ تنظیمات ═══════════════════════════════════════════════════════════════
/// همان کلیدهای ریشهٔ <c>DB</c>ِ نسخهٔ وب که «تنظیم» بودند نه «داده»:
/// نامِ پمپ، نرخِ اتحادیه، آستانهٔ کمبودِ مخزن و مانندِ آن.
///
/// خواندن از یک کشِ درون‌حافظه‌ای است تا محاسبه‌های پرتکرار (نرخِ اتحادیه در
/// هر ردیفِ قرض‌دار) به دیتابیس نزنند؛ هر نوشتن کش را تازه می‌کند.
/// </summary>
public sealed class SettingsService : IUnionRateProvider
{
    public const string StationName = "stationName";
    public const string StationAddress = "stationAddress";
    public const string StationPhone = "stationPhone";
    public const string UnionRatePetrol = "unionRatePetrol";
    public const string UnionRateDiesel = "unionRateDiesel";
    public const string BuyPerLiterPetrol = "buyPerLiter_petrol";
    public const string BuyPerLiterDiesel = "buyPerLiter_diesel";
    public const string LowStockThreshold = "lowStockThreshold";
    public const string LowStockPhone = "lowStockPhone";
    public const string ServerUrl = "serverUrl";
    public const string SyncCode = "syncCode";

    private readonly PumpDbFactory _dbf;
    private readonly PermissionService? _perm;
    private Dictionary<string, string?>? _cache;
    private readonly object _lock = new();

    public SettingsService(PumpDbFactory dbf, PermissionService? perm = null)
    { _dbf = dbf; _perm = perm; }

    private Dictionary<string, string?> Cache
    {
        get
        {
            lock (_lock)
            {
                if (_cache is not null) return _cache;
                using var db = _dbf.Create();
                _cache = db.Settings.AsNoTracking().ToDictionary(s => s.Key, s => s.Value);
                return _cache;
            }
        }
    }

    public void Invalidate() { lock (_lock) _cache = null; }

    public string? Get(string key) => Cache.TryGetValue(key, out var v) ? v : null;

    public string GetString(string key, string fallback = "") => Get(key) ?? fallback;

    public decimal GetDecimal(string key, decimal fallback = 0m) =>
        decimal.TryParse(Get(key), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : fallback;

    public bool GetBool(string key, bool fallback = false) =>
        Get(key) is { } v ? v is "1" or "true" or "True" : fallback;

    public void Set(string key, string? value)
    {
        _perm?.Require(Permission.ManageSettings);
        using var db = _dbf.Create();
        var row = db.Settings.FirstOrDefault(s => s.Key == key);
        if (row is null) db.Settings.Add(new Setting { Key = key, Value = value });
        else row.Value = value;
        db.SaveChanges();
        Invalidate();
    }

    public void Set(string key, decimal value) =>
        Set(key, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>نرخِ اتحادیه — پایهٔ تبدیلِ «پول به لیتر» در حسابِ قرض‌داران.</summary>
    public decimal UnionRate(FuelType fuel) =>
        GetDecimal(fuel == FuelType.Diesel ? UnionRateDiesel : UnionRatePetrol);
}
