using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ زنجیرهٔ پایه به ترتیبِ زمان (۱۴۰۵/۰۷/۱۹) ══════════════════════════════════
/// «اختلافِ شروع و ختم یک عددِ رندم نشان می‌دهد… پارچهٔ پاک‌شده… شروعِ بعدی باید
/// از آخرین ختمِ معتبرِ پیش از خودش باشد.» هر سناریو روی SQLiteِ واقعی.
/// </summary>
public class ParchaChainTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-chain-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private ParchaDataService Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var sync = new ShiftWaraqSyncService(dbf, perm, new WaraqService(), new SettingsService(dbf, perm));
        return new ParchaDataService(dbf, perm, trash, new ParchaService(), sync, () => "1405/07/01");
    }

    private static async Task<ShiftSaveResult> Save(ParchaDataService p, string date, ShiftKind kind,
                                                    int num, decimal start, decimal end, bool force = true)
    {
        var r = await p.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, kind, date, "احمد", num, start, end, 60m, 0m, 0m, 0m, "", 20m, force));
        Assert.True(r.Ok, r.Error);
        return r;
    }

    private static Task<decimal?> NewCard(ParchaDataService p, string date, int num, bool night) =>
        p.PrevEndAsync(FuelType.Petrol, num, PumpYaqobi.Application.Localization.Shamsi.Key(date),
                       long.MaxValue, night);

    [Fact]
    public async Task KarteTaze_BaKhatmeShifteGhabli_NaBozorgtarin()
    {
        var p = Host();
        await Save(p, "1405/07/01", ShiftKind.Day, 1, 1000m, 1500m);
        var d2 = await Save(p, "1405/07/02", ShiftKind.Day, 1, 1500m, 99000m);   // ختمِ اشتباه (یک صفرِ اضافه)
        await Save(p, "1405/07/03", ShiftKind.Day, 1, 2100m, 2600m);

        //  کارتِ تازهٔ ۰۴ ⇒ ختمِ ۰۳، نه بزرگ‌ترین (۹۹۰۰۰)
        Assert.Equal(2600m, await NewCard(p, "1405/07/04", 1, false));
        //  کارتِ تازه در تاریخِ گذشته (۰۲/…) ⇒ ختمِ پیش از خودش، نه ختمِ فردایش
        Assert.Equal(1500m, await NewCard(p, "1405/07/01", 1, true));
        //  پارچهٔ ذخیره‌شدهٔ ۰۲ که دوباره باز شده ⇒ با ختمِ خودش نه
        Assert.Equal(1500m, await p.PrevEndAsync(FuelType.Petrol, 1, d2.Report!.DateKey, d2.Report.Id,
                                                 false, d2.Shift!.Id));
    }

    [Fact]
    public async Task Shab_PasAzRuze_HamanParcha()
    {
        var p = Host();
        var day = await Save(p, "1405/07/05", ShiftKind.Day, 2, 100m, 250m);
        var night = await Save(p, "1405/07/05", ShiftKind.Night, 2, 250m, 400m, force: false);
        Assert.Equal(day.Report!.Id, night.Report!.Id);
        //  شبِ ذخیره‌شده ⇒ ختمِ روزِ همان پارچه
        Assert.Equal(250m, await p.PrevEndAsync(FuelType.Petrol, 2, night.Report.DateKey, night.Report.Id,
                                                true, night.Shift!.Id));
        //  روزِ همان پارچه ⇒ هیچ‌وقت با شبِ خودش سنجیده نمی‌شود
        Assert.Null(await p.PrevEndAsync(FuelType.Petrol, 2, day.Report.DateKey, day.Report.Id,
                                         false, day.Shift!.Id));
        //  پارچهٔ تازهٔ همان روز ⇒ پس از شب
        Assert.Equal(400m, await NewCard(p, "1405/07/05", 2, false));
    }

    [Fact]
    public async Task PayeyeDigar_VaBiShomare_HargezMarjaNist()
    {
        var p = Host();
        await Save(p, "1405/07/01", ShiftKind.Day, 1, 1000m, 1500m);
        await Save(p, "1405/07/01", ShiftKind.Day, 3, 50m, 90m);
        Assert.Equal(1500m, await NewCard(p, "1405/07/02", 1, false));
        Assert.Equal(90m, await NewCard(p, "1405/07/02", 3, false));
        Assert.Null(await NewCard(p, "1405/07/02", 2, false));       // پایهٔ ۲ هنوز هیچ
        Assert.Null(await NewCard(p, "1405/07/02", 0, false));       // بی شماره با پایه‌های شماره‌دار نه
        await Save(p, "1405/07/01", ShiftKind.Night, 0, 7m, 9m);
        Assert.Equal(9m, await NewCard(p, "1405/07/02", 0, false));
    }

    [Fact]
    public async Task PaarchayePakShode_DigarGhabli_Nist()
    {
        var p = Host();
        await Save(p, "1405/07/01", ShiftKind.Day, 1, 1000m, 1500m);
        var wrong = await Save(p, "1405/07/02", ShiftKind.Day, 1, 1500m, 100000m);
        Assert.Equal(100000m, await NewCard(p, "1405/07/03", 1, false));
        await p.DeleteAsync(wrong.Report!.Id);
        Assert.Equal(1500m, await NewCard(p, "1405/07/03", 1, false));

        //  و تاریخچه هم همان زنجیره: ۱۵۰۰ ⇒ ۱۵۰۰ سالم است
        await Save(p, "1405/07/03", ShiftKind.Day, 1, 1500m, 1800m);
        var h = await p.BaseHistoryAsync(FuelType.Petrol);
        Assert.Equal(2, h.Count);
        Assert.All(h, r => Assert.False(r.Low));
    }

    [Fact]
    public async Task Tarikhche_IkhtelafRaBaShifteGhabli_NaBozorgtarin()
    {
        var p = Host();
        await Save(p, "1405/07/01", ShiftKind.Day, 1, 1000m, 99000m);   // یک ختمِ اشتباه
        await Save(p, "1405/07/02", ShiftKind.Day, 1, 99000m, 99100m);
        await Save(p, "1405/07/03", ShiftKind.Day, 1, 99100m, 99200m);
        var h = await p.BaseHistoryAsync(FuelType.Petrol);
        Assert.All(h, r => Assert.False(r.Low));
    }
}
