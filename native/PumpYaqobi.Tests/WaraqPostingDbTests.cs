using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ همان همگام‌سازی، این‌بار روی دیتابیسِ واقعی ═══════════════════════════════
///
/// آزمون‌های <see cref="WaraqPostingTests"/> منطق را می‌سنجند؛ این‌جا مسیرِ
/// کامل سنجیده می‌شود: ورق در دیتابیس، حساب در دیتابیس، و ردیفی که واقعاً
/// نوشته (و واقعاً حذف) می‌شود.
///
/// ⚠️ «حذف» این‌جا مهم است: کلیدِ حسابِ یک ردیف ‎null‎پذیر است، پس اگر ردیف را
/// فقط از فهرست برداریم، EF آن را بی‌حساب می‌کند و در جدول می‌مانَد — قرضی که
/// در هیچ حسابی دیده نمی‌شود ولی هست.
/// </summary>
public class WaraqPostingDbTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-wp-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (WaraqPostingService Post, WaraqDataService Data, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        return (new WaraqPostingService(dbf, perm, new WaraqService()),
                new WaraqDataService(dbf, perm, trash), dbf);
    }

    private static async Task<Debtor> PersonAsync(PumpDbFactory dbf, string name)
    {
        await using var db = dbf.Create();
        var p = new Debtor { Name = name, LegacyId = "p" + Guid.NewGuid().ToString("N")[..8] };
        p.MainAccount.Name = name;
        db.Debtors.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    /// <summary>ورقی با یک ردیفِ آمادهٔ همان نام.</summary>
    private static async Task<WaraqEntry> SheetAsync(WaraqDataService data, PumpDbFactory dbf,
                                                     string rowName, decimal liters)
    {
        var w = await data.OpenOrCreateAsync("1405/06/18", "پمپ یعقوبی");
        await using var db = dbf.Create();
        var shift = await db.WaraqShifts.Include(s => s.Transactions)
                            .FirstAsync(s => s.WaraqId == w.Id && s.Kind == ShiftKind.Day);
        shift.PricePerLiter = 50m;
        var t = shift.Transactions.OrderBy(x => x.SortIndex).First();
        t.Name = rowName;
        t.Liters = liters;
        await db.SaveChangesAsync();
        return w;
    }

    private static async Task<List<DebtRow>> RowsAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return await db.DebtRows.AsNoTracking().Where(r => r.Src == "waraq").ToListAsync();
    }

    /// <summary>نامی که در ورق نوشته می‌شود، خودش به حسابِ صاحبش می‌رسد.</summary>
    [Fact]
    public async Task WhatIsTypedInTheSheetReachesTheAccountByItself()
    {
        var (post, data, dbf) = Host();
        await PersonAsync(dbf, "محمد هارون");
        var w = await SheetAsync(data, dbf, "هارون", 10m);

        var report = await post.SyncAsync(w.Id);

        Assert.Equal(1, report.Posted);
        var row = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(500m, row.Bardagi);
        Assert.NotNull(row.FuelAccountId);
    }

    /// <summary>دوباره زدنش ردیفِ دوم نمی‌سازد.</summary>
    [Fact]
    public async Task SyncingTwiceKeepsOneRow()
    {
        var (post, data, dbf) = Host();
        await PersonAsync(dbf, "محمد هارون");
        var w = await SheetAsync(data, dbf, "هارون", 10m);

        await post.SyncAsync(w.Id);
        await post.SyncAsync(w.Id);

        Assert.Single(await RowsAsync(dbf));
    }

    /// <summary>
    /// نام که پاک شود، ردیف از دیتابیس هم می‌رود — نه این‌که بی‌حساب بمانَد.
    /// </summary>
    [Fact]
    public async Task ClearingTheNameReallyDeletesTheRow()
    {
        var (post, data, dbf) = Host();
        await PersonAsync(dbf, "محمد هارون");
        var w = await SheetAsync(data, dbf, "هارون", 10m);
        await post.SyncAsync(w.Id);
        Assert.Single(await RowsAsync(dbf));

        await using (var db = dbf.Create())
        {
            var t = await db.WaraqTransactions.FirstAsync(x => x.Name == "هارون");
            t.Name = "";
            await db.SaveChangesAsync();
        }
        await post.SyncAsync(w.Id);

        Assert.Empty(await RowsAsync(dbf));
    }

    /// <summary>«واحد پول» که انتخاب شود، ردیف واقعاً به دفترِ پول کوچ می‌کند.</summary>
    [Fact]
    public async Task ChangingTheUnitMovesTheRowInTheDatabaseToo()
    {
        var (post, data, dbf) = Host();
        await PersonAsync(dbf, "محمد هارون");
        var w = await SheetAsync(data, dbf, "هارون", 10m);
        await post.SyncAsync(w.Id);

        await using (var db = dbf.Create())
        {
            var t = await db.WaraqTransactions.FirstAsync(x => x.Name == "هارون");
            t.Unit = LedgerMode.Money;
            await db.SaveChangesAsync();
        }
        await post.SyncAsync(w.Id);

        var row = Assert.Single(await RowsAsync(dbf));
        Assert.Null(row.FuelAccountId);
        Assert.NotNull(row.MoneyAccountId);
    }

    /// <summary>«مصرف» به بخشِ مصارف می‌رود و در حسابِ کسی نمی‌مانَد.</summary>
    [Fact]
    public async Task AnExpenseRowEndsUpInTheExpensesTable()
    {
        var (post, data, dbf) = Host();
        await PersonAsync(dbf, "محمد هارون");
        var w = await SheetAsync(data, dbf, "هارون", 10m);
        await post.SyncAsync(w.Id);

        await using (var db = dbf.Create())
        {
            var t = await db.WaraqTransactions.FirstAsync(x => x.Name == "هارون");
            t.Type = WaraqTxnType.Expense;
            await db.SaveChangesAsync();
        }
        await post.SyncAsync(w.Id);

        Assert.Empty(await RowsAsync(dbf));
        await using var check = dbf.Create();
        var e = Assert.Single(await check.Expenses.AsNoTracking()
                                         .Where(x => x.SrcKey != null && x.SrcKey != "").ToListAsync());
        Assert.Equal(500m, e.Amount);
        Assert.Equal("هارون", e.Title);
    }
}
