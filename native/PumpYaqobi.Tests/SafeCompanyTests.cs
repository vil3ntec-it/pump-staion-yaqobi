using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ گاوصندوق ← حسابِ شرکت (۱۴۰۵/۰۷/۱۹) ══════════════════════════════════════
/// «بردگی‌ای که از گاوصندوق به نامِ یک شرکت ثبت شود باید با همان ارز (دالر یا
/// افغانی) رسیدِ همان شرکت شود — نه مثلِ صرافی رفتار کند، نه گم شود.»
/// همه روی SQLiteِ واقعی، از درِ همان سرویس‌هایی که برنامه می‌زند.
/// </summary>
public class SafeCompanyTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-sc-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (SafeCompanySyncService Sync, CompanyDataService Companies, LedgerService<SafeEntry> Safe, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var companies = new CompanyDataService(dbf, perm, trash);
        return (new SafeCompanySyncService(dbf, perm, companies), companies,
                new LedgerService<SafeEntry>(dbf, perm, trash, "safe", e => e.Title ?? ""), dbf);
    }

    private static async Task<List<CompanyRow>> RowsAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return await db.CompanyRows.AsNoTracking().OrderBy(r => r.SortIndex).ThenBy(r => r.Id).ToListAsync();
    }

    [Theory]
    [InlineData(Currency.Afn)]
    [InlineData(Currency.Usd)]
    public async Task Bardagi_BaHamanArz_RasideSherkatMishavad(Currency cur)
    {
        var (sync, companies, safe, dbf) = Host();
        var c = await companies.AddAsync("شرکت نفت هرات");
        var e = await safe.AddAsync(new SafeEntry
        {
            DateShamsi = "1405/07/10", Kind = SafeEntryKind.Bardagi, Title = "شرکت نفت هرات",
            Amount = 12960.5m, Currency = cur, Note = "حواله 235",
        });
        Assert.Equal(ExchangeLinkResult.Linked, await sync.SyncAsync(e));

        var r = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(c.Id, r.CompanyId);
        Assert.Equal(12960.5m, r.Poul);                 // ⛔ عدد همان، بی تبدیل
        Assert.Equal(cur, r.PoulCurrency);              // ⛔ ارزِ گاوصندوق، نه دالرِ صرافی
        Assert.Equal(0m, r.Usd);
        Assert.Equal(0m, r.Ton);
        Assert.Null(r.SourceExchangeId);                // رفتارِ صرافی نیست
        Assert.Equal(SafeCompanySyncService.KeyOf(e), r.SourceReceiptId);
        Assert.Equal("1405/07/10", r.DateShamsi);
        Assert.Equal("حواله 235", r.Note);
    }

    [Fact]
    public async Task Virayesh_SareJayash_VaDizel_JabeJaMishavad_NaDoBar()
    {
        var (sync, companies, safe, dbf) = Host();
        var c = await companies.AddAsync("شرکت نفت هرات");
        var e = await safe.AddAsync(new SafeEntry { Kind = SafeEntryKind.Bardagi, Title = "شرکت نفت هرات", Amount = 100m });
        await sync.SyncAsync(e);
        e.Amount = 250m; e.Currency = Currency.Usd;
        await sync.SyncAsync(e);
        var r = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(250m, r.Poul);
        Assert.Equal(Currency.Usd, r.PoulCurrency);

        e.Title = "شرکت نفت هرات دیزل";
        await sync.SyncAsync(e);
        r = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(FuelType.Diesel, r.Fuel);
        Assert.Equal(c.Id, r.CompanyId);
    }

    [Fact]
    public async Task Mandagi_YaNameNashenas_HichRasidiNamisazad_VaQabliRaBarmidarad()
    {
        var (sync, companies, safe, dbf) = Host();
        await companies.AddAsync("شرکت نفت هرات");
        var e = await safe.AddAsync(new SafeEntry { Kind = SafeEntryKind.Bardagi, Title = "شرکت نفت هرات", Amount = 100m });
        await sync.SyncAsync(e);
        Assert.Single(await RowsAsync(dbf));

        e.Kind = SafeEntryKind.Mandagi;
        Assert.Equal(ExchangeLinkResult.Empty, await sync.SyncAsync(e));
        Assert.Empty(await RowsAsync(dbf));

        e.Kind = SafeEntryKind.Bardagi; e.Title = "شرکتی که نیست";
        Assert.Equal(ExchangeLinkResult.NotFound, await sync.SyncAsync(e));
        Assert.Empty(await RowsAsync(dbf));
        await using var db = dbf.Create();
        Assert.Equal(1, await db.TilCompanies.CountAsync());   // ⛔ شرکتِ تازه ساخته نشد
    }

    [Fact]
    public async Task Hazf_RasideSherkatRaHamMibarad_VaRadifeDastiDastNamikhorad()
    {
        var (sync, companies, safe, dbf) = Host();
        var c = await companies.AddAsync("شرکت نفت هرات");
        await companies.PutReceiptAsync(c.Id, FuelType.Petrol, new CompanyRow { Name = "دستی", Poul = 5m });
        var e = await safe.AddAsync(new SafeEntry { Kind = SafeEntryKind.Bardagi, Title = "شرکت نفت هرات", Amount = 100m });
        await sync.SyncAsync(e);
        Assert.Equal(2, (await RowsAsync(dbf)).Count);
        await sync.UnlinkAsync(e);
        var r = Assert.Single(await RowsAsync(dbf));
        Assert.Equal("دستی", r.Name);
    }
}
