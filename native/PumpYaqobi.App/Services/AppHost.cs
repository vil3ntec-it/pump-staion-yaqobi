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
        //  ⛔ **دفترِ ریشه همانی است که برنامه با آن بالا آمد** — نه
        //  `PumpDbFactory.DefaultPath`. سنجه‌ها و ابزارِ عکس‌گیری دیتابیسِ
        //  موقتِ خودشان را می‌دهند؛ با مسیرِ پیش‌فرض، نخستین «حساب عوض شد»
        //  آن‌ها را به پوشهٔ **واقعیِ** کاربر می‌برد و روی دفترِ خودِ صاحب
        //  پمپ می‌نوشتند. پوشهٔ `accounts/` هم کنارِ همین می‌نشیند.
        _rootDb = Db.DbPath;
        Db.EnsureReady();
        Toasts = new ToastService();
        Session = new UserSession();
        Permissions = new PermissionService(Session);
        Auth = new AuthService(Db, Session);
        Settings = new SettingsService(Db, Permissions);
        Locks = new SectionLockService(Settings);
        SectionNotes = new SectionNoteService(Db, Permissions);
        Debt = new DebtCalculationService(Settings);
        Safe = new SafeService();
        Exchange = new ExchangeService();
        Retail = new RetailService();
        Expenses = new ExpenseService();
        Company = new CompanyService();
        CompanyPurchases = new CompanyPurchaseService(Company);
        Parcha = new ParchaService();
        Waraq = new WaraqService();
        Storage = new StorageService();
        AmanatCalc = new AmanatService();
        AttendanceCalc = new AttendanceService();
        Aging = new AgingService(Debt);
        Membership = new MembershipService();
        DebtSummary = new DebtSummaryService(Aging, Membership);
        StaffShort = new StaffShortService(Waraq);
        MonthReport = new MonthReportService();
        TankDip = new TankDipService();
        Trash = new TrashService(Db, Permissions, Session);
        Debtors = new DebtorService(Db, Permissions, Trash);
        Companies = new CompanyDataService(Db, Permissions, Trash);
        WaraqData = new WaraqDataService(Db, Permissions, Trash);
        ShiftWaraqSync = new ShiftWaraqSyncService(Db, Permissions, Waraq, Settings);
        WaraqPosting = new WaraqPostingService(Db, Permissions, Waraq, ShiftWaraqSync);
        ParchaData = new ParchaDataService(Db, Permissions, Trash, Parcha, ShiftWaraqSync);
        StorageData = new StorageDataService(Db, Permissions, Trash, Storage, Settings, Companies);
        Amanat = new AmanatDataService(Db, Permissions, Trash, Settings);
        Invoices = new InvoiceService(Db, Permissions, Trash, Debtors);
        ParchaReceipts = new ParchaReceiptService(Db, Permissions, Trash, Debtors);
        DebtReceipts = new DebtQuickReceiptService(Db, Permissions, Trash);
        ExchangeSync = new ExchangeCompanySyncService(Db, Permissions);
        Attendance = new AttendanceDataService(Db, Permissions, Trash);
        Tools = new ToolsDataService(Db, Permissions, Trash, Aging, StaffShort, MonthReport,
                                     Membership, DebtSummary);
        Cameras = new CameraDataService(Db, Permissions, Trash);
        Voice = new VoiceDataService(Db, Permissions);
        LegacyImport = new LegacyImportService(Db, Permissions, Settings);
        Backup = new BackupService(Db, Permissions);
        History = new HistoryService(Db, Permissions, Exchange, Retail, Company, AmanatCalc, Amanat, Waraq);
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

    private StationPublisher? _publisher;

    /// <summary>
    /// ══ پلِ زندهٔ برنامه به گوشی‌ها ═══════════════════════════════════════
    ///
    /// عکسِ برنامه را روی سرورِ خانگی تازه نگه می‌دارد تا اپِ کارمندان و ربات
    /// همیشه همان چیزی را ببینند که روی این کامپیوتر است.
    ///
    /// ⚠️ تنبل ساخته می‌شود، نه در سازنده: خودش <c>AppHost</c> می‌خواهد و در
    /// سازنده هنوز چیزی برای دادن نیست. تا کسی صدایش نزند، هیچ نخی هم
    /// نمی‌سازد — پس آزمون‌ها و ابزارِ عکس‌گیری اصلاً با آن کاری ندارند.
    /// </summary>
    public StationPublisher Publisher => _publisher ??= new StationPublisher(
        this, new HomeSync(() => HomeLink.Config(this)), () => HomeLink.StationCode(this));

    /// <summary>
    /// پشتیبانِ هر شش ساعت روی سرورِ خانگی — شرحش در <see cref="BackupPusher"/>.
    /// تنبل ساخته می‌شود، مثلِ ناشر.
    /// </summary>
    public BackupPusher BackupToServer => _backupPusher ??= new BackupPusher(this);

    private BackupPusher? _backupPusher;

    /// <summary>
    /// همان ناشر، ولی **بی ساختن**. چراغِ سرورِ سربرگ هر ثانیه از این می‌پرسد و
    /// نباید در آزمون‌ها و عکس‌گیری‌ها نخِ ناشر را بیدار کند.
    /// </summary>
    public StationPublisher? PublisherIfStarted => _publisher;

    private SyncEngine? _sync;

    /// <summary>
    /// ══ همگام‌سازی با سرورِ حساب (VILL3N Sync v1) ════════════════════════
    ///
    /// شرحش در <see cref="SyncEngine"/> و <c>native/docs/SYNC-fa.md</c>.
    /// تنبل ساخته می‌شود، مثلِ ناشر: تا کسی صدایش نزند هیچ نخی هم نمی‌سازد.
    /// </summary>
    public SyncEngine Sync => _sync ??= new SyncEngine(this);

    /// <summary>همان موتور، ولی **بی ساختن** — برای چراغِ نوارِ پایین.</summary>
    public SyncEngine? SyncIfStarted => _sync;


    // ══ هر حساب، دفترِ خودش ═════════════════════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «حسابِ اول با حسابِ دوم عوض
    //  بشه، اطلاعات دست نخوره، توی حساب‌ها بمونن، حساب‌ها عوض می‌شه و
    //  اطلاعاتِ همون حساب نشون داده بشه — مثلِ برنامه‌های حرفه‌ای.»
    //
    //  ⛔ این **تنها جای تصمیم** است. مسیر را `AccountLedger` می‌گوید،
    //  جابه‌جایی را `PumpDbFactory.SwitchTo` انجام می‌دهد، و پوسته فقط
    //  `LedgerSwitched` را می‌شنود و صفحه‌ها را از نو می‌خواند. سه جای
    //  تصمیم یعنی روزی دفتر عوض می‌شود و صفحه عددِ دفترِ قبلی را نشان
    //  می‌دهد.

    /// <summary>
    /// دفتری که همین حالا باز است مالِ کدام حساب است — خالی یعنی «هنوز
    /// حسابی نیست» یا «دفترِ ریشه، بی‌صاحب».
    /// </summary>
    public string LedgerAccountId { get; private set; } = "";

    /// <summary>
    /// دفتر عوض شد. پوسته با این خبر همهٔ بخش‌ها را «کهنه» می‌کند، بخشِ
    /// جلوی چشم را از نو می‌خواند و قفلِ همان دفترِ تازه را می‌پرسد.
    /// </summary>
    public event Action? LedgerSwitched;

    private readonly object _ledgerGate = new();

    /// <summary>دفتری که برنامه با آن بالا آمد — پوشهٔ حساب‌ها کنارِ همین است.</summary>
    private readonly string _rootDb;

    /// <summary>
    /// ══ دفترِ این حساب را باز کن ═══════════════════════════════════════════
    ///
    /// بی‌ضرر و تکرارپذیر: تا وقتی حساب عوض نشده باشد، فقط دو رشته را
    /// مقایسه می‌کند و برمی‌گردد — نه فایلی می‌خواند، نه دستوری به دیتابیس
    /// می‌زند. پس صدا زدنش از هر جایی ارزان است.
    ///
    /// ⛔ <b>هیچ داده‌ای پاک نمی‌شود.</b> دفترِ حسابِ قبلی سرِ جایش می‌ماند و
    /// با برگشتنِ همان حساب، دست‌نخورده برمی‌گردد.
    ///
    /// ⚠️ <b>موتورِ همگام‌سازی وسطِ کار نگه داشته می‌شود</b> و این لازم است،
    /// نه تجمل: آن حلقه روی نخِ دیگری می‌دود و اگر درست وسطِ جابه‌جایی
    /// opهای دفترِ حسابِ <b>قبلی</b> را برداشته باشد، آن‌ها را با توکنِ
    /// حسابِ <b>تازه</b> می‌فرستد — یعنی دادهٔ یک مشتری در دفترِ ابریِ
    /// مشتریِ دیگر. این کندی نیست، خرابیِ داده است.
    /// </summary>
    /// <param name="accountId">شناسهٔ حسابِ واردشده؛ خالی یعنی بی‌حساب.</param>
    /// <returns><c>true</c> یعنی واقعاً دفتر عوض شد.</returns>
    public bool UseLedgerOf(string? accountId)
    {
        var id = (accountId ?? "").Trim();
        lock (_ledgerGate)
        {
            if (string.Equals(id, LedgerAccountId, StringComparison.Ordinal)) return false;

            //  ⛔ **خروج از حساب دفتر را عوض نمی‌کند.** شناسهٔ خالی یعنی
            //  «نمی‌دانیم کیست»، نه «برگرد به دفترِ ریشه» — و دفترِ ریشه
            //  مالِ حسابِ **دیگری** است. بی این خط، خروجِ حسابِ دوم دفترِ
            //  حسابِ اول را جلوی چشمش می‌گذاشت.
            if (id.Length == 0 && LedgerAccountId.Length > 0) return false;

            var file = AppSettings.Load();

            //  نخستین حساب، دفترِ موجود را برمی‌دارد — همان چیزی که دادهٔ
            //  «بی‌حساب» را به حسابِ تازه می‌رساند (قاعدهٔ ۱۴۰۵/۰۷/۰۷).
            //  ⚠️ `Save()`ی بادوام، نه `SaveSoon()`: بالای خودِ خاصیت نوشته چرا.
            if (AccountLedger.ShouldClaimRoot(id, file.LedgerAccountId))
            {
                file.LedgerAccountId = id;
                try { file.Save(); } catch { /* نشد ⇒ دورِ بعد دوباره */ }
            }

            var want = AccountLedger.PathFor(_rootDb, id, file.LedgerAccountId);

            //  همان فایل است (حسابِ صاحبِ ریشه، یا هنوز بی‌حساب) ⇒ فقط نامش
            //  را می‌نویسیم و هیچ چیزی از نو خوانده نمی‌شود.
            if (string.Equals(Path.GetFullPath(want), Path.GetFullPath(Db.DbPath),
                              StringComparison.OrdinalIgnoreCase))
            {
                LedgerAccountId = id;
                return false;
            }

            var sync = _sync;
            try { sync?.Hold(true); } catch { /* رفاه */ }
            try
            {
                if (!Db.SwitchTo(want)) { LedgerAccountId = id; return false; }
                LedgerAccountId = id;
            }
            finally { try { sync?.Hold(false); } catch { /* رفاه */ } }
        }

        //  دفترِ دیگر ⇒ ‎Ctrl+Z‎ نباید قلمِ سطلِ دفترِ قبلی را برگرداند
        UndoHub.Clear();
        LedgerSwitched?.Invoke();
        return true;
    }

    /// <summary>پیامِ کوتاهِ پایینِ صفحه — همان showToastِ نسخهٔ وب.</summary>
    public void Toast(string text, ToastKind kind = ToastKind.Info) => Toasts.Show(text, kind);
    public UserSession Session { get; }
    public PermissionService Permissions { get; }
    public AuthService Auth { get; }
    public SettingsService Settings { get; }

    /// <summary>رمزِ بخش‌های «مفاد» و «ضرر» — شرحش در <see cref="SectionLockService"/>.</summary>
    public SectionLockService Locks { get; }

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
    /// <summary>خریدهای مخزنِ هر شرکت و «جستجوی خرید» — فقط‌خواندنی.</summary>
    public CompanyPurchaseService CompanyPurchases { get; }
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

    /// <summary>
    /// «تاریخچهٔ همین بخش» — همان ‎openSectionHistory(kind)‎ی سایت. ویومدلِ اصلی
    /// آن را می‌نشاند؛ بخش‌ها فقط کلیدِ خودشان را می‌دهند (‎HistoryService.Kinds‎).
    /// </summary>
    public Func<string, Task>? OpenHistory { get; set; }

    /// <summary>
    /// «برگرد به صفحهٔ اول» — همان کاری که دکمهٔ برگشتِ صفحهٔ ورود می‌کند.
    ///
    /// ویومدلِ اصلی آن را می‌نشاند؛ بخش‌ها فقط صدایش می‌زنند. کسی که
    /// نمی‌خواهد حساب بسازد باید بتواند برگردد سرِ دفترِ خودش.
    /// </summary>
    public Func<Task>? GoHome { get; set; }

    /// <summary>
    /// «برگرد به همان بخشی که از آن آمدم» — با شناسهٔ بخش.
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «تاریخچه‌ها دکمهٔ برگشت به همان بخشی که
    /// از آن رفتم ندارد و می‌رود توی بخشِ تاریخچه‌ها.» ⛔ راهِ رفت
    /// (<see cref="OpenHistory"/>) بود و راهِ برگشت نبود.
    ///
    /// ⚠️ <b>قفلِ پلن و قفلِ رمزِ بخش را دور نمی‌زند</b>: ویومدلِ اصلی این را
    /// به همان <c>GoAsync</c>ی همیشگی می‌بندد، نه به یک مسیرِ دوم.
    /// </summary>
    public Func<string, Task>? GoSection { get; set; }

    /// <summary>
    /// بخشِ زندهٔ یک شناسه — برای «این حساب را باز کن» از بخشِ دیگر (چت ⇒
    /// قرض‌داران). ⚠️ فقط پیدا می‌کند؛ رفتن به آن همچنان از <see cref="GoSection"/>
    /// است تا قفلِ پلن و رمزِ بخش دور زده نشود.
    /// </summary>
    public Func<string, object?>? FindSection { get; set; }

    /// <summary>قرض‌های کهنه — «چند روز است هیچ ردیفی ندارد».</summary>
    public AgingService Aging { get; }

    /// <summary>مدتِ عضویت — «از کِی مشتریِ ما است».</summary>
    public MembershipService Membership { get; }

    /// <summary>قرض‌های دسته‌جمعی — هر قرض‌دار یک خط.</summary>
    public DebtSummaryService DebtSummary { get; }

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
        lock (StartLock)
        {
            if (Current is not null) return Current;
            Current = new AppHost(dbPath);
        }

        //  ══ دفترِ همان حسابی که آخرین بار وارد شده بود ════════════════════
        //
        //  ⛔ **پیش از هر خواندنی** — قفلِ برنامه، بخشِ اول و نوار همه از
        //  دفتر می‌خوانند، و اگر این‌جا نباشد نخستین فریمِ برنامه دفترِ
        //  حسابِ **قبلی** را نشان می‌دهد و یک لحظه بعد عوض می‌شود.
        //
        //  ⚠️ و هیچ شنونده‌ای هنوز نیست، پس `LedgerSwitched` به هوا می‌رود
        //  و هیچ چیزی دو بار خوانده نمی‌شود — عمدی است.
        //
        //  ⛔ **با مسیرِ صریح هیچ کاری نمی‌کند**: سنجه‌ها و ابزارِ عکس‌گیری
        //  دیتابیسِ موقتِ خودشان را می‌دهند و نباید به پوشهٔ حسابِ واقعیِ
        //  کاربر بروند.
        if (dbPath is null)
        {
            try { Current.UseLedgerOf(AppSettings.Load().CloudUserId); }
            catch { /* دفترِ ریشه سرِ جایش است؛ برنامه باید بالا بیاید */ }

            //  ══ اثرِ انگشتِ همین کامپیوتر — بارِ اول، برای نصب‌های امروزی ═══
            //  ⚠️ TOFU: نصبی که پیش از ۱۴۰۵/۰۷/۱۲ فعال شده این را ندارد و
            //  همین‌جا ثبتش می‌کند، پس مجوزش سالم می‌ماند؛ از آن به بعد کپیِ
            //  همین تنظیمات روی کامپیوترِ دیگر پذیرفته نمی‌شود
            //  (`CloudConfig.MachineMoved`). فقط نصبِ فعال‌شده — نصبِ تازه
            //  همان لحظهٔ فعال شدن ثبتش می‌کند.
            try
            {
                var f = AppSettings.Load();
                if (f.CloudDeviceToken.Length > 0 && CloudConfig.RecordMachine(f)) f.Save();
            }
            catch { /* ثبت نشدنش هیچ‌وقت نباید جلوی بالا آمدن را بگیرد */ }
        }

        return Current;
    }

    private static readonly object StartLock = new();
}
