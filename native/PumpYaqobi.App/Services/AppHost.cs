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
        SectionNotes = new SectionNoteService(Db, Permissions);
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
        Aging = new AgingService(Debt);
        StaffShort = new StaffShortService(Waraq);
        MonthReport = new MonthReportService();
        TankDip = new TankDipService();
        Trash = new TrashService(Db, Permissions, Session);
        Debtors = new DebtorService(Db, Permissions, Trash);
        Companies = new CompanyDataService(Db, Permissions, Trash);
        WaraqData = new WaraqDataService(Db, Permissions, Trash);
        ShiftWaraqSync = new ShiftWaraqSyncService(Db, Permissions, Waraq, Settings);
        WaraqPosting = new WaraqPostingService(Db, Permissions, Waraq);
        ParchaData = new ParchaDataService(Db, Permissions, Trash, Parcha, ShiftWaraqSync);
        StorageData = new StorageDataService(Db, Permissions, Trash, Storage, Settings, Companies);
        Amanat = new AmanatDataService(Db, Permissions, Trash, Settings);
        Invoices = new InvoiceService(Db, Permissions, Trash, Debtors);
        ParchaReceipts = new ParchaReceiptService(Db, Permissions, Trash, Debtors);
        DebtReceipts = new DebtQuickReceiptService(Db, Permissions, Trash);
        ExchangeSync = new ExchangeCompanySyncService(Db, Permissions);
        Attendance = new AttendanceDataService(Db, Permissions, Trash);
        Tools = new ToolsDataService(Db, Permissions, Trash, Aging, StaffShort, MonthReport);
        Cameras = new CameraDataService(Db, Permissions, Trash);
        Voice = new VoiceDataService(Db, Permissions);
        LegacyImport = new LegacyImportService(Db, Permissions, Settings);
        Backup = new BackupService(Db, Permissions);
        History = new HistoryService(Db, Permissions, Exchange, Retail, Company, AmanatCalc, Amanat);
        SafeLedger = new LedgerService<SafeEntry>(Db, Permissions, Trash, "safe",
            r => (r.Title ?? "") + " — " + Shamsi.Money(r.Amount));
        ExchangeLedger = new LedgerService<ExchangeRow>(Db, Permissions, Trash, "sarrafi",
            r => (r.Description ?? "") + " — " + Shamsi.Money(r.Amount));
        ExpenseLedger = new LedgerService<Expense>(Db, Permissions, Trash, "expense",
            r => (r.Title ?? "") + " — " + Shamsi.Money(r.Amount));
        // ⚠️ چکنه پشتِ اجازهٔ مدیر است: در نسخهٔ وب ‎updateChakana‎ و همهٔ
        // دکمه‌های افزودنِ ردیف/ماهش ‎requireAdmin()‎ دارند — برخلافِ مصارف و
        // گاوصندوق و صرافی که ویرایششان برای کارمند هم باز است.
        RetailLedger = new LedgerService<RetailRow>(Db, Permissions, Trash, "chakana",
            r => (r.Name ?? "") + " — " + Shamsi.Money(r.Liters) + " لیتر",
            Permission.ManagerOnly);
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

    /// <summary>صندوقِ نوت‌های هر بخش — همتای ‎.sec-note-box‎ی نسخهٔ وب.</summary>
    public SectionNoteService SectionNotes { get; }
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

    /// <summary>ردیف‌های ورق ⇐ حسابِ قرض‌دار و بخشِ مصارف.</summary>
    public WaraqPostingService WaraqPosting { get; }

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

    /// <summary>صرافی ← حسابِ شرکت — بردگیِ دالریِ هر سطر.</summary>
    public ExchangeCompanySyncService ExchangeSync { get; }
    public AttendanceService AttendanceCalc { get; }
    public AttendanceDataService Attendance { get; }

    /// <summary>قرض‌های کهنه — «چند روز است هیچ ردیفی ندارد».</summary>
    public AgingService Aging { get; }

    /// <summary>کمبودی/اضافیِ کارمندان — از خودِ ورق‌ها، نه از دفترِ جدا.</summary>
    public StaffShortService StaffShort { get; }

    /// <summary>گزارشِ پایانِ ماه — فقط جمعِ ثبت‌های موجود.</summary>
    public MonthReportService MonthReport { get; }

    /// <summary>میله‌زنیِ مخزن و کم‌آمدِ تانکر.</summary>
    public TankDipService TankDip { get; }

    /// <summary>خوراکِ همهٔ ابزارهای بالا و دو دفترِ کوچکشان.</summary>
    public ToolsDataService Tools { get; }

    /// <summary>دفترِ دوربین‌های مداربسته — افزودن و حذفش پشتِ اجازهٔ مدیر.</summary>
    public CameraDataService Cameras { get; }

    /// <summary>صداهای ثبت‌شدهٔ جستجوی صوتی — همه روی خودِ دستگاه.</summary>
    public VoiceDataService Voice { get; }

    /// <summary>آوردنِ دادهٔ نسخهٔ وب — یک‌بار، با بکاپ و سنجشِ شمارش.</summary>
    public LegacyImportService LegacyImport { get; }

    /// <summary>بکاپ، عکسِ روزانه و بازگردانی — بندِ ۲۳.</summary>
    public BackupService Backup { get; }

    /// <summary>تاریخچهٔ هر بخش — فقط خواندنی.</summary>
    public HistoryService History { get; }
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
    ///
    /// <para>⚠️ قفل، برای ابزارها و آزمون‌هایی که ممکن است هم‌زمان صدایش
    /// بزنند — بی آن، دو نخ هر دو ‎Current‎ را خالی می‌بینند و دو میزبان
    /// ساخته می‌شود.</para>
    /// </summary>
    public static AppHost Start(string? dbPath = null)
    {
        lock (StartLock) return Current ??= new AppHost(dbPath);
    }

    private static readonly object StartLock = new();
}
