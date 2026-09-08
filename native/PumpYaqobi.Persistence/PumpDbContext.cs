using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Persistence;

/// <summary>
/// ══ دیتابیسِ برنامه ═══════════════════════════════════════════════════════
/// بندِ ۵: localStorage/IndexedDB نسخهٔ HTML جای خود را به SQLiteِ واقعی می‌دهد.
///
/// قاعده‌هایی که این‌جا اعمال می‌شوند و در همه‌جا یکسان‌اند:
///   • هر رکورد کلید، CreatedAt، UpdatedAt و DeletedAt (حذفِ نرم) دارد.
///   • هیچ رکوردِ مالی واقعاً پاک نمی‌شود — فقط DeletedAt می‌گیرد و از همهٔ
///     Queryها با فیلترِ سراسری بیرون می‌ماند.
///   • ایندکس روی همان چیزهایی که بندِ ۵ خواسته: تاریخ، نام، شمارهٔ حساب،
///     شمارهٔ فاکتور، نوع سوخت، وضعیتِ بدهی و کلیدِ ماه (برای فیلترِ ماهانه).
///   • تاریخِ شمسی هم به‌صورت رشته (همان‌طور که کاربر می‌نویسد) و هم به‌صورت
///     DateKey عددی نگه داشته می‌شود؛ مرتب‌سازی و ایندکس روی عدد است، چون
///     مقایسهٔ رشته‌ای روزهای تک‌رقمی را غلط می‌چیند (باگی که در HTML بود).
/// </summary>
public sealed class PumpDbContext : DbContext
{
    public PumpDbContext(DbContextOptions<PumpDbContext> options) : base(options) { }

