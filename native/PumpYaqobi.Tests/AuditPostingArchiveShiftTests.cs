using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ممیزی: کلیدِ منبعِ ردیفِ ورق «جای ردیف» است. ردیفی که به آرشیو رفته، پس از
/// حذفِ یک ردیفِ بالاتر در ورق، کلیدش به ردیفِ دیگری (شخصِ دیگر) می‌رسد — و آن
/// ردیف نه ثبت می‌شود و نه ثبتِ پیشینش می‌ماند.
/// </summary>
public class AuditPostingArchiveShiftTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-aud-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    [Fact]
    public async Task DeletingAnEarlierSheetRow_DoesNotEraseAnotherPersonsDebt_WhenAKeyWasArchived()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var calc = new WaraqService();
        var safe = new ShiftWaraqSyncService(dbf, perm, calc, new SettingsService(dbf, perm));
        var post = new WaraqPostingService(dbf, perm, calc, safe);
        var data = new WaraqDataService(dbf, perm, trash);
        var debtors = new DebtorService(dbf, perm, trash);

        long karimAcct, rahimAcct;
        await using (var db = dbf.Create())
        {
            var k = new Debtor { Name = "کریم", LegacyId = "pk" }; k.MainAccount.Name = "کریم";
            var r = new Debtor { Name = "رحیم", LegacyId = "pr" }; r.MainAccount.Name = "رحیم";
            db.Debtors.AddRange(k, r);
            await db.SaveChangesAsync();
            karimAcct = k.MainAccount.Id; rahimAcct = r.MainAccount.Id;
        }

        var w = await data.OpenOrCreateAsync("1405/06/18", "پمپ");
        long shiftId;
        await using (var db = dbf.Create())
        {
            var sd = await db.WaraqShifts.Include(s => s.Transactions)
                             .FirstAsync(s => s.WaraqId == w.Id && s.Kind == ShiftKind.Day);
            shiftId = sd.Id;
            var t = sd.Transactions.OrderBy(x => x.SortIndex).ToList();
            // ردیفِ ۰ و ۱: کریم · ردیفِ ۲: رحیم
            t[0].Name = "کریم"; t[0].Amount = 1000m; t[0].AmountAuto = false;
            t[1].Name = "کریم"; t[1].Amount = 2000m; t[1].AmountAuto = false;
            t[2].Name = "رحیم"; t[2].Amount = 5000m; t[2].AmountAuto = false;
            await db.SaveChangesAsync();
        }
        await post.SyncAsync(w.Id);

        // «جدول جدید» برای کریم — دو ردیفش به آرشیو رفت
        await debtors.ArchiveTableAsync(karimAcct, "1405/06/20");

        // کاربر ردیفِ اولِ ورق را حذف می‌کند (همان ‎DeleteTxnAsync‎ی صفحه) و ورق دوباره ثبت می‌شود
        await using (var db = dbf.Create())
        {
            var first = await db.WaraqTransactions.Where(t => t.ShiftId == shiftId)
                                .OrderBy(t => t.SortIndex).ThenBy(t => t.Id).FirstAsync();
            db.WaraqTransactions.Remove(first);
            await db.SaveChangesAsync();
        }
        await post.SyncAsync(w.Id);

        // ⛔ قرضِ ۵۰۰۰ِ رحیم هنوز در ورق هست؛ باید در حسابش هم باشد
        await using (var db = dbf.Create())
        {
            var rahim = await db.DebtRows.AsNoTracking()
                                .Where(r => r.FuelAccountId == rahimAcct && r.Bardagi != 0m).ToListAsync();
            Assert.Equal(5000m, rahim.Sum(r => r.Bardagi));
        }
    }
}
