using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
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
        // دیتابیسِ غیرپیش‌فرض یعنی «اجرای جدا» (عکس‌گیری/آزمون) — تنظیمات هم
        // باید کنارِ همان باشد، نه در پوشهٔ واقعیِ کاربر.
        if (dbPath is not null) AppSettings.DirOverride = Path.GetDirectoryName(dbPath);
        Db = new PumpDbFactory(dbPath);
        Db.EnsureReady();
        Toasts = new ToastService();
        Session = new UserSession();
        Permissions = new PermissionService(Session);
        Auth = new AuthService(Db, Session);
        Settings = new SettingsService(Db, Permissions);
        Debt = new DebtCalculationService(Settings);
        Safe = new SafeService();
        Exchange = new ExchangeService();
        Retail = new RetailService();
        Expenses = new ExpenseService();
        Company = new CompanyService();
        Parcha = new ParchaService();
        Waraq = new WaraqService();
        Storage = new StorageService();
        AmanatCalc = new AmanatService();
        AttendanceCalc = new AttendanceService();
        Trash = new TrashService(Db, Permissions, Session);
        Debtors = new DebtorService(Db, Permissions, Trash);
        Companies = new CompanyDataService(Db, Permissions, Trash);
        WaraqData = new WaraqDataService(Db, Permissions, Trash);
        ShiftWaraqSync = new ShiftWaraqSyncService(Db, Permissions, Waraq, Settings);
        ParchaData = new ParchaDataService(Db, Permissions, Trash, Parcha, ShiftWaraqSync);
        StorageData = new StorageDataService(Db, Permissions, Trash, Storage, Settings, Companies);
        Amanat = new AmanatDataService(Db, Permissions, Trash, Settings);
        Invoices = new InvoiceService(Db, Permissions, Trash, Debtors);
        ParchaReceipts = new ParchaReceiptService(Db, Permissions, Trash, Debtors);
        DebtReceipts = new DebtQuickReceiptService(Db, Permissions, Trash);
        Attendance = new AttendanceDataService(Db, Permissions, Trash);
        SafeLedger = new LedgerService<SafeEntry>(Db, Permissions, Trash, "safe",
            r => (r.Title ?? "") + " — " + Shamsi.Money(r.Amount));
        ExchangeLedger = new LedgerService<ExchangeRow>(Db, Permissions, Trash, "sarrafi",
            r => (r.Description ?? "") + " — " + Shamsi.Money(r.Amount));
        ExpenseLedger = new LedgerService<Expense>(Db, Permissions, Trash, "expense",
            r => (r.Title ?? "") + " — " + Shamsi.Money(r.Amount));
        RetailLedger = new LedgerService<RetailRow>(Db, Permissions, Trash, "chakana",
            r => (r.Name ?? "") + " — " + Shamsi.Money(r.Liters) + " لیتر");
        ExtraIncomeLedger = new LedgerService<ExtraIncome>(Db, Permissions, Trash, "extraincome",
            r => (r.Seller ?? "") + " — " + Shamsi.Money(r.Amount));
    }

    public PumpDbFactory Db { get; }
    public ToastService Toasts { get; }

    /// <summary>پیامِ کوتاهِ پایینِ صفحه — همان showToastِ نسخهٔ وب.</summary>
    public void Toast(string text, ToastKind kind = ToastKind.Info) => Toasts.Show(text, kind);
    public UserSession Session { get; }
    public PermissionService Permissions { get; }
    public AuthService Auth { get; }
    public SettingsService Settings { get; }
    public DebtCalculationService Debt { get; }
    public SafeService Safe { get; }
    public ExchangeService Exchange { get; }
    public RetailService Retail { get; }
    public ExpenseService Expenses { get; }
    public TrashService Trash { get; }
    public DebtorService Debtors { get; }
    public CompanyDataService Companies { get; }
    public CompanyService Company { get; }
    public ParchaService Parcha { get; }
    public ParchaDataService ParchaData { get; }
    public WaraqService Waraq { get; }
    public WaraqDataService WaraqData { get; }

    /// <summary>پارچه ← ورق ← گاوصندوق — همان زنجیرهٔ خودکارِ نسخهٔ وب.</summary>
    public ShiftWaraqSyncService ShiftWaraqSync { get; }
    public StorageService Storage { get; }
    public StorageDataService StorageData { get; }
    public AmanatService AmanatCalc { get; }
    public AmanatDataService Amanat { get; }
    public InvoiceService Invoices { get; }
    public ParchaReceiptService ParchaReceipts { get; }

    /// <summary>«رسید قرض‌داران» — پرداختِ نقدیِ مستقیم به حساب.</summary>
    public DebtQuickReceiptService DebtReceipts { get; }
    public AttendanceService AttendanceCalc { get; }
    public AttendanceDataService Attendance { get; }
    public LedgerService<SafeEntry> SafeLedger { get; }
    public LedgerService<ExchangeRow> ExchangeLedger { get; }
    public LedgerService<Expense> ExpenseLedger { get; }
    public LedgerService<RetailRow> RetailLedger { get; }
    public LedgerService<ExtraIncome> ExtraIncomeLedger { get; }

    /// <summary>نمونهٔ زندهٔ برنامه.</summary>
    public static AppHost Current { get; private set; } = null!;

    /// <summary>
    /// اگر از پیش ساخته شده باشد همان می‌ماند — تا ابزارِ عکس‌گیری بتواند
    /// پیش از بالا آمدنِ برنامه دیتابیسِ موقتِ خودش را بنشاند و هرگز به
    /// دادهٔ واقعیِ کاربر دست نزند.
    /// </summary>
    public static AppHost Start(string? dbPath = null) => Current ??= new AppHost(dbPath);
}
