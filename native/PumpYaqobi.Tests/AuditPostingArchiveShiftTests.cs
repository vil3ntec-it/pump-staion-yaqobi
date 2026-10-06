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

    /// <summary>
    /// ⛔ (۱۴۰۵/۰۷/۲۲، «همه رو درست کن») همان شخص و همان مبلغ: ردیفِ تازه‌ای که پس از حذفِ
    /// ردیفی بالاتر به کلیدِ آرشیوشده رسید، ثبتِ تازه است — و خودِ ثبتِ آرشیوشده که یک
    /// خانه بالا آمد، دوباره ثبت نمی‌شود. با «همان حساب، همان مبلغ» هر دو برعکس بود:
    /// تازه گم می‌شد و آرشیوشده دو بار شمرده می‌شد.
    /// </summary>
    [Fact]
    public async Task SamePersonSameAmount_ShiftedIntoArchivedKey_IsPosted_AndTheArchivedOneIsNotReposted()
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
            db.WaraqTransactions.RemoveRange(sd.Transactions);
            sd.Transactions.Clear();
            db.WaraqTransactions.Add(new WaraqTransaction { ShiftId = shiftId, SortIndex = 1, Name = "رحیم", Amount = 300m, AmountAuto = false });
            db.WaraqTransactions.Add(new WaraqTransaction { ShiftId = shiftId, SortIndex = 2, Name = "کریم", Amount = 2000m, AmountAuto = false });
            await db.SaveChangesAsync();
        }
        await post.SyncAsync(w.Id);

        // «جدول جدید» برای کریم — ثبتِ ۲۰۰۰ِ ردیفِ ۱ به آرشیو رفت
        await debtors.ArchiveTableAsync(karimAcct, "1405/06/20");

        // قرضِ تازهٔ کریم، باز ۲۰۰۰، در ردیفِ ۲
        string fresh;
        await using (var db = dbf.Create())
        {
            var t = new WaraqTransaction { ShiftId = shiftId, SortIndex = 3, Name = "کریم", Amount = 2000m, AmountAuto = false };
            db.WaraqTransactions.Add(t);
            await db.SaveChangesAsync();
            fresh = t.SyncUid!;
        }
        await post.SyncAsync(w.Id);

        // ردیفِ رحیم حذف ⇒ ثبتِ آرشیوشده به ردیفِ ۰، ثبتِ تازه به ردیفِ ۱ (کلیدِ آرشیوشده)
        await using (var db = dbf.Create())
        {
            var first = await db.WaraqTransactions.Where(t => t.ShiftId == shiftId)
                                .OrderBy(t => t.SortIndex).ThenBy(t => t.Id).FirstAsync();
            db.WaraqTransactions.Remove(first);
            await db.SaveChangesAsync();
        }
        await post.SyncAsync(w.Id);

        await using (var db = dbf.Create())
        {
            var karim = await db.DebtRows.AsNoTracking()
                                .Where(r => r.FuelAccountId == karimAcct && r.Bardagi != 0m).ToListAsync();
            var row = Assert.Single(karim);
            Assert.Equal(2000m, row.Bardagi);
            Assert.Equal(fresh, row.SrcTxn);
            Assert.Empty(await db.DebtRows.AsNoTracking()
                                 .Where(r => r.FuelAccountId == rahimAcct && r.Bardagi != 0m).ToListAsync());
        }
    }
}
