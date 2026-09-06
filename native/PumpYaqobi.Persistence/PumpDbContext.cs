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
    public DbSet<SafeEntry> SafeEntries => Set<SafeEntry>();
    public DbSet<ExchangeRow> ExchangeRows => Set<ExchangeRow>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<RetailRow> RetailRows => Set<RetailRow>();

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

        b.Entity<DebtRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FuelAccountId);
            e.HasIndex(x => x.MoneyAccountId);
            e.HasIndex(x => x.DateKey);                            // فیلتر و مرتب‌سازیِ تاریخ
            e.HasIndex(x => new { x.FuelAccountId, x.SortIndex }); // ترتیبِ ردیف‌های یک حساب
            e.HasIndex(x => x.Fuel);                            // تفکیکِ پطرول/دیزل
            e.HasIndex(x => x.Name);
            e.Property(x => x.Fuel).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<SafeEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.MonthKey);      // صفحه ماه‌به‌ماه فیلتر می‌شود
            e.HasIndex(x => x.Kind);
            e.Property(x => x.Kind).HasConversion<int>();
            e.Property(x => x.Currency).HasConversion<int>();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<ExchangeRow>(e =>
        {
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
                // حذفِ نرم: رکوردِ مالی هیچ‌وقت واقعاً پاک نمی‌شود
                entry.State = EntityState.Modified;
                entry.Entity.DeletedAt = now;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
