using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Infrastructure.Migration;

namespace PumpYaqobi.Services.Data;

/// <summary>نتیجهٔ یک مهاجرت — چه آمد، چه نیامد، و اگر نشد چرا.</summary>
/// <param name="BackupPath">بکاپِ خودکارِ پیش از مهاجرت؛ اگر چیزی خراب شد، همین‌جاست.</param>
/// <param name="SourceRecords">شمارشِ خودِ فایلِ بکاپ (‎_dbRecordCount‎).</param>
/// <param name="ImportedRecords">شمارشِ همان جدول‌ها پس از نوشتن در دیتابیس.</param>
public sealed record ImportOutcome(
    bool Ok, string? BackupPath, int SourceRecords, int ImportedRecords,
    string Message, IReadOnlyList<string> Warnings);

/// <summary>
/// ══ آوردنِ دادهٔ نسخهٔ وب (بندِ ۳۱) ═══════════════════════════════════════════
/// فایلِ بکاپِ نسخهٔ وب چیزی جز ‎JSON.stringify(DB)‎ نیست. این سرویس آن را
/// می‌خواند و **یک‌بار** به دیتابیسِ نیتیو می‌آورد.
///
/// چهار قاعده‌ای که بندِ ۳۱ می‌خواهد، همه این‌جا هستند:
///
///   ۱) **پیش از هر کاری بکاپ.** فایلِ دیتابیسِ فعلی کنارِ خودش کپی می‌شود.
///   ۲) **یا همه، یا هیچ.** همهٔ نوشتن‌ها در یک تراکنش‌اند.
///   ۳) **پس از نوشتن، شمارش.** اگر عددِ رکوردها با فایل نخواند، تراکنش
///      برمی‌گردد و دادهٔ فعلی دست‌نخورده می‌ماند.
///   ۴) **دادهٔ فعلی بی‌خبر بازنویسی نمی‌شود.** اگر دیتابیس خالی نباشد، مهاجرت
///      فقط با ‎replaceExisting‎ی صریح انجام می‌شود.
///
/// ⚠️ «نصفه‌کاره» بدترین حالت است: کاربر پیامِ موفقیت می‌بیند و نصفِ حساب‌هایش
/// نیست. برای همین هیچ مسیری این‌جا نیست که بدونِ سنجشِ شمارش به موفقیت برسد.
/// </summary>
public sealed class LegacyImportService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly SettingsService _settings;

    public LegacyImportService(PumpDbFactory dbf, PermissionService perm, SettingsService settings)
    { _dbf = dbf; _perm = perm; _settings = settings; }

    /// <summary>آیا دیتابیس دادهٔ واقعی دارد؟ (مهاجرت روی دادهٔ موجود بی‌اجازه انجام نمی‌شود)</summary>
    public async Task<bool> HasDataAsync(CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        return await db.Debtors.AnyAsync(ct) || await db.Reports.AnyAsync(ct)
            || await db.SafeEntries.AnyAsync(ct) || await db.WaraqEntries.AnyAsync(ct)
            || await db.TilCompanies.AnyAsync(ct);
    }

    /// <summary>کپیِ فایلِ دیتابیس، کنارِ خودش، با مهرِ زمان.</summary>
    public string? BackupNow()
    {
        try
        {
            if (!File.Exists(_dbf.DbPath)) return null;
            var dir = Path.GetDirectoryName(_dbf.DbPath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(_dbf.DbPath)
                     + "-پیش‌از‌مهاجرت-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".db";
            var target = Path.Combine(dir, name);
            File.Copy(_dbf.DbPath, target, overwrite: false);
            return target;
        }
        catch
        {
            // نتوانستیم بکاپ بگیریم — مهاجرت هم نباید انجام شود. صداکننده می‌بیند.
            return null;
        }
    }

    public async Task<ImportOutcome> ImportAsync(string json, bool replaceExisting = false,
                                                 CancellationToken ct = default)
    {
        _perm.Require(Permission.Import);

        LegacyBundle bundle;
        try { bundle = new LegacyOperationsImporter().ParseAll(json); }
        catch (Exception ex)
        {
            return new ImportOutcome(false, null, 0, 0,
                "فایلِ بکاپ خوانده نشد: " + ex.Message, Array.Empty<string>());
        }

        if (bundle.SourceRecords == 0 && bundle.Settings.Count == 0)
            return new ImportOutcome(false, null, 0, 0,
                "این فایل بکاپِ این برنامه نیست", bundle.Report.Warnings);

        if (!replaceExisting && await HasDataAsync(ct))
            return new ImportOutcome(false, null, bundle.SourceRecords, 0,
                "دیتابیس خالی نیست — برای جایگزینی باید صریح تایید کنید",
                bundle.Report.Warnings);

        // ── قاعدهٔ ۱: بکاپِ خودکار، پیش از هر نوشتنی ──
        var backup = BackupNow();
        if (backup is null && File.Exists(_dbf.DbPath))
            return new ImportOutcome(false, null, bundle.SourceRecords, 0,
                "بکاپِ پیش از مهاجرت گرفته نشد — مهاجرت انجام نشد", bundle.Report.Warnings);

        await using var db = _dbf.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (replaceExisting) await ClearAsync(db, ct);
            Write(db, bundle);
            await db.SaveChangesAsync(ct);

            // ── قاعدهٔ ۳: شمارشِ پس از نوشتن ──
            var imported = await CountAsync(db, ct);
            if (imported != bundle.SourceRecords)
            {
                await tx.RollbackAsync(ct);
                return new ImportOutcome(false, backup, bundle.SourceRecords, imported,
                    $"شمارشِ رکوردها نخواند ({imported} در برابرِ {bundle.SourceRecords}) — "
                    + "هیچ چیزی نوشته نشد و دادهٔ فعلی دست‌نخورده ماند",
                    bundle.Report.Warnings);
            }

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(ct); } catch { }
            return new ImportOutcome(false, backup, bundle.SourceRecords, 0,
                "مهاجرت ناتمام ماند و برگردانده شد: " + ex.Message, bundle.Report.Warnings);
        }

        // تنظیم‌ها بیرونِ تراکنش‌اند چون در جدولِ خودشان و بی‌خطرند؛ نبودنشان
        // یعنی نرخِ اتحادیه صفر، پس بعد از نشستنِ داده نوشته می‌شوند.
        foreach (var (k, v) in bundle.Settings)
            try { _settings.Set(k, v); } catch { }

        return new ImportOutcome(true, backup, bundle.SourceRecords, bundle.SourceRecords,
            $"✅ {bundle.SourceRecords} رکورد آورده شد", bundle.Report.Warnings);
    }

    /// <summary>‎_dbRecordCount‎ روی دیتابیسِ نیتیو — همان شانزده جدول.</summary>
    public static async Task<int> CountAsync(Persistence.PumpDbContext db, CancellationToken ct = default)
    {
        // ⚠️ «پارچه» در نیتیو یک جدول است ولی در نسخهٔ وب دو تا (‎reports‎ و
        // ‎shifts‎) — شمارش باید همان دو را با هم بدهد، نه یکی.
        var n = await db.Reports.CountAsync(ct);
        n += await db.Debtors.CountAsync(ct);
        n += await db.TilCompanies.CountAsync(ct);
        n += await db.ExchangeRows.CountAsync(ct);
        n += await db.WaraqEntries.CountAsync(ct);
        n += await db.Expenses.CountAsync(ct);
        n += await db.SafeEntries.CountAsync(ct);
        n += await db.AmanatAccounts.CountAsync(ct);
        n += await db.FuelPurchases.CountAsync(ct);
        n += await db.Invoices.CountAsync(ct);
        n += await db.Attendance.CountAsync(ct);
        n += await db.StaffMembers.CountAsync(ct);
        n += await db.RetailRows.CountAsync(ct);
        return n;
    }

    private static async Task ClearAsync(Persistence.PumpDbContext db, CancellationToken ct)
    {
        // ترتیب مهم نیست چون همه در یک تراکنش‌اند و کلیدهای خارجی آبشاری‌اند،
        // ولی جدول‌های فرزند اول می‌روند تا خطای کلیدِ خارجی پیش نیاید.
        db.DebtRows.RemoveRange(await db.DebtRows.ToListAsync(ct));
        db.DebtAccounts.RemoveRange(await db.DebtAccounts.ToListAsync(ct));
        db.Debtors.RemoveRange(await db.Debtors.ToListAsync(ct));
        db.CompanyRows.RemoveRange(await db.CompanyRows.ToListAsync(ct));
        db.TilCompanies.RemoveRange(await db.TilCompanies.ToListAsync(ct));
        db.WaraqPumps.RemoveRange(await db.WaraqPumps.ToListAsync(ct));
        db.WaraqTransactions.RemoveRange(await db.WaraqTransactions.ToListAsync(ct));
        db.WaraqShifts.RemoveRange(await db.WaraqShifts.ToListAsync(ct));
        db.WaraqEntries.RemoveRange(await db.WaraqEntries.ToListAsync(ct));
        db.AmanatRows.RemoveRange(await db.AmanatRows.ToListAsync(ct));
        db.AmanatAccounts.RemoveRange(await db.AmanatAccounts.ToListAsync(ct));
        db.Attendance.RemoveRange(await db.Attendance.ToListAsync(ct));
        db.SalaryPayments.RemoveRange(await db.SalaryPayments.ToListAsync(ct));
        db.StaffMembers.RemoveRange(await db.StaffMembers.ToListAsync(ct));
        db.Reports.RemoveRange(await db.Reports.ToListAsync(ct));
        db.ShiftDataSet.RemoveRange(await db.ShiftDataSet.ToListAsync(ct));
        db.FuelPurchases.RemoveRange(await db.FuelPurchases.ToListAsync(ct));
        db.SafeEntries.RemoveRange(await db.SafeEntries.ToListAsync(ct));
        db.ExchangeRows.RemoveRange(await db.ExchangeRows.ToListAsync(ct));
        db.Expenses.RemoveRange(await db.Expenses.ToListAsync(ct));
        db.RetailRows.RemoveRange(await db.RetailRows.ToListAsync(ct));
        db.ExtraIncomes.RemoveRange(await db.ExtraIncomes.ToListAsync(ct));
        db.ParchaReceipts.RemoveRange(await db.ParchaReceipts.ToListAsync(ct));
        db.DebtQuickReceipts.RemoveRange(await db.DebtQuickReceipts.ToListAsync(ct));
        db.Invoices.RemoveRange(await db.Invoices.ToListAsync(ct));
        db.TankDips.RemoveRange(await db.TankDips.ToListAsync(ct));
        db.TankerUnloads.RemoveRange(await db.TankerUnloads.ToListAsync(ct));
        db.RateHistory.RemoveRange(await db.RateHistory.ToListAsync(ct));
        db.StaffShortSettles.RemoveRange(await db.StaffShortSettles.ToListAsync(ct));
        db.Cameras.RemoveRange(await db.Cameras.ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }

    private static void Write(Persistence.PumpDbContext db, LegacyBundle b)
    {
        db.Debtors.AddRange(b.Debtors);
        db.SafeEntries.AddRange(b.Safe);
        db.ExchangeRows.AddRange(b.Exchange);
        db.Expenses.AddRange(b.Expenses);
        db.RetailRows.AddRange(b.Retail);
        db.Reports.AddRange(b.Reports);
        db.FuelPurchases.AddRange(b.Purchases);
        db.TilCompanies.AddRange(b.Companies);
        db.TankDips.AddRange(b.Dips);
        db.TankerUnloads.AddRange(b.Unloads);
        db.RateHistory.AddRange(b.Rates);
        db.ExtraIncomes.AddRange(b.ExtraIncomes);
        db.ParchaReceipts.AddRange(b.ParchaReceipts);
        db.DebtQuickReceipts.AddRange(b.QuickReceipts);
        db.WaraqEntries.AddRange(b.Waraq);
        db.Invoices.AddRange(b.Invoices);
        db.AmanatAccounts.AddRange(b.Amanat);
        db.StaffMembers.AddRange(b.Staff);
        db.StaffShortSettles.AddRange(b.Settles);
        db.Cameras.AddRange(b.Cameras);

        // ⚠️ حاضری و معاش آخر می‌آیند: کلیدِ عددیِ کارمند تا پیش از این وجود
        // ندارد. EF با ‎Staff‎ی که شیءِ همان کارمند است، کلید را خودش پر می‌کند.
        var byLegacy = b.Staff.Where(s => !string.IsNullOrEmpty(s.LegacyId))
                              .GroupBy(s => s.LegacyId!)
                              .ToDictionary(g => g.Key, g => g.First());

        foreach (var a in b.Attendance)
        {
            if (a.StaffLegacyId is not null && byLegacy.TryGetValue(a.StaffLegacyId, out var st))
                a.Row.Staff = st;
            db.Attendance.Add(a.Row);
        }
        foreach (var p in b.Salaries)
        {
            if (p.StaffLegacyId is not null && byLegacy.TryGetValue(p.StaffLegacyId, out var st))
                p.Row.Staff = st;
            db.SalaryPayments.Add(p.Row);
        }
    }
}
