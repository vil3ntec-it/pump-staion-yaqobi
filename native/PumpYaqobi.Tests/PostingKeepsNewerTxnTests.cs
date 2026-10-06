using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ⛔ (۱۴۰۵/۰۷/۲۲، بازبینی) ثبتِ ورق به حساب‌ها روی نخِ دیگر ورق را می‌خوانَد، مبلغِ خودکار را
/// بازحساب می‌کند و همان را می‌نویسد. اگر کاربر در همان فاصله ردیف را ذخیره کرد، آن نوشتن نباید
/// «لیترِ کهنه × فی» را روی نوشتهٔ تازهٔ او بنشاند.
/// </summary>
public class PostingKeepsNewerTxnTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-keep-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private async Task<(PumpDbFactory dbf, WaraqDataService data, long txnId)> Setup()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var data = new WaraqDataService(dbf, perm, new TrashService(dbf, perm, session));
        var w = await data.OpenOrCreateAsync("1405/06/18", "پمپ");
        long id;
        await using (var db = dbf.Create())
        {
            var t = await db.WaraqTransactions.Where(x => x.Shift!.WaraqId == w.Id).OrderBy(x => x.SortIndex).FirstAsync();
            t.Name = "کریم"; t.Liters = 10m; t.Amount = 0m; t.AmountAuto = true;
            await db.SaveChangesAsync();
            id = t.Id;
        }
        return (dbf, data, id);
    }

    [Fact]
    public async Task UserSaveDuringPosting_IsNotOverwritten()
    {
        var (dbf, data, id) = await Setup();

        //  ثبت ورق را خوانده و مبلغ را بازحساب کرده (۱۰ × ۶۰)…
        await using var posting = dbf.Create();
        var stale = await posting.WaraqTransactions.FirstAsync(x => x.Id == id);
        stale.Amount = 600m;

        //  …و در همین فاصله کاربر «20» نوشت و ذخیره شد (همان نسخهٔ جدای صفحه)
        await using (var ui = dbf.Create())
        {
            var t = await ui.WaraqTransactions.AsNoTracking().FirstAsync(x => x.Id == id);
            t.Liters = 20m; t.Amount = 1200m;
            await data.SaveTxnAsync(t);
        }

        await using (var tx = await posting.Database.BeginTransactionAsync())
        {
            await WaraqPostingService.KeepNewerTxnsAsync(posting, default);
            await posting.SaveChangesAsync();
            await tx.CommitAsync();
        }

        await using var check = dbf.Create();
        var disk = await check.WaraqTransactions.AsNoTracking().FirstAsync(x => x.Id == id);
        Assert.Equal(20m, disk.Liters);
        Assert.Equal(1200m, disk.Amount);
    }

    [Fact]
    public async Task NoUserSave_NormalizedAmountIsStillWritten()
    {
        var (dbf, _, id) = await Setup();
        await using (var posting = dbf.Create())
        {
            var t = await posting.WaraqTransactions.FirstAsync(x => x.Id == id);
            t.Amount = 600m;
            await WaraqPostingService.KeepNewerTxnsAsync(posting, default);
            await posting.SaveChangesAsync();
        }
        await using var check = dbf.Create();
        Assert.Equal(600m, (await check.WaraqTransactions.AsNoTracking().FirstAsync(x => x.Id == id)).Amount);
    }
}
