using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بندِ ۲۳ — سطلِ زباله، بکاپ و بازگردانی ══════════════════════════════════
/// این آزمون‌ها روی **فایلِ واقعیِ دیتابیس** کار می‌کنند، نه روی حافظه: کارِ
/// بکاپ و بازگردانی اصلاً همین است — فایل. با دیتابیسِ در-حافظه، «فایل جایگزین
/// شد» هیچ معنایی ندارد و آزمون سبزِ بی‌خاصیت می‌شود.
/// </summary>
public class BackupTrashTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "pump-bk-" + Guid.NewGuid().ToString("N"));

    private string File_ => Path.Combine(_dir, "pump.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private (PumpDbFactory Db, TrashService Trash, BackupService Backup,
             DebtorService Debtors, PermissionService Perm) Host(UserRole role = UserRole.Admin)
    {
        Directory.CreateDirectory(_dir);
        var dbf = new PumpDbFactory(File_);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(role, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        return (dbf, trash, new BackupService(dbf, perm), new DebtorService(dbf, perm, trash), perm);
    }

    private static async Task<Debtor> AddPersonAsync(PumpDbFactory dbf, string name)
    {
        await using var db = dbf.Create();
        var p = new Debtor { Name = name, LegacyId = "p" + Guid.NewGuid().ToString("N")[..8] };
        p.MainAccount.FuelRows.Add(new DebtRow
        {
            DateShamsi = "1405/06/12", Name = "بردگی", Liters = 40m, PricePerLiter = 80m,
        });
        db.Debtors.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    // ══ سطلِ زباله ═════════════════════════════════════════════════════════

    /// <summary>
    /// حذفِ یک قرض‌دار باید **کلِ حساب و ردیف‌هایش** را هم برگرداند.
    ///
    /// این همان جایی است که پیش از این می‌شکست: در سطل فقط خودِ شخص می‌نشست و
    /// «بازگرداندن» شخصی بی‌حساب و بی‌ردیف پس می‌داد — یعنی حسابِ برگشته خالی
    /// بود و کاربر خیال می‌کرد قرضش پاک شده.
    /// </summary>
    [Fact]
    public async Task RestoringADebtorBringsBackItsAccountsAndRows()
    {
        var (dbf, trash, _, debtors, _) = Host();
        var p = await AddPersonAsync(dbf, "کریم جان");

        await debtors.DeleteDebtorAsync(p.Id);

        await using (var db = dbf.Create())
        {
            Assert.Empty(await db.Debtors.ToListAsync());
            Assert.Empty(await db.DebtRows.ToListAsync());
        }

        var item = Assert.Single(await trash.ListAsync());
        Assert.Equal("debtor", item.Kind);

        Assert.Null(await trash.RestoreAsync(item.Id));

        await using (var db = dbf.Create())
        {
            var back = Assert.Single(await db.Debtors.Include(x => x.MainAccount)
                                                     .ThenInclude(a => a!.FuelRows)
                                                     .ToListAsync());
            Assert.Equal("کریم جان", back.Name);
            Assert.Equal(p.Id, back.Id);                    // همان کلید، نه کلیدِ تازه
            var row = Assert.Single(back.MainAccount.FuelRows);
            Assert.Equal(40m, row.Liters);
            Assert.Equal(80m, row.PricePerLiter);
        }

        Assert.Empty(await trash.ListAsync());              // از سطل هم برداشته شد
    }

    /// <summary>یک ردیفِ تنها هم باید سرِ جای خودش برگردد — نه به حسابِ دیگری.</summary>
    [Fact]
    public async Task RestoringASingleRowPutsItBackInTheSameAccount()
    {
        var (dbf, trash, _, debtors, _) = Host();
        var p = await AddPersonAsync(dbf, "نادر");

        long rowId, acctId;
        await using (var db = dbf.Create())
        {
            var r = await db.DebtRows.FirstAsync();
            rowId = r.Id; acctId = r.FuelAccountId!.Value;
        }

        await debtors.DeleteRowAsync(rowId);
        var item = Assert.Single(await trash.ListAsync());
        Assert.Null(await trash.RestoreAsync(item.Id));

        await using (var db2 = dbf.Create())
        {
            var back = Assert.Single(await db2.DebtRows.ToListAsync());
            Assert.Equal(rowId, back.Id);
            Assert.Equal(acctId, back.FuelAccountId);
        }
    }

    /// <summary>پانزده روز — همان عددِ نسخهٔ وب؛ کهنه‌تر خودش می‌رود.</summary>
    [Fact]
    public async Task TrashKeepsFifteenDaysAndPrunesOlder()
    {
        var (dbf, trash, _, debtors, _) = Host();
        Assert.Equal(15, TrashService.RetentionDays);

        var p = await AddPersonAsync(dbf, "کهنه");
        await debtors.DeleteDebtorAsync(p.Id);

        await using (var db = dbf.Create())
        {
            var t = await db.Trash.FirstAsync();
            t.DeletedAtUtc = DateTime.UtcNow.AddDays(-16);
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await trash.PruneAsync());
        Assert.Empty(await trash.ListAsync());
    }

    /// <summary>تازه‌ها نمی‌روند — وگرنه «پاک کردنِ خودکار» یعنی از دست دادنِ داده.</summary>
    [Fact]
    public async Task PruneLeavesFreshItemsAlone()
    {
        var (dbf, trash, _, debtors, _) = Host();
        var p = await AddPersonAsync(dbf, "تازه");
        await debtors.DeleteDebtorAsync(p.Id);

        Assert.Equal(0, await trash.PruneAsync());
        var item = Assert.Single(await trash.ListAsync());
        Assert.InRange(TrashService.DaysLeft(item), 14, 15);
    }

    /// <summary>خالی کردنِ سطل کارِ مدیر است — کارمند نباید بتواند.</summary>
    [Fact]
    public async Task StaffCannotEmptyTheTrash()
    {
        var (dbf, trash, _, _, _) = Host(UserRole.Staff);
        await using (var db = dbf.Create())
        {
            db.Trash.Add(new TrashItem { Kind = "expense", Label = "چیزی", PayloadJson = "{}" });
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<PermissionDeniedException>(() => trash.EmptyAsync());
        Assert.Single(await trash.ListAsync());
    }

    // ══ بکاپ ═══════════════════════════════════════════════════════════════

    /// <summary>عکس باید فایلی باشد که خودش خوانده می‌شود و رکوردها را دارد.</summary>
    [Fact]
    public async Task ASnapshotIsAReadableDatabaseWithTheSameRecords()
    {
        var (dbf, _, backup, _, _) = Host();
        await AddPersonAsync(dbf, "یکی");
        await AddPersonAsync(dbf, "دوتا");

        var target = Path.Combine(_dir, "snap.db");
        backup.WriteSnapshot(target);

        Assert.True(File.Exists(target));
        // دو شخص + دو ردیف = چهار رکورد در شمارشِ ‎Inspect‎
        Assert.Equal(4, BackupService.Inspect(target));
    }

    /// <summary>هر فایلی بکاپ نیست — این همان چیزی است که جلوی فاجعه را می‌گیرد.</summary>
    [Fact]
    public void AForeignFileIsNotAcceptedAsABackup()
    {
        Host();
        var junk = Path.Combine(_dir, "junk.db");
        File.WriteAllText(junk, "این فایلِ بکاپ نیست");

        Assert.Equal(-1, BackupService.Inspect(junk));
        Assert.Equal(-1, BackupService.Inspect(Path.Combine(_dir, "nothing-here.db")));
    }

    /// <summary>عکسِ روزانه ساخته می‌شود و در فهرست می‌آید.</summary>
    [Fact]
    public async Task TodaysSnapshotShowsUpInTheList()
    {
        var (dbf, _, backup, _, _) = Host();
        await AddPersonAsync(dbf, "یکی");

        Assert.NotNull(backup.SnapshotToday());
        var f = Assert.Single(backup.List());
        Assert.True(f.Bytes > 0);
        Assert.Equal(2, BackupService.Inspect(f.Path));
    }

    // ══ بازگردانی ══════════════════════════════════════════════════════════

    /// <summary>
    /// کارِ اصلی: بکاپ گرفته می‌شود، بعد داده عوض می‌شود، بعد بازگردانی —
    /// و دیتابیس باید دقیقاً همان چیزی باشد که در بکاپ بود.
    /// </summary>
    [Fact]
    public async Task RestoreBringsTheDatabaseBackExactly()
    {
        var (dbf, _, backup, _, _) = Host();
        await AddPersonAsync(dbf, "کریم جان");

        var snap = Path.Combine(_dir, "before.db");
        backup.WriteSnapshot(snap);

        await AddPersonAsync(dbf, "پس از بکاپ");
        await using (var db = dbf.Create())
            Assert.Equal(2, await db.Debtors.CountAsync());

        var outcome = backup.Restore(snap);
        Assert.True(outcome.Ok, outcome.Message);
        Assert.NotNull(outcome.SafetyCopy);              // راهِ برگشت هست

        await using (var db = dbf.Create())
        {
            var back = Assert.Single(await db.Debtors.ToListAsync());
            Assert.Equal("کریم جان", back.Name);
        }
    }

    /// <summary>
    /// عکسِ ایمنی باید واقعاً حالِ **پیش از** بازگردانی را داشته باشد — وگرنه
    /// «برگشت‌پذیر» فقط یک حرف است.
    /// </summary>
    [Fact]
    public async Task TheSafetyCopyHoldsTheStateFromBeforeTheRestore()
    {
        var (dbf, _, backup, _, _) = Host();
        await AddPersonAsync(dbf, "اولی");

        var snap = Path.Combine(_dir, "one.db");
        backup.WriteSnapshot(snap);

        await AddPersonAsync(dbf, "دومی");
        var outcome = backup.Restore(snap);

        Assert.True(outcome.Ok, outcome.Message);
        Assert.Equal(4, BackupService.Inspect(outcome.SafetyCopy!));   // دو شخص + دو ردیف
    }

    /// <summary>فایلِ بیگانه هیچ چیزی را عوض نمی‌کند.</summary>
    [Fact]
    public async Task AForeignFileChangesNothing()
    {
        var (dbf, _, backup, _, _) = Host();
        await AddPersonAsync(dbf, "دست‌نخورده");

        var junk = Path.Combine(_dir, "junk2.db");
        File.WriteAllText(junk, "nope");

        var outcome = backup.Restore(junk);
        Assert.False(outcome.Ok);
        Assert.Null(outcome.SafetyCopy);

        await using var db = dbf.Create();
        Assert.Equal("دست‌نخورده", (await db.Debtors.SingleAsync()).Name);
    }

    /// <summary>بازگردانی کارِ مدیر است — کارمند نباید بتواند.</summary>
    [Fact]
    public void StaffCannotRestore()
    {
        var (_, _, backup, _, _) = Host(UserRole.Staff);
        Assert.Throws<PermissionDeniedException>(() => backup.Restore("هرچه"));
    }
}
