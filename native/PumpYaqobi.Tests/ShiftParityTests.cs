using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ برابریِ «پارچه» با نسخهٔ وب ═════════════════════════════════════════════
///
/// این‌جا هیچ فرمولی دوباره نوشته نشده. ۴۰۰ ورودیِ تصادفی (ولی قطعی) در یک
/// کرومیومِ واقعی داخلِ خودِ ‎index.html‎ در کادرهای واقعیِ صفحه نشست، خودِ
/// ‎calcShift‎ و ‎saveShift‎ صدا زده شدند، و هرچه روی صفحه و در ‎DB‎ نشست در
/// ‎golden-shift.json‎ ذخیره شد. این آزمون همان ورودی‌ها را به C# می‌دهد.
///
/// سه چیزِ جدا سنجیده می‌شود:
///   ۱. ‎calcShift‎ — چهار خانهٔ خودکار، عددِ کادرِ «فایدهٔ فی‌لیتر»، رنگِ
///      «پول موجود» و نوشتهٔ زیرِ کادرِ فایده.
///   ۲. ‎saveShift‎ — رکوردی که واقعاً ذخیره می‌شود، و اینکه «پارچهٔ جدید»
///      پارچهٔ پیشین را دست نمی‌زند.
///   ۳. ‎syncShiftToWaraq‎ — ردیفِ پایه‌ای که در ورقِ همان تاریخ می‌نشیند.
/// </summary>
public class ShiftParityTests : IDisposable
{
    // ── دادهٔ طلایی ────────────────────────────────────────────────────────
    private sealed record CalcCase(
        string p, string fuel, double start, double end, double price, double debt,
        double buy, double box, double profitPerBox,
        string sale, string money, string profit, string avail,
        bool availNeg, string buyLbl,
        double saleN, double moneyN, double profitN, double availN);

    private sealed record Rec(
        string name, int pumpNum, double start, double end, double price,
        double profitPer, double buyPerLiter, double sale, double money,
        double debt, double available, double profit, string? savedAt);

    private sealed record Pump(
        int num, string fuel, string worker, double start, double end,
        double pricePerLiter, double debt, string srcKey);

    private sealed record SaveCase(
        string fuel, string type, string date, bool forceNew, int pumpNum,
        double start, double end, double price, double debt, double buy, double box,
        Rec? rec, int reportCount, int dieselCount, List<Pump> pumps,
        double availableFromShift, double shiftPrice, string workerName);

    private sealed record Golden(List<CalcCase> calc, List<SaveCase> saved, string today);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-shift.json");
        if (!File.Exists(p)) p = "golden-shift.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    private static void Close(double expected, decimal actual, string what)
    {
        var a = (double)actual;
        var tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Assert.True(Math.Abs(expected - a) <= tol, $"{what}: انتظار {expected} بود، {a} آمد");
    }

    /// <summary>نوشتهٔ نسخهٔ وب واحد هم دارد («۱٬۲۳۴ لیتر») — واحد کنار می‌رود.</summary>
    private static string Bare(string s) =>
        s.Replace("لیتر", "").Replace("افغانی", "").Trim();

    // ── ۱) calcShift ──────────────────────────────────────────────────────
    [Fact]
    public void CalcShift_MatchesTheHtml_NumbersAndText()
    {
        var svc = new ParchaService();
        foreach (var c in Load().calc)
        {
            var n = svc.CalcShift((decimal)c.start, (decimal)c.end, (decimal)c.price,
                                  (decimal)c.debt, (decimal)c.buy, (decimal)c.box);

            Close(c.profitPerBox, n.ProfitPerBox, $"فایدهٔ کادر [{c.p}]");
            Close(c.saleN, n.Sale > 0m ? n.Sale : 0m, $"فروش [{c.p}]");
            Close(c.moneyN, n.Money > 0m ? n.Money : 0m, $"جملهٔ موجودی [{c.p}]");
            Close(c.profitN, n.Profit > 0m ? n.Profit : 0m, $"فایده [{c.p}]");
            if (c.moneyN > 0) Close(c.availN, n.Available, $"پول موجود [{c.p}]");

            Assert.Equal(Bare(c.sale), Bare(n.SaleText));
            Assert.Equal(Bare(c.money), Bare(n.MoneyText));
            Assert.Equal(Bare(c.profit), Bare(n.ProfitText));
            Assert.Equal(Bare(c.avail), Bare(n.AvailableText));
            Assert.Equal(c.availNeg, n.AvailableIsNegative);
            Assert.Equal(c.buyLbl.Replace(" ", ""), n.BuyPerLabel.Replace(" ", ""));
        }
    }

    /// <summary>
    /// عددِ کادر گردشده است (‎toFixed(1)‎) ولی عددی که همان لحظه به‌عنوان
    /// «فایده» نشان داده می‌شود گرد نشده — دقیقاً مثلِ نسخهٔ وب.
    /// </summary>
    [Fact]
    public void ProfitPerBox_IsRoundedButLiveProfitIsNot()
    {
        var n = new ParchaService().CalcShift(0m, 100m, 60.55m, 0m, 20m, 0m);
        Assert.Equal(40.6m, n.ProfitPerBox);      // 40.55 → toFixed(1)
        Assert.Equal(40.55m, n.ProfitPer);
        Assert.Equal(4055m, n.Profit);            // نمایش، با عددِ گرد نشده
    }

