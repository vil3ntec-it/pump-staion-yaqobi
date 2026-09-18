using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ذخیرهٔ حساب، ردیف‌هایش را لمس نمی‌کند ═══════════════════════════════════
///
/// گزارشِ صاحب ریپو از قدیم: «حسابِ قرض‌داران دیر باز می‌شود.» سنجهٔ
/// ‎personperf‎ (۱۴۰۵/۰۷/۰۱) عددش را داد و ریشه همین‌جا بود:
/// ‎DbSet.Attach(a)‎ گرافِ حساب را می‌پیماید، پس نوشتنِ **یک ستون** روی حسابی
/// که تازه از ‎LoadFullAsync‎ آمده، همهٔ ۵۰٬۰۰۰ ردیفش را هم ردیابی می‌کرد و
/// ‎SaveChanges‎ رویشان ‎DetectChanges‎ می‌دوید — ۱۲٬۴۵۶ میلی‌ثانیه برای یک
/// ‎bool‎.
///
/// این آزمون **رفتار** را قفل می‌کند، نه سرعت را: عددهای حساب باید بنشینند،
/// و ردیف‌ها نباید نه ردیابی شوند و نه عوض شوند.
/// </summary>
public class AccountUpdateTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-au-{Guid.NewGuid():N}.db");

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

    /// <summary>یک قرض‌دار با حسابِ اصلی و چند ردیف — همان شکلِ واقعی.</summary>
    private static async Task<(long PersonId, long AccountId)> SeedAsync(PumpDbFactory dbf, int rows)
    {
        await using var db = dbf.Create();
        var p = new Debtor { Name = "آزمون", LegacyId = "au1" };
        db.Debtors.Add(p);
        await db.SaveChangesAsync();

        var a = new DebtAccount { MainOfDebtorId = p.Id, Name = "آزمون", Mode = LedgerMode.Fuel };
        db.DebtAccounts.Add(a);
        await db.SaveChangesAsync();

        for (var i = 0; i < rows; i++)
            db.DebtRows.Add(new DebtRow
            {
                FuelAccountId = a.Id, SortIndex = i, Name = "ردیفِ " + i,
                DateShamsi = "1405/06/18", Liters = 10m, PricePerLiter = 62m,
                Bardagi = 620m, Albaqi = 620m,
            });
        await db.SaveChangesAsync();
        return (p.Id, a.Id);
    }

    [Fact]
    public async Task ZakhireyeHesab_RadifHaRa_LamsNemikonad()
    {
        var (debtors, dbf) = Host();
        var (personId, _) = await SeedAsync(dbf, 40);

        //  حسابِ **کامل** — یعنی ردیف‌هایش هم در حافظه‌اند، همان چیزی که
        //  صفحهٔ شخص دستش دارد.
        var full = await debtors.LoadFullAsync(personId);
        Assert.NotNull(full);
        var acct = full!.MainAccount!;
        Assert.Equal(40, acct.FuelRows.Count);

        //  کاربر یکی از ردیف‌ها را در حافظه دست‌کاری می‌کند ولی ذخیره‌اش
        //  نمی‌کند. ذخیرهٔ **حساب** نباید این را به دیسک ببرد.
        acct.FuelRows[0].Name = "دست‌نخورده نماند؟";
        acct.PercentPetrol = 7m;
        acct.ReceiptsMigrated = true;

        await debtors.UpdateAccountAsync(acct);

        await using var db = dbf.Create();
        var saved = await db.DebtAccounts.AsNoTracking().FirstAsync(x => x.Id == acct.Id);
        Assert.Equal(7m, saved.PercentPetrol);
        Assert.True(saved.ReceiptsMigrated);

        var row0 = await db.DebtRows.AsNoTracking()
                           .OrderBy(x => x.SortIndex).FirstAsync(x => x.FuelAccountId == acct.Id);
        Assert.Equal("ردیفِ 0", row0.Name);
        Assert.Equal(40, await db.DebtRows.CountAsync(x => x.FuelAccountId == acct.Id));
    }

    [Fact]
    public async Task ZakhireyeShakhs_RadifHaRa_LamsNemikonad()
    {
        var (debtors, dbf) = Host();
        var (personId, _) = await SeedAsync(dbf, 25);

        var full = await debtors.LoadFullAsync(personId);
        Assert.NotNull(full);
        full!.Phone = "0700000000";
        full.MainAccount!.FuelRows[1].Name = "این هم نباید برود";

        await debtors.UpdateDebtorAsync(full);

        await using var db = dbf.Create();
        Assert.Equal("0700000000",
            (await db.Debtors.AsNoTracking().FirstAsync(x => x.Id == personId)).Phone);
        var row1 = await db.DebtRows.AsNoTracking()
                           .Where(x => x.FuelAccountId == full.MainAccount!.Id)
                           .OrderBy(x => x.SortIndex).Skip(1).FirstAsync();
        Assert.Equal("ردیفِ 1", row1.Name);
    }

    /// <summary>
    /// همان قاعده برای شرکتِ تیل. تنها صداکنندهٔ ‎Companies.UpdateAsync‎
    /// ساختنِ کیو‌آرِ یک شرکت است (‎AcctLive.EnsureAsync‎)، و آن شرکت از
    /// ‎LoadAsync‎ می‌آید — یعنی ردیف‌هایش هم همراهش‌اند.
    /// </summary>
    [Fact]
    public async Task ZakhireyeSherkat_RadifHaRa_LamsNemikonad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var svc = new CompanyDataService(dbf, perm, trash);

        long id;
        await using (var db = dbf.Create())
        {
            var c = new TilCompany { Name = "شرکتِ آزمون" };
            db.TilCompanies.Add(c);
            await db.SaveChangesAsync();
            id = c.Id;
            for (var i = 0; i < 20; i++)
                db.CompanyRows.Add(new CompanyRow { CompanyId = c.Id, Name = "ردیفِ " + i, SortIndex = i });
            await db.SaveChangesAsync();
        }

        var full = await svc.LoadAsync(id);
        Assert.NotNull(full);
        Assert.Equal(20, full!.Rows.Count);
        full.Rows[0].Name = "این نباید برود";
        full.QrKey = "abc123";

        await svc.UpdateAsync(full);

        await using var check = dbf.Create();
        Assert.Equal("abc123", (await check.TilCompanies.AsNoTracking().FirstAsync(x => x.Id == id)).QrKey);
        var row0 = await check.CompanyRows.AsNoTracking()
                              .Where(x => x.CompanyId == id).OrderBy(x => x.SortIndex).FirstAsync();
        Assert.Equal("ردیفِ 0", row0.Name);
        Assert.Equal(20, await check.CompanyRows.CountAsync(x => x.CompanyId == id));
    }

    /// <summary>
    /// مهرِ «مهاجرت شد» هم از همان در می‌رود و باید هم بنشیند و هم ردیف‌ها را
    /// دست‌نخورده بگذارد — چون ‎CommitReceiptMigrationAsync‎ هم ‎MarkOnly‎ شد.
    /// </summary>
    [Fact]
    public async Task MohreMohajerat_Minesheenad_VaRadifHa_SalemMimanand()
    {
        var (debtors, dbf) = Host();
        var (personId, _) = await SeedAsync(dbf, 30);

        var full = await debtors.LoadFullAsync(personId);
        var acct = full!.MainAccount!;
        acct.ReceiptsMigrated = true;

        await debtors.CommitReceiptMigrationAsync(acct, Array.Empty<DebtRow>());

        await using var db = dbf.Create();
        Assert.True((await db.DebtAccounts.AsNoTracking().FirstAsync(x => x.Id == acct.Id)).ReceiptsMigrated);
        Assert.Equal(30, await db.DebtRows.CountAsync(x => x.FuelAccountId == acct.Id));
    }
}
