using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ صرافی ← حسابِ شرکت ═════════════════════════════════════════════════════
/// ‎syncSarrafiToCompany‎ — نفرِ شرکت به صرافی می‌رود و «بردگی» را می‌گیرد؛
/// همان بردگی به‌صورت **دالری** در حسابِ همان شرکت می‌نشیند.
/// </summary>
public class ExchangeCompanyTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-sf-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (ExchangeCompanySyncService Sync, CompanyDataService Companies, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        return (new ExchangeCompanySyncService(dbf, perm),
                new CompanyDataService(dbf, perm, new TrashService(dbf, perm, session)), dbf);
    }

    private static async Task<List<CompanyRow>> RowsAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return await db.CompanyRows.AsNoTracking()
                       .OrderBy(r => r.SortIndex).ThenBy(r => r.Id).ToListAsync();
    }

    // ── نشستنِ بردگی در حساب ───────────────────────────────────────────────
    [Fact]
    public async Task TheBardagiLandsInTheCompanyAccountAsDollars()
    {
        var (sync, companies, dbf) = Host();
        var c = await companies.AddAsync("شرکت نفت هرات");

        var row = new ExchangeRow
        {
            DateShamsi = "1405/06/12", Description = "شرکت نفت هرات",
            Amount = 100000m, Rate = 70m, Bardagi = 1200m,
        };
        Assert.Equal(ExchangeLinkResult.Linked, await sync.SyncAsync(row));

        var r = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(c.Id, r.CompanyId);
        Assert.Equal(1200m, r.Poul);
        Assert.Equal(Currency.Usd, r.PoulCurrency);      // ⚠️ دالر، نه افغانی
        Assert.Equal(0m, r.Ton);
        Assert.Equal(0m, r.Usd);
        Assert.Equal(0m, r.Rate);
        Assert.Equal("💱 شرکت نفت هرات از صرافی", r.Name);
        Assert.Equal("1405/06/12", r.DateShamsi);
        Assert.Equal(row.LegacyId, r.SourceExchangeId);
        Assert.Equal(FuelType.Petrol, r.Fuel);           // پیش‌فرض
    }

    /// <summary>نوعِ تیل از متن — و پیش‌فرض پطرول است.</summary>
    [Fact]
    public async Task TheFuelComesFromTheText()
    {
        var (sync, companies, dbf) = Host();
        await companies.AddAsync("الفت");

        await sync.SyncAsync(new ExchangeRow { Description = "الفت دیزل", Bardagi = 500m });

        Assert.Equal(FuelType.Diesel, (await RowsAsync(dbf))[0].Fuel);
    }

    /// <summary>
    /// ⚠️ نوعِ تیل فقط از **باقی‌ماندهٔ** متن خوانده می‌شود، نه از کلِ آن.
    /// وگرنه شرکتی که «دیزل» در نامش هست همیشه دیزل تشخیص داده می‌شد، حتی
    /// وقتی کاربر چیزِ دیگری نوشته باشد.
    /// </summary>
    [Fact]
    public async Task TheCompanysOwnNameDoesNotDecideTheFuel()
    {
        var (sync, companies, dbf) = Host();
        await companies.AddAsync("شرکت دیزل هرات");

        var row = new ExchangeRow { Description = "شرکت دیزل هرات", Bardagi = 400m };
        await sync.SyncAsync(row);

        // «دیزل» تنها داخلِ نامِ خودِ شرکت است → پیش‌فرض، یعنی پطرول
        Assert.Equal(FuelType.Petrol, (await RowsAsync(dbf))[0].Fuel);
    }

    // ── ویرایش ─────────────────────────────────────────────────────────────
    /// <summary>ویرایشِ مبلغ، همان ردیف را سرِ جایش به‌روز می‌کند.</summary>
    [Fact]
    public async Task EditingUpdatesTheSameRowInPlace()
    {
        var (sync, companies, dbf) = Host();
        await companies.AddAsync("الفت");

        var row = new ExchangeRow { Description = "الفت", Bardagi = 500m };
        await sync.SyncAsync(row);
        var first = Assert.Single(await RowsAsync(dbf));

        row.Bardagi = 800m;
        await sync.SyncAsync(row);

        var again = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(first.Id, again.Id);          // همان ردیف، نه ردیفِ تازه
        Assert.Equal(800m, again.Poul);
    }

    /// <summary>
    /// ⚠️ عوض شدنِ نام: بردگی نباید در حسابِ شرکتِ قبلی هم بماند — وگرنه دو
    /// بار شمرده می‌شود.
    /// </summary>
    [Fact]
    public async Task ChangingTheNameMovesTheRowInsteadOfLeavingACopy()
    {
        var (sync, companies, dbf) = Host();
        var a = await companies.AddAsync("الفت");
        var b = await companies.AddAsync("برادران احمدی");

        var row = new ExchangeRow { Description = "الفت", Bardagi = 500m };
        await sync.SyncAsync(row);
        Assert.Equal(a.Id, (await RowsAsync(dbf)).Single().CompanyId);

        row.Description = "برادران احمدی";
        await sync.SyncAsync(row);

        var rows = await RowsAsync(dbf);
        Assert.Single(rows);                       // یکی، نه دوتا
        Assert.Equal(b.Id, rows[0].CompanyId);
    }

    /// <summary>عوض شدنِ نوعِ تیل هم ردیف را جابه‌جا می‌کند، نه تکثیر.</summary>
    [Fact]
    public async Task ChangingTheFuelMovesTheRowToTheOtherLedger()
    {
        var (sync, companies, dbf) = Host();
        await companies.AddAsync("الفت");

        var row = new ExchangeRow { Description = "الفت", Bardagi = 500m };
        await sync.SyncAsync(row);
        Assert.Equal(FuelType.Petrol, (await RowsAsync(dbf)).Single().Fuel);

        row.Description = "الفت دیزل";
        await sync.SyncAsync(row);

        var rows = await RowsAsync(dbf);
        Assert.Single(rows);
        Assert.Equal(FuelType.Diesel, rows[0].Fuel);
    }

    /// <summary>بردگیِ صفر یعنی ردیف برداشته شود، نه ردیفِ صفر بماند.</summary>
    [Fact]
    public async Task ZeroBardagiRemovesTheRow()
    {
        var (sync, companies, dbf) = Host();
        await companies.AddAsync("الفت");

        var row = new ExchangeRow { Description = "الفت", Bardagi = 500m };
        await sync.SyncAsync(row);
        Assert.Single(await RowsAsync(dbf));

        row.Bardagi = 0m;
        await sync.SyncAsync(row);
        Assert.Empty(await RowsAsync(dbf));
    }

    /// <summary>
    /// شرکتِ نبوده ساخته **نمی‌شود** — برخلافِ «خریدِ مخزن». نسخهٔ وب صریح
    /// نوشته: «بخش‌های صرافی/گاوصندوق/ورق دیگر حساب نمی‌سازند».
    /// </summary>
    [Fact]
    public async Task AnUnknownNameNeverCreatesACompany()
    {
        var (sync, companies, dbf) = Host();
        await companies.AddAsync("الفت");

        var row = new ExchangeRow { Description = "شرکتِ ناشناس", Bardagi = 500m };
        Assert.Equal(ExchangeLinkResult.NotFound, await sync.SyncAsync(row));

        Assert.Single(await companies.ListAsync());
        Assert.Empty(await RowsAsync(dbf));
    }

    /// <summary>توضیحاتِ خالی یعنی هیچ کاری لازم نیست.</summary>
    [Fact]
    public async Task AnEmptyDescriptionDoesNothing()
    {
        var (sync, _, dbf) = Host();
        Assert.Equal(ExchangeLinkResult.Empty,
            await sync.SyncAsync(new ExchangeRow { Description = "", Bardagi = 500m }));
        Assert.Empty(await RowsAsync(dbf));
    }

    /// <summary>ردیفِ خودکار در نخستین ردیفِ خالی می‌نشیند، نه روی ردیفِ پُر.</summary>
    [Fact]
    public async Task TheRowTakesTheFirstEmptySlot()
    {
        var (sync, companies, dbf) = Host();
        var c = await companies.AddAsync("الفت");
        await companies.SaveRowAsync(new CompanyRow
        { CompanyId = c.Id, Fuel = FuelType.Petrol, SortIndex = 0, Name = "خرید", Ton = 12m });
        await companies.SaveRowAsync(new CompanyRow
        { CompanyId = c.Id, Fuel = FuelType.Petrol, SortIndex = 1 });     // خالی

        await sync.SyncAsync(new ExchangeRow { Description = "الفت", Bardagi = 300m });

        var rows = await RowsAsync(dbf);
        Assert.Equal(2, rows.Count);
        Assert.Equal("خرید", rows[0].Name);          // ردیفِ پُر دست‌نخورده
        Assert.Equal(300m, rows[1].Poul);            // در ردیفِ خالی نشست
    }

    /// <summary>
    /// حذفِ سطرِ صرافی، ردیفِ خودکارش را هم می‌بَرد — وگرنه بردگی در حسابِ
    /// شرکت می‌مانَد بی آنکه سطری پشتش باشد.
    /// </summary>
    [Fact]
    public async Task DeletingTheExchangeRowTakesTheCompanyRowWithIt()
    {
        var (sync, companies, dbf) = Host();
        await companies.AddAsync("الفت");

        var row = new ExchangeRow { Description = "الفت", Bardagi = 500m };
        await sync.SyncAsync(row);
        Assert.Single(await RowsAsync(dbf));

        await sync.UnlinkAsync(row.LegacyId);
        Assert.Empty(await RowsAsync(dbf));
    }

}

