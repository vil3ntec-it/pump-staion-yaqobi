using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «جدول جدید» و جدول‌های آرشیو ══════════════════════════════════════════
/// همتای ‎newPersonTable()‎ و ‎acct.tableHistory‎ی نسخهٔ وب.
///
/// چیزهایی که این‌جا قفل می‌شوند، چون هر کدام یک‌بار در نسخهٔ وب باگ بوده‌اند:
///
///   • عکسِ جدول واقعاً گرفته می‌شود و ردیف‌هایش در آرشیو می‌مانند.
///   • جدولِ زنده خالی می‌شود و رسیدهای سربرگ صفر می‌شوند — وگرنه رسیدِ
///     جدولِ رفته، روی جدولِ نو دوباره شمرده می‌شد.
///   • ⚠️ دفترِ آن‌یکی واحد (پول یا تیل) اصلاً دست نمی‌خورد. این دو دفترِ
///     کاملاً جدا هستند و «جدول جدید» فقط مالِ دفترِ واحدِ فعال است.
///   • آرشیو عکسِ گذشته است: ویرایشِ بعدیِ حساب رویش اثر ندارد.
/// </summary>
public class TableArchiveTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-arc-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (DebtorService Debtors, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        return (new DebtorService(dbf, perm, trash), dbf);
    }

    /// <summary>یک قرض‌دار با دو ردیفِ تیل و یک ردیفِ پول، و رسیدهای سربرگ.</summary>
    private static async Task<DebtAccount> SeedAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        var p = new Debtor
        {
            Name = "کریم",
            LegacyId = "p" + Guid.NewGuid().ToString("N")[..8],
            MainAccount = new DebtAccount
            {
                Mode = LedgerMode.Fuel,
                PercentPetrol = 4m,
                RasidFuelPetrol = 120m,
                RasidMoneyPetrol = 900m,
                Note = "یادداشتِ کهنه",
            },
        };
        db.Debtors.Add(p);
        await db.SaveChangesAsync();

        var a = p.MainAccount;
        db.DebtRows.AddRange(
            new DebtRow { FuelAccountId = a.Id, DateShamsi = "1405/6/1", Name = "ردیفِ تیل ۱", Liters = 50m },
            new DebtRow { FuelAccountId = a.Id, DateShamsi = "1405/6/2", Name = "ردیفِ تیل ۲", Liters = 30m },
            new DebtRow { MoneyAccountId = a.Id, DateShamsi = "1405/6/3", Name = "ردیفِ پول", Bardagi = 700m, ByMoney = true });
        await db.SaveChangesAsync();
        return a;
    }

    [Fact]
    public async Task NewTableArchivesTheLiveRowsAndLeavesItEmpty()
    {
        var (svc, dbf) = Host();
        var a = await SeedAsync(dbf);

        var snap = await svc.ArchiveTableAsync(a.Id, "1405/6/14");

        Assert.Equal(2, snap.RowCount);
        Assert.Equal("1405/6/14", snap.CreatedShamsi);
        Assert.False(snap.IsMoney);                       // واحدِ فعال «تیل» بود
        Assert.Equal(4m, snap.PercentPetrol);
        Assert.Equal(120m, snap.RasidFuelPetrol);
        Assert.Equal("یادداشتِ کهنه", snap.Note);

        var rows = DebtorService.ArchiveRows(snap);
        Assert.Equal(new[] { "ردیفِ تیل ۱", "ردیفِ تیل ۲" }, rows.Select(r => r.Name));
        Assert.Equal(80m, rows.Sum(r => r.Liters));
    }

    [Fact]
    public async Task TheOtherLedgerIsNeverTouched()
    {
        var (svc, dbf) = Host();
        var a = await SeedAsync(dbf);

        await svc.ArchiveTableAsync(a.Id, "1405/6/14");

        await using var db = dbf.Create();
        var left = await db.DebtRows.AsNoTracking().ToListAsync();

        // دفترِ تیل خالی شد، دفترِ پول سرِ جایش ماند
        Assert.Single(left);
        Assert.Equal("ردیفِ پول", left[0].Name);
        Assert.Equal(a.Id, left[0].MoneyAccountId);
    }

    [Fact]
    public async Task HeaderRasidOfTheActiveUnitIsClearedAndTheOtherIsKept()
    {
        var (svc, dbf) = Host();
        var a = await SeedAsync(dbf);

        await svc.ArchiveTableAsync(a.Id, "1405/6/14");

        await using var db = dbf.Create();
        var acct = await db.DebtAccounts.AsNoTracking().FirstAsync(x => x.Id == a.Id);

        Assert.Equal(0m, acct.RasidFuelPetrol);    // واحدِ فعال، پاک شد
        Assert.Equal(900m, acct.RasidMoneyPetrol); // واحدِ دیگر، دست‌نخورده
        Assert.Null(acct.Note);
    }

    [Fact]
    public async Task ArchivesAreSnapshots_LaterEditsDoNotReachThem()
    {
        var (svc, dbf) = Host();
        var a = await SeedAsync(dbf);
        await svc.ArchiveTableAsync(a.Id, "1405/6/14");

        // ردیفِ تازه در جدولِ نو
        await svc.SaveRowAsync(new DebtRow
        { FuelAccountId = a.Id, DateShamsi = "1405/6/20", Name = "ردیفِ نو", Liters = 5m });

        var list = await svc.ListArchivesAsync(a.Id);
        var rows = DebtorService.ArchiveRows(Assert.Single(list));

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.Name == "ردیفِ نو");
    }

    [Fact]
    public async Task ArchivesComeBackNewestFirstAndCanBeDeleted()
    {
        var (svc, dbf) = Host();
        var a = await SeedAsync(dbf);

        await svc.ArchiveTableAsync(a.Id, "1405/6/14");
        await svc.SaveRowAsync(new DebtRow
        { FuelAccountId = a.Id, DateShamsi = "1405/7/1", Name = "دورِ دوم", Liters = 9m });
        await svc.ArchiveTableAsync(a.Id, "1405/7/2");

        var list = await svc.ListArchivesAsync(a.Id);
        Assert.Equal(new[] { "1405/7/2", "1405/6/14" }, list.Select(h => h.CreatedShamsi));

        await svc.DeleteArchiveAsync(list[0].Id);
        Assert.Single(await svc.ListArchivesAsync(a.Id));
    }
}
