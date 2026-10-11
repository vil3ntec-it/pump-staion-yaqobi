using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ قابلیت‌های حرفه‌ای (۳.۱.۲۶۸ به بعد) ══════════════════════════════════════
/// </summary>
public class ProFeaturesTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-pro-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        return d!.FullName;
    }

    // ── ۹) سقفِ قرض — ذخیره می‌شود، و فقط هشدار است ─────────────────────────

    [Fact]
    public async Task SaghfeGharz_ZakhireMishavad_VaHesabeDigarDastNemikhorad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var svc = new DebtorService(dbf, perm, new TrashService(dbf, perm, session));

        long pid;
        await using (var db = dbf.Create())
        {
            var p = new Debtor { Name = "کریم", LegacyId = "pro9" };
            db.Debtors.Add(p);
            await db.SaveChangesAsync();
            db.DebtAccounts.Add(new DebtAccount { MainOfDebtorId = p.Id, Name = "کریم" });
            await db.SaveChangesAsync();
            pid = p.Id;
        }
        var sub = await svc.AddSubAccountAsync(pid, "موتر");
        sub.CreditLimit = 50000m;
        await svc.UpdateAccountAsync(sub);

        var full = (await svc.LoadFullAsync(pid))!;
        Assert.Equal(50000m, full.SubAccounts.Single().CreditLimit);
        Assert.Null(full.MainAccount.CreditLimit);   // سقفِ هر حساب مالِ همان است
    }

    [Fact]
    public void SaghfeGharz_FaghatHoshdar_HichRadifiRadNemishavad()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App/ViewModels/Sections/PersonViewModel.cs"));
        Assert.Contains("public bool OverLimit =>", src);
        Assert.Contains("Totals.All.Albaqi > lim", src);
        Assert.Contains("nameof(OverLimit), nameof(OverLimitText)", src);
        var xaml = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App/Views/Sections/PersonView.axaml"));
        Assert.Contains("{Binding CreditLimitText}", xaml);
        Assert.Contains("{Binding Current.OverLimitText}", xaml);
    }

    // ── ۵) پیش‌بینیِ تمام شدن ────────────────────────────────────────────

    [Theory]
    [InlineData(10000, 2000, 5, false)]
    [InlineData(5000, 2000, 2, true)]
    [InlineData(500, 2000, 0, true)]
    public void PishBini_RozhayeBas(int stock, int avg, int days, bool order)
    {
        var t = PumpYaqobi.App.ViewModels.Sections.StorageSectionViewModel.Forecast(stock, avg);
        Assert.Contains(days == 0 ? "کمتر از یک روز" : $"حدودِ {Shamsi.Money(days)} روز", t);
        Assert.Equal(order, t.Contains("وقتِ سفارش"));
    }

    [Fact]
    public void PishBini_BiDade_Khali() =>
        Assert.Equal("", PumpYaqobi.App.ViewModels.Sections.StorageSectionViewModel.Forecast(10000, 0));

    [Fact]
    public async Task MiyanginForosh_AzAkharin14RozeParchedar()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        await using (var db = dbf.Create())
        {
            for (var day = 1; day <= 20; day++)
                db.Reports.Add(new ParchaReport
                {
                    Fuel = FuelType.Petrol, DateKey = 14050700 + day,
                    DayShift = new ShiftData { Sale = day <= 6 ? 99999m : 1000m },
                    NightShift = new ShiftData { Sale = 500m },
                });
            await db.SaveChangesAsync();
        }
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var svc = new StorageDataService(dbf, perm, trash, new StorageService(), new SettingsService(dbf, perm), new CompanyDataService(dbf, perm, trash));
        // آخرین ۱۴ روز (۷ تا ۲۰): هر روز ۱۵۰۰ — روزهای کهنهٔ ۹۹۹۹۹ بیرون می‌مانند
        Assert.Equal(1500m, await svc.AvgDailySaleAsync(FuelType.Petrol));
        Assert.Equal(0m, await svc.AvgDailySaleAsync(FuelType.Diesel));
    }

    // ── ۴ و ۱) آبِ تهِ مخزن و روندِ کسر و اضافه ─────────────────────────────

    [Fact]
    public async Task AbeTaheMakhzan_ZakhireMishavad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var svc = new StorageDataService(dbf, perm, trash, new StorageService(), new SettingsService(dbf, perm), new CompanyDataService(dbf, perm, trash));
        await svc.SaveDipAsync(new TankDip { Fuel = FuelType.Diesel, DateShamsi = "1405/07/20", Measured = 9000m, Expected = 9100m, Water = 35m });
        var d = (await svc.DipsAsync(FuelType.Diesel)).Single();
        Assert.Equal(35m, d.Water);
        Assert.Equal(0m, d.BookAdjust);   // آب هیچ حسابی را عوض نمی‌کند
    }

    [Fact]
    public void RavandVaAb_DarSafhe()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App/Views/Sections/StorageSectionView.axaml"));
        Assert.Contains("{Binding DipTrend}", xaml);
        Assert.Contains("{Binding DipWater}", xaml);
        Assert.Contains("{Binding WaterText}", xaml);
        Assert.Contains("{Binding ForecastText}", xaml);
    }

    // ── ۱۱) شمارشِ نوت ───────────────────────────────────────────────────

    [Fact]
    public void ShomaresheNot_BastanVaBazkardan()
    {
        var W = typeof(PumpYaqobi.App.ViewModels.Sections.WaraqPageViewModel);
        var packed = PumpYaqobi.App.ViewModels.Sections.WaraqPageViewModel.PackCash(new[] { (1000, 3m), (500, 0m), (100, 7m) });
        Assert.Equal("1000:3,100:7", packed);
        var back = PumpYaqobi.App.ViewModels.Sections.WaraqPageViewModel.UnpackCash(packed);
        Assert.Equal(2, back.Count);
        Assert.Equal(3m, back[1000]);
        Assert.Equal(7m, back[100]);
        Assert.Empty(PumpYaqobi.App.ViewModels.Sections.WaraqPageViewModel.UnpackCash("خراب,1000:x,:3"));
        Assert.NotNull(W);
    }

    [Fact]
    public async Task ShomaresheNot_RoyeShift_ZakhireMishavad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        long sid;
        await using (var db = dbf.Create())
        {
            var w = new WaraqEntry { DateShamsi = "1405/07/20", DateKey = 14050720 };
            w.Shifts.Add(new WaraqShift { Kind = ShiftKind.Day });
            db.WaraqEntries.Add(w);
            await db.SaveChangesAsync();
            sid = w.Shifts[0].Id;
        }
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var svc = new WaraqDataService(dbf, perm, new TrashService(dbf, perm, session));
        await svc.SaveShiftAsync(new WaraqShift { Id = sid, CashCount = "1000:3" });
        await using (var db = dbf.Create())
            Assert.Equal("1000:3", db.WaraqShifts.Single(x => x.Id == sid).CashCount);
    }

    // ── ۸) فروشِ هر کارمند و هر پایه ─────────────────────────────────────

    [Fact]
    public void ForosheKarmandVaPaye()
    {
        var shift = new WaraqShift { WorkerName = "علی" };
        shift.Pumps.Add(new WaraqPump { Num = 1, Fuel = FuelType.Petrol, Start = 100m, End = 300m, PricePerLiter = 80m });
        shift.Pumps.Add(new WaraqPump { Num = 2, Fuel = FuelType.Diesel, Start = 0m, End = 120m, TestLiters = 20m, PricePerLiter = 70m, Worker = "رحیم" });
        var e = new WaraqEntry { Shifts = { shift } };
        var shift2 = new WaraqShift { WorkerName = "علی" };
        shift2.Pumps.Add(new WaraqPump { Num = 1, Fuel = FuelType.Petrol, Start = 300m, End = 350m, PricePerLiter = 80m });
        var e2 = new WaraqEntry { Shifts = { shift2 } };

        var (w, p) = WaraqService.SalesBy(new[] { e, e2 });
        var ali = w.Single(x => x.Name == "علی");
        Assert.Equal(250m, ali.Liters);
        Assert.Equal(20000m, ali.Money);
        Assert.Equal(2, ali.Count);
        var rahim = w.Single(x => x.Name == "رحیم");
        Assert.Equal(100m, rahim.Liters);   // آزمایشی فروش نیست
        Assert.Equal(7000m, rahim.Money);
        Assert.Equal(2, p.Count);
        Assert.Equal(250m, p.Single(x => x.Name.Contains("پطرول")).Liters);
    }

    // ── ۲) حجم در ۱۵ درجه ────────────────────────────────────────────────

    [Fact]
    public void HajmDar15Daraje()
    {
        Assert.Equal(10000m, TankDipService.At15(10000m, 15m, FuelType.Diesel));     // در ۱۵ درجه همان
        Assert.Equal(9748m, TankDipService.At15(10000m, 35m, FuelType.Petrol));      // گرما ⇒ کمتر
        Assert.True(TankDipService.At15(10000m, 0m, FuelType.Petrol) > 10000m);      // سرما ⇒ بیشتر
        Assert.True(TankDipService.At15(10000m, 35m, FuelType.Diesel) > 9748m);      // دیزل کمتر منبسط می‌شود
    }

    // ── ۱۰) پلاکِ موتر روی ردیفِ قرض ─────────────────────────────────────

    [Fact]
    public async Task PelakRoyeRadifeGharz()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        long rid;
        await using (var db = dbf.Create())
        {
            var p = new Debtor { Name = "شرکتِ آریا", LegacyId = "pro10" };
            db.Debtors.Add(p);
            await db.SaveChangesAsync();
            var a = new DebtAccount { MainOfDebtorId = p.Id, Name = "شرکتِ آریا" };
            db.DebtAccounts.Add(a);
            await db.SaveChangesAsync();
            var r = new DebtRow { FuelAccountId = a.Id, DateShamsi = "1405/07/20", Liters = 50m, Plate = "کابل ۴۵۶" };
            db.DebtRows.Add(r);
            await db.SaveChangesAsync();
            rid = r.Id;
        }
        await using (var db = dbf.Create())
            Assert.Equal("کابل ۴۵۶", db.DebtRows.Single(x => x.Id == rid).Plate);
        var xaml = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App/Views/Sections/PersonView.axaml"));
        Assert.Contains("Header=\"پلاک\" Binding=\"{Binding Plate}\"", xaml);
    }
}
