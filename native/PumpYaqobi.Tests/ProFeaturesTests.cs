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
}