    public DbSet<Debtor> Debtors => Set<Debtor>();
    public DbSet<DebtAccount> DebtAccounts => Set<DebtAccount>();
    public DbSet<DebtRow> DebtRows => Set<DebtRow>();
    public DbSet<DebtTableArchive> DebtTableArchives => Set<DebtTableArchive>();
    public DbSet<SafeEntry> SafeEntries => Set<SafeEntry>();
    public DbSet<ExchangeRow> ExchangeRows => Set<ExchangeRow>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<RetailRow> RetailRows => Set<RetailRow>();
    public DbSet<ShiftData> ShiftDataSet => Set<ShiftData>();
    public DbSet<ParchaReport> Reports => Set<ParchaReport>();
    public DbSet<FuelPurchase> FuelPurchases => Set<FuelPurchase>();
    public DbSet<TankDip> TankDips => Set<TankDip>();
    public DbSet<TankerUnload> TankerUnloads => Set<TankerUnload>();
    public DbSet<TilCompany> TilCompanies => Set<TilCompany>();
    public DbSet<CompanyRow> CompanyRows => Set<CompanyRow>();
    public DbSet<AmanatAccount> AmanatAccounts => Set<AmanatAccount>();
    public DbSet<AmanatRow> AmanatRows => Set<AmanatRow>();
    public DbSet<WaraqEntry> WaraqEntries => Set<WaraqEntry>();
    /// <summary>صفِ «رسید پارچه‌ها» — ردیف‌هایی که هنوز واردِ حسابِ کسی نشده‌اند.</summary>
    public DbSet<ParchaReceipt> ParchaReceipts => Set<ParchaReceipt>();
    /// <summary>«رسید قرض‌داران» — پرداختِ نقدیِ مستقیم به حساب.</summary>
    public DbSet<DebtQuickReceipt> DebtQuickReceipts => Set<DebtQuickReceipt>();
    public DbSet<WaraqShift> WaraqShifts => Set<WaraqShift>();
    public DbSet<WaraqPump> WaraqPumps => Set<WaraqPump>();
    public DbSet<WaraqTransaction> WaraqTransactions => Set<WaraqTransaction>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<AttendanceRow> Attendance => Set<AttendanceRow>();
    public DbSet<SalaryPayment> SalaryPayments => Set<SalaryPayment>();
    public DbSet<StaffShortage> StaffShortages => Set<StaffShortage>();
    public DbSet<StaffShortSettle> StaffShortSettles => Set<StaffShortSettle>();
    public DbSet<VoiceTemplate> VoiceTemplates => Set<VoiceTemplate>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<ExtraIncome> ExtraIncomes => Set<ExtraIncome>();
    public DbSet<RateHistoryEntry> RateHistory => Set<RateHistoryEntry>();
    public DbSet<TrashItem> Trash => Set<TrashItem>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    /// <summary>یادداشت‌های هر بخش — همتای «صندوق نوت‌ها»ی نسخهٔ وب.</summary>
    public DbSet<SectionNote> SectionNotes => Set<SectionNote>();
    public DbSet<SectionNoteDraft> SectionNoteDrafts => Set<SectionNoteDraft>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Debtor>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.HasIndex(x => x.Name);                 // جست‌وجو بر اساس نام
            e.HasIndex(x => x.LegacyId).IsUnique();  // برای مهاجرت و کیو‌آرهای قدیمی
            e.HasIndex(x => x.IsNoInvoice);
            e.HasOne(x => x.MainAccount).WithOne().HasForeignKey<DebtAccount>(x => x.MainOfDebtorId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.SubAccounts).WithOne().HasForeignKey(x => x.DebtorId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<DebtAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.IsMain);
            e.HasIndex(x => x.DebtorId);
            e.HasIndex(x => x.LegacySubId);
            e.Property(x => x.Mode).HasConversion<int>();
            // مبالغ: دقتِ ثابت، نه شناور — پول هرگز با double نگه داشته نمی‌شود
            foreach (var p in new[] { nameof(DebtAccount.PercentPetrol), nameof(DebtAccount.PercentDiesel),
                                      nameof(DebtAccount.PercentLegacy), nameof(DebtAccount.RasidFuelPetrol),
                                      nameof(DebtAccount.RasidFuelDiesel), nameof(DebtAccount.RasidMoneyPetrol),
                                      nameof(DebtAccount.RasidMoneyDiesel) })
                e.Property(p).HasColumnType("TEXT");
            e.HasMany(x => x.FuelRows).WithOne().HasForeignKey(x => x.FuelAccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.MoneyRows).WithOne().HasForeignKey(x => x.MoneyAccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<ParchaReceipt>(e =>
        {
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.Account);
            // بدونِ این، رسیدِ ثبت‌شده (که فقط حذفِ نرم شده) در صف می‌ماند
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<DebtRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FuelAccountId);
            e.HasIndex(x => x.MoneyAccountId);
            e.HasIndex(x => x.DateKey);                            // فیلتر و مرتب‌سازیِ تاریخ
            e.HasIndex(x => new { x.FuelAccountId, x.SortIndex }); // ترتیبِ ردیف‌های یک حساب
            e.HasIndex(x => x.Fuel);                            // تفکیکِ پطرول/دیزل
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.InvoiceId);
            e.HasIndex(x => x.SrcKey);      // یافتنِ ردیفِ هم‌منبع هنگامِ ثبتِ دوباره
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // جدول‌های آرشیوِ حساب — عکسِ گذشته، بی هیچ کلیدِ خارجی به ردیف‌های زنده
        b.Entity<DebtTableArchive>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AccountId);
            foreach (var p in new[] { nameof(DebtTableArchive.PercentPetrol), nameof(DebtTableArchive.PercentDiesel),
                                      nameof(DebtTableArchive.RasidFuelPetrol), nameof(DebtTableArchive.RasidFuelDiesel),
                                      nameof(DebtTableArchive.RasidMoneyPetrol), nameof(DebtTableArchive.RasidMoneyDiesel) })
                e.Property(p).HasColumnType("TEXT");
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<DebtQuickReceipt>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.MonthKey);      // منویِ ماه
            e.HasIndex(x => x.LegacyId).IsUnique();
            e.Ignore(x => x.SrcKey);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<SafeEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.MonthKey);      // صفحه ماه‌به‌ماه فیلتر می‌شود
            e.HasIndex(x => x.Kind);
            e.HasIndex(x => x.SrcKey);        // ردیفِ خودکارِ «فروشِ ورق»
            e.Property(x => x.Kind).HasConversion<int>();
            e.Property(x => x.Currency).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<ExchangeRow>(e =>
        {
            e.HasIndex(x => x.LegacyId);       // پیوند با ردیفِ حسابِ شرکت
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.MonthKey);
            e.Property(x => x.Currency).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<Expense>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.MonthKey);
            e.HasIndex(x => x.Title);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<RetailRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.MonthKey);
            e.HasIndex(x => x.Fuel);
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ── پارچه‌ها ──────────────────────────────────────────────────────
        b.Entity<ShiftData>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<ParchaReport>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.Fuel);
            e.HasIndex(x => new { x.Fuel, x.DateKey });
            e.HasIndex(x => x.LegacyId);
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasOne(x => x.DayShift).WithMany().HasForeignKey(x => x.DayShiftId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.NightShift).WithMany().HasForeignKey(x => x.NightShiftId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ── مخزن ──────────────────────────────────────────────────────────
        b.Entity<FuelPurchase>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.Fuel);
            e.HasIndex(x => x.Seller);
            e.HasIndex(x => x.LegacyId);
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<TankDip>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Fuel, x.DateKey });
            e.HasIndex(x => x.MonthKey);
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<TankerUnload>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Fuel, x.DateKey });
            e.HasIndex(x => x.MonthKey);
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ── شرکت‌های تیل ──────────────────────────────────────────────────
        b.Entity<TilCompany>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.LegacyId);
            e.HasMany(x => x.Rows).WithOne(x => x.Company!).HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<CompanyRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CompanyId, x.Fuel, x.SortIndex });
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.SourceExchangeId);   // ردیفِ خودکارِ یک سطرِ صرافی
            e.Property(x => x.Fuel).HasConversion<int>();
            e.Property(x => x.PoulCurrency).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null && x.Company!.DeletedAt == null);
        });

        // ── تیل امانت ─────────────────────────────────────────────────────
        b.Entity<AmanatAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Fuel);
            e.HasIndex(x => x.LegacyId);
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasMany(x => x.Rows).WithOne(x => x.Account!).HasForeignKey(x => x.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<AmanatRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AccountId, x.SortIndex });
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.State);
            e.Property(x => x.State).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null && x.Account!.DeletedAt == null);
        });

        // ── ورقِ روزانه ───────────────────────────────────────────────────
        b.Entity<WaraqEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.LegacyId);
            e.Property(x => x.ActiveShift).HasConversion<int>();
            e.HasMany(x => x.Shifts).WithOne(x => x.Waraq!).HasForeignKey(x => x.WaraqId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<WaraqShift>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WaraqId, x.Kind });
            e.Property(x => x.Kind).HasConversion<int>();
            e.HasMany(x => x.Pumps).WithOne(x => x.Shift!).HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Transactions).WithOne(x => x.Shift!).HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null && x.Waraq!.DeletedAt == null);
        });

        b.Entity<WaraqPump>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ShiftId, x.SortIndex });
            e.HasIndex(x => new { x.ShiftId, x.SrcKey });   // یافتنِ ردیفِ همان پارچه
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null && x.Shift!.DeletedAt == null);
        });

        b.Entity<WaraqTransaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ShiftId, x.SortIndex });
            e.Property(x => x.Fuel).HasConversion<int>();
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.Unit).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null && x.Shift!.DeletedAt == null);
        });

        // ── فاکتورها ──────────────────────────────────────────────────────
        b.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.InvoiceNumber);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.CustomerName);
            e.HasIndex(x => x.Fuel);
            e.HasIndex(x => x.LegacyId);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ── کارمندان ──────────────────────────────────────────────────────
        b.Entity<StaffMember>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.LegacyId);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<AttendanceRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StaffId, x.DateKey });
            e.HasIndex(x => x.DateKey);
            e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<SalaryPayment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StaffId, x.MonthKey });
            e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<StaffShortage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.StaffId);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<StaffShortSettle>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NameKey);
            e.HasIndex(x => x.DateKey);
            e.Property(x => x.Kind).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<VoiceTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AccountKey);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ── دیگر ──────────────────────────────────────────────────────────
        b.Entity<Camera>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SortIndex);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<ExtraIncome>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.MonthKey);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<RateHistoryEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Fuel, x.DateKey });
            e.HasIndex(x => x.MonthKey);
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<TrashItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Kind);
            e.HasIndex(x => x.DeletedAtUtc);
            // سطلِ زباله عمداً فیلترِ حذفِ نرم ندارد — خودش همان سطل است.
        });

        b.Entity<Setting>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).IsRequired();
        });

        b.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.Role).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AtUtc);
            e.HasIndex(x => x.Action);
        });

        b.Entity<SectionNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SectionKey).IsRequired();
            e.HasIndex(x => x.SectionKey);          // «نوت‌های همین بخش»
        });

        b.Entity<SectionNoteDraft>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SectionKey).IsRequired();
            // هر بخش فقط یک پیش‌نویس دارد — همان ‎_noteDrafts()[key]‎ی نسخهٔ وب
            e.HasIndex(x => x.SectionKey).IsUnique();
        });

        base.OnModelCreating(b);
    }

    /// <summary>
    /// مهرِ زمان خودکار. هیچ سرویسی نباید یادش برود — پس این‌جا، در یک جا،
    /// برای همهٔ موجودیت‌ها انجام می‌شود.
    /// </summary>
    public override int SaveChanges()
    { Stamp(); return base.SaveChanges(); }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    { Stamp(); return base.SaveChangesAsync(ct); }

    private void Stamp()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added) { entry.Entity.CreatedAt = now; entry.Entity.UpdatedAt = now; }
            else if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
            else if (entry.State == EntityState.Deleted)
            {
                // سطلِ زباله و تاریخچه خودشان «بایگانی»اند؛ حذف از آن‌ها باید
                // واقعاً حذف باشد، وگرنه «خالی کردنِ سطل» هیچ‌وقت خالی نمی‌کند.
                if (entry.Entity is TrashItem or AuditEntry) continue;

                // بقیه: حذفِ نرم — رکوردِ مالی هیچ‌وقت واقعاً پاک نمی‌شود
                entry.State = EntityState.Modified;
                entry.Entity.DeletedAt = now;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
