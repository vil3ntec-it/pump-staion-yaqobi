using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ویرایشِ دوطرفه: ورق ⇄ پارچه ⇄ تاریخچه (۱۴۰۵/۰۷/۱۷) ═══════════════════
///
/// گزارشِ صاحب ریپو: «قرض رو اشتباه نوشتم و خاستم تغییر بدم؛ از ورق تغییر
/// می‌خوره اما توی گزارشِ پارچه‌ها و تاریخچه‌ها تغییر نمی‌کنه… و ویرایشِ
/// تاریخچه هم بزار.» هر سنجه روی SQLiteِ واقعی.
/// </summary>
public class ParchaWaraqEditTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-pwe-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private sealed record Host(PumpDbFactory Db, ParchaDataService Parcha, WaraqDataService Waraq,
                               HistoryService History);

    private Host Make()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var sync = new ShiftWaraqSyncService(dbf, perm, new WaraqService(), settings);
        var amanat = new AmanatDataService(dbf, perm, trash, settings);
        return new Host(dbf, new ParchaDataService(dbf, perm, trash, new ParchaService(), sync),
                        new WaraqDataService(dbf, perm, trash),
                        new HistoryService(dbf, perm, new ExchangeService(), new RetailService(),
                                           new CompanyService(), new AmanatService(), amanat, new WaraqService()));
    }

    /// <summary>پارچهٔ روزِ پطرول: ۰ ⇒ ۱۰۰ لیتر، فی ۶۰، قرض ۵۰۰.</summary>
    private static async Task<ShiftSaveResult> SaveAsync(Host h) =>
        await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, "1405/07/10", "کریم", 1, 0m, 100m, 60m,
            500m, 0m, 0m, "", 0m, ForceNew: true));

    private static async Task<WaraqPump> PumpOf(Host h, string key)
    {
        await using var db = h.Db.Create();
        return await db.WaraqPumps.AsNoTracking().SingleAsync(p => p.SrcKey == key);
    }

    private static async Task<ShiftData> ShiftOf(Host h, long reportId)
    {
        await using var db = h.Db.Create();
        var r = await db.Reports.AsNoTracking().Include(x => x.DayShift).SingleAsync(x => x.Id == reportId);
        return r.DayShift!;
    }

    [Fact]
    public async Task QarzeWaraq_DarParchaVaTarikhche_Mineshinad()
    {
        var h = Make();
        var res = await SaveAsync(h);
        Assert.True(res.Ok);

        var p = await PumpOf(h, res.SrcKey!);
        Assert.Equal(500m, p.Debt);
        p.Debt = 800m;
        p.End = 120m;
        await h.Waraq.SavePumpAsync(p);

        var s = await ShiftOf(h, res.Report!.Id);
        Assert.Equal(800m, s.Debt);
        Assert.Equal(120m, s.End);
        //  ⛔ عددهای حساب‌شده با همان CalcShift: ۱۲۰ لیتر × ۶۰ − ۸۰۰
        Assert.Equal(120m, s.Sale);
        Assert.Equal(7200m, s.Money);
        Assert.Equal(6400m, s.Available);
        Assert.Equal("کریم", s.Name);

        var row = (await h.History.FeedAsync("shift")).Single();
        Assert.Equal(800m, row.ShiftRef!.Debt);
        Assert.Equal(7200m, row.Amount);
    }

    [Fact]
    public async Task TarikhcheYeParcha_Virayesh_DarWaraqVaGavsanduq_Mineshinad()
    {
        var h = Make();
        var res = await SaveAsync(h);

        var edit = await h.Parcha.EditShiftAsync(res.Report!.Id, ShiftKind.Day, "کریم", 1,
                                                 0m, 150m, 60m, 900m);
        Assert.True(edit.Ok);
        Assert.Single(edit.WaraqIds);

        var p = await PumpOf(h, res.SrcKey!);
        Assert.Equal(150m, p.End);
        Assert.Equal(900m, p.Debt);

        await using var db = h.Db.Create();
        var safe = await db.SafeEntries.AsNoTracking().SingleAsync(e => e.SrcKey!.StartsWith("wq-sales-"));
        Assert.Equal(9000m, safe.Amount);   // ۱۵۰ × ۶۰
    }

    [Fact]
    public async Task Virayesh_KhatmeKamtarAzShoro_HichChiziNemineviseh()
    {
        var h = Make();
        var res = await SaveAsync(h);
        var edit = await h.Parcha.EditShiftAsync(res.Report!.Id, ShiftKind.Day, "کریم", 1,
                                                 200m, 100m, 60m, 500m);
        Assert.False(edit.Ok);
        var s = await ShiftOf(h, res.Report.Id);
        Assert.Equal(0m, s.Start);
        Assert.Equal(100m, s.End);
    }

    [Fact]
    public async Task NameKhali_NameParcha_RaPakNemikonad_VaPayeyeDasti_BiAsar()
    {
        var h = Make();
        var res = await SaveAsync(h);
        var p = await PumpOf(h, res.SrcKey!);
        p.Worker = "";
        await h.Waraq.SavePumpAsync(p);
        Assert.Equal("کریم", (await ShiftOf(h, res.Report!.Id)).Name);

        //  پایهٔ دستی (بی SrcKey) به هیچ پارچه‌ای نمی‌رسد
        var manual = new WaraqPump { ShiftId = p.ShiftId, Num = 9, Start = 0m, End = 50m, Debt = 1m };
        await h.Waraq.SavePumpAsync(manual);
        var s = await ShiftOf(h, res.Report.Id);
        Assert.Equal(500m, s.Debt);
        Assert.Equal(100m, s.End);
    }

    [Fact]
    public void Kelid_FaghatKelideParcha()
    {
        Assert.True(ShiftWaraqSyncService.TryParseKey("p-12-day", out var id, out var k));   // کلیدِ کهنه هنوز خوانده می‌شود
        Assert.Equal("12", id); Assert.Equal(ShiftKind.Day, k);
        Assert.True(ShiftWaraqSyncService.TryParseKey("p-u01HXYZ-day", out var uid, out _));    // ⛔ شورا ب۲
        Assert.Equal("u01HXYZ", uid);
        Assert.True(ShiftWaraqSyncService.TryParseKey("d-3-night", out _, out var k2));
        Assert.Equal(ShiftKind.Night, k2);
        Assert.False(ShiftWaraqSyncService.TryParseKey("p-live-day", out _, out _));
        Assert.False(ShiftWaraqSyncService.TryParseKey(null, out _, out _));
        Assert.False(ShiftWaraqSyncService.TryParseKey("wq-sales-1-day", out _, out _));
    }
}
