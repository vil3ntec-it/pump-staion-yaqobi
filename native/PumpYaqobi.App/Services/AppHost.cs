using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Services.Data;
using PumpYaqobi.Services.Security;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ سرویس‌های برنامه ═══════════════════════════════════════════════════════
/// یک جای واحد برای ساختنِ سرویس‌ها. برنامه یک نمونهٔ زنده دارد و آزمون‌ها
/// می‌توانند نمونهٔ خودشان را با دیتابیسِ موقت بسازند.
/// </summary>
public sealed class AppHost
{
    public AppHost(string? dbPath = null)
    {
        Db = new PumpDbFactory(dbPath);
        Db.EnsureReady();
        Session = new UserSession();
        Permissions = new PermissionService(Session);
        Auth = new AuthService(Db, Session);
        Settings = new SettingsService(Db, Permissions);
        Debt = new DebtCalculationService(Settings);
        Safe = new SafeService();
        Exchange = new ExchangeService();
    }

    public PumpDbFactory Db { get; }
    public UserSession Session { get; }
    public PermissionService Permissions { get; }
    public AuthService Auth { get; }
    public SettingsService Settings { get; }
    public DebtCalculationService Debt { get; }
    public SafeService Safe { get; }
    public ExchangeService Exchange { get; }

    /// <summary>نمونهٔ زندهٔ برنامه.</summary>
    public static AppHost Current { get; private set; } = null!;

    public static AppHost Start(string? dbPath = null) => Current = new AppHost(dbPath);
}
