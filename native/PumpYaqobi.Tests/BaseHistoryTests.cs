using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پایهٔ کمتر، و تاریخچهٔ پایه‌ها ═══════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶):
///   «اگر پارچهٔ اول ۲۲۳۰۰۰ بود و چندین پارچه با عددهای بالاتر دادم و بعد یکی
///    پیدا شد که ۲۲۲۰۰۰ باشد… یک پیام بیاید بغلِ همان کادرِ شروع پایه و بگوید
///    این کمتر است و مانعی نباشد… و در تاریخچهٔ پارچه‌ها همهٔ شروع و ختم‌ها،
///    شب و روز، پشتِ سرِ هم با اسمِ کارمند و تاریخ و شماره و چندشنبه.»
/// </summary>
public class BaseHistoryTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-base-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (ParchaDataService Parcha, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var sync = new ShiftWaraqSyncService(dbf, perm, new WaraqService(), settings);
        return (new ParchaDataService(dbf, perm, trash, new ParchaService(), sync, () => "1405/07/01"), dbf);
    }

    private static Task<ShiftSaveResult> Save(ParchaDataService p, string date, string name,
                                              int num, decimal start, decimal end, bool force = true,
                                              bool low = false) =>
        p.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, date, name, num,
            start, end, 60m, 0m, 0m, 0m, "", 20m, force, low));

    /// <summary>
    /// «بزرگ‌ترین ختمِ ثبت‌شده» — و **فقط همان شمارهٔ پایه**، چون هر پایه
    /// شمارندهٔ خودش را دارد و مقایسهٔ پایهٔ ۱ با پایهٔ ۳ بی‌معناست.
    /// </summary>
    [Fact]
    public async Task LastBase_IsTheHighestEndOfThatPumpOnly()
    {
        var (p, _) = Host();
        await Save(p, "1405/07/01", "احمد", 1, 222000m, 223000m);
        await Save(p, "1405/07/02", "محمود", 1, 223000m, 224500m);
        await Save(p, "1405/07/02", "کریم", 3, 10m, 90m);

        Assert.Equal(224500m, await p.LastBaseAsync(FuelType.Petrol, 1));
        Assert.Equal(90m, await p.LastBaseAsync(FuelType.Petrol, 3));
        Assert.Equal(224500m, await p.LastBaseAsync(FuelType.Petrol, 0));   // «هر شماره‌ای»
        Assert.Equal(0m, await p.LastBaseAsync(FuelType.Diesel, 1));        // دیزل دفترِ خودش را دارد
    }

    /// <summary>
    /// تاریخچه: از قدیم به تازه، با نام و تاریخ و شماره و روزِ هفته — و
    /// ستونِ سرخِ «کمتر» دقیقاً روی همان پارچه‌ای که عقب رفته.
    /// </summary>
    [Fact]
    public async Task BaseHistory_IsOldestFirstAndFlagsTheOneThatWentBackwards()
    {
        var (p, _) = Host();
        await Save(p, "1405/07/01", "احمد", 1, 222000m, 223000m);
        await Save(p, "1405/07/02", "محمود", 1, 223000m, 224000m);
        await Save(p, "1405/07/03", "کریم", 1, 222000m, 222500m);   // ← عقب رفت

        var h = await p.BaseHistoryAsync(FuelType.Petrol);
        Assert.Equal(3, h.Count);
        Assert.Equal(new[] { "احمد", "محمود", "کریم" }, h.Select(x => x.Name).ToArray());
        Assert.All(h, r => Assert.Equal(1, r.PumpNum));
        Assert.All(h, r => Assert.NotEqual("", r.DayName));          // چندشنبه است
        Assert.All(h, r => Assert.Equal(ShiftKind.Day, r.Kind));

        Assert.False(h[0].Low);      // اولی مبنا است، عقب‌رفتنی ندارد
        Assert.False(h[1].Low);
        Assert.True(h[2].Low);       // ۲۲۲۰۰۰ < ۲۲۴۰۰۰
    }

    /// <summary>
    /// ⛔ نشان فقط **رنگ** است: ذخیره انجام می‌شود، عددها دست نمی‌خورند، و
    /// ردیفِ ورق همان ردیفِ همیشگی است — فقط ‎LowBase‎ش راست می‌شود.
    /// </summary>
    [Fact]
    public async Task TheLowBaseMarkNeverBlocksTheSaveAndChangesNoNumber()
    {
        var (p, dbf) = Host();
        var res = await Save(p, "1405/07/05", "احمد", 2, 100m, 500m, low: true);
        Assert.True(res.Ok);
        Assert.Equal(100m, res.Shift!.Start);
        Assert.Equal(500m, res.Shift.End);

        // ⚠️ هر خواندن در بلوکِ خودش بسته می‌شود: اتصالِ بازِ خواننده حینِ
        // نوشتنِ بعدی روی SQLite یعنی «database is locked»ِ گاه‌به‌گاه.
        await using (var db = dbf.Create())
        {
            var pump = await db.WaraqPumps.AsNoTracking().SingleAsync();
            Assert.True(pump.LowBase);
            Assert.Equal(100m, pump.Start);
            Assert.Equal(500m, pump.End);
        }

        // و ذخیرهٔ بعدیِ همان پارچه بی نشان، سرخی را برمی‌دارد
        await p.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, "1405/07/05", "احمد", 2,
            100m, 500m, 60m, 0m, 0m, 0m, "", 20m, false, false));

        await using (var db2 = dbf.Create())
            Assert.False((await db2.WaraqPumps.AsNoTracking().SingleAsync()).LowBase);
    }
}