/// <summary>
/// ══ چکنه پشتِ اجازهٔ مدیر ═══════════════════════════════════════════════════
/// در نسخهٔ وب ‎updateChakana‎ و همهٔ دکمه‌های افزودنِ ردیف/ماهِ چکنه
/// ‎requireAdmin()‎ دارند — برخلافِ مصارف و گاوصندوق و صرافی که ویرایششان
/// برای کارمند هم باز است.
///
/// ⚠️ ‎EditData‎ کافی نیست: کارمند آن را دارد.
/// </summary>
public class RetailPermissionTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-ck-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private LedgerService<RetailRow> Ledger(UserRole role)
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(role, "آزمون");
        var perm = new PermissionService(session);
        return new LedgerService<RetailRow>(dbf, perm, new TrashService(dbf, perm, session),
            "chakana", r => r.Name ?? "", Permission.ManagerOnly);
    }

    [Fact]
    public async Task AStaffUserCannotTouchTheRetailLedger()
    {
        var staff = Ledger(UserRole.Staff);
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => staff.AddAsync(new RetailRow { DateShamsi = "1405/06/12", Name = "خریدار" }));
    }

    [Fact]
    public async Task TheManagerCan()
    {
        var admin = Ledger(UserRole.Admin);
        var r = await admin.AddAsync(new RetailRow { DateShamsi = "1405/06/12", Name = "خریدار" });
        Assert.NotEqual(0, r.Id);

        r.Liters = 20m;
        await admin.UpdateAsync(r);
        await admin.DeleteAsync(r.Id);
    }

    /// <summary>کارمند هنوز می‌بیند — فقط نمی‌تواند عوض کند.</summary>
    [Fact]
    public async Task AStaffUserCanStillRead()
        => Assert.Empty(await Ledger(UserRole.Staff).ListAsync(null));

    /// <summary>و بقیهٔ دفترها همچنان برای کارمند باز می‌مانند.</summary>
    [Fact]
    public async Task TheOtherLedgersStayOpenToStaff()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Staff, "کارمند");
        var perm = new PermissionService(session);
        var expenses = new LedgerService<Expense>(dbf, perm,
            new TrashService(dbf, perm, session), "expense", e => e.Title ?? "");

        var e = await expenses.AddAsync(new Expense { DateShamsi = "1405/06/12", Title = "چای" });
        Assert.NotEqual(0, e.Id);
    }
}