    /// <summary>
    /// فیِ خرید که باشد، عددِ دستیِ کادر پس زده می‌شود — همان کاری که
    /// ‎calcShift‎ می‌کرد و ‎onProfitPerManual‎ جلویش را نمی‌گرفت.
    /// </summary>
    [Fact]
    public void ManualProfitPer_SurvivesOnlyWhenThereIsNoPurchasePrice()
    {
        var svc = new ParchaService();
        Assert.Equal(30m, svc.CalcShift(0, 100, 60, 0, 30, 7).ProfitPerBox);  // پس زده شد
        Assert.Equal(7m, svc.CalcShift(0, 100, 60, 0, 0, 7).ProfitPerBox);    // فیِ خرید ندارد
        Assert.Equal(7m, svc.CalcShift(0, 100, 0, 0, 30, 7).ProfitPerBox);    // فیِ فروش ندارد
    }

    // ── ۲ و ۳) saveShift و ورق ────────────────────────────────────────────
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-shift-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (ParchaDataService Parcha, PumpDbFactory Db) Host(string? today = null)
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var waraqCalc = new WaraqService();
        var settings = new SettingsService(dbf, perm);
        var sync = new ShiftWaraqSyncService(dbf, perm, waraqCalc, settings);
        return (new ParchaDataService(dbf, perm, trash, new ParchaService(), sync,
                                      today is null ? null : () => today), dbf);
    }

    [Fact]
    public async Task SaveShift_StoresTheSameRecordAndWaraqRowsAsTheHtml()
    {
        var g = Load();
        var (parcha, dbf) = Host(g.today);
        var golden = g.saved;

        foreach (var c in golden)
        {
            var fuel = c.fuel == "diesel" ? FuelType.Diesel : FuelType.Petrol;
            var kind = c.type == "night" ? ShiftKind.Night : ShiftKind.Day;

            var res = await parcha.SaveShiftFlowAsync(new ShiftSaveRequest(
                Fuel: fuel, Kind: kind, DateShamsi: c.date,
                Name: c.rec!.name, PumpNum: c.pumpNum,
                Start: (decimal)c.start, End: (decimal)c.end, Price: (decimal)c.price,
                Debt: (decimal)c.debt, BoxProfitPer: (decimal)c.box, AvailMan: 0m, Note: "",
                BuyPerLiter: (decimal)c.buy, ForceNew: c.forceNew));

            Assert.True(res.Ok, res.Error);
            var s = res.Shift!;

            Close(c.rec.start, s.Start, "شروع");
            Close(c.rec.end, s.End, "ختم");
            Close(c.rec.price, s.Price, "فی");
            Close(c.rec.profitPer, s.ProfitPer, "فایدهٔ فی‌لیتر");
            Close(c.rec.buyPerLiter, s.BuyPerLiter, "فیِ خرید");
            Close(c.rec.sale, s.Sale, "فروش");
            Close(c.rec.money, s.Money, "جملهٔ موجودی");
            Close(c.rec.debt, s.Debt, "قرض");
            Close(c.rec.available, s.Available, "پول موجود");
            Close(c.rec.profit, s.Profit, "فایده");
            Assert.Equal(c.rec.name, s.Name);
            Assert.Equal(c.pumpNum, s.PumpNum);
            Assert.Equal(c.date, s.SavedAt);

            // شمارِ پارچه‌ها هم باید مو‌به‌مو یکی باشد — این همان چیزی است که
            // «پارچهٔ تازه کِی ساخته می‌شود» را می‌سنجد، نه فقط عددهای داخلش.
            await using var db = dbf.Create();
            Assert.Equal(c.reportCount,
                await db.Reports.AsNoTracking().CountAsync(r => r.Fuel == FuelType.Petrol));
            Assert.Equal(c.dieselCount,
                await db.Reports.AsNoTracking().CountAsync(r => r.Fuel == FuelType.Diesel));
        }

        // ── ردیف‌های ورق: هر پارچه یک ردیفِ جدا با کلیدِ خودش ──────────────
        await using (var db = dbf.Create())
        {
            var pumps = await db.WaraqPumps.AsNoTracking().ToListAsync();
            Assert.All(pumps, p => Assert.False(string.IsNullOrEmpty(p.SrcKey)));
            Assert.Equal(pumps.Count, pumps.Select(p => (p.ShiftId, p.SrcKey)).Distinct().Count());

            // شمارِ ردیف‌های ورق = شمارِ پارچه‌های ذخیره‌شده
            var shifts = await db.ShiftDataSet.AsNoTracking().CountAsync();
            Assert.Equal(shifts, pumps.Count);
        }
    }

    /// <summary>
    /// خواستهٔ صریحِ صاحب ریپو: «پارچهٔ جدیدِ روز» نباید دادهٔ شب یا سوختِ
    /// دیگر را پاک کند. سه پارچه ثبت می‌شود و بعد پارچهٔ جدیدِ روز — هر سه
    /// رکوردِ پیشین باید سرِ جایشان باشند.
    /// </summary>
    [Fact]
    public async Task NewParcha_TouchesOnlyItsOwnShiftAndFuel()
    {
        var (parcha, dbf) = Host();

        async Task<ShiftSaveResult> Save(FuelType f, ShiftKind k, string name,
                                         decimal end, bool force = false) =>
            await parcha.SaveShiftFlowAsync(new ShiftSaveRequest(
                f, k, "1405/06/10", name, 1, 0m, end, 60m, 0m, 0m, 0m, "", 20m, force));

        var pd = await Save(FuelType.Petrol, ShiftKind.Day, "روزِ پطرول", 100m);
        var pn = await Save(FuelType.Petrol, ShiftKind.Night, "شبِ پطرول", 200m);
        var dd = await Save(FuelType.Diesel, ShiftKind.Day, "روزِ دیزل", 300m);
        var dn = await Save(FuelType.Diesel, ShiftKind.Night, "شبِ دیزل", 400m);
        Assert.True(pd.Ok && pn.Ok && dd.Ok && dn.Ok);

        // «پارچهٔ جدیدِ روزِ پطرول» → پارچهٔ تازه، ولی هیچ‌کدام از سه‌تای دیگر نرود
        var again = await Save(FuelType.Petrol, ShiftKind.Day, "روزِ پطرولِ دوم", 500m, force: true);
        Assert.True(again.Ok);
        Assert.NotEqual(pd.Report!.Id, again.Report!.Id);

        await using var db = dbf.Create();
        var names = await db.ShiftDataSet.AsNoTracking().Select(s => s.Name).ToListAsync();
        Assert.Contains("روزِ پطرول", names);
        Assert.Contains("شبِ پطرول", names);
        Assert.Contains("روزِ دیزل", names);
        Assert.Contains("شبِ دیزل", names);
        Assert.Contains("روزِ پطرولِ دوم", names);
        Assert.Equal(5, names.Count);

        // و در ورق هم پنج ردیفِ جدا با پنج کلیدِ جدا
        var keys = await db.WaraqPumps.AsNoTracking().Select(p => p.SrcKey).ToListAsync();
        Assert.Equal(5, keys.Distinct().Count());
    }

    /// <summary>
    /// ذخیرهٔ دوباره (ویرایش) با همان تاریخ و بی پرچم، ردیفِ دوتایی نمی‌سازد.
    /// </summary>
    [Fact]
    public async Task SavingTwice_UpdatesInsteadOfDuplicating()
    {
        var (parcha, dbf) = Host();
        for (var i = 0; i < 3; i++)
            await parcha.SaveShiftFlowAsync(new ShiftSaveRequest(
                FuelType.Petrol, ShiftKind.Day, "1405/06/11", "احمد", 2,
                0m, 100m + i, 60m, 0m, 0m, 0m, "", 20m, false));

        await using var db = dbf.Create();
        Assert.Equal(1, await db.ShiftDataSet.AsNoTracking().CountAsync());
        Assert.Equal(1, await db.WaraqPumps.AsNoTracking().CountAsync());
        Assert.Equal(102m, (await db.ShiftDataSet.AsNoTracking().SingleAsync()).End);
    }

    /// <summary>
    /// فروشِ ورق به گاوصندوق می‌رود و با ویرایش فقط همان ردیف به‌روز می‌شود.
    /// </summary>
    [Fact]
    public async Task WaraqSales_LandInTheSafeOnceAndAreUpdatedInPlace()
    {
        var (parcha, dbf) = Host();
        await parcha.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, "1405/06/12", "احمد", 1,
            0m, 100m, 60m, 0m, 0m, 0m, "", 20m, false));

        await using (var db = dbf.Create())
        {
            var row = await db.SafeEntries.AsNoTracking().SingleAsync();
            Assert.Equal(6000m, row.Amount);
            Assert.StartsWith("wq-sales-", row.SrcKey);
            Assert.Contains("1405/06/12", row.Title);
        }

        await parcha.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, "1405/06/12", "احمد", 1,
            0m, 200m, 60m, 0m, 0m, 0m, "", 20m, false));

        await using (var db = dbf.Create())
        {
            var row = await db.SafeEntries.AsNoTracking().SingleAsync();
            Assert.Equal(12000m, row.Amount);
        }
    }

    /// <summary>ذخیره پشتِ اجازه است — همان ‎currentRole === 'viewer'‎ی نسخهٔ وب.</summary>
    [Fact]
    public async Task SaveShift_IsBlockedForViewers()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();          // پیش‌فرض: بیننده
        var perm = new PermissionService(session);
        var svc = new ParchaDataService(dbf, perm, new TrashService(dbf, perm, session),
                                        new ParchaService());

        await Assert.ThrowsAnyAsync<Exception>(() => svc.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, "1405/06/10", "احمد", 1,
            0m, 100m, 60m, 0m, 0m, 0m, "", 20m, false)));
    }
}
