using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «قرض‌ها از جمله فروش کم نمی‌شود» — ۱۴۰۵/۰۷/۱۸ ═══════════════════════════
/// فروشِ ورق منهای قرض‌های همان ورق، در کادرِ خلاصه و در گاوصندوق؛ و ردیف‌های
/// قدیمیِ گاوصندوق یک بار با آپدیت درست می‌شوند. همه روی SQLiteِ واقعی.
/// </summary>
[Collection(OpLogCollection.Name)]
public class WaraqNetSalesTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-net-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private (PumpDbFactory Db, ParchaDataService Parcha, ShiftWaraqSyncService Sync) Make()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var sync = new ShiftWaraqSyncService(dbf, perm, new WaraqService(), new SettingsService(dbf, perm));
        return (dbf, new ParchaDataService(dbf, perm, trash, new ParchaService(), sync), sync);
    }

    private static async Task<decimal> SafeSales(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return (await db.SafeEntries.AsNoTracking()
                        .Where(e => e.SrcKey != null && e.SrcKey.StartsWith("wq-sales-"))
                        .Select(e => e.Amount).ToListAsync()).Sum();
    }

    private static async Task<long> AddDebt(PumpDbFactory dbf, decimal amount)
    {
        await using var db = dbf.Create();
        var sd = await db.WaraqShifts.FirstAsync(s => s.Kind == ShiftKind.Day);
        db.WaraqTransactions.Add(new WaraqTransaction
        {
            ShiftId = sd.Id, Name = "کریم", Amount = amount, AmountAuto = false,
            Type = WaraqTxnType.Debt, SortIndex = 9,
        });
        await db.SaveChangesAsync();
        return sd.WaraqId;
    }

    [Fact]
    public void JomleyeForush_MenhayeGharz_Ast_VaForusheSotunDastNakhord()
    {
        var sd = new WaraqShift
        {
            Pumps = { new WaraqPump { Start = 0m, End = 100m, PricePerLiter = 60m } },
            Transactions = { new WaraqTransaction { Name = "کریم", Amount = 2000m, AmountAuto = false, Type = WaraqTxnType.Debt } },
            FabricDebt = 500m,
        };
        var t = new WaraqService().ShiftTotals(sd);
        Assert.Equal(6000m, t.Sales);          // جمعِ ستون (لیتر × فی)
        Assert.Equal(2500m, t.Debt);
        Assert.Equal(3500m, t.Net);            // ⛔ منهای قرض‌ها
    }

    [Fact]
    public async Task Gavsanduq_ForusheMenhayeGharz_Migirad()
    {
        var h = Make();
        var r = await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(FuelType.Petrol, ShiftKind.Day,
            "1405/07/10", "کریم", 1, 0m, 100m, 60m, 0m, 0m, 0m, "", 20m, false));
        Assert.True(r.Ok, r.Error);
        Assert.Equal(6000m, await SafeSales(h.Db));

        var wid = await AddDebt(h.Db, 2000m);
        await h.Sync.ResyncSalesAsync(new[] { wid });
        Assert.Equal(4000m, await SafeSales(h.Db));
    }

    [Fact]
    public async Task RadifhayeGhadimi_YekBar_BaApdeit_DorostMishavand()
    {
        var h = Make();
        var r = await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(FuelType.Petrol, ShiftKind.Day,
            "1405/07/10", "کریم", 1, 0m, 100m, 60m, 0m, 0m, 0m, "", 20m, false));
        Assert.True(r.Ok, r.Error);
        await AddDebt(h.Db, 2000m);

        async Task Old(decimal v)
        {
            await using var db = h.Db.Create();
            var row = await db.SafeEntries.FirstAsync(e => e.SrcKey != null && e.SrcKey.StartsWith("wq-sales-"));
            row.Amount = v;
            await db.SaveChangesAsync();
        }

        await Old(6000m);                                  // همان‌که نسخهٔ پیشین رسانده بود
        //  ⛔ ردیفِ کهنه‌ای که ‎AmountAuto‎ ندارد — نباید نوشته شود (هزاران ‎UPDATE‎)
        await using (var db = h.Db.Create())
        {
            foreach (var t in db.WaraqTransactions) t.AmountAuto = null;
            await db.SaveChangesAsync();
        }
        long opsBefore;
        await using (var db = h.Db.Create()) opsBefore = await db.SyncOps.CountAsync(o => o.TableName != "SafeEntry");
        Assert.Equal(1, await h.Sync.FixOldSalesOnceAsync());
        await using (var db = h.Db.Create())
        {
            Assert.Equal(opsBefore, await db.SyncOps.CountAsync(o => o.TableName != "SafeEntry"));
            Assert.All(db.WaraqTransactions.ToList(), t => Assert.Null(t.AmountAuto));
        }
        Assert.Equal(4000m, await SafeSales(h.Db));

        await Old(6000m);                                  // بارِ دوم: هیچ کاری — فقط یک بار
        Assert.Equal(0, await h.Sync.FixOldSalesOnceAsync());
        Assert.Equal(6000m, await SafeSales(h.Db));
    }
}
